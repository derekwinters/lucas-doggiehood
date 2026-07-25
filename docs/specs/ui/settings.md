# Settings menu

*Wireframe issue: [#218](https://github.com/derekwinters/lucas-doggiehood/issues/218). Implements/covers: the in-game settings menu ([#219](https://github.com/derekwinters/lucas-doggiehood/issues/219)). Approved: Derek, 2026-07-25 (in-session).*
*Mockup: [mockups/settings.html](mockups/settings.html).*

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

**About pane:** app name · a tappable **version** label (also the debug-unlock target) · a small credits line.
**Debug pane** (hidden until unlocked): a list of on-device toggles/actions — first is **Show backyard fences** (drives `WorldBuilder.ForceFencesVisible`, [#152](https://github.com/derekwinters/lucas-doggiehood/issues/152)); room for more (e.g. **Add coins**, [#286](https://github.com/derekwinters/lucas-doggiehood/issues/286)).

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
| `GearButtonSizePx` | `88` | HUD settings entry point |
| `GearMarginPx` | `32` | Gear inset from its HUD corner |
| `CloseButtonSizePx` | `72` | Close (✕) button, top-right |

The panel **chrome** (outline 6 / corner radius 40 / drop-shadow 12) and the **pill/tab** styling are owned by the shared components ([#173](https://github.com/derekwinters/lucas-doggiehood/issues/173)) — not re-specified here; this page places those components and sizes the panel, sidebar, tabs, version label, debug rows, and gear.

## Notes

- **Debug unlock gesture.** Tapping the **version** label **10 times within 10 seconds** reveals the Debug tab in the sidebar; fewer taps, or 10 spread over more than 10s, does not. The gesture + the debug-toggle registry are engine-free **Core** logic (unit-tested); the panel/tabs/version display/toggle wiring is the Unity layer ([#219](https://github.com/derekwinters/lucas-doggiehood/issues/219)).
- **Debug ships hidden.** The Debug menu is in the build but unreachable without the gesture — no stray entry point.
- **Standard this sets.** New debug affordances live as **toggles/actions in this Debug tab**, not as temporary code edits ([#219](https://github.com/derekwinters/lucas-doggiehood/issues/219)).
- **Future tabs** (audio, save/reset) are out of scope, but the sidebar leaves room for them.
- **Reference resolution.** Constants are authored at the 1920×1200 (16:10) reference per [Overview](index.md); a Unity `CanvasScaler` scales from this so each px constant has a fixed meaning across tablet sizes.
- Style itself (outlines, flat shadows, pill shapes, rounded type) lives in [Art & UI Style](../world/art-style.md); this page is layout only.

## Design decisions (resolved)

1. **Entry point** — how Settings opens → **a gear button in the HUD's top-right corner (furthest right)**, with the **currency chip moved just inboard, to the gear's left**. This nudges the currency-chip placement, so the HUD wireframe ([#174](https://github.com/derekwinters/lucas-doggiehood/issues/174)) is flagged to reconcile — the gear takes the corner.
2. **Sidebar vs. bottom tabs on portrait** → **sidebar only.** The game is landscape-locked ([#22](https://github.com/derekwinters/lucas-doggiehood/issues/22)/[#256](https://github.com/derekwinters/lucas-doggiehood/issues/256)), so there is no portrait case that would need a bottom-tab variant.
3. **Does the Debug unlock persist across launches?** → **resets each session** (ships hidden, re-hidden every launch; re-tap to reveal). Persisting across launches remains a one-flag change if we later prefer it.
