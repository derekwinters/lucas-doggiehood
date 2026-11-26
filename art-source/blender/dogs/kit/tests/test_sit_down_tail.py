"""The optional SIT_DOWN["tail_lift"] and SIT_DOWN["tail_lag"] keys set how the tail clears the
ground during SitDown; without them a breed gets exactly the SitDown tail it got before.

  python3 art-source/blender/dogs/kit/tests/test_sit_down_tail.py            (a few seconds)
  python3 -m unittest discover -s art-source/blender/dogs/kit/tests

Needs bpy (==5.3.0), but builds no dog: each case runs in its own process (one breed per process)
with the Golden's breed.py copied into a temporary folder, with or without lines appended that set
the keys, and poses a bare armature (dh_dog_rig.make_armature).

  tail_lift  degrees the tail swings up, clear of the ground, on the way down: tail_1 gets
             tail_lift * sin(pi * s) on top of its sit progress (s: 0 standing .. 1 sat). Default 18.
  tail_lag   frames the tail's sit progress trails the body's: it starts tail_lag frames after the
             body (frame 4) and still finishes when it did before (frame 48). Default 6.

The lag is read off make_sit_down() with its leg solver and baker replaced by recorders (the
clip's leg work needs a built dog and does not touch the tail); the lift off sit_body() itself.
Nothing is written into the repository.
"""
import importlib.util
import json
import os
import shutil
import subprocess
import sys
import tempfile
import unittest

TESTS = os.path.dirname(os.path.abspath(__file__))
KIT = os.path.dirname(TESTS)
GOLDEN = os.path.join(os.path.dirname(KIT), "golden-retriever")

BODY_T0, BODY_T1 = 4, 46        # make_sit_down's body progress frames
OLD_LIFT_DEG = 18               # SitDown's tail lift before the key existed
OLD_LAG = 6                     # ... and its tail lag
LIFT, LAG = 60, 0               # a low-carried tail (as the German Shepherd's)
S_PROBE = (0.0, 0.25, 0.5, 0.75, 1.0)
TOL = 1e-6

PROBE = r'''
import json, math, sys
sys.path.insert(0, %(kit)r)
import dh_breed
dh_breed.get()
import bpy
from mathutils import Vector
import dh_dog as D
import dh_dog_rig as RG
import dh_dog_anim as A
import dh_dog_clips as C

B = D.B
arm = RG.make_armature("probe_arm", bpy.context.scene.collection)
rig = A.Rig.__new__(A.Rig)          # the pose helpers only, no dog built
rig.arm = arm
for pb in arm.pose.bones:
    pb.rotation_mode = "QUATERNION"
rig.sit_spec = RG.poses()["sit"]
sp = rig.sit_spec
out = {"sit_down": dict(B.SIT_DOWN), "lift": {}}

# ---- the lift: tail_1 from sit_body at body progress s (tail progress = s)
for s in %(s_probe)r:
    C.sit_body(rig, s, s_head=s, s_tail=s)
    th = sp["hips"]["rot"][0] * s
    base = -th + (sp["tail_1"]["rot"][0] + sp["hips"]["rot"][0]) * s     # no lift
    out["lift"][repr(s)] = dict(got=list(arm.pose.bones["tail_1"].rotation_quaternion),
                                base_deg=base, sin=math.sin(math.pi * s),
                                q={str(d): list(rig.q_world("tail_1", base + d * math.sin(math.pi * s)))
                                   for d in (%(old_lift)r, %(lift)r)})

# ---- the lag: the tail progress make_sit_down asks sit_body for, frame by frame
tails = []
real_sit_body = C.sit_body
C.sit_body = lambda rig, s, s_head=None, s_tail=None: tails.append(s_tail)
C.sit_targets = lambda rig: {leg: (Vector((0, 0, 0)), 0.0, 0.0) for leg in A.LEGS}
rig.rest = {leg: {"paw0": Vector((0, 0, 0))} for leg in A.LEGS}
rig.update = lambda: None
rig.solve_leg = lambda *a, **k: None
rig.bake = lambda name, n, pose, loop: [pose(f) for f in range(n + 1)]
meta = C.make_sit_down(rig)
C.sit_body = real_sit_body
out["frames"] = meta["frames"]
out["s_tail"] = tails
print("PROBE " + json.dumps(out))
'''


def has_bpy():
    return importlib.util.find_spec("bpy") is not None


def smooth(x):
    x = min(1.0, max(0.0, x))
    return x * x * (3 - 2 * x)


def close(a, b, tol=TOL):
    return all(abs(x - y) <= tol for x, y in zip(a, b))


@unittest.skipUnless(has_bpy(), "needs bpy==5.3.0")
class SitDownTail(unittest.TestCase):
    cache = {}

    def probe(self, sit_down_keys):
        """The probe's output for the Golden's breed.py, with sit_down_keys merged into SIT_DOWN."""
        key = json.dumps(sit_down_keys, sort_keys=True)
        if key in self.cache:
            return self.cache[key]
        with tempfile.TemporaryDirectory(prefix="dh_sit_down_tail_") as tmp:
            shutil.copy(os.path.join(GOLDEN, "breed.py"), tmp)
            if sit_down_keys:
                with open(os.path.join(tmp, "breed.py"), "a") as fh:
                    fh.write("\nSIT_DOWN.update(%r)\n" % (sit_down_keys,))
            code = PROBE % {"kit": KIT, "s_probe": S_PROBE, "old_lift": OLD_LIFT_DEG, "lift": LIFT}
            r = subprocess.run([sys.executable, "-c", code], capture_output=True, text=True,
                               timeout=300, env=dict(os.environ, DH_BREED=tmp))
        out = r.stdout + r.stderr
        self.assertEqual(r.returncode, 0, out[-3000:])
        line = [ln for ln in r.stdout.splitlines() if ln.startswith("PROBE ")]
        self.assertEqual(len(line), 1, out[-3000:])
        got = json.loads(line[0][len("PROBE "):])
        self.cache[key] = got
        return got

    def old_s_tail(self, f):
        return smooth((f - BODY_T0 - OLD_LAG) / (BODY_T1 - BODY_T0 - 4))

    # ------------------------------------------------------------------ tail_lift
    def test_breed_without_tail_lift_gets_the_old_lift(self):
        got = self.probe({})
        self.assertNotIn("tail_lift", got["sit_down"])
        for s, d in got["lift"].items():
            self.assertTrue(close(d["got"], d["q"][str(OLD_LIFT_DEG)]), "tail_1 at s=%s is not the old lift" % s)

    def test_tail_lift_sets_how_far_the_tail_swings_clear(self):
        got = self.probe({"tail_lift": LIFT})
        for s, d in got["lift"].items():
            self.assertTrue(close(d["got"], d["q"][str(LIFT)]), "tail_1 at s=%s ignores tail_lift" % s)
        mid = got["lift"][repr(0.5)]
        self.assertFalse(close(mid["got"], mid["q"][str(OLD_LIFT_DEG)]))

    # ------------------------------------------------------------------ tail_lag
    def test_breed_without_tail_lag_gets_the_old_lag(self):
        got = self.probe({})
        self.assertNotIn("tail_lag", got["sit_down"])
        self.assertEqual(len(got["s_tail"]), got["frames"])
        for f, s in enumerate(got["s_tail"]):
            self.assertAlmostEqual(s, self.old_s_tail(f), places=9, msg="frame %d" % f)

    def test_tail_lag_sets_how_long_the_tail_trails_the_hips(self):
        got = self.probe({"tail_lag": LAG})
        st = got["s_tail"]
        start = BODY_T0 + LAG
        for f in range(start + 1):
            self.assertEqual(st[f], 0.0, "the tail moves before frame %d (frame %d)" % (start, f))
        self.assertGreater(st[start + 1], 0.0, "the tail does not start at frame %d" % (start + 1))
        self.assertGreater(st[BODY_T0 + OLD_LAG], 0.0, "with less lag the tail should be under way")
        end = BODY_T1 + 2                 # where the tail finished before the key existed
        self.assertLess(st[end - 1], 1.0)
        for f in range(end, got["frames"]):
            self.assertEqual(st[f], 1.0, "the tail is not sat by frame %d (frame %d)" % (end, f))

    def test_both_keys_together(self):
        got = self.probe({"tail_lift": LIFT, "tail_lag": LAG})
        mid = got["lift"][repr(0.5)]
        self.assertTrue(close(mid["got"], mid["q"][str(LIFT)]))
        self.assertEqual(got["s_tail"][BODY_T0 + LAG], 0.0)
        self.assertGreater(got["s_tail"][BODY_T0 + LAG + 1], 0.0)


if __name__ == "__main__":
    unittest.main(verbosity=2)
