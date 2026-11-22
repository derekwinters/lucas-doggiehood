"""Dressed street with the round-3 street props: the approved houses (bases
1, 3, 5), the approved textured trees, and every new street prop placed at
the intersection, along the sidewalks and at the corners.
Usage: python3 render_street_props.py <dressed|closeup> <out.png>"""
import math, os, sys
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
# The committed round-3 tree modules (../trees) and house kit (art-source/blender).
sys.path.insert(0, os.path.normpath(os.path.join(HERE, "..", "trees")))
sys.path.insert(0, os.path.normpath(os.path.join(HERE, "..", "..")))
import dh_env as E  # noqa
import dh_trees_tex as TT  # noqa
import dh_props as P  # noqa
TT.install_into_env()
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
        place(P.mailbox(name="mailbox_%d" % k), dx + 1.6, VERGE_MID)
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
    # street trees in the tree lawn, lamps between them; skip walks and mailboxes
    for x in (-27, -9, 9, 26):
        place(E.tree_street(name="st_%d" % x), x, VERGE_MID, z=0.0)
        mb = E.EnvBuilder("ring_%d" % x)
        mb.disc((0, 0), 0.36, 0.0, 0.03, "mulch", n=10)
        place(mb.build(), x, VERGE_MID)
    for x in (-22, -4.5, 13.5):
        place(P.lamp_lantern(name="lamp_%d" % x), x, VERGE_MID)
    # ---- round-3 street props -------------------------------------------------
    N_VERGE, S_VERGE = VERGE_MID, ROAD_CL - 3.0 - E.VERGE_W / 2          # north / south tree lawns
    W_VERGE, E_VERGE = SIDE_X - 3.0 - E.VERGE_W / 2, SIDE_X + 3.0 + E.VERGE_W / 2
    N_CURB, S_CURB = ROAD_CL + 3.0, ROAD_CL - 3.0
    # intersection: stop for both side-road approaches, dead end up the north arm
    place(P.stop_sign(name="stop_nb"), E_VERGE, S_CURB - 0.9)                       # northbound, faces -Y
    place(P.stop_sign(name="stop_sb"), W_VERGE, N_CURB + 0.9, yaw=180)              # southbound, faces +Y
    place(P.dead_end_sign(name="dead_end"), E_VERGE, N_CURB + 6.5)                  # turning in, faces -Y
    # crosswalks on three legs + stop lines on the side road
    place(P.crosswalk(name="xw_n"), SIDE_X, ROAD_CL + 4.75, yaw=90, z=0.04)
    place(P.crosswalk(name="xw_s"), SIDE_X, ROAD_CL - 4.75, yaw=90, z=0.04)
    place(P.crosswalk(name="xw_w"), SIDE_X - 4.75, ROAD_CL, z=0.04)
    place(P.stop_line(name="sl_nb"), SIDE_X + 3.0, ROAD_CL - 6.9, yaw=90, z=0.04)
    place(P.stop_line(name="sl_sb"), SIDE_X - 3.0, ROAD_CL + 6.9, yaw=-90, z=0.04)
    place(P.crosswalk_sign(name="xw_sign"), SIDE_X - 6.4, S_VERGE, yaw=-90)        # eastbound, at the crossing
    place(P.ped_crossing_sign(name="ped_ahead"), 10.0, S_VERGE, yaw=-90)            # eastbound warning
    place(P.speed_limit_sign(name="speed25"), -15.0, S_VERGE, yaw=-90)
    place(P.dogs_at_play_sign(name="dogs_play"), 1.5, N_VERGE, yaw=90)
    # cobra-heads light the intersection; lanterns stay along the block
    place(P.lamp_cobra(name="cobra_sw"), SIDE_X - 6.1, ROAD_CL - 6.1, yaw=135)
    place(P.lamp_cobra(name="cobra_ne"), SIDE_X + 6.1, ROAD_CL + 6.1, yaw=-45)
    # hydrant + no-parking at the NW corner, bus stop in the corner lot
    place(P.hydrant(name="hydrant"), SIDE_X - 8.4, N_VERGE)
    place(P.no_parking_sign(name="no_park"), SIDE_X - 9.7, N_VERGE, yaw=90)
    place(P.bus_stop_sign(name="bus_sign"), SIDE_X - 11.0, N_VERGE, yaw=90)
    pad = P.PropBuilder("bus_pad")
    pad.box((-1.5, -0.55, -0.02), (1.5, 0.55, 0.035), "concrete", tile=P.CONC_TILE)
    place(pad.build(), SIDE_X - 7.4, STREET_Y - 2.15)
    place(P.bench_bus(name="bus_bench"), SIDE_X - 7.0, STREET_Y - 2.1, z=0.035)
    place(P.newspaper_box(name="news"), SIDE_X - 8.55, STREET_Y - 2.25, z=0.035)
    # pocket park on the NE corner: park bench, dog-waste station, bike rack, lantern
    pk = P.PropBuilder("park_pad")
    pk.box((SIDE_X + 7.0, STREET_Y - 2.75, -0.02), (SIDE_X + 14.8, STREET_Y + 0.2, 0.035), "concrete",
           tile=P.CONC_TILE)
    place(pk.build(), 0, 0)
    place(P.bench_park(name="park_bench_a"), SIDE_X + 9.4, STREET_Y - 0.55, z=0.035)
    place(P.dog_waste_station(name="waste"), SIDE_X + 7.55, STREET_Y - 1.1, z=0.035)
    place(P.bike_rack(name="bikes"), SIDE_X + 12.7, STREET_Y - 1.6, z=0.035)
    place(P.lamp_lantern(name="lamp_park"), SIDE_X + 14.2, STREET_Y - 0.4, z=0.035)
    # collection day: carts at the curb of the first two houses
    for k, (B, cx, dx) in enumerate(fronts[:2]):
        place(P.trash_bin(name="trash_%d" % k), dx - 1.9, N_VERGE)
        place(P.recycling_bin(name="recy_%d" % k), dx - 2.8, N_VERGE)
    # utility poles + wires along the south side
    poles = [-57.0, -27.0, -4.0, 19.5, 50.0]
    PY = S_VERGE - 2.7
    for x in poles:
        place(P.utility_pole(name="pole_%d" % x), x, PY)
    for a, b_ in zip(poles, poles[1:]):
        place(P.utility_wires(name="wires_%d" % a, span=b_ - a), a, PY)
    # road decals: manholes on the centreline, storm drains at the curbs
    for x, y in ((-15.0, ROAD_CL + 0.6), (12.0, ROAD_CL - 0.6), (SIDE_X + 0.6, ROAD_CL + 22.0)):
        place(P.manhole(name="mh_%d" % x), x, y, z=0.04)
    for x, y, yw in ((SIDE_X - 8.0, N_CURB, 0), (SIDE_X - 8.0, S_CURB, 180), (-3.0, N_CURB, 0),
                     (SIDE_X + 3.0, ROAD_CL + 10.0, -90)):
        place(P.storm_drain(name="sd_%d_%d" % (x, y)), x, y, yaw=yw, z=0.04)

K.setup_render(OUT, res=RES, bg="#E8EEF2", samples=48)
if CLOSEUP:
    CAM_YAW, CAM_PITCH = K.GAME_YAW, K.GAME_PITCH
    RES = (1600, 1200)
    sc = bpy.context.scene
    sc.render.resolution_x, sc.render.resolution_y = RES
    hints = [S.frame_hint(17.0, ROAD_CL + 3.0), S.frame_hint(SIDE_X + 14.0, STREET_Y + 2.0),
             S.frame_hint(SIDE_X + 4.0, ROAD_CL - 7.0), S.frame_hint(20.0, ROAD_CL - 6.0)]
else:
    hints = [S.frame_hint(-30, ROAD_CL - 1), S.frame_hint(36, STREET_Y + 27), S.frame_hint(SIDE_X + 14.5, ROAD_CL - 7.2)]
K.frame_camera(hints, margin=1.0, yaw=CAM_YAW, pitch=CAM_PITCH, res=RES)
for h in hints:
    bpy.data.objects.remove(h)
K.render()
print("WROTE", OUT)
