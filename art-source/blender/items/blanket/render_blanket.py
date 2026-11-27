"""Review renders for the blanket (#413), EEVEE headless. Renders are not byte-reproducible.

  python3 art-source/blender/items/blanket/render_blanket.py [out_dir] [--only yard,close,compare,footprint,sheet]

Builds the blanket from this folder's committed textures (run export_blanket.py first) and writes:

  blanket-yard.png       the game camera (orthographic, pitch 45, yaw -40) on the front yard of
                         base 1 at level 1, on the approved lawn: the blanket beside the walk with
                         the approved adult Golden Retriever sitting on it (the largest adult dog)
  blanket-yard-close.png the same camera zoomed in on the blanket and the dog
  blanket-closeup.png    a low 3/4 perspective close-up: plaid, fringe, the folded corner showing
                         the fleece reverse
  blanket-compare.png    at the game camera: today's graybox slabs for the bed and the cushion
                         (DecorationView colours, 1.4 x 0.35 x 1.0 m), today's blanket slab, and
                         the new blanket, labelled
  blanket-footprint.png  straight down (orthographic) with the 1.4 x 1.0 m slab footprint drawn as
                         a white outline
  blanket-sheet.png      all of the above on one labelled sheet

out_dir defaults to this folder's out/ (git-ignored). Needs bpy==5.3.0, numpy, Pillow, and the
committed dog, house and ground sets. Without a GPU, EEVEE runs on Mesa's lavapipe; this script
points the Vulkan loader at it when VK_ICD_FILENAMES is unset and the file exists.
"""
import math
import os
import sys

if "VK_ICD_FILENAMES" not in os.environ and os.path.exists("/opt/lavapipe/lvp_icd.json"):
    os.environ["VK_ICD_FILENAMES"] = "/opt/lavapipe/lvp_icd.json"

HERE = os.path.dirname(os.path.abspath(__file__))
ART = os.path.normpath(os.path.join(HERE, "..", ".."))           # art-source/blender
KIT = os.path.join(ART, "dogs", "kit")
GOLDEN = os.path.join(ART, "dogs", "golden-retriever")
sys.path.insert(0, HERE)
sys.path.insert(0, KIT)
sys.path.insert(0, os.path.join(ART, "environment", "ground"))
import dh_breed  # noqa: E402
dh_breed.use(GOLDEN)
import bpy  # noqa: E402
from bpy_extras.object_utils import world_to_camera_view  # noqa: E402
from mathutils import Vector  # noqa: E402
from PIL import Image, ImageDraw, ImageFont  # noqa: E402

import dh_blanket as BL  # noqa: E402
import dh_dog as D  # noqa: E402
import dh_dog_render as R  # noqa: E402
import dh_ground as G  # noqa: E402

argv = sys.argv[1:]
ONLY = None
if "--only" in argv:
    i = argv.index("--only")
    ONLY = set(argv[i + 1].split(","))
    del argv[i:i + 2]
OUT = os.path.abspath(argv[0]) if argv else os.path.join(HERE, "out")
os.makedirs(OUT, exist_ok=True)

TEX = {"plaid": os.path.join(HERE, "textures", "blanket_plaid_color.png"),
       "normal": os.path.join(HERE, "textures", "blanket_weave_normal.png"),
       "back": os.path.join(HERE, "textures", "blanket_back_color.png")}
HOUSE = os.path.join(ART, "exports", "base-01-level1.glb")
LAWN_LOOK, LAWN_APPROACH = "c", "rec"        # the approved lawn (#801, #834)
GAME_YAW, GAME_PITCH = -40, 45
SAMPLES = 48

# yard layout (base 1's pivot at the origin, front facade at y = -3.5)
BLANKET_AT = (-2.6, -6.4)
BLANKET_YAW = -8.0
DOG_AT = (-2.62, -6.30)
DOG_YAW = -32.0
WALK = (-0.65, 0.65, -11.0, -3.6)            # front walkway x0, x1, y0, y1
SIDEWALK_Y = (-12.5, -11.0)

COMPARE_STEP = 2.4                            # metres between items in the comparison
SLAB = (1.4, 0.35, 1.0)                       # DecorationView graybox slab, w x h x d
SLAB_COLOURS = {"bed": "#6C4FC4", "cushion": "#FF6F61", "blanket": "#3FA7D6"}

F1 = "/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf"
F2 = "/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf"
BG = (241, 236, 227)
INK = (40, 36, 32)
SUB = (105, 98, 90)


def font(sz, bold=False):
    return ImageFont.truetype(F1 if bold else F2, sz)


def want(k):
    return ONLY is None or k in ONLY


def scene(path, res):
    R.reset()
    R.setup(path, res=res, samples=SAMPLES)


def lawn(size=40.0):
    return G.lawn_tile(LAWN_LOOK, LAWN_APPROACH, -size / 2, -size / 2, size=size, name="lawn")


def flat(name, x0, x1, y0, y1, z, hex_color, rough=0.9):
    me = bpy.data.meshes.new(name)
    me.from_pydata([(x0, y0, z), (x1, y0, z), (x1, y1, z), (x0, y1, z)], [], [(0, 1, 2, 3)])
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    b = m.node_tree.nodes["Principled BSDF"]
    b.inputs["Base Color"].default_value = R.hex_lin(hex_color)
    b.inputs["Roughness"].default_value = rough
    me.materials.append(m)
    ob = bpy.data.objects.new(name, me)
    bpy.context.scene.collection.objects.link(ob)
    return ob


def blanket(at=(0, 0), yaw=0.0):
    ob = BL.build(TEX)
    ob.location = (at[0], at[1], 0.0)
    ob.rotation_euler = (0, 0, math.radians(yaw))
    bpy.context.view_layer.update()
    return ob


def dog(at, yaw, pose="sit"):
    parts = D.build(None, pose=pose)
    arm = D.build.armature
    root = bpy.data.objects.new("dog_root", None)
    bpy.context.scene.collection.objects.link(root)
    arm.parent = root
    root.location = (at[0], at[1], 0.0)
    root.rotation_euler = (0, 0, math.radians(yaw))
    bpy.context.view_layer.update()
    return list(parts.values())


def import_glb(path):
    before = set(bpy.data.objects)
    bpy.ops.import_scene.gltf(filepath=path)
    bpy.context.view_layer.update()
    return [o for o in bpy.data.objects if o not in before]


def slab(kind, at):
    w, h, d = SLAB
    me = bpy.data.meshes.new("slab_" + kind)
    x, y = at
    vs = [(x + sx * w / 2, y + sy * d / 2, z) for z in (0, h) for sy in (-1, 1) for sx in (-1, 1)]
    fs = [(0, 2, 3, 1), (4, 5, 7, 6), (0, 1, 5, 4), (1, 3, 7, 5), (3, 2, 6, 7), (2, 0, 4, 6)]
    me.from_pydata(vs, [], fs)
    m = bpy.data.materials.new("slab_" + kind)
    m.use_nodes = True
    b = m.node_tree.nodes["Principled BSDF"]
    b.inputs["Base Color"].default_value = R.hex_lin(SLAB_COLOURS[kind])
    b.inputs["Roughness"].default_value = 0.6
    me.materials.append(m)
    ob = bpy.data.objects.new("slab_" + kind, me)
    bpy.context.scene.collection.objects.link(ob)
    return ob


def outline(w, d, at=(0, 0), z=0.003, r=0.006, hex_color="#FFFFFF"):
    """The slab footprint as a thin emissive loop on the ground."""
    x, y = at
    me = bpy.data.meshes.new("footprint")
    vs = [(x - w / 2, y - d / 2, z), (x + w / 2, y - d / 2, z), (x + w / 2, y + d / 2, z), (x - w / 2, y + d / 2, z)]
    me.from_pydata(vs, [(0, 1), (1, 2), (2, 3), (3, 0)], [])
    ob = bpy.data.objects.new("footprint", me)
    bpy.context.scene.collection.objects.link(ob)
    ob.modifiers.new("skin", "SKIN")
    for v in ob.data.skin_vertices[0].data:
        v.radius = (r, r)
    m = bpy.data.materials.new("footprint")
    m.use_nodes = True
    b = m.node_tree.nodes["Principled BSDF"]
    b.inputs["Base Color"].default_value = R.hex_lin(hex_color)
    b.inputs["Emission Color"].default_value = R.hex_lin(hex_color)
    b.inputs["Emission Strength"].default_value = 1.0
    ob.data.materials.append(m)
    return ob


def project(cam, res, p):
    sc = bpy.context.scene
    v = world_to_camera_view(sc, cam, Vector(p))
    return int(v.x * res[0]), int((1 - v.y) * res[1])


def caption(img, title, sub):
    W = img.width
    out = Image.new("RGB", (W, img.height + 92), BG)
    d = ImageDraw.Draw(out)
    d.text((22, 14), title, fill=INK, font=font(28, True))
    d.text((22, 54), sub, fill=SUB, font=font(19))
    out.paste(img, (0, 92))
    return out


# ---------------------------------------------------------------------------
def yard(close=False):
    res = (1100, 900) if close else (1500, 1100)
    name = "blanket-yard-close" if close else "blanket-yard"
    path = os.path.join(OUT, name + ".png")
    scene(path, res)
    lawn()
    flat("walk", WALK[0], WALK[1], WALK[2], WALK[3], 0.012, "#BDB8AE")
    flat("sidewalk", -12, 12, SIDEWALK_Y[0], SIDEWALK_Y[1], 0.012, "#C4BFB5")
    house = import_glb(HOUSE) if os.path.exists(HOUSE) else []
    bl = blanket(BLANKET_AT, BLANKET_YAW)
    parts = dog(DOG_AT, DOG_YAW)
    if close:
        frame = [R.box_object((BLANKET_AT[0] - 1.0, BLANKET_AT[1] - 0.8, 0), (BLANKET_AT[0] + 1.0, BLANKET_AT[1] + 0.8, 1.2))]
        margin = 1.05
    else:
        frame = [R.box_object((-5.0, -9.0, 0), (4.5, 3.5, 4.0))]
        margin = 1.02
    R.camera(frame, GAME_YAW, GAME_PITCH, res, margin=margin)
    R.render()
    im = Image.open(path).convert("RGB")
    if close:
        im = caption(im, "Game camera, zoomed in",
                     "Golden Retriever (adult, approved model) sitting on the blanket; the kit has no lying 'Rest' pose yet")
    else:
        im = caption(im, "In the yard at the game camera",
                     "Base 1 front yard, approved lawn; orthographic, pitch 45, yaw -40; adult Golden on the blanket")
    im.save(path)
    return path


def closeup():
    res = (1400, 1000)
    path = os.path.join(OUT, "blanket-closeup.png")
    scene(path, res)
    lawn(12)
    bl = blanket()
    R.camera([bl], -28, 32, res, margin=1.08, ortho=False, lens=70)
    R.render()
    im = caption(Image.open(path).convert("RGB"), "Close-up",
                 "Wool plaid throw, fringed short ends, front-right corner folded back to show the cream fleece reverse")
    im.save(path)
    return path


def compare():
    res = (1700, 700)
    path = os.path.join(OUT, "blanket-compare.png")
    scene(path, res)
    lawn(30)
    # line the four up along the camera's screen-right axis so they read left to right
    rx, ry = math.cos(math.radians(GAME_YAW)), math.sin(math.radians(GAME_YAW))
    spots = [((k - 1.5) * COMPARE_STEP * rx, (k - 1.5) * COMPARE_STEP * ry) for k in range(4)]
    objs = [slab("bed", spots[0]), slab("cushion", spots[1]), slab("blanket", spots[2]), blanket(spots[3])]
    cam = R.camera(objs, GAME_YAW, GAME_PITCH, res, margin=1.25)
    R.render()
    im = Image.open(path).convert("RGB")
    d = ImageDraw.Draw(im)
    labels = ["Bed (graybox slab today)", "Cushion (graybox slab today)", "Blanket (graybox slab today)",
              "Blanket (this model)"]
    for (x, y), t in zip(spots, labels):
        px, _ = project(cam, res, (x, y, 0))
        py = max(project(cam, res, (x + sx * SLAB[0] / 2, y + sy * SLAB[2] / 2, 0))[1]
                 for sx in (-1, 1) for sy in (-1, 1))
        d.text((px, py + 24), t, fill=INK, font=font(20, True), anchor="ma")
    im = caption(im, "Beside the bed and the cushion, at the game camera",
                 "No bed (#411) or cushion (#412) model exists yet, so they are today's DecorationView slabs, 1.4 x 0.35 x 1.0 m")
    im.save(path)
    del objs
    return path


def footprint():
    res = (1200, 900)
    path = os.path.join(OUT, "blanket-footprint.png")
    scene(path, res)
    lawn(10)
    blanket()
    outline(SLAB[0], SLAB[2])
    frame = [R.box_object((-0.8, -0.58, 0), (0.8, 0.58, 0.1))]
    cam = R.camera(frame, 0, 90, res, margin=1.04)
    R.render()
    im = Image.open(path).convert("RGB")
    im = caption(im, "Footprint, straight down",
                 "White outline = today's graybox slab, 1.4 x 1.0 m. export_blanket.py and the tests check every vertex is inside it")
    im.save(path)
    return path


def sheet(paths):
    W = 1600
    ims = [Image.open(p).convert("RGB") for p in paths if os.path.exists(p)]
    rows = []
    # yard full width; then yard-close + closeup side by side; compare full width; footprint half
    def fit(im, w):
        return im.resize((w, int(im.height * w / im.width)), Image.LANCZOS)
    byname = {os.path.basename(p): Image.open(p).convert("RGB") for p in paths if os.path.exists(p)}
    order = [("blanket-yard.png",), ("blanket-yard-close.png", "blanket-closeup.png"), ("blanket-compare.png",),
             ("blanket-footprint.png",)]
    for group in order:
        got = [byname[g] for g in group if g in byname]
        if not got:
            continue
        w = W // len(got) if len(group) > 1 else (W if group[0] != "blanket-footprint.png" else W // 2)
        fitted = [fit(g, w) for g in got]
        h = max(f.height for f in fitted)
        row = Image.new("RGB", (W, h), BG)
        x = 0 if len(fitted) > 1 or w == W else (W - w) // 2
        for f in fitted:
            row.paste(f, (x, 0))
            x += w
        rows.append(row)
    head = 110
    out = Image.new("RGB", (W, head + sum(r.height for r in rows) + 12 * len(rows)), BG)
    d = ImageDraw.Draw(out)
    d.text((24, 18), "Blanket (#413): review sheet", fill=INK, font=font(36, True))
    st = BL
    d.text((24, 66), "Plaid wool throw, %.2f x %.2f m cloth + fringe, inside the 1.4 x 1.0 m slab footprint" % (
        st.BODY_W, st.BODY_D), fill=SUB, font=font(22))
    y = head
    for r in rows:
        out.paste(r, (0, y))
        y += r.height + 12
    p = os.path.join(OUT, "blanket-sheet.png")
    out.save(p)
    del ims
    return p


if __name__ == "__main__":
    done = []
    if want("yard"):
        done.append(yard(False))
    if want("close"):
        done.append(yard(True))
        done.append(closeup())
    if want("compare"):
        done.append(compare())
    if want("footprint"):
        done.append(footprint())
    if want("sheet"):
        done.append(sheet([os.path.join(OUT, n) for n in ("blanket-yard.png", "blanket-yard-close.png",
                                                            "blanket-closeup.png", "blanket-compare.png",
                                                            "blanket-footprint.png")]))
    for p in done:
        print("wrote", p)
