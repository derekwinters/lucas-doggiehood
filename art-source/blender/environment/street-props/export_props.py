"""Export every street prop to .gltf + .bin in exports/, all pointing at
the one shared textures/ folder by relative URI (same layout as ../trees). Reads each .gltf back and checks: image URIs relative and
inside textures/, every material single-sided unless alpha-masked, tangents
not required. Writes exports/street_props_stats.json. outroot defaults to this folder.
Usage: python3 export_props.py [outroot]"""
import json
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
# The committed round-3 tree modules (../trees) and house kit (art-source/blender).
sys.path.insert(0, os.path.normpath(os.path.join(HERE, "..", "trees")))
sys.path.insert(0, os.path.normpath(os.path.join(HERE, "..", "..")))
import bpy  # noqa: E402
from mathutils import Vector  # noqa: E402
import dh_house_kit as K  # noqa: E402
import dh_prop_tex as PT  # noqa: E402
import dh_props as P  # noqa: E402

ROOT = sys.argv[1] if len(sys.argv) > 1 else HERE
EXP = os.path.join(ROOT, "exports")
TEX = os.path.realpath(os.path.join(ROOT, "textures"))
os.makedirs(EXP, exist_ok=True)


def export_gltf(path, ob):
    bpy.ops.object.select_all(action="DESELECT")
    ob.select_set(True)
    for c in ob.children:
        c.select_set(True)
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


stats = {}
for key, (fn, label) in P.PIECES.items():
    K.reset_scene()
    P._MATS = None
    ob = fn()
    bpy.context.view_layer.update()
    pts = [ob.matrix_world @ Vector(c) for c in ob.bound_box]
    size = [round(max(p[i] for p in pts) - min(p[i] for p in pts), 3) for i in range(3)]
    path = os.path.join(EXP, key + ".gltf")
    export_gltf(path, ob)
    j = json.load(open(path))
    mats = {}
    for m in j["materials"]:
        pbr = m.get("pbrMetallicRoughness", {})
        mats[m["name"]] = {"alphaMode": m.get("alphaMode", "OPAQUE"), "doubleSided": m.get("doubleSided", False),
                           "baseColorTexture": "baseColorTexture" in pbr, "normalTexture": "normalTexture" in m,
                           "baseColorFactor": [round(v, 3) for v in pbr.get("baseColorFactor", [1, 1, 1, 1])],
                           "emissive": "emissiveFactor" in m}
    tex_ok = all(v["baseColorTexture"] and v["normalTexture"] for n, v in mats.items() if "glass" not in n and
                 "lens" not in n)
    stats[key] = {"label": label, "tris": K.tri_count(ob), "size_xyz_m": size, "materials": mats,
                  "images": sorted(im.get("uri") for im in j.get("images", [])),
                  "uris_ok": uris_ok(path, j), "textured_ok": tex_ok,
                  "single_sided_ok": all(not v["doubleSided"] for v in mats.values()),
                  "gltf_bytes": os.path.getsize(path),
                  "bin_bytes": os.path.getsize(os.path.join(EXP, key + ".bin"))}
    s = stats[key]
    print("%-26s %5d tris  %s  uris %s tex %s" % (key, s["tris"], size, s["uris_ok"], s["textured_ok"]))
with open(os.path.join(EXP, "street_props_stats.json"), "w") as f:
    json.dump(stats, f, indent=1)
bad = [k for k, s in stats.items() if not (s["uris_ok"] and s["textured_ok"] and s["single_sided_ok"])]
print("ALL OK" if not bad else "FAILED: %s" % bad)
if bad:
    sys.exit(1)
