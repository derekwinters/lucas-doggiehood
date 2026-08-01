# Settings menu

*Wireframe issue: [#218](https://github.com/derekwinters/lucas-doggiehood/issues/218). Implements/covers: the in-game settings menu ([#219](https://github.com/derekwinters/lucas-doggiehood/issues/219)). Approved: Derek, 2026-07-25 (in-session).*
*Mockup: [mockups/settings.html](mockups/settings.html).*
*Status: **implemented** ([#219](https://github.com/derekwinters/lucas-doggiehood/issues/219)) — Unity `SettingsPanel` (`Assets/Scripts/Unity/SettingsPanel.cs`) built under the [#256](https://github.com/derekwinters/lucas-doggiehood/issues/256) `UiCanvas` CanvasScaler; unlock gesture + debug-toggle registry in `Doggiehood.Core.Debugging`; opened from the `HudOverlay` gear. **Candy Cottage restyle** ([#298](https://github.com/derekwinters/lucas-doggiehood/issues/298)): the graybox chrome is replaced with the shared [Candy Cottage](../world/art-style.md) direction — thick Ink outlines, flat hard drop-shadows, rounded panel + pill tabs/close/add-coins action + switch, from the shared palette — drawn procedurally by the UGUI `CandyChromeUgui` helper (device-safe per [#291](https://github.com/derekwinters/lucas-doggiehood/issues/291); layout unchanged).*

## Purpose

An in-game **Settings menu**, opened from a gear on the HUD: a left **sidebar of tabs** with a content pane. It gives two things — a real settings surface we grow over time (starting with an **About** tab showing the app version) and an **on-device Debug tab**, hidden by default and revealed by tapping the version label 10× within 10s (the Android developer-options gesture), so we can debug on a tablet without the Editor. Reference resolution is 1920×1200 per [Overview](index.md).

## Regions

| Region | Contains | Shared component |
|---|---|---|
| Title | "Settings" heading | [Shared panel chrome](shared-components.md) |
| Sidebar | Vertical tab rail — **About**, plus **Debug** once unlocked | [PillButton](shared-components.md#pill-button-pillbutton)-styled rows |
| Content pane | The active tab's body (About or Debug) | [Shared panel chrome](shared-components.md) |
| Close | Top-right ✕ dismiss affordance | [Shared panel chrome](shared-components.md) |
| Entry point | **Gear** button on the HUD that opens this panel | [Shared panel chrome](shared-components.md) |

**About pane:** app name · a **"Designed by Lucas"** tagline line · a tappable **version** label (also the debug-unlock target).
**Debug pane** (hidden until unlocked): a list of on-device toggles/actions — first is **Show backyard fences** (drives `WorldBuilder.ForceFencesVisible`, [#152](https://github.com/derekwinters/lucas-doggiehood/issues/152)), followed by **Add coins** (a gold **+100** action that grants `DebugAddCoinsAmount` = 100 coins to the wallet via the Core `Wallet.Deposit` seam, so neighborhood expansion can be tested without grinding quests, [#286](https://github.com/derekwinters/lucas-doggiehood/issues/286)), then **Refresh quests now** (a gold action pill — same `DebugActionWidthPx`/`DebugActionHeightPx` size as **Add coins** — that immediately triggers the new-quest randomization via the Core `QuestManager.ForceRefresh` seam, skipping the 8-hour refresh timer so quest content can be tested without waiting, [#457](https://github.com/derekwinters/lucas-doggiehood/issues/457)); room for more.

## Anchors & layout constants

| Constant | Value | Applies to |
|---|---|---|
| `SettingsPanelAnchor` | `Center` | Panel position — centered over a dim scrim so the neighborhood stays visible behind it |
| `SettingsPanelWidthPx` | `1400` | Panel width |
| `SettingsPanelHeightPx` | `820` | Panel height |
| `SettingsPanelPaddingPx` | `48` | Panel inset (padding) |
| `SidebarWidthPx` | `360` | Tab rail |
| `SidebarContentGapPx` | `40` | Rail → content pane |
| `TabHeightPx` | `96` | Each sidebar tab |
| `TabGapPx` | `16` | Between tabs |
| `TabRadiusPx` | `24` | Tab corners |
| `VersionFontSizePx` | `44` | Version label (the 10-tap unlock target) |
| `ToggleTrackWidthPx` | `104` | Debug toggle switch track |
| `ToggleTrackHeightPx` | `56` | Debug toggle switch track |
| `ToggleKnobPx` | `44` | Debug toggle switch knob |
| `DebugRowHeightPx` | `96` | Each debug toggle/action row |
| `DebugRowGapPx` | `20` | Gap between stacked debug rows |
| `DebugActionWidthPx` | `200` | Gold action pill (e.g. **+100**) width |
| `DebugActionHeightPx` | `72` | Debug action pill height |
| `DebugAddCoinsAmount` | `100` | Coins granted per **Add coins** tap ([#286](https://github.com/derekwinters/lucas-doggiehood/issues/286)) |
| `GearButtonSizePx` | `88` | HUD settings entry point |
| `GearMarginPx` | `32` | Gear inset from its HUD corner |
| `CloseButtonSizePx` | `72` | Close (✕) button, top-right |

The panel **chrome** and the **pill/tab** styling are owned by the shared components ([#173](https://github.com/derekwinters/lucas-doggiehood/issues/173), [shared-components.md](shared-components.md)) — not re-specified here; the shared baseline is a thick Ink outline (`OutlineThicknessPx` = 6), corner radius `PanelRadiusPx` = 40, and a single flat hard drop-shadow with no blur (`ShadowOffsetPx` = 8). This page places those components and sizes the panel, sidebar, tabs, version label, debug rows, and gear.

## Notes

- **Entry-point gear rendering ([#370](https://github.com/derekwinters/lucas-doggiehood/issues/370)).** The HUD **gear** (the entry point, drawn by `HudOverlay`, not the panel) wears the shared [Candy Cottage](../world/art-style.md) chrome, matching the neighbouring currency chip: an Ink outline disc with a hard straight-down drop-shadow over a cream fill, and a **procedural** Ink toothed-disc gear icon — no font glyph, no raster art. It previously drew a `⚙` (U+2699) font glyph on a default IMGUI button, which the bundled `DejaVuSans` cannot render, so on device it fell back to an unstyled gray box ([#291](https://github.com/derekwinters/lucas-doggiehood/issues/291)). Placement (`GearButtonSizePx` = 88, `GearMarginPx` = 32) and behaviour (tap opens Settings) are unchanged; see [hud.md](hud.md).
- **Debug unlock gesture.** Tapping the **version** label **10 times within 10 seconds** reveals the Debug tab in the sidebar; fewer taps, or 10 spread over more than 10s, does not. The gesture + the debug-toggle registry are engine-free **Core** logic (unit-tested); the panel/tabs/version display/toggle wiring is the Unity layer ([#219](https://github.com/derekwinters/lucas-doggiehood/issues/219)).
- **Debug ships hidden.** The Debug menu is in the build but unreachable without the gesture — no stray entry point.
- **Bundled UI font.** This is the game's first runtime-built UGUI (the HUD/onboarding are IMGUI), so its shader and font must be pulled into the build explicitly or the Android build strips them — the panel rendered as a magenta box with invisible text on device ([#291](https://github.com/derekwinters/lucas-doggiehood/issues/291)). Fixed by retaining `UI/Default` in **Always Included Shaders** and bundling **DejaVu Sans** (`Assets/UI/Fonts/Resources/DejaVuSans.ttf` — deliberately outside the `Assets/Art` low-poly-scanned tree, loaded via `Resources.Load`) instead of the Editor-only `Resources.GetBuiltinResource` lookup. DejaVu covers the ✕ close glyph but not the fullwidth plus, so the **Add coins** action uses a plain ASCII `+`. See `docs/engineering/unity-serialization.md`.
- **Runtime UGUI input needs an EventSystem.** UGUI pointer input requires **both** a `GraphicRaycaster` on the canvas (owned by `UiCanvas`) **and** an active `EventSystem` with an input module in the scene. Unity only auto-creates an EventSystem from the Editor's menu, never for runtime-built UI, so without one every control in this panel (close ✕, version-tap unlock, scrim tap-to-close, Debug toggles) is inert on device even though the handlers are wired — while the IMGUI HUD gear still opens Settings because it bypasses the EventSystem ([#327](https://github.com/derekwinters/lucas-doggiehood/issues/327)). `WorldBootstrap` therefore ensures exactly one persistent `EventSystem` + `StandaloneInputModule` beside the `UiCanvas` (the classic legacy-input module, matching `activeInputHandler: 0`), guarded against duplicates in `UiEventSystem.Ensure()`.
- **Standard this sets.** New debug affordances live as **toggles/actions in this Debug tab**, not as temporary code edits ([#219](https://github.com/derekwinters/lucas-doggiehood/issues/219)).
- **Future tabs** (audio, save/reset) are out of scope, but the sidebar leaves room for them.
- **Reference resolution.** Constants are authored at the 1920×1200 (16:10) reference per [Overview](index.md); a Unity `CanvasScaler` scales from this so each px constant has a fixed meaning across tablet sizes.
- Style itself (outlines, flat shadows, pill shapes, rounded type) lives in [Art & UI Style](../world/art-style.md); this page is layout only.

## Design decisions (resolved)

1. **Entry point** — how Settings opens → **a gear button in the HUD's top-right corner (furthest right)**, with the **currency chip moved just inboard, to the gear's left**. This nudges the currency-chip placement, so the HUD wireframe ([#174](https://github.com/derekwinters/lucas-doggiehood/issues/174)) is flagged to reconcile — the gear takes the corner.
2. **Sidebar vs. bottom tabs on portrait** → **sidebar only.** The game is landscape-locked ([#22](https://github.com/derekwinters/lucas-doggiehood/issues/22)/[#256](https://github.com/derekwinters/lucas-doggiehood/issues/256)), so there is no portrait case that would need a bottom-tab variant.
3. **Does the Debug unlock persist across launches?** → **resets each session** (ships hidden, re-hidden every launch; re-tap to reveal). Persisting across launches remains a one-flag change if we later prefer it.
