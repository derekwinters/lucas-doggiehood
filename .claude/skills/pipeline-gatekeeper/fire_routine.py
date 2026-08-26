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
import re
import sys
import urllib.error
import urllib.request

ROUTINE_BETA = "experimental-cc-routine-2026-04-01"
ANTHROPIC_VERSION = "2023-06-01"

#: Anything naming a private Claude Code session (#735). Not a credential, but
#: this repository is public, so a workflow log is world-readable and
#: permanent, and an identifier published there cannot be unpublished. The bare
#: session id is matched as well as the URL — the id is the identifying part,
#: and a link is only that id with a prefix.
PRIVATE_LINK = re.compile(
    r"https?://[^\s\"'<>]*claude\.ai[^\s\"'<>]*|session_[A-Za-z0-9_-]+",
    re.IGNORECASE,
)


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


def interpret_fire_response(status, body):
    """Classify a fire response into ``(ok, detail)``.

    A truthful success signal (#380): ``ok`` is True **only** when the endpoint
    returned a real ``routine_fire`` body carrying a ``claude_code_session_url``
    — proof a session was actually created. `urlopen` raises only on 4xx/5xx,
    so a bare "2xx ⇒ success" check would report a false "fired" when
    ``AI_TRIAGE_URL`` points somewhere other than the ``/fire`` endpoint (e.g.
    a page that returns a 200 HTML body). For any non-success, ``detail`` names
    the HTTP status and a body snippet so the cause is visible in the workflow
    log (a 401's error message, an unexpected 200's body, etc.).

    ``detail`` never carries the session link (#735). The check reads it and
    reports only that it was there: this repository is public, so the workflow
    log is a publication, and a value that is never returned cannot later be
    printed by accident. The snippet is redacted for the same reason — a
    response that fails *after* a session was created carries the link in its
    body, so the failure branch would otherwise publish what the success
    branch no longer does.
    """
    if isinstance(body, (bytes, bytearray)):
        text = body.decode("utf-8", "replace")
    else:
        text = body or ""
    try:
        parsed = json.loads(text)
    except (ValueError, TypeError):
        parsed = None
    if (200 <= status < 300 and isinstance(parsed, dict)
            and parsed.get("claude_code_session_url")):
        return True, ""
    snippet = " ".join(PRIVATE_LINK.sub("<redacted>", text).split())[:300]
    return False, "HTTP %s: %s" % (status, snippet or "(empty body)")


def fire(issue_number, repo):
    """Best-effort POST to the Routine fire endpoint. Returns True if a session
    was actually created.

    Reads ``AI_TRIAGE_URL`` / ``AI_TRIAGE_SECRET`` from the environment. A
    missing secret (Routine not wired up) or any network error is logged and
    swallowed — the label move has already happened, so a failed fire must
    never fail the gatekeeper. The response is classified by
    ``interpret_fire_response`` so the log reflects what actually happened:
    that a session was created on success, or the status + redacted body on
    failure. Never the session link itself (#735).
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
            status, raw = resp.status, resp.read()
    except urllib.error.HTTPError as exc:
        # 4xx/5xx — read the error body (the API's error JSON), which the bare
        # exception string drops, so the log names why (e.g. a 401 bad token).
        body = b""
        try:
            body = exc.read()
        except Exception:  # noqa: BLE001
            pass
        _, detail = interpret_fire_response(exc.code, body)
        sys.stderr.write("reactive-triage: fire for #%d failed: %s\n"
                         % (issue_number, detail))
        return False
    except Exception as exc:  # noqa: BLE001 — network error, never fatal
        sys.stderr.write("reactive-triage: fire for #%d failed: %s\n"
                         % (issue_number, exc))
        return False

    ok, detail = interpret_fire_response(status, raw)
    if ok:
        # That a session was created, never which one (#735).
        sys.stderr.write("reactive-triage: fired Routine for #%d\n"
                         % issue_number)
    else:
        # A 2xx that isn't a routine_fire — e.g. AI_TRIAGE_URL is not the /fire
        # endpoint. Do NOT report a false success.
        sys.stderr.write(
            "reactive-triage: fire for #%d returned no session (check "
            "AI_TRIAGE_URL points at the /fire endpoint): %s\n"
            % (issue_number, detail))
    return ok
