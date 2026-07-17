# Versioning & Releases

*Issues: [#73](https://github.com/derekwinters/lucas-doggiehood/issues/73), [#74](https://github.com/derekwinters/lucas-doggiehood/issues/74), [#81](https://github.com/derekwinters/lucas-doggiehood/issues/81), [#114](https://github.com/derekwinters/lucas-doggiehood/issues/114)*

## Version source of truth

The app version lives in a plain **`VERSION`** file at the repo root, which release-please updates directly. The build script reads this file and injects the value into Unity's PlayerSettings (bundle version) at build time. ([#73](https://github.com/derekwinters/lucas-doggiehood/issues/73))

This was chosen over having release-please patch Unity's `ProjectSettings/ProjectSettings.asset` directly, since that file is large and riskier for automated text patching.

`VERSION`'s content is the bare semver followed by a marker comment:

```
0.2.0 # x-release-please-version
```

The marker is required — release-please's generic `extra-files` updater (used for any file without a recognized JSON/YAML/TOML/XML extension) only rewrites a line that contains `x-release-please-version`; it does not blindly overwrite a file's entire contents. Without the marker, the updater has nothing to match and silently leaves the file untouched. This is exactly what happened in [#114](https://github.com/derekwinters/lucas-doggiehood/issues/114): `VERSION` stayed on `0.1.0` across a real `0.2.0` release because it was a bare version string with no marker, even though release-please's own `manifest.json` bookkeeping had correctly advanced. Anything that reads `VERSION` (build workflows, docs publishing) strips everything from `#` onward before parsing, so the trailing comment never leaks into a `versionName`/`versionCode`.

## release-please configuration

Config lives at non-default paths:

- `.github/release-please/config.json`
- `.github/release-please/manifest.json`

Settings:

- `release-type`: `simple`, with an `extra-files` entry declaring `VERSION` explicitly as `{"type": "generic", "path": "VERSION"}` (release-please would infer the generic updater for an extensionless file either way, but spelling it out documents which updater applies and that `VERSION` therefore needs the `x-release-please-version` marker described above)
- `bump-minor-pre-major`: `false`
- `bump-patch-for-minor-pre-major`: `false` — standard semver: a `feat:` commit bumps minor even pre-1.0.0

Requires [Conventional Commits](https://www.conventionalcommits.org/) (`feat:`, `fix:`, etc.) to be followed consistently, since release-please's version computation depends on them. A CI check lints PR titles against this ([#82](https://github.com/derekwinters/lucas-doggiehood/issues/82)) so it doesn't rely on manual discipline alone.

A Core guard test (`CoreTests/Doggiehood.Core.Tests/Versioning/VersionFileGuardTests.cs`) asserts `VERSION` contains a valid bare semver and matches `manifest.json`'s tracked version, so the two can't silently drift apart again the way they did in #114.

## Combined nightly PRs squash with raw conventional lines in the body

The [issue pipeline](issue-pipeline.md)'s nightly dev run builds several issues
onto one branch and opens a **single combined PR**. To keep release-please's
changelog granular despite the single squash-merge, the PR is structured so its
squash commit carries one Conventional-Commit line per issue:

- **PR title** = the lead change's Conventional line (e.g.
  `feat: give approach-to-rest real walk-to-decoration movement`). This is what
  `pr-title-lint` checks and what names the release.
- **PR body** = the remaining issues' Conventional-Commit lines as **raw**
  lines (no leading `*`/`-`), one per issue, so that on squash-merge they land
  in the squash commit message. release-please parses each as its own entry and
  emits one changelog line per issue.

This depends on the repository's squash-merge commit message being set to "Pull
request title and description" so the body lines survive the squash (a
Direct-Involvement setup item). Without it, only the title's entry would reach
the changelog.

## Android version code

Android's `versionCode` (a plain incrementing integer, separate from the human-readable `versionName`) is derived at build time as `git rev-list --count main` — deterministic and always increasing, independent of CI run-number state. ([#81](https://github.com/derekwinters/lucas-doggiehood/issues/81))

## Docs versioning

This documentation site is deployed with [mike](https://github.com/jimporter/mike), tagged to the same version as the app (from the `VERSION` file / release tags) so historical doc versions stay browsable alongside the current one. See [CI/CD](ci-cd.md) for the publish workflow.
