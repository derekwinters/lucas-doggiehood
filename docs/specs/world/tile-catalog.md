# Tile Catalog (Design Reference)

*Related: [#105](https://github.com/derekwinters/lucas-doggiehood/issues/105) (this page), [#106](https://github.com/derekwinters/lucas-doggiehood/issues/106) (sidewalks and the walk network, implemented — see [Sidewalks & Walk Network](sidewalks.md)), [#109](https://github.com/derekwinters/lucas-doggiehood/issues/109) (deferred multi-tile grid/placement system, slated for v0.4), [#86](https://github.com/derekwinters/lucas-doggiehood/issues/86) (map-shape question, resolved 2026-07-14 — see [Neighborhood Expansion](../expansion.md#map-shape))*

!!! note "Status: design reference only, not implemented"
    This catalog is a design reference for the deferred multi-tile grid/placement system ([#109](https://github.com/derekwinters/lucas-doggiehood/issues/109)) — it is **not implemented in Core by this issue**. Today the codebase contains exactly one tile: the `FourWay` starting intersection, built as [`NeighborhoodLayout`](world.md). No `TileType` enum, adjacency validation, or per-type lot logic exists yet. Its sidewalks, crosswalks, and walk network graph **are** implemented ([#106](https://github.com/derekwinters/lucas-doggiehood/issues/106)) — see [Sidewalks & Walk Network](sidewalks.md) — using a generic, data-driven graph rather than any tile/adjacency system, so that implementation doesn't presuppose an answer to this catalog's open questions.

## Standard dimensions

These 7 measurements are locked in Core (`WorldDimensions`, [#105](https://github.com/derekwinters/lucas-doggiehood/issues/105)) as the geometric basis every tile type below will eventually be built from.

| Standard | Value |
|---|---|
| Tile size | 60m x 60m |
| Road width | 6m |
| Grass verge (road edge -> sidewalk) | 0.75m — Derek's 2026-07-13 midpoint decision (in conversation; originally 1.5m, briefly 0m the same day): a logical setback that puts the dogs' walk line at 4.75m from the road centerline, within the City Kit road tiles' paved band ([#121](https://github.com/derekwinters/lucas-doggiehood/issues/121)/[#122](https://github.com/derekwinters/lucas-doggiehood/issues/122)); no visual grass strip in the kit-art path — see [Sidewalks & Walk Network](sidewalks.md) |
| Sidewalk width | 2m |
| Crosswalk width | 3m |
| Cul-de-sac bulb radius | 9m |
| Opposing-turn arch radius | quarter-circle, 15m (peak of arch reaches ~15m from tile center) |

## The 17 tile types

Each tile is a 60m x 60m square with roads entering/exiting along some subset of its N/S/E/W edges. `FourWay` is the existing starting tile ([#7](https://github.com/derekwinters/lucas-doggiehood/issues/7), [#38](https://github.com/derekwinters/lucas-doggiehood/issues/38)); the other 16 are designed for the future multi-tile grid ([#109](https://github.com/derekwinters/lucas-doggiehood/issues/109)).

| Type | Road edges | Sketch |
|---|---|---|
| `FourWay` | N,S,E,W | `╋` — the starting tile |
| `StraightNS` | N,S | `┃` |
| `StraightEW` | E,W | `━` |
| `TurnNE` | N,E | `┗` |
| `TurnNW` | N,W | `┛` |
| `TurnSE` | S,E | `┏` |
| `TurnSW` | S,W | `┓` |
| `TeeNorth` | E,W,N | `┻` (east/west with north half only — upside-down T) |
| `TeeSouth` | E,W,S | `┳` (east/west with south half only — T) |
| `TeeEast` | N,S,E | `┣` (north/south with east only) |
| `TeeWest` | N,S,W | `┫` (north/south with west only) |
| `CulDeSacNorth` | N | `╹` road enters from the north edge, ends in a bulb |
| `CulDeSacSouth` | S | `╻` |
| `CulDeSacEast` | E | `╺` |
| `CulDeSacWest` | W | `╸` |
| `OpposingTurnsNS` | E,W | `⬭` two quarter-circle-radius arches joining the E/W edge connections — one bowing north, one bowing south — enclosing a central island |
| `OpposingTurnsEW` | N,S | `⬯` the 90° rotation: arches bowing east and west |

## Open design question: do opposing-turn arches join into a loop?

*Unresolved — carried forward to [#109](https://github.com/derekwinters/lucas-doggiehood/issues/109), does not block this issue*

The current assumption, per Derek, is that each arch in `OpposingTurnsNS`/`OpposingTurnsEW` is "a curved road, an arch, likely a quarter circle radius" — implying the two arches join into a continuous loop around the central island, using the 15m `OpposingTurnArchRadius`. Whether they're actually meant to connect into a loop, versus being disconnected curves, is not yet decided. This needs an answer before tile geometry is built in [#109](https://github.com/derekwinters/lucas-doggiehood/issues/109), but not before then.
