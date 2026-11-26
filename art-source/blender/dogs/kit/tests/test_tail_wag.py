"""Invariant (#895): the wag lives in the tail.

  python3 art-source/blender/dogs/kit/tests/test_tail_wag.py              (about 20 s)
  python3 -m unittest discover -s art-source/blender/dogs/kit/tests

During TailWag the torso stays essentially still: only the tail chain swings side to side; the
hips and spine may follow with at most a small fraction of the tail's motion, and the body does
not bounce. This bakes the kit's TailWag clip on the Golden Retriever's rig (in a subprocess, one
breed per process) and measures, over every frame of the loop:
  - each torso bone's (hips, spine_1-3) peak-to-peak world rotation, in degrees,
  - the root's peak-to-peak height, in metres (the body bounce),
  - tail_1's peak-to-peak world rotation and the tail tip's side-to-side sweep.
Nothing is written into the repository.
"""
import importlib.util
import json
import math
import os
import subprocess
import sys
import unittest

TESTS = os.path.dirname(os.path.abspath(__file__))
KIT = os.path.dirname(TESTS)
GOLDEN = os.path.join(os.path.dirname(KIT), "golden-retriever")

TORSO = ("hips", "spine_1", "spine_2", "spine_3")
TORSO_MAX_DEG = 2.0          # peak-to-peak world rotation of any torso bone: a barely-there follow
TORSO_TAIL_FRACTION = 0.05   # ... and at most this fraction of tail_1's own swing
ROOT_MAX_BOUNCE_M = 0.001    # peak-to-peak root height: no bounce
TAIL_MIN_DEG = 35.0          # tail_1 still swings at least as hard as before (#895)
TAIL_TIP_MIN_SWEEP_M = 0.50  # and the tip sweeps wide side to side


def measure(breed):
    """Bake TailWag for `breed` and return the motion ranges (run in a bpy process)."""
    sys.path.insert(0, KIT)
    import dh_breed
    dh_breed.use(breed)
    import bpy
    import dh_dog_anim as A
    import dh_dog_clips as C

    rig = A.Rig()
    meta = C.make_tail_wag(rig)
    act = bpy.data.actions["TailWag"]
    rots = {b: [] for b in TORSO + ("tail_1",)}
    root_z, tip_x = [], []
    for f in range(meta["frames"] + 1):
        rig.play(act, f)
        rig.update()
        for b in rots:
            rots[b].append(rig.delta(b).to_quaternion())
        root_z.append(rig.joint("root").z)
        tip_x.append(rig.joint("tail_4", tail=True).x)
    rig.arm.animation_data.action = None

    def p2p_deg(qs):
        worst = 0.0
        for i, a in enumerate(qs):
            for b in qs[i + 1:]:
                d = math.degrees(a.rotation_difference(b).angle)
                worst = max(worst, min(d, 360 - d))
        return worst

    return dict(rot_p2p_deg={b: p2p_deg(q) for b, q in rots.items()},
                root_bounce_m=max(root_z) - min(root_z),
                tail_tip_sweep_m=max(tip_x) - min(tip_x))


def has_bpy():
    return importlib.util.find_spec("bpy") is not None


@unittest.skipUnless(has_bpy(), "needs bpy==5.3.0")
class WagLivesInTheTail(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        r = subprocess.run([sys.executable, os.path.abspath(__file__), "--measure", GOLDEN],
                           capture_output=True, text=True, timeout=600)
        out = r.stdout + r.stderr
        assert r.returncode == 0, out[-4000:]
        line = [ln for ln in r.stdout.splitlines() if ln.startswith("TAILWAG ")][-1]
        cls.m = json.loads(line[len("TAILWAG "):])
        print("\nTailWag motion:", json.dumps(cls.m, indent=1))

    def test_torso_stays_essentially_still(self):
        tail = self.m["rot_p2p_deg"]["tail_1"]
        for b in TORSO:
            r = self.m["rot_p2p_deg"][b]
            self.assertLessEqual(r, TORSO_MAX_DEG, "%s swings %.2f deg" % (b, r))
            self.assertLessEqual(r, TORSO_TAIL_FRACTION * tail,
                                 "%s swings %.2f deg, tail_1 %.2f deg" % (b, r, tail))

    def test_body_does_not_bounce(self):
        self.assertLessEqual(self.m["root_bounce_m"], ROOT_MAX_BOUNCE_M)

    def test_tail_still_wags_hard(self):
        self.assertGreaterEqual(self.m["rot_p2p_deg"]["tail_1"], TAIL_MIN_DEG)
        self.assertGreaterEqual(self.m["tail_tip_sweep_m"], TAIL_TIP_MIN_SWEEP_M)


if __name__ == "__main__":
    if len(sys.argv) == 3 and sys.argv[1] == "--measure":
        print("TAILWAG " + json.dumps(measure(sys.argv[2])))
    else:
        unittest.main(verbosity=2)
