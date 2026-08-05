"""Unit tests for the pure builders/parsers in ``milestone_ops.py``.

Same split as the other deterministic skills (``issue-blockers``): only the
network-free helpers are exercised here — the ``_api_request`` I/O edge and the
subcommand handlers that call it are not.
"""

import os
import sys
import unittest

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

import milestone_ops  # noqa: E402


# A mocked ``GET /milestones?state=all`` payload — two milestones, one whose
# title (``v0.11``) resolves to a number (21) that is NOT its version string.
MILESTONES = [
    {"number": 21, "state": "closed", "title": "v0.11",
     "open_issues": 0, "closed_issues": 7},
    {"number": 22, "state": "open", "title": "v0.12",
     "open_issues": 3, "closed_issues": 1},
]


class ListFormatterTests(unittest.TestCase):
    def test_one_row_per_milestone(self):
        rows = milestone_ops.format_list(MILESTONES).splitlines()
        self.assertEqual(len(rows), len(MILESTONES))

    def test_row_carries_number_state_title_and_counts(self):
        row = milestone_ops.format_row(MILESTONES[1])
        self.assertIn("22", row)
        self.assertIn("open", row)
        self.assertIn("v0.12", row)
        self.assertIn("3", row)   # open_issues
        self.assertIn("1", row)   # closed_issues


class ResolveNumberTests(unittest.TestCase):
    def test_exact_title_match_returns_number(self):
        # "v0.11" must resolve to milestone number 21, not the version string.
        self.assertEqual(milestone_ops.resolve_number(MILESTONES, "v0.11"), 21)

    def test_bare_numeric_passes_straight_through(self):
        # A bare number is used unresolved, even if no such milestone is present.
        self.assertEqual(milestone_ops.resolve_number(MILESTONES, "999"), 999)

    def test_unknown_title_raises(self):
        with self.assertRaises(ValueError):
            milestone_ops.resolve_number(MILESTONES, "v9.99")


class PathBuilderTests(unittest.TestCase):
    def test_milestones_path(self):
        self.assertEqual(
            milestone_ops.milestones_path("o/r"),
            "/repos/o/r/milestones?state=all&per_page=100")

    def test_milestone_issues_path(self):
        self.assertEqual(
            milestone_ops.milestone_issues_path("o/r", 22),
            "/repos/o/r/issues?milestone=22&state=open")

    def test_milestone_item_path(self):
        self.assertEqual(
            milestone_ops.milestone_item_path("o/r", 22),
            "/repos/o/r/milestones/22")


class PatchPayloadTests(unittest.TestCase):
    def test_close_payload(self):
        self.assertEqual(milestone_ops.close_payload(), {"state": "closed"})

    def test_reopen_payload(self):
        self.assertEqual(milestone_ops.reopen_payload(), {"state": "open"})


class CloseGuardTests(unittest.TestCase):
    def test_refuses_when_open_issues_and_no_force(self):
        self.assertFalse(milestone_ops.should_close(open_count=1, force=False))

    def test_proceeds_when_forced_despite_open_issues(self):
        self.assertTrue(milestone_ops.should_close(open_count=3, force=True))

    def test_proceeds_when_no_open_issues(self):
        self.assertTrue(milestone_ops.should_close(open_count=0, force=False))


class RepoParseTests(unittest.TestCase):
    def test_valid_repo_passes_through(self):
        self.assertEqual(milestone_ops.parse_repo("owner/name"), "owner/name")

    def test_missing_slash_rejected(self):
        with self.assertRaises(ValueError):
            milestone_ops.parse_repo("noslash")

    def test_empty_half_rejected(self):
        with self.assertRaises(ValueError):
            milestone_ops.parse_repo("owner/")


if __name__ == "__main__":
    unittest.main()
