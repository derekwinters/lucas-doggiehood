# Dog ball (#333) — approved

A tennis-style dog ball to replace the white graybox sphere for a lost or delivered **ball**
(`ItemCatalog` "ball"). It is used by #928 (the lost item) and #929 (the delivered toy in the back
yard). This folder holds **source assets only**. Nothing here is imported by Unity, because the
folder is outside `Assets/`. Bringing it into the game is #928's job.

Derek approved the size (25 cm) and the six felt colours on 2026-11-27.

## The model

| | |
|---|---|
| Object | `ball` (one mesh, the same in every colour) |
| Exports | `exports/ball_<colour>.gltf` + `.bin`, one per colour (blue, orange, red, magenta, purple, navy), textures by relative URI `../textures/` |
| Size | **25 cm across** (0.25 m on every axis), about 3.7× a real tennis ball (6.7 cm). A deliberate exaggeration so the ball reads at the game camera: see [Game size](#game-size) |
| Triangles | **1,520** (a 40 × 20 UV sphere). Within the provisional 6,000-triangle per-mesh backstop (#799), and also under the 2,000 cap Unity enforces today |
| Pivot | On the ground under the ball's centre, so it rests on whatever surface it is placed at (the same rule as the props) |
| Material | `DH_Item_Ball_<Colour>` (for example `DH_Item_Ball_Blue`), like the dogs' `DH_Dog_Fur_<Coat>`: one opaque, single-sided material per export with its colour map and the shared tangent-space normal map; roughness 0.92, not metallic |
| Textures | `ball_felt_<colour>_color.png` (one per colour) and `ball_felt_normal.png` (shared): 1024 × 512, equirectangular to match the sphere's UVs, drawn for a real 6.7 cm ball (the game ball is the same ball scaled up) |

All the surface detail is in the maps: felt nap and light fibre tufts, soft mottling, and the
curved white rubber seam set in a shallow groove. The seam is the classic tennis-ball curve, which
splits the ball into two equal dumbbell-shaped felt pieces. The felt noise is computed in 3D on the
ball, so nothing seams where the texture wraps and nothing stretches at the poles.

Metres, Z up, transforms applied, glTF Y-up on export, as in
[blender-pipeline.md](../../../../docs/engineering/blender-pipeline.md). A ball is round, so it has
no front.

## Game size

A real tennis ball is **6.7 cm** (ITF 6.54–6.86 cm). At that size the ball was about 6 pixels across
at the game camera's closest zoom and about 2 at the default zoom, and Derek's review of the first
renders was that it was barely visible. The game ball is therefore **25 cm across, about 3.7× real**.
Derek picked 25 cm from renders of 20, 25 and 30 cm beside the Labrador.

That is a deliberate readability exaggeration. The approved Labrador export is itself about 2.8× a
real Labrador (the top of its head is at 1.58 m), so a tennis ball at the dogs' scale would be about
19 cm; 25 cm is a little larger than that, about the width of the dog's front paw and leg. It is
not head-sized: the Labrador's head, ears included, is about 0.74 m across in the export.

| Zoom | Ball on screen (1080 px tall) |
|---|---:|
| Closest (`CameraController.MinZoom` 6, a 12 m tall view) | 22.5 px |
| Default (`CameraController.DefaultZoom` 18, a 36 m tall view) | 7.5 px |

The size is one constant (`dh_ball_tex.DIAMETER_M`). The texture detail (seam width, felt nap,
groove) is drawn against `REAL_DIAMETER_M`, so changing the size scales the ball without redrawing
its maps.

## Colours

The ball comes in **six felt colours, all with the white seam**, built the way the dogs' coats are:
the mesh and the normal map are shared, and each colour has its own colour map and its own export.

| Colour | Hex | Export | Delta E on grass | Delta E on sidewalk |
|---|---|---|---:|---:|
| Blue | `#2F7FD8` | `ball_blue.gltf` | 87 | 58 |
| Orange | `#F2711C` | `ball_orange.gltf` | 71 | 57 |
| Red | `#D8302A` | `ball_red.gltf` | 89 | 67 |
| Magenta | `#E0358F` | `ball_magenta.gltf` | 107 | 71 |
| Purple | `#7A4FC9` | `ball_purple.gltf` | 109 | 70 |
| Navy | `#24409A` | `ball_navy.gltf` | 94 | 65 |

Delta E (CIE76) is how different the ball's pixels are from the ground around them in
`render_ball.py colours` (the game camera at its closest zoom, 25 cm ball, approved lawn and
sidewalk); higher is easier to spot. It is in `review/colour-variants.png`.

Derek picked these six from a palette of eight. He left out **optic yellow** and **teal**, which
measured weakest (optic yellow 34 on grass, teal 48 on grass and 41 on the sidewalk): both sit close
to the lawn's green, so a real tennis-ball yellow does not suit this lawn.

Things to know about the picks:
- **Red** is nearly the red finder ring's colour (`#FF2A2A`, #602) and the dogs' red collars, and
  **magenta** sits near the dogs' pink tongues. Derek chose both anyway. A red ball inside the red
  finder ring is still easy to see (the ring is about 2 m across, far wider than the ball), but the
  two read as the same colour family.
- **Orange** is close to the yellow and golden coats, so it blends a little when it is near those
  dogs.
- **Navy** is the darkest, so it has less contrast next to a black Labrador.

Which colour a given ball is in the game (for example per dog, per delivery or at random) is not
decided here; it belongs to #928 and #929. Adding or changing a colour is one entry in
`dh_ball_tex.VARIANTS` and a re-export.

## Files

```
ball/
  dh_ball_tex.py     the ball's numbers (size, triangle budget), VARIANTS (the colours), the seam curve,
                     the textures
  dh_ball.py         the mesh and the per-colour material (bpy)
  export_ball.py     glTF export per colour, read-back checks, exports/ball_stats.json (shared
                     measurements plus one entry per colour); --viewer writes .glb files
  render_ball.py     review renders, to out/ (git-ignored)
  tests/test_ball.py seam, size, colour, texture and export checks, including the byte-for-byte rebuild
  textures/          ball_felt_<colour>_color.png (6), ball_felt_normal.png
  exports/           ball_<colour>.gltf + .bin (6 pairs), ball_stats.json
  review/            the review renders (below)
```

It imports the house kit (`../..`, for `reset_scene` and `tri_count`) and the dog kit's render
helpers (`../../dogs/kit/dh_dog_render.py`) by relative path, and copies nothing.

## Review renders

All at 25 cm.

| File | Shows |
|---|---|
| `review/gamecam-grass.png` | The game camera (orthographic, pitch 45, yaw −40) at its closest zoom, the blue ball beside the approved adult Labrador on the approved lawn, with a 3× pixel inset |
| `review/gamecam-sidewalk.png` | The same on the approved sidewalk concrete |
| `review/closeup.png` | The six colours side by side: felt, seam and groove |
| `review/colour-variants.png` | The six colours on grass and sidewalk at the game camera, with their delta E |
| `review/ball-sheet.png` | All four on one page |

## Regenerating

Needs headless Blender as a Python module (`pip install bpy==5.3.0`), numpy and Pillow; the renders
also need EEVEE (Mesa's lavapipe without a GPU). From the repository root:

```
python3 art-source/blender/items/ball/dh_ball_tex.py       # rewrite textures/ (the export builds them only if missing)
python3 art-source/blender/items/ball/export_ball.py       # exports/, with checks; exits non-zero on a failure
python3 art-source/blender/items/ball/export_ball.py --viewer art-source/blender/items/ball/out/viewer
python3 art-source/blender/items/ball/render_ball.py all   # every review render -> out/
python3 -m pytest art-source/blender/items/ball/tests      # about 30 s
```

The export checks, for every colour, that the image URIs are relative and resolve in `textures/` and
that the material has a colour and a normal map and is single-sided; and that the triangle count is within the budget,
that the size is 25 cm on every axis, and that the origin is on the ground under the centre.

Two export runs, and a texture rebuild, reproduce the committed `exports/` and `textures/` byte for
byte; `tests/test_ball.py` checks this in fresh processes. `--viewer` writes `ball_<colour>.glb`
for each colour and `ball_all.glb` (all six in a row), with the textures embedded, for a web 3D
viewer. They are not committed, because a set stores each texture once.
