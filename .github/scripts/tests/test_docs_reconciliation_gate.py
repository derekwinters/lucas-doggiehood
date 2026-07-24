"""Unit tests for the docs-reconciliation gate decision (issue #254).

The gate's decision is a pure function of three inputs — does the PR touch
docs, is it release-please's release PR, and is the `skip-docs` label present —
plus a short *live-label grace poll* that absorbs the `opened`/`labeled`
trigger race that produced the transient red on every skip-docs PR.

These tests pin the full decision matrix and, crucially, prove that a code-only
PR whose `skip-docs` label lands a moment after `opened` is observed by the
SAME run (no failing run is ever produced).
"""

import os
import sys
import unittest

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

from docs_reconciliation_gate import SKIP_LABEL, evaluate  # noqa: E402


class FakeClock:
    """Deterministic monotonic clock; sleep() just advances virtual time."""

    def __init__(self):
        self.t = 0.0

    def now(self):
        return self.t

    def sleep(self, seconds):
        self.t += seconds


class ScriptedFetcher:
    """Returns a queued label list per call; repeats the last once exhausted."""

    def __init__(self, sequence):
        self._sequence = list(sequence)
        self.calls = 0

    def __call__(self):
        self.calls += 1
        idx = min(self.calls - 1, len(self._sequence) - 1)
        return list(self._sequence[idx])


class DocsGateDecisionTests(unittest.TestCase):
    # --- Full decision matrix (semantics preserved) ---

    def test_docs_change_passes(self):
        result = evaluate(docs_changed=True, is_release=False, skip_present=False)
        self.assertTrue(result.passed)

    def test_release_pr_passes(self):
        # release-please's mechanical release PR stays exempt.
        result = evaluate(docs_changed=False, is_release=True, skip_present=False)
        self.assertTrue(result.passed)

    def test_code_only_with_skip_label_in_payload_passes(self):
        result = evaluate(docs_changed=False, is_release=False, skip_present=True)
        self.assertTrue(result.passed)

    def test_code_only_without_docs_or_skip_fails(self):
        # Preserved gate semantics: no docs change and no skip-docs => fail.
        result = evaluate(docs_changed=False, is_release=False, skip_present=False)
        self.assertFalse(result.passed)

    # --- The #254 fix: live-label grace poll absorbs the opened/labeled race ---

    def test_skip_label_landing_during_grace_window_passes_no_transient_red(self):
        # The `opened` payload had no label ([]) but pipeline-dev applies
        # skip-docs a moment later; the same run must observe it and pass.
        clock = FakeClock()
        fetcher = ScriptedFetcher([[], [], [SKIP_LABEL]])
        result = evaluate(
            docs_changed=False,
            is_release=False,
            skip_present=False,
            fetch_live_labels=fetcher,
            grace_seconds=45.0,
            poll_interval=5.0,
            clock=clock.now,
            sleep=clock.sleep,
        )
        self.assertTrue(result.passed)
        self.assertGreaterEqual(fetcher.calls, 2)

    def test_no_label_within_grace_window_still_fails(self):
        # A genuine code-only PR that never gets docs or skip-docs still fails,
        # even with the grace poll — and the poll terminates.
        clock = FakeClock()
        fetcher = ScriptedFetcher([[]])
        result = evaluate(
            docs_changed=False,
            is_release=False,
            skip_present=False,
            fetch_live_labels=fetcher,
            grace_seconds=30.0,
            poll_interval=5.0,
            clock=clock.now,
            sleep=clock.sleep,
        )
        self.assertFalse(result.passed)
        self.assertLessEqual(clock.now(), 30.0 + 5.0)

    def test_release_exemption_wins_before_any_polling(self):
        # A release PR never polls (fetcher must not be called).
        fetcher = ScriptedFetcher([[]])
        result = evaluate(
            docs_changed=False,
            is_release=True,
            skip_present=False,
            fetch_live_labels=fetcher,
            grace_seconds=45.0,
        )
        self.assertTrue(result.passed)
        self.assertEqual(fetcher.calls, 0)


if __name__ == "__main__":
    unittest.main()
