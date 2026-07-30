"""Tests for the reactive-triage Routine fire builder (#378).

`fire_routine.build_fire_request` is the pure half of the reactive-triage hook:
given the fire URL + secret (the `AI_TRIAGE_URL` / `AI_TRIAGE_SECRET` repo
secrets) and an issue number, it returns the concrete HTTP request the
gatekeeper POSTs to the Claude Code Routine `/fire` endpoint — or `None` when
the secrets are absent, so the hook is a clean no-op before Derek has wired the
Routine up. The actual `urlopen` lives in `fire_routine.fire` (network glue,
untested like `_github_api.request`).

Run: python3 -m unittest discover -s .claude/skills/pipeline-gatekeeper/tests
"""

import os
import sys
import unittest

sys.path.insert(0, os.path.join(os.path.dirname(__file__), os.pardir))
import fire_routine  # noqa: E402


class TestBuildFireRequest(unittest.TestCase):
    URL = "https://api.anthropic.com/v1/claude_code/routines/trig_x/fire"
    SECRET = "sk-ant-oat01-xxxxx"
    REPO = "derekwinters/lucas-doggiehood"

    def test_none_when_url_missing(self):
        self.assertIsNone(fire_routine.build_fire_request(
            "", self.SECRET, 123, self.REPO))

    def test_none_when_secret_missing(self):
        self.assertIsNone(fire_routine.build_fire_request(
            self.URL, "", 123, self.REPO))

    def test_targets_the_fire_url(self):
        req = fire_routine.build_fire_request(
            self.URL, self.SECRET, 123, self.REPO)
        self.assertEqual(req["url"], self.URL)

    def test_authenticates_with_bearer_secret(self):
        req = fire_routine.build_fire_request(
            self.URL, self.SECRET, 123, self.REPO)
        self.assertEqual(req["headers"]["Authorization"],
                         "Bearer %s" % self.SECRET)

    def test_carries_the_research_preview_beta_header(self):
        req = fire_routine.build_fire_request(
            self.URL, self.SECRET, 123, self.REPO)
        self.assertEqual(req["headers"]["anthropic-beta"],
                         fire_routine.ROUTINE_BETA)

    def test_payload_text_carries_issue_number_and_repo(self):
        req = fire_routine.build_fire_request(
            self.URL, self.SECRET, 123, self.REPO)
        text = req["body"]["text"]
        self.assertIn("123", text)
        self.assertIn(self.REPO, text)


if __name__ == "__main__":
    unittest.main()
