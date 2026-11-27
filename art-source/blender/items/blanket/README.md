# Blanket (comfort decoration, #413)

A soft wool plaid throw lying rumpled on the grass. It has fringed short ends, and its front-right corner
is folded back so the cream fleece underside shows. It replaces the blue graybox slab that a delivered
blanket shows as today (`DecorationView`). This folder holds **source assets only**. Nothing here is under
`Assets/`, and the game does not use it yet. Showing it in the game is #932. The model waits on Derek's
approval of the review renders on #413.

It follows [Blender Asset Pipeline](../../../../docs/engineering/blender-pipeline.md): it is built from
named dimensions by a script with no hand modelling, it exports as glTF 2.0, and two runs give the same bytes.

## Files

```
blanket/
  dh_blanket_tex.py   the three textures (numpy + PIL, fixed seeds)
  dh_blanket.py       the model: DIMS as named constants, build(tex) -> the "blanket" object
  export_blanket.py   textures + exports/blanket.glb + blanket_stats.json, then read-back checks
  render_blanket.py   review renders to out/ (git-ignored)
  make_viewer.py      out/blanket-viewer.html: a one-file three.js viewer with the .glb inlined
  tests/test_blanket.py
  textures/           blanket_plaid_color.png, blanket_weave_normal.png, blanket_back_color.png
  exports/            blanket.glb (self-contained, 312 KB, textures embedded), blanket_stats.json
  review/             the review renders (below)
```

## Numbers

| | |
|---|---|
| Overall size (fringe included) | **1.372 × 0.921 × 0.071 m** (X across × Y deep × Z up) |
| Cloth | 1.24 × 0.92 m, 12 mm thick, fringe 6.6 cm past each short end |
| Footprint it must fit | 1.4 × 1.0 m, today's graybox slab (`DecorationView`). It clears that by 1.4 cm a side across and 4 cm a side deep |
| Height | 0 at the ground. The tallest ridge peaks at 7.1 cm and the folded corner stands about 5.2 cm. The slab it replaces was 0.35 m tall |
| Triangles | **3,428**: cloth top and fleece underside 2,852, cut edge 216, fringe 360 |
| Budget | Under the provisional 6,000-triangle per-mesh backstop (#799). It is **over** the 2,000 cap that `ArtBudget.MaxTrianglesPerMesh` still enforces until #799 lands, like most approved Blender models |
| Materials | `DH_Item_blanket_plaid` (colour + weave normal), `DH_Item_blanket_back` (fleece colour), both single-sided, and `DH_Item_blanket_fringe` (the plaid texture, double-sided because the tassels are flat strips) |
| Textures | 512² plaid colour, 512² weave normal, 256² fleece colour |
| Pivot | On the ground at the centre of the footprint, the same point as the slab's centre |
| Axes | Blender front −Y, up +Z. The `.glb` is Y-up (`export_yup`) |

`exports/blanket_stats.json` holds the measured values and the result of each check.

The houses and the package (`../package/`) export a self-contained `.glb` with embedded textures, and
so does this, rather than using the `.gltf` + shared `textures/` layout of the trees and street props.
It is one small model with its own textures, and the same file loads in a web viewer. The PNGs in
`textures/` are the generator's output, kept so a rebuild can be compared byte for byte.

## How it's built

`dh_blanket.py` lays a flat grid over the cloth with UVs in metres (one plaid repeat is 0.32 m). It lifts
each point by a few soft ridges where the throw has bunched, plus a low ripple. It rolls the corner past
the fold line over a half cylinder (radius 2.6 cm) so the corner lies upside down on top. A Solidify
modifier then gives the cloth its 12 mm thickness: the plaid stays on top, and the fleece goes on the
underside and the cut edge. The modifier is applied before export. The tassels are tapered strips lying
on the grass, folded with the corner where they hang off it. Their small splay comes from a fixed seed.

The plaid is a woven 2/2 twill tartan (navy and mid blue, cream and mustard lines). Where a warp stripe
crosses a weft stripe, the colours mix thread by thread, the way a real plaid does. Mid blue echoes the
graybox blanket's `#3FA7D6`.

## Checks and tests

`export_blanket.py` reads its own `.glb` back and **exits non-zero** if any of these fail:

- Every vertex is inside the 1.4 × 1.0 m footprint, centred on the pivot.
- The lowest vertex is on the ground (within 1 mm) and nothing is taller than 0.10 m.
- Triangles are at or under 6,000.
- Every image is embedded, and the file has a single embedded buffer, so the `.glb` stands alone.
- Plaid and back are single-sided and opaque, and the fringe is double-sided.

`python3 -m unittest discover -s art-source/blender/items/blanket/tests -v` runs the same footprint,
ground, pivot, budget and embedding checks on the **committed** `.glb` without Blender. With `bpy` it also
rebuilds the set twice in fresh processes and requires every output to match the committed bytes.

> **Invariant — the blanket never leaves the slab footprint.** Every vertex of the export lies inside the
> 1.4 × 1.0 m rectangle centred on its pivot, on or above the ground. That way the blanket goes where the
> slab went, and placement does not change.

## Regenerating

From the repository root, with `pip install bpy==5.3.0` plus numpy and Pillow:

```
python3 art-source/blender/items/blanket/export_blanket.py                 # textures + exports + checks, ~2 s
python3 art-source/blender/items/blanket/render_blanket.py [out_dir] [--only yard,close,compare,footprint,sheet]
python3 art-source/blender/items/blanket/make_viewer.py [out_dir]          # no Blender needed
```

Two export runs give byte-identical textures, `.glb` and stats. The test above checks this, and #413
records the hashes. Renders are not byte-reproducible. Without a GPU, the render script uses Mesa's
lavapipe (`/opt/lavapipe`) as the dog renders do, and it needs the committed dog kit, base 1 and the
lawn set.

## Review renders (`review/`)

| File | What it shows |
|---|---|
| `blanket-yard.png` | Game camera (orthographic, pitch 45, yaw −40) on base 1's front yard on the approved lawn, with the approved adult Golden Retriever sitting on the blanket |
| `blanket-yard-close.png` | The same camera, zoomed in on the dog and the blanket |
| `blanket-closeup.png` | A low 3/4 perspective close-up: plaid, fringe, the folded corner and the fleece underside |
| `blanket-compare.png` | At the game camera: today's bed, cushion and blanket slabs beside the new blanket |
| `blanket-footprint.png` | Straight down, with the 1.4 × 1.0 m slab footprint drawn in white |
| `blanket-sheet.png` | All of the above on one sheet (full size) |

To keep the folder small, the single views are stored 1,000 px wide. The sheet is stored at full size.

## Open concerns

- **The dog sits rather than lies.** The spec's "Rest" pose is lying down
  ([Dog Behavior → Animations](../../../../docs/specs/dogs/behavior.md#animations)), but the dog kit has no
  lying pose or clip yet. The renders use the approved sit pose.
- **A large adult dog lying flat is longer than the blanket.** The Golden is 1.49 m nose to rump, and the
  footprint is only 1.4 m long. When a Rest pose exists, a big dog lying on it will overhang the ends.
  That looks natural for a throw, but it is a decision for the Rest pose work, not this model.
- **There is no bed or cushion model to compare against yet** (#411, #412). The comparison uses their
  graybox slabs.
- **There is no rest-spot empty.** #932 lines the dog's rest spot up with where the dog rests today, which
  is the decoration's position, and that is this model's pivot. No `ATTACH_*` empty was added because no
  spec asks for one.
