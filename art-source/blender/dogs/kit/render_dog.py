"""Review renders for a breed's dog (EEVEE, headless).

  python3 render_dog.py <breed> <shot> [outdir]   shot = turnaround | gamecam | face | coats | rig | pivots |
                                                         splash | lod | mouth | hips | joins | sit | chest | all
Writes <outdir>/<file>.png (default: <breed>/out/, git-ignored; the committed sheets are in
<breed>/review/). Textures must exist (run export_dog.py first). Renders are not
byte-reproducible. Captions and framing boxes come from the breed's RENDER.

"Before" rows (a breed's earlier design rounds) are optional: RENDER["before"][shot]
names a script in the breed's folder (e.g. the Golden's render_round2.py /
render_round3.py) and the folder under <DH_DOG_ROUNDS> holding that round's
builder, which is not in the repository. Without it those rows are left empty.
gamecam needs the approved base-1 house and street props from art-source/blender
(found relative to this folder, or DH_ART). splash needs DH_SPLASH, the path to
the breed's splash art, which is not in the repo.

Without a GPU, EEVEE runs on Mesa's lavapipe (Vulkan on the CPU); this script
points the Vulkan loader at it when VK_ICD_FILENAMES is unset and the file exists.
"""
import math
import os
import sys

if "VK_ICD_FILENAMES" not in os.environ and os.path.exists("/opt/lavapipe/lvp_icd.json"):
    os.environ["VK_ICD_FILENAMES"] = "/opt/lavapipe/lvp_icd.json"

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import dh_breed  # noqa: E402

if __name__ == "__main__":
    if len(sys.argv) < 2:
        sys.exit(__doc__)
    dh_breed.use(sys.argv[1])
    del sys.argv[1]
import bpy  # noqa: E402
from bpy_extras.object_utils import world_to_camera_view  # noqa: E402
from mathutils import Matrix, Vector  # noqa: E402
from PIL import Image, ImageDraw, ImageFont  # noqa: E402

import dh_dog as D  # noqa: E402
import dh_dog_render as R  # noqa: E402
import dh_dog_rig as RG  # noqa: E402
import subprocess  # noqa: E402

B = D.B
RD = B.RENDER
CAP = RD["captions"]
ROOT = dh_breed.DIR             # the breed's folder: textures/, exports/, review/
COAT0 = list(B.COATS)[0]        # the default coat
OUT = os.path.abspath(sys.argv[2]) if len(sys.argv) > 2 else os.path.join(ROOT, "out")
TMP = os.path.join(OUT, "_parts")
REPO_ART = os.environ.get("DH_ART", os.path.normpath(os.path.join(HERE, "..", "..")))   # art-source/blender
SPLASH = os.environ.get("DH_SPLASH")           # the breed's splash art; not in the repo
ROUNDS = os.environ.get("DH_DOG_ROUNDS", ROOT)  # holds the earlier rounds' builders; not in the repo
STAND_IN = (1.26, 1.50, 1.58)                  # today's in-game dog box (dh_scene.DOG), w x l x h
F1 = "/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf"
F2 = "/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf"
BG = (241, 236, 227)
INK = (40, 36, 32)
SUB = (105, 98, 90)
os.makedirs(TMP, exist_ok=True)


def font(sz, bold=False):
    return ImageFont.truetype(F1 if bold else F2, sz)


def scene(path, res, samples=64, floor_hex=R.FLOOR, **kw):
    R.reset()
    R.setup(path, res=res, samples=samples, **kw)
    if floor_hex:
        R.floor(60, floor_hex)


def dog(coat=COAT0, lod=False, at=(0, 0, 0), yaw=0.0, scale=1.0, pose="rest"):
    parts = D.build(coat, lod=lod, pose=pose)
    arm = D.build.armature
    root = bpy.data.objects.new("dog_root", None)
    bpy.context.scene.collection.objects.link(root)
    arm.parent = root
    root.location = at
    root.rotation_euler = (0, 0, math.radians(yaw))
    root.scale = (scale, scale, scale)
    bpy.context.view_layer.update()
    dog.armature = arm
    return parts


def joints(arm, names=None):
    """World positions of the posed bone heads."""
    return {pb.name: arm.matrix_world @ pb.head for pb in arm.pose.bones if names is None or pb.name in names}


def dots(d, cam, res, pts, r=7):
    for p in pts:
        x, y = project(cam, res, p)
        d.ellipse((x - r, y - r, x + r, y + r), fill=(214, 48, 49), outline=(255, 255, 255), width=2)


HEAD_BOX = tuple(tuple(c) for c in RD["head_box"])       # world box around the head (rest pose)
MOUTH_BOX = tuple(tuple(c) for c in RD["mouth_box"])     # world box around the mouth (close-up)


def project(cam, res, p):
    q = world_to_camera_view(bpy.context.scene, cam, Vector(p))
    return q.x * res[0], (1 - q.y) * res[1]


def header(img, title, sub=None, h=96):
    W = img.width
    out = Image.new("RGB", (W, img.height + h), BG)
    out.paste(img, (0, h))
    d = ImageDraw.Draw(out)
    d.text((28, 18), title, fill=INK, font=font(38, True))
    if sub:
        d.text((30, 62), sub, fill=SUB, font=font(22))
    return out


def label(d, x, y, a, b=None, anchor="mm", fs=24):
    fa, fb = font(fs, True), font(int(fs * 0.85))
    tw = max(d.textlength(a, font=fa), d.textlength(b, font=fb) if b else 0)
    hh = fs + 8 + (int(fs * 0.85) + 6 if b else 0)
    x0 = x - tw / 2 - 10 if anchor == "mm" else (x if anchor == "lm" else x - tw - 20)
    d.rounded_rectangle((x0, y - hh / 2, x0 + tw + 20, y + hh / 2), 8, fill=(255, 255, 255), outline=(170, 162, 150))
    d.text((x0 + 10, y - hh / 2 + 4), a, fill=INK, font=fa)
    if b:
        d.text((x0 + 10, y - hh / 2 + fs + 8), b, fill=SUB, font=fb)


def stats():
    import json
    p = os.path.join(ROOT, "exports", "dog_stats.json")
    return json.load(open(p)) if os.path.exists(p) else None


# ---------------------------------------------------------------------------
def turnaround():
    st = stats()
    m = st["measurements_m"] if st else {}
    # (name, yaw, pitch, render res, placement box on the sheet)
    views = [("3/4 front", -40, 14, (1000, 760), (0, 0)), ("Side", -90, 0, (600, 380), (1000, 0)),
             ("Top", 0, 89.9, (600, 380), (1000, 380)), ("Front", 0, 0, (533, 420), (0, 770)),
             ("Back", 180, 0, (533, 420), (533, 770))]
    W, H = 1600, 1190
    sheet = Image.new("RGB", (W, H), BG)
    d = ImageDraw.Draw(sheet)
    for name, yaw, pitch, res, (x, y) in views:
        path = os.path.join(TMP, "turn_%s.png" % name.split()[0].replace("/", ""))
        scene(path, res, samples=48)
        parts = dog()
        cam = R.camera(list(parts.values()), yaw, pitch, res, margin=1.12)
        R.render()
        im = Image.open(path).convert("RGB")
        di = ImageDraw.Draw(im)
        if name in ("Side", "Front"):
            ppm = res[0] / cam.data.ortho_scale
            x0, y0 = res[0] - 30 - ppm, res[1] - 30
            di.rectangle((x0, y0, x0 + ppm, y0 + 8), fill=INK)
            di.text((x0, y0 - 28), "1 m", fill=INK, font=font(20, True))
        sheet.paste(im, (x, y))
        d.text((x + 16, y + 10), name, fill=INK, font=font(26, True))
    x, y = 1100, 800
    lines = [("Sizes (adult)", True), ("Nose to rump  %.2f m" % m.get("nose_to_rump", 0), False),
             ("Nose to tail tip  %.2f m" % m.get("nose_to_tail_tip", 0), False),
             ("Top of head  %.2f m" % m.get("top_of_head", 0), False),
             ("Top of back  %.2f m" % m.get("back_height", 0), False),
             ("Width incl. ears  %.2f m" % m.get("width_incl_ears", 0), False),
             ("Stand-in box  %.2f x %.2f x %.2f m" % STAND_IN, False),
             ("%d triangles, %d bones" % (st["full"]["total_tris"] if st else 0, st["full"]["bones"] if st else 0), False)]
    for i, (t, bold) in enumerate(lines):
        d.text((x, y + i * 44), t, fill=INK if i < 6 else SUB, font=font(26 if i == 0 else 24, bold))
    sheet = header(sheet, *CAP["turnaround"])
    sheet.save(os.path.join(OUT, "turnaround.png"))


# ---------------------------------------------------------------------------
def _import_gltf(path, at=(0, 0, 0), yaw=0.0):
    before = set(bpy.data.objects)
    bpy.ops.import_scene.gltf(filepath=path)
    new = [o for o in bpy.data.objects if o not in before]
    roots = [o for o in new if o.parent is None]
    for r in roots:
        r.location = Vector(r.location) + Vector(at)
        r.rotation_euler = (r.rotation_euler[0], r.rotation_euler[1], r.rotation_euler[2] + math.radians(yaw))
    bpy.context.view_layer.update()
    return new


def stand_in_box(at, yaw=0.0):
    """The in-game stand-in (dh_scene.dog_box, DOG = 1.26 x 1.50 x 1.58) as a thin wire cage."""
    w, l, h = STAND_IN
    me = bpy.data.meshes.new("stand_in")
    vs = [(x, y, z) for z in (0, h) for y in (-l / 2, l / 2) for x in (-w / 2, w / 2)]
    es = [(0, 1), (2, 3), (4, 5), (6, 7), (0, 2), (1, 3), (4, 6), (5, 7), (0, 4), (1, 5), (2, 6), (3, 7)]
    me.from_pydata(vs, es, [])
    ob = bpy.data.objects.new("stand_in", me)
    bpy.context.scene.collection.objects.link(ob)
    mod = ob.modifiers.new("skin", "SKIN")
    for v in ob.data.skin_vertices[0].data:
        v.radius = (0.012, 0.012)
    m = bpy.data.materials.new("ghost")
    m.use_nodes = True
    b = m.node_tree.nodes["Principled BSDF"]
    b.inputs["Base Color"].default_value = R.hex_lin("#3B6EA8")
    b.inputs["Emission Color"].default_value = R.hex_lin("#3B6EA8")
    b.inputs["Emission Strength"].default_value = 0.6
    ob.data.materials.append(m)
    ob.location = at
    ob.rotation_euler = (0, 0, math.radians(yaw))
    return ob


def gamecam():
    res = (1600, 1100)
    path_w = os.path.join(TMP, "game_wide.png")
    path_c = os.path.join(TMP, "game_close.png")
    props = os.path.join(REPO_ART, "environment", "street-props", "exports")
    house = os.path.join(REPO_ART, "exports", "base-01-level1.glb")
    shots = []
    for path, close in ((path_w, False), (path_c, True)):
        scene(path, (1100, 1100) if close else (1500, 1100), samples=48, floor_hex="#8E9E68")
        # lawn, front walk and sidewalk strip
        walk = bpy.data.meshes.new("walk")
        walk.from_pydata([(-6, -9.6, 0.004), (8, -9.6, 0.004), (8, -8.0, 0.004), (-6, -8.0, 0.004)], [], [(0, 1, 2, 3)])
        wm = bpy.data.materials.new("walk")
        wm.use_nodes = True
        wm.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = R.hex_lin("#BDB8AE")
        walk.materials.append(wm)
        bpy.context.scene.collection.objects.link(bpy.data.objects.new("walk", walk))
        hs = _import_gltf(house) if os.path.exists(house) else []
        hy = _import_gltf(os.path.join(props, "fire_hydrant.gltf"), at=(3.6, -8.4, 0))
        mb = _import_gltf(os.path.join(props, "mailbox.gltf"), at=(-1.4, -7.7, 0))
        parts = dog(at=(1.4, -6.4, 0), yaw=-25)
        pup = dog(at=(3.0, -6.0, 0), yaw=-50, scale=0.55)
        box = stand_in_box((-0.6, -6.0, 0), yaw=-25)
        objs = list(parts.values()) + list(pup.values()) + [box] + hy + mb
        if not close:
            objs += hs
        cam = R.camera([o for o in objs if o.type == "MESH"], -40, 45, (1100, 1100) if close else (1500, 1100),
                       margin=1.10 if close else 1.04)
        R.render()
        shots.append(Image.open(path).convert("RGB"))
    wide, close = shots
    W = 1600
    wide = wide.resize((W, int(wide.height * W / wide.width)), Image.LANCZOS)
    close = close.resize((W, int(close.height * W / close.width)), Image.LANCZOS)
    sheet = Image.new("RGB", (W, wide.height + close.height + 60), BG)
    sheet.paste(wide, (0, 0))
    d = ImageDraw.Draw(sheet)
    d.text((20, wide.height + 14), "Same camera, zoomed in: adult, puppy (0.55 scale), blue cage = today's in-game "
           "box stand-in (%.2f x %.2f x %.2f m)" % STAND_IN, fill=INK, font=font(21, True))
    sheet.paste(close, (0, wide.height + 60))
    sheet = header(sheet, *CAP["gamecam"])
    sheet.save(os.path.join(OUT, "gamecam.png"))


# ---------------------------------------------------------------------------
def face():
    res = (1600, 1200)
    path = os.path.join(OUT, "face-closeup.png")
    scene(path, res, samples=96)
    dog()
    R.camera([R.box_object(*HEAD_BOX)], -35, 8, res, ortho=False, lens=85, margin=1.05)
    R.render()
    im = header(Image.open(path).convert("RGB"), *CAP["face"])
    im.save(path)


def coats():
    res = (1600, 760)
    path = os.path.join(TMP, "coats.png")
    scene(path, res, samples=64)
    allp = []
    coats_ = list(B.COATS)
    xs = [-2.5 + 1.55 * i for i in range(len(coats_))]
    for coat, x in zip(coats_, xs):
        allp += list(dog(coat, at=(x, 0, 0), yaw=-20).values())
    px_ = xs[-1] + 1.25
    pup = dog(COAT0, at=(px_, -0.1, 0), yaw=-20, scale=0.55)
    allp += list(pup.values())
    cam = R.camera(allp, -30, 10, res, margin=1.14)
    R.render()
    im = Image.open(path).convert("RGB")
    d = ImageDraw.Draw(im)
    for t, x in zip([B.COAT_LABELS.get(c, c.capitalize()) for c in coats_] + ["Puppy, 0.55 scale"],
                    xs + [px_]):
        px, py = project(cam, res, (x, -0.9, 0))
        label(d, px, min(py + 30, res[1] - 40), t)
    im = header(im, *CAP["coats"])
    im.save(os.path.join(OUT, "coats-and-puppy.png"))


# ---------------------------------------------------------------------------
BONE_COL = {"root": (90, 90, 90), "hips": (200, 60, 60), "spine": (230, 140, 40), "neck": (220, 190, 40),
            "head": (90, 170, 70), "jaw": (40, 160, 160), "ear": (60, 110, 200), "tail": (150, 80, 190),
            "front": (210, 80, 150), "back": (110, 70, 40)}


def _bone_col(name):
    for k, c in BONE_COL.items():
        if name.startswith(k):
            return c
    return (128, 128, 128)


def _weight_material(ob):
    """Vertex colours = each bone's colour, mixed by weight, with a light per-bone shade."""
    me = ob.data
    names = {g.index: g.name for g in ob.vertex_groups}
    col = me.color_attributes.new("w", "FLOAT_COLOR", "POINT")
    shade = {n: 0.75 + 0.25 * ((i * 7) % 5) / 4 for i, n in enumerate(RG.BONE_NAMES)}
    for v in me.vertices:
        c = [0.0, 0.0, 0.0]
        for g in v.groups:
            n = names[g.group]
            bc = _bone_col(n)
            for k in range(3):
                c[k] += g.weight * bc[k] / 255 * shade[n]
        col.data[v.index].color = (*[(x / 1.0) ** 2.2 for x in c], 1)
    m = bpy.data.materials.new("weights")
    m.use_nodes = True
    nt = m.node_tree
    a = nt.nodes.new("ShaderNodeVertexColor")
    a.layer_name = "w"
    nt.links.new(a.outputs["Color"], nt.nodes["Principled BSDF"].inputs["Base Color"])
    nt.nodes["Principled BSDF"].inputs["Roughness"].default_value = 0.7
    for i in range(len(me.materials)):
        me.materials[i] = m


def rig():
    res = (800, 760)
    ims = []
    for mode in ("weights", "bones"):
        path = os.path.join(TMP, "rig_%s.png" % mode)
        scene(path, res, samples=48)
        parts = dog()
        arm = dog.armature
        if mode == "weights":
            for k in ("body", "ear_L", "ear_R"):
                _weight_material(parts[k])
        cam = R.camera(list(parts.values()), -60, 14, res, margin=1.1)
        R.render()
        im = Image.open(path).convert("RGB")
        d = ImageDraw.Draw(im)
        if mode == "bones":
            for pb in arm.pose.bones:
                a = project(cam, res, arm.matrix_world @ pb.head)
                b = project(cam, res, arm.matrix_world @ pb.tail)
                d.line((a, b), fill=(30, 30, 30), width=7)
                d.line((a, b), fill=_bone_col(pb.name), width=4)
            dots(d, cam, res, joints(arm).values(), r=5)
        d.text((16, 12), "Smooth skin weights (colour = bone)" if mode == "weights" else "32 bones (dots = joints)",
               fill=INK, font=font(26, True))
        ims.append(im)
    sheet = Image.new("RGB", (1600, 760 + 250), BG)
    for i, im in enumerate(ims):
        sheet.paste(im, (i * 800, 0))
    d = ImageDraw.Draw(sheet)
    st = stats()
    y = 790
    keys = [("hips / root", "hips"), ("spine 1-3", "spine"), ("neck", "neck"), ("head", "head"), ("jaw", "jaw"),
            ("ears (2 each)", "ear"), ("tail 1-4", "tail"), ("front: shoulder, elbow, wrist, paw", "front"),
            ("back: hip, stifle, hock, foot", "back")]
    for i, (t, k) in enumerate(keys):
        x, yy = 30 + (i % 3) * 520, y + (i // 3) * 40
        d.rectangle((x, yy + 4, x + 24, yy + 28), fill=BONE_COL[k])
        d.text((x + 36, yy + 2), t, fill=INK, font=font(22))
    if st:
        f = st["full"]
        pieces = ", ".join("%s %d" % (k, v["tris"]) for k, v in f["parts"].items())
        d.text((30, y + 135), "Triangles: %d total - %s" % (f["total_tris"], pieces), fill=SUB, font=font(19))
        w = f["skin_weights"]
        d.text((30, y + 168), "Skin: %d vertices, at most %d bones each (%s), weights normalised. "
               "Eyes, nose -> head; collar -> neck; tongue -> jaw." % (
                   w["vertices"], w["max_influences"],
                   ", ".join("%s: %s" % (k, v) for k, v in w["influence_histogram"].items())), fill=SUB, font=font(19))
    sheet = header(sheet, "Rig - one skinned body, 32 bones",
                   "Body + ears are skinned (<= 4 weights per vertex). Rigid pieces follow their bone.")
    sheet.save(os.path.join(OUT, "rig.png"))


# ---------------------------------------------------------------------------
def pivots():
    res = (900, 900)
    ims = []
    for pose, title in (("rest", "Rest (default pose)"), ("sit", "(a) Sit"),
                        ("wag", "(b) Tail swung, mouth open, ear up")):
        path = os.path.join(TMP, "pose_%s.png" % pose)
        scene(path, res, samples=48)
        parts = dog(pose=pose)
        cam = R.camera(list(parts.values()), -50, 12, res, margin=1.12)
        R.render()
        im = Image.open(path).convert("RGB")
        d = ImageDraw.Draw(im)
        dots(d, cam, res, [p for n, p in joints(dog.armature).items() if n != "root"], r=6)
        d.text((20, 16), title, fill=INK, font=font(30, True))
        ims.append(im)
    W = 1600
    h = int(900 * (W / 3) / 900)
    sheet = Image.new("RGB", (W, h), BG)
    for i, im in enumerate(ims):
        sheet.paste(im.resize((W // 3, h), Image.LANCZOS), (i * W // 3, 0))
    sheet = header(sheet, "Pivot check - skinned static poses (not animation)",
                   "Bones rotated about their joints; the one skin bends with them. Red dots = joints (drawn on top).")
    sheet.save(os.path.join(OUT, "pivot-check.png"))


# ---------------------------------------------------------------------------
def splash():
    res = (1000, 1300)
    path = os.path.join(TMP, "splash3d.png")
    scene(path, res, samples=96)
    parts = dog()
    R.camera(list(parts.values()), -22, 16, res, ortho=False, lens=60, margin=1.45)
    R.render()
    a = Image.open(path).convert("RGB")
    if not SPLASH:
        sys.exit("splash: set DH_SPLASH to the breed's splash art")
    s = Image.open(SPLASH).convert("RGBA")
    s.thumbnail((760, 1200), Image.LANCZOS)
    b = Image.new("RGB", (800, 1300), BG)
    b.paste(s, ((800 - s.width) // 2, (1300 - s.height) // 2), s)
    W = 1600
    a = a.resize((800, 1040), Image.LANCZOS)
    b = b.resize((800, 1040), Image.LANCZOS)
    sheet = Image.new("RGB", (W, 1040), BG)
    sheet.paste(a, (0, 0))
    sheet.paste(b, (800, 0))
    d = ImageDraw.Draw(sheet)
    d.text((20, 16), "3D concept (EEVEE)", fill=INK, font=font(28, True))
    d.text((820, 16), "Splash art (reference)", fill=INK, font=font(28, True))
    sheet = header(sheet, *CAP["splash"])
    sheet.save(os.path.join(OUT, "vs-splash.png"))


# ---------------------------------------------------------------------------
def lod():
    res = (1600, 800)
    path = os.path.join(TMP, "lod.png")
    scene(path, res, samples=48)
    a = dog(at=(-0.8, 0, 0), yaw=-20)
    b = dog(lod=True, at=(0.8, 0, 0), yaw=-20)
    cam = R.camera(list(a.values()) + list(b.values()), -30, 14, res, margin=1.1)
    R.render()
    im = Image.open(path).convert("RGB")
    d = ImageDraw.Draw(im)
    ta = sum(D.tri_count(o) for o in a.values())
    tb = sum(D.tri_count(o) for o in b.values())
    for t, s2, x in (("Full", "%d tris" % ta, -0.8), ("LOD1", "%d tris" % tb, 0.8)):
        px, py = project(cam, res, (x, -0.9, 0))
        label(d, px, min(py + 30, res[1] - 40), t, s2)
    im = header(im, "Full model vs LOD1", "Same field and rig, coarser retopology (its own 512 fur atlas); "
                "fewer sides on the eyes, ears, collar and tongue.")
    im.save(os.path.join(OUT, "lod1.png"))


# ---------------------------------------------------------------------------
MOUTH_VIEWS = [("Side", -90, 0, 70), ("3/4", -38, 8, 70), ("Front", 0, 4, 70), ("Close-up", -58, -4, 120)]


def mouth_tiles(tag, pose):
    """Render the four mouth views of the current builder into TMP/mouth_<tag>_<i>.png."""
    res = (400, 340)
    for i, (name, yaw, pitch, lens) in enumerate(MOUTH_VIEWS):
        path = os.path.join(TMP, "mouth_%s_%d.png" % (tag, i))
        scene(path, res, samples=48)
        dog(pose=pose)
        if name == "Close-up":
            box = R.box_object(*MOUTH_BOX)
            R.camera([box], yaw, pitch, res, ortho=False, lens=lens, margin=1.1)
        else:
            R.camera([R.box_object(*HEAD_BOX)], yaw, pitch, res, ortho=False, lens=lens, margin=1.05)
        R.render()


def before(shot, job):
    """Render a shot's "before" tiles (stem "<prefix>_before_<i>") with the breed's own script for
    an earlier design round, if the breed declares one (RENDER["before"][shot]) and that round's
    builder is under DH_DOG_ROUNDS. job = [[stem, [box lo, box hi], yaw, pitch, lens (0 =
    orthographic), [w, h], margin, pose, cut_y], ...]. Returns the row label, or None."""
    spec = RD.get("before", {}).get(shot)
    if not spec:
        return None
    rounds = os.path.join(ROUNDS, spec["rounds"])
    if os.path.isdir(rounds):
        import json
        subprocess.run([sys.executable, os.path.join(ROOT, spec["script"]), rounds, TMP, json.dumps(job)], check=True)
    return spec["label"]


def mouth():
    mouth_tiles("open", "rest")
    mouth_tiles("closed", "closed")
    lab = before("mouth", [["mouth_before_%d" % i, HEAD_BOX if n != "Close-up" else MOUTH_BOX,
                            y, p, ln, [400, 340], 1.05 if n != "Close-up" else 1.1, "rest", None]
                           for i, (n, y, p, ln) in enumerate(MOUTH_VIEWS)])
    rows = [("open", CAP["mouth_rows"][0]), ("closed", CAP["mouth_rows"][1])] + ([("before", lab)] if lab else [])
    W, th, head_h = 1600, 340, 46
    sheet = Image.new("RGB", (W, len(rows) * (th + head_h)), BG)
    d = ImageDraw.Draw(sheet)
    for r, (tag, title) in enumerate(rows):
        y = r * (th + head_h)
        d.text((16, y + 10), title, fill=INK, font=font(26, True))
        for i, (name, *_rest) in enumerate(MOUTH_VIEWS):
            p = os.path.join(TMP, "mouth_%s_%d.png" % (tag, i))
            if os.path.exists(p):
                sheet.paste(Image.open(p).convert("RGB"), (i * 400, y + head_h))
            d.text((i * 400 + 10, y + head_h + 6), name, fill=SUB, font=font(20, True))
    sheet = header(sheet, *CAP["mouth"])
    sheet.save(os.path.join(OUT, "mouth.png"))


# ---------------------------------------------------------------------------
def hips():
    res = (800, 600)
    views = [("rest", "Rest - side", -90, 0), ("rest", "Rest - rear 3/4", -140, 12),
             ("sit", "Sit - side", -90, 0), ("sit", "Sit - rear 3/4", -140, 12)]
    ims = []
    names = ["hips"] + ["back_%s_L" % n for n in ("hip", "stifle", "hock", "foot")] + \
            ["back_%s_R" % n for n in ("hip", "stifle", "hock", "foot")] + ["tail_1"]
    for pose, title, yaw, pitch in views:
        path = os.path.join(TMP, "hips_%s_%d.png" % (pose, yaw))
        scene(path, res, samples=48)
        dog(pose=pose)
        lo, hi = RD["hips_box"][pose]
        cam = R.camera([R.box_object(lo, hi)], yaw, pitch, res, ortho=True, margin=1.06)
        R.render()
        im = Image.open(path).convert("RGB")
        dr = ImageDraw.Draw(im)
        dots(dr, cam, res, joints(dog.armature, names).values())
        dr.text((16, 12), title, fill=INK, font=font(28, True))
        ims.append(im)
    sheet = Image.new("RGB", (1600, 1200 + 142), BG)
    for i, im in enumerate(ims):
        sheet.paste(im, ((i % 2) * 800, (i // 2) * 600))
    d = ImageDraw.Draw(sheet)
    notes = CAP.get("hips_notes", [])
    for i, t in enumerate(notes):
        d.text((24, 1212 + i * 32), t, fill=INK if i < 2 else SUB, font=font(22))
    sheet = header(sheet, *CAP["hips"])
    sheet.save(os.path.join(OUT, "hips.png"))


# ---------------------------------------------------------------------------
# (name, joint the close-up centres on, offset from it, box half size, yaw, pitch)
JOIN_VIEWS = [tuple(v) for v in RD["join_views"]]


def _join_box(centre, off, half):
    c = [centre[i] + off[i] for i in range(3)]
    return (tuple(c[i] - half[i] for i in range(3)), tuple(c[i] + half[i] for i in range(3)))


def rest_joint(name):
    b = {n: h for n, h, _t, _p in RG.bone_table()}[name]
    return (b[0], b[1] + D.OY, b[2])


def join_tiles(tag, pose):
    res = (520, 460)
    for i, (name, jn, off, half, yaw, pitch) in enumerate(JOIN_VIEWS):
        path = os.path.join(TMP, "join_%s_%d.png" % (tag, i))
        scene(path, res, samples=48)
        dog(pose=pose)
        centre = joints(dog.armature)[jn]
        R.camera([R.box_object(*_join_box(centre, off, half))], yaw, pitch, res, ortho=False, lens=60, margin=1.0)
        R.render()


def joins():
    join_tiles("rest", "rest")
    join_tiles("sit", "sit")
    lab = before("joins", [["join_before_%d" % i, _join_box(rest_joint(jn), off, half), y, p, 60, [520, 460], 1.0,
                            "rest", None] for i, (n, jn, off, half, y, p) in enumerate(JOIN_VIEWS)])
    rows = ([("before", lab)] if lab else []) + [("rest", CAP["joins_rows"][0]), ("sit", CAP["joins_rows"][1])]
    W, th, head_h = 1560, 460, 46
    sheet = Image.new("RGB", (W, len(rows) * (th + head_h)), BG)
    d = ImageDraw.Draw(sheet)
    for r, (tag, title) in enumerate(rows):
        y = r * (th + head_h)
        d.text((16, y + 10), title, fill=INK, font=font(26, True))
        for i, (name, *_rest) in enumerate(JOIN_VIEWS):
            p = os.path.join(TMP, "join_%s_%d.png" % (tag, i))
            if os.path.exists(p):
                sheet.paste(Image.open(p).convert("RGB"), (i * 520, y + head_h))
            d.text((i * 520 + 10, y + head_h + 6), name, fill=SUB, font=font(20, True))
    sheet = header(sheet, *CAP["joins"])
    sheet.save(os.path.join(OUT, "joins.png"))


def sit():
    """Ground-level side views of rest and sit, so ground contact is obvious."""
    import dh_dog_rig as RG
    res = (900, 620)
    tiles = []
    views = [("rest", "Rest - side, camera at ground height", None),
             ("sit", "Sit - side, camera at ground height", None),
             ("sit", "Sit - hind leg and haunch, ground level", tuple(RD["sit_closeups"]["hind"])),
             ("sit", "Sit - front paws, ground level", tuple(RD["sit_closeups"]["front"]))]
    meas = {}
    for i, (pose, title, box) in enumerate(views):
        path = os.path.join(TMP, "sit_%d.png" % i)
        scene(path, res, samples=48)
        parts = dog(pose=pose)
        if pose == "sit" and not meas:
            meas = RG.contacts(parts["body"], D.build.weights)
        if box:     # close-up centred (along the body) on a posed joint
            j = joints(dog.armature, [box[0]])[box[0]]
            lo, hi = (-0.3, j.y - box[1], 0.0), (0.3, j.y + box[1], box[2])
        else:
            lo, hi = RD["body_box"]
        # pitch 0: the camera's optical axis is level, so the floor is seen edge-on as a line
        cam = R.camera([R.box_object((lo[0], lo[1], lo[2] - 0.06), hi)], -90, 0, res, ortho=True, margin=1.08)
        R.render()
        im = Image.open(path).convert("RGB")
        dr = ImageDraw.Draw(im, "RGBA")
        y0 = project(cam, res, (0, 0, 0))[1]
        dr.rectangle((0, y0, res[0], res[1]), fill=(120, 100, 80, 40))
        dr.line((0, y0, res[0], y0), fill=(60, 50, 40, 255), width=2)
        dr.text((res[0] - 12, y0 + 6), "ground z = 0", fill=(60, 50, 40), font=font(18), anchor="ra")
        dr.text((16, 12), title, fill=INK, font=font(26, True))
        tiles.append(im)
    sheet = Image.new("RGB", (1800, 1240 + 150), BG)
    for i, im in enumerate(tiles):
        sheet.paste(im, ((i % 2) * 900, (i // 2) * 620))
    d = ImageDraw.Draw(sheet)
    cm = lambda k: "%+.1f cm" % (meas[k] * 100)
    lines = ["Lowest skinned vertex per part in the sit pose (0 = on the ground, + hovering, - sinking):",
             "haunches / rump %s   hocks %s   hind feet %s   front paws %s   tail %s   whole body %s" % (
                 cm("haunches"), cm("hocks"), cm("hind_feet"), cm("front_paws"), cm("tail"), cm("lowest")),
             "Full model (shown). export_dog.py checks the full model and the LOD: every part within 1 cm, nothing below -1 cm."]
    for i, t in enumerate(lines):
        d.text((24, 1258 + i * 36), t, fill=INK if i == 1 else SUB, font=font(24, i == 1))
    sheet = header(sheet, *CAP["sit"])
    sheet.save(os.path.join(OUT, "sit.png"))


CHEST_VIEWS = [("Front", 0, 4, None), ("Front, section: only y < %g m kept" % RD["chest_cut_y"], 0, 0, RD["chest_cut_y"]),
               ("Side", -90, 2, None), ("Front 3/4", -35, 12, None)]
CHEST_BOX = tuple(tuple(c) for c in RD["chest_box"])      # front half of the dog, rest pose


def chest():
    """The chest from the front, in section, from the side and 3/4 (and the breed's earlier round, if
    it has one): same orthographic cameras. The cut view drops everything behind a plane through
    the chest, so the haunches (wider than the chest) don't set the outline."""
    res = (600, 600)        # square: Blender's ortho scale spans the longer side
    for i, (n, yaw, pitch, cut) in enumerate(CHEST_VIEWS):
        scene(os.path.join(TMP, "chest_now_%d.png" % i), res, samples=48)
        dog()
        cam = R.camera([R.box_object(*CHEST_BOX)], yaw, pitch, res, ortho=True, margin=1.04)
        if cut is not None:
            cam.data.clip_end = cut - cam.location.y
        R.render()
    lab = None
    if os.environ.get("DH_SKIP_BEFORE") != "1":
        lab = before("chest", [["chest_before_%d" % i, CHEST_BOX, y, p, 0, list(res), 1.04, "rest", cut]
                               for i, (n, y, p, cut) in enumerate(CHEST_VIEWS)])
    rows = ([("before", lab)] if lab else []) + [("now", CAP["chest_row"])]
    head_h = 46
    sheet = Image.new("RGB", (len(CHEST_VIEWS) * res[0], len(rows) * (res[1] + head_h)), BG)
    d = ImageDraw.Draw(sheet)
    for r_, (tag, title) in enumerate(rows):
        y = r_ * (res[1] + head_h)
        d.text((16, y + 10), title, fill=INK, font=font(26, True))
        for i, (n, *_x) in enumerate(CHEST_VIEWS):
            p = os.path.join(TMP, "chest_%s_%d.png" % (tag, i))
            if os.path.exists(p):
                sheet.paste(Image.open(p).convert("RGB"), (i * res[0], y + head_h))
            d.text((i * res[0] + 10, y + head_h + 6), n, fill=SUB, font=font(20, True))
    sheet = header(sheet, *CAP["chest"])
    sheet.save(os.path.join(OUT, "chest.png"))


SHOTS = {"turnaround": turnaround, "gamecam": gamecam, "face": face, "coats": coats, "rig": rig,
         "pivots": pivots, "splash": splash, "lod": lod,
         "mouth": mouth, "hips": hips, "joins": joins, "sit": sit, "chest": chest}

if __name__ == "__main__":
    os.makedirs(OUT, exist_ok=True)
    which = sys.argv[1] if len(sys.argv) > 1 else "all"
    for k in (SHOTS if which == "all" else [which]):
        SHOTS[k]()
        print("rendered", k)
