"""Export the dog toy (#332) to .gltf + .bin in exports/, pointing at this
set's textures/ by relative URI (the same layout as the street props). Writes
the fleece textures first, so one command rebuilds every committed file.

Reads the .gltf back and checks: one node and one mesh named toy_duck, every
material named DH_Toy_*, every image URI relative and inside textures/, every
plush material has a colour and a normal texture, nothing double-sided, the
triangle count within dh_toy.TRI_BUDGET and the pivot on the ground at the
footprint centre. Writes exports/toy_stats.json and exits non-zero if a check
fails. outroot defaults to this folder.

Usage: python3 export_toy.py [outroot]
"""
import json
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import bpy  # noqa: E402
from mathutils import Vector  # noqa: E402
import dh_toy as T  # noqa: E402
import dh_toy_tex as TX  # noqa: E402

ROOT = os.path.abspath(sys.argv[1]) if len(sys.argv) > 1 else HERE
EXP = os.path.join(ROOT, "exports")
TEX = os.path.realpath(os.path.join(ROOT, "textures"))
PIVOT_TOL = 0.002   # metres
DECIMALS = 4        # stats rounding


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


def main():
    os.makedirs(EXP, exist_ok=True)
    bpy.ops.wm.read_factory_settings(use_empty=True)
    tex = TX.write(TEX)
    ob = T.build(tex)
    bpy.context.view_layer.update()
    pts = [ob.matrix_world @ Vector(c) for c in ob.bound_box]
    lo = [round(min(p[i] for p in pts), DECIMALS) for i in range(3)]
    hi = [round(max(p[i] for p in pts), DECIMALS) for i in range(3)]
    path = os.path.join(EXP, T.NAME + ".gltf")
    export_gltf(path, ob)
    j = json.load(open(path))
    mats = {}
    for m in j["materials"]:
        pbr = m.get("pbrMetallicRoughness", {})
        mats[m["name"]] = {"doubleSided": m.get("doubleSided", False),
                           "baseColorTexture": "baseColorTexture" in pbr, "normalTexture": "normalTexture" in m,
                           "baseColorFactor": [round(v, DECIMALS) for v in pbr.get("baseColorFactor", [1, 1, 1, 1])]}
    tris = T.tri_count(ob)
    checks = {
        "one_node_and_mesh": [n["name"] for n in j["nodes"]] == [T.NAME] and len(j["meshes"]) == 1,
        "material_names": all(n.startswith("DH_Toy_") for n in mats),
        "uris": uris_ok(path, j),
        "plush_textured": all(v["baseColorTexture"] and v["normalTexture"]
                              for n, v in mats.items() if n.startswith("DH_Toy_plush")),
        "single_sided": not any(v["doubleSided"] for v in mats.values()),
        "triangle_budget": 0 < tris <= T.TRI_BUDGET,
        "pivot": abs(lo[2]) <= PIVOT_TOL and abs(lo[0] + hi[0]) <= PIVOT_TOL and abs(lo[1] + hi[1]) <= PIVOT_TOL,
    }
    stats = {T.NAME: {"tris": tris, "tri_budget": T.TRI_BUDGET,
                      "size_xyz_m": [round(hi[i] - lo[i], DECIMALS) for i in range(3)],
                      "bounds_min_m": lo, "bounds_max_m": hi, "materials": mats,
                      "images": sorted(im.get("uri") for im in j.get("images", [])),
                      "checks": checks, "gltf_bytes": os.path.getsize(path),
                      "bin_bytes": os.path.getsize(os.path.join(EXP, T.NAME + ".bin"))}}
    with open(os.path.join(EXP, "toy_stats.json"), "w") as f:
        json.dump(stats, f, indent=1)
        f.write("\n")
    s = stats[T.NAME]
    print("%s  %d tris (budget %d)  size %s m" % (T.NAME, tris, T.TRI_BUDGET, s["size_xyz_m"]))
    bad = [k for k, ok in checks.items() if not ok]
    print("ALL OK" if not bad else "FAILED: %s" % bad)
    return 1 if bad else 0


if __name__ == "__main__":
    sys.exit(main())
