"""Review renders for the dog bed (#411) (EEVEE, headless). Renders are not byte-reproducible.

  python3 art-source/blender/items/bed/render_bed.py [out_dir] [--only gamecam,closeup,closeup_dog,turnaround,footprint,sheet]

Writes <out_dir>/<shot>.png (default: this folder's out/, git-ignored). Run export_bed.py first:
the renders import exports/dog_bed.gltf, so they show exactly what was exported.

  gamecam      the bed beside approved base 1 (level 1) on the lawn at the game camera
               (orthographic, 45 deg pitch, yaw -40), an adult Labrador lying on it: the whole
               lot, and the same camera zoomed in
  closeup      the bed alone, perspective 3/4 from the front left
  closeup_dog  the same close-up with the Labrador lying on it
  turnaround   front, left side, back and top views of the bed alone (orthographic)
  footprint    top view with today's 1.4 x 1.0 m graybox slab drawn over the bed, and the
               measured size
  sheet        the four above on one page (renders any that are missing)

The dog is the approved Labrador (dogs/labrador/exports/dog_labrador_yellow.gltf). The dog kit has
no lying clip yet (#933), so LIE below is a render-only stand-in: a prone lie (chest, elbows and
hocks down, the pose #933 describes) on ATTACH_REST. It is not an animation and is not exported.

Without a GPU, EEVEE runs on Mesa's lavapipe (Vulkan on the CPU); this script points the Vulkan
loader at it when VK_ICD_FILENAMES is unset and the file exists.
"""
import json
import math
import os
import sys

if "VK_ICD_FILENAMES" not in os.environ and os.path.exists("/opt/lavapipe/lvp_icd.json"):
    os.environ["VK_ICD_FILENAMES"] = "/opt/lavapipe/lvp_icd.json"

HERE = os.path.dirname(os.path.abspath(__file__))
ART = os.path.normpath(os.path.join(HERE, "..", ".."))            # art-source/blender
sys.path.insert(0, HERE)
sys.path.insert(0, os.path.join(ART, "dogs", "kit"))              # dh_dog_render: scene, lights, camera
import bpy  # noqa: E402
from bpy_extras.object_utils import world_to_camera_view  # noqa: E402
from mathutils import Matrix, Vector  # noqa: E402
from PIL import Image, ImageDraw, ImageFont  # noqa: E402

import check_bed as C  # noqa: E402
import dh_bed as BD  # noqa: E402
import dh_dog_render as R  # noqa: E402

args = [a for a in sys.argv[1:]]
ONLY = None
if "--only" in args:
    i = args.index("--only")
    ONLY = args[i + 1].split(",")
    del args[i:i + 2]
OUT = os.path.abspath(args[0]) if args else os.path.join(HERE, "out")
TMP = os.path.join(OUT, "_parts")
os.makedirs(TMP, exist_ok=True)

BED_GLTF = os.path.join(HERE, "exports", "dog_bed.gltf")
STATS = os.path.join(HERE, "exports", "bed_stats.json")
DOG_GLTF = os.path.join(ART, "dogs", "labrador", "exports", "dog_labrador_yellow.gltf")
HOUSE = os.path.join(ART, "exports", "base-01-level1.glb")
GAME_YAW, GAME_PITCH = -40, 45
CLOSE_HALF = (1.05, 0.85, 0.9)    # the zoomed game-camera shot frames this half-box round the bed
LAWN = "#8E9E68"
BED_AT = (-6.2, -1.2, 0.0)        # beside base 1's chimney gable (-X), the side the game camera sees
F1 = "/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf"
F2 = "/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf"
BG = (241, 236, 227)
INK = (40, 36, 32)
SUB = (105, 98, 90)
SLAB_RGB = (214, 48, 49)
SAMPLES = 48

# The render-only lying pose (#933 will add the real one: "chest, elbows and hocks resting on the
# ground"): a prone lie, front legs folded forward flat, hind legs folded under, tail curled to the
# side. Bone rotations about armature axes (degrees, relative per bone, as in
# dogs/kit/dh_dog_rig.apply_pose), worked out from the Labrador's joint positions; then the dog is
# turned (nose from -Y to -X, toward the game camera) and set on the cushion at ATTACH_REST.
LIE = {
    "neck": (8, 0, 0), "head": (4, 0, 4),
    "front_shoulder_L": (39, 0, 0), "front_elbow_L": (-120, 0, 0), "front_wrist_L": (5, 0, 0),
    "front_paw_L": (64, 0, 0),
    "front_shoulder_R": (39, 0, 0), "front_elbow_R": (-120, 0, 0), "front_wrist_R": (5, 0, 0),
    "front_paw_R": (64, 0, 0),
    "back_hip_L": (-43, 0, 0), "back_stifle_L": (97, 0, 0), "back_hock_L": (-135, 0, 0), "back_foot_L": (69, 0, 0),
    "back_hip_R": (-43, 0, 0), "back_stifle_R": (97, 0, 0), "back_hock_R": (-135, 0, 0), "back_foot_R": (69, 0, 0),
    "tail_1": (-24, 0, 0), "tail_2": (-10, 0, 28), "tail_3": (0, 0, 26), "tail_4": (0, 0, 20),
}
LIE_YAW = -90                      # nose from -Y to -X
LIE_SINK = 0.03                    # the dog presses this far into the cushion
LIE_BACK_Y = 0.0                   # the dog's middle sits this far toward the back bolster


def font(sz, bold=False):
    return ImageFont.truetype(F1 if bold else F2, sz)


def want(shot):
    return ONLY is None or shot in ONLY


def scene(path, res, floor_hex=LAWN, bg=R.BG):
    R.reset()
    R.setup(path, res=res, samples=SAMPLES, bg=bg)
    if floor_hex:
        R.floor(80, floor_hex)


def import_gltf(path, at=(0, 0, 0)):
    before = set(bpy.data.objects)
    bpy.ops.import_scene.gltf(filepath=path)
    new = [o for o in bpy.data.objects if o not in before]
    for o in new:
        if o.parent is None:
            o.location = Vector(o.location) + Vector(at)
        if o.type == "MESH" and o.parent is None and o.name.startswith("Icosphere"):
            o.hide_render = True      # the importer's bone-shape helper
    bpy.context.view_layer.update()
    return new


def bed(at=(0, 0, 0)):
    objs = import_gltf(BED_GLTF, at)
    return [o for o in objs if o.type == "MESH"], [o for o in objs if o.name.startswith(BD.REST_NAME)][0]


def pose_bones(arm, spec):
    for pb in arm.pose.bones:
        pb.rotation_mode = "QUATERNION"
        pb.rotation_quaternion = (1, 0, 0, 0)
    for name, (rx, ry, rz) in spec.items():
        pb = arm.pose.bones[name]
        Rl = pb.bone.matrix_local.to_3x3()
        Q = (Matrix.Rotation(math.radians(rz), 3, "Z") @ Matrix.Rotation(math.radians(ry), 3, "Y")
             @ Matrix.Rotation(math.radians(rx), 3, "X"))
        pb.rotation_quaternion = (Rl.inverted() @ Q @ Rl).to_quaternion()
    bpy.context.view_layer.update()


def lying_dog(rest):
    """The Labrador in the LIE pose, lying on the cushion at the rest empty."""
    objs = import_gltf(DOG_GLTF)
    arm = [o for o in objs if o.type == "ARMATURE"][0]
    meshes = [o for o in objs if o.type == "MESH" and not o.hide_render]
    pose_bones(arm, LIE)
    root = bpy.data.objects.new("dog_root", None)
    bpy.context.scene.collection.objects.link(root)
    arm.parent = root
    root.rotation_mode = "XYZ"
    root.rotation_euler = (0, 0, math.radians(LIE_YAW))
    bpy.context.view_layer.update()
    pts = R.world_points(meshes)
    lo = [min(p[i] for p in pts) for i in range(3)]
    hi = [max(p[i] for p in pts) for i in range(3)]
    rp = rest.matrix_world.translation
    root.location = (rp.x - (lo[0] + hi[0]) / 2, rp.y + LIE_BACK_Y - (lo[1] + hi[1]) / 2, rp.z - LIE_SINK - lo[2])
    bpy.context.view_layer.update()
    return meshes


def project(cam, res, p):
    v = world_to_camera_view(bpy.context.scene, cam, Vector(p))
    return v.x * res[0], (1 - v.y) * res[1]


def header(img, title, sub=None, h=96):
    out = Image.new("RGB", (img.width, img.height + h), BG)
    out.paste(img, (0, h))
    d = ImageDraw.Draw(out)
    d.text((24, 16), title, fill=INK, font=font(34, True))
    if sub:
        d.text((24, 60), sub, fill=SUB, font=font(21))
    return out


def stats():
    with open(STATS) as fh:
        return json.load(fh)


# ---------------------------------------------------------------------------
def gamecam():
    shots = []
    for name, close, res in (("game_wide", False, (1500, 1100)), ("game_close", True, (1100, 1100))):
        path = os.path.join(TMP, name + ".png")
        scene(path, res)
        house = import_gltf(HOUSE)
        bm, rest = bed(BED_AT)
        dog = lying_dog(rest)
        objs = bm + dog + ([] if close else [o for o in house if o.type == "MESH"])
        if close:
            objs = objs + [R.box_object((BED_AT[0] - CLOSE_HALF[0], BED_AT[1] - CLOSE_HALF[1], 0),
                                        (BED_AT[0] + CLOSE_HALF[0], BED_AT[1] + CLOSE_HALF[1], CLOSE_HALF[2]))]
        R.camera(objs, GAME_YAW, GAME_PITCH, res, margin=1.06 if close else 1.04)
        R.render()
        shots.append(Image.open(path).convert("RGB"))
    wide, close = shots
    W = 1600
    wide = wide.resize((W, int(wide.height * W / wide.width)), Image.LANCZOS)
    close = close.resize((W, int(close.height * W / close.width)), Image.LANCZOS)
    sheet = Image.new("RGB", (W, wide.height + close.height + 60), BG)
    sheet.paste(wide, (0, 0))
    d = ImageDraw.Draw(sheet)
    d.text((20, wide.height + 14), "Same game camera, zoomed in on the bed: adult Labrador lying on the cushion",
           fill=INK, font=font(21, True))
    sheet.paste(close, (0, wide.height + 60))
    sheet = header(sheet, "Dog bed at the game camera",
                   "Beside approved base 1 (level 1), orthographic, 45 deg pitch, yaw -40. Lying pose is a "
                   "render-only stand-in (no lying clip yet, #933)")
    sheet.save(os.path.join(OUT, "gamecam.png"))
    close.save(os.path.join(OUT, "gamecam-close.png"))


def closeup(with_dog):
    res = (1600, 1100)
    name = "closeup_dog" if with_dog else "closeup"
    path = os.path.join(TMP, name + ".png")
    scene(path, res)
    bm, rest = bed()
    objs = list(bm)
    if with_dog:
        objs += lying_dog(rest)
    R.camera(objs, -32, 30, res, margin=1.12, ortho=False, lens=70)
    R.render()
    img = Image.open(path).convert("RGB")
    s = stats()
    if with_dog:
        title, sub = "Dog bed close-up, with an adult Labrador", ("Labrador nose to rump 1.46 m on a %.2f x %.2f m bed; "
                                                                  "lying pose is a render-only stand-in (#933)"
                                                                  % tuple(s["size_xyz_m"][:2]))
    else:
        title, sub = "Dog bed close-up", ("Padded bolster with a low step-in front, flat cream fleece cushion. "
                                          "%d triangles, %.2f x %.2f x %.2f m" % (s["tris"], *s["size_xyz_m"]))
    header(img, title, sub).save(os.path.join(OUT, name.replace("_", "-") + ".png"))


def turnaround():
    views = (("Front", 0, 0), ("Left side", -90, 0), ("Back", 180, 0), ("3/4 from above", -40, 55))
    tiles = []
    for label, yaw, pitch in views:
        res = (800, 520)
        path = os.path.join(TMP, "turn_%d_%d.png" % (yaw, pitch))
        scene(path, res, floor_hex="#C9C3B8")
        bm, _ = bed()
        R.camera(bm, yaw, pitch, res, margin=1.12)
        R.render()
        im = Image.open(path).convert("RGB")
        ImageDraw.Draw(im).text((18, 14), label, fill=INK, font=font(26, True))
        tiles.append(im)
    W, H = tiles[0].size
    sheet = Image.new("RGB", (W * 2, H * 2), BG)
    for i, t in enumerate(tiles):
        sheet.paste(t, ((i % 2) * W, (i // 2) * H))
    header(sheet, "Dog bed turnaround", "Orthographic; front (-Y in Blender, +Z in glTF) is the low step-in edge").save(
        os.path.join(OUT, "turnaround.png"))


def footprint():
    res = (1400, 1100)
    path = os.path.join(TMP, "footprint.png")
    scene(path, res, floor_hex="#E4DED3")
    bm, rest = bed()
    frame = R.box_object((-C.SLAB_W / 2 - 0.25, -C.SLAB_D / 2 - 0.25, 0), (C.SLAB_W / 2 + 0.25, C.SLAB_D / 2 + 0.25, 0))
    cam = R.camera(bm + [frame], 0, 90, res, margin=1.05)
    R.render()
    img = Image.open(path).convert("RGB")
    d = ImageDraw.Draw(img)
    hw, hd = C.SLAB_W / 2, C.SLAB_D / 2
    corners = [project(cam, res, (x, y, 0.36)) for x, y in ((-hw, -hd), (hw, -hd), (hw, hd), (-hw, hd))]
    d.line(corners + [corners[0]], fill=SLAB_RGB, width=4)
    rp = project(cam, res, tuple(rest.matrix_world.translation))
    d.ellipse((rp[0] - 9, rp[1] - 9, rp[0] + 9, rp[1] + 9), fill=(40, 90, 200), outline=(255, 255, 255), width=2)
    d.text((rp[0] + 14, rp[1] - 12), "ATTACH_REST", fill=(40, 90, 200), font=font(22, True))
    (x0, y0), (x1, _), (_, y1) = corners[3], corners[2], corners[1]
    d.text(((x0 + x1) / 2, y0 - 40), "slab 1.40 m", fill=SLAB_RGB, font=font(26, True), anchor="mm")
    d.text((x1 + 18, (y0 + y1) / 2), "1.00 m", fill=SLAB_RGB, font=font(26, True), anchor="lm")
    s = stats()
    ok = not s["checks"]["footprint"] and not s["checks"]["pivot"]
    d.text((24, res[1] - 44), "Front (step-in edge) is at the bottom of this view", fill=SUB, font=font(21))
    header(img, "Dog bed footprint check",
           "Red: today's graybox slab, %.1f x %.1f m. Bed measures %.3f x %.3f m, pivot centred on the ground: %s"
           % (C.SLAB_W, C.SLAB_D, s["size_xyz_m"][0], s["size_xyz_m"][1], "fits" if ok else "FAILS")).save(
        os.path.join(OUT, "footprint.png"))


def sheet():
    parts = {"gamecam-close": None, "closeup-dog": None, "turnaround": None, "footprint": None}
    for k in parts:
        p = os.path.join(OUT, k + ".png")
        if not os.path.exists(p):
            {"gamecam-close": gamecam, "closeup-dog": lambda: closeup(True), "turnaround": turnaround,
             "footprint": footprint}[k]()
        parts[k] = Image.open(p).convert("RGB")
    W = 1200
    tiles = []
    for k, cap in (("gamecam-close", "Game camera, adult Labrador lying on it"), ("closeup-dog", "Close-up"),
                   ("turnaround", "Turnaround"), ("footprint", "Footprint check")):
        im = parts[k]
        im = im.resize((W, int(im.height * W / im.width)), Image.LANCZOS)
        t = Image.new("RGB", (W, im.height + 44), BG)
        ImageDraw.Draw(t).text((14, 8), cap, fill=INK, font=font(24, True))
        t.paste(im, (0, 44))
        tiles.append(t)
    rows = [max(tiles[0].height, tiles[1].height), max(tiles[2].height, tiles[3].height)]
    out = Image.new("RGB", (W * 2 + 20, rows[0] + rows[1] + 20), BG)
    out.paste(tiles[0], (0, 0))
    out.paste(tiles[1], (W + 20, 0))
    out.paste(tiles[2], (0, rows[0] + 20))
    out.paste(tiles[3], (W + 20, rows[0] + 20))
    s = stats()
    header(out, "Dog bed (#411): review sheet",
           "%d triangles (backstop %d, #799), %.2f x %.2f x %.2f m, cushion top (rest point) at %.2f m"
           % (s["tris"], s["budget_tris"], *s["size_xyz_m"], s["rest_point_m"][2]), h=110).save(
        os.path.join(OUT, "sheet.png"))


if __name__ == "__main__":
    if want("gamecam"):
        gamecam()
    if want("closeup"):
        closeup(False)
    if want("closeup_dog"):
        closeup(True)
    if want("turnaround"):
        turnaround()
    if want("footprint"):
        footprint()
    if want("sheet"):
        sheet()
    print("renders ->", OUT)
