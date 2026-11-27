"""Export the approved backyard pools (#741) to self-contained .glb files in exports/, each with
its stats: pool_circle.glb + pool_circle_stats.json and pool_oval.glb + pool_oval_stats.json.

The pools have flat colours and no textures, so they export as .glb like the houses: each file
loads in a web glTF viewer as it is. Afterwards run the check:

  python3 art-source/blender/items/pool/export_pool.py
  python3 -m unittest discover -s art-source/blender/items/pool/tests

Usage: python3 export_pool.py [outroot] [--shape circle|oval]
  outroot defaults to this folder; writes outroot/exports/. --shape exports just that one.
"""
import json
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import dh_pool as P  # noqa: E402  (puts the house kit on the path)
import bpy  # noqa: E402
import dh_house_kit as K  # noqa: E402
import pool_core as PC  # noqa: E402

ROOT, SHAPES = HERE, list(PC.SHAPES)
argv = sys.argv[1:]
while argv:
    a = argv.pop(0)
    if a == "--shape":
        SHAPES = [argv.pop(0)]
    else:
        ROOT = a
for shape in SHAPES:
    if shape not in PC.SHAPES:
        sys.exit("unknown --shape %s; one of %s" % (shape, ", ".join(PC.SHAPES)))
EXP = os.path.join(ROOT, "exports")
os.makedirs(EXP, exist_ok=True)

bad = []
for shape in SHAPES:
    K.reset_scene()
    ob = P.build(shape)
    bpy.context.view_layer.update()
    glb = P.name_for(shape) + ".glb"
    path = os.path.join(EXP, glb)
    K.export_glb(path, [ob])
    tris = K.tri_count(ob)
    stats = {
        "name": "Backyard dog pool, %s (#741)" % shape,
        "units": "metres, Blender axes (up +Z, pivot on the ground at the centre of the water); "
                 "the .glb is Y-up",
        "file": glb,
        "tris": tris,
        "tri_budget": PC.TRI_BUDGET,
        "verts": len(ob.data.vertices),
        "materials": [m.name for m in ob.data.materials],
        "glb_bytes": os.path.getsize(path),
    }
    stats.update(P.measures(shape, ob))
    with open(os.path.join(EXP, P.name_for(shape) + "_stats.json"), "w") as f:
        json.dump(stats, f, indent=1)
        f.write("\n")
    print("%s  %d tris  footprint %.2f x %.2f m  height %.4f m" % (
        glb, tris, stats["footprint_size_m"][0], stats["footprint_size_m"][1], stats["height_m"]))
    if tris > PC.TRI_BUDGET:
        bad.append(shape)
if bad:
    print("FAILED: over the %d-triangle budget: %s" % (PC.TRI_BUDGET, ", ".join(bad)))
    sys.exit(1)
print("ALL OK")
