#!/usr/bin/env python3
"""Reconciliation sweep for the Doggiehood issue pipeline (issue #246).

Detects issues that have silently drifted out of the pipeline's label state
machine and classifies each drift as either an **auto-fix** (safe, unambiguous
label move applied by the gatekeeper routine) or a **flag** (surfaced in the
dashboard's "⚠️ Reconcile" section for Derek to act on). See
`docs/engineering/issue-pipeline.md`.

Two responsibilities, cleanly split so the detection is testable without
network (same pattern as ``select_queue.py`` / ``render_dashboard.py``):

  * ``process(state) -> findings`` — PURE. JSON state in, JSON findings out; no
    GitHub I/O. This is the single home of the detection rules and the only
    thing the unit tests drive.
  * ``fetch_state(repo, token)`` — queries the GitHub REST API (stdlib urllib,
    no third-party deps) and assembles the state dict. Not unit-tested.

Done-ness — closing keywords only (CLAUDE.md rule #10, #277): an issue's work is
judged "on main" ONLY from a **closing-keyword** reference
(``Closes``/``Fixes``/``Resolves #N`` and their tense/case variants) in a merged
commit *body* reachable from main, or from its deliverables existing at HEAD. A
bare ``#N`` / ``Refs #N`` / ``Part of #N`` / ``Relates to #N`` merely links and
does **not** mark an issue done. Done-ness is also **never** taken from a
PR/commit *title* (avoiding the bundled-squash blind spot, 2026-07-23 comment on
#246): the nightly builder squash-merges several issues under one lead PR title,
so a title-only match keeps missing bundled squashes.

Input schema (stdin):
  {
    "issues": [
      {"number": 109, "state": "open", "labels": ["in-progress"],
       "milestone": "v0.4", "is_epic": false, "is_dashboard": false,
       "has_open_pr": false, "prose_deps": [178],
       "has_analysis_comment": false, "handback_label_adds": 0}
    ],
    "merged_commit_body_refs": [56, 54, 189, 222],
    "deliverables_present": {"58": true}
  }

Output schema (stdout):
  {
    "strip_labels":            [{"number": 211, "labels": ["in-progress"]}],
    "requeue":                 [{"number": 109, "from": "in-progress",
                                "to": "ready-for-work"}],
    "requeue_triage":          [{"number": 569, "from": "pending-approval",
                                "to": "ai-triage"}],
    "flag_done":               [{"number": 56, "reason": "..."}],
    "flag_orphaned_ready":     [{"number": 300}],
    "flag_prose_dep":          [{"number": 178, "refs": [109]}],
    "flag_orphaned_analysis":  [{"number": 570}],
    "flag_stuck_triage":       [{"number": 684,
                                "handback_state": "needs-clarification",
                                "handbacks": 3}]
  }

``strip_labels``, ``requeue`` and ``requeue_triage`` are the auto-fixes
(applied by the gatekeeper); the five ``flag_*`` lists are surfaced read-only
on the dashboard. Every list is sorted by issue number.

``requeue_triage`` / ``flag_orphaned_analysis`` catch the non-atomic reactive-
triage hand-off (#582): the hand-off posts the analysis comment and sets the
hand-back state label as two separate writes, so a partial failure can leave a
state label with no analysis (``requeue_triage`` re-queues it to ``ai-triage``)
or an analysis with no state label (``flag_orphaned_analysis`` flags it — the
intended state is ambiguous). ``has_analysis_comment`` per issue drives both.

``flag_stuck_triage`` (#710) is the BOUND on ``requeue_triage``: past
``MAX_UNRECOGNIZED_HANDBACKS`` hand-backs with no analysis the sweep can see,
re-queuing has demonstrably not healed anything, so the auto-fix stops and the
issue is flagged instead. ``handback_label_adds`` per issue drives it.
"""

import json
import os
import re
import sys
import urllib.error
import urllib.request

REPO_DEFAULT = "derekwinters/lucas-doggiehood"
API = "https://api.github.com"
DASHBOARD_ISSUE = 193

# The pipeline-state labels, in canonical flow order. A closed issue must carry
# none of these; an open issue carries exactly one at a time.
PIPELINE_STATE_LABELS = [
    "ai-triage",
    "pending-approval",
    "needs-clarification",
    "ready-for-work",
    "in-progress",
]

FLAG_DONE_REASON = "work landed on main (merged commit body / deliverables) but issue still open"

# The two hand-back state labels a triage run sets on Derek (`triage-issue/
# SKILL.md`). The non-atomic-hand-off rules (#582) turn on whether one of these
# is present, independent of `ai-triage` (the pre-triage state) and the
# post-approval `ready-for-work`/`in-progress` states.
TRIAGE_HANDBACK_LABELS = ["pending-approval", "needs-clarification"]

# The structural stop on `requeue_triage` (issue #710). `requeue_triage` is an
# unattended auto-fix, so a `has_analysis_signature` that cannot see an issue's
# analysis re-fires triage on that issue every cron sweep, forever — twice now
# (#100/#643 → #654, then #683/#684 → #710), each time one marker-phrasing
# variant later. Widening the recognizer fixes the known variant; this bound is
# what stops the NEXT one from churning. Once an issue has been handed back this
# many times WITHOUT the sweep ever recognizing an analysis, the auto-fix stops
# and `flag_stuck_triage` surfaces it on the dashboard instead.
#
# **Invariant — the sweep never re-queues one issue unboundedly.** A recognizer
# gap must degrade to a flag a human sees, never to unattended churn.
#
# N = 3 is the flap count actually observed on #684 before #710 was filed — a
# real, already-seen threshold rather than a guessed one.
MAX_UNRECOGNIZED_HANDBACKS = 3

# A triage-authored analysis comment identifies itself with one of the two
# hand-back shapes `triage-issue/SKILL.md` defines: a `## Build checklist`
# heading (the bug / spec-feature / `/propose` routes) or the `❓ Needs from
# Derek/Lucas:` marker (the needs-clarification route). Recognized the same way
# `_closing_refs_in` recognizes closing keywords — a fixed textual signature,
# not a semantic read. Used by `fetch_state` to populate `has_analysis_comment`.
#
# The marker match tolerates Markdown EMPHASIS runs (`*`/`_`) around the marker
# text (issue #654): triage writes the callout bolded — `❓ **Needs from
# Derek/Lucas:**` — and a literal-substring check missed every one of those,
# because the `**` sits between `❓ ` and `Needs`. Since the ask route never
# emits a `## Build checklist` heading, that marker is the comment's ONLY
# signature, so the miss made `requeue_triage` re-fire triage on the same issue
# every cron sweep, forever (#100, #643). Only emphasis characters and
# horizontal whitespace may sit in the gaps — never arbitrary prose — and the
# `❓` anchor stays required, so a bare mention of the phrase is still not a
# signature.
#
# The marker is ALSO matched as a Markdown HEADING with no trailing colon
# (issue #710) — `## ❓ Needs from Derek/Lucas` — which is how triage wrote it on
# #683 and #684, and which the colon-requiring form above missed exactly the way
# #654's literal-substring form missed the bolded one. A heading is a structural
# element, so no colon is needed to keep it unambiguous; the `❓` anchor is still
# required and the heading must be the whole line, so a prose mention (or a
# heading merely naming the phrase) is still not a signature.
_ANALYSIS_HEADING_RE = re.compile(r"(?im)^[ \t]*#{1,6}[ \t]+build checklist[ \t]*$")
_EMPHASIS = r"[ \t]*[*_]*[ \t]*"
_NEEDS_CLARIFICATION_RE = re.compile(
    r"❓" + _EMPHASIS + r"Needs from Derek/Lucas" + _EMPHASIS + r":")
_NEEDS_CLARIFICATION_HEADING_RE = re.compile(
    r"(?im)^[ \t]*#{1,6}" + _EMPHASIS + r"❓" + _EMPHASIS
    + r"Needs from Derek/Lucas" + _EMPHASIS + r":?[ \t]*$")


def has_analysis_signature(text):
    """True iff ``text`` carries a triage-authored analysis signature.

    The signature is a ``## Build checklist`` heading (any heading level,
    case-insensitive) OR the ``❓ Needs from Derek/Lucas`` marker — the two
    hand-back comment shapes ``triage-issue/SKILL.md`` produces. The marker is
    recognized in both shapes triage actually writes:

    * **inline**, with a required colon and tolerant of Markdown emphasis around
      the text (issue #654) — ``❓ **Needs from Derek/Lucas:**`` and its
      italic/underscore variants, colon inside or outside the emphasis run;
    * **as a heading** of any level, with the colon optional (issue #710) —
      ``## ❓ Needs from Derek/Lucas`` — since the heading itself is what makes
      the line unambiguous.

    A plain comment that merely mentions the word "checklist" in prose does not
    match (the heading form is required), and neither does the marker phrase
    without its ``❓`` anchor, in prose or in a heading.
    """
    text = text or ""
    if _NEEDS_CLARIFICATION_RE.search(text):
        return True
    if _NEEDS_CLARIFICATION_HEADING_RE.search(text):
        return True
    return bool(_ANALYSIS_HEADING_RE.search(text))


def count_handback_label_adds(events):
    """How many times ``events`` hands the issue back to Derek.

    Counts the ``labeled`` timeline events that add a hand-back state label
    (``pending-approval`` / ``needs-clarification``) — one per completed triage
    run. Pure events-in / int-out; the paginated timeline read that feeds it
    lives in ``fetch_state``. Malformed entries are ignored.

    Hand-back ADDS are counted rather than ``ai-triage`` re-adds deliberately
    (issue #710): ``ai-triage`` is also added by ``/admit``, ``/revise``,
    ``/redo`` and the blocker-revisit sweep, and a single label ``PUT`` emits its
    ``labeled``/``unlabeled`` events with identical timestamps in no guaranteed
    order, so a hand-back → ``ai-triage`` *pairing* cannot be reconstructed
    reliably. The hand-back add needs no pairing and no author attribution.
    """
    count = 0
    for e in events or []:
        if not isinstance(e, dict) or e.get("event") != "labeled":
            continue
        label = e.get("label") or {}
        if label.get("name") in TRIAGE_HANDBACK_LABELS:
            count += 1
    return count


def _is_done(number, body_refs, deliverables):
    if number in body_refs:
        return True
    # deliverables_present keys may arrive as JSON strings or ints.
    return bool(deliverables.get(number, deliverables.get(str(number), False)))


def process(data, events_only=False):
    """Classify drift from ``data`` into auto-fixes and flags.

    ``events_only`` (issue #319) gates the ``requeue`` auto-fix to the cron
    backstop ONLY — pass ``True`` from the event-triggered sweep
    (``issues: [closed, labeled]`` / ``pull_request: [closed]``) and it is
    omitted entirely; ``strip_labels`` and every ``flag_*`` are unaffected in
    either mode. ``requeue_triage`` (#582) is gated the same way and for the
    same class of reason: on a ``labeled`` event the just-set ``pending-approval``
    can momentarily precede the analysis comment's visibility, so requeuing it
    on the event path would churn a healthy triage; the cron backstop heals the
    genuine #569-shape stall (which has no triggering event anyway). This exists
    because ``pull_request: [closed]`` fires before
    GitHub finishes auto-closing a ``Closes #N`` issue and before the merge is
    reliably visible on ``main`` — in that instant a just-merged `in-progress`
    issue can transiently look like a stalled one, and requeuing it right then
    would re-arm the #109 re-pick loop. A genuine stall has no triggering
    event anyway, so nothing is lost by leaving it for cron.
    """
    body_refs = set(data.get("merged_commit_body_refs", []))
    deliverables = data.get("deliverables_present", {}) or {}

    strip_labels = []
    requeue = []
    requeue_triage = []
    flag_done = []
    flag_orphaned_ready = []
    flag_prose_dep = []
    flag_orphaned_analysis = []
    flag_stuck_triage = []

    for issue in data.get("issues", []):
        # Excluded throughout the pipeline: epics, the dashboard issue, parked.
        labels = issue.get("labels", [])
        if issue.get("is_epic") or issue.get("is_dashboard"):
            continue
        if "parked" in labels:
            continue

        number = issue["number"]
        state = issue.get("state", "open")
        done = _is_done(number, body_refs, deliverables)

        if state == "closed":
            # Mirror of merged-but-open: a closed issue must not keep lying with
            # a pipeline-state label. Strip only those, in canonical order.
            stale = [l for l in PIPELINE_STATE_LABELS if l in labels]
            if stale:
                strip_labels.append({"number": number, "labels": stale})
            continue

        # --- open issues ---------------------------------------------------
        if done:
            # Merged-but-open (incl. bundled squash): flag for Derek to close.
            # The classification split — a done issue is NEVER requeued, which
            # is what stops the #109 re-pick loop.
            flag_done.append({"number": number, "reason": FLAG_DONE_REASON})
        elif ("in-progress" in labels and not issue.get("has_open_pr")
              and not events_only):
            # Stalled: picked up by a nightly build that dropped its commits,
            # no open PR, not on main -> return to the queue so it retries.
            # Cron-only (events_only=False) — see the events_only docstring
            # above for the auto-close race this avoids.
            requeue.append({"number": number, "from": "in-progress",
                            "to": "ready-for-work"})

        # --- non-atomic triage hand-off drift (issue #582) -----------------
        # The reactive-triage hand-off does two non-transactional writes: post
        # the analysis comment and set the hand-back state label (removing
        # `ai-triage`). A partial failure leaves one of two drift shapes, and
        # neither self-heals under the rules above.
        has_analysis = issue.get("has_analysis_comment", False)
        handback = [l for l in TRIAGE_HANDBACK_LABELS if l in labels]
        handbacks = issue.get("handback_label_adds", 0) or 0
        if handback and not has_analysis and not events_only:
            # Rule (a) — the #569 shape: a hand-back state label is set but no
            # analysis comment was ever posted, so there is no plan for Derek to
            # `/approve`. Auto-fix: strip the state label, re-add `ai-triage`,
            # so the issue re-enters the triage queue and actually gets a plan.
            # Cron-only, like `requeue`: on the event path a just-set
            # `pending-approval` can momentarily precede the analysis comment's
            # visibility, and requeuing it there would churn a healthy triage.
            #
            # ...BOUNDED (#710). Past `MAX_UNRECOGNIZED_HANDBACKS` hand-backs
            # with no analysis the sweep can see, the auto-fix has demonstrably
            # not healed anything — re-firing it again only reposts another
            # near-duplicate comment. Stop and FLAG instead, leaving the issue's
            # current label alone, so the cause (almost always a recognizer that
            # cannot see the hand-back's marker) is visible on the dashboard
            # rather than churning silently. The count degrades to 0 when the
            # timeline read fails, which keeps the healing auto-fix as the
            # default.
            if handbacks >= MAX_UNRECOGNIZED_HANDBACKS:
                flag_stuck_triage.append({"number": number,
                                          "handback_state": handback[0],
                                          "handbacks": handbacks})
            else:
                requeue_triage.append({"number": number, "from": handback[0],
                                       "to": "ai-triage"})
        if has_analysis and not any(l in labels for l in PIPELINE_STATE_LABELS):
            # Rule (b) — the residual #570 shape: a full analysis comment exists
            # but no pipeline-state label at all, so the issue is invisible to
            # the approval queue and the dashboard. FLAG, not auto-fix — which
            # hand-back state was intended (`pending-approval` vs
            # `needs-clarification`) is ambiguous from the comment shape alone.
            flag_orphaned_analysis.append({"number": number})

        # Stretch flags (independent of the above).
        if "ready-for-work" in labels and not issue.get("milestone"):
            flag_orphaned_ready.append({"number": number})
        prose = issue.get("prose_deps") or []
        if prose:
            flag_prose_dep.append({"number": number, "refs": sorted(prose)})

    by_num = lambda f: f["number"]
    return {
        "strip_labels": sorted(strip_labels, key=by_num),
        "requeue": sorted(requeue, key=by_num),
        "requeue_triage": sorted(requeue_triage, key=by_num),
        "flag_done": sorted(flag_done, key=by_num),
        "flag_orphaned_ready": sorted(flag_orphaned_ready, key=by_num),
        "flag_prose_dep": sorted(flag_prose_dep, key=by_num),
        "flag_orphaned_analysis": sorted(flag_orphaned_analysis, key=by_num),
        "flag_stuck_triage": sorted(flag_stuck_triage, key=by_num),
    }


# --------------------------------------------------------------------------
# Live-state fetch (GitHub REST API via stdlib urllib). Not exercised by the
# unit tests, which drive process() directly with a fixture. Mirrors
# render_dashboard.fetch_state's shape.
# --------------------------------------------------------------------------

def _api_get(path, token):
    req = urllib.request.Request(API + path)
    req.add_header("Authorization", "Bearer %s" % token)
    req.add_header("Accept", "application/vnd.github+json")
    req.add_header("User-Agent", "doggiehood-reconcile")
    with urllib.request.urlopen(req) as resp:
        return json.load(resp)


def _paginate(path, token):
    items, page = [], 1
    sep = "&" if "?" in path else "?"
    while True:
        batch = _api_get("%s%sper_page=100&page=%d" % (path, sep, page), token)
        if not batch:
            break
        items.extend(batch)
        if len(batch) < 100:
            break
        page += 1
    return items


# A GitHub closing keyword (any tense) immediately preceding ``#N``. Per
# CLAUDE.md rule #10, ONLY these resolve an issue — a bare ``#N`` / ``Refs #N`` /
# ``Part of #N`` / ``Relates to #N`` merely links and must NOT mark work done.
# ``\b`` keeps ``prefix #5`` from tripping ``fix``; ``[:\s]+`` allows the
# ``Closes #N`` and ``Closes: #N`` separators GitHub accepts.
_CLOSING_REF_RE = re.compile(
    r"\b(?:close[sd]?|fix(?:e[sd])?|resolve[sd]?)[:\s]+#(\d+)",
    re.IGNORECASE,
)


def _closing_refs_in(text):
    """Issue numbers a closing keyword resolves in ``text``.

    Matches ``close|closes|closed|fix|fixes|fixed|resolve|resolves|resolved``
    immediately before ``#N`` (case-insensitive). A bare ``#N``, ``Refs #N``,
    ``Part of #N``, or ``Relates to #N`` returns nothing — those only link.
    """
    return {int(n) for n in _CLOSING_REF_RE.findall(text or "")}


# --------------------------------------------------------------------------
# Prose-only dependency detection (issue #248). A dependency must be recorded
# as a STRUCTURED line — `Blocked by: #N` (hard gate) or `Depends on: #N` (soft
# ordering) — or a native GitHub relationship; never as prose alone, which the
# nightly builder (`select_queue.py`) and dashboard cannot see. This flags the
# drift: a body mentioning `depends on #N` / `blocked by #N` in a sentence with
# no matching structured line for that number. Pure text-in / numbers-out, same
# shape as `_closing_refs_in`; wired into `fetch_state` to populate `prose_deps`.
# --------------------------------------------------------------------------

# A dependency PHRASE anywhere in a line. `\b` stops `unblocked by` from
# matching. This alone does not distinguish prose from structured — see below.
_DEP_PHRASE_RE = re.compile(r"(?i)\b(?:blocked by|depends on)\b")

# A STRUCTURED dependency line: the keyword, then a REQUIRED colon, then `#N`,
# at line start (optionally behind a `-`/`*` list marker). The colon is what
# makes the line canonical/structured — it is the form select_queue.py and the
# dashboard parse. Everything else that merely mentions the phrase is prose.
_STRUCTURED_DEP_LINE_RE = re.compile(
    r"(?i)^[ \t]*(?:[-*][ \t]+)?(?:blocked by|depends on)[ \t]*:[ \t]*#\d+"
)

_HASH_NUM_RE = re.compile(r"#(\d+)")


# --------------------------------------------------------------------------
# Native hard-blocker source + canonical merge (issue #321). GitHub's native
# issue-dependency relationships cover the HARD-blocker form only (`Blocked by:`
# semantics); there is NO native equivalent for the soft-ordering `Depends on:`
# form (#197), which stays text-line-only. Every deterministic reader unions the
# native `blocked_by` set with the text-line parse through `merge_blockers`, so
# the blocker graph the nightly builder, reconcile, the dashboard, and the #212
# milestone-order gate consume is identical everywhere — one canonical rule, no
# duplicated parsing. Migration-safe: the text line keeps being read throughout.
# --------------------------------------------------------------------------

def merge_blockers(text_blockers, native_blockers=()):
    """Canonical hard-blocker merge: text-line `Blocked by:` ∪ native.

    Returns the union of the structured ``Blocked by: #N`` numbers and the
    native GitHub issue-dependency ``blocked_by`` numbers, coerced to ``int``,
    de-duplicated and sorted ascending. ``native_blockers`` defaults to empty so
    a caller that only has the text line still works during migration.
    """
    merged = {int(n) for n in text_blockers}
    merged |= {int(n) for n in native_blockers}
    return sorted(merged)


def native_blocked_by(repo, number, token):
    """Set of issue **numbers** ``#number`` is natively "blocked by".

    Reads the GitHub issue-dependencies REST API
    (``GET /repos/{repo}/issues/{n}/dependencies/blocked_by``), which returns
    full issue objects — so each blocker's identity is its ``number`` field.
    (The write side, ``POST …/blocked_by``, takes a numeric ``issue_id`` instead
    — a read/write asymmetry this reader deliberately does not conflate.)

    I/O only, not exercised by the unit tests (same split as the rest of this
    fetch layer). Any HTTP error — e.g. the dependencies API being unavailable —
    degrades safely to an empty set so the sweep falls back to the text line
    rather than crashing (migration-safe).
    """
    try:
        items = _paginate(
            "/repos/%s/issues/%d/dependencies/blocked_by" % (repo, number),
            token)
    except urllib.error.HTTPError:
        return set()
    return {i["number"] for i in items if isinstance(i, dict) and "number" in i}


def _issue_has_analysis_comment(repo, number, token):
    """True iff issue ``#number`` carries a triage-authored analysis comment.

    Pages the issue's comments (``GET /repos/{repo}/issues/{n}/comments``, the
    same paginated fetch pattern the rest of this layer uses) and tests each
    body against ``has_analysis_signature``. I/O only, not exercised by the unit
    tests (same split as the rest of this fetch layer — the signature match
    itself is unit-tested via ``has_analysis_signature``). Any HTTP error
    degrades safely to ``False`` so the sweep does not crash on a comment fetch.
    """
    try:
        comments = _paginate(
            "/repos/%s/issues/%d/comments" % (repo, number), token)
    except urllib.error.HTTPError:
        return False
    return any(has_analysis_signature(c.get("body")) for c in comments
               if isinstance(c, dict))


def _issue_handback_label_adds(repo, number, token):
    """How many times issue ``#number`` has been handed back to Derek.

    Pages the issue's event timeline (``GET /repos/{repo}/issues/{n}/timeline``)
    and counts its hand-back label adds via the pure
    ``count_handback_label_adds``. I/O only, not exercised by the unit tests
    (same split as the rest of this fetch layer). Any HTTP error degrades safely
    to ``0`` — the same direction as ``_issue_has_analysis_comment``'s ``False``:
    an unreadable timeline must never let the ``flag_stuck_triage`` guard
    suppress the healing ``requeue_triage`` auto-fix.
    """
    try:
        events = _paginate(
            "/repos/%s/issues/%d/timeline" % (repo, number), token)
    except urllib.error.HTTPError:
        return 0
    return count_handback_label_adds(events)


def prose_deps_in(body, native_refs=()):
    """Issue numbers referenced as a dependency **only in prose** in ``body``.

    A number is returned when it is named on a line carrying a dependency
    phrase (``depends on`` / ``blocked by``, case-insensitive) but is **not**
    cleared by a structured ``Blocked by: #N`` / ``Depends on: #N`` line (the
    canonical, colon-bearing form) anywhere in the body, nor present in
    ``native_refs`` (numbers carried as native GitHub relationships). The result
    is sorted and de-duplicated. A bare ``#N`` with no dependency phrase, or any
    number already backed by a structured line, is never returned.
    """
    body = body or ""
    structured = set()
    referenced = set()
    for line in body.splitlines():
        if _STRUCTURED_DEP_LINE_RE.match(line):
            structured.update(int(n) for n in _HASH_NUM_RE.findall(line))
        if _DEP_PHRASE_RE.search(line):
            referenced.update(int(n) for n in _HASH_NUM_RE.findall(line))
    satisfied = structured | {int(n) for n in native_refs}
    return sorted(referenced - satisfied)


def fetch_state(repo, token):
    """Assemble the reconcile state dict from live GitHub state.

    Done-ness is gathered from **closing-keyword** references in merged commit
    **bodies** on the default branch — never PR/commit titles, never a bare
    ``#N``/``Refs #N`` (CLAUDE.md rule #10, #277) — so bundled squashes are
    caught (see module docstring) without prose cross-references false-positing.
    Open-PR association is likewise taken from each open PR's closing-keyword
    refs in its title+body, so a PR that merely "Relates to #N" does not mark
    #N as having an open PR.

    ``prose_deps`` is populated per issue from ``prose_deps_in`` (issue #248):
    dependency numbers named only in prose (`depends on #N` / `blocked by #N`)
    with no matching structured ``Blocked by:`` / ``Depends on:`` line.
    """
    raw = _paginate("/repos/%s/issues?state=all" % repo, token)
    issues_raw = [i for i in raw if "pull_request" not in i]

    def labels_of(i):
        return [l["name"] for l in i.get("labels", [])]

    # Issues an OPEN PR is set to CLOSE (from the PR title+body). A PR that
    # merely "Relates to #N" must NOT mark #N as having an open PR — that would
    # wrongly suppress a legitimate stalled-`in-progress` requeue (#277).
    open_pr_refs = set()
    for p in _paginate("/repos/%s/pulls?state=open" % repo, token):
        text = "%s\n%s" % (p.get("title", ""), p.get("body") or "")
        open_pr_refs |= _closing_refs_in(text)

    # Merged commit BODY closing-keyword references reachable from the default
    # branch. The commit message body (not the subject line) is what carries
    # `Closes #N`; a prose `#N` / `Refs #N` is a link, not a landing (#277).
    body_refs = set()
    for c in _paginate("/repos/%s/commits" % repo, token):
        message = (c.get("commit") or {}).get("message", "")
        parts = message.split("\n", 1)
        commit_body = parts[1] if len(parts) > 1 else ""
        body_refs |= _closing_refs_in(commit_body)

    issues = []
    for i in issues_raw:
        labels = labels_of(i)
        ms = i.get("milestone")
        # Native hard-blocker relationships satisfy a dependency just like a
        # structured `Blocked by:` line (#321), so they must clear the
        # prose-only-dependency flag. Only open issues are fetched — closed ones
        # are excluded from the prose-dep flag by `process` anyway, and skipping
        # them avoids needless API calls.
        native_refs = (native_blocked_by(repo, i["number"], token)
                       if i["state"] == "open" else set())
        # `has_analysis_comment` (#582) needs one paginated comment fetch per
        # OPEN issue — same cost class as the per-issue `native_blocked_by`
        # call above, and the triage-hand-off rules only fire on open issues, so
        # closed ones are skipped.
        has_analysis = (_issue_has_analysis_comment(repo, i["number"], token)
                        if i["state"] == "open" else False)
        # `handback_label_adds` (#710) feeds the `flag_stuck_triage` bound on
        # `requeue_triage`. Only fetched for an OPEN issue that is actually in a
        # hand-back state with no analysis the sweep can see — the single branch
        # that consults it — so the extra timeline read costs nothing on a
        # healthy board.
        needs_count = (i["state"] == "open" and not has_analysis
                       and any(l in labels for l in TRIAGE_HANDBACK_LABELS))
        handback_adds = (_issue_handback_label_adds(repo, i["number"], token)
                         if needs_count else 0)
        issues.append({
            "number": i["number"],
            "state": i["state"],
            "labels": labels,
            "milestone": ms["title"] if ms else None,
            "is_epic": "type:epic" in labels,
            "is_dashboard": i["number"] == DASHBOARD_ISSUE,
            "has_open_pr": i["number"] in open_pr_refs,
            "prose_deps": prose_deps_in(i.get("body") or "",
                                        native_refs=native_refs),
            "has_analysis_comment": has_analysis,
            "handback_label_adds": handback_adds,
        })

    return {
        "issues": issues,
        "merged_commit_body_refs": sorted(body_refs),
        "deliverables_present": {},
    }


def main(argv):
    events_only = "--events-only" in argv
    if "--live" in argv:
        # Fetch live GitHub state, then classify.
        repo = os.environ.get("RECONCILE_REPO", REPO_DEFAULT)
        token = os.environ.get("GITHUB_TOKEN")
        if not token:
            sys.stderr.write("GITHUB_TOKEN is required for --live fetch.\n")
            return 2
        data = fetch_state(repo, token)
    else:
        # Default: state JSON on stdin (tests, local preview, gatekeeper pipe).
        data = json.load(sys.stdin)
    json.dump(process(data, events_only=events_only), sys.stdout, indent=2)
    sys.stdout.write("\n")
    return 0


if __name__ == "__main__":
    raise SystemExit(main(sys.argv[1:]))
