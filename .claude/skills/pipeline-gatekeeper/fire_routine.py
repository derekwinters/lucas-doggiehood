#!/usr/bin/env python3
"""Reactive-triage hook: fire the analysis Routine when an issue is admitted.

The reactive-triage change (#378) makes analysis event-driven instead of
purely scheduled. When the gatekeeper newly adds the ``ai-triage`` label to an
issue (via ``/admit``, ``/revise``, ``/redo``, ``/propose``, or the blocker
auto-revisit — see ``apply_actions.fires_triage``), the glue scripts POST to a
Claude Code **Routine** `/fire` endpoint so the analysis routine runs for that
one issue right away.

This is an OUTBOUND HTTPS call from the already-running gatekeeper workflow —
not an inbound GitHub webhook — so GitHub's ``GITHUB_TOKEN`` no-recursion guard
(which would suppress an ``on: issues: [labeled]`` trigger for the bot's own
label add) never applies. Auth and endpoint come from two repo Actions
secrets, surfaced as env vars:

  * ``AI_TRIAGE_URL``    — the Routine's per-routine fire endpoint
                           (``https://api.anthropic.com/v1/claude_code/routines/{id}/fire``)
  * ``AI_TRIAGE_SECRET`` — the bearer token generated for that Routine

If either is absent the hook is a clean no-op — the label move already
succeeded, and firing is best-effort — so the gatekeeper works unchanged
before Derek has wired the Routine up (and a fire failure never fails the
workflow).

Split for testability, mirroring ``_github_api.request``: ``build_fire_request``
is pure (unit-tested in tests/test_fire_routine.py); ``fire`` is the thin
``urllib`` I/O over it (network glue, untested).

The `/fire` endpoint is in research preview under the
``experimental-cc-routine-2026-04-01`` beta header; request/response shapes and
token semantics may change under a future dated beta header.
"""

import json
import os
import sys
import urllib.request

ROUTINE_BETA = "experimental-cc-routine-2026-04-01"
ANTHROPIC_VERSION = "2023-06-01"


def build_fire_request(url, secret, issue_number, repo):
    """The concrete fire request, or ``None`` when it can't/shouldn't be sent.

    Returns ``None`` if ``url`` or ``secret`` is falsy (the Routine isn't wired
    up yet) — the caller then no-ops. Otherwise returns a dict with ``url``,
    ``headers``, and ``body`` (the JSON payload). The payload's freeform
    ``text`` names the repo and issue number; the Routine prompt is responsible
    for parsing the issue number out of the untrusted ``<routine-fire-payload>``
    wrapper and ignoring anything else.
    """
    if not url or not secret:
        return None
    return {
        "url": url,
        "headers": {
            "Authorization": "Bearer %s" % secret,
            "anthropic-beta": ROUTINE_BETA,
            "anthropic-version": ANTHROPIC_VERSION,
            "Content-Type": "application/json",
        },
        "body": {
            "text": "Run triage on issue #%d in %s." % (issue_number, repo),
        },
    }


def fire(issue_number, repo):
    """Best-effort POST to the Routine fire endpoint. Returns True if sent.

    Reads ``AI_TRIAGE_URL`` / ``AI_TRIAGE_SECRET`` from the environment. A
    missing secret (Routine not wired up) or any network error is logged and
    swallowed — the label move has already happened, so a failed fire must
    never fail the gatekeeper.
    """
    req = build_fire_request(
        os.environ.get("AI_TRIAGE_URL"),
        os.environ.get("AI_TRIAGE_SECRET"),
        issue_number, repo)
    if req is None:
        sys.stderr.write(
            "reactive-triage: AI_TRIAGE_URL/AI_TRIAGE_SECRET unset — "
            "not firing for #%d\n" % issue_number)
        return False
    data = json.dumps(req["body"]).encode()
    request = urllib.request.Request(req["url"], data=data, method="POST")
    for name, value in req["headers"].items():
        request.add_header(name, value)
    try:
        with urllib.request.urlopen(request) as resp:
            resp.read()
        sys.stderr.write("reactive-triage: fired Routine for #%d\n"
                         % issue_number)
        return True
    except Exception as exc:  # noqa: BLE001 — best-effort, never fatal
        sys.stderr.write("reactive-triage: fire for #%d failed: %s\n"
                         % (issue_number, exc))
        return False
