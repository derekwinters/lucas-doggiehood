# Tech Stack

*Epic: [#70](https://github.com/derekwinters/lucas-doggiehood/issues/70)*

## Engine

**Unity 6 LTS**, using **Unity Personal** (free) — this is a non-commercial project well under the revenue/funding threshold that gates paid tiers. ([#71](https://github.com/derekwinters/lucas-doggiehood/issues/71))

CI builds use [`game-ci/unity-builder`](https://github.com/game-ci/unity-builder), activated with a Personal license file generated once and stored as a GitHub secret.

## Target platform

Android. Application ID: **`com.derekwinters.doggiehood`** ([#80](https://github.com/derekwinters/lucas-doggiehood/issues/80)) — permanent once published, debug builds apply an `applicationIdSuffix` (e.g. `.debug`) so they can install side-by-side with release builds on the same device.

Since CI builds APKs directly with `game-ci/unity-builder` (no exported Gradle project to add a `buildTypes { debug { ... } }` block to), the suffix is applied at Unity build time instead: an editor build hook (`Assets/Scripts/Unity/Editor/DebugApplicationIdBuildProcessor.cs`) appends `.debug` to `PlayerSettings.Android.applicationIdentifier` before the build and restores the permanent id afterward, whenever the `DOGGIEHOOD_DEBUG_BUILD` environment variable is set to a truthy value. `pr-build.yml` and `rc-build.yml` set it; `release-build.yml` does not, so release builds always ship the bare `com.derekwinters.doggiehood` id.

### ABIs and build variants

The **device build is ARM64-only** — `ProjectSettings.asset` commits `AndroidTargetArchitectures: 2` and nothing in the normal build path changes it.

The one exception is the **emulator build variant** ([#648](https://github.com/derekwinters/lucas-doggiehood/issues/648)), attached to every release as `doggiehood-vX.Y.Z-emulator.apk` so the game can be run in an x86_64 Android emulator without a hand-produced build. It reuses the `.debug`-suffix mechanism above: a second editor build hook (`Assets/Scripts/Unity/Editor/EmulatorBuildProcessor.cs`) applies the whole profile at build time and restores every field afterward. Two channels can ask for it — the `DOGGIEHOOD_EMULATOR_BUILD` environment variable for local and editor builds, and the `-doggiehoodEmulatorBuild` switch on Unity's command line for CI, which is the only one that reaches the editor inside game-ci's build container ([#731](https://github.com/derekwinters/lucas-doggiehood/issues/731); see [ci-cd.md](ci-cd.md#release-builds)). Nothing about it is committed to `ProjectSettings.asset`, so a run that asks for neither can't inherit any of it:

| Setting | Emulator variant | Why |
|---|---|---|
| Android ABI | `X86_64` only | The variant exists to run on x86_64 emulators; carrying ARM64 too would only bloat it. |
| applicationId | `com.derekwinters.doggiehood.emulator` | Installs side-by-side with the device build, like `.debug`. |
| Graphics APIs | Vulkan → GLES3 (Auto Graphics API off) | Unity's runtime backend auto-pick was hanging the render worker on the reported emulator host. Vulkan stays first so emulators that support it still exercise that path, with GLES3 as the fallback. |
| Wide colour gamut | Off (sRGB only) | Emulator-safe rendering; the project's committed default is already sRGB. |
| Multithreaded rendering | Off | The render worker is what hangs. |
| Unity audio engine | Off ("Disable Unity Audio") | A per-thread profile taken during a Waydroid hang ([#707](https://github.com/derekwinters/lucas-doggiehood/issues/707)) showed `FMOD stream thr` and `AudioTrack` pinned at ~100% CPU with `UnityMain` blocked behind them, while the render thread slept. The project ships no audio assets, so the variant gives the subsystem up rather than let it spin against an emulated audio HAL. The profiled binary was **not** an emulator build — see the warning below. |
| Signing | Debug, same as every other build | No separate decision — nothing in the current scope uses a real keystore ([#75](https://github.com/derekwinters/lucas-doggiehood/issues/75)). |

One emulator APK is produced, not a matrix of graphics variants. The `.emulator` suffix composes with `.debug` if a run ever asks for both.

!!! warning "Every row above was motivated by a trace from the *device* APK, not an emulator build"
    Two limits apply to this table. The first was known when the rows were written; the second was discovered afterwards and is the more serious of the two.

    **No agent and no CI job can reproduce the reporting hosts** ([testing.md](testing.md): no on-device or emulator testing). CI-hosted Android emulators render through SwiftShader rather than a real GPU driver, and Waydroid is a container sharing the host's vCPUs and audio/graphics HALs. So no row here has been confirmed to actually clear the hang on the machine that reported it.

    **And the evidence behind them came from the wrong binary.** [#706](https://github.com/derekwinters/lucas-doggiehood/issues/706) established that every release up to and including `v0.14.0` published an `-emulator.apk` that was a byte-for-byte copy of the ARM64 device APK. Every ANR trace and thread profile gathered so far — including the `FMOD stream thr` / `AudioTrack` profile that motivated the audio row ([#707](https://github.com/derekwinters/lucas-doggiehood/issues/707)) — was therefore captured against a device build carrying *none* of these mitigations, running under an ARM-translation layer. The reporter's own control comparison in [#705](https://github.com/derekwinters/lucas-doggiehood/issues/705) ("a native x86_64 build behaves identically to the ARM64 one, which rules out `libhoudini`/`libndk_translation`") collapses with it, because both installs were the same file. Translation overhead is back on the table as a candidate cause of the CPU spin, and the audio reading is *confounded*, not confirmed.

    Every row is therefore an evidence-led best-effort setting, not a diagnosis, and each stays open until a tester confirms it against a genuinely distinct `-emulator.apk` — **and no such build has shipped yet**. The #706 fix made the two builds genuinely distinct but both were still device builds, because the flag requesting the profile never reached Unity ([#731](https://github.com/derekwinters/lucas-doggiehood/issues/731)); the gate refused to upload the pair, so `v0.15.0` published no assets at all. The first confirmable emulator APK is the one built after the #731 fix. What *is* guaranteed here is the blast radius: the profile is applied at build time and restored afterward, so a wrong guess can only affect `doggiehood-vX.Y.Z-emulator.apk`, never the device build.

    **Invariant — the emulator profile never changes what the device build ships.** Every setting it touches is captured before the build and restored after it, and none of them is committed to `ProjectSettings.asset`. `EmulatorBuildProcessorTests` asserts both halves (untouched when neither channel asks for the variant, restored after an emulator build) for every field in the table, so a new mitigation can't leak into the release APK.

    **Invariant — only a trace from a verified emulator build can confirm an emulator mitigation.** A trace from any other binary may motivate a best-effort row here, but it never confirms one and never closes the issue that proposed it. "Verified" has a mechanical meaning: `.github/scripts/verify_emulator_build_variant.py` fails the release before upload unless the two APKs are genuinely distinct builds with the right applicationId and ABIs (see [ci-cd.md](ci-cd.md)).

> **How the spec is changing (#731).** This page used to say the emulator hook is "toggled by the `DOGGIEHOOD_EMULATOR_BUILD` environment variable", and that a genuinely distinct emulator APK would first exist in the release built after the #706 fix. It now says the hook takes two channels — that environment variable locally, and a `-doggiehoodEmulatorBuild` command-line switch in CI — and that no confirmable emulator build has shipped yet. The reason is that #731 found the environment variable never crossed into game-ci's build container, so `v0.15.0`'s pair were both device builds and the release shipped with no assets; the mitigations in the table above remain untested against a real emulator binary.

> **How the spec is changing (#707).** This page used to justify the emulator audio row with "a per-thread profile of a hung *emulator* APK", and warned only that the mitigations were *unconfirmed* on the reporting host. It now says that profile was taken against the ARM64 **device** APK — [#706](https://github.com/derekwinters/lucas-doggiehood/issues/706) found that no genuine emulator build had ever shipped — so the audio finding is confounded rather than merely unconfirmed, and the row stands as a best-effort setting rather than a diagnosis. The change is here because #706's finding landed after the audio row was written, and a reader shouldn't have to reconcile the two pages themselves.

!!! note "Neither colour gamut nor Disable Unity Audio has a public `PlayerSettings` API"
    In Unity 6, `PlayerSettings.GetColorGamuts`/`SetColorGamuts` are `internal`, and there is no Android-specific wide-colour-gamut key at all (it's the project-wide `m_ColorGamuts` list). "Disable Unity Audio" has no public API either, and it lives on a *different* settings singleton — `AudioManager`, not `PlayerSettings`. `EmulatorBuildProcessor` therefore reads and writes both fields through serialized data, via the public `Unsupported.GetSerializedAssetInterfaceSingleton(...)`; every other field in the table uses a normal public API. Per [unity-serialization.md](unity-serialization.md), `m_ColorGamuts` and `m_DisableAudio` are verified against real `ProjectSettings.asset` / `ProjectSettings/AudioManager.asset` files (and, for `m_DisableAudio`, against Unity's own `AudioManagerInspector`, which reads it as a `boolValue`) rather than guessed. Both accessors degrade to a warning rather than a build failure if the property can't be resolved — which is precisely why `EmulatorBuildProcessorTests` asserts a **round trip** through each one: a silently-ignored setting would otherwise look fixed while shipping the old behaviour.

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
