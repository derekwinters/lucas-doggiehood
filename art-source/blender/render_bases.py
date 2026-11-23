"""Concept renders + GLBs for base houses 1-10, the line-ups and the
neighbourhood. Level 2 is the garage (#780, #817). Usage:

    python3 art-source/blender/render_bases.py OUT_DIR [--bases 6,7,8,9,10]
        [--only finished,framing,level4close,level4back,glb,
                lineup,lineupframing,lineup4,lineup2,lineup2framing,lineup24,lineupall,neighborhood]

Per-base renders/GLBs are made for --bases; stats (bases_stats.json) always
cover all ten.
"""
import json
import math
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import bpy  # noqa: E402
import dh_house_kit as K  # noqa: E402
import dh_scene as S  # noqa: E402
import importlib  # noqa: E402

BASES = {n: importlib.import_module("base_house_%d" % n) for n in range(1, 11)}
args = [a for a in sys.argv[1:] if not a.startswith("--")]
OUT = args[0] if args else os.path.join(HERE, "out")
os.makedirs(OUT, exist_ok=True)
ONLY, NS = None, [6, 7, 8, 9, 10]
for i, a in enumerate(sys.argv):
    if a == "--only":
        ONLY = set(sys.argv[i + 1].split(","))
    if a == "--bases":
        NS = [int(x) for x in sys.argv[i + 1].split(",")]
BG = K.COMMON["ground_studio"]


def want(n):
    return ONLY is None or n in ONLY


def pal(B):
    return K.scheme_palette(B.RENDER_SCHEME)


def door_x(B):
    return getattr(B, "DOOR_X", 0.0)


def bounds3(o):
    xs = [v.co.x for v in o.data.vertices]
    ys = [v.co.y for v in o.data.vertices]
    zs = [v.co.z for v in o.data.vertices]
    return [round(min(xs), 2), round(max(xs), 2), round(min(ys), 2), round(max(ys), 2), round(max(zs), 2)]


def shift(objs, dx, dy=0.0, yaw=0.0):
    for o in objs:
        if o.parent is None:
            o.location.x += dx
            o.location.y += dy


# ---------------------------------------------------------------- stats
STATS = {}
for n, B in BASES.items():
    r = {"name": B.NAME, "scheme": B.RENDER_SCHEME, "dims": B.DIMS, "front_y": B.FRONT_Y}
    for mode in ("finished", "framing"):
        K.reset_scene()
        ob, _ = B.build(mode, palette=pal(B))
        r["tris_" + mode] = K.tri_count(ob)
        r["bounds_" + mode] = bounds3(ob)
        if want("glb") and n in NS:
            K.export_glb(os.path.join(OUT, "base-%d-%s.glb" % (n, mode)), [ob])
    for style_scheme in ("Sage Cottage", "Greige Modern"):     # shingle vs metal roof
        K.reset_scene()
        objs = S.build_level(B, K.scheme_palette(style_scheme), 4)
        r["tris_level4_" + K.scheme_palette(style_scheme)["_roof_style"]] = \
            sum(K.tri_count(o) for o in S.meshes(objs))
    K.reset_scene()
    objs = S.build_level(B, pal(B), 4)
    b = S.composite_bounds(objs)
    r["placement_garage_road_side"] = S.level4_placement(b, B.FRONT_Y, True)
    r["placement_garage_far_side"] = S.level4_placement(b, B.FRONT_Y, False)
    a, d, p = S.fit_pieces(B)
    r["pieces"] = {"garage": a, "deck": d, "patio": p}
    r["attach"] = {k: list(v) for k, v in B.attach_locations().items()}
    STATS[n] = r
with open(os.path.join(HERE, "bases_stats.json"), "w") as f:
    json.dump(STATS, f, indent=1, default=str)
print("STATS written")


# ---------------------------------------------------------------- per base
for n in NS:
    B = BASES[n]
    for mode in ("finished", "framing"):
        if not want(mode):
            continue
        K.reset_scene()
        ref, ref_att = B.build("finished", palette=pal(B), name="frame_ref")
        K.ground_plane(80, BG)
        K.setup_render(os.path.join(OUT, "base-%d-%s.png" % (n, mode)), bg=BG)
        K.frame_camera([ref], margin=1.12)
        if mode == "framing":
            for o in [ref] + ref_att:
                bpy.data.objects.remove(o)
            B.build("framing", palette=pal(B))
        K.render()

    if want("level4close"):
        K.reset_scene()
        objs = S.build_level(B, pal(B), 4)
        P = STATS[n]["placement_garage_road_side"]
        ms = S.meshes(objs)
        y_front = P["composite"][2]
        S.street(P["road_cl_y"], walk_xs=[door_x(B)], front_y=y_front, side_road_x=P["side_road_x"])
        half_w = B.DIMS["width"] / 2
        S.lot_lines(P, half_w, STATS[n]["pieces"]["garage"]["width"])
        dog = S.dog_box((door_x(B) + 2.0, y_front - 1.6, 0), yaw_deg=-25)
        hint = S.frame_hint(P["side_road_x"] - 0.5, B.FRONT_Y + 1.5)
        K.setup_render(os.path.join(OUT, "base-%d-level4-close.png" % n), bg=BG)
        K.frame_camera(ms + [dog, hint], margin=1.12)
        bpy.data.objects.remove(hint)
        K.render()

    if want("level4back"):
        K.reset_scene()
        objs = S.build_level(B, pal(B), 4)
        ax, ay, _ = B.attach_locations()["ATTACH_ADDITION"]
        a = STATS[n]["pieces"]["garage"]
        dog = S.dog_box((ax + a["width"] / 2 + 0.3, ay + a["depth"] / 2 + 2.4, 0.08), yaw_deg=150)
        K.ground_plane(80, BG)
        K.setup_render(os.path.join(OUT, "base-%d-level4-back.png" % n), bg=BG)
        K.frame_camera(S.meshes(objs) + [dog], margin=1.06, yaw=140)
        K.render()

# ---------------------------------------------------------------- line-ups
LINEUP_SCHEMES = {1: "Sage Cottage", 2: "Dove Gray", 3: "Colonial White", 4: "Charcoal Craftsman",
                  5: "Coastal Blue", 6: "Navy Saltbox", 7: "Greige Modern", 8: "Cream & Forest",
                  9: "Adobe Tile", 10: "Buttercream"}
GAP = 3.0
ROW_GAP = 34.0                         # second row of lineup-all sits this far behind the first
LINEUP_YAW, LINEUP_PITCH = -18, 30     # flatter 3/4 view so the silhouettes read side by side


def _row(ns, level, mode, y_off=0.0):
    xs, ms = 0.0, []
    for n in ns:
        B = BASES[n]
        objs = S.build_level(B, K.scheme_palette(LINEUP_SCHEMES[n]), level, mode=mode)
        b = S.composite_bounds(objs)
        shift(objs, xs - b[0], -B.FRONT_Y + BASES[1].FRONT_Y + y_off)   # FRONT_Y lines on one street line
        xs += (b[1] - b[0]) + GAP
        ms += S.meshes(objs)
    return ms, xs - GAP


def lineup(rows, level, mode, path, res=(2400, 900)):
    K.reset_scene()
    ms = []
    for i, ns in enumerate(rows):
        row, width = _row(ns, level, mode, y_off=i * ROW_GAP)
        ms += row
        ms.append(S.dog_box((width / 2, BASES[1].FRONT_Y - 2.2 + i * ROW_GAP, 0), yaw_deg=-20))
    K.ground_plane(800, BG)
    K.setup_render(path, res=res, bg=BG)
    K.frame_camera(ms, margin=1.04, yaw=LINEUP_YAW, pitch=LINEUP_PITCH, res=res)
    K.render()


SET1, SET2 = [1, 2, 3, 4, 5], [6, 7, 8, 9, 10]
for key, rows, level, mode, fn in (
        ("lineup", [SET1], 1, "finished", "lineup-finished.png"),
        ("lineupframing", [SET1], 1, "framing", "lineup-framing.png"),
        ("lineup4", [SET1], 4, "finished", "lineup-level4.png"),
        ("lineup2", [SET2], 1, "finished", "lineup2-finished.png"),
        ("lineup2framing", [SET2], 1, "framing", "lineup2-framing.png"),
        ("lineup24", [SET2], 4, "finished", "lineup2-level4.png")):
    if want(key):
        lineup(rows, level, mode, os.path.join(OUT, fn))
if want("lineupall"):
    lineup([SET1, SET2], 1, "finished", os.path.join(OUT, "lineup-all-finished.png"), res=(2400, 1500))

# ---------------------------------------------------------------- street
if want("neighborhood"):
    # two parallel streets, 36 m apart: five houses on each
    K.reset_scene()
    streets = [
        [(9, "Adobe Tile", 2), (1, "Coastal Blue", 1), (10, "Dove Gray", 4), (4, "Cedar Lodge", 3),
         (7, "Colonial White", 1)],
        [(3, "Navy Saltbox", 4), (8, "Cream & Forest", 2), (2, "Greige Modern", 1), (6, "Red Farmhouse", 3),
         (5, "Buttercream", 2)],
    ]
    spacing, street_gap = 22.0, 36.0
    ms = []
    for r, picks in enumerate(streets):
        street_y = BASES[1].FRONT_Y + r * street_gap
        walks = []
        for i, (n, sch, lvl) in enumerate(picks):
            B = BASES[n]
            dx = (i - (len(picks) - 1) / 2) * spacing + (r - 0.5) * 4.0
            objs = S.build_level(B, K.scheme_palette(sch), lvl)
            b = S.composite_bounds(objs)
            cx = dx - (b[0] + b[1]) / 2
            shift(objs, cx, street_y - B.FRONT_Y)
            ms += S.meshes(objs)
            walks.append(cx + door_x(B))
        road_cl = street_y - S.FRONT_D
        S.street(road_cl, x_range=(-200, 200), walk_xs=walks, front_y=street_y - 0.6, grass=(r == 0))
        for x, yaw in ((-33 + r * 8, -20), (12 - r * 20, 35)):
            S.dog_box((x, street_y - 2.3, 0.0), yaw_deg=yaw)
    res = (2000, 1250)
    K.setup_render(os.path.join(OUT, "color-neighborhood.png"), res=res, bg=BG, samples=32)
    hint = S.frame_hint(0, BASES[1].FRONT_Y - S.FRONT_D - 3.2)
    K.frame_camera(ms + [hint], margin=1.02, yaw=-14, pitch=42, res=res)
    bpy.data.objects.remove(hint)
    K.render()
print("DONE")
