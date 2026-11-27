"""Write the blanket's textures and export it (#413), then read the export back and check it.

  python3 art-source/blender/items/blanket/export_blanket.py [outroot]

Writes <outroot>/textures/blanket_*.png, <outroot>/exports/blanket.glb (self-contained, textures
embedded, like the houses and the package: one small model with its own textures, and the same
file loads in a web viewer) and <outroot>/exports/blanket_stats.json. outroot defaults to this
folder. The PNGs in textures/ are kept so a rebuild can be compared byte for byte. Two runs write
byte-identical files.

Read-back checks (exit non-zero if any fails):
  - every vertex lies inside the 1.4 x 1.0 m graybox slab footprint (DecorationView), centred
    on the pivot, on or above the ground, and the lowest vertex touches it
  - nothing is taller than FLAT_MAX_H (it lies flat)
  - triangles <= the provisional per-mesh backstop (#799)
  - every image is embedded (no external URI) and the .glb has a single embedded buffer
  - the plaid and back materials are single-sided and opaque; the fringe is double-sided
"""
import json
import os
import struct
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import dh_blanket_tex as BT  # noqa: E402
import bpy  # noqa: E402
import dh_blanket as B  # noqa: E402

SLAB_W = 1.4                # DecorationView's graybox slab footprint, across
SLAB_D = 1.0                # ... and deep
FLAT_MAX_H = 0.10           # lies flat: nothing taller than this
GROUND_TOL = 0.001
CENTRE_TOL = 0.02
TRI_BACKSTOP = 6000         # provisional per-mesh backstop (#799)
SINGLE_SIDED = ("DH_Item_blanket_plaid", "DH_Item_blanket_back")
DOUBLE_SIDED = ("DH_Item_blanket_fringe",)


def export(root):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    tex = BT.make_all(root)
    ob = B.build(tex)
    exp = os.path.join(root, "exports")
    os.makedirs(exp, exist_ok=True)
    path = os.path.join(exp, B.NAME + ".glb")
    bpy.ops.object.select_all(action="DESELECT")
    ob.select_set(True)
    bpy.ops.export_scene.gltf(filepath=path, export_format="GLB", use_selection=True,
                              export_apply=True, export_yup=True, export_animations=False)
    return path, B.tri_count(ob)


def read_glb(path):
    with open(path, "rb") as fh:
        data = fh.read()
    if data[:4] != b"glTF":
        raise ValueError("%s is not a binary glTF" % path)
    ln = struct.unpack_from("<I", data, 12)[0]
    return json.loads(data[20:20 + ln].decode())


def read_back(root, path):
    j = read_glb(path)
    lo, hi = [1e9] * 3, [-1e9] * 3
    tris = 0
    for mesh in j["meshes"]:
        for p in mesh["primitives"]:
            a = j["accessors"][p["attributes"]["POSITION"]]
            lo = [min(lo[i], a["min"][i]) for i in range(3)]
            hi = [max(hi[i], a["max"][i]) for i in range(3)]
            tris += j["accessors"][p["indices"]]["count"] // 3
    # glTF is Y-up with front +Z: Blender x = gx, y = -gz, z = gy
    bx, by, bz = (lo[0], hi[0]), (-hi[2], -lo[2]), (lo[1], hi[1])
    images = sorted(im.get("name", "") for im in j.get("images", []))
    embedded = bool(images) and all("uri" not in im and "bufferView" in im for im in j["images"]) \
        and len(j["buffers"]) == 1 and "uri" not in j["buffers"][0]
    mats = {m["name"]: {"doubleSided": m.get("doubleSided", False), "alphaMode": m.get("alphaMode", "OPAQUE")}
            for m in j["materials"]}
    checks = {
        "footprint": bx[1] - bx[0] <= SLAB_W and by[1] - by[0] <= SLAB_D
        and max(abs(v) for v in bx) <= SLAB_W / 2 and max(abs(v) for v in by) <= SLAB_D / 2,
        "centred": abs(sum(bx) / 2) < CENTRE_TOL and abs(sum(by) / 2) < CENTRE_TOL,
        "on_ground": -GROUND_TOL <= bz[0] <= GROUND_TOL,
        "flat": bz[1] <= FLAT_MAX_H,
        "tri_budget": tris <= TRI_BACKSTOP,
        "embedded": embedded,
        "materials": all(mats.get(n, {}).get("doubleSided") is False for n in SINGLE_SIDED)
        and all(mats.get(n, {}).get("doubleSided") is True for n in DOUBLE_SIDED)
        and all(v["alphaMode"] == "OPAQUE" for v in mats.values()),
    }
    stats = {
        "name": B.NAME,
        "units": "metres, Blender axes (front -Y, up +Z); the glTF file is Y-up (front +Z)",
        "tris": tris,
        "size_xyz_m": [round(bx[1] - bx[0], 4), round(by[1] - by[0], 4), round(bz[1] - bz[0], 4)],
        "bounds_min_m": [round(bx[0], 4), round(by[0], 4), round(bz[0], 4)],
        "bounds_max_m": [round(bx[1], 4), round(by[1], 4), round(bz[1], 4)],
        "slab_footprint_m": [SLAB_W, SLAB_D],
        "tri_backstop": TRI_BACKSTOP,
        "materials": mats,
        "images": images,
        "glb_bytes": os.path.getsize(path),
        "checks": checks,
    }
    return stats


def main():
    root = os.path.abspath(sys.argv[1]) if len(sys.argv) > 1 else HERE
    path, tris_blend = export(root)
    stats = read_back(root, path)
    with open(os.path.join(root, "exports", B.NAME + "_stats.json"), "w") as fh:
        json.dump(stats, fh, indent=1, sort_keys=True)
        fh.write("\n")
    print("%s: %d tris (Blender %d), size %s m" % (B.NAME, stats["tris"], tris_blend, stats["size_xyz_m"]))
    bad = [k for k, v in stats["checks"].items() if not v]
    for k, v in stats["checks"].items():
        print("  %-10s %s" % (k, "ok" if v else "FAILED"))
    print("ALL OK" if not bad else "FAILED: %s" % ", ".join(bad))
    if bad:
        sys.exit(1)


if __name__ == "__main__":
    main()
