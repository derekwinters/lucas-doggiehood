"""Round 1 vs round 2 vs textured maple, side by side, labelled with
triangle counts. Usage: python3 render_compare.py <out.png>"""
import importlib.util, os, sys
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import dh_trees_tex as TT  # noqa
import dh_env as E2  # noqa
import dh_house_kit as K  # noqa
import dh_scene as S  # noqa
import bpy  # noqa
from mathutils import Vector  # noqa
from bpy_extras.object_utils import world_to_camera_view  # noqa
from PIL import Image, ImageDraw, ImageFont  # noqa
spec = importlib.util.spec_from_file_location("dh_env_round1", os.path.join(HERE, "dh_env_round1.py"))
E1 = importlib.util.module_from_spec(spec)
spec.loader.exec_module(E1)

OUT = sys.argv[1]
RES = (2400, 1100)
K.reset_scene()
trees = [(E1.tree_maple("large", name="r1_maple"), "Round 1: low-poly"),
         (E2.tree_maple("large", name="r2_maple"), "Round 2: high-poly"),
         (TT.tree_maple("large", name="r3_maple"), "Round 3: textured cards")]
for i, (ob, _) in enumerate(trees):
    ob.location = ((i - 1) * 9.5, 0, 0)
E2.lawn_patch(-80, 80, -60, 60)
K.setup_render(OUT, res=RES, bg="#E8EEF2", samples=64)
cam = K.frame_camera([o for o, _ in trees], margin=1.12, yaw=-30, pitch=24, res=RES)
bpy.context.view_layer.update()
K.render()
sc = bpy.context.scene
img = Image.open(OUT).convert("RGB")
d = ImageDraw.Draw(img)
f1 = ImageFont.truetype("/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf", 34)
f2 = ImageFont.truetype("/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf", 28)
for ob, label in trees:
    tris = K.tri_count(ob)
    co = world_to_camera_view(sc, cam, ob.matrix_world @ Vector((0, 0, 0)))
    x, y = co.x * RES[0], (1 - co.y) * RES[1] + 30
    l2 = "%s triangles" % format(tris, ",")
    tw = max(d.textlength(label, font=f1), d.textlength(l2, font=f2))
    d.rounded_rectangle((x - tw / 2 - 14, y - 6, x + tw / 2 + 14, y + 80), 10, fill="#FFFFFF", outline="#8A8A8A")
    d.text((x - d.textlength(label, font=f1) / 2, y), label, fill="#222222", font=f1)
    d.text((x - d.textlength(l2, font=f2) / 2, y + 42), l2, fill="#444444", font=f2)
    print(label, tris)
img.save(OUT)
