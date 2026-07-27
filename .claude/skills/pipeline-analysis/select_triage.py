#!/usr/bin/env python3
"""Triage discovery for the pipeline-analysis skill (issue #320).

Reads a JSON snapshot of issues on stdin and writes the set of issues that
are eligible for single-issue triage on stdout, each with the context the
`triage-issue` skill needs to inherit its steering. Pure and deterministic —
no GitHub I/O. The skill (`pipeline-analysis/SKILL.md`, the dispatcher)
gathers the snapshot via the GitHub MCP tools, runs this to decide *which*
issues need triage, then invokes `triage-issue` once per returned issue with
bounded concurrency. `triage-issue` can also run standalone on a single issue
number without this script at all.

Eligible = **open** AND labeled **`ai-triage`** AND **not** `type:epic` AND
**not** the dashboard issue (#193) AND **not** `parked`. Mirrors the exact
pure `process(data)` / stdin-stdout `main()` shape of
`pipeline-dev/select_queue.py` and `pipeline-reconcile/reconcile.py`.

Input schema (stdin):
  {
    "repo_owner": "derekwinters",
    "issues": [
      {"number": 210, "state": "open", "labels": ["ai-triage"],
       "milestone": "04 - Quests & Economy" | null,
       "is_epic": false, "is_dashboard": false,
       "comments": [
         {"id": 7, "author": "derekwinters", "body": "/revise cover X"}
       ]}
    ]
  }

Output schema (stdout):
  {
    "eligible": [210, 212],
    "context": [
      {"number": 210, "milestone": "04 - Quests & Economy",
       "latest_note": {"command": "revise", "notes": "cover X"} | null}
    ],
    "skipped": [{"number": 193, "reason": "dashboard"}]
  }

``context`` carries only the eligible issues, each with its current
milestone and the **latest** owner `/revise` / `/redo` / `/propose` comment
(by list order — comments are expected in chronological order, matching the
GitHub API) so `triage-issue` inherits Derek's most recent steering note when
re-triaging. `/revise <notes>` carries free-text ``notes``; `/redo` and
`/propose` carry no argument, so their ``notes`` is ``null``. Only comments
authored by ``repo_owner`` are considered (the bad-actor gate mirrors
`pipeline-gatekeeper/parse_commands.py`).
"""

import json
import re
import sys

DASHBOARD_ISSUE = 193

# A command is `/word` at start-of-line or after whitespace; the rest of the
# line is captured for `/revise`, which carries free-text notes. `/redo` and
# `/propose` take no argument.
_NOTE_CMD_RE = re.compile(
    r"(?:^|\s)/(revise|redo|propose)\b[ \t]*([^\n\r]*)",
    re.MULTILINE,
)


def _eligible(issue):
    """Return (True, None) if eligible for triage, else (False, reason)."""
    if issue.get("state", "open") != "open":
        return False, "closed"
    if issue.get("is_epic"):
        return False, "type:epic"
    if issue.get("is_dashboard") or issue.get("number") == DASHBOARD_ISSUE:
        return False, "dashboard"
    labels = issue.get("labels", [])
    if "parked" in labels:
        return False, "parked"
    if "ai-triage" not in labels:
        return False, "missing ai-triage label"
    return True, None


def _latest_note(comments, owner):
    """Latest owner `/revise` / `/redo` / `/propose` note, or None.

    "Latest" means the last matching comment in list order (comments are
    expected in chronological order, matching the GitHub API).
    """
    latest = None
    for comment in comments or []:
        if comment.get("author") != owner:
            continue
        body = comment.get("body", "")
        match = None
        for m in _NOTE_CMD_RE.finditer(body):
            match = m
        if match is None:
            continue
        cmd = match.group(1).lower()
        notes = None
        if cmd == "revise":
            notes = match.group(2).strip() or None
        latest = {"command": cmd, "notes": notes}
    return latest


def process(data):
    owner = data.get("repo_owner")
    eligible, context, skipped = [], [], []

    for issue in data.get("issues", []):
        number = issue["number"]
        ok, reason = _eligible(issue)
        if not ok:
            skipped.append({"number": number, "reason": reason})
            continue
        eligible.append(number)
        context.append({
            "number": number,
            "milestone": issue.get("milestone"),
            "latest_note": _latest_note(issue.get("comments"), owner),
        })

    eligible.sort()
    context.sort(key=lambda c: c["number"])
    skipped.sort(key=lambda s: s["number"])

    return {"eligible": eligible, "context": context, "skipped": skipped}


def main():
    data = json.load(sys.stdin)
    json.dump(process(data), sys.stdout, indent=2)
    sys.stdout.write("\n")


if __name__ == "__main__":
    main()
