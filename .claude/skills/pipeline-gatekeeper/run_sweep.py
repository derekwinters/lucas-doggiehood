#!/usr/bin/env python3
"""Glue for the board-wide gatekeeper sweep workflow (issue #319, Part B).

Runs `check_revisits` (blocker auto-revisit, #241) and `reconcile`'s
auto-fixes board-wide, applying results via the GitHub REST API (stdlib
`urllib` — same shape as `pipeline-reconcile/reconcile.py`'s own
`fetch_state`). Two modes, chosen by `--cron`:

  * event mode (default) — driven by `issues: [closed, labeled]` /
    `pull_request: [closed]`. Runs `check_revisits` + `reconcile` in
    `events_only=True` mode: strip-stale-label + merged-but-open flag apply,
    but `requeue` is withheld. See `reconcile.process`'s `events_only`
    docstring for the auto-close race this avoids — at the instant a PR
    merges, a just-merged `in-progress` issue can transiently look exactly
    like a stalled one, and requeuing it right then would re-arm the #109
    re-pick loop.
  * cron mode (`--cron`) — the low-frequency schedule backstop. Adds the
    `requeue` auto-fix (a genuine stall has no triggering event anyway, so
    nothing is lost by only requeuing here), and re-processes any
    `issue_comment` command the primary `gatekeeper-comment.yml` workflow
    missed (e.g. a dropped webhook delivery) via the same 👀 watermark
    idempotency `parse_commands.process` already relies on.

Not unit-tested — pure network glue over already-tested pure functions
(`check_revisits.check_blocker_revisits`, `reconcile.process`,
`parse_commands.process`, `apply_actions.*`). See
`.github/workflows/gatekeeper-sweep.yml` and
`docs/engineering/issue-pipeline.md`.
"""

import os
import sys

_HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, _HERE)
sys.path.insert(0, os.path.join(_HERE, os.pardir, "pipeline-reconcile"))
import apply_actions  # noqa: E402
import check_revisits  # noqa: E402
import fire_routine  # noqa: E402
import parse_commands  # noqa: E402
import reconcile  # noqa: E402
from _github_api import request  # noqa: E402

REPO_DEFAULT = "derekwinters/lucas-doggiehood"
REPO_OWNER = "derekwinters"
DASHBOARD_ISSUE = 193
EPIC_LABEL = "type:epic"


def _fetch_open_issues(repo, token, want_comments):
    """Open issues (number, labels, body, milestone, is_epic, is_dashboard),
    with comments populated only when `want_comments` — the cron-only
    missed-command safety net; the event modes never need them."""
    raw = reconcile._paginate("/repos/%s/issues?state=open" % repo, token)
    raw = [i for i in raw if "pull_request" not in i]
    out = []
    for i in raw:
        labels = [l["name"] for l in i.get("labels", [])]
        entry = {
            "number": i["number"],
            "labels": labels,
            "body": i.get("body") or "",
            "is_epic": EPIC_LABEL in labels,
            "is_dashboard": i["number"] == DASHBOARD_ISSUE,
        }
        ms = i.get("milestone")
        entry["milestone"] = ms["title"] if ms else None
        if want_comments and i.get("comments", 0) > 0:
            comments = request(
                "GET", "/repos/%s/issues/%d/comments?per_page=100"
                % (repo, i["number"]), token) or []
            entry["comments"] = [{
                "id": c["id"],
                "author": c["user"]["login"],
                "body": c.get("body", ""),
                "processed": _has_watermark(repo, token, c["id"]),
            } for c in comments]
        else:
            entry["comments"] = []
        out.append(entry)
    return out


def _has_watermark(repo, token, comment_id):
    reactions = request(
        "GET", "/repos/%s/issues/comments/%d/reactions?per_page=100"
        % (repo, comment_id), token) or []
    return any(r["content"] == "eyes" for r in reactions)


def _replace_labels(repo, token, number, remove=(), add=()):
    issue = request("GET", "/repos/%s/issues/%d" % (repo, number), token)
    current = [l["name"] for l in issue.get("labels", [])]
    remove = set(remove)
    new_labels = [l for l in current if l not in remove]
    for l in add:
        if l not in new_labels:
            new_labels.append(l)
    if set(new_labels) != set(current):
        request("PUT", "/repos/%s/issues/%d/labels" % (repo, number), token,
                 {"labels": new_labels})


def _apply_revisit(repo, token, revisit, current_labels):
    number = revisit["issue"]
    _replace_labels(repo, token, number,
                     remove=revisit["remove_labels"], add=revisit["add_labels"])
    # Reactive triage (#378): a blocker-cleared revisit re-adds `ai-triage`,
    # so fire the analysis Routine for this issue immediately (best-effort).
    new_labels = apply_actions.merge_labels(current_labels, revisit)
    if apply_actions.fires_triage(current_labels, new_labels):
        fire_routine.fire(number, repo)
    blockers = ", ".join("#%d" % b for b in revisit["blockers_resolved"])
    text = ("Blocker(s) %s cleared — revisiting.\n\nYour move: %s"
            % (blockers, parse_commands.MENUS[revisit["menu"]]))
    request("POST", "/repos/%s/issues/%d/comments" % (repo, number), token,
             {"body": text})


def _apply_comment_action(repo, token, action):
    issue = request("GET", "/repos/%s/issues/%d" % (repo, action["issue"]), token)
    current_labels = [l["name"] for l in issue.get("labels", [])]
    new_labels = apply_actions.merge_labels(current_labels, action)
    if set(new_labels) != set(current_labels):
        request("PUT", "/repos/%s/issues/%d/labels"
                 % (repo, action["issue"]), token, {"labels": new_labels})
        # Reactive triage (#378): a missed /admit etc. replayed by the cron
        # safety net still fires the Routine when it newly adds `ai-triage`.
        if apply_actions.fires_triage(current_labels, new_labels):
            fire_routine.fire(action["issue"], repo)
    ack = apply_actions.render_ack(action)
    if ack:
        request("POST", "/repos/%s/issues/%d/comments"
                 % (repo, action["issue"]), token, {"body": ack})
    for reaction in apply_actions.reactions_for(action):
        request("POST", "/repos/%s/issues/comments/%d/reactions"
                % (repo, action["comment_id"]), token, {"content": reaction})


def main(argv):
    cron = "--cron" in argv
    token = os.environ.get("GITHUB_TOKEN")
    if not token:
        sys.stderr.write("GITHUB_TOKEN is required.\n")
        return 2
    repo = os.environ.get("GATEKEEPER_REPO", REPO_DEFAULT)

    open_issues = _fetch_open_issues(repo, token, want_comments=cron)

    # 1. Blocker auto-revisit (#241) — board-wide, both modes.
    revisit_input = [{"number": i["number"], "labels": i["labels"],
                       "body": i["body"]} for i in open_issues]
    labels_by_issue = {i["number"]: i["labels"] for i in open_issues}
    for revisit in check_revisits.check_blocker_revisits(revisit_input):
        _apply_revisit(repo, token, revisit,
                       labels_by_issue.get(revisit["issue"], []))
        sys.stderr.write("#%d revisit (blockers %s)\n"
                          % (revisit["issue"], revisit["blockers_resolved"]))

    # 2. Reconcile — strip-stale + merged-but-open flag always; `requeue`
    #    only on the cron backstop (events_only gates it — see reconcile.py).
    rec_state = reconcile.fetch_state(repo, token)
    findings = reconcile.process(rec_state, events_only=not cron)
    for f in findings["strip_labels"]:
        _replace_labels(repo, token, f["number"], remove=f["labels"])
        sys.stderr.write("#%d strip stale labels %s\n"
                          % (f["number"], f["labels"]))
    for f in findings["requeue"]:
        _replace_labels(repo, token, f["number"],
                         remove=["in-progress"], add=["ready-for-work"])
        sys.stderr.write("#%d requeue -> ready-for-work\n" % f["number"])
    # flag_* findings are read-only — surfaced by the dashboard, never acted
    # on here (see pipeline-reconcile's non-negotiable: the sweep never
    # closes an issue).

    # 3. Missed-command safety net — cron only. Re-processes any /command
    #    whose comment never got the 👀 watermark (e.g. a dropped webhook
    #    delivery that gatekeeper-comment.yml never saw).
    if cron:
        milestones = [m["title"] for m in request(
            "GET", "/repos/%s/milestones?state=open&per_page=100" % repo,
            token)]
        snapshot = {
            "repo_owner": REPO_OWNER,
            "milestones": milestones,
            "issues": [{
                "number": i["number"], "labels": i["labels"],
                "is_epic": i["is_epic"], "is_dashboard": i["is_dashboard"],
                "milestone": i["milestone"], "comments": i["comments"],
            } for i in open_issues],
        }
        out = parse_commands.process(snapshot)
        for action in out["actions"]:
            _apply_comment_action(repo, token, action)
            sys.stderr.write("#%d %s (missed-command sweep)\n"
                              % (action["issue"], ",".join(action["commands"])))

    return 0


if __name__ == "__main__":
    raise SystemExit(main(sys.argv[1:]))
