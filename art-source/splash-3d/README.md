# art-source/splash-3d: the splash street as real 3D models (exploration)

This folder rebuilds the 11 houses of the splash art (`Assets/Art/Splash/cover-art.png`) as
real 3D models in Blender, together with the street, lawns, paths, steps, mailboxes, fences,
trees and bushes, and solves a camera that sees them the way the painting does. The goal is
to be able to show the houses from any angle and, later, animate from the splash into the
game view (#935). It is an exploration: nothing here is used by the game, and nothing under
`Assets/` changes.

Everything comes from scripts run headless with `bpy` 5.3.0, the same setup as
`art-source/blender/` (see `docs/engineering/blender-pipeline.md`). There is no `.blend`
file; the scripts are the source.

## Files

| File | What it does |
|---|---|
| `splash_data.py` | Measured data, plain Python: the painted street edges and sidewalk joints for the camera solve, `camera()` (the solve), and every house, tree, bush, fence and mailbox as screen-space shapes and colours measured on the original |
| `build_scene.py` | `build(mode, focal_px, toe_in)`: builds the whole street in Blender. `mode="render"` gives toon materials, `mode="export"` plain Principled materials for glTF |
| `render_splash.py` | Renders from the splash camera (with and without a sky plate made from the original's sky) and the turnaround views |
| `review.py` | The comparison images: side-by-side, 50 % overlay, edge overlay, per-house sheet, turnaround sheet (no bpy) |
| `export_glb.py` | `splash_houses.glb` (the whole street), `house_<key>.glb` per house, `splash_camera.json` |
| `build_viewer.py` | A single-file three.js viewer with the GLB embedded |

## Rebuilding

From the repository root (`pip install bpy==5.3.0`; EEVEE needs `libegl1` and
`libgl1-mesa-dri` in a headless container):

```
python3 art-source/splash-3d/render_splash.py OUT/render --scale 1.0      # about 4 minutes on CPU
python3 art-source/splash-3d/review.py OUT/render OUT/review
python3 art-source/splash-3d/export_glb.py OUT/exports                     # a few seconds
python3 art-source/splash-3d/build_viewer.py OUT/exports OUT/viewer/index.html
```

`render_splash.py` takes `--focal PX` to try another focal length and `--toe-in K` (0 to 1)
to turn each house toward the camera (see below). Renders, exports and the viewer are not
committed; the reviewed set lives outside the repository.

## How a house is built

Each house in the painting is a **front-gable bay** (the gabled front with the door) beside a
**side wing** under a side-gabled roof that runs away from the street (the red house's big
navy roof, the yellow house's mustard wall with the two-pane window, the blue house's dark
wing with the chimney). Every screen-space shape is measured on the original, and
`build_scene.py` back-projects it through the solved camera:

- A front wall is a plane square to the street at the distance where its foot meets the
  ground (`depth_of(ground_y)`), so every door, window, sill, trim band, gable and barge on
  a front lands on its painted outline by construction.
- What the painting cannot show is chosen in metres (`DEPTH` in `splash_data.py`): the bay is
  1.2 m deep, the wing sits 0.6 m behind the bay's front and is 6 m deep (cut to fit before
  the next house), and the wing's ridge height is solved so it appears at the painted
  ridge line.
- Colours are the median of the original's pixels on each surface. The far houses keep the
  haze the painting gives them; the ground gets a distance haze in the shader instead.

Each house is its own collection, under an empty `house_<key>` at the centre of its front
wall's foot, with `walls`, `roof` and `details` meshes. Keys: `red`, `yellow`, `teal`,
`far_salmon`, `far_rose` (left, near to far) and `green`, `blue`, `pink`, `far_yellow`,
`far_pink`, `far_blue` (right).

## Camera

The painting is drawn in one-point perspective: front walls are flat, verticals are vertical
and every line along the street meets near one point. A real camera does that when it looks
straight down the street, level (no pitch, no roll), with the horizon raised by a vertical
lens shift.

| | Value | How |
|---|---|---|
| Vanishing point | (1477.2, 595.8) px | least-squares meeting point of the six painted street edges (lawn edge, sidewalk edge, kerb foot, both sides) |
| Height | 2.8 m above the sidewalk | chosen as the scale: gives a 5.9 m road, 1.46 m sidewalks and 2.3-2.4 m near doors |
| Focal length | 3400 px = 43.5 mm on a 36 mm sensor, 45.0 deg wide | chosen, see below |
| Lens shift | x -0.0246, y -0.0612 (of the frame width) | puts the vanishing point at the solved pixel |
| Position, rotation | (0, 0, 2.8) m, rotation (90 deg, 0, 0) | looks along +Y, the street's axis |

Lateral positions do not depend on the focal length (X = (x - cx) * h / (y - vy) for a point on
the ground), so the street, lawns, paths and every house's front land in the same place at any
focal length. The focal length only sets how deep things are along the street. Two ways to
pick it disagree:

- **Square sidewalk slabs** (the two painted joints one slab apart): about 1720 px (22 mm).
  At that width every house's street-side wall and roof slope show wide and hide most of the
  next house's wing, which the painting never shows.
- **What the painting shows**: no street-side walls at all, which no real focal length gives.
  Longer is closer; 3400 px keeps sizes and spacing believable (houses about 9 m apart along
  the street on the near left) and slabs 2.1 m long. 1720, 2400 and 3400 were rendered and
  compared.

**Fit.** The six edges miss the solved point by 0-15 px except the near segment of the right
road edge (26 px), which kinks in the painting. The painting's left and right sidewalks meet at
slightly different points ((1469.5, 606) and (1453.8, 579.7), as the 2D rebuild measured);
one camera takes the point between them.

## What is hard (and where the painting cheats)

- **The houses' sides.** The painting draws every house as a flat front elevation with no
  street-side wall and no street-side roof slope, while the street itself is in perspective. A
  house beside a street seen this way must show the side facing the street; in 3D each house's
  bay side and roof slope hide part of the next house's wing (worst: red over yellow, yellow
  over teal, green over blue, blue over pink). Shallow bays and a long lens keep it small;
  nothing removes it.
- **Toe-in cheat.** `--toe-in 1` turns each house about its front centre to face the camera,
  which hides those sides and brings the wings back, at the cost of houses no longer square
  to the street (they fan toward the camera when seen from above) and fronts that are no
  longer exactly level. It is a render-time option, not the default.
- **The houses face the viewer, not the street.** Fronts are square to the street's axis and
  each front path runs sideways to the sidewalk, as painted. A house in the game faces its
  street; turning these for the game view is a later decision.
- **Wings as darker walls.** The painting paints each wing much darker than the bay, as if it
  were a side wall in shade, though it faces the viewer. The models keep the measured colours,
  so a wing is a darker paint, not a shadow.
- **Painted shading.** The toon shading is flat colour times a light ramp with soft sun
  shadows. The painting's soft gradients, glare on the glass and shingle dashes are not
  modelled. There are no outlines on the houses in the painting, so none are drawn.
- **Steps.** The painting's near steps run sideways toward the street. The models put two
  steps straight in front of each door, which look smaller from the splash camera.
- **Foliage and props** are low-poly stand-ins (jittered icospheres, simple mailboxes and
  picket runs) at the painted positions and sizes; the far mailboxes sit where the 2D rebuild
  moved them (by its street layout rules), so some stand in front of far houses' doors.
