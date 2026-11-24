"""Export the road kit to .gltf + .bin in exports/, pointing at this set's one
shared textures/ folder by relative URI (same layout as the other
environment sets). Every road tile type, for every look:

  road_<look>_<Type>.gltf/.bin   one mesh, two materials:
      DH_Road_asphalt_<tone>     asphalt colour + normal (UV0 = world / 6.4 m),
                                 x COLOR_0 (patches); UV1 = world / 211 m
                                 (TEXCOORD_1) for the ground macro, which glTF
                                 cannot express, so it is not in the material
      DH_Road_markings_<finish>  markings atlas colour + normal: paint, crack
                                 sealant and the flat curb band

Each tile is built at grid coordinate (0, 0), so it is a SAMPLE: its UVs and
its seeded patches and cracks depend on where the tile is. In the game the
mesh would be built at run time by Core + a thin mesh builder (as for the
lawn), or imported once and given a world-space shader. See README.
Sidewalks are not exported here (#803).

Reads each .gltf back and checks: image URIs relative and inside textures/;
two materials, each with a colour and a normal texture; TEXCOORD_0,
TEXCOORD_1 and COLOR_0 present; nothing double-sided; the mesh's X/Z
bounds equal the tile (+-30 m) for every type whose road reaches a tile edge.
Writes exports/roads_stats.json; exits non-zero if a check fails.
Only look A (Derek's pick, #836) is exported and kept by default.
Usage: python3 export_roads.py [outroot] [--looks a,b,c]
"""
import json
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import dh_roads as D  # noqa: E402
import bpy  # noqa: E402
import dh_house_kit as K  # noqa: E402
import dh_road_geom as R  # noqa: E402

ROOT = sys.argv[1] if len(sys.argv) > 1 and not sys.argv[1].startswith("--") else HERE
LOOKS = ["a"]                   # Derek's pick (#836); --looks a,b,c exports the others too
for i, a in enumerate(sys.argv):
    if a == "--looks":
        LOOKS = sys.argv[i + 1].split(",")
EXP = os.path.join(ROOT, "exports")
TEX = os.path.realpath(os.path.join(ROOT, "textures"))
os.makedirs(EXP, exist_ok=True)


def export_gltf(path, ob):
    bpy.ops.object.select_all(action="DESELECT")
    ob.select_set(True)
    bpy.ops.export_scene.gltf(filepath=path, export_format="GLTF_SEPARATE", use_selection=True,
                              export_apply=True, export_yup=True, export_keep_originals=True,
                              export_vertex_color="ACTIVE")


def uris_ok(path, j):
    for im in j.get("images", []):
        uri = im.get("uri")
        if uri is None or os.path.isabs(uri) or ":" in uri:
            return False
        f = os.path.realpath(os.path.join(os.path.dirname(path), uri))
        if os.path.dirname(f) != TEX or not os.path.isfile(f):
            return False
    return True


stats, bad = {}, []
for look in LOOKS:
    for t in R.ROAD_TYPES:
        K.reset_scene()
        res = D.build_tile(t, (0, 0), look, centre=(0.0, 0.0), sidewalks=False, export=True)
        ob = res["road"]
        key = "road_%s_%s" % (look, t)
        ob.name = key
        path = os.path.join(EXP, key + ".gltf")
        export_gltf(path, ob)
        j = json.load(open(path))
        mats = j.get("materials", [])
        mat_ok = len(mats) == 2 and all("baseColorTexture" in m.get("pbrMetallicRoughness", {}) and
                                         "normalTexture" in m for m in mats)
        single = not any(m.get("doubleSided") for m in mats)
        prims = j["meshes"][0]["primitives"]
        attr_ok = all(all(a in p["attributes"] for a in ("TEXCOORD_0", "TEXCOORD_1", "COLOR_0")) for p in prims)
        lo = [min(j["accessors"][p["attributes"]["POSITION"]]["min"][i] for p in prims) for i in range(3)]
        hi = [max(j["accessors"][p["attributes"]["POSITION"]]["max"][i] for p in prims) for i in range(3)]
        # glTF is Y-up: x = Core X, -z = Core Z
        lay = res["layout"]
        bounds_ok = True
        for d in lay.edges:
            if d[0] > 0:
                bounds_ok &= abs(hi[0] - R.HALF) < 1e-4
            if d[0] < 0:
                bounds_ok &= abs(lo[0] + R.HALF) < 1e-4
            if d[1] > 0:
                bounds_ok &= abs(-lo[2] - R.HALF) < 1e-4
            if d[1] < 0:
                bounds_ok &= abs(-hi[2] + R.HALF) < 1e-4
        verts = sum(j["accessors"][p["attributes"]["POSITION"]]["count"] for p in prims)
        s = stats[key] = {"look": look, "type": t, "tris": K.tri_count(ob), "verts_gltf": verts,
                          "primitives": len(prims), "images": sorted(im.get("uri") for im in j.get("images", [])),
                          "uris_ok": uris_ok(path, j), "materials_ok": mat_ok, "single_sided": single,
                          "attributes_ok": attr_ok, "bounds_ok": bool(bounds_ok),
                          "bounds_min": [round(v, 4) for v in lo], "bounds_max": [round(v, 4) for v in hi],
                          "gltf_bytes": os.path.getsize(path),
                          "bin_bytes": os.path.getsize(os.path.join(EXP, key + ".bin")),
                          "counts": res["counts"]}
        ok = s["uris_ok"] and mat_ok and single and attr_ok and s["bounds_ok"]
        print("%-28s %5d tris %5d verts %d prims  %s" % (key, s["tris"], verts, len(prims), "ok" if ok else "FAIL"))
        if not ok:
            bad.append(key)
with open(os.path.join(EXP, "roads_stats.json"), "w") as f:
    json.dump(stats, f, indent=1)
print("ALL OK" if not bad else "FAILED: %s" % bad)
if bad:
    sys.exit(1)
