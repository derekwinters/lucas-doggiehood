"""Tests for the pipeline-analysis triage discovery selector.

Run: python3 -m unittest discover -s .claude/skills/pipeline-analysis/tests
Pure JSON-in/JSON-out; no GitHub access needed. See ../select_triage.py.
"""

import json
import os
import subprocess
import sys
import unittest

SCRIPT = os.path.join(os.path.dirname(__file__), os.pardir, "select_triage.py")

DASHBOARD_NUMBER = 193


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
        "state": "open",
        "labels": ["ai-triage"],
        "milestone": "04 - Quests & Economy",
        "is_epic": False,
        "is_dashboard": False,
        "comments": [],
    }
    d.update(kw)
    return d


def payload(issues, repo_owner="derekwinters"):
    return {"repo_owner": repo_owner, "issues": issues}


class TestSelectTriage(unittest.TestCase):
    def test_basic_eligibility(self):
        out = run(payload([issue(210), issue(212)]))
        self.assertEqual(out["eligible"], [210, 212])

    def test_missing_ai_triage_label_excluded(self):
        out = run(payload([issue(210), issue(211, labels=["pending-approval"])]))
        self.assertEqual(out["eligible"], [210])
        self.assertTrue(any(s["number"] == 211 for s in out["skipped"]))

    def test_closed_excluded(self):
        out = run(payload([issue(210), issue(211, state="closed")]))
        self.assertEqual(out["eligible"], [210])
        self.assertTrue(any(s["number"] == 211 for s in out["skipped"]))

    def test_epic_excluded(self):
        out = run(payload([issue(210), issue(211, is_epic=True)]))
        self.assertEqual(out["eligible"], [210])
        self.assertTrue(any(s["number"] == 211 for s in out["skipped"]))

    def test_dashboard_excluded(self):
        out = run(payload([
            issue(210),
            issue(DASHBOARD_NUMBER, is_dashboard=True, labels=["ai-triage", "dashboard"]),
        ]))
        self.assertEqual(out["eligible"], [210])
        self.assertTrue(any(s["number"] == DASHBOARD_NUMBER for s in out["skipped"]))

    def test_parked_excluded(self):
        out = run(payload([
            issue(210),
            issue(211, labels=["ai-triage", "parked"]),
        ]))
        self.assertEqual(out["eligible"], [210])
        self.assertTrue(any(s["number"] == 211 for s in out["skipped"]))

    def test_context_carries_milestone(self):
        out = run(payload([issue(210, milestone="05 - Playtest Fixes & Polish")]))
        ctx = {c["number"]: c for c in out["context"]}
        self.assertEqual(ctx[210]["milestone"], "05 - Playtest Fixes & Polish")

    def test_context_no_milestone_is_null(self):
        out = run(payload([issue(210, milestone=None)]))
        ctx = {c["number"]: c for c in out["context"]}
        self.assertIsNone(ctx[210]["milestone"])

    def test_context_carries_latest_revise_note(self):
        out = run(payload([issue(210, comments=[
            {"id": 1, "author": "derekwinters", "body": "looks close"},
            {"id": 2, "author": "derekwinters",
             "body": "/revise please cover the edge case with no coins"},
        ])]))
        ctx = {c["number"]: c for c in out["context"]}
        self.assertEqual(ctx[210]["latest_note"],
                         {"command": "revise",
                          "notes": "please cover the edge case with no coins"})

    def test_context_carries_latest_of_multiple_notes(self):
        # Two owner revise comments -> the LATER one (by list order) wins.
        out = run(payload([issue(210, comments=[
            {"id": 1, "author": "derekwinters", "body": "/revise first note"},
            {"id": 2, "author": "derekwinters", "body": "/revise second note"},
        ])]))
        ctx = {c["number"]: c for c in out["context"]}
        self.assertEqual(ctx[210]["latest_note"]["notes"], "second note")

    def test_context_carries_redo_note_with_no_argument(self):
        out = run(payload([issue(210, comments=[
            {"id": 1, "author": "derekwinters", "body": "/redo"},
        ])]))
        ctx = {c["number"]: c for c in out["context"]}
        self.assertEqual(ctx[210]["latest_note"], {"command": "redo", "notes": None})

    def test_context_carries_propose_note(self):
        out = run(payload([issue(210, comments=[
            {"id": 1, "author": "derekwinters", "body": "/propose"},
        ])]))
        ctx = {c["number"]: c for c in out["context"]}
        self.assertEqual(ctx[210]["latest_note"], {"command": "propose", "notes": None})

    def test_context_no_note_is_null(self):
        out = run(payload([issue(210, comments=[
            {"id": 1, "author": "derekwinters", "body": "just a thought"},
        ])]))
        ctx = {c["number"]: c for c in out["context"]}
        self.assertIsNone(ctx[210]["latest_note"])

    def test_non_owner_comment_ignored_for_notes(self):
        out = run(payload([issue(210, comments=[
            {"id": 1, "author": "someone-else", "body": "/revise not the owner"},
        ])]))
        ctx = {c["number"]: c for c in out["context"]}
        self.assertIsNone(ctx[210]["latest_note"])

    def test_context_only_covers_eligible_issues(self):
        out = run(payload([issue(210), issue(211, is_epic=True)]))
        numbers = {c["number"] for c in out["context"]}
        self.assertEqual(numbers, {210})


if __name__ == "__main__":
    unittest.main()
