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
                       comments=[comment("looks good /approve", cid=7)]),
        ]))
        acts = self.actions_for(181, out)
        self.assertEqual(len(acts), 1)
        a = acts[0]
        self.assertIn("approve", a["commands"])
        self.assertIn("ready-for-work", a["add_labels"])
        self.assertIn("pending-approval", a["remove_labels"])
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

    def test_focus_honored_on_dashboard_issue(self):
        # #204: /focus is the one command accepted on the dashboard issue
        # itself, so focus can be set from #193 — every other command stays
        # excluded there (see test_non_focus_command_ignored_on_dashboard).
        out = run(payload(
            [base_issue(number=193, is_dashboard=True,
                        comments=[comment("/focus v0.4", cid=8)])],
            milestones=["v0.4", "v1.0"],
        ))
        a = self.actions_for(193, out)[0]
        self.assertEqual(a["set_focus"], "v0.4")

    def test_non_focus_command_ignored_on_dashboard(self):
        # Only /focus is honored on the dashboard; other commands are dropped.
        out = run(payload([
            base_issue(number=193, is_dashboard=True,
                       comments=[comment("/approve\n/admit", cid=9)]),
        ]))
        self.assertEqual(self.actions_for(193, out), [])

    def test_url_slash_does_not_trigger(self):
        out = run(payload([
            base_issue(number=180,
                       comments=[comment("see http://x/approve/foo", cid=8)]),
        ]))
        self.assertEqual(self.actions_for(180, out), [])


if __name__ == "__main__":
    unittest.main()
