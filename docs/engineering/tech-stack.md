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

#### Central balance tuning (`TuningConfig`)

*[#620](https://github.com/derekwinters/lucas-doggiehood/issues/620)*

Core **balance** values — quest payout, pacing window/cap, tile-unlock and house-build/upgrade pricing, cost-tier bands and gates, move-in chances/weights, onboarding reward — are not only named, they are read at runtime from a single, overridable Core config: `Doggiehood.Core.Tuning.TuningConfig`. Each value is a named field on `TuningConfig`, seeded to the shipping default; a fresh `TuningConfig` reproduces today's exact behavior. The domain "numbers" classes (`EconomyNumbers`, `TileUnlockNumbers`, `HouseBuildNumbers`, `MoveInNumbers`, `HouseUpgradeNumbers`, `QuestCostTiers`, `OnboardingRewardChainNumbers`) stay the documented, discoverable homes for each value but now read from `TuningConfig.Active`, so every seam and gameplay path that goes through them is live-tunable from one place.

`TuningConfig.Active` is the config all Core balance reads resolve against; `TuningConfig.ResetToDefaults()` restores it to a fresh, shipping-defaults instance. This is the seam the debug tuning menu (#622) mutates live, and where the pacing rebalances (#623–#626) set their new defaults. `TuningConfig` is plain C# with no `UnityEngine` dependency, so it stays in the engine-free Core assembly and NUnit-testable. The named-constant rule above is unchanged — `TuningConfig` is *where* the Core balance constants now live, not an exception to naming them.

#### Automated backstop

The rule above is the standard, and it's absolute — but a lightweight CI check (`.github/scripts/check_geometry_literals.py`, wired via `geometry-lint.yml`) catches the egregious case that motivated it (#159 shipped `140f`/`16f`/`32f` inside `OnGUI`). It's deliberately conservative and low-false-positive: it flags an f-suffixed float literal only when it sits in a method body (not a type-level `const`/field declaration) and its magnitude is at least `3` — so structural values (`0`/`1`/`2` for identity, both-sides, centering) and sub-unit fractions (anchors, colour channels, epsilons) are ignored. It is a backstop for the obvious pixel-size/offset/rotation/speed literal, **not** a replacement for the human standard, which still covers every geometry/tuning value regardless of magnitude.

Because the game tree predates the rule, the check **ratchets against a committed baseline** (`.github/scripts/geometry_literals_baseline.txt`): the pre-existing literals are recorded there and don't fail CI, while any newly introduced one does. The baseline is the burn-down list — as literals get named, regenerate it with `python3 .github/scripts/check_geometry_literals.py --update-baseline`. Its unit tests run in the same job. Adding a genuinely new named-constant-worthy literal, or one below the heuristic's threshold, is still governed by the absolute rule and by review — the check only automates the floor.

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
