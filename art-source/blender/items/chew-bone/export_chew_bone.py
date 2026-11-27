"""Export the chew bone (#923) to exports/chew_bone.gltf + .bin, pointing at textures/ by relative
URI (the same layout as the trees and street props), and write exports/chew_bone_stats.json.

  python3 art-source/blender/items/chew-bone/export_chew_bone.py [outroot]

outroot defaults to this folder; the textures are painted into <outroot>/textures first. Reads the
.gltf back and exits non-zero unless: every image URI is relative and resolves inside textures/,
the material has a colour and a normal map, nothing is double-sided, and the triangle count is
within BUDGET.
"""
import json
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import dh_chew_bone as CB  # noqa: E402  (puts the house kit on the path)
import bpy  # noqa: E402
import dh_house_kit as K  # noqa: E402

BUDGET = 6000                  # provisional per-mesh backstop (#799)
ROOT = os.path.abspath(sys.argv[1]) if len(sys.argv) > 1 else HERE
EXP = os.path.join(ROOT, "exports")
TEX = os.path.realpath(os.path.join(ROOT, "textures"))
os.makedirs(EXP, exist_ok=True)


def uris_ok(path, j):
    for im in j.get("images", []):
        uri = im.get("uri")
        if uri is None or os.path.isabs(uri) or ":" in uri:
            return False
        f = os.path.realpath(os.path.join(os.path.dirname(path), uri))
        if os.path.dirname(f) != TEX or not os.path.isfile(f):
            return False
    return True


K.reset_scene()
paths = CB.make_textures(TEX)
ob = CB.build(paths)
bpy.context.view_layer.update()
bpy.ops.object.select_all(action="DESELECT")
ob.select_set(True)
path = os.path.join(EXP, CB.NAME + ".gltf")
bpy.ops.export_scene.gltf(filepath=path, export_format="GLTF_SEPARATE", use_selection=True,
                          export_apply=True, export_yup=True, export_keep_originals=True)
with open(path) as fh:
    j = json.load(fh)
mats = {}
for m in j["materials"]:
    pbr = m.get("pbrMetallicRoughness", {})
    mats[m["name"]] = {"baseColorTexture": "baseColorTexture" in pbr, "normalTexture": "normalTexture" in m,
                       "doubleSided": m.get("doubleSided", False),
                       "roughness": round(pbr.get("roughnessFactor", 1.0), 3)}
stats = {CB.NAME: {
    "label": "Chew bone",
    "units": "metres, Blender axes (long axis X, up +Z); the glTF file is Y-up",
    "tris": K.tri_count(ob), "budget": BUDGET,
    "size_xyz_m": [round(d, 3) for d in ob.dimensions],
    "dims_m": CB.DIMS, "dog_scale": CB.DOG_SCALE,
    "materials": mats,
    "images": sorted(im.get("uri") for im in j.get("images", [])),
    "uris_ok": uris_ok(path, j),
    "textured_ok": all(v["baseColorTexture"] and v["normalTexture"] for v in mats.values()),
    "single_sided_ok": all(not v["doubleSided"] for v in mats.values()),
    "gltf_bytes": os.path.getsize(path),
    "bin_bytes": os.path.getsize(os.path.join(EXP, CB.NAME + ".bin")),
}}
s = stats[CB.NAME]
s["budget_ok"] = s["tris"] <= BUDGET
with open(os.path.join(EXP, "chew_bone_stats.json"), "w") as fh:
    json.dump(stats, fh, indent=1)
print("%-12s %5d tris  %s  uris %s tex %s" % (CB.NAME, s["tris"], s["size_xyz_m"], s["uris_ok"], s["textured_ok"]))
ok = s["uris_ok"] and s["textured_ok"] and s["single_sided_ok"] and s["budget_ok"]
print("ALL OK" if ok else "FAILED")
if not ok:
    sys.exit(1)
