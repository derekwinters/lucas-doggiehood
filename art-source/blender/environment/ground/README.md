# Lawn and lot ground (approved, #801, #834)

Derek approved the lawn ground, and it is kept here as **source assets only** (#834). This folder
holds just what the approved pick needs: one
lawn atlas, a macro tint and the concrete used for paving in the renders. It also holds the logic
for tinting one lot's lawn with mowed stripes. That is a concept only; the mowing feature is not
built.

Nothing here is imported by Unity, because the folder is outside `Assets/`.

## Derek's picks (2026-11-24)

- **Look C, crisp tufts.** Base colour `#5D983D`, crisp light tufts of 2–3 blades, strong normal.
- **Edge set 1 ("C1").** A flat concrete curb band level with the lawn, lawn flush with the
  sidewalk, walkway and driveway (paving 1 cm proud), and lawn right up to the fence posts.
- **Repeat hiding: (d) + (a), as recommended.** 8 shared-edge variants, one per 3 m world cell, in
  one 1024 × 512 atlas, plus the macro tint. See [Repeat hiding](#repeat-hiding-the-chosen-approach).

The ground is one lawn mesh per 60 m map tile, lying under everything, with paving drawn on top.
All UVs are world space, so nothing seams at tile or lot borders.

## Files

```
ground/
  dh_ground_tex.py   textures (seeded numpy + PIL); checks every atlas slot's shared edge band
  dh_ground.py       lawn materials (with optional mow stripes), per-tile lawn meshes, paving on
                     top, the starter-lot scene, the edge close-up scene
  dh_mow_stripes.py  the mow-stripe math, plain Python, with a self-check
  export_ground.py   one sample 60 m tile to exports/ (.gltf + .bin, ../textures URIs), read-back checks
  render_ground.py   the first round's comparison renders and viewer .glb files, to out/ (git-ignored)
  render_stripes.py  the mow-stripe renders (strengths), to out/ (git-ignored)
  render_stripe_patterns.py  the stripe patterns and the aliasing check, to out/
  textures/          lawn_c_cells8_{color,normal}.png, ground_macro.png, ground_concrete_{color,normal}.png
  exports/           ground_c_tile_rec.gltf/.bin + ground_stats.json
  review/            the approved review renders (see Review renders)
```

It imports the house kit and `dh_scene.py` (`../..`), the fences (`../fences`) and the
street-prop and tree helpers by relative path, and copies nothing.

## Repeat hiding: the chosen approach

Each tile's lawn is a grid of 3 m world cells (cut at the tile edge). Each cell's UV0 points into
one of 8 atlas slots, chosen when the mesh is built by a seeded integer hash of the cell's world
coordinates (`dh_ground.cell_variant`). A cell cut by a tile border gets the same variant and UVs
on both sides. UV1 = world position / 211 m carries the macro tint.

- **Atlas:** 1024 × 512, 4 × 2 slots of 256 px (a 224 px cell plus 16 px of wrap-around padding),
  75 px/m, colour and normal. Every variant shares the same edge band, so the padding is identical
  in every slot and coarse mip levels pull in matching colour. No bleeding was seen in any render.
  ASTC 6×6 with mipmaps: **about 0.30 MB colour + 0.30 MB normal**, which is also the most it
  takes on disk.
- **Macro:** a 256² multiplier map (19–53 m blobs, 211 m period). Its Unity home depends on the
  mow-stripe choice below: in the recommended lawn shader it is a 0.04 MB texture, otherwise
  vertex colour (the export carries it as `COLOR_0`).
- **Mesh:** 800 triangles and 1,600 vertices per 60 m tile, against 2 for a plain plane.
- The tile mesh depends on where the tile is, so the game builds it at run time (a Core hash plus
  a mesh builder). The export is a sample for checking, not a shared asset.

### Why this approach (first-round findings)

| Approach | Hides the period repeat? | Texture memory (ASTC 6×6, mips) | Lawn tris per tile |
|---|---|---|---|
| Plain tiling | No: a clear repeat at 30 m and far out | 1.19 MB | 2 |
| (a) macro only | No: the period shows through | 1.23 MB | 2 |
| (b) 4 shared-edge variants per period cell | Mostly: a faint regular pattern | 4.76 MB | 128–800 |
| (c) (b) + (a) | Mostly | 4.80 MB | 128–800 |
| (d) 8 / 12 / 16 light variants per 3 m cell | Yes, at every zoom | **0.60 MB at 8 @ 1024 × 512** | 800 |
| **(d) + (a), chosen** | Yes, and it reads naturally | 0.60 MB + 0.04 MB | 800 |

- 8, 12 and 16 variants look the same at every zoom. The only pattern left is a faint 3 m lattice
  visible with contrast boosted 3×, and it comes from the shared edge band, not the variant count.
- 1024² and 2048² atlases look the same at the game camera.
- (d) without the macro reads as one flat green. The macro gives back large-scale variation.
- The other looks (A, B), the other approaches and the other edge sets (raised curb, raised slabs,
  grass fringe, mulch strip, mow strip) are still in the scripts. Their textures were trimmed and
  are rebuilt on demand (see Regenerating).

## Mow stripes on one lot (concept and logic only)

A lot that has been mowed shows stripes running front to back, parallel to the house's side
walls, alternating lighter and darker than the unmowed yards around it. One number, **stripe
strength** from 0 to 1, controls them; 0 is exactly an unmowed lawn, so growing back is just
lowering it. A second per-lot setting, **pattern**, picks one of exactly four stripe directions
(Derek, 2026-11-24):

| Pattern | t | Seen from the street, the stripes run |
|---|---|---|
| `front-to-back` | 0° | from the street to the back, parallel to the house's side walls |
| `side-to-side` | 90° | across the lot, parallel to the house front |
| `diagonal-left` | +45° | toward the back and to the left |
| `diagonal-right` | −45° | toward the back and to the right |

"Left" and "right" assume the lot frame's v points from the street to the back of the lot, as it
does for the starter lot. Core sets the frame from the house's facing.

**The rule** (`dh_mow_stripes.py`): a colour multiplier on the lawn at each world point p,

```
mult(p) = 1 + strength * AMP_RGB * wave(s) * mask(u, v)
s       = u cos(t) + v sin(t)       t from the pattern table above
wave(s) = clamp(6 * sin(pi * s / STRIPE_W), -1, 1)
```

- u, v are p in the lot's frame, measured from the centre of the lot's lawn rectangle: u across
  the lot (along the house front), v front to back.
- s is the distance measured **perpendicular to the stripes**, so a stripe is 0.75 m wide in every
  pattern, both diagonals included. It is not stretched to √2 × 0.75 m along the lot's axes.
- `wave` is a soft square wave, +1 on one stripe and −1 on the next, with an 8 cm transition. It
  is odd in s, and the lawn rectangle is symmetric through its centre (turning u, v into −u, −v
  flips s), so it averages to exactly 0 over the lot for all four patterns. The light stripes are as much lighter
  as the dark ones are darker, and **the lot's average colour stays that of an unmowed lawn.**
- `mask` is 1 inside the lot's lawn rectangle and 0 outside, with a 5 cm soft edge. Outside,
  `mult` is exactly 1, so **paving, verge and neighbouring lots never change.**
- The lawn rectangle is the lot's quadrant with each road-side edge pulled in by 5.75 m (Core's
  `LotBounds.StreetCorridorInset`: half road + verge + sidewalk), so stripes stop at the sidewalk's
  outer edge and at the lot line to a neighbour. The house, paving and fence sit on top, so stripes
  under them never show.

**Numbers chosen:**

| | Value | Why |
|---|---|---|
| Contrast | **1.5 × the first round** | Derek, 2026-11-24: "make the lines like 50% darker". Both sides scale by 1.5 so the stripes stay balanced around the unmowed colour: lighter and darker than the neighbours, not only darker. The first round was ±7% (×1.075 / 1.065 / 1.11 light, ×0.925 / 0.935 / 0.89 dark). |
| Stripe width | **0.75 m** | One pass of a 21-inch walk-behind mower with overlap. In the renders a stripe is about 22 px wide in the one-lot view and 12 px in the whole-tile view (measured): clearly stripes, not noise. Only 0.75 m was rendered; 0.5 m would be about 8 px at tile zoom, close to the tuft texture's scale, and 1 m (a riding mower) is the other sensible value. |
| Light stripe | ×1.1125 R, ×1.0975 G, ×1.165 B | About 11% brighter, and a little paler, like blades bent away from the viewer that show their light undersides and catch the sky. |
| Dark stripe | ×0.8875 R, ×0.9025 G, ×0.835 B | About 11% darker and a little richer, like blades bent toward the viewer, showing their shaded tips. |

Real mow stripes are not paint: they are light catching blades the mower bent one way or the
other, so they swap light and dark as you walk around the lawn. The game camera's yaw is fixed, so
a fixed tint is a fair stand-in.

### How to build it in Unity: three options

| | (1) Lawn shader with per-lot parameters (**recommended**) | (2) Baked vertex colours | (3) Small per-lot mask texture |
|---|---|---|---|
| What it is | The lawn Shader Graph computes `mult` per pixel from world position and up to 4 lots' parameters | The tile mesh gets extra vertex columns at every stripe edge and the stripe tint is written into vertex colours | A tiny stripe texture per mowed lot, sampled in lot-local coordinates |
| Extra tris | 0 | about +1,200 per striped lot for front-to-back or side-to-side (65 vertex columns × 9 rows of 3 m cells); more for either diagonal, whose stripe edges cut every cell at 45° | 0 |
| Extra textures | 0 | 0 | one small texture per mowed lot |
| Changing strength or pattern | set one float or vector, free | rewrite the colours and re-upload the mesh | set one float |
| Edges | anti-aliased, can fade out when stripes get thinner than a pixel | hard-edged and aliased unless more columns are added | soft, depends on texture size |
| Unity needs | the project's first custom shader: one URP Lit Shader Graph for the lawn (atlas on UV0, macro, stripe sub-graph on absolute world position); a per-tile `MaterialPropertyBlock` with up to 4 lots, each two `Vector4`s (the lawn rect's centre and half sizes on the world axes; the stripe normal from the pattern, stripe width, strength), set by a thin MonoBehaviour from Core lot state. Core turns the pattern into the stripe normal, so the shader has no notion of patterns | a material that reads vertex colour (URP Lit does not; glTFast's do, or a custom shader); a CPU recolour path in the tile-mesh builder | a custom shader anyway, because URP Lit cannot place a texture in lot-local space |
| Fit with #776 (importer) | Independent of the importer: the lawn mesh is built at run time, not imported. The same shader can sample the macro too, so the macro no longer depends on vertex colour or on glTFast's materials | Ties the lawn to a vertex-colour-reading material, so to glTFast's shaders or a custom one | Independent of the importer, but needs the custom shader of (1) plus textures |

**Recommendation: (1).** It costs no triangles and no textures, changes strength for free, gives
clean edges at every zoom, and keeps the logic small enough to mirror in Core and test.
(3) needs the same custom shader as (1) and adds textures, so it is strictly worse. (2) avoids a
shader only if the importer's material reads vertex colour, and it costs triangles and CPU work.

This revises the first round's plan: the macro was going to be vertex colour so the lawn could
stay on stock URP Lit. With a lawn shader for the stripes, the macro is better as a 0.04 MB texture
in that shader, and the tile mesh no longer needs `COLOR_0`.

A URP Decal Projector over the lot is a fourth way that avoids a custom lawn shader. It was not
tried; it needs the decal renderer feature and adds overdraw per mowed lot.

### The patterns at the fixed game camera (aliasing check)

`render_stripe_patterns.py` measures each pattern's stripes on screen. Each figure comes from the
2-D spectrum of the striped image minus the unmowed one, and the angle was confirmed with a
gradient structure tensor. It also renders each view a second time with **no anti-aliasing**
(EEVEE at 1 sample) and reports the alias error: how far the stripe image drifts from the
24-sample one, as a fraction of the stripe signal.

| Pattern | Angle on screen (from horizontal) | Stripe width on screen: lot / tile / 180 m | Alias error, no AA: lot / tile / 180 m |
|---|---|---|---|
| front-to-back | 40° | 22 / 12 / 3.9 px | 0.06 / 0.08 / 0.20 |
| side-to-side | 149° | 20 / 11 / 3.7 px | 0.06 / 0.08 / 0.20 |
| diagonal-left (rendered as "diagonal") | **4°**, almost horizontal | 18 / 10 / **3.3 px** | 0.06 / 0.09 / **0.23** |
| diagonal-right (derived, not rendered) | **98°**, almost vertical | 26 / 14 / 4.7 px | not measured |

The renders were made when there was one diagonal, named `diagonal`, which is today's
`diagonal-left`. `diagonal-right` is its mirror image and was not rendered. Its row is derived:
the orthographic camera maps ground directions to the screen linearly, so the front-to-back and
side-to-side measurements fix that mapping. The mapping predicts diagonal-left at 3.0–3.6° and
18.5 / 10.1 / 3.3 px, against the measured 3.6–4.2° and 18.3 / 10.0 / 3.3 px.

What this shows:

- **At −40° yaw diagonal-left lands almost parallel to the pixel rows.** It runs about 4° off
  horizontal, roughly halfway between the lot's two screen directions (40° and 149°). It is the
  near-horizontal, and a near-horizontal edge is the worst case for stair-stepping: each
  step is about 1 / tan 4° ≈ 14 px long. Those long steps are what crawl when the camera pans
  vertically. **Diagonal-right is about 8° off vertical**, the same risk turned on its side: steps
  of about 1 / tan 8° ≈ 7 px that would crawl on a sideways pan.
- **Diagonal-left is also the narrowest on screen.** Near-horizontal stripes are measured
  vertically, where the 45° pitch foreshortens most, so it reaches 2 px per stripe first: at about
  a 300 m wide view, against about 350 m for front-to-back and side-to-side. Diagonal-right is
  the widest on screen, because near-vertical stripes are measured horizontally where nothing is
  foreshortened, so it reaches 2 px last, at about 420 m.
- **In still frames the difference is small.** At lot and tile zoom the three rendered patterns alias about the
  same (6–9% error without AA). At 180 m every pattern aliases noticeably (20–23%), and the
  diagonal-left most. The `patterns-alias-far-*.png` crops show clean but stepped edges, with no moiré.
- **Not measured:** crawl while the camera moves, and a real mobile GPU. A still frame cannot show
  either. The mitigation is the same for every pattern and matters most for the diagonals:
  the shader fades the stripes by screen-space derivative (`fwidth` of s) as a stripe approaches
  2 px, and uses `fwidth` to soften the square wave's edge to at least a pixel. Test this on a
  device before shipping.

### Review renders (review/, committed)

The approved renders, Blender's and PIL's own PNG output with no metadata chunks:

- `compare-looks.png`: the three looks (A, B, C) on a lot and far out. C was picked.
- `far-c-chosen.png`: look C with the chosen approach, 3 × 3 tiles (180 m): no repeat, no tile
  border. It was rendered as `far-c-cells1k8_macro`, whose 8 slots are byte for byte the
  `lawn_c_cells8` atlas, with Blender's metadata chunks removed and the pixels unchanged. Look C
  had no mid-zoom render.
- `compare-edges-street.png`, `compare-edges-fence.png`: edge sets 1–3 for the three looks. Set 1
  was picked.
- `stripes-compare.png`: stripe strength 1.0, 0.5 and 0.0 (front-to-back).
- `patterns-compare.png`: front-to-back, side-to-side and diagonal-left at strength 1.0.

### Stripe renders (out/, git-ignored)

- `stripes-lot-s100/s050/s000.png`: the striped lot at the game camera, at strength 1.0, 0.5, 0.0.
- `stripes-tile-s100/s050/s000.png`: the whole starter tile (60 m, four quadrants), same strengths.
- `stripes-close-neighbour.png`: an 8 m close-up on the lot line to the neighbouring lot; red pegs
  stand on the line.
- `stripes-close-sidewalk.png`: an 8 m close-up where the lot meets the sidewalk and walkway.
- `stripes-far.png`: 3 × 3 tiles (180 m) at strength 1.0.
- `stripes-diff.png`: |strength 1 − strength 0| × 8, showing every pixel the stripes changed.
- `stripes-compare.png`: lot and tile views at the three strengths side by side.
- `stripes_stats.json`: the lot rectangle, changed-pixel count and mean colour.
- `patterns-lot-<pattern>.png`, `patterns-tile-<pattern>.png`: each pattern at strength 1.0.
- `patterns-compare.png`: front-to-back, side-to-side and diagonal-left side by side, lot and tile.
- `patterns-close-diagonal.png`: an 8 m close-up of diagonal-left at the lot line to the
  neighbouring lot; red pegs stand on the line.
- `patterns-alias-far-<pattern>.png`: the 180 m view with no anti-aliasing, stripes only, ×8,
  enlarged 2× with nearest-neighbour.
- `patterns_stats.json`: the screen angle, width and alias error per pattern and view.

Results: the diff image shows change only inside the lot's lawn (none on paving, verge, house or
the three neighbouring quadrants). Over the changed pixels the mean colour is (108.56, 175.46,
71.15) at strength 1, (108.69, 175.63, 71.31) at 0.5 and (108.74, 175.69, 71.38) at 0, so the
average stays the same within 0.23 of 255. At 180 m the stripes are faint but visible. Those
numbers are for front-to-back. The other patterns were checked for balance and confinement by
the self-check. Side-to-side and diagonal-left were also checked by eye in the renders: the
diagonal close-up stops at the pegs on the lot line.

## Sizes

- **Textures:** `lawn_c_cells8` 1024 × 512 colour + normal, `ground_macro` 256², `ground_concrete`
  512² colour + normal (render-only paving).
- **Periods:** cells 3 m, macro 211 m, concrete 1 m.
- **Exported sample tile:** 800 tris, 1,600 vertices, `TEXCOORD_1` and `COLOR_0`.
- **Review renders:** 6 PNGs, about 10 MB.
- **This folder:** about 11.5 MB, of which textures 1.2 MB, exports 0.1 MB, and scripts plus
  README the rest (excluding the git-ignored `out/` and `__pycache__/`).

## Regenerating

```
pip install bpy==5.3.0                    # plus Pillow, numpy; renders need libegl1 + libgl1-mesa-dri
python3 art-source/blender/environment/ground/dh_ground_tex.py      # the kept textures + shared-edge check
python3 art-source/blender/environment/ground/dh_ground_tex.py --all  # also every comparison texture
python3 art-source/blender/environment/ground/export_ground.py      # exports/ + read-back checks
python3 art-source/blender/environment/ground/dh_mow_stripes.py     # stripe self-check
python3 art-source/blender/environment/ground/render_stripes.py [OUT] [--samples 24]
python3 art-source/blender/environment/ground/render_stripe_patterns.py [OUT] [--samples 24]
python3 art-source/blender/environment/ground/render_ground.py [OUT] [--looks a,b,c] [--approaches ...]
        [--views far,mid,lot,border,period,edges,sheets,glb] [--samples 24] [--glb rec]
```

The texture and export scripts each gave byte-identical results when run twice. `render_ground.py`
builds any comparison texture it needs into `textures/` on demand; delete those again before
committing. Renders are not byte-reproducible.

## Still open

- **The mowing feature itself:** when a lot gets mowed, how fast stripes grow back, and the
  stripe-strength numbers in Core. This folder only sets the look and the logic.
- **Stripe direction on other lots.** The patterns are relative to the house: front-to-back
  follows its side walls. A lot whose house faces a side road needs its frame from the house's
  facing, which Core already knows.
- **Verge and open space.** With one lawn plane under everything, they are the same lawn.
- **The driveway.** It is dh_scene's proposal, and the driveway itself is still open (#803).
- **Unity:** the importer (#776), the lawn shader, a run-time tile-mesh builder in Core, and the
  art budget (#799).
