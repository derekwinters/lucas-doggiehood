"""Unit tests for the EditMode results gate (issue #534 / #517 CI flake).

`game-ci/unity-test-runner` sometimes returns a nonzero docker exit code while
the Unity editor tears down (license return / batch-mode shutdown) *after* a
fully green EditMode run — the results XML is written and every test-case reads
`result="Passed"`, yet the step still fails. That flaked four runs in a row on
PR #534, blocking a merge on a passing suite.

`verify_editmode_results.py` re-derives the real verdict from the NUnit3
results XML the runner leaves behind, so a clean teardown-only flake no longer
fails the required check. Crucially it still upholds the #163 invariant — the
gate can only go green when the suite *actually ran* (a results file exists with
`total > 0` and no failures); a run that died before producing results, or one
with a real test failure, still fails.

These tests pin the two pure functions the gate is built from: `parse_run`
(reading one NUnit3 test-run document) and `assess` (the pass/fail decision over
the parsed runs).
"""

import os
import sys
import tempfile
import unittest

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

from verify_editmode_results import (  # noqa: E402
    MalformedResults,
    assess,
    evaluate_directory,
    parse_run,
)


def _run_xml(total, passed, failed, result, cases=""):
    return (
        '<?xml version="1.0" encoding="utf-8"?>\n'
        '<test-run id="2" testcasecount="{total}" result="{result}" '
        'total="{total}" passed="{passed}" failed="{failed}" '
        'inconclusive="0" skipped="0" asserts="0">{cases}</test-run>'
    ).format(total=total, passed=passed, failed=failed, result=result, cases=cases)


_FAILED_CASE = (
    '<test-case id="9" name="Boom" fullname="N.T.Boom" result="Failed">'
    "<failure><message>nope</message></failure></test-case>"
)


class ParseRunTests(unittest.TestCase):
    def test_reads_counts_and_result_from_all_pass_run(self):
        summary = parse_run(_run_xml(839, 839, 0, "Passed"))
        self.assertEqual(summary.total, 839)
        self.assertEqual(summary.failed, 0)
        self.assertEqual(summary.result, "Passed")
        self.assertEqual(summary.failed_names, [])

    def test_collects_failed_case_names(self):
        summary = parse_run(_run_xml(3, 2, 1, "Failed", cases=_FAILED_CASE))
        self.assertEqual(summary.failed, 1)
        self.assertEqual(summary.failed_names, ["N.T.Boom"])

    def test_rejects_non_test_run_root(self):
        with self.assertRaises(MalformedResults):
            parse_run('<?xml version="1.0"?><coverage/>')

    def test_rejects_malformed_xml(self):
        with self.assertRaises(MalformedResults):
            parse_run("<test-run not closed")


class AssessTests(unittest.TestCase):
    def test_all_pass_single_run_is_ok(self):
        verdict = assess([parse_run(_run_xml(839, 839, 0, "Passed"))])
        self.assertTrue(verdict.ok)
        self.assertEqual(verdict.total, 839)
        self.assertEqual(verdict.failed, 0)

    def test_no_runs_is_not_ok(self):
        # Suite never produced results (died before/at startup) — #163: a
        # required check must not go green without the suite actually running.
        verdict = assess([])
        self.assertFalse(verdict.ok)

    def test_zero_total_is_not_ok(self):
        verdict = assess([parse_run(_run_xml(0, 0, 0, "Passed"))])
        self.assertFalse(verdict.ok)

    def test_any_failure_is_not_ok(self):
        verdict = assess([parse_run(_run_xml(3, 2, 1, "Failed", cases=_FAILED_CASE))])
        self.assertFalse(verdict.ok)
        self.assertIn("N.T.Boom", verdict.reason)

    def test_result_failed_attr_blocks_even_with_zero_failed_count(self):
        # Defensive: trust an explicit Failed verdict even if the count says 0.
        verdict = assess([parse_run(_run_xml(5, 5, 0, "Failed"))])
        self.assertFalse(verdict.ok)

    def test_aggregates_across_multiple_runs(self):
        ok_run = parse_run(_run_xml(10, 10, 0, "Passed"))
        bad_run = parse_run(_run_xml(2, 1, 1, "Failed", cases=_FAILED_CASE))
        self.assertTrue(assess([ok_run]).ok)
        self.assertFalse(assess([ok_run, bad_run]).ok)


class EvaluateDirectoryTests(unittest.TestCase):
    def test_green_directory_passes(self):
        with tempfile.TemporaryDirectory() as d:
            with open(os.path.join(d, "editmode-results.xml"), "w") as f:
                f.write(_run_xml(839, 839, 0, "Passed"))
            verdict = evaluate_directory(d)
            self.assertTrue(verdict.ok)
            self.assertEqual(verdict.total, 839)

    def test_missing_directory_fails(self):
        verdict = evaluate_directory("/no/such/dir/really")
        self.assertFalse(verdict.ok)

    def test_directory_without_results_fails(self):
        with tempfile.TemporaryDirectory() as d:
            # A coverage-only artifact dir with no test-run document.
            with open(os.path.join(d, "coverage.xml"), "w") as f:
                f.write("<coverage/>")
            verdict = evaluate_directory(d)
            self.assertFalse(verdict.ok)

    def test_directory_with_failing_results_fails(self):
        with tempfile.TemporaryDirectory() as d:
            with open(os.path.join(d, "editmode-results.xml"), "w") as f:
                f.write(_run_xml(3, 2, 1, "Failed", cases=_FAILED_CASE))
            verdict = evaluate_directory(d)
            self.assertFalse(verdict.ok)
            self.assertIn("N.T.Boom", verdict.reason)


if __name__ == "__main__":
    unittest.main()
