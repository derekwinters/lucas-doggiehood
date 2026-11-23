# Backyard fences (concept round, #806)

Three fence styles for Derek to choose from. The style is still **Open** on
[World Art → Fences](../../../../docs/specs/world/world-art.md#fences). Each style has
the same modular pieces, built so that they can be laid along the five runs `LotFence` traces
today without changing that geometry. Nothing here is approved, and nothing is imported by
Unity (it is outside `Assets/`).

## Files

```
fences/
  dh_fence_tex.py     generates the five texture pairs in textures/ (seeded, numpy + PIL)
  dh_fences.py        the styles, the pieces, and lay_runs() (FenceTiling's compress-to-fit)
  dh_fence_fit.py     the starter corner lot + LotFence runs + the ray-cast clearance check
  fit_check.py        prints the fit check, writes out/fit_check.json
  export_fences.py    15 .gltf + .bin pairs in exports/, read-back checks, fences_stats.json
  render_fences.py    review renders to out/, plus self-contained viewer .glb files in out/glb/
  textures/           fence_{cedar,paintwood,rail,wire,metal}_{color,normal}.png, each stored once
  exports/            one .gltf per piece, all pointing at ../textures/
```

The script imports the committed street-prop builder and materials (`../street-props`), the
tree modules (`../trees`) and the house kit and `dh_scene.py` (`../..`) by relative path, the
same way the props do. It does not copy them.

## The three styles

| | A. Cedar privacy | B. White picket | C. Split rail + wire |
|---|---|---|---|
| Height | **1.83 m** board tops (6 ft); posts 1.80 m + cap, corner 1.86 m + cap | **1.07 m** picket points (3.5 ft); posts 1.12 m + pyramid cap | **1.35 m** post tops (today's height); top rail 1.20 m, wire 0.03–1.12 m |
| Look | Vertical dog-eared western red cedar boards, 14 cm, tight gaps, each board a different natural tone | Pointed 7 cm pickets, 6 cm gaps, white exterior paint over visible grain | Three rough split-cedar rails through weathered posts, black vinyl-coated welded wire (50 × 100 mm, 5 mm) on the yard side |
| From the game camera | A solid wall. It hides the ground and dogs right behind the near runs | Reads as a light outline and keeps the yard fully visible | Reads as a rustic outline; the wire nearly disappears at game distance |
| Panel tris | 376 | 312 | 62 |
| Line / corner / end post | 24 / 24 / 72 | 28 / 28 / 76 | 10 / 10 / 58 |
| Gate (1.19 m leaf) | 244 | 228 | 98 |
| Fenced ranch lot (27 panels, 22 line, 4 corner, 2 end posts) | **10,920** | **9,304** | **2,050** |
| Materials | `DH_Fence_cedar`, `DH_Fence_hardware` | `DH_Fence_white_paint`, `DH_Fence_hardware` | `DH_Fence_split_rail`, `DH_Fence_wire`, `DH_Fence_hardware` |

**Why C.** The game camera looks down into backyards where the dogs play. C is the option you
can see through (A is not). It is a natural, unpainted material (B is crisp white paint). And a
split rail lined with welded wire is what dog owners actually put up, because a bare split rail
doesn't hold a dog. It also keeps today's 1.35 m height, so choosing it needs no height decision.

**Heights.** Today's fence is 1.35 m (Kenney piece × 5). A is 1.83 m because privacy fences are
6 ft; at that height it blocks the view into the yard from the camera side. B is 1.07 m because
a picket fence is about 3.5 ft. Either height is Derek's call. Every piece is modelled at real
size (1 unit = 1 m), so the game would place these models at scale 1, not × 5.

## Pieces

Every piece is one mesh. The run follows local +X, the finished ("good neighbour") face is −Y and
faces out of the yard, and the pivot is on the ground.

- **panel**: one bay, post centre to post centre, `NOMINAL_LEN` = 0.475 × 5 = **2.375 m**. That is
  FenceTiling's nominal piece today, so the whole-piece count per run doesn't change (a real 8 ft
  bay is 2.44 m). The panel contains **no posts**, so when FenceTiling scales a panel's length to
  `run / count`, only the boards, pickets, rails and wire narrow. Posts are never squashed. The
  line-up shows each panel at × 1.0, × 0.85 and × 0.588.
- **line_post**: at every joint inside a run. **corner_post**: a heavier post where two runs meet,
  including the side-run front end where a connector turns in.
- **end_post**: where a connector meets the house. Its pivot is on the face that touches the wall,
  its body runs along +X (down the connector), and two black wall brackets hold it.
- **gate**: a 1.19 m walk-gate leaf (half a bay) with strap hinges at −X and a latch at +X. No spec
  says where a gate goes, so it is placed nowhere.

Colour comes from generated textures, not geometry. The cedar texture holds eight boards of
different tones, and each board's UVs pick one of them (seeded per piece), so neighbouring boards
differ without extra materials. The wire is one alpha-masked, double-sided quad.

## Fit check

`fit_check.py` builds the base at level 4 (garage, deck, patio) and places it with
`dh_scene.level4_placement()`, the rule behind the House Design clearance table. It then traces
the five runs the way `LotFence.BackyardRuns` does, in base-local metres (front −Y, y = 0 is the
house's depth midpoint):

- The side runs sit 7.75 m off the side road's centreline and 2 m inside the far quadrant edge.
- The rear run sits 2 m inside the rear edge.
- The connectors run at y = 0 to the side-wall midpoints, x = ±W/2, where W is the base's wall
  width.

Clearances come from horizontal rays cast from the fence centre line at 0.04–1.9 m. Eaves above
every fence option don't count, but a slab, porch floor or patio below fence height does. The
driveway and walkway are flat, so they are checked in plan. Clearances are to the fence centre
line; the fence's own half-depth (about 0.05–0.08 m) comes off them. With the garage on the road
side or on the neighbour side, every base-local number is identical, so each base appears once.

| | Ranch (base 2) | Farmhouse (base 7) |
|---|---|---|
| Fence rectangle (x, rear y) | −8.49 … 11.76, 14.90; yard 20.25 wide | −9.35 … 10.90, 12.00; yard 20.25 wide |
| Connector, no-garage side | 2.79 m → 2 pieces × 0.588 | 5.75 m → 3 pieces × 0.807 |
| Side run, no-garage side | 14.90 m → 7 × 0.896; house 2.64 (2.17 to the eave line, overhead) | 12.00 m → 6 × 0.842; house (wrap porch) 3.75 |
| Rear run | 20.25 m → 9 × 0.947; **patio 6.40**, deck 7.30, house 10.70, garage 11.55 | 20.25 m → 9 × 0.947; **patio 2.60**, deck 3.50, house 6.90, garage 7.75 |
| Side run, garage side | 14.90 m → 7 × 0.896; **garage 2.41**, patio 2.41, house and deck 5.91 | 12.00 m → 6 × 0.842; garage 3.65, patio 3.65, house and deck 7.15 |
| Connector, garage side | 6.06 m → 3 × 0.850 | 7.30 m → 4 × 0.769 |
| No-garage connector lands on | **the side wall** (x = −5.70; the slab lip at −5.85 is 0.15 m before it at ground level) | **the wrap porch**: it meets the porch edge (x = −5.60) at 0.04–0.45 m after 3.75 m, then crosses 2.0 m of porch over the floor to the wall (−3.60) |
| Garage connector lands on | **the garage end wall** (x = 9.35 slab, 9.20 wall) after 2.41 m; the spec-literal end (x = +5.70) is **inside the garage**, 3.65 m past it | **the garage end wall** (x = 7.25) after 3.65 m; literal end **inside the garage**, 3.65 m past it |
| Nearest driveway / walkway | 4.10 m (garage connector) / 6.93 m | 3.20 m (garage connector) / 7.74 m |
| Crosses driveway or walkway | no | no |

**Problems found:**

1. **On every base, the garage-side connector runs 3.65 m through the garage** if it is built as
   the spec says (to the base's side-wall midpoint). All ten bases were checked with
   `fit_check.py --bases 1,…,10`: the literal end lies inside the garage on all ten, and the first
   thing the connector meets is the garage's end wall.
2. **On the farmhouse, the other connector crosses the wrap porch** (2.0 m of it).
3. **On the colonial (3) and Dutch colonial (5), the no-garage connector hits the exterior
   chimney.** It sits 1.0 m out from the wall, centred on the side-wall midpoint, which is exactly
   where the connector lands.
4. **Short connectors compress hard.** The ranch's 2.79 m connector is 2 panels at × 0.588. If
   connectors stop at the garage, the ranch's garage connector is 2.41–2.52 m, which is 2 panels
   at about × 0.51–0.53. Boards and pickets then render at half width. It still reads as a fence
   (see the line-ups), but it is the most squashed place on the lot.
5. Nothing collides on the side or rear runs. The tightest spots are the ranch garage side at
   2.41 m to the garage slab, and the farmhouse rear at 2.60 m to the patio. Both match the House
   Design table once you account for eaves.

**In the renders, connectors stop at the first thing they meet at 0.45 m** (the side wall, the
garage end wall or a porch). That is a render choice only, not a proposal. What a connector
should do at a garage or porch is [#783](https://github.com/derekwinters/lucas-doggiehood/issues/783)'s
open question.

## Regenerating

```
pip install bpy==5.3.0                    # plus Pillow, numpy; renders need libegl1 + libgl1-mesa-dri
python3 art-source/blender/environment/fences/dh_fence_tex.py      # textures/ (byte-identical each run)
python3 art-source/blender/environment/fences/export_fences.py     # exports/ (byte-identical each run, checks)
python3 art-source/blender/environment/fences/fit_check.py [OUT] [--bases 2,7]
python3 art-source/blender/environment/fences/render_fences.py [OUT] [--styles ...] [--views lineup,lot,connector,garage,compare,glb]
```

Two runs of the texture and export scripts were compared and are byte-identical. A full render
of the three styles takes about 6 minutes on CPU EEVEE.

## Open questions for Derek

- **Which style**, or more than one (for example, a choice per lot or a fence the player can buy).
- **Height** if A or B: 1.83 m or 1.07 m instead of today's 1.35 m. With A, the near runs hide the
  yard from the game camera.
- **The connectors at a garage, a porch or a chimney** (#783; the problem list above).
- **Bay length**: keep 2.375 m (today's FenceTiling count) or use a real 8 ft (2.44 m) bay. The
  second changes FenceTiling's nominal length.
- **Gates**: whether a fence has one, and where. The front is open today.
- **Wire visibility (C)**: at game distance the alpha-masked mesh mostly mips away. Keeping it
  visible needs coverage-preserving mipmaps or a coarser texture, the same Unity import question
  the tree leaves have (#776).
- **For #813**: the models are 1 m units with the good face on −Y, so FenceTiling's × 5 height
  scale and its 0.475 m model length don't apply as they are. Posts must also be placed separately
  from panels, at every piece joint.
