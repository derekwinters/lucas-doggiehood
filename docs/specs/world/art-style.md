# Art & UI Style

*Issues: [#64](https://github.com/derekwinters/lucas-doggiehood/issues/64) (world/house style), [#65](https://github.com/derekwinters/lucas-doggiehood/issues/65) (UI chrome)*

The house and world palette below is implemented: the neighborhood renders real [Kenney City Kit Suburban](https://kenney.nl/assets/city-kit-suburban) (CC0) models, staged under `Assets/Art/Houses/CityKitSuburban/`. The UI chrome direction ("Candy Cottage") is still a decided-but-not-yet-implemented spec for the components listed below.

## Color palette

**Bright & saturated.** Bold, punchy colors that read clearly at a distance and skew playful rather than naturalistic — not the muted/earthy or soft-pastel alternatives that were considered. ([#64](https://github.com/derekwinters/lucas-doggiehood/issues/64))

Environment surfaces (grass, street, sidewalk, crosswalk) carry this palette as hex constants on `Doggiehood.Core.Art.Palette`. Dog coats carry it as per-breed hex data. Houses carry it through the kit's own hand-painted textures (below), not through Core-owned hex color data.

## House architecture

**Real kit models, one per house, painted with the kit's own texture variants.** Each of the 4 starting houses ([World & Neighborhood](world.md)) renders as one of the 21 Kenney City Kit Suburban house meshes — chosen so no two starting houses share a model — giving each dog's home its own silhouette, rather than a uniform tract-house look. ([#64](https://github.com/derekwinters/lucas-doggiehood/issues/64))

Per-house color comes from swapping in one of the kit's own hand-painted texture variants (`colormap` — the kit's default — plus `variation-a`/`variation-b`/`variation-c`) as the model's main texture, rather than from procedural roof/porch geometry or Core-owned hex colors. `Doggiehood.Core.Art.HouseStyleTable` is the single source of truth for both which model (`HouseStyle.ModelName`) and which texture variant (`HouseStyle.TintVariant`) each starting house gets; `WorldBuilder.BuildHouseModel` applies the tint by cloning the model's renderer materials and setting `.mainTexture` to the loaded variant texture — a real texture swap, since the kit's variant textures are distinct hand-painted alternates, not a white base meant to be color-multiplied. The specific 4 model letters currently assigned (b/g/k/m) are a placeholder pending Derek and Lucas's own visual review ([#122](https://github.com/derekwinters/lucas-doggiehood/issues/122)) — swapping which of the 21 letters is used is a config change to `HouseStyleTable`, not a rebuild.

When the kit model can't be loaded (an EditMode test seam, `WorldBuilder.ForcePrimitiveFallback`), houses fall back to a single plain graybox primitive with no per-house color or roof/porch detail — the graybox path exists to keep the world buildable/testable without the art assets, not to preserve the style spec on its own.

## UI chrome direction: "Candy Cottage"

Thick dark outlines on all UI chrome, flat hard drop-shadows (no blur), chunky pill-shaped buttons and chips, bold rounded sans-serif type. A sticker-book feel, chosen specifically over a flatter "paper-craft" alternative for its legibility and match with the bright/saturated palette and low-poly toy-shelf look. ([#65](https://github.com/derekwinters/lucas-doggiehood/issues/65))

Applies to: the currency chip, the speech bubble indicator, the dialogue box, decoration/gift choice buttons, and should extend to menus/settings for consistency.

A reference mockup exists at [this artifact](https://claude.ai/code/artifact/e3f24c36-85e3-4301-a099-c16c8ecc47f0), showing both the chosen "Candy Cottage" direction and the rejected "Flat Paper-Craft" alternative for contrast.

## Build checklist

- [x] Defined color palette (bright/saturated hex values, or the kit's equivalent hand-painted textures for houses) applied consistently across houses, dogs, UI
- [x] 4 distinct house silhouettes (real kit models, one per house, painted with a distinct kit texture variant per house — placeholder model picks pending [#122](https://github.com/derekwinters/lucas-doggiehood/issues/122))
- [ ] UI components (buttons, chips, dialogue box) use thick dark outlines + flat hard drop-shadows
- [ ] Currency chip, speech bubble icon, dialogue box, and choice buttons all follow the Candy Cottage direction
- [ ] Menus/settings screens follow the same UI chrome for consistency
