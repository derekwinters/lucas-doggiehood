# Sidewalks & Walk Network

*Related: [#106](https://github.com/derekwinters/lucas-doggiehood/issues/106) (this page), [#105](https://github.com/derekwinters/lucas-doggiehood/issues/105) (locked world dimensions this page consumes), [#109](https://github.com/derekwinters/lucas-doggiehood/issues/109) (deferred multi-tile grid, out of scope here), [#112](https://github.com/derekwinters/lucas-doggiehood/issues/112) (deferred approach-to-rest movement, out of scope here)*

## Symmetric sidewalk placement

Every road gets a grass verge and a sidewalk on **both** sides — not just one edge. Moving outward from the road centerline: road → grass verge → sidewalk, mirrored on the other side. All three widths are the locked [standard dimensions](tile-catalog.md#standard-dimensions) (`WorldDimensions`, [#105](https://github.com/derekwinters/lucas-doggiehood/issues/105)) — nothing on this page introduces a new measurement:

| Standard | Value |
|---|---|
| Road width | 6m |
| Grass verge (road edge → sidewalk) | 1.5m |
| Sidewalk width | 2m |

A sidewalk's centerline sits `RoadWidth / 2 + GrassVergeWidth + SidewalkWidth / 2` from the road's own centerline — `5.5m` for the starting intersection's two roads. `Road` and `Sidewalk` (Core, `Assets/Scripts/Core/World/`) express this purely as that formula over `WorldDimensions`; a guard test (`WorldDimensionsGuardTests`) fails the build if any of the locked values is ever re-declared as a literal outside `WorldDimensions.cs`.

## The crosswalk box

At the starting intersection, the two crossing roads produce four box corners (one per compass direction the roads' sidewalks meet). Each of the 4 road arms — N, S, E, W — gets one crosswalk, `CrosswalkWidth` (3m) wide, connecting the two box corners on either side of that arm and letting a pedestrian cross straight over that road instead of going the long way around through both corners.

This is placeholder geometry: crosswalks render as a flat, distinctly colored rectangular patch (no zebra-stripe markings) — see [Art & UI Style](art-style.md) for the palette and [Build checklist](#build-checklist) below for what's actually implemented.

## The walk network graph

`WalkNetwork` (Core) is a generic, data-driven graph over nodes (points) and edges — sidewalk segments, crosswalks, and driveway stubs — built from whatever `Road`s and house lots are passed to `WalkNetwork.BuildFrom`. It is **not** a `TileType` enum or a multi-tile adjacency grid — that system is explicitly deferred to [#109](https://github.com/derekwinters/lucas-doggiehood/issues/109) (`06 - Neighborhood Expansion`, post-MVP). Today it happens to describe exactly the starting tile's one intersection, because that's all `NeighborhoodLayout` produces; the same algorithm would extend to more roads without changes.

Construction, generically:

1. **Sidewalk arms.** Every road's two sidewalks are split into arms wherever another road crosses them, so a sidewalk never runs through another road's own footprint.
2. **Crosswalks.** At each crossing, the four box-corner nodes are connected pairwise into the 4-crosswalk box described above.
3. **Driveway stubs.** Each house lot gets one stub edge to the nearest point on the nearest sidewalk edge (perpendicular projection, clamped to the segment) — splitting that sidewalk edge at the attach point if needed.

`NeighborhoodLayout.WalkNetwork` caches one instance built from `NeighborhoodLayout.Roads` and `NeighborhoodLayout.HouseLots`. The resulting network is fully connected — every node can reach every other node, including every house lot via its driveway stub — which a Core test asserts directly (BFS reachability from any starting node).

`WalkNetwork.FindPath` is a small Dijkstra shortest-path over the graph (a priority-queue implementation would be overkill for a graph this size) — used by both wander and walking-home.

## Wander: a node-to-node walk over the network

`WanderBehavior` ([Dog Behavior](../dogs/behavior.md)) no longer does centerline-bounce math on the streets directly. It now random-walks node to node over the sidewalk+crosswalk portion of the network — **driveway stubs are never entered**, so general wander never crosses onto a house lot or into a yard.

At each node, the choice between continuing straight and deviating/turning is weighted (`WanderBehavior.NextTarget(current, continueWeight, deviateWeight)`); the parameterless overload — what every caller uses today — defaults to even/uniform randomness between the two. The mechanism exists so a future issue can bias it per personality; **that wiring is explicitly not done yet**. This means the per-personality `MovementProfile.TurnProbability` distinction from [Dog Behavior](../dogs/behavior.md#movement-conveys-personality) (Excited's long straight stretches) is temporarily not reflected in wander's actual path shape — `Speed` still applies, so Excited dogs are still faster, but the "long straight stretches" pacing is dormant until a follow-up issue passes personality-derived weights through `DogView`.

## Walking home routes over the network

Walking home after accepting a "buy me X" quest ([Quest Content](../quests/quest-content.md)) used to move in a straight line, ignoring streets entirely. Core now computes the actual route with `WalkNetwork.FindPath` from the dog's current position to its house lot, ending via that lot's driveway stub; the Unity layer (`QuestDirector`) only consumes the returned waypoint list and walks it frame by frame (`Vector3.MoveTowards` hop to hop) — the Core/Unity split stays intact, per [Testing Strategy](../../engineering/testing.md).

## Explicitly out of scope here

- **Approach-to-rest movement** ([#112](https://github.com/derekwinters/lucas-doggiehood/issues/112)) — `RestBehavior` stays a pure probabilistic state flip with no movement of its own; it doesn't touch the walk network.
- **Multi-tile grid / adjacency system** ([#109](https://github.com/derekwinters/lucas-doggiehood/issues/109), `06 - Neighborhood Expansion`) — no `TileType` enum exists. `WalkNetwork` is generic enough to extend to it later, but building that system is not part of this page.
- **Literal crosswalk striping** — deferred visual polish; today's crosswalk is one flat-colored patch.

## Build checklist

- [x] Every road declares a grass verge and sidewalk on both sides, sized from `WorldDimensions` only
- [x] The 4-crosswalk box exists at the starting intersection, one crosswalk per road arm
- [x] The walk network graph (sidewalks + crosswalks + driveway stubs) is generated from `NeighborhoodLayout`'s roads and house lots, and is fully connected
- [x] `WanderBehavior` only ever produces positions on the sidewalk/crosswalk network — never a road surface, never a driveway stub
- [x] `WanderBehavior`'s node choice supports weighted continue-vs-deviate decisions, defaulting to even randomness
- [x] Walking home paths over the sidewalk/crosswalk/driveway-stub network to the destination lot
- [x] `WorldBuilder` renders road, verge, sidewalk, and crosswalk as visually distinct placeholder-colored surfaces; spawned dogs stand on sidewalks
- [ ] On-device visual check (human task, not attempted here)
