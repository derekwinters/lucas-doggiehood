"""Tests for the deterministic dashboard renderer.

Run: python3 -m unittest discover -s .claude/skills/pipeline-dashboard/tests

`render_body(state)` is pure (no GitHub I/O), so it is tested against a fixed
fixture. Two kinds of check:
  * structural invariants (pie values, headers, focus marker, exclusions);
  * a byte-for-byte golden snapshot (expected_dashboard.md) that locks the
    approved format so accidental drift fails CI.
"""

import json
import os
import sys
import unittest

HERE = os.path.dirname(__file__)
sys.path.insert(0, os.path.join(HERE, os.pardir))

import render_dashboard  # noqa: E402

FIXTURE = os.path.join(HERE, "fixture_state.json")
GOLDEN = os.path.join(HERE, "expected_dashboard.md")


def load_state():
    with open(FIXTURE) as fh:
        return json.load(fh)


class TestRender(unittest.TestCase):
    def setUp(self):
        self.state = load_state()
        self.body = render_dashboard.render_body(self.state)

    def test_focus_marker_present_and_first(self):
        first = self.body.splitlines()[0]
        self.assertEqual(
            first, "<!-- pipeline-focus: 03 - Dogs & Conversations -->"
        )

    def test_pie_values(self):
        self.assertIn('"Done" : 18', self.body)
        self.assertIn('"Ready for work" : 0', self.body)
        self.assertIn('"Remaining" : 5', self.body)

    def test_pie_colors(self):
        # done=green, ready=yellow, remaining=red
        self.assertIn('"pie1": "#3fae5a"', self.body)
        self.assertIn('"pie3": "#d64545"', self.body)

    def test_complete_headline(self):
        self.assertIn("18 / 23 complete", self.body)

    def test_your_move_counts(self):
        self.assertIn("| 🆕 New ideas to `/admit` | **2** |", self.body)
        self.assertIn("| ✅ Analyses to `/approve` | **3** |", self.body)
        self.assertIn("| ❓ Questions to answer | **1** |", self.body)

    def test_sections_present(self):
        for header in (
            "## 🎯 Focus milestone",
            "## 🔀 Pull requests",
            "## 🆕 New ideas",
            "## ✅ Pending approval",
            "## ❓ Needs clarification",
            "## 📦 Other milestones",
            "### 📖 Command reference",
        ):
            self.assertIn(header, self.body)

    def test_intake_links(self):
        self.assertIn(
            "[#180](https://github.com/derekwinters/lucas-doggiehood/issues/180)",
            self.body,
        )

    def test_release_please_in_automation(self):
        self.assertIn("chore(main): release 0.3.0", self.body)

    def test_post_mvp_annotated(self):
        self.assertIn("post-MVP", self.body)

    def test_closed_milestone_excluded(self):
        # Closed milestones (100% done) must not clutter "Other milestones"
        # nor the by-milestone chart. See issue #214.
        self.assertNotIn("00 - Concepts & Core Mechanics", self.body)
        self.assertNotIn("m00", self.body)

    def test_deterministic(self):
        self.assertEqual(self.body, render_dashboard.render_body(load_state()))

    def test_matches_golden(self):
        with open(GOLDEN) as fh:
            expected = fh.read()
        self.assertEqual(self.body, expected)


if __name__ == "__main__":
    unittest.main()
