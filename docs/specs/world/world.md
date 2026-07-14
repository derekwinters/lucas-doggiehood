# World & Neighborhood

*Epic: [#2](https://github.com/derekwinters/lucas-doggiehood/issues/2)*

## Starting scope

The first playable neighborhood is deliberately small: **one intersection of two streets, with one house in each of the four quadrants.** ([#38](https://github.com/derekwinters/lucas-doggiehood/issues/38))

- 4 houses total to start, each home to a dog household (see [Dog Roster & Names](../dogs/roster-names.md))
- More streets and houses are designed but explicitly **post-MVP** — see [Neighborhood Expansion](../expansion.md)

## Layout

Streets are lined with houses that the player views and explores from above ([#7](https://github.com/derekwinters/lucas-doggiehood/issues/7)). There is no player-character traversal — see [Camera, Navigation & Controls](camera-controls.md) for how the player actually moves through the space.

## House scale

> **Decision (2026-07-14, Derek, on [#145](https://github.com/derekwinters/lucas-doggiehood/issues/145)):** every City Kit Suburban house model renders at **one fixed uniform scale: ×7** — the kit-wide default unless a specific model gets a documented exception (none today). This replaces the earlier rule that normalized each model's max horizontal footprint to a uniform 8m, which gave every model a *different* scale factor — houses weren't actually at the same scale, so doors read different sizes house to house.

The constant lives in Core as `HousePlacement.KitScale` (`WorldBuilder.HouseKitScale` is the Unity-side alias), and everything downstream — front-setback placement, door world positions, walkway starts, the catalog gallery — derives from it. At ×7 the four starting models' footprints are: building-type-b **12.80×7.98m**, building-type-g **10.15×8.25m**, building-type-k **6.45×7.14m**, building-type-m **10.00×10.00m**. ×8 was rejected at the time because building-type-b would have been 14.6m wide against [#129](https://github.com/derekwinters/lucas-doggiehood/issues/129)'s then-current 15m lot-fence square (a 0.5m-margin guard); at ×7 it kept a 1.1m margin. That square is gone — [#146](https://github.com/derekwinters/lucas-doggiehood/issues/146) reshaped fences to anchor on the house itself (see [Backyard fences](#backyard-fences)), whose own guard is the 0.5m rear-wall clearance — but the ×7 decision stands on its own.

## Backyard fences

> **Decision (2026-07-14, Derek, on [#146](https://github.com/derekwinters/lucas-doggiehood/issues/146), reshaping [#129](https://github.com/derekwinters/lucas-doggiehood/issues/129)):** each fence starts at the **midpoint of each side wall of the house** and wraps around the **back yard** only — the **front yard stays open**. No fence line crosses the front, so #129's gate-gap design is gone: the front walkway ([#128](https://github.com/derekwinters/lucas-doggiehood/issues/128)) runs door → sidewalk through the open front and never meets a fence. Fences are **defined for every lot but hidden by default** — a future quest purchases them ([#147](https://github.com/derekwinters/lucas-doggiehood/issues/147)).

The geometry lives in Core (`LotFence`): three continuous runs — side-wall midpoint → rear corner, rear line, rear corner → side-wall midpoint — that rotate with the house facing (a house facing +X has its side walls along Z). Fence width follows the house: the side anchors sit at the scaled side walls' midpoints, so the width is each model's scaled `FootprintX` at the fixed ×7 kit scale ([#145](https://github.com/derekwinters/lucas-doggiehood/issues/145)). The rear line reuses #129's 7.5m boundary constant (`RearBoundaryFromLotCenter`) **for the rear only**: lot center + 7.5m away from the faced street, which every model's setback-shifted rear wall clears by at least 3.0m at ×7 (test-enforced at ≥ 0.5m; the deepest model, building-type-m at 10.00m, keeps 3.004m). Its house-relative equivalent is **13m behind the scaled front facade** (`RearLineBehindFacade` — the [#127](https://github.com/derekwinters/lucas-doggiehood/issues/127) setback puts every facade 5.5m street-side of its lot center), which is the form the catalog gallery uses. Exact alignment with property layouts is deferred to [#147](https://github.com/derekwinters/lucas-doggiehood/issues/147).

Fencing is **per lot** — `HouseLot.HasFence`, default **off** since #146 — so the built world renders **no fences** today. The geometry stays queryable for a disabled lot: `LotFence.GeometryFor` always describes the fence (the #147 purchase flow and the [#126](https://github.com/derekwinters/lucas-doggiehood/issues/126) gallery need it), while the flag-respecting `LotFence.RunsFor` — empty while hidden — is what `WorldBuilder` consumes. `WorldBuilder.ForceFencesVisible` is the Editor-check/test seam that builds every fence anyway.

Visually, fences render as tiled Kenney City Kit Suburban `fence` pieces at uniform scale ×5 (1.35m tall — half the ground-tile scale, which would read as a 2.7m wall). Core's `FenceTiling` compresses a whole number of pieces to cover each run exactly (the same compress-to-fit rule as the walkway pavers), so fence ends land precisely on the side-wall anchors and back-yard corners; `WorldBuilder` instantiates the pieces verbatim and falls back to one thin graybox rail per run when the kit piece can't be loaded. The catalog gallery shows each model's real backyard fence outline from the same Core API (`LotFence.BackyardRuns`).

## Lighting & time

Static, pleasant daytime lighting — always sunny/mid-day. **No day/night cycle and no weather system for MVP.** ([#39](https://github.com/derekwinters/lucas-doggiehood/issues/39))

This is a deliberate simplification: it avoids needing lighting variants for every asset, and a future weather/day-night system is tracked separately as an explicit future idea — see [Future Ideas](../future-ideas.md).

## Art direction

Low-poly 3D art style throughout — houses, dogs, streets, and props all share this style ([#6](https://github.com/derekwinters/lucas-doggiehood/issues/6)). The specific color palette and house architecture are specified in [Art & UI Style](art-style.md).

A cover art concept for the game exists at [#90](https://github.com/derekwinters/lucas-doggiehood/issues/90) as a visual reference for overall tone.

## Build checklist

- [ ] One intersection, two streets, 4 house lots (one per quadrant)
- [ ] Houses placed per [Art & UI Style](art-style.md) (cottage silhouettes, one per house)
- [x] Backyard fence defined per lot (anchored at the house's side-wall midpoints, front yard open), hidden by default until purchased ([#146](https://github.com/derekwinters/lucas-doggiehood/issues/146); purchase quest: [#147](https://github.com/derekwinters/lucas-doggiehood/issues/147))
- [ ] Static daytime lighting setup, no day/night or weather systems present
- [ ] Scene readable and navigable at the isometric camera angle (see [Camera, Navigation & Controls](camera-controls.md))
