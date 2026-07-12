# Map Grid & Sidewalks

*Issues: [#105](https://github.com/derekwinters/lucas-doggiehood/issues/105) (map grid & standards), [#106](https://github.com/derekwinters/lucas-doggiehood/issues/106) (sidewalks)*

## The world is a grid of tiles

The world is a square grid of **tiles**. The starting map is exactly **one tile**: the four-way intersection with four properties, one per quadrant ([World & Neighborhood](world.md)).

The map expands by adding one or more tiles adjacent to existing ones ([Neighborhood Expansion](../expansion.md) mechanics sit on top of this geometry). Every tile is always one of the defined types below. The first expansions will be hand-picked; procedurally generated tile selection is explicitly future work.

## Tile-type catalog

A tile type is defined by **which of its edges have a road connection**. Roads meet edges at their midpoints, so adjacent tiles always line up; adding a tile is only legal when every shared edge matches (road-to-road, or no-road-to-no-road).

| Type | Road edges | Shape |
|---|---|---|
| `FourWay` | N, S, E, W | `╋` — the starting tile |
| `StraightNS` | N, S | `┃` |
| `StraightEW` | E, W | `━` |
| `TurnNE` | N, E | `┗` |
| `TurnNW` | N, W | `┛` |
| `TurnSE` | S, E | `┏` |
| `TurnSW` | S, W | `┓` |
| `TeeNorth` | E, W, N | `┻` |
| `TeeSouth` | E, W, S | `┳` |
| `TeeEast` | N, S, E | `┣` |
| `TeeWest` | N, S, W | `┫` |
| `CulDeSacNorth` | N | `╹` — road enters from the north, ends in a bulb |
| `CulDeSacSouth` | S | `╻` |
| `CulDeSacEast` | E | `╺` |
| `CulDeSacWest` | W | `╸` |
| `OpposingTurnsNS` | E, W | `⬭` — two quarter-circle-radius arches joining the E/W edge connections, one bowing north and one bowing south, enclosing a central island |
| `OpposingTurnsEW` | N, S | `⬯` — the 90° rotation: arches bowing east and west |

Each tile type defines its buildable **property lots** in the non-road areas — the starting `FourWay` tile has 4, one per quadrant. Lot layouts for other types are specified when each type is first used.

## Standard dimensions

All world geometry derives from these constants, defined **once** in Core — nothing may duplicate them, so the design cannot drift over time:

| Standard | Value | Status |
|---|---|---|
| Tile size | 60m × 60m | locked |
| Road width | 6m | locked |
| Grass verge (road edge → sidewalk) | 1.5m | proposed — pending confirmation on #105 |
| Sidewalk width | 2m | proposed — pending confirmation on #105 |
| Crosswalk width | 3m | proposed — pending confirmation on #105 |
| Cul-de-sac bulb radius | 9m | proposed — pending confirmation on #105 |
| Opposing-turn arch radius | quarter-circle, peak ~15m from tile center | proposed — pending confirmation on #105 |

## Sidewalks

*Source: [#106](https://github.com/derekwinters/lucas-doggiehood/issues/106)*

Every road has a sidewalk:

- East–west roads: sidewalk on the **north side**.
- North–south roads: sidewalk on the **east side**.

North/east placement keeps sidewalks visible at the fixed isometric camera angle — large future buildings sitting south or west of a road would slightly occlude those sides.

From the road edge outward: **road → grass verge → sidewalk** (dimensions per the standards table).

**Crosswalks** cross the road wherever the sidewalk network needs to continue across one (intersections and T-junctions), and are the only legal road crossing. Sidewalks plus crosswalks form one connected walk network per map.

**All walking happens on sidewalks.** Dog movement — wandering, walking home after accepting a quest, heading to a comfort decoration — paths along the sidewalk/crosswalk network, never on the road surface. Delivery trucks are vehicles and stay on the road.

## Build checklist

- [ ] Tile-type catalog exists in Core with each type's road edges; starting map is one `FourWay` tile at grid (0,0)
- [ ] Existing world data (streets, lots, wander/camera bounds) derives from the tile map + standard dimensions, not standalone constants
- [ ] Adjacent-edge compatibility enforced when adding tiles
- [ ] Standard dimensions defined at a single point, guard-tested against duplication
- [ ] Every road has its north/east sidewalk with grass verge; crosswalks connect the walk network
- [ ] Dogs walk only on the sidewalk/crosswalk network; trucks stay on roads
- [ ] Sidewalks/verges/crosswalks render distinctly and read clearly at the isometric angle
