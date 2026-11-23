"""Fit check for the backyard fence on the starter corner lot (#806, #783).

Builds a base at level 4 (garage, deck, patio; dh_scene.build_level), places
it with dh_scene.level4_placement() (the same rule as the House Design
clearance table) and traces the five LotFence runs exactly as Core does
(LotFence.BackyardRuns):

  connector   side-wall midpoint (-W/2, 0) -> side-run front end (fx0, 0)
  side run    (fx0, 0) -> (fx0, rear)
  rear run    (fx0, rear) -> (fx1, rear)
  side run    (fx1, rear) -> (fx1, 0)
  connector   (fx1, 0) -> side-wall midpoint (+W/2, 0)

in base-local Blender coordinates: pivot = centre of the base's wall
footprint, front -Y, so y = 0 is the house's depth midpoint, and the
connectors reach x = +-W/2 (W = the base's wall width, which is what
LotFence's scaled FootprintX/2 becomes once the footprint is the wall
rectangle; that definition itself is Open on #777/#778). The side runs sit
7.75 m off the side road's centreline and 2 m inside the far quadrant edge;
the rear run 2 m inside the rear quadrant edge (dh_scene's FENCE_* values).

Clearances are measured by casting horizontal rays from the fence centre
line toward the house at several heights up to the tallest fence option
(so eaves above every fence are ignored, and a porch floor or patio below it
is not), against each object separately: base, garage, deck, patio. The
driveway and front walkway are flat ground, so they are checked in plan.
"""
import math
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
sys.path.insert(0, os.path.normpath(os.path.join(HERE, "..", "..")))
import bpy  # noqa: E402
from mathutils import Vector  # noqa: E402
from mathutils.bvhtree import BVHTree  # noqa: E402
import dh_house_kit as K  # noqa: E402
import dh_scene as S  # noqa: E402

RAY_HEIGHTS = (0.04, 0.2, 0.45, 0.8, 1.2, 1.6, 1.9)   # up to the tallest option (privacy 1.83 m + cap)
RAY_STEP = 0.05                                       # sample spacing along a run
RAY_FAR = 60.0
CONNECTOR_Y_OFFSETS = (-0.09, 0.0, 0.09)               # the fence's own thickness band around y = 0
WALL_BAND = 0.25                                      # hits this close to the wall rectangle count as the wall
WALK_HALF = 0.6                                       # front walkway half-width (dh_scene.street)
RENDER_STOP_Z = "0.45"                                # renders stop a connector at its contact at this height
RUN_NAMES = ("connector_low", "side_low", "rear", "side_high", "connector_high")


def build_lot(n, addition_toward_road=True):
    """Level-4 base n + driveway; returns a dict with everything the checks
    and renders need. Clears the scene first."""
    import importlib
    K.reset_scene()
    B = importlib.import_module("base_house_%d" % n)
    pal = K.scheme_palette(B.RENDER_SCHEME)
    objs = S.build_level(B, pal, 4)
    dw, dinfo = S.driveway(B, palette=pal)
    b = S.composite_bounds(objs)
    P = S.level4_placement(b, B.FRONT_Y, addition_toward_road)
    W, D = B.DIMS["width"], B.DIMS["depth"]
    fx0, fx1, _, rear = P["fence"]
    runs = [(-W / 2, 0.0, fx0, 0.0), (fx0, 0.0, fx0, rear), (fx0, rear, fx1, rear), (fx1, rear, fx1, 0.0),
            (fx1, 0.0, W / 2, 0.0)]
    meshes = {o.name: o for o in S.meshes(objs)}
    named = {}
    for nm, o in meshes.items():
        if nm.startswith("base_house"):
            named["base"] = o
        elif "garage" in nm:
            named["garage"] = o
        elif "deck" in nm:
            named["deck"] = o
        elif "patio" in nm:
            named["patio"] = o
    walk = (getattr(B, "DOOR_X", 0.0) - WALK_HALF, getattr(B, "DOOR_X", 0.0) + WALK_HALF,
            P["road_cl_y"] + S.SIDEWALK_D, b[2])
    return {"n": n, "B": B, "pal": pal, "objs": objs, "driveway": dw, "dinfo": dinfo, "bounds": b, "P": P,
            "W": W, "D": D, "runs": runs, "named": named, "walk": walk}


def _bvh(o):
    bpy.context.view_layer.update()
    mw = o.matrix_world
    vs = [mw @ v.co for v in o.data.vertices]
    return BVHTree.FromPolygons(vs, [tuple(p.vertices) for p in o.data.polygons])


def _first_hits(bvhs, origin, d):
    out = {}
    for k, t in bvhs.items():
        h = t.ray_cast(Vector(origin), Vector(d), RAY_FAR)
        if h[0] is not None:
            out[k] = (h[3], h[0])
    return out


def _classify_base(p, W, D):
    if abs(p.x) <= W / 2 + WALL_BAND and abs(p.y) <= D / 2 + WALL_BAND:
        return "house wall/slab"
    return "base outside the walls (porch, steps or chimney)"


def _rect_seg_dist(rect, seg):
    """Plan distance from an axis-aligned rect (x0, x1, y0, y1) to an
    axis-aligned segment; 0 when they touch or cross."""
    x0, x1, y0, y1 = rect
    ax, ay, bx, by = seg
    sx0, sx1, sy0, sy1 = min(ax, bx), max(ax, bx), min(ay, by), max(ay, by)
    dx = max(0.0, max(x0 - sx1, sx0 - x1))
    dy = max(0.0, max(y0 - sy1, sy0 - y1))
    return math.hypot(dx, dy)


def check(lot):
    """Numbers for one lot: runs, clearances, where each connector lands."""
    W, D, runs, P = lot["W"], lot["D"], lot["runs"], lot["P"]
    bvhs = {k: _bvh(o) for k, o in lot["named"].items()}
    fx0, fx1, _, rear = P["fence"]
    yard_c = Vector(((fx0 + fx1) / 2, rear / 2, 0))
    res = {"base": lot["n"], "name": lot["B"].NAME, "addition_toward_road": P["addition_toward_road"],
           "quadrant": [round(v, 2) for v in P["quad"]], "fence_rect": [round(fx0, 3), round(fx1, 3), 0.0,
                                                                        round(rear, 3)],
           "yard_width_m": round(fx1 - fx0, 3), "wall_W": W, "wall_D": D,
           "composite_bounds": [round(v, 2) for v in lot["bounds"]], "placement_clearance": P["clearance"],
           "runs": [], "connectors": {}}
    for name, (ax, ay, bx, by) in zip(RUN_NAMES, runs):
        L = math.hypot(bx - ax, by - ay)
        run = {"run": name, "from": [round(ax, 3), round(ay, 3)], "to": [round(bx, 3), round(by, 3)],
               "length_m": round(L, 3)}
        if name.startswith("side") or name == "rear":
            t = Vector(((bx - ax) / L, (by - ay) / L, 0))
            nrm = Vector((-t.y, t.x, 0))
            mid = Vector(((ax + bx) / 2, (ay + by) / 2, 0))
            if nrm.dot(yard_c - mid) < 0:
                nrm = -nrm
            mins = {}
            steps = int(L / RAY_STEP)
            for i in range(steps + 1):
                s = i * L / steps
                for z in RAY_HEIGHTS:
                    o = Vector((ax, ay, z)) + t * s
                    for k, (dist, p) in _first_hits(bvhs, o, nrm).items():
                        if k not in mins or dist < mins[k][0]:
                            mins[k] = (dist, p)
            run["clearance_m"] = {k: round(v[0], 2) for k, v in sorted(mins.items())}
        res["runs"].append(run)
    # connectors: cast from the side-run corner toward the house
    for name, (ax, ay, bx, by) in (("connector_low", runs[0]), ("connector_high", runs[4])):
        corner = Vector((bx, by, 0))           # runs[0] ends at the corner; runs[4] starts there
        wall_end = Vector((ax, ay, 0))
        if name == "connector_high":
            corner, wall_end = Vector((ax, ay, 0)), Vector((bx, by, 0))
        d = (wall_end - corner).normalized()
        lit = (wall_end - corner).length
        firsts, by_z = {}, {}
        for z in RAY_HEIGHTS:
            for oy in CONNECTOR_Y_OFFSETS:
                o = corner + Vector((0, oy, z))
                for k, (dist, p) in _first_hits(bvhs, o, d).items():
                    if k not in firsts or dist < firsts[k][0]:
                        firsts[k] = (dist, p, z)
                    if z not in by_z or dist < by_z[z][0]:
                        by_z[z] = (dist, p, k)
        landing_k = min(firsts, key=lambda k: firsts[k][0]) if firsts else None
        info = {"literal_length_m": round(lit, 3), "literal_end": [round(wall_end.x, 3), round(wall_end.y, 3)]}
        if landing_k:
            dist, p, z = firsts[landing_k]
            what = landing_k if landing_k != "base" else _classify_base(p, W, D)
            info.update({"first_contact": what, "first_contact_object": landing_k,
                         "first_contact_x": round(p.x, 3), "first_contact_height_m": z,
                         "length_to_first_contact_m": round(dist, 3),
                         "overlap_past_first_contact_m": round(lit - dist, 3),
                         "contact_by_height": {str(z): [v[2], round(v[1].x, 3)] for z, v in sorted(by_z.items())}})
        # the spec-literal end: is (+-W/2, 0) on the base wall, or inside another piece?
        info["literal_end_inside"] = [k for k in ("garage", "deck", "patio") if k in lot["named"] and
                                      _inside_xy(lot["named"][k], wall_end)]
        # side clearance along the connector (front/back), up to first contact
        reach = info.get("length_to_first_contact_m", lit)
        side = {}
        n = max(1, int(reach / RAY_STEP))
        for i in range(n + 1):
            o0 = corner + d * (reach * i / n)
            for sgn in (-1, 1):
                for z in RAY_HEIGHTS:
                    o = o0 + Vector((0, 0, z))
                    for k, (dist, p) in _first_hits(bvhs, o, Vector((0, sgn, 0))).items():
                        key = "%s_%s" % (k, "front" if sgn < 0 else "behind")
                        if key not in side or dist < side[key]:
                            side[key] = dist
        info["side_clearance_m"] = {k: round(v, 2) for k, v in sorted(side.items())}
        res["connectors"][name] = info
    # flat ground: driveway (front-yard slab + verge slab) and front walkway, in plan
    dx0, dx1 = lot["dinfo"]["x"]
    dy0, dy1 = lot["dinfo"]["y"]
    drive = (dx0, dx1, dy0, dy1)
    res["driveway_rect"] = [round(v, 2) for v in drive]
    res["walkway_rect"] = [round(v, 2) for v in lot["walk"]]
    res["ground"] = {name: {"driveway_m": round(_rect_seg_dist(drive, r), 2),
                            "walkway_m": round(_rect_seg_dist(lot["walk"], r), 2)}
                     for name, r in zip(RUN_NAMES, runs)}
    res["crosses_driveway_or_walkway"] = any(v["driveway_m"] == 0 or v["walkway_m"] == 0
                                             for v in res["ground"].values())
    return res


def _inside_xy(o, p):
    bpy.context.view_layer.update()
    xs, ys = [], []
    for v in o.data.vertices:
        w = o.matrix_world @ v.co
        xs.append(w.x)
        ys.append(w.y)
    return min(xs) < p.x < max(xs) and min(ys) < p.y < max(ys)


def contact_ends(res):
    """Where a render stops each connector: its first contact with the house
    (render-only; whether a fence should stop there is #783's question)."""
    out = {}
    for name in ("connector_low", "connector_high"):
        c = res["connectors"][name]
        x = c.get("contact_by_height", {}).get(RENDER_STOP_Z, [None, c["literal_end"][0]])[1]
        out[name] = (x, 0.0)
    return out
