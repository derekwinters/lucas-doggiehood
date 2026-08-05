"""Unit tests for the pure logic in ``release_flow.py``.

Same split as the other deterministic pipeline skills (``set_blocker.py``,
``pipeline-gatekeeper``): only the network-free helpers are exercised here; the
``_api_request`` I/O edge and the ``main()`` orchestration seam are not.

Run from the skill folder:

    python3 -m unittest discover -s tests
"""

import os
import sys
import unittest

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

import release_flow as rf  # noqa: E402


MAIN_SHA = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"
OLD_SHA = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb"


# --- is_regenerated: base-SHA gate -----------------------------------------

class IsRegeneratedShaTests(unittest.TestCase):
    def test_false_when_base_sha_behind_live_main(self):
        pr = {"base_sha": OLD_SHA, "body": "* feat: thing (#573)\n* fix: other (#575)"}
        self.assertFalse(rf.is_regenerated(pr, [573, 575], MAIN_SHA))

    def test_true_when_base_matches_and_all_prs_present(self):
        pr = {"base_sha": MAIN_SHA, "body": "* feat: thing (#573)\n* fix: other (#575)"}
        self.assertTrue(rf.is_regenerated(pr, [573, 575], MAIN_SHA))


# --- is_regenerated: changelog-membership gate (the #573/#575 lag) ----------

class IsRegeneratedChangelogTests(unittest.TestCase):
    def test_false_when_a_pr_number_missing(self):
        # The exact regeneration-lag scenario: base is current, but #575 has not
        # landed in the changelog yet — only #573 is listed.
        pr = {"base_sha": MAIN_SHA, "body": "* feat: thing (#573)"}
        self.assertFalse(rf.is_regenerated(pr, [573, 575], MAIN_SHA))

    def test_pr_number_match_is_not_a_substring(self):
        # "#57" must not satisfy an expectation for "#573".
        pr = {"base_sha": MAIN_SHA, "body": "* feat: thing (#57)"}
        self.assertFalse(rf.is_regenerated(pr, [573], MAIN_SHA))

    def test_missing_pr_numbers_lists_the_gap(self):
        self.assertEqual(
            rf.missing_pr_numbers("* feat (#573)", [573, 575]), [575])
        self.assertEqual(
            rf.missing_pr_numbers("* feat (#573)\n* fix (#575)", [573, 575]), [])


# --- CI-trigger selection: close/reopen only, no workflow_dispatch ---------

class CiTriggerTests(unittest.TestCase):
    def test_selects_close_reopen_unconditionally(self):
        self.assertEqual(rf.choose_ci_trigger_action(), rf.CI_TRIGGER_CLOSE_REOPEN)

    def test_close_reopen_is_the_only_supported_action(self):
        # There is deliberately no workflow_dispatch branch: none of the
        # workflows that run against the release PR declare it (see SKILL.md).
        self.assertNotEqual(rf.CI_TRIGGER_CLOSE_REOPEN, "workflow_dispatch")
        self.assertEqual(rf.CI_TRIGGER_CLOSE_REOPEN, "close_reopen")


# --- check poll loop (reuses ci-watch's PASSED/FAILED/TIMEOUT shape) --------

def _check(name, status="completed", conclusion="success"):
    return {"name": name, "status": status, "conclusion": conclusion}


ALL_GREEN = [
    _check("docs-test / build"),
    _check("docs-test / gate-tests"),
    _check("pr-title-lint / lint"),
]

ALL_PENDING = [
    _check("docs-test / build", status="in_progress", conclusion=None),
    _check("docs-test / gate-tests", status="queued", conclusion=None),
    _check("pr-title-lint / lint", status="in_progress", conclusion=None),
]


class ClassifyChecksTests(unittest.TestCase):
    def test_all_required_green_is_passed(self):
        self.assertEqual(
            rf.classify_checks(ALL_GREEN, rf.REQUIRED_CHECKS), rf.PASSED)

    def test_a_required_still_running_is_pending(self):
        self.assertEqual(
            rf.classify_checks(ALL_PENDING, rf.REQUIRED_CHECKS), rf.PENDING)

    def test_a_required_missing_is_pending(self):
        self.assertEqual(
            rf.classify_checks(ALL_GREEN[:2], rf.REQUIRED_CHECKS), rf.PENDING)

    def test_a_required_failure_is_failed(self):
        checks = [
            _check("docs-test / build"),
            _check("docs-test / gate-tests", conclusion="failure"),
            _check("pr-title-lint / lint"),
        ]
        self.assertEqual(rf.classify_checks(checks, rf.REQUIRED_CHECKS), rf.FAILED)

    def test_skipped_required_counts_as_pass(self):
        checks = [
            _check("docs-test / build", conclusion="skipped"),
            _check("docs-test / gate-tests"),
            _check("pr-title-lint / lint"),
        ]
        self.assertEqual(rf.classify_checks(checks, rf.REQUIRED_CHECKS), rf.PASSED)


class PollChecksTests(unittest.TestCase):
    def test_reaches_passed_after_pending_polls(self):
        seq = [ALL_PENDING, ALL_PENDING, ALL_GREEN]
        calls = {"n": 0}

        def fetch():
            checks = seq[calls["n"]]
            calls["n"] += 1
            return checks

        result = rf.poll_checks(fetch, rf.REQUIRED_CHECKS,
                                timeout_polls=5, sleep_fn=lambda _s: None)
        self.assertEqual(result, rf.PASSED)
        self.assertEqual(calls["n"], 3)

    def test_times_out_when_never_resolves(self):
        result = rf.poll_checks(lambda: ALL_PENDING, rf.REQUIRED_CHECKS,
                                timeout_polls=4, sleep_fn=lambda _s: None)
        self.assertEqual(result, rf.TIMEOUT)

    def test_returns_failed_immediately(self):
        failed = [
            _check("docs-test / build", conclusion="failure"),
            _check("docs-test / gate-tests"),
            _check("pr-title-lint / lint"),
        ]
        result = rf.poll_checks(lambda: failed, rf.REQUIRED_CHECKS,
                                timeout_polls=4, sleep_fn=lambda _s: None)
        self.assertEqual(result, rf.FAILED)


# --- squash-merge request builder ------------------------------------------

class VersionParseTests(unittest.TestCase):
    def test_version_from_release_title(self):
        self.assertEqual(
            rf.version_from_title("chore(main): release 0.12.0"), "0.12.0")

    def test_version_from_title_rejects_non_release(self):
        with self.assertRaises(ValueError):
            rf.version_from_title("feat: something (#1)")

    def test_version_from_version_file_strips(self):
        self.assertEqual(rf.version_from_version_file("0.12.0\n"), "0.12.0")


class MergeRequestTests(unittest.TestCase):
    def test_merge_request_uses_squash_and_release_title(self):
        req = rf.build_merge_request("o/r", 557, "0.12.0")
        self.assertEqual(req["method"], "PUT")
        self.assertEqual(req["path"], "/repos/o/r/pulls/557/merge")
        self.assertEqual(req["payload"]["merge_method"], "squash")
        self.assertEqual(req["payload"]["commit_title"],
                         "chore(main): release 0.12.0")

    def test_release_commit_title(self):
        self.assertEqual(
            rf.release_commit_title("1.0.0"), "chore(main): release 1.0.0")


# --- post-merge verification (bounded poll; tagging is async) ---------------

class ReleaseCompleteTests(unittest.TestCase):
    def _state(self, tags, releases, labels):
        return {"tags": tags, "releases": releases, "labels": labels}

    def test_incomplete_when_label_not_yet_flipped(self):
        state = self._state(["v0.12.0"], ["v0.12.0"],
                            [rf.AUTORELEASE_PENDING])
        self.assertFalse(rf.is_release_complete(state, "0.12.0"))

    def test_incomplete_when_tag_missing(self):
        state = self._state([], [], [rf.AUTORELEASE_TAGGED])
        self.assertFalse(rf.is_release_complete(state, "0.12.0"))

    def test_incomplete_when_release_missing(self):
        state = self._state(["v0.12.0"], [], [rf.AUTORELEASE_TAGGED])
        self.assertFalse(rf.is_release_complete(state, "0.12.0"))

    def test_complete_when_all_present(self):
        state = self._state(["v0.12.0"], ["v0.12.0"], [rf.AUTORELEASE_TAGGED])
        self.assertTrue(rf.is_release_complete(state, "0.12.0"))


class PollVerificationTests(unittest.TestCase):
    def test_succeeds_after_label_flips(self):
        pending = {"tags": ["v0.12.0"], "releases": ["v0.12.0"],
                   "labels": [rf.AUTORELEASE_PENDING]}
        flipped = {"tags": ["v0.12.0"], "releases": ["v0.12.0"],
                   "labels": [rf.AUTORELEASE_TAGGED]}
        seq = [pending, pending, flipped]
        calls = {"n": 0}

        def fetch():
            state = seq[calls["n"]]
            calls["n"] += 1
            return state

        self.assertTrue(rf.poll_verification(
            fetch, "0.12.0", timeout_polls=5, sleep_fn=lambda _s: None))
        self.assertEqual(calls["n"], 3)

    def test_fails_when_never_flips(self):
        pending = {"tags": ["v0.12.0"], "releases": ["v0.12.0"],
                   "labels": [rf.AUTORELEASE_PENDING]}
        self.assertFalse(rf.poll_verification(
            lambda: pending, "0.12.0", timeout_polls=3, sleep_fn=lambda _s: None))


class TagNameTests(unittest.TestCase):
    def test_tag_name_prefixes_v(self):
        self.assertEqual(rf.tag_name("0.12.0"), "v0.12.0")


# --- path builders ----------------------------------------------------------

class PathBuilderTests(unittest.TestCase):
    def test_merge_path(self):
        self.assertEqual(rf.merge_path("o/r", 557),
                         "/repos/o/r/pulls/557/merge")

    def test_pr_path(self):
        self.assertEqual(rf.pr_path("o/r", 557),
                         "/repos/o/r/pulls/557")

    def test_parse_repo_valid(self):
        self.assertEqual(rf.parse_repo("owner/name"), "owner/name")

    def test_parse_repo_rejects_missing_slash(self):
        with self.assertRaises(ValueError):
            rf.parse_repo("noslash")


if __name__ == "__main__":
    unittest.main()
