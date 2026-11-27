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

`render_splash.py` takes `--focal PX` to try another focal length. Renders, exports and the
viewer are not committed; the reviewed set lives outside the repository.

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
   footprints; with the current depths none needed it.
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
about 2.5 to 7.5 m), and across the street as long as its old depth (about 7.5 m). The gaps
along each side follow the painting: 3 to 10 m between the nearer houses, but 13 to 27 m
before the far ones, because the painting packs its far houses closely while the solved
camera puts them far down the street.

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

Lateral positions do not depend on the focal length (X = (x - cx) * h / (y - vy) for a point
on the ground), so the street, lawns and the houses' distance from the road are the same at
any focal length; the focal length only sets how far apart things are along the street.
Square sidewalk slabs would give about 1720 px (22 mm); 3400 px was chosen while the houses
faced the camera (it kept their street-side walls thin), and it is kept so the street, the
spacing and the comparison with the first version stay the same.

**Fit.** The six edges miss the solved point by 0-15 px except the near segment of the right
road edge (26 px), which kinks in the painting. The painting's left and right sidewalks meet at
slightly different points ((1469.5, 606) and (1453.8, 579.7), as the 2D rebuild measured);
one camera takes the point between them.

## The splash view now, and what is hard

- **Fronts are seen edge-on.** From the splash camera each front is a narrow, steeply
  foreshortened strip facing the road; the painted doors, windows and flower boxes read only on
  the nearest houses. What faces the camera instead is the wing's end wall, a plain coloured
  wall under a gable (the red house's dark red end, the blue house's teal end), with no windows
  because the painting shows none there. The street reads as a real street; the painting's
  row of bright, detailed fronts is gone.
- **Far houses hide behind near ones.** Seen end-on, each house's wing hides much of the next;
  the far rose and far blue houses are barely visible.
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
