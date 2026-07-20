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
            first, "<!-- pipeline-focus: v0.4 -->"
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

    def test_no_mvp_language(self):
        self.assertNotIn("MVP", self.body)

    def test_deterministic(self):
        self.assertEqual(self.body, render_dashboard.render_body(load_state()))

    def test_matches_golden(self):
        with open(GOLDEN) as fh:
            expected = fh.read()
        self.assertEqual(self.body, expected)


class TestResolveFocus(unittest.TestCase):
    """Focus precedence: explicit override (DASHBOARD_SET_FOCUS, i.e. a `/focus`
    command re-rendering) > the #193 marker > lowest version milestone with
    ready work. Setting focus via a fresh render — not a hand-edit of #193's
    body — is what keeps the stored body raw (#204 corruption)."""

    def ms(self, ready):
        return {t: {"done": 0, "ready": r, "remaining": 0}
                for t, r in ready.items()}

    def test_override_wins_over_marker(self):
        out = render_dashboard._resolve_focus(
            "v1.0", "v0.4", self.ms({"v0.4": 3, "v1.0": 1}))
        self.assertEqual(out, "v1.0")

    def test_marker_used_when_no_override(self):
        out = render_dashboard._resolve_focus(
            None, "v0.4", self.ms({"v0.4": 3}))
        self.assertEqual(out, "v0.4")

    def test_fallback_to_lowest_version_with_ready(self):
        out = render_dashboard._resolve_focus(
            None, None, self.ms({"v1.0": 2, "v0.4": 1}))
        self.assertEqual(out, "v0.4")


if __name__ == "__main__":
    unittest.main()
