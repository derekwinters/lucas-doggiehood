# CI/CD

*Issues: [#75](https://github.com/derekwinters/lucas-doggiehood/issues/75), [#76](https://github.com/derekwinters/lucas-doggiehood/issues/76), [#82](https://github.com/derekwinters/lucas-doggiehood/issues/82)*

## PR debug builds

Every PR builds a debug APK via CI:

- Uses Android's default debug signing (no real keystore yet — deliberate, see [#75](https://github.com/derekwinters/lucas-doggiehood/issues/75))
- Embeds the short commit SHA in the version name (e.g. `0.1.0-a1b2c3d`) so every build is uniquely identifiable
- Uploaded as a GitHub Actions artifact only — no Firebase/Play distribution for now

## Release-candidate builds

When a release-please release PR is open, CI builds a release-candidate APK versioned like `v1.0.0-rc1`. Each time the release PR branch is rebased (new commits land on `main` while it's open), the RC number increments (`rc2`, `rc3`, ...). Once that release ships, the next release-please PR starts over at `rc1`. ([#76](https://github.com/derekwinters/lucas-doggiehood/issues/76))

!!! warning "Needs verification before implementing"
    release-please has some built-in prerelease/versioning support, but its exact behavior for resetting and incrementing prerelease numbers across PR rebases needs to be checked against current release-please docs/source before relying on it — it may need a small custom Actions step layered on top (e.g. deriving the RC number from the number of pushes to the open release PR) rather than trusting the native prerelease handling to match this spec exactly.

Both PR debug builds and RC builds use debug signing and are distributed as GitHub Actions artifacts only, consistent with the rest of MVP scope.

## Commit linting

A required CI check lints PR titles against Conventional Commits and fails the PR if it doesn't conform, since release-please's version-bump computation depends on them. ([#82](https://github.com/derekwinters/lucas-doggiehood/issues/82))

## Docs site build & publish

Two workflows, both gated to only run on changes under `docs/`, `mkdocs.yml`, or the workflow files themselves — so unrelated app-code PRs don't burn CI minutes on the docs pipeline:

- **`docs-test.yml`**: on any PR touching docs, runs `mkdocs build --strict` to catch broken links/config before merge. No deploy.
- **`docs-publish.yml`**: on push to `main` (when docs changed) or on a release tag, builds the site and publishes it with `mike`, versioned to match the app's `VERSION` file, to GitHub Pages.

See the workflow files at `.github/workflows/docs-test.yml` and `.github/workflows/docs-publish.yml`.
