# Dog bed (#411)

A padded **bolster dog bed** to replace the purple graybox slab that a delivered `bed` shows as
today (`DecorationView`). It is a source asset only. Nothing here is imported by Unity, because the
folder is outside `Assets/`. Showing it in the game is #932, after Derek approves the renders on
#411. It follows `docs/engineering/blender-pipeline.md`.

The bed has a low padded base, a puffy bolster round the rim and a flat cream fleece cushion
tucked inside it. The bolster's top dips at the front, so it reads as the side a dog steps in from.

> **The bed is bigger than today's slab (Derek, #411 review).** The issue first asked for "about
> today's slab footprint (1.4 × 1.0 m), so placement doesn't change". After the first renders Derek
> asked for the bed to be a little bigger, so an adult dog fits it better. It is now
> **1.60 × 1.15 m**, about 15% bigger on each side. Placement no longer stays exactly as it is:
> #932 has to reserve the larger footprint. `YardPlacement` puts slots 2 m apart along the
> slab's 1.0 m side, so if the bed keeps the slab's orientation (Open on #776), two beds side by
> side are 0.85 m apart instead of 1.0 m.

## Size, pivot and triangles

All numbers come from `exports/bed_stats.json`, which `export_bed.py` writes.

| | |
|---|---|
| Footprint | **1.60 × 1.15 m** (X × Y), long side along X like today's graybox slab (`DecorationView` scale 1.4 × 0.35 × 1.0) |
| Height | 0.36 m to the bolster top at the back and sides, 0.25 m at the front dip (the slab is 0.35 m) |
| Cushion top (rest height) | **0.18 m**, flat over at least 0.95 × 0.50 m round the centre |
| Pivot | on the ground at the footprint centre |
| Front | the step-in edge faces −Y in Blender, which is +Z in the glTF |
| Triangles | **1,792** in one mesh. The bed's own target is 2,000 (Derek, #411 review), well under the provisional per-mesh backstop of 6,000 (#799) |
| Rest point | `ATTACH_REST` empty at (0, 0, 0.18) on the cushion top, a child of the mesh, exported as a glTF node (glTF position (0, 0.18, 0)) |

**Where the triangles go.** The bolster sweep has 56 × 10 sides (1,120 triangles). The base and
cushion each have 48 sides with a straight side, a 2-step rounded top edge and a flat top (336
each). Neither has a bottom: the base sits on the ground and the cushion inside the base, so a
bottom is never seen. The first round had 3,072 triangles at 64 × 12 and with bottoms.

**Invariant — the bed never leaves its declared footprint (1.60 × 1.15 m).** Whatever places it
can reserve that rectangle and know the bed stays inside it. `export_bed.py` fails if the bed is
bigger than the footprint or fills less than 96% of either side. It also fails if the bed is over
the 2,000-triangle target or the rest area is smaller than 0.95 × 0.50 m.

**For #932.** The footprint grew from the slab's 1.4 × 1.0 m to 1.60 × 1.15 m, so placement and
yard clearances need the new size. Today a resting dog stays at ground height inside the 0.35 m
slab. On the bed it should lie on the cushion at `ATTACH_REST`, 0.18 m up.

## Files

| Path | What it is |
|---|---|
| `dh_bed.py` | The generator: `DIMS`, `build()`. Base, bolster sweep and cushion in one mesh, plus `ATTACH_REST` |
| `dh_bed_tex.py` | The textures (numpy + Pillow, fixed seeds): a plain-weave canvas (light, tinted per material) and a sherpa fleece, each 512² colour + normal |
| `check_bed.py` | The checks as plain functions: footprint (and today's slab, for comparison), pivot, triangle backstop and target, flat rest area, glTF read-back (image URIs inside `textures/`, `DH_Item_Bed_` material names, `ATTACH_REST` node) |
| `export_bed.py` | Writes `textures/`, `exports/dog_bed.gltf` + `.bin` and `exports/bed_stats.json`, then reads the glTF back and runs every check. Prints `ALL OK`, or `FAILED` and exits 1 |
| `render_bed.py` | Review renders (see below) |
| `export_viewer.py` | A self-contained `viewer.html` (mesh and textures embedded, three.js r128 from cdnjs, drag to orbit) for review. Written to `out/`, not committed |
| `bed_colours.json` | The 10-colour palette (name, cover hex, base hex) and how to tint with it, written by `export_bed.py` from `PALETTE` in `dh_bed.py`. Generated; do not edit by hand |
| `tests/` | `test_bed_checks.py` (the checks, no Blender), `test_bed_palette.py` (the palette rules, and that the export and `bed_colours.json` agree), `test_bed_model.py` (builds the bed and measures it), `test_bed_rebuild.py` (re-exports in fresh processes and requires the committed bytes, the palette included) |
| `exports/` | `dog_bed.gltf`, `dog_bed.bin`, `bed_stats.json` |
| `textures/` | `bed_cover_color.png`, `bed_cover_normal.png`, `bed_fleece_color.png`, `bed_fleece_normal.png` |

**Materials.** `DH_Item_Bed_Cover` (the bolster) and `DH_Item_Bed_Base` (the base) share one
neutral grey canvas map. Each is multiplied by its colour, which is exported as `baseColorFactor`.
`DH_Item_Bed_Cushion` is the cream fleece map, used as it is. The `.gltf` references the images as
`../textures/<name>.png` and embeds none, like the street props.

## Colours (Derek, #411 review: "make the color dynamic")

The colour is not baked in. The canvas map is neutral grey, so a bed's colour is simply the base
colour of two materials. This is the same idea as the house roofs, where one greyscale mask takes
the scheme colour as Base Color. One export and one set of textures serve every colour.

**To tint a bed (#932):** take a colour from `bed_colours.json`. Set the base colour
(`baseColorFactor`) of `DH_Item_Bed_Cover` to its `cover`, and of `DH_Item_Bed_Base` to its `base`.
Leave `DH_Item_Bed_Cushion` alone. The JSON holds sRGB hex; the glTF stores linear values, as any
glTF importer expects. The exported `dog_bed.gltf` is in the default colour, Heather Plum.

| # | Name | Cover | Base |
|---|---|---|---|
| 1 | **Heather Plum** (default) | `#6E5B82` | `#4D405B` |
| 2 | Denim Blue | `#4A7AAD` | `#345579` |
| 3 | Dove Grey | `#A9ADB2` | `#76797D` |
| 4 | Charcoal | `#3F4248` | `#2C2E32` |
| 5 | Rust | `#B5603A` | `#7F4329` |
| 6 | Cranberry | `#8E2F45` | `#632130` |
| 7 | Mustard | `#C9A03E` | `#8D702B` |
| 8 | Dusty Rose | `#C48A8F` | `#896164` |
| 9 | Teal | `#2F7A7E` | `#215558` |
| 10 | Cocoa | `#6B4A35` | `#4B3425` |

The base is always the cover × 0.70 (`BASE_DARKEN`), like the houses' derived colours.
`check_bed.palette_errors()` enforces the rules, and `export_bed.py` fails if any break:
- exactly 10 colours, with unique names and valid hex;
- every two covers at least ΔE 15 apart (CIE76; the closest pair is about ΔE 22);
- every cover at least ΔE 20 from the lawn, so no bed disappears into the grass (green is
  deliberately absent);
- every base darker than its cover.

**Open: who picks a bed's colour.** At random, per owner, by the player when the bed is delivered,
or always the default. That is a design decision for Derek and Lucas, and it belongs to #932 or a
spec change. Nothing here assumes one.

## Regenerating

From the repository root, with `pip install bpy==5.3.0` (plus numpy and Pillow):

```
python3 art-source/blender/items/bed/export_bed.py                 # textures, exports, checks (a few seconds)
python3 -m unittest discover -s art-source/blender/items/bed/tests # 48 tests, a few seconds
python3 art-source/blender/items/bed/render_bed.py [out_dir] [--only gamecam,closeup,closeup_dog,turnaround,footprint,sheet,colours]
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
- **`footprint`**: a top view with the bed's 1.60 × 1.15 m footprint outlined in black, today's
  1.4 × 1.0 m slab outlined in red for comparison, and `ATTACH_REST` marked. The viewer shows the
  same two outlines.
- **`sheet`**: all four on one page.
- **`colours`**: all 10 palette colours at the game camera on the lawn, labelled with name and hex,
  plus a row of close-ups.

**The lying pose is a render-only stand-in.** The dog kit has no lying clip yet (#933). The renders
pose the Lab's exported skeleton by hand (`LIE` in `render_bed.py`) in a prone lie: chest down,
front legs folded forward, hind legs folded under, tail curled to the side. That is the pose #933
describes. It is not exported and is not the #933 clip.

`export_viewer.py`'s viewer has a row of swatch buttons that retint the bed live, starting on
Heather Plum.

The renders are not committed yet. As the pipeline says, only renders Derek approves are copied into
`review/` and committed.

## Open concerns

1. **An adult Lab still nearly fills the bed.** The Lab is 1.46 m from nose to rump, and the bed is
   1.60 × 1.15 m. Lying prone it now fits on the cushion, with its rump against the back bolster and
   its head over the front.
2. **Colour choice is Open** (see Colours above). Heather Plum is the default only because it is
   what the export is tinted with. It nods to the graybox bed's purple (`#6C4FC4`).
3. **Orientation in Unity** is still Open on #776, as it is for every Blender model. The slab's
   long side runs along Unity X, and the bed's long side runs along X too.
