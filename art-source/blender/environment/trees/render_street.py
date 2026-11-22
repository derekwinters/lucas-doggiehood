"""Dressed vs undressed street: three approved houses (bases 1, 3, 5) on a
street with a side road. Usage: python3 render_street.py <dressed|undressed> <out.png>"""
import math, os, sys
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import dh_env as E  # noqa
import dh_house_kit as K  # noqa
import dh_scene as S  # noqa
import importlib  # noqa
import bpy  # noqa

MODE, OUT = sys.argv[1], sys.argv[2]
DRESSED = MODE in ("dressed", "closeup")
CLOSEUP = MODE == "closeup"          # game camera (yaw -40, pitch 45) on the corner lot
HOUSES = [(1, "Sage Cottage"), (3, "Colonial White"), (5, "Coastal Blue")]
SPACING = 18.0
STREET_Y = 0.0                       # every house's FRONT_Y lands here
ROAD_CL = STREET_Y - S.FRONT_D
SIDE_X = 33.0                        # side road centreline (corner lot is the right-hand house)
VERGE_MID = ROAD_CL + 3.0 + E.VERGE_W / 2
WALK_HALF = 0.6
RES = (2000, 1250)
CAM_YAW, CAM_PITCH = -12.0, 40.0

K.reset_scene()
ms = []


def place(ob, x, y, yaw=0.0, z=0.0):
    ob.location = (x, y, z)
    ob.rotation_euler = (0, 0, math.radians(yaw))
    ms.append(ob)
    ms.extend(ob.children)
    return ob


walks, fronts = [], []
for i, (n, sch) in enumerate(HOUSES):
    B = importlib.import_module("base_house_%d" % n)
    objs = S.build_level(B, K.scheme_palette(sch), 1)
    b = S.composite_bounds(objs)
    cx = (i - 1) * SPACING - (b[0] + b[1]) / 2
    for o in objs:
        if o.parent is None:
            o.location.x += cx
            o.location.y += STREET_Y - B.FRONT_Y
    ms += S.meshes(objs)
    dx = cx + getattr(B, "DOOR_X", 0.0)
    walks.append(dx)
    fronts.append((B, cx, dx))

ms.append(S.street(ROAD_CL, x_range=(-120, 120), walk_xs=walks, front_y=STREET_Y - 0.6, side_road_x=SIDE_X,
                   grass=not DRESSED))
if DRESSED:
    ms.append(E.lawn_patch(-120, 120, ROAD_CL - 80, ROAD_CL + 120))

S.dog_box((walks[0] + 4.0, ROAD_CL + 4.75, 0), yaw_deg=-70)

if DRESSED:
    for k, (B, cx, dx) in enumerate(fronts):
        W = B.DIMS["width"]
        y_back = STREET_Y - K.SLAB_OVERHANG - 0.02
        depth = 1.1
        # foundation beds either side of the door / steps
        for xa, xb in ((cx - W / 2 - 0.3, dx - 1.45), (dx + 1.45, cx + W / 2 + 0.3)):
            L = xb - xa
            if L > 1.4:
                place(E.bed_foundation(length=L, depth=depth, name="bed_%d_%d" % (k, xa > dx)), (xa + xb) / 2, y_back)
        # walkway borders down both sides of the front walk
        y0 = ROAD_CL + S.SIDEWALK_D + 0.15
        Lw = (y_back - depth - 0.15) - y0
        place(E.walk_border(length=Lw, width=0.5, name="border_l%d" % k), dx - WALK_HALF - 0.55, y0)
        place(E.walk_border(length=Lw, width=0.5, name="border_r%d" % k), dx + WALK_HALF + 0.05, y0)
        # mailbox at the curb, beside the walk
        place(E.mailbox(name="mailbox_%d" % k), dx + 1.6, VERGE_MID)
        # side yards: a feature tree each side, columnar accents at the back corners
        side = [E.tree_japanese_maple, E.tree_cherry, E.tree_maple][k]
        sx = cx - W / 2 - 3.6 if k != 2 else cx + W / 2 + 3.4
        place(side(name="yard_%d" % k) if side is E.tree_japanese_maple else side("small", name="yard_%d" % k),
              sx, STREET_Y + 2.5)
        place(E.tree_columnar("small", name="col_%d" % k), cx + W / 2 + 1.4, STREET_Y + B.DIMS["depth"] - 1.0)
        place(E.shrub_round("large", name="corner_shrub_%d" % k), cx - W / 2 - 0.9, STREET_Y + 0.4)
    # back-of-lot trees behind the row
    for x, f in ((-26, lambda: E.tree_spruce("large", name="spr_a")), (-8.5, lambda: E.tree_maple("large", name="mp_b")),
                 (9, lambda: E.tree_spruce("large", name="spr_b")), (23, lambda: E.tree_maple("large", name="mp_c"))):
        place(f(), x, STREET_Y + 15.5)
    place(E.hedge_boxwood(length=3.0, name="hedge_a"), walks[1] + 4.6, ROAD_CL + S.SIDEWALK_D + 0.6)
    place(E.planter(name="planter_a"), walks[1] + 1.3, STREET_Y + 0.2)
    place(E.planter(name="planter_b"), walks[1] - 1.3, STREET_Y + 0.2)
    place(E.stepping_stones(count=5, name="stones_a"), fronts[0][1] + 4.6, ROAD_CL + S.SIDEWALK_D + 0.5)
    place(E.bed_corner(name="corner_bed"), SIDE_X - S.SIDEWALK_D - 0.3, ROAD_CL + S.SIDEWALK_D + 0.3, yaw=90)
    # street trees in the tree lawn, lamps between them; skip walks and mailboxes
    for x in (-27, -9, 9, 26):
        place(E.tree_street(name="st_%d" % x), x, VERGE_MID, z=0.0)
        mb = E.EnvBuilder("ring_%d" % x)
        mb.disc((0, 0), 0.36, 0.0, 0.03, "mulch", n=10)
        place(mb.build(), x, VERGE_MID)
    for x in (-22, -4.5, 13.5):
        place(E.lamp_post(name="lamp_%d" % x), x, VERGE_MID)
    # corner: side-road stop sign on the near side for traffic heading toward the main road,
    # street-name sign + hydrant on the far corner, bench on the corner lot
    place(E.stop_sign(name="stop"), SIDE_X + 3.0 + E.VERGE_W / 2, ROAD_CL - 3.0 - E.VERGE_W - 2.4)
    place(E.street_sign(name="names"), SIDE_X - 3.0 - E.VERGE_W / 2, VERGE_MID)
    place(E.hydrant(name="hydrant"), SIDE_X - 8.0, VERGE_MID)
    place(E.bench(name="bench"), SIDE_X - S.SIDEWALK_D - 0.7, ROAD_CL - 9.5, yaw=90)
    place(E.lamp_post(name="lamp_corner"), SIDE_X + 3.0 + E.VERGE_W / 2, VERGE_MID)

K.setup_render(OUT, res=RES, bg="#E8EEF2", samples=40)
if CLOSEUP:
    CAM_YAW, CAM_PITCH = K.GAME_YAW, K.GAME_PITCH
    RES = (1600, 1200)
    sc = bpy.context.scene
    sc.render.resolution_x, sc.render.resolution_y = RES
hint_a = S.frame_hint(-30, ROAD_CL - 1) if not CLOSEUP else S.frame_hint(8.0, ROAD_CL + 2.0)
hint_b = S.frame_hint(36, STREET_Y + 27) if not CLOSEUP else S.frame_hint(22.0, STREET_Y + 10.0)
hint_c = S.frame_hint(37.5, ROAD_CL - 7.2) if not CLOSEUP else S.frame_hint(33.0, ROAD_CL - 7.5)
houses = [o for o in ms if o.type == "MESH" and o.name.startswith(("Base", "base"))]
K.frame_camera([hint_a, hint_b, hint_c], margin=1.0, yaw=CAM_YAW, pitch=CAM_PITCH, res=RES)
bpy.data.objects.remove(hint_a)
bpy.data.objects.remove(hint_b)
bpy.data.objects.remove(hint_c)
K.render()
print("WROTE", OUT)
