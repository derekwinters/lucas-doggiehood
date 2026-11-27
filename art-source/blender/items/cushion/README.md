# Cushion (design build for #412, pending approval)

The cushion that replaces today's coral graybox slab when a dog's yard gets a cushion
(`DecorationView`, #48). It is a **puffy, tufted floor cushion**: two soft fabric panels sewn
together at a seam 10 cm off the ground, a top that domes up to a broad, near-level plateau a dog
lies on, six cream tufting buttons, cream piping round the seam, and little pinched "ears" at the
corners. It keeps today's coral colour family. Showing it in the game is #932.

Nothing here is imported by Unity (the folder is outside `Assets/`). It follows
`docs/engineering/blender-pipeline.md`. **Status: awaiting Derek's approval of the renders on
#412.**

## Files

```
items/cushion/
  dh_cushion.py            the model, from the named numbers at its top
  export_cushion.py        export + read-back checks; prints ALL OK
  render_cushion.py        review renders: gamecam | closeup | sheet | posecheck
  make_viewer.py           out/viewer.html: a self-contained three.js viewer (not committed)
  exports/item_cushion.gltf + item_cushion.bin   the cushion (glTF 2.0, flat colours, no images)
  exports/cushion_stats.json   triangles, size, rest-top height, materials
  review/gamecam.png       in a yard beside the base-1 house, the adult Golden lying on it, game camera
  review/closeup.png       3/4, side (10 cm bar), top, end, with the Golden lying on it, today's slab
  review/render-sheet.png  both game-camera shots and the close-ups on one page
  tests/test_cushion_export.py   the checks below, on two fresh builds
```

## Size, pivot and axes

| | |
|---|---|
| Size | **1.380 m long × 0.978 m deep × 0.295 m high**, piping included |
| Slab footprint | 1.4 × 1.0 m (today's `DecorationView` slab): fits with about 1 cm to spare on each side, so placement does not change |
| Rest top | **0.295 m** at the centre; the top is a near-level plateau over the middle 0.8 × 0.5 m (within 5 cm, the tufting dimples included) |
| Seam / piping | 0.10 m off the ground |
| Pivot | The centre of the footprint, on the ground |
| Axes | Blender: long side along X, front −Y, up +Z, metres. glTF: long side along X, up +Y (`export_yup`) |

The slab is 0.35 m tall; the cushion is lower (0.295 m), so a dog resting on it sits a little
lower than on the slab. Where exactly a resting dog goes is #932's to decide.

## Nodes, triangles and materials

One node, `item_cushion`, holding one mesh with three primitives:

| Material | What | Triangles |
|---|---|---|
| `DH_Item_cushion_fabric` (coral `#E9765F`) | Top panel (40 × 28 grid) and bottom panel (20 × 14) | 2,800 |
| `DH_Item_cushion_piping` (cream `#F3E4CB`) | The cord round the seam, 2 cm radius, 6 sides | 1,632 |
| `DH_Item_cushion_button` (cream `#F3E4CB`) | Six flattened-dome buttons, 3 × 2 | 300 |
| **Total** | | **4,732** |

The provisional per-mesh backstop is **6,000 triangles** (#799), so the cushion is at 79% of it.
No textures; flat material colours only. Every material is single-sided and uses no glTF
extension. No collider, camera, light, skin or animation.

## How it differs from the bed and the blanket

The bed (#411, "a real padded dog bed") and the blanket (#413, "lying flat") are being built in
parallel and have no model yet, so the cushion is made to contrast with what each brief says:

- **Not a bed:** no raised rim or bolster. The cushion is one domed pillow, highest in the middle,
  with nothing round its edge but the thin piping.
- **Not a blanket:** it is thick (0.295 m), puffy and firm-edged, where a blanket lies flat with
  folds. `test_top_is_restable` holds the top at least 0.15 m off the ground.
- **Colour:** it keeps coral, the colour today's graybox already gives the cushion (the bed's slab
  is purple and the blanket's blue), so the three stay apart at the game camera.

## Checks (all pass)

`export_cushion.py` reads the written file back and exits non-zero unless: there is exactly one
node and one mesh and nothing else; the node has no transform; the footprint fits 1.4 × 1.0 m and
is centred on the pivot; the lowest point is on the ground; the triangles are within the backstop
and match Blender's count; nothing is double-sided.

`tests/test_cushion_export.py` builds the cushion twice into a temporary folder (fresh processes)
and checks independently, with plain Python reading the `.gltf` + `.bin`:

> **Invariants**
> - **The cushion fits today's slab footprint:** at most 1.4 m (X) by 1.0 m (Z), centred on the
>   pivot, so placement does not change.
> - **The export is reproducible:** a second build, in another fresh process, is byte-identical to
>   the first and to the committed `exports/`.

It also checks the cushion sits on the ground, the top is a near-level plateau at least 0.15 m
high over the central 0.8 × 0.5 m rest area, the triangles are within the backstop and match
`cushion_stats.json`, and the file holds only the cushion.

## Regenerating

From the repository root, with `pip install bpy==5.3.0` (plus Pillow for the renders):

```
python3 art-source/blender/items/cushion/export_cushion.py                    # exports/, about 1 s, ALL OK
python3 art-source/blender/items/cushion/render_cushion.py gamecam [outdir]   # also closeup | sheet
python3 art-source/blender/items/cushion/make_viewer.py [out.html]
python3 -m unittest discover -s art-source/blender/items/cushion/tests        # about 2 s
```

The export rewrites `exports/` byte for byte. The renders go to `out/` (git-ignored) and are not
byte-reproducible; the committed ones in `review/` were rendered from this folder's committed
build. The render scripts reuse the dogs' render helpers (`dogs/kit/dh_dog_render.py`) and read the
approved `exports/base-01-level1.glb` house, the picket fence exports and the Golden's
`dog_golden_default.gltf`; nothing shared was changed. The PNGs are saved by Pillow (IHDR / IDAT /
IEND only, no metadata chunks).

**The lying dog is a review stand-in.** The dogs have no lying clip yet (LieDown / RestIdle are
#933), so `render_cushion.py` poses the imported Golden's bones into a sphinx pose itself
(`POSE`, `TAIL`), only for these renders. Nothing about the dog is exported or changed.

## Open

- Where a resting dog's body goes on the model (the rest spot) and the import into Unity: #932.
- The real per-scene art budget: #799 and its follow-ups.
