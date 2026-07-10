# Tech Stack

*Epic: [#70](https://github.com/derekwinters/lucas-doggiehood/issues/70)*

## Engine

**Unity 6 LTS**, using **Unity Personal** (free) — this is a non-commercial project well under the revenue/funding threshold that gates paid tiers. ([#71](https://github.com/derekwinters/lucas-doggiehood/issues/71))

CI builds use [`game-ci/unity-builder`](https://github.com/game-ci/unity-builder), activated with a Personal license file generated once and stored as a GitHub secret.

## Target platform

Android. Application ID: **`com.derekwinters.doggiehood`** ([#80](https://github.com/derekwinters/lucas-doggiehood/issues/80)) — permanent once published, debug builds apply an `applicationIdSuffix` (e.g. `.debug`) so they can install side-by-side with release builds on the same device.

## Code architecture: Core / Unity split

*[#72](https://github.com/derekwinters/lucas-doggiehood/issues/72) — foundational, applies to every feature*

Game logic (quest system, economy, dog state, name-pool selection, house leveling, etc.) lives in **plain C# assemblies with no `UnityEngine` dependency**, so it runs under plain NUnit instantly, with no Unity runtime, editor, or device needed at all.

Unity Test Framework (EditMode tests, run headless via `-batchmode -nographics` in CI) only needs to cover the thin `MonoBehaviour`/scene-wiring layer that connects Core logic to the actual game. No PlayMode or on-device testing is required for CI to have thorough coverage.

**Default new logic to Core** unless it genuinely requires Unity APIs (rendering, input, physics, scene management). See [Testing Strategy](testing.md) for how this plays into TDD.

## Repo hygiene

- **Git LFS** tracks common binary asset types (`.png`, `.psd`, `.fbx`, `.wav`, `.mp3`, `.ttf`, etc.) from the first commit. ([#79](https://github.com/derekwinters/lucas-doggiehood/issues/79))
- Standard Unity `.gitignore` (ignores `Library/`, `Temp/`, `Obj/`, `Build/`, `Logs/`, `UserSettings/`, etc.). ([#83](https://github.com/derekwinters/lucas-doggiehood/issues/83))
