"""Write the blanket's textures and export one .glb per colourway (#413), then read each back
and check it.

  python3 art-source/blender/items/blanket/export_blanket.py [outroot]

Writes <outroot>/textures/blanket_*.png, <outroot>/exports/blanket_<colourway>.glb for every
colourway in blanket_spec.COLOURWAYS, and <outroot>/exports/blanket_stats.json. outroot defaults
to this folder. Two runs write byte-identical files.

One self-contained .glb per colourway (textures embedded), the way the dogs export one file per
coat (dog_<breed>_<coat>): the same mesh in every file, only the plaid material and its texture
differ (DH_Item_blanket_plaid_<Colourway>, like DH_Dog_Fur_<Coat>). The PNGs in textures/ are
kept so a rebuild can be compared byte for byte.

Read-back checks on every file (exit non-zero if any fails):
  - every vertex lies inside the blanket_spec footprint (FOOTPRINT_W x FOOTPRINT_D), centred on
    the pivot, on or above the ground, and the lowest vertex touches it
  - nothing is taller than FLAT_MAX_H (it lies flat)
  - triangles <= the provisional per-mesh backstop (#799)
  - every image is embedded (no external URI) and the .glb has a single embedded buffer
  - the plaid and back materials are single-sided and opaque; the fringe is double-sided
  - every colourway has the same triangle count and size
"""
import json
import os
import struct
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import blanket_spec as S  # noqa: E402
import dh_blanket_tex as BT  # noqa: E402
import bpy  # noqa: E402
import dh_blanket as B  # noqa: E402

FLAT_MAX_H = 0.10           # lies flat: nothing taller than this
GROUND_TOL = 0.001
CENTRE_TOL = 0.02
FOOTPRINT_TOL = 1e-4        # float32 positions in the .glb
TRI_BACKSTOP = 6000         # provisional per-mesh backstop (#799)


def export(root, tex, colourway):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    ob = B.build(tex, colourway)
    exp = os.path.join(root, "exports")
    os.makedirs(exp, exist_ok=True)
    path = os.path.join(exp, "%s_%s.glb" % (B.NAME, colourway))
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


def read_back(path, colourway):
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
    names = B.material_names(colourway)
    checks = {
        "footprint": max(abs(v) for v in bx) <= S.FOOTPRINT_W / 2 + FOOTPRINT_TOL
        and max(abs(v) for v in by) <= S.FOOTPRINT_D / 2 + FOOTPRINT_TOL,
        "centred": abs(sum(bx) / 2) < CENTRE_TOL and abs(sum(by) / 2) < CENTRE_TOL,
        "on_ground": -GROUND_TOL <= bz[0] <= GROUND_TOL,
        "flat": bz[1] <= FLAT_MAX_H,
        "tri_budget": tris <= TRI_BACKSTOP,
        "embedded": embedded,
        "materials": mats.get(names["plaid"], {}).get("doubleSided") is False
        and mats.get(names["back"], {}).get("doubleSided") is False
        and mats.get(names["fringe"], {}).get("doubleSided") is True
        and all(v["alphaMode"] == "OPAQUE" for v in mats.values()),
    }
    return {
        "file": os.path.basename(path),
        "tris": tris,
        "size_xyz_m": [round(bx[1] - bx[0], 4), round(by[1] - by[0], 4), round(bz[1] - bz[0], 4)],
        "bounds_min_m": [round(bx[0], 4), round(by[0], 4), round(bz[0], 4)],
        "bounds_max_m": [round(bx[1], 4), round(by[1], 4), round(bz[1], 4)],
        "materials": mats,
        "images": images,
        "glb_bytes": os.path.getsize(path),
        "checks": checks,
    }


def main():
    root = os.path.abspath(sys.argv[1]) if len(sys.argv) > 1 else HERE
    tex = BT.make_all(root)
    per = {}
    for key, label in S.COLOURWAYS:
        path, tris_blend = export(root, tex, key)
        per[key] = dict(read_back(path, key), label=label)
        print("%s: %d tris (Blender %d), size %s m" % (per[key]["file"], per[key]["tris"], tris_blend,
                                                       per[key]["size_xyz_m"]))
    same = len({(v["tris"], tuple(v["size_xyz_m"])) for v in per.values()}) == 1
    stats = {
        "name": B.NAME,
        "units": "metres, Blender axes (front -Y, up +Z); the .glb files are Y-up (front +Z)",
        "cloth_m": [S.CLOTH_W, S.CLOTH_D],
        "footprint_m": [S.FOOTPRINT_W, S.FOOTPRINT_D],
        "tri_backstop": TRI_BACKSTOP,
        "default": S.DEFAULT,
        "colourways": per,
        "same_mesh_every_colourway": same,
    }
    with open(os.path.join(root, "exports", B.NAME + "_stats.json"), "w") as fh:
        json.dump(stats, fh, indent=1, sort_keys=True)
        fh.write("\n")
    bad = ["%s:%s" % (k, c) for k, v in per.items() for c, ok in v["checks"].items() if not ok]
    if not same:
        bad.append("same_mesh_every_colourway")
    for c in per[S.DEFAULT]["checks"]:
        print("  %-10s %s" % (c, "ok" if all(v["checks"][c] for v in per.values()) else "FAILED"))
    print("  %-10s %s" % ("same_mesh", "ok" if same else "FAILED"))
    print("ALL OK" if not bad else "FAILED: %s" % ", ".join(bad))
    if bad:
        sys.exit(1)


if __name__ == "__main__":
    main()
