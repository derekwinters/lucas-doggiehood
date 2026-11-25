"""Find the sit pose parameters (dh_dog_rig.SIT) by measuring the skinned mesh.

  python3 solve_sit.py

Adjusts, in turn, until every contact group's lowest vertex is within 2 mm of the ground:
  drop       (root down)                 -> the haunches touch
  shoulder   (front legs, about vertical) -> the front paws touch
  hock_z / foot_pitch                    -> the hind leg lies on the ground: hock and rear foot touch
  tail       (tail_1)                    -> the tail lies on the ground instead of in it
The hip tilt (SIT["hips"]) and the stifle height are chosen, not solved.
Prints the SIT dict to paste into dh_dog_rig.py. Deterministic (fixed start, fixed steps).
"""
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import bpy  # noqa: E402,F401  (loads bmesh / mathutils)
import dh_dog as D  # noqa: E402
import dh_dog_rig as RG  # noqa: E402


def main():
    parts = D.build("default")
    arm, W = D.build.armature, D.build.weights
    p = dict(RG.SIT)

    def meas():
        RG.apply_pose(arm, parts, "sit", p)
        return RG.contacts(parts["body"], W)

    def fit(key, group, step, sign, iters=12):
        for _ in range(iters):
            c = meas()[group]
            if abs(c) < 0.002:
                return
            # secant step from a small probe
            p[key] += step
            c2 = meas()[group]
            p[key] -= step
            slope = (c2 - c) / step
            if abs(slope) < 1e-6:
                return
            p[key] -= max(-0.5, min(0.5, c / slope)) if key == "drop" else max(-8, min(8, c / slope))
    for _ in range(4):
        fit("drop", "haunches", 0.01, 1)
        fit("hips", "front_paws", 1.0, 1)
        fit("hock_z", "hocks", 0.005, 1)
        fit("foot_pitch", "hind_feet", 0.5, 1)
        fit("tail", "tail", 1.0, 1)
    c = meas()
    print("SIT = {%s}" % ", ".join('"%s": %s' % (k, round(v, 4)) for k, v in p.items()))
    print("contacts:", {k: round(v, 4) for k, v in c.items()})


if __name__ == "__main__":
    main()
