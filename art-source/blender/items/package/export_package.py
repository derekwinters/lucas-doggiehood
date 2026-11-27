"""Export the delivery package (#410): textures, one self-contained .glb, and its stats.

  python3 art-source/blender/items/package/export_package.py [outroot]

outroot defaults to this folder. Writes
  <outroot>/textures/package_*.png        (dh_package_tex.py)
  <outroot>/exports/package.glb           Y-up, transforms applied, textures embedded, so the same
                                          file loads in Unity (#776) and in a three.js web viewer
  <outroot>/exports/package_stats.json    size, triangles, materials, the checks below

then reads the .glb back and exits non-zero unless every check passes: one mesh on one node named
`package`, DH_Package_* materials, nothing double-sided, every image embedded, no animations or
skins, the file's size equal to DIMS, on the ground, within the art budget (#799), near the graybox
size, and inside the truck's stated footprint (see package_checks.py for why that is all the truck
check can be until #924 states its cargo bay). Prints ALL OK on success.
"""
import json
import os
import struct
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
ROOT = os.path.abspath(sys.argv[1]) if len(sys.argv) > 1 else HERE
os.environ["DH_PACKAGE_TEXTURES"] = os.path.join(ROOT, "textures")

import bpy  # noqa: E402
from mathutils import Vector  # noqa: E402
import dh_package as P  # noqa: E402
import dh_package_tex as PT  # noqa: E402
import package_checks as C  # noqa: E402

MM = 0.001
ROUND = 4


def read_glb_json(path):
    with open(path, "rb") as fh:
        data = fh.read()
    n, kind = struct.unpack_from("<II", data, 12)
    assert data[:4] == b"glTF" and kind == 0x4E4F534A
    return json.loads(data[20:20 + n].decode("utf-8"))


def export_glb(path, ob):
    bpy.ops.object.select_all(action="DESELECT")
    ob.select_set(True)
    bpy.ops.export_scene.gltf(filepath=path, export_format="GLB", use_selection=True,
                              export_apply=True, export_yup=True, export_animations=False)


def main():
    exp = os.path.join(ROOT, "exports")
    os.makedirs(exp, exist_ok=True)
    PT.write_all(os.environ["DH_PACKAGE_TEXTURES"])
    P.K.reset_scene()
    ob = P.build()
    bpy.context.view_layer.update()
    pts = [ob.matrix_world @ Vector(c) for c in ob.bound_box]
    size = [round(max(p[i] for p in pts) - min(p[i] for p in pts), ROUND) for i in range(3)]
    tris = P.K.tri_count(ob)
    path = os.path.join(exp, P.NAME + ".glb")
    export_glb(path, ob)

    j = read_glb_json(path)
    acc = [j["accessors"][p["attributes"]["POSITION"]] for p in j["meshes"][0]["primitives"]]
    lo = [min(a["min"][i] for a in acc) for i in range(3)]
    hi = [max(a["max"][i] for a in acc) for i in range(3)]
    file_size = [hi[0] - lo[0], hi[2] - lo[2], hi[1] - lo[1]]          # glTF Y-up -> Blender axes
    dims = [P.DIMS["length"], P.DIMS["depth"], P.DIMS["height"]]
    checks = {
        "one_mesh_named_package": len(j["meshes"]) == 1
        and [n.get("name") for n in j["nodes"] if "mesh" in n] == [P.NAME],
        "materials_prefixed": all(m["name"].startswith("DH_Package_") for m in j["materials"]),
        "single_sided": not any(m.get("doubleSided", False) for m in j["materials"]),
        "images_embedded": bool(j.get("images")) and all("uri" not in im and "bufferView" in im
                                                          for im in j["images"]),
        "no_animations_or_skins": "animations" not in j and "skins" not in j,
        "size_matches_dims": all(abs(a - b) <= MM for a, b in zip(file_size, dims)),
        "on_the_ground": abs(lo[1]) <= MM,
        "within_budget": C.within_budget(tris),
        "near_graybox": C.near_graybox(size),
        "truck_footprint_fit": C.fits_in_cargo(size, C.TRUCK_FOOTPRINT + (None,)),
    }
    stats = {
        "name": "Delivery package (#410)",
        "units": "metres, Blender axes (front -Y, up +Z); the .glb is Y-up (front +Z)",
        "pivot": "on the ground at the footprint centre",
        "size_xyz_m": size,
        "tris": tris,
        "budget_tris": C.ART_BUDGET_TRIS,
        "graybox_m": C.GRAYBOX_SIZE,
        "materials": [m["name"] for m in j["materials"]],
        "images": sorted(im.get("name", "") for im in j["images"]),
        "truck_footprint_m": [round(v, ROUND) for v in C.TRUCK_FOOTPRINT],
        "truck_footprint_fit": checks["truck_footprint_fit"],
        "truck_cargo_bay": "not stated yet (#924); check with package_checks.py --cargo L W H",
        "glb_bytes": os.path.getsize(path),
        "checks": checks,
        "all_checks_ok": all(checks.values()),
    }
    with open(os.path.join(exp, "package_stats.json"), "w") as fh:
        json.dump(stats, fh, indent=1, sort_keys=False)
        fh.write("\n")
    print("%s  %d tris  size %s m  %d bytes" % (P.NAME, tris, size, stats["glb_bytes"]))
    bad = [k for k, v in checks.items() if not v]
    print("ALL OK" if not bad else "FAILED: %s" % ", ".join(bad))
    return 0 if not bad else 1


if __name__ == "__main__":
    sys.exit(main())
