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
            "## ⏸️ Parked",
            "## ⚠️ Reconcile",
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

    def test_your_move_pr_line_has_no_release_please(self):
        # The "Your move" PR callout shows only the awaiting-merge count with
        # no release-please parenthetical (#225); the parenthetical only
        # matters at milestone close, which the pie already covers.
        pr_line = next(
            ln for ln in self.body.splitlines()
            if "PRs awaiting your merge" in ln
        )
        self.assertNotIn("release-please", pr_line)
        self.assertEqual(pr_line, "| 🔀 PRs awaiting your merge | **0** |")
        # ...but the release-please PR is still listed in the Automation section.
        self.assertIn("chore(main): release 0.3.0", self.body)

    def test_post_mvp_annotated(self):
        self.assertIn("post-MVP", self.body)

    def test_closed_milestone_excluded(self):
        # Closed milestones (100% done) must not clutter "Other milestones"
        # nor the by-milestone chart. See issue #214.
        self.assertNotIn("00 - Concepts & Core Mechanics", self.body)
        self.assertNotIn("m00", self.body)

    def test_parked_section_lists_open_parked_issues(self):
        # #249: parked issues get a read-only listing so they stay visible.
        self.assertIn("## ⏸️ Parked", self.body)
        self.assertIn(
            "[#172](https://github.com/derekwinters/lucas-doggiehood/issues/172)",
            self.body,
        )
        self.assertIn("Seasonal weather effects", self.body)

    def test_parked_issue_only_appears_in_parked_section(self):
        # #249: the Parked section is a separate listing, NOT a re-admission.
        # The parked issue must not leak into any active queue/count. It should
        # appear in the rendered body exactly once — inside the Parked section.
        self.assertEqual(self.body.count("/issues/172)"), 1)
        # Active queues/counts are unchanged from the current golden.
        self.assertIn('"Ready for work" : 0', self.body)
        self.assertIn("| 🆕 New ideas to `/admit` | **2** |", self.body)
        self.assertIn("| ✅ Analyses to `/approve` | **3** |", self.body)
        self.assertIn("| ❓ Questions to answer | **1** |", self.body)

    def test_parked_empty_shows_none_state(self):
        # #249: with no parked issues the header still renders, followed by the
        # shared empty-state line, and rendering does not raise.
        state = load_state()
        state["parked"] = []
        body = render_dashboard.render_body(state)
        self.assertIn("## ⏸️ Parked", body)
        parked_idx = body.index("## ⏸️ Parked")
        reconcile_idx = body.index("## ⚠️ Reconcile")
        self.assertIn("_None right now._", body[parked_idx:reconcile_idx])

    def test_reconcile_section_lists_flag_findings(self):
        # Merged-but-open issues are flagged for a manual close (not auto-closed
        # — #211 owns auto-close). See issue #246.
        self.assertIn("## ⚠️ Reconcile", self.body)
        self.assertIn("Merged but still open", self.body)
        self.assertIn(
            "[#56](https://github.com/derekwinters/lucas-doggiehood/issues/56)",
            self.body,
        )
        # Stretch flags surface too.
        self.assertIn("Ready-for-work with no milestone", self.body)
        self.assertIn("Prose-only dependency references", self.body)
        # Auto-fix activity note.
        self.assertIn("2 stale-label strip(s), 1 requeue(s)", self.body)

    def test_reconcile_empty_shows_clean_message(self):
        state = load_state()
        state["reconcile"] = {}
        body = render_dashboard.render_body(state)
        self.assertIn("Nothing to reconcile", body)

    def test_deterministic(self):
        self.assertEqual(self.body, render_dashboard.render_body(load_state()))

    def test_matches_golden(self):
        with open(GOLDEN) as fh:
            expected = fh.read()
        self.assertEqual(self.body, expected)


if __name__ == "__main__":
    unittest.main()
