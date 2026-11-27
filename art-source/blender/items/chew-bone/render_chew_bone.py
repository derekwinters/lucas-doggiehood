"""Review renders for the chew bone (#923), EEVEE, headless. One shot per command:

  python3 art-source/blender/items/chew-bone/render_chew_bone.py <gamecam|closeup|turnaround> [outdir]

  gamecam     the game camera (orthographic, 45 deg pitch, yaw -40) on the approved lawn: the bone
              beside the approved adult Golden, with the approved base-1 house behind (wide), and
              the same camera zoomed in on the dog and the bone
  closeup     a perspective close-up of the bone on the lawn
  turnaround  side, end and top, orthographic, with a 10 cm scale bar

Writes <outdir>/chew-bone-<shot>.png (default: out/, git-ignored; the approved renders are copied
into review/). Run export_chew_bone.py first (the bone's textures). Needs the committed Golden
export, lawn textures and base-1 house. Renders are not byte-reproducible.

Without a GPU, EEVEE runs on Mesa's lavapipe (Vulkan on the CPU); this script points the Vulkan
loader at it when VK_ICD_FILENAMES is unset and the file exists.
"""
import math
import os
import sys

if "VK_ICD_FILENAMES" not in os.environ and os.path.exists("/opt/lavapipe/lvp_icd.json"):
    os.environ["VK_ICD_FILENAMES"] = "/opt/lavapipe/lvp_icd.json"

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import dh_chew_bone as CB  # noqa: E402  (puts the house kit and the dog kit on the path)
sys.path.insert(0, os.path.join(CB.ART, "environment", "ground"))
import bpy  # noqa: E402
from mathutils import Vector  # noqa: E402
from PIL import Image, ImageDraw, ImageFont  # noqa: E402
import dh_dog_render as R  # noqa: E402
import dh_ground as G  # noqa: E402

SHOT = sys.argv[1] if len(sys.argv) > 1 else "gamecam"
OUT = os.path.abspath(sys.argv[2]) if len(sys.argv) > 2 else os.path.join(HERE, "out")
TMP = os.path.join(OUT, "_parts")
DOG = os.path.join(CB.ART, "dogs", "golden-retriever", "exports", "dog_golden_default.gltf")
HOUSE = os.path.join(CB.ART, "exports", "base-01-level1.glb")
GAME_YAW, GAME_PITCH = -40.0, 45.0
LAWN = ("c", "rec")            # Derek's approved lawn (environment/ground)
LAWN_SPAN = 90.0               # m, square lawn under every shot
DOG_AT, DOG_YAW = (0.0, 0.0, 0.0), -25.0
BONE_AT, BONE_YAW = (0.85, -0.75, 0.0), 35.0    # beside the dog's front paws
HOUSE_AT = (0.0, 9.0, 0.0)
CLOSE_YAW, CLOSE_PITCH, CLOSE_LENS = -30.0, 28.0, 70.0
SCALE_BAR = 0.10               # m, on the turnaround views
BG = (241, 236, 227)
INK = (40, 36, 32)
SUB = (105, 98, 90)
F1 = "/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf"
F2 = "/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf"
os.makedirs(TMP, exist_ok=True)


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


def scene(path, res, samples=48, lawn=True):
    R.reset()
    R.setup(path, res=res, samples=samples)
    if lawn:
        half = LAWN_SPAN / 2
        G.lawn_tile(*LAWN, -half, -half, size=LAWN_SPAN)
    else:
        R.floor(size=4)


def import_gltf(path, at, yaw):
    before = set(bpy.data.objects)
    bpy.ops.import_scene.gltf(filepath=path)
    new = [o for o in bpy.data.objects if o not in before]
    for r in (o for o in new if o.parent is None):
        r.location = Vector(r.location) + Vector(at)
        r.rotation_euler.z += math.radians(yaw)
    bpy.context.view_layer.update()
    return new


def bone(at=BONE_AT, yaw=BONE_YAW):
    ob = CB.build()
    ob.location = at
    ob.rotation_euler = (0, 0, math.radians(yaw))
    bpy.context.view_layer.update()
    return ob


def gamecam():
    shots = []
    for name, res, close in (("wide", (1500, 1000), False), ("zoom", (1500, 1000), True)):
        path = os.path.join(TMP, "game_%s.png" % name)
        scene(path, res)
        dog = import_gltf(DOG, DOG_AT, DOG_YAW)
        b = bone()
        objs = [o for o in dog if o.type == "MESH"] + [b]
        if not close:
            objs += [o for o in import_gltf(HOUSE, HOUSE_AT, 0.0) if o.type == "MESH"]
        R.camera(objs, GAME_YAW, GAME_PITCH, res, margin=1.25 if close else 1.05)
        R.render()
        shots.append(Image.open(path).convert("RGB"))
    wide, zoom = shots
    sheet = Image.new("RGB", (wide.width, wide.height + zoom.height + 60), BG)
    sheet.paste(wide, (0, 0))
    d = ImageDraw.Draw(sheet)
    d.text((20, wide.height + 14), "Same camera, zoomed in: the chew bone beside the adult Golden's front paws",
           fill=INK, font=font(24, True))
    sheet.paste(zoom, (0, wide.height + 60))
    return header(sheet, "Chew bone - game camera",
                  "Ortho, 45 deg pitch, yaw -40. Approved lawn, adult Golden, base 1. "
                  + summary())


def closeup():
    res = (1600, 1000)
    path = os.path.join(TMP, "closeup.png")
    scene(path, res, samples=96)
    b = bone(at=(0, 0, 0), yaw=0.0)
    R.camera([b], CLOSE_YAW, CLOSE_PITCH, res, ortho=False, lens=CLOSE_LENS, margin=1.25)
    R.render()
    return header(Image.open(path).convert("RGB"), "Chew bone - close-up", summary())


def turnaround():
    views = [("Side", 0, 0), ("End", -90, 0), ("Top", 0, 89.9)]
    res = (800, 520)
    sheet = Image.new("RGB", (res[0] * len(views), res[1]), BG)
    d = ImageDraw.Draw(sheet)
    for i, (name, yaw, pitch) in enumerate(views):
        path = os.path.join(TMP, "turn_%s.png" % name.lower())
        scene(path, res, lawn=False)
        b = bone(at=(0, 0, 0), yaw=0.0)
        cam = R.camera([b], yaw, pitch, res, margin=1.25)
        R.render()
        im = Image.open(path).convert("RGB")
        di = ImageDraw.Draw(im)
        ppm = res[0] / cam.data.ortho_scale
        x0, y0 = res[0] - 30 - ppm * SCALE_BAR, res[1] - 30
        di.rectangle((x0, y0, x0 + ppm * SCALE_BAR, y0 + 8), fill=INK)
        di.text((x0, y0 - 28), "10 cm", fill=INK, font=font(20, True))
        sheet.paste(im, (i * res[0], 0))
        d.text((i * res[0] + 16, 10), name, fill=INK, font=font(26, True))
    return header(sheet, "Chew bone - side, end, top", "Orthographic. " + summary())


def summary():
    ob = bpy.data.objects.get(CB.NAME)
    size = ob.dimensions if ob else (0, 0, 0)
    return "%d triangles, %.3f x %.3f x %.3f m." % (CB.tri_count(ob) if ob else 0, *size)


SHOTS = {"gamecam": gamecam, "closeup": closeup, "turnaround": turnaround}

if __name__ == "__main__":
    if SHOT not in SHOTS:
        sys.exit(__doc__)
    img = SHOTS[SHOT]()
    out = os.path.join(OUT, "chew-bone-%s.png" % SHOT)
    img.save(out)
    print("wrote", out)
