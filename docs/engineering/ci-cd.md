# CI/CD

*Issues: [#75](https://github.com/derekwinters/lucas-doggiehood/issues/75), [#76](https://github.com/derekwinters/lucas-doggiehood/issues/76), [#80](https://github.com/derekwinters/lucas-doggiehood/issues/80), [#82](https://github.com/derekwinters/lucas-doggiehood/issues/82)*

## PR debug builds

Every PR builds a debug APK via CI:

- Uses Android's default debug signing (no real keystore yet — deliberate, see [#75](https://github.com/derekwinters/lucas-doggiehood/issues/75))
- Embeds the short commit SHA in the version name (e.g. `0.1.0-a1b2c3d`) so every build is uniquely identifiable
- Applies the `.debug` applicationId suffix ([#80](https://github.com/derekwinters/lucas-doggiehood/issues/80)) via the `DOGGIEHOOD_DEBUG_BUILD` env var, so it can install side-by-side with a release build on the same device
- Uploaded as a GitHub Actions artifact only — no Firebase/Play distribution for now

## Release-candidate builds

When a release-please release PR is open, CI builds a release-candidate APK versioned like `v1.0.0-rc1`. Each time the release PR branch is rebased (new commits land on `main` while it's open), the RC number increments (`rc2`, `rc3`, ...). Once that release ships, the next release-please PR starts over at `rc1`. ([#76](https://github.com/derekwinters/lucas-doggiehood/issues/76))

!!! note "RC numbering design (resolved)"
    release-please's native prerelease support bumps prerelease numbers when *releases* happen, not when the open release PR is rebased, so it can't produce `rc1` → `rc2` across pushes to the same open PR. Instead, `rc-build.yml` derives the RC number itself: it counts that workflow's runs on the release PR's branch since the PR was opened (current run included). Every push to the open release PR adds a run, incrementing the RC; a fresh release PR after a release ships has a later created-at watermark, so the count — and the RC number — starts over at `rc1`. The release PR's `VERSION` file already carries the next version (it's a release-please extra-file), so builds are versioned `v<VERSION>-rc<N>`.

Both PR debug builds and RC builds use debug signing, apply the same `.debug` applicationId suffix ([#80](https://github.com/derekwinters/lucas-doggiehood/issues/80)), and are distributed as GitHub Actions artifacts only, consistent with the rest of the current release scope.

## Release builds

When a release ships (release-please publishes the `vX.Y.Z` GitHub release), the APK for that tag is built and **attached to the release page** as `doggiehood-vX.Y.Z.apk` — so each release carries its installable build directly, not just as a transient Actions artifact. Debug signing, same as everything else in the current release scope.

The build-and-attach step lives **inside `release-please.yml`**, as a `build-and-attach` job gated on the release-please job's `release_created` output (`if: needs.release-please.outputs.release_created == 'true'`) and checking out the new `tag_name` output. It runs in the *same* workflow run that publishes the release.

!!! note "Why it's not a separate `release: published` workflow (resolved — [#357](https://github.com/derekwinters/lucas-doggiehood/issues/357))"
    release-please publishes the GitHub release with the default `GITHUB_TOKEN`, and GitHub deliberately does **not** fire workflows from events initiated by `GITHUB_TOKEN` (to prevent recursive runs). A workflow triggered `on: release: [published]` therefore silently never ran for automated releases — only the manual backfill did. Wiring the build into the release-please run itself, gated on `release_created`, sidesteps the token restriction entirely.

`release-build.yml` is retained as the **`workflow_dispatch` backfill**: a manual run that takes an existing release `tag` input and builds + attaches its APK — for a release whose publish predates this mechanism, or to re-attach a build on demand. Its old `release: published` trigger has been removed.

Both paths preserve the **graceful-skip contract**: a "Check for Unity license secret" step gates every build step with `if: steps.license.outputs.present == 'true'`, so a missing `UNITY_LICENSE` secret emits a warning and skips rather than failing the run.

## Commit linting

A required CI check lints PR titles against Conventional Commits and fails the PR if it doesn't conform, since release-please's version-bump computation depends on them. ([#82](https://github.com/derekwinters/lucas-doggiehood/issues/82))

## Docs site build & publish

- **`docs-test.yml`**: runs its `build` job on **every** PR (no path filter) so it can report a status on code-only PRs too, backing the [docs-reconciliation rule](agent-workflow.md):
    - PR touches docs → runs `mkdocs build --strict` to catch broken links/config before merge. No deploy.
    - PR touches no docs → the reconciliation gate fails the PR unless it carries the `skip-docs` label (the deliberate escape hatch for a genuinely doc-irrelevant change).
    - **release-please's auto-generated release PR is exempt.** It only bumps `VERSION`/`CHANGELOG.md`/`manifest.json` and can neither reconcile docs nor self-apply a label, so without an exemption the gate would block every release. It's identified by its deterministic `release-please--branches--*` head branch or its `autorelease: pending` label — either match bypasses the gate.
    - **No transient red on `skip-docs` PRs ([#254](https://github.com/derekwinters/lucas-doggiehood/issues/254)).** `pipeline-dev` can't set `skip-docs` atomically at PR creation — it opens the PR, then labels it a moment later — and the workflow triggers on both `opened` and `labeled`. So the `opened` run would fire before the label lands and fail the gate, then the `labeled` run would re-run and pass; the PR ended green but the superseded failed run still fired a spurious CI-failure webhook on essentially every code-only/tooling PR. To stop that, the gate's decision is a unit-tested script, `.github/scripts/docs_reconciliation_gate.py`: when the triggering payload carries no `skip-docs`, it polls the PR's **live** labels for a short grace window (~45s) before failing, so the same `opened` run observes the just-applied label and passes — **no failing run is ever produced.** Semantics are unchanged: a docs change, the release exemption, or a `skip-docs` present up front still pass, and a genuine code-only PR that never gets docs or the label still fails once the grace window elapses. The live-label fetch is why the workflow grants `pull-requests: read`; the decision matrix (docs-change → pass, release → pass, code-only+`skip-docs` → pass, code-only without either → fail, and the label-landing-during-grace case) is covered by the `gate-tests` job on every PR.
- **`docs-publish.yml`**: path-gated to changes under `docs/`, `mkdocs.yml`, or the workflow file itself. On push to `main` (when docs changed) or on a release tag, builds the site and publishes it with `mike`, versioned to match the app's `VERSION` file, to GitHub Pages.

See the workflow files at `.github/workflows/docs-test.yml` and `.github/workflows/docs-publish.yml`.

## Issue-pipeline workflows

The [AI issue-management pipeline](issue-pipeline.md) adds two workflows, both scoped by path so they don't run on unrelated PRs:

- **`pipeline-tests.yml`**: on any PR touching `.claude/skills/pipeline-*`, runs the pure-Python unit tests for the deterministic pipeline scripts (the gatekeeper command parser, the dev queue selector, and the dashboard renderer's golden snapshot). Stdlib only — no Unity, no pip dependencies.
- **`dashboard.yml`**: a scheduled (and `workflow_dispatch`) job that regenerates the live dashboard issue ([#193](https://github.com/derekwinters/lucas-doggiehood/issues/193)) deterministically from repo state, authenticating its headless issue-body PATCH with the built-in `GITHUB_TOKEN`. It runs a few times a day, shortly after each AI routine, and also `on: issues: [labeled, unlabeled]` so the board refreshes the instant a label changes ([#442](https://github.com/derekwinters/lucas-doggiehood/issues/442)); a constant-group `concurrency` (`cancel-in-progress: false`) serializes the resulting bursts. That trigger fires for human/UI/PAT label moves only — GitHub's no-recursion guard suppresses runs from the automation's own `GITHUB_TOKEN`-authored moves, which the gatekeeper covers by re-rendering #193 inline instead (see `docs/engineering/issue-pipeline.md`).
