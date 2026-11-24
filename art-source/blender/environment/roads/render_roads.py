"""Review renders for the road kit (#802), all at the game camera
(orthographic, pitch 45, yaw -40), lawn look C under everything:

  starter-<look>.png   the starter FourWay with the approved corner lot (ranch
                       base 2, level 4, fence style C), its sidewalks,
                       walkway and driveway
  far-<look>.png       a 3 x 3 block of mixed tile types (dh_road_geom.BLOCK),
                       180 m, zoomed far out: repeats and tile borders
  cross-<look>.png     close-up of the block's FourWay: crosswalks, stop
                       lines, corners
  border-<look>.png    close-up on the border between the FourWay (0, 0) and
                       the TeeSouth (0, 1); red pegs stand on the border line
  bend-<look>.png      close-up of the TurnNE bend (1, -1)
  cds-<look>.png       close-up of the CulDeSacNorth dead end (-1, -1)
  catalog-<look>.png   all 15 road tile types, one 60 m tile each, labelled
Sheets: compare-looks.png (starter, far, cross, border per look),
        compare-detail.png (bend, cul-de-sac per look)
GLBs (self-contained, textures embedded) in glb/:
  roads-<look>-starter.glb   the starter scene, lawn macro baked into vertex
                             colour, asphalt without the macro (glTF has no
                             detail map)

Usage: python3 render_roads.py [OUT_DIR] [--looks a,b,c]
       [--views starter,block,catalog,sheets,glb] [--samples 24]
OUT_DIR defaults to this folder's out/ (git-ignored).
"""
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import dh_roads as D  # noqa: E402
import bpy  # noqa: E402
import dh_house_kit as K  # noqa: E402
import dh_road_geom as R  # noqa: E402
import dh_ground as G  # noqa: E402

OUT = sys.argv[1] if len(sys.argv) > 1 and not sys.argv[1].startswith("--") else os.path.join(HERE, "out")
LOOKS = list(D.LOOKS)
VIEWS = {"starter", "block", "catalog", "sheets", "glb"}
SAMPLES = 24
for i, a in enumerate(sys.argv):
    if a == "--looks":
        LOOKS = sys.argv[i + 1].split(",")
    if a == "--views":
        VIEWS = set(sys.argv[i + 1].split(","))
    if a == "--samples":
        SAMPLES = int(sys.argv[i + 1])
GLB = os.path.join(OUT, "glb")
BG = K.COMMON["ground_studio"]
FAR_RES = (1600, 1200)
LOT_RES = (1500, 1100)
CLOSE_RES = (1400, 1050)
CAT_RES = (560, 420)
CROSS_HALF = 11.0               # intersection close-up: +-11 m around the tile centre
BORDER_AT = (0.0, R.HALF)       # FourWay (0, 0) | TeeSouth (0, 1)
BORDER_HALF = 7.0
DETAIL_HALF = 14.0
BEND_TILE = (1, -1)             # TurnNE in the block
CDS_TILE = (-1, -1)             # CulDeSacNorth in the block
CDS_FRAME = (0.0, 7.0)          # frame offset toward the open road
STARTER_MARGIN = 6.0            # starter view: the lot quadrant + this much past the tile centre
PEG = (0.05, 0.45)
CAT_SPACING = 80.0
CAT_COLS = 5
LABEL_H = 64
FONT = "/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf"


def _clear_camera_and_lights():
    for o in list(bpy.data.objects):
        if o.type in ("CAMERA", "LIGHT"):
            bpy.data.objects.remove(o)


def _hint(x, y, z=0.0):
    h = K.MeshBuilder("hint", {"g": "#000000"})
    h.box((x - 0.01, y - 0.01, z), (x + 0.01, y + 0.01, z + 0.01), "g")
    return h.build()


def _render(path, pts, res, margin=1.0, z_top=0.0, samples=None):
    _clear_camera_and_lights()
    hints = [_hint(x, y) for x, y in pts] + ([_hint(pts[0][0], pts[0][1], z_top)] if z_top else [])
    K.setup_render(path, res=res, bg=BG, samples=samples or SAMPLES)
    K.frame_camera(hints, margin=margin, res=res)
    for h in hints:
        bpy.data.objects.remove(h)
    K.render()
    print("wrote", path, flush=True)


def _box(c, half):
    return [(c[0] - half, c[1] - half), (c[0] + half, c[1] + half), (c[0] - half, c[1] + half),
            (c[0] + half, c[1] - half)]


def _pegs_on_y(y, xs):
    m = K.MeshBuilder("pegs", {"peg": "#E0302A"})
    w, h = PEG
    for x in xs:
        m.box((x - w, y - w, 0), (x + w, y + w, h), "peg")
    return m.build()


def starter_view(look):
    sc = D.starter_scene(look)
    cx, cy = sc["tile_centre"]
    qx0, qx1, qy0, qy1 = sc["lot"]["P"]["quad"]
    # the lot quadrant plus the intersection beyond its road corner
    xs = [qx0, qx1, cx - STARTER_MARGIN if abs(qx0 - cx) > abs(qx1 - cx) else cx + STARTER_MARGIN]
    ys = [qy0, qy1, cy - STARTER_MARGIN if abs(qy0 - cy) > abs(qy1 - cy) else cy + STARTER_MARGIN]
    pts = [(x, y) for x in (min(xs), max(xs)) for y in (min(ys), max(ys))]
    _render(os.path.join(OUT, "starter-%s.png" % look), pts, LOT_RES, margin=1.02, z_top=6.0)


def block_views(look, stats):
    K.reset_scene()
    objs, info = D.build_area(R.BLOCK, look)
    stats["block"] = {"%d,%d %s" % (c, r, R.BLOCK[(c, r)]): v["counts"] for (c, r), v in info.items()}
    half = R.TILE * 1.5
    _render(os.path.join(OUT, "far-%s.png" % look), _box((0, 0), half), FAR_RES, margin=1.01)
    _render(os.path.join(OUT, "cross-%s.png" % look), _box((0, 0), CROSS_HALF), CLOSE_RES)
    pegs = _pegs_on_y(BORDER_AT[1], [BORDER_AT[0] + d for d in (-BORDER_HALF + 0.4, -R.OFF, -1.5, 1.5, R.OFF,
                                                                  BORDER_HALF - 0.4)])
    _render(os.path.join(OUT, "border-%s.png" % look), _box(BORDER_AT, BORDER_HALF), CLOSE_RES)
    bpy.data.objects.remove(pegs)
    bc = R.tile_centre(BEND_TILE)
    _render(os.path.join(OUT, "bend-%s.png" % look), _box(bc, DETAIL_HALF), CLOSE_RES)
    cc = R.tile_centre(CDS_TILE)
    _render(os.path.join(OUT, "cds-%s.png" % look), _box((cc[0] + CDS_FRAME[0], cc[1] + CDS_FRAME[1]),
                                                           DETAIL_HALF), CLOSE_RES)


def catalog(look, stats):
    K.reset_scene()
    cells = []
    for i, t in enumerate(R.ROAD_TYPES):
        r, c = divmod(i, CAT_COLS)
        centre = (c * CAT_SPACING, -r * CAT_SPACING)
        G.lawn_tile(D.LAWN_LOOK, D.LAWN_APPROACH, centre[0] - R.HALF, centre[1] - R.HALF)
        res = D.build_tile(t, (0, 0), look, centre=centre)
        stats.setdefault("tiles", {})[t] = res["counts"]
        cells.append((t, centre))
    paths = []
    for t, centre in cells:
        p = os.path.join(OUT, "cat", "%s-%s.png" % (look, t))
        os.makedirs(os.path.dirname(p), exist_ok=True)
        _render(p, _box(centre, R.HALF), CAT_RES, margin=1.0, samples=min(SAMPLES, 12))
        paths.append((p, t))
    sheet(paths, os.path.join(OUT, "catalog-%s.png" % look), cols=CAT_COLS, scale=1.0,
          title="%s: every road tile type (one 60 m tile each)" % D.LOOKS[look]["label"])


def export_glb(look):
    sc = D.starter_scene(look)
    for o in sc["road_objs"]:
        if o.name.startswith("lawn_"):
            bpy.data.objects.remove(o)
    cx, cy = sc["tile_centre"]
    lawn = []
    for (c, r) in R.STARTER:
        lawn.append(G.lawn_tile(D.LAWN_LOOK, D.LAWN_APPROACH, cx + c * R.TILE - R.HALF, cy + r * R.TILE - R.HALF,
                                bake_macro_vcol=True))
    # roads again with the export material (no macro), so the glTF keeps its textures
    for o in list(bpy.data.objects):
        if o.name.startswith("road_"):
            bpy.data.objects.remove(o)
    for (c, r), t in R.STARTER.items():
        D.build_tile(t, (c, r), look, centre=(cx + c * R.TILE, cy + r * R.TILE), sidewalks=False, export=True)
    bpy.ops.object.select_all(action="DESELECT")
    for o in bpy.data.objects:
        if o.type == "MESH":
            o.hide_set(False)
            o.select_set(True)
    os.makedirs(GLB, exist_ok=True)
    path = os.path.join(GLB, "roads-%s-starter.glb" % look)
    bpy.ops.export_scene.gltf(filepath=path, export_format="GLB", use_selection=True, export_apply=True,
                              export_yup=True, export_vertex_color="ACTIVE")
    print("wrote", path, flush=True)


# ---------------------------------------------------------------------------
# sheets
# ---------------------------------------------------------------------------
def sheet(cells, out, cols=4, scale=0.5, title=None):
    from PIL import Image, ImageDraw, ImageFont
    cells = [(p, l) for p, l in cells if os.path.exists(p)]
    if not cells:
        return
    ims = [Image.open(p).convert("RGB") for p, _ in cells]
    w, h = int(ims[0].size[0] * scale), int(ims[0].size[1] * scale)
    cols = min(cols, len(ims))
    rows = (len(ims) + cols - 1) // cols
    top = LABEL_H if title else 0
    sh = Image.new("RGB", (w * cols, top + (h + LABEL_H) * rows), BG)
    d = ImageDraw.Draw(sh)
    font = ImageFont.truetype(FONT, 22)
    if title:
        d.text((12, 18), title, fill="#222222", font=ImageFont.truetype(FONT, 26))
    for i, (im, (_, label)) in enumerate(zip(ims, cells)):
        r, c = divmod(i, cols)
        x, y = c * w, top + r * (h + LABEL_H)
        sh.paste(im.resize((w, h), Image.LANCZOS), (x, y + LABEL_H))
        for k, line in enumerate(label.split("\n")[:2]):
            d.text((x + 12, y + 6 + k * 27), line, fill="#222222", font=font)
    sh.save(out)
    print("wrote", out, flush=True)


def sheets():
    L = lambda k: D.LOOKS[k]["label"]  # noqa: E731
    names = {"starter": "starter tile + lot", "far": "3 x 3 mixed tiles, far",
             "cross": "intersection close-up", "border": "tile border (red pegs)",
             "bend": "bend (TurnNE) close-up", "cds": "cul-de-sac close-up"}
    cells = [(os.path.join(OUT, "%s-%s.png" % (v, k)), "%s\n%s" % (L(k), names[v]))
             for k in LOOKS for v in ("starter", "far", "cross", "border")]
    sheet(cells, os.path.join(OUT, "compare-looks.png"), cols=4, scale=0.4)
    cells = [(os.path.join(OUT, "%s-%s.png" % (v, k)), "%s: %s" % (L(k), names[v]))
             for k in LOOKS for v in ("bend", "cds")]
    sheet(cells, os.path.join(OUT, "compare-detail.png"), cols=2, scale=0.5)


if __name__ == "__main__":
    import json
    os.makedirs(OUT, exist_ok=True)
    stats = {}
    for lk in LOOKS:
        st = {}
        if "starter" in VIEWS:
            starter_view(lk)
        if "block" in VIEWS:
            block_views(lk, st)
        if "catalog" in VIEWS:
            catalog(lk, st)
        if "glb" in VIEWS:
            export_glb(lk)
        stats[lk] = st
        print("done", lk, flush=True)
    if any(stats.values()):
        with open(os.path.join(OUT, "render_stats.json"), "w") as f:
            json.dump(stats, f, indent=1)
    if "sheets" in VIEWS:
        sheets()
    print("DONE")
