# Street props (approved reference set, round 3)

Street lights, signs, benches and the rest of the street furniture, built the
way the approved textured trees were made: light geometry with tileable colour
and normal maps doing the work. Derek approved all 26 on 2026-11-22 as a
consistent **reference set**, and #821 locked them in here as **source assets
only**. Nothing in this folder is imported by Unity (it is outside `Assets/`).
Only the six props marked **In use** in the table below are meant for the
game for now (#805); the rest are **reference only**, kept so later props stay
in the same style, and are not imported.

The round-2 lamp, stop sign, bench, hydrant and mailbox were rebuilt from their
round-2 builders (same shapes and scale, about a third to a tenth of the
triangles), and the rest is new.

There are no street names anywhere, no ads, and no network or account hooks.
The bus-stop sign is the only place a bus appears.

## Files

```
street-props/
  dh_prop_tex.py, dh_props.py, export_props.py, ...   the generators (see Regenerating)
  exports/*.gltf + *.bin       one glTF per piece, all pointing at ../textures/
  exports/street_props_stats.json   triangles, sizes, materials, URI + texture checks per piece
  textures/                    6 colour + normal pairs shared by every piece, each stored once
  review/sampler-street-props.png   every piece, labelled (triangles, size), dog stand-in for scale
  review/street-props-dressed.png   the street (approved houses + textured trees) dressed with the props
  review/street-props-closeup.png   game camera (ortho, yaw -40, pitch 45) on the intersection
```

The layout matches `../trees` (`exports/` and `textures/` side by side; each
`.gltf` references `../textures/<name>.png`). `export_props.py` reads every
exported `.gltf` back and checks that each image URI is relative and resolves
into `textures/`, that every textured material has both a base-colour and a
normal texture, and that nothing is double-sided. All pieces pass, and the
script exits non-zero if any fails.

## Pieces

Every piece is a single mesh with its origin on the ground at the post base or
footprint centre, front facing -Y. Road decals have their origin on the road
surface and sit 5 mm above it. Human-use sizes use the same 1.15 scale as the
round-2 props and the houses' doors.

| Piece | File | Tris | Size X x Y x Z (m) | Materials | Status |
|---|---|---:|---|---|---|
| Lamp post, lantern | `lamp_post_lantern.gltf` | 354 | 0.43 x 0.49 x 5.01 | lamp_black, lamp_glass | **In use** |
| Street light, cobra-head | `lamp_post_cobra.gltf` | 176 | 0.40 x 2.72 x 6.56 | cobra_grey, concrete, led_lens, pole_galv | Reference only |
| Stop | `stop_sign.gltf` | 64 | 0.87 x 0.09 x 3.17 | galv, labels | **In use** |
| Yield | `yield_sign.gltf` | 34 | 0.87 x 0.09 x 3.06 | galv, labels | Reference only |
| Speed limit 25 | `speed_limit_25.gltf` | 88 | 0.70 x 0.09 x 3.17 | galv, labels | Reference only |
| Slow - Dogs at Play | `slow_dogs_at_play.gltf` | 132 | 1.18 x 0.09 x 4.10 | galv, labels | Reference only |
| Pedestrian crossing | `pedestrian_crossing_ahead.gltf` | 64 | 1.18 x 0.09 x 3.52 | galv, labels | Reference only |
| Crosswalk (with arrow) | `crosswalk_sign.gltf` | 132 | 1.18 x 0.09 x 3.92 | galv, labels | Reference only |
| Dead end | `dead_end.gltf` | 64 | 1.18 x 0.09 x 3.52 | galv, labels | Reference only |
| No parking | `no_parking.gltf` | 88 | 0.34 x 0.09 x 2.83 | galv, labels | Reference only |
| Bus stop sign | `bus_stop_sign.gltf` | 88 | 0.53 x 0.09 x 3.00 | galv, labels | Reference only |
| Park bench | `bench_park.gltf` | 228 | 1.84 x 0.68 x 1.02 | bench_iron, bench_wood | **In use** |
| Bus-stop bench | `bench_bus.gltf` | 354 | 2.19 x 0.71 x 1.04 | bus_green | Reference only |
| Fire hydrant | `fire_hydrant.gltf` | 361 | 0.48 x 0.45 x 0.92 | hydrant_cap, hydrant_red | **In use** |
| Mailbox | `mailbox.gltf` | 117 | 0.23 x 0.60 x 1.47 | flag, hydrant_cap, mailbox, post_white | **In use** |
| Trash cart | `trash_bin.gltf` | 130 | 0.79 x 0.92 x 1.14 | rubber, trash | Reference only |
| Recycling cart | `recycling_bin.gltf` | 132 | 0.79 x 0.92 x 1.14 | labels, recycle, rubber | Reference only |
| Dog-waste station | `dog_waste_station.gltf` | 149 | 0.85 x 0.42 x 1.79 | labels, rubber, waste_green | Reference only |
| Bike rack | `bike_rack.gltf` | 288 | 1.78 x 0.75 x 1.03 | galv | Reference only |
| Newspaper box | `newspaper_box.gltf` | 48 | 0.60 x 0.52 x 1.25 | labels, news_navy | **In use** |
| Utility pole | `utility_pole.gltf` | 352 | 0.74 x 2.40 x 10.29 | insulator, pole_galv, pole_wood, transformer | Reference only |
| Wires, one 30 m span | `utility_wires.gltf` | 384 | 30.00 x 2.04 x 4.49 | wire | Reference only |
| Manhole cover (decal) | `manhole_cover.gltf` | 10 | 0.76 x 0.76 x 0.00 | labels | Reference only |
| Storm drain (decal) | `storm_drain.gltf` | 2 | 1.06 x 0.64 x 0.00 | labels | Reference only |
| Crosswalk (decal) | `crosswalk.gltf` | 12 | 3.00 x 5.50 x 0.00 | labels | Reference only |
| Stop line (decal) | `stop_line.gltf` | 2 | 0.40 x 2.70 x 0.00 | labels | Reference only |

All 26 pieces: 3853 triangles in total. In use: the lantern lamp post, stop sign, park bench, fire hydrant, mailbox and newspaper box (1,172 triangles together).

### Round-2 props, rebuilt

| Piece | Round 2 tris | Round 3 tris |
|---|---:|---:|
| Lamp post (lantern) | 1834 | 354 |
| Stop sign | 602 | 64 |
| Park bench | 1860 | 228 |
| Fire hydrant | 1476 | 361 |
| Mailbox | 740 | 117 |

The round-2 two-blade street-name sign was dropped because the brief says no
street names.

## Textures (shared by every piece)

| File | Size | Used for |
|---|---|---|
| `prop_paint_color/normal.png` | 512, tileable | Enamel or powder-coat paint (orange-peel, faint scratches). Each paint colour is a glTF `baseColorFactor` multiplied over this one texture. |
| `prop_galv_color/normal.png` | 512, tileable | Galvanised steel spangle: sign posts, sign backs, cobra pole, bike rack. |
| `prop_concrete_color/normal.png` | 512, tileable | Light-pole pier, bus-stop and pocket-park pads. |
| `prop_wood_color/normal.png` | 512, tileable, grain along V | Bench slats (as is). The utility pole uses it tinted dark. |
| `prop_plastic_color/normal.png` | 512, tileable | Wheeled carts, wheels, insulators, wires. |
| `prop_labels_color/normal.png` | 2048 atlas, 4 x 4 cells | Every sign face, the dog-waste panel, the recycling label, the newspaper-box front, the manhole cover, the storm grate and the road paint. |

Label atlas cells:

| Row | Cells |
|---|---|
| 0 | stop, yield, speed limit 25, dogs-at-play diamond |
| 1 | SLOW / DOGS AT PLAY plaque, pedestrian, down-arrow plaque, dead end |
| 2 | no parking, bus stop, dog-waste panel, recycling |
| 3 | newspaper front, manhole, storm grate, worn road paint |

The atlas is opaque. Decals are flat opaque polygons (the manhole is a
12-gon), so there is no alpha anywhere in the street props.

Materials: 22 tinted tileable materials plus `labels`, `lamp_glass` (warm
emissive) and `led_lens` (cool emissive), all prefixed `DH_Prop_`. A typical
piece uses 2 to 4 of them.

## Dressed street

`review/street-props-dressed.png` uses the round-2 street (bases 1, 3 and 5, with the
textured trees swapped in) and places:

- **Intersection.**
  - A stop sign on each side-road approach. The southbound one faces away
    from the camera, as it would in reality.
  - Dead end up the north arm.
  - Continental crosswalks on three legs, plus stop lines.
  - A crosswalk sign at the west crossing, facing eastbound traffic.
- **Lighting.** Cobra-head lights on the SW and NE corners, angled over the
  intersection. Lantern posts stay along the block between the street trees.
- **NW corner.**
  - Hydrant with a no-parking sign beside it.
  - Bus-stop sign.
  - Bus-stop bench and newspaper box on a concrete pad in the corner lot.
- **NE corner pocket park.** Park bench, dog-waste station, bike rack and a
  lantern on a concrete pad.
- **Along the block.**
  - Mailboxes at each house.
  - Trash and recycling carts at the curb of the first two houses.
  - "Slow - Dogs at Play" on the north side; "pedestrian crossing" and "speed
    limit 25" on the south side, facing eastbound traffic.
- **South side.** Utility poles with sagging wires (spans of 23 to 31 m).
- **Road decals.** Manholes on the centrelines and storm drains at the curbs.

`review/street-props-closeup.png` is the game camera (orthographic, yaw -40,
pitch 45) on that intersection.

## Notes and open points

- **Utility wires.** At the game camera they read as thin dark lines (1.8 to
  2.8 cm radius, 4-sided). The 30 m span costs 384 triangles. Because they
  cross the road visually at this pitch, they are worth a look in-game before
  committing to them.
- **Sign orientation.**
  - Signs face the traffic they serve, so some show their galvanised backs
    to the camera (the southbound stop sign, for example). Signs that face
    along the street read edge-on, like the bus-stop and no-parking signs.
  - If the game would rather every sign read from the camera, that is a
    placement choice, not a model change.
- **Lamp emission.** The lantern glass and LED lens are emissive (strengths
  1.2 and 1.0) in daylight, as in round 2. Night lighting is a separate
  question.
- **No-parking sign size.** At its real 12 x 18 in size it is small from the
  game camera.
- **Not built.** Street-name signs (excluded by the brief) and any
  advertising panels (the bus bench has none).

## Regenerating

The scripts sit in this folder and import the committed round-3 tree modules
from `../trees` (`dh_tree_tex.py`, `dh_trees_tex.py`, and the round-2 kit
`dh_env.py`) and the house kit from `art-source/blender/`. They need headless
Blender as a Python module (`pip install bpy==5.3.0`), numpy and Pillow. From
the repository root:

```
python3 art-source/blender/environment/street-props/export_props.py   # .gltf + .bin -> exports/, with checks + stats
python3 art-source/blender/environment/street-props/dh_prop_tex.py    # rewrite textures/ (slow; the export only builds them if missing)
art-source/blender/environment/street-props/run_sampler.sh            # four panels + sheet -> out/review/
python3 art-source/blender/environment/street-props/render_street_props.py dressed out/street-props-dressed.png
python3 art-source/blender/environment/street-props/render_street_props.py closeup out/street-props-closeup.png
python3 art-source/blender/environment/street-props/props_table.py art-source/blender/environment/street-props/exports/street_props_stats.json
```

Two export runs produce byte-identical files, matching the committed
`exports/`, and a texture rebuild reproduces the committed PNGs byte for byte.
Render output belongs under an `out/` folder (git-ignored); copy a render into
`review/` only to replace an approved one. `props_table.py` prints the pieces
table above without the Status column, which is maintained by hand.

| Script | Role |
|---|---|
| `dh_prop_tex.py` | Procedural textures |
| `dh_props.py` | Builders, materials and the `PIECES` registry |
| `export_props.py` | glTF export and checks |
| `render_props_sampler.py`, `run_sampler.sh` | Sampler |
| `render_street_props.py` | Dressed street and close-up |
| `props_table.py` | README pieces table |
