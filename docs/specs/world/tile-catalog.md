# Tile Catalog (Design Reference)

*Related: [#105](https://github.com/derekwinters/lucas-doggiehood/issues/105) (this page), [#106](https://github.com/derekwinters/lucas-doggiehood/issues/106) (sidewalks and the walk network, implemented — see [Sidewalks & Walk Network](sidewalks.md)), [#109](https://github.com/derekwinters/lucas-doggiehood/issues/109) (multi-tile grid/placement system, implemented), [#86](https://github.com/derekwinters/lucas-doggiehood/issues/86) (map-shape question, resolved 2026-07-14 — see [Neighborhood Expansion](../expansion.md#map-shape))*

!!! note "Status: implemented in Core ([#109](https://github.com/derekwinters/lucas-doggiehood/issues/109))"
    The catalog, grid-coordinate tile map, placement/adjacency validation, and per-type property-lot definitions are built in `Doggiehood.Core.World`: `TileType` (17 values), `TileTypeDefinition`/`TileCatalog` (road edges and, for the OpposingTurns types, arcs), `TileCoordinate`/`TileMap` (placement + adjacency), `TileGeometry` (world-space derivation), and `TileLotCatalog` (per-type lot slots for the 16 non-`FourWay` types). The starting `FourWay` intersection still uses its own hand-placed [`NeighborhoodLayout`](world.md) rather than this generic system. Zone unlock/house-building on top of this geometry (v0.4's #55/#56/#57) is still future work — this issue only builds the geometry itself; procedural tile selection stays explicitly out of scope, per [Neighborhood Expansion](../expansion.md#map-shape). Sidewalks, crosswalks, and the walk network graph are implemented separately ([#106](https://github.com/derekwinters/lucas-doggiehood/issues/106)) — see [Sidewalks & Walk Network](sidewalks.md) — using a generic, data-driven graph rather than this tile/adjacency system.

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

Each tile is a 60m x 60m square with roads entering/exiting along some subset of its N/S/E/W edges. `FourWay` is the existing starting tile ([#7](https://github.com/derekwinters/lucas-doggiehood/issues/7), [#38](https://github.com/derekwinters/lucas-doggiehood/issues/38)); the other 16 are built for the multi-tile grid ([#109](https://github.com/derekwinters/lucas-doggiehood/issues/109)).

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
| `OpposingTurnsNS` | N,E,S,W | `⬭` a NE-corner turn arc and an unrelated SW-corner turn arc — one bowing north(-east), one bowing south(-west) — enclosing a central island, **not** connected to each other |
| `OpposingTurnsEW` | N,E,S,W | `⬯` the 90° rotation: a NW-corner turn arc and an unrelated SE-corner turn arc, bowing west and east respectively — also not connected to each other |

## Resolved: opposing-turn arches do not join into a loop

*Resolved 2026-07-18 by Derek on [#109](https://github.com/derekwinters/lucas-doggiehood/issues/109), overriding the earlier #105 "loop/island" framing below*

> The two turns would not connect. Each arc would connect two adjacent sides only. There would be two distinct, unconnected arcs.

So each arch in `OpposingTurnsNS`/`OpposingTurnsEW` is a **turn** in exactly the same sense as the `TurnNE`/`TurnNW`/`TurnSE`/`TurnSW` tiles above: it joins two *adjacent* (corner) edges, not the two *opposite* edges the original framing assumed. `OpposingTurnsNS` is a `TurnNE`-shaped arc plus an unconnected `TurnSW`-shaped arc; `OpposingTurnsEW` is that pairing's 90° rotation (`TurnNW` + `TurnSE`). Between them the two arcs touch all four edges, but there is no path from one arc into the other — no loop, no shared connection point — matching Core's `TileArc`/`TileTypeDefinition.EdgesConnectedVia` in `Doggiehood.Core.World` (built by [#109](https://github.com/derekwinters/lucas-doggiehood/issues/109)), which scopes each edge's connection to its own arc only. The 15m `OpposingTurnArchRadius` (see [Standard dimensions](#standard-dimensions)) still describes each arc's individual curve; it just no longer describes one continuous loop.

<details>
<summary>Original (superseded) framing, kept for history</summary>

The original assumption, per Derek, was that each arch in `OpposingTurnsNS`/`OpposingTurnsEW` is "a curved road, an arch, likely a quarter circle radius" — implying the two arches join into a continuous loop around the central island, using the 15m `OpposingTurnArchRadius`, with both arches connecting the tile's two *opposite* edges (E/W for `OpposingTurnsNS`, N/S for `OpposingTurnsEW`). This is superseded by the resolution above.

</details>
