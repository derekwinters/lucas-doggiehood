# Art & UI Style

*Issues: [#64](https://github.com/derekwinters/lucas-doggiehood/issues/64) (world/house style), [#65](https://github.com/derekwinters/lucas-doggiehood/issues/65) (UI chrome)*

The house and world palette below is implemented: the neighborhood renders real [Kenney City Kit Suburban](https://kenney.nl/assets/city-kit-suburban) (CC0) models, staged under `Assets/Art/Houses/CityKitSuburban/`. The UI chrome direction ("Candy Cottage") is still a decided-but-not-yet-implemented spec for the components listed below.

## Color palette

**Bright & saturated.** Bold, punchy colors that read clearly at a distance and skew playful rather than naturalistic — not the muted/earthy or soft-pastel alternatives that were considered. ([#64](https://github.com/derekwinters/lucas-doggiehood/issues/64))

Environment surfaces (grass, street, sidewalk, crosswalk) carry this palette as hex constants on `Doggiehood.Core.Art.Palette`. Dog coats carry it as per-breed hex data. Houses carry it through the kit's own hand-painted textures (below), not through Core-owned hex color data.

## House architecture

**Real kit models, one per house, painted with the kit's own texture variants.** Each of the 4 starting houses ([World & Neighborhood](world.md)) renders as one of the 21 Kenney City Kit Suburban house meshes — chosen so no two starting houses share a model — giving each dog's home its own silhouette, rather than a uniform tract-house look. ([#64](https://github.com/derekwinters/lucas-doggiehood/issues/64))

Per-house color comes from swapping in one of the kit's own hand-painted texture variants (`colormap` — the kit's default — plus `variation-a`/`variation-b`/`variation-c`) as the model's main texture, rather than from procedural roof/porch geometry or Core-owned hex colors. `Doggiehood.Core.Art.HouseStyleTable` is the single source of truth for both which model (`HouseStyle.ModelName`) and which texture variant (`HouseStyle.TintVariant`) each starting house gets; `WorldBuilder.BuildHouseModel` applies the tint by cloning the model's renderer materials and setting `.mainTexture` to the loaded variant texture — a real texture swap, since the kit's variant textures are distinct hand-painted alternates, not a white base meant to be color-multiplied. The specific 4 model letters assigned are r/h/k/q: the Level-1 (as-built) mesh of each house's proposed [#59](https://github.com/derekwinters/lucas-doggiehood/issues/59) upgrade path, so homes start small and visibly grow as they level up. This is Derek's call (2026-07-25) resolving the earlier placeholder picks flagged in [#122](https://github.com/derekwinters/lucas-doggiehood/issues/122); each house keeps its original tint variant (the tint is independent of the model). The per-house front-door anchors for the new starters r/h/q are provisional placeholders (centered on X, toward the street) pending a gallery authoring pass, unlike the door data for the retained meshes, which is authored. Swapping which of the 21 letters is used is a config change to `HouseStyleTable`, not a rebuild.

Because r/h/k/q are each house's *Level-1* mesh, upgrading a house ([#59](https://github.com/derekwinters/lucas-doggiehood/issues/59)) swaps in the next rung of its per-house model ladder — the full L1→L4 ladders and their "anchored on L1" rule live in [Neighborhood Expansion → House leveling](../expansion.md#house-leveling), backed by `Doggiehood.Core.Art.HouseLevelModelTable`. As of the v0.6 de-graybox pass every rung has a full `HouseModelCatalog` entry, so all four levels render their chosen kit mesh at the fixed uniform `HousePlacement.KitScale` — a house visibly grows as it levels up while every ladder mesh still fits the shared lot envelope (no per-model normalization, no lot resize). The only thing still deferred is the door anchor of each non-L1 ladder mesh (and the r/h/q L1 starters), which stays a provisional placeholder pending a gallery authoring pass; the models are final. The graybox fallback remains only for a genuinely-unknown mesh (e.g. an expansion house with no ladder).

When the kit model can't be loaded (an EditMode test seam, `WorldBuilder.ForcePrimitiveFallback`), houses fall back to a single plain graybox primitive with no per-house color or roof/porch detail — the graybox path exists to keep the world buildable/testable without the art assets, not to preserve the style spec on its own.

## UI chrome direction: "Candy Cottage"

This section owns UI *style* only. UI **layout** — a screen's regions, anchors, and named size/margin constants — lives in the [UI Wireframes](../ui/index.md) section and follows the [UI Design Process](../../engineering/ui-design-process.md) (wireframe approved before any code). The style rules below are pulled forward as the shared baseline on [Shared UI Components](../ui/shared-components.md); every per-screen wireframe references them rather than restating them.

Thick dark outlines on all UI chrome, flat hard drop-shadows (no blur), chunky pill-shaped buttons and chips, bold rounded sans-serif type. A sticker-book feel, chosen specifically over a flatter "paper-craft" alternative for its legibility and match with the bright/saturated palette and low-poly toy-shelf look. ([#65](https://github.com/derekwinters/lucas-doggiehood/issues/65))

Applies to: the currency chip, the speech bubble indicator, the dialogue box, decoration/gift choice buttons, and should extend to menus/settings for consistency.

A reference mockup exists at [this artifact](https://claude.ai/code/artifact/e3f24c36-85e3-4301-a099-c16c8ecc47f0), showing both the chosen "Candy Cottage" direction and the rejected "Flat Paper-Craft" alternative for contrast.

## Build checklist

- [x] Defined color palette (bright/saturated hex values, or the kit's equivalent hand-painted textures for houses) applied consistently across houses, dogs, UI
- [x] 4 distinct house silhouettes (real kit models, one per house, painted with a distinct kit texture variant per house — the Level-1 mesh r/h/k/q of each house's [#59](https://github.com/derekwinters/lucas-doggiehood/issues/59) upgrade path, per Derek's 2026-07-25 call resolving the [#122](https://github.com/derekwinters/lucas-doggiehood/issues/122) placeholder picks; r/h/q door anchors provisional pending a gallery pass)
- [ ] UI components (buttons, chips, dialogue box) use thick dark outlines + flat hard drop-shadows
- [ ] Currency chip, speech bubble icon, dialogue box, and choice buttons all follow the Candy Cottage direction
- [ ] Menus/settings screens follow the same UI chrome for consistency
