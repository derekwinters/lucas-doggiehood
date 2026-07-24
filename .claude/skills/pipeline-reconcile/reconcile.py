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
       "has_open_pr": false, "prose_deps": [178]}
    ],
    "merged_commit_body_refs": [56, 54, 189, 222],
    "deliverables_present": {"58": true}
  }

Output schema (stdout):
  {
    "strip_labels":         [{"number": 211, "labels": ["in-progress"]}],
    "requeue":              [{"number": 109, "from": "in-progress",
                             "to": "ready-for-work"}],
    "flag_done":            [{"number": 56, "reason": "..."}],
    "flag_orphaned_ready":  [{"number": 300}],
    "flag_prose_dep":       [{"number": 178, "refs": [109]}]
  }

``strip_labels`` and ``requeue`` are the auto-fixes (applied by the
gatekeeper); the three ``flag_*`` lists are surfaced read-only on the
dashboard. Every list is sorted by issue number.
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


def _is_done(number, body_refs, deliverables):
    if number in body_refs:
        return True
    # deliverables_present keys may arrive as JSON strings or ints.
    return bool(deliverables.get(number, deliverables.get(str(number), False)))


def process(data):
    body_refs = set(data.get("merged_commit_body_refs", []))
    deliverables = data.get("deliverables_present", {}) or {}

    strip_labels = []
    requeue = []
    flag_done = []
    flag_orphaned_ready = []
    flag_prose_dep = []

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
        elif "in-progress" in labels and not issue.get("has_open_pr"):
            # Stalled: picked up by a nightly build that dropped its commits,
            # no open PR, not on main -> return to the queue so it retries.
            requeue.append({"number": number, "from": "in-progress",
                            "to": "ready-for-work"})

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
        "flag_done": sorted(flag_done, key=by_num),
        "flag_orphaned_ready": sorted(flag_orphaned_ready, key=by_num),
        "flag_prose_dep": sorted(flag_prose_dep, key=by_num),
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
        issues.append({
            "number": i["number"],
            "state": i["state"],
            "labels": labels,
            "milestone": ms["title"] if ms else None,
            "is_epic": "type:epic" in labels,
            "is_dashboard": i["number"] == DASHBOARD_ISSUE,
            "has_open_pr": i["number"] in open_pr_refs,
            "prose_deps": prose_deps_in(i.get("body") or ""),
        })

    return {
        "issues": issues,
        "merged_commit_body_refs": sorted(body_refs),
        "deliverables_present": {},
    }


def main(argv):
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
    json.dump(process(data), sys.stdout, indent=2)
    sys.stdout.write("\n")
    return 0


if __name__ == "__main__":
    raise SystemExit(main(sys.argv[1:]))
