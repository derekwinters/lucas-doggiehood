"""Export the paving (#803; P1 approved, #851) to .gltf + .bin in exports/, pointing at
this set's textures/ by relative URI (same layout as the road kit):

  paving_<opt>_<Type>.gltf/.bin   one tile's sidewalk slabs: ONE material
                                  (DH_Paving_concrete_<key>: slab atlas colour
                                  + normal), TEXCOORD_0 (atlas), COLOR_0 (tone)
  paving_<opt>_lot.gltf/.bin      the starter lot's walkway + driveway

Each tile is built at grid coordinate (0, 0): the slab variants come from a
hash of the WORLD position, so this is a sample (as for the road kit). In
the game the slabs would be built at run time by Core + a thin mesh builder.

Reads each .gltf back and checks: image URIs relative and inside textures/ (or
the road kit's, for an asphalt driveway);
the sidewalk mesh has exactly one material with a colour and a normal
texture; TEXCOORD_0 and COLOR_0 present; nothing double-sided; X/Z bounds
reach the tile edge (+-30 m) wherever the sidewalk does. Writes
exports/paving_stats.json; exits non-zero if a check fails.
Usage: python3 export_paving.py [outroot] [--opts p1,p2,p3]
"""
import json
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import dh_paving as P  # noqa: E402
import bpy  # noqa: E402
import dh_house_kit as K  # noqa: E402
import dh_road_geom as R  # noqa: E402

ROOT = sys.argv[1] if len(sys.argv) > 1 and not sys.argv[1].startswith("--") else HERE
OPTS = ["p1"]                   # Derek's pick (#851); --opts p1,p2,p3 for the others
for i, a in enumerate(sys.argv):
    if a == "--opts":
        OPTS = sys.argv[i + 1].split(",")
EXP = os.path.join(ROOT, "exports")
TEX = os.path.realpath(os.path.join(ROOT, "textures"))
ROAD_TEX = os.path.realpath(os.path.join(HERE, "..", "roads", "textures"))   # an asphalt driveway shares the road's
os.makedirs(EXP, exist_ok=True)


def export_gltf(path, ob):
    bpy.ops.object.select_all(action="DESELECT")
    ob.select_set(True)
    bpy.ops.export_scene.gltf(filepath=path, export_format="GLTF_SEPARATE", use_selection=True,
                              export_apply=True, export_yup=True, export_keep_originals=True,
                              export_vertex_color="ACTIVE")


def read_back(path, n_mats=None):
    j = json.load(open(path))
    bad = []
    for im in j.get("images", []):
        uri = im.get("uri", "")
        full = os.path.realpath(os.path.join(os.path.dirname(path), uri))
        if os.path.isabs(uri) or ":" in uri or not any(full.startswith(d + os.sep) for d in (TEX, ROAD_TEX)) or not os.path.exists(full):
            bad.append("image uri %s" % uri)
    mats = j.get("materials", [])
    if n_mats is not None and len(mats) != n_mats:
        bad.append("%d materials" % len(mats))
    for m in mats:
        if m.get("doubleSided"):
            bad.append("%s double-sided" % m["name"])
        if "baseColorTexture" not in m.get("pbrMetallicRoughness", {}) or "normalTexture" not in m:
            bad.append("%s missing a texture" % m["name"])
    xs, zs = [], []
    for mesh in j["meshes"]:
        for pr in mesh["primitives"]:
            a = pr["attributes"]
            for k in ("TEXCOORD_0", "COLOR_0"):
                if k not in a:
                    bad.append("no %s" % k)
            acc = j["accessors"][a["POSITION"]]
            xs += [acc["min"][0], acc["max"][0]]
            zs += [-acc["max"][2], -acc["min"][2]]           # glTF +Z = Blender -Y
    bins = sum(os.path.getsize(os.path.join(os.path.dirname(path), b["uri"])) for b in j["buffers"])
    return bad, (min(xs), max(xs), min(zs), max(zs)), bins


def main():
    fails, stats = [], {}
    for opt in OPTS:
        st = stats[opt] = {}
        for t in R.ROAD_TYPES:
            K.reset_scene()
            ob, slabs = P.build_sidewalks(t, (0, 0), opt, centre=(0.0, 0.0), name="paving_%s_%s" % (opt, t))
            path = os.path.join(EXP, "paving_%s_%s.gltf" % (opt, t))
            export_gltf(path, ob)
            bad, bb, nbin = read_back(path, n_mats=1)
            lay = R.Layout(t)
            want = lay.sidewalk.bounds                     # tile-local x, y (= Core X, Z)
            got = (bb[0], bb[2], bb[1], bb[3])
            if any(abs(a - b) > 1e-3 for a, b in zip(want, got)):
                bad.append("bounds %s != %s" % (got, want))
            st[t] = {"slabs": len(slabs), "tris": K.tri_count(ob), "bin_bytes": nbin}
            fails += ["%s %s: %s" % (opt, t, b) for b in bad]
        K.reset_scene()
        steps = P.front_steps(P.STARTER_BASE)               # P1's walkway width comes from the steps
        sc = P.G.lot_scene(base=P.STARTER_BASE)             # the approved lot, for its door / road / garage
        lot, cy = sc["lot"], sc["tile_centre"][1]
        door = (getattr(lot["B"], "DOOR_X", 0.0), lot["bounds"][2])
        K.reset_scene()
        ob, _ = P.lot_paving(opt, door, cy, lot["dinfo"]["x"], lot["dinfo"]["y"][1], name="paving_%s_lot" % opt,
                             export=True, steps=steps)
        path = os.path.join(EXP, "paving_%s_lot.gltf" % opt)
        export_gltf(path, ob)
        bad, bb, nbin = read_back(path)
        st["lot"] = {"tris": K.tri_count(ob), "bin_bytes": nbin, "materials": len(ob.data.materials)}
        fails += ["%s lot: %s" % (opt, b) for b in bad]
    with open(os.path.join(EXP, "paving_stats.json"), "w") as f:
        json.dump(stats, f, indent=1)
    for opt, st in stats.items():
        tiles = [v for k, v in st.items() if k != "lot"]
        print("%s: %d types, tris %d-%d, bin %d-%d bytes; lot tris %d, %d materials" % (
            opt, len(tiles), min(v["tris"] for v in tiles), max(v["tris"] for v in tiles),
            min(v["bin_bytes"] for v in tiles), max(v["bin_bytes"] for v in tiles), st["lot"]["tris"],
            st["lot"]["materials"]))
    print("FAILED:\n  " + "\n  ".join(fails) if fails else "ALL OK")
    return 1 if fails else 0


if __name__ == "__main__":
    sys.exit(main())
