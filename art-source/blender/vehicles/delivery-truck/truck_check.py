"""Read the delivery truck's .glb back with plain Python (no Blender) and check it (#924).

  python3 truck_check.py [path/to/delivery_truck.glb]

Used by export_truck.py after every export and by tests/. Checks: the footprint
equals today's truck from Core (#660, #672), every mesh is under ArtBudget's
per-mesh cap and the whole truck under the #799 budget, the node set and pivots
match truck_dims, the open door stays inside the width, and only the body and
door use the tint material. Prints the errors and exits non-zero if any.
"""
import json
import math
import os
import struct
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import truck_dims as D  # noqa: E402

core_footprint = D.core_footprint
core_road_width = D.core_road_width
core_max_tris_per_mesh = D.core_max_tris_per_mesh

COMP = {5120: "b", 5121: "B", 5122: "h", 5123: "H", 5125: "I", 5126: "f"}
NCOMP = {"SCALAR": 1, "VEC2": 2, "VEC3": 3, "VEC4": 4}
TOL = 1e-3


def load(path):
    """The glTF JSON with its binary chunk attached as g["_bin"]."""
    data = open(path, "rb").read()
    magic, _, _ = struct.unpack_from("<III", data, 0)
    if magic != 0x46546C67:
        raise ValueError("not a .glb: %s" % path)
    jlen = struct.unpack_from("<I", data, 12)[0]
    g = json.loads(data[20:20 + jlen].decode("utf-8"))
    off = 20 + jlen
    blen = struct.unpack_from("<I", data, off)[0]
    g["_bin"] = data[off + 8:off + 8 + blen]
    return g


def accessor(g, i):
    a = g["accessors"][i]
    bv = g["bufferViews"][a["bufferView"]]
    n = NCOMP[a["type"]]
    fmt = COMP[a["componentType"]]
    size = struct.calcsize(fmt)
    stride = bv.get("byteStride", size * n)
    base = bv.get("byteOffset", 0) + a.get("byteOffset", 0)
    out = []
    for k in range(a["count"]):
        v = struct.unpack_from("<%d%s" % (n, fmt), g["_bin"], base + k * stride)
        out.append(v if n > 1 else v[0])
    return out


def to_gltf(p):
    """Blender (x, y, z) -> glTF Y-up (x, z, -y)."""
    return [p[0], p[2], -p[1]]


def close(a, b, tol=TOL):
    return len(a) == len(b) and all(abs(x - y) <= tol for x, y in zip(a, b))


def nodes_by_name(g):
    return {n["name"]: n for n in g["nodes"]}


def materials_by_name(g):
    return {m["name"]: m for m in g.get("materials", [])}


def mesh_positions(g, mi):
    pts = []
    for prim in g["meshes"][mi]["primitives"]:
        pts += accessor(g, prim["attributes"]["POSITION"])
    return pts


def mesh_bounds(g, mi):
    pts = mesh_positions(g, mi)
    return [min(p[i] for p in pts) for i in range(3)], [max(p[i] for p in pts) for i in range(3)]


def _quat_rot(q, v):
    x, y, z, w = q
    # v' = v + 2w(q x v) + 2 q x (q x v)
    cx = y * v[2] - z * v[1]
    cy = z * v[0] - x * v[2]
    cz = x * v[1] - y * v[0]
    ccx = y * cz - z * cy
    ccy = z * cx - x * cz
    ccz = x * cy - y * cx
    return [v[0] + 2 * (w * cx + ccx), v[1] + 2 * (w * cy + ccy), v[2] + 2 * (w * cz + ccz)]


def _apply(node, p):
    s = node.get("scale", [1, 1, 1])
    p = [p[0] * s[0], p[1] * s[1], p[2] * s[2]]
    if "rotation" in node:
        p = _quat_rot(node["rotation"], p)
    t = node.get("translation", [0, 0, 0])
    return [p[0] + t[0], p[1] + t[1], p[2] + t[2]]


def _parents(g):
    par = {}
    for i, n in enumerate(g["nodes"]):
        for c in n.get("children", []):
            par[c] = i
    return par


def _world_points(g, ni, pts):
    par = _parents(g)
    while ni is not None:
        pts = [_apply(g["nodes"][ni], p) for p in pts]
        ni = par.get(ni)
    return pts


def node_world_bounds(g, name):
    idx = [i for i, n in enumerate(g["nodes"]) if n["name"] == name][0]
    pts = _world_points(g, idx, mesh_positions(g, g["nodes"][idx]["mesh"]))
    return [min(p[i] for p in pts) for i in range(3)], [max(p[i] for p in pts) for i in range(3)]


def scene_bounds(g):
    lo, hi = [math.inf] * 3, [-math.inf] * 3
    for n in g["nodes"]:
        if "mesh" in n:
            a, b = node_world_bounds(g, n["name"])
            lo = [min(x, y) for x, y in zip(lo, a)]
            hi = [max(x, y) for x, y in zip(hi, b)]
    return lo, hi


def tris_per_node(g):
    out = {}
    for n in g["nodes"]:
        if "mesh" in n:
            t = 0
            for prim in g["meshes"][n["mesh"]]["primitives"]:
                t += g["accessors"][prim["indices"]]["count"] // 3
            out[n["name"]] = t
    return out


def materials_per_node(g):
    names = [m["name"] for m in g.get("materials", [])]
    out = {}
    for n in g["nodes"]:
        if "mesh" in n:
            out[n["name"]] = sorted({names[p["material"]] for p in g["meshes"][n["mesh"]]["primitives"]
                                     if "material" in p})
    return out


def door_open_x(g, open_deg, name="door_rear"):
    """World X of every door vertex with the door swung open_deg about its hinge
    (Blender +Z, which is glTF +Y)."""
    idx = [i for i, n in enumerate(g["nodes"]) if n["name"] == name][0]
    a = math.radians(open_deg) / 2
    q = [0.0, math.sin(a), 0.0, math.cos(a)]
    pts = [_quat_rot(q, p) for p in mesh_positions(g, g["nodes"][idx]["mesh"])]
    return [p[0] for p in _world_points(g, idx, pts)]


def stats(g):
    lo, hi = scene_bounds(g)
    tris = tris_per_node(g)
    nodes = nodes_by_name(g)
    return {
        "size_gltf_xyz_m": [round(h - l, 4) for l, h in zip(lo, hi)],
        "bounds_gltf_m": {"min": [round(v, 4) for v in lo], "max": [round(v, 4) for v in hi]},
        "tris": tris,
        "tris_total": sum(tris.values()),
        "pivots_gltf_m": {n: [round(v, 4) for v in nodes[n].get("translation", [0, 0, 0])]
                          for n in sorted(nodes)},
        "materials": materials_per_node(g),
    }


def check(path):
    g = load(path)
    err = []
    length, width = core_footprint()
    lo, hi = scene_bounds(g)
    if abs((hi[2] - lo[2]) - length) > TOL:
        err.append("length %.4f != Core %.4f" % (hi[2] - lo[2], length))
    if abs((hi[0] - lo[0]) - width) > TOL:
        err.append("width %.4f != Core %.4f" % (hi[0] - lo[0], width))
    if abs(lo[1]) > TOL or abs(lo[0] + hi[0]) > TOL or abs(lo[2] + hi[2]) > TOL:
        err.append("pivot is not on the ground at the footprint centre: %s %s" % (lo, hi))
    road = core_road_width()
    if road / 4 + (hi[0] - lo[0]) / 2 > road / 2 + 1e-6:
        err.append("does not fit inside its lane")
    cap = core_max_tris_per_mesh()
    tris = tris_per_node(g)
    err += ["%s: %d tris > per-mesh cap %d" % (n, t, cap) for n, t in tris.items() if t > cap]
    if sum(tris.values()) > D.BUDGET_TRIS:
        err.append("%d tris > budget %d" % (sum(tris.values()), D.BUDGET_TRIS))
    nodes = nodes_by_name(g)
    want = {"delivery_truck", "door_rear"} | set(D.WHEEL_PIVOTS)
    if set(nodes) != want:
        err.append("nodes %s != %s" % (sorted(nodes), sorted(want)))
    else:
        for w, p in D.WHEEL_PIVOTS.items():
            if not close(nodes[w].get("translation", [0, 0, 0]), to_gltf(p)):
                err.append("%s pivot is not on its axle" % w)
        if not close(nodes["door_rear"].get("translation", [0, 0, 0]), to_gltf(D.DOOR_HINGE)):
            err.append("door pivot is not on its hinge")
        if any(abs(x) > width / 2 + TOL for x in door_open_x(g, D.DOOR_OPEN_DEG)):
            err.append("the open door reaches past the truck's width")
        used = materials_per_node(g)
        tinted = sorted(n for n, ms in used.items() if D.MATERIAL_PREFIX + "body" in ms)
        if tinted != ["delivery_truck", "door_rear"]:
            err.append("tint material on %s" % tinted)
    return err


if __name__ == "__main__":
    p = sys.argv[1] if len(sys.argv) > 1 else os.path.join(HERE, "exports", "delivery_truck.glb")
    e = check(p)
    print("\n".join(e) if e else "ALL OK")
    sys.exit(1 if e else 0)
