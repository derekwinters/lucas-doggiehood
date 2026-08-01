# Tile Catalog (Design Reference)

*Related: [#105](https://github.com/derekwinters/lucas-doggiehood/issues/105) (this page), [#106](https://github.com/derekwinters/lucas-doggiehood/issues/106) (sidewalks and the walk network, implemented — see [Sidewalks & Walk Network](sidewalks.md)), [#109](https://github.com/derekwinters/lucas-doggiehood/issues/109) (multi-tile grid/placement system, implemented), [#86](https://github.com/derekwinters/lucas-doggiehood/issues/86) (map-shape question, resolved 2026-07-14 — see [Neighborhood Expansion](../expansion.md#map-shape))*

!!! note "Status: implemented in Core ([#109](https://github.com/derekwinters/lucas-doggiehood/issues/109))"
    The catalog, grid-coordinate tile map, placement/adjacency validation, and per-type property-lot definitions are built in `Doggiehood.Core.World`: `TileType` (17 values), `TileTypeDefinition`/`TileCatalog` (road edges and, for the OpposingTurns types, arcs), `TileCoordinate`/`TileMap` (placement + adjacency), `TileGeometry` (world-space derivation), and `TileLotCatalog` (per-type lot slots for the 16 non-`FourWay` types). The starting `FourWay` intersection still uses its own hand-placed [`NeighborhoodLayout`](world.md) rather than this generic system — but its road arms now reach the **tile edge** (`NeighborhoodLayout.StreetHalfLength` = `WorldDimensions.TileSize / 2` = 30m, no longer the old hand-picked 26m), so when expansion places a neighbouring tile the two tiles' road arms meet edge-to-edge and the street network reads as continuous ([#392](https://github.com/derekwinters/lucas-doggiehood/issues/392); the kit-art corridor compresses a whole number of road tiles to fit each arm exactly, à la `WalkwayTiling` — see [Sidewalks & Walk Network](sidewalks.md#road-arm-extent)). Zone unlock/house-building on top of this geometry (v0.4's #55/#56/#57) is still future work — this issue only builds the geometry itself; procedural tile selection stays explicitly out of scope, per [Neighborhood Expansion](../expansion.md#map-shape). Sidewalks, crosswalks, and the walk network graph are implemented separately ([#106](https://github.com/derekwinters/lucas-doggiehood/issues/106)) — see [Sidewalks & Walk Network](sidewalks.md) — using a generic, data-driven graph rather than this tile/adjacency system.

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

## Grid coordinates

The world is an integer tile grid addressed as **`(x, y)`** — `x` runs east/west, `y` runs north/south. In Core these map to `TileCoordinate.Col` (`x`) and `TileCoordinate.Row` (`y`); world-space is derived by `TileGeometry` (`Col → +X`, `Row → +Z`). The starting `FourWay` intersection is the origin **`(0, 0)`**; the tile directly north of it is **`(0, 1)`**, directly east is **`(1, 0)`**.

A tile's identity is its integer `(x, y)`; a type name's compass suffix (e.g. `CulDeSacEast`) describes *which edge carries the road*, **not** where the tile sits on the map. Position comes from the coordinate, connectivity from the type/code below — keeping the two separate avoids the "is that northwest or is that an east-facing cul-de-sac?" confusion.

## The 17 tile types

Each tile is a 60m x 60m square with roads entering/exiting along some subset of its N/S/E/W edges. `FourWay` is the existing starting tile ([#7](https://github.com/derekwinters/lucas-doggiehood/issues/7), [#38](https://github.com/derekwinters/lucas-doggiehood/issues/38)); the other 16 are built for the multi-tile grid ([#109](https://github.com/derekwinters/lucas-doggiehood/issues/109)).

The **Code** column is the compact authoring token (see [Tile codes](#tile-codes-connectivity-as-a-single-source) below); it encodes the same road edges as the "Road edges" column.

| Type | Code | Road edges | Sketch |
|---|---|---|---|
| `FourWay` | `NSEW.` | N,S,E,W | `╋` — the starting tile |
| `StraightNS` | `NS--.` | N,S | `┃` |
| `StraightEW` | `--EW.` | E,W | `━` |
| `TurnNE` | `N-E-.` | N,E | `┗` |
| `TurnNW` | `N--W.` | N,W | `┛` |
| `TurnSE` | `-SE-.` | S,E | `┏` |
| `TurnSW` | `-S-W.` | S,W | `┓` |
| `TeeNorth` | `N-EW.` | E,W,N | `┻` (east/west with north half only — upside-down T) |
| `TeeSouth` | `-SEW.` | E,W,S | `┳` (east/west with south half only — T) |
| `TeeEast` | `NSE-.` | N,S,E | `┣` (north/south with east only) |
| `TeeWest` | `NS-W.` | N,S,W | `┫` (north/south with west only) |
| `CulDeSacNorth` | `N---.` | N | `╹` road enters from the north edge, ends in a bulb |
| `CulDeSacSouth` | `-S--.` | S | `╻` |
| `CulDeSacEast` | `--E-.` | E | `╺` |
| `CulDeSacWest` | `---W.` | W | `╸` |
| `OpposingTurnsNS` | `NSEW\` | N,E,S,W | `⬭` a NE-corner turn arc and an unrelated SW-corner turn arc — one bowing north(-east), one bowing south(-west) — enclosing a central island, **not** connected to each other |
| `OpposingTurnsEW` | `NSEW/` | N,E,S,W | `⬯` the 90° rotation: a NW-corner turn arc and an unrelated SE-corner turn arc, bowing west and east respectively — also not connected to each other |

### Tile codes — connectivity as a single source

The **Code** is a fixed-width connectivity token: slots 1–4 are the N/S/E/W edges (letter = road on that edge, `-` = none) and slot 5 is the junction tag — `.` for an ordinary single junction or dead-end, and `\` / `/` for the two `OpposingTurns` tiles, where the slash marks the wall between the tile's two disconnected arcs: `\` separates {N,E} from {S,W} (so the arcs are NE + SW = `OpposingTurnsNS`), `/` separates {N,W} from {S,E} (NW + SE = `OpposingTurnsEW`).

Because the first four slots *are* the road-edge data, adjacency is a pure slot comparison — a tile's East slot must agree with its east-neighbor's West slot (road meets road, or gap meets gap), while slot 5 never affects adjacency, only the tile's internal routing. The code is intended as the **single source** the Core `TileCatalog`, this table, and map-authoring all read, so the road-edge data can't drift between code and docs — see [#359](https://github.com/derekwinters/lucas-doggiehood/issues/359) for the Core unification that removes today's duplication (edges hand-listed in `TileCatalog.BuildDefinitions()` *and* re-typed in this table).

Maps are drawn and validated with the [Map Builder](../../tools/index.md) tool, which reads these codes.

### Road tile art — kit meshes per type

Each junction/terminus type renders a **single Kenney City Kit Roads mesh at the tile centre**, plus tiled `road-straight` arms reaching out to each road edge. `RoadTileArt` (Core) resolves the `TileType` → `(Resources key, yaw, bakes-crosswalks)` mapping; `WorldBuilder` places the mesh and yaws it so its authored orientation lines up with the tile's declared edges ([#508](https://github.com/derekwinters/lucas-doggiehood/issues/508)). The authored (0°-yaw) orientation of each staged 1×1-unit piece (→10×10m at `RoadTileScale`) was read from the kit OBJ vertices.

| Type(s) | Kit mesh | Baked crosswalks | Authored (0°) orientation |
|---|---|---|---|
| `FourWay` | `road-crossroad-path` | yes (4 arms) | symmetric |
| `TeeNorth/East/South/West` | `road-intersection-path` | yes (3 arms) | omits the SOUTH arm = `TeeNorth`; others are 90°/180°/270° |
| `TurnNW/NE/SE/SW` | `road-bend` | no | connects NORTH+WEST = `TurnNW`; rounded corner (Derek's locked call; `road-bend-square` is a one-line swap) |
| `CulDeSacEast/South/West/North` | `road-end-round` | no | road exits EAST = `CulDeSacEast`; rounded bulb |
| `StraightNS`, `StraightEW` | *(none — tiled `road-straight` arms)* | n/a | — |
| `OpposingTurnsNS/EW` | *(none yet — #508 follow-up)* | n/a | would compose two independent bends |

Crosswalks are baked into the 4-way/Tee meshes, so the kit path needs no separate crossing tiles; the primitive graybox fallback derives one crosswalk patch per intersection arm from `TileCrosswalkGeometry` instead — see [Sidewalks & Walk Network](sidewalks.md#the-crosswalk-box).

## Property lots per tile

*Design decisions 2026-07-30 / 2026-07-31 (Derek & Lucas), captured from the [Map Builder](../../tools/index.md). The Core lot rules below are implemented in `TileLotCatalog.LotsFor`; each bend's curved/cupped corner is exposed via `TileLotCatalog.TryGetCuppedCorner`, and a cul-de-sac's dropped bulb-side (tree) quadrants via `TileLotCatalog.TreeQuadrantsFor` ([#383](https://github.com/derekwinters/lucas-doggiehood/issues/383), refined by [#385](https://github.com/derekwinters/lucas-doggiehood/issues/385)).*

Each tile offers up to four **property lots**, one per quadrant (NE/NW/SE/SW). Not every quadrant holds a house — lot assignment is per tile type, and unbuilt quadrants become green space (parks/water in the open areas are future content, see [Neighborhood Expansion](../expansion.md)):

- **Twin bends (`OpposingTurnsNS`/`OpposingTurnsEW`): no lots.** Their two arcs leave no clean buildable quadrant.
- **Bends (`Turn*`): two lots — drop the small corner the curve cups AND its diagonal opposite.** The cupped corner is the bend's own corner (`TurnNE` drops NE, `TurnSW` drops SW, etc.); the corner diagonally opposite it borders neither roaded edge, so it can never face a road and is dropped too. The two kept lots each border a straight roaded edge square-on: `TurnNE`→NW,SE · `TurnNW`→NE,SW · `TurnSE`→NE,SW · `TurnSW`→NW,SE. A bend renders as a **curved corner**, not two straight bands meeting at a right angle.
- **Cul-de-sacs (`CulDeSac*`): two lots — keep the two quadrants adjacent to the single roaded edge.** `CulDeSacNorth`→NE,NW · `CulDeSacSouth`→SE,SW · `CulDeSacEast`→NE,SE · `CulDeSacWest`→NW,SW. The two bulb-side quadrants become **open space with trees** (reusing the #170 tree environment art, rendered by `WorldBuilder`). The two **kept** quadrants get houses, so their procedural [yard landscaping](world.md#yard-landscaping) must exclude the tile's road: the kept quadrant faces the tile's single road arm, and a lot on this non-origin tile is only trimmed against that road once the yard clip is made **tile-aware** — `LotBounds.RoadsFor(lot, tileType)` converts the tile's `TileRoadGeometry` arm to a `Road` alongside the origin's streets ([#455](https://github.com/derekwinters/lucas-doggiehood/issues/455), after a playtest showed yard trees landing in the first unlocked cul-de-sac's street).
- **All other types** (`FourWay`, `Straight*`, `Tee*`): all four quadrant lots.

**House facing — settled (2026-07-31, Derek): remove, no rotation.** On bends and cul-de-sacs the road curves, so a corner house can't always face it square-on. Rather than rotate houses to fan around a curve, the lots that can't face a road square-on are simply **removed** (they become green space, or open space with trees for cul-de-sacs) — which is exactly why bends and cul-de-sacs keep only two lots above. Every remaining lot already borders a straight roaded edge square-on, so no house ever carries a facing/rotation value.

## Resolved: opposing-turn arches do not join into a loop

*Resolved 2026-07-18 by Derek on [#109](https://github.com/derekwinters/lucas-doggiehood/issues/109), overriding the earlier #105 "loop/island" framing below*

> The two turns would not connect. Each arc would connect two adjacent sides only. There would be two distinct, unconnected arcs.

So each arch in `OpposingTurnsNS`/`OpposingTurnsEW` is a **turn** in exactly the same sense as the `TurnNE`/`TurnNW`/`TurnSE`/`TurnSW` tiles above: it joins two *adjacent* (corner) edges, not the two *opposite* edges the original framing assumed. `OpposingTurnsNS` is a `TurnNE`-shaped arc plus an unconnected `TurnSW`-shaped arc; `OpposingTurnsEW` is that pairing's 90° rotation (`TurnNW` + `TurnSE`). Between them the two arcs touch all four edges, but there is no path from one arc into the other — no loop, no shared connection point — matching Core's `TileArc`/`TileTypeDefinition.EdgesConnectedVia` in `Doggiehood.Core.World` (built by [#109](https://github.com/derekwinters/lucas-doggiehood/issues/109)), which scopes each edge's connection to its own arc only. The 15m `OpposingTurnArchRadius` (see [Standard dimensions](#standard-dimensions)) still describes each arc's individual curve; it just no longer describes one continuous loop.

<details>
<summary>Original (superseded) framing, kept for history</summary>

The original assumption, per Derek, was that each arch in `OpposingTurnsNS`/`OpposingTurnsEW` is "a curved road, an arch, likely a quarter circle radius" — implying the two arches join into a continuous loop around the central island, using the 15m `OpposingTurnArchRadius`, with both arches connecting the tile's two *opposite* edges (E/W for `OpposingTurnsNS`, N/S for `OpposingTurnsEW`). This is superseded by the resolution above.

</details>
