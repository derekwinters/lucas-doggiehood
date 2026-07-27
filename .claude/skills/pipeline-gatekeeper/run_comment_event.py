#!/usr/bin/env python3
"""Glue for the per-issue, comment-triggered gatekeeper workflow (issue #319).

Reads the raw `issue_comment` event GitHub Actions wrote to
`GITHUB_EVENT_PATH`, builds the one-issue snapshot (`fetch_comment_event`),
runs the deterministic parser (`parse_commands`), and applies whatever it
returns (`apply_actions`) via the GitHub REST API (stdlib `urllib`, no
third-party deps — the same shape as `pipeline-reconcile/reconcile.py`'s and
`pipeline-dashboard/render_dashboard.py`'s live-fetch/write halves).

Not unit-tested — this is pure network glue over already-tested pure
functions (`fetch_comment_event.build_snapshot`, `parse_commands.process`,
`apply_actions.*`). See `.github/workflows/gatekeeper-comment.yml` and
`docs/engineering/issue-pipeline.md`.
"""

import json
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import apply_actions  # noqa: E402
import fetch_comment_event  # noqa: E402
import parse_commands  # noqa: E402
from _github_api import request  # noqa: E402

REPO_DEFAULT = "derekwinters/lucas-doggiehood"
REPO_OWNER = "derekwinters"


def _fetch_open_milestones(repo, token):
    items = request(
        "GET", "/repos/%s/milestones?state=open&per_page=100" % repo, token)
    return [m["title"] for m in items]


def _milestone_number(repo, token, title):
    items = request(
        "GET", "/repos/%s/milestones?state=all&per_page=100" % repo, token)
    for m in items:
        if m["title"] == title:
            return m["number"]
    return None


def _apply_action(repo, token, action, current_labels):
    number = action["issue"]
    new_labels = apply_actions.merge_labels(current_labels, action)
    if set(new_labels) != set(current_labels):
        request("PUT", "/repos/%s/issues/%d/labels" % (repo, number), token,
                 {"labels": new_labels})

    milestone_title = apply_actions.milestone_write_for(action)
    if milestone_title:
        num = _milestone_number(repo, token, milestone_title)
        if num is not None:
            request("PATCH", "/repos/%s/issues/%d" % (repo, number), token,
                     {"milestone": num})

    ack = apply_actions.render_ack(action)
    if ack:
        request("POST", "/repos/%s/issues/%d/comments" % (repo, number),
                 token, {"body": ack})

    for reaction in apply_actions.reactions_for(action):
        request("POST", "/repos/%s/issues/comments/%d/reactions"
                % (repo, action["comment_id"]), token, {"content": reaction})


def _apply_skip(repo, token, skip):
    text = apply_actions.render_skip_ack(skip)
    if text:
        request("POST", "/repos/%s/issues/%d/comments" % (repo, skip["issue"]),
                 token, {"body": text})
    comment_id = skip.get("comment_id")
    if comment_id is not None:
        for reaction in ("+1", "eyes"):
            request("POST", "/repos/%s/issues/comments/%d/reactions"
                    % (repo, comment_id), token, {"content": reaction})


def main():
    event_path = os.environ.get("GITHUB_EVENT_PATH")
    if not event_path:
        sys.stderr.write("GITHUB_EVENT_PATH is required.\n")
        return 2
    with open(event_path) as f:
        event = json.load(f)

    # In-script owner-gate — defense-in-depth on top of the workflow's own
    # `if:` job condition and the private-repo platform gate (only
    # collaborators can comment at all).
    author = ((event.get("comment") or {}).get("user") or {}).get("login")
    if author != REPO_OWNER:
        sys.stderr.write("Comment author %r is not the owner — skipping.\n"
                          % author)
        return 0

    token = os.environ.get("GITHUB_TOKEN")
    if not token:
        sys.stderr.write("GITHUB_TOKEN is required.\n")
        return 2
    repo = os.environ.get("GATEKEEPER_REPO", REPO_DEFAULT)

    milestones = _fetch_open_milestones(repo, token)
    snapshot, skip_reason = fetch_comment_event.build_snapshot(
        event, repo_owner=REPO_OWNER, milestones=milestones)
    if snapshot is None:
        sys.stderr.write("Skipping event: %s\n" % skip_reason)
        return 0

    current_labels = snapshot["issues"][0]["labels"]
    out = parse_commands.process(snapshot)

    for action in out["actions"]:
        _apply_action(repo, token, action, current_labels)
        sys.stderr.write("#%d %s\n"
                          % (action["issue"], ",".join(action["commands"])))

    for skip in out["skipped"]:
        _apply_skip(repo, token, skip)
        sys.stderr.write("#%s skipped: %s\n"
                          % (skip.get("issue"), skip.get("reason")))

    return 0


if __name__ == "__main__":
    raise SystemExit(main())
