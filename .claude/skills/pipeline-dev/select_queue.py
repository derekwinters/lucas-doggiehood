#!/usr/bin/env python3
"""Queue selection for the pipeline-dev skill.

Reads a JSON snapshot of open issues on stdin and writes the nightly build
queue on stdout. Pure and deterministic — no GitHub I/O. The skill (SKILL.md)
gathers the snapshot via the GitHub MCP tools, runs this to decide *what* and
*in what order* to build, then drives `doggiehood-dev` over the result.

Two distinct dependency notions (see issue #197):

  * ``blocked_by`` — a HARD gate. An issue is not eligible until every hard
    blocker is closed/merged (i.e. no longer in ``open_issue_numbers``).
  * ``depends_on`` — an ORDERING hint used to topologically sort the eligible
    set so a prerequisite builds before its dependent. It does not gate
    eligibility; edges to non-eligible issues are ignored for ordering.

Input schema (stdin):
  {
    "focus_milestone": "v0.4",
    "cap": 3,
    "open_issue_numbers": [205, 210, 211, 212],
    "issues": [
      {"number": 210, "labels": ["ready-for-work"],
       "milestone": "v0.4", "is_epic": false,
       "has_open_pr": false, "blocked_by": [], "depends_on": []}
    ]
  }

Output schema (stdout):
  {
    "build_order": [212, 210, 211],   # eligible set, topologically ordered
    "selected":    [212, 210, 211],   # build_order truncated to the cap
    "capped_out":  [],                # eligible but beyond the cap tonight
    "skipped":     [{"number": 205, "reason": "..."}]
  }
"""

import json
import sys


def _eligible(issue, focus, open_set):
    """Return (True, None) if eligible, else (False, reason)."""
    labels = issue.get("labels", [])
    if issue.get("is_epic"):
        return False, "type:epic"
    if "parked" in labels:
        return False, "parked"
    if "ready-for-work" not in labels:
        return False, "not ready-for-work"
    if issue.get("milestone") != focus:
        return False, "outside focus milestone"
    if issue.get("has_open_pr"):
        return False, "has an open PR"
    open_blockers = [b for b in issue.get("blocked_by", []) if b in open_set]
    if open_blockers:
        joined = ", ".join("#%d" % b for b in open_blockers)
        return False, "blocked by %s (open)" % joined
    return True, None


def _topo_order(eligible):
    """Topologically sort eligible issues, ties broken by issue number.

    Edges come from ``depends_on`` but only those pointing at another eligible
    issue matter. Falls back to number order if a cycle is detected.
    """
    nums = sorted(e["number"] for e in eligible)
    by_num = {e["number"]: e for e in eligible}
    elig_set = set(nums)

    # Prerequisites of each node, restricted to the eligible set.
    prereqs = {
        n: set(d for d in by_num[n].get("depends_on", []) if d in elig_set)
        for n in nums
    }

    ordered = []
    placed = set()
    # Deterministic Kahn's algorithm: repeatedly take the lowest-numbered node
    # whose prerequisites are all already placed.
    progress = True
    while len(ordered) < len(nums) and progress:
        progress = False
        for n in nums:
            if n in placed:
                continue
            if prereqs[n] <= placed:
                ordered.append(n)
                placed.add(n)
                progress = True
    if len(ordered) < len(nums):
        # Cycle: append the remainder in number order (deterministic).
        for n in nums:
            if n not in placed:
                ordered.append(n)
    return ordered


def process(data):
    focus = data.get("focus_milestone")
    cap = data.get("cap", 3)
    open_set = set(data.get("open_issue_numbers", []))

    eligible, skipped = [], []
    for issue in data.get("issues", []):
        ok, reason = _eligible(issue, focus, open_set)
        if ok:
            eligible.append(issue)
        else:
            skipped.append({"number": issue["number"], "reason": reason})

    build_order = _topo_order(eligible)
    selected = build_order[:cap]
    capped_out = build_order[cap:]

    return {
        "build_order": build_order,
        "selected": selected,
        "capped_out": capped_out,
        "skipped": skipped,
    }


def main():
    data = json.load(sys.stdin)
    json.dump(process(data), sys.stdout, indent=2)
    sys.stdout.write("\n")


if __name__ == "__main__":
    main()
