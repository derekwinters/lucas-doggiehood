# World & Neighborhood

*Epic: [#2](https://github.com/derekwinters/lucas-doggiehood/issues/2)*

## Starting scope

The first playable neighborhood is deliberately small: **one intersection of two streets, with one house in each of the four quadrants.** ([#38](https://github.com/derekwinters/lucas-doggiehood/issues/38))

- 4 houses total to start, each home to a dog household (see [Dog Roster & Names](../dogs/roster-names.md))
- More streets and houses are designed but explicitly **post-MVP** — see [Neighborhood Expansion](../expansion.md)

## Layout

Streets are lined with houses that the player views and explores from above ([#7](https://github.com/derekwinters/lucas-doggiehood/issues/7)). There is no player-character traversal — see [Camera, Navigation & Controls](camera-controls.md) for how the player actually moves through the space.

## Lighting & time

Static, pleasant daytime lighting — always sunny/mid-day. **No day/night cycle and no weather system for MVP.** ([#39](https://github.com/derekwinters/lucas-doggiehood/issues/39))

This is a deliberate simplification: it avoids needing lighting variants for every asset, and a future weather/day-night system is tracked separately as an explicit future idea — see [Future Ideas](../future-ideas.md).

## Art direction

Low-poly 3D art style throughout — houses, dogs, streets, and props all share this style ([#6](https://github.com/derekwinters/lucas-doggiehood/issues/6)). The specific color palette and house architecture are specified in [Art & UI Style](art-style.md).

A cover art concept for the game exists at [#90](https://github.com/derekwinters/lucas-doggiehood/issues/90) as a visual reference for overall tone.

## Build checklist

- [ ] One intersection, two streets, 4 house lots (one per quadrant)
- [ ] Houses placed per [Art & UI Style](art-style.md) (cottage silhouettes, one per house)
- [ ] Static daytime lighting setup, no day/night or weather systems present
- [ ] Scene readable and navigable at the isometric camera angle (see [Camera, Navigation & Controls](camera-controls.md))
