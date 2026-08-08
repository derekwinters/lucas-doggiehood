#!/usr/bin/env python3
"""Create/remove/list native GitHub issue **dependency** relationships.

Doggiehood records "issue A is blocked by issue B" as a *native* GitHub
issue-dependency relationship, never as a prose line in the body (CLAUDE.md
rule #12). The pipeline's reconcile sweep already reads native blockers
(``reconcile.py::native_blocked_by``) and *flags* prose-only dependency lines
as drift (``flag_prose_dep``); this script is the write side that keeps work
on the native path.

The GitHub issue-dependencies REST API is asymmetric (same note as
``reconcile.py::native_blocked_by``):

  * READ  ``GET  /repos/{repo}/issues/{n}/dependencies/blocked_by`` returns full
    issue objects, so each blocker's identity there is its ``number``.
  * WRITE ``POST /repos/{repo}/issues/{n}/dependencies/blocked_by`` takes a
    numeric ``{"issue_id": <db id>}`` — the issue's global database id, **not**
    its ``#number``. ``DELETE …/blocked_by/{issue_id}`` likewise.

So to say "#A is blocked by #B" this script resolves #B's ``id`` (one GET) and
POSTs it to #A's ``blocked_by`` list.

Same split as the other deterministic skills: the URL/path builders are PURE
and unit-tested (``tests/test_set_blocker.py``); GitHub I/O lives only in the
``_api_request`` edge and is not unit-tested.

Usage:
  # "#295 is blocked by #360"
  set_blocker.py 295 --blocked-by 360
  # remove that relationship
  set_blocker.py 295 --blocked-by 360 --remove
  # list what #295 is blocked by
  set_blocker.py 295 --list
  # a different repo
  set_blocker.py 295 --blocked-by 360 --repo owner/name

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


# --- pure path/payload builders (unit-tested) ------------------------------

def blocked_by_path(repo, number):
    """Collection path for what ``#number`` is blocked by (GET/POST)."""
    return "/repos/%s/issues/%d/dependencies/blocked_by" % (repo, number)


def blocked_by_item_path(repo, number, issue_id):
    """Member path for one native blocker of ``#number`` (DELETE)."""
    return "%s/%d" % (blocked_by_path(repo, number), issue_id)


def issue_path(repo, number):
    """Path to a single issue — used to resolve its database ``id``."""
    return "/repos/%s/issues/%d" % (repo, number)


def add_payload(blocker_issue_id):
    """POST body that adds ``blocker_issue_id`` as a blocker."""
    return {"issue_id": blocker_issue_id}


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
    req.add_header("User-Agent", "doggiehood-issue-blockers")
    if data is not None:
        req.add_header("Content-Type", "application/json")
    with urllib.request.urlopen(req) as resp:
        body = resp.read()
        return json.loads(body) if body else None


def resolve_issue_id(repo, number, token):
    """The global database ``id`` for ``#number`` (needed by the write API)."""
    return _api_request("GET", issue_path(repo, number), token)["id"]


def list_blockers(repo, number, token):
    items = _api_request("GET", blocked_by_path(repo, number), token) or []
    return [i["number"] for i in items if isinstance(i, dict) and "number" in i]


def add_blocker(repo, blocked, blocker, token):
    blocker_id = resolve_issue_id(repo, blocker, token)
    _api_request("POST", blocked_by_path(repo, blocked), token,
                 add_payload(blocker_id))


def remove_blocker(repo, blocked, blocker, token):
    blocker_id = resolve_issue_id(repo, blocker, token)
    _api_request("DELETE", blocked_by_item_path(repo, blocked, blocker_id), token)


def main(argv=None):
    parser = argparse.ArgumentParser(
        description="Manage native GitHub issue blocker (dependency) relationships.")
    parser.add_argument("blocked", type=int,
                        help="the issue number that is blocked / whose blockers to list")
    parser.add_argument("--blocked-by", type=int, metavar="N",
                        help="the issue number that blocks it")
    parser.add_argument("--remove", action="store_true",
                        help="remove the relationship instead of adding it")
    parser.add_argument("--list", action="store_true",
                        help="list what the issue is natively blocked by")
    parser.add_argument("--repo", type=parse_repo, default=REPO_DEFAULT,
                        help="owner/name (default %(default)s)")
    args = parser.parse_args(argv)

    token = _token()
    try:
        if args.list:
            blockers = list_blockers(args.repo, args.blocked, token)
            if blockers:
                print("#%d is blocked by: %s"
                      % (args.blocked, ", ".join("#%d" % n for n in blockers)))
            else:
                print("#%d has no native blockers." % args.blocked)
            return 0

        if args.blocked_by is None:
            parser.error("--blocked-by is required unless --list is given")

        if args.remove:
            remove_blocker(args.repo, args.blocked, args.blocked_by, token)
            print("Removed: #%d no longer blocked by #%d"
                  % (args.blocked, args.blocked_by))
        else:
            add_blocker(args.repo, args.blocked, args.blocked_by, token)
            print("Set: #%d is blocked by #%d" % (args.blocked, args.blocked_by))
        return 0
    except urllib.error.HTTPError as exc:
        sys.stderr.write("GitHub API error %d: %s\n" % (exc.code, exc.read().decode(errors="replace")))
        return 1


if __name__ == "__main__":
    sys.exit(main())
