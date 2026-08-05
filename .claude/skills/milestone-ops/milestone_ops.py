#!/usr/bin/env python3
"""List / close / reopen / count-open GitHub **milestones** via REST.

The GitHub MCP server exposes no milestone CRUD (no list, close, reopen, or
"open issues in milestone"), so every milestone chore otherwise means
rediscovering the raw REST calls by hand. This script wraps them, mirroring
``.claude/skills/issue-blockers/`` (built for the same reason: no MCP tool for
a GitHub object we routinely need to manage).

The endpoints (and their gotchas):

  * LIST   ``GET   /repos/{repo}/milestones?state=all&per_page=100`` — carries
    ``number``, ``state``, ``title``, ``open_issues``, ``closed_issues``. The
    ``number`` is required by the PATCH/close call and is **not** the version
    string (e.g. ``v0.11`` was milestone number ``21``).
  * OPEN   ``GET   /repos/{repo}/issues?milestone={number}&state=open`` — the
    pre-close safety check; don't close a milestone with open work.
  * PATCH  ``PATCH /repos/{repo}/milestones/{number}`` with
    ``{"state":"closed"}`` (close) or ``{"state":"open"}`` (reopen).

Because ``number`` != version string, ``close``/``reopen``/``open-issues`` take
a title OR a number and resolve a title against the ``list`` payload; a bare
number passes straight through.

Same split as the other deterministic skills: the URL/path/payload builders and
parsers are PURE and unit-tested (``tests/test_milestone_ops.py``); GitHub I/O
lives only in the ``_api_request`` edge and is not unit-tested.

Usage:
  milestone_ops.py list
  milestone_ops.py open-issues v0.11        # or: open-issues 21
  milestone_ops.py close v0.11 [--force]    # refuses if open issues remain
  milestone_ops.py reopen v0.11
  milestone_ops.py list --repo owner/name

GITHUB_TOKEN (or GH_TOKEN) must be set. Exit code 0 on success, 1 on error.
"""

import argparse
import json
import os
import sys
import urllib.error
import urllib.request

REPO_DEFAULT = "derekwinters/lucas-doggiehood"
API = "https://api.github.com"


# --- pure path/payload/parse builders (unit-tested) ------------------------

def milestones_path(repo):
    """LIST path — all milestones with their issue counts."""
    return "/repos/%s/milestones?state=all&per_page=100" % repo


def milestone_issues_path(repo, number):
    """OPEN-issues path — open issues assigned to milestone ``number``."""
    return "/repos/%s/issues?milestone=%d&state=open" % (repo, number)


def milestone_item_path(repo, number):
    """PATCH path — a single milestone (close/reopen)."""
    return "/repos/%s/milestones/%d" % (repo, number)


def close_payload():
    """PATCH body that closes a milestone."""
    return {"state": "closed"}


def reopen_payload():
    """PATCH body that reopens a milestone."""
    return {"state": "open"}


def resolve_number(milestones, arg):
    """Resolve a milestone ``title`` OR ``number`` to its numeric ``number``.

    A bare numeric argument passes straight through unresolved (the ``number``
    is not the version string, so callers may already know it). Otherwise the
    argument is matched as an exact title against the ``list`` payload.
    """
    text = str(arg)
    if text.isdigit():
        return int(text)
    for m in milestones:
        if m.get("title") == text:
            return m["number"]
    raise ValueError("no milestone titled %r" % text)


def should_close(open_count, force):
    """The close-guard's yes/no decision.

    Proceed only when there is no open work, or the caller passed ``--force``.
    """
    return bool(force) or open_count == 0


def format_row(milestone):
    """One formatted line for a milestone: number, state, title, counts."""
    return "#%s  [%s]  %s  (open: %s, closed: %s)" % (
        milestone.get("number"),
        milestone.get("state"),
        milestone.get("title"),
        milestone.get("open_issues"),
        milestone.get("closed_issues"),
    )


def format_list(milestones):
    """One row per milestone, newline-joined."""
    return "\n".join(format_row(m) for m in milestones)


def parse_repo(value):
    """Validate an ``owner/name`` repo slug, returning it unchanged."""
    parts = value.split("/")
    if len(parts) != 2 or not all(parts):
        raise ValueError("repo must be 'owner/name', got %r" % value)
    return value


# --- GitHub I/O edge (not unit-tested) -------------------------------------

def _token():
    token = os.environ.get("GITHUB_TOKEN") or os.environ.get("GH_TOKEN")
    if not token:
        sys.stderr.write("GITHUB_TOKEN (or GH_TOKEN) is required.\n")
        sys.exit(1)
    return token


def _api_request(method, path, token, payload=None):
    data = json.dumps(payload).encode() if payload is not None else None
    req = urllib.request.Request(API + path, data=data, method=method)
    req.add_header("Authorization", "Bearer %s" % token)
    req.add_header("Accept", "application/vnd.github+json")
    req.add_header("X-GitHub-Api-Version", "2022-11-28")
    req.add_header("User-Agent", "doggiehood-milestone-ops")
    if data is not None:
        req.add_header("Content-Type", "application/json")
    with urllib.request.urlopen(req) as resp:
        body = resp.read()
        return json.loads(body) if body else None


def fetch_milestones(repo, token):
    return _api_request("GET", milestones_path(repo), token) or []


def count_open_issues(repo, number, token):
    items = _api_request("GET", milestone_issues_path(repo, number), token) or []
    return len(items)


def patch_milestone(repo, number, token, payload):
    _api_request("PATCH", milestone_item_path(repo, number), token, payload)


def main(argv=None):
    parser = argparse.ArgumentParser(
        description="List / close / reopen / count-open GitHub milestones.")
    parser.add_argument("--repo", type=parse_repo, default=REPO_DEFAULT,
                        help="owner/name (default %(default)s)")
    sub = parser.add_subparsers(dest="command", required=True)

    sub.add_parser("list", help="list all milestones with issue counts")

    p_open = sub.add_parser("open-issues", help="count open issues in a milestone")
    p_open.add_argument("milestone", help="milestone title or number")

    p_close = sub.add_parser("close", help="close a milestone (refuses if open work)")
    p_close.add_argument("milestone", help="milestone title or number")
    p_close.add_argument("--force", action="store_true",
                         help="close even if open issues remain")

    p_reopen = sub.add_parser("reopen", help="reopen a closed milestone")
    p_reopen.add_argument("milestone", help="milestone title or number")

    args = parser.parse_args(argv)
    token = _token()

    try:
        if args.command == "list":
            milestones = fetch_milestones(args.repo, token)
            print(format_list(milestones))
            return 0

        # The remaining commands resolve a title/number against the live list.
        milestones = fetch_milestones(args.repo, token)
        number = resolve_number(milestones, args.milestone)

        if args.command == "open-issues":
            open_count = count_open_issues(args.repo, number, token)
            print("Milestone #%d has %d open issue(s)." % (number, open_count))
            return 0

        if args.command == "reopen":
            patch_milestone(args.repo, number, token, reopen_payload())
            print("Reopened milestone #%d." % number)
            return 0

        if args.command == "close":
            open_count = count_open_issues(args.repo, number, token)
            if not should_close(open_count, args.force):
                sys.stderr.write(
                    "Refusing to close milestone #%d: %d open issue(s) remain. "
                    "Pass --force to override.\n" % (number, open_count))
                return 1
            patch_milestone(args.repo, number, token, close_payload())
            print("Closed milestone #%d." % number)
            return 0
    except ValueError as exc:
        sys.stderr.write("%s\n" % exc)
        return 1
    except urllib.error.HTTPError as exc:
        sys.stderr.write("GitHub API error %d: %s\n"
                         % (exc.code, exc.read().decode(errors="replace")))
        return 1

    return 0


if __name__ == "__main__":
    sys.exit(main())
