# Backyard dog pools (approved, #741)

The real models for the pool a delivered `pool` gift leaves in a dog's back yard. Today the game
draws a graybox cylinder there (#740). Derek approved **two** pools, a 5 m round one and a
6 × 3 m oval, and the game will **pick one at random for each pool** ("Both pools look good let's
add both and pick a random one each time"). Putting them in the game, the random pick included, is
#926. Letting dogs swim in them is #921.

Nothing here is imported by Unity, because the folder is outside `Assets/`.

## How we got here

- **Round 1:** one round pool at the graybox's exact size (two dogs wide), with a step ledge all
  the way round. Derek: too skinny, and the two-tier base didn't make sense.
- **Round 2:** one plain wall, three dogs wide, three steps up the outside.
- **Round 3, approved:** Derek set the **water** size directly: "try a 5m water circle, and a 6m x
  3m oval with small radius corners". The water is now the driving dimension, and each model's
  footprint is whatever the pool then covers. Both shapes were approved.

## What they are

Both are open-topped, above-ground pools built the same way, from one generator, with flat colours
and no hand modelling:

- **Wall:** one slate blue-grey painted wall from the ground to the rim, with darker uprights about
  every 1.25 m, and an off-white coping cap on the rim.
- **Water:** turquoise, 0.24 m below the rim (PoolPlacement's water height), with a few pale ripple
  arcs on it so it reads as water from the 45° game camera. A pale aqua liner shows between the
  water and the rim. Below the water the liner goes down to a floor 0.10 m above the ground, so
  there is a real basin for a dog to swim in later (#921).
- **Steps:** 3 treads, 0.26 m deep and 0.90 m wide, in even 0.40 m rises (ground, three treads,
  rim). The treads are off-white and the risers and sides darker, so they read as steps from above.
  They are on the camera's side: on the round pool at plan angle 260° (front-left, turned 30° off
  the camera's line so one side shows), on the oval in the middle of its front (−Y) long side.

## Sizes

Every ring of a pool (liner, wall, rim) is the water outline pushed outward, so both shapes have
the same 8 cm wall and 2.5 cm rim lip. Height and water height come from
`Assets/Scripts/Core/World/PoolPlacement.cs`, which `pool_core.py` reads at run time.

| Measure | Circle | Oval |
|---|---|---|
| Water surface | **5.00 m** across | **6.00 × 3.00 m**, corner radius **0.45 m**, long axis X |
| Outer rim | 5.21 m across | 6.21 × 3.21 m |
| Footprint, steps included (x × y) | **5.21 × 5.99 m** (x −2.605…2.605, y −3.387…2.605) | **6.21 × 3.97 m** (x −3.105…3.105, y −2.36…1.605) |
| Bounding radius from the origin | **3.39 m** (the step corners) | **3.31 m** (the rim corners) |
| Height (ground to rim top) | 1.5844 m (`PoolHeight`) | 1.5844 m |
| Water surface above the ground | 1.3468 m (`PoolWaterSurfaceHeight`), 0.2377 m below the rim | the same |
| Water depth (floor to surface) | 1.25 m | 1.25 m |
| Triangles | **914** | **600** |

The origin (the pivot) is on the ground at the centre of the water. The footprint is lopsided in y
because the steps stand out from one side.

**Invariant — each pool model, steps included, stays within its recorded footprint and never stands
taller than `PoolPlacement.PoolHeight`.** The footprint is what `pool_core.footprint()` derives
from the dimensions and what `exports/pool_<shape>_stats.json` records.
`tests/test_pool_footprint.py` enforces it against the exports.

### What the game has to change (#926)

`PoolPlacement` still keeps a **2.54 m circle** clear for the graybox. Both models are much bigger,
so its clear space must be updated before they go in the game. A circle of each model's bounding
radius would reserve 6.78 m across for the round pool and 6.62 m for the oval. The oval is only
3.97 m deep, so it really needs a **rectangular** clearance (with the pool's yaw) rather than a
circle. The random pick between the two shapes is game behaviour and is also #926's.

Both fit the starter lot's back yard in the review renders with at least one dog width (1.27 m,
PoolPlacement's comfort margin) to the fence, the house and the deck.

## Triangles

The provisional art budget from #799 is 6,000 triangles per model.

| Part | Circle (48 sides) | Oval (28 sides) |
|---|---:|---:|
| Wall | 96 | 56 |
| Uprights | 66 (11) | 72 (12) |
| Rim cap (outer edge, top, inner edge, two lip undersides) | 480 | 280 |
| Liner wall | 96 | 56 |
| Floor and water | 92 | 52 |
| Ripple arcs (5) | 60 | 60 |
| Steps | 24 | 24 |
| **Total** | **914** | **600** |

One mesh per pool, nine materials: `DH_Item_pool_shell`, `DH_Item_pool_coping`, `DH_Item_pool_upright`,
`DH_Item_pool_step`, `DH_Item_pool_riser`, `DH_Item_pool_liner`, `DH_Item_pool_floor`, `DH_Item_pool_water`,
`DH_Item_pool_ripple` (the items convention, `DH_Item_<item>_<slot>`). The water is opaque.
Whether Unity makes it see-through, or animates it, is for #926 and #921.

## Files

```
items/pool/
  pool_core.py        the dimensions and plan geometry (plain Python): water sizes, outlines,
                      steps, footprint(); reads PoolPlacement.cs; the triangle budget
  dh_pool.py          the generator: build("circle" | "oval"), materials, measures()
  export_pool.py      writes both exports (or one, with --shape)
  render_pool.py      review renders to out/ (git-ignored)
  tests/test_pool_footprint.py   the size check (plain Python, no Blender)
  exports/pool_circle.glb, pool_circle_stats.json
  exports/pool_oval.glb, pool_oval_stats.json
                      self-contained .glb files (no textures): each loads as-is in a web glTF viewer
  review/             the approved review renders (below)
```

It imports the house kit (`../..`, for `material()`, `tri_count()` and `export_glb()`) and, for the
renders, the ground, fence and lot scene (`../../environment/ground`) and the approved yellow
Labrador export (`../../dogs/labrador/exports`) by relative path. It copies nothing.

## Conventions

Metres, Z up in Blender, exported Y-up (`export_yup=True`) with transforms applied. The pivot is on
the ground at the centre of the water. The objects are `pool_circle` and `pool_oval`. With yaw 0,
each pool shows its steps to the game camera.

## Regenerating

From the repository root, with `pip install bpy==5.3.0`:

```
python3 art-source/blender/items/pool/export_pool.py                 # both; --shape circle|oval for one
python3 -m unittest discover -s art-source/blender/items/pool/tests
python3 art-source/blender/items/pool/render_pool.py                 # needs Mesa EGL in a container
```

The export rewrites `exports/` byte for byte. This was verified with two runs on `bpy` 5.3.0,
each into a fresh folder, whose files matched each other and the committed ones.

The check fails if either pool's water stops spanning its approved size or its height, water
height or extent stops matching, if the steps no longer reach the footprint's edge, or if a
triangle count goes over budget or disagrees with its stats file.

## Review renders

Approved by Derek on #741, copied unchanged from `render_pool.py`'s output:

- `review/pool-compare-gamecam.png`: both pools side by side, in the same yard at the same game
  camera, with the Lab.
- `review/pool-<shape>-yard-gamecam.png`: the starter corner lot's fenced back yard (ranch at
  level 4, split-rail fence, approved lawn) at the game camera (ortho, pitch 45°, yaw −40°), with
  the pool beside the adult yellow Lab.
- `review/pool-<shape>-water-closeup.png`: the same scene at the game camera, framed on the pool.
- `review/pool-<shape>-turnaround.png`: side, three-quarter and top views with the Lab for scale,
  and a red ring at the bounding radius.

The split-rail fence is used only for the render; which fence style the game uses is still open
(#831).
