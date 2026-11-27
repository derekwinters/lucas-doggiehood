"""Export the dog bed (#411): textures, then exports/dog_bed.gltf + .bin (images by relative URI
into textures/, like the street props, tinted with the default palette colour),
exports/bed_stats.json and bed_colours.json (the 10-colour palette). Reads the .gltf back and
runs every check in check_bed.py; prints ALL OK, or FAILED and exits 1.

  python3 art-source/blender/items/bed/export_bed.py [outroot]

outroot defaults to this folder (the committed textures/, exports/ and bed_colours.json). Two runs give
byte-identical files on bpy 5.3.0 (tests/test_bed_rebuild.py).
"""
import json
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import bpy  # noqa: E402
from mathutils import Vector  # noqa: E402
import check_bed as C  # noqa: E402
import dh_bed as BD  # noqa: E402
import dh_bed_tex as BT  # noqa: E402
import dh_house_kit as K  # noqa: E402

ROOT = os.path.abspath(sys.argv[1]) if len(sys.argv) > 1 else HERE
EXP = os.path.join(ROOT, "exports")
TEX = os.path.join(ROOT, "textures")
RAY_TOP = 5.0               # rays for the rest-area check start this high
RAY_GRID = 9                # rays per side over the rest area


def export_gltf(path, ob):
    bpy.ops.object.select_all(action="DESELECT")
    ob.select_set(True)
    for c in ob.children:
        c.select_set(True)
    bpy.ops.export_scene.gltf(filepath=path, export_format="GLTF_SEPARATE", use_selection=True,
                              export_apply=True, export_yup=True, export_keep_originals=True)


def rest_samples(rest):
    dg = bpy.context.evaluated_depsgraph_get()
    rw, rd = C.REST_AREA_MIN
    zs = []
    for i in range(RAY_GRID):
        for k in range(RAY_GRID):
            o = Vector((rest.x - rw / 2 + rw * i / (RAY_GRID - 1), rest.y - rd / 2 + rd * k / (RAY_GRID - 1), RAY_TOP))
            hit, loc, *_ = bpy.context.scene.ray_cast(dg, o, Vector((0, 0, -1)))
            zs.append(loc.z if hit else None)
    return zs


def palette_doc():
    return {
        "source": "art-source/blender/items/bed/dh_bed.py PALETTE",
        "generated_by": "art-source/blender/items/bed/export_bed.py",
        "how_to_tint": ("The cover and base maps are neutral grey. Tint a bed by setting the base colour "
                        "(glTF baseColorFactor) of DH_Item_Bed_Cover to 'cover' and of DH_Item_Bed_Base to "
                        "'base'. DH_Item_Bed_Cushion is never tinted. The exported glTF is in the default colour."),
        "base_darken": C.BASE_DARKEN,
        "default": BD.DEFAULT_COLOUR,
        "colours": BD.palette(),
    }


def main():
    os.makedirs(EXP, exist_ok=True)
    pal = palette_doc()
    with open(os.path.join(ROOT, "bed_colours.json"), "w") as fh:
        json.dump(pal, fh, indent=2)
        fh.write("\n")
    BT.build_all(TEX)
    K.reset_scene()
    ob = BD.build(tex_dir=TEX)
    pts = [ob.matrix_world @ v.co for v in ob.data.vertices]
    lo = [min(p[i] for p in pts) for i in range(3)]
    hi = [max(p[i] for p in pts) for i in range(3)]
    size = [hi[i] - lo[i] for i in range(3)]
    rest = [c for c in ob.children if c.name == BD.REST_NAME][0].matrix_world.translation
    tris = K.tri_count(ob)
    path = os.path.join(EXP, BD.NAME + ".gltf")
    export_gltf(path, ob)
    with open(path) as fh:
        j = json.load(fh)
    checks = {
        "footprint": C.footprint_errors(size),
        "pivot": C.pivot_errors(lo, hi),
        "budget": C.budget_errors(tris) + C.budget_errors(tris, C.TARGET_TRIS),
        "flat_top": C.flat_top_errors(rest_samples(rest), BD.DIMS["rest_z"]),
        "gltf": C.gltf_errors(j, path, TEX),
        "palette": C.palette_errors(pal),
    }
    mats = {}
    for m in j["materials"]:
        pbr = m.get("pbrMetallicRoughness", {})
        mats[m["name"]] = {"baseColorFactor": [round(v, 4) for v in pbr.get("baseColorFactor", [1, 1, 1, 1])],
                           "baseColorTexture": "baseColorTexture" in pbr, "normalTexture": "normalTexture" in m,
                           "doubleSided": m.get("doubleSided", False)}
    stats = {
        "name": "Dog bed (#411)",
        "units": "metres, Blender axes (front -Y, up +Z); the glTF file is Y-up (front +Z)",
        "object": BD.NAME,
        "tris": tris,
        "budget_tris": C.BUDGET_TRIS,
        "target_tris": C.TARGET_TRIS,
        "size_xyz_m": [round(v, 4) for v in size],
        "footprint_m": [C.FOOTPRINT_W, C.FOOTPRINT_D],
        "todays_slab_m": [C.SLAB_W, C.SLAB_D],
        "rest_point_m": [round(v, 4) for v in rest],
        "rest_area_m": list(C.REST_AREA_MIN),
        "bolster_top_m": {"back_and_sides": BD.DIMS["back_top"], "front": BD.DIMS["front_top"]},
        "colour": BD.DEFAULT_COLOUR,
        "palette": "bed_colours.json (%d colours)" % len(pal["colours"]),
        "materials": mats,
        "images": sorted(im["uri"] for im in j.get("images", [])),
        "gltf_bytes": os.path.getsize(path),
        "bin_bytes": os.path.getsize(os.path.join(EXP, BD.NAME + ".bin")),
        "checks": checks,
    }
    stats["all_checks_ok"] = not any(checks.values())
    with open(os.path.join(EXP, "bed_stats.json"), "w") as fh:
        json.dump(stats, fh, indent=1)
        fh.write("\n")
    print("%s  %d tris  %.3f x %.3f x %.3f m  rest %s" % (BD.NAME, tris, size[0], size[1], size[2],
                                                         stats["rest_point_m"]))
    for name, errs in checks.items():
        print("  %-10s %s" % (name, "ok" if not errs else "; ".join(errs)))
    if stats["all_checks_ok"]:
        print("ALL OK")
    else:
        print("FAILED")
        sys.exit(1)


if __name__ == "__main__":
    main()
