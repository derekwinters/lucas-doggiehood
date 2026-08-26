# Settings menu

*Wireframe issue: [#218](https://github.com/derekwinters/lucas-doggiehood/issues/218). Implements/covers: the in-game settings menu ([#219](https://github.com/derekwinters/lucas-doggiehood/issues/219)). Approved: Derek, 2026-07-25 (in-session).*
*Mockup: [mockups/settings.html](mockups/settings.html).*
*Status: **implemented** ([#219](https://github.com/derekwinters/lucas-doggiehood/issues/219)) — Unity `SettingsPanel` (`Assets/Scripts/Unity/SettingsPanel.cs`) built under the [#256](https://github.com/derekwinters/lucas-doggiehood/issues/256) `UiCanvas` CanvasScaler; unlock gesture + debug-toggle registry in `Doggiehood.Core.Debugging`; opened from the `HudOverlay` gear. **Candy Cottage restyle** ([#298](https://github.com/derekwinters/lucas-doggiehood/issues/298)): the graybox chrome is replaced with the shared [Candy Cottage](../world/art-style.md) direction — thick Ink outlines, flat hard drop-shadows, rounded panel + pill tabs/close/add-coins action + switch, from the shared palette — drawn procedurally by the UGUI `CandyChromeUgui` helper (device-safe per [#291](https://github.com/derekwinters/lucas-doggiehood/issues/291); layout unchanged). **Debug sub-tabs** ([#716](https://github.com/derekwinters/lucas-doggiehood/issues/716)): the Debug pane's flat row list is replaced by a sub-tab bar over three named groups, with each group's row capacity computed by the engine-free `Doggiehood.Core.Ui.SettingsDebugPaneMetrics` and the grouping declared in `Doggiehood.Core.Ui.DebugSubTabRoster`.*

## Purpose

An in-game **Settings menu**, opened from a gear on the HUD: a left **sidebar of tabs** with a content pane. It gives two things — a real settings surface we grow over time (starting with an **About** tab showing the app version) and an **on-device Debug tab**, hidden by default and revealed by tapping the version label 10× within 10s (the Android developer-options gesture), so we can debug on a tablet without the Editor. Reference resolution is 1920×1200 per [Overview](index.md).

## Regions

| Region | Contains | Shared component |
|---|---|---|
| Title | "Settings" heading | [Shared panel chrome](shared-components.md) |
| Sidebar | Vertical tab rail — **About**, plus **Debug** once unlocked | [PillButton](shared-components.md#pill-button-pillbutton)-styled rows |
| Content pane | The active tab's body (About or Debug) | [Shared panel chrome](shared-components.md) |
| Debug sub-tab bar | Horizontal strip at the top of the **Debug** pane — **General** · **Visuals & Tools** · **Reports** ([#716](https://github.com/derekwinters/lucas-doggiehood/issues/716)) | [PillButton](shared-components.md#pill-button-pillbutton)-styled pills |
| Close | Top-right ✕ dismiss affordance | [Shared panel chrome](shared-components.md) |
| Entry point | **Gear** button on the HUD that opens this panel | [Shared panel chrome](shared-components.md) |

**About pane:** app name · a **"Designed by Lucas"** tagline line · a tappable **version** label (also the debug-unlock target).
**Debug pane** (hidden until unlocked): a list of on-device toggles/actions — first is **Show backyard fences** (drives `WorldBuilder.ForceFencesVisible`, [#152](https://github.com/derekwinters/lucas-doggiehood/issues/152)), followed by **Add coins** (a gold **+100** action that grants `DebugAddCoinsAmount` = 100 coins to the wallet via the Core `Wallet.Deposit` seam, so neighborhood expansion can be tested without grinding quests, [#286](https://github.com/derekwinters/lucas-doggiehood/issues/286)), then **Refresh quests now** (a gold action pill — same `DebugActionWidthPx`/`DebugActionHeightPx` size as **Add coins** — that immediately triggers the new-quest randomization via the Core `QuestManager.ForceRefresh` seam, skipping the hourly refresh timer so quest content can be tested without waiting, [#457](https://github.com/derekwinters/lucas-doggiehood/issues/457)), then **Show debug element colors** (a switch — same track/knob as **Show backyard fences** — that drives `WorldBuilder.ShowDebugElementColors`: when on, the base ground plane and the camera void backstop are painted two loudly-different, obviously-fake **debug colours** — magenta ground vs. cyan backstop, from the Core `DebugElementColors` seam — instead of the matched `Palette.GrassHex`, so a playtester can tell which element the bottom-of-screen "border" actually is; it is a **diagnostic-only** switch, **not** a shipped visual, **off by default** and reset each session like the rest of the Debug tab, and toggling it repaints the ground and reconfigures the camera live via the `SettingsPanel.DebugColorsRefresh` hook, [#611](https://github.com/derekwinters/lucas-doggiehood/issues/611)), then **Tune balance…** (an action row that opens the [Debug tuning menu](debug-tuning-menu.md) — an overlay of grouped balance sliders over the Core `TuningConfig`, layered as a centered modal over this Settings panel; it is built exactly like the rows above it, in **every** build, with the 10-tap Debug unlock as its only gate ([#656](https://github.com/derekwinters/lucas-doggiehood/issues/656) — reversing the dev-build-only rule #622 originally shipped); its action pill is the same `DebugActionWidthPx`/`DebugActionHeightPx` size as **Add coins**, tinted with the tuning wireframe's sky accent and reading **Open**, because it opens a sub-panel rather than performing a one-shot action, [#621](https://github.com/derekwinters/lucas-doggiehood/issues/621)/[#622](https://github.com/derekwinters/lucas-doggiehood/issues/622)). These rows are **not one flat list** — they are split across the Debug pane's own **sub-tabs**, below ([#716](https://github.com/derekwinters/lucas-doggiehood/issues/716)).

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
| `DebugSubTabHeightPx` | `72` | Each Debug **sub-tab** pill — the pill height the pane already uses (`DebugActionHeightPx`) ([#716](https://github.com/derekwinters/lucas-doggiehood/issues/716)) |
| `DebugSubTabGapPx` | `16` | Between sub-tab pills — the outer `TabGapPx`, one level in ([#716](https://github.com/derekwinters/lucas-doggiehood/issues/716)) |
| `DebugSubTabRadiusPx` | `24` | Sub-tab pill corners — the outer `TabRadiusPx`, same shape one level in ([#716](https://github.com/derekwinters/lucas-doggiehood/issues/716)) |
| `DebugSubTabBarGapPx` | `20` | Sub-tab bar → first row — one `DebugRowGapPx` ([#716](https://github.com/derekwinters/lucas-doggiehood/issues/716)) |
| `GearButtonSizePx` | `88` | HUD settings entry point |
| `GearMarginPx` | `32` | Gear inset from its HUD corner |
| `CloseButtonSizePx` | `72` | Close (✕) button, top-right |

The panel **chrome** and the **pill/tab** styling are owned by the shared components ([#173](https://github.com/derekwinters/lucas-doggiehood/issues/173), [shared-components.md](shared-components.md)) — not re-specified here; the shared baseline is a thick Ink outline (`OutlineThicknessPx` = 6), corner radius `PanelRadiusPx` = 40, and a single flat hard drop-shadow with no blur (`ShadowOffsetPx` = 8). This page places those components and sizes the panel, sidebar, tabs, version label, debug rows, and gear.

## Debug sub-tabs ([#716](https://github.com/derekwinters/lucas-doggiehood/issues/716))

A horizontal **sub-tab bar** sits at the top of the Debug content pane, inside the pane's own padding box, splitting the Debug rows into named groups. Only one group's rows show at a time; tapping a sub-tab swaps the row list beneath the bar. This is nested *inside* the outer sidebar tab (**About** / **Debug**) — it does not touch the sidebar, and the 10-tap unlock still gates the whole thing.

The bar is `DebugSubTabHeightPx` tall and spans the pane's full width; the pills split that width evenly, each giving up half of the shared `DebugSubTabGapPx` to its neighbour, so no per-pill width constant exists. They wear the same [PillButton](shared-components.md#pill-button-pillbutton) chrome as the sidebar tabs at `DebugSubTabRadiusPx` corners, with the same active-Coral / inactive-Cream role tints. The row list starts `DebugSubTabBarGapPx` below the bar, and rows inside a group stack from that list's own top at the existing `DebugRowHeightPx` / `DebugRowGapPx` metrics. The pane opens on **General**.

### Row → sub-tab

| Sub-tab | Rows |
|---|---|
| **General** | Show backyard fences · Add coins · Refresh quests now |
| **Visuals & Tools** | Show debug element colors · Tune balance… |
| **Reports** | Copy bug report · Save bug report ([#692](https://github.com/derekwinters/lucas-doggiehood/issues/692)) |

The grouping is data, not layout: it lives in the engine-free `Doggiehood.Core.Ui.DebugSubTabRoster`, so "every row is reachable from exactly one sub-tab — none dropped, none duplicated" is arithmetic a test checks rather than a thing to eyeball. #692's two **Reports** rows are *placed* here by #716 and *built* by #692; they already count against the capacity below.

### Capacity — computed, never counted by eye

The Debug pane's own box, at the 1920×1200 reference: `SettingsPanelHeightPx` 820, less the title band + top padding (152), less the bottom panel padding (48), less the pane's own 48px inset top and bottom → **524px**. The bar costs `DebugSubTabHeightPx` + `DebugSubTabBarGapPx` = **92px** off the top before any row starts, leaving 432px; at a row pitch of `DebugRowHeightPx` + `DebugRowGapPx` = 116px that is **3 rows per sub-tab**.

| Rows in a group | Bottom edge from the pane's top | Inside the 524px pane? |
|---|---|---|
| 1 | 92 + 96 = 188 | yes |
| 2 | 188 + 116 = 304 | yes |
| 3 | 304 + 116 = 420 | yes |
| 4 | 420 + 116 = 536 | **no** — 12px past the pane |

That number is **derived, never typed**: `Doggiehood.Core.Ui.SettingsDebugPaneMetrics` computes it from the constants above, so it stays correct if `SettingsPanelHeightPx` or the row metrics ever change.

**Invariant — every Debug row renders fully inside the Settings panel.** A row that does not fit is not merely tight, it is unreachable; each sub-tab's row capacity is computed from `SettingsPanelHeightPx` and the pane's own constants (including `DebugSubTabHeightPx`/`DebugSubTabBarGapPx`) **strictly inside the Debug pane's own box** — the panel's incidental bottom margin is never spent as capacity — so a group holding one row too many fails the suite instead of floating a button over the scrim.

> **How the spec is changing (#716).** This page used to describe the Debug pane as a single flat *"list of on-device toggles/actions"* with room for more, and never said how many rows would actually fit → it now describes a **sub-tabbed** pane: a `DebugSubTabHeightPx`-tall bar of named groups at the top, one group's rows on screen at a time, and a capacity **computed** from the pane's remaining height → because the flat list ran out of room at exactly five rows, its fifth only fit by quietly spending the panel's bottom margin, and the sixth ([#692](https://github.com/derekwinters/lucas-doggiehood/issues/692)'s bug-report rows) had nowhere on screen to go. The old wording had no rule that could have caught that, which is why the invariant above is now stated as a rule about *how* the pane may work, not just an outcome.


## Notes

- **Entry-point gear rendering ([#370](https://github.com/derekwinters/lucas-doggiehood/issues/370)).** The HUD **gear** (the entry point, drawn by `HudOverlay`, not the panel) wears the shared [Candy Cottage](../world/art-style.md) chrome, matching the neighbouring currency chip: an Ink outline disc with a hard straight-down drop-shadow over a cream fill, and a **procedural** Ink toothed-disc gear icon — no font glyph, no raster art. It previously drew a `⚙` (U+2699) font glyph on a default IMGUI button, which the bundled `DejaVuSans` cannot render, so on device it fell back to an unstyled gray box ([#291](https://github.com/derekwinters/lucas-doggiehood/issues/291)). Placement (`GearButtonSizePx` = 88, `GearMarginPx` = 32) and behaviour (tap opens Settings) are unchanged; see [hud.md](hud.md).
- **Debug unlock gesture.** Tapping the **version** label **10 times within 10 seconds** reveals the Debug tab in the sidebar; fewer taps, or 10 spread over more than 10s, does not. The gesture + the debug-toggle registry are engine-free **Core** logic (unit-tested); the panel/tabs/version display/toggle wiring is the Unity layer ([#219](https://github.com/derekwinters/lucas-doggiehood/issues/219)).
- **Debug ships hidden.** The Debug menu is in the build but unreachable without the gesture — no stray entry point.
- **The Debug tab is in shipping builds on purpose, for now ([#656](https://github.com/derekwinters/lucas-doggiehood/issues/656), Derek).** Every Debug affordance — including the **Tune balance…** row and the [tuning menu](debug-tuning-menu.md) it opens — is built in **every** build (development, release-candidate and the shipping release alike), with the 10-tap unlock as its **only** gate. No Debug row carries a build-configuration gate on top of it; `#622`'s dev-build-only rule for the tuning row was reversed by [#656](https://github.com/derekwinters/lucas-doggiehood/issues/656). This is deliberate and temporary: the debug menu stays in the game while the balance is still being dialed in on-device, and the **entire** debug menu (tab, gesture, rows, tuning panel) is removed in one later pass once Derek and Lucas are comfortable with the game — that removal gets its own issue at that time. Because the gesture is the only gate, it is asserted directly by EditMode tests rather than assumed.
- **Bundled UI font.** This is the game's first runtime-built UGUI (the HUD/onboarding are IMGUI), so its shader and font must be pulled into the build explicitly or the Android build strips them — the panel rendered as a magenta box with invisible text on device ([#291](https://github.com/derekwinters/lucas-doggiehood/issues/291)). Fixed by retaining `UI/Default` in **Always Included Shaders** and bundling **DejaVu Sans** (`Assets/UI/Fonts/Resources/DejaVuSans.ttf` — deliberately outside the `Assets/Art` low-poly-scanned tree, loaded via `Resources.Load`) instead of the Editor-only `Resources.GetBuiltinResource` lookup. DejaVu covers the ✕ close glyph but not the fullwidth plus, so the **Add coins** action uses a plain ASCII `+`. See `docs/engineering/unity-serialization.md`.
- **Runtime UGUI input needs an EventSystem.** UGUI pointer input requires **both** a `GraphicRaycaster` on the canvas (owned by `UiCanvas`) **and** an active `EventSystem` with an input module in the scene. Unity only auto-creates an EventSystem from the Editor's menu, never for runtime-built UI, so without one every control in this panel (close ✕, version-tap unlock, scrim tap-to-close, Debug toggles) is inert on device even though the handlers are wired — while the IMGUI HUD gear still opens Settings because it bypasses the EventSystem ([#327](https://github.com/derekwinters/lucas-doggiehood/issues/327)). `WorldBootstrap` therefore ensures exactly one persistent `EventSystem` + `StandaloneInputModule` beside the `UiCanvas` (the classic legacy-input module, matching `activeInputHandler: 0`), guarded against duplicates in `UiEventSystem.Ensure()`.
- **Standard this sets.** New debug affordances live as **toggles/actions in this Debug tab**, not as temporary code edits ([#219](https://github.com/derekwinters/lucas-doggiehood/issues/219)). Since [#716](https://github.com/derekwinters/lucas-doggiehood/issues/716) a new row also has to be **placed in a sub-tab** — add it to `DebugSubTabRoster`, and if its group is full, the fix is a new sub-tab, not a squeezed-in row.
- **Future tabs** (audio, save/reset) are out of scope, but the sidebar leaves room for them.
- **Reference resolution.** Constants are authored at the 1920×1200 (16:10) reference per [Overview](index.md); a Unity `CanvasScaler` scales from this so each px constant has a fixed meaning across tablet sizes.
- Style itself (outlines, flat shadows, pill shapes, rounded type) lives in [Art & UI Style](../world/art-style.md); this page is layout only.

## Design decisions (resolved)

1. **Entry point** — how Settings opens → **a gear button in the HUD's top-right corner (furthest right)**, with the **currency chip moved just inboard, to the gear's left**. This nudges the currency-chip placement, so the HUD wireframe ([#174](https://github.com/derekwinters/lucas-doggiehood/issues/174)) is flagged to reconcile — the gear takes the corner.
2. **Sidebar vs. bottom tabs on portrait** → **sidebar only.** The game is landscape-locked ([#22](https://github.com/derekwinters/lucas-doggiehood/issues/22)/[#256](https://github.com/derekwinters/lucas-doggiehood/issues/256)), so there is no portrait case that would need a bottom-tab variant.
3. **Does the Debug unlock persist across launches?** → **resets each session** (ships hidden, re-hidden every launch; re-tap to reveal). Persisting across launches remains a one-flag change if we later prefer it.
4. **How does the Debug pane hold more than five rows?** ([#716](https://github.com/derekwinters/lucas-doggiehood/issues/716)) → **sub-tabs on the Debug pane** — named groups behind a small bar at the top of the pane, rather than scrolling the pane, growing the panel, shrinking the rows, or doubling actions onto one row. No scrollbar region, no taller panel, and the row metric other tabs would share stays put.
