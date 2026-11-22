"""Export each textured tree as .gltf (JSON) + .bin, then read back each
file's JSON and check its leaf materials kept alphaMode MASK + doubleSided.

Images are NOT embedded or copied: every tree references the one shared
copy of its atlas and bark maps in ../textures/ by relative URI
(export_keep_originals), so each texture is stored once in the repository.
Run with an out dir that is a sibling of textures/ (normally exports/) so
the relative URIs resolve; the check below fails otherwise.
Usage: python3 export_trees_tex.py <outdir>"""
import json, os, sys
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import dh_trees_tex as TT  # noqa
import dh_house_kit as K  # noqa
import bpy  # noqa
from mathutils import Vector  # noqa
OUT = sys.argv[1]
os.makedirs(OUT, exist_ok=True)


def export_gltf(path, ob):
    """Kit's export settings, but separate .gltf + .bin with the original
    texture files referenced in place rather than embedded."""
    bpy.ops.object.select_all(action="DESELECT")
    ob.select_set(True)
    for c in ob.children:
        c.select_set(True)
    bpy.ops.export_scene.gltf(filepath=path, export_format="GLTF_SEPARATE", use_selection=True,
                              export_apply=True, export_yup=True, export_keep_originals=True)


def gltf_json(path):
    with open(path) as f:
        return json.load(f)


def uris_ok(path, j):
    """Every image URI is relative, resolves to a file in the shared
    textures/ folder, and no image is embedded in the buffer."""
    tex = os.path.realpath(os.path.join(HERE, "textures"))
    for im in j.get("images", []):
        uri = im.get("uri")
        if uri is None or os.path.isabs(uri) or ":" in uri:
            return False
        f = os.path.realpath(os.path.join(os.path.dirname(path), uri))
        if os.path.dirname(f) != tex or not os.path.isfile(f):
            return False
    return True


stats = {}
for key, (fn, label) in TT.PIECES.items():
    K.reset_scene()
    ob = fn()
    bpy.context.view_layer.update()
    pts = [ob.matrix_world @ Vector(c) for c in ob.bound_box]
    size = [round(max(p[i] for p in pts) - min(p[i] for p in pts), 2) for i in range(3)]
    me = ob.data
    leaf_tris = sum(len(p.vertices) - 2 for p in me.polygons if "leaves" in me.materials[p.material_index].name)
    path = os.path.join(OUT, key + ".gltf")
    export_gltf(path, ob)
    j = gltf_json(path)
    mats = {m["name"]: {"alphaMode": m.get("alphaMode", "OPAQUE"), "alphaCutoff": m.get("alphaCutoff"),
                        "doubleSided": m.get("doubleSided", False),
                        "normalTexture": "normalTexture" in m,
                        "baseColorFactor": m.get("pbrMetallicRoughness", {}).get("baseColorFactor")}
            for m in j["materials"]}
    imgs = [im.get("uri") for im in j.get("images", [])]
    ok = all(v["alphaMode"] == "MASK" and v["doubleSided"] for n, v in mats.items() if "leaves" in n)
    stats[key] = {"label": label, "tris": K.tri_count(ob), "leaf_tris": leaf_tris, "size_xyz_m": size,
                  "materials": mats, "images": imgs, "leaf_alpha_mask_double_sided_ok": ok,
                  "shared_texture_uris_ok": uris_ok(path, j),
                  "gltf_bytes": os.path.getsize(path),
                  "bin_bytes": os.path.getsize(os.path.splitext(path)[0] + ".bin")}
    print(key, stats[key]["tris"], size, "MASK/doubleSided ok" if ok else "CHECK FAILED",
          "URIs ok" if stats[key]["shared_texture_uris_ok"] else "URI CHECK FAILED")
with open(os.path.join(OUT, "trees_textured_stats.json"), "w") as f:
    json.dump(stats, f, indent=1)
bad = [k for k, v in stats.items()
       if not (v["leaf_alpha_mask_double_sided_ok"] and v["shared_texture_uris_ok"])]
if bad:
    sys.exit("glTF check failed: %s" % ", ".join(bad))
