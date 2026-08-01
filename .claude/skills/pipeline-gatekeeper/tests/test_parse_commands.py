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

sys.path.insert(0, os.path.join(os.path.dirname(__file__), os.pardir))
import parse_commands  # noqa: E402


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


def blocker(number, kind="blocked-by", state="open", milestone=None):
    return {"number": number, "kind": kind, "state": state,
            "milestone": milestone}


# The live version-scheme milestones the #212 order gate reasons about.
VERSION_MILESTONES = ["v0.4", "v1.0", "v1.1", "v2.0", "Direct Involvement Needed"]


class TestMilestoneOrder(unittest.TestCase):
    """`milestone_order()` parses vMAJOR.MINOR titles into a sortable key and
    treats any non-version title as unordered (#212)."""

    def test_version_titles_sort_in_release_order(self):
        order = parse_commands.milestone_order
        self.assertLess(order("v0.4"), order("v1.0"))
        self.assertLess(order("v1.0"), order("v1.1"))
        self.assertLess(order("v1.1"), order("v2.0"))

    def test_non_version_titles_are_unordered(self):
        self.assertIsNone(parse_commands.milestone_order("Direct Involvement Needed"))
        self.assertIsNone(parse_commands.milestone_order("03 - Dogs & Conversations"))
        self.assertIsNone(parse_commands.milestone_order(None))
        self.assertIsNone(parse_commands.milestone_order(""))


class TestBlockerMilestoneGate(unittest.TestCase):
    """#212: on /approve or /milestone the resulting milestone for A must not
    precede any OPEN blocker B's milestone, and every open blocker must itself
    be scheduled. A violation is REFUSED — A untouched, a `skipped` record with
    the conflict + an ack naming #A and #B, no auto-bump."""

    def actions_for(self, issue_number, out):
        return [a for a in out["actions"] if a["issue"] == issue_number]

    def skips_for(self, comment_id, out):
        return [s for s in out["skipped"] if s.get("comment_id") == comment_id]

    def test_snapshot_defaults_safely_without_blockers_or_milestone(self):
        # Round-trip: an issue with neither `blockers` nor a set milestone still
        # parses; a bare /admit is unaffected by the new fields.
        out = run(payload([
            base_issue(number=180, comments=[comment("/admit", cid=1)]),
        ], milestones=VERSION_MILESTONES))
        a = self.actions_for(180, out)[0]
        self.assertIn("ai-triage", a["add_labels"])

    def test_approve_with_open_unscheduled_blocker_is_refused(self):
        out = run(payload([
            base_issue(number=181, labels=["pending-approval"],
                       milestone="v1.0",
                       blockers=[blocker(500, milestone=None)],
                       comments=[comment("/approve", cid=7)]),
        ], milestones=VERSION_MILESTONES))
        self.assertEqual(self.actions_for(181, out), [])
        skips = self.skips_for(7, out)
        self.assertTrue(any(s.get("reason") == "blocker-unscheduled"
                            for s in skips))
        s = [s for s in skips if s.get("reason") == "blocker-unscheduled"][0]
        self.assertEqual(s.get("issue"), 181)
        self.assertEqual(s.get("blocker"), 500)
        self.assertIn("#181", s.get("ack", ""))
        self.assertIn("#500", s.get("ack", ""))

    def test_approve_earlier_than_open_blocker_is_refused(self):
        # A resolves to v0.4 (its milestone field); blocker B is open in v1.0.
        out = run(payload([
            base_issue(number=181, labels=["pending-approval"],
                       milestone="v0.4",
                       blockers=[blocker(600, milestone="v1.0")],
                       comments=[comment("/approve\n/milestone v0.4", cid=8)]),
        ], milestones=VERSION_MILESTONES))
        self.assertEqual(self.actions_for(181, out), [])
        skips = self.skips_for(8, out)
        s = [s for s in skips if s.get("reason") == "blocker-inversion"]
        self.assertEqual(len(s), 1)
        ack = s[0].get("ack", "")
        self.assertIn("#181", ack)
        self.assertIn("#600", ack)
        self.assertIn("v1.0", ack)

    def test_milestone_earlier_than_open_blocker_is_refused(self):
        # A bare /milestone placing A before an open blocker is refused too —
        # no set_milestone applied.
        out = run(payload([
            base_issue(number=181, labels=["pending-approval"],
                       milestone="v1.1",
                       blockers=[blocker(600, milestone="v1.0")],
                       comments=[comment("/milestone v0.4", cid=9)]),
        ], milestones=VERSION_MILESTONES))
        self.assertEqual(self.actions_for(181, out), [])
        skips = self.skips_for(9, out)
        self.assertTrue(any(s.get("reason") == "blocker-inversion"
                            for s in skips))

    def test_equal_milestone_is_allowed(self):
        out = run(payload([
            base_issue(number=181, labels=["pending-approval"],
                       milestone="v1.0",
                       blockers=[blocker(600, milestone="v1.0")],
                       comments=[comment("/approve", cid=10)]),
        ], milestones=VERSION_MILESTONES))
        a = self.actions_for(181, out)[0]
        self.assertIn("ready-for-work", a["add_labels"])

    def test_later_milestone_is_allowed(self):
        out = run(payload([
            base_issue(number=181, labels=["pending-approval"],
                       milestone="v1.1",
                       blockers=[blocker(600, milestone="v1.0")],
                       comments=[comment("/approve", cid=11)]),
        ], milestones=VERSION_MILESTONES))
        a = self.actions_for(181, out)[0]
        self.assertIn("ready-for-work", a["add_labels"])

    def test_closed_blocker_is_ignored(self):
        out = run(payload([
            base_issue(number=181, labels=["pending-approval"],
                       milestone="v0.4",
                       blockers=[blocker(600, state="closed", milestone="v1.0")],
                       comments=[comment("/approve", cid=12)]),
        ], milestones=VERSION_MILESTONES))
        a = self.actions_for(181, out)[0]
        self.assertIn("ready-for-work", a["add_labels"])

    def test_soft_depends_on_uses_the_same_refuse_rule(self):
        out = run(payload([
            base_issue(number=181, labels=["pending-approval"],
                       milestone="v0.4",
                       blockers=[blocker(600, kind="depends-on",
                                         milestone="v1.0")],
                       comments=[comment("/approve", cid=13)]),
        ], milestones=VERSION_MILESTONES))
        self.assertEqual(self.actions_for(181, out), [])
        self.assertTrue(any(s.get("reason") == "blocker-inversion"
                            for s in self.skips_for(13, out)))

    def test_non_milestone_command_unaffected_by_blocker_gate(self):
        # /park still parks even with a would-be-inverting open blocker — the
        # gate is scoped to /approve and /milestone only.
        out = run(payload([
            base_issue(number=182, labels=["pending-approval"],
                       milestone="v0.4",
                       blockers=[blocker(600, milestone="v1.0")],
                       comments=[comment("/park", cid=14)]),
        ], milestones=VERSION_MILESTONES))
        a = self.actions_for(182, out)[0]
        self.assertIn("parked", a["add_labels"])


class TestParseCommands(unittest.TestCase):
    def actions_for(self, issue_number, out):
        return [a for a in out["actions"] if a["issue"] == issue_number]

    def test_approve_by_owner(self):
        out = run(payload([
            base_issue(number=181, labels=["pending-approval"],
                       milestone="07 - Polish & Onboarding",
                       comments=[comment("looks good /approve", cid=7)]),
        ]))
        acts = self.actions_for(181, out)
        self.assertEqual(len(acts), 1)
        a = acts[0]
        self.assertIn("approve", a["commands"])
        self.assertIn("ready-for-work", a["add_labels"])
        self.assertIn("pending-approval", a["remove_labels"])
        # Part A (#319): /approve is now a presence-check + label flip only —
        # the milestone is already set (by analysis, at pending-approval), so
        # approve does NOT re-set it; set_milestone stays null on a bare
        # /approve.
        self.assertIsNone(a["set_milestone"])
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

    def test_approve_no_longer_resolves_inline_milestone(self):
        # Part A (#319): the /approve gate now checks ONLY the issue's
        # already-set milestone field — an inline /milestone in the SAME
        # comment is a separate command and no longer feeds the gate (no more
        # resolution / comment-scraping). On a milestone-less issue, /approve
        # is refused even though /milestone is right there in the same
        # comment; /milestone still fires as its own independent command.
        out = run(payload([
            base_issue(number=181, labels=["pending-approval"],
                       comments=[comment("/approve\n/milestone 07", cid=8)]),
        ]))
        acts = self.actions_for(181, out)
        self.assertEqual(len(acts), 1)
        a = acts[0]
        self.assertEqual(a["commands"], ["milestone"])
        self.assertEqual(a["set_milestone"], "07 - Polish & Onboarding")
        self.assertEqual(a["add_labels"], [])
        self.assertEqual(a["remove_labels"], [])
        reasons = {s.get("reason") for s in out["skipped"]
                   if s.get("comment_id") == 8}
        self.assertIn("approve-no-milestone", reasons)

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
        # #247: an issue that already carries a milestone approves cleanly.
        # Part A (#319): approve no longer re-sets it — set_milestone stays
        # null since the field is already correct.
        out = run(payload([
            base_issue(number=181, labels=["pending-approval"],
                       milestone="04 - Quests & Economy",
                       comments=[comment("/approve", cid=8)]),
        ]))
        a = self.actions_for(181, out)[0]
        self.assertIn("ready-for-work", a["add_labels"])
        self.assertIsNone(a["set_milestone"])

    def test_milestone_then_approve_in_separate_comments_now_required(self):
        # Part A (#319): since /approve's gate no longer resolves an inline
        # /milestone in the same comment, setting the milestone and approving
        # must now happen as two separate actions/comments — this is the new
        # expected flow. A /milestone command run first (its own action)...
        out_milestone = run(payload([
            base_issue(number=181, labels=["pending-approval"],
                       comments=[comment("/milestone 07", cid=8)]),
        ]))
        a = self.actions_for(181, out_milestone)[0]
        self.assertEqual(a["set_milestone"], "07 - Polish & Onboarding")
        # ...then, once the field is actually set (a fresh snapshot), a later
        # /approve comment resolves cleanly.
        out_approve = run(payload([
            base_issue(number=181, labels=["pending-approval"],
                       milestone="07 - Polish & Onboarding",
                       comments=[comment("/approve", cid=9)]),
        ]))
        b = self.actions_for(181, out_approve)[0]
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
