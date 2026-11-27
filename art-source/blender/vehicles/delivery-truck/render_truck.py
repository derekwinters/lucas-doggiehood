"""Review renders for the delivery truck (#924).

  python3 art-source/blender/vehicles/delivery-truck/render_truck.py [OUT_DIR] [--views a,b] [--samples 32]

OUT_DIR defaults to this folder's out/ (git-ignored). Views:

  gamecam     Derek's three review colours (truck_dims.REVIEW_COLORS: white, dark brown,
              dark blue), door closed and open, at the game camera (orthographic, pitch 45, yaw -40) on a straight road tile
              (approved road kit look A on the approved lawn), the truck in its lane
              (RoadLane.Offset = RoadWidth / 4 right of the centreline) beside the
              approved adult Golden Retriever on the sidewalk:
              gamecam-<colour>-closed.png, gamecam-<colour>-open.png, and gamecam-sheet.png (all six)
  sides       lettering-sides.png: both long sides of each review colour, square on
  colors      the seven Palette.CarColorHex colours, one game-camera tile each: colors.png
  closeup     closeup-front.png (3/4 front) and closeup-rear-open.png (3/4 rear, door open),
              in dark brown
  motion      motion-sheet.png: the door swinging 0 -> open, and a wheel turning
              (lug nuts show the spin), four frames each

The truck is built by dh_truck (the same build the export uses); the dog is the
committed Golden export. Renders are not byte-reproducible.
"""
import math
import os
import sys

if "VK_ICD_FILENAMES" not in os.environ and os.path.exists("/opt/lavapipe/lvp_icd.json"):
    os.environ["VK_ICD_FILENAMES"] = "/opt/lavapipe/lvp_icd.json"

HERE = os.path.dirname(os.path.abspath(__file__))
ART = os.path.normpath(os.path.join(HERE, "..", ".."))
sys.path.insert(0, HERE)
sys.path.insert(0, os.path.join(ART, "environment", "roads"))
import dh_roads as RD  # noqa: E402  (puts the lawn, props, trees and the kit on the path)
import bpy  # noqa: E402
from mathutils import Vector  # noqa: E402
from PIL import Image, ImageDraw, ImageFont  # noqa: E402
import dh_house_kit as K  # noqa: E402
import dh_truck as T  # noqa: E402
import truck_dims as D  # noqa: E402

args = [a for a in sys.argv[1:]]
OUT = args[0] if args and not args[0].startswith("--") else os.path.join(HERE, "out")
VIEWS = {"gamecam", "sides", "colors", "closeup", "motion"}
SAMPLES = 32
for i, a in enumerate(args):
    if a == "--views":
        VIEWS = set(args[i + 1].split(","))
    if a == "--samples":
        SAMPLES = int(args[i + 1])
os.makedirs(OUT, exist_ok=True)
TMP = os.path.join(OUT, "_frames")
os.makedirs(TMP, exist_ok=True)

DOG_GLTF = os.path.join(ART, "dogs", "golden-retriever", "exports", "dog_golden_default.gltf")
FONT = "/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf"
ROAD_TILE = "StraightNS"                # road along Y; the truck drives toward -Y
LANE_X = -D.core_road_width() / 4       # right-hand lane when heading -Y (right is -X)
SIDEWALK_X = -4.75                      # kerb-side sidewalk centreline (RoadWidth/2 + verge + SidewalkWidth/2)
DOG_AT = (SIDEWALK_X, 2.2, 0.0)         # on the sidewalk, level with the rear door
DOG_YAW = 90.0                          # the Golden faces -Y; this turns it toward the road (+X)
GAMECAM_RES = (1200, 900)
GAMECAM_FRAME = ((-7.5, -4.5), (4.0, 4.5))      # x, y area the game camera frames
CLOSE_RES = (1200, 900)
COLOR_TILE_RES = (500, 420)
COLORS_HALF = 3.4                       # each colour tile frames +-this around the truck
WHEEL_HALF = 0.75                       # wheel frames: +-this around the axle
FRAME_RES = (600, 450)
DOOR_FRAMES = (0.0, -30.0, -60.0, D.DOOR_OPEN_DEG)
WHEEL_FRAMES = (0.0, 15.0, 30.0, 45.0)    # under 72: five lugs repeat every 72 deg
STUDIO = K.COMMON["ground_studio"]
LABEL_H = 54
WHITE, RED, DARK_BROWN = D.CAR_COLORS[0], D.CAR_COLORS[4], D.DARK_BROWN
SIDE_RES = (900, 500)
SIDE_LEFT_YAW = 90.0                    # camera on +X looking at the truck's left side
SIDE_RIGHT_YAW = -90.0
SIDE_PITCH = 8.0


def _font(size):
    try:
        return ImageFont.truetype(FONT, size)
    except OSError:
        return ImageFont.load_default()


def _hint(lo, hi):
    """An invisible box that only steers the camera framing."""
    h = K.MeshBuilder("frame_hint", {"g": "#000000"})
    h.box(lo, hi, "g")
    ob = h.build()
    ob.hide_render = True
    return ob


def _import_dog(at, yaw):
    before = set(bpy.data.objects)
    bpy.ops.import_scene.gltf(filepath=DOG_GLTF)
    new = [o for o in bpy.data.objects if o not in before]
    for r in (o for o in new if o.parent is None):
        r.location = Vector(r.location) + Vector(at)
        r.rotation_euler = (r.rotation_euler[0], r.rotation_euler[1], r.rotation_euler[2] + math.radians(yaw))
    bpy.context.view_layer.update()
    return [o for o in new if o.type == "MESH"]


def _truck(at=(0.0, 0.0, 0.0), color=WHITE[1], door=0.0, wheel=0.0):
    root = T.build()
    root.location = at
    T.tint(color)
    T.set_pose(root, door_deg=door, wheel_deg=wheel)
    return root


def _all(root):
    return [root] + list(root.children)


def _clear_view():
    for o in list(bpy.data.objects):
        if o.type in ("CAMERA", "LIGHT"):
            bpy.data.objects.remove(o)


def _render(path, res, frame_objs, yaw=K.GAME_YAW, pitch=K.GAME_PITCH, margin=1.05, ortho=True,
            lens=50.0, bg=STUDIO):
    _clear_view()
    K.setup_render(path, res=res, bg=bg, samples=SAMPLES)
    cam = K.frame_camera(frame_objs, margin=margin, yaw=yaw, pitch=pitch, res=res)
    if not ortho:
        # perspective close-up: same direction, pulled back to fit
        cam.data.type = "PERSP"
        cam.data.lens = lens
        pts = [o.matrix_world @ Vector(c) for o in frame_objs if o.type == "MESH" for c in o.bound_box]
        centre = sum(pts, Vector()) / len(pts)
        radius = max((p - centre).length for p in pts)
        fov = 2 * math.atan(18.0 / lens)   # 36 mm sensor width
        dist = radius * margin / math.sin(fov / 2 * min(1.0, res[1] / res[0]))
        fwd = cam.rotation_euler.to_matrix() @ Vector((0, 0, -1))
        cam.location = centre - fwd * dist
        cam.data.clip_end = 200
    K.render()


def _road_scene():
    K.reset_scene()
    objs, _ = RD.build_area({(0, 0): ROAD_TILE}, "a")
    return objs


def view_gamecam():
    files = []
    for name, hexc in D.REVIEW_COLORS:
        for door, label in ((0.0, "closed"), (D.DOOR_OPEN_DEG, "open")):
            _road_scene()
            root = _truck((LANE_X, 0.0, 0.0), hexc, door)
            _import_dog(DOG_AT, DOG_YAW)
            (x0, y0), (x1, y1) = GAMECAM_FRAME
            hint = _hint((x0, y0, 0.0), (x1, y1, D.HEIGHT))
            p = os.path.join(OUT, "gamecam-%s-%s.png" % (name.replace(" ", "-"), label))
            _render(p, GAMECAM_RES, [hint], margin=1.0)
            files.append((p, "%s, door %s" % (name, label)))
            del root
    sheet(files, os.path.join(OUT, "gamecam-sheet.png"), cols=2, scale=0.5)


def view_colors():
    """One game-camera tile per car colour, the same camera and scale for each."""
    files = []
    for name, hexc in D.CAR_COLORS:
        _road_scene()
        _truck((LANE_X, 0.0, 0.0), hexc)
        hint = _hint((LANE_X - COLORS_HALF, -COLORS_HALF, 0.0), (LANE_X + COLORS_HALF, COLORS_HALF, D.HEIGHT))
        p = os.path.join(TMP, "color_%s.png" % name.replace(" ", "_"))
        _render(p, COLOR_TILE_RES, [hint], margin=1.0)
        files.append((p, "%s %s" % (name, hexc)))
    sheet(files, os.path.join(OUT, "colors.png"), cols=4, scale=1.0)


def view_sides():
    """Both long sides of each review colour, orthographic and square on, so the
    lettering can be read: the left side (camera on +X) and the right (camera on -X)."""
    files = []
    for name, hexc in D.REVIEW_COLORS:
        for label, yaw in (("left side", SIDE_LEFT_YAW), ("right side", SIDE_RIGHT_YAW)):
            K.reset_scene()
            K.ground_plane(30, STUDIO)
            root = _truck(color=hexc)
            p = os.path.join(TMP, "side_%s_%s.png" % (name.replace(" ", "_"), label.split()[0]))
            _render(p, SIDE_RES, _all(root), yaw=yaw, pitch=SIDE_PITCH, margin=1.08)
            files.append((p, "%s, %s" % (name, label)))
    sheet(files, os.path.join(OUT, "lettering-sides.png"), cols=2, scale=1.0)


def view_closeup():
    _road_scene()
    root = _truck((LANE_X, 0.0, 0.0), DARK_BROWN[1])
    _render(os.path.join(OUT, "closeup-front.png"), CLOSE_RES, _all(root), yaw=-50.0, pitch=18.0,
            margin=1.0, ortho=False, lens=60.0)
    T.set_pose(root, door_deg=D.DOOR_OPEN_DEG)
    dog = _import_dog(DOG_AT, DOG_YAW)
    _render(os.path.join(OUT, "closeup-rear-open.png"), CLOSE_RES, _all(root) + dog, yaw=-145.0,
            pitch=22.0, margin=0.95, ortho=False, lens=60.0)


def view_motion():
    files = []
    for k, deg in enumerate(DOOR_FRAMES):
        K.reset_scene()
        K.ground_plane(30, STUDIO)
        root = _truck(color=RED[1], door=deg)
        p = os.path.join(TMP, "door_%d.png" % k)
        _render(p, FRAME_RES, _all(root), yaw=-150.0, pitch=25.0, margin=1.1)
        files.append((p, "door %d deg" % round(deg)))
    for k, deg in enumerate(WHEEL_FRAMES):
        K.reset_scene()
        K.ground_plane(30, STUDIO)
        _truck(color=RED[1], wheel=deg)
        x, y, z = D.WHEEL_PIVOTS["wheel_front_left"]
        hint = _hint((x, y - WHEEL_HALF, 0.0), (x + 0.2, y + WHEEL_HALF, z + WHEEL_HALF))
        p = os.path.join(TMP, "wheel_%d.png" % k)
        _render(p, FRAME_RES, [hint], yaw=90.0, pitch=8.0, margin=1.0)
        files.append((p, "wheel %d deg" % round(deg)))
    sheet(files, os.path.join(OUT, "motion-sheet.png"), cols=4, scale=1.0)


def sheet(files, path, cols, scale):
    ims = [Image.open(p).convert("RGB") for p, _ in files]
    w, h = int(ims[0].width * scale), int(ims[0].height * scale)
    rows = (len(ims) + cols - 1) // cols
    out = Image.new("RGB", (cols * w, rows * (h + LABEL_H)), (255, 255, 255))
    d = ImageDraw.Draw(out)
    for i, (im, (_, label)) in enumerate(zip(ims, files)):
        x, y = (i % cols) * w, (i // cols) * (h + LABEL_H)
        out.paste(im.resize((w, h), Image.LANCZOS), (x, y + LABEL_H))
        d.text((x + 14, y + 12), label, fill=(30, 30, 30), font=_font(24))
    out.save(path)


for v, fn in (("gamecam", view_gamecam), ("sides", view_sides), ("colors", view_colors), ("closeup", view_closeup),
              ("motion", view_motion)):
    if v in VIEWS:
        fn()
        print("rendered", v)
