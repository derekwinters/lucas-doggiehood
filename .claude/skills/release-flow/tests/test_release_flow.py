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


# --- CI-trigger selection: halt and ask, never close/reopen (#618) ---------

class CiTriggerTests(unittest.TestCase):
    def test_selects_ask_user(self):
        self.assertEqual(rf.choose_ci_trigger_action(), rf.CI_TRIGGER_ASK_USER)


class NoCloseReopenTests(unittest.TestCase):
    """#618: the release PR's state must never be toggled. These are the
    acceptance guards — they fail if close/reopen creeps back in."""

    def test_close_then_reopen_helper_is_gone(self):
        self.assertFalse(hasattr(rf, "close_then_reopen_pr"))

    def test_no_close_reopen_action_constant(self):
        self.assertFalse(hasattr(rf, "CI_TRIGGER_CLOSE_REOPEN"))

    def test_choose_ci_trigger_action_never_returns_close_reopen(self):
        self.assertNotEqual(rf.choose_ci_trigger_action(), "close_reopen")

    def test_module_source_never_sets_pr_state(self):
        # A PATCH of the PR's `state` field is the only way to close or reopen
        # it, so the source must contain no such payload at all.
        with open(rf.__file__) as handle:
            source = handle.read()
        self.assertNotIn('"state": "closed"', source)
        self.assertNotIn('"state": "open"', source)


# --- awaiting-approval runs (what the UI's "Approve and run" button releases) -

def _run(name, conclusion="action_required", run_id=1):
    return {"id": run_id, "name": name, "status": "completed",
            "conclusion": conclusion}


class RunsAwaitingApprovalTests(unittest.TestCase):
    def test_selects_only_action_required_runs(self):
        runs = [
            _run("docs-test", run_id=11),
            _run("pr-title-lint", run_id=12),
            _run("sweep", conclusion="success", run_id=13),
        ]
        self.assertEqual([r["id"] for r in rf.runs_awaiting_approval(runs)],
                         [11, 12])

    def test_empty_when_nothing_is_parked(self):
        self.assertEqual(
            rf.runs_awaiting_approval([_run("docs-test", conclusion="success")]),
            [])


class RunUrlTests(unittest.TestCase):
    def test_run_url_points_at_the_actions_run(self):
        self.assertEqual(rf.run_url("o/r", 31148250028),
                         "https://github.com/o/r/actions/runs/31148250028")


# --- the halt prompt (pure text builder) ------------------------------------

class CiPromptTests(unittest.TestCase):
    def _prompt(self):
        return rf.format_ci_prompt(
            "o/r", 634,
            [_run("docs-test", run_id=11), _run("pr-title-lint", run_id=12)])

    def test_lists_each_awaiting_run_with_its_url(self):
        prompt = self._prompt()
        self.assertIn("docs-test", prompt)
        self.assertIn(rf.run_url("o/r", 11), prompt)
        self.assertIn("pr-title-lint", prompt)
        self.assertIn(rf.run_url("o/r", 12), prompt)

    def test_offers_both_documented_choices(self):
        prompt = self._prompt()
        self.assertIn("continue", prompt)
        self.assertIn("skip", prompt)

    def test_never_suggests_closing_or_reopening_the_pr(self):
        self.assertNotIn("reopen", self._prompt().lower())

    def test_points_at_the_pr_checks_when_nothing_is_parked(self):
        prompt = rf.format_ci_prompt("o/r", 634, [])
        self.assertIn("https://github.com/o/r/pull/634/checks", prompt)


# --- parsing the user's answer ----------------------------------------------

class ParseCiAnswerTests(unittest.TestCase):
    def test_continue_words(self):
        for text in ("continue", "  Continue  ", "c", "yes", "y", "approved"):
            self.assertEqual(rf.parse_ci_answer(text), rf.CI_ANSWER_CONTINUE,
                             msg=text)

    def test_skip_words(self):
        for text in ("skip", "SKIP", " s ", "override"):
            self.assertEqual(rf.parse_ci_answer(text), rf.CI_ANSWER_SKIP,
                             msg=text)

    def test_anything_else_aborts(self):
        for text in ("", None, "maybe", "quit", "n"):
            self.assertEqual(rf.parse_ci_answer(text), rf.CI_ANSWER_ABORT,
                             msg=repr(text))


# --- check poll loop (reuses ci-watch's PASSED/FAILED/TIMEOUT shape) --------

def _check(name, status="completed", conclusion="success"):
    return {"name": name, "status": status, "conclusion": conclusion}


ALL_GREEN = [
    _check("build"),
    _check("gate-tests"),
    _check("Conventional Commits PR title"),
]

ALL_PENDING = [
    _check("build", status="in_progress", conclusion=None),
    _check("gate-tests", status="queued", conclusion=None),
    _check("Conventional Commits PR title", status="in_progress", conclusion=None),
]


# The exact check-run names GitHub reports on the release PR (from the #631
# empirical capture). The three required ones (``build``, ``gate-tests``,
# ``Conventional Commits PR title``) are surrounded by non-required noise
# checks that must be ignored.
REAL_RELEASE_PR_CHECKS = [
    _check("Conventional Commits PR title"),
    _check("Debug APK"),
    _check("Release-candidate APK"),
    _check("build"),
    _check("gate-tests"),
    _check("sweep"),
]


class RealReleasePrCheckNameTests(unittest.TestCase):
    """Regression guard for #631: ``REQUIRED_CHECKS`` must be the raw check-run
    names GitHub actually reports, or ``classify_checks`` never matches and the
    poll hangs to TIMEOUT while the checks are green."""

    def test_real_green_release_pr_classifies_as_passed(self):
        self.assertEqual(
            rf.classify_checks(REAL_RELEASE_PR_CHECKS, rf.REQUIRED_CHECKS),
            rf.PASSED)

    def test_required_checks_are_the_raw_run_names(self):
        self.assertEqual(
            set(rf.REQUIRED_CHECKS),
            {"build", "gate-tests", "Conventional Commits PR title"})


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
            _check("build"),
            _check("gate-tests", conclusion="failure"),
            _check("Conventional Commits PR title"),
        ]
        self.assertEqual(rf.classify_checks(checks, rf.REQUIRED_CHECKS), rf.FAILED)

    def test_skipped_required_counts_as_pass(self):
        checks = [
            _check("build", conclusion="skipped"),
            _check("gate-tests"),
            _check("Conventional Commits PR title"),
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
            _check("build", conclusion="failure"),
            _check("gate-tests"),
            _check("Conventional Commits PR title"),
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
