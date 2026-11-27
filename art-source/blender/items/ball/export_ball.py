"""Export the dog ball (#333), one glTF per colour variant like the dogs' coats:
exports/ball_<variant>.gltf + .bin, pointing at ../textures/ by relative URI (its own colour map
and the shared normal map). Reads each back, checks it and writes exports/ball_stats.json. Exits
non-zero if a check fails.

  python3 export_ball.py [outroot] [--viewer outdir]

outroot defaults to this folder; its textures/ are built first if missing. --viewer also writes
self-contained .glb files (textures embedded) for a web 3D viewer: ball_<variant>.glb and
ball_all.glb (every variant in a row). They are a by-product for review only and are not committed,
because a set stores each texture once (blender-pipeline.md).

Checks: the image URIs are relative and resolve inside textures/, every material has a colour and a
normal map, nothing is double-sided, the triangle count is within the art budget (TRI_BUDGET), the
size is the ball's diameter on every axis, and the origin is on the ground under the ball's centre.
"""
import json
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
sys.path.insert(0, os.path.normpath(os.path.join(HERE, "..", "..")))   # the house kit (art-source/blender)
import bpy  # noqa: E402
from mathutils import Vector  # noqa: E402
import dh_house_kit as K  # noqa: E402
import dh_ball_tex as BT  # noqa: E402
import dh_ball as B  # noqa: E402

SIZE_TOLERANCE_M = 0.002       # the faceted sphere is a hair under the true diameter across flats
ORIGIN_TOLERANCE_M = 1e-5
VIEWER_ROW_GAP = 1.4           # centre spacing of the balls in ball_all.glb, in diameters


def export(path, ob, fmt="GLTF_SEPARATE"):
    bpy.ops.object.select_all(action="DESELECT")
    ob.select_set(True)
    bpy.ops.export_scene.gltf(filepath=path, export_format=fmt, use_selection=True, export_apply=True,
                              export_yup=True, export_keep_originals=(fmt == "GLTF_SEPARATE"))


def uris_ok(path, j, tex_dir):
    for im in j.get("images", []):
        uri = im.get("uri")
        if uri is None or os.path.isabs(uri) or ":" in uri:
            return False
        f = os.path.realpath(os.path.join(os.path.dirname(path), uri))
        if os.path.dirname(f) != tex_dir or not os.path.isfile(f):
            return False
    return True


def _uri(j, info):
    return j["images"][j["textures"][info["index"]]["source"]].get("uri")


def export_variant(exp, tex, tex_dir, key):
    """Export one colour variant to exports/ball_<key>.gltf + .bin; returns (its stats, the object)."""
    K.reset_scene()
    ob = B.build(key, tex)
    bpy.context.view_layer.update()
    path = os.path.join(exp, "%s_%s.gltf" % (B.NAME, key))
    export(path, ob)
    with open(path) as f:
        j = json.load(f)
    m = j["materials"]
    pbr = [x.get("pbrMetallicRoughness", {}) for x in m]
    return {
        "hex": BT.VARIANTS[key], "file": os.path.basename(path), "object": j["nodes"][0]["name"],
        "materials": [x["name"] for x in m],
        "images": [_uri(j, pbr[0]["baseColorTexture"]), _uri(j, m[0]["normalTexture"])]
        if "baseColorTexture" in pbr[0] and "normalTexture" in m[0] else [],
        "uris_ok": uris_ok(path, j, tex_dir),
        "textured_ok": all("baseColorTexture" in b and "normalTexture" in x for b, x in zip(pbr, m)),
        "single_sided_ok": all(not x.get("doubleSided", False) for x in m),
        "roughness": round(pbr[0].get("roughnessFactor", 1.0), 3),
        "gltf_bytes": os.path.getsize(path), "bin_bytes": os.path.getsize(path[:-5] + ".bin"),
    }, ob


def export_viewer(outdir, tex):
    """Self-contained .glb files (textures embedded) for a web 3D viewer: ball_<key>.glb per
    variant, and ball_all.glb with every variant in a row along X."""
    os.makedirs(outdir, exist_ok=True)
    for key in BT.VARIANTS:
        K.reset_scene()
        export(os.path.join(outdir, "%s_%s.glb" % (B.NAME, key)), B.build(key, tex), fmt="GLB")
    K.reset_scene()
    obs = []
    for i, key in enumerate(BT.VARIANTS):
        ob = B.build(key, tex)
        ob.name = "%s_%s" % (B.NAME, key)
        ob.location.x = (i - (len(BT.VARIANTS) - 1) / 2) * BT.DIAMETER_M * VIEWER_ROW_GAP
        obs.append(ob)
    bpy.ops.object.select_all(action="DESELECT")
    for ob in obs:
        ob.select_set(True)
    bpy.ops.export_scene.gltf(filepath=os.path.join(outdir, B.NAME + "_all.glb"), export_format="GLB",
                              use_selection=True, export_apply=True, export_yup=True)
    print("viewer ->", outdir)


def main(argv):
    viewer = None
    if "--viewer" in argv:
        i = argv.index("--viewer")
        viewer = os.path.abspath(argv[i + 1])
        argv = argv[:i] + argv[i + 2:]
    root = os.path.abspath(argv[0]) if argv else HERE
    exp = os.path.join(root, "exports")
    tex_dir = os.path.realpath(os.path.join(root, "textures"))
    os.makedirs(exp, exist_ok=True)
    tex = BT.paths(tex_dir)

    variants, ob = {}, None
    for key in BT.VARIANTS:
        variants[key], ob = export_variant(exp, tex, tex_dir, key)
    pts = [ob.matrix_world @ Vector(c) for c in ob.bound_box]
    lo = [round(min(p[i] for p in pts), 5) for i in range(3)]
    hi = [round(max(p[i] for p in pts), 5) for i in range(3)]
    size = [round(hi[i] - lo[i], 4) for i in range(3)]
    stats = {
        "object": B.NAME, "tris": K.tri_count(ob), "tri_budget": BT.TRI_BUDGET,
        "diameter_m": BT.DIAMETER_M, "real_diameter_m": BT.REAL_DIAMETER_M,
        "size_xyz_m": size, "bbox_min_m": lo, "bbox_max_m": hi,
        "budget_ok": K.tri_count(ob) <= BT.TRI_BUDGET,
        "size_ok": all(abs(s - BT.DIAMETER_M) <= SIZE_TOLERANCE_M for s in size),
        "pivot_ok": abs(lo[2]) <= ORIGIN_TOLERANCE_M and abs(lo[0] + hi[0]) <= ORIGIN_TOLERANCE_M
        and abs(lo[1] + hi[1]) <= ORIGIN_TOLERANCE_M,
        "variants": variants,
    }
    with open(os.path.join(exp, "ball_stats.json"), "w") as f:
        json.dump(stats, f, indent=1)
        f.write("\n")
    print("%s  %d tris  size %s m  budget %s  size %s  pivot %s" % (
        B.NAME, stats["tris"], size, stats["budget_ok"], stats["size_ok"], stats["pivot_ok"]))
    for k, v in variants.items():
        print("  %-8s %s  %s  uris %s  textured %s  single-sided %s" % (
            k, v["hex"], v["file"], v["uris_ok"], v["textured_ok"], v["single_sided_ok"]))
    if viewer:
        export_viewer(viewer, tex)
    ok = all(stats[k] for k in ("budget_ok", "size_ok", "pivot_ok")) and all(
        v[k] for v in variants.values() for k in ("uris_ok", "textured_ok", "single_sided_ok"))
    print("ALL OK" if ok else "FAILED")
    return 0 if ok else 1


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
