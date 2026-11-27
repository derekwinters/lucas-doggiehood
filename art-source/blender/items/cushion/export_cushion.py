"""Export the cushion (#412) to .gltf + .bin, read it back and check it.

  python3 art-source/blender/items/cushion/export_cushion.py [outroot]   (outroot defaults to this folder)

Writes <outroot>/exports/:
  item_cushion.gltf + item_cushion.bin   the cushion as one node and one mesh (fabric, piping,
                                         buttons), flat material colours, no images
  cushion_stats.json                     triangles per material, size, rest-top height, the checks

Checks on the file read back (exit non-zero on failure): exactly one node and one mesh, nothing
else (no camera, light, skin, animation, image or extension); the footprint fits today's
1.4 x 1.0 m slab and is centred on the pivot; the bottom sits on the ground; triangles within the
provisional per-mesh backstop (#799) and equal to Blender's count; every material single-sided.
Prints ALL OK when everything passes. Two runs give byte-identical files.
"""
import json
import os
import struct
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import dh_cushion as C  # noqa: E402
import bpy  # noqa: E402

K = C.K
ROOT = os.path.abspath(sys.argv[1]) if len(sys.argv) > 1 else HERE
EXP = os.path.join(ROOT, "exports")
GLTF = os.path.join(EXP, C.NAME + ".gltf")
BIN = os.path.join(EXP, C.NAME + ".bin")
STATS = os.path.join(EXP, "cushion_stats.json")
MAX_TRIS = 6000                    # provisional per-mesh backstop (#799)
FOOTPRINT_X, FOOTPRINT_Z = 1.4, 1.0  # today's DecorationView slab (glTF / Unity X and Z)
CENTRE_TOL = 0.005
GROUND_TOL = 0.001
ROUND = 4                          # decimals in the stats

COMP = {5120: ("b", 1), 5121: ("B", 1), 5122: ("h", 2), 5123: ("H", 2), 5125: ("I", 4), 5126: ("f", 4)}
NCOMP = {"SCALAR": 1, "VEC2": 2, "VEC3": 3, "VEC4": 4, "MAT4": 16}


def accessor(j, binb, i):
    a = j["accessors"][i]
    v = j["bufferViews"][a["bufferView"]]
    fmt, size = COMP[a["componentType"]]
    n = NCOMP[a["type"]]
    stride = v.get("byteStride", size * n)
    off = v.get("byteOffset", 0) + a.get("byteOffset", 0)
    return [struct.unpack_from("<%d%s" % (n, fmt), binb, off + k * stride) for k in range(a["count"])]


def export(ob):
    bpy.ops.object.select_all(action="DESELECT")
    ob.select_set(True)
    bpy.ops.export_scene.gltf(
        filepath=GLTF, export_format="GLTF_SEPARATE", use_selection=True, export_apply=True, export_yup=True,
        export_animations=False, export_materials="EXPORT", export_cameras=False, export_lights=False,
        export_extras=False)


def check(j, binb):
    errs = []
    names = [n.get("name") for n in j["nodes"]]
    if names != [C.NAME]:
        errs.append("nodes %s" % names)
    if len(j.get("meshes", [])) != 1:
        errs.append("%d meshes" % len(j.get("meshes", [])))
    for key in ("cameras", "skins", "animations", "images", "textures", "samplers",
                "extensionsUsed", "extensionsRequired"):
        if key in j:
            errs.append("unexpected %s" % key)
    for n in j["nodes"]:
        for key in ("translation", "rotation", "scale", "matrix", "extensions"):
            if key in n:
                errs.append("node %s has %s" % (n.get("name"), key))
    if any(m.get("doubleSided") for m in j.get("materials", [])):
        errs.append("double-sided material")
    pts, tris = [], {}
    for p in j["meshes"][0]["primitives"]:
        pts += accessor(j, binb, p["attributes"]["POSITION"])
        mat = j["materials"][p["material"]]["name"].replace("DH_Item_cushion_", "")
        tris[mat] = tris.get(mat, 0) + j["accessors"][p["indices"]]["count"] // 3
    lo = [min(q[i] for q in pts) for i in range(3)]
    hi = [max(q[i] for q in pts) for i in range(3)]
    size = [hi[i] - lo[i] for i in range(3)]
    if size[0] > FOOTPRINT_X or size[2] > FOOTPRINT_Z:
        errs.append("footprint %.3f x %.3f m over the %.1f x %.1f slab" % (size[0], size[2], FOOTPRINT_X, FOOTPRINT_Z))
    if abs(hi[0] + lo[0]) / 2 > CENTRE_TOL or abs(hi[2] + lo[2]) / 2 > CENTRE_TOL:
        errs.append("footprint not centred on the pivot")
    if abs(lo[1]) > GROUND_TOL:
        errs.append("lowest point %.4f m, not on the ground" % lo[1])
    return errs, tris, size


def main():
    os.makedirs(EXP, exist_ok=True)
    K.reset_scene()
    ob = C.build()
    bpy.context.view_layer.update()
    blender_tris = K.tri_count(ob)
    export(ob)
    j = json.load(open(GLTF))
    binb = open(BIN, "rb").read()
    errs, tris, size = check(j, binb)
    tris["total"] = sum(tris.values())
    if tris["total"] != blender_tris:
        errs.append("glTF has %d triangles, Blender %d" % (tris["total"], blender_tris))
    if tris["total"] > MAX_TRIS:
        errs.append("%d triangles over the %d backstop" % (tris["total"], MAX_TRIS))
    j.setdefault("asset", {})["extras"] = {
        "dh_note": "Doggiehood cushion (#412), design build pending approval; pivot at the footprint centre on "
                   "the ground; long side along X; a dog rests on the top at about %.2f m" % C.rest_top()}
    with open(GLTF, "w") as fh:
        json.dump(j, fh, indent=1)
    stats = {"name": C.NAME, "tris": tris, "tri_backstop": MAX_TRIS,
             "size_xyz_m": [round(size[0], ROUND), round(size[2], ROUND), round(size[1], ROUND)],
             "size_note": "length (X) x depth (Blender Y / glTF Z) x height",
             "slab_footprint_m": [FOOTPRINT_X, FOOTPRINT_Z],
             "rest_top_m": round(C.rest_top(), ROUND),
             "seam_height_m": C.SEAM_Z,
             "units": "metres; Blender axes front -Y up +Z (glTF: front +Z, up +Y)",
             "pivot": "centre of the footprint, on the ground",
             "materials": sorted(m["name"] for m in j["materials"]),
             "gltf_bytes": os.path.getsize(GLTF), "bin_bytes": os.path.getsize(BIN),
             "checks_ok": not errs}
    with open(STATS, "w") as fh:
        json.dump(stats, fh, indent=1)
    print("%s: %d triangles %s, size %s m, rest top %.3f m"
          % (C.NAME, tris["total"], tris, stats["size_xyz_m"], C.rest_top()))
    print("ALL OK" if not errs else "FAILED: %s" % errs)
    sys.exit(0 if not errs else 1)


if __name__ == "__main__":
    main()
