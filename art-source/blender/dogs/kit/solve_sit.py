"""Find a breed's sit pose parameters (SIT in its breed.py) by measuring the skinned mesh.

  python3 solve_sit.py <breed>

Adjusts, in turn, until every contact group's lowest vertex is within 2 mm of the ground:
  drop       (root down)                 -> the haunches touch
  shoulder   (front legs, about vertical) -> the front paws touch
  hock_z / foot_pitch                    -> the hind leg lies on the ground: hock and rear foot touch
  tail       (tail_1)                    -> the tail lies on the ground instead of in it
                                            (not for a tail carried in the sit: SIT_POSE["tail_on_ground"])
The hip tilt (SIT["hips"]) and the stifle height are chosen, not solved.
Starts from the breed's current SIT and prints the new SIT dict to paste into its breed.py.
Deterministic (fixed start, fixed steps).
"""
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import dh_breed  # noqa: E402

if __name__ == "__main__":
    if len(sys.argv) < 2:
        sys.exit(__doc__)
    dh_breed.use(sys.argv[1])
import bpy  # noqa: E402,F401  (loads bmesh / mathutils)
import dh_dog as D  # noqa: E402
import dh_dog_rig as RG  # noqa: E402


# (SIT key, contact group, probe step, sign), fitted in turn; a tail carried in the sit
# (SIT_POSE["tail_on_ground"] False) is not fitted: its SIT["tail"] is the breed's choice
FITS = [f for f in (("drop", "haunches", 0.01, 1), ("hips", "front_paws", 1.0, 1), ("hock_z", "hocks", 0.005, 1),
                    ("foot_pitch", "hind_feet", 0.5, 1), ("tail", "tail", 1.0, 1))
        if f[1] in RG.SIT_TOUCH]


def main():
    parts = D.build(list(D.T.COATS)[0], materials=False)     # contact needs no textures
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
        for key, group, step, sign in FITS:
            fit(key, group, step, sign)
    c = meas()
    print("SIT = {%s}" % ", ".join('"%s": %s' % (k, round(v, 4)) for k, v in p.items()))
    print("contacts:", {k: round(v, 4) for k, v in c.items()})


if __name__ == "__main__":
    main()
