# Textured trees (approved, round 3)

Derek approved these textured tree concepts on 2026-11-22, and #816 locked them in here as **source assets only**, the same way #814 did for the houses. Nothing in this folder is imported by Unity (it is outside `Assets/`), and the game does not use any of it yet. The tree model issue (#804) starts from these files.

Each crown is a few hundred alpha-cut leaf cards carrying procedurally painted leaf sprigs, on a light branch skeleton with a textured, normal-mapped bark. The large trees come in at **1.6–4.7k triangles, against 26–28k in round 2**, and they read better: real leaf shapes, a ragged edge with light coming through, and dappled shadows.

## Regenerating

Run from the repository root with headless Blender as a Python module (`pip install bpy==5.3.0`), plus numpy and Pillow. The scripts import the house kit (`dh_house_kit.py`, `dh_scene.py`, the base houses) from `art-source/blender/`, two folders up; there is no separate copy.

- **Exports:** `python3 art-source/blender/environment/trees/export_trees_tex.py art-source/blender/environment/trees/exports` rewrites the 10 `.gltf` + `.bin` pairs and `trees_textured_stats.json` in a few seconds, and checks every leaf material (see "glTF check" below). Two runs produce byte-identical files. Export into `exports/` (a sibling of `textures/`): the texture URIs are relative, and the check fails if they don't resolve into `textures/`.
- **Textures:** `python3 art-source/blender/environment/trees/dh_tree_tex.py` rewrites the 12 PNGs in `textures/`. This is a slow step; the exporter only calls it when the atlases are missing, so normally it uses the committed ones. A regeneration reproduced the committed PNGs byte for byte.
- **Renders:** `render_tree_closeup_tex.py <out.png>`, `render_compare.py <out.png>`, `DH_SAMPLER_LAYOUT=4,9,12 render_samplers_tex.py <out.png>` and `render_street_tex.py <dressed|closeup> <out.png>`, each run with `python3` from this folder's path. Write their output under `out/` (git-ignored). The lawn, mulch and sign textures the street scenes make on demand also go to `out/env-textures/`. The street scenes paint houses through the house kit, which bakes any missing tinted roof texture into `art-source/blender/textures/`; don't commit those.

## Review renders (`review/`)

| File | What |
|---|---|
| `tree-closeup.png` | The large maple and large cherry, at the same framing as round 2's close-up. |
| `tree-closeup-compare.png` | The round 1, round 2 and textured maple side by side, labelled with triangle counts (637 / 27,578 / 4,740). |
| `sampler-trees.png` | All 10 textured trees with the dog, at the game camera (yaw −40°, pitch 45°, orthographic). |
| `dressed-street-gamecam.png` | The round 2 dressed-street scene with **only the trees swapped** (beds, shrubs and props are still the round 2 models), at the game camera on the corner lot. |

In the street scene the 14 trees now cost **35,519 triangles, down from about 250,650 in round 2**. The rest of the round 2 dressing is unchanged at 110,974.

## Trees (`exports/`)

| File | Tree | Tris | of which leaf cards | Round 2 tris | Size X×Y×Z (m) | `.bin` |
|---|---|---|---|---|---|---|
| `tree-maple-large.gltf` | Maple (large) | **4,740** | 3,040 | 27,578 | 7.7 x 7.5 x 8.6 | 329 KB |
| `tree-maple-small.gltf` | Maple (young) | **3,460** | 1,760 | 23,018 | 4.6 x 4.7 x 5.4 | 242 KB |
| `tree-cherry-large.gltf` | Cherry (large) | **4,256** | 2,156 | 26,852 | 8.0 x 8.2 x 5.7 | 297 KB |
| `tree-cherry-small.gltf` | Cherry (young) | **2,900** | 800 | 23,652 | 4.8 x 4.5 x 3.5 | 204 KB |
| `tree-spruce-large.gltf` | Spruce (large) | **1,593** | 1,494 | 25,416 | 6.2 x 6.2 x 10.4 | 109 KB |
| `tree-spruce-small.gltf` | Spruce (young) | **1,233** | 1,134 | 19,224 | 3.4 x 3.4 x 5.7 | 85 KB |
| `tree-columnar-large.gltf` | Columnar (large) | **1,760** | 1,660 | 11,406 | 2.8 x 2.9 x 10.1 | 120 KB |
| `tree-columnar-small.gltf` | Columnar (young) | **760** | 660 | 9,934 | 1.7 x 1.9 x 6.2 | 52 KB |
| `tree-street.gltf` | Street tree | **2,961** | 1,760 | 15,116 (incl. curb strip) | 5.7 x 5.7 x 7.0 | 206 KB |
| `tree-japanese-maple.gltf` | Japanese maple (yard) | **2,365** | 1,264 | 11,646 | 5.1 x 4.9 x 2.9 | 165 KB |

- **Large trees** fall between 1.6k and 4.7k triangles, inside the 3–8k target or below it.
- **The spruce is the cheapest** large tree (1.6k), because each branch is just two crossed needle cards.
- **Branch skeletons** take about 1–1.7k of the broadleaf totals. They could drop further: the inner limbs are mostly hidden.
- **Textures are stored once.** Each tree is a `.gltf` (JSON) plus a `.bin` (geometry only, 52–329 KB). Images are not embedded: every `.gltf` references the shared atlas and bark maps by relative URI (`../textures/<name>.png`), so the whole set carries one copy of each of the 12 textures (about 13 MB) instead of a copy per tree (the earlier GLBs were 2.8–3.4 MB each, almost all embedded textures). In Unity, keep sharing them across trees and compress them (ASTC/ETC2). The importer has to resolve the external URIs (a `.gltf` importer such as glTFast does), with `textures/` copied alongside.

## Textures (`textures/`)

All textures are generated procedurally by `dh_tree_tex.py`, using numpy and PIL with no external images.

| File | Size | What |
|---|---|---|
| `maple_leaf_atlas.png` | 1024² RGBA | 4 sprigs, 22–28 leaves each: palmate 5-lobed leaves with a midrib and lobe veins, a two-tone light half, per-leaf colour jitter, on brown twigs. |
| `street_leaf_atlas.png` | 1024² RGBA | 4 sprigs of small serrated ovate leaves in lighter greens. A first try with pinnate, honey-locust-style leaflets read as "shredded paper" at the game camera, so I replaced it. |
| `column_leaf_atlas.png` | 1024² RGBA | 4 dense sprigs of small dark ovate leaves (hornbeam). |
| `jmaple_leaf_atlas.png` | 1024² RGBA | 4 sprigs of 7-lobed Japanese maple leaves in reds. |
| `cherry_blossom_atlas.png` | 1024² RGBA | 4 blossom sprigs, about 90 five-petal flowers each: notched petals in 5 pinks, a dark pink eye with yellow stamens, and a few bronze young leaves. |
| `spruce_needle_atlas.png` | 1024² RGBA | 4 needle sprays (1024×256 each): a twig with 16 pairs of side branchlets carrying needles, plus lighter new growth at the tips. Each frond is widest a third of the way out. |
| `bark_color.png` + `bark_normal.png` | 512×1024 RGB | Tileable bark with long vertical plates and dark fissures, made from periodic Fourier noise so it repeats without seams, plus a matching tangent-space normal map. One tile covers 0.6 × 1.2 m. |
| `bark_cherry_*`, `bark_spruce_*` | 512×1024 RGB | The same bark recipe in cherry and spruce colours. |

**No alpha fringes.** Every atlas is edge-padded: the colour under transparent pixels is filled outward from the nearby leaves. Neither filtering nor mipmaps can pull in black or white halos. In the 100 % crops of the close-up I found no fringes. Each sprig is also kept inside its own cell, so no leaf is cut off by a straight cell edge.

## How the trees are built (`dh_trees_tex.py`)

- **Branches:** the same branching skeleton as round 2, simplified.
  - The trunk is 9–10 sides with a flared base; limbs are 5–8 sides.
  - The bark has UVs that wrap around each branch, sized to the bark tile.
- **Canopy cards:** each card is a V-folded quad (4 triangles) showing one sprig from the atlas.
  - Maple and street tree: about 450–760 cards spread over one lumpy, noise-modulated crown ellipsoid. The cards bunch toward the outer shell and the underside is flatter.
  - Cherry: blossom cards gathered at the branch tips, so the forked dark limbs show underneath.
  - Japanese maple: flat, near-horizontal tiers.
  - Columnar: stacked clusters along the leader.
  - Each card's sprig base sits inside the crown and its tip points outward, which gives the ragged, see-through edge.
- **Spherical normals:** every card vertex gets a custom normal bent toward the crown ellipsoid (70 % whole crown, 30 % local cluster, then 15 % of the card's own normal). The crown therefore shades as one soft volume, lit on top and shaded underneath, instead of as noisy separate planes. Each card's winding is set so its front faces outward, in agreement with the bent normals.
- **Interior cards** use a second material with `baseColorFactor` 0.6. This gives cheap self-shadowing, so the crown has depth instead of looking flat.
- **Spruce:** 17 whorls of 6–8 drooping branches. Each branch is two needle-spray cards crossed at ±28° about the branch, bent along its droop. Their normals point out from the trunk axis and upward. Three crossed vertical cards form the leader.

## glTF check (done on the exported files)

`export_trees_tex.py` re-reads each `.gltf`'s JSON after export; the results are in `exports/trees_textured_stats.json`.
- Every leaf material exports as **`alphaMode: MASK`** with the default 0.5 cutoff (the Blender material sends the texture alpha through a Math: Round node) and **`doubleSided: true`**.
- Bark exports as `OPAQUE`, single-sided, with a `normalTexture`.
- The inner-shade leaf materials carry `baseColorFactor` 0.6.
- Every image URI is relative and resolves into `textures/`, with nothing embedded (`shared_texture_uris_ok`).
- All 10 trees pass; the script exits non-zero if any fails.
- The exporter prints a harmless warning that one image is used by several texture nodes.

## Concerns for Derek and Lucas
1. **Edge-on cards.** From a low angle (the close-up's 24° pitch) a few cards at the crown's edge are seen almost side-on and show as short streaks. From the game camera's 45° they are rare. A Unity shader that fades cards out as they turn edge-on would remove them entirely; that is the usual fix.
2. **Unity setup:**
   - Leaf materials need a double-sided, alpha-clip (cutout) shader.
   - Unity's double-sided lighting flips the normal on back faces. The cards are wound outward to suit this, but a shader that keeps the front normal on back faces (common in foliage shaders) gives the softest result.
   - Enable "preserve coverage" on the atlas mipmaps, or the crowns will thin out with distance.
3. **Custom normals must survive import.** In the import settings, set Normals to "Import". If Unity recalculates them, the soft-volume shading is lost.
4. **The remaining street dressing is still the round 2 high-poly models** (111k triangles for 3 lots). Beds and shrubs would benefit from the same card treatment next.
5. **Street layout, unchanged from earlier rounds:** the 0.75 m tree lawn is narrow for a full street tree, and the front yards are 2.75 m deep.

## Scripts (this folder)
- `dh_tree_tex.py`: the procedural atlases and bark.
- `dh_trees_tex.py`: the tree builders, card placement, normals and materials.
- `export_trees_tex.py`: the `.gltf` + `.bin` exports plus the glTF check.
- Render scripts:
  - `render_tree_closeup_tex.py`
  - `render_compare.py`
  - `render_samplers_tex.py` (run with `DH_SAMPLER_LAYOUT=4,9,12`)
  - `render_street_tex.py <dressed|closeup>`
- `dh_env.py` (the round 2 environment kit, whose branch-growth helpers the textured trees reuse), `dh_env_round1.py` (round 1, used only by the comparison render), `render_samplers.py` and `render_street.py` are here because the scripts above import or run them. They also hold the round 2 shrubs, beds and props, which are **not** part of this approval.
