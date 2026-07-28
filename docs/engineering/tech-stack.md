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

**Runtime UGUI needs a bootstrapped `EventSystem`.** The runtime-built UI stack (`UiCanvas` + `GraphicRaycaster`, the `SettingsPanel`) only receives pointer input when an active `EventSystem` with an input module also exists — Unity auto-creates one only from the Editor's UI menu, never for runtime-built UI. `WorldBootstrap` ensures exactly one persistent `EventSystem` + `StandaloneInputModule` (`UiEventSystem.Ensure()`) beside the canvas; the module is the **legacy** `StandaloneInputModule` because the project pins `activeInputHandler: 0` (legacy Input Manager). Without it, UGUI controls are silently inert even though their handlers are wired ([#327](https://github.com/derekwinters/lucas-doggiehood/issues/327)).

### Geometry, layout, and tuning values are named variables

*[#161](https://github.com/derekwinters/lucas-doggiehood/issues/161) — applies to every feature, graybox included*

Every geometry, layout, and tuning value — sizes, offsets, margins, positions, durations, speeds, payouts — is declared as a named constant, static field, or serialized field at the top of its type (or in a shared Core numbers class where one exists, e.g. `EconomyNumbers`). Inline numeric literals in method bodies are not acceptable for these values, in either Core or the Unity wiring layer. Graybox code is not exempt: interim UI gets restyled, and named values are what make that a one-line change.

For **UI layout** values specifically, the named constants don't originate in the code — they originate in the screen's approved wireframe. A UI screen's size/margin/anchor constants are defined and approved in its [UI wireframe spec](../specs/ui/index.md) first (see the [UI Design Process](ui-design-process.md)); implementation code declares exactly those constants, and EditMode tests assert the built UI against them.

## Editor developer menus

Playtesting/authoring helpers live under the top-level **`Doggiehood`** editor
menu, in the Editor-only assembly (`Assets/Scripts/Unity/Editor/`) so they are
stripped from player builds:

- **`Doggiehood ▸ Build Catalog Gallery`** — procedurally rebuilds the house-catalog authoring gallery scene (`CatalogGalleryBuilder`, [#126](https://github.com/derekwinters/lucas-doggiehood/issues/126)).
- **`Doggiehood ▸ Reset Save Data`** — deletes the local save file (`doggiehood-save.txt` in `Application.persistentDataPath`) after a confirmation dialog, so the next launch starts a fresh game ([#187](https://github.com/derekwinters/lucas-doggiehood/issues/187)). The disk work is the testable `SaveStore.DeleteSave()` seam; the menu only adds the confirmation.

## On-device debug menu: toggles in the Debug tab

*[#219](https://github.com/derekwinters/lucas-doggiehood/issues/219) — the standard for all future debug affordances*

Editor menus above only help at an authoring machine. Debugging a **real build on a tablet** goes through the in-game **Settings ▸ Debug tab** instead (`SettingsPanel`, `docs/specs/ui/settings.md`), unlocked the Android developer-options way — tap the version label 10× within 10s. The unlock is engine-free Core logic (`Doggiehood.Core.Debugging.DebugUnlockGesture`) and **resets each session** (ships hidden, re-hidden every launch); the Debug menu is in the build but has no stray entry point.

**Standard:** a new debug affordance is a **toggle or action registered in the Debug tab**, never a temporary code edit (e.g. hand-setting a static seam and reverting it). Register it in `Doggiehood.Core.Debugging.DebugToggleRegistry` (name → bool, unknown names handled safely) and bind its effect in the thin Unity layer. The first toggle, **Show backyard fences**, drives the existing `WorldBuilder.ForceFencesVisible` seam and calls `WorldBuilder.RebuildFences` so the enclosures show/hide on a live build ([#152](https://github.com/derekwinters/lucas-doggiehood/issues/152)). The gesture and registry are unit-tested in the Core suite; the panel/toggle wiring is EditMode-tested against the wireframe constants.

## Repo hygiene

- **Git LFS** tracks common binary asset types (`.png`, `.psd`, `.fbx`, `.wav`, `.mp3`, `.ttf`, etc.) from the first commit. ([#79](https://github.com/derekwinters/lucas-doggiehood/issues/79))
- Standard Unity `.gitignore` (ignores `Library/`, `Temp/`, `Obj/`, `Build/`, `Logs/`, `UserSettings/`, etc.). ([#83](https://github.com/derekwinters/lucas-doggiehood/issues/83))
