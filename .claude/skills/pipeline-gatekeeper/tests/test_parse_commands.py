"""Tests for the gatekeeper command parser (pipeline-gatekeeper).

Run: python3 -m unittest discover -s .claude/skills/pipeline-gatekeeper/tests
The parser is deterministic and pure (JSON in -> JSON out), so it needs no
GitHub access to test. See ../parse_commands.py.
"""

import json
import os
import subprocess
import sys
import unittest

SCRIPT = os.path.join(os.path.dirname(__file__), os.pardir, "parse_commands.py")


def run(payload):
    proc = subprocess.run(
        [sys.executable, SCRIPT],
        input=json.dumps(payload),
        capture_output=True,
        text=True,
    )
    assert proc.returncode == 0, proc.stderr
    return json.loads(proc.stdout)


def base_issue(**kw):
    issue = {
        "number": 100,
        "labels": [],
        "is_epic": False,
        "is_dashboard": False,
        "milestone": None,
        "proposed_milestone": None,
        "comments": [],
    }
    issue.update(kw)
    return issue


def payload(issues, **kw):
    p = {
        "repo_owner": "derekwinters",
        "milestones": [
            "03 - Dogs & Conversations",
            "04 - Quests & Economy",
            "07 - Polish & Onboarding",
        ],
        "issues": issues,
    }
    p.update(kw)
    return p


def comment(body, author="derekwinters", cid=1, processed=False):
    return {"id": cid, "author": author, "body": body, "processed": processed}


class TestParseCommands(unittest.TestCase):
    def actions_for(self, issue_number, out):
        return [a for a in out["actions"] if a["issue"] == issue_number]

    def test_approve_by_owner(self):
        out = run(payload([
            base_issue(number=181, labels=["pending-approval"],
                       proposed_milestone="07 - Polish & Onboarding",
                       comments=[comment("looks good /approve", cid=7)]),
        ]))
        acts = self.actions_for(181, out)
        self.assertEqual(len(acts), 1)
        a = acts[0]
        self.assertIn("approve", a["commands"])
        self.assertIn("ready-for-work", a["add_labels"])
        self.assertIn("pending-approval", a["remove_labels"])
        # ready-for-work ⇒ has milestone (#247): the resolved milestone rides
        # along on the action so the move is never milestone-less.
        self.assertEqual(a["set_milestone"], "07 - Polish & Onboarding")
        self.assertEqual(a["react"], 7)

    def test_non_owner_is_noop(self):
        out = run(payload([
            base_issue(number=181, labels=["pending-approval"],
                       comments=[comment("/approve", author="randouser", cid=9)]),
        ]))
        self.assertEqual(self.actions_for(181, out), [])
        self.assertTrue(any(s.get("comment_id") == 9 for s in out["skipped"]))

    def test_watermarked_comment_is_idempotent(self):
        out = run(payload([
            base_issue(number=181, labels=["pending-approval"],
                       comments=[comment("/approve", cid=9, processed=True)]),
        ]))
        self.assertEqual(self.actions_for(181, out), [])

    def test_epic_and_dashboard_skipped(self):
        out = run(payload([
            base_issue(number=191, is_epic=True,
                       comments=[comment("/admit", cid=1)]),
            base_issue(number=193, is_dashboard=True,
                       comments=[comment("/admit", cid=2)]),
        ]))
        self.assertEqual(out["actions"], [])

    def test_admit_adds_triage(self):
        out = run(payload([
            base_issue(number=180, comments=[comment("/admit", cid=3)]),
        ]))
        a = self.actions_for(180, out)[0]
        self.assertIn("ai-triage", a["add_labels"])

    def test_park_and_unpark(self):
        out = run(payload([
            base_issue(number=180, comments=[comment("/park", cid=4)]),
            base_issue(number=182, labels=["parked"],
                       comments=[comment("/unpark", cid=5)]),
        ]))
        self.assertIn("parked", self.actions_for(180, out)[0]["add_labels"])
        self.assertIn("parked", self.actions_for(182, out)[0]["remove_labels"])

    def test_parked_issue_ignores_non_unpark(self):
        out = run(payload([
            base_issue(number=182, labels=["parked"],
                       comments=[comment("/approve", cid=6)]),
        ]))
        self.assertEqual(self.actions_for(182, out), [])

    def test_milestone_matches_number_prefix(self):
        out = run(payload([
            base_issue(number=181, labels=["pending-approval"],
                       comments=[comment("/milestone 04", cid=8)]),
        ]))
        a = self.actions_for(181, out)[0]
        self.assertEqual(a["set_milestone"], "04 - Quests & Economy")

    def test_approve_with_inline_milestone(self):
        out = run(payload([
            base_issue(number=181, labels=["pending-approval"],
                       comments=[comment("/approve\n/milestone 07", cid=8)]),
        ]))
        a = self.actions_for(181, out)[0]
        self.assertIn("ready-for-work", a["add_labels"])
        self.assertEqual(a["set_milestone"], "07 - Polish & Onboarding")

    def test_focus_sets_focus_not_labels(self):
        out = run(payload([
            base_issue(number=185, comments=[comment("/focus 04", cid=8)]),
        ]))
        a = self.actions_for(185, out)[0]
        self.assertEqual(a["set_focus"], "04 - Quests & Economy")
        self.assertEqual(a["add_labels"], [])

    def test_revise_routes_back_to_analysis(self):
        out = run(payload([
            base_issue(number=181, labels=["pending-approval"],
                       comments=[comment("/revise please add camera rotation", cid=8)]),
        ]))
        a = self.actions_for(181, out)[0]
        self.assertIn("ai-triage", a["add_labels"])
        self.assertIn("pending-approval", a["remove_labels"])
        self.assertEqual(a["revise_notes"], "please add camera rotation")

    def test_propose_flag(self):
        out = run(payload([
            base_issue(number=185, labels=["needs-clarification"],
                       comments=[comment("/propose", cid=8)]),
        ]))
        a = self.actions_for(185, out)[0]
        self.assertTrue(a["propose"])
        self.assertIn("ai-triage", a["add_labels"])

    def test_url_slash_does_not_trigger(self):
        out = run(payload([
            base_issue(number=180,
                       comments=[comment("see http://x/approve/foo", cid=8)]),
        ]))
        self.assertEqual(self.actions_for(180, out), [])

    def test_focus_honored_on_dashboard_issue(self):
        # /focus IS honored on the dashboard issue (the UI directs the owner
        # to comment it there — issue #204); only set_focus survives.
        out = run(payload([
            base_issue(number=193, is_dashboard=True,
                       comments=[comment("/focus 04", cid=8)]),
        ]))
        acts = self.actions_for(193, out)
        self.assertEqual(len(acts), 1)
        a = acts[0]
        self.assertEqual(a["set_focus"], "04 - Quests & Economy")
        self.assertEqual(a["add_labels"], [])
        self.assertEqual(a["remove_labels"], [])
        self.assertIsNone(a["menu"])

    def test_issue_scoped_command_not_honored_on_dashboard(self):
        # /approve (issue-scoped) on the dashboard issue is ignored.
        out = run(payload([
            base_issue(number=193, is_dashboard=True,
                       comments=[comment("/approve", cid=9)]),
        ]))
        self.assertEqual(self.actions_for(193, out), [])

    def test_focus_plus_issue_command_on_dashboard_keeps_only_focus(self):
        out = run(payload([
            base_issue(number=193, is_dashboard=True,
                       comments=[comment("/approve\n/focus 07", cid=9)]),
        ]))
        a = self.actions_for(193, out)[0]
        self.assertEqual(a["set_focus"], "07 - Polish & Onboarding")
        self.assertEqual(a["commands"], ["focus"])
        self.assertEqual(a["add_labels"], [])

    def test_epic_still_skipped_entirely(self):
        out = run(payload([
            base_issue(number=191, is_epic=True,
                       comments=[comment("/focus 04", cid=1)]),
        ]))
        self.assertEqual(out["actions"], [])

    def test_unmatched_focus_is_rejected(self):
        out = run(payload([
            base_issue(number=185, comments=[comment("/focus 99", cid=8)]),
        ]))
        self.assertEqual(self.actions_for(185, out), [])
        self.assertTrue(any(
            s.get("comment_id") == 8 and s.get("reason") == "focus-no-match"
            for s in out["skipped"]))

    def test_unmatched_milestone_is_rejected(self):
        out = run(payload([
            base_issue(number=181, labels=["pending-approval"],
                       comments=[comment("/milestone 99", cid=8)]),
        ]))
        self.assertEqual(self.actions_for(181, out), [])
        self.assertTrue(any(
            s.get("comment_id") == 8 and s.get("reason") == "milestone-no-match"
            for s in out["skipped"]))

    def test_approve_with_unmatched_milestone_is_refused(self):
        # ready-for-work ⇒ has milestone (#247): a bare /approve whose only
        # milestone hint is an unmatched /milestone 99 resolves no milestone, so
        # the transition is refused — no ready-for-work, no label removals — and
        # BOTH the milestone-no-match and approve-no-milestone skips are emitted.
        out = run(payload([
            base_issue(number=181, labels=["pending-approval"],
                       comments=[comment("/approve\n/milestone 99", cid=8)]),
        ]))
        self.assertEqual(self.actions_for(181, out), [])
        reasons = {s.get("reason") for s in out["skipped"]
                   if s.get("comment_id") == 8}
        self.assertIn("milestone-no-match", reasons)
        self.assertIn("approve-no-milestone", reasons)

    def test_approve_without_milestone_is_refused(self):
        # #247: /approve with no inline /milestone, no current milestone, and no
        # analysis-proposed milestone must NOT move the issue to ready-for-work.
        out = run(payload([
            base_issue(number=181, labels=["pending-approval"],
                       comments=[comment("looks good /approve", cid=8)]),
        ]))
        self.assertEqual(self.actions_for(181, out), [])
        refusal = [s for s in out["skipped"]
                   if s.get("comment_id") == 8
                   and s.get("reason") == "approve-no-milestone"]
        self.assertEqual(len(refusal), 1)
        # The refusal carries the which-milestone hand-back menu so the skill
        # asks Derek which milestone before re-approving.
        self.assertEqual(refusal[0].get("menu"), "which-milestone")

    def test_approve_uses_issue_current_milestone(self):
        # #247: an issue that already carries a milestone approves cleanly, and
        # the resolved milestone rides along on the action.
        out = run(payload([
            base_issue(number=181, labels=["pending-approval"],
                       milestone="04 - Quests & Economy",
                       comments=[comment("/approve", cid=8)]),
        ]))
        a = self.actions_for(181, out)[0]
        self.assertIn("ready-for-work", a["add_labels"])
        self.assertEqual(a["set_milestone"], "04 - Quests & Economy")

    def test_approve_uses_proposed_milestone(self):
        # #247: the analysis-proposed milestone resolves the gate when nothing
        # else is present.
        out = run(payload([
            base_issue(number=181, labels=["pending-approval"],
                       proposed_milestone="03 - Dogs & Conversations",
                       comments=[comment("/approve", cid=8)]),
        ]))
        a = self.actions_for(181, out)[0]
        self.assertIn("ready-for-work", a["add_labels"])
        self.assertEqual(a["set_milestone"], "03 - Dogs & Conversations")

    def test_approve_inline_milestone_overrides_proposed(self):
        # An inline /milestone wins over the proposed one, and order does not
        # matter: /milestone before /approve resolves identically.
        out_a = run(payload([
            base_issue(number=181, labels=["pending-approval"],
                       proposed_milestone="03 - Dogs & Conversations",
                       comments=[comment("/approve\n/milestone 07", cid=8)]),
        ]))
        out_b = run(payload([
            base_issue(number=181, labels=["pending-approval"],
                       proposed_milestone="03 - Dogs & Conversations",
                       comments=[comment("/milestone 07\n/approve", cid=8)]),
        ]))
        a = self.actions_for(181, out_a)[0]
        b = self.actions_for(181, out_b)[0]
        self.assertEqual(a["set_milestone"], "07 - Polish & Onboarding")
        self.assertEqual(b["set_milestone"], "07 - Polish & Onboarding")
        self.assertIn("ready-for-work", a["add_labels"])
        self.assertIn("ready-for-work", b["add_labels"])

    def test_cap_sets_cap_on_dashboard(self):
        # /cap <n> is honored on the dashboard issue (#193) and resolves to a
        # set_cap action, mirroring /focus's dashboard carve-out (#240).
        out = run(payload([
            base_issue(number=193, is_dashboard=True,
                       comments=[comment("/cap 5", cid=10)]),
        ]))
        acts = self.actions_for(193, out)
        self.assertEqual(len(acts), 1)
        a = acts[0]
        self.assertEqual(a["set_cap"], 5)
        self.assertEqual(a["add_labels"], [])
        self.assertEqual(a["remove_labels"], [])
        self.assertIsNone(a["menu"])

    def test_cap_non_numeric_is_rejected(self):
        out = run(payload([
            base_issue(number=193, is_dashboard=True,
                       comments=[comment("/cap banana", cid=11)]),
        ]))
        self.assertEqual(self.actions_for(193, out), [])
        self.assertTrue(any(
            s.get("comment_id") == 11 and s.get("reason") == "cap-invalid"
            for s in out["skipped"]))

    def test_cap_non_positive_is_rejected(self):
        out = run(payload([
            base_issue(number=193, is_dashboard=True,
                       comments=[comment("/cap 0", cid=12)]),
        ]))
        self.assertEqual(self.actions_for(193, out), [])
        self.assertTrue(any(
            s.get("comment_id") == 12 and s.get("reason") == "cap-invalid"
            for s in out["skipped"]))
        # No marker/action change accompanies the rejection.
        self.assertEqual(out["actions"], [])

    def test_cap_ignored_on_non_dashboard_issue(self):
        # /cap is honored ONLY on the dashboard issue, unlike /focus which is
        # honored everywhere (#240).
        out = run(payload([
            base_issue(number=185, comments=[comment("/cap 5", cid=13)]),
        ]))
        self.assertEqual(self.actions_for(185, out), [])

    def test_non_approve_command_unaffected_by_milestone_gate(self):
        # The milestone gate is scoped to /approve only: /admit and /park still
        # act on a milestone-less issue.
        out = run(payload([
            base_issue(number=180, comments=[comment("/admit", cid=3)]),
            base_issue(number=182, comments=[comment("/park", cid=4)]),
        ]))
        self.assertIn("ai-triage", self.actions_for(180, out)[0]["add_labels"])
        self.assertIn("parked", self.actions_for(182, out)[0]["add_labels"])


if __name__ == "__main__":
    unittest.main()
