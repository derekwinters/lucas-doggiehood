"""Tests for the mechanical issue gate (`PreToolUse` hook) — issue #684.

Run: python3 -m unittest discover -s .claude/hooks/tests

The decision is a pure function (event + approved-issue signal + injected
label lookup -> Decision), so these tests never touch the network: every
GitHub lookup is a stub. GitHub I/O lives only at the edge in
`issue_gate.live_labels()`, mirroring the shape of
`.claude/skills/pipeline-gatekeeper/parse_commands.py`.
"""

import json
import os
import subprocess
import sys
import unittest

HOOK_DIR = os.path.join(os.path.dirname(__file__), os.pardir)
SCRIPT = os.path.join(HOOK_DIR, "issue_gate.py")

sys.path.insert(0, os.path.abspath(HOOK_DIR))
import issue_gate  # noqa: E402

ROOT = "/repo"


def event(tool="Write", path="/repo/Assets/Scripts/Core/Thing.cs", **kw):
    payload = {
        "hook_event_name": "PreToolUse",
        "tool_name": tool,
        "tool_input": {"file_path": path},
        "cwd": ROOT,
    }
    payload.update(kw)
    return payload


def never_called(number):  # pragma: no cover - guards accidental I/O
    raise AssertionError("label lookup must not run for issue #%s" % number)


class GateAppliesTest(unittest.TestCase):
    """Which (tool, path) pairs the gate fires on at all."""

    def test_write_tools_against_each_gated_glob_are_gated(self):
        for tool in ("Edit", "Write", "MultiEdit"):
            for path in (
                "/repo/Assets/Scripts/Core/Dogs/Dog.cs",
                "/repo/CoreTests/Doggiehood.Core.Tests/DogTests.cs",
                "/repo/ProjectSettings/ProjectSettings.asset",
                "/repo/Packages/manifest.json",
            ):
                self.assertTrue(
                    issue_gate.gate_applies(tool, path, project_root=ROOT),
                    "%s %s should be gated" % (tool, path))

    def test_ungated_paths_are_not_gated(self):
        for path in (
            "/repo/docs/specs/quests/quest-content.md",
            "/repo/.claude/hooks/issue_gate.py",
            "/repo/CLAUDE.md",
            "/repo/.github/workflows/ci-tests.yml",
        ):
            self.assertFalse(
                issue_gate.gate_applies("Write", path, project_root=ROOT), path)

    def test_read_only_tools_are_never_gated(self):
        for tool in ("Read", "Grep", "Glob", "Bash", "WebFetch"):
            self.assertFalse(
                issue_gate.gate_applies(
                    tool, "/repo/Assets/Scripts/Core/Dog.cs", project_root=ROOT),
                tool)

    def test_paths_outside_the_project_are_not_gated(self):
        self.assertFalse(issue_gate.gate_applies(
            "Write", "/tmp/scratch/Assets/Thing.cs", project_root=ROOT))

    def test_relative_paths_are_matched_against_the_project_root(self):
        self.assertTrue(issue_gate.gate_applies(
            "Write", "Assets/Scripts/Core/Dog.cs", project_root=ROOT))


class DecideWithoutSignalTest(unittest.TestCase):
    """No approved-issue signal -> denial, with no GitHub lookup attempted."""

    def test_denied_when_env_var_is_unset(self):
        decision = issue_gate.decide(
            event(), approved_issue=None, lookup=never_called,
            project_root=ROOT)
        self.assertFalse(decision.allowed)
        self.assertIn("DOGGIEHOOD_APPROVED_ISSUE", decision.reason)

    def test_denied_when_env_var_is_blank(self):
        decision = issue_gate.decide(
            event(tool="Edit", path="/repo/CoreTests/x.cs"),
            approved_issue="   ", lookup=never_called, project_root=ROOT)
        self.assertFalse(decision.allowed)

    def test_denied_when_env_var_is_not_an_issue_number(self):
        decision = issue_gate.decide(
            event(), approved_issue="the-map-bug", lookup=never_called,
            project_root=ROOT)
        self.assertFalse(decision.allowed)

    def test_denial_names_the_claude_md_rule(self):
        decision = issue_gate.decide(
            event(), approved_issue=None, lookup=never_called,
            project_root=ROOT)
        self.assertIn("rule #13", decision.reason)

    def test_ungated_path_is_allowed_with_no_signal_and_no_lookup(self):
        decision = issue_gate.decide(
            event(path="/repo/docs/engineering/agent-workflow.md"),
            approved_issue=None, lookup=never_called, project_root=ROOT)
        self.assertTrue(decision.allowed)

    def test_read_only_tool_is_allowed_with_no_signal_and_no_lookup(self):
        decision = issue_gate.decide(
            event(tool="Read"), approved_issue=None, lookup=never_called,
            project_root=ROOT)
        self.assertTrue(decision.allowed)


class LiveLabelTest(unittest.TestCase):
    """The signal is verified against the issue's LIVE labels, never trusted.

    Passing states are `ready-for-work` OR `in-progress`. The pipeline's
    labels are a mutually exclusive state machine and both `pipeline-dev` and
    `milestone-orchestration` set an issue to `in-progress` *before* any code
    is written, so a `ready-for-work`-only check would deny every automated
    build (see the issue's "Held mid-run" comment).
    """

    def gate(self, labels, approved_issue="684"):
        self.looked_up = []

        def lookup(number):
            self.looked_up.append(number)
            return labels

        return issue_gate.decide(
            event(), approved_issue=approved_issue, lookup=lookup,
            project_root=ROOT)

    def test_allowed_when_the_issue_is_ready_for_work(self):
        decision = self.gate({"type:bug", "ready-for-work"})
        self.assertTrue(decision.allowed, decision.reason)
        self.assertEqual(self.looked_up, [684])

    def test_allowed_when_the_issue_is_in_progress(self):
        decision = self.gate({"area:ai", "in-progress"})
        self.assertTrue(decision.allowed, decision.reason)

    def test_denied_when_the_issue_carries_neither_label(self):
        decision = self.gate({"ai-triage", "pending-approval"})
        self.assertFalse(decision.allowed)
        self.assertIn("684", decision.reason)
        self.assertIn("ready-for-work", decision.reason)

    def test_denied_when_the_issue_has_no_labels_at_all(self):
        # An agent inventing an issue number lands here.
        self.assertFalse(self.gate(set()).allowed)

    def test_denied_when_the_issue_does_not_exist(self):
        # `live_labels` reports a definitive "no such issue" as an empty set,
        # distinct from the None it returns when it cannot tell.
        self.assertFalse(self.gate(set(), approved_issue="999999").allowed)

    def test_allowed_when_the_live_state_cannot_be_determined(self):
        # Fail-open: a GitHub outage (or a session with no token) must not
        # halt all development. The no-signal denial above needs no network.
        self.assertTrue(self.gate(None).allowed)

    def test_allowed_when_the_lookup_raises(self):
        def boom(number):
            raise RuntimeError("api down")

        decision = issue_gate.decide(
            event(), approved_issue="684", lookup=boom, project_root=ROOT)
        self.assertTrue(decision.allowed, decision.reason)

    def test_no_lookup_for_an_ungated_path(self):
        decision = issue_gate.decide(
            event(path="/repo/docs/x.md"), approved_issue="684",
            lookup=never_called, project_root=ROOT)
        self.assertTrue(decision.allowed)


class HookProtocolTest(unittest.TestCase):
    """The script edge: stdin JSON in, PreToolUse hook JSON out."""

    def run_hook(self, payload, env=None):
        environ = dict(os.environ)
        environ.pop("DOGGIEHOOD_APPROVED_ISSUE", None)
        environ.update(env or {})
        proc = subprocess.run(
            [sys.executable, SCRIPT], input=json.dumps(payload),
            capture_output=True, text=True, env=environ)
        return proc

    def test_denial_emits_a_deny_permission_decision_and_exits_zero(self):
        # The script edge resolves the real project root from its own
        # location, so this payload names a real in-repo path.
        proc = self.run_hook(event(path=os.path.join(
            issue_gate._default_root(), "Assets", "Scripts", "Core", "X.cs")))
        self.assertEqual(proc.returncode, 0, proc.stderr)
        out = json.loads(proc.stdout)
        specific = out["hookSpecificOutput"]
        self.assertEqual(specific["hookEventName"], "PreToolUse")
        self.assertEqual(specific["permissionDecision"], "deny")
        self.assertIn("rule #13", specific["permissionDecisionReason"])

    def test_ungated_path_emits_no_decision(self):
        proc = self.run_hook(event(path=os.path.join(
            issue_gate._default_root(), "docs", "engineering",
            "agent-workflow.md")))
        self.assertEqual(proc.returncode, 0, proc.stderr)
        self.assertEqual(proc.stdout.strip(), "")

    def test_malformed_input_never_blocks_the_session(self):
        proc = subprocess.run(
            [sys.executable, SCRIPT], input="not json at all",
            capture_output=True, text=True)
        self.assertEqual(proc.returncode, 0, proc.stderr)
        self.assertEqual(proc.stdout.strip(), "")


class PipelinePlumbingTest(unittest.TestCase):
    """The builders must hand the gate the issue they are about to build.

    Without this, the gate denies every automated build — so the instruction
    is asserted here rather than left to memory.
    """

    def skill_text(self, name):
        path = os.path.join(issue_gate._default_root(), ".claude", "skills",
                            name, "SKILL.md")
        with open(path, encoding="utf-8") as handle:
            return handle.read()

    def test_builder_skills_set_the_approved_issue_env_var(self):
        for skill in ("pipeline-dev", "milestone-orchestration"):
            self.assertIn(issue_gate.ENV_APPROVED_ISSUE,
                          self.skill_text(skill),
                          "%s must set %s before invoking doggiehood-dev"
                          % (skill, issue_gate.ENV_APPROVED_ISSUE))


class SettingsWiringTest(unittest.TestCase):
    """The gate is only real if `.claude/settings.json` actually fires it."""

    def setUp(self):
        path = os.path.join(
            issue_gate._default_root(), ".claude", "settings.json")
        self.assertTrue(os.path.exists(path),
                        ".claude/settings.json must wire the PreToolUse gate")
        with open(path, encoding="utf-8") as handle:
            self.settings = json.load(handle)

    def entries(self):
        return self.settings.get("hooks", {}).get("PreToolUse", [])

    def test_every_gated_tool_is_matched_by_a_hook_running_the_gate(self):
        for tool in issue_gate.GATED_TOOLS:
            matched = [
                entry for entry in self.entries()
                if tool in [m.strip() for m in entry.get("matcher", "").split("|")]
            ]
            self.assertTrue(matched, "no PreToolUse matcher covers %s" % tool)
            commands = [hook.get("command", "")
                        for entry in matched
                        for hook in entry.get("hooks", [])]
            self.assertTrue(
                any("issue_gate.py" in command for command in commands),
                "%s is matched but does not run issue_gate.py" % tool)


if __name__ == "__main__":
    unittest.main()
