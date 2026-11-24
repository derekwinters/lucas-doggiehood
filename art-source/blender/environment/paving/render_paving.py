"""Review renders for the paving (#803; P1 approved, #851), all at the game camera
(orthographic, pitch 45, yaw -40), on the approved road kit (look A) and
lawn (look C):

  starter-<opt>.png   the starter FourWay with the approved corner lot (ranch
                      base 2, level 4: garage, driveway, walkway; fence C)
  drive-<opt>.png     close-up: the driveway and walkway meeting the sidewalk
  far-<opt>.png       the 3 x 3 mixed block (dh_road_geom.BLOCK), 180 m
  border-<opt>.png    close-up on the FourWay (0, 0) | TeeSouth (0, 1) border;
                      red pegs stand on the border line
  landing-<opt>.png   close-up of a FourWay corner: box corner and crosswalk
                      landings
  bend-<opt>.png      close-up of the TurnNE bend (1, -1)
  cds-<opt>.png       close-up of the CulDeSacNorth dead end (-1, -1)
  row-<opt>.png       a straight street of four houses (ROW_BASES) across a tile
                      border (red pegs), at the game camera (view "row");
  row-close-<opt>.png the two houses either side of that border
  steps-<opt>-base<N>.png  close-up of the walkway at base N's front steps
                      (view "steps", bases from --bases; default STEP_BASES)
Sheets: compare-options.png, compare-detail.png.
GLBs (self-contained, textures embedded) in glb/:
  paving-<opt>-starter.glb  the starter scene (lawn macro baked into vertex
                            colour; asphalt without the macro)

Usage: python3 render_paving.py [OUT_DIR] [--opts p1,p2,p3]
       [--views starter,block,steps,sheets,glb] [--bases 2,3,8] [--samples 24]
"""
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
sys.path.insert(0, os.path.normpath(os.path.join(HERE, "..", "roads")))
import dh_paving as P  # noqa: E402
import bpy  # noqa: E402
import dh_house_kit as K  # noqa: E402
import dh_road_geom as R  # noqa: E402
import dh_ground as G  # noqa: E402
import dh_roads as D  # noqa: E402
import render_roads as RR  # noqa: E402  (shares _render, _box, _pegs_on_y, sheet)

OUT = sys.argv[1] if len(sys.argv) > 1 and not sys.argv[1].startswith("--") else os.path.join(HERE, "out")
OPTS = ["p1"]                   # Derek's pick (#851); --opts p1,p2,p3 for the others
VIEWS = {"starter", "block", "sheets", "glb"}
STEP_BASES = [2, 3, 8]           # the starter ranch, the widest steps (porch), the narrowest
ROW_BASES = [1, 3, 8, 9]         # street view: four bases, walkways 1.6 / 2.2 / 1.3 / 1.8 m
ROW_FRAME = (-88.0, -2.0, 2.0, 15.0)     # x0, x1, y0, y1: three houses, the tile border at x = -30
ROW_CLOSE = (-56.0, -6.0, 2.0, 13.0)     # the two houses either side of the border
ROW_RES = (2400, 1500)
for i, a in enumerate(sys.argv):
    if a == "--opts":
        OPTS = sys.argv[i + 1].split(",")
    if a == "--views":
        VIEWS = set(sys.argv[i + 1].split(","))
    if a == "--samples":
        RR.SAMPLES = int(sys.argv[i + 1])
    if a == "--bases":
        STEP_BASES = [int(x) for x in sys.argv[i + 1].split(",")]
RR.OUT = OUT
GLB = os.path.join(OUT, "glb")
LANDING_AT = (R.OFF, R.OFF)      # FourWay corner, tile-local
LANDING_HALF = 5.0
DRIVE_PAD = (3.0, 1.5, 3.0)      # drive close-up: past the walkway/driveway sides, below the curb, above
BORDER_HALF = 7.0
STEPS_PAD = (2.5, 1.0, 2.0)      # steps close-up: past the walkway's sides, below the curb, behind the step
BEND_HALF = 11.0


def starter_views(opt):
    K.reset_scene()
    sc = P.starter_scene(opt)
    cx, cy = sc["tile_centre"]
    qx0, qx1, qy0, qy1 = sc["lot"]["P"]["quad"]
    xs = [qx0, qx1, cx - RR.STARTER_MARGIN if abs(qx0 - cx) > abs(qx1 - cx) else cx + RR.STARTER_MARGIN]
    ys = [qy0, qy1, cy - RR.STARTER_MARGIN if abs(qy0 - cy) > abs(qy1 - cy) else cy + RR.STARTER_MARGIN]
    pts = [(x, y) for x in (min(xs), max(xs)) for y in (min(ys), max(ys))]
    RR._render(os.path.join(OUT, "starter-%s.png" % opt), pts, RR.LOT_RES, margin=1.02, z_top=6.0)
    w = sc["shapes"]["walkway"].bounds
    dx0, dx1 = sc["lot"]["dinfo"]["x"]
    x0, x1 = min(w[0], dx0) - DRIVE_PAD[0], max(w[2], dx1) + DRIVE_PAD[0]
    y0, y1 = cy + R.ROAD_HALF - DRIVE_PAD[1], max(w[3], sc["lot"]["dinfo"]["y"][1]) + DRIVE_PAD[2]
    pts = [(x0, y0), (x1, y0), (x0, y1), (x1, y1)]
    RR._render(os.path.join(OUT, "drive-%s.png" % opt), pts, RR.CLOSE_RES, margin=1.0, z_top=2.5)


def steps_view(opt, n):
    K.reset_scene()
    sc = P.starter_scene(opt, base=n)
    cy = sc["tile_centre"][1]
    w = sc["shapes"]["walkway"].bounds
    st = sc["steps"]
    x0, x1 = w[0] - STEPS_PAD[0], w[2] + STEPS_PAD[0]
    y0, y1 = cy + R.ROAD_HALF - STEPS_PAD[1], st["y_front"] + STEPS_PAD[2]
    pts = [(x0, y0), (x1, y0), (x0, y1), (x1, y1)]
    RR._render(os.path.join(OUT, "steps-%s-base%d.png" % (opt, n)), pts, RR.CLOSE_RES, margin=1.0, z_top=2.5)


def row_view(opt):
    P.street_row(opt, ROW_BASES)
    x0, x1, y0, y1 = ROW_FRAME
    border = K.MeshBuilder("border_pegs", {"peg": "#E0302A"})
    w, h = RR.PEG
    for y in (R.OFF - R.SW / 2 - 0.3, R.OFF + R.SW / 2 + 0.3):     # red pegs either side of the sidewalk at the border
        border.box((-R.HALF - w, y - w, 0), (-R.HALF + w, y + w, h), "peg")
    border.build()
    RR._render(os.path.join(OUT, "row-%s.png" % opt), [(x0, y0), (x1, y0), (x0, y1), (x1, y1)], ROW_RES,
               margin=1.0, z_top=4.0)
    x0, x1, y0, y1 = ROW_CLOSE
    RR._render(os.path.join(OUT, "row-close-%s.png" % opt), [(x0, y0), (x1, y0), (x0, y1), (x1, y1)], ROW_RES,
               margin=1.0, z_top=2.0)


def block_views(opt, stats):
    K.reset_scene()
    objs, info = P.build_area(R.BLOCK, opt)
    stats["block"] = {"%d,%d %s" % (c, r, R.BLOCK[(c, r)]): {"slabs": len(v["slabs"]),
                                                              "tris": K.tri_count(v["paving"]) if v["paving"] else 0}
                      for (c, r), v in info.items()}
    RR._render(os.path.join(OUT, "far-%s.png" % opt), RR._box((0, 0), R.TILE * 1.5), RR.FAR_RES, margin=1.01)
    by = R.HALF
    pegs = RR._pegs_on_y(by, [d for d in (-BORDER_HALF + 0.4, -R.OFF - 0.6, -R.OFF + 0.6, R.OFF - 0.6, R.OFF + 0.6,
                                          BORDER_HALF - 0.4)])
    RR._render(os.path.join(OUT, "border-%s.png" % opt), RR._box((0.0, by), BORDER_HALF), RR.CLOSE_RES)
    bpy.data.objects.remove(pegs)
    RR._render(os.path.join(OUT, "landing-%s.png" % opt), RR._box(LANDING_AT, LANDING_HALF), RR.CLOSE_RES)
    bc = R.tile_centre(RR.BEND_TILE)
    cc = R.Layout(R.BLOCK[RR.BEND_TILE]).corner_c          # frame the bend's corner, not the tile centre
    RR._render(os.path.join(OUT, "bend-%s.png" % opt), RR._box((bc[0] + cc[0], bc[1] + cc[1]), BEND_HALF),
               RR.CLOSE_RES)
    cc = R.tile_centre(RR.CDS_TILE)
    RR._render(os.path.join(OUT, "cds-%s.png" % opt), RR._box((cc[0] + RR.CDS_FRAME[0], cc[1] + RR.CDS_FRAME[1]),
                                                               RR.DETAIL_HALF), RR.CLOSE_RES)


def export_glb(opt):
    K.reset_scene()
    sc = P.starter_scene(opt, export=True)
    cx, cy = sc["tile_centre"]
    for o in list(bpy.data.objects):
        if o.name.startswith("lawn_") or o.name.startswith("road_"):
            bpy.data.objects.remove(o)
    for (c, r), t in R.STARTER.items():
        G.lawn_tile(D.LAWN_LOOK, D.LAWN_APPROACH, cx + c * R.TILE - R.HALF, cy + r * R.TILE - R.HALF,
                    bake_macro_vcol=True)
        D.build_tile(t, (c, r), P.ROAD_LOOK, centre=(cx + c * R.TILE, cy + r * R.TILE), sidewalks=False, export=True)
    bpy.ops.object.select_all(action="DESELECT")
    for o in bpy.data.objects:
        if o.type == "MESH":
            o.hide_set(False)
            o.select_set(True)
    os.makedirs(GLB, exist_ok=True)
    path = os.path.join(GLB, "paving-%s-starter.glb" % opt)
    bpy.ops.export_scene.gltf(filepath=path, export_format="GLB", use_selection=True, export_apply=True,
                              export_yup=True, export_vertex_color="ACTIVE")
    print("wrote", path, flush=True)


def sheets():
    L = lambda k: P.OPTIONS[k]["label"]  # noqa: E731
    names = {"starter": "starter tile + lot", "far": "3 x 3 mixed tiles, far", "border": "tile border (red pegs)",
             "landing": "FourWay corner + landings", "drive": "driveway + walkway at the sidewalk",
             "bend": "bend (TurnNE)", "cds": "cul-de-sac"}
    cells = [(os.path.join(OUT, "%s-%s.png" % (v, k)), "%s\n%s" % (L(k), names[v]))
             for k in OPTS for v in ("starter", "far", "border", "landing")]
    RR.sheet(cells, os.path.join(OUT, "compare-options.png"), cols=4, scale=0.4)
    cells = [(os.path.join(OUT, "%s-%s.png" % (v, k)), "%s\n%s" % (L(k), names[v]))
             for k in OPTS for v in ("drive", "bend", "cds")]
    RR.sheet(cells, os.path.join(OUT, "compare-detail.png"), cols=3, scale=0.45)


if __name__ == "__main__":
    import json
    os.makedirs(OUT, exist_ok=True)
    stats = {}
    for o in OPTS:
        st = {}
        if "starter" in VIEWS:
            starter_views(o)
        if "block" in VIEWS:
            block_views(o, st)
        if "row" in VIEWS:
            row_view(o)
        if "steps" in VIEWS:
            for n in STEP_BASES:
                steps_view(o, n)
        if "glb" in VIEWS:
            export_glb(o)
        stats[o] = st
        print("done", o, flush=True)
    if any(stats.values()):
        with open(os.path.join(OUT, "render_stats.json"), "w") as f:
            json.dump(stats, f, indent=1)
    if "sheets" in VIEWS:
        sheets()
    print("DONE")
