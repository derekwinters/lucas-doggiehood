"""The Swim and Shake clips (#934) and the Swim water-line check.

  python3 art-source/blender/dogs/kit/tests/test_swim_shake.py              (about 1 min)
  python3 -m unittest discover -s art-source/blender/dogs/kit/tests

Needs bpy (==5.3.0). Every case runs in its own process (one breed per process) with the Golden's
breed.py copied into a temporary folder, with its SWIM line removed or replaced, and nothing is
written into the repository.

  Swim    loop: a dog-paddle in place. The front legs paddle alternately under the chest, the hind
          legs kick, the head is held up and the tail streams out behind as a rudder. No paw is
          planted: the clip is checked against the breed's water line, not the ground.
  Shake   one-shot, about 1 s: starts and ends standing with all four paws planted throughout; a
          wave of roll runs from the head through the shoulders, ribs and hips to the tail, and
          the ears flap. Every ear and tail rotation goes through the kit's EARS["swing"] and
          TAIL_SWING scaling, so no breed needs its own code.
  SWIM    the optional breed key {"water_z": m, "paddle": m}: the water line's height in the
          clip's frame and the front paws' half stroke (fore and aft). Leave it out and the kit
          derives both from the breed's own rig (the water line at the base of the neck, the
          stroke from the front leg's height), so a breed without it still builds; every other
          clip is exactly as before either way.

Invariant - Swim is checked against the water line, not the ground: the head and nose (head and
jaw skin, nose, eyes, tongue) stay above the breed's water line in every frame, and the clip loops
on its first frame. Shake keeps the normal ground, contact and foot-slide checks.

Three probes:
  - the clips' pose functions on a bare armature (dh_dog_rig.make_armature) with the leg solver,
    the baker and the planting replaced by stubs; every final rotation (Rig.q_world, after the
    EARS["swing"] / TAIL_SWING scaling) and every leg target is recorded (a few seconds each);
  - dh_dog_check.check_clip on made-up frames, for the water-line check itself (a few seconds);
  - the Golden built for real, Swim and Shake baked and checked on the skinned mesh (about 30 s).
"""
import importlib.util
import json
import os
import re
import shutil
import subprocess
import sys
import tempfile
import unittest

TESTS = os.path.dirname(os.path.abspath(__file__))
KIT = os.path.dirname(TESTS)
GOLDEN = os.path.join(os.path.dirname(KIT), "golden-retriever")

EAR_SWING = (-1.0, -0.5, 2.0)       # every component differs from 1 (and from each other)
TAIL_SWING = (0.25, -0.5, 2.0)
WATER_Z, PADDLE = 0.777, 0.0612     # a made-up SWIM, distinct from any default
OTHER_CLIPS = ("Idle", "Walk", "Run", "TailWag", "PlayBow", "SitDown", "SitIdle")
WAVE = ("head", "neck", "spine_3", "spine_2", "spine_1", "hips", "tail_1")   # head to tail
EAR_BONES = ("ear_L_1", "ear_L_2", "ear_R_1", "ear_R_2")
TAIL_BONES = ("tail_1", "tail_2", "tail_3", "tail_4")
ONSET_DEG = 0.5
TORSO = ("spine_3", "spine_2", "spine_1", "hips")   # shoulders, ribs, hips
TORSO_SWING_MIN_DEG = 14.0  # each torso link swings at least this far peak to peak (Derek: "shake more")
TOL = 1e-9

CLIPS_PROBE = r'''
import json, math, sys
sys.path.insert(0, %(kit)r)
import dh_breed
dh_breed.get()
import bpy
import numpy as np
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
rig.clamped = []
rec, legs = [], []
clip = [""]
real_q = A.Rig.q_world
def rec_q(self, bone, rx=0.0, ry=0.0, rz=0.0):
    rec.append([clip[0], bone, rx, ry, rz])
    return real_q(self, bone, rx, ry, rz)
A.Rig.q_world = rec_q
# stubs: no dog, no baking; the leg solver records its targets
C.plant_all = lambda rig, tag, k=0.85: None
C.sit_targets = lambda rig: {leg: (Vector((0, 0, 0)), 0.0, 0.0) for leg in A.LEGS}
rig.rest = {}
for leg, names in A.LEGS.items():
    bones = [arm.data.bones[n] for n in names]
    J = [np.array([b.head_local.y, b.head_local.z]) for b in bones] + [np.array([bones[-1].tail_local.y, bones[-1].tail_local.z])]
    x = bones[0].head_local.x
    paw = Vector((x, J[3][0], J[3][1]))
    rig.rest[leg] = {"J": J, "L": [float(np.linalg.norm(J[i + 1] - J[i])) for i in range(4)], "x": x,
                     "ph": [math.degrees(math.atan2(*(J[i + 1] - J[i])[::-1])) for i in range(4)],
                     "paw": paw, "paw0": paw - Vector((0, 0, 0.004))}
rig.update = lambda: None
def rec_leg(leg, paw, paw_d=0.0, lower_d=None, fold=0.0, k=0.85, tag=""):
    legs.append([clip[0], leg, tag, list(paw), paw_d])
rig.solve_leg = rec_leg
rig.turn_world = lambda *a, **k: None
rig.joint = lambda bone, tail=False: arm.data.bones[bone].tail_local if tail else arm.data.bones[bone].head_local
rig.bake = lambda name, n, pose, loop=True: [pose(0 if (loop and f == n) else f) for f in range(n + 1)]
out = {"order": list(C.ORDER), "has_swim": hasattr(B, "SWIM"), "jaw_rest": D.JAW_REST_OPEN_DEG,
       "neck_z": arm.data.bones["neck"].head_local.z, "metas": {}}
for name, fn in (("Idle", lambda: [C.idle_body(rig, f / 24) for f in range(24)]),
                 ("Walk", lambda: [C.walk_body(rig, f / 24) for f in range(24)]),
                 ("Run", lambda: [C.run_body(rig, f / 24) for f in range(24)]),
                 ("TailWag", lambda: C.make_tail_wag(rig)),
                 ("PlayBow", lambda: C.make_play_bow(rig)),
                 ("SitDown", lambda: C.make_sit_down(rig)),
                 ("SitIdle", lambda: C.make_sit_idle(rig)),
                 ("Swim", lambda: C.make(rig, "Swim")),
                 ("Shake", lambda: C.make(rig, "Shake"))):
    clip[0] = name
    meta = fn()
    if isinstance(meta, dict):
        out["metas"][name] = {k: v for k, v in meta.items() if k in ("name", "frames", "loop", "ground_speed",
                                                                  "plants", "water_z", "paddle")}
out["sets"] = rec
out["legs"] = legs
print("PROBE " + json.dumps(out))
'''

CHECK_PROBE = r'''
import json, sys
sys.path.insert(0, %(kit)r)
import dh_breed
dh_breed.get()
import bpy
import numpy as np
import dh_dog_rig as RG
import dh_dog_check as K

# a made-up dog: one body vertex per bone (its weights all on that bone) and three rigid parts
nb = len(RG.BONE_NAMES)
class Fake:
    W = np.eye(nb)
    parts = {}
rig = Fake()
def frames(head_z, nose_z, ear_z, paw_z, n=4, dip_at=None):
    out = []
    for f in range(n + 1):
        body = np.zeros((nb, 3))
        body[:, 2] = 0.9
        for b in ("head", "jaw"):
            body[RG.BONE_NAMES.index(b), 2] = head_z if f != dip_at else 0.5
        for b in ("front_paw_L", "front_paw_R", "back_foot_L", "back_foot_R"):
            body[RG.BONE_NAMES.index(b), 2] = paw_z
        out.append({"body": body, "nose": np.array([[0.0, -0.5, nose_z]]),
                    "eye_L": np.array([[0.1, -0.5, head_z + 0.1]]),
                    "ear_L": np.array([[0.3, -0.4, ear_z]])})
    return out
bpy.data.actions.new("Swim")
bpy.data.actions.new("Shake")
W = 0.7
cases = {
    "clear":     ("Swim", frames(1.0, 1.0, 0.3, -0.2), W),
    "chin_dips": ("Swim", frames(1.0, 1.0, 0.3, -0.2, dip_at=2), W),
    "nose_dips": ("Swim", frames(1.0, 0.6, 0.3, -0.2), W),
    "on_ground": ("Shake", frames(1.0, 1.0, 0.3, -0.2), None),
}
res = {}
for k, (name, S, water) in cases.items():
    lines, errs, summ = K.check_clip(rig, name, 4, 0.0, None, None, samples=S, water=water)
    res[k] = {"errs": errs, "lines": lines, "summ": summ}
print("PROBE " + json.dumps(res))
'''

GOLDEN_PROBE = r'''
import json, sys
sys.path.insert(0, %(kit)r)
import dh_breed
dh_breed.get()
import bpy
import numpy as np
import dh_dog_anim as A
import dh_dog_check as K
import dh_dog_clips as C
import dh_dog_rig as RG

rig = A.Rig()
res = {}
for name in ("Swim", "Shake"):
    m = C.make(rig, name)
    act = bpy.data.actions[name]
    S = K.sample(rig, act, m["frames"])
    lines, errs, summ = K.check_clip(rig, name, m["frames"], m.get("ground_speed", 0.0), m.get("plants"),
                                     m.get("touch"), samples=S, water=m.get("water_z"))
    dom = np.array(RG.BONE_NAMES)[rig.W.argmax(1)]
    paw = {}
    for leg, bones in K.PAW_BONES.items():
        sel = np.isin(dom, bones)
        paw[leg] = [[float(v) for v in P["body"][sel].mean(0)] for P in S]
    res[name] = {"meta": {k: v for k, v in m.items() if k in ("frames", "loop", "water_z", "paddle", "plants")},
                 "errs": errs, "lines": lines, "paw": paw}
res["clamped"] = rig.clamped
print("PROBE " + json.dumps(res))
'''

SWIM_LINE = re.compile(r"^SWIM = .*$", re.M)


def has_bpy():
    return importlib.util.find_spec("bpy") is not None


def run_probe(script, extra=None, drop_swim=False, timeout=600):
    """The probe's output for the Golden's breed.py, its SWIM line dropped and/or `extra` appended."""
    with tempfile.TemporaryDirectory(prefix="dh_swim_shake_") as tmp:
        with open(os.path.join(GOLDEN, "breed.py")) as fh:
            src = fh.read()
        if drop_swim:
            src = SWIM_LINE.sub("", src)
        if extra:
            src += "\n" + extra + "\n"
        with open(os.path.join(tmp, "breed.py"), "w") as fh:
            fh.write(src)
        shutil.copytree(os.path.join(GOLDEN, "textures"), os.path.join(tmp, "textures"))   # a built dog reads them
        r = subprocess.run([sys.executable, "-c", script % {"kit": KIT}], capture_output=True, text=True,
                           timeout=timeout, env=dict(os.environ, DH_BREED=tmp))
    out = r.stdout + r.stderr
    if r.returncode:
        raise AssertionError("probe failed:\n" + out[-3000:])
    line = [ln for ln in r.stdout.splitlines() if ln.startswith("PROBE ")]
    if len(line) != 1:
        raise AssertionError("no probe output:\n" + out[-3000:])
    return json.loads(line[0][len("PROBE "):])


def series(sets, clip, bone, axis):
    """Per call, one rotation component of a bone in a clip (the clips set each bone once a frame)."""
    return [r[2 + axis] for r in sets if r[0] == clip and r[1] == bone]


def onset(vals):
    return next((i for i, v in enumerate(vals) if abs(v) > ONSET_DEG), None)


def last(vals):
    return max((i for i, v in enumerate(vals) if abs(v) > ONSET_DEG), default=None)


@unittest.skipUnless(has_bpy(), "needs bpy==5.3.0")
class SwimShakeClips(unittest.TestCase):
    cache = {}

    def probe(self, extra=None, drop_swim=True):
        key = (extra, drop_swim)
        if key not in self.cache:
            self.cache[key] = run_probe(CLIPS_PROBE, extra, drop_swim)
        return self.cache[key]

    def base(self):
        return self.probe()

    def swim_set(self):
        return self.probe("SWIM = %r" % ({"water_z": WATER_Z, "paddle": PADDLE},))

    def swung(self):
        return self.probe('EARS["swing"] = %r\nTAIL_SWING = %r' % (EAR_SWING, TAIL_SWING))

    # ------------------------------------------------------------------ registry
    def test_swim_and_shake_are_kit_clips(self):
        got = self.base()
        self.assertIn("Swim", got["order"])
        self.assertIn("Shake", got["order"])
        self.assertEqual(got["metas"]["Swim"]["name"], "Swim")
        self.assertEqual(got["metas"]["Shake"]["name"], "Shake")

    # ------------------------------------------------------------------ Shake
    def test_shake_is_a_one_shot_of_about_a_second_with_every_paw_planted(self):
        m = self.base()["metas"]["Shake"]
        self.assertFalse(m["loop"])
        self.assertGreaterEqual(m["frames"], 24)
        self.assertLessEqual(m["frames"], 36)
        self.assertEqual(m["ground_speed"], 0.0)
        self.assertEqual(m["plants"], {leg: [[0, m["frames"]]] for leg in ("FL", "FR", "HL", "HR")})

    def test_shake_starts_and_ends_standing_still(self):
        got = self.base()
        n = got["metas"]["Shake"]["frames"]
        sets = [r for r in got["sets"] if r[0] == "Shake"]
        per_frame = len(sets) // (n + 1)
        self.assertEqual(per_frame * (n + 1), len(sets), "every frame sets the same bones")
        for f in (0, n):
            for clip, bone, *rot in sets[f * per_frame:(f + 1) * per_frame]:
                want = [got["jaw_rest"], 0, 0] if bone == "jaw" else [0, 0, 0]
                for i in range(3):
                    self.assertAlmostEqual(rot[i], want[i], delta=1e-6, msg="f%d %s axis %d" % (f, bone, i))

    def test_shake_rolls_in_a_wave_from_head_to_tail(self):
        sets = self.base()["sets"]
        roll = {b: series(sets, "Shake", b, 1) for b in WAVE[:-1]}
        roll["tail_1"] = series(sets, "Shake", "tail_1", 2)        # the tail swings side to side
        starts = [onset(roll[b]) for b in WAVE]
        ends = [last(roll[b]) for b in WAVE]
        self.assertNotIn(None, starts, "every bone of the wave moves: %s" % dict(zip(WAVE, starts)))
        self.assertLess(starts[0], starts[-1], "the head starts before the tail")
        self.assertLess(ends[0], ends[-1], "the head settles before the tail")
        # the wave travels: each bone's shaking is centred later than the one before it (the mean
        # frame of its motion, weighted by how far it is turned)
        when = [sum(f * abs(v) for f, v in enumerate(roll[b])) / sum(abs(v) for v in roll[b]) for b in WAVE]
        for i in range(len(WAVE) - 1):
            self.assertLess(when[i], when[i + 1], "%s shakes before %s: %s" % (WAVE[i], WAVE[i + 1], when))
        for b in WAVE:
            self.assertGreater(max(roll[b]) - min(roll[b]), 6.0, "%s shakes" % b)
            # an oscillation, not one lean: it swings both ways several times
            sign = [v > 0 for v in roll[b] if abs(v) > ONSET_DEG]
            self.assertGreaterEqual(sum(1 for x, y in zip(sign, sign[1:]) if x != y), 4, "%s oscillates" % b)

    def test_shake_twists_the_body_vigorously(self):
        # Derek's review (#934): "Have the body shake more" - the torso's roll is the shake, so each
        # link through the shoulders, ribs and hips swings well past the first pass's 6-12 degrees
        sets = self.base()["sets"]
        for b in TORSO:
            r = series(sets, "Shake", b, 1)
            self.assertGreater(max(r) - min(r), TORSO_SWING_MIN_DEG, "%s swings %.1f deg" % (b, max(r) - min(r)))

    def test_shake_flaps_the_ears(self):
        sets = self.base()["sets"]
        for ear in ("ear_L_1", "ear_R_1"):
            flap = max(max(series(sets, "Shake", ear, a)) - min(series(sets, "Shake", ear, a)) for a in range(3))
            self.assertGreater(flap, 30.0, ear)

    def test_shake_uses_ear_swing_and_tail_swing(self):
        a = [r for r in self.base()["sets"] if r[0] == "Shake"]
        b = [r for r in self.swung()["sets"] if r[0] == "Shake"]
        self.assertEqual(len(a), len(b))
        scaled = 0
        for (ca, bone, *ra), (cb, bone_b, *rb) in zip(a, b):
            self.assertEqual(bone, bone_b)
            sw = EAR_SWING if bone in EAR_BONES else TAIL_SWING if bone in TAIL_BONES else (1, 1, 1)
            scaled += bone in EAR_BONES + TAIL_BONES and any(abs(v) > 1e-6 for v in ra)
            for i in range(3):
                self.assertAlmostEqual(rb[i], ra[i] * sw[i], delta=TOL, msg="%s axis %d" % (bone, i))
        self.assertGreater(scaled, 100)

    # ------------------------------------------------------------------ Swim
    def test_swim_is_a_loop_with_no_planted_paw(self):
        m = self.base()["metas"]["Swim"]
        self.assertTrue(m["loop"])
        self.assertGreaterEqual(m["frames"], 12)
        self.assertEqual(m["ground_speed"], 0.0)
        self.assertFalse(m.get("plants"))

    def test_swim_without_the_key_takes_the_water_line_and_stroke_from_the_rig(self):
        got = self.base()
        self.assertFalse(got["has_swim"])
        m = got["metas"]["Swim"]
        self.assertAlmostEqual(m["water_z"], got["neck_z"], delta=1e-6)
        # the stroke is a fraction of the front leg's height (shoulder joint to paw joint)
        legs = [r for r in got["legs"] if r[0] == "Swim" and r[1] == "FL"]
        ys = [r[3][1] for r in legs]
        self.assertAlmostEqual((max(ys) - min(ys)) / 2, m["paddle"], delta=1e-3)
        self.assertGreater(m["paddle"], 0.02)
        self.assertLess(m["paddle"], 0.3)

    def test_swim_takes_the_breeds_water_line_and_stroke(self):
        got = self.swim_set()
        self.assertTrue(got["has_swim"])
        m = got["metas"]["Swim"]
        self.assertEqual(m["water_z"], WATER_Z)
        self.assertEqual(m["paddle"], PADDLE)
        ys = [r[3][1] for r in got["legs"] if r[0] == "Swim" and r[1] == "FL"]
        self.assertAlmostEqual((max(ys) - min(ys)) / 2, PADDLE, delta=1e-3)

    def test_swim_paddles_the_front_legs_alternately_and_kicks_the_hind_legs(self):
        got = self.base()
        n = got["metas"]["Swim"]["frames"]
        tr = {leg: [r[3] for r in got["legs"] if r[0] == "Swim" and r[1] == leg] for leg in ("FL", "FR", "HL", "HR")}
        for leg, pts in tr.items():
            self.assertEqual(len(pts), n + 1, leg)
            for i in (1, 2):                                     # loops: last frame == first
                self.assertAlmostEqual(pts[0][i], pts[n][i], delta=1e-9, msg=leg)
            self.assertGreater(max(p[1] for p in pts) - min(p[1] for p in pts), 0.02, "%s strokes" % leg)
            self.assertGreater(max(p[2] for p in pts) - min(p[2] for p in pts), 0.01, "%s goes up and down" % leg)
        h = n // 2
        for a, b in (("FL", "FR"), ("HL", "HR")):            # left and right half a cycle apart
            for f in range(n):
                for i in (1, 2):
                    self.assertAlmostEqual(tr[a][f][i], tr[b][(f + h) % n][i], delta=1e-6,
                                           msg="%s f%d vs %s f%d" % (a, f, b, (f + h) % n))
        # a paddle, not a walk: the power stroke pushes back (+Y) low, the recovery comes forward high
        for leg in ("FL", "FR"):
            pts = tr[leg][:n]
            back = [pts[(f + 1) % n][1] - pts[f][1] > 0 for f in range(n)]
            zb = [pts[f][2] for f in range(n) if back[f]]
            zf = [pts[f][2] for f in range(n) if not back[f]]
            self.assertLess(sum(zb) / len(zb), sum(zf) / len(zf), leg)

    def test_swim_and_shake_change_no_other_clip(self):
        a = [r for r in self.base()["sets"] if r[0] in OTHER_CLIPS]
        b = [r for r in self.swim_set()["sets"] if r[0] in OTHER_CLIPS]
        self.assertEqual(a, b)
        self.assertEqual(self.base()["metas"]["TailWag"], self.swim_set()["metas"]["TailWag"])


@unittest.skipUnless(has_bpy(), "needs bpy==5.3.0")
class WaterLineCheck(unittest.TestCase):
    got = None

    def res(self, case):
        if WaterLineCheck.got is None:
            WaterLineCheck.got = run_probe(CHECK_PROBE)
        return self.got[case]

    def test_a_clear_head_passes_and_the_ground_is_not_checked(self):
        r = self.res("clear")
        self.assertEqual(r["errs"], [])        # paws 20 cm below z = 0 and ears in the water are fine
        self.assertTrue(any("water line" in ln for ln in r["lines"]), r["lines"])
        self.assertAlmostEqual(r["summ"]["head_clearance_m"], 1.0 - 0.7, delta=1e-6)

    def test_a_chin_below_the_water_line_fails(self):
        r = self.res("chin_dips")
        self.assertEqual(len(r["errs"]), 1, r["errs"])
        self.assertIn("water line", r["errs"][0])
        self.assertIn("f2", r["errs"][0])

    def test_a_nose_below_the_water_line_fails(self):
        r = self.res("nose_dips")
        self.assertEqual(len(r["errs"]), 1, r["errs"])
        self.assertIn("water line", r["errs"][0])

    def test_other_clips_keep_the_ground_check(self):
        r = self.res("on_ground")
        self.assertEqual(len(r["errs"]), 1, r["errs"])
        self.assertIn("sinks", r["errs"][0])


@unittest.skipUnless(has_bpy(), "needs bpy==5.3.0")
class GoldenSwimShake(unittest.TestCase):
    got = None

    def res(self):
        if GoldenSwimShake.got is None:
            GoldenSwimShake.got = run_probe(GOLDEN_PROBE, drop_swim=False)
        return self.got

    def test_golden_swim_and_shake_pass_their_checks(self):
        got = self.res()
        self.assertEqual(got["clamped"], [])
        for name in ("Swim", "Shake"):
            self.assertEqual(got[name]["errs"], [], "\n".join(got[name]["lines"]))

    def test_golden_swims_with_the_paws_moving(self):
        s = self.res()["Swim"]
        for leg, pts in s["paw"].items():
            ys = [p[1] for p in pts]
            self.assertGreater(max(ys) - min(ys), s["meta"]["paddle"], leg)


if __name__ == "__main__":
    unittest.main(verbosity=2)
