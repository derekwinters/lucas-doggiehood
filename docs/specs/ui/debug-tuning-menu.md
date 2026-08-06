# Debug tuning menu

*Wireframe issue: [#621](https://github.com/derekwinters/lucas-doggiehood/issues/621). Implements/covers: the dev-build-only balance tuning overlay ([#622](https://github.com/derekwinters/lucas-doggiehood/issues/622), over the Core `TuningConfig` of [#620](https://github.com/derekwinters/lucas-doggiehood/issues/620)). Approved: pending Derek review.*
*Mockup: [mockups/debug-tuning-menu.html](mockups/debug-tuning-menu.html).*

## Purpose

A **dev-build-only** overlay that exposes the game's balance values (the Core [`TuningConfig`](../../engineering/tech-stack.md), [#620](https://github.com/derekwinters/lucas-doggiehood/issues/620)) as labeled sliders, grouped by system, so Derek can dial pacing/economy/expansion/move-in in **live on-device** rather than guessing numbers and rebuilding. It is reached from a new **"Tune balance…"** action row in the Settings **Debug** tab ([settings.md](settings.md)) and layers as a centered modal overlay **over** the open Settings panel. It is a developer instrument, not player-facing chrome: styling is **deliberately utilitarian — explicitly NOT the [Candy Cottage](../world/art-style.md) art treatment** — and it **must never appear in a release build**. Reference resolution is 1920×1200 per [Overview](index.md).

## Regions

| Region | Contains | Shared component |
|---|---|---|
| Debug-tab entry row | A **"Tune balance…"** action row added to the Settings Debug pane; tapping it opens this overlay. Dev builds only | Debug action row ([settings.md](settings.md), `DebugRowHeightPx`) |
| Scrim | Full-screen dim behind the panel; makes the overlay **modal** — a tap over it is absorbed and never reaches the Settings panel or the world behind it ([modal-overlay convention](shared-components.md#modal-overlays-block-world-input)). Tapping it dismisses the tuning panel (returns to Settings) | — |
| Header | A top bar with the title **"Balance Tuning · DEV"**, a global **Reset all** button (right), and a **close ✕** | *(utilitarian — local, see Notes)* |
| Scroll body | A single vertically **scrollable** column holding the four groups in order: **Pacing**, **Economy**, **Expansion**, **Move-in** | *(utilitarian — local)* |
| Group | Per group: a group-header row (group name + a per-group **Reset** button) followed by that group's labeled control rows | *(utilitarian — local)* |
| Control row | One tunable: a **label** (left), its **live current value** (right, tabular), and a **slider** (track + knob) spanning the row. A linear `base + step×n` tunable is **two** control rows — one `base`, one `step` | Utilitarian slider (local, see Notes) |
| Reset controls | Global **Reset all** (in the header) and a per-group **Reset** button on each group header — each restores its scope to the shipping `TuningConfig` defaults | *(utilitarian — local)* |

### Group contents (representative tunables)

Grounded in the epic [#619](https://github.com/derekwinters/lucas-doggiehood/issues/619) sub-issues and the central `TuningConfig` ([#620](https://github.com/derekwinters/lucas-doggiehood/issues/620)) — the **source of truth** for these constants and their defaults. Values shown are representative; the point is the *structure* (which tunables, scalar-vs-`base`+`step`), not final numbers, which live in `TuningConfig` and land via [#623–#626](https://github.com/derekwinters/lucas-doggiehood/issues/619).

| Group | Control rows (each = one slider) | Shape |
|---|---|---|
| **Pacing** | Quest min target (floor) · Quest max target (ceiling) · Pacing window (hours) · Refresh interval (minutes) | 4 scalars |
| **Economy** | Quest reward (coins) · Paid-quest markup (×) · Cost-tier 1 gate (coins) · Cost-tier 2 gate (coins) | 4 scalars |
| **Expansion** | Tile unlock — base · Tile unlock — step · House build — base · House build — step · Upgrade cost — base · Upgrade cost — step | 3 linear = 6 sliders |
| **Move-in** | Early rate (%) · Late rate (%) · Population span | 3 scalars |

## Anchors & layout constants

Every size, margin, and anchor is a **named constant** — never a fixed pixel position — so the layout holds across the supported tablet aspect ratios. These are the exact constants the [#622](https://github.com/derekwinters/lucas-doggiehood/issues/622) implementation declares (per [#161](../../engineering/tech-stack.md#geometry-layout-and-tuning-values-are-named-variables)); EditMode tests assert the built overlay against them. This screen is **exempt from the shared Candy Cottage chrome**, so it does **not** inherit the shared outline/shadow/radius baseline — its own utilitarian values are named here instead.

| Constant | Value | Applies to |
|---|---|---|
| `TuningPanelAnchor` | `Center` | Panel position — centered over a dim scrim, layered above the Settings panel |
| `TuningPanelWidthPx` | `1200` | Panel width |
| `TuningPanelHeightPx` | `920` | Panel height (fixed; the body scrolls within) |
| `TuningPanelPaddingPx` | `32` | Panel inset (padding) |
| `TuningPanelRadiusPx` | `16` | Panel corners (utilitarian — squarer than the Candy `PanelRadiusPx` = 40) |
| `TuningPanelOutlinePx` | `2` | Panel outline (thin — not the shared `OutlineThicknessPx` = 6) |
| `HeaderHeightPx` | `88` | Header bar |
| `HeaderTitleFontPx` | `40` | Header title |
| `HeaderGapPx` | `24` | Header bar → scroll body |
| `CloseButtonSizePx` | `56` | Close (✕) button, header right |
| `ResetAllButtonWidthPx` | `220` | Global **Reset all** button (header) |
| `ResetButtonHeightPx` | `56` | Reset-all and per-group Reset button height |
| `ScrollBodyPaddingRightPx` | `24` | Inset between body content and the scrollbar |
| `ScrollbarWidthPx` | `12` | Vertical scrollbar |
| `GroupGapPx` | `28` | Between one group and the next |
| `GroupHeaderHeightPx` | `64` | Group-header row |
| `GroupHeaderFontPx` | `32` | Group name |
| `GroupResetButtonWidthPx` | `150` | Per-group **Reset** button |
| `ControlRowHeightPx` | `96` | Each labeled control (slider) row |
| `ControlRowGapPx` | `12` | Between control rows within a group |
| `ControlRowPaddingXPx` | `24` | Control-row horizontal inset |
| `ControlLabelFontPx` | `28` | Control label |
| `ControlValueFontPx` | `30` | Live current value (tabular figures) |
| `SliderTrackHeightPx` | `12` | Slider track |
| `SliderTrackRadiusPx` | `6` | Slider track corners |
| `SliderKnobPx` | `40` | Slider knob (drag target) |
| `SliderLabelValueGapPx` | `8` | Label/value row → slider track |
| `EntryRowHeightPx` | `96` | The **"Tune balance…"** Debug-tab entry row (matches `DebugRowHeightPx`, [settings.md](settings.md)) |

The **entry row** reuses the Settings Debug-pane row metrics ([settings.md](settings.md) `DebugRowHeightPx` = 96, action-pill sizing) — it is a Debug action row like **Add coins** / **Refresh quests now**, not a new component. Everything inside the tuning panel is **local, utilitarian** chrome sized by the constants above; it intentionally does **not** point at [Shared UI Components](shared-components.md) for its fills/outline/shadow.

## Notes

- **Dev-build-only visibility — the hard rule.** The **"Tune balance…"** entry row, and therefore this whole overlay, is compiled/shown **only in development builds** (e.g. gated behind `Debug.isDebugBuild` / a `DEVELOPMENT_BUILD` guard at the Unity layer). It **must never appear in a release build** — not merely hidden behind the existing 10-tap Debug unlock, but absent from release entirely. The unlock gesture still gates the Debug *tab* itself as today; the dev-build guard is an additional, release-excluding gate on this row.
- **Entry point (resolved — Option A, Derek 2026-08-06, [#621](https://github.com/derekwinters/lucas-doggiehood/issues/621#issuecomment-5204316292)).** The menu is reached from a **new action row in the existing Settings Debug tab** ("Tune balance…"), consistent with the [#219 standard](settings.md) that debug affordances live in the Debug tab. This introduces a "row opens a sub-panel" pattern the Debug tab hasn't used before (its rows were all inline toggles/actions).
- **Panel-vs-Settings relationship — layer, don't replace.** The tuning panel opens as a **centered modal overlay layered over** the still-open Settings panel (which stays behind, dimmed by the scrim), per the [modal-overlay convention](shared-components.md#modal-overlays-block-world-input). It does **not** tear down / replace Settings. Closing the tuning panel (✕ or scrim tap) returns to the Settings Debug tab exactly as it was, so tuning is a quick in-and-out from Debug rather than a navigation away. The overlay registers with `ModalInputGate` while open so taps never leak to Settings or the world beneath.
- **Scroll behavior.** The four groups live in one **vertically scrollable** column inside the fixed-height panel; the header (title + Reset all + ✕) is **pinned** and does not scroll. When all controls fit, no scrollbar shows; when they overflow (the common case with ~17 control rows), a `ScrollbarWidthPx` vertical scrollbar appears and content is inset by `ScrollBodyPaddingRightPx` so a knob never overlaps it. Groups do **not** collapse — it is a single flat scroll, not sub-tabs (kept simple for a dev tool).
- **The utilitarian slider (local, layout-only addition).** Sliders have **no** entry in [Shared UI Components](shared-components.md) today, and this screen is explicitly exempt from Candy Cottage chrome, so a plain slider is documented **here** rather than promoted to a shared style decision. Each slider is: a **label** + its **live current value** (tabular) on one line, and below it a horizontal **track** (`SliderTrackHeightPx`) with a draggable **knob** (`SliderKnobPx`). Dragging updates the live value in place and pushes it straight into the active `TuningConfig` field ([#620](https://github.com/derekwinters/lucas-doggiehood/issues/620)) — the change is live. Each slider's min/max/step come from that field's declared range in `TuningConfig`, not from this layout.
- **`base` + `step` = two sliders.** A tunable that is a linear `base + step×n` function (tile unlock, house build, upgrade cost) is shown as **two** adjacent control rows — one for `base`, one for `step` — matching the field shape [#620](https://github.com/derekwinters/lucas-doggiehood/issues/620) exposes. "A few sliders per item is fine."
- **Reset-to-defaults — both scopes.** A **global Reset all** in the header restores every field to the shipping `TuningConfig` defaults; each group also carries its **own Reset** restoring just that group's fields. Reset = re-seed from a fresh `TuningConfig` ([#620](https://github.com/derekwinters/lucas-doggiehood/issues/620)'s reset-to-defaults seam); the live values snap back.
- **TuningConfig is the source of these values.** This overlay is a *view* over the central Core [`TuningConfig`](../../engineering/tech-stack.md) ([#620](https://github.com/derekwinters/lucas-doggiehood/issues/620)); it declares no balance numbers of its own — labels, ranges, defaults, and current values all come from that config. The representative numbers in the mockup are illustrative only.
- **Reference resolution.** Constants are authored at the 1920×1200 (16:10) reference per [Overview](index.md); a Unity `CanvasScaler` scales from this so each px constant has a fixed meaning across tablet sizes.
- **Style is deliberately NOT Candy Cottage.** Unlike every other wireframe here, this page does not source its look from [Art & UI Style](../world/art-style.md) — it is a utilitarian dev tool (flat panel, thin outline, monospace-friendly value readouts, plain track/knob sliders). It stays clean, legible, and self-contained, but carries none of the outlines/hard-shadows/pill chrome. This exemption is intentional and recorded so a later reviewer doesn't "fix" it toward the game's art direction.
