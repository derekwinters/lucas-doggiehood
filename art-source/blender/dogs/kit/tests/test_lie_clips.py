"""The lying clips (#933): LieDown (standing -> the breed's LIE pose, once) and LieIdle (a loop in
the LIE pose), the LIE pose itself and the lying-contact check.

  python3 art-source/blender/dogs/kit/tests/test_lie_clips.py               (a few seconds)
  python3 -m unittest discover -s art-source/blender/dogs/kit/tests

The data tests need no Blender. The pose and clip tests need bpy (==5.3.0) but build no dog: they
run in their own process with the Golden's breed.py and pose a bare armature
(dh_dog_rig.make_armature), with the leg solver and the leg targets replaced by stubs (they need a
built dog; the legs' contact is checked on every breed's skinned mesh by `build_breed.py <breed>
motion`). Nothing is written into the repository.

  LIE (breed.py)   the lying (sphinx) pose: the hips tilt by LIE["hips"] and the root drops by
                   LIE["drop"]; each front leg runs back from the shoulder to the elbow at
                   LIE["elbow_z"], forward along the ground to the wrist at LIE["wrist_z"], then the
                   pastern lies forward, level, and the paw is flat; each hind leg folds as in the
                   sit (stifle at LIE["stifle_z"], hock at LIE["hock_z"], rear foot forward along the
                   ground at LIE["foot_pitch"]); the neck turns back by the hip tilt so the head
                   keeps its standing angle; the tail is LIE["tail"], ["tail_2"], ["tail_3"].
"""
import importlib.util
import json
import os
import subprocess
import sys
import unittest

TESTS = os.path.dirname(os.path.abspath(__file__))
KIT = os.path.dirname(TESTS)
DOGS = os.path.dirname(KIT)
GOLDEN = os.path.join(DOGS, "golden-retriever")
sys.path.insert(0, KIT)

import dh_breed  # noqa: E402

BREEDS = ("beagle", "chihuahua", "french-bulldog", "frenchton", "german-shepherd", "golden-retriever",
          "labrador", "puggle")
LIE_KEYS = {"hips", "drop", "elbow_z", "wrist_z", "stifle_z", "hock_z", "foot_pitch", "tail", "tail_2", "tail_3"}
# the groups that rest on the ground in the lie; the tail is not one (a short or curled tail cannot
# reach it lying down), it only must not sink
LIE_TOUCH = ["chest", "elbows", "front_paws", "hocks", "hind_feet"]
SLOW_BREATH_FRAMES = 60         # a resting breath takes at least 2 s (Idle breathes every 1.33 s)
TILT = 7.0                      # a hip tilt the probe also poses (a breed's LIE may have none)
DEG = 1e-4
M = 1e-6

PROBE = r'''
import json, math, sys
sys.path.insert(0, %(kit)r)
import dh_breed
dh_breed.use(%(breed)r)
import bpy
from mathutils import Vector
import dh_dog as D
import dh_dog_rig as RG
import dh_dog_anim as A
import dh_dog_clips as C

B = D.B
arm = RG.make_armature("probe_arm", bpy.context.scene.collection)
for pb in arm.pose.bones:
    pb.rotation_mode = "QUATERNION"
rig = A.Rig.__new__(A.Rig)          # the pose helpers only, no dog built
rig.arm = arm
rig.sit_spec = RG.poses()["sit"]
real_update = rig.update
out = {"lie": dict(B.LIE)}


def world():
    """Posed world joints {bone: [head y, head z, tail y, tail z]} and bone directions."""
    real_update()
    j = {}
    for pb in arm.pose.bones:
        h, t = arm.matrix_world @ pb.head, arm.matrix_world @ pb.tail
        j[pb.name] = [h.y, h.z, t.y, t.z]
    return j


def quats():
    return {pb.name: list(pb.rotation_quaternion) for pb in arm.pose.bones}, list(arm.pose.bones["root"].location)


rig.reset()
out["rest"] = world()
rig.apply_spec(RG.lie_spec())
out["posed"] = world()
out["spec_q"] = quats()
rig.reset()
tilted = dict(B.LIE, hips=B.LIE["hips"] + %(tilt)r)
rig.apply_spec(RG.lie_spec(tilted))
out["tilted"] = world()
out["tilted_lie"] = tilted
rig.reset()
out["touch"] = list(RG.LIE_TOUCH)

# the clips, with no dog: the legs are stubbed, the baker poses every frame and records it
C.lie_targets = lambda rig: {leg: (Vector((0, 0, 0)), 0.0, 0.0) for leg in A.LEGS}
rig.rest = {leg: {"paw0": Vector((0, 0, 0))} for leg in A.LEGS}
rig.update = lambda: None
rig.solve_leg = lambda *a, **k: None
frames = {}


def bake(name, n, pose, loop=True):
    frames[name] = []
    for f in range(n + 1):
        rig.reset()
        pose(f)
        frames[name].append(quats())
rig.bake = bake
out["metas"] = {}
for name in ("LieDown", "LieIdle"):
    m = C.make(rig, name)
    out["metas"][name] = {k: m[k] for k in ("name", "frames", "loop", "ground_speed")}
    out["metas"][name]["touch"] = {str(k): v for k, v in m.get("touch", {}).items()}
    out["metas"][name]["plants"] = m.get("plants")
out["frames"] = frames
out["order"] = list(C.ORDER)
cases = {"all_down": {"chest": 0.0, "elbows": 0.002, "front_paws": -0.003, "hocks": 0.001, "hind_feet": 0.0,
                      "tail": 0.004, "lowest": -0.003},
         "tail_up": {"chest": 0.0, "elbows": 0.002, "front_paws": -0.003, "hocks": 0.001, "hind_feet": 0.0,
                     "tail": 0.20, "lowest": -0.003},
         "chest_up": {"chest": 0.03, "elbows": 0.002, "front_paws": -0.003, "hocks": 0.001, "hind_feet": 0.0,
                      "tail": 0.004, "lowest": -0.003},
         "elbow_sinks": {"chest": 0.0, "elbows": -0.02, "front_paws": -0.003, "hocks": 0.05, "hind_feet": 0.0,
                         "tail": 0.004, "lowest": -0.02}}
out["errors"] = {k: RG.lie_contact_errors(c, 0.01) for k, c in cases.items()}
print("PROBE " + json.dumps(out))
'''


def has_bpy():
    return importlib.util.find_spec("bpy") is not None


class LieIsBreedData(unittest.TestCase):
    """Invariant: the kit holds no breed's numbers. The lying pose is each breed's LIE."""

    def test_lie_is_a_required_breed_field(self):
        self.assertIn("LIE", dh_breed.REQUIRED)

    def test_every_breed_defines_its_lie(self):
        for b in BREEDS:
            with self.subTest(breed=b):
                lie = dh_breed.load(os.path.join(DOGS, b)).LIE
                self.assertEqual(set(lie), LIE_KEYS)
                self.assertLess(lie["drop"], 0.0)
                for k in ("elbow_z", "wrist_z", "stifle_z"):
                    self.assertGreater(lie[k], 0.0, k)

    def test_build_breed_has_a_solve_lie_step(self):
        import build_breed
        plan = build_breed.plan(["golden-retriever", "solve-lie"])
        self.assertEqual([s for s, _ in plan], ["solve-lie"])
        self.assertTrue(plan[0][1][1].endswith("solve_lie.py"))


@unittest.skipUnless(has_bpy(), "needs bpy==5.3.0")
class LieClips(unittest.TestCase):
    cache = None

    def probe(self):
        if LieClips.cache is None:
            r = subprocess.run([sys.executable, "-c", PROBE % {"kit": KIT, "breed": GOLDEN, "tilt": TILT}],
                               capture_output=True, text=True, timeout=300)
            out = r.stdout + r.stderr
            self.assertEqual(r.returncode, 0, out[-3000:])
            line = [ln for ln in r.stdout.splitlines() if ln.startswith("PROBE ")]
            self.assertEqual(len(line), 1, out[-3000:])
            LieClips.cache = json.loads(line[0][len("PROBE "):])
        return LieClips.cache

    # ------------------------------------------------------------------ the LIE pose
    def test_lie_pose_lowers_the_body_and_tilts_the_hips(self):
        got = self.probe()
        self.check_lowered_and_tilted(got["lie"], got["posed"], got["rest"])
        self.check_lowered_and_tilted(got["tilted_lie"], got["tilted"], got["rest"])

    def check_lowered_and_tilted(self, lie, p, r):
        # the hips joint drops by LIE["drop"] (it is the pivot of the hip tilt) ...
        self.assertAlmostEqual(p["hips"][1] - r["hips"][1], lie["drop"], delta=M)
        # ... and the body turns about it by LIE["hips"] (+ = nose down)
        import math
        dy, dz = p["spine_3"][0] - p["hips"][0], p["spine_3"][1] - p["hips"][1]
        ry, rz = r["spine_3"][0] - r["hips"][0], r["spine_3"][1] - r["hips"][1]
        turn = math.degrees(math.atan2(dz, dy) - math.atan2(rz, ry))
        self.assertAlmostEqual((turn + 180) % 360 - 180, lie["hips"], delta=1e-3)

    def test_front_legs_lie_along_the_ground(self):
        got = self.probe()
        self.check_front(got["lie"], got["posed"], got["rest"])
        self.check_front(got["tilted_lie"], got["tilted"], got["rest"])

    def check_front(self, lie, p, r):
        for s in "LR":
            sh, el, wr, paw = (p["front_%s_%s" % (n, s)] for n in ("shoulder", "elbow", "wrist", "paw"))
            self.assertAlmostEqual(el[1], lie["elbow_z"], delta=M, msg="elbow height")
            self.assertGreater(el[0], sh[0], "the elbow is behind the shoulder")
            self.assertAlmostEqual(wr[1], lie["wrist_z"], delta=M, msg="wrist height")
            self.assertLess(wr[0], el[0], "the forearm runs forward from the elbow")
            self.assertAlmostEqual(paw[1], lie["wrist_z"], delta=M, msg="the pastern lies level")
            self.assertLess(paw[0], wr[0], "the pastern points forward")
            rp = r["front_paw_%s" % s]
            self.assertAlmostEqual(paw[2] - paw[0], rp[2] - rp[0], delta=M, msg="the paw is flat (y)")
            self.assertAlmostEqual(paw[3] - paw[1], rp[3] - rp[1], delta=M, msg="the paw is flat (z)")

    def test_hind_legs_fold_as_in_the_sit(self):
        got = self.probe()
        self.check_hind(got["lie"], got["posed"])
        self.check_hind(got["tilted_lie"], got["tilted"])

    def check_hind(self, lie, p):
        for s in "LR":
            self.assertAlmostEqual(p["back_stifle_%s" % s][1], lie["stifle_z"], delta=M, msg="stifle height")
            self.assertAlmostEqual(p["back_hock_%s" % s][1], lie["hock_z"], delta=M, msg="hock height")
            self.assertLess(p["back_stifle_%s" % s][0], p["back_hip_%s" % s][0], "the thigh runs forward")
            self.assertGreater(p["back_hock_%s" % s][0], p["back_stifle_%s" % s][0], "the gaskin runs back")
            self.assertLess(p["back_foot_%s" % s][0], p["back_hock_%s" % s][0], "the rear foot runs forward")

    def test_head_keeps_its_standing_angle(self):
        got = self.probe()
        r = got["rest"]["head"]
        for p in (got["posed"]["head"], got["tilted"]["head"]):
            self.assertAlmostEqual(p[2] - p[0], r[2] - r[0], delta=M)
            self.assertAlmostEqual(p[3] - p[1], r[3] - r[1], delta=M)

    def test_lying_contact_groups(self):
        self.assertEqual(self.probe()["touch"], LIE_TOUCH)

    def test_lying_contact_check(self):
        e = self.probe()["errors"]
        self.assertEqual(e["all_down"], [])
        self.assertEqual(e["tail_up"], [])
        self.assertEqual(e["chest_up"], ["lie: chest at z=+0.0300 (more than 1 cm off the ground)"])
        self.assertEqual(e["elbow_sinks"], ["lie: elbows at z=-0.0200 (more than 1 cm off the ground)",
                                            "lie: hocks at z=+0.0500 (more than 1 cm off the ground)",
                                            "lie: lowest at z=-0.0200 sinks below the ground"])

    # ------------------------------------------------------------------ the clips
    def test_lie_clips_are_registered(self):
        order = self.probe()["order"]
        self.assertIn("LieDown", order)
        self.assertIn("LieIdle", order)
        self.assertNotIn("RestIdle", order)

    def test_lie_down_goes_once_from_standing_to_the_lie(self):
        got = self.probe()
        m = got["metas"]["LieDown"]
        self.assertFalse(m["loop"])
        self.assertEqual(m["ground_speed"], 0.0)
        n = m["frames"]
        self.assertEqual(m["touch"], {str(n): LIE_TOUCH})
        fr = got["frames"]["LieDown"]
        self.assertEqual(len(fr), n + 1)
        q_lie, root_lie = got["spec_q"]
        q0, root0 = fr[0]
        for b in ("hips", "spine_1", "spine_2", "spine_3", "neck", "head", "tail_1"):
            for a, w in zip(q0[b], (1, 0, 0, 0)):
                self.assertAlmostEqual(a, w, delta=DEG, msg="frame 0 %s is standing" % b)
        for a in root0:
            self.assertAlmostEqual(a, 0.0, delta=M)
        qn, rootn = fr[n]
        for b in ("hips", "neck", "head", "tail_1", "tail_2", "tail_3"):
            for a, w in zip(qn[b], q_lie[b]):
                self.assertAlmostEqual(a, w, delta=DEG, msg="last frame %s is the LIE" % b)
        for a, w in zip(rootn, root_lie):
            self.assertAlmostEqual(a, w, delta=M)

    def test_lie_idle_loops_in_the_lie(self):
        got = self.probe()
        m = got["metas"]["LieIdle"]
        self.assertTrue(m["loop"])
        n = m["frames"]
        self.assertEqual(sorted(m["plants"]), ["FL", "FR", "HL", "HR"])
        for leg, ivs in m["plants"].items():
            self.assertEqual(ivs, [[0, n]], leg)
        self.assertIn("0", m["touch"])
        for f, groups in m["touch"].items():
            self.assertEqual(groups, LIE_TOUCH, "frame %s" % f)
        fr = got["frames"]["LieIdle"]
        # the loop closes on its first frame: frame n is posed as frame 0
        q0, r0 = fr[0]
        qn, rn = fr[n]
        for b in q0:
            for a, w in zip(q0[b], qn[b]):
                self.assertAlmostEqual(a, w, delta=DEG, msg="%s at frame %d vs 0" % (b, n))
        for a, w in zip(r0, rn):
            self.assertAlmostEqual(a, w, delta=M)
        # frame 0 is the LIE (the body and tail; the legs are re-solved to it)
        q_lie, root_lie = got["spec_q"]
        for b in ("hips", "tail_1", "tail_2", "tail_3"):
            for a, w in zip(q0[b], q_lie[b]):
                self.assertAlmostEqual(a, w, delta=DEG, msg="frame 0 %s is the LIE" % b)

    def test_lie_idle_breathes_slowly(self):
        got = self.probe()
        n = got["metas"]["LieIdle"]["frames"]
        w = [fr[0]["spine_2"][1] for fr in got["frames"]["LieIdle"]][:n]
        self.assertGreater(max(w) - min(w), 1e-4, "spine_2 does not breathe")
        peaks = sum(1 for f in range(n) if w[f] > w[f - 1] and w[f] >= w[(f + 1) % n])
        self.assertGreaterEqual(peaks, 1)
        self.assertLessEqual(peaks, n // SLOW_BREATH_FRAMES, "breathing faster than one breath per 2 s")

    def test_lie_idle_moves_the_tail_or_an_ear_now_and_then(self):
        got = self.probe()
        fr = got["frames"]["LieIdle"]
        n = got["metas"]["LieIdle"]["frames"]
        moved = []
        for b in ("ear_L_1", "ear_R_1", "tail_1"):
            q0 = fr[0][0][b]
            d = [max(abs(a - c) for a, c in zip(fr[f][0][b], q0)) for f in range(n)]
            still = sum(1 for x in d if x < 1e-6)
            if max(d) > 0.02 and still >= n // 2:       # over 2 deg, and still for half the loop
                moved.append(b)
        self.assertTrue(moved, "no occasional ear or tail move")


if __name__ == "__main__":
    unittest.main(verbosity=2)
