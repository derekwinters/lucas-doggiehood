"""Render a labelled sampler grid per category, at the game camera.
Usage: blender-less python3 render_samplers.py <trees|plants|street> <out.png>"""
import math, os, sys
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import dh_env as E  # noqa
import dh_house_kit as K  # noqa
import dh_scene as S  # noqa
import bpy  # noqa
from mathutils import Vector  # noqa
from bpy_extras.object_utils import world_to_camera_view  # noqa
from PIL import Image, ImageDraw, ImageFont  # noqa

CAT, OUT = sys.argv[1], sys.argv[2]
LAYOUT = {"trees": (5, 8.0, 9.0), "plants": (4, 6.5, 6.0), "street": (4, 4.0, 4.6)}
COLS, SX, SY = LAYOUT[CAT]
if os.environ.get("DH_SAMPLER_LAYOUT"):        # e.g. "4,9,12" (cols, x step, y step)
    c_, x_, y_ = os.environ["DH_SAMPLER_LAYOUT"].split(",")
    COLS, SX, SY = int(c_), float(x_), float(y_)
RES = (2400, 1600)
YAW = math.radians(K.GAME_YAW)
RIGHT = Vector((math.cos(YAW), math.sin(YAW), 0))
UP = Vector((-math.sin(YAW), math.cos(YAW), 0))     # away from camera on the ground

K.reset_scene()
items = [(k, v) for k, v in E.PIECES.items() if v[1] == CAT] + [("dog", None)]
rows = (len(items) + COLS - 1) // COLS
objs, anchors = [], []
for i, (key, spec) in enumerate(items):
    r, c = divmod(i, COLS)
    p = RIGHT * (c - (COLS - 1) / 2) * SX + UP * ((rows - 1) / 2 - r) * SY
    if spec is None:
        ob = S.dog_box((p.x, p.y, 0), 0)
        label = "Dog (1.26 x 1.50 x 1.58 m)"
        obl = ob if isinstance(ob, list) else [ob]
    else:
        ob = spec[0]()
        bb = [ob.matrix_world @ Vector(cn) for cn in ob.bound_box]
        cx = sum(v.x for v in bb) / 8; cy = sum(v.y for v in bb) / 8
        ob.location = (p.x - cx, p.y - cy, 0)
        label = spec[2]
        obl = [ob] + list(ob.children)
    objs += obl
    anchors.append((obl, label))
ground = K.ground_plane(200, "#C9C4B6")
K.setup_render(OUT, res=RES, bg="#EEF1EC", samples=48)
bpy.context.view_layer.update()
cam = K.frame_camera([o for o in objs if o.type == "MESH"], margin=1.16, res=RES)
K.render()
sc = bpy.context.scene
img = Image.open(OUT).convert("RGB")
d = ImageDraw.Draw(img)
font = ImageFont.truetype("/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf", 26)
for obl, label in anchors:
    pts = [world_to_camera_view(sc, cam, o.matrix_world @ v.co) for o in obl for v in o.data.vertices]
    x = (min(q.x for q in pts) + max(q.x for q in pts)) / 2 * RES[0]
    y = (1 - min(q.y for q in pts)) * RES[1] + 12
    tw = d.textlength(label, font=font)
    d.rounded_rectangle((x - tw / 2 - 8, y - 4, x + tw / 2 + 8, y + 28), 6, fill="#FFFFFF", outline="#8A8A8A")
    d.text((x - tw / 2, y), label, fill="#222222", font=font)
img.save(OUT)
print("WROTE", OUT)
