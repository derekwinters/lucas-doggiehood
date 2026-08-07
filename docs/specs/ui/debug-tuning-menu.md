# Debug tuning menu

*Wireframe issue: [#621](https://github.com/derekwinters/lucas-doggiehood/issues/621). Implements/covers: the balance tuning overlay ([#622](https://github.com/derekwinters/lucas-doggiehood/issues/622), over the Core `TuningConfig` of [#620](https://github.com/derekwinters/lucas-doggiehood/issues/620)). Approved: Derek, [#621](https://github.com/derekwinters/lucas-doggiehood/issues/621) (closed completed) — with the gating rule since corrected by [#656](https://github.com/derekwinters/lucas-doggiehood/issues/656), see [Notes](#notes).*
*Mockup: [mockups/debug-tuning-menu.html](mockups/debug-tuning-menu.html).*

## Purpose

A **debug-menu** overlay that exposes the game's balance values (the Core [`TuningConfig`](../../engineering/tech-stack.md), [#620](https://github.com/derekwinters/lucas-doggiehood/issues/620)) as labeled sliders, grouped by system, so Derek can dial pacing/economy/expansion/move-in in **live on-device** rather than guessing numbers and rebuilding. It is reached from a new **"Tune balance…"** action row in the Settings **Debug** tab ([settings.md](settings.md)) and layers as a centered modal overlay **over** the open Settings panel. It is a developer instrument, not player-facing chrome: styling is **deliberately utilitarian — explicitly NOT the [Candy Cottage](../world/art-style.md) art treatment** — and it is reached **only through the existing 10-tap Debug unlock** ([#219](https://github.com/derekwinters/lucas-doggiehood/issues/219)), the same gate as every other Debug-tab affordance. Reference resolution is 1920×1200 per [Overview](index.md).

## Regions

| Region | Contains | Shared component |
|---|---|---|
| Debug-tab entry row | A **"Tune balance…"** action row added to the Settings Debug pane; tapping it opens this overlay. Present in every build, behind the 10-tap Debug unlock | Debug action row ([settings.md](settings.md), `DebugRowHeightPx`) |
| Scrim | Full-screen dim behind the panel; makes the overlay **modal** — a tap over it is absorbed and never reaches the Settings panel or the world behind it ([modal-overlay convention](shared-components.md#modal-overlays-block-world-input)). Tapping it dismisses the tuning panel (returns to Settings) | — |
| Header | A top bar with the title **"Balance Tuning · DEV"**, a global **Reset all** button (right), and a **close ✕** | *(utilitarian — local, see Notes)* |
| Scroll body | A single vertically **scrollable** column holding the four groups in order: **Pacing**, **Economy**, **Expansion**, **Move-in** | *(utilitarian — local)* |
| Group | Per group: a group-header row (group name + a per-group **Reset** button) followed by that group's labeled control rows | *(utilitarian — local)* |
| Control row | One tunable: a **label** (left), its **live current value** (right, tabular), and a **slider** (track + knob) spanning the row. A linear `base + step×n` tunable is **two** control rows — one `base`, one `step` | Utilitarian slider (local, see Notes) |
| Reset controls | Global **Reset all** (in the header) and a per-group **Reset** button on each group header — each restores its scope to the shipping `TuningConfig` defaults | *(utilitarian — local)* |

### Group contents

Grounded in the epic [#619](https://github.com/derekwinters/lucas-doggiehood/issues/619) sub-issues and the central `TuningConfig` ([#620](https://github.com/derekwinters/lucas-doggiehood/issues/620)) — the **source of truth** for these constants and their defaults. The wireframe's job is the *structure* (which groups, scalar-vs-`base`+`step`), not the numbers.

**As built ([#622](https://github.com/derekwinters/lucas-doggiehood/issues/622)) the panel renders _every_ `TuningConfig` field, not a curated subset** — that is what "no hard-coded value list that can drift from Core" requires. The row set comes from the engine-free **`TuningCatalog`** (`Doggiehood.Core.Tuning`), one descriptor per `TuningConfig` instance field carrying its label, unit, group and slider range; a Core reflection test fails if a field is ever added without one. Adding a tunable in [#623–#626](https://github.com/derekwinters/lucas-doggiehood/issues/619) therefore adds its slider automatically. The mockup's ~17 illustrative rows are a subset of the real set for legibility.

| Group | Control rows (each = one slider) | Count |
|---|---|---|
| **Pacing** | Quest min target (floor) · Quest max target (ceiling) · Dogs per active quest · Pacing window · Refresh interval | 5 |
| **Economy** | Quest reward · Paid-quest markup · Starter cost min/max · Mid cost min/max · Premium cost min · Starter/Mid/Premium tier gates · Onboarding reward per step | 11 |
| **Expansion** | Tile unlock — base/step/origin tiles · House build — base/step/houses per step · House max level · Upgrade cost — to level 2/3/4 | 10 |
| **Move-in** | Early move-in chance · Early increment per quest · Late move-in chance · Late increment per quest · Early/Late population · Household weights (single, parent+puppy, three dogs) · Easter-egg chance · Breed weight smoothing | 11 |

**Slider ranges.** `TuningConfig` declares shipping defaults but no bounds, so `TuningCatalog` declares each field's `min`/`max`/`step` by a stated convention rather than per-field invention: a proportion is `0..1` step `0.01`; a multiplier runs from its neutral floor step `0.1`; a whole-number count/cost runs from its natural floor (`0`, or `1` where zero would be degenerate) to a round ceiling comfortably above the default, step `1`. A Core test asserts every shipping default sits inside its own range, so building the panel can never silently snap a balance value. These are dev-tool slider bounds, not balance numbers.

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

The implementation (`TuningMenuOverlay`) declares a handful of further named constants (#161) that the table above doesn't cover, each transcribed off the mockup CSS or derived from the constants above rather than invented: `HeaderButtonGapPx` = 20 (`.thead` gap), `ButtonRadiusPx` / `ControlRowRadiusPx` = 8, `ButtonOutlinePx` / `ControlRowOutlinePx` = 1, `GroupHeaderRuleHeightPx` = 2 (`.ghead` bottom rule), the button/close/group-reset font sizes (26 / 30 / 22), and `LabelLineHeightPx` + `ControlRowInsetYPx`, which centre the (label line + `SliderLabelValueGapPx` + slider band) stack inside `ControlRowHeightPx`.

## Notes

- **Visibility — the 10-tap Debug unlock is the only gate (corrected by [#656](https://github.com/derekwinters/lucas-doggiehood/issues/656), Derek).** The **"Tune balance…"** entry row, and therefore this whole overlay, is built in **every** build — development, release-candidate, and the shipping release alike — and is reachable **only** after the existing **10-tap Debug unlock** ([#219](https://github.com/derekwinters/lucas-doggiehood/issues/219)) has revealed the Debug tab. There is no build-configuration gate on it at all: `SettingsPanel.Init(state, version)` builds the row like every other Debug row, and `TuningMenuOverlay.Create(parent)` builds the overlay unconditionally, closed. *This reverses the rule this page originally carried* — that the row was dev-builds-only and "must never appear in a release build … not merely hidden behind the existing 10-tap Debug unlock". Derek overruled exactly that clause: *"it was meant to be part of the existing debug menu but not debug build version only … I want the debug menu there always, hidden by tap, until we decide to remove it when we are comfortable with the game."* Its presence in a shipping release is therefore **deliberate and temporary**, not an oversight — the balance is still being dialed in on-device. When Derek and Lucas are comfortable with the game, the **entire debug menu** (the Debug tab, its unlock gesture, this tuning panel and the other on-device debug affordances) is removed in one deliberate pass, which will get its own issue at that time. Because the unlock gesture is now the *only* thing between a shipping player and the balance sliders, EditMode tests assert that gate directly: the row and its pill are inactive until ten version taps inside the window reveal the Debug tab and it is selected.
- **Entry point (resolved — Option A, Derek 2026-08-06, [#621](https://github.com/derekwinters/lucas-doggiehood/issues/621#issuecomment-5204316292)).** The menu is reached from a **new action row in the existing Settings Debug tab** ("Tune balance…"), consistent with the [#219 standard](settings.md) that debug affordances live in the Debug tab. This introduces a "row opens a sub-panel" pattern the Debug tab hasn't used before (its rows were all inline toggles/actions).
- **Panel-vs-Settings relationship — layer, don't replace.** The tuning panel opens as a **centered modal overlay layered over** the still-open Settings panel (which stays behind, dimmed by the scrim), per the [modal-overlay convention](shared-components.md#modal-overlays-block-world-input). It does **not** tear down / replace Settings. Closing the tuning panel (✕ or scrim tap) returns to the Settings Debug tab exactly as it was, so tuning is a quick in-and-out from Debug rather than a navigation away. The overlay registers with `ModalInputGate` while open so taps never leak to Settings or the world beneath.
- **Scroll behavior.** The four groups live in one **vertically scrollable** column inside the fixed-height panel; the header (title + Reset all + ✕) is **pinned** and does not scroll. When all controls fit, no scrollbar shows; when they overflow (the common case with ~17 control rows), a `ScrollbarWidthPx` vertical scrollbar appears and content is inset by `ScrollBodyPaddingRightPx` so a knob never overlaps it. Groups do **not** collapse — it is a single flat scroll, not sub-tabs (kept simple for a dev tool).
- **The utilitarian slider (local, layout-only addition).** Sliders have **no** entry in [Shared UI Components](shared-components.md) today, and this screen is explicitly exempt from Candy Cottage chrome, so a plain slider is documented **here** rather than promoted to a shared style decision. Each slider is: a **label** + its **live current value** (tabular) on one line, and below it a horizontal **track** (`SliderTrackHeightPx`) with a draggable **knob** (`SliderKnobPx`). Dragging updates the live value in place and pushes it straight into the active `TuningConfig` field ([#620](https://github.com/derekwinters/lucas-doggiehood/issues/620)) — the change is live. Each slider's min/max/step come from that field's declared range in `TuningConfig`, not from this layout.
- **`base` + `step` = two sliders.** A tunable that is a linear `base + step×n` function (tile unlock, house build, upgrade cost) is shown as **two** adjacent control rows — one for `base`, one for `step` — matching the field shape [#620](https://github.com/derekwinters/lucas-doggiehood/issues/620) exposes. "A few sliders per item is fine."
- **Reset-to-defaults — both scopes.** A **global Reset all** in the header restores every field to the shipping `TuningConfig` defaults; each group also carries its **own Reset** restoring just that group's fields. Reset = re-seed from a fresh `TuningConfig` ([#620](https://github.com/derekwinters/lucas-doggiehood/issues/620)'s reset-to-defaults seam); the live values snap back. Two Core seams back this: `TuningConfig.ResetToDefaults()` (global — swaps in a fresh instance) and `TuningConfig.ResetGroupToDefaults(group)` ([#622](https://github.com/derekwinters/lucas-doggiehood/issues/622) — restores just that group's fields **in place**, so the other groups' live overrides survive a partial reset).
- **TuningConfig is the source of these values.** This overlay is a *view* over the central Core [`TuningConfig`](../../engineering/tech-stack.md) ([#620](https://github.com/derekwinters/lucas-doggiehood/issues/620)); it declares no balance numbers of its own — labels, ranges, defaults, and current values all come from Core (the config plus its `TuningCatalog` descriptors). The representative numbers in the mockup are illustrative only. A drag writes straight into `TuningConfig.Active` and the Core seams re-read it on every access, so the change is live with no restart; the write is clamped/snapped to the field's declared range by Core, never by the view.

- **Implementation deltas from the mockup ([#622](https://github.com/derekwinters/lucas-doggiehood/issues/622)).** Two intentional, cosmetic-only differences: the header renders the Regions table's single title string **"Balance Tuning · DEV"** rather than the mockup's separate title + `DEV` chip; and the Debug-tab entry pill reads **"Open"** rather than "Open ▸", because the bundled UI font (DejaVu Sans, [#291](https://github.com/derekwinters/lucas-doggiehood/issues/291)) is not guaranteed to carry the triangle glyph — the same ASCII-only rule the **Add coins** / **Refresh quests now** pills already follow ([settings.md](settings.md)).
- **Reference resolution.** Constants are authored at the 1920×1200 (16:10) reference per [Overview](index.md); a Unity `CanvasScaler` scales from this so each px constant has a fixed meaning across tablet sizes.
- **Style is deliberately NOT Candy Cottage.** Unlike every other wireframe here, this page does not source its look from [Art & UI Style](../world/art-style.md) — it is a utilitarian dev tool (flat panel, thin outline, monospace-friendly value readouts, plain track/knob sliders). It stays clean, legible, and self-contained, but carries none of the outlines/hard-shadows/pill chrome. This exemption is intentional and recorded so a later reviewer doesn't "fix" it toward the game's art direction.
