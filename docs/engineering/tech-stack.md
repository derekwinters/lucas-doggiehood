# Tech Stack

*Epic: [#70](https://github.com/derekwinters/lucas-doggiehood/issues/70)*

## Engine

**Unity 6 LTS**, using **Unity Personal** (free) — this is a non-commercial project well under the revenue/funding threshold that gates paid tiers. ([#71](https://github.com/derekwinters/lucas-doggiehood/issues/71))

CI builds use [`game-ci/unity-builder`](https://github.com/game-ci/unity-builder), activated with a Personal license file generated once and stored as a GitHub secret.

## Target platform

Android. Application ID: **`com.derekwinters.doggiehood`** ([#80](https://github.com/derekwinters/lucas-doggiehood/issues/80)) — permanent once published, debug builds apply an `applicationIdSuffix` (e.g. `.debug`) so they can install side-by-side with release builds on the same device.

Since CI builds APKs directly with `game-ci/unity-builder` (no exported Gradle project to add a `buildTypes { debug { ... } }` block to), the suffix is applied at Unity build time instead: an editor build hook (`Assets/Scripts/Unity/Editor/DebugApplicationIdBuildProcessor.cs`) appends `.debug` to `PlayerSettings.Android.applicationIdentifier` before the build and restores the permanent id afterward, whenever the `DOGGIEHOOD_DEBUG_BUILD` environment variable is set to a truthy value. `pr-build.yml` and `rc-build.yml` set it; `release-build.yml` does not, so release builds always ship the bare `com.derekwinters.doggiehood` id.

## Code architecture: Core / Unity split

*[#72](https://github.com/derekwinters/lucas-doggiehood/issues/72) — foundational, applies to every feature*

Game logic (quest system, economy, dog state, name-pool selection, house leveling, etc.) lives in **plain C# assemblies with no `UnityEngine` dependency**, so it runs under plain NUnit instantly, with no Unity runtime, editor, or device needed at all.

Unity Test Framework (EditMode tests, run headless via `-batchmode -nographics` in CI) only needs to cover the thin `MonoBehaviour`/scene-wiring layer that connects Core logic to the actual game. No PlayMode or on-device testing is required for CI to have thorough coverage.

**Default new logic to Core** unless it genuinely requires Unity APIs (rendering, input, physics, scene management). See [Testing Strategy](testing.md) for how this plays into TDD.

### Geometry, layout, and tuning values are named variables

*[#161](https://github.com/derekwinters/lucas-doggiehood/issues/161) — applies to every feature, graybox included*

Every geometry, layout, and tuning value — sizes, offsets, margins, positions, durations, speeds, payouts — is declared as a named constant, static field, or serialized field at the top of its type (or in a shared Core numbers class where one exists, e.g. `EconomyNumbers`). Inline numeric literals in method bodies are not acceptable for these values, in either Core or the Unity wiring layer. Graybox code is not exempt: interim UI gets restyled, and named values are what make that a one-line change.

## Repo hygiene

- **Git LFS** tracks common binary asset types (`.png`, `.psd`, `.fbx`, `.wav`, `.mp3`, `.ttf`, etc.) from the first commit. ([#79](https://github.com/derekwinters/lucas-doggiehood/issues/79))
- Standard Unity `.gitignore` (ignores `Library/`, `Temp/`, `Obj/`, `Build/`, `Logs/`, `UserSettings/`, etc.). ([#83](https://github.com/derekwinters/lucas-doggiehood/issues/83))
