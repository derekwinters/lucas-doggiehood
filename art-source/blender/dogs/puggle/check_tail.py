"""Puggle-only extra: the curled tail stays clear of the back (and of the ground) in every frame of
every clip.

  python3 art-source/blender/dogs/puggle/check_tail.py [out_file]

Bakes every clip exactly as the kit's motion step does (dh_dog_clips on the default coat) and, on
the skinned mesh in every frame, measures:
  - back clearance: the signed distance (m) of every vertex of the curl (vertices whose strongest
    bone is tail_2, tail_3 or tail_4) from the back's surface (the faces whose vertices all belong to
    hips, spine_1, spine_2 or a thigh). Negative = the tail is inside the back;
  - ground clearance: the lowest tail vertex (any tail bone) above z = 0.
Fails (exit 1) if the curl comes within CLEAR m of the back or the tail within CLEAR m of the
ground in any frame. Writes the report to out_file (default: motion/review/tail-clearance.txt).
Deterministic.
"""
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
KIT = os.path.join(os.path.dirname(HERE), "kit")
sys.path.insert(0, KIT)
import dh_breed  # noqa: E402

dh_breed.use(HERE)
import bpy  # noqa: E402,F401
import numpy as np  # noqa: E402
from mathutils import Vector  # noqa: E402
from mathutils.bvhtree import BVHTree  # noqa: E402

import dh_dog_anim as A  # noqa: E402
import dh_dog_check as K  # noqa: E402
import dh_dog_clips as C  # noqa: E402
import dh_dog_rig as RG  # noqa: E402

CLEAR = 0.01
CURL = ("tail_2", "tail_3", "tail_4")
BACK = ("hips", "spine_1", "spine_2", "back_hip_L", "back_hip_R")
OUT = sys.argv[1] if len(sys.argv) > 1 else os.path.join(HERE, "motion", "review", "tail-clearance.txt")


def main():
    rig = A.Rig()
    dom = np.array(RG.BONE_NAMES)[rig.W.argmax(1)]
    curl = np.isin(dom, CURL)
    tail = np.char.startswith(dom.astype(str), "tail_")
    back = np.isin(dom, BACK)
    polys = [list(p.vertices) for p in rig.parts["body"].data.polygons]
    back_faces = [p for p in polys if back[p].all()]
    lines = ["Puggle - curled tail clearance (skinned mesh, every frame)",
             "back = signed distance of the curl (tail_2..4 vertices) from the back's surface "
             "(hips, spine_1, spine_2, thighs); ground = lowest tail vertex. Metres; must stay above %.3f." % CLEAR,
             ""]
    fails = []
    for name in C.ORDER:
        try:
            m = C.make(rig, name)
        except KeyError:
            continue
        S = K.sample(rig, bpy.data.actions[name], m["frames"])
        worst_b, worst_g = (1e9, -1), (1e9, -1)
        for f, P in enumerate(S):
            v = P["body"]
            bvh = BVHTree.FromPolygons([tuple(x) for x in v], back_faces)
            ds = []
            for p in v[curl]:
                loc, nrm, _, d = bvh.find_nearest(Vector(p))
                ds.append(d if (Vector(p) - loc).dot(nrm) >= 0 else -d)
            b = min(ds)
            g = float(v[tail, 2].min())
            worst_b = min(worst_b, (b, f))
            worst_g = min(worst_g, (g, f))
        ok = worst_b[0] > CLEAR and worst_g[0] > CLEAR
        lines.append("%-8s %3d frames   back %+.4f (f%d)   ground %+.4f (f%d)   %s"
                     % (name, m["frames"], worst_b[0], worst_b[1], worst_g[0], worst_g[1], "OK" if ok else "FAIL"))
        if not ok:
            fails.append(name)
    lines.append("")
    lines.append("ALL OK" if not fails else "FAILED: " + ", ".join(fails))
    os.makedirs(os.path.dirname(OUT), exist_ok=True)
    with open(OUT, "w") as fh:
        fh.write("\n".join(lines) + "\n")
    print("\n".join(lines))
    sys.exit(1 if fails else 0)


if __name__ == "__main__":
    main()
