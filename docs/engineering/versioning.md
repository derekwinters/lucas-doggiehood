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

## Milestone versions vs. the shipped version

GitHub **milestones are version-*named*** (`v0.4`, `v1.0`, …) as planning targets — see [Conventions](../intro/conventions.md#milestones-are-version-numbered-scopes). Those labels are **decoupled** from the version release-please computes here: `VERSION` advances from Conventional-Commit history, so a milestone titled `v0.4` describes a *scope*, and the version that actually ships when its work lands need not be exactly `0.4.0`. Milestones plan; release-please versions. We deliberately don't add tooling to force the two to line up.

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

## One issue → one PR → one squash commit → one changelog entry

The [issue pipeline](issue-pipeline.md)'s nightly dev run builds each selected
issue on **its own branch** and opens **one PR per issue** — never a combined
PR batching several issues together. This keeps release-please's changelog
granular through the repository's squash-merge setting without any fragile
PR-body trickery:

- **PR title** = that issue's single Conventional-Commit line (e.g.
  `feat: give approach-to-rest real walk-to-decoration movement`). This is what
  `pr-title-lint` checks, and — because the PR resolves exactly one issue — it
  is also the natural subject of the squash commit that lands on `main`.
- **Squash commit** = one Conventional Commit. release-please parses that single
  header and emits exactly one clean changelog entry for the issue. No extra
  Conventional lines are smuggled through the PR body, so nothing can be
  silently dropped by the squash the way a combined multi-issue PR did
  ([#213](https://github.com/derekwinters/lucas-doggiehood/issues/213)).
- **PR body** carries the required `## Deviations and Decisions` section plus a
  `Closes #N` line so merging auto-closes the issue (see
  [Development Agent & Workflow](agent-workflow.md#how-an-issue-gets-worked)).
  The body is documentation for the reviewer, not a changelog-assembly
  mechanism.

Because each issue now lands as its own squash commit, `main` gains one commit
per merged issue rather than one per multi-issue batch. Android's `versionCode`
(derived below as `git rev-list --count main`) therefore rises one-per-issue —
a smaller, steadier increment that is still strictly monotonic and
deterministic.

## Android version code

Android's `versionCode` (a plain incrementing integer, separate from the human-readable `versionName`) is derived at build time as `git rev-list --count main` — deterministic and always increasing, independent of CI run-number state. ([#81](https://github.com/derekwinters/lucas-doggiehood/issues/81))

## Docs versioning

This documentation site is deployed with [mike](https://github.com/jimporter/mike), tagged to the same version as the app (from the `VERSION` file / release tags) so historical doc versions stay browsable alongside the current one. See [CI/CD](ci-cd.md) for the publish workflow.
