"""Export every fence piece of every style to .gltf + .bin in exports/, all
pointing at this set's one shared textures/ folder by relative URI (same
layout as ../street-props and ../trees). Reads each .gltf back and checks:
image URIs relative and inside textures/; every opaque material has a colour
and a normal texture and is single-sided; the wire mesh is alphaMode MASK and
double-sided. Writes exports/fences_stats.json (triangles, sizes, materials)
and exits non-zero if a check fails. outroot defaults to this folder.
Usage: python3 export_fences.py [outroot]"""
import json
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import dh_fences as F  # noqa: E402  (puts ../street-props, ../trees and the house kit on the path)
import bpy  # noqa: E402
from mathutils import Vector  # noqa: E402
import dh_house_kit as K  # noqa: E402

ROOT = sys.argv[1] if len(sys.argv) > 1 else HERE
EXP = os.path.join(ROOT, "exports")
TEX = os.path.realpath(os.path.join(ROOT, "textures"))
os.makedirs(EXP, exist_ok=True)


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


def mat_ok(name, v):
    if name == "DH_Fence_wire":
        return v["alphaMode"] == "MASK" and v["doubleSided"] and v["baseColorTexture"] and v["normalTexture"]
    return v["alphaMode"] == "OPAQUE" and not v["doubleSided"] and v["baseColorTexture"] and v["normalTexture"]


stats = {}
for style in F.STYLES:
    for kind in F.PIECE_KINDS:
        K.reset_scene()
        ob = F.build_piece(style, kind)
        bpy.context.view_layer.update()
        pts = [ob.matrix_world @ Vector(c) for c in ob.bound_box]
        size = [round(max(p[i] for p in pts) - min(p[i] for p in pts), 3) for i in range(3)]
        key = F.piece_name(style, kind)
        path = os.path.join(EXP, key + ".gltf")
        export_gltf(path, ob)
        j = json.load(open(path))
        mats = {}
        for m in j["materials"]:
            pbr = m.get("pbrMetallicRoughness", {})
            mats[m["name"]] = {"alphaMode": m.get("alphaMode", "OPAQUE"), "doubleSided": m.get("doubleSided", False),
                               "baseColorTexture": "baseColorTexture" in pbr, "normalTexture": "normalTexture" in m,
                               "baseColorFactor": [round(v, 3) for v in pbr.get("baseColorFactor", [1, 1, 1, 1])]}
        stats[key] = {"style": style, "kind": kind, "tris": K.tri_count(ob), "size_xyz_m": size,
                      "materials": mats, "images": sorted(im.get("uri") for im in j.get("images", [])),
                      "uris_ok": uris_ok(path, j), "materials_ok": all(mat_ok(n, v) for n, v in mats.items()),
                      "gltf_bytes": os.path.getsize(path),
                      "bin_bytes": os.path.getsize(os.path.join(EXP, key + ".bin"))}
        s = stats[key]
        print("%-28s %4d tris  %s  uris %s mats %s" % (key, s["tris"], size, s["uris_ok"], s["materials_ok"]))
with open(os.path.join(EXP, "fences_stats.json"), "w") as f:
    json.dump(stats, f, indent=1)
bad = [k for k, s in stats.items() if not (s["uris_ok"] and s["materials_ok"])]
print("ALL OK" if not bad else "FAILED: %s" % bad)
if bad:
    sys.exit(1)
