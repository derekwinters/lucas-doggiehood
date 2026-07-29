"""Unit tests for the pure path/payload builders in ``set_blocker.py``.

Same split as the other deterministic skills: only the network-free helpers
are exercised here; the ``_api_request`` I/O edge is not.
"""

import os
import sys
import unittest

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

import set_blocker  # noqa: E402


class PathBuilderTests(unittest.TestCase):
    def test_blocked_by_path(self):
        self.assertEqual(
            set_blocker.blocked_by_path("derekwinters/lucas-doggiehood", 295),
            "/repos/derekwinters/lucas-doggiehood/issues/295/dependencies/blocked_by")

    def test_blocked_by_item_path_uses_db_id_not_number(self):
        # The DELETE member path is keyed by the blocker's database id, not #number.
        self.assertEqual(
            set_blocker.blocked_by_item_path("o/r", 295, 4879550551),
            "/repos/o/r/issues/295/dependencies/blocked_by/4879550551")

    def test_issue_path(self):
        self.assertEqual(set_blocker.issue_path("o/r", 360),
                         "/repos/o/r/issues/360")


class PayloadTests(unittest.TestCase):
    def test_add_payload_carries_issue_id(self):
        self.assertEqual(set_blocker.add_payload(123), {"issue_id": 123})


class RepoParseTests(unittest.TestCase):
    def test_valid_repo_passes_through(self):
        self.assertEqual(set_blocker.parse_repo("owner/name"), "owner/name")

    def test_missing_slash_rejected(self):
        with self.assertRaises(ValueError):
            set_blocker.parse_repo("noslash")

    def test_empty_half_rejected(self):
        with self.assertRaises(ValueError):
            set_blocker.parse_repo("owner/")


if __name__ == "__main__":
    unittest.main()
