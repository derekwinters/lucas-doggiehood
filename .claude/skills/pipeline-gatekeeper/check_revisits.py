#!/usr/bin/env python3
"""Deterministic blocker auto-revisit check for the pipeline-gatekeeper skill.

An issue can be parked in ``needs-clarification`` *only because* it is
``Blocked by: #N`` — it needed a decision that lives in its blocker. Once every
such blocker is resolved (its blocker reaches ``ready-for-work``/``in-progress``
or is closed/merged), nothing was re-examining that issue: analysis only acts on
``ai-triage`` and the gatekeeper only acts on Derek's comments, so it stalled
indefinitely. This module is the missing automatic transition (issue #241): it
detects those now-unblocked ``needs-clarification`` issues and returns a revisit
action that swaps them back into the analysis queue.

Unlike ``parse_commands.py`` this is **not** a comment command — it is a
state-derived transition — so it lives alongside that parser as its own
function, not inside the comment parser. The gatekeeper runs it after processing
comment commands and applies each returned label swap, posting a short
auto-comment ending in the ``back-to-analysis`` menu.

It performs no GitHub I/O — the skill (SKILL.md) gathers the open-issue snapshot
via the GitHub MCP tools and applies each returned action.

Input schema (stdin / ``process``):
  {"issues": [{"number": 2, "labels": ["needs-clarification"],
               "body": "Blocked by: #1"}]}

  ``issues`` is the snapshot of **open** issues only (number, labels, body); a
  closed/merged blocker is therefore simply absent from the list and counts as
  resolved.

Output schema (stdout / ``process``):
  {"revisits": [{"issue": 2, "add_labels": ["ai-triage"],
                 "remove_labels": ["needs-clarification"],
                 "blockers_resolved": [1], "menu": "back-to-analysis"}]}

A blocker is **resolved** when it is absent from the open snapshot (closed) or
carries ``ready-for-work`` / ``in-progress``. An issue is revisited only when it
is ``needs-clarification``, not ``parked``, has at least one structured
``Blocked by: #N`` line, and **every** such blocker is resolved.
"""

import json
import re
import sys

# A blocker counts as resolved once it reaches one of these states (or closes,
# which drops it from the open snapshot entirely).
RESOLVED_LABELS = {"ready-for-work", "in-progress"}

# The label swap a revisit applies — back into the analysis queue, mirroring
# /revise and /redo. Kept here so the transition is declared, not inlined.
REVISIT_ADD = ["ai-triage"]
REVISIT_REMOVE = ["needs-clarification"]
REVISIT_MENU = "back-to-analysis"

# A STRUCTURED hard-blocker line: the keyword ``blocked by``, then a REQUIRED
# colon, then ``#N``, at line start (optionally behind a ``-``/``*`` list
# marker). The colon is what makes the line canonical — the same form
# select_queue.py, reconcile.py, and the dashboard parse. A prose mention
# without the colon (``blocked by #1`` mid-sentence) must NOT fire an automatic
# label move, so it is deliberately not matched here.
_BLOCKED_BY_LINE_RE = re.compile(
    r"(?im)^[ \t]*(?:[-*][ \t]+)?blocked by[ \t]*:[ \t]*(.+)$"
)
_HASH_NUM_RE = re.compile(r"#(\d+)")


def _structured_blockers(body):
    """Issue numbers named on structured ``Blocked by: #N`` lines in ``body``."""
    nums = []
    for m in _BLOCKED_BY_LINE_RE.finditer(body or ""):
        nums.extend(int(n) for n in _HASH_NUM_RE.findall(m.group(1)))
    return nums


def check_blocker_revisits(issues):
    """Return revisit actions for now-unblocked ``needs-clarification`` issues.

    ``issues`` is the snapshot of OPEN issues (each ``{number, labels, body}``).
    Returns a list of ``{"issue", "add_labels", "remove_labels",
    "blockers_resolved", "menu"}`` — one per issue that should swap back into
    the analysis queue.
    """
    open_nums = {i["number"] for i in issues}
    labels_by = {i["number"]: set(i.get("labels", [])) for i in issues}

    def resolved(blocker):
        if blocker not in open_nums:
            return True  # closed / merged -> gone from the open snapshot
        return bool(labels_by[blocker] & RESOLVED_LABELS)

    revisits = []
    for i in issues:
        labels = set(i.get("labels", []))
        if "needs-clarification" not in labels or "parked" in labels:
            continue
        blockers = _structured_blockers(i.get("body", ""))
        if not blockers:
            continue
        if all(resolved(b) for b in blockers):
            revisits.append({
                "issue": i["number"],
                "add_labels": list(REVISIT_ADD),
                "remove_labels": list(REVISIT_REMOVE),
                "blockers_resolved": sorted(set(blockers)),
                "menu": REVISIT_MENU,
            })
    return revisits


def process(data):
    return {"revisits": check_blocker_revisits(data.get("issues", []))}


def main():
    data = json.load(sys.stdin)
    json.dump(process(data), sys.stdout, indent=2)
    sys.stdout.write("\n")


if __name__ == "__main__":
    main()
