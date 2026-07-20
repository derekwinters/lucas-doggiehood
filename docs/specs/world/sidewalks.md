# Sidewalks & Walk Network

*Related: [#106](https://github.com/derekwinters/lucas-doggiehood/issues/106) (this page), [#105](https://github.com/derekwinters/lucas-doggiehood/issues/105) (locked world dimensions this page consumes), [#128](https://github.com/derekwinters/lucas-doggiehood/issues/128) (front walkways, which replaced the driveway stubs on this page), [#109](https://github.com/derekwinters/lucas-doggiehood/issues/109) (deferred multi-tile grid, out of scope here), [#112](https://github.com/derekwinters/lucas-doggiehood/issues/112) (deferred approach-to-rest movement, out of scope here)*

## Symmetric sidewalk placement

Every road gets a sidewalk on **both** sides — not just one edge. Moving outward from the road centerline: road → verge setback → sidewalk, mirrored on the other side, with grass outside the sidewalk. The widths are the locked [standard dimensions](tile-catalog.md#standard-dimensions) (`WorldDimensions`, [#105](https://github.com/derekwinters/lucas-doggiehood/issues/105)) — nothing on this page introduces a new measurement:

| Standard | Value |
|---|---|
| Road width | 6m |
| Grass verge (road edge → sidewalk) | 0.75m — a logical setback, see below |
| Sidewalk width | 2m |

> **Decision (2026-07-13, Derek, in conversation):** the grass verge between road and sidewalk no longer exists — `GrassVergeWidth` went from `1.5m` to `0m`. This supersedes the original [#106](https://github.com/derekwinters/lucas-doggiehood/issues/106) verge. Context: aligning Core's walk geometry with the Kenney City Kit Roads art ([#121](https://github.com/derekwinters/lucas-doggiehood/issues/121)/[#122](https://github.com/derekwinters/lucas-doggiehood/issues/122)). At tile scale 10, a kit road tile has road surface 0–3m from the centerline and a raised curb+sidewalk band spanning 3–5m; with a 0m verge, Core's sidewalk centerline lands at exactly 4m — dogs walk centered on the kit's modeled pavement, and the crosswalk edges (±4m) land inside the crossroad tile's own corner sidewalks (4–5m). The constant stays in `WorldDimensions` (and in the offset formula) so the geometry keeps deriving from one place.

> **Decision (2026-07-13, Derek, in conversation — follow-up to the above, same day):** `GrassVergeWidth` is now `0.75m`. After seeing the kit world in the Editor, Derek found the dogs at 4m "a little too close to the road" and asked for the midpoint between the previous distance (5.5m at the 1.5m verge) and the current one (4m at the 0m verge) — putting the dogs' walk line at `4.75m`. This is a **logical setback for dog placement**, not a return of the visual grass strip: the kit tiles pave 3–5m from the centerline, so in the kit-art path nothing visually changes and the earlier "no grass verge" decision (a visual call) stands — 4.75m sits within the paved band, near its outer edge. Only `WorldBuilder`'s primitive graybox fallback renders the 0.75m verge as an actual grass strip.

A sidewalk's centerline sits `RoadWidth / 2 + GrassVergeWidth + SidewalkWidth / 2` from the road's own centerline — `4.75m` for the starting intersection's two roads. `Road` and `Sidewalk` (Core, `Assets/Scripts/Core/World/`) express this purely as that formula over `WorldDimensions`; a guard test (`WorldDimensionsGuardTests`) fails the build if any of the locked values is ever re-declared as a literal outside `WorldDimensions.cs`.

## The crosswalk box

At the starting intersection, the two crossing roads produce four box corners (one per compass direction the roads' sidewalks meet). Each of the 4 road arms — N, S, E, W — gets one crosswalk, `CrosswalkWidth` (3m) wide, connecting the two box corners on either side of that arm and letting a pedestrian cross straight over that road instead of going the long way around through both corners.

This is placeholder geometry: crosswalks render as a flat, distinctly colored rectangular patch (no zebra-stripe markings) — see [Art & UI Style](art-style.md) for the palette and [Build checklist](#build-checklist) below for what's actually implemented.

Visually, each crosswalk only covers the road and verge band — `RoadWidth + 2 × GrassVergeWidth` (7.5m at the 0.75m verge) — never the sidewalks themselves, which keep their own sidewalk-colored surface right up to the crosswalk's edge. The walk network's `Crosswalk` edge is still a straight line from one sidewalk's center to the other's (that's the real distance a dog covers crossing the road, and it's what keeps the graph connected to the sidewalk arm nodes) — this is purely a rendering clip in `WorldBuilder`, not a change to the graph.

## The walk network graph

`WalkNetwork` (Core) is a generic, data-driven graph over nodes (points) and edges — sidewalk segments, crosswalks, and front walkways — built from whatever `Road`s and house lots are passed to `WalkNetwork.BuildFrom`. It is **not** a `TileType` enum or a multi-tile adjacency grid — that system is explicitly deferred to [#109](https://github.com/derekwinters/lucas-doggiehood/issues/109) (slated for v0.4). Today it happens to describe exactly the starting tile's one intersection, because that's all `NeighborhoodLayout` produces; the same algorithm would extend to more roads without changes.

> **Decision (2026-07-14, Derek, on [#128](https://github.com/derekwinters/lucas-doggiehood/issues/128)):** the neighborhood has **no driveways** — front walkways *replaced* the `DrivewayStub` edges (`WalkEdgeKind.FrontWalkway`; the enum member was renamed, not duplicated) as each lot's connection to the sidewalk. A walkway runs from the house's actual **front door** — the [#125](https://github.com/derekwinters/lucas-doggiehood/issues/125) catalog door position at the [#127](https://github.com/derekwinters/lucas-doggiehood/issues/127) front-setback house position — perpendicular to the street onto the sidewalk, so dogs path to real front doors. It keeps the stub's two contracts: general wander never enters it, and it is the only way on/off a lot. The lot-side node of the connection is therefore the **door**, not the lot center; house lot centers (±14) remain as data anchoring the deferred expansion geometry, but no walk-network edge touches them anymore.

Construction, generically:

1. **Sidewalk arms.** Every road's two sidewalks are split into arms wherever another road crosses them, so a sidewalk never runs through another road's own footprint.
2. **Crosswalks.** At each crossing, the four box-corner nodes are connected pairwise into the 4-crosswalk box described above.
3. **Front walkways.** Each house lot gets one walkway edge from its front door to its street-facing sidewalk. The sidewalk edge nearest the *lot center* still decides which street the house faces (unchanged from the stub, and still what `HousePlacement.FrontFacing` keys off); from that facing, `WalkNetwork.BuildFrom` derives the house's front-setback position (`HousePlacement`'s pure helpers) and its catalog door (`HouseModel.FrontDoorWorldPosition`), then projects the door perpendicularly onto that sidewalk (clamped to the segment) — splitting the sidewalk edge at the attach point if needed. The per-lot edge is queryable via `WalkNetwork.TryGetFrontWalkway(houseId)` (A = door node, B = sidewalk attach).

`NeighborhoodLayout.WalkNetwork` caches one instance built from `NeighborhoodLayout.Roads` and `NeighborhoodLayout.HouseLots`. The resulting network is fully connected — every node can reach every other node, including every house's front door via its walkway — which a Core test asserts directly (BFS reachability from any starting node).

`WalkNetwork.FindPath` is a small Dijkstra shortest-path over the graph (a priority-queue implementation would be overkill for a graph this size) — used by both wander and walking-home.

## Wander: a node-to-node walk over the network

`WanderBehavior` ([Dog Behavior](../dogs/behavior.md)) no longer does centerline-bounce math on the streets directly. It now random-walks node to node over the sidewalk+crosswalk portion of the network — **front walkways are never entered**, so general wander never crosses onto a house lot, into a yard, or up to a door.

At each node, the choice between continuing straight and deviating/turning is weighted. Two overloads exist: `WanderBehavior.NextTarget(current, continueWeight, deviateWeight)` takes an explicit override, while the parameterless `WanderBehavior.NextTarget(current)` — what every caller (`DogView`) uses today — derives `continueWeight = 1 - TurnProbability` and `deviateWeight = TurnProbability` from the dog's own `MovementProfile.TurnProbability` (#89). A lower `TurnProbability` (Excited) means a higher continue-weight, i.e. longer straight stretches before a turn, so the per-personality distinction from [Dog Behavior](../dogs/behavior.md#movement-conveys-personality) carries all the way through to wander's actual path shape — both `Speed` and turn pacing now differ for Excited dogs, not just Speed.

## Ground height: sidewalks sit above the road

*Related: [#151](https://github.com/derekwinters/lucas-doggiehood/issues/151)*

The Kenney City Kit Roads tiles model the sidewalk band raised a little
above the road surface (a real curb, not just a color change) — placing a
dog at a single fixed world-Y clipped its legs into that raised mesh
whenever it stood on a sidewalk. `WorldDimensions` now locks
`RoadSurfaceHeight` (0m, the road/crosswalk plane) and
`SidewalkSurfaceHeight` (0.2m, measured directly from the shared kit road
tile mesh) alongside the other world dimensions. `WalkNetwork.GroundHeight
(from, to)` resolves which of the two applies to a hop between two
adjacent network nodes from the specific edge connecting them — a Sidewalk
or FrontWalkway edge resolves to the sidewalk height, a Crosswalk edge to
the road height — rather than from either endpoint node alone, since a
box-corner node (where a crosswalk meets its sidewalks) carries edges of
both kinds and only the edge actually being crossed disambiguates them.
`DogView`'s wander step and `DogSpawner`'s sidewalk spawn point both
consume this Core query instead of hardcoding Y.

## Walking home routes over the network

Walking home after accepting a "buy me X" quest ([Quest Content](../quests/quest-content.md)) used to move in a straight line, ignoring streets entirely. Core now computes the actual route with `WalkNetwork.FindPath` from the dog's current position to its house's **front door**, ending via that lot's front walkway ([#128](https://github.com/derekwinters/lucas-doggiehood/issues/128) — the dog arrives at the actual door, which also feeds future quest arrivals); the Unity layer (`QuestDirector`) only consumes the returned waypoint list and walks it frame by frame (`Vector3.MoveTowards` hop to hop) — the Core/Unity split stays intact, per [Testing Strategy](../../engineering/testing.md). Dogs also spawn at their walkway's sidewalk attach point (`DogSpawner`), staggered along the sidewalk so housemates don't overlap.

## Walkway visuals

A front walkway renders as tiled Kenney City Kit Suburban `path-short` pieces — the clean square-paver look, matching the kit sidewalk aesthetic (the kit's stone variants remain an available re-pick for Derek and Lucas). The math lives in Core (`WalkwayTiling`): pieces scale ×10 in width — the same derivation as `WorldBuilder.RoadTileScale`, landing the paver's 0.2 model width exactly on `SidewalkWidth` (2m), which is also the walkway edge's declared width — and along the walkway a whole number of pieces is compressed to cover door → sidewalk exactly (no gap at the door, no overshoot onto the kit road tile's raised sidewalk band). `WorldBuilder` instantiates the pieces verbatim, and falls back to one flat graybox strip when the kit piece can't be loaded — the same primitive-fallback pattern as the roads and houses.

## Explicitly out of scope here

- **Approach-to-rest movement** ([#112](https://github.com/derekwinters/lucas-doggiehood/issues/112)) — `RestBehavior` stays a pure probabilistic state flip with no movement of its own; it doesn't touch the walk network.
- **Multi-tile grid / adjacency system** ([#109](https://github.com/derekwinters/lucas-doggiehood/issues/109), slated for v0.4) — no `TileType` enum exists. `WalkNetwork` is generic enough to extend to it later, but building that system is not part of this page.
- **Literal crosswalk striping** — deferred visual polish; today's crosswalk is one flat-colored patch.

## Build checklist

- [x] Every road declares a sidewalk on both sides, set back 0.75m from the road edge (2026-07-13 midpoint decision — a logical setback for dog placement, no visual grass strip in the kit-art path), sized from `WorldDimensions` only
- [x] The 4-crosswalk box exists at the starting intersection, one crosswalk per road arm
- [x] The walk network graph (sidewalks + crosswalks + front walkways) is generated from `NeighborhoodLayout`'s roads and house lots, and is fully connected
- [x] Each front walkway runs from the house's catalog door position to its street-facing sidewalk, perpendicular to the street (#128)
- [x] `WanderBehavior` only ever produces positions on the sidewalk/crosswalk network — never a road surface, never a front walkway
- [x] `WanderBehavior`'s node choice supports weighted continue-vs-deviate decisions, defaulting to a per-personality split derived from `MovementProfile.TurnProbability` (#89)
- [x] Walking home paths over the sidewalk/crosswalk/front-walkway network to the destination house's front door
- [x] Front walkways render as tiled City Kit Suburban path pieces along the Core segment, with a graybox strip fallback when the assets are absent (#128)
- [x] `WorldBuilder` renders road, verge, sidewalk, and crosswalk as visually distinct placeholder-colored surfaces in the primitive fallback (the kit tiles bring their own art); spawned dogs stand on sidewalks
- [x] Crosswalks render only over the road itself, never over sidewalk pavement
- [x] Dogs standing or walking on a sidewalk rest at the sidewalk's raised surface height, not the road's — `WalkNetwork.GroundHeight` resolves the right one per edge (#151)
- [ ] On-device visual check (human task, not attempted here)
