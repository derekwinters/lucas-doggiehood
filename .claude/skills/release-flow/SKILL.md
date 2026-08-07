---
name: release-flow
description: >
  Drive the release-please merge flow for Doggiehood: confirm the release PR
  has regenerated, force real CI onto it (close → reopen), squash-merge with
  the `chore(main): release X.Y.Z` title, and verify the `vX.Y.Z` tag, the
  GitHub Release, and the `autorelease: tagged` label all appear. Use at
  release time, when merging release-please's auto-generated release PR — it
  captures the two gotchas that bite every release so they aren't re-derived.
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

### 2. The release PR gets ZERO CI checks

release-please pushes its branch with the built-in **`GITHUB_TOKEN`**, and
GitHub deliberately does **not** run `on: pull_request` / `on: push` workflows
for `GITHUB_TOKEN` pushes — this is intentional loop-prevention. So the release
PR sits at **0 checks**, combined status `pending`, `mergeable_state: blocked`,
and it **never goes green on its own**. (v0.11.0's PR #557 was merged with 0
checks via owner override; going forward we want real green checks first.)

## Why close → reopen, and not `workflow_dispatch`

To get real checks we re-fire a `pull_request` event **without** a
`GITHUB_TOKEN` push. Two options exist in principle:

- **Close → reopen** the PR — fires `pull_request: reopened`. No branch
  mutation, safe for release-please's branch.
- **`workflow_dispatch`** each required workflow against the
  `release-please--branches--main` ref — *only works if the workflow declares
  `workflow_dispatch`.*

**Decision: close → reopen, unconditionally.** An audit of every workflow's
trigger block shows that **none** of the workflows that actually run against the
release PR declare `workflow_dispatch`, so that option is not available today:

| Workflow | Trigger | Runs on release PR? |
|---|---|---|
| `docs-test.yml` | `pull_request: [opened, synchronize, reopened, labeled, unlabeled]` — no path filter; has a built-in release-PR exemption (auto-passes) | **Yes** — jobs `build`, `gate-tests` |
| `pr-title-lint.yml` | `pull_request: [opened, edited, synchronize, reopened]`; `chore` is an allowed type | **Yes** — job `lint` |
| `ci-tests.yml`, `pr-build.yml`, `geometry-lint.yml`, `pipeline-tests.yml` | path-filtered to `Assets/**` / `Packages/**` / `.claude/skills/pipeline-*/**` | **No** — the release PR only touches `VERSION`, `CHANGELOG.md`, `.github/release-please/manifest.json`, so they never fire regardless of trigger method |
| any of the above | `workflow_dispatch`? | **None declare it** |

So `choose_ci_trigger_action()` hard-codes `close_reopen` with no branch. If
`workflow_dispatch` is ever added to `docs-test`/`pr-title-lint`, supporting it
is a *follow-up*, not something to speculatively branch on now.

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
2. **Force CI** — `choose_ci_trigger_action()` → close → reopen the PR.
3. **Poll checks** — `poll_checks(...)` reuses ci-watch's `PASSED` / `FAILED` /
   `TIMEOUT` shape over `REQUIRED_CHECKS` (skipped counts as pass). Only proceed
   on `PASSED`.
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
python3 "$SKILL" 557 --expected-pr 573 --expected-pr 575

# Force CI + wait for green, but stop before merging (dry run of the gate).
python3 "$SKILL" 557 --expected-pr 573 --no-merge

# A different repo
python3 "$SKILL" 557 --expected-pr 573 --repo owner/name
```

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
