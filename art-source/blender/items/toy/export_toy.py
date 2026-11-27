"""Export every toy design (#332) to .gltf + .bin in exports/, pointing at this
set's textures/ by relative URI (the same layout as the street props). Writes
the fleece textures first, so one command rebuilds every committed file.

Reads each .gltf back and checks: one node and one mesh named toy_<design>,
every material named DH_Toy_*, every image URI relative and inside textures/,
every plush material has a colour and a normal texture, nothing double-sided,
the triangle count within dh_toy.TRI_BUDGET, the pivot on the ground at the
footprint centre, and every face pointing outwards (positive enclosed volume).
Also records whether a ray straight down through the footprint centre hits
the toy (`covers_centre`: the flying disc must be solid, not a ring). Writes
exports/toy_stats.json and exits non-zero if a check fails. outroot defaults
to this folder.

Usage: python3 export_toy.py [outroot] [--designs disc,chew_cone,...]
"""
import json
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import bpy  # noqa: E402
from mathutils import Vector  # noqa: E402
from mathutils.bvhtree import BVHTree  # noqa: E402
import dh_toy as T  # noqa: E402
import dh_toy_tex as TX  # noqa: E402

FLAGS = {"--designs"}
args = [a for i, a in enumerate(sys.argv[1:], 1) if a not in FLAGS and sys.argv[i - 1] not in FLAGS]
ROOT = os.path.abspath(args[0]) if args else HERE
DESIGNS = list(T.DESIGNS)
for i, a in enumerate(sys.argv):
    if a == "--designs":
        DESIGNS = sys.argv[i + 1].split(",")
EXP = os.path.join(ROOT, "exports")
TEX = os.path.realpath(os.path.join(ROOT, "textures"))
PIVOT_TOL = 0.002   # metres
DECIMALS = 4        # stats rounding
RAY_FROM_Z = 10.0   # metres above the ground for the centre ray


def export_gltf(path, ob):
    bpy.ops.object.select_all(action="DESELECT")
    ob.select_set(True)
    bpy.ops.export_scene.gltf(filepath=path, export_format="GLTF_SEPARATE", use_selection=True,
                              export_apply=True, export_yup=True, export_keep_originals=True)


def uris_ok(path, j):
    for im in j.get("images", []):
        uri = im.get("uri")
        if uri is None or os.path.isabs(uri) or ":" in uri:
            return False
        f = os.path.realpath(os.path.join(os.path.dirname(path), uri))
        if os.path.dirname(f) != TEX or not os.path.isfile(f):
            return False
    return True


def signed_volume(ob):
    me = ob.data
    vol = 0.0
    for p in me.polygons:
        a, b, c = (me.vertices[i].co for i in p.vertices[:3])
        vol += a.dot(b.cross(c)) / 6.0
    return vol


def covers_centre(ob):
    me = ob.data
    tree = BVHTree.FromPolygons([v.co.copy() for v in me.vertices], [tuple(p.vertices) for p in me.polygons])
    hit = tree.ray_cast(Vector((0.0, 0.0, RAY_FROM_Z)), Vector((0.0, 0.0, -1.0)))
    return hit[0] is not None


def export_design(design, tex):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    ob = T.build(tex, design)
    name = ob.name
    bpy.context.view_layer.update()
    pts = [ob.matrix_world @ Vector(c) for c in ob.bound_box]
    lo = [round(min(p[i] for p in pts), DECIMALS) for i in range(3)]
    hi = [round(max(p[i] for p in pts), DECIMALS) for i in range(3)]
    path = os.path.join(EXP, name + ".gltf")
    export_gltf(path, ob)
    j = json.load(open(path))
    mats = {}
    for m in j["materials"]:
        pbr = m.get("pbrMetallicRoughness", {})
        mats[m["name"]] = {"doubleSided": m.get("doubleSided", False),
                           "baseColorTexture": "baseColorTexture" in pbr, "normalTexture": "normalTexture" in m,
                           "baseColorFactor": [round(v, DECIMALS) for v in pbr.get("baseColorFactor", [1, 1, 1, 1])]}
    plush = {T.material_name(k) for k, (_, kind) in T.DESIGNS[design]["slots"].items() if kind == "plush"}
    tris = T.tri_count(ob)
    checks = {
        "one_node_and_mesh": [n["name"] for n in j["nodes"]] == [name] and len(j["meshes"]) == 1,
        "material_names": all(n.startswith("DH_Toy_") for n in mats),
        "uris": uris_ok(path, j),
        "plush_textured": all(mats[n]["baseColorTexture"] and mats[n]["normalTexture"] for n in plush),
        "single_sided": not any(v["doubleSided"] for v in mats.values()),
        "triangle_budget": 0 < tris <= T.TRI_BUDGET,
        "pivot": abs(lo[2]) <= PIVOT_TOL and abs(lo[0] + hi[0]) <= PIVOT_TOL and abs(lo[1] + hi[1]) <= PIVOT_TOL,
        "outward_faces": signed_volume(ob) > 0,
    }
    return name, {"design": design, "label": T.DESIGNS[design]["label"], "tris": tris, "tri_budget": T.TRI_BUDGET,
                  "size_xyz_m": [round(hi[i] - lo[i], DECIMALS) for i in range(3)],
                  "bounds_min_m": lo, "bounds_max_m": hi, "covers_centre": covers_centre(ob), "materials": mats,
                  "images": sorted(im.get("uri") for im in j.get("images", [])),
                  "checks": checks, "gltf_bytes": os.path.getsize(path),
                  "bin_bytes": os.path.getsize(os.path.join(EXP, name + ".bin"))}


def main():
    os.makedirs(EXP, exist_ok=True)
    tex = TX.write(TEX)
    stats = {}
    for d in DESIGNS:
        name, s = export_design(d, tex)
        stats[name] = s
        bad = [k for k, ok in s["checks"].items() if not ok]
        print("%-20s %5d tris  size %s m  %s" % (name, s["tris"], s["size_xyz_m"], "ok" if not bad else bad))
    with open(os.path.join(EXP, "toy_stats.json"), "w") as f:
        json.dump(stats, f, indent=1)
        f.write("\n")
    bad = [n for n, s in stats.items() if not all(s["checks"].values())]
    print("ALL OK" if not bad else "FAILED: %s" % bad)
    return 1 if bad else 0


if __name__ == "__main__":
    sys.exit(main())
