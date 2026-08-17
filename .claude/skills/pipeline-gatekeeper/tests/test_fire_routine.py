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


class TestInterpretFireResponse(unittest.TestCase):
    """`interpret_fire_response(status, body)` decides whether a fire actually
    created a session — so the workflow log stops reporting a false "fired" on
    any 2xx (#380). Success requires a real `routine_fire` body carrying a
    session URL; anything else is a failure whose detail names the status and
    body so the cause is visible in the log."""

    ROUTINE_FIRE = (b'{"type": "routine_fire", "claude_code_session_id": '
                    b'"session_01X", "claude_code_session_url": '
                    b'"https://claude.ai/code/session_01X"}')

    def test_real_routine_fire_is_success_and_never_returns_the_session_url(self):
        """#735 — a session link is private and this repository is public.

        The `claude_code_session_url` check is what makes success truthful, so
        it stays; carrying the link back out of that check is what published
        it to anyone reading the Actions log.
        """
        ok, detail = fire_routine.interpret_fire_response(200, self.ROUTINE_FIRE)
        self.assertTrue(ok)
        self.assertNotIn("claude.ai", detail or "")
        self.assertNotIn("session_01X", detail or "")

    def test_a_failing_body_carrying_a_session_link_is_redacted(self):
        """#735 — the leak that survives deleting the success path.

        The failure detail is built from the raw response body, so a response
        that fails *after* a session was created carries the link into the log
        through this branch instead.
        """
        body = (b'{"type": "routine_fire", "claude_code_session_url": '
                b'"https://claude.ai/code/session_01X", "error": "timeout"}')
        ok, detail = fire_routine.interpret_fire_response(503, body)
        self.assertFalse(ok)
        self.assertNotIn("claude.ai", detail)
        self.assertNotIn("session_01X", detail)
        self.assertIn("503", detail)

    def test_2xx_without_session_url_is_not_success(self):
        # e.g. AI_TRIAGE_URL pointed at a page that returned a 200 HTML body.
        ok, detail = fire_routine.interpret_fire_response(
            200, b"<!doctype html><html>...</html>")
        self.assertFalse(ok)
        self.assertIn("200", detail)

    def test_http_error_status_and_body_surface_in_detail(self):
        ok, detail = fire_routine.interpret_fire_response(
            401, b'{"error": {"message": "invalid bearer token"}}')
        self.assertFalse(ok)
        self.assertIn("401", detail)
        self.assertIn("invalid bearer token", detail)

    def test_empty_body_is_not_success(self):
        ok, _ = fire_routine.interpret_fire_response(200, b"")
        self.assertFalse(ok)


if __name__ == "__main__":
    unittest.main()
