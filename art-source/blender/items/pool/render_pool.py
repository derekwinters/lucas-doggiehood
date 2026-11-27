"""Review renders for the backyard pools (#741). Each pool is its exported .glb (run
export_pool.py first), so the renders show exactly what was exported. Per shape (circle, oval):

  pool-<shape>-yard-gamecam.png   the starter corner lot's fenced back yard (ranch, level 4,
                                  split-rail fence, approved lawn) at the game camera (ortho,
                                  pitch 45, yaw -40), the pool beside an adult Labrador (the
                                  approved yellow export); one framing shared by both shapes
  pool-<shape>-water-closeup.png  the same scene, game camera, framed on the pool and the water
  pool-<shape>-turnaround.png     side, three-quarter and top views on the studio ground with the
                                  Lab for scale, a red ring at the bounding radius, labelled
Plus:
  pool-compare-gamecam.png        the two yard views side by side, labelled

Usage: python3 render_pool.py [OUT_DIR] [--shape circle|oval]
       [--views yard,closeup,turnaround,compare] [--samples 32]
OUT_DIR defaults to this folder's out/ (git-ignored). Without --shape, renders both shapes.
"""
import json
import math
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
ART = os.path.normpath(os.path.join(HERE, "..", ".."))
sys.path.insert(0, HERE)
sys.path.insert(0, os.path.join(ART, "environment", "ground"))
import dh_ground as G  # noqa: E402  (puts ../fences, ../street-props, ../trees and the kit on the path)
import bpy  # noqa: E402
from mathutils import Vector  # noqa: E402
import dh_house_kit as K  # noqa: E402
import pool_core as PC  # noqa: E402

OUT = os.path.join(HERE, "out")
VIEWS = {"yard", "closeup", "turnaround", "compare"}
SAMPLES = 32
SHAPES = list(PC.SHAPES)
argv = sys.argv[1:]
while argv:
    a = argv.pop(0)
    if a == "--views":
        VIEWS = set(argv.pop(0).split(","))
    elif a == "--samples":
        SAMPLES = int(argv.pop(0))
    elif a == "--shape":
        SHAPES = [argv.pop(0)]
    else:
        OUT = a
os.makedirs(OUT, exist_ok=True)

EXPORTS = os.path.join(HERE, "exports")
DOG_GLTF = os.path.join(ART, "dogs", "labrador", "exports", "dog_labrador_yellow.gltf")
BG = K.COMMON["ground_studio"]
LOOK, APPROACH = "c", "rec"       # the approved lawn (#834)
YARD_RES = (1500, 1100)
CLOSE_RES = (1400, 1000)
TURN_RES = (900, 700)
# The starter lot's back yard: house rear wall y = 4.23, deck to y = 7.63 for x >= -1.08, rear
# fence y = 14.9, left fence x = -8.49. Each pool (steps included) keeps at least PoolPlacement's
# one-dog comfort margin (1.27 m) to the fence, house and deck: (pool centre, dog position).
LAYOUT = {
    "circle": ((-4.4, 10.8), (0.2, 11.0)),
    "oval": ((-3.95, 11.5), (1.0, 11.6)),
}
DOG_YAW = -100.0                  # degrees; the Lab faces the pool
YARD_FRAME = ((-9.2, 7.2), (3.6, 15.6))    # plan box framed by the yard views (fence corner in shot)
YARD_TOP = 2.0
CLOSE_HALF = 3.6                  # close-up frames this far round the pool
CLOSE_TOP = 1.9
TURN_DOG_GAP = 1.3                # the Lab stands this far past the bounding ring
TURN_DOG_YAW = -90.0
TURN_VIEWS = (("Side", 0.0, 2.0), ("Three-quarter", -40.0, 28.0), ("Top", -40.0, 90.0))   # label, yaw, pitch
FOOTPRINT_SEGMENTS = 96           # render-only bounding ring on the turnaround's ground
FOOTPRINT_LINE = 0.025
FOOTPRINT_Z = 0.003
FOOTPRINT_HEX = "#C0392B"
LABEL_H = 64
FONT = "/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf"
FONT_PX = 24


def _glb(shape):
    return os.path.join(EXPORTS, "pool_%s.glb" % shape)


def _stats(shape):
    with open(os.path.join(EXPORTS, "pool_%s_stats.json" % shape)) as f:
        return json.load(f)


def _water_text(st):
    w = st["water_m"]
    return ("%g m across" % w[0]) if w[0] == w[1] else ("%g x %g m" % (w[0], w[1]))


def _import(path, at, yaw=0.0):
    before = set(bpy.data.objects)
    bpy.ops.import_scene.gltf(filepath=path)
    new = [o for o in bpy.data.objects if o not in before]
    for r in (o for o in new if o.parent is None):
        r.location = Vector(r.location) + Vector((at[0], at[1], 0.0))
        # the glTF importer leaves objects in quaternion mode, where rotation_euler is ignored
        e = r.matrix_basis.to_euler("XYZ")
        r.rotation_mode = "XYZ"
        r.rotation_euler = (e.x, e.y, e.z + math.radians(yaw))
    bpy.context.view_layer.update()
    return new


def _hints(box, top):
    (x0, y0), (x1, y1) = box
    out = []
    for x, y, z in ((x0, y0, 0), (x1, y0, 0), (x0, y1, 0), (x1, y1, 0), (x0, y0, top)):
        h = K.MeshBuilder("hint", {"hint": "#000000"})   # own slot: K.material reuses by name
        h.box((x - 0.02, y - 0.02, z), (x + 0.02, y + 0.02, z + 0.02), "hint")
        out.append(h.build())
    return out


def _footprint_ring(r):
    """A thin red ring on the ground at the bounding radius, to show the model stays inside it."""
    vs, fs = [], []
    for i in range(FOOTPRINT_SEGMENTS):
        a = 2 * math.pi * i / FOOTPRINT_SEGMENTS
        for rr in (r - FOOTPRINT_LINE, r):
            vs.append((rr * math.cos(a), rr * math.sin(a), FOOTPRINT_Z))
    for i in range(FOOTPRINT_SEGMENTS):
        j = (i + 1) % FOOTPRINT_SEGMENTS
        fs.append((2 * i, 2 * i + 1, 2 * j + 1, 2 * j))
    me = bpy.data.meshes.new("footprint")
    me.from_pydata(vs, [], fs)
    me.materials.append(K.material("DH_Render_footprint", FOOTPRINT_HEX))
    ob = bpy.data.objects.new("footprint", me)
    bpy.context.scene.collection.objects.link(ob)
    return ob


def _clear_camera_and_lights():
    for o in list(bpy.data.objects):
        if o.type in ("CAMERA", "LIGHT"):
            bpy.data.objects.remove(o)


def _render(path, box, top, res, yaw=K.GAME_YAW, pitch=K.GAME_PITCH, margin=1.0):
    _clear_camera_and_lights()
    hints = _hints(box, top)
    K.setup_render(path, res=res, bg=BG, samples=SAMPLES)
    K.frame_camera(hints, margin=margin, yaw=yaw, pitch=pitch, res=res)
    for h in hints:
        bpy.data.objects.remove(h)
    K.render()
    print("wrote", path, flush=True)


def render_yard(shape):
    pool_at, dog_at = LAYOUT[shape]
    K.reset_scene()
    sc = G.lot_scene()
    for d in sc["dogs"]:               # the box stand-ins; the real Lab goes in instead
        bpy.data.objects.remove(d)
    G.lawn_tiles(LOOK, APPROACH, sc["tiles"])
    _import(_glb(shape), pool_at)
    _import(DOG_GLTF, dog_at, DOG_YAW)
    if "yard" in VIEWS:
        _render(os.path.join(OUT, "pool-%s-yard-gamecam.png" % shape), YARD_FRAME, YARD_TOP, YARD_RES)
    if "closeup" in VIEWS:
        px, py = pool_at
        box = ((px - CLOSE_HALF, py - CLOSE_HALF), (px + CLOSE_HALF, py + CLOSE_HALF))
        _render(os.path.join(OUT, "pool-%s-water-closeup.png" % shape), box, CLOSE_TOP, CLOSE_RES)


def render_turnaround(shape):
    from PIL import Image, ImageDraw, ImageFont
    st = _stats(shape)
    r = st["bounding_radius_m"]
    dog_at = (r + TURN_DOG_GAP, 0.0)
    K.reset_scene()
    K.ground_plane(40, BG)
    _import(_glb(shape), (0.0, 0.0))
    _import(DOG_GLTF, dog_at, TURN_DOG_YAW)
    _footprint_ring(r)
    box = ((-r, -r), (dog_at[0] + 1.0, r))
    cells = []
    for label, yaw, pitch in TURN_VIEWS:
        p = os.path.join(OUT, "_turn_%s.png" % label.lower())
        _render(p, box, st["height_m"], TURN_RES, yaw=yaw, pitch=pitch, margin=1.08)
        cells.append((p, label))
    w, h = TURN_RES
    sheet = Image.new("RGB", (w * len(cells), h + 2 * LABEL_H), BG)
    d = ImageDraw.Draw(sheet)
    font = ImageFont.truetype(FONT, FONT_PX)
    for i, (p, label) in enumerate(cells):
        sheet.paste(Image.open(p).convert("RGB"), (i * w, LABEL_H))
        d.text((i * w + 16, 18), label, fill="#222222", font=font)
        os.remove(p)
    caption = ("%s: water %s, footprint %.2f x %.2f m (bounding radius %.2f m, red ring), %.2f m tall, "
               "water %.2f m below the rim, %d triangles. Lab for scale." % (
                   shape.capitalize(), _water_text(st), st["footprint_size_m"][0], st["footprint_size_m"][1],
                   r, st["height_m"], st["water_below_rim_m"], st["tris"]))
    d.text((16, h + LABEL_H + 18), caption, fill="#222222", font=font)
    out = os.path.join(OUT, "pool-%s-turnaround.png" % shape)
    sheet.save(out)
    print("wrote", out, flush=True)


def render_compare():
    """The shapes' yard views (identical framing) side by side, labelled."""
    from PIL import Image, ImageDraw, ImageFont
    cells = []
    for shape in PC.SHAPES:
        st = _stats(shape)
        label = "%s: water %s, footprint %.2f x %.2f m, %d triangles" % (
            shape.capitalize(), _water_text(st), st["footprint_size_m"][0], st["footprint_size_m"][1], st["tris"])
        cells.append((os.path.join(OUT, "pool-%s-yard-gamecam.png" % shape), label))
    w, h = YARD_RES
    sheet = Image.new("RGB", (w * len(cells), h + LABEL_H), BG)
    d = ImageDraw.Draw(sheet)
    font = ImageFont.truetype(FONT, FONT_PX)
    for i, (p, label) in enumerate(cells):
        sheet.paste(Image.open(p).convert("RGB"), (i * w, LABEL_H))
        d.text((i * w + 16, 18), label, fill="#222222", font=font)
    out = os.path.join(OUT, "pool-compare-gamecam.png")
    sheet.save(out)
    print("wrote", out, flush=True)


for s in SHAPES:
    if VIEWS & {"yard", "closeup"}:
        render_yard(s)
    if "turnaround" in VIEWS:
        render_turnaround(s)
if "compare" in VIEWS:
    render_compare()
