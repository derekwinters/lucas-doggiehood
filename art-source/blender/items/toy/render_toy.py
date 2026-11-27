"""Review renders and the web-viewer GLB for the dog toy (#332).

  toy-grass.png      the toy on the approved lawn beside the approved adult
                     Labrador, at the game camera (ortho, pitch 45, yaw -40)
  toy-sidewalk.png   the same on the approved sidewalk
  toy-closeup.png    the toy from four sides, beside today's 0.6 m graybox
                     lost-item sphere, with a 10 cm scale bar
  glb/toy_duck.glb   self-contained (textures embedded) for a three.js viewer

Each game-camera sheet has three panels: the whole frame at the closest zoom
(CameraController.MinZoom, 6 m orthographic half-height) and a crop at the
default zoom (DefaultZoom, 18 m), both at 1:1 pixels on a 1080-pixel-tall
landscape screen, plus a closer view of the dog and the toy.

Run export_toy.py first (the renders read its textures). Renders are not
byte-reproducible. Without a GPU, EEVEE runs on Mesa's lavapipe.

Usage: python3 render_toy.py [OUT_DIR] [--views grass,sidewalk,closeup,glb] [--samples 48]
OUT_DIR defaults to this folder's out/ (git-ignored).
"""
import math
import os
import sys

if "VK_ICD_FILENAMES" not in os.environ and os.path.exists("/opt/lavapipe/lvp_icd.json"):
    os.environ["VK_ICD_FILENAMES"] = "/opt/lavapipe/lvp_icd.json"

HERE = os.path.dirname(os.path.abspath(__file__))
ART = os.path.normpath(os.path.join(HERE, "..", ".."))          # art-source/blender
sys.path.insert(0, HERE)
sys.path.insert(0, ART)
import bpy  # noqa: E402
from mathutils import Vector  # noqa: E402
from PIL import Image, ImageDraw, ImageFont  # noqa: E402
import dh_house_kit as K  # noqa: E402
import dh_toy as T  # noqa: E402

FLAGS = {"--views", "--samples"}      # each takes one value
args = [a for i, a in enumerate(sys.argv[1:], 1) if a not in FLAGS and sys.argv[i - 1] not in FLAGS]
OUT = os.path.abspath(args[0]) if args else os.path.join(HERE, "out")
VIEWS = {"grass", "sidewalk", "closeup", "glb"}
SAMPLES = 48
for i, a in enumerate(sys.argv):
    if a == "--views":
        VIEWS = set(sys.argv[i + 1].split(","))
    if a == "--samples":
        SAMPLES = int(sys.argv[i + 1])
TMP = os.path.join(OUT, "_parts")
GLB = os.path.join(OUT, "glb")
os.makedirs(TMP, exist_ok=True)
os.makedirs(GLB, exist_ok=True)

TEX = {"color": os.path.join(HERE, "textures", "toy_fleece_color.png"),
       "normal": os.path.join(HERE, "textures", "toy_fleece_normal.png")}
ENV = os.path.join(ART, "environment")
GROUND = os.path.join(ENV, "ground", "exports", "ground_c_tile_rec.gltf")
GROUND_AT = (-30.0, -30.0, 0.0)      # the lawn tile spans 0..60 m; centre it on the road tile
ROAD = os.path.join(ENV, "roads", "exports", "road_a_StraightEW.gltf")
PAVING = os.path.join(ENV, "paving", "exports", "paving_p1_StraightEW.gltf")
HOUSE = os.path.join(ART, "exports", "base-01-level1.glb")
HOUSE_AT = (-7.0, 13.0, 0.0)         # base 1's facade on the 9.5 m front line (it is 7 m deep)
DOG = os.path.join(ART, "dogs", "labrador", "exports", "dog_labrador_yellow.gltf")

# Where the toy and the dog stand (world metres; the road runs along X at y = 0,
# the sidewalk is y 3.75..5.75, the front lawn is beyond it)
SPOTS = {"grass": {"toy": (0.9, 8.0), "toy_yaw": -30.0, "dog": (-0.5, 7.9), "dog_yaw": -60.0},
         "sidewalk": {"toy": (0.9, 4.75), "toy_yaw": 25.0, "dog": (-0.5, 4.9), "dog_yaw": -95.0}}

# The game camera (Assets/Scripts/Core/Cameras): ortho, pitch 45, zoom = ortho half-height
SCREEN = (1920, 1080)                # a 1080-pixel-tall landscape phone screen
MIN_ZOOM, DEFAULT_ZOOM = 6.0, 18.0   # CameraController.MinZoom / DefaultZoom
CROP = (960, 540)                    # the default-zoom crop, at 1:1 pixels
CLOSE = (960, 540)                   # the closer dog-and-toy view
CLOSE_ZOOM = 1.3                     # ortho half-height of the closer view (closer than the game allows)
CLOSE_AIM_Z = 0.5                    # aim a little above the ground, at the dog's middle
SPHERE_D = 0.6                       # LostItemView.SphereScale: today's graybox lost item
SPHERE_HEX = "#FFFFFF"
SPHERE_AT = (0.0, 0.75, SPHERE_D / 2)   # behind the toy, so every close-up view shows both
STUDIO_BG = "#EEF1EC"
STUDIO_FLOOR = "#D9D6CE"
CLOSEUP_TILE = (800, 600)
CLOSEUP_VIEWS = [("Game angle (yaw -40, pitch 45)", -40, 45), ("Front 3/4, low", -30, 12),
                 ("Side", -90, 0), ("Top", 0, 89.9)]
SCALE_BAR_M = 0.10
HEADER_H = 90
CAPTION_H = 44
F1 = "/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf"
F2 = "/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf"
INK = (40, 36, 32)
SUB = (105, 98, 90)
PAPER = (241, 236, 227)
TITLE_PT, SUB_PT, CAP_PT = 34, 21, 22


def font(sz, bold=False):
    return ImageFont.truetype(F1 if bold else F2, sz)


def import_gltf(path, at=(0, 0, 0), yaw=0.0):
    before = set(bpy.data.objects)
    bpy.ops.import_scene.gltf(filepath=path)
    new = [o for o in bpy.data.objects if o not in before]
    for r in (o for o in new if o.parent is None):
        r.location = Vector(r.location) + Vector(at)
        r.rotation_euler.z += math.radians(yaw)
    bpy.context.view_layer.update()
    return new


def toy(at=(0, 0, 0), yaw=0.0):
    ob = T.build(TEX)
    ob.location = at
    ob.rotation_euler.z = math.radians(yaw)
    bpy.context.view_layer.update()
    return ob


def game_camera(target, zoom, res):
    """The game camera centred on a ground point; zoom = orthographic half-height (m)."""
    cam_data = bpy.data.cameras.new("Cam")
    cam_data.type = "ORTHO"
    cam_data.ortho_scale = 2 * zoom * max(1.0, res[0] / res[1])
    cam_data.clip_end = 500
    cam = bpy.data.objects.new("Cam", cam_data)
    bpy.context.scene.collection.objects.link(cam)
    cam.rotation_euler = (math.radians(90 - K.GAME_PITCH), 0, math.radians(K.GAME_YAW))
    back = cam.rotation_euler.to_matrix() @ Vector((0, 0, 1))
    cam.location = Vector(target) + back * 80
    bpy.context.scene.camera = cam
    return cam


def street_scene(path, res, surface):
    K.reset_scene()
    K.setup_render(path, res=res, samples=SAMPLES)
    import_gltf(GROUND, at=GROUND_AT)
    import_gltf(ROAD)
    import_gltf(PAVING)
    import_gltf(HOUSE, at=HOUSE_AT)
    sp = SPOTS[surface]
    import_gltf(DOG, at=sp["dog"] + (0.0,), yaw=sp["dog_yaw"])
    toy(sp["toy"] + (0.0,), sp["toy_yaw"])


def header(img, title, sub):
    out = Image.new("RGB", (img.width, img.height + HEADER_H), PAPER)
    out.paste(img, (0, HEADER_H))
    d = ImageDraw.Draw(out)
    d.text((24, 14), title, fill=INK, font=font(TITLE_PT, True))
    d.text((26, 56), sub, fill=SUB, font=font(SUB_PT))
    return out


def caption(img, text):
    out = Image.new("RGB", (img.width, img.height + CAPTION_H), PAPER)
    out.paste(img, (0, CAPTION_H))
    ImageDraw.Draw(out).text((14, 10), text, fill=INK, font=font(CAP_PT, True))
    return out


def street_sheet(surface):
    sp = SPOTS[surface]
    target = sp["toy"] + (0.0,)
    parts = {}
    for key, zoom in (("min", MIN_ZOOM), ("default", DEFAULT_ZOOM)):
        p = os.path.join(TMP, "%s_%s.png" % (surface, key))
        street_scene(p, SCREEN, surface)
        game_camera(target, zoom, SCREEN)
        K.render()
        parts[key] = Image.open(p).convert("RGB")
    p = os.path.join(TMP, "%s_close.png" % surface)
    street_scene(p, CLOSE, surface)
    mid = tuple((a + b) / 2 for a, b in zip(sp["toy"], sp["dog"])) + (CLOSE_AIM_Z,)
    game_camera(mid, CLOSE_ZOOM, CLOSE)
    K.render()
    close = Image.open(p).convert("RGB")
    cx, cy = SCREEN[0] // 2, SCREEN[1] // 2
    crop = parts["default"].crop((cx - CROP[0] // 2, cy - CROP[1] // 2, cx + CROP[0] // 2, cy + CROP[1] // 2))
    top = caption(parts["min"], "Closest game zoom (MinZoom 6 m), whole 1920 x 1080 screen at 1:1")
    left = caption(crop, "Default zoom (18 m), centre crop at 1:1")
    right = caption(close, "Closer (zoom 1.3 m, past the game's limit): beside the adult Labrador")
    sheet = Image.new("RGB", (SCREEN[0], top.height + left.height), PAPER)
    sheet.paste(top, (0, 0))
    sheet.paste(left, (0, top.height))
    sheet.paste(right, (CROP[0], top.height))
    size = "%.2f x %.2f x %.2f m" % tuple(stats_size())
    sheet = header(sheet, "Dog toy (#332): squeaky plush duck on the %s" % ("lawn" if surface == "grass" else surface),
                   "Game camera (ortho, pitch 45, yaw -40). Toy %s, %d triangles. Dog: approved adult Labrador, "
                   "yellow coat." % (size, stats_tris()))
    sheet.save(os.path.join(OUT, "toy-%s.png" % surface))


def _stats():
    import json
    return json.load(open(os.path.join(HERE, "exports", "toy_stats.json")))[T.NAME]


def stats_size():
    return _stats()["size_xyz_m"]


def stats_tris():
    return _stats()["tris"]


def closeup():
    tiles = []
    for i, (name, yaw, pitch) in enumerate(CLOSEUP_VIEWS):
        p = os.path.join(TMP, "close_%d.png" % i)
        K.reset_scene()
        K.setup_render(p, res=CLOSEUP_TILE, bg=STUDIO_BG, samples=SAMPLES)
        K.ground_plane(20, STUDIO_FLOOR)
        ob = toy()
        bpy.ops.mesh.primitive_uv_sphere_add(radius=SPHERE_D / 2, location=SPHERE_AT)
        ball = bpy.context.active_object
        ball.data.materials.append(K.material("graybox", SPHERE_HEX))
        bpy.ops.object.shade_smooth()
        cam = K.frame_camera([ob, ball], margin=1.15, yaw=yaw, pitch=pitch, res=CLOSEUP_TILE)
        K.render()
        im = Image.open(p).convert("RGB")
        d = ImageDraw.Draw(im)
        ppm = CLOSEUP_TILE[0] / cam.data.ortho_scale
        x0, y0 = CLOSEUP_TILE[0] - 40 - ppm * SCALE_BAR_M, CLOSEUP_TILE[1] - 34
        d.rectangle((x0, y0, x0 + ppm * SCALE_BAR_M, y0 + 7), fill=INK)
        d.text((x0, y0 - 26), "10 cm", fill=INK, font=font(19, True))
        tiles.append(caption(im, name))
    w, h = tiles[0].size
    sheet = Image.new("RGB", (w * 2, h * 2), PAPER)
    for i, t in enumerate(tiles):
        sheet.paste(t, ((i % 2) * w, (i // 2) * h))
    size = "%.2f wide x %.2f long x %.2f tall m" % tuple(stats_size())
    sheet = header(sheet, "Dog toy (#332): squeaky plush duck, close-up",
                   "%s, %d triangles. White: today's 0.6 m graybox lost-item sphere, for size." % (size, stats_tris()))
    sheet.save(os.path.join(OUT, "toy-closeup.png"))


def glb():
    K.reset_scene()
    ob = toy()
    bpy.ops.object.select_all(action="DESELECT")
    ob.select_set(True)
    bpy.ops.export_scene.gltf(filepath=os.path.join(GLB, T.NAME + ".glb"), export_format="GLB",
                              use_selection=True, export_apply=True, export_yup=True)


if __name__ == "__main__":
    if "glb" in VIEWS:
        glb()
    if "closeup" in VIEWS:
        closeup()
    for s in ("grass", "sidewalk"):
        if s in VIEWS:
            street_sheet(s)
