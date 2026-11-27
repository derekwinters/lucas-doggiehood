# art-source/splash-3d: the splash street as real 3D models (exploration)

This folder rebuilds the 11 houses of the splash art (`Assets/Art/Splash/cover-art.png`) as
real 3D models in Blender, together with the street, lawns, paths, steps, mailboxes, fences,
trees and bushes, seen from a camera solved from the painting. The goal is to show the houses
from any angle and, later, animate from the splash into the game view (#935). It is an
exploration: nothing here is used by the game, and nothing under `Assets/` changes.

**Every house faces the street** (Derek's decision): its front wall is parallel to the road,
its door faces the road, and its path runs straight from the foot of its steps to the
sidewalk, as real houses and the game's houses do. The painting instead draws every front
facing the viewer; the first version of this folder copied that, and its review images are
kept beside the new ones with a `-facing-camera` suffix.

Everything comes from scripts run headless with `bpy` 5.3.0, the same setup as
`art-source/blender/` (see `docs/engineering/blender-pipeline.md`). There is no `.blend`
file; the scripts are the source.

## Files

| File | What it does |
|---|---|
| `splash_data.py` | Measured data, plain Python: the painted street edges and sidewalk joints for the camera solve, `camera()` (the solve), and every house, tree, bush and fence as screen-space shapes and colours measured on the original |
| `build_scene.py` | `build(mode, focal_px)`: builds the whole street in Blender. `mode="render"` gives toon materials, `mode="export"` plain Principled materials for glTF. `build.NOTES` records what the layout pass moved |
| `render_splash.py` | Renders from the splash camera (with and without a sky plate made from the original's sky), the turnaround views, and `house_screen_boxes.json` (where each house lands in the frame) |
| `review.py` | The comparison images: side-by-side, 50 % overlay, edge overlay, per-house sheet, turnaround sheet (no bpy) |
| `export_glb.py` | `splash_houses.glb` (the whole street), `house_<key>.glb` per house, `splash_camera.json` |
| `build_viewer.py` | A single-file three.js viewer with the GLB embedded; its "Splash camera" button uses the splash view |
| `camera_options.py` | A labelled sheet comparing splash views (no bpy) |

## Rebuilding

From the repository root (`pip install bpy==5.3.0`; EEVEE needs `libegl1` and
`libgl1-mesa-dri` in a headless container):

```
python3 art-source/splash-3d/render_splash.py OUT/render --scale 1.0      # about 4 minutes on CPU
python3 art-source/splash-3d/review.py OUT/render OUT/review
python3 art-source/splash-3d/export_glb.py OUT/exports                     # a few seconds
python3 art-source/splash-3d/build_viewer.py OUT/exports OUT/viewer/index.html
```

`render_splash.py` takes `--lens MM` (a render lens, reframed; see "Splash view"), `--cam-y M`,
`--cam-z M`, `--max-gap M` (0 keeps the painting's spacing) and `--focal PX` (the world's
layout lens). `camera_options.py OUT.png NAME=RENDER_DIR ...` puts several renders on one
labelled sheet next to the original. Renders, exports and the viewer are not committed; the
reviewed set lives outside the repository.

## How a house is built

Each house in the painting is a **front-gable bay** (the gabled front with the door) beside a
**side wing** under a side-gabled roof (the red house's big navy roof, the yellow house's
mustard wall with the two-pane window, the blue house's dark wing with the chimney).

1. **Measure.** Every shape of the painted front is back-projected through the solved camera
   onto the plane where the house's foot meets the ground. That gives each door, window, sill,
   trim band, gable and barge its real size and place on the front. Colours are the median of
   the original's pixels on each surface; the far houses keep the haze the painting gives
   them, and the ground gets a distance haze in the shader instead.
2. **Fill in depth.** What the painting cannot show is chosen in metres (`DEPTH` in
   `splash_data.py`): the bay is 4 m deep, the wing sits 0.6 m behind the bay's front and is
   6 m deep, and the wing's ridge height is solved so that, from the painting's viewpoint,
   it sat on the painted ridge line. A wing that runs off the image is 6 m wide.
3. **Face the street** (`face_street()`). Each house turns a quarter turn about the centre of
   its front wall's foot (left side +90 deg, right side -90 deg), so its front faces the road.
   Its front wall stands `FRONT_SETBACK` = 3.2 m behind the sidewalk's lawn edge; its door
   keeps the distance along the street the painting gives it, so the order and spacing of each
   side are the painting's. Houses would be pushed apart to keep `LOT_GAP` = 1.5 m between
   footprints (with the current depths none needs it), and a house more than
   `FAR_GAP_MAX` = 4 m behind the one before it is pulled up to that gap, which brings the
   far houses closer (`--max-gap 0` keeps the painted spacing).
4. **Lot** (`build_lots()`). Each house's steps stand in front of its door; its path runs
   straight from the foot of the steps, square to the street, to the sidewalk; its mailbox
   stands on the lawn 0.45 m from the sidewalk, 0.7 m from the path on the camera side,
   1.5 m tall (one size for all). Path and mailbox are parented to the house.
5. **Props** (`clear_props()`). Trees and bushes stay where they are painted unless a house
   now stands there; then they move into that house's front yard. A fence that would cut
   through a house is dropped. `NOTES["props"]` lists what moved (today: the trees by the
   yellow, blue and far pink houses and the bush by the green house moved; the fence left of
   the blue house is dropped).

Each house is its own collection, under an empty `house_<key>` at the centre of its front
wall's foot (its pivot, turned to face the road), with `walls`, `roof`, `details` and `lot`
meshes. Keys: `red`, `yellow`, `teal`, `far_salmon`, `far_rose` (left, near to far) and
`green`, `blue`, `pink`, `far_yellow`, `far_pink`, `far_blue` (right).

**Footprints.** Along the street each house is now as long as its old width (bay plus wing,
about 2.5 to 7.5 m), and across the street as long as its old depth (about 7.5 m). With the
painted spacing the gaps along each side are 3 to 10 m between the nearer houses but 13 to
27 m before the far ones (the painting packs its far houses closely while the solved camera
puts them far down the street); the 4 m cap pulls the far salmon house 12.7 m and the far rose
35.2 m closer on the left, and the right side's houses from the blue one on by 6 to 28 m.

## Camera

There are two cameras: the **solved camera**, fitted to the painting, which lays out the
world, and the **splash view**, the camera the splash is rendered from.

### Solved camera (lays out the world)

The painting is drawn in one-point perspective: front walls are flat, verticals are vertical
and every line along the street meets near one point. A real camera does that when it looks
straight down the street, level (no pitch, no roll), with the horizon raised by a vertical
lens shift.

| | Value | How |
|---|---|---|
| Vanishing point | (1477.2, 595.8) px | least-squares meeting point of the six painted street edges (lawn edge, sidewalk edge, kerb foot, both sides) |
| Height | 2.8 m above the sidewalk | chosen as the scale: gives a 5.9 m road, 1.46 m sidewalks and 2.3-2.4 m near doors |
| Focal length | 3400 px = 43.5 mm on a 36 mm sensor, 45 deg wide | chosen: square sidewalk slabs would give about 1720 px, but 3400 kept the first, camera-facing houses' street-side walls thin. It sets how far apart things are along the street |
| Lens shift | x -0.0246, y -0.0612 (of the frame width) | puts the vanishing point at the solved pixel |
| Position, rotation | (0, 0, 2.8) m, rotation (90 deg, 0, 0) | looks along +Y, the street's axis |

**Fit.** The six edges miss the solved point by 0-15 px except the near segment of the right
road edge (26 px), which kinks in the painting. The painting's left and right sidewalks meet at
slightly different points ((1469.5, 606) and (1453.8, 579.7), as the 2D rebuild measured);
one camera takes the point between them.

### Splash view (renders the splash; the default since Derek asked for stronger perspective)

Derek asked for the road to fade small faster so the house fronts, which now face the road,
show more. The splash view keeps the solved camera's height, level aim and lens shift and
changes only the lens and where the camera stands: a wider lens, moved forward along the
street by `y = ref * (1 - f_view / f_solved)` so the two near houses' fronts (red and green,
`VIEW_REF_HOUSES`) keep their painted size.

| | Value |
|---|---|
| Lens | 18 mm on a 36 mm sensor (1408 px, 90 deg wide) |
| Position | (0, 14.9, 2.8) m: 14.9 m further down the street than the solved camera, same height |
| Aim, shift | level, looking along +Y, lens shift x -0.0246, y -0.0612 (the vanishing point stays at (1477, 596) px) |
| Spacing | far houses pulled up to 4 m gaps (`FAR_GAP_MAX`) |

`review/camera-options.png` compares the options rendered (all reframed, same height unless
noted): A 43.5 mm (the previous splash view, the solved camera itself), B 28 mm, C 22 mm,
D 18 mm, E 22 mm with the far houses pulled to 4 m gaps, F 22 mm from 4 m high, and G 18 mm
with the far houses pulled to 4 m gaps, which is the default.

What changes and what does not:

- **The road, sidewalks and vanishing point do not move.** A level camera at the same height
  and shift draws every line along the street through the same vanishing point at the same
  angle whatever its lens or position, so the road's edges and the horizon are exactly where
  the painting has them in every option. What changes is how fast things shrink with
  distance: at 18 mm the road's far end (the cross street, 70 m out) sits about 72 px below the
  horizon instead of 136 px, so the street visibly runs off to a point sooner.
- **Near fronts.** The visible width of a front, as a share of the painted front's width:
  red 30 % at 43.5 mm, 46 % at 28 mm, 59 % at 22 mm, 71 % at 18 mm; green 30, 47, 61 and 76 %.
  Their doors, windows and flower boxes read clearly at 18 mm.
- **Middle and far fronts change less.** A front's angle to the camera grows as the camera
  comes forward, but the house also shrinks. At 18 mm with 4 m gaps (against 43.5 mm):
  yellow 28 % (22 %), teal 19 % (18 %), pink 20 % (15 %), blue 33 % (18 %, from 29 to 51 px);
  the far houses' fronts are 3 to 7 px wide (2 to 5 px). Pulling the far houses to 4 m gaps
  brings them nearer, so they read as a row of houses again rather than specks.
- **The higher camera (F)** shows more lawn and more of the roofs but turns no front further
  toward the camera, so it is not the default.
- **Lost at 18 mm:** what stands nearer than 14.9 m is behind the camera: the bush at the
  front left is gone, and the near trees are cut at the frame's edge. The painted near houses
  fill the frame's edges much as in the painting, but with their end walls large in the
  foreground.

The previous street-facing review at 43.5 mm is kept with a `-43mm` suffix, and the first,
camera-facing version with a `-facing-camera` suffix.

## The splash view now, and what is hard

- **Fronts are seen at an angle.** From the splash view each front is a foreshortened strip
  facing the road; at 18 mm the near fronts show most of their painted detail, the middle ones
  a narrow strip and the far ones almost nothing. What faces the camera instead is the wing's end wall, a plain coloured
  wall under a gable (the red house's dark red end, the blue house's teal end), with no windows
  because the painting shows none there. The street reads as a real street; the painting's
  row of bright, detailed fronts is gone.
- **Far houses hide behind near ones.** Seen end-on, each house's wing hides much of the next;
  the far houses are small and partly hidden even with the 4 m gaps.
- **The painting's perspective is not physical.** It draws every house as a flat front
  elevation, square to the viewer, while the street is in perspective; no camera reproduces
  both. The `-facing-camera` version matched the fronts to the pixel and showed the cost as
  street-side walls covering the next house; the street-facing version is real but no longer
  matches the painting's composition. A splash that keeps the painting's look from this
  camera would need its own composition (for example houses set further back with more
  space between them, or windows on the walls that face the camera), which is a design
  decision.
- **Wings as darker walls.** The painting paints each wing much darker than the bay, as if it
  were a side wall in shade. The models keep the measured colours, so a wing is a darker paint.
- **Painted shading.** The toon shading is flat colour times a light ramp with soft sun
  shadows. The painting's soft gradients, glare on the glass and shingle dashes are not
  modelled. There are no outlines on the houses in the painting, so none are drawn.
- **Foliage and props** are low-poly stand-ins (jittered icospheres, simple mailboxes and
  picket runs) at the painted positions and sizes.
