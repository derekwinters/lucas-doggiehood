# Dog bed (#411)

A padded **bolster dog bed** to replace the purple graybox slab that a delivered `bed` shows as
today (`DecorationView`). It is a source asset only. Nothing here is imported by Unity, because the
folder is outside `Assets/`. Showing it in the game is #932, after Derek approves the renders on
#411. It follows `docs/engineering/blender-pipeline.md`.

The bed has a low padded base, a puffy bolster round the rim and a flat cream fleece cushion
tucked inside it. The bolster's top dips at the front, so it reads as the side a dog steps in from.

## Size, pivot and triangles

All numbers come from `exports/bed_stats.json`, which `export_bed.py` writes.

| | |
|---|---|
| Footprint | **1.40 × 1.00 m** (X × Y), exactly today's graybox slab (`DecorationView` scale 1.4 × 0.35 × 1.0), long side along X like the slab |
| Height | 0.34 m to the bolster top at the back and sides, 0.23 m at the front dip (the slab is 0.35 m) |
| Cushion top (rest height) | **0.17 m**, flat over at least 0.80 × 0.40 m round the centre |
| Pivot | on the ground at the footprint centre |
| Front | the step-in edge faces −Y in Blender, which is +Z in the glTF |
| Triangles | **3,072** in one mesh, against the provisional per-mesh backstop of 6,000 (#799) |
| Rest point | `ATTACH_REST` empty at (0, 0, 0.17) on the cushion top, a child of the mesh, exported as a glTF node (glTF position (0, 0.17, 0)) |

**Invariant — the bed never leaves today's slab footprint (1.4 × 1.0 m).** Swapping the slab for
the bed moves nothing. `export_bed.py` fails if the bed is bigger than the slab, or fills less than
96% of either side.

**For #932.** Today a resting dog stays at ground height inside the 0.35 m slab. On the bed it
should lie on the cushion at `ATTACH_REST`, 0.17 m up.

## Files

| Path | What it is |
|---|---|
| `dh_bed.py` | The generator: `DIMS`, `build()`. Base, bolster sweep and cushion in one mesh, plus `ATTACH_REST` |
| `dh_bed_tex.py` | The textures (numpy + Pillow, fixed seeds): a plain-weave canvas (light, tinted per material) and a sherpa fleece, each 512² colour + normal |
| `check_bed.py` | The checks as plain functions: slab footprint, pivot, triangle backstop, flat rest area, glTF read-back (image URIs inside `textures/`, `DH_Item_Bed_` material names, `ATTACH_REST` node) |
| `export_bed.py` | Writes `textures/`, `exports/dog_bed.gltf` + `.bin` and `exports/bed_stats.json`, then reads the glTF back and runs every check. Prints `ALL OK`, or `FAILED` and exits 1 |
| `render_bed.py` | Review renders (see below) |
| `export_viewer.py` | A self-contained `viewer.html` (mesh and textures embedded, three.js r128 from cdnjs, drag to orbit) for review. Written to `out/`, not committed |
| `tests/` | `test_bed_checks.py` (the checks, no Blender), `test_bed_model.py` (builds the bed and measures it), `test_bed_rebuild.py` (re-exports in fresh processes and requires the committed bytes) |
| `exports/` | `dog_bed.gltf`, `dog_bed.bin`, `bed_stats.json` |
| `textures/` | `bed_cover_color.png`, `bed_cover_normal.png`, `bed_fleece_color.png`, `bed_fleece_normal.png` |

**Materials.** `DH_Item_Bed_Cover` (bolster, heather plum `#6E5B82`) and `DH_Item_Bed_Base` (base,
darker plum `#4B4057`) share the canvas map, each multiplied by its own colour, which is exported
as `baseColorFactor`. `DH_Item_Bed_Cushion` is the cream fleece map, used as it is. The `.gltf`
references the images as `../textures/<name>.png` and embeds none, like the street props.

## Regenerating

From the repository root, with `pip install bpy==5.3.0` (plus numpy and Pillow):

```
python3 art-source/blender/items/bed/export_bed.py                 # textures, exports, checks (a few seconds)
python3 -m unittest discover -s art-source/blender/items/bed/tests # 30 tests, a few seconds
python3 art-source/blender/items/bed/render_bed.py [out_dir] [--only gamecam,closeup,closeup_dog,turnaround,footprint,sheet]
python3 art-source/blender/items/bed/export_viewer.py [out.html]
```

Two runs of `export_bed.py` give byte-identical files, and `tests/test_bed_rebuild.py` checks this
against the committed files. The renders take about 5 minutes on CPU (EEVEE on Mesa's lavapipe)
and go to `out/` by default, which is git-ignored.

## Review renders

`render_bed.py` imports the exported glTF, so the renders show exactly what was exported.

- **`gamecam`**: the bed on the lawn beside approved base 1 (level 1), at the game camera
  (orthographic, 45° pitch, yaw −40°), with the approved **Labrador** (adult, default yellow coat)
  lying on it. It renders the whole lot, then the same camera zoomed in.
- **`closeup`** / **`closeup_dog`**: a perspective 3/4 close-up of the bed alone, then with the Lab.
- **`turnaround`**: front, side, back and a 3/4 view from above.
- **`footprint`**: a top view with the 1.4 × 1.0 m slab outlined in red and `ATTACH_REST` marked.
- **`sheet`**: all four on one page.

**The lying pose is a render-only stand-in.** The dog kit has no lying clip yet (#933). The renders
pose the Lab's exported skeleton by hand (`LIE` in `render_bed.py`) in a prone lie: chest down,
front legs folded forward, hind legs folded under, tail curled to the side. That is the pose #933
describes. It is not exported and is not the #933 clip.

The renders are not committed yet. As the pipeline says, only renders Derek approves are copied into
`review/` and committed.

## Open concerns

1. **An adult Lab fills the bed.** The Lab is 1.46 m from nose to rump, and the bed is 1.40 × 1.00 m.
   Lying prone it covers the cushion, and its rump and head rest on the bolster. That suits a dog
   bed, but a bigger bed would mean a bigger footprint than today's slab, so it would no longer be
   "placement doesn't change".
2. **Colour.** The plum nods to the graybox bed's purple (`#6C4FC4`) in a realistic fabric shade.
   It is one constant per material in `COLOURS` (`dh_bed.py`) if Derek wants another colour.
3. **Orientation in Unity** is still Open on #776, as it is for every Blender model. The slab's
   long side runs along Unity X, and the bed's long side runs along X too.
