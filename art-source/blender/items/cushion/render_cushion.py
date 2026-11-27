"""Review renders for the cushion (#412) (EEVEE, headless; Mesa lavapipe when there is no GPU).

  python3 render_cushion.py <shot> [outdir]     shot = gamecam | closeup | sheet | posecheck

  gamecam    the cushion in a yard beside the approved base-1 house (level 1) and a picket fence,
             with the approved adult Golden Retriever lying on it, at the game camera (ortho,
             yaw -40, pitch 45): wide, and the same camera zoomed in on the cushion
  closeup    the cushion on its own from several angles (3/4, side with a 10 cm bar, top, front),
             plus a 3/4 view with the dog lying on it, with its size and triangle count
  sheet      render-sheet.png: the two game-camera shots and the close-ups on one page (renders
             gamecam and closeup first if their parts are missing)
  posecheck  side and front views of the lying dog on the cushion, for tuning the pose

outdir defaults to out/ here (git-ignored); the committed renders are in review/. The house, fence
and dog are read from their committed exports under art-source/blender; the cushion is built by
dh_cushion.py. The lying pose is a review stand-in posed here (the dogs have no lying clip yet:
LieDown / RestIdle are #933); it is not exported. Renders are not byte-reproducible. PNGs are
re-saved with Pillow, which writes no metadata chunks.
"""
import json
import math
import os
import sys

if "VK_ICD_FILENAMES" not in os.environ and os.path.exists("/opt/lavapipe/lvp_icd.json"):
    os.environ["VK_ICD_FILENAMES"] = "/opt/lavapipe/lvp_icd.json"

HERE = os.path.dirname(os.path.abspath(__file__))
ART = os.path.normpath(os.path.join(HERE, "..", ".."))          # art-source/blender
sys.path.insert(0, HERE)
sys.path.insert(0, os.path.join(ART, "dogs", "kit"))           # the shared render helpers (read-only)
import dh_cushion as C  # noqa: E402
import bpy  # noqa: E402
from mathutils import Matrix, Vector  # noqa: E402
from PIL import Image, ImageDraw, ImageFont  # noqa: E402
import dh_dog_render as R  # noqa: E402

SHOT = sys.argv[1] if len(sys.argv) > 1 else "gamecam"
OUT = os.path.abspath(sys.argv[2]) if len(sys.argv) > 2 else os.path.join(HERE, "out")
TMP = os.path.join(OUT, "_parts")
HOUSE = os.path.join(ART, "exports", "base-01-level1.glb")
DOG = os.path.join(ART, "dogs", "golden-retriever", "exports", "dog_golden_default.gltf")
FENCES = os.path.join(ART, "environment", "fences", "exports")
F1 = "/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf"
F2 = "/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf"
BG = (241, 236, 227)
INK = (40, 36, 32)
SUB = (105, 98, 90)
GAME_YAW, GAME_PITCH = -40, 45
GRASS = "#8E9E68"

# --- the yard: the cushion beside the house's front-left corner, a picket fence run behind it ---
CUSHION_AT = (-7.2, -1.2, 0.0)
CUSHION_YAW = 0.0               # long side along X, as the slab is placed today
FENCE_PANEL_LEN = 2.375         # fences/README.md: one bay, post centre to post centre
FENCE_Y = 2.2
FENCE_X0, FENCE_PANELS = -12.0, 3
ZOOM_HALF = 1.6                 # half-size of the box the zoomed game-camera shot frames

# --- the lying (sphinx) pose: the review stand-in until #933. Bone directions are in the dog's
# own axes (front -Y, up +Z, +X its left), applied top-down along each chain. ---
DOG_DROP = 0.50                 # the root lowers this far, so the chest rests on the cushion top
DOG_SINK = 0.035                # and settles this far into the soft top
DOG_SHIFT_Y = 0.10              # the body sits this far back, so the front paws reach the front edge
POSE = (
    ("front_shoulder_{s}", (0.0, 0.70, -0.72)),
    ("front_elbow_{s}", (0.0, -1.0, -0.06)),
    ("front_wrist_{s}", (0.0, -1.0, 0.0)),
    ("front_paw_{s}", (0.0, -1.0, 0.0)),
    ("back_hip_{s}", (0.55, -0.62, -0.56)),
    ("back_stifle_{s}", (0.25, 0.95, -0.18)),
    ("back_hock_{s}", (0.10, -1.0, -0.02)),
    ("back_foot_{s}", (0.05, -1.0, 0.0)),
)
TAIL = (("tail_1", (0.15, 1.0, -0.55)), ("tail_2", (0.45, 1.0, -0.15)),
        ("tail_3", (0.85, 0.75, -0.05)), ("tail_4", (1.0, 0.3, 0.0)))
NECK_NOD_DEG = 12.0             # the head tips forward a little, relaxed


def font(sz, bold=False):
    return ImageFont.truetype(F1 if bold else F2, sz)


def header(img, title, sub=None, h=96):
    out = Image.new("RGB", (img.width, img.height + h), BG)
    out.paste(img, (0, h))
    d = ImageDraw.Draw(out)
    d.text((28, 18), title, fill=INK, font=font(38, True))
    if sub:
        d.text((30, 62), sub, fill=SUB, font=font(22))
    return out


def save(img, name):
    """Save through Pillow: IHDR / IDAT / IEND only, no text or C2PA chunks."""
    p = os.path.join(OUT, name)
    img.convert("RGB").save(p, "PNG", optimize=True)
    print("wrote", p)


def scene(path, res, samples=64, floor_hex=R.FLOOR, **kw):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    R.reset()
    R.setup(path, res=res, samples=samples, **kw)
    if floor_hex:
        R.floor(80, floor_hex)


def import_gltf(path, at=(0, 0, 0), yaw=0.0):
    before = set(bpy.data.objects)
    bpy.ops.import_scene.gltf(filepath=path)
    new = [o for o in bpy.data.objects if o not in before]
    for r in (o for o in new if o.parent is None):
        r.rotation_mode = "XYZ"           # the glTF importer leaves nodes in quaternion mode
        r.location = Vector(r.location) + Vector(at)
        r.rotation_euler.z += math.radians(yaw)
    bpy.context.view_layer.update()
    return new


def cushion(at=CUSHION_AT, yaw=CUSHION_YAW):
    ob = C.build()
    ob.location = at
    ob.rotation_euler = (0, 0, math.radians(yaw))
    bpy.context.view_layer.update()
    return ob


def stats():
    p = os.path.join(HERE, "exports", "cushion_stats.json")
    return json.load(open(p)) if os.path.exists(p) else None


# ---------------------------------------------------------------------------
# The lying dog
# ---------------------------------------------------------------------------
def _aim(arm, name, direction):
    """Turn one pose bone about its head so it points along `direction` (armature space)."""
    pb = arm.pose.bones[name]
    cur = (pb.tail - pb.head).normalized()
    q = cur.rotation_difference(Vector(direction).normalized())
    h = pb.head.copy()
    pb.matrix = Matrix.Translation(h) @ q.to_matrix().to_4x4() @ Matrix.Translation(-h) @ pb.matrix
    bpy.context.view_layer.update()


def lying_dog(top_z, at, yaw):
    """The Golden, posed lying (sphinx) with its chest on a surface at height top_z."""
    objs = import_gltf(DOG)
    arm = next(o for o in objs if o.type == "ARMATURE")
    for o in objs:
        if o.parent is None:
            o.location, o.rotation_euler = (0, 0, 0), (0, 0, 0)
    bpy.context.view_layer.update()
    root = arm.pose.bones["root"]
    root.matrix = Matrix.Translation((0, DOG_SHIFT_Y, -DOG_DROP)) @ root.matrix
    bpy.context.view_layer.update()
    for side, sx in (("L", 1.0), ("R", -1.0)):
        for bone, (x, y, z) in POSE:
            _aim(arm, bone.format(s=side), (x * sx, y, z))
    for bone, d in TAIL:
        _aim(arm, bone, d)
    neck = arm.pose.bones["neck"]
    h = neck.head.copy()
    neck.matrix = (Matrix.Translation(h) @ Matrix.Rotation(math.radians(-NECK_NOD_DEG), 4, "X")
                   @ Matrix.Translation(-h) @ neck.matrix)
    bpy.context.view_layer.update()
    meshes = [o for o in objs if o.type == "MESH"]
    chest = lowest_body(meshes)
    for o in objs:
        if o.parent is None:
            o.location = (at[0], at[1], at[2] + top_z - DOG_SINK - chest)
            o.rotation_euler = (0, 0, math.radians(yaw))
    bpy.context.view_layer.update()
    return meshes


def lowest_body(meshes):
    """The chest's lowest point (the body mesh near the dog's middle), so the chest, not a paw,
    rests on the top; paws and hocks may hang over the cushion's edges."""
    body = [o for o in meshes if o.name.endswith("_body")]
    pts = R.world_points(body)
    mid = [p.z for p in pts if abs(p.x) < 0.12 and -0.15 < p.y < 0.45]
    return min(mid)


def dog_on_cushion(at=CUSHION_AT, yaw=CUSHION_YAW):
    """The dog lies along the cushion's long side (the cushion's X), head toward -X."""
    return lying_dog(C.rest_top(), at, yaw - 90)


# ---------------------------------------------------------------------------
def yard():
    objs = [o for o in import_gltf(HOUSE) if o.type == "MESH"]
    for i in range(FENCE_PANELS):
        x = FENCE_X0 + i * FENCE_PANEL_LEN
        objs += [o for o in import_gltf(os.path.join(FENCES, "fence_picket_panel.gltf"), at=(x, FENCE_Y, 0))
                 if o.type == "MESH"]
        objs += [o for o in import_gltf(os.path.join(FENCES, "fence_picket_line_post.gltf"),
                                        at=(x - FENCE_PANEL_LEN / 2, FENCE_Y, 0)) if o.type == "MESH"]
    objs += [o for o in import_gltf(os.path.join(FENCES, "fence_picket_end_post.gltf"),
                                    at=(FENCE_X0 + (FENCE_PANELS - 0.5) * FENCE_PANEL_LEN, FENCE_Y, 0))
             if o.type == "MESH"]
    return objs


def gamecam():
    shots = []
    for tag, res, close in (("wide", (1500, 1100), False), ("close", (1500, 1000), True)):
        path = os.path.join(TMP, "game_%s.png" % tag)
        scene(path, res, samples=48, floor_hex=GRASS)
        house = yard()
        cu = cushion()
        dog = dog_on_cushion()
        if close:
            cx, cy = CUSHION_AT[0], CUSHION_AT[1]
            frame = [R.box_object((cx - ZOOM_HALF, cy - ZOOM_HALF, 0), (cx + ZOOM_HALF, cy + ZOOM_HALF, 1.0))]
        else:
            frame = house + [cu] + dog
        R.camera(frame, GAME_YAW, GAME_PITCH, res, margin=1.06 if not close else 1.0)
        R.render()
        shots.append(Image.open(path).convert("RGB"))
    W = 1600
    wide, close = [s.resize((W, int(s.height * W / s.width)), Image.LANCZOS) for s in shots]
    sheet = Image.new("RGB", (W, wide.height + close.height + 60), BG)
    sheet.paste(wide, (0, 0))
    d = ImageDraw.Draw(sheet)
    d.text((20, wide.height + 14), "Same camera, zoomed in on the cushion", fill=INK, font=font(21, True))
    sheet.paste(close, (0, wide.height + 60))
    st = stats()
    sub = ("%.2f x %.2f m (slab 1.4 x 1.0), %d triangles; the adult Golden lying on it, beside the base-1 house"
           % (st["size_xyz_m"][0], st["size_xyz_m"][1], st["tris"]["total"])) if st else None
    save(header(sheet, "Cushion in a yard at the game camera (ortho, yaw -40, pitch 45)", sub), "gamecam.png")


CLOSE_VIEWS = (   # (label, yaw, pitch, res, paste at, with dog)
    ("3/4", -40, 30, (1000, 640), (0, 0), False),
    ("Side (long edge)", 0, 0, (600, 320), (1000, 0), False),
    ("Top", 0, 89.9, (600, 320), (1000, 320), False),
    ("End", -90, 0, (500, 360), (0, 640), False),
    ("With the adult Golden lying on it", -40, 30, (1100, 600), (500, 640), True),
)
SLAB_VIEW = ("Today's graybox slab, same 3/4 view", -40, 30, (500, 240), (0, 1000))
SLAB_SIZE = (1.4, 1.0, 0.35)    # DecorationView's cube scale (X, Z, Y in Unity)
SLAB_HEX = "#FF6F61"            # DecorationView's cushion colour


def closeup():
    st = stats()
    W, H = 1600, 1240
    sheet = Image.new("RGB", (W, H), BG)
    d = ImageDraw.Draw(sheet)
    for i, (name, yaw, pitch, res, (x, y), with_dog) in enumerate(CLOSE_VIEWS):
        path = os.path.join(TMP, "close_%d.png" % i)
        scene(path, res, samples=48)
        cu = cushion(at=(0, 0, 0))
        objs = [cu] + (dog_on_cushion(at=(0, 0, 0)) if with_dog else [])
        cam = R.camera(objs, yaw, pitch, res, margin=1.15)
        R.render()
        im = Image.open(path).convert("RGB")
        if name.startswith("Side"):
            di = ImageDraw.Draw(im)
            ppm = res[0] / cam.data.ortho_scale * 0.1
            x0, y0 = res[0] - 30 - ppm, res[1] - 26
            di.rectangle((x0, y0, x0 + ppm, y0 + 8), fill=INK)
            di.text((x0 - 14, y0 - 28), "10 cm", fill=INK, font=font(20, True))
        sheet.paste(im, (x, y))
        d.text((x + 16, y + 10), name, fill=INK, font=font(26, True))
    name, yaw, pitch, res, (x, y) = SLAB_VIEW
    path = os.path.join(TMP, "close_slab.png")
    scene(path, res, samples=48)
    sx, sy, sz = SLAB_SIZE
    slab = R.box_object((-sx / 2, -sy / 2, 0), (sx / 2, sy / 2, sz))
    slab.hide_render = False
    bpy.context.view_layer.objects.active = slab
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="SELECT")
    bpy.ops.mesh.convex_hull()
    bpy.ops.object.mode_set(mode="OBJECT")
    slab.data.materials.append(C.K.material("slab", SLAB_HEX))
    R.camera([slab], yaw, pitch, res, margin=1.3)
    R.render()
    sheet.paste(Image.open(path).convert("RGB"), (x, y))
    d.text((x + 16, y + 8), name, fill=INK, font=font(20, True))
    sub = ("%d triangles (fabric %d, piping %d, buttons %d); %.3f x %.3f x %.3f m; rest top %.2f m"
           % (st["tris"]["total"], st["tris"]["fabric"], st["tris"]["piping"], st["tris"]["button"],
              *st["size_xyz_m"], st["rest_top_m"])) if st else None
    save(header(sheet, "Cushion close-up: a puffy tufted floor cushion", sub), "closeup.png")


def sheet():
    if not os.path.exists(os.path.join(OUT, "gamecam.png")):
        gamecam()
    if not os.path.exists(os.path.join(OUT, "closeup.png")):
        closeup()
    wide = Image.open(os.path.join(TMP, "game_wide.png")).convert("RGB")
    close = Image.open(os.path.join(TMP, "game_close.png")).convert("RGB")
    cl = Image.open(os.path.join(OUT, "closeup.png")).convert("RGB")
    W, half = 1600, 800
    wide = wide.resize((half, int(wide.height * half / wide.width)), Image.LANCZOS)
    close = close.resize((half, int(close.height * half / close.width)), Image.LANCZOS)
    top_h = max(wide.height, close.height) + 44
    cl = cl.resize((W, int(cl.height * W / cl.width)), Image.LANCZOS)
    page = Image.new("RGB", (W, top_h + cl.height), BG)
    d = ImageDraw.Draw(page)
    page.paste(wide, (0, 44))
    page.paste(close, (half, 44))
    d.text((16, 10), "Game camera: in the yard", fill=INK, font=font(24, True))
    d.text((half + 16, 10), "Game camera: zoomed in", fill=INK, font=font(24, True))
    page.paste(cl, (0, top_h))
    st = stats()
    sub = ("%.2f x %.2f x %.2f m inside the 1.4 x 1.0 m slab; %d triangles of the 6,000 backstop (#799); "
           "the dog lies on a %.2f m high top" % (*st["size_xyz_m"], st["tris"]["total"], st["rest_top_m"])) \
        if st else None
    save(header(page, "Cushion (#412): review sheet", sub), "render-sheet.png")


def posecheck():
    ims = []
    for i, (yaw, pitch) in enumerate(((90, 0), (0, 0), (-40, 30))):
        path = os.path.join(TMP, "pose_%d.png" % i)
        scene(path, (800, 500), samples=16)
        cu = cushion(at=(0, 0, 0))
        dog = dog_on_cushion(at=(0, 0, 0))
        R.camera([cu] + dog, yaw, pitch, (800, 500), margin=1.1)
        R.render()
        ims.append(Image.open(path).convert("RGB"))
    page = Image.new("RGB", (800, 1500), BG)
    for i, im in enumerate(ims):
        page.paste(im, (0, 500 * i))
    save(page, "posecheck.png")


if __name__ == "__main__":
    os.makedirs(TMP, exist_ok=True)
    {"gamecam": gamecam, "closeup": closeup, "sheet": sheet, "posecheck": posecheck}[SHOT]()
