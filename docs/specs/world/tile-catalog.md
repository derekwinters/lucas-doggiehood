# Tile Catalog (Design Reference)

*Related: [#105](https://github.com/derekwinters/lucas-doggiehood/issues/105) (this page), [#106](https://github.com/derekwinters/lucas-doggiehood/issues/106) (sidewalks and the walk network, implemented — see [Sidewalks & Walk Network](sidewalks.md)), [#109](https://github.com/derekwinters/lucas-doggiehood/issues/109) (multi-tile grid/placement system, implemented), [#86](https://github.com/derekwinters/lucas-doggiehood/issues/86) (map-shape question, resolved 2026-07-14 — see [Neighborhood Expansion](../expansion.md#map-shape))*

!!! note "Status: implemented in Core ([#109](https://github.com/derekwinters/lucas-doggiehood/issues/109))"
    The catalog, grid-coordinate tile map, placement/adjacency validation, and per-type property-lot definitions are built in `Doggiehood.Core.World`: `TileType` (18 values — the 17 road tiles plus the roadless [`GreenSpace`](#green-space-tile-539) added by [#539](https://github.com/derekwinters/lucas-doggiehood/issues/539)), `TileTypeDefinition`/`TileCatalog` (road edges and, for the OpposingTurns types, arcs), `TileCoordinate`/`TileMap` (placement + adjacency), `TileGeometry` (world-space derivation), and `TileLotCatalog` (per-type lot slots for the 16 lot-bearing types — every type except `FourWay` and `GreenSpace`). The starting `FourWay` intersection still uses its own hand-placed [`NeighborhoodLayout`](world.md) rather than this generic system — but its road arms now reach the **tile edge** (`NeighborhoodLayout.StreetHalfLength` = `WorldDimensions.TileSize / 2` = 30m, no longer the old hand-picked 26m), so when expansion places a neighbouring tile the two tiles' road arms meet edge-to-edge and the street network reads as continuous ([#392](https://github.com/derekwinters/lucas-doggiehood/issues/392); the kit-art corridor compresses a whole number of road tiles to fit each arm exactly, à la `WalkwayTiling` — see [Sidewalks & Walk Network](sidewalks.md#road-arm-extent)). Zone unlock/house-building on top of this geometry (v0.4's #55/#56/#57) is still future work — this issue only builds the geometry itself; procedural tile selection stays explicitly out of scope, per [Neighborhood Expansion](../expansion.md#map-shape). Sidewalks, crosswalks, and the walk network graph are implemented separately ([#106](https://github.com/derekwinters/lucas-doggiehood/issues/106)) — see [Sidewalks & Walk Network](sidewalks.md) — using a generic, data-driven graph rather than this tile/adjacency system.

## Standard dimensions

These measurements are locked in Core (`WorldDimensions`, [#105](https://github.com/derekwinters/lucas-doggiehood/issues/105); the road-bend corner radius added by [#581](https://github.com/derekwinters/lucas-doggiehood/issues/581)) as the geometric basis every tile type below will eventually be built from.

| Standard | Value |
|---|---|
| Tile size | 60m x 60m |
| Road width | 6m |
| Grass verge (road edge -> sidewalk) | 0.75m — Derek's 2026-07-13 midpoint decision (in conversation; originally 1.5m, briefly 0m the same day): a logical setback that puts the dogs' walk line at 4.75m from the road centerline, within the City Kit road tiles' paved band ([#121](https://github.com/derekwinters/lucas-doggiehood/issues/121)/[#122](https://github.com/derekwinters/lucas-doggiehood/issues/122)); no visual grass strip in the kit-art path — see [Sidewalks & Walk Network](sidewalks.md) |
| Sidewalk width | 2m |
| Crosswalk width | 3m |
| Cul-de-sac bulb radius | 9m |
| Opposing-turn arch radius | quarter-circle, 15m (peak of arch reaches ~15m from tile center) |
| Road-bend corner radius | 5m — the plain `Turn*`/`road-bend` road centerline's corner-arc radius, measured from the shared `road-bend` kit FBX (raw radius 50 at the raw×0.1 world scale, midline of the inner 20-raw and outer 80-raw asphalt-edge arcs). The walk network curves a bend's sidewalks concentrically about it ([#581](https://github.com/derekwinters/lucas-doggiehood/issues/581)); see [Sidewalks & Walk Network](sidewalks.md) |

## Grid coordinates

The world is an integer tile grid addressed as **`(x, y)`** — `x` runs east/west, `y` runs north/south. In Core these map to `TileCoordinate.Col` (`x`) and `TileCoordinate.Row` (`y`); world-space is derived by `TileGeometry` (`Col → +X`, `Row → +Z`). The starting `FourWay` intersection is the origin **`(0, 0)`**; the tile directly north of it is **`(0, 1)`**, directly east is **`(1, 0)`**.

A tile's identity is its integer `(x, y)`; a type name's compass suffix (e.g. `CulDeSacEast`) describes *which edge carries the road*, **not** where the tile sits on the map. Position comes from the coordinate, connectivity from the type/code below — keeping the two separate avoids the "is that northwest or is that an east-facing cul-de-sac?" confusion.

## The 17 tile types

Each tile is a 60m x 60m square with roads entering/exiting along some subset of its N/S/E/W edges. `FourWay` is the existing starting tile ([#7](https://github.com/derekwinters/lucas-doggiehood/issues/7), [#38](https://github.com/derekwinters/lucas-doggiehood/issues/38)); the next 16 are built for the multi-tile grid ([#109](https://github.com/derekwinters/lucas-doggiehood/issues/109)). An 18th roadless [`GreenSpace`](#green-space-tile-539) tile ([#539](https://github.com/derekwinters/lucas-doggiehood/issues/539)) is listed at the end of the table below and detailed in [its own subsection](#green-space-tile-539).

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
| `GreenSpace` | `----.` | *(none)* | `▦` a roadless full-tile park/open-grass tile — see [Green-space tile](#green-space-tile-539) below |

### Green-space tile ([#539](https://github.com/derekwinters/lucas-doggiehood/issues/539))

`GreenSpace` is the 18th type: a full grid tile the same size and shape as the other 17, but carrying **no road on any edge** (empty `RoadEdges`, no arcs) and **no buildable lot** (`TileLotCatalog.LotsFor(GreenSpace)` = zero lots — no houses, ever). It is authored into the target map (`docs/tools/map-data.json`) exactly like any other type — `{x, y, type: "GreenSpace"}` — and its empty road-edge set means it validates through `TileMap.CanPlace` only against neighbours whose shared edge is *also* roadless.

Distinct from **unbuilt-quadrant green space** (the open-grass fill *inside* a placed road tile, described under [Property lots per tile](#property-lots-per-tile) — a static property of a tile's own type). This `GreenSpace` *tile type* turns an entire tile on, and unlike the paid, road-connected [expansion frontier](../expansion.md#green-space-tiles-auto-activated) it **auto-activates for free** — no coin cost, no lock icon — the moment 2+ of its 4 edges border an already-activated tile (a placed road tile *or* an already-activated green space), cascading unbounded to a fixpoint. Because it carries no road it can never satisfy `TileMap.HasRoadConnectionAt`, so it is structurally excluded from the frontier and never shows a lock icon. See [Neighborhood Expansion → Green-space tiles](../expansion.md#green-space-tiles-auto-activated) for the activation/persistence detail.

### Tile codes — connectivity as a single source

The **Code** is a fixed-width connectivity token: slots 1–4 are the N/S/E/W edges (letter = road on that edge, `-` = none) and slot 5 is the junction tag — `.` for an ordinary single junction or dead-end, and `\` / `/` for the two `OpposingTurns` tiles, where the slash marks the wall between the tile's two disconnected arcs: `\` separates {N,E} from {S,W} (so the arcs are NE + SW = `OpposingTurnsNS`), `/` separates {N,W} from {S,E} (NW + SE = `OpposingTurnsEW`).

Because the first four slots *are* the road-edge data, adjacency is a pure slot comparison — a tile's East slot must agree with its east-neighbor's West slot (road meets road, or gap meets gap), while slot 5 never affects adjacency, only the tile's internal routing. The code is intended as the **single source** the Core `TileCatalog`, this table, and map-authoring all read, so the road-edge data can't drift between code and docs — see [#359](https://github.com/derekwinters/lucas-doggiehood/issues/359) for the Core unification that removes today's duplication (edges hand-listed in `TileCatalog.BuildDefinitions()` *and* re-typed in this table).

Maps are drawn and validated with the [Map Builder](../../tools/index.md) tool, which reads these codes.

### Road tile art — kit meshes per type

Each junction/terminus type renders a **single Kenney City Kit Roads mesh at the tile centre**, plus tiled `road-straight` arms reaching out to each road edge. `RoadTileArt` (Core) resolves the `TileType` → `(Resources key, yaw, bakes-crosswalks)` mapping; `WorldBuilder` places the mesh and yaws it so its **imported** orientation lines up with the tile's declared edges ([#508](https://github.com/derekwinters/lucas-doggiehood/issues/508)). The 0°-yaw orientation of each staged 1×1-unit piece (→10×10m at `RoadTileScale`) is the mesh as Unity **imports** it — which matters because Unity's FBX import applies a handedness (X-axis) mirror. That mirror is invisible on the symmetric pieces (4-way, straight) and on the E/W-symmetric Tee, but it flips the two chirally-asymmetric pieces: the cul-de-sac round end and the bend. #508 read the yaws from the un-imported kit OBJ/FBX source pose instead, so both of those shipped 180°/mirrored off ([#514](https://github.com/derekwinters/lucas-doggiehood/issues/514) fixed the cul-de-sac; [#515](https://github.com/derekwinters/lucas-doggiehood/issues/515) the bend). The empirical mesh-geometry guard that pins each yaw against the real imported vertices lives in the EditMode `WorldKitArtTests` (it needs `UnityEngine.Mesh`); the Core `RoadTileArtTests` stays a pure data-table pin.

| Type(s) | Kit mesh | Baked crosswalks | Authored (0°) orientation |
|---|---|---|---|
| `FourWay` | `road-crossroad-path` | yes (4 arms) | symmetric |
| `TeeNorth/East/South/West` | `road-intersection-path` | yes (3 arms) | omits the SOUTH arm = `TeeNorth`; others are 90°/180°/270° |
| `TurnNW/NE/SE/SW` | `road-bend` | no | raw kit connects NORTH+WEST, but the FBX-import X-mirror (W↔E) flips it, so the imported bend connects NORTH+EAST = `TurnNE` at 0-yaw; the mirror also reverses rotation sense, so the four yaws are derived per-arm (`TurnNE` 0°, `TurnSE` 90°, `TurnSW` 180°, `TurnNW` 270°), not a uniform offset ([#515](https://github.com/derekwinters/lucas-doggiehood/issues/515)). Rounded corner (Derek's locked call; `road-bend-square` is a one-line swap) |
| `CulDeSacEast/South/West/North` | `road-end-round` | no | imported open road exits WEST at 0-yaw (the raw kit source exits +X/EAST, mirrored on FBX import), so `CulDeSacEast` takes a half-turn to bring the open road to its EAST edge; rounded bulb caps the other side ([#514](https://github.com/derekwinters/lucas-doggiehood/issues/514)) |
| `StraightNS`, `StraightEW` | *(none — tiled `road-straight` arms)* | n/a | — |
| `GreenSpace` | *(none — no road art)* | n/a | a roadless park tile: no centre mesh, no arms, no crosswalks. Its only visible effect is the grass ground plane growing to cover it (`WorldBuilder.ResizeGroundToMap`); the road-art and lot-building passes iterate the player-unlocked road tiles, which never include a green space, so it no-ops gracefully ([#539](https://github.com/derekwinters/lucas-doggiehood/issues/539)) |
| `OpposingTurnsNS/EW` | *(none — not kit-renderable)* | n/a | would need two independent bends composed in one tile with no central crossing; the `road-curve` family that could sweep a wide double-arc is a 2×2-unit piece that doesn't fit the single-tile grid, so there is no clean kit build. **The live map no longer places one** ([#516](https://github.com/derekwinters/lucas-doggiehood/issues/516) swapped the sole `OpposingTurnsNS` at `(6,-1)` to `FourWay`), and the [Map Builder](../../tools/index.md) now greys these two "Twin bends" out as not-buildable so a future author can't place one back. The types stay **defined** in Core (still modeled, simply unused on the live map) rather than retired. |

Crosswalks are baked into the 4-way/Tee meshes, so the kit path needs no separate crossing tiles; the primitive graybox fallback derives one crosswalk patch per intersection arm from `TileCrosswalkGeometry` instead — see [Sidewalks & Walk Network](sidewalks.md#the-crosswalk-box).

## Property lots per tile

*Design decisions 2026-07-30 / 2026-07-31 (Derek & Lucas), captured from the [Map Builder](../../tools/index.md). The Core lot rules below are implemented in `TileLotCatalog.LotsFor`; each bend's curved/cupped corner is exposed via `TileLotCatalog.TryGetCuppedCorner`, and a cul-de-sac's dropped bulb-side (tree) quadrants via `TileLotCatalog.TreeQuadrantsFor` ([#383](https://github.com/derekwinters/lucas-doggiehood/issues/383), refined by [#385](https://github.com/derekwinters/lucas-doggiehood/issues/385)).*

Each tile offers up to four **property lots**, one per quadrant (NE/NW/SE/SW). Not every quadrant holds a house — lot assignment is per tile type, and unbuilt quadrants become green space (parks/water in the open areas are future content, see [Neighborhood Expansion](../expansion.md)). This *unbuilt-quadrant* green space is a static property of a road tile's own type — distinct from the whole-tile [`GreenSpace` tile type](#green-space-tile-539) ([#539](https://github.com/derekwinters/lucas-doggiehood/issues/539)), which turns an entire tile on:

- **Twin bends (`OpposingTurnsNS`/`OpposingTurnsEW`): no lots.** Their two arcs leave no clean buildable quadrant.
- **Bends (`Turn*`): two lots — drop the small corner the curve cups AND its diagonal opposite.** The cupped corner is the bend's own corner (`TurnNE` drops NE, `TurnSW` drops SW, etc.); the corner diagonally opposite it borders neither roaded edge, so it can never face a road and is dropped too. The two kept lots each border a straight roaded edge square-on: `TurnNE`→NW,SE · `TurnNW`→NE,SW · `TurnSE`→NE,SW · `TurnSW`→NW,SE. A bend renders as a **curved corner**, not two straight bands meeting at a right angle.
- **Cul-de-sacs (`CulDeSac*`): two lots — keep the two quadrants adjacent to the single roaded edge.** `CulDeSacNorth`→NE,NW · `CulDeSacSouth`→SE,SW · `CulDeSacEast`→NE,SE · `CulDeSacWest`→NW,SW. The two bulb-side quadrants become **open space with trees** (reusing the #170 tree environment art, rendered by `WorldBuilder`). The two **kept** quadrants get houses, so their procedural [yard landscaping](world.md#yard-landscaping) must exclude the tile's road: the kept quadrant faces the tile's single road arm, and a lot on this non-origin tile is only trimmed against that road once the yard clip is made **tile-aware** — `LotBounds.RoadsFor(lot, tileType)` converts the tile's `TileRoadGeometry` arm to a `Road` alongside the origin's streets ([#455](https://github.com/derekwinters/lucas-doggiehood/issues/455), after a playtest showed yard trees landing in the first unlocked cul-de-sac's street).
- **Green-space tile (`GreenSpace`, [#539](https://github.com/derekwinters/lucas-doggiehood/issues/539)): no lots** — a park/open-grass tile that never holds a house (`TileLotCatalog.LotsFor(GreenSpace)` returns an empty set). Not to be confused with the unbuilt-quadrant green space *inside* a road tile above; see [Green-space tile](#green-space-tile-539).
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
