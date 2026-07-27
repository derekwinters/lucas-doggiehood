#!/usr/bin/env python3
"""Per-issue fetch layer for the comment-triggered gatekeeper (issue #319).

Builds the single-issue, single-comment snapshot that `parse_commands.process`
already consumes as a one-element `issues` list, straight from a raw GitHub
`issue_comment` webhook event payload (`github.event` in the Actions job that
runs on `on: issue_comment: [created]`) — no GitHub API round-trip is needed
for this, since the event itself already carries the issue's number, labels,
milestone, and the one new comment.

Pure function, no GitHub I/O (mirrors the `process()` / `fetch_state()` split
used by `pipeline-reconcile/reconcile.py`): `build_snapshot` only shapes data
already in hand. The workflow script (not unit-tested, like `fetch_state`) is
responsible for reading the event off disk (`GITHUB_EVENT_PATH`), fetching the
live open-milestones list, calling this, then `parse_commands.process`, then
the apply layer (`apply_actions.py`).

Skips two cases outright, before ever reaching the parser:
  * a PR comment — `issue_comment` fires for pull requests too; a PR's
    `issue` object carries a `pull_request` key that a plain issue never has.
  * a bot-authored comment — defense-in-depth on top of the platform's own
    no-recursive-trigger guard for `GITHUB_TOKEN`-authored events (the
    workflow's own ack comment must never be able to loop back in).

Everyone-but-the-owner is intentionally NOT filtered here — that is the
gatekeeper's owner-gate, enforced by `parse_commands.process` itself (via
`skipped: [{"reason": "not-owner"}]`) so there is exactly one place that rule
lives, matching every other pipeline entry point.
"""

DASHBOARD_ISSUE_NUMBER = 193
EPIC_LABEL = "type:epic"


def _is_bot_comment(comment):
    user = comment.get("user") or {}
    if user.get("type") == "Bot":
        return True
    login = user.get("login") or ""
    return login.endswith("[bot]")


def build_snapshot(event, repo_owner, milestones=None):
    """Build the one-element `issues` snapshot from an `issue_comment` event.

    Returns `(snapshot, None)` when the event should be processed — `snapshot`
    is the same `data` dict `parse_commands.process` expects. Returns
    `(None, skip_reason)` — `"pr-comment"` or `"bot-comment"` — when the event
    must be ignored before ever reaching the parser.
    """
    issue = event.get("issue") or {}
    comment = event.get("comment") or {}

    if "pull_request" in issue:
        return None, "pr-comment"
    if _is_bot_comment(comment):
        return None, "bot-comment"

    labels = [l["name"] for l in issue.get("labels", [])]
    milestone = issue.get("milestone")
    number = issue["number"]

    snapshot_issue = {
        "number": number,
        "labels": labels,
        "is_epic": EPIC_LABEL in labels,
        "is_dashboard": number == DASHBOARD_ISSUE_NUMBER,
        "milestone": milestone["title"] if milestone else None,
        "comments": [{
            "id": comment["id"],
            "author": (comment.get("user") or {}).get("login"),
            "body": comment.get("body", ""),
            "processed": False,
        }],
    }
    return {
        "repo_owner": repo_owner,
        "milestones": list(milestones or []),
        "issues": [snapshot_issue],
    }, None
