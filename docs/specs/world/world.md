# World & Neighborhood

*Epic: [#2](https://github.com/derekwinters/lucas-doggiehood/issues/2)*

## Starting scope

The first playable neighborhood is deliberately small: **one intersection of two streets, with one house in each of the four quadrants.** ([#38](https://github.com/derekwinters/lucas-doggiehood/issues/38))

- 4 houses total to start, each home to a dog household (see [Dog Roster & Names](../dogs/roster-names.md))
- More streets and houses are designed but explicitly **post-MVP** — see [Neighborhood Expansion](../expansion.md)

## Layout

Streets are lined with houses that the player views and explores from above ([#7](https://github.com/derekwinters/lucas-doggiehood/issues/7)). There is no player-character traversal — see [Camera, Navigation & Controls](camera-controls.md) for how the player actually moves through the space.

## Lot fences

> **Decision (2026-07-13, Derek, on [#129](https://github.com/derekwinters/lucas-doggiehood/issues/129)):** each house lot is bounded by a fence with a **gate gap where the front walkway ([#128](https://github.com/derekwinters/lucas-doggiehood/issues/128)) crosses**, so the walkway always passes through the gate.

The geometry lives in Core (`LotFence`): an axis-aligned square of half-extent **7.5m** around the lot center — strictly clear of the sidewalk (8.25m away on both streets of a corner lot, leaving a 0.75m grass strip) and containing every setback-shifted house footprint with margin (the [#127](https://github.com/derekwinters/lucas-doggiehood/issues/127) facade sits 5.5m street-side of the lot center), all test-enforced. The gate gap is **3m** wide (the walkway's 2m visual width plus 0.5m clearance per side), centered on the walkway line.

Fencing is **per lot** — `HouseLot.HasFence`, default **on** for all four starting lots — so a later design decision can make fences a buyable decoration or house-level upgrade without reshaping the geometry; that decision is explicitly *not* made yet. A lot with the flag off contributes no fence geometry.

Visually, fences render as tiled Kenney City Kit Suburban `fence` pieces at uniform scale ×5 (1.35m tall — half the ground-tile scale, which would read as a 2.7m wall). Core's `FenceTiling` compresses a whole number of pieces to cover each run exactly (the same compress-to-fit rule as the walkway pavers), so fence ends land precisely on lot corners and gate-gap edges; `WorldBuilder` instantiates the pieces verbatim and falls back to one thin graybox rail per run when the kit piece can't be loaded.

## Lighting & time

Static, pleasant daytime lighting — always sunny/mid-day. **No day/night cycle and no weather system for MVP.** ([#39](https://github.com/derekwinters/lucas-doggiehood/issues/39))

This is a deliberate simplification: it avoids needing lighting variants for every asset, and a future weather/day-night system is tracked separately as an explicit future idea — see [Future Ideas](../future-ideas.md).

## Art direction

Low-poly 3D art style throughout — houses, dogs, streets, and props all share this style ([#6](https://github.com/derekwinters/lucas-doggiehood/issues/6)). The specific color palette and house architecture are specified in [Art & UI Style](art-style.md).

A cover art concept for the game exists at [#90](https://github.com/derekwinters/lucas-doggiehood/issues/90) as a visual reference for overall tone.

## Build checklist

- [ ] One intersection, two streets, 4 house lots (one per quadrant)
- [ ] Houses placed per [Art & UI Style](art-style.md) (cottage silhouettes, one per house)
- [x] Each lot fenced (per-lot flag, all four on) with the gate gap centered on its front walkway ([#129](https://github.com/derekwinters/lucas-doggiehood/issues/129))
- [ ] Static daytime lighting setup, no day/night or weather systems present
- [ ] Scene readable and navigable at the isometric camera angle (see [Camera, Navigation & Controls](camera-controls.md))
