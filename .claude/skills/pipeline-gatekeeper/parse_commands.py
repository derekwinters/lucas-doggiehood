#!/usr/bin/env python3
"""Deterministic command parser for the pipeline-gatekeeper skill.

Reads a JSON snapshot of open issues and their comments on stdin, and writes
a JSON list of label-move *actions* on stdout. It performs no GitHub I/O — the
skill (SKILL.md) is responsible for gathering the snapshot via the GitHub MCP
tools and for applying each action (label edits, milestone set, reaction
watermark, acknowledgment comment).

Design rules encoded here (see issue #194 and docs/engineering/issue-pipeline.md):
  * Only comments authored by the repo owner are honored (the bad-actor gate).
  * Comments already carrying the "processed" watermark are skipped
    (idempotency across runs).
  * `type:epic` issues are never touched.
  * The dashboard issue (#193) is special (see issue #204): it is where the
    dashboard UI tells the owner to comment `/focus`, so the GLOBAL `/focus`
    command IS honored there. Every issue-scoped command on the dashboard
    (`/admit`, `/approve`, `/milestone`, ...) is ignored — only `set_focus`
    survives, and no label/milestone/menu change is emitted for it. Relocating
    the focus marker to a dedicated single-writer control store (a control
    issue or a repo variable) is tracked as decision #8; until then the marker
    lives on the dashboard body and `/focus` is accepted on the dashboard.
  * `/cap <n>` (issue #240) is the nightly build-cap tunable and, unlike
    `/focus`, is honored ONLY on the dashboard issue — everywhere else it is
    silently ignored (a no-op), never surfaced as a per-issue command. A
    non-numeric or non-positive argument is REJECTED (`cap-invalid` in
    `skipped`) with no `set_cap` change, mirroring `focus-no-match`.
  * A `parked` issue only honors `/unpark`; every other command is ignored
    while it is parked.
  * `/focus <arg>` and `/milestone <arg>` are REJECTED when the argument
    resolves to no live milestone (`focus-no-match` / `milestone-no-match` in
    `skipped`) rather than silently writing a null milestone.
  * `ready-for-work` implies a milestone (issue #247): `/approve` is a
    presence-check + label flip (issue #319, Part A) — it moves an issue to
    `ready-for-work` only when the issue's milestone FIELD is already set (by
    analysis, at `pending-approval`). No resolution, no name→number matching,
    no comment-scraping: an inline `/milestone` in the SAME comment does NOT
    feed this gate (it is a separate, independent command; see it applied on
    a later run once the field updates). If the field is null, the approve is
    REFUSED (`approve-no-milestone` in `skipped`, carrying the
    `which-milestone` menu) with no label move, leaving the issue in its
    prior state.
  * A command must start at a line start or after whitespace, so URLs and file
    paths such as `http://x/approve/y` never trigger it.

Input schema (stdin):
  {
    "repo_owner": "derekwinters",
    "milestones": ["03 - Dogs & Conversations", "04 - Quests & Economy", ...],
    "issues": [
      {"number": 181, "labels": ["pending-approval"],
       "is_epic": false, "is_dashboard": false,
       "milestone": "04 - Quests & Economy" | null,
       "comments": [
         {"id": 7, "author": "derekwinters", "body": "...", "processed": false}
       ]}
    ]
  }

  `milestone` is the issue's currently-set milestone title (or null) — the
  ONLY thing the `/approve` milestone gate reads (issue #319, Part A). Analysis
  now sets this field directly when it routes an issue to `pending-approval`
  (rather than only proposing one in prose), so this parser no longer reads or
  resolves any analysis-proposed-milestone comment-scrape at all.

Output schema (stdout):
  {
    "actions": [
      {"issue": 181, "comment_id": 7, "commands": ["approve"],
       "add_labels": ["ready-for-work"], "remove_labels": ["pending-approval"],
       "set_milestone": "07 - Polish & Onboarding" | null,
       "set_focus": "04 - Quests & Economy" | null,
       "set_cap": 5 | null,
       "propose": false, "revise_notes": null, "redo": false,
       "react": 7,
       "menu": "ready-for-work"}
    ],
    "skipped": [{"issue": 181, "comment_id": 9, "reason": "not-owner"}]
  }

Skip reasons: "not-owner", "no-op", "parked-ignored", "focus-no-match",
"milestone-no-match", "approve-no-milestone", "cap-invalid".
"""

import json
import re
import sys

# Commands that carry a free-text argument to end of line.
_ARG_COMMANDS = {"revise", "milestone", "focus", "cap"}
_KNOWN = ["admit", "approve", "revise", "redo", "propose",
          "park", "unpark", "milestone", "focus", "cap"]

# A command is `/word` at start-of-line or after whitespace; the rest of the
# line (for arg commands) is captured in group 2.
_CMD_RE = re.compile(
    r"(?:^|\s)/(" + "|".join(_KNOWN) + r")\b[ \t]*([^\n\r]*)",
    re.MULTILINE,
)

# The menu shown after each kind of hand-back (the "Your move" line the skill
# appends when acknowledging).
MENUS = {
    "admitted": "`/park` (or wait for analysis)",
    "ready-for-work": "`/focus <milestone>` · `/park`",
    "back-to-analysis": "`/park` (or wait for the next analysis pass)",
    "parked": "`/unpark`",
    "unparked": "`/admit` · `/approve` · `/park`",
    "milestone": "`/approve` · `/revise <notes>` · `/park`",
    "focus": "(nightly dev now targets this milestone)",
    "proposed": "`/park` (analysis will draft a PROPOSAL)",
    # Hand-back when /approve resolves no milestone (issue #247): the issue is
    # left in its prior state and Derek is asked which milestone to use.
    "which-milestone": "`/milestone <name>` then `/approve` · `/revise <notes>` · `/park`",
}


def _match_milestone(arg, milestones):
    """Resolve a `/milestone`/`/focus` argument to a full milestone title.

    Accepts a leading number ("04"), a title fragment, or the exact title.
    Returns the matched title or None.
    """
    arg = (arg or "").strip().strip("`").strip()
    if not arg:
        return None
    low = arg.lower()
    # Exact title.
    for m in milestones:
        if m.lower() == low:
            return m
    # Leading numeric prefix, e.g. "04" -> "04 - Quests & Economy".
    num = re.match(r"^0*(\d+)", arg)
    if num:
        n = num.group(1)
        for m in milestones:
            mnum = re.match(r"^0*(\d+)", m)
            if mnum and mnum.group(1) == n:
                return m
    # Fragment contained in a title.
    hits = [m for m in milestones if low in m.lower()]
    if len(hits) == 1:
        return hits[0]
    return None


def _match_cap(arg):
    """Resolve a `/cap` argument to a positive integer, or None if invalid.

    Rejects non-numeric input (letters, decimals, blank) and non-positive
    integers (zero or negative) — mirrors `_match_milestone`'s "no match"
    shape but for an integer instead of a milestone title (issue #240).
    """
    arg = (arg or "").strip().strip("`").strip()
    if not re.fullmatch(r"-?\d+", arg):
        return None
    n = int(arg)
    if n <= 0:
        return None
    return n


def _parse_comment(body):
    """Return an ordered list of (command, arg) tuples found in a comment."""
    found = []
    for m in _CMD_RE.finditer(body or ""):
        cmd = m.group(1).lower()
        arg = m.group(2).strip() if cmd in _ARG_COMMANDS else ""
        found.append((cmd, arg))
    return found


def _build_action(issue, comment, commands, milestones, is_parked,
                  dashboard_only=False):
    """Translate the commands found in one comment into a single action.

    Returns ``(action_or_None, rejected)`` where ``rejected`` is a list of
    per-command skip records (unmatched ``/focus`` / ``/milestone`` args). On
    the dashboard issue (``dashboard_only``) only the global ``/focus`` command
    is honored — issue-scoped commands are ignored and no label/milestone/menu
    change is emitted.
    """
    add, remove = [], []
    action = {
        "issue": issue["number"],
        "comment_id": comment["id"],
        "commands": [],
        "add_labels": add,
        "remove_labels": remove,
        "set_milestone": None,
        "set_focus": None,
        "set_cap": None,
        "propose": False,
        "revise_notes": None,
        "redo": False,
        "react": comment["id"],
        "menu": None,
    }
    rejected = []

    def want_add(label):
        if label not in add:
            add.append(label)

    def want_remove(label):
        if label not in remove:
            remove.append(label)

    def reject(reason, menu=None):
        record = {
            "issue": issue["number"],
            "comment_id": comment["id"],
            "reason": reason,
        }
        if menu is not None:
            record["menu"] = menu
        rejected.append(record)

    for cmd, arg in commands:
        # A parked issue only responds to /unpark.
        if is_parked and cmd != "unpark":
            continue
        # On the dashboard issue, /focus (global, honored everywhere) and
        # /cap (dashboard-only, see below) are the only commands honored;
        # every issue-scoped command is ignored there.
        if dashboard_only and cmd not in ("focus", "cap"):
            continue
        # /cap is the mirror image of /focus: honored ONLY on the dashboard
        # issue, silently ignored everywhere else (#240).
        if cmd == "cap" and not dashboard_only:
            continue

        if cmd == "milestone":
            resolved = _match_milestone(arg, milestones)
            if resolved is None:
                reject("milestone-no-match")
                continue
            action["commands"].append(cmd)
            action["set_milestone"] = resolved
            if action["menu"] is None:
                action["menu"] = "milestone"
            continue
        if cmd == "focus":
            resolved = _match_milestone(arg, milestones)
            if resolved is None:
                reject("focus-no-match")
                continue
            action["commands"].append(cmd)
            action["set_focus"] = resolved
            # On the dashboard issue keep ONLY the focus marker change — no
            # menu/ack, so the dashboard body isn't churned by acknowledgments.
            if action["menu"] is None and not dashboard_only:
                action["menu"] = "focus"
            continue
        if cmd == "cap":
            resolved = _match_cap(arg)
            if resolved is None:
                reject("cap-invalid")
                continue
            action["commands"].append(cmd)
            action["set_cap"] = resolved
            # /cap only ever fires on the dashboard issue, so — like /focus
            # there — keep ONLY the cap marker change, no menu/ack, so the
            # dashboard body isn't churned by acknowledgments.
            continue

        action["commands"].append(cmd)
        if cmd == "admit":
            want_add("ai-triage")
            action["menu"] = "admitted"
        elif cmd == "approve":
            want_add("ready-for-work")
            for lbl in ("pending-approval", "needs-clarification", "ai-triage"):
                want_remove(lbl)
            action["menu"] = "ready-for-work"
        elif cmd == "revise":
            want_add("ai-triage")
            for lbl in ("pending-approval", "needs-clarification"):
                want_remove(lbl)
            action["revise_notes"] = arg or None
            action["menu"] = "back-to-analysis"
        elif cmd == "redo":
            want_add("ai-triage")
            for lbl in ("pending-approval", "needs-clarification"):
                want_remove(lbl)
            action["redo"] = True
            action["menu"] = "back-to-analysis"
        elif cmd == "propose":
            want_add("ai-triage")
            for lbl in ("pending-approval", "needs-clarification"):
                want_remove(lbl)
            action["propose"] = True
            action["menu"] = "proposed"
        elif cmd == "park":
            want_add("parked")
            action["menu"] = "parked"
        elif cmd == "unpark":
            want_remove("parked")
            action["menu"] = "unparked"

    # Post-loop: enforce ready-for-work ⇒ has milestone (issue #247), as a pure
    # presence-check (issue #319, Part A) — analysis now sets the milestone
    # FIELD directly at pending-approval, so /approve does no resolution of its
    # own: it neither reads an inline /milestone from this same comment nor
    # re-writes the field on success (it is already correct). If the field is
    # null, refuse the transition: undo the labels /approve added, drop it from
    # the command list, and record an `approve-no-milestone` skip carrying the
    # which-milestone hand-back so the issue stays in its prior state. (A
    # /milestone command in the same comment is unaffected by this refusal —
    # it is a separate, independent command handled in the loop above.)
    #
    # NOTE (#212, forward-looking): this is the PRESENCE gate only. A sibling
    # ORDER gate (the milestone must not precede a blocker's milestone) is not
    # yet built. Now that milestone ownership lives in analysis (#319), #212
    # belongs there (or in the dashboard) — layered onto the value analysis
    # already resolved — never re-added here to /approve's presence-check.
    if "approve" in action["commands"] and not issue.get("milestone"):
        reject("approve-no-milestone", menu="which-milestone")
        action["commands"].remove("approve")
        if "ready-for-work" in add:
            add.remove("ready-for-work")
        for lbl in ("pending-approval", "needs-clarification", "ai-triage"):
            if lbl in remove:
                remove.remove(lbl)
        if action["menu"] == "ready-for-work":
            action["menu"] = None

    if not action["commands"]:
        return None, rejected
    return action, rejected


def process(data):
    owner = data.get("repo_owner")
    milestones = data.get("milestones", [])
    actions, skipped = [], []

    for issue in data.get("issues", []):
        # type:epic issues are never touched. The dashboard issue is NOT
        # skipped wholesale: the global /focus command is honored there (the
        # dashboard UI tells the owner to comment /focus on it — issue #204),
        # while every issue-scoped command is ignored.
        if issue.get("is_epic"):
            continue
        dashboard_only = bool(issue.get("is_dashboard"))
        labels = issue.get("labels", [])
        is_parked = "parked" in labels
        for comment in issue.get("comments", []):
            if comment.get("processed"):
                continue
            body = comment.get("body", "")
            commands = _parse_comment(body)
            if not commands:
                continue
            if comment.get("author") != owner:
                skipped.append({
                    "issue": issue["number"],
                    "comment_id": comment["id"],
                    "reason": "not-owner",
                })
                continue
            action, rejected = _build_action(
                issue, comment, commands, milestones, is_parked,
                dashboard_only=dashboard_only)
            skipped.extend(rejected)
            if action is None:
                if not rejected:
                    skipped.append({
                        "issue": issue["number"],
                        "comment_id": comment["id"],
                        "reason": "parked-ignored" if is_parked else "no-op",
                    })
                continue
            actions.append(action)

    return {"actions": actions, "skipped": skipped}


def main():
    data = json.load(sys.stdin)
    json.dump(process(data), sys.stdout, indent=2)
    sys.stdout.write("\n")


if __name__ == "__main__":
    main()
