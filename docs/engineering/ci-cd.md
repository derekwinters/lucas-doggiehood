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

Both PR debug builds and RC builds use debug signing, apply the same `.debug` applicationId suffix ([#80](https://github.com/derekwinters/lucas-doggiehood/issues/80)), and are distributed as GitHub Actions artifacts only, consistent with the rest of MVP scope.

## Release builds

When a release ships (release-please publishes the `vX.Y.Z` GitHub release), `release-build.yml` builds the APK for that tag and **attaches it to the release page** as `doggiehood-vX.Y.Z.apk` — so each release carries its installable build directly, not just as a transient Actions artifact. Debug signing, same as everything else in MVP scope.

## Commit linting

A required CI check lints PR titles against Conventional Commits and fails the PR if it doesn't conform, since release-please's version-bump computation depends on them. ([#82](https://github.com/derekwinters/lucas-doggiehood/issues/82))

## Docs site build & publish

- **`docs-test.yml`**: runs its `build` job on **every** PR (no path filter) so it can report a status on code-only PRs too, backing the [docs-reconciliation rule](agent-workflow.md):
    - PR touches docs → runs `mkdocs build --strict` to catch broken links/config before merge. No deploy.
    - PR touches no docs → the reconciliation gate fails the PR unless it carries the `skip-docs` label (the deliberate escape hatch for a genuinely doc-irrelevant change).
    - **release-please's auto-generated release PR is exempt.** It only bumps `VERSION`/`CHANGELOG.md`/`manifest.json` and can neither reconcile docs nor self-apply a label, so without an exemption the gate would block every release. It's identified by its deterministic `release-please--branches--*` head branch or its `autorelease: pending` label — either match bypasses the gate.
- **`docs-publish.yml`**: path-gated to changes under `docs/`, `mkdocs.yml`, or the workflow file itself. On push to `main` (when docs changed) or on a release tag, builds the site and publishes it with `mike`, versioned to match the app's `VERSION` file, to GitHub Pages.

See the workflow files at `.github/workflows/docs-test.yml` and `.github/workflows/docs-publish.yml`.

## Issue-pipeline workflows

The [AI issue-management pipeline](issue-pipeline.md) adds two workflows, both scoped by path so they don't run on unrelated PRs:

- **`pipeline-tests.yml`**: on any PR touching `.claude/skills/pipeline-*`, runs the pure-Python unit tests for the deterministic pipeline scripts (the gatekeeper command parser, the dev queue selector, and the dashboard renderer's golden snapshot). Stdlib only — no Unity, no pip dependencies.
- **`dashboard.yml`**: a scheduled (and `workflow_dispatch`) job that regenerates the live dashboard issue ([#193](https://github.com/derekwinters/lucas-doggiehood/issues/193)) deterministically from repo state, authenticating its headless issue-body PATCH with the built-in `GITHUB_TOKEN`. It runs a few times a day, shortly after each AI routine.
