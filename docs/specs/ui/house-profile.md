# House profile

*Wireframe issue: [#293](https://github.com/derekwinters/lucas-doggiehood/issues/293) (unblocks [#208](https://github.com/derekwinters/lucas-doggiehood/issues/208)). Implements/covers: house profile view (opened by tapping a house). Approved: Derek, 2026-07-26 (in-session).*
*Mockup: [mockups/house-profile.html](mockups/house-profile.html).*

## Purpose

Tapping a **house** opens its profile ([#208](https://github.com/derekwinters/lucas-doggiehood/issues/208)) — the mirror of the [dog profile](dog-profile.md) ([#177](https://github.com/derekwinters/lucas-doggiehood/issues/177)). A centered card over a dimmed scene shows the house's **level** (1–4), the **resident dog(s)** as tappable links to their own profiles, and an **entry point to the house-upgrade action** ([#59](https://github.com/derekwinters/lucas-doggiehood/issues/59)). Content scope is option B, settled by Derek on [#208](https://github.com/derekwinters/lucas-doggiehood/issues/208) (2026-07-25). Reference resolution is 1920×1200 per [Overview](index.md).

## Regions

| Region | Contains | Shared component |
|---|---|---|
| Header | House thumbnail · "House" label · **level badge** (`Lv N` + N-of-4 pips) | [Shared panel chrome](shared-components.md) |
| Residents | Vertical list of resident rows; each row is a tappable link (avatar · name · breed chip) opening that [dog's profile](dog-profile.md). Vacant houses show an empty state instead | [Shared panel chrome](shared-components.md) |
| Footer action | The **Upgrade** button — entry point to the house-upgrade action ([#59](https://github.com/derekwinters/lucas-doggiehood/issues/59)); shows the next cost, or a disabled *Max level* state at level 4 | [PillButton](shared-components.md#pill-button-pillbutton) |
| Close | Top-right ✕ dismiss affordance | [Shared panel chrome](shared-components.md) |

## Anchors & layout constants

| Constant | Value | Applies to |
|---|---|---|
| `ProfileAnchor` | `Center` | Card position — centered over a dim scrim so the neighborhood stays visible behind it (matches [dog profile](dog-profile.md)) |
| `ProfileWidthPx` | `900` | Card width (matches [dog profile](dog-profile.md) so the two profiles read as a pair) |
| `ProfilePaddingPx` | `48` | Card inset (padding) |
| `ThumbnailSizePx` | `220` | House thumbnail (mirrors the dog portrait's `PortraitSizePx`) |
| `LevelPipCount` | `4` | Number of level pips (the level cap) |
| `LevelPipDiameterPx` | `28` | Each level pip |
| `LevelPipGapPx` | `12` | Gap between pips |
| `ResidentRowHeightPx` | `120` | Each resident link row |
| `ResidentRowGapPx` | `16` | Gap between resident rows |
| `ResidentAvatarSizePx` | `96` | Resident avatar inside a row |
| `CloseButtonSizePx` | `72` | Close (✕) button, top-right (matches [dog profile](dog-profile.md)) |

The panel **chrome** (outline 6 / corner radius 40 / drop-shadow 12–14) and the **Upgrade** button (96 px [PillButton](shared-components.md#pill-button-pillbutton), spend/coral role tint) are owned by the [shared components](shared-components.md) ([#173](https://github.com/derekwinters/lucas-doggiehood/issues/173)) — neither is re-specified here. The resident breed chip reuses the same chip shape the [dog profile](dog-profile.md) uses. This page introduces **no new atomic component**: the level badge is a label plus a row of shared chip-shaped pips, and each resident row is shared panel chrome wrapping an avatar, a name, and a breed chip.

*Status: the Candy Cottage chrome is **implemented** ([#465](https://github.com/derekwinters/lucas-doggiehood/issues/465)) — `Assets/Scripts/Unity/HouseProfileOverlay.cs` draws the card (Panel fill, Ink outline, hard drop-shadow, `PanelRadiusPx` corners), the Cream close pill, and the Coral Upgrade `PillButton` (greying onto the Disabled role when unaffordable / at Max level) via the shared UGUI `CandyChromeUgui` helper. The level badge + pips, resident rows, breed chips, and the render-to-texture thumbnail / avatar frames ([#464](https://github.com/derekwinters/lucas-doggiehood/issues/464)) carry the Ink outline as inline elements (no drop-shadow); non-palette accent fills (stage tan, sky) are kept, remapping only the palette-role fills onto the named `CandyChromeUgui` constants ([#161](../../engineering/tech-stack.md#geometry-layout-and-tuning-values-are-named-variables)). Layout is unchanged.*

## Notes

- **Resident-count states (open question 2 in [#293](https://github.com/derekwinters/lucas-doggiehood/issues/293), resolved here).** The residents region is a vertical list sized by `ResidentRowHeightPx`/`ResidentRowGapPx`; the card grows with the row count. Households are single / parent+puppy / three-dog ([Expansion → Move-in system](../expansion.md#move-in-system)), so the list holds **0–3** rows and never needs scrolling:
  - **0 (vacant):** no rows; a muted empty-state line — *"No dogs live here yet."* — reflecting the greyscaled vacant house ([#58](https://github.com/derekwinters/lucas-doggiehood/issues/58)). No Upgrade action is offered for a vacant house.
  - **1:** one resident row.
  - **2 (parent + puppy):** two rows; the shared breed follows from the household rule.
  - **3:** three rows.
- **Upgrade entry point (open question 1 in [#293](https://github.com/derekwinters/lucas-doggiehood/issues/293), resolved here).** The single footer action is **Upgrade** — the house-upgrade action from [House leveling](../expansion.md#house-leveling) ([#59](https://github.com/derekwinters/lucas-doggiehood/issues/59)), the one house-level action with settled Core behavior and pricing. The button shows the next upgrade's cost (100 / 200 / 400 coins) and disables into a *Max level* state at level 4 (`HouseUpgradeNumbers.MaxLevel`). This wireframe designs the **entry-point affordance**; the button's behavior when tapped was the open flow decision carried to [#294](https://github.com/derekwinters/lucas-doggiehood/issues/294). A **decoration-slot jump** is deliberately *not* included: no decoration-management screen has a wireframe yet, so a jump would point at an undesigned destination — deferred until such a screen exists (flag, don't invent, per rule #8).
- **Upgrade behavior ([#294](https://github.com/derekwinters/lucas-doggiehood/issues/294), Derek's Option A, 2026-07-26).** Tapping **Upgrade** spends coins **directly** through the Core entry point `GameState.TryUpgradeHouse(houseId)` — **no confirmation screen** (so #294 needed no new overlay/wireframe). Beyond the *Max level* disabled state, the button also **greys out and disables when the live wallet balance can't cover the next step**, reflecting affordability (`HouseProfile.CanAffordUpgrade`) read live against the wallet — the same "never cache" contract the currency HUD uses, never snapshotted. On a successful upgrade the open profile re-renders in place, so the new `Lv N` / filled pips / next cost / affordability show immediately — and the **house in the world re-renders too**, swapping up to the new level's mesh so the home visibly grows ([#407](https://github.com/derekwinters/lucas-doggiehood/issues/407); the world-side mechanism is `HouseUpgradeDirector.RefreshHouse`, detailed in the [#59 upgrade implementation note](../expansion.md#house-leveling)). The affordable-vs-unaffordable disabled treatment reuses the same graybox disabled tint as *Max level* (no new chrome); the shared-component styling pass ([#173](https://github.com/derekwinters/lucas-doggiehood/issues/173)) owns its final look.
- **Level display.** Level 1–4 is shown as `Lv N` plus `LevelPipCount` pips (filled = current level), so the current level and the remaining headroom to upgrade both read at a glance. Per-level house *visuals* are the model-swap ladders in [House leveling](../expansion.md#house-leveling); the thumbnail renders the house's current mesh (see the render note below).
- **Thumbnail & resident avatars are render-to-texture snapshots ([#464](https://github.com/derekwinters/lucas-doggiehood/issues/464), Derek's Option A, 2026-08-01).** The `ThumbnailSizePx` box and each `ResidentAvatarSizePx` box show a **live render of the actual 3D model**, not the flat placeholder color: an off-screen portrait camera (`PortraitCamera`) snapshots the house's real current model — its actual kit variant and upgrade level, with its vacancy/palette tint — into the thumbnail, and each resident dog's real breed-tinted model into its avatar. The house model name is resolved through the same Core source the world uses (`Doggiehood.Core.Art.HouseModelResolver`), so a profile can never drift from the house standing in the neighborhood. The snapshot is captured **once on overlay-open** (and again on a successful upgrade, when the profile re-renders) — not live every frame — so up to four portraits (1 house + up to 3 residents) cost one render each. This is a content-fill change within the already-approved boxes; the layout is unchanged.
- **Data.** Level, vacancy, and the resident roster read from the house's Core data (`Doggiehood.Core.World.House` — `IsVacant`, level; residents via the dog roster); this page fixes layout only. The field values shown in the mockup are placeholders.
- **Symmetry with the dog profile.** Card anchor, width, padding, thumbnail size, and close button match the [dog profile](dog-profile.md) so tapping a dog and tapping its house feel like two faces of the same card. A resident link here opens the [dog profile](dog-profile.md); the dog profile's **Home** button is the reciprocal path back to the house.
- **Reference resolution.** Constants are authored at the 1920×1200 (16:10) reference per [Overview](index.md); a Unity `CanvasScaler` scales from this so each px constant has a fixed meaning across tablet sizes.
- Style itself (outlines, flat shadows, pill shapes, rounded type) lives in [Art & UI Style](../world/art-style.md); this page is layout only.
