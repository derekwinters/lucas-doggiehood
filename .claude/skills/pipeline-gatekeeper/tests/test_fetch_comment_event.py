"""Tests for the per-issue fetch layer (issue #319).

`fetch_comment_event.build_snapshot` turns one raw GitHub `issue_comment`
webhook event payload into the single-issue, single-comment snapshot that
`parse_commands.process` already consumes as a one-element `issues` list — no
GitHub API round-trip is needed, since the event itself carries everything.
Pure function, no GitHub I/O; see ../fetch_comment_event.py.

Run: python3 -m unittest discover -s .claude/skills/pipeline-gatekeeper/tests
"""

import os
import sys
import unittest

sys.path.insert(0, os.path.join(os.path.dirname(__file__), os.pardir))
import fetch_comment_event as fce  # noqa: E402
import parse_commands  # noqa: E402


def make_event(issue_overrides=None, comment_overrides=None):
    issue = {
        "number": 181,
        "labels": [{"name": "pending-approval"}],
        "milestone": None,
    }
    issue.update(issue_overrides or {})
    comment = {
        "id": 7,
        "body": "/approve",
        "user": {"login": "derekwinters", "type": "User"},
    }
    comment.update(comment_overrides or {})
    return {"action": "created", "issue": issue, "comment": comment}


class TestBuildSnapshot(unittest.TestCase):
    def test_builds_one_element_issues_snapshot(self):
        event = make_event()
        snap, skip = fce.build_snapshot(
            event, repo_owner="derekwinters",
            milestones=["04 - Quests & Economy"])
        self.assertIsNone(skip)
        self.assertEqual(snap["repo_owner"], "derekwinters")
        self.assertEqual(snap["milestones"], ["04 - Quests & Economy"])
        self.assertEqual(len(snap["issues"]), 1)
        i = snap["issues"][0]
        self.assertEqual(i["number"], 181)
        self.assertEqual(i["labels"], ["pending-approval"])
        self.assertFalse(i["is_epic"])
        self.assertFalse(i["is_dashboard"])
        self.assertIsNone(i["milestone"])
        self.assertEqual(i["comments"], [
            {"id": 7, "author": "derekwinters", "body": "/approve",
             "processed": False},
        ])

    def test_pr_comment_is_skipped(self):
        # issue_comment fires for PRs too (a PR carries a `pull_request` key
        # in its `issue` object) — these must never reach the parser.
        event = make_event(issue_overrides={"pull_request": {"url": "x"}})
        snap, skip = fce.build_snapshot(event, repo_owner="derekwinters")
        self.assertIsNone(snap)
        self.assertEqual(skip, "pr-comment")

    def test_bot_comment_is_skipped(self):
        # Defense-in-depth against the ack comment ever looping back in
        # (belt-and-suspenders on top of GITHUB_TOKEN's own no-recurse guard).
        event = make_event(comment_overrides={
            "user": {"login": "github-actions[bot]", "type": "Bot"}})
        snap, skip = fce.build_snapshot(event, repo_owner="derekwinters")
        self.assertIsNone(snap)
        self.assertEqual(skip, "bot-comment")

    def test_bot_comment_skipped_by_login_suffix_even_if_type_missing(self):
        event = make_event(comment_overrides={
            "user": {"login": "some-app[bot]"}})
        snap, skip = fce.build_snapshot(event, repo_owner="derekwinters")
        self.assertIsNone(snap)
        self.assertEqual(skip, "bot-comment")

    def test_epic_label_sets_is_epic(self):
        event = make_event(issue_overrides={
            "labels": [{"name": "type:epic"}]})
        snap, _ = fce.build_snapshot(event, repo_owner="derekwinters")
        self.assertTrue(snap["issues"][0]["is_epic"])

    def test_dashboard_issue_number_sets_is_dashboard(self):
        event = make_event(issue_overrides={"number": 193, "labels": []})
        snap, _ = fce.build_snapshot(event, repo_owner="derekwinters")
        self.assertTrue(snap["issues"][0]["is_dashboard"])

    def test_current_milestone_carried_as_title(self):
        event = make_event(issue_overrides={
            "milestone": {"title": "04 - Quests & Economy"}})
        snap, _ = fce.build_snapshot(event, repo_owner="derekwinters")
        self.assertEqual(snap["issues"][0]["milestone"],
                          "04 - Quests & Economy")

    def test_snapshot_feeds_parse_commands_equal_to_hand_authored_fixture(self):
        # Verify (per the issue's checklist): parse output on the built
        # snapshot equals the equivalent hand-authored fixture.
        event = make_event(issue_overrides={
            "milestone": {"title": "04 - Quests & Economy"}})
        snap, skip = fce.build_snapshot(
            event, repo_owner="derekwinters",
            milestones=["04 - Quests & Economy"])
        self.assertIsNone(skip)

        hand_fixture = {
            "repo_owner": "derekwinters",
            "milestones": ["04 - Quests & Economy"],
            "issues": [{
                "number": 181,
                "labels": ["pending-approval"],
                "is_epic": False,
                "is_dashboard": False,
                "milestone": "04 - Quests & Economy",
                "comments": [{"id": 7, "author": "derekwinters",
                              "body": "/approve", "processed": False}],
            }],
        }
        self.assertEqual(parse_commands.process(snap),
                          parse_commands.process(hand_fixture))


if __name__ == "__main__":
    unittest.main()
