"""The optional EARS["side"] and EARS["swing"] keys make an erect ear; without them a breed gets
exactly the hanging ear it got before.

  python3 art-source/blender/dogs/kit/tests/test_erect_ears.py               (a few seconds)
  python3 -m unittest discover -s art-source/blender/dogs/kit/tests

Needs bpy (==5.3.0), but builds no dog: each case runs in its own process (one breed per process)
with the Golden's breed.py copied into a temporary folder, with or without lines appended that set
the keys, then lofts the two ears with dh_dog.ear_part() and poses a bare armature
(dh_dog_rig.make_armature) with dh_dog_rig.apply_pose() and dh_dog_anim.Rig.set(). Nothing is
written into the repository.

  EARS["side"]   the ear loft's side axis for the left ear (default +Y: the width of a hanging ear
                 runs front-to-back). An erect ear's width runs across the head. The right ear is
                 the left's mirror image, and its vertex on ring i at position k is the mirror of
                 the left's at (n - k) mod n, which lies on the same side of the ring: a texel of
                 the shared ear island shows the same face (front or back) on both ears. (The
                 hanging ear's right ear pairs k with (n/2 - k) mod n instead: the opposite face.)
  EARS["swing"]  per world axis multipliers on every ear rotation the clips and poses ask for
                 (written for hanging ears). Bones other than the ears are unchanged.
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

SIDE = (1.0, 0.5, 0.0)          # an erect ear's width runs across, turned a little to open forward
SWING = (-0.5, -1.0, 2.0)       # every component differs from 1 (and from each other)
SET_ROT = (10.0, 20.0, 30.0)    # an arbitrary rotation passed to Rig.set
TOL = 1e-6

PROBE = r'''
import json, sys
sys.path.insert(0, %(kit)r)
import dh_breed
dh_breed.get()
import bpy
from mathutils import Vector
import dh_dog as D
import dh_dog_rig as RG
import dh_dog_anim as A

B = D.B
out = {"has_side": "side" in B.EARS, "has_swing": "swing" in B.EARS}

# ---- the ears' loft, both sides, full and LOD
def verts(p):
    p.bm.verts.ensure_lookup_table()
    return [tuple(v.co) for v in p.bm.verts]

def reference(q, sx, side):
    """The ear as the kit lofted it before EARS["side"] existed, with the given side axis."""
    p = D.Part("ref")
    spec = list(B.EARS["rings"])
    if q["lod"]:
        spec = [spec[i] for i in B.EARS["lod_rings"]]
    e, dome = B.LOFT_STYLE["ear"]
    rings = [D.Ring((sx * x, y, z), w, t, t, e) for x, y, z, w, t in spec]
    p.loft(rings, q["ear_n"], Vector(side), D.T.ISLANDS["ear"][0], dome=dome)
    return verts(p)

ears = {}
for lod in (False, True):
    q = D.QUALITY[lod]
    nr = len(B.EARS["lod_rings"]) if lod else len(B.EARS["rings"])
    ears["lod" if lod else "full"] = dict(
        n=q["ear_n"], rings=nr, L=verts(D.ear_part(q, 1)), R=verts(D.ear_part(q, -1)),
        ref_L=reference(q, 1, D.Y), ref_R=reference(q, -1, D.Y))
out["ears"] = ears

# ---- ear rotations: a bare armature, posed by apply_pose and by Rig.set
arm = RG.make_armature("probe_arm", bpy.context.scene.collection)
rig = A.Rig.__new__(A.Rig)          # the pose helpers only, no dog built
rig.arm = arm
for pb in arm.pose.bones:
    pb.rotation_mode = "QUATERNION"

def quat(b):
    return list(arm.pose.bones[b].rotation_quaternion)

spec = RG.poses()["wag"]
RG.apply_pose(arm, {}, "wag")
out["wag"] = {b: dict(rot=list(d["rot"]), got=quat(b))
              for b, d in spec.items() if not b.startswith("_") and "rot" in d}
rs = %(set_rot)r
out["set"] = {}
for b in ("ear_L_1", "ear_L_2", "ear_R_1", "ear_R_2", "neck", "head", "tail_1"):
    rig.set(b, *rs)
    out["set"][b] = quat(b)
# the quaternion q_world gives for any rotation, to compare against (q_world does no scaling)
def qw(b, r):
    return list(rig.q_world(b, *r))
sw = B.EARS.get("swing", (1.0, 1.0, 1.0))
scale = lambda b, r: tuple(r[i] * sw[i] for i in range(3)) if b.startswith("ear_") else tuple(r)
out["expect_set"] = {b: qw(b, scale(b, rs)) for b in out["set"]}
out["plain_set"] = {b: qw(b, rs) for b in out["set"]}
for b, d in out["wag"].items():
    d["expect"] = qw(b, scale(b, d["rot"]))
    d["plain"] = qw(b, d["rot"])
print("PROBE " + json.dumps(out))
'''


def has_bpy():
    return importlib.util.find_spec("bpy") is not None


def mirror(v):
    return (-v[0], v[1], v[2])


def close(a, b, tol=TOL):
    return all(abs(x - y) <= tol for x, y in zip(a, b))


@unittest.skipUnless(has_bpy(), "needs bpy==5.3.0")
class ErectEars(unittest.TestCase):
    cache = {}

    def probe(self, extra):
        """The probe's output for the Golden's breed.py plus the lines in extra (None: as it is)."""
        if extra in self.cache:
            return self.cache[extra]
        with tempfile.TemporaryDirectory(prefix="dh_erect_ears_") as tmp:
            shutil.copy(os.path.join(GOLDEN, "breed.py"), tmp)
            if extra is not None:
                with open(os.path.join(tmp, "breed.py"), "a") as fh:
                    fh.write("\n" + extra + "\n")
            r = subprocess.run([sys.executable, "-c", PROBE % {"kit": KIT, "set_rot": SET_ROT}],
                               capture_output=True, text=True, timeout=300, env=dict(os.environ, DH_BREED=tmp))
        out = r.stdout + r.stderr
        self.assertEqual(r.returncode, 0, out[-3000:])
        line = [ln for ln in r.stdout.splitlines() if ln.startswith("PROBE ")]
        self.assertEqual(len(line), 1, out[-3000:])
        got = json.loads(line[0][len("PROBE "):])
        self.cache[extra] = got
        return got

    def assert_mirrored(self, ear, index_map, what):
        """The right ear's vertex (ring i, k) is the mirror of the left's (ring i, index_map(k));
        the two cap centres (after the rings) are mirrored one to one."""
        n, m, L, R = ear["n"], ear["rings"], ear["L"], ear["R"]
        self.assertEqual(len(L), len(R))
        self.assertEqual(len(L), m * n + 2)
        for i in range(m):
            for k in range(n):
                self.assertTrue(close(R[i * n + k], mirror(L[i * n + index_map(k, n)])),
                                "%s: ring %d vertex %d is not mirrored" % (what, i, k))
        for j in (m * n, m * n + 1):
            self.assertTrue(close(R[j], mirror(L[j])), "%s: cap centre not mirrored" % what)

    @staticmethod
    def span(vs, axis):
        c = [v[axis] for v in vs]
        return max(c) - min(c)

    # ------------------------------------------------------------------ EARS["side"]
    def test_breed_without_side_gets_the_old_hanging_ear(self):
        got = self.probe(None)
        self.assertFalse(got["has_side"])
        for name, ear in got["ears"].items():
            for s in "LR":
                self.assertEqual(len(ear[s]), len(ear["ref_" + s]))
                for a, b in zip(ear[s], ear["ref_" + s]):
                    self.assertTrue(close(a, b), "%s %s ear differs from the old loft" % (name, s))
            # a hanging ear's width runs front-to-back
            self.assertGreater(self.span(ear["L"], 1), self.span(ear["L"], 0))
            self.assert_mirrored(ear, lambda k, n: (n // 2 - k) % n, name)

    def test_side_turns_the_ear_width_across_the_head(self):
        base = self.probe(None)
        got = self.probe('EARS["side"] = %r' % (SIDE,))
        self.assertTrue(got["has_side"])
        for name, ear in got["ears"].items():
            self.assertTrue(ear["L"] != base["ears"][name]["L"], "%s: EARS[\"side\"] did not change the ear" % name)
            for s in "LR":
                self.assertGreater(self.span(ear[s], 0), self.span(ear[s], 1),
                                   "%s %s ear: width does not run across" % (name, s))

    def test_side_right_ear_is_mirrored_with_matching_faces(self):
        got = self.probe('EARS["side"] = %r' % (SIDE,))
        for name, ear in got["ears"].items():
            self.assert_mirrored(ear, lambda k, n: (n - k) % n, name)

    # ------------------------------------------------------------------ EARS["swing"]
    def test_breed_without_swing_rotates_ears_as_asked(self):
        got = self.probe(None)
        self.assertFalse(got["has_swing"])
        for b, q in got["set"].items():
            self.assertTrue(close(q, got["plain_set"][b]), "Rig.set scaled %s" % b)
        for b, d in got["wag"].items():
            self.assertTrue(close(d["got"], d["plain"]), "apply_pose scaled %s" % b)

    def test_swing_scales_ear_rotations_per_axis(self):
        got = self.probe('EARS["swing"] = %r' % (SWING,))
        self.assertTrue(got["has_swing"])
        ears = [b for b in got["set"] if b.startswith("ear_")]
        self.assertTrue(ears)
        for b in ears:
            self.assertTrue(close(got["set"][b], got["expect_set"][b]), "Rig.set did not swing %s" % b)
            self.assertFalse(close(got["set"][b], got["plain_set"][b]))
        wag_ears = [b for b in got["wag"] if b.startswith("ear_")]
        self.assertTrue(wag_ears, "the wag pose has no ear rotation to check")
        for b in wag_ears:
            self.assertTrue(close(got["wag"][b]["got"], got["wag"][b]["expect"]), "apply_pose did not swing %s" % b)

    def test_swing_leaves_other_bones_alone(self):
        got = self.probe('EARS["swing"] = %r' % (SWING,))
        for b, q in got["set"].items():
            if not b.startswith("ear_"):
                self.assertTrue(close(q, got["plain_set"][b]), "Rig.set scaled %s" % b)
        for b, d in got["wag"].items():
            if not b.startswith("ear_"):
                self.assertTrue(close(d["got"], d["plain"]), "apply_pose scaled %s" % b)


if __name__ == "__main__":
    unittest.main(verbosity=2)
