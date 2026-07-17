"""Tests for the pipeline-dev queue selector.

Run: python3 -m unittest discover -s .claude/skills/pipeline-dev/tests
Pure JSON-in/JSON-out; no GitHub access needed. See ../select_queue.py.
"""

import json
import os
import subprocess
import sys
import unittest

SCRIPT = os.path.join(os.path.dirname(__file__), os.pardir, "select_queue.py")


def run(payload):
    proc = subprocess.run(
        [sys.executable, SCRIPT],
        input=json.dumps(payload),
        capture_output=True,
        text=True,
    )
    assert proc.returncode == 0, proc.stderr
    return json.loads(proc.stdout)


def issue(number, **kw):
    d = {
        "number": number,
        "labels": ["ready-for-work"],
        "milestone": "04 - Quests & Economy",
        "is_epic": False,
        "has_open_pr": False,
        "blocked_by": [],
        "depends_on": [],
    }
    d.update(kw)
    return d


def payload(issues, focus="04 - Quests & Economy", cap=3, open_numbers=None):
    if open_numbers is None:
        open_numbers = [i["number"] for i in issues]
    return {
        "focus_milestone": focus,
        "cap": cap,
        "open_issue_numbers": open_numbers,
        "issues": issues,
    }


class TestSelectQueue(unittest.TestCase):
    def test_basic_eligibility_and_number_order(self):
        out = run(payload([issue(212), issue(210), issue(211)]))
        self.assertEqual(out["selected"], [210, 211, 212])

    def test_wrong_milestone_excluded(self):
        out = run(payload([
            issue(210),
            issue(211, milestone="07 - Polish & Onboarding"),
        ]))
        self.assertEqual(out["selected"], [210])

    def test_missing_ready_label_excluded(self):
        out = run(payload([issue(210), issue(211, labels=["pending-approval"])]))
        self.assertEqual(out["selected"], [210])

    def test_parked_and_epic_excluded(self):
        out = run(payload([
            issue(210),
            issue(211, labels=["ready-for-work", "parked"]),
            issue(212, is_epic=True),
        ]))
        self.assertEqual(out["selected"], [210])

    def test_open_pr_excluded(self):
        out = run(payload([issue(210), issue(211, has_open_pr=True)]))
        self.assertEqual(out["selected"], [210])

    def test_blocked_by_open_issue_excluded(self):
        # 211 is blocked by 205, which is still open -> not eligible.
        out = run(payload(
            [issue(210), issue(211, blocked_by=[205])],
            open_numbers=[205, 210, 211],
        ))
        self.assertEqual(out["selected"], [210])
        self.assertTrue(any(s["number"] == 211 for s in out["skipped"]))

    def test_blocked_by_closed_issue_included(self):
        # 205 is closed (not in open set) -> 211 becomes eligible.
        out = run(payload(
            [issue(210), issue(211, blocked_by=[205])],
            open_numbers=[210, 211],
        ))
        self.assertEqual(out["selected"], [210, 211])

    def test_topological_order_dependency_first(self):
        # 210 depends on 212 for ordering (both eligible, no hard blocker) ->
        # 212 must build before 210 even though 210 has the lower number.
        out = run(payload([
            issue(210, depends_on=[212]),
            issue(212),
        ], open_numbers=[210, 212]))
        self.assertLess(
            out["build_order"].index(212), out["build_order"].index(210)
        )

    def test_cap_limits_selection(self):
        out = run(payload([issue(n) for n in (210, 211, 212, 213)], cap=3))
        self.assertEqual(len(out["selected"]), 3)
        self.assertEqual(out["selected"], [210, 211, 212])
        self.assertEqual(out["capped_out"], [213])

    def test_no_focus_defaults_empty_when_none_match(self):
        out = run(payload([issue(210)], focus="99 - Nonexistent"))
        self.assertEqual(out["selected"], [])


if __name__ == "__main__":
    unittest.main()
