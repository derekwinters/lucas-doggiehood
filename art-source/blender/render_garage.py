"""Review renders for the garage concept (#780, #785), per base:
  base-NN-street.png   level 4 on its lot at the game camera (ortho, yaw -40,
                       pitch 45): street, sidewalk, front walkway, the proposed
                       driveway, lot line (dashed) and backyard fence line
                       (white), dog for scale; garage on the side-road side
  base-NN-back.png     level 4 from the back (yaw 140): patio behind the garage,
                       deck stairs landing on it
  base-NN-closeup.png  level 2: the garage and its driveway, front three-quarter
Usage: python3 render_garage.py OUT_DIR [--bases 1,2,...] [--views street,back,closeup]
"""
import importlib
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import bpy  # noqa: E402
import dh_house_kit as K  # noqa: E402
import dh_scene as S  # noqa: E402

args = [a for a in sys.argv[1:] if not a.startswith("--")]
OUT = args[0]
os.makedirs(OUT, exist_ok=True)
NS, VIEWS = list(range(1, 11)), {"street", "back", "closeup"}
for i, a in enumerate(sys.argv):
    if a == "--bases":
        NS = [int(x) for x in sys.argv[i + 1].split(",")]
    if a == "--views":
        VIEWS = set(sys.argv[i + 1].split(","))
BG = K.COMMON["ground_studio"]
SAMPLES = 40


def setup(n, B, level):
    K.reset_scene()
    pal = K.scheme_palette(B.RENDER_SCHEME)
    objs = S.build_level(B, pal, level)
    dw, info = S.driveway(B, palette=pal)
    return objs, dw, info, pal


for n in NS:
    B = importlib.import_module("base_house_%d" % n)
    g = S.fit_pieces(B)[0]
    ax, ay, _ = B.attach_locations()["ATTACH_ADDITION"]
    dx = getattr(B, "DOOR_X", 0.0)
    if "street" in VIEWS:
        objs, dw, info, pal = setup(n, B, 4)
        b = S.composite_bounds(objs)
        P = S.level4_placement(b, B.FRONT_Y, True)
        st = [o for o in bpy.data.objects if o.type == "STRUCTURE"]
        S.street(P["road_cl_y"], walk_xs=[dx], front_y=b[2] + 0.05, side_road_x=P["side_road_x"])
        S.lot_lines(P, B.DIMS["width"] / 2, g["width"] + K.SLAB_OVERHANG)
        dog = S.dog_box((dx - 2.2, P["road_cl_y"] + 4.75, 0), yaw_deg=-25)
        hint = S.frame_hint(P["side_road_x"] - 0.5, P["road_cl_y"] + 2.0)
        hint2 = S.frame_hint(P["fence"][0] + 1.0, P["fence"][3] + 0.5)
        K.setup_render(os.path.join(OUT, "base-%02d-street.png" % n), res=(1500, 1100), bg=BG, samples=SAMPLES)
        K.frame_camera(S.meshes(objs) + [dw, dog, hint, hint2], margin=1.05, res=(1500, 1100))
        bpy.data.objects.remove(hint)
        bpy.data.objects.remove(hint2)
        K.render()
    if "back" in VIEWS:
        objs, dw, info, pal = setup(n, B, 4)
        bpy.data.objects.remove(dw)
        dog = S.dog_box((ax + g["width"] / 2 + 0.3, ay + g["depth"] / 2 + 2.6, 0.08), yaw_deg=150)
        K.ground_plane(80, BG)
        K.setup_render(os.path.join(OUT, "base-%02d-back.png" % n), res=(1500, 1100), bg=BG, samples=SAMPLES)
        K.frame_camera(S.meshes(objs) + [dog], margin=1.06, yaw=140, res=(1500, 1100))
        K.render()
    if "closeup" in VIEWS:
        objs, dw, info, pal = setup(n, B, 2)
        ms = S.meshes(objs)
        road_cl = B.FRONT_Y - S.FRONT_D
        S.street(road_cl, x_range=(-40, 40), walk_xs=[dx], front_y=B.FRONT_Y - 0.6)
        dog = S.dog_box((ax + g["width"] / 2 - 0.4, road_cl + 4.75, 0), yaw_deg=-160)
        h1 = S.frame_hint(ax + g["width"] + 1.2, ay - g["depth"] / 2 - 4.0)
        h2 = S.frame_hint(ax - 3.0, ay + g["depth"] / 2)
        h3 = S.frame_hint(ax - 3.0, road_cl + 3.5)
        h4 = S.frame_hint(ax + g["width"] + 1.2, road_cl + 3.5)
        K.setup_render(os.path.join(OUT, "base-%02d-closeup.png" % n), res=(1400, 1050), bg=BG, samples=SAMPLES)
        K.frame_camera([ms[1], dog, h1, h2, h3, h4], margin=1.02, yaw=28, pitch=28, res=(1400, 1050))
        for h in (h1, h2, h3, h4):
            bpy.data.objects.remove(h)
        K.render()
print("DONE")
