"""Tests for the reactive-triage partial-write recovery detection (issue #582).

Run: python3 -m unittest discover -s .claude/skills/triage-issue/tests
Pure fixture-in / decision-out; no GitHub access needed. See ../triage_repair.py.

These unit-test the re-fire idempotency detection the same way
`pipeline-reconcile/reconcile.py`'s rules are tested: given the issue's labels,
its analysis-comment timestamps, and the most-recent re-admission timestamp,
decide whether a re-fire is REPAIRING a prior partial write (apply just the
missing label move, do NOT repost the analysis) or triaging fresh.
"""

import os
import sys
import unittest

sys.path.insert(0, os.path.join(os.path.dirname(__file__), os.pardir))
import triage_repair  # noqa: E402  (imported after sys.path tweak)


READMIT = "2026-08-05T04:00:00Z"
FRESH = "2026-08-05T05:00:00Z"   # after the re-admission
STALE = "2026-08-05T03:00:00Z"   # before the re-admission (e.g. pre-/redo)


class TestAnalysisCommentTimes(unittest.TestCase):
    """`analysis_comment_times(comments)`: keep the created_at of exactly the
    comments whose body matches the triage analysis signature (reusing
    reconcile.has_analysis_signature)."""

    def test_filters_to_signature_comments(self):
        comments = [
            {"body": "/admit", "created_at": "2026-08-05T03:00:00Z"},
            {"body": "## Build checklist\n- [ ] x", "created_at": FRESH},
            {"body": "Your move: `/park`", "created_at": "2026-08-05T05:01:00Z"},
        ]
        self.assertEqual(triage_repair.analysis_comment_times(comments), [FRESH])

    def test_needs_marker_counts_as_analysis(self):
        comments = [{"body": "❓ Needs from Derek/Lucas: which layout?",
                     "created_at": FRESH}]
        self.assertEqual(triage_repair.analysis_comment_times(comments), [FRESH])

    def test_bolded_needs_marker_counts_as_analysis(self):
        # Issue #654: triage's real ask-route comments BOLD the marker, and the
        # ask route never emits a `## Build checklist`, so the emphasis-tolerant
        # marker match is the only thing that sees them.
        comments = [{"body": "❓ **Needs from Derek/Lucas:** the dialogue lines.",
                     "created_at": FRESH}]
        self.assertEqual(triage_repair.analysis_comment_times(comments), [FRESH])

    def test_heading_form_needs_marker_counts_as_analysis(self):
        # Issue #710: triage also writes the marker as a HEADING with no colon
        # (`## ❓ Needs from Derek/Lucas`) — the shape on #683 and #684. The
        # repair path shares reconcile's recognizer, so a re-fire landing on one
        # of those comments must repair the label move, not repost a duplicate.
        comments = [{"body": "## ❓ Needs from Derek/Lucas\n\n**Primary…**",
                     "created_at": FRESH}]
        self.assertEqual(triage_repair.analysis_comment_times(comments), [FRESH])

    def test_heading_form_re_fire_is_repair_end_to_end(self):
        comments = [
            {"body": "/admit", "created_at": READMIT},
            {"body": "## Triage — stopping here\n\n## ❓ Needs from Derek/Lucas"
                     "\n\nWhich gate do you want?", "created_at": FRESH},
        ]
        times = triage_repair.analysis_comment_times(comments)
        self.assertTrue(triage_repair.is_partial_write_repair(
            ["ai-triage"], times, READMIT))

    def test_no_analysis_comments_empty(self):
        comments = [{"body": "/admit", "created_at": READMIT},
                    {"body": "LGTM", "created_at": FRESH}]
        self.assertEqual(triage_repair.analysis_comment_times(comments), [])

    def test_empty_input(self):
        self.assertEqual(triage_repair.analysis_comment_times([]), [])


class TestIsPartialWriteRepair(unittest.TestCase):
    """`is_partial_write_repair(labels, analysis_times, readmit_time)`: True iff
    a prior run already posted THIS re-admission's analysis but never completed
    the label move — so the re-fire must repair (label move only), not repost."""

    def test_fresh_analysis_no_handback_is_repair(self):
        self.assertTrue(triage_repair.is_partial_write_repair(
            ["ai-triage"], [FRESH], READMIT))

    def test_handback_label_present_is_not_repair(self):
        # Already handed back (label move completed) -> nothing to repair.
        for state in ("pending-approval", "needs-clarification"):
            self.assertFalse(triage_repair.is_partial_write_repair(
                [state], [FRESH], READMIT), state)

    def test_no_analysis_comment_is_not_repair(self):
        # Nothing posted yet -> a normal fresh triage, not a repair.
        self.assertFalse(triage_repair.is_partial_write_repair(
            ["ai-triage"], [], READMIT))

    def test_stale_analysis_is_not_repair(self):
        # The only analysis predates the re-admission (e.g. a /redo issued after
        # an earlier analysis) -> must re-triage fresh, not repair the stale one.
        self.assertFalse(triage_repair.is_partial_write_repair(
            ["ai-triage"], [STALE], READMIT))

    def test_mixed_stale_and_fresh_is_repair(self):
        self.assertTrue(triage_repair.is_partial_write_repair(
            ["ai-triage"], [STALE, FRESH], READMIT))

    def test_analysis_exactly_at_readmit_is_repair(self):
        # A comment stamped at the re-admission instant counts as belonging to
        # this admission (inclusive boundary).
        self.assertTrue(triage_repair.is_partial_write_repair(
            ["ai-triage"], [READMIT], READMIT))

    def test_none_timestamps_ignored(self):
        self.assertFalse(triage_repair.is_partial_write_repair(
            ["ai-triage"], [None], READMIT))

    def test_bolded_analysis_re_fire_is_repair_end_to_end(self):
        # Issue #654, the second-order effect: a re-fire landing on an issue that
        # already carries a BOLDED analysis and no hand-back label must repair
        # (apply just the missing label move) instead of reposting a near-
        # duplicate analysis — which is what filled #100's thread. Exercised
        # through the real comment list, not pre-filtered timestamps.
        comments = [
            {"body": "/admit", "created_at": READMIT},
            {"body": "❓ **Needs from Derek/Lucas:** the quest dialogue lines.",
             "created_at": FRESH},
        ]
        times = triage_repair.analysis_comment_times(comments)
        self.assertTrue(triage_repair.is_partial_write_repair(
            ["ai-triage"], times, READMIT))


if __name__ == "__main__":
    unittest.main()
