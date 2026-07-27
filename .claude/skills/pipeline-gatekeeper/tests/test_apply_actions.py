"""Tests for the apply layer (issue #319).

`apply_actions.py` turns one `parse_commands` action (or skip) into the
concrete write instructions the comment-triggered workflow issues: the merged
label set to PATCH (PATCH replaces the whole label list, so it must be
computed against the issue's current labels), the acknowledgment text
(rendered from `parse_commands.MENUS`), and the reactions to add. Pure
functions, no GitHub I/O — see ../apply_actions.py.

Run: python3 -m unittest discover -s .claude/skills/pipeline-gatekeeper/tests
"""

import os
import sys
import unittest

sys.path.insert(0, os.path.join(os.path.dirname(__file__), os.pardir))
import apply_actions  # noqa: E402


class TestMergeLabels(unittest.TestCase):
    def test_merge_removes_and_adds(self):
        current = ["pending-approval", "type:task", "area:ai"]
        action = {"add_labels": ["ready-for-work"],
                  "remove_labels": ["pending-approval"]}
        merged = apply_actions.merge_labels(current, action)
        self.assertEqual(merged, ["type:task", "area:ai", "ready-for-work"])

    def test_merge_is_a_no_op_when_no_changes(self):
        current = ["ai-triage", "type:bug"]
        action = {"add_labels": [], "remove_labels": []}
        self.assertEqual(apply_actions.merge_labels(current, action), current)

    def test_merge_does_not_duplicate_an_already_present_label(self):
        current = ["ready-for-work", "type:task"]
        action = {"add_labels": ["ready-for-work"], "remove_labels": []}
        merged = apply_actions.merge_labels(current, action)
        self.assertEqual(merged, ["ready-for-work", "type:task"])

    def test_merge_ignores_a_remove_for_a_label_not_present(self):
        current = ["type:task"]
        action = {"add_labels": [], "remove_labels": ["ai-triage"]}
        self.assertEqual(apply_actions.merge_labels(current, action),
                          ["type:task"])


class TestRenderAck(unittest.TestCase):
    def test_renders_the_menu_for_the_action(self):
        action = {"menu": "ready-for-work"}
        text = apply_actions.render_ack(action)
        self.assertIn("Your move", text)
        self.assertIn("/focus", text)

    def test_no_menu_renders_nothing(self):
        # e.g. a bare /focus or /cap on the dashboard issue is applied
        # silently — no ack comment, so the dashboard body isn't churned.
        self.assertIsNone(apply_actions.render_ack({"menu": None}))


class TestReactionsFor(unittest.TestCase):
    def test_honored_action_gets_thumbsup_and_watermark(self):
        reactions = apply_actions.reactions_for({"menu": "ready-for-work"})
        self.assertIn("+1", reactions)
        self.assertIn("eyes", reactions)


class TestNoMilestoneSetOnApprove(unittest.TestCase):
    def test_approve_action_carries_no_milestone_write(self):
        # Part A (#319): the milestone is already set by analysis; approve's
        # action must never carry a set_milestone the apply layer would write.
        action = {"commands": ["approve"], "add_labels": ["ready-for-work"],
                  "remove_labels": ["pending-approval"], "set_milestone": None,
                  "menu": "ready-for-work"}
        self.assertIsNone(apply_actions.milestone_write_for(action))

    def test_milestone_command_does_carry_a_milestone_write(self):
        action = {"commands": ["milestone"], "add_labels": [],
                  "remove_labels": [], "set_milestone": "07 - Polish & Onboarding",
                  "menu": "milestone"}
        self.assertEqual(apply_actions.milestone_write_for(action),
                          "07 - Polish & Onboarding")


class TestRenderSkipAck(unittest.TestCase):
    def test_approve_no_milestone_skip_gets_a_hand_back(self):
        skip = {"issue": 181, "comment_id": 8,
                "reason": "approve-no-milestone", "menu": "which-milestone"}
        text = apply_actions.render_skip_ack(skip)
        self.assertIn("181", text)
        self.assertIn("/milestone", text)

    def test_other_skip_reasons_get_no_ack(self):
        for reason in ("not-owner", "parked-ignored", "no-op",
                       "focus-no-match", "milestone-no-match", "cap-invalid"):
            skip = {"issue": 181, "comment_id": 8, "reason": reason}
            self.assertIsNone(apply_actions.render_skip_ack(skip), reason)


if __name__ == "__main__":
    unittest.main()
