# Doggiehood houses: approved concept models and colour schemes

These are the generator scripts, exports and review renders for the ten base houses and three upgrade pieces of the house design system (epic #767). Derek approved the full set of models and the 12 round-2 colour schemes on 2026-11-21, and #814 locked them in here as **source assets only**. Nothing in this folder is imported by Unity (it is outside `Assets/`), and the game does not use any of it yet. The model, export and import issues of epic #767 start from these files.

The ten bases are the side-gabled cottage (1), a ranch (2), a two-storey colonial with a porch (3), a craftsman bungalow (4), a Dutch colonial with a gambrel roof (5), a split-level (6), a modern farmhouse (7), a storybook Tudor (8), a Spanish mission (9) and a small Queen Anne Victorian (10). They all use the same three upgrade pieces (addition, deck, patio) and the 12 realistic colour schemes. Everything comes from Blender Python scripts run headless (`pip install bpy==5.3.0`), with no hand modelling.

## Files

| Path | What it is |
|---|---|
| `dh_house_kit.py` | Shared helpers: the shared dimensions, **the 12 approved colour schemes (`COLOR_SCHEMES`)**, the superseded round-1 schemes (`COLOR_SCHEMES_BRIGHT`, kept for comparison only), the palette, a mesh builder, finished walls with trimmed openings, framed stud walls, gable infill and gable studs, roof planes (a finished slab, or rafters plus a ridge board, built on the same plane), slab, floor framing, attach points, GLB export and the render setup |
| `base_house_1.py` … `base_house_10.py` | One script per base. Each exposes the same interface: `DIMS`, `NAME`, `RENDER_SCHEME`, `FRONT_Y` (the line that takes `FrontSetback`), `PIECES` (per-base piece parameters), `attach_locations()` and `build("finished" \| "framing", palette=…)`. Both modes come from one `DIMS` dict |
| `piece_addition.py`, `piece_deck.py`, `piece_patio.py` | One script per upgrade piece, each parameterized by its own `DIMS` |
| `dh_scene.py` | Shared scene logic: `fit_pieces()` (per-base piece parameters, solving the deck stair gap and stair position so the stairs land on the patio), `build_level()` (base + first N−1 pieces), `level4_placement()` (the placement rule and four clearances), lot lines, street and the dog stand-in |
| `export_houses.py` | Writes every `.glb` in `exports/` (see below) |
| `export_colour_schemes.py` | Writes `colour-schemes.json` from `COLOR_SCHEMES` |
| `colour-schemes.json` | The 12 approved schemes and their slot colours as plain JSON, so later work can read them without Blender. Generated; do not edit by hand |
| `render_base_1.py` | Builds, exports and renders base 1, the pieces and the colour-scheme grid, and writes `base_1_stats.json` |
| `render_bases.py` | Renders bases 2–10, the line-ups and the neighbourhood street, and writes `bases_stats.json` (dims, tris, attach points, piece parameters, placement for both addition sides) for all ten bases |
| `base_1_stats.json`, `bases_stats.json` | The measured numbers behind the tables below |
| `textures/` | Roof textures: the greyscale masks `roof_shingle_mask.png` and `roof_tile_mask.png` (made by `roof_mask()` in the kit), plus the tinted copies `roof_<style>_<HEX>.png` baked for the concept GLBs (made by `tinted_texture()` on first use) |
| `exports/base-NN-framing.glb` (NN = 01–10) | The framing version of each base alone, in its render scheme |
| `exports/base-NN-levelL.glb` (L = 1–4) | The finished house at each level, in its render scheme: level 1 is the base, level 2 adds the addition, level 3 the deck, level 4 the patio. Bases carry the `ATTACH_ADDITION` and `ATTACH_DECK` empties |
| `exports/piece-addition.glb`, `piece-deck.glb`, `piece-patio.glb` | The upgrade pieces on their own, with their default `DIMS`, in Sage Cottage. Each piece's origin is its attach point. The addition carries the `ATTACH_PATIO` empty |
| `review/color-schemes.png` | **Round 2 (approved):** all 12 colour schemes on the finished level-4 cottage at the game camera angle (orthographic, 45° pitch, from the front-left), labelled with siding and roof swatches |
| `review/color-neighborhood.png` | Two parallel streets, five houses each: all ten bases, ten different schemes, levels 1–4. Front street: mission/Adobe Tile L2, cottage/Coastal Blue L1, Queen Anne/Dove Gray L4, craftsman/Cedar Lodge L3, farmhouse/Colonial White L1. Back street: colonial/Navy Saltbox L4, Tudor/Cream & Forest L2, ranch/Greige Modern L1, split-level/Red Farmhouse L3, gambrel/Buttercream L2 |
| `review/lineup-all-finished.png` | All ten at level 1 in two rows (1–5 in front, 6–10 behind), each in its render scheme, dog for scale |
| `review/lineup-finished.png`, `lineup-framing.png`, `lineup-level4.png` | Bases 1–5 side by side at level 1 (finished, framing) and level 4, each in a different scheme, dog for scale. A flatter 3/4 view (yaw −18°, pitch 30°) so the silhouettes compare |
| `review/lineup2-finished.png`, `lineup2-framing.png`, `lineup2-level4.png` | Bases 6–10, same views |

Render schemes per base: 1 Sage Cottage, 2 Dove Gray, 3 Colonial White, 4 Charcoal Craftsman, 5 Coastal Blue, 6 Navy Saltbox, 7 Greige Modern, 8 Cream & Forest, 9 Adobe Tile, 10 Buttercream.

## Regenerating

All commands run from the repository root with headless Blender installed as a Python module (`pip install bpy==5.3.0`; the scripts also use Pillow).

- **Exports:** `python3 art-source/blender/export_houses.py art-source/blender/exports` rewrites all 53 `.glb` files in a few seconds. Two runs on the same machine produce byte-identical files.
- **Colour schemes:** `python3 art-source/blender/export_colour_schemes.py` rewrites `colour-schemes.json`.
- **Renders:** `python3 art-source/blender/render_base_1.py [out_dir]` for base 1, the pieces and the scheme grid (about 10 minutes on CPU), and `python3 art-source/blender/render_bases.py [out_dir] [--bases 6,7,8,9,10] [--only finished,framing,level4close,level4back,glb,lineup,lineupframing,lineup4,lineup2,lineup2framing,lineup24,lineupall,neighborhood]` for bases 2–10, the line-ups and the street (about 25 minutes per set of five). Both take `--only` for a subset and default to `art-source/blender/out/` (git-ignored). EEVEE needs Mesa's software EGL (`libegl1`, `libgl1-mesa-dri`) in a headless container. The renders write the stats JSON files next to the scripts. Only the review renders listed above are committed.

**Roofs in the exports.** The embedded roof image is the tinted copy for that scheme (`textures/roof_<style>_<HEX>.png`), wired straight into the glTF base colour with no `baseColorFactor`, so each roof carries one tint and viewers show it as rendered (see concern 21).

## Scale and lot envelope (inferred)

1 Blender unit = 1 m at **in-game size**. The game applies no `KitScale` to these models.

**Today's Kenney houses at in-game scale.** `HousePlacement.KitScale` = 7. I measured these sizes from the kit FBX files (W × D × H):

| Kenney model | Size at ×7 |
|---|---|
| r (house 1 starter) | 7.2 × 7.1 × 8.0 m |
| h | 9.1 × 6.4 × 5.2 m |
| k | 6.5 × 7.1 × 8.1 m |
| q | 8.7 × 6.2 × 6.4 m |
| b (largest ladder rung) | 12.8 × 8.0 × 8.0 m |
| g | 10.2 × 8.3 × 5.4 m |
| m | 10.0 × 10.0 × 5.2 m |

So the level-1 starters are about 6.5–9 m wide, 6–7 m deep and 5–8 m tall.

**Dog.** The Cube Pets adult dog is **1.26 m wide × 1.50 m long × 1.58 m tall** in game (measured from `animal-dog.fbx`, which matches `PoolPlacement.DogModelHeight` = 1.58). The brief guessed about 0.6 m, but dogs in this world are big, so doors are sized at about 2.35 m.

**Lot, for a starting corner lot, in house-local coordinates.** Pivot at the centre of the house's wall footprint, front facing −Y:
- A lot is one tile quadrant, **30 × 30 m** (`WorldDimensions.TileSize` = 60).
- The front facade sits **8.5 m** from the faced road's centreline. That is road half-width 3 + verge 0.75 + sidewalk 2 + `FrontSetback` 2.75.
- The house centre sits **14 m** from the perpendicular road's centreline (`LotDistanceFromCenter`).
- The backyard fence envelope comes from `LotFence`. On the road side it is 7.75 m off the perpendicular road's centreline, which leaves **6.25 m** from the house centre. On the neighbour side it is 2 m inside the lot edge, which leaves **14 m** from the house centre. At the back it is **19.5 m** behind the facade. Width across is 20.25 m.

**Base house 1 dimensions.**

| Dimension | Value |
|---|---|
| Wall footprint | 7.5 (W) × 7.0 (D) m |
| Slab | sticks out 0.15 m past the walls, top at 0.35 m |
| Finished floor | 0.65 m |
| Wall height | 3.0 m (top plate at 3.65 m) |
| Roof pitch | 40° |
| Ridge | about 6.9 m (about 7.8 m to the chimney top) |
| Overall bounds incl. chimney, stoop and roof overhang | x −4.75…4.14, y −5.10…4.10 |
| Front door | 1.3 × 2.35 m |

**Upgrade pieces.**

| Piece | Size | Attach point |
|---|---|---|
| Addition | 4.0 m out × 4.5 m along the side wall, wall height 2.7 m, 30° gable | `ATTACH_ADDITION` = (+3.75, −0.25, 0). Set back 1 m from the facade |
| Deck | 6.0 × 4.0 m, surface at 0.60 m (just under the back door), railing in the trim colour. A 3-riser stair leaves the deck's addition-side edge and lands on the patio | `ATTACH_DECK` = (0, +3.5, 0) |
| Patio | 4.0 × 5.5 m (as wide as the addition), 8 cm tall pavers. It sits directly behind the addition | `ATTACH_PATIO` is on the **addition**: the back edge of its slab, centred on it. Addition-local (2.15, 2.40, 0), which is (+5.90, +2.15, 0) in base space |

## Placement rule: the lot positions the level-4 house

**Rule (Derek, follow-up to this concept):** the fully upgraded (level-4) house must fit every lot. So the lot positions the **level-4 composite** (base + addition + deck + patio), not the base alone. The base is placed off-centre so the whole composite fits, and **nothing moves when a piece is added**.

**How it is computed** (`level4_placement` in `dh_scene.py`):
1. Take the composite's bounding box in house-local space. For base 1 that is x −4.75…+8.14 and y −5.10…+7.65. It includes the chimney, the stoop, roof overhangs, the addition's rake and the patio.
2. **Across the lot:** centre that box between the two side runs of the backyard-fence envelope. That envelope is 7.75 m to 28 m from the side road's centreline, 20.25 m wide.
3. **Front to back:** the facade still sits at `FrontSetback`, 8.5 m from the faced road's centreline, exactly as today.

**Result for base 1.** The house pivot sits at **19.57 m** from the side road's centreline when the addition faces the road, and at **16.18 m** when it faces the neighbour. Today's fixed `LotDistanceFromCenter` is 14 m.

**Checked against the tightest starter lot.** All 4 starting lots are corner lots with the same envelope, and the case checked is the addition on the **road side**. The patio now sits behind the addition. Both sides were checked, the addition facing the road and facing the neighbour. The addition keeps its 4.0 × 4.5 m size; no resize was needed.

| Side | Clearance |
|---|---|
| Road side: addition rake → side fence | **3.68 m** |
| Neighbour side: chimney → side fence | **3.68 m** |
| Front: stoop → sidewalk outer edge | **1.15 m** (facade → sidewalk stays 2.75 m) |
| Rear: patio / deck back edge → rear fence | **8.35 m** (was 4.00 m when the patio sat behind the deck) |

The clearances come out the same with the addition on the neighbour side, because the composite is centred across the envelope.

**All ten bases on the tightest starter lot** (same rule, level-4 composite centred across the 20.25 m fence envelope; each base's `FRONT_Y` at `FrontSetback`). Every base fits with the addition on **either** side, and the clearances are the same on both sides:

| Base | Level-4 width × depth | Pivot from side-road CL (addition road side / neighbour side) | Road-side fence | Neighbour-side fence | Front → sidewalk | Rear → fence |
|---|---|---|---|---|---|---|
| 1 Cottage | 12.89 × 12.75 m | 19.57 / 16.18 m | 3.68 | 3.68 | 1.15 | 8.35 |
| 2 Ranch | 16.42 × 13.00 m | 19.76 / 15.99 m | **1.92** | **1.92** | 1.10 | 8.15 |
| 3 Colonial | 13.79 × 13.59 m | 19.57 / 16.18 m | 3.23 | 3.23 | 2.11 | 6.55 |
| 4 Craftsman | 13.99 × 14.79 m | 19.57 / 16.18 m | 3.13 | 3.13 | 2.11 | **5.35** |
| 5 Gambrel | 13.79 × 13.55 m | 19.57 / 16.18 m | 3.23 | 3.23 | 1.35 | 7.35 |
| 6 Split-level | 15.89 × 12.34 m | 19.82 / 15.93 m | 2.18 | 2.18 | 2.36 | 7.55 |
| 7 Farmhouse | 13.93 × 15.79 m | 18.90 / 16.85 m | 3.16 | 3.16 | 2.11 | **4.35** |
| 8 Tudor | 14.99 × 14.45 m | 19.57 / 16.18 m | 2.63 | 2.63 | 1.45 | 6.35 |
| 9 Mission | 15.36 × 13.95 m | 19.78 / 15.97 m | 2.44 | 2.44 | 2.35 | 5.95 |
| 10 Queen Anne | 13.90 × 15.39 m | 19.32 / 16.43 m | 3.18 | 3.18 | 2.11 | 4.75 |

Front clearance is from the front-most thing (steps, garage apron) to the sidewalk's outer edge. For the colonial and craftsman, `FRONT_Y` is the **porch front**, so their enclosed walls sit a further 1.8 / 2.2 m back from the street than the other bases. The ranch is the widest and the tightest; it still leaves 1.9 m each side. In the second set, the farmhouse and the Queen Anne are the deepest (wrap porch / front porch plus a 9.0 / 8.6 m deep house), leaving 4.35 and 4.75 m behind the patio; the farmhouse's wrap porch also pushes its composite 1.9 m toward −X, so its pivot sits 18.9 m from the side road rather than 19.6 m. Front clearance is measured to the front-most thing; for the farmhouse, Tudor, mission and Queen Anne, `FRONT_Y` is the porch / cross-gable / loggia front, so their main walls sit further back.

For comparison, the old rule fails. A pivot at 14 m with the addition on the road side leaves the addition **1.89 m past** the road-side fence line, which is the failure flagged in the first pass.

## Colour schemes, round 2: realistic (current)

Derek asked for more standard, realistic house colours and roofs, so `COLOR_SCHEMES` is now 12 typical American-suburban schemes. The game assigns one per house at random. They are defined once, as data, in `dh_house_kit.py`. Every base and every piece paints itself only through these slots, so later bases reuse the same 12 and pieces always match their house. The round-2 renders committed here are `review/color-schemes.png` and `review/color-neighborhood.png`. The exported `.glb`s are each in their base's render scheme (pieces in Sage Cottage).

| # | Name | Siding | Roof | Roof style | Trim | Shutters | Door | Chimney | Window box | Flowers | Deck | Patio |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | Colonial White | #F2F0EA | #4A4D53 | shingle | #FFFFFF | #24272C | #9E2A30 | #9C4A3A | #FFFFFF | #D9433F / #F4F2EC | #8E5D3E | #C9C5BC / #B4AFA5 |
| 2 | Cream & Forest | #EDE3C8 | #7D807F | shingle | #FFFFFF | #2F4A37 | #2F4A37 | #8F8B84 | #2F4A37 | #E26A8A / #F4F2EC | #A87449 | #D2CBBE / #BDB5A6 |
| 3 | Adobe Tile | #DCC4A0 | #B5552F | tile | #F7F1E4 | #5E7A6E | #6E4A2E | #C9B79A | #6E4A2E | #D9433F / #F2C14E | #8C5636 | #C9774F / #B2623F |
| 4 | Greige Modern | #BEB6A9 | #2E3034 | metal | #F5F3EE | #2E3034 | #B9824C | #8A8782 | #2E3034 | #F4F2EC / #9B7FC0 | #6F4D36 | #A9A6A0 / #93908A |
| 5 | Dove Gray | #AEB4B9 | #2D2E31 | shingle | #FFFFFF | #FFFFFF | #2C5C8C | #9E4F3C | #FFFFFF | #D9433F / #F4F2EC | #8F857A | #8E99A3 / #A8B0B7 |
| 6 | Charcoal Craftsman | #4F535A | #6E5846 | shingle | #F2EEE6 | #F2EEE6 | #B57B45 | #9C978E | #B57B45 | #F2C14E / #E26A8A | #A06B43 | #BDB7AC / #A59F94 |
| 7 | Coastal Blue | #A8BDCB | #58636F | shingle | #FFFFFF | #FFFFFF | #26385A | #A3A09A | #FFFFFF | #7B5BA6 / #F4F2EC | #9A9188 | #D7D2C6 / #BFC6CC |
| 8 | Navy Saltbox | #2F3F5E | #3E4146 | shingle | #F6F4EE | #F6F4EE | #A3262F | #A0503C | #F6F4EE | #D9433F / #F2C14E | #8E5D3E | #C9C5BC / #A9A399 |
| 9 | Sage Cottage | #9DA88C | #4F4137 | shingle | #F4EFE1 | #414C38 | #8B2E2E | #A45A42 | #F4EFE1 | #E26A8A / #F2C14E | #9A6240 | #D9C7A5 / #C2AE8B |
| 10 | Red Farmhouse | #8E2F2A | #878E95 | metal | #FFFFFF | #FFFFFF | #2B2B2B | #97928A | #FFFFFF | #F2C14E / #F4F2EC | #8C5A3C | #B9B4AA / #A29C91 |
| 11 | Buttercream | #EAD9A0 | #3D5B45 | metal | #FFFFFF | #3D5B45 | #9A6A3E | #A4553F | #FFFFFF | #D9433F / #7B5BA6 | #B07A47 | #A65A42 / #8E4B37 |
| 12 | Cedar Lodge | #8C6244 | #36383C | shingle | #F1EADB | #3B4A36 | #3B4A36 | #8F8B84 | #3B4A36 | #F2C14E / #E26A8A | #6E4A33 | #9C978E / #B8B2A6 |

**Design intent.**
- **Siding** covers the usual range: white, cream, adobe beige, greige, light gray, charcoal, pale blue-gray, navy, sage, barn red, buttercream and cedar brown.
- **Trim** is white or off-white.
- **Shutters and doors** use realistic accents: black, navy, forest or olive green, burgundy, natural wood and white.
- **Chimneys** are brick red or gray stone.
- **Decks** are wood stains: cedar, walnut, weathered gray, honey and redwood.
- **Patios** are concrete, bluestone, flagstone, sandstone, brick or terracotta pavers.
- **Shared colours.** Window-box flowers are real flower colours. For the realistic schemes the leaf green (`#4F8A3C`) and glass (`#A8D2E6`) are calmer. Round 1 keeps its candy versions.

**Iteration notes.**
- The first round-2 grid had *Colonial White* and *Dove Gray* as near twins. Dove Gray became a mid gray (`#AEB4B9`) with white shutters and a blue door.
- The galvanized roof on *Red Farmhouse* was toned down (`#878E95`).
- The lot and street renders now use neutral ambient light, because the green world colour had been tinting white siding mint.
- Closest remaining pairs: *Colonial White* vs. *Cream & Forest* (white vs. cream, charcoal vs. weathered-gray roof, black vs. green shutters); *Navy Saltbox* vs. *Charcoal Craftsman* (dark sidings, told apart by the black vs. brown roof).

**Roofs and roof textures.**
- **Asphalt shingle** (8 schemes: charcoal, weathered gray, black, brown, slate, dark brown): a tileable greyscale mask, multiplied by the scheme's roof colour. It covers 1 m × 1 m in 240 px. There are 4 courses of 0.25 m, each with three 0.33 m tabs staggered by half a tab. Each tab has its own random shade, there is a shadow line at the bottom edge of each course, and 3-tab slots are cut partway up from that edge.
- **Clay tile** (Adobe Tile, terracotta): a second mask covering 0.96 m × 0.96 m. It has 3 barrels with cylindrical shading and 4 courses with shadow lines.
- **Standing-seam metal** (Greige Modern black, Red Farmhouse galvanized, Buttercream forest green): **geometry, not texture**. Raised seam strips run up the slope every 0.5 m, 5 × 6 cm, in a lighter shade of the roof colour.
- **Round-1 look** (`courses`): the old 4 raised course lines.
- Ridge caps are the roof colour × 0.8.

**How the textures are mapped.** Roof faces get planar UVs **in metres**: u runs across the slope and v runs up it. So every house and piece shows the same texel size, and the courses stay level.

**How this carries into Unity.**
- One small greyscale texture per roof style goes on a Lit material.
- The Base Map is the mask, and Base Color is the scheme's roof hex. URP multiplies the two, so 12 schemes need only 2 textures.
- For the concept GLBs I baked the tinted copy (`textures/roof_<style>_<HEX>.png`) and wired it straight into the glTF base colour, so any viewer shows it as rendered.
- The masks are in `textures/`. The generator is `roof_mask()` in the kit.

**Round 1 kept, superseded.** The candy-bright set is still in the kit as `COLOR_SCHEMES_BRIGHT` and still renders, with its raised course lines; pass any of its names to `scheme_palette()`. It was **rejected**: it is kept for comparison only and is not in `colour-schemes.json`. Its hex table is in the kit source; the round-1 comparison render was not committed.

## Triangle counts

Counts by roof style. Shingle and tile roofs are textures, so they cost **fewer** triangles than round 1's raised course lines. Metal seams are geometry. Since bases 2–5 were added, window and door trim no longer has a face buried in the wall (`cull_back` in the kit), which took about 10% off every house and the addition; base 1's numbers below are the new ones.

| Base | Finished | Framing | Level 4, shingle/tile (base + 3 pieces) | Level 4, metal |
|---|---|---|---|---|
| 1 Cottage | 1,574 | 2,160 | **3,596** | 4,172 |
| 2 Ranch | 1,658 | 2,712 | **3,680** | 4,016 |
| 3 Colonial | 2,672 | 4,020 | **4,694** | 5,318 |
| 4 Craftsman | 2,288 | 2,904 | **4,310** | 5,318 |
| 5 Gambrel | 2,134 | 3,468 | **4,156** | 4,348 |
| 6 Split-level | 2,062 | 3,932 | **4,084** | 4,972 |
| 7 Farmhouse | 2,824 (metal, its render scheme) | 3,840 | **4,390** | 5,110 |
| 8 Tudor | 2,118 | 3,084 | **4,140** | 5,028 |
| 9 Mission | 2,498 (tile) | 3,008 | **4,520** | 4,784 |
| 10 Queen Anne | 3,080 (metal, its render scheme) | 4,820 | **4,670** | 5,294 |

Pieces (shingle / metal): addition 606 / 798, deck 1,068, patio 348. For comparison, a Kenney house is about 770–1,750 triangles.

**Budget for bases 6–10** (Derek's target: level 4 at or under the colonial, about 4.7k, with shingle/tile roofs). **All five are under it**: split-level 4,084, farmhouse 4,390, Tudor 4,140, mission 4,520, Queen Anne 4,670 (right at the line). With **metal** roofs every one of them goes over (4,784–5,294), as do the colonial and craftsman: seams are geometry, and the steep or big roofs carry many. Arched openings were cut from 6 to 4 segments per half-circle to bring the mission (which has ten arches) under the line, from 4,848 to 4,520.

**Budget, first set.** Bases 1, 2 and 5 stay within about ±15% of base 1 at level 4. The **colonial is about 30% over** (4,694 vs 3,596), because it has two storeys of shuttered windows (14 windows on the street side and gables). I already dropped one upstairs back window and the back mullions; going further would mean losing shutters on the side walls. The craftsman's metal-roof number is high because its long, deep roof carries many seams. See concern 10.

## Base house 1: the side-gabled cottage

A small side-gabled cottage. All its colours come from the scheme, and it is shown here in Sage Cottage (round 2); round 1 used the candy Sunny Cottage scheme. It has shuttered windows with flower boxes under the front windows, a chimney on the outside of the left gable, and a shed canopy over the front door. The ridge runs side to side, so the right gable wall is where the addition butts in, under the main rake.

**Framing version.**
- The concrete slab, stoop, back step and chimney footing are identical in both versions.
- On top of the slab: rim joists and joists, and a partly laid subfloor so some joists show at the back.
- Stud walls with bottom plates and double top plates, king studs, headers, sills and cripples.
- Gable-end studs up to the rafter line, rafters with tails, and a ridge board.
- Three temporary diagonal braces as a "being built" cue.
- No siding, roof covering, trim or chimney.
- Raw wood is in two tones: studs and rafters are lighter, while plates, headers and rims are darker.

## Bases 2–5

All four follow base 1's conventions: pivot at the centre of the wall footprint, front facing −Y, slab top 0.35, finished floor 0.65, 0.22 m walls, the same stud/rafter sizes, the addition on +X and the deck on the back wall. The finished and framing versions are built from the same `DIMS`, and their slab, porch deck, steps and footings are **identical** in both modes, so the two line up exactly (checked: same front, back and side slab bounds; only roof overhangs and exterior trim differ).

| | 2 Ranch | 3 Two-storey colonial | 4 Craftsman bungalow | 5 Dutch colonial (gambrel) |
|---|---|---|---|---|
| Silhouette | Long, low, hipped | Tall symmetrical box, full-width porch | Front gable over an inset porch | Barn-like double-pitch roof with a front shed dormer |
| Wall footprint | 11.4 × 7.2 m | 8.4 × 7.0 m (+ 1.8 m porch in front) | 8.6 × 10.0 m (incl. 2.2 m inset porch) | 8.4 × 8.0 m |
| Wall height | 2.8 m | 5.9 m (two 2.8 m storeys + 0.3 m floor platform) | 2.9 m | 2.8 m (upper storey is inside the roof) |
| Roof | Hip, 22°, 0.5 m eaves | Side gable, 35°; porch shed roof | Front gable, 26°, 0.75 m eaves, 0.6 m rakes, gabled dormer on the −X slope | Gambrel: 66° lower slope over 1.3 m, 24° upper; shed dormer at 10° |
| Height (incl. chimney) | 6.0 m | 10.1 m | 6.6 m | 8.9 m |
| Overall bounds (finished) | x ±6.33, y −5.25…4.23 | x −5.2…4.59, y −5.94…4.1 | x −5.3…5.12, y −5.64…5.64 | x −5.2…4.54, y −5.4…4.6 |
| `FRONT_Y` (line at `FrontSetback`) | −3.6 (facade) | −5.3 (**porch front**) | −5.0 (**porch front**) | −4.0 (facade) |
| Character pieces | Attached one-car garage with sectional door and concrete apron, picture window with flower box, chimney through the roof | Shutters on both floors, square porch columns on bases, brick chimney outside the −X gable | Tapered columns on stone piers, louvred gable vent, knee braces, exposed rafter tails, stone chimney | Shuttered windows with flower boxes, two-window shed dormer, gable windows on both floors, stone chimney |
| Framing extras | Hip and jack rafters, garage header | Second-floor platform between the storeys, porch rafters on temporary posts | Porch header on temporary posts, dormer framing | Second-floor platform, knuckle purlins, gambrel rafters, dormer framing |
| Render scheme | Dove Gray | Colonial White | Charcoal Craftsman | Coastal Blue |

**Per-base piece parameters.** The pieces are the same scripts; each base passes only what it needs (`PIECES` in its module, merged by `dh_scene.fit_pieces`):

| Base | `ATTACH_ADDITION` | Addition overrides | `ATTACH_DECK` | Deck width | Stair gap (solved) |
|---|---|---|---|---|---|
| 1 Cottage | (3.75, −0.25, 0) | none | (0, 3.5, 0) | 6.0 | 0.9 |
| 2 Ranch | (5.7, −0.15, 0) | wall 2.6, pitch 22°, `tie_in` 2.6 | (1.95, 3.6, 0) | 6.0 | 0.9 |
| 3 Colonial | (4.2, −0.25, 0) | none | (0.45, 3.5, 0) | 6.0 | 0.9 |
| 4 Craftsman | (4.3, 1.25, 0) | wall 2.6, pitch 26°, `tie_in` 2.5 | (0.55, 5.0, 0) | 6.0 | 0.9 |
| 5 Gambrel | (4.2, 0.25, 0) | none | (0.45, 4.0, 0) | 6.0 | 0.9 |

- The addition is always 4.0 m out × 4.5 m along the wall, with its centre **3.75 m in front of the back wall** (`ADDITION_BACK_INSET`), so the patio behind it lines up with the deck on every base.
- **`tie_in`** is new on the addition. Where the addition meets an **eave** side (ranch, craftsman), its roof runs that far into the base until its ridge dies into the main slope, instead of butting into a wall under the eave. On gable sides (cottage, colonial, gambrel) it is 0 and the addition butts into the gable wall as before.
- **The deck stays 6.0 m wide on every base.** It is centred off the back door so its +X edge sits 0.9 m from the addition's slab (`deck_layout()` in the kit); the back door is placed from the same number. The stairs' position along the deck is solved so they land on the patio.
- Patio: unchanged, as wide as the addition (4.0 × 5.5 m), on the addition's `ATTACH_PATIO`.

**Iteration notes.**
- **Saltbox swapped for a gambrel.** In the low-poly style a saltbox read as a lopsided version of the cottage. The gambrel is unmistakable from the game camera, so base 5 is a Dutch colonial instead.
- The colonial's stacked windows first rendered as shutters with no glass. The kit's finished wall assumed one opening per position along the wall; it now handles windows stacked above each other (two storeys).
- The gambrel's upstairs read as blank roof from the street, so it gained the classic Dutch-colonial shed dormer.
- The craftsman's roof dominated the game view, so it gained a small gabled dormer on the street-facing slope and a louvred vent in place of a plain square in the front gable. Its ridge is kept under the main ridge.

## Bases 6–10

Same conventions as bases 1–5 (pivot at the centre of the wall footprint, front −Y, addition on +X, deck on the back wall, slab top 0.35, floor 0.65), the same pieces and the same 12 schemes. Finished and framing come from one `DIMS`; slab, porch decks, steps, footings and paving are identical in both, so they line up exactly.

| | 6 Split-level | 7 Modern farmhouse | 8 Storybook Tudor | 9 Spanish mission | 10 Queen Anne |
|---|---|---|---|---|---|
| Silhouette | Tall front-gabled wing beside a low side-gabled wing: a stepped roofline | Narrow, very steep (50°) front gable over a wrap porch | Steep side gable with a steeper (58°) front cross-gable | Long low hip with an arched loggia and a curved parapet | Two storeys, steep front gable, octagonal corner turret with a pointed roof |
| Footprint | 11.0 × 7.8 m (tall wing 5.0 wide; living wing 6.0 wide, 1.6 m set back) | 7.2 × 9.0 m + 2.0 m wrap porch (front and −X side) | 9.6 × 7.6 m + cross-gable 4.0 wide, 1.4 m forward | 10.4 × 7.4 m + loggia 4.8 × 2.0 m | 8.0 × 8.6 m + turret (r 1.55 m) + 2.0 m porch |
| Walls | Tall wing 4.7 m, upper level 1.75 m above the living floor (the half-storey offset); living wing 2.5 m | 4.6 m (storey and a half; loft windows above the porch roof) | 2.7 m (low walls, big roofs) | 3.0 m, stucco | 5.9 m (two storeys); turret to 8.7 m |
| Roof | 38° front gable + 18° side gable | 50° front gable; porch 14°, two legs meeting on a hip line | 50° main, 58° cross-gable | 18° hip | 45° front gable; turret cone 3.9 m tall with a finial |
| Height | 8.2 m (chimney) | 10.1 m | 9.3 m (chimney) | 6.1 m | 13.2 m (finial) |
| `FRONT_Y` | −3.9 (tall wing's front wall) | −6.5 (**porch front**) | −5.2 (**cross-gable front**) | −5.7 (**loggia / courtyard wall front**) | −6.3 (**porch front**) |
| Character pieces | Stacked windows at in-between heights, picture window with a flower box, louvred vent | Board-and-batten (vertical battens on every wall and gable), tall windows, big loft window in the gable, stovepipe | Half-timbering (girt, band posts, king post, raking braces) in the scheme's deck stain, arched door with trim ring, flower boxes, tall stone chimney | Three front arches + one each side, mission parapet with a bell niche, arched windows and door, tile-capped courtyard wall, stucco chimney | Turret windows on both floors, king-post gable truss, turned porch posts with brackets and a spindle frieze, shutters |
| Framing extras | Mid-height platform in the tall wing | Loft platform, porch rafters on both legs | Cross-gable rafters | Loggia posts, header, flat-roof joists, parapet studs | Turret posts, plates and conical rafters; second-floor platform |
| Render scheme | Navy Saltbox | Greige Modern | Cream & Forest | Adobe Tile | Buttercream |

**Piece settings.**

| Base | `ATTACH_ADDITION` | Addition overrides | `ATTACH_DECK` | Deck width | Stair gap |
|---|---|---|---|---|---|
| 6 Split-level | (5.5, 0.15, 0) | none (+X is the living wing's gable) | (1.75, 3.9, 0) | 6.0 | 0.9 |
| 7 Farmhouse | (3.6, 0.75, 0) | pitch 50°, `tie_in` 1.1 (eave side) | (−0.15, 4.5, 0) | 6.0 | 0.9 |
| 8 Tudor | (4.8, 0.05, 0) | pitch 50°, wall 2.5 m (gable side, no tie-in) | (1.05, 3.8, 0) | 6.0 | 0.9 |
| 9 Mission | (5.2, −0.05, 0) | pitch 18°, wall 2.6 m, `tie_in` 1.5 (hip side) | (1.45, 3.7, 0) | 6.0 | 0.9 |
| 10 Queen Anne | (4.0, 0.55, 0) | pitch 40° (eave side, but the 5.9 m wall is tall enough to butt into; no tie-in) | (0.25, 4.3, 0) | 6.0 | 0.9 |

**Kit additions for this set.** `Opening(arched=True)` (round head with wall-coloured corner fills and a trim ring; used by the Tudor and mission doors and windows), `arch_points()` and `arcade()` (arched loggia faces), and a `grass` switch on the street helper. The farmhouse's wrap-porch roof, the Tudor's half-timbering, the mission's parapet and the Queen Anne's turret live in their own base scripts.

**All 12 schemes.** I rendered each of bases 6–10 at level 4 in all 12 schemes (low-res check sheets, not delivered). All read correctly. The Tudor's timbers are lowest-contrast on Navy Saltbox and Charcoal Craftsman, and the mission only shows clay tile in Adobe Tile; in the others it's a stucco house with a shingle or metal hip roof.

**Camera readability check.** All five suggested styles passed; none was swapped. Notes from iterating:
- Split-level: at first the living wing's roof read as tall as the tall wing. Lowering it to 18° and making the tall wing taller (4.7 m walls, 38°) gives a clear step. The front door was moved out of the tall wing's shadow.
- Farmhouse: with one-storey walls, the 2 m wrap porch roof hid the whole facade from the 45° camera. It's now a storey and a half, so loft windows and battens show above the porch roof. The porch floor became boards over a footing (a solid stained block looked like a basement).
- Split-level: an upper window on the tall wing's +X wall poked through the living wing's roof and was removed.
- Queen Anne: the turret was raised so its roof clears the main eave and reads as a tower.
- Mission: arches were cut to 4 segments per half-circle for the triangle budget; they still read as round.

## Things I was unsure about / concerns

1. **The placement rule needs code and spec changes later.** Today `NeighborhoodLayout.LotDistanceFromCenter` = 14 is a fixed lot anchor, and `HousePlacement` places the base from it. The new rule means the pivot comes from each base's level-4 composite instead, and so does where the house sits across the lot. That affects `HousePlacement`, the fence connectors, `YardLandscaping`'s house obstacle, the pool's placement and the front walkway. This belongs to #773 / #778 / #789. Nothing in `Assets/` was changed here. Zone lots (cul-de-sac and others) were not checked, only the starting corner lots.
2. **The fence connectors hit the addition.** `LotFence` runs a short connector from each side fence run to the house's side-wall midpoint, at the house's depth midpoint. With the addition installed, that midpoint is inside the addition. In the renders I stopped the connector at the addition's end wall, which is now on the road side. #783 needs a rule for this.
3. **The deck's stairs depend on the base's geometry.** The patio no longer depends on the deck. But the deck's stairs have to reach the patio edge, so `stair_gap` (0.9 m) is the distance from the deck's side edge to the addition slab. For base 1 that is 3.9 − 3.0. A wider or narrower base would need its own deck width or stair gap. Ideally each base's deck is sized so that its side edge lines up with that base's addition. The patio also shrank from 7.0 × 4.5 m to 4.0 × 5.5 m so it fits behind the addition.
4. **Footprint definition.** The finished and framing meshes have different bounds (the roof overhang only exists on the finished one). Today's `HouseModelCatalog` footprints are measured mesh bounds. For the new houses, the catalog should use a declared footprint, either the slab or the wall rectangle, not mesh bounds. Otherwise the front setback and the walkway would shift between vacant and occupied.
5. **Piece colours are solved by the schemes.** The pieces only paint through the scheme slots, so a piece always matches its house. The exported `.glb`s are each in one scheme (their base's render scheme; the pieces in Sage Cottage). In Unity, either the 12 schemes get applied by material-slot name (the material names are `DH_<scheme>_<slot>`), or each part gets exported 12 times. That is a pipeline decision for #774.
6. **Axis convention for Unity.** In Blender the front faces −Y. The glTF exporter's Y-up conversion turns that into +Z, but the Kenney models face local −Z (`ModelYawOffsetDegrees` = 180). The pipeline page (#774) should fix one convention before import. Also, the GLBs carry their materials as the glTF metallic-roughness material, not Unity materials.
7. **The framing has more triangles than the finished house** (e.g. 2,160 vs 1,574 for base 1, 4,020 vs 2,672 for the colonial) because every stud is a box. That is fine for 4–20 houses, but worth knowing.
8. **Dog size.** As noted above, the in-game dog is 1.58 m tall, not 0.6 m. The stand-in uses the real Cube Pets bounds.
9. **Rendering setup.** EEVEE needed Mesa's software EGL (`libegl1`, `libgl1-mesa-dri`) installed in the container, because headless `bpy` has no GPU. The GLB export logs a harmless "MeshOptimizer not available" message.
10. **Triangle budget for the two-storey colonial** is about 30% over base 1 at level 4 (4,694 vs 3,596; table above). Fine for 4–20 houses, but if there is a hard per-house budget, the colonial is the one to trim (side-wall shutters first).
11. **`FRONT_Y` for porch houses.** I put the porch front, not the wall, on the `FrontSetback` line for the colonial and craftsman. The alternative (walls on the line, porch encroaching into the 2.75 m setback) would leave only about 0.3 m to the sidewalk. Whichever is chosen, the catalog needs a declared front line per base (ties into concern 4).
12. **Additions on eave sides.** The ranch and craftsman additions meet the base under its eave, so their roofs tie into the main slope (`tie_in`). It looks right, but the addition piece is no longer independent of its base: its roof is longer and it uses that base's pitch. The piece specs (#780–#782) should allow per-base addition parameters.
13. **The ranch garage.** The garage door sits at the finished-floor level, with a sloped concrete apron down to the grass. A garage at slab level would need its own floor, and the game has no cars, so it is decorative only. It's also the one base whose front door is off-centre (x = +0.3), so the front walk has to come from the base, not assume x = 0.
14. **Dormers** are decorative only: there is no upstairs interior. In the framing version the gambrel's lower rafters still run through the dormer opening.
15. **Metal roofs on the bigger bases** push level 4 over the 4.7k line (bases 3, 4, 6–10: up to 5.3k). If that matters, the seam spacing could widen on large roofs, or metal could become a texture like the shingles.
16. **The farmhouse's wrap porch** keeps the +X side and the back clear, so the addition and deck attach as usual, but it makes the composite asymmetric (1.9 m extra on −X). The placement rule handles it; the fence connector on −X would hit the porch, not the wall (same family as concern 2).
17. **The mission's courtyard wall and loggia** are part of the base, inside the front setback line (`FRONT_Y` is their front). The game's front walk / yard landscaping should treat the courtyard as part of the house footprint.
18. **The Queen Anne is the tallest house by far** (13.2 m to the finial, 11.6 m to the turret roof), about twice the ranch. It may dominate a street of single-storey houses; the turret can come down if so.
19. **Metal seams only on gable roofs.** Hip roofs (ranch, mission) and the porch / wrap-porch roofs built straight from roof planes show metal schemes as a flat colour with no seams. That's why the mission's metal count is low. If metal should read the same everywhere, the seam pass needs to move into `RoofPlane.slab`.
20. **Half-timber colour** uses the scheme's deck stain (darkened), so every scheme still has a timber colour without adding a 13th slot. On dark-siding schemes (Navy Saltbox, Charcoal Craftsman) the timbers are low-contrast.
21. **The embedded roof texture is pre-tinted per scheme.** Each GLB carries its scheme's tinted roof image, so its roof colour is fixed. Recolouring roofs at runtime (one material per roof style, Base Map × the scheme hex, as in "How this carries into Unity") would need the greyscale mask from `textures/` instead. That is for #774 to decide.
