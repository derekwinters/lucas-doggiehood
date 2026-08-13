# Tile Catalog (Design Reference)

*Related: [#105](https://github.com/derekwinters/lucas-doggiehood/issues/105) (this page), [#106](https://github.com/derekwinters/lucas-doggiehood/issues/106) (sidewalks and the walk network, implemented — see [Sidewalks & Walk Network](sidewalks.md)), [#109](https://github.com/derekwinters/lucas-doggiehood/issues/109) (multi-tile grid/placement system, implemented), [#86](https://github.com/derekwinters/lucas-doggiehood/issues/86) (map-shape question, resolved 2026-07-14 — see [Neighborhood Expansion](../expansion.md#map-shape))*

!!! note "Status: implemented in Core ([#109](https://github.com/derekwinters/lucas-doggiehood/issues/109))"
    The catalog, grid-coordinate tile map, placement/adjacency validation, and per-type property-lot definitions are built in `Doggiehood.Core.World`: `TileType` (16 values — the 15 road tiles plus the roadless [`GreenSpace`](#green-space-tile-539) added by [#539](https://github.com/derekwinters/lucas-doggiehood/issues/539); the two `OpposingTurns` "twin bends" were removed outright by [#583](https://github.com/derekwinters/lucas-doggiehood/issues/583)), `TileTypeDefinition`/`TileCatalog` (road edges), `TileCoordinate`/`TileMap` (placement + adjacency), `TileGeometry` (world-space derivation), and `TileLotCatalog` (per-type lot slots for the 15 lot-bearing types — every type except the roadless `GreenSpace`). The starting `FourWay` intersection still uses its own hand-placed [`NeighborhoodLayout`](world.md) rather than this generic system — but its road arms now reach the **tile edge** (`NeighborhoodLayout.StreetHalfLength` = `WorldDimensions.TileSize / 2` = 30m, no longer the old hand-picked 26m), so when expansion places a neighbouring tile the two tiles' road arms meet edge-to-edge and the street network reads as continuous ([#392](https://github.com/derekwinters/lucas-doggiehood/issues/392); the kit-art corridor compresses a whole number of road tiles to fit each arm exactly, à la `WalkwayTiling` — see [Sidewalks & Walk Network](sidewalks.md#road-arm-extent)). Zone unlock/house-building on top of this geometry (v0.4's #55/#56/#57) is still future work — this issue only builds the geometry itself; procedural tile selection stays explicitly out of scope, per [Neighborhood Expansion](../expansion.md#map-shape). Sidewalks, crosswalks, and the walk network graph are implemented separately ([#106](https://github.com/derekwinters/lucas-doggiehood/issues/106)) — see [Sidewalks & Walk Network](sidewalks.md) — using a generic, data-driven graph rather than this tile/adjacency system.

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
| Road-bend corner radius | 5m — the plain `Turn*`/`road-bend` road centerline's corner-arc radius, measured from the shared `road-bend` kit FBX (raw radius 50 at the raw×0.1 world scale, midline of the inner 20-raw and outer 80-raw asphalt-edge arcs). The walk network curves a bend's sidewalks concentrically about it ([#581](https://github.com/derekwinters/lucas-doggiehood/issues/581)); see [Sidewalks & Walk Network](sidewalks.md) |

The **lane offset** (`1.5m`, the distance from a road centerline to a lane's centre, [#672](https://github.com/derekwinters/lucas-doggiehood/issues/672)) is **not** a locked dimension — it is *derived* as `RoadWidth / 4` in `RoadLane.Offset`, so widening the road carries the lanes with it and there is nothing new to keep in sync. See [Sidewalks & Walk Network § Lanes](sidewalks.md#lanes-672).

## Grid coordinates

The world is an integer tile grid addressed as **`(x, y)`** — `x` runs east/west, `y` runs north/south. In Core these map to `TileCoordinate.Col` (`x`) and `TileCoordinate.Row` (`y`); world-space is derived by `TileGeometry` (`Col → +X`, `Row → +Z`). The starting `FourWay` intersection is the origin **`(0, 0)`**; the tile directly north of it is **`(0, 1)`**, directly east is **`(1, 0)`**.

A tile's identity is its integer `(x, y)`; a type name's compass suffix (e.g. `CulDeSacEast`) describes *which edge carries the road*, **not** where the tile sits on the map. Position comes from the coordinate, connectivity from the type/code below — keeping the two separate avoids the "is that northwest or is that an east-facing cul-de-sac?" confusion.

## The 15 tile types

Each tile is a 60m x 60m square with roads entering/exiting along some subset of its N/S/E/W edges. `FourWay` is the existing starting tile ([#7](https://github.com/derekwinters/lucas-doggiehood/issues/7), [#38](https://github.com/derekwinters/lucas-doggiehood/issues/38)); the next 14 are built for the multi-tile grid ([#109](https://github.com/derekwinters/lucas-doggiehood/issues/109)). A 16th roadless [`GreenSpace`](#green-space-tile-539) tile ([#539](https://github.com/derekwinters/lucas-doggiehood/issues/539)) is listed at the end of the table below and detailed in [its own subsection](#green-space-tile-539). Two further `OpposingTurns` "twin bend" types were part of this catalog until [#583](https://github.com/derekwinters/lucas-doggiehood/issues/583) removed them — see [Removed: the opposing-turn twin bends](#removed-the-opposing-turn-twin-bends-583).

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
| `GreenSpace` | `----.` | *(none)* | `▦` a roadless full-tile park/open-grass tile — see [Green-space tile](#green-space-tile-539) below |

### Green-space tile ([#539](https://github.com/derekwinters/lucas-doggiehood/issues/539))

`GreenSpace` is the 16th type: a full grid tile the same size and shape as the other 15, but carrying **no road on any edge** (empty `RoadEdges`) and **no buildable lot** (`TileLotCatalog.LotsFor(GreenSpace)` = zero lots — no houses, ever). It is authored into the target map (`docs/tools/map-data.json`) exactly like any other type — `{x, y, type: "GreenSpace"}` — and its empty road-edge set means it validates through `TileMap.CanPlace` only against neighbours whose shared edge is *also* roadless.

Distinct from **unbuilt-quadrant green space** (the open-grass fill *inside* a placed road tile, described under [Property lots per tile](#property-lots-per-tile) — a static property of a tile's own type). This `GreenSpace` *tile type* turns an entire tile on, and unlike the paid, road-connected [expansion frontier](../expansion.md#green-space-tiles-auto-activated) it **auto-activates for free** — no coin cost, no lock icon — the moment 2+ of its 4 edges border an already-activated tile (a placed road tile *or* an already-activated green space), cascading unbounded to a fixpoint. Because it carries no road it can never satisfy `TileMap.HasRoadConnectionAt`, so it is structurally excluded from the frontier and never shows a lock icon. See [Neighborhood Expansion → Green-space tiles](../expansion.md#green-space-tiles-auto-activated) for the activation/persistence detail.

### Tile codes — connectivity as a single source

The **Code** is a fixed-width connectivity token: slots 1–4 are the N/S/E/W edges (letter = road on that edge, `-` = none) and slot 5 is the junction tag, `.` for every current type. The tag once distinguished the two `OpposingTurns` tiles' disconnected arcs (`\` / `/`); those types were removed in [#583](https://github.com/derekwinters/lucas-doggiehood/issues/583), so the slot now carries no information and is kept only so previously authored codes still parse.

Because the first four slots *are* the road-edge data, adjacency is a pure slot comparison — a tile's East slot must agree with its east-neighbor's West slot (road meets road, or gap meets gap), while slot 5 never affects adjacency. The code is intended as the **single source** the Core `TileCatalog`, this table, and map-authoring all read, so the road-edge data can't drift between code and docs — see [#359](https://github.com/derekwinters/lucas-doggiehood/issues/359) for the Core unification that removes today's duplication (edges hand-listed in `TileCatalog.BuildDefinitions()` *and* re-typed in this table).

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

Crosswalks are baked into the 4-way/Tee meshes, so the kit path needs no separate crossing tiles; the primitive graybox fallback derives one crosswalk patch per intersection arm from `TileCrosswalkGeometry` instead — see [Sidewalks & Walk Network](sidewalks.md#the-crosswalk-box).

## Property lots per tile

*Design decisions 2026-07-30 / 2026-07-31 (Derek & Lucas), captured from the [Map Builder](../../tools/index.md). The Core lot rules below are implemented in `TileLotCatalog.LotsFor`; each bend's curved/cupped corner is exposed via `TileLotCatalog.TryGetCuppedCorner`, and every dropped (no-lot) quadrant's tree slots via `TileLotCatalog.TreeQuadrantsFor` ([#383](https://github.com/derekwinters/lucas-doggiehood/issues/383), refined by [#385](https://github.com/derekwinters/lucas-doggiehood/issues/385) and generalized to all tile types by [#614](https://github.com/derekwinters/lucas-doggiehood/issues/614)).*

Each tile offers up to four **property lots**, one per quadrant (NE/NW/SE/SW). Not every quadrant holds a house — lot assignment is per tile type, and **every quadrant with no kept house lot becomes open space with trees** (reusing the #170 tree environment art; [#614](https://github.com/derekwinters/lucas-doggiehood/issues/614)). The tree quadrants are derived as "the tile's four quadrants minus its `LotsFor` quadrants", so trees and lots share one source of truth and can never disagree — the sole exception is the whole-tile [`GreenSpace` tile type](#green-space-tile-539) ([#539](https://github.com/derekwinters/lucas-doggiehood/issues/539)), a separate park tile that stays bare (it has no lots, but a naive "no lot ⇒ trees" rule is explicitly *not* applied to it). This *unbuilt-quadrant* open space with trees is a static property of a road tile's own type — distinct from that whole-tile `GreenSpace`, which turns an entire tile on:

- **Bends (`Turn*`): two lots — drop the small corner the curve cups AND its diagonal opposite.** The cupped corner is the bend's own corner (`TurnNE` drops NE, `TurnSW` drops SW, etc.); the corner diagonally opposite it borders neither roaded edge, so it can never face a road and is dropped too. The two kept lots each border a straight roaded edge square-on: `TurnNE`→NW,SE · `TurnNW`→NE,SW · `TurnSE`→NE,SW · `TurnSW`→NW,SE. Both **dropped** quadrants become **open space with trees**; the cupped corner's tree is kept clear of the bend's road arc via the tile-aware road clearance (`LotBounds.RoadsFor`/`ClearRoadCorridors`, [#455](https://github.com/derekwinters/lucas-doggiehood/issues/455)/[#581](https://github.com/derekwinters/lucas-doggiehood/issues/581)) and any pick with no clean grass to stand on is skipped rather than force-placed, while the diagonal-opposite quadrant (bordering no roaded edge) keeps its full cluster. Each dropped quadrant is planted with 2–4 spaced trees, not one ([#700](https://github.com/derekwinters/lucas-doggiehood/issues/700) — see [How an open-space quadrant is planted](#how-an-open-space-quadrant-is-planted-700)). A bend renders as a **curved corner**, not two straight bands meeting at a right angle.
- **Cul-de-sacs (`CulDeSac*`): two lots — keep the two quadrants adjacent to the single roaded edge.** `CulDeSacNorth`→NE,NW · `CulDeSacSouth`→SE,SW · `CulDeSacEast`→NE,SE · `CulDeSacWest`→NW,SW. The two bulb-side quadrants become **open space with trees** (reusing the #170 tree environment art, rendered by `WorldBuilder`) — a 2–4 tree cluster each, kept clear of the paved bulb at the tile centre ([#700](https://github.com/derekwinters/lucas-doggiehood/issues/700)). The two **kept** quadrants get houses, so their procedural [yard landscaping](world.md#yard-landscaping) must exclude the tile's road: the kept quadrant faces the tile's single road arm, and a lot on this non-origin tile is only trimmed against that road once the yard clip is made **tile-aware** — `LotBounds.RoadsFor(lot, tileType)` converts the tile's `TileRoadGeometry` arm to a `Road` alongside the origin's streets ([#455](https://github.com/derekwinters/lucas-doggiehood/issues/455), after a playtest showed yard trees landing in the first unlocked cul-de-sac's street).
- **Green-space tile (`GreenSpace`, [#539](https://github.com/derekwinters/lucas-doggiehood/issues/539)): no lots** — a park/open-grass tile that never holds a house (`TileLotCatalog.LotsFor(GreenSpace)` returns an empty set). Not to be confused with the unbuilt-quadrant green space *inside* a road tile above; see [Green-space tile](#green-space-tile-539).
- **All other types** (`FourWay`, `Straight*`, `Tee*`): all four quadrant lots. **A `FourWay` is a full intersection — all four quadrants border a straight roaded edge square-on — so it carries one buildable lot per quadrant *wherever it appears on the map, not only at the origin* ([#607](https://github.com/derekwinters/lucas-doggiehood/issues/607)).** `TileLotCatalog.LotsFor(FourWay)` returns the four quadrant slots like any other full-lot type; the **origin** FourWay is the one exception where those lots are *not* catalog-generated — its four houses are seeded from `NeighborhoodLayout.HouseLots` (ids 1–4), so `GameState.LotsForUnlockedTile` returns an empty set for the origin coordinate to avoid double-counting them, while every non-origin FourWay gets its four `TileLotCatalog` lots with stable `FrontierHouseId.For(coordinate, quadrant)` ids. (Before #607 a non-origin FourWay unlocked with **zero** lots — nowhere to build a house.)

### How an open-space quadrant is planted ([#700](https://github.com/derekwinters/lucas-doggiehood/issues/700))

An open-space quadrant is a whole 30×30m patch of grass, so it is planted with a small **cluster** of trees, not a single one. Each quadrant draws **2–4 trees** (`YardLandscaping.OpenSpaceSelectMin`/`OpenSpaceSelectMax`) from the *same* rejection-sampling machinery a house yard uses (`YardLandscaping.GenerateOpenSpaceCandidates`/`SelectOpenSpace`): candidates are scattered across the quadrant's road-cleared grass (`TileGeometry.OpenSpaceGrassFor`), spaced at least `YardLandscaping.MinSpacing` apart so canopies never overlap, and each pick carries its own model (`tree-large`/`tree-small`) and the [#458](https://github.com/derekwinters/lucas-doggiehood/issues/458) per-tree size variance (`[1.0, 1.25]` — never smaller than the baseline). The whole cluster is seeded deterministically from `(tile coordinate, quadrant)` — the same injective key `FrontierHouseId.For` builds — so a tile renders identically across sessions, saves and rebuilds, exactly as a lot's yard trees are seeded from its house id.

**Invariant — an open-space quadrant is planted with at least two spaced trees, never one.** A lone tree in a 30m quadrant reads as bare grass next to a landscaped cul-de-sac (Derek & Lucas, v0.14 playtest), so "one tree per dropped quadrant" is ruled out by the spec, not just by the current code. Pinned across every tile type in `OpenSpaceTreesTests`.

**Invariant — an open-space tree never stands on pavement.** Every pick clears the tile's road corridors (road + verge + sidewalk, `LotBounds.StreetCorridorInset`) *and* a dead-end tile's **bulb** at the tile centre (`WorldDimensions.CulDeSacBulbRadius`) — the paved turnaround the per-edge corridor trim cannot see, because a cul-de-sac stub's road extent stops at the tile centre while its pavement keeps going. A quadrant the roads leave no clean grass in is left empty rather than force-planted.

**Invariant — an unlocked lot shows its predetermined house's yard trees before *and* after its house is built.** Trees belong to the lot, not to the house standing on it: they are rolled from the predetermined house style at unlock ([#434](https://github.com/derekwinters/lucas-doggiehood/issues/434)/[#461](https://github.com/derekwinters/lucas-doggiehood/issues/461)) and re-rendered on every world build for every lot of an unlocked tile, built or not (`WorldBuilder.BuildEmptyLots`). This is what keeps a full-lot tile — a `Tee*` 3-way, a `Straight*`, a non-origin `FourWay`, none of which has any open-space quadrant — from reading as an empty field. See [Neighborhood Expansion](../expansion.md).

> **How the spec is changing ([#700](https://github.com/derekwinters/lucas-doggiehood/issues/700)).** It used to say a dropped quadrant gets *a tree* — one, at that quadrant's fixed 14m corner offset, at the flat baseline size → it now says a dropped quadrant is **planted with a cluster of 2–4 spaced, size-varied trees scattered over its clean grass**, seeded per tile-coordinate-and-quadrant → because in playtesting a single tree in a 30×30m lot read as bare ground, and the reason open-space trees were excluded from #458's size variance ("no lot/seed context to draw from") disappears once each quadrant has its own seed. The 3-way (`Tee*`) half of the same report is *not* an open-space case at all — a 3-way's four quadrants are house lots — so instead of inventing open-space trees for it, the spec now states the standing rule out loud: an unlocked lot shows its predetermined yard trees before and after its house is built.

**House facing — settled (2026-07-31, Derek): remove, no rotation.** On bends and cul-de-sacs the road curves, so a corner house can't always face it square-on. Rather than rotate houses to fan around a curve, the lots that can't face a road square-on are simply **removed** (they become open space with trees, [#614](https://github.com/derekwinters/lucas-doggiehood/issues/614)) — which is exactly why bends and cul-de-sacs keep only two lots above. Every remaining lot already borders a straight roaded edge square-on, so no house ever carries a facing/rotation value.

## Removed: the opposing-turn twin bends ([#583](https://github.com/derekwinters/lucas-doggiehood/issues/583))

*Decision (2026-08-07, Derek, on [#583](https://github.com/derekwinters/lucas-doggiehood/issues/583)): **"remove tile completely"** — superseding the earlier "keep them defined-but-unused in Core" state that [#516](https://github.com/derekwinters/lucas-doggiehood/issues/516)/[#508](https://github.com/derekwinters/lucas-doggiehood/issues/508) left behind.*

The catalog once carried two more types — `OpposingTurnsNS` and `OpposingTurnsEW`, the "twin bends": a tile with two *independent, unconnected* corner arcs touching all four edges (NE + SW for `OpposingTurnsNS`, NW + SE for its 90° rotation). They were never kit-renderable — two bends in one tile with no central crossing has no City Kit mesh, and the `road-curve` family that could sweep a wide double-arc is a 2×2-unit piece that doesn't fit the single-tile grid. #516 had already swapped the sole authored `OpposingTurnsNS` at `(6,-1)` to `FourWay` and greyed the pair out in the [Map Builder](../../tools/index.md), leaving them defined-but-unused.

#583 removed them outright: the `TileType` values, their `TileCatalog` entries, the `TileArc`/`TileTypeDefinition.Arcs`/`EdgesConnectedVia` arc machinery that existed only to model them, their no-lots rule, the 15m `OpposingTurnArchRadius` standard dimension, and the Map Builder's greyed-out "Twin bends" palette group. Nothing on the live map or in any save referenced them (`TileType` serializes by name), so the removal is behavior-neutral. The remaining tile set is pinned by name in `TileCatalogTests.Types_ExposesExactlyTheDefinedCatalogTypes`, so they can't reappear silently.

<details>
<summary>Design history of the removed types, kept for the record</summary>

**Resolved 2026-07-18 by Derek on [#109](https://github.com/derekwinters/lucas-doggiehood/issues/109), overriding the earlier #105 "loop/island" framing:**

> The two turns would not connect. Each arc would connect two adjacent sides only. There would be two distinct, unconnected arcs.

So each arch was a **turn** in exactly the same sense as the `TurnNE`/`TurnNW`/`TurnSE`/`TurnSW` tiles above: it joined two *adjacent* (corner) edges, not the two *opposite* edges the original framing assumed. Between them the two arcs touched all four edges, but there was no path from one arc into the other — no loop, no shared connection point.

The **original (superseded) #105 framing** was that each arch is "a curved road, an arch, likely a quarter circle radius" — implying the two arches join into a continuous loop around a central island, using a 15m `OpposingTurnArchRadius`, with both arches connecting the tile's two *opposite* edges (E/W for `OpposingTurnsNS`, N/S for `OpposingTurnsEW`).

</details>
