"""Tests for the blocker auto-revisit check (pipeline-gatekeeper).

Run: python3 -m unittest discover -s .claude/skills/pipeline-gatekeeper/tests

`check_blocker_revisits` is deterministic and pure (a snapshot of open issues
in -> revisit actions out), so it needs no GitHub access. See
../check_revisits.py.
"""

import json
import os
import subprocess
import sys
import unittest

HERE = os.path.dirname(__file__)
sys.path.insert(0, os.path.join(HERE, os.pardir))

import check_revisits  # noqa: E402

SCRIPT = os.path.join(HERE, os.pardir, "check_revisits.py")


def issue(number, labels=None, body="", native_blocked_by=None):
    d = {"number": number, "labels": labels or [], "body": body}
    if native_blocked_by is not None:
        d["native_blocked_by"] = native_blocked_by
    return d


def revisits_for(number, out):
    return [r for r in out if r["issue"] == number]


class TestCheckBlockerRevisits(unittest.TestCase):
    def test_blocker_now_ready_for_work_triggers_revisit(self):
        # Issue 1 blocks issue 2. Issue 2 is parked in needs-clarification only
        # because it needed a decision from issue 1; once issue 1 reaches
        # ready-for-work, issue 2 should be revisited.
        out = check_revisits.check_blocker_revisits([
            issue(1, labels=["ready-for-work"]),
            issue(2, labels=["needs-clarification"], body="Blocked by: #1"),
        ])
        r = revisits_for(2, out)
        self.assertEqual(len(r), 1)
        self.assertIn("ai-triage", r[0]["add_labels"])
        self.assertIn("needs-clarification", r[0]["remove_labels"])
        self.assertEqual(r[0]["blockers_resolved"], [1])
        self.assertEqual(r[0]["menu"], "back-to-analysis")

    def test_blocker_in_progress_triggers_revisit(self):
        out = check_revisits.check_blocker_revisits([
            issue(1, labels=["in-progress"]),
            issue(2, labels=["needs-clarification"], body="Blocked by: #1"),
        ])
        self.assertEqual(len(revisits_for(2, out)), 1)

    def test_closed_blocker_absent_from_snapshot_triggers_revisit(self):
        # The snapshot lists only OPEN issues, so a closed/merged blocker simply
        # isn't present -> it counts as resolved.
        out = check_revisits.check_blocker_revisits([
            issue(2, labels=["needs-clarification"], body="Blocked by: #1"),
        ])
        self.assertEqual(len(revisits_for(2, out)), 1)

    def test_multiple_blockers_partial_resolution_does_not_revisit(self):
        # 2 is blocked by 1 (ready) and 3 (still open, unresolved) -> stay put.
        out = check_revisits.check_blocker_revisits([
            issue(1, labels=["ready-for-work"]),
            issue(3, labels=["needs-clarification"]),
            issue(2, labels=["needs-clarification"],
                  body="Blocked by: #1\nBlocked by: #3"),
        ])
        self.assertEqual(revisits_for(2, out), [])

    def test_multiple_blockers_all_resolved_revisits(self):
        # 1 ready, 3 closed (absent) -> both resolved -> revisit, both listed.
        out = check_revisits.check_blocker_revisits([
            issue(1, labels=["ready-for-work"]),
            issue(2, labels=["needs-clarification"],
                  body="Blocked by: #1\nBlocked by: #3"),
        ])
        r = revisits_for(2, out)
        self.assertEqual(len(r), 1)
        self.assertEqual(r[0]["blockers_resolved"], [1, 3])

    def test_blocker_still_open_unresolved_does_not_revisit(self):
        # Blocker 1 is open with no ready-for-work/in-progress label -> not
        # resolved; no false trigger (regression guard).
        out = check_revisits.check_blocker_revisits([
            issue(1, labels=["pending-approval"]),
            issue(2, labels=["needs-clarification"], body="Blocked by: #1"),
        ])
        self.assertEqual(revisits_for(2, out), [])

    def test_no_blocked_by_line_never_revisits(self):
        # An issue in needs-clarification with no Blocked by line is never
        # touched (regression guard — no false triggers).
        out = check_revisits.check_blocker_revisits([
            issue(2, labels=["needs-clarification"], body="just a question"),
        ])
        self.assertEqual(revisits_for(2, out), [])

    def test_prose_blocked_by_without_colon_is_ignored(self):
        # Only the structured, colon-bearing form gates a revisit; a prose
        # mention must not fire an automatic label move.
        out = check_revisits.check_blocker_revisits([
            issue(1, labels=["ready-for-work"]),
            issue(2, labels=["needs-clarification"],
                  body="this was blocked by #1 earlier"),
        ])
        self.assertEqual(revisits_for(2, out), [])

    def test_only_needs_clarification_issues_are_revisited(self):
        # A pending-approval issue whose blocker is resolved is NOT revisited —
        # the auto-revisit is scoped to needs-clarification.
        out = check_revisits.check_blocker_revisits([
            issue(1, labels=["ready-for-work"]),
            issue(2, labels=["pending-approval"], body="Blocked by: #1"),
        ])
        self.assertEqual(revisits_for(2, out), [])

    def test_parked_issue_is_not_revisited(self):
        # A parked issue is hidden from every routine; never auto-revisit it.
        out = check_revisits.check_blocker_revisits([
            issue(1, labels=["ready-for-work"]),
            issue(2, labels=["needs-clarification", "parked"],
                  body="Blocked by: #1"),
        ])
        self.assertEqual(revisits_for(2, out), [])

    def test_native_only_blocker_triggers_revisit(self):
        # Issue 2's ONLY blocker is a native GitHub relationship (#321) — no
        # `Blocked by:` text line at all. Once blocker #1 is closed (absent from
        # the open snapshot), issue 2 must still revisit. The gatekeeper SKILL
        # feeds native blockers into the snapshot's `native_blocked_by` field.
        out = check_revisits.check_blocker_revisits([
            issue(2, labels=["needs-clarification"], body="just a question",
                  native_blocked_by=[1]),
        ])
        r = revisits_for(2, out)
        self.assertEqual(len(r), 1)
        self.assertEqual(r[0]["blockers_resolved"], [1])

    def test_native_only_blocker_unresolved_does_not_revisit(self):
        # Native blocker #1 is open and unresolved -> no revisit (regression
        # guard, parity with the text-line path).
        out = check_revisits.check_blocker_revisits([
            issue(1, labels=["pending-approval"]),
            issue(2, labels=["needs-clarification"], body="",
                  native_blocked_by=[1]),
        ])
        self.assertEqual(revisits_for(2, out), [])

    def test_native_and_text_blockers_union(self):
        # Text line names #1, native relationship names #3 — the union gates the
        # revisit and both are reported once all resolve (1 ready, 3 absent).
        out = check_revisits.check_blocker_revisits([
            issue(1, labels=["ready-for-work"]),
            issue(2, labels=["needs-clarification"], body="Blocked by: #1",
                  native_blocked_by=[3]),
        ])
        r = revisits_for(2, out)
        self.assertEqual(len(r), 1)
        self.assertEqual(r[0]["blockers_resolved"], [1, 3])

    def test_native_and_text_blockers_partial_does_not_revisit(self):
        # Text blocker #1 resolved but native blocker #3 still unresolved -> stay.
        out = check_revisits.check_blocker_revisits([
            issue(1, labels=["ready-for-work"]),
            issue(3, labels=["pending-approval"]),
            issue(2, labels=["needs-clarification"], body="Blocked by: #1",
                  native_blocked_by=[3]),
        ])
        self.assertEqual(revisits_for(2, out), [])

    def test_wireframe_blocker_at_ready_for_work_does_not_revisit(self):
        # #396: a wireframe-producing blocker (`type:wireframe`) at
        # `ready-for-work` only means "approved to go draft the wireframe," not
        # "the wireframe is a distilled, closed contract" (CLAUDE.md rule #8 /
        # ui-design-process.md). The downstream issue is hard-gated on the
        # wireframe being CLOSED, so `ready-for-work` must NOT resolve it.
        out = check_revisits.check_blocker_revisits([
            issue(1, labels=["type:wireframe", "ready-for-work"]),
            issue(2, labels=["needs-clarification"], body="Blocked by: #1"),
        ])
        self.assertEqual(revisits_for(2, out), [])

    def test_wireframe_blocker_at_in_progress_does_not_revisit(self):
        # Same carve-out at `in-progress` — the wireframe still isn't distilled.
        out = check_revisits.check_blocker_revisits([
            issue(1, labels=["type:wireframe", "in-progress"]),
            issue(2, labels=["needs-clarification"], body="Blocked by: #1"),
        ])
        self.assertEqual(revisits_for(2, out), [])

    def test_wireframe_blocker_closed_revisits(self):
        # Once the wireframe blocker is closed (absent from the open snapshot),
        # its distillation contract has landed and the downstream revisits.
        out = check_revisits.check_blocker_revisits([
            issue(2, labels=["needs-clarification"], body="Blocked by: #1"),
        ])
        self.assertEqual(len(revisits_for(2, out)), 1)

    def test_wireframe_revisit_stable_across_repeat_sweeps(self):
        # #396 churn guard: re-running the sweep on the SAME unchanged snapshot
        # (wireframe blocker still at `ready-for-work`) must produce ZERO
        # revisits every time — no infinite re-fire loop.
        snapshot = [
            issue(1, labels=["type:wireframe", "ready-for-work"]),
            issue(2, labels=["needs-clarification"], body="Blocked by: #1"),
        ]
        first = check_revisits.check_blocker_revisits(snapshot)
        second = check_revisits.check_blocker_revisits(snapshot)
        self.assertEqual(revisits_for(2, first), [])
        self.assertEqual(revisits_for(2, second), [])

    def test_non_wireframe_blocker_still_revisits_at_ready_for_work(self):
        # Regression guard for #241: an ordinary (non-wireframe) blocker at
        # `ready-for-work` still resolves and revisits — the carve-out is scoped
        # strictly to `type:wireframe` blockers.
        out = check_revisits.check_blocker_revisits([
            issue(1, labels=["ready-for-work"]),
            issue(2, labels=["needs-clarification"], body="Blocked by: #1"),
        ])
        self.assertEqual(len(revisits_for(2, out)), 1)

    def test_mixed_wireframe_and_ordinary_blockers_revisit_when_both_resolved(self):
        # #396: two blockers — one `type:wireframe` (closed, absent) and one
        # ordinary at `ready-for-work`. The revisit fires once BOTH are resolved
        # under their respective rules, and both are reported.
        out = check_revisits.check_blocker_revisits([
            issue(3, labels=["ready-for-work"]),
            issue(2, labels=["needs-clarification"],
                  body="Blocked by: #1\nBlocked by: #3"),
        ])
        r = revisits_for(2, out)
        self.assertEqual(len(r), 1)
        self.assertEqual(r[0]["blockers_resolved"], [1, 3])

    def test_mixed_wireframe_open_blocks_revisit(self):
        # The wireframe blocker (#1) is still open at `ready-for-work` while the
        # ordinary blocker (#3) has resolved -> the wireframe holds the revisit.
        out = check_revisits.check_blocker_revisits([
            issue(1, labels=["type:wireframe", "ready-for-work"]),
            issue(3, labels=["ready-for-work"]),
            issue(2, labels=["needs-clarification"],
                  body="Blocked by: #1\nBlocked by: #3"),
        ])
        self.assertEqual(revisits_for(2, out), [])

    def test_process_stdin_stdout(self):
        # The script wrapper reads a snapshot on stdin and writes revisits JSON.
        payload = {"issues": [
            issue(1, labels=["ready-for-work"]),
            issue(2, labels=["needs-clarification"], body="Blocked by: #1"),
        ]}
        proc = subprocess.run(
            [sys.executable, SCRIPT], input=json.dumps(payload),
            capture_output=True, text=True)
        self.assertEqual(proc.returncode, 0, proc.stderr)
        out = json.loads(proc.stdout)
        self.assertEqual(len(out["revisits"]), 1)
        self.assertEqual(out["revisits"][0]["issue"], 2)


if __name__ == "__main__":
    unittest.main()
