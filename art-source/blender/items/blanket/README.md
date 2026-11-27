# Blanket (comfort decoration, #413)

A soft wool plaid throw lying gently rumpled on the grass. It has a short fringe on its two short
ends and a cream fleece underside, and it comes in four colourways. It is **2.0 × 1.4 m**, big enough
for an adult Golden Retriever lying flat. It replaces the blue graybox slab that a delivered blanket
shows as today (`DecorationView`). This folder holds **source assets only**. Nothing here is under
`Assets/`, and the game does not use it yet. Showing it in the game is #932. The model waits on
Derek's approval of the review renders on #413.

**Round 2 (Derek, 2026-11-27).** Derek asked for a bigger, simpler blanket with several colours:

- The cloth went from 1.24 × 0.92 m to 2.0 × 1.4 m.
- The folded-back corner is gone, the rumples are gentler, and the fringe is one quad per tassel.
- Triangles fell from 3,428 to 2,080.
- There are now four colourways.

The model follows [Blender Asset Pipeline](../../../../docs/engineering/blender-pipeline.md). Scripts
build it from named dimensions with no hand modelling, it exports as glTF 2.0, and two runs produce the
same bytes.

## Files

```
blanket/
  blanket_spec.py     THE SIZE AND THE COLOURWAYS, in one place (plain Python, no Blender)
  dh_blanket_tex.py   the textures: one plaid per colourway, the shared weave normal and fleece (numpy + PIL, fixed seeds)
  dh_blanket.py       the model: build(tex, colourway) -> the "blanket" object
  export_blanket.py   textures + one .glb per colourway + blanket_stats.json, then read-back checks
  render_blanket.py   review renders to out/ (git-ignored)
  make_viewer.py      out/blanket-viewer.html: a one-file three.js viewer, all colourways inlined, with a switcher
  tests/test_blanket.py
  textures/           blanket_plaid_<colourway>_color.png (4), blanket_weave_normal.png, blanket_back_color.png
  exports/            blanket_<colourway>.glb (4, self-contained), blanket_stats.json
  review/             the review renders (below)
```

**Changing the size.** Edit `CLOTH_W` / `CLOTH_D` in `blanket_spec.py` and run the export. The model,
the rumples, the fringe count, the checks, the tests, the renders and the viewer all read the size
from there. The tests also require the cloth to be at least the approved Golden's measured
nose-to-rump length and width (`dogs/golden-retriever/exports/dog_stats.json`).

## Numbers

| | |
|---|---|
| Cloth | **2.0 × 1.4 m**, 12 mm thick |
| Placement footprint | **2.12 × 1.40 m**: the cloth plus a 6 cm fringe past each short end, centred on the pivot |
| Height | 0 at the ground, at most 3.6 cm (the gentle rumples) |
| Triangles | **2,080** in every colourway: cloth top and fleece underside 1,800 (25 × 18 quads each), cut edge 172, fringe 108 (27 one-quad tassels per end) |
| Budget | Under the provisional 6,000-triangle per-mesh backstop (#799). It is **over** the 2,000 cap that `ArtBudget.MaxTrianglesPerMesh` still enforces until #799 lands, as most approved Blender models are |
| Materials | `DH_Item_blanket_plaid_<Colourway>` (colour + shared weave normal, single-sided), `DH_Item_blanket_back` (fleece, single-sided, the same in every colourway) and `DH_Item_blanket_fringe_<Colourway>` (the plaid texture, double-sided because the tassels are flat strips) |
| Textures | 512² plaid colour per colourway, 512² weave normal, 256² fleece colour |
| File size | about 300 KB per `.glb` |
| Pivot | On the ground at the centre of the footprint |
| Axes | Blender front −Y, up +Z. The `.glb` files are Y-up (`export_yup`) |

`exports/blanket_stats.json` holds the measured values per colourway and the result of every check.

**The game needs a larger footprint (#932).** Today's graybox slab is 1.4 × 1.0 m. The pool's clearance
around a decoration (`PoolPlacement.DecorationFootprintRadius` = 0.8602 m) is half that slab's diagonal.
The blanket's footprint is 2.12 × 1.40 m, so its half-diagonal is about 1.27 m. Placing it is #932's job,
and #932 has to reserve this footprint instead of the slab's.

## Colourways

| Key | Label | Ground / block / line / accent | File |
|---|---|---|---|
| `navy` (default) | Navy & blue | `#22385E` / `#3D78AE` / `#E6D9BC` / `#C8963A` | `blanket_navy.glb` |
| `red` | Red & green tartan | `#A3272B` / `#24503A` / `#E9D9A6` / `#1E2C4A` | `blanket_red.glb` |
| `green` | Green & cream | `#2C6A2E` / `#8DB25E` / `#EDE3CA` / `#C9B27A` | `blanket_green.glb` |
| `purple` | Grey & purple | `#7F7C86` / `#5E3A7A` / `#E6E1EA` / `#B79AD6` | `blanket_purple.glb` |

Every colourway weaves the same sett (a dominant ground, a second block colour, a light line and a
thin accent line) as a 2/2 twill with a faint brushed-wool mottle. Only the colours change.

**How the variants are exported:** one self-contained `.glb` per colourway, the way the dogs export one
file per coat (`dog_golden_default`, `_light`, `_dark` with `DH_Dog_Fur_<Coat>`). Every file carries
the identical mesh, and the tests check the vertex positions match. Only the plaid and fringe
materials and their texture differ. So #932 can either load the picked colourway's file, or load one
file and swap the plaid texture on its material. Both give the same result.

**The random pick is game code, not this asset.** Derek wants a gifted blanket to get one colourway at
random when it is delivered. This folder only provides the four colourways. Picking one, and keeping it
for that blanket, is #932's job.

## How it's built

`dh_blanket.py` builds the cloth in five steps:

1. Lay a flat grid over the cloth, about 8 cm per quad. The UVs are in metres, and one plaid repeat is
   0.32 m.
2. Lift each point by a few soft, low ridges plus a gentle ripple. The ridges sit at fractions of the
   cloth, so they follow a size change. They fade out near the edges, so the hem lies on the grass.
3. Give the cloth its 12 mm thickness with a Solidify modifier. The plaid stays on top, and the fleece
   goes on the underside and the cut edge. The modifier is applied before export.
4. Pin the cut edge back onto the cloth's outline, because Solidify leans it out a fraction of a
   millimetre where a rumple meets the hem.
5. Add the tassels. Each is one tapered quad lying on the grass. A fixed seed sets their small splay,
   and their tips stop exactly at the footprint edge.

## Checks and tests

`export_blanket.py` reads every `.glb` it wrote back and **exits non-zero** if any of these fail:

- Every vertex is inside the 2.12 × 1.40 m footprint, centred on the pivot.
- The lowest vertex is on the ground (within 1 mm), and nothing is taller than 0.10 m.
- Triangles are at or under 6,000.
- Every image is embedded, and the file has a single embedded buffer.
- The plaid and back materials are single-sided and opaque, and the fringe is double-sided.
- Every colourway has the same triangle count and size.

`python3 -m unittest discover -s art-source/blender/items/blanket/tests -v` runs on the committed files,
without Blender. It checks that:

- Every vertex is inside the footprint, and the blanket fills it.
- An adult Golden lying flat fits on the cloth.
- The pivot is centred and the blanket lies flat on the ground.
- Every file is within the triangle budget, has stats, and embeds its textures.
- There are four colourways with navy first, and each has its own `.glb`, texture, material name and
  stats entry.
- Every colourway carries the same mesh.
- Each plaid's dominant colour (its average, which is what the game camera sees once the texture is
  mipmapped) is at least 60 RGB units from every other colourway's.

With `bpy` installed, the tests also rebuild the whole set twice in fresh processes and require every
output to match the committed bytes.

> **Invariant — the blanket never leaves its placement footprint.** Every vertex of every colourway's
> export lies inside the `FOOTPRINT_W × FOOTPRINT_D` rectangle (2.12 × 1.40 m) centred on its pivot, on
> or above the ground.

## Regenerating

From the repository root, with `pip install bpy==5.3.0` plus numpy and Pillow:

```
python3 art-source/blender/items/blanket/export_blanket.py                 # textures + 4 exports + checks, a few seconds
python3 art-source/blender/items/blanket/render_blanket.py [out_dir] [--only yard,close,compare,footprint,colourways,sheet]
python3 art-source/blender/items/blanket/make_viewer.py [out_dir]          # no Blender needed
```

Two export runs give byte-identical textures, `.glb` files and stats. The test above checks this.
Renders are not byte-reproducible. Without a GPU, the render script uses Mesa's lavapipe
(`/opt/lavapipe`) as the dog renders do. It needs the committed dog kit, base 1 and the lawn set.

## Review renders (`review/`)

| File | What it shows |
|---|---|
| `blanket-yard.png` | Game camera (orthographic, pitch 45, yaw −40) on base 1's front yard on the approved lawn, with the approved adult Golden Retriever on the navy blanket |
| `blanket-yard-close.png` | The same camera, zoomed in on the dog and the blanket |
| `blanket-closeup.png` | A low 3/4 perspective close-up: plaid, fringe and fleece edge |
| `blanket-colourways.png` | The four colourways side by side at the game camera |
| `blanket-compare.png` | At the game camera: today's bed, cushion and blanket slabs beside the new blanket |
| `blanket-footprint.png` | Straight down: the new footprint (white), today's 1.4 × 1.0 m slab (orange) and an adult Golden lying flat, 1.49 × 0.74 m (red) |
| `blanket-sheet.png` | All of the above on one sheet (full size) |

To keep the folder small, the single views are stored 1,000 px wide. The sheet is stored at full size.

## Open concerns

- **The dog sits rather than lies.** The spec's "Rest" pose is lying down
  ([Dog Behavior → Animations](../../../../docs/specs/dogs/behavior.md#animations)), but the dog kit has
  no lying pose or clip yet. The renders use the approved sit pose. The footprint render shows that a
  lying adult Golden fits.
- **There is no bed or cushion model to compare against yet** (#411, #412). The comparison uses their
  graybox slabs.
- **There is no rest-spot empty.** The dog rests at the decoration's position, which is this model's
  pivot.
