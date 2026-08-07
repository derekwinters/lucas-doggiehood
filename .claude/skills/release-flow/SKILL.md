---
name: release-flow
description: >
  Drive the release-please merge flow for Doggiehood: confirm the release PR
  has regenerated, halt so the user can approve its parked CI runs (the skill
  never toggles the PR's state), squash-merge with the
  `chore(main): release X.Y.Z` title, and verify the `vX.Y.Z` tag, the GitHub
  Release, and the `autorelease: tagged` label all appear. Use at release time,
  when merging release-please's auto-generated release PR — it captures the two
  gotchas that bite every release so they aren't re-derived.
---

# Release flow — merging release-please's release PR

This skill exists because cutting a release is **not** "merge the green PR."
The release PR is special in two non-obvious ways, and both are silent traps.
`release_flow.py` encodes the flow; this runbook explains why.

`GITHUB_TOKEN` (or `GH_TOKEN`) must be set (it already is in CI and web
sessions).

## The two gotchas

### 1. Regeneration lag (never merge too early)

After the last feature PR merges, release-please needs a moment to **regenerate**
the release PR: rebase its base branch onto the new `main` *and* append the
just-merged commits to `CHANGELOG.md`. If you merge before that finishes, the
release ships **missing commits**.

Concretely, cutting v0.11.0: after merging **#573** the release PR listed only
#573; **#575** appeared only after a *second* regeneration pass. So you must wait
until **both**:

- the release PR's **base SHA == the live `main` SHA**, and
- the changelog/body lists **every** intended PR number.

`is_regenerated(pr, expected_pr_numbers, latest_main_sha)` is exactly this
conjunction. PR-number matching is whole-token (`#57` does not satisfy `#573`).

### 2. The release PR's CI is parked awaiting approval

The release PR sits at **0 check runs** on its head commit, combined status
`pending`, `mergeable_state: blocked`, and it **never goes green on its own**.
(v0.11.0's PR #557 was merged with 0 checks via owner override.)

The reason is *not* that no event fired. Probing the live release PR (#634,
head `38fac4e6`) shows the runs **do** exist — GitHub is just holding them:

```
rc-build       pull_request  completed / action_required
docs-test      pull_request  completed / action_required
pr-build       pull_request  completed / action_required
pr-title-lint  pull_request  completed / action_required
```

`conclusion: action_required` is GitHub's "waiting for approval" state — exactly
what the **"Approve and run"** button in the Actions/Checks UI clears. Until
someone clears it, no job runs and no check run is ever attached to the commit,
which is why `GET /commits/{sha}/check-runs` returns 0.

## Why the skill halts and asks, instead of forcing CI (#618)

**Close → reopen is banned.** The skill used to force CI by toggling the PR
closed and open again (firing `pull_request: reopened`). Derek ruled that out:
toggling a release PR's state is a heavy, surprising side effect — it churns the
PR timeline, can disrupt subscribers and automation, and briefly puts the
release PR into a `closed` state. `close_then_reopen_pr` is gone, and unit tests
assert it stays gone (`NoCloseReopenTests`, including a source scan for a PR
`state` payload).

**No API path replaces it, because the token lacks the scope.** Every candidate
in #618 was probed live against this repo, and all of them fail the same way:

| Candidate trigger | Result |
|---|---|
| `POST .../actions/workflows/{docs-test,pr-title-lint}.yml/dispatches` | **403** `Resource not accessible by integration` |
| Same call against `dashboard.yml`, which **does** declare `workflow_dispatch` | **403** — same error, proving it's the token scope, *not* a missing trigger block |
| `POST .../actions/runs/{id}/approve` on the real `action_required` runs above | **403** |
| `POST .../actions/runs/{id}/rerun` | **403** |
| Empty commit pushed with a PAT | mutates release-please's branch and needs a stored PAT that doesn't exist — #618's own least-preferred option |

The decisive evidence is row 2: the dispatch fails identically on a workflow
that *already* declares `workflow_dispatch`, so **adding `workflow_dispatch:` to
`docs-test.yml` / `pr-title-lint.yml` would not have helped** — the skill's
token simply has no `actions: write`. (It does have `actions: read`, which is
how the skill lists the parked runs to show you.) Granting `actions: write` is a
repo/app permission change only Derek can make; if that ever happens, an
approve-based trigger becomes viable and this section should be revisited.

So the skill takes #618's documented fallback: **stop and ask.** It prints the
parked runs with direct links and waits for one of two answers — `continue`
(you approved/ran CI yourself; poll for green) or `skip` (waive CI and merge on
owner override). Anything else aborts without touching the PR.

For reference, the workflows that would run on the release PR once approved:

| Workflow | Trigger | Runs on release PR? |
|---|---|---|
| `docs-test.yml` | `pull_request: [opened, synchronize, reopened, labeled, unlabeled]` — no path filter; has a built-in release-PR exemption (auto-passes) | **Yes** — jobs `build`, `gate-tests` |
| `pr-title-lint.yml` | `pull_request: [opened, edited, synchronize, reopened]`; `chore` is an allowed type | **Yes** — job `lint` |
| `ci-tests.yml`, `geometry-lint.yml`, `pipeline-tests.yml` | path-filtered to `Assets/**` / `Packages/**` / `.claude/skills/pipeline-*/**` | **No** — the release PR only touches `VERSION`, `CHANGELOG.md`, `.github/release-please/manifest.json` |

The **required green checks** on the release PR are therefore exactly these
three jobs — spelled with the **raw check-run names GitHub reports** (the job's
`name:` if set, else its id), which is what `fetch_pr_checks` returns and what
`REQUIRED_CHECKS` must match (see #631; the old `"<workflow> / <job>"` spelling
never matched, so the poll hung to `TIMEOUT` while the checks were green):

| Workflow / job | Check-run name |
|---|---|
| `docs-test.yml` → `build` | `build` |
| `docs-test.yml` → `gate-tests` | `gate-tests` |
| `pr-title-lint.yml` → `lint` (`name: Conventional Commits PR title`) | `Conventional Commits PR title` |

(`REQUIRED_CHECKS` in `release_flow.py`. Other checks that also land on the
release PR — e.g. `Debug APK`, `Release-candidate APK`, `sweep` — are non-required
noise and are ignored by `classify_checks`.)

## The step sequence

Given the release PR number `N` and the list of feature PRs that must be in it:

1. **Regenerated?** Fetch the PR and live `main`; assert
   `is_regenerated(...)`. If the base is behind or a PR number is missing,
   **stop and wait** — release-please is still regenerating.
2. **Halt and ask** — `choose_ci_trigger_action()` → `ask_user`. The skill lists
   the runs `runs_awaiting_approval(...)` found parked at `action_required`,
   prints `format_ci_prompt(...)`, and waits. `parse_ci_answer(...)` maps the
   reply to `continue` / `skip` / `abort`; unrecognized input aborts, so a stray
   keypress can never merge. Nothing about the PR is modified at this step.
3. **Poll checks** (`continue` only) — `poll_checks(...)` reuses ci-watch's
   `PASSED` / `FAILED` / `TIMEOUT` shape over `REQUIRED_CHECKS` (skipped counts
   as pass). Only proceed on `PASSED`. On `skip`, polling is bypassed entirely
   and the merge happens on owner override.
4. **Squash-merge** — `build_merge_request(...)` builds the `PUT …/merge` call
   with `merge_method: "squash"` and commit title
   `chore(main): release X.Y.Z` (parsed from the PR title / `VERSION`). That
   exact title is what release-please detects to tag the release.
5. **Verify (bounded poll)** — tagging is async after the merge lands, so
   `poll_verification(...)` waits until `is_release_complete(...)`: the
   `vX.Y.Z` tag exists, the GitHub Release exists, and the PR label flipped
   `autorelease: pending` → `autorelease: tagged`.

## Running it

```bash
SKILL=.claude/skills/release-flow/release_flow.py

# Merge release PR #557; require #573 and #575 to be in the changelog first.
# Halts at the CI prompt and reads your answer from stdin.
python3 "$SKILL" 557 --expected-pr 573 --expected-pr 575

# Non-interactive: you already hit "Approve and run" in the UI, so go straight
# to polling for green.
python3 "$SKILL" 557 --expected-pr 573 --ci continue

# Non-interactive: waive CI and merge on owner override (what v0.11.0 did).
python3 "$SKILL" 557 --expected-pr 573 --ci skip

# Wait for green, but stop before merging (dry run of the gate).
python3 "$SKILL" 557 --expected-pr 573 --ci continue --no-merge

# A different repo
python3 "$SKILL" 557 --expected-pr 573 --repo owner/name
```

Because the skill can no longer start CI itself, the usual release is a
**two-step** interaction: run it, click "Approve and run" on the links it
prints, then answer `continue`.

## Shape (for maintenance)

Same split as the other deterministic skills. The pure logic — the regeneration
gate, check classification, the merge-request builder, the verification
predicate, and the poll loops (with an injectable `sleep_fn` clock) — is
unit-tested in `tests/test_release_flow.py`. GitHub I/O lives only in the
`_api_request` edge and the `run()` / `main()` orchestration seam, which are not
unit-tested. Run the tests from the skill folder:

```bash
python3 -m unittest discover -s tests
```
