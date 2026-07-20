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
  * `type:epic` and the dashboard issue are never touched.
  * A `parked` issue only honors `/unpark`; every other command is ignored
    while it is parked.
  * A command must start at a line start or after whitespace, so URLs and file
    paths such as `http://x/approve/y` never trigger it.

Input schema (stdin):
  {
    "repo_owner": "derekwinters",
    "milestones": ["v0.4", "v1.0", ...],
    "issues": [
      {"number": 181, "labels": ["pending-approval"],
       "is_epic": false, "is_dashboard": false,
       "comments": [
         {"id": 7, "author": "derekwinters", "body": "...", "processed": false}
       ]}
    ]
  }

Output schema (stdout):
  {
    "actions": [
      {"issue": 181, "comment_id": 7, "commands": ["approve"],
       "add_labels": ["ready-for-work"], "remove_labels": ["pending-approval"],
       "set_milestone": "v1.0" | null,
       "set_focus": "v0.4" | null,
       "propose": false, "revise_notes": null, "redo": false,
       "react": 7,
       "menu": "ready-for-work"}
    ],
    "skipped": [{"issue": 181, "comment_id": 9, "reason": "not-owner"}]
  }
"""

import json
import re
import sys

# Commands that carry a free-text argument to end of line.
_ARG_COMMANDS = {"revise", "milestone", "focus"}
_KNOWN = ["admit", "approve", "revise", "redo", "propose",
          "park", "unpark", "milestone", "focus"]

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


def _parse_comment(body):
    """Return an ordered list of (command, arg) tuples found in a comment."""
    found = []
    for m in _CMD_RE.finditer(body or ""):
        cmd = m.group(1).lower()
        arg = m.group(2).strip() if cmd in _ARG_COMMANDS else ""
        found.append((cmd, arg))
    return found


def _build_action(issue, comment, commands, milestones, is_parked):
    """Translate the commands found in one comment into a single action."""
    add, remove = [], []
    action = {
        "issue": issue["number"],
        "comment_id": comment["id"],
        "commands": [],
        "add_labels": add,
        "remove_labels": remove,
        "set_milestone": None,
        "set_focus": None,
        "propose": False,
        "revise_notes": None,
        "redo": False,
        "react": comment["id"],
        "menu": None,
    }

    def want_add(label):
        if label not in add:
            add.append(label)

    def want_remove(label):
        if label not in remove:
            remove.append(label)

    honored = False
    for cmd, arg in commands:
        # A parked issue only responds to /unpark.
        if is_parked and cmd != "unpark":
            continue
        honored = True
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
        elif cmd == "milestone":
            resolved = _match_milestone(arg, milestones)
            action["set_milestone"] = resolved
            if action["menu"] is None:
                action["menu"] = "milestone"
        elif cmd == "focus":
            action["set_focus"] = _match_milestone(arg, milestones)
            if action["menu"] is None:
                action["menu"] = "focus"

    if not honored:
        return None
    return action


def process(data):
    owner = data.get("repo_owner")
    milestones = data.get("milestones", [])
    actions, skipped = [], []

    for issue in data.get("issues", []):
        if issue.get("is_epic") or issue.get("is_dashboard"):
            continue
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
            action = _build_action(issue, comment, commands, milestones,
                                   is_parked)
            if action is None:
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
