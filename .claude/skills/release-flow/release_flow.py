#!/usr/bin/env python3
"""Drive the release-please merge flow: regenerate check, force CI onto the
release PR, squash-merge, and verify the tag.

Two non-obvious things bite every release (see SKILL.md for the full runbook):

  1. **Regeneration lag.** After the last feature PR merges, release-please
     needs a moment to rebase the release PR's base onto the new ``main`` and
     add the just-merged commits to the changelog. Merge too early and the
     release ships missing commits. ``is_regenerated`` gates on both: the PR's
     base SHA equals live ``main`` AND every expected PR number is in the body.

  2. **The release PR's CI is parked awaiting approval, and this skill cannot
     release it.** The workflow runs for the release PR head *do* exist, but
     GitHub holds them at ``status: completed, conclusion: action_required`` —
     the state the UI's "Approve and run" button clears — so the head commit
     carries **0 check runs** and the PR is ``blocked``. Every API that would
     release them (``workflow_dispatch``, run ``approve``, run ``rerun``) needs
     the ``actions: write`` scope, which this skill's token does not have: all
     three return ``403 Resource not accessible by integration``. So the skill
     **halts and asks the user** (#618) instead of forcing CI. It deliberately
     does NOT close → reopen the PR: toggling a release PR's state is a heavy,
     surprising side effect and is banned.

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

# The checks we require green on the release PR, spelled with the RAW check-run
# names GitHub reports (what ``fetch_pr_checks`` returns via ``run["name"]``),
# NOT the branch-protection "<workflow> / <job>" form — see #631. GitHub names a
# check run after the job's ``name:`` if set, else the job id: docs-test.yml's
# jobs are ``build`` and ``gate-tests`` (no ``name:``), and pr-title-lint.yml's
# ``lint`` job sets ``name: Conventional Commits PR title``.
#
# Everything else that lands on the release PR (Debug APK, Release-candidate
# APK, sweep, ...) is non-required noise: those workflows are either
# path-filtered to Assets/**, Packages/**, or .claude/skills/pipeline-*/** (the
# release PR only touches VERSION, CHANGELOG.md, and
# .github/release-please/manifest.json) or are not gates for the release, so we
# classify over this required subset only.
REQUIRED_CHECKS = (
    "build",
    "gate-tests",
    "Conventional Commits PR title",
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

# The CI-trigger action. There is exactly one (#618): halt and ask the user.
# No automated force-CI path exists — see the module docstring / SKILL.md — and
# close → reopen is banned, so the skill never triggers CI by itself.
CI_TRIGGER_ASK_USER = "ask_user"

# GitHub's conclusion for a run it parked pending manual approval. This is what
# the release PR's runs sit at, and what "Approve and run" in the UI clears.
AWAITING_APPROVAL_CONCLUSION = "action_required"

# How the user may answer the halt prompt.
CI_ANSWER_CONTINUE = "continue"  # CI was approved/run by hand; poll for green
CI_ANSWER_SKIP = "skip"          # waive CI; squash-merge on owner override
CI_ANSWER_ABORT = "abort"        # anything else: stop, changing nothing

_CONTINUE_ANSWERS = ("continue", "c", "yes", "y", "run", "ran", "approved")
_SKIP_ANSWERS = ("skip", "s", "override", "no-ci")

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


# --- CI-trigger selection + the halt prompt (pure) -------------------------

def choose_ci_trigger_action(workflows=None):
    """Always halt and ask the user (#618). No API this skill's token can reach
    starts the release PR's parked runs, and close → reopen is banned, so there
    is nothing to branch to. ``workflows`` is accepted only to keep the
    intentional lack of branching explicit and future-auditable."""
    return CI_TRIGGER_ASK_USER


def runs_awaiting_approval(runs):
    """The workflow runs GitHub parked pending manual approval — exactly the
    ones the user has to release with "Approve and run"."""
    return [r for r in runs
            if r.get("conclusion") == AWAITING_APPROVAL_CONCLUSION]


def run_url(repo, run_id):
    return "https://github.com/%s/actions/runs/%s" % (repo, run_id)


def pr_checks_url(repo, number):
    return "https://github.com/%s/pull/%d/checks" % (repo, number)


def format_ci_prompt(repo, number, awaiting):
    """The halt message. Tells the user exactly what to click and what to
    answer; never suggests toggling the PR's state."""
    lines = [
        "",
        "CI on release PR #%d cannot be started from here." % number,
        "  This skill's token has no 'actions: write' scope: workflow_dispatch,",
        "  run approve and run rerun all return 403. Toggling the release PR's",
        "  state to force CI is banned (#618), so nothing was changed.",
        "",
    ]
    if awaiting:
        lines.append("  These runs are waiting for your approval "
                     "(\"Approve and run\"):")
        for r in awaiting:
            lines.append("    %-16s %s" % (r.get("name"),
                                           run_url(repo, r.get("id"))))
    else:
        lines.append("  No runs are parked awaiting approval; start the "
                     "required checks from:")
        lines.append("    %s" % pr_checks_url(repo, number))
    lines += [
        "",
        "  Then answer:",
        "    continue - the required checks are running; wait for them to go green",
        "    skip     - waive CI and squash-merge on owner override",
        "    (anything else aborts, changing nothing)",
        "",
    ]
    return "\n".join(lines)


def parse_ci_answer(text):
    """Map the user's reply at the halt prompt to CONTINUE / SKIP / ABORT.
    Anything unrecognized aborts, so a stray keypress can never merge."""
    value = (text or "").strip().lower()
    if value in _CONTINUE_ANSWERS:
        return CI_ANSWER_CONTINUE
    if value in _SKIP_ANSWERS:
        return CI_ANSWER_SKIP
    return CI_ANSWER_ABORT


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


def fetch_workflow_runs(repo, head_sha, token):
    """The workflow runs for the release PR head, as ``{id, name, conclusion}``
    rows. Read-only: listing runs needs only ``actions: read``, which the token
    does have (unlike the write endpoints that could start them)."""
    data = _api_request(
        "GET", "/repos/%s/actions/runs?head_sha=%s&per_page=100"
        % (repo, head_sha), token) or {}
    return [{"id": r.get("id"), "name": r.get("name"),
             "conclusion": r.get("conclusion")}
            for r in data.get("workflow_runs", [])]


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
        sleep_fn=time.sleep, ci_answer=None, ask_fn=None):
    """End-to-end release-please merge flow. Returns 0 on success, 1 on error.
    The interesting decisions all delegate to the pure helpers above.

    ``ci_answer`` pre-answers the CI halt prompt for non-interactive runs
    (``--ci continue`` / ``--ci skip``); otherwise ``ask_fn`` (default
    ``input``) reads the answer from the user."""
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

    # The CI step is a HALT, not a trigger (#618): ask, then act on the answer.
    action = choose_ci_trigger_action()
    if action != CI_TRIGGER_ASK_USER:
        sys.stderr.write("Unsupported CI trigger action %r.\n" % action)
        return 1
    head_sha = _head_sha(repo, number, token)
    awaiting = runs_awaiting_approval(
        fetch_workflow_runs(repo, head_sha, token))
    sys.stderr.write(format_ci_prompt(repo, number, awaiting))
    answer = parse_ci_answer(
        ci_answer if ci_answer is not None else (ask_fn or input)("> "))

    if answer == CI_ANSWER_ABORT:
        sys.stderr.write("Aborted; the release PR was not touched.\n")
        return 1

    if answer == CI_ANSWER_CONTINUE:
        check_result = poll_checks(
            lambda: fetch_pr_checks(repo, _head_sha(repo, number, token), token),
            REQUIRED_CHECKS, sleep_fn=sleep_fn)
        if check_result != PASSED:
            sys.stderr.write("CI did not pass (%s); not merging.\n" % check_result)
            return 1
        if not do_merge:
            print("Checks green; --no-merge set, stopping before merge.")
            return 0
    else:
        print("CI waived by the user; merging #%d on owner override." % number)
        if not do_merge:
            print("--no-merge set, stopping before merge.")
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
    parser.add_argument("--ci", choices=(CI_ANSWER_CONTINUE, CI_ANSWER_SKIP),
                        dest="ci_answer", default=None,
                        help="pre-answer the CI halt prompt for non-interactive "
                             "runs: 'continue' (checks were approved by hand) or "
                             "'skip' (waive CI, merge on owner override)")
    args = parser.parse_args(argv)

    token = _token()
    try:
        return run(args.repo, args.pr, args.expected_prs, token,
                   do_merge=not args.no_merge, ci_answer=args.ci_answer)
    except urllib.error.HTTPError as exc:
        sys.stderr.write("GitHub API error %d: %s\n"
                         % (exc.code, exc.read().decode(errors="replace")))
        return 1


if __name__ == "__main__":
    sys.exit(main())
