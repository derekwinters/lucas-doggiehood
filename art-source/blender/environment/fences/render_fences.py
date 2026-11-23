"""Review renders and viewer GLBs for the three fence options (#806 concept).

Per style (privacy, picket, splitrail):
  fence-<style>-lineup.png     every piece in a row: the panel at full length
                               and compressed as FenceTiling would (x0.85, and
                               x0.59 as on the ranch's road-side connector),
                               line / corner / end post (against a wall stub),
                               the gate; dog stand-in for scale
  fence-<style>-lot.png        the ranch (base 2) at level 4 on the starter
                               corner lot, fenced, at the game camera
                               (ortho, pitch 45, yaw -40)
  fence-<style>-connector.png  close-up: the road-side connector meeting the
                               ranch's side wall (end post + wall brackets)
  fence-<style>-garage.png     close-up: the other connector meeting the garage
Plus:
  fence-compare.png            the three lot renders side by side, labelled
  glb/fence-<style>-lineup.glb, glb/fence-<style>-ranch-lot.glb
                               self-contained (textures embedded) for a web viewer

Connectors are drawn stopping at the first thing they meet at 0.45 m
(the side wall, the garage end wall or a porch). That is a render choice
only: the spec-literal connector runs on to the base's side wall, through
the garage, and what a fence should do there is Open (#783).

Usage: python3 render_fences.py [OUT_DIR] [--styles privacy,picket,splitrail]
       [--views lineup,lot,connector,garage,compare,glb] [--base 2] [--samples 32]
OUT_DIR defaults to this folder's out/ (git-ignored).
"""
import math
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import dh_fences as F  # noqa: E402  (puts ../street-props, ../trees and the house kit on the path)
import dh_fence_fit as FF  # noqa: E402
import bpy  # noqa: E402
import dh_house_kit as K  # noqa: E402
import dh_scene as S  # noqa: E402

args = [a for a in sys.argv[1:] if not a.startswith("--")]
OUT = args[0] if args else os.path.join(HERE, "out")
STYLES = list(F.STYLES)
VIEWS = {"lineup", "lot", "connector", "garage", "compare", "glb"}
BASE = 2
SAMPLES = 32
for i, a in enumerate(sys.argv):
    if a == "--styles":
        STYLES = sys.argv[i + 1].split(",")
    if a == "--views":
        VIEWS = set(sys.argv[i + 1].split(","))
    if a == "--base":
        BASE = int(sys.argv[i + 1])
    if a == "--samples":
        SAMPLES = int(sys.argv[i + 1])
GLB = os.path.join(OUT, "glb")
os.makedirs(GLB, exist_ok=True)
BG = K.COMMON["ground_studio"]
LOT_RES = (1500, 1100)
CLOSE_RES = (1400, 1000)
LINEUP_RES = (1800, 800)
LINEUP_GAP = 0.6                 # space between pieces in the line-up
LINEUP_SCALES = (1.0, 0.85, 0.588)
WALL_STUB = (0.25, 0.7, 2.4)     # (thick, long, high) wall the end post meets in the line-up
STREET_MARGIN = 12.0             # street drawn this far past the lot quadrant
CLOSE_BOX = 2.6                  # half-size of the close-up framing box
CLOSE_SHIFT = 0.9                # framing centre, out from the junction along the connector
CLOSE_TOP = 2.6                  # framing includes this height above the junction
CLOSE_DOG = (2.6, 3.4)           # dog stand-in (out along the connector, back into the yard)
CLOSE_YAW = {"connector_low": -55, "connector_high": 35}
CLOSE_PITCH = 35
COMPARE_LABEL_H = 70


def _hint(x, y, z=0.0):
    h = K.MeshBuilder("hint", {"g": "#000000"})
    h.box((x - 0.05, y - 0.05, z), (x + 0.05, y + 0.05, z + 0.05), "g")
    return h.build()


def _render(path, frame_objs, res, **cam):
    K.setup_render(path, res=res, bg=BG, samples=SAMPLES)
    K.frame_camera(frame_objs, res=res, **cam)
    K.render()


def lot_scene(style, base=BASE, toward=True):
    lot = FF.build_lot(base, toward)
    res = FF.check(lot)
    ends = FF.contact_ends(res)
    runs = list(lot["runs"])
    lo, hi = ends["connector_low"], ends["connector_high"]
    runs[0] = (lo[0], lo[1], runs[0][2], runs[0][3])
    runs[4] = (runs[4][0], runs[4][1], hi[0], hi[1])
    P = lot["P"]
    fx0, fx1, _, rear = P["fence"]
    house_ends = {(round(lo[0], 4), round(lo[1], 4)), (round(hi[0], 4), round(hi[1], 4))}
    fence, report = F.lay_runs(runs, style, ((fx0 + fx1) / 2, rear / 2), house_ends, name="fence_" + style)
    qx0, qx1, qy0, qy1 = P["quad"]
    B = lot["B"]
    street = S.street(P["road_cl_y"], x_range=(qx0 - STREET_MARGIN, qx1 + STREET_MARGIN),
                      walk_xs=[getattr(B, "DOOR_X", 0.0)], front_y=lot["bounds"][2] + 0.05,
                      side_road_x=P["side_road_x"])
    dogs = [S.dog_box((fx0 + 2.2, rear - 3.0, 0), yaw_deg=120), S.dog_box((fx1 - 1.6, rear - 2.0, 0), yaw_deg=200)]
    return {"lot": lot, "res": res, "fence": fence, "report": report, "street": street, "dogs": dogs,
            "runs": runs}


def lot_frame(sc):
    P = sc["lot"]["P"]
    qx0, qx1, qy0, qy1 = P["quad"]
    hints = [_hint(qx0, qy0), _hint(qx1, qy0), _hint(qx0, qy1), _hint(qx1, qy1)]
    return S.meshes(sc["lot"]["objs"]) + sc["fence"] + hints, hints


def render_lot(style, path):
    sc = lot_scene(style)
    objs, hints = lot_frame(sc)
    K.setup_render(path, res=LOT_RES, bg=BG, samples=SAMPLES)
    K.frame_camera(objs, margin=1.02, res=LOT_RES)
    for h in hints:
        bpy.data.objects.remove(h)
    K.render()
    return sc


def render_close(style, path, which):
    sc = lot_scene(style)
    x, y = (sc["runs"][0][0], sc["runs"][0][1]) if which == "connector_low" else (sc["runs"][4][2], sc["runs"][4][3])
    sgn = -1 if which == "connector_low" else 1
    cx = x + sgn * CLOSE_SHIFT
    hints = [_hint(cx - CLOSE_BOX, y - CLOSE_BOX), _hint(cx + CLOSE_BOX, y + CLOSE_BOX),
             _hint(x, y, CLOSE_TOP)]
    S.dog_box((x + sgn * CLOSE_DOG[0], y + CLOSE_DOG[1], 0), yaw_deg=160)
    K.setup_render(path, res=CLOSE_RES, bg=BG, samples=SAMPLES)
    yaw = CLOSE_YAW[which]
    K.frame_camera(hints, margin=1.0, yaw=yaw, pitch=CLOSE_PITCH, res=CLOSE_RES)
    for h in hints:
        bpy.data.objects.remove(h)
    K.render()


def lineup_scene(style):
    K.reset_scene()
    protos = F.build_style_pieces(style)
    objs, x = [], 0.0

    def place(kind, sx=1.0, dx=None):
        nonlocal x
        src = protos[kind]
        o = bpy.data.objects.new("%s_%s_%d" % (style, kind, len(objs)), src.data)
        bpy.context.scene.collection.objects.link(o)
        w = (src.dimensions.x * sx) if dx is None else dx
        o.location = (x + (w / 2 if kind not in ("end_post",) else 0.0), 0, 0)
        o.scale = (sx, 1, 1)
        objs.append(o)
        x += w + LINEUP_GAP
        return o

    for s in LINEUP_SCALES:
        place("panel", s)
    place("line_post")
    place("corner_post")
    # end post against a wall stub (the wall face is the post's pivot plane);
    # turned 180 deg so the wall is on the far side from this camera
    pal = K.scheme_palette("Dove Gray")
    wb = K.MeshBuilder("wall_stub", pal)
    t, ln, h = WALL_STUB
    ep = place("end_post")
    wx = x - LINEUP_GAP
    ep.location.x = wx
    ep.rotation_euler = (0, 0, math.pi)
    wb.box((wx, -ln / 2, 0), (wx + t, ln / 2, h), "siding")
    objs.append(wb.build())
    x = wx + t + LINEUP_GAP
    place("line_post")
    x -= LINEUP_GAP
    place("gate", dx=F.GATE_LEN)
    x -= LINEUP_GAP
    place("line_post")
    for o in protos.values():
        o.hide_render = True
        o.hide_set(True)
    dog = S.dog_box((x + 1.2, 1.0, 0), yaw_deg=-120)
    ground = K.ground_plane(60, K.COMMON["grass"])
    return objs + [dog], ground


def render_lineup(style, path):
    objs, ground = lineup_scene(style)
    _render(path, objs, LINEUP_RES, margin=1.04, yaw=-22, pitch=14)


def export_selection(path, objs):
    bpy.ops.object.select_all(action="DESELECT")
    for o in objs:
        o.hide_set(False)
        o.select_set(True)
    bpy.ops.export_scene.gltf(filepath=path, export_format="GLB", use_selection=True, export_apply=True,
                              export_yup=True)


def export_glbs(style):
    objs, ground = lineup_scene(style)
    export_selection(os.path.join(GLB, "fence-%s-lineup.glb" % style), objs + [ground])
    K.reset_scene()
    sc = lot_scene(style)
    house = [o for o in sc["lot"]["objs"]]
    export_selection(os.path.join(GLB, "fence-%s-ranch-lot.glb" % style),
                     house + [sc["lot"]["driveway"], sc["street"]] + sc["fence"] + sc["dogs"])


def compare(paths, out):
    from PIL import Image, ImageDraw, ImageFont
    ims = [Image.open(p).convert("RGB") for p in paths]
    w, h = ims[0].size
    sheet = Image.new("RGB", (w * len(ims), h + COMPARE_LABEL_H), BG)
    d = ImageDraw.Draw(sheet)
    font = ImageFont.truetype("/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf", 40)
    for i, (im, st) in enumerate(zip(ims, STYLES)):
        sheet.paste(im, (i * w, COMPARE_LABEL_H))
        s = F.STYLES[st]
        d.text((i * w + 30, 14), "%s  (%.2f m)" % (s["label"], s["height"]), fill="#222222", font=font)
    sheet.save(out)


if __name__ == "__main__":
    os.makedirs(OUT, exist_ok=True)
    lot_paths = []
    for st in STYLES:
        if "lineup" in VIEWS:
            render_lineup(st, os.path.join(OUT, "fence-%s-lineup.png" % st))
        if "lot" in VIEWS or "compare" in VIEWS:
            p = os.path.join(OUT, "fence-%s-lot.png" % st)
            render_lot(st, p)
            lot_paths.append(p)
        if "connector" in VIEWS:
            render_close(st, os.path.join(OUT, "fence-%s-connector.png" % st), "connector_low")
        if "garage" in VIEWS:
            render_close(st, os.path.join(OUT, "fence-%s-garage.png" % st), "connector_high")
        if "glb" in VIEWS:
            export_glbs(st)
        print("done", st)
    if "compare" in VIEWS and len(lot_paths) == len(STYLES):
        compare(lot_paths, os.path.join(OUT, "fence-compare.png"))
    print("DONE")
