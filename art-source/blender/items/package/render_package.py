"""Review renders of the delivery package (#410), drawn from the committed export.

  python3 art-source/blender/items/package/render_package.py <door|closeup|turnaround|all> [out_dir]

  door        game camera (orthographic, pitch 45, yaw -40) at base 1's front door: the package on
              the front walk where the truck drops it, beside the approved adult Golden Retriever
  closeup     the package close up from the game camera's direction, beside a ghost of today's
              0.6 m graybox cube
  turnaround  front, right, back, left and top, stitched into one sheet with the size and triangles

Writes <out_dir>/package-<shot>.png (default: out/ next to this file, git-ignored; approved renders
are copied into review/). Run export_package.py first; door also needs the approved base-1 house
(art-source/blender/exports/base-01-level1.glb) and the Golden's default export. Without a GPU,
EEVEE runs on Mesa's lavapipe; this script points the Vulkan loader at it when VK_ICD_FILENAMES is
unset and the file exists. Renders are not byte-reproducible.
"""
import json
import math
import os
import sys

if "VK_ICD_FILENAMES" not in os.environ and os.path.exists("/opt/lavapipe/lvp_icd.json"):
    os.environ["VK_ICD_FILENAMES"] = "/opt/lavapipe/lvp_icd.json"

HERE = os.path.dirname(os.path.abspath(__file__))
ART = os.path.normpath(os.path.join(HERE, "..", ".."))
sys.path.insert(0, HERE)
sys.path.insert(0, ART)
import bpy  # noqa: E402
from mathutils import Vector  # noqa: E402
from PIL import Image, ImageDraw, ImageFont  # noqa: E402
import dh_house_kit as K  # noqa: E402
import package_checks as C  # noqa: E402

PACKAGE_GLB = os.path.join(HERE, "exports", "package.glb")
STATS = os.path.join(HERE, "exports", "package_stats.json")
HOUSE_GLB = os.path.join(ART, "exports", "base-01-level1.glb")
DOG_GLTF = os.path.join(ART, "dogs", "golden-retriever", "exports", "dog_golden_default.gltf")
FONT = "/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf"
FONT_REG = "/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf"

LAWN = "#8E9E68"
WALK = "#BDB8AE"
BG = "#E8EEF2"
GHOST = "#B9BEC4"
INK = (34, 38, 44)
SUB = (96, 102, 110)

# the door scene, in base 1's own coordinates (pivot at its footprint centre, front at FRONT_Y)
DOOR_X = 0.0                 # base 1's front door is centred on its front wall
DOOR_HALF = 1.0              # the landing and steps lie within this of the door's centre line
WALK_HALF = 0.6              # front walk half width (dh_scene.street)
WALK_Z = 0.03
WALK_LEN = 6.0
PACKAGE_OUT = 0.55           # package centre, out from the foot of the steps
PACKAGE_DX = -0.15
PACKAGE_YAW = 8.0
DOG_OUT = 0.95               # dog origin, out from the foot of the steps
DOG_DX = 1.35
DOG_YAW = -35.0
DOOR_RES = (1600, 1200)
DOOR_VIEW = ((-2.8, -2.8, 0.0), (4.2, 2.4, 3.2))   # framing box (lo, hi), relative to the step foot

CLOSE_RES = (1400, 1050)
CLOSE_PITCH = 35.0
GHOST_AT = (0.95, 0.15, 0.0)

TURN_RES = (700, 620)
TURN_VIEWS = (("Front", 0.0, 12.0), ("Right", 90.0, 12.0), ("Back", 180.0, 12.0), ("Left", 270.0, 12.0),
              ("Top", 0.0, 89.0))
HEADER_H = 110
CAPTION_H = 56


def _import(path, at=(0, 0, 0), yaw=0.0):
    before = set(bpy.data.objects)
    bpy.ops.import_scene.gltf(filepath=path)
    new = [o for o in bpy.data.objects if o not in before]
    for r in (o for o in new if o.parent is None):
        r.location = Vector(r.location) + Vector(at)
        r.rotation_euler = (r.rotation_euler[0], r.rotation_euler[1], r.rotation_euler[2] + math.radians(yaw))
    bpy.context.view_layer.update()
    return new


def _plane(name, x0, y0, x1, y1, z, hex_):
    # each plane gets its own material key: the kit names materials DH_<key> and reuses them by name
    m = K.MeshBuilder(name, {name: hex_})
    m.box((x0, y0, z - 0.05), (x1, y1, z), name)
    return m.build()


def _hint_box(lo, hi):
    """An invisible box that frame_camera fits to (removed before the render)."""
    m = K.MeshBuilder("frame_hint", {"frame_hint": "#000000"})
    m.box(lo, hi, "frame_hint")
    return m.build()


def _frame(lo, hi, res, yaw=K.GAME_YAW, pitch=K.GAME_PITCH, margin=1.0):
    h = _hint_box(lo, hi)
    K.frame_camera([h], margin=margin, yaw=yaw, pitch=pitch, res=res)
    bpy.data.objects.remove(h)


def _ghost_cube(at):
    m = K.MeshBuilder("graybox_cube", {"graybox_cube": GHOST})
    s = C.GRAYBOX_SIZE
    m.box((at[0] - s / 2, at[1] - s / 2, 0), (at[0] + s / 2, at[1] + s / 2, s), "graybox_cube")
    return m.build()


def _step_foot(objs):
    """The front edge of the house's landing and steps in front of the door (lowest Y of its
    geometry within DOOR_HALF of the door's centre line)."""
    ys = []
    for o in objs:
        if o.type != "MESH":
            continue
        for v in o.data.vertices:
            w = o.matrix_world @ v.co
            if abs(w.x - DOOR_X) < DOOR_HALF:
                ys.append(w.y)
    return min(ys)


def door(out):
    K.reset_scene()
    K.setup_render(out, res=DOOR_RES, bg=BG)
    _plane("lawn", -12, -14, 12, 8, 0.0, LAWN)
    house = _import(HOUSE_GLB)
    foot = _step_foot(house)
    _plane("walk", DOOR_X - WALK_HALF, foot - WALK_LEN, DOOR_X + WALK_HALF, foot + 0.2, WALK_Z, WALK)
    _import(DOG_GLTF, at=(DOOR_X + DOG_DX, foot - DOG_OUT, WALK_Z), yaw=DOG_YAW)
    _import(PACKAGE_GLB, at=(DOOR_X + PACKAGE_DX, foot - PACKAGE_OUT, WALK_Z), yaw=PACKAGE_YAW)
    lo, hi = DOOR_VIEW
    _frame((DOOR_X + lo[0], foot + lo[1], lo[2]), (DOOR_X + hi[0], foot + hi[1], hi[2]), DOOR_RES)
    K.render()


def closeup(out):
    K.reset_scene()
    K.setup_render(out, res=CLOSE_RES, bg=BG)
    _plane("walk", -2, -2, 2, 2, 0.0, WALK)
    _import(PACKAGE_GLB)
    _ghost_cube(GHOST_AT)
    s = C.GRAYBOX_SIZE
    _frame((-0.45, -0.45, 0.0), (GHOST_AT[0] + s / 2 + 0.05, 0.6, s), CLOSE_RES, pitch=CLOSE_PITCH, margin=1.12)
    K.render()


def _font(size, bold=True):
    try:
        return ImageFont.truetype(FONT if bold else FONT_REG, size)
    except OSError:
        return ImageFont.load_default()


def turnaround(out):
    tmp = []
    for label, yaw, pitch in TURN_VIEWS:
        path = out + ".%s.png" % label.lower()
        K.reset_scene()
        K.setup_render(path, res=TURN_RES, bg=BG)
        _plane("walk", -2, -2, 2, 2, 0.0, WALK)
        _import(PACKAGE_GLB)
        _frame((-0.32, -0.27, 0.0), (0.32, 0.27, 0.46), TURN_RES, yaw=yaw, pitch=pitch, margin=1.35)
        K.render()
        tmp.append((label, path))
    with open(STATS) as fh:
        st = json.load(fh)
    w, h = TURN_RES
    cols = 3
    sheet = Image.new("RGB", (cols * w, HEADER_H + 2 * (h + CAPTION_H)), (255, 255, 255))
    d = ImageDraw.Draw(sheet)
    d.text((24, 18), "Delivery package (#410): turnaround", fill=INK, font=_font(40))
    x, y, z = st["size_xyz_m"]
    d.text((24, 66), "%.2f x %.2f x %.2f m (L x D x H), %d triangles, budget %d" % (x, y, z, st["tris"],
                                                                                    st["budget_tris"]),
           fill=SUB, font=_font(26, False))
    for i, (label, path) in enumerate(tmp):
        cx, cy = (i % cols) * w, HEADER_H + (i // cols) * (h + CAPTION_H)
        sheet.paste(Image.open(path).convert("RGB"), (cx, cy + CAPTION_H))
        d.text((cx + 16, cy + 12), label, fill=INK, font=_font(30))
        os.remove(path)
    sheet.save(out)


SHOTS = {"door": door, "closeup": closeup, "turnaround": turnaround}


def main():
    if len(sys.argv) < 2 or sys.argv[1] not in list(SHOTS) + ["all"]:
        sys.exit(__doc__)
    out_dir = sys.argv[2] if len(sys.argv) > 2 else os.path.join(HERE, "out")
    os.makedirs(out_dir, exist_ok=True)
    for shot in (SHOTS if sys.argv[1] == "all" else [sys.argv[1]]):
        path = os.path.join(out_dir, "package-%s.png" % shot)
        SHOTS[shot](path)
        print("WROTE", path)


if __name__ == "__main__":
    main()
