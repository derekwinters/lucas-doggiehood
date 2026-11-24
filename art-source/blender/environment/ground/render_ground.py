"""Proof renders for the lawn ground (#801 concept), all at the game camera
(orthographic, pitch 45, yaw -40) unless noted:

  far-<look>-<approach>.png     a 3 x 3 block of 60 m tiles (180 m), roads
                                and sidewalks on top, zoomed far out, to look
                                for repeats and tile borders
  lot-<look>-<approach>.png     the starter corner lot: ranch (base 2) at
                                level 4, fence style C, on its tile
  mid-<look>-<approach>.png     30 m of open lawn around a tile corner (no
                                roads), game camera: the zoom where a 3-8 m
                                repeat or a too-small variant count shows
  border-<look>-<approach>.png  close-up (6 m) centred on a TILE CORNER (two
                                tile borders cross there); small red pegs at
                                the frame edge mark the border lines
  period-<look>-<approach>.png  close-up (6 m) centred on a point that is a
                                texture-period AND a cell boundary for every
                                look (x = y = 24 m), pegs mark the lines
  edges-street-<look>-<v>.png   close-up of the edge treatments at the
                                street: curb / verge, sidewalk, front
                                walkway, driveway
  edges-fence-<look>-<v>.png    ... and under a fence line (style C)
Sheets (labelled, side by side):
  compare-far.png, compare-lot.png, compare-border.png, compare-period.png,
  compare-variants.png (cells 8 / 12 / 16 far out), compare-edges-{street,fence}.png,
  compare-looks.png (the three looks, lot + far, one approach)
GLBs (self-contained, textures embedded) in glb/:
  ground-<look>-lot.glb         the lot with the lawn as approach `--glb`,
                                the macro baked into vertex colours

Usage: python3 render_ground.py [OUT_DIR] [--looks a,b,c] [--approaches ...]
       [--views far,mid,lot,border,period,edges,sheets,glb] [--samples 24]
       [--glb cells1k8_macro]
OUT_DIR defaults to this folder's out/ (git-ignored).
"""
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import dh_ground as G  # noqa: E402  (puts ../fences, ../street-props, ../trees and the kit on the path)
import bpy  # noqa: E402
import dh_house_kit as K  # noqa: E402
import dh_scene as S  # noqa: E402
import dh_ground_tex as GT  # noqa: E402

OUT = sys.argv[1] if len(sys.argv) > 1 and not sys.argv[1].startswith("--") else os.path.join(HERE, "out")
LOOKS = list(GT.LAWNS)
APPROACHES = list(G.APPROACHES)
VIEWS = {"far", "mid", "lot", "border", "period", "edges", "sheets", "glb"}
SAMPLES = 24
GLB_APPROACH = "rec"
for i, a in enumerate(sys.argv):
    if a == "--looks":
        LOOKS = sys.argv[i + 1].split(",")
    if a == "--approaches":
        APPROACHES = sys.argv[i + 1].split(",")
    if a == "--views":
        VIEWS = set(sys.argv[i + 1].split(","))
    if a == "--samples":
        SAMPLES = int(sys.argv[i + 1])
    if a == "--glb":
        GLB_APPROACH = sys.argv[i + 1]
GLB = os.path.join(OUT, "glb")
BG = K.COMMON["ground_studio"]
FAR_RES = (1600, 1200)
LOT_RES = (1500, 1100)
CLOSE_RES = (1000, 760)
EDGE_RES = (1200, 900)
CLOSE_HALF = 3.0                  # close-ups frame +-3 m around their centre
MID_HALF = 15.0                   # mid view: 30 m of open lawn around a tile corner
BORDER_AT = (30.0, 30.0)          # a tile corner in the far block (tiles centred on multiples of 60)
PERIOD_AT = (24.0, 24.0)          # a period boundary for 3, 4 and 8 m and a 3 m cell boundary
PEG = (0.04, 0.35)                # marker peg half-width, height
PEG_INSET = 0.25                  # pegs this far inside the close-up frame
STREET_FAR_N = 3                  # 3 x 3 tiles
LABEL_H = 64
FONT = "/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf"
COMPARE_COLS = 4


def _clear_camera_and_lights():
    for o in list(bpy.data.objects):
        if o.type in ("CAMERA", "LIGHT"):
            bpy.data.objects.remove(o)


def _hint(x, y, z=0.0):
    h = K.MeshBuilder("hint", {"g": "#000000"})
    h.box((x - 0.05, y - 0.05, z), (x + 0.05, y + 0.05, z + 0.05), "g")
    return h.build()


def _render(path, pts, res, margin=1.0, z_top=0.0):
    _clear_camera_and_lights()
    hints = [_hint(x, y) for x, y in pts] + ([_hint(pts[0][0], pts[0][1], z_top)] if z_top else [])
    K.setup_render(path, res=res, bg=BG, samples=SAMPLES)
    K.frame_camera(hints, margin=margin, res=res)
    for h in hints:
        bpy.data.objects.remove(h)
    K.render()
    print("wrote", path, flush=True)


def _pegs(cx, cy):
    """Red pegs at the close-up's frame edge on the lines x = cx and y = cy."""
    m = K.MeshBuilder("pegs", {"peg": "#E0302A"})
    w, h = PEG
    d = CLOSE_HALF - PEG_INSET
    for x, y in ((cx, cy - d), (cx, cy + d), (cx - d, cy), (cx + d, cy)):
        m.box((x - w, y - w, 0), (x + w, y + w, h), "peg")
    return m.build()


def _remove(objs):
    for o in objs:
        if o.name in bpy.data.objects:
            bpy.data.objects.remove(o)


def _box(c, half):
    return [(c[0] - half, c[1] - half), (c[0] + half, c[1] + half), (c[0] - half, c[1] + half),
            (c[0] + half, c[1] - half)]


def ground_views(look, approaches, stats):
    """far / border / period for one look: one scene, lawn swapped per approach."""
    K.reset_scene()
    tiles = G.tile_grid(STREET_FAR_N)
    streets = [G.street_tile(x0 + G.TILE_SIZE / 2, y0 + G.TILE_SIZE / 2, name="street_%d" % i)
               for i, (x0, y0) in enumerate(tiles)]
    half = G.TILE_SIZE * STREET_FAR_N / 2
    for ap in approaches:
        lawn = G.lawn_tiles(look, ap, tiles)
        stats.setdefault(ap, {})["tris_per_tile"] = G.tri_count(lawn[:1])
        if "far" in VIEWS:
            _render(os.path.join(OUT, "far-%s-%s.png" % (look, ap)), _box((0, 0), half), FAR_RES, margin=1.01)
        if "mid" in VIEWS:
            _render(os.path.join(OUT, "mid-%s-%s.png" % (look, ap)), _box(BORDER_AT, MID_HALF), FAR_RES)
        for view, at in (("border", BORDER_AT), ("period", PERIOD_AT)):
            if view in VIEWS:
                pegs = _pegs(*at)
                _render(os.path.join(OUT, "%s-%s-%s.png" % (view, look, ap)), _box(at, CLOSE_HALF), CLOSE_RES)
                _remove([pegs])
        _remove(lawn)
    _remove(streets)


def lot_views(look, approaches):
    K.reset_scene()
    sc = G.lot_scene()
    qx0, qx1, qy0, qy1 = sc["lot"]["P"]["quad"]
    pts = [(qx0, qy0), (qx1, qy0), (qx0, qy1), (qx1, qy1)]
    for ap in approaches:
        lawn = G.lawn_tiles(look, ap, sc["tiles"])
        _render(os.path.join(OUT, "lot-%s-%s.png" % (look, ap)), pts, LOT_RES, margin=1.02, z_top=6.0)
        _remove(lawn)


def edge_views(look):
    for v in G.EDGE_VARIANTS:
        K.reset_scene()
        objs, frames = G.vignette(v, look)
        G.lawn_tile(look, "plain", -G.TILE_SIZE / 2, -G.TILE_SIZE / 2)
        for fr, pts in frames.items():
            _render(os.path.join(OUT, "edges-%s-%s-%s.png" % (fr, look, v)), pts, EDGE_RES, margin=1.0,
                    z_top=1.4 if fr == "fence" else 0.0)


def export_glb(look, approach):
    K.reset_scene()
    sc = G.lot_scene()
    mat = G.lawn_material(look, approach.replace("_macro", "") if approach.endswith("_macro") else approach)
    lawn = G.lawn_tiles(look, approach, sc["tiles"], mat=mat, bake_macro_vcol=G.APPROACHES[approach]["macro"])
    objs = [o for o in sc["lot"]["objs"]] + sc["fence"] + sc["streets"] + [sc["paving"]] + sc["dogs"] + lawn
    bpy.ops.object.select_all(action="DESELECT")
    for o in objs:
        o.hide_set(False)
        o.select_set(True)
    os.makedirs(GLB, exist_ok=True)
    path = os.path.join(GLB, "ground-%s-lot.glb" % look)
    bpy.ops.export_scene.gltf(filepath=path, export_format="GLB", use_selection=True, export_apply=True,
                              export_yup=True, export_vertex_color="ACTIVE")
    print("wrote", path, flush=True)


# ---------------------------------------------------------------------------
# sheets
# ---------------------------------------------------------------------------
def sheet(cells, out, cols=COMPARE_COLS, scale=0.5):
    """cells: [(path, label)]; missing paths are skipped."""
    from PIL import Image, ImageDraw, ImageFont
    cells = [(p, l) for p, l in cells if os.path.exists(p)]
    if not cells:
        return
    ims = [Image.open(p).convert("RGB") for p, _ in cells]
    w, h = int(ims[0].size[0] * scale), int(ims[0].size[1] * scale)
    cols = min(cols, len(ims))
    rows = (len(ims) + cols - 1) // cols
    sh = Image.new("RGB", (w * cols, (h + LABEL_H) * rows), BG)
    d = ImageDraw.Draw(sh)
    font = ImageFont.truetype(FONT, 22)
    for i, (im, (_, label)) in enumerate(zip(ims, cells)):
        r, c = divmod(i, cols)
        x, y = c * w, r * (h + LABEL_H)
        sh.paste(im.resize((w, h)), (x, y + LABEL_H))
        for k, line in enumerate(label.split("\n")[:2]):
            d.text((x + 12, y + 6 + k * 27), line, fill="#222222", font=font)
    sh.save(out)
    print("wrote", out, flush=True)


def sheets():
    L = lambda k: GT.LAWNS[k]["label"]  # noqa: E731
    A = lambda a: G.APPROACHES[a]["label"]  # noqa: E731
    for view in ("far", "mid", "border", "period", "lot"):
        cells = [(os.path.join(OUT, "%s-%s-%s.png" % (view, lk, ap)), "%s\n%s" % (L(lk), A(ap)))
                 for lk in LOOKS for ap in APPROACHES]
        sheet(cells, os.path.join(OUT, "compare-%s.png" % view), cols=COMPARE_COLS, scale=0.45)
    cells = [(os.path.join(OUT, "far-%s-%s.png" % (lk, ap)), "%s\n%s" % (L(lk), A(ap)))
             for lk in LOOKS for ap in ("cells8", "cells12", "cells16", "cells16_macro")]
    sheet(cells, os.path.join(OUT, "compare-variants.png"), cols=4, scale=0.45)
    for fr in G.VIG_FRAMES:
        cells = [(os.path.join(OUT, "edges-%s-%s-%s.png" % (fr, lk, v)), "%s\n%s" % (L(lk), G.EDGE_VARIANTS[v]["label"]))
                 for lk in LOOKS for v in G.EDGE_VARIANTS]
        sheet(cells, os.path.join(OUT, "compare-edges-%s.png" % fr), cols=3, scale=0.5)
    cells = [(os.path.join(OUT, "lot-%s-%s.png" % (lk, GLB_APPROACH)), "%s\n%s" % (L(lk), A(GLB_APPROACH)))
             for lk in LOOKS]
    cells += [(os.path.join(OUT, "far-%s-%s.png" % (lk, GLB_APPROACH)), "%s, 3 x 3 tiles\n%s" % (L(lk), A(GLB_APPROACH)))
              for lk in LOOKS]
    sheet(cells, os.path.join(OUT, "compare-looks.png"), cols=3, scale=0.5)


if __name__ == "__main__":
    os.makedirs(OUT, exist_ok=True)
    import json
    stats = {}
    for lk in LOOKS:
        st = {}
        if VIEWS & {"far", "mid", "border", "period"}:
            ground_views(lk, APPROACHES, st)
        if "lot" in VIEWS:
            lot_views(lk, APPROACHES)
        if "edges" in VIEWS:
            edge_views(lk)
        if "glb" in VIEWS:
            export_glb(lk, GLB_APPROACH)
        stats[lk] = st
        print("done", lk, flush=True)
    if stats and any(stats.values()):
        with open(os.path.join(OUT, "render_stats.json"), "w") as f:
            json.dump(stats, f, indent=1)
    if "sheets" in VIEWS:
        sheets()
    print("DONE")
