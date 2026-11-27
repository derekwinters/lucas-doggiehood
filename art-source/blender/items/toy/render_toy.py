"""Review renders and the web-viewer GLBs for the five dog toys (#332).

  toy-lineup-grass.png      all five in a row beside the approved adult Labrador
  toy-lineup-sidewalk.png   on the lawn, and on the sidewalk, at the game camera
                            (ortho, pitch 45, yaw -40), numbered and named
  toy-<design>-closeup.png  each toy from four sides beside today's 0.6 m graybox
                            lost-item sphere, with a 10 cm scale bar
  glb/toy_<design>.glb      one self-contained GLB per design (textures embedded),
                            for a three.js viewer

Each line-up sheet has three panels: a closer view with name tags, a crop at the
closest game zoom (CameraController.MinZoom, 6 m orthographic half-height) and
one at the default zoom (DefaultZoom, 18 m), both at 1:1 pixels on a
1080-pixel-tall landscape screen.

Run export_toy.py first (the renders read its textures). Renders are not
byte-reproducible. Without a GPU, EEVEE runs on Mesa's lavapipe, which can
crash with no message after several renders in one process: render a few
views per command (for example --views lineup, then --views closeup with two
or three --designs at a time).

Usage: python3 render_toy.py [OUT_DIR] [--views lineup,closeup,glb] [--designs disc,...] [--samples 48]
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

FLAGS = {"--views", "--samples", "--designs"}      # each takes one value
args = [a for i, a in enumerate(sys.argv[1:], 1) if a not in FLAGS and sys.argv[i - 1] not in FLAGS]
OUT = os.path.abspath(args[0]) if args else os.path.join(HERE, "out")
VIEWS = {"lineup", "closeup", "glb"}
SAMPLES = 48
DESIGNS = list(T.DESIGNS)
for i, a in enumerate(sys.argv):
    if a == "--designs":
        DESIGNS = sys.argv[i + 1].split(",")
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

# The game camera (Assets/Scripts/Core/Cameras): ortho, pitch 45, zoom = ortho half-height
SCREEN = (1920, 1080)                # a 1080-pixel-tall landscape phone screen
MIN_ZOOM, DEFAULT_ZOOM = 6.0, 18.0   # CameraController.MinZoom / DefaultZoom
CROP = (960, 540)                    # the game-zoom crops, at 1:1 pixels
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


def toy(design, at=(0, 0, 0), yaw=0.0):
    ob = T.build(TEX, design)
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


def _stats(design):
    import json
    return json.load(open(os.path.join(HERE, "exports", "toy_stats.json")))[T.object_name(design)]


def stats_size(design):
    return _stats(design)["size_xyz_m"]


def stats_tris(design):
    return _stats(design)["tris"]


def number_of(design):
    return list(T.DESIGNS).index(design) + 1


def label_of(design):
    return "%d. %s" % (number_of(design), T.DESIGNS[design]["label"])


def closeup(design):
    tiles = []
    for i, (name, yaw, pitch) in enumerate(CLOSEUP_VIEWS):
        p = os.path.join(TMP, "close_%s_%d.png" % (design, i))
        K.reset_scene()
        K.setup_render(p, res=CLOSEUP_TILE, bg=STUDIO_BG, samples=SAMPLES)
        K.ground_plane(20, STUDIO_FLOOR)
        ob = toy(design)
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
    size = "%.2f wide x %.2f long x %.2f tall m" % tuple(stats_size(design))
    sheet = header(sheet, "Dog toy (#332): %s, close-up" % label_of(design),
                   "%s, %d triangles. White: today's 0.6 m graybox lost-item sphere, for size."
                   % (size, stats_tris(design)))
    sheet.save(os.path.join(OUT, "toy-%s-closeup.png" % design))


def glb(design):
    K.reset_scene()
    ob = toy(design)
    bpy.ops.object.select_all(action="DESELECT")
    ob.select_set(True)
    bpy.ops.export_scene.gltf(filepath=os.path.join(GLB, T.object_name(design) + ".glb"), export_format="GLB",
                              use_selection=True, export_apply=True, export_yup=True)


# ---------------------------------------------------------------------------
# Line-up: every toy in a row beside the dog, at the game camera
# ---------------------------------------------------------------------------
LINEUP_ROW_Y = {"grass": 8.0, "sidewalk": 4.75}   # lawn beyond the sidewalk / the sidewalk's centre line
LINEUP_DOG_X = -1.3
LINEUP_DOG_YAW = -80.0
LINEUP_X0 = 0.0              # first toy
LINEUP_STEP = 0.95           # metres between toys along the street
LINEUP_TOY_YAW = -20.0
LINEUP_CLOSE = (1920, 820)
LINEUP_CLOSE_ZOOM = 1.75     # ortho half-height of the closer view (closer than the game allows)
LINEUP_AIM_Z = 0.3
TAG_UP_PX = {"close": 70, "game": 26}
TAG_PT = {"close": 22, "game": 15}


def _tag(d, xy, text, pt, up):
    x, y = xy
    f = font(pt, True)
    w = d.textlength(text, font=f)
    box = (x - w / 2 - 7, y - up - pt - 6, x + w / 2 + 7, y - up + 4)
    d.rounded_rectangle(box, 6, fill=(255, 255, 255), outline=(120, 112, 100))
    d.text((box[0] + 7, box[1] + 3), text, fill=INK, font=f)


def _project(cam, res, p):
    from bpy_extras.object_utils import world_to_camera_view
    q = world_to_camera_view(bpy.context.scene, cam, Vector(p))
    return q.x * res[0], (1 - q.y) * res[1]


def lineup_scene(path, res, surface, designs):
    K.reset_scene()
    K.setup_render(path, res=res, samples=SAMPLES)
    import_gltf(GROUND, at=GROUND_AT)
    import_gltf(ROAD)
    import_gltf(PAVING)
    import_gltf(HOUSE, at=HOUSE_AT)
    y = LINEUP_ROW_Y[surface]
    import_gltf(DOG, at=(LINEUP_DOG_X, y, 0.0), yaw=LINEUP_DOG_YAW)
    spots = []
    for i, d in enumerate(designs):
        at = (LINEUP_X0 + i * LINEUP_STEP, y, 0.0)
        toy(d, at, LINEUP_TOY_YAW)
        spots.append((d, at))
    return spots


def lineup_sheet(surface, designs):
    y = LINEUP_ROW_Y[surface]
    mid = ((LINEUP_DOG_X + LINEUP_X0 + (len(designs) - 1) * LINEUP_STEP) / 2, y, LINEUP_AIM_Z)
    panels = {}
    for key, zoom, res in (("close", LINEUP_CLOSE_ZOOM, LINEUP_CLOSE), ("min", MIN_ZOOM, SCREEN),
                           ("default", DEFAULT_ZOOM, SCREEN)):
        p = os.path.join(TMP, "lineup_%s_%s.png" % (surface, key))
        spots = lineup_scene(p, res, surface, designs)
        cam = game_camera(mid, zoom, res)
        K.render()
        im = Image.open(p).convert("RGB")
        pts = [(d, _project(cam, res, at)) for d, at in spots]
        if key != "close":
            cx, cy = res[0] // 2, res[1] // 2
            box = (cx - CROP[0] // 2, cy - CROP[1] // 2, cx + CROP[0] // 2, cy + CROP[1] // 2)
            im = im.crop(box)
            pts = [(d, (x - box[0], y_ - box[1])) for d, (x, y_) in pts]
        dr = ImageDraw.Draw(im)
        kind = "close" if key == "close" else "game"
        for d, xy in pts:
            text = label_of(d) if key == "close" else str(number_of(d))
            _tag(dr, xy, text, TAG_PT[kind], TAG_UP_PX[kind])
        panels[key] = im
    top = caption(panels["close"], "Closer (zoom %.2f m, past the game's limit), beside the adult Labrador"
                  % LINEUP_CLOSE_ZOOM)
    left = caption(panels["min"], "Closest game zoom (MinZoom 6 m), centre crop at 1:1")
    right = caption(panels["default"], "Default zoom (18 m), centre crop at 1:1")
    sheet = Image.new("RGB", (LINEUP_CLOSE[0], top.height + left.height), PAPER)
    sheet.paste(top, (0, 0))
    sheet.paste(left, (0, top.height))
    sheet.paste(right, (CROP[0], top.height))
    facts = "   ".join("%d. %d tris, %.2f x %.2f x %.2f m" % ((number_of(d), stats_tris(d))
                                                            + tuple(stats_size(d))) for d in designs)
    sheet = header(sheet, "Dog toys (#332) on the %s, game camera (ortho, pitch 45, yaw -40)"
                   % ("lawn" if surface == "grass" else "sidewalk"), facts)
    sheet.save(os.path.join(OUT, "toy-lineup-%s.png" % surface))


if __name__ == "__main__":
    if "glb" in VIEWS:
        for d in DESIGNS:
            glb(d)
    if "closeup" in VIEWS:
        for d in DESIGNS:
            closeup(d)
    if "lineup" in VIEWS:
        for s in ("grass", "sidewalk"):
            lineup_sheet(s, DESIGNS)
