# Delivery truck (#924): concept for review

Our own delivery truck, to replace the Kenney Car Kit "delivery" model. It has the
**same length and width as today's truck**, a body that takes the car colours as a
material multiply, the word "Delivery" on both long sides, four separate wheels that spin
on their axles and a rear door that swings open. There is no driver or anyone else in it. It is built by a Blender Python
script, following [`docs/engineering/blender-pipeline.md`](../../../../docs/engineering/blender-pipeline.md).

**Status: waiting for Derek's review on #924.** Nothing here is imported by Unity (the
folder is outside `Assets/`). Using it in the game is #930.

## Files

```
delivery-truck/
  truck_dims.py        every dimension, pivot and material colour; reads the footprint, car
                       colours, road width and per-mesh triangle cap from Core (plain Python)
  dh_truck.py          builds the truck in Blender (house kit MeshBuilder), plus pose and tint
                       helpers for the renders
  export_truck.py      writes exports/delivery_truck.glb and exports/delivery_truck_stats.json,
                       then runs truck_check on the file; exits non-zero on any error
  truck_check.py       reads the .glb back with plain Python (no Blender) and checks it
  render_truck.py      the review renders (to out/, git-ignored)
  tests/test_delivery_truck.py   the checks as unit tests, plus the byte-for-byte rebuild
  exports/delivery_truck.glb     self-contained glTF binary, truck at rest (door closed)
  exports/delivery_truck_stats.json   size, bounds, triangles, pivots, materials
  review/              the review renders (see below)
```

## Size: today's footprint, from Core

| | Value | Where it comes from |
|---|---|---|
| Length | **4.875 m** | `DeliveryTruckFootprint.ModelLengthAtImportScale` 3.25 × `ModelScale` 1.5 (#547, #660) |
| Width | **2.25 m** | `DeliveryTruckFootprint.ModelWidthAtImportScale` 1.5 × `ModelScale` 1.5 (#672) |
| Height | 2.475 m | the Kenney `delivery.fbx` body top (1.65 at import) × 1.5, measured in Blender |

`truck_dims.py` reads the length and width out of `DeliveryTruckFootprint.cs` when the
script runs, so they are never re-typed. The width is the lettering on the cargo box sides:
the box is 2.238 m wide and the lettering stands 6 mm off each side, so its outer face is
exactly the footprint's edge. The mirrors reach the same line and nothing else goes past it. Bumper to bumper is the length.

What that keeps true in the game:
- **Crosswalks (#660):** 4.875 m is the body length `FitsBetweenCrosswalkBands` was
  checked with (1.125 m of margin), so the crosswalk setbacks don't change.
- **Lane (#672):** `RoadLane.Offset` (1.5 m) + half the width (1.125 m) = 2.625 m ≤
  `RoadWidth / 2` (3 m), so the truck stays on the pavement in its lane.
- **Car-following (#600):** routing and following only use the footprint, which is unchanged.

**Invariant — the Blender truck has today's footprint.** Its length and width equal
`DeliveryTruckFootprint`'s nominal body length and width, read from Core. If Core's
figures change, the tests fail until the truck is re-exported.

## Parts and pivots

Units are metres. Blender axes: front −Y, up +Z, so the truck's **left is +X** and its
right (kerb) side is −X. The exported glTF is Y-up with the front at +Z
(glTF = (x, z, −y)); the truck's left is still +X. How that maps to the game's yaw is #776.

| Node | Pivot (Blender x, y, z) | Pivot (glTF x, y, z) | Moves |
|---|---|---|---|
| `delivery_truck` (root, the body) | (0, 0, 0) | (0, 0, 0) | on the ground at the footprint centre |
| `wheel_front_left` | (0.90, −1.50, 0.42) | (0.90, 0.42, 1.50) | spins about its local X axis |
| `wheel_front_right` | (−0.90, −1.50, 0.42) | (−0.90, 0.42, 1.50) | spins about its local X axis |
| `wheel_rear_left` | (0.90, 1.05, 0.42) | (0.90, 0.42, −1.05) | spins about its local X axis |
| `wheel_rear_right` | (−0.90, 1.05, 0.42) | (−0.90, 0.42, −1.05) | spins about its local X axis |
| `door_rear` | (0.95, 2.32, 0.55) | (0.95, 0.55, −2.32) | swings about its local vertical axis (Blender Z, glTF Y) |
| `decal_delivery_left` | (1.119, 0.76, 1.66) | (1.119, 1.66, −0.76) | static: "Delivery" on the left side of the cargo box |
| `decal_delivery_right` | (−1.119, 0.76, 1.66) | (−1.119, 1.66, −0.76) | static: "Delivery" on the right side |

- **Wheels:** radius 0.42 m, 0.30 m wide, centred on the axle; each touches the ground.
  A positive rotation about +X rolls the truck forward. Rolling at speed `v` turns a wheel
  `v / 0.42` radians per second. Five lug nuts make the spin visible (they repeat every 72°).
- **Rear door: one swing door, hinged on the truck's left (+X).** The pivot is the bottom
  of the hinge line, at the inner edge of the left door post on the rear face. The door
  is 1.90 × 1.75 m. **Open = −90°** about the vertical axis: it swings out and lies straight
  back behind the truck along its left side, so an open door stays inside the truck's
  width and leaves the kerb side, where the package goes, clear. Behind it is a dark
  0.6 m cargo hold.
- **Lettering (Derek's review):** "Delivery", 1.80 m long, centred on each long side of
  the cargo box above the rear wheel arch. It is built from Blender's built-in font (no
  font file), filled flat and converted to triangles in the script. Each side reads
  normally from that side (front to back on the left, back to front on the right) and
  every face points outward. Its origin is on the box side at the centre of the word.
  The lettering is **off-white with a dark outline**, so the same lettering reads on a
  white body (the outline carries it) and on dark brown or dark blue (the fill carries it).
- The export is at rest: door closed, wheels unturned, no rotation or scale on any node.

## Materials

| Material | Colour | On | Tinted |
|---|---|---|---|
| `DH_Truck_body` | white `#FFFFFF` | body (root) and door | **yes**: Unity multiplies it by `Palette.CarColorHex` (#601), which over white gives exactly that colour |
| `DH_Truck_glass` | `#1F2A33` | windscreen, cab side windows | no |
| `DH_Truck_tyre` | `#262628` | tyres | no |
| `DH_Truck_trim` | `#3A3C40` | bumpers, grille, mirrors, handles, chassis, wheel wells, cargo hold, hinges, wheel caps and lugs | no |
| `DH_Truck_hub` | `#B9BDC2` | wheel hubs | no |
| `DH_Truck_headlight` | `#F2EFE2` | headlights | no |
| `DH_Truck_taillight` | `#B3242A` | tail lights | no |
| `DH_Truck_lettering` | `#F7F5EE` | "Delivery" fill, both sides | no |
| `DH_Truck_lettering_outline` | `#1E2226` | the lettering's outline, 3 mm under the fill | no |

Flat colours only, no textures. Only `delivery_truck` and `door_rear` use the body
material, so the body tint never reaches the lettering. Today's `DeliveryTruckView.ApplyCarColor` tints **every** renderer, so #930
has to tint only `DH_Truck_body`.

## Triangles

| Node | Triangles |
|---|---:|
| `delivery_truck` (body) | 392 |
| each wheel (×4) | 208 |
| `door_rear` | 72 |
| `decal_delivery_left` | 267 |
| `decal_delivery_right` | 267 |
| **Total** | **1,830** |

Every mesh is under today's enforced `ArtBudget.MaxTrianglesPerMesh` (2,000), and the
whole truck is well under the provisional 6,000-triangle backstop (#799). The `.glb` is
about 115 KB.

## Checks

`truck_check.py` (run by `export_truck.py` on every export, and by the tests):
- length and width equal Core's footprint (±1 mm), centred on the pivot, on the ground;
  height 2.475 m; fits inside its lane
- every mesh ≤ `ArtBudget.MaxTrianglesPerMesh`, the total ≤ 6,000
- the nodes are the body, four wheels, the door and the two lettering decals, nothing else; wheel pivots on their
  axles, the wheel round about the pivot; the door pivot on its hinge edge
- the door, opened to −90°, stays inside the width
- only the body and door carry `DH_Truck_body`; glass, tyres and trim are their own materials
- one lettering decal on each long side of the cargo box, at the footprint's edge, above
  the wheel arches; every lettering face points outward and the word reads front to back
  on the left and back to front on the right (its one descender, the final "y", marks the
  end); only the decals carry `DH_Truck_lettering`

```
python3 -m unittest discover -s art-source/blender/vehicles/delivery-truck/tests
```

All 22 tests pass. Only `RebuildIsByteIdentical` needs `bpy`; it exports twice in fresh
processes and compares with the committed `.glb` (skipped without `bpy`).

## Review renders (review/)

Derek's review colours are **white** (`#EDEDED`, the palette's), **dark brown** (`#4A3222`,
not in the palette) and **dark blue** (`#23366B`, the palette's): `truck_dims.REVIEW_COLORS`.

- `gamecam-<white|dark-brown|dark-blue>-<closed|open>.png`, and all six on
  `gamecam-sheet.png`: the game camera
  (orthographic, pitch 45°, yaw −40°) on a straight road tile (the approved road kit, look
  A, on the approved lawn). The truck is in its lane heading −Y, and the approved adult
  Golden Retriever (`dogs/golden-retriever/exports/dog_golden_default.gltf`) stands on the
  kerb-side sidewalk level with the rear door.
- `lettering-sides.png`: both long sides of each review colour, square on, so the lettering
  can be read from each side.
- `colors.png`: the seven `Palette.CarColorHex` colours, one game-camera tile each.
- `closeup-front.png` (3/4 front) and `closeup-rear-open.png` (3/4 rear, door open, with the
  dog), in dark brown.
- `motion-sheet.png`: the door at 0°, −30°, −60° and −90°, and a front wheel turned 0°, 15°,
  30° and 45°.

## Regenerating

From the repository root, with `pip install bpy==5.3.0` (the renders also need Pillow,
`shapely==2.2.0` for the road kit, and EEVEE's headless setup: Mesa's software EGL or lavapipe):

```
python3 art-source/blender/vehicles/delivery-truck/export_truck.py          # exports/, prints ALL OK
python3 art-source/blender/vehicles/delivery-truck/render_truck.py [OUT] [--views gamecam,sides,colors,closeup,motion] [--samples 32]
```

Two exports give byte-identical files (sha256), and so does the test's fresh-process
rebuild. Renders are not byte-reproducible. EEVEE on lavapipe can stop partway through a long
run; if a view is missing, render it on its own with `--views`.

## Open for Derek

1. **Shape.** A box delivery truck: a short-hood cab with a cargo box behind it, in the
   house kit's flat-shaded style. The issue fixes only the footprint; the cab and box
   proportions, the 2.475 m height (Kenney's) and the 0.84 m wheels are my choices.
2. **Door.** One swing door hinged on the left, opening 90° to lie straight back. The
   alternative was a roll-up door, which never leaves the footprint; a swing door was
   chosen because the issue asks for a hinge pivot.
3. **Hub and lamp colours** (silver hubs, cream headlights, red tail lights) are untinted
   extras beyond the issue's glass, tyre and trim materials.
4. **Lettering colour.** Off-white with a dark outline, one look for every body colour.
   The other option was dark lettering on a light panel.
5. **Dark brown is not a game colour yet.** The trucks pick from `Palette.CarColorHex`
   (white, black, silver, gray, red, dark blue, dark green), and dark brown (`#4A3222`
   here) is only defined in these scripts for the review. A truck-specific colour set,
   including dark brown, needs a Core palette change, in #930 or a new issue.
