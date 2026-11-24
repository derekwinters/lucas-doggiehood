# Paving (approved, #803, #851)

Concrete sidewalks with expansion joints, front walkways and driveways, on the approved road kit
(look A, #836) and lawn (look C, #801). Derek approved them, and they are kept here as **source
assets only** (#851). Nothing here is imported by Unity, because the folder is outside `Assets/`.

## Derek's pick (2026-11-23)

Derek approved **P1, Classic broom, with aligned joints**: "If it looks like that all the time I'm
okay with it."

- **Concrete:** light `#D6D2C8`, broom finish, tooled joints about every 1.5 m, used for the
  sidewalk, the front walkway and the driveway.
- **Front walkway:** as wide as the narrower bottom step of that house's front steps, read from each
  base's geometry (1.3 – 2.2 m). It is visual only: Core's 2 m walk path is unchanged and runs down
  the middle of the walkway.
- **Driveway:** straight, with no flare, 3.0 m wide (two 1.5 m slabs) from the garage apron to the
  road edge. The sidewalk runs straight across it.
- **Joints line up:** the walkway edges, the driveway edges and the driveway's centre joint all fall
  on sidewalk joints.
- `textures/` and `exports/` hold P1 only. P2 (sawn grey concrete, paver walkway, asphalt driveway)
  and P3 (warm exposed aggregate, flagstone walkway, ribbon driveway) stay selectable in
  `dh_paving.OPTIONS`. Their textures rebuild on demand, byte for byte (see Regenerating).
- `review/` holds the approved renders, plus `compare-options.png` from the concept round for
  context.

## Geometry: today's footprint, from Core

The sidewalk outline is the approved road kit's (`dh_road_geom.Layout.sidewalk`), which
`../roads/check_geometry.py` verifies against Core:
- a 2 m sidewalk at 3.75 – 5.75 m from the road centreline;
- a 0.75 m lawn verge and the flat 0.15 m curb;
- paved crosswalk landings in the verge;
- the bend arcs (sidewalk centre radii 0.25 and 9.75 m) and the 9 m cul-de-sac arc.

This set only decides where the **joints** go (`dh_paving_geom.py`):

- A tile edge is always a joint, so slabs never straddle tiles and joints meet across borders.
- Each run is cut at fixed stations, then each stretch between two stations is split into
  `round(length / 1.5)` equal slabs. The slab layout is computed per tile, from that tile's lots
  only (see "Lots").
- No slab is shorter than `MIN_SLAB` = 0.75 m. A shorter gap drops a soft joint, which only a
  straight tile's centre joint is. A gap between two fixed joints would fail the check, and none
  occurs.
- FourWay/Tee box corner: a 2 x 2 m slab, the two 0.6 x 2 m crosswalk landings and a 0.6 m square.
  Tee closed side: the slab facing each crosswalk is exactly the 2 m landing.
- Bends: radial joints on the outer arc between the tangent points; the inner corner is one slab.
  Cul-de-sac: radial joints on the arc, the two kinks cut on the bisector.

## Lots: walkway, driveway and aligned joints

- **Walkway width = the bottom (narrower) front step.** `dh_paving.front_steps(n)` records every box
  base n builds and takes the frontmost box that stands on the ground across the door's x. The
  walkway is centred on that step and runs from its front edge to the sidewalk's outer edge.

  | Base | 1 | 2 | 3 | 4 | 5 | 6 | 7 | 8 | 9 | 10 |
  |---|---|---|---|---|---|---|---|---|---|---|
  | Walkway (m) | 1.60 | 1.40 | 2.20 | 2.00 | 1.60 | 1.40 | 2.00 | 1.30 | 1.80 | 1.60 |
  | Core centreline clearance (m) | 0.80 | 0.70 | 1.10 | 1.00 | 0.80 | 0.70 | 1.00 | 0.65 | 0.90 | 0.80 |

  Every bottom step is centred on its door, so the clearance of Core's walk-path centreline is half
  the width.
- **Driveway:** 3.0 m, centred on the garage. The garage door is 2.6 m and its apron 2.9 m. It runs
  from the garage apron to the sidewalk's outer edge, with an apron of the same width across the
  verge to the road edge, under the curb band.
- **Aligned joints.** The walkway's two edges, the driveway's two edges and its centre joint are
  fixed sidewalk joints. So the sidewalk slab in front of a walkway is exactly its width, and the
  driveway meets two 1.5 m slabs. The same lines carry into the lot and down the driveway apron.
  Joints across the walkway and driveway come every ~1.5 m, compress-to-fit, so a full slab meets
  the steps and the garage apron.
- **Slab lengths on the 10 bases' lots:**
  - sidewalk run slabs 1.05 – 2.2 m. The 2.2 m slab is base 3's walkway front, and the 1.05 m slab
    is base 6's short gap between its walkway and driveway;
  - walkway slabs 1.2 – 2.2 m;
  - driveway slabs 1.28 – 1.68 m.
- **What the game would pass in.** `dh_paving_geom.sidewalk_slabs(layout, spacing, frontage)` needs
  only the tile type and, for each lot on that tile, these points on the sidewalk run the house
  faces:
  - the walkway's edges: door x ± half the bottom step's width;
  - the driveway's edges and centre: garage centre ± 1.5 m, and the centre.

  Map size does not matter. The layout takes 2.5 – 12.5 ms per tile in plain Python (shapely).
- The house position is the lot scene's. Core places the starter houses differently, which is
  open in #852.

## Heights (#839)

| Surface | Top above the lawn |
|---|---|
| Lawn | 0 |
| Asphalt | 0.010 m |
| Sidewalk, walkway, driveway, apron | **0.012 m** |
| Curb band | 0.013 m |
| Paint | 0.0145 m |

Core's `SidewalkSurfaceHeight` is 0.2 m today, while the art's sidewalk top is **0.012 m**. #839
should match it. Core is not changed here.

## Technique and budget

- **Joints are in the texture, not geometry.** Each slab maps onto one whole slot of a
  1024 x 512 atlas (4 x 2 slots, 240 px slab + 8 px padding), and the slot border is the joint.
  That costs no extra triangles, and mipmapping fades the joints smoothly instead of shimmering.
- **No repeats:** there are 8 slab variants, chosen per slab by a hash of its world centre, plus a
  0.95 – 1.0 grey tone in vertex colour `COLOR_0`. The hash is integer arithmetic on
  centimetre-rounded coordinates, so Core could reproduce it.
- **One material per tile** covers every concrete surface: sidewalk, walkway, driveway and apron.
- **Texture memory (ASTC 6x6, with mips):** 0.62 MB for the atlas's colour and normal maps.
- **Meshes:** 128 – 312 triangles per tile's sidewalk (`exports/paving_stats.json`), and 10 – 26 KB
  of `.bin` per tile.
- Joint width scales with a slab's shape: the 0.6 m landing strips draw their long-side joints at
  about a third of the normal width.

## Files

| File | What |
|---|---|
| `dh_paving_tex.py` | the slab atlas (P1; P2/P3 and pavers/flagstone with `--all`) → `textures/` |
| `dh_paving_geom.py` | pure shapely: slabs, aligned joints, walkway, driveway |
| `dh_paving.py` | Blender meshes and materials, `front_steps`, `starter_scene`, `street_row` |
| `check_paving.py` | numerical check against Core, the road kit and all 10 bases → `paving_report.json` |
| `export_paving.py` | every tile type + the starter lot → `exports/`, with read-back |
| `render_paving.py` | review renders and GLBs → `out/` (git-ignored) |
| `review/` | the approved renders |

`../roads/core_dump` also dumps the starter walkways and sidewalks (`starter_walkways`,
`starter_sidewalks` in `../roads/core_geometry.json`), which `check_paving.py` reads.

## Regenerating

```
python3 dh_paving_tex.py           # P1 textures (fixed seeds); --all for every option's
python3 check_paving.py            # ALL OK, writes paving_report.json
python3 export_paving.py           # ALL OK, writes exports/ (--opts p1,p2,p3 for the others)
python3 render_paving.py out/p1-aligned --views row,starter,steps,glb --bases 3   # review/
```

The road kit's `core_geometry.json` comes from `python3 ../roads/check_geometry.py --refresh`
(needs the .NET SDK).
