"""Find a breed's lying pose parameters (LIE in its breed.py) by measuring the skinned mesh.

  python3 solve_lie.py <breed>

Adjusts, in turn, until every lying contact group's lowest vertex is within 2 mm of the ground:
  drop        (root down)                    -> the chest rests on the ground
  elbow_z     (elbow joint height)           -> the elbows (forearms) rest on it
  wrist_z     (wrist joint height)           -> the front paws rest on it
  hock_z / foot_pitch                        -> the hind leg lies on the ground: hock and rear foot
then, last and only where it can reach, tail (tail_1) -> the tail lies on the ground instead of in
it. The tail is not a lying contact: a tail carried in the sit (SIT_POSE["tail_on_ground"]) is not
fitted, and a tail too short to reach the ground lying down keeps the breed's LIE["tail"].
The hip tilt (LIE["hips"]), the stifle height and LIE["tail_2"] / ["tail_3"] are chosen, not solved.
Starts from the breed's current LIE and prints the new LIE dict to paste into its breed.py, then
the contacts it measured (every group, and the lowest vertex of any part). Deterministic (fixed
start, fixed steps).
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
import dh_dog_anim as A  # noqa: E402
import dh_dog_check as K  # noqa: E402
import dh_dog_rig as RG  # noqa: E402

# (LIE key, contact group, probe step, largest step), fitted in turn
FITS = [("drop", "chest", 0.01, 0.5), ("elbow_z", "elbows", 0.005, 0.05), ("wrist_z", "front_paws", 0.005, 0.05),
        ("hock_z", "hocks", 0.005, 0.05), ("foot_pitch", "hind_feet", 0.5, 8.0)]
TAIL_FIT = ("tail", "tail", 1.0, 8.0)
REACH = 0.002       # metres: a fit has reached the ground when its group is this close


def measure(rig, p):
    """{group: lowest z} for the lie with parameters p, plus the lowest vertex of any part."""
    rig.reset()
    rig.apply_spec(RG.lie_spec(p))
    rig.update()
    c = RG.contacts(rig.parts["body"], rig.W, RG.GROUPS)
    c["lowest_any_part"] = min(float(v[:, 2].min()) for v in K.frame_points(rig).values())
    return c


def solve(rig, p, rounds=4, iters=12):
    def fit(key, group, step, most):
        for _ in range(iters):
            c = measure(rig, p)[group]
            if abs(c) < REACH:
                return
            # secant step from a small probe
            p[key] += step
            c2 = measure(rig, p)[group]
            p[key] -= step
            slope = (c2 - c) / step
            if abs(slope) < 1e-6:
                return
            p[key] -= max(-most, min(most, c / slope))
    for _ in range(rounds):
        for f in FITS:
            fit(*f)
    if RG.TAIL_ON_GROUND:
        start = p["tail"]
        for _ in range(rounds):
            fit(*TAIL_FIT)
        if abs(measure(rig, p)["tail"]) > 5 * REACH:       # cannot reach the ground: keep it as it was
            p["tail"] = start
    return p


def main():
    rig = A.Rig(materials=False)           # contact needs no textures
    p = solve(rig, dict(RG.LIE))
    c = measure(rig, p)
    print("LIE = {%s}" % ", ".join('"%s": %s' % (k, round(v, 4)) for k, v in p.items()))
    print("contacts:", {k: round(v, 4) for k, v in c.items()})
    errs = RG.lie_contact_errors(c, 0.01)
    print("lying contact:", "OK" if not errs else "; ".join(errs))


if __name__ == "__main__":
    main()
