"""Export the delivery truck (#924) to a self-contained .glb, read it back and check it.

  python3 art-source/blender/vehicles/delivery-truck/export_truck.py [out_dir]

Writes <out_dir>/delivery_truck.glb (default: this folder's exports/) and, for the
default folder only, exports/delivery_truck_stats.json. The export is the truck at
rest: door closed, wheels unturned, body material white. truck_check.py reads the
.glb back with plain Python; the script prints ALL OK or the errors and exits
non-zero if any check fails. Two runs give byte-identical files (bpy 5.3.0).
"""
import json
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
sys.path.insert(0, os.path.normpath(os.path.join(HERE, "..", "..")))
import bpy  # noqa: E402,F401  (before the kit, which imports bmesh)
import dh_house_kit as K  # noqa: E402
import dh_truck as T  # noqa: E402
import truck_check as C  # noqa: E402
import truck_dims as D  # noqa: E402

OUT = sys.argv[1] if len(sys.argv) > 1 else os.path.join(HERE, "exports")
os.makedirs(OUT, exist_ok=True)
GLB = os.path.join(OUT, "delivery_truck.glb")

K.reset_scene()
root = T.build()
K.export_glb(GLB, [root])

errors = C.check(GLB)
if os.path.realpath(OUT) == os.path.realpath(os.path.join(HERE, "exports")):
    s = C.stats(C.load(GLB))
    s = {"name": "Delivery truck (#924)",
         "units": "metres; glTF axes (Y up, front +Z, truck's left +X). Blender: front -Y, up +Z",
         "core_footprint_m": {"length": D.LENGTH, "width": D.WIDTH},
         "height_m": D.HEIGHT,
         "door_open_deg_about_hinge_z_blender": D.DOOR_OPEN_DEG,
         "budget_tris": D.BUDGET_TRIS, "per_mesh_cap_tris": D.core_max_tris_per_mesh(),
         "glb_bytes": os.path.getsize(GLB), "errors": errors, **s}
    with open(os.path.join(OUT, "delivery_truck_stats.json"), "w") as f:
        json.dump(s, f, indent=1, sort_keys=False)
        f.write("\n")
    print(json.dumps({k: s[k] for k in ("size_gltf_xyz_m", "tris", "tris_total")}))
print("\n".join(errors) if errors else "ALL OK")
sys.exit(1 if errors else 0)
