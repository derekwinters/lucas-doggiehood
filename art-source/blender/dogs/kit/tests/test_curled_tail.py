"""The optional keys for a tail carried curled over the back: TAIL_SWING and
SIT_POSE["tail_on_ground"]. Without them a breed gets exactly the tail it got before.

  python3 art-source/blender/dogs/kit/tests/test_curled_tail.py              (a few seconds)
  python3 -m unittest discover -s art-source/blender/dogs/kit/tests

Needs bpy (==5.3.0), but builds no dog: each case runs in its own process (one breed per process)
with the Golden's breed.py copied into a temporary folder, with or without lines appended that set
the keys, and drives the clips' pose functions on a bare armature (dh_dog_rig.make_armature), with
the leg solver, the baker and the leg targets replaced by stubs (they need a built dog and do not
touch the tail). Every Rig.set call is recorded. Nothing is written into the repository.

  TAIL_SWING                  per world axis multipliers on the tail rotations (tail_1..tail_4) the
                              clips ask for in Idle, Walk, Run, TailWag and PlayBow, and in the "wag"
                              test pose. They are written for a tail carried out behind; a tail curled
                              over the back would be driven into the back by their lifts. The sit's
                              tail (SIT, SIT_POSE, SitDown, SitIdle) and every other bone are unchanged.
  SIT_POSE["tail_on_ground"]  False: the tail is carried in the sit, not laid on the ground. It leaves
                              the sit's ground-contact groups (the model's sit check, solve-sit's fits
                              and the SitDown / SitIdle touches), and the model's sit check instead
                              requires it to stay at least 1 cm clear of the ground.
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

SWING = (0.25, -0.5, 2.0)           # every component differs from 1 (and from each other)
OLD_TOUCH = ["haunches", "hocks", "hind_feet", "front_paws", "tail"]
OLD_FITS = ["drop", "hips", "hock_z", "foot_pitch", "tail"]
TAIL_BONES = ("tail_1", "tail_2", "tail_3", "tail_4")
SWUNG_CLIPS = ("Idle", "Walk", "Run", "TailWag", "PlayBow")
SIT_CLIPS = ("SitDown", "SitIdle")
TOL = 1e-9

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
import solve_sit as SS

B = D.B
arm = RG.make_armature("probe_arm", bpy.context.scene.collection)
for pb in arm.pose.bones:
    pb.rotation_mode = "QUATERNION"
rig = A.Rig.__new__(A.Rig)          # the pose helpers only, no dog built
rig.arm = arm
rig.sit_spec = RG.poses()["sit"]
rec = []
clip = [""]
real_set = A.Rig.set
def rec_set(self, bone, rx=0.0, ry=0.0, rz=0.0):
    rec.append([clip[0], bone, rx, ry, rz])
    return real_set(self, bone, rx, ry, rz)
A.Rig.set = rec_set
# stubs: no dog, no legs, no baking
C.plant_all = lambda rig, tag, k=0.85: None
C.sit_targets = lambda rig: {leg: (Vector((0, 0, 0)), 0.0, 0.0) for leg in A.LEGS}
rig.rest = {leg: {"paw0": Vector((0, 0, 0)), "L": (0.2, 0.2, 0.1), "x": 0.1, "ph": (0, 0, -90, 0)} for leg in A.LEGS}
rig.update = lambda: None
rig.solve_leg = lambda *a, **k: None
rig.turn_world = lambda *a, **k: None
rig.joint = lambda *a, **k: Vector((0, 0, 0.5))
rig.bake = lambda name, n, pose, loop=True: [pose(f) for f in range(n + 1)]
out = {"has_swing": hasattr(B, "TAIL_SWING"), "tail_on_ground": B.SIT_POSE.get("tail_on_ground", True)}
for name, fn in (("Idle", lambda: [C.idle_body(rig, f / 24) for f in range(24)]),
                 ("Walk", lambda: [C.walk_body(rig, f / 24) for f in range(24)]),
                 ("Run", lambda: [C.run_body(rig, f / 24) for f in range(24)]),
                 ("TailWag", lambda: C.make_tail_wag(rig)),
                 ("PlayBow", lambda: C.make_play_bow(rig)),
                 ("SitDown", lambda: C.make_sit_down(rig)),
                 ("SitIdle", lambda: C.make_sit_idle(rig))):
    clip[0] = name
    meta = fn()
    if isinstance(meta, dict) and "touch" in meta:
        out.setdefault("touch", {})[name] = {str(k): v for k, v in meta["touch"].items()}
out["sets"] = rec
P = RG.poses()
out["poses"] = {k: {b: list(d.get("rot", (0, 0, 0))) for b, d in P[k].items() if not b.startswith("_")}
                for k in ("wag", "sit")}
out["sit_touch"] = list(RG.SIT_TOUCH)
out["fits"] = [f[0] for f in SS.FITS]
cases = {"all_down": {"haunches": 0.0, "hocks": 0.002, "hind_feet": -0.003, "front_paws": 0.001, "tail": 0.004,
                      "lowest": -0.003, "lowest_any_part": -0.003},
         "tail_up": {"haunches": 0.0, "hocks": 0.002, "hind_feet": -0.003, "front_paws": 0.001, "tail": 0.20,
                     "lowest": -0.003, "lowest_any_part": -0.003},
         "rump_up_sinks": {"haunches": 0.05, "hocks": 0.002, "hind_feet": -0.003, "front_paws": 0.001, "tail": 0.20,
                           "lowest": -0.02, "lowest_any_part": -0.02}}
out["errors"] = {k: RG.sit_contact_errors(c, 0.01) for k, c in cases.items()}
print("PROBE " + json.dumps(out))
'''


def has_bpy():
    return importlib.util.find_spec("bpy") is not None


@unittest.skipUnless(has_bpy(), "needs bpy==5.3.0")
class CurledTail(unittest.TestCase):
    cache = {}

    def probe(self, extra=""):
        """The probe's output for the Golden's breed.py with `extra` appended."""
        if extra in self.cache:
            return self.cache[extra]
        with tempfile.TemporaryDirectory(prefix="dh_curled_tail_") as tmp:
            shutil.copy(os.path.join(GOLDEN, "breed.py"), tmp)
            if extra:
                with open(os.path.join(tmp, "breed.py"), "a") as fh:
                    fh.write("\n" + extra + "\n")
            r = subprocess.run([sys.executable, "-c", PROBE % {"kit": KIT}], capture_output=True, text=True,
                               timeout=300, env=dict(os.environ, DH_BREED=tmp))
        out = r.stdout + r.stderr
        self.assertEqual(r.returncode, 0, out[-3000:])
        line = [ln for ln in r.stdout.splitlines() if ln.startswith("PROBE ")]
        self.assertEqual(len(line), 1, out[-3000:])
        got = json.loads(line[0][len("PROBE "):])
        self.cache[extra] = got
        return got

    def base(self):
        return self.probe("")

    def swung(self):
        return self.probe("TAIL_SWING = %r" % (SWING,))

    def carried(self):
        return self.probe('SIT_POSE["tail_on_ground"] = False')

    # ------------------------------------------------------------------ TAIL_SWING
    def test_breed_without_tail_swing_is_unchanged(self):
        got = self.base()
        self.assertFalse(got["has_swing"])
        # the wag test pose and the sit keep the values they always had
        self.assertEqual(got["poses"]["wag"]["tail_1"], [8, 0, 14])
        self.assertEqual(got["poses"]["wag"]["tail_4"], [3, 0, 8])

    def test_tail_swing_scales_the_clips_tail_rotations_only(self):
        a, b = self.base()["sets"], self.swung()["sets"]
        self.assertEqual(len(a), len(b))
        scaled = 0
        for (ca, bone, *ra), (cb, bone_b, *rb) in zip(a, b):
            self.assertEqual((ca, bone), (cb, bone_b))
            if bone in TAIL_BONES and ca in SWUNG_CLIPS:
                want = [ra[i] * SWING[i] for i in range(3)]
                scaled += any(abs(v) > 1e-6 for v in ra)
            else:
                want = ra
            for i in range(3):
                self.assertAlmostEqual(rb[i], want[i], delta=TOL, msg="%s %s axis %d" % (ca, bone, i))
        self.assertGreater(scaled, 100)
        for c in SWUNG_CLIPS:
            self.assertTrue(any(r[0] == c and r[1] in TAIL_BONES for r in a), "%s sets no tail bone" % c)

    def test_tail_swing_leaves_the_sit_alone(self):
        a, b = self.base(), self.swung()
        sa = [r for r in a["sets"] if r[0] in SIT_CLIPS]
        sb = [r for r in b["sets"] if r[0] in SIT_CLIPS]
        self.assertEqual(sa, sb)
        self.assertTrue(any(r[1] == "tail_1" for r in sa))
        self.assertEqual(a["poses"]["sit"], b["poses"]["sit"])

    def test_tail_swing_scales_the_wag_test_pose(self):
        a, b = self.base()["poses"]["wag"], self.swung()["poses"]["wag"]
        for bone, rot in a.items():
            want = [rot[i] * SWING[i] for i in range(3)] if bone in TAIL_BONES else rot
            for i in range(3):
                self.assertAlmostEqual(b[bone][i], want[i], delta=TOL, msg="%s axis %d" % (bone, i))

    # ------------------------------------------------------------------ SIT_POSE["tail_on_ground"]
    def test_breed_without_the_key_lays_the_tail_on_the_ground(self):
        got = self.base()
        self.assertTrue(got["tail_on_ground"])
        self.assertEqual(got["sit_touch"], OLD_TOUCH)
        self.assertEqual(got["fits"], OLD_FITS)
        self.assertEqual(got["touch"]["SitDown"], {"54": OLD_TOUCH})
        self.assertEqual(got["touch"]["SitIdle"], {"0": OLD_TOUCH, "22": OLD_TOUCH, "67": OLD_TOUCH})
        e = got["errors"]
        self.assertEqual(e["all_down"], [])
        self.assertEqual(e["tail_up"], ["sit: tail at z=+0.2000 (more than 1 cm off the ground)"])
        self.assertEqual(e["rump_up_sinks"], ["sit: haunches at z=+0.0500 (more than 1 cm off the ground)",
                                              "sit: tail at z=+0.2000 (more than 1 cm off the ground)",
                                              "sit: lowest at z=-0.0200 sinks below the ground",
                                              "sit: lowest_any_part at z=-0.0200 sinks below the ground"])

    def test_a_carried_tail_leaves_the_ground_contacts(self):
        got = self.carried()
        self.assertFalse(got["tail_on_ground"])
        want = [g for g in OLD_TOUCH if g != "tail"]
        self.assertEqual(got["sit_touch"], want)
        self.assertEqual(got["fits"], [f for f in OLD_FITS if f != "tail"])
        self.assertEqual(got["touch"]["SitDown"], {"54": want})
        self.assertEqual(got["touch"]["SitIdle"], {"0": want, "22": want, "67": want})

    def test_a_carried_tail_must_stay_clear_of_the_ground(self):
        e = self.carried()["errors"]
        self.assertEqual(e["tail_up"], [])
        self.assertEqual(e["all_down"], ["sit: tail at z=+0.0040 (carried, but within 1 cm of the ground)"])
        self.assertEqual(e["rump_up_sinks"], ["sit: haunches at z=+0.0500 (more than 1 cm off the ground)",
                                              "sit: lowest at z=-0.0200 sinks below the ground",
                                              "sit: lowest_any_part at z=-0.0200 sinks below the ground"])

    def test_a_carried_tail_keeps_the_sit_poses(self):
        self.assertEqual(self.base()["poses"]["sit"], self.carried()["poses"]["sit"])
        self.assertEqual([r for r in self.base()["sets"] if r[0] in SIT_CLIPS],
                         [r for r in self.carried()["sets"] if r[0] in SIT_CLIPS])


if __name__ == "__main__":
    unittest.main(verbosity=2)
