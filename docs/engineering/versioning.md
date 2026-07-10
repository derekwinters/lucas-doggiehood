# Versioning & Releases

*Issues: [#73](https://github.com/derekwinters/lucas-doggiehood/issues/73), [#74](https://github.com/derekwinters/lucas-doggiehood/issues/74), [#81](https://github.com/derekwinters/lucas-doggiehood/issues/81)*

## Version source of truth

The app version lives in a plain **`VERSION`** file at the repo root, which release-please updates directly. The build script reads this file and injects the value into Unity's PlayerSettings (bundle version) at build time. ([#73](https://github.com/derekwinters/lucas-doggiehood/issues/73))

This was chosen over having release-please patch Unity's `ProjectSettings/ProjectSettings.asset` directly, since that file is large and riskier for automated text patching.

## release-please configuration

Config lives at non-default paths:

- `.github/release-please/config.json`
- `.github/release-please/manifest.json`

Settings:

- `release-type`: `simple`, with an `extra-files` entry pointing at the root `VERSION` file
- `bump-minor-pre-major`: `false`
- `bump-patch-for-minor-pre-major`: `false` — standard semver: a `feat:` commit bumps minor even pre-1.0.0

Requires [Conventional Commits](https://www.conventionalcommits.org/) (`feat:`, `fix:`, etc.) to be followed consistently, since release-please's version computation depends on them. A CI check lints PR titles against this ([#82](https://github.com/derekwinters/lucas-doggiehood/issues/82)) so it doesn't rely on manual discipline alone.

## Android version code

Android's `versionCode` (a plain incrementing integer, separate from the human-readable `versionName`) is derived at build time as `git rev-list --count main` — deterministic and always increasing, independent of CI run-number state. ([#81](https://github.com/derekwinters/lucas-doggiehood/issues/81))

## Docs versioning

This documentation site is deployed with [mike](https://github.com/jimporter/mike), tagged to the same version as the app (from the `VERSION` file / release tags) so historical doc versions stay browsable alongside the current one. See [CI/CD](ci-cd.md) for the publish workflow.
