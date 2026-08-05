#!/usr/bin/env python3
"""Drive the release-please merge flow: regenerate check, force CI onto the
release PR, squash-merge, and verify the tag.

Two non-obvious things bite every release (see SKILL.md for the full runbook):

  1. **Regeneration lag.** After the last feature PR merges, release-please
     needs a moment to rebase the release PR's base onto the new ``main`` and
     add the just-merged commits to the changelog. Merge too early and the
     release ships missing commits. ``is_regenerated`` gates on both: the PR's
     base SHA equals live ``main`` AND every expected PR number is in the body.

  2. **The release PR gets ZERO CI checks.** release-please pushes its branch
     with the built-in ``GITHUB_TOKEN``, and GitHub deliberately does not run
     ``on: pull_request`` / ``on: push`` workflows for ``GITHUB_TOKEN`` pushes
     (loop-prevention). The release PR sits at 0 checks, ``pending``, blocked.
     We force real checks by **close → reopen** (fires the ``reopened`` event).
     This is the *sole* CI-trigger path: an audit of every workflow that runs
     against the release PR found NONE declaring ``workflow_dispatch`` — so
     ``choose_ci_trigger_action`` hard-codes close/reopen with no branch.

Same split as the other deterministic pipeline skills (``set_blocker.py``): the
pure logic (SHA/changelog gates, check classification, request builders,
verification predicate, poll loops with an injectable clock) is unit-tested in
``tests/test_release_flow.py``; GitHub I/O lives only in the ``_api_request``
edge and the ``main()`` orchestration seam, which are not unit-tested.

GITHUB_TOKEN (or GH_TOKEN) must be set. Exit code 0 on success, 1 on error.
"""

import argparse
import json
import os
import re
import sys
import time
import urllib.error
import urllib.request

REPO_DEFAULT = "derekwinters/lucas-doggiehood"
API = "https://api.github.com"

# The only checks that actually fire on the release PR. Everything else
# (ci-tests, pr-build, geometry-lint, pipeline-tests) is path-filtered to
# Assets/**, Packages/**, or .claude/skills/pipeline-*/**; the release PR only
# touches VERSION, CHANGELOG.md, and .github/release-please/manifest.json, so
# those never register a check on it.
REQUIRED_CHECKS = (
    "docs-test / build",
    "docs-test / gate-tests",
    "pr-title-lint / lint",
)

# Poll-loop tuning (mirrors ci-watch's default cadence/timeout).
CHECK_POLL_INTERVAL_SECONDS = 30
CHECK_TIMEOUT_POLLS = 40
# Tagging happens asynchronously after the squash merge lands, so poll for it.
VERIFY_POLL_INTERVAL_SECONDS = 15
VERIFY_TIMEOUT_POLLS = 20

# Poll results (same vocabulary as ci-watch's result block).
PASSED = "PASSED"
FAILED = "FAILED"
PENDING = "PENDING"
TIMEOUT = "TIMEOUT"

# Conclusions that count as green.
_PASSING_CONCLUSIONS = ("success", "skipped", "neutral")

# The CI-trigger action. Close → reopen is the ONLY supported path (see module
# docstring / SKILL.md); there is deliberately no workflow_dispatch alternative.
CI_TRIGGER_CLOSE_REOPEN = "close_reopen"

# release-please's autorelease label states.
AUTORELEASE_PENDING = "autorelease: pending"
AUTORELEASE_TAGGED = "autorelease: tagged"

_RELEASE_TITLE_RE = re.compile(r"^chore\(main\):\s*release\s+(\d+\.\d+\.\d+)\s*$")


# --- regeneration gate (pure) ----------------------------------------------

def contains_pr_number(text, number):
    """True if ``#number`` appears as a whole token in ``text`` (``#57`` does
    not satisfy ``#573``)."""
    return re.search(r"(?<!\d)#%d(?!\d)" % number, text or "") is not None


def missing_pr_numbers(text, expected_pr_numbers):
    """The subset of ``expected_pr_numbers`` NOT present in ``text``."""
    return [n for n in expected_pr_numbers if not contains_pr_number(text, n)]


def is_regenerated(pr, expected_pr_numbers, latest_main_sha):
    """True only when release-please has fully regenerated the release PR:
    its base SHA equals live ``main`` AND every expected merged-PR number is
    listed in its body/changelog."""
    if pr.get("base_sha") != latest_main_sha:
        return False
    return not missing_pr_numbers(pr.get("body") or "", expected_pr_numbers)


# --- CI-trigger selection (pure) -------------------------------------------

def choose_ci_trigger_action(workflows=None):
    """Always close → reopen. No workflow that runs against the release PR
    declares ``workflow_dispatch`` (trigger-block audit in SKILL.md), so there
    is no other path to branch to. ``workflows`` is accepted only to make the
    intentional lack of branching explicit and future-auditable."""
    return CI_TRIGGER_CLOSE_REOPEN


# --- check classification + poll loop (pure) -------------------------------

def classify_checks(checks, required_names):
    """Reduce a check-run list to ``PASSED`` / ``FAILED`` / ``PENDING`` over the
    required set (mirrors ci-watch: skipped counts as passing)."""
    pending = False
    for name in required_names:
        matches = [c for c in checks if c.get("name") == name]
        if not matches:
            pending = True
            continue
        for c in matches:
            if c.get("status") != "completed":
                pending = True
            elif c.get("conclusion") not in _PASSING_CONCLUSIONS:
                return FAILED
    return PENDING if pending else PASSED


def poll_checks(fetch_checks, required_names,
                timeout_polls=CHECK_TIMEOUT_POLLS, sleep_fn=None):
    """Poll ``fetch_checks()`` until the required checks resolve. Returns
    ``PASSED`` / ``FAILED`` as soon as decided, else ``TIMEOUT`` after
    ``timeout_polls`` polls. ``sleep_fn`` is injected for testability."""
    for _ in range(timeout_polls):
        result = classify_checks(fetch_checks(), required_names)
        if result in (PASSED, FAILED):
            return result
        if sleep_fn is not None:
            sleep_fn(CHECK_POLL_INTERVAL_SECONDS)
    return TIMEOUT


# --- squash-merge request builder (pure) -----------------------------------

def version_from_title(title):
    """Parse ``X.Y.Z`` from a ``chore(main): release X.Y.Z`` PR title."""
    m = _RELEASE_TITLE_RE.match(title or "")
    if not m:
        raise ValueError("not a release PR title: %r" % title)
    return m.group(1)


def version_from_version_file(content):
    """The version from the repo's ``/VERSION`` file (release-please owns it)."""
    return (content or "").strip()


def release_commit_title(version):
    return "chore(main): release %s" % version


def merge_path(repo, number):
    return "/repos/%s/pulls/%d/merge" % (repo, number)


def pr_path(repo, number):
    return "/repos/%s/pulls/%d" % (repo, number)


def build_merge_request(repo, number, version):
    """The squash-merge request: PUT the merge endpoint with a ``squash`` method
    and the ``chore(main): release X.Y.Z`` commit title release-please detects."""
    return {
        "method": "PUT",
        "path": merge_path(repo, number),
        "payload": {
            "merge_method": "squash",
            "commit_title": release_commit_title(version),
        },
    }


# --- post-merge verification (pure) ----------------------------------------

def tag_name(version):
    return "v%s" % version


def is_release_complete(state, version):
    """True only when the ``vX.Y.Z`` tag exists, the GitHub Release exists, and
    the release PR's label has flipped to ``autorelease: tagged``."""
    tag = tag_name(version)
    return (
        tag in state.get("tags", [])
        and tag in state.get("releases", [])
        and AUTORELEASE_TAGGED in state.get("labels", [])
    )


def poll_verification(fetch_state, version,
                      timeout_polls=VERIFY_TIMEOUT_POLLS, sleep_fn=None):
    """Poll ``fetch_state()`` until the release is fully tagged, bounded by
    ``timeout_polls`` (tagging is async after the merge lands)."""
    for _ in range(timeout_polls):
        if is_release_complete(fetch_state(), version):
            return True
        if sleep_fn is not None:
            sleep_fn(VERIFY_POLL_INTERVAL_SECONDS)
    return False


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
    req.add_header("User-Agent", "doggiehood-release-flow")
    if data is not None:
        req.add_header("Content-Type", "application/json")
    with urllib.request.urlopen(req) as resp:
        body = resp.read()
        return json.loads(body) if body else None


def fetch_latest_main_sha(repo, token):
    ref = _api_request("GET", "/repos/%s/commits/main" % repo, token)
    return ref["sha"]


def fetch_pr(repo, number, token):
    pr = _api_request("GET", pr_path(repo, number), token)
    return {
        "number": pr["number"],
        "title": pr["title"],
        "body": pr.get("body") or "",
        "base_sha": pr["base"]["sha"],
        "head_ref": pr["head"]["ref"],
        "labels": [lbl["name"] for lbl in pr.get("labels", [])],
    }


def fetch_pr_checks(repo, head_sha, token):
    """Flatten the combined check-runs on the PR head into ci-watch-style
    ``{name, status, conclusion}`` rows, naming each ``<workflow> / <job>`` to
    match branch-protection required-check names."""
    data = _api_request(
        "GET", "/repos/%s/commits/%s/check-runs?per_page=100" % (repo, head_sha),
        token) or {}
    rows = []
    for run in data.get("check_runs", []):
        rows.append({
            "name": run.get("name"),
            "status": run.get("status"),
            "conclusion": run.get("conclusion"),
        })
    return rows


def close_then_reopen_pr(repo, number, token):
    """Fire the ``reopened`` ``pull_request`` event so the workflows finally run
    against the release PR (they never ran on release-please's GITHUB_TOKEN
    push)."""
    _api_request("PATCH", pr_path(repo, number), token, {"state": "closed"})
    _api_request("PATCH", pr_path(repo, number), token, {"state": "open"})


def merge_pr(request, token):
    return _api_request(request["method"], request["path"], token,
                        request["payload"])


def fetch_release_state(repo, number, version, token):
    tag = tag_name(version)
    tags = []
    try:
        _api_request("GET", "/repos/%s/git/ref/tags/%s" % (repo, tag), token)
        tags.append(tag)
    except urllib.error.HTTPError as exc:
        if exc.code != 404:
            raise
    releases = []
    try:
        _api_request("GET", "/repos/%s/releases/tags/%s" % (repo, tag), token)
        releases.append(tag)
    except urllib.error.HTTPError as exc:
        if exc.code != 404:
            raise
    pr = _api_request("GET", pr_path(repo, number), token)
    labels = [lbl["name"] for lbl in pr.get("labels", [])]
    return {"tags": tags, "releases": releases, "labels": labels}


# --- orchestration seam (not unit-tested) ----------------------------------

def run(repo, number, expected_pr_numbers, token, do_merge=True,
        sleep_fn=time.sleep):
    """End-to-end release-please merge flow. Returns 0 on success, 1 on error.
    The interesting decisions all delegate to the pure helpers above."""
    pr = fetch_pr(repo, number, token)
    latest_main = fetch_latest_main_sha(repo, token)

    if not is_regenerated(pr, expected_pr_numbers, latest_main):
        gap = missing_pr_numbers(pr["body"], expected_pr_numbers)
        sys.stderr.write(
            "Release PR #%d is not regenerated yet: base==main? %s; missing PRs: %s\n"
            % (number, pr["base_sha"] == latest_main,
               ", ".join("#%d" % n for n in gap) or "none"))
        return 1

    version = version_from_title(pr["title"])

    action = choose_ci_trigger_action()
    print("Triggering CI via %s on release PR #%d" % (action, number))
    close_then_reopen_pr(repo, number, token)

    check_result = poll_checks(
        lambda: fetch_pr_checks(repo, _head_sha(repo, number, token), token),
        REQUIRED_CHECKS, sleep_fn=sleep_fn)
    if check_result != PASSED:
        sys.stderr.write("CI did not pass (%s); not merging.\n" % check_result)
        return 1

    if not do_merge:
        print("Checks green; --no-merge set, stopping before merge.")
        return 0

    request = build_merge_request(repo, number, version)
    merge_pr(request, token)
    print("Squash-merged #%d as %s" % (number, release_commit_title(version)))

    ok = poll_verification(
        lambda: fetch_release_state(repo, number, version, token),
        version, sleep_fn=sleep_fn)
    if not ok:
        sys.stderr.write(
            "Merged, but tag/Release/label did not fully materialize in time.\n")
        return 1
    print("Verified: tag %s + GitHub Release exist and label flipped to '%s'."
          % (tag_name(version), AUTORELEASE_TAGGED))
    return 0


def _head_sha(repo, number, token):
    pr = _api_request("GET", pr_path(repo, number), token)
    return pr["head"]["sha"]


def main(argv=None):
    parser = argparse.ArgumentParser(
        description="Drive the release-please merge flow for the release PR.")
    parser.add_argument("pr", type=int, help="the release PR number")
    parser.add_argument("--expected-pr", type=int, action="append", default=[],
                        metavar="N", dest="expected_prs",
                        help="a merged PR number that MUST appear in the changelog "
                             "before merging (repeatable)")
    parser.add_argument("--repo", type=parse_repo, default=REPO_DEFAULT,
                        help="owner/name (default %(default)s)")
    parser.add_argument("--no-merge", action="store_true",
                        help="stop after CI passes; do not squash-merge")
    args = parser.parse_args(argv)

    token = _token()
    try:
        return run(args.repo, args.pr, args.expected_prs, token,
                   do_merge=not args.no_merge)
    except urllib.error.HTTPError as exc:
        sys.stderr.write("GitHub API error %d: %s\n"
                         % (exc.code, exc.read().decode(errors="replace")))
        return 1


if __name__ == "__main__":
    sys.exit(main())
