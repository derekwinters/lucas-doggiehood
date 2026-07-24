"""Tests for the pipeline-reconcile detection rules.

Run: python3 -m unittest discover -s .claude/skills/pipeline-reconcile/tests
Pure JSON-in/JSON-out; no GitHub access needed. See ../reconcile.py.

Each rule is exercised with a representative fixture plus its healthy/negative
counterpart, so a regression in the classification split (done-ness vs. stall)
fails CI. The bundled-squash blind spot (issue #246, 2026-07-23 comment) is
locked in by `test_title_only_reference_does_not_flag_done`.
"""

import json
import os
import subprocess
import sys
import unittest

SCRIPT = os.path.join(os.path.dirname(__file__), os.pardir, "reconcile.py")


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
        "labels": [],
        "milestone": "v0.4",
        "is_epic": False,
        "is_dashboard": False,
        "has_open_pr": False,
        "prose_deps": [],
    }
    d.update(kw)
    return d


def payload(issues, merged_commit_body_refs=None, deliverables_present=None):
    return {
        "issues": issues,
        "merged_commit_body_refs": merged_commit_body_refs or [],
        "deliverables_present": deliverables_present or {},
    }


def numbers(findings):
    return [f["number"] for f in findings]


class TestClosedStaleLabels(unittest.TestCase):
    def test_closed_with_stale_labels_strips_exactly_those(self):
        # A closed issue still carrying pipeline-state labels -> strip only the
        # pipeline-state labels (mirror of merged-but-open; the #211 Closes gap).
        out = run(payload([
            issue(211, state="closed",
                  labels=["in-progress", "type:task", "area:ai"]),
        ]))
        self.assertEqual(numbers(out["strip_labels"]), [211])
        self.assertEqual(out["strip_labels"][0]["labels"], ["in-progress"])

    def test_closed_with_multiple_stale_labels(self):
        out = run(payload([
            issue(500, state="closed",
                  labels=["ready-for-work", "in-progress", "ai-triage"]),
        ]))
        # Reported in the canonical pipeline-state order, not input order.
        self.assertEqual(
            out["strip_labels"][0]["labels"],
            ["ai-triage", "ready-for-work", "in-progress"],
        )

    def test_clean_closed_issue_no_finding(self):
        out = run(payload([
            issue(400, state="closed", labels=["type:task", "area:ai"]),
        ]))
        self.assertEqual(out["strip_labels"], [])

    def test_open_issue_never_strip(self):
        # strip_labels only ever fires on CLOSED issues.
        out = run(payload([issue(300, state="open", labels=["in-progress"])]))
        self.assertEqual(out["strip_labels"], [])


class TestStalledInProgress(unittest.TestCase):
    def test_stalled_in_progress_requeues(self):
        # open, in-progress, no open PR, not on main -> return to ready-for-work.
        out = run(payload([issue(109, labels=["in-progress"])]))
        self.assertEqual(numbers(out["requeue"]), [109])
        self.assertEqual(out["requeue"][0]["from"], "in-progress")
        self.assertEqual(out["requeue"][0]["to"], "ready-for-work")

    def test_in_progress_with_open_pr_not_requeued(self):
        out = run(payload([issue(109, labels=["in-progress"], has_open_pr=True)]))
        self.assertEqual(out["requeue"], [])

    def test_ready_for_work_not_requeued(self):
        out = run(payload([issue(109, labels=["ready-for-work"])]))
        self.assertEqual(out["requeue"], [])


class TestMergedButOpen(unittest.TestCase):
    def test_merged_but_open_via_commit_body_flags(self):
        # #56 landed on main (its number is in a merged commit body) but is
        # still open -> flag_done for Derek to close (not auto-closed; #211
        # owns the clean single-issue auto-close).
        out = run(payload(
            [issue(56, labels=["in-progress"])],
            merged_commit_body_refs=[56],
        ))
        self.assertEqual(numbers(out["flag_done"]), [56])

    def test_title_only_reference_does_not_flag_done(self):
        # The bundled-squash guard (#246): done-ness is decided ONLY by a
        # merged commit *body* reference (or deliverables), never by a PR/commit
        # *title* referencing the issue. #999 is referenced only by a title, so
        # it is NOT in merged_commit_body_refs -> no flag_done.
        out = run(payload(
            [issue(999, labels=["in-progress"])],
            merged_commit_body_refs=[],
        ))
        self.assertEqual(out["flag_done"], [])
        # ...and with no on-main signal it is instead treated as a stall.
        self.assertEqual(numbers(out["requeue"]), [999])

    def test_deliverables_present_flags_done(self):
        # The second done-ness signal: the issue's deliverables exist at HEAD.
        out = run(payload(
            [issue(58, labels=["in-progress"])],
            deliverables_present={"58": True},
        ))
        self.assertEqual(numbers(out["flag_done"]), [58])

    def test_closed_done_issue_not_flag_done(self):
        # flag_done is for OPEN issues; a closed one is handled by strip_labels.
        out = run(payload(
            [issue(56, state="closed", labels=["in-progress"])],
            merged_commit_body_refs=[56],
        ))
        self.assertEqual(out["flag_done"], [])
        self.assertEqual(numbers(out["strip_labels"]), [56])


class TestClassificationSplit(unittest.TestCase):
    def test_done_in_progress_flags_not_requeue(self):
        # The done-ness guard that stops the #109 re-pick loop: an open
        # in-progress issue that IS already on main classifies as flag_done,
        # never requeue.
        out = run(payload(
            [issue(109, labels=["in-progress"])],
            merged_commit_body_refs=[109],
        ))
        self.assertEqual(numbers(out["flag_done"]), [109])
        self.assertEqual(out["requeue"], [])


class TestStretchRules(unittest.TestCase):
    def test_orphaned_ready_no_milestone_flags(self):
        out = run(payload([issue(300, labels=["ready-for-work"], milestone=None)]))
        self.assertEqual(numbers(out["flag_orphaned_ready"]), [300])

    def test_ready_with_milestone_not_orphaned(self):
        out = run(payload([issue(300, labels=["ready-for-work"], milestone="v0.4")]))
        self.assertEqual(out["flag_orphaned_ready"], [])

    def test_prose_only_dependency_flags(self):
        out = run(payload([issue(178, labels=["ready-for-work"], prose_deps=[109])]))
        self.assertEqual(numbers(out["flag_prose_dep"]), [178])
        self.assertEqual(out["flag_prose_dep"][0]["refs"], [109])


class TestExclusionsAndHealth(unittest.TestCase):
    def test_epic_excluded(self):
        out = run(payload([
            issue(191, state="closed", labels=["in-progress"], is_epic=True),
        ]))
        self.assertEqual(out["strip_labels"], [])

    def test_dashboard_excluded(self):
        out = run(payload([
            issue(193, labels=["in-progress"], is_dashboard=True),
        ]))
        self.assertEqual(out["requeue"], [])

    def test_parked_excluded(self):
        out = run(payload([
            issue(700, labels=["in-progress", "parked"]),
        ]))
        self.assertEqual(out["requeue"], [])

    def test_empty_and_healthy_board_no_findings(self):
        out = run(payload([
            issue(1, state="closed", labels=["type:task"]),
            issue(2, labels=["ready-for-work"], milestone="v0.4"),
            issue(3, labels=["in-progress"], has_open_pr=True),
        ]))
        for key in ("strip_labels", "requeue", "flag_done",
                    "flag_orphaned_ready", "flag_prose_dep"):
            self.assertEqual(out[key], [], key)

    def test_findings_sorted_by_number(self):
        out = run(payload([
            issue(300, labels=["in-progress"]),
            issue(100, labels=["in-progress"]),
            issue(200, labels=["in-progress"]),
        ]))
        self.assertEqual(numbers(out["requeue"]), [100, 200, 300])


if __name__ == "__main__":
    unittest.main()
