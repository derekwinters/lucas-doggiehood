"""Export sample lawn tiles to .gltf + .bin in exports/, pointing at this
set's one shared textures/ folder by relative URI (same layout as the other
environment sets). Derek's pick (2026-11-24): look C with the chosen
approach "rec", (d)+(a). One 60 m map tile at the world origin tile
[0, 60] x [0, 60]:

  ground_c_tile_rec   the (d) grid: 3 m world cells (400 quads, 800 tris),
                      each cell's UV0 in its variant's slot of the 1024 x 512
                      8-variant atlas; UV1 = world / MACRO_M (TEXCOORD_1);
                      the macro tint baked per vertex as COLOR_0

These are SAMPLES for review and for checking the importer. In the game the
tile mesh depends on where the tile is (every cell's variant comes from its
world coordinates), so it would be built at run time by the tile builder,
not imported. glTF has no detail map, so the macro tint (UV1) is not in the
.gltf material; in Unity it is URP Lit's Detail Albedo on UV1.

Reads each .gltf back and checks: image URIs relative and inside
textures/; the lawn material has a colour and a normal texture; the cells
tile carries TEXCOORD_1 and COLOR_0. Writes exports/ground_stats.json and exits non-zero
if a check fails.
Usage: python3 export_ground.py [outroot]"""
import json
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import dh_ground as G  # noqa: E402
import bpy  # noqa: E402
import dh_house_kit as K  # noqa: E402
import dh_ground_tex as GT  # noqa: E402

ROOT = sys.argv[1] if len(sys.argv) > 1 else HERE
EXP = os.path.join(ROOT, "exports")
TEX = os.path.realpath(os.path.join(ROOT, "textures"))
LOOK = "c"
EXPORT_APPROACHES = {"rec": "rec"}
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


stats = {}
for look in (LOOK,):
    for tag, ap in EXPORT_APPROACHES.items():
        K.reset_scene()
        key = "ground_%s_tile_%s" % (look, tag)
        ob = G.lawn_tile(look, ap, 0.0, 0.0, name=key, bake_macro_vcol=True)
        path = os.path.join(EXP, key + ".gltf")
        export_gltf(path, ob)
        j = json.load(open(path))
        m = j["materials"][0]
        pbr = m.get("pbrMetallicRoughness", {})
        attrs = j["meshes"][0]["primitives"][0]["attributes"]
        mat_ok = "baseColorTexture" in pbr and "normalTexture" in m
        uv1_ok = "TEXCOORD_1" in attrs and "COLOR_0" in attrs
        stats[key] = {"look": look, "approach": ap, "tris": K.tri_count(ob), "verts_gltf":
                      j["accessors"][attrs["POSITION"]]["count"],
                      "images": sorted(im.get("uri") for im in j.get("images", [])),
                      "uris_ok": uris_ok(path, j), "material_ok": mat_ok, "uv1_ok": uv1_ok,
                      "gltf_bytes": os.path.getsize(path), "bin_bytes": os.path.getsize(os.path.join(EXP, key + ".bin"))}
        s = stats[key]
        print("%-24s %4d tris %5d verts  uris %s mat %s uv1 %s" % (key, s["tris"], s["verts_gltf"], s["uris_ok"],
                                                                   s["material_ok"], s["uv1_ok"]))
with open(os.path.join(EXP, "ground_stats.json"), "w") as f:
    json.dump(stats, f, indent=1)
bad = [k for k, s in stats.items() if not (s["uris_ok"] and s["material_ok"] and s["uv1_ok"])]
print("ALL OK" if not bad else "FAILED: %s" % bad)
if bad:
    sys.exit(1)
