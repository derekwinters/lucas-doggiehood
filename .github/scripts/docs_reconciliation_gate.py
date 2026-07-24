#!/usr/bin/env python3
"""Docs-reconciliation gate decision for `.github/workflows/docs-test.yml`.

This is the code-only branch of the docs-test gate: a PR that touches no docs
must either be release-please's mechanical release PR or carry the `skip-docs`
label, otherwise it fails asking for a docs reconciliation.

Issue #254 — kill the transient red on skip-docs PRs. `pipeline-dev` cannot set
`skip-docs` atomically at PR creation, so it opens the PR and applies the label
a moment later. The workflow triggers on both `opened` and `labeled`, and the
`opened` event's payload label snapshot is `[]` — under the old inline gate that
made the `opened` run fail (exit 1) before the `labeled` run re-ran and passed.
The PR ended green, but the superseded failing run still fired a CI-failure
webhook — a false alarm on essentially every code-only/tooling PR.

The fix: when the triggering event carries no `skip-docs` label, don't fail
immediately — poll the PR's *live* label set for a short grace window. Because
`pipeline-dev` applies the label within seconds, the SAME `opened` run observes
it and passes, so no failing run is ever produced. Gate semantics are otherwise
untouched: a docs change, a release PR, or a label present up front still pass,
and a genuine code-only PR that never gets docs or the label still fails once
the grace window elapses.

The decision is a pure function (`evaluate`) so it can be unit-tested without
network or a live workflow; `main` wires it to `gh` for the live-label fetch.
"""

import json
import os
import subprocess
import sys
import time

SKIP_LABEL = "skip-docs"

ERROR_MESSAGE = (
    "Documentation changes are almost always needed for a behavior/design "
    "change. This PR changes no docs/** page. Reconcile the affected spec in "
    "this PR, or — if it truly needs none — add the 'skip-docs' label."
)

# Defaults for the live-label grace poll. pipeline-dev applies skip-docs within
# ~8s of opening the PR (verified on #253), so 45s of grace with a 5s cadence
# absorbs the race with wide margin while adding at most ~45s to a PR that
# genuinely fails the gate.
DEFAULT_GRACE_SECONDS = 45.0
DEFAULT_POLL_INTERVAL = 5.0


class GateResult:
    """Outcome of the gate decision."""

    def __init__(self, passed, message):
        self.passed = passed
        self.message = message

    @property
    def exit_code(self):
        return 0 if self.passed else 1


def evaluate(
    *,
    docs_changed,
    is_release,
    skip_present,
    fetch_live_labels=None,
    grace_seconds=0.0,
    poll_interval=DEFAULT_POLL_INTERVAL,
    clock=None,
    sleep=None,
):
    """Decide the docs-reconciliation gate outcome.

    Args:
        docs_changed: PR touches docs (handled by the strict site-build path).
        is_release: PR is release-please's release PR (exempt).
        skip_present: `skip-docs` was present in the triggering event payload.
        fetch_live_labels: optional zero-arg callable returning the PR's current
            label names; polled during the grace window when the label was not
            in the payload. Absorbs the opened/labeled race (#254).
        grace_seconds: how long to poll live labels before failing.
        poll_interval: seconds between live-label polls.
        clock / sleep: injectable time source for deterministic tests.
    """
    if docs_changed:
        return GateResult(True, "PR touches docs — handled by the strict site-build path.")
    if is_release:
        return GateResult(
            True, "release-please release PR — docs reconciliation gate does not apply."
        )
    if skip_present:
        return GateResult(
            True, "skip-docs label present — docs reconciliation gate bypassed for this PR."
        )

    # No skip-docs label in the triggering payload. Rather than fail on the bare
    # `opened` event (the #254 flap), poll the live label set for a grace window.
    if fetch_live_labels is not None and grace_seconds > 0:
        clock = clock or time.monotonic
        sleep = sleep or time.sleep
        deadline = clock() + grace_seconds
        while True:
            if SKIP_LABEL in fetch_live_labels():
                return GateResult(
                    True,
                    "skip-docs label observed on the live PR within the grace window "
                    "— gate bypassed (absorbs the opened/labeled race, #254).",
                )
            remaining = deadline - clock()
            if remaining <= 0:
                break
            sleep(min(poll_interval, remaining))

    return GateResult(False, ERROR_MESSAGE)


def _gh_live_labels(repo, pr_number):
    """Fetch the PR's current label names via the GitHub CLI."""
    proc = subprocess.run(
        [
            "gh",
            "api",
            "-H",
            "Accept: application/vnd.github+json",
            f"repos/{repo}/issues/{pr_number}/labels",
            "--jq",
            ".[].name",
        ],
        capture_output=True,
        text=True,
    )
    if proc.returncode != 0:
        # A transient API hiccup must not itself fail the gate; treat as "no
        # label observed this poll" and let the loop retry / time out.
        sys.stderr.write(proc.stderr)
        return []
    return [line for line in proc.stdout.splitlines() if line.strip()]


def main():
    docs_changed = os.environ.get("DOCS_CHANGED") == "true"
    is_release = os.environ.get("IS_RELEASE") == "true"

    try:
        initial_labels = json.loads(os.environ.get("PR_LABELS_JSON", "[]"))
    except json.JSONDecodeError:
        initial_labels = []
    skip_present = SKIP_LABEL in initial_labels

    repo = os.environ.get("REPO", "")
    pr_number = os.environ.get("PR_NUMBER", "")
    grace_seconds = float(os.environ.get("GRACE_SECONDS", DEFAULT_GRACE_SECONDS))
    poll_interval = float(os.environ.get("POLL_INTERVAL", DEFAULT_POLL_INTERVAL))

    fetch = None
    if repo and pr_number:
        fetch = lambda: _gh_live_labels(repo, pr_number)  # noqa: E731

    result = evaluate(
        docs_changed=docs_changed,
        is_release=is_release,
        skip_present=skip_present,
        fetch_live_labels=fetch,
        grace_seconds=grace_seconds,
        poll_interval=poll_interval,
    )

    if result.passed:
        print(result.message)
    else:
        print(f"::error::{result.message}")
    sys.exit(result.exit_code)


if __name__ == "__main__":
    main()
