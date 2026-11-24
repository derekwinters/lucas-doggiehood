# Road kit (approved, #802, #836)

A road kit for **every road tile type** in the tile catalog: asphalt, lane paint, stop lines,
crosswalks and the flat curb. It matches today's geometry exactly, sits on the approved lawn
(look C, edge set 1, #801/#834) and is seamless across tile borders. Derek approved it, and it is
kept here as **source assets only** (#836).

Nothing here is imported by Unity, because the folder is outside `Assets/`.

## Derek's pick (2026-11-23)

- **Look A, Fresh blacktop:** dark asphalt, clean paint, a double solid yellow centre line,
  continental bar crosswalks, stop lines, no wear.
- `textures/` and `exports/` hold look A only. Looks B and C stay selectable in the scripts
  (`dh_roads.LOOKS`); their textures rebuild on demand, byte for byte (see Regenerating).
- The open road questions are in #837. Until they are answered, the kit keeps its defaults: a stop
  line on every approach, a rounded dead end, square intersection corners and paved crosswalk
  landings.

## Looks (the three axes mix and match)

| Look | Asphalt | Paint | Centre line | Crosswalk | Wear |
|---|---|---|---|---|---|
| **A. Fresh blacktop** | dark, near-black `#3B3D40` | clean | double solid yellow | continental bars | none |
| **B. Weathered grey** | light grey `#76777A`, exposed aggregate | worn | dashed yellow (2 m every 6 m) | two lines | 1-3 dark patches + 5-10 sealed cracks per tile |
| **C. Quiet street** | charcoal `#55575A` | clean | none | ladder (two lines + bars) | 0-2 patches + 0-3 cracks per tile |

Every look has a stop line on the right-hand approach lane of every intersection arm, 0.6 m before
the crosswalk. Asphalt tone, paint finish, centre line, crosswalk style and wear are independent
settings in `dh_roads.LOOKS`; any combination works.

## Geometry: today's footprint, from Core

`dh_road_geom.py` reads `WorldDimensions.cs` and `TileCatalog.cs` at run time, so no measurement
is re-typed. Per tile, outward from the road centreline:

| Band | Offset from centreline | Source |
|---|---|---|
| Asphalt | 0 – 3.0 m | `RoadWidth / 2` |
| Flat curb (concrete band, flush) | 3.0 – 3.15 m | Derek's edge set 1 (0.15 m, as in `dh_ground`) |
| Lawn verge | 3.15 – 3.75 m | `GrassVergeWidth` 0.75 m incl. curb |
| Sidewalk | 3.75 – 5.75 m | centreline 4.75 m, `SidewalkWidth` 2 m |

- **FourWay, Tee:** a crosswalk on every road arm, centred 4.75 m from the tile centre, 3 m along
  the arm (`TileCrosswalkGeometry`); paint only on the asphalt. Box corners are paved from the curb
  to the sidewalk's outer edge, and each crosswalk end has a paved landing across the verge, so a
  dog never crosses lawn to reach a crosswalk.
- **Turn:** road centreline on a 5 m arc (`RoadBendCornerRadius`), asphalt radii 2 and 8 m,
  sidewalk centrelines on Core's 0.25 m and 9.75 m arcs (`WalkNetwork.BuildBendArc`).
- **Cul-de-sac:** the pavement follows Core's walk path (`BuildCulDeSacTurnaround`): straight to the
  tile centre, then a 9 m arc whose centre is 7.64 m back down the road. So the asphalt ends in a
  shallow rounded cap 0.39 m past the tile centre, **not** a wide 9 m turning circle (see Open
  questions).
- **GreenSpace:** no road; only the lawn.

### Verified numerically (`check_geometry.py`)

The Core side is not re-derived: `core_dump/` is a small .NET console app that compiles
`Assets/Scripts/Core` and dumps Core's own walk network, crosswalk rectangles, road segments,
open-space trees and `TileMap.CanPlace` results to `core_geometry.json`. The check compares the
kit's shapes with that, for all 15 road types (all pass):

- Cross-sections at 12, 20 and 29.999 m on every road arm: asphalt [−3, 3], curb [3, 3.15] each
  side, sidewalk [3.75, 5.75] each side.
- All 32 road edges have the same cross-section at the tile border, so any two tiles meet
  seamlessly.
- Every Core road segment's straight part is asphalt.
- Every Core sidewalk walk edge lies on the sidewalk: 1.00 m inside the 2 m band and 1.60 m from the
  curb on straights. On curves it is 0.954 m (bends) and 0.963 m (cul-de-sac) inside, and 1.554 /
  1.562 m from the curb, because Core walks a curve on straight chords up to 2 m long, cutting up to
  5 cm inside the arc the pavement follows.
- Crosswalk paint: one band per Core rectangle (4 on a FourWay, 3 on a Tee), covering exactly its
  along-arm extent, and inside the asphalt, for all three styles.
- Every marking of every look is on the asphalt; every stop line is in the right-hand approach lane.
- Bend asphalt radii 2.0 and 8.0 m (±1 mm tessellation); Core's bend walk nodes are at 0.25 and
  9.75 m.
- The 3 × 3 review block is a valid map (Core's `TileMap.CanPlace`).
- Exports (`export_roads.py`) read back: every road edge's mesh bounds land exactly on ±30 m.

**Found while checking, not caused by the kit:** Core's open-space trees can stand on a bend's
outer sidewalk. `TileGeometry.PavementFor` clears the straight road corridors but not the outer
curve, so 4 of about 1,200 sampled trees (TurnNW, TurnSE, over 49 tile coordinates) land on the
outer arc's sidewalk, 8.9–10.5 m from the corner centre. The Kenney bend has the same outer curve,
so this probably happens in today's game too. Tracked in #838.

## Technical recommendation

| Choice | Recommendation | Why |
|---|---|---|
| Asphalt texture | **one 512² colour + normal, world UV, 6.4 m period** | It holds only detail finer than ~0.5 m, so there is no period-scale feature to repeat. 6.4 m doesn't divide 60 m, so each tile lands at a different phase. Large-scale variation comes from the lawn's macro (UV1, luminance only, 60%) and seeded per-tile patches and cracks, none of which repeat. |
| Markings | **geometry strips, one 512 × 128 atlas** (white, yellow, crack sealant, curb concrete; 1 m swatches tileable in U) | Crisp at every zoom, positions exact per tile, tiny texture. Strips are cut at whole metres so U runs on continuously. Paint is opaque (worn holes are painted in the asphalt colour), so there is no alpha test or blending. |
| Decals as geometry vs. baked | **Geometry overlay** (4.5 mm above the asphalt) | Baking would need a unique texture per tile (a 60 m tile at 80 px/m is 4800²). With an ortho camera the depth is linear: 4.5 mm is about 70 depth steps at 24 bit with a 1 km far plane, so there is no z-fighting. |
| Curb | **in the markings atlas** | Saves a material; flat and flush, as Derek chose. |
| Materials / draw calls | **2 per tile** (asphalt, markings) + sidewalk (#803) + lawn | Patches are the same asphalt material, darkened by vertex colour. The asphalt shader can be the lawn's planned Shader Graph (world UV + macro on UV1 + vertex colour), so there is no new shader family. |
| Mesh | built per tile at run time (Core shapes + a thin mesh builder), like the lawn | UVs are world space, and patches and cracks are seeded per tile coordinate. One imported mesh per type with a world-space shader also works if the wear is dropped. |

**Triangles per tile** (exported, `exports/roads_stats.json`): FourWay 814–982, Tee 580–794,
Straight 246–486, Turn 514–872, Cul-de-sac 189–437. Most of it is the 1 m strip pieces of the curb
and paint. A 3 × 3 view is ≤ 9k road triangles. Asphalt alone is 30–120 triangles per tile.

**Texture memory (ASTC 6 × 6, mips, one look):** asphalt 0.155 MB colour + 0.155 MB normal,
markings 0.039 + 0.039 MB, macro shared with the lawn (0). **Total ≈ 0.39 MB.** A 1024² asphalt
would be 1.24 MB; it was not rendered. If ASTC 6 × 6 normals band on device, use 4 × 4 for the
asphalt normal (+0.2 MB). On disk in the build it is the same compressed data. The source PNGs are
0.75–0.97 MB per asphalt pair and 0.14–0.16 MB per atlas, because noise compresses poorly as PNG.

## Files

```
roads/
  dh_road_tex.py       textures (seeded numpy + PIL), wrap check
  dh_road_geom.py      every tile type's shapes from Core (shapely, no Blender), markings, seeded wear
  dh_roads.py          Blender meshes + materials, LOOKS, the starter scene
  check_geometry.py    the numerical check against core_geometry.json -> geometry_report.json
  core_dump/           .NET console app that dumps Core's geometry (bin/obj git-ignored)
  core_geometry.json   its output
  geometry_report.json the check's last output
  export_roads.py      the 15 types to exports/ (.gltf + .bin, ../textures URIs), read-back checks;
                       look A by default, --looks a,b,c for the others
  render_roads.py      review renders + sheets + self-contained .glb per look, to out/ (git-ignored)
  textures/            look A: asphalt_fresh_{color,normal}.png, road_markings_clean_{color,normal}.png
  exports/             road_a_<Type>.gltf/.bin for the 15 types + roads_stats.json
  review/              the approved renders (see Review renders)
```

## Review renders (review/, committed)

Blender's and PIL's own PNG output, unchanged:

- `compare-looks.png`: the three looks: starter tile with the lot, the 3 × 3 mixed block far out,
  the intersection close-up and a tile border. Look A was picked.
- `compare-detail.png`: the bend and the cul-de-sac in each look.
- `catalog-a.png`: all 15 road tile types in look A, one 60 m tile each.
- `starter-a.png`, `far-a.png`, `cross-a.png`, `border-a.png`: look A at full size. In `border-a`,
  red pegs stand on the border between a FourWay and a TeeSouth.

It imports the lawn (`../ground`), and through it the fences, props, trees and house kit, by
relative path, and copies nothing. **New dependency: `shapely` (2.2.0)** for the 2-D polygon work
(offsets, unions, constrained triangulation). Exact offsets of arcs and the cul-de-sac's kinked
path by hand would be a lot of fragile code.

## Regenerating

```
pip install bpy==5.3.0 shapely==2.2.0     # plus Pillow, numpy; renders need libegl1 + libgl1-mesa-dri
python3 art-source/blender/environment/roads/dh_road_tex.py        # look A textures + wrap check (--all: every look)
python3 art-source/blender/environment/roads/check_geometry.py [--refresh]   # --refresh needs dotnet-sdk-8.0
python3 art-source/blender/environment/roads/export_roads.py       # exports/ (look A) + read-back checks
python3 art-source/blender/environment/roads/render_roads.py [OUT] [--looks a,b,c] [--views starter,block,catalog,sheets,glb] [--samples 24]
```

The texture and export scripts each gave byte-identical results when run twice (sha256).
`render_roads.py` builds any look's textures into `textures/` on demand; delete those again before
committing. Renders are not byte-reproducible.

## Still open (#837)

1. **Stop lines on every approach?** The spec puts stop signs only on "side-street approaches"
   (#805/#812 decide which streets those are). The kit paints a stop line on every arm. Once
   side streets are decided, the stop line should follow the sign.
2. **Cul-de-sac shape.** Matching Core exactly gives a rounded dead end, not a wide turning
   circle, because the 9 m `CulDeSacBulbRadius` is the radius of the **sidewalk** path in Core. A
   real bulb would mean changing the walk network (a Core change, not art).
3. **Paved crosswalk landings and corners** across the 0.75 m verge (my default, so dogs never
   cross lawn to reach a crosswalk). The lawn round's placeholder streets left one landing per
   corner as lawn.
4. **Square intersection corners.** The asphalt has no curb-return radius at FourWay/Tee corners,
   to keep the footprint exact. A radius of 0.75 m or less would stay off the sidewalk if you want
   softer corners.
5. **Sidewalk height.** The flat-curb look puts sidewalks 1.2 cm up, but Core still has
   `SidewalkSurfaceHeight` = 0.2 m (the Kenney kit's raised band), so dogs would float. Tracked in
   #839.

## Not done here

- No Unity work: importing, the run-time tile-mesh builder and the asphalt shader come later
  (#776, #809).
- The 512² vs 1024² asphalt choice is argued, not rendered side by side. Crawl and z-fighting on a
  real phone are not measured.
- Driveway and walkway surfaces and the sidewalk texture are #803's. The sidewalks here reuse the
  lawn round's concrete.
