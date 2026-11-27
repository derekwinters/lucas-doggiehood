"""The optional COLLAR["ray"] key: how far out (m) the collar's rays start when they find the skin
around the collar loop. Without it a breed gets the old 0.6 m.

  python3 art-source/blender/dogs/kit/tests/test_collar_ray.py               (a few seconds)
  python3 -m unittest discover -s art-source/blender/dogs/kit/tests

The strap hugs the skin: from 0.6 m out along each direction around the collar loop, a ray is cast
back toward the loop's centre and the strap sits on the first hit. A tail curled over the back can
be the first thing such a ray meets, and the strap then runs out to the tail. A breed can start the
rays nearer (inside the tail, outside the neck) with COLLAR["ray"].

Needs bpy (==5.3.0), but builds no dog: each case runs in its own process with the Golden's
breed.py copied into a temporary folder (with or without the key appended) and lofts the collar
around a stand-in skin: a ball for the neck about the Golden's collar loop, plus a ball 0.45 m out
from the loop in its plane (a stand-in for the tail). Nothing is written into the repository.
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

PROBE = r'''
import json, math, sys
sys.path.insert(0, %(kit)r)
import dh_breed
dh_breed.get()
import bpy, bmesh
from mathutils import Matrix, Vector
from mathutils.bvhtree import BVHTree
import dh_dog as D
import dh_dog_shape as S

B = D.B
i = B.COLLAR["ring"]
c = Vector((0, S.TORSO[i][0], S.TORSO[i][1]))
bm = bmesh.new()
# neck: a ball of radius 0.15 about the loop centre (every ray around the loop meets it)
bmesh.ops.create_uvsphere(bm, u_segments=48, v_segments=24, radius=0.15, matrix=Matrix.Translation(c))
# a ball 0.45 m out from the loop, in the loop's plane: in the way of the old 0.6 m rays
tilt = (Vector((0, S.TORSO[i + 1][0], S.TORSO[i + 1][1])) - Vector((0, S.TORSO[i - 1][0], S.TORSO[i - 1][1]))).normalized()
b = tilt.cross(D.X).normalized()
bmesh.ops.create_uvsphere(bm, u_segments=16, v_segments=8, radius=0.06, matrix=Matrix.Translation(c + b * 0.45))
bvh = BVHTree.FromBMesh(bm)
p, _ = D.collar_part(D.QUALITY[False], bvh)
far = max((v.co - c).length for v in p.bm.verts)
print("PROBE " + json.dumps({"ray": B.COLLAR.get("ray"), "far": far}))
'''


def has_bpy():
    return importlib.util.find_spec("bpy") is not None


@unittest.skipUnless(has_bpy(), "needs bpy==5.3.0")
class CollarRay(unittest.TestCase):
    def probe(self, extra=""):
        with tempfile.TemporaryDirectory(prefix="dh_collar_ray_") as tmp:
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
        return json.loads(line[0][len("PROBE "):])

    def test_breed_without_the_key_casts_from_0_6_m(self):
        got = self.probe()
        self.assertIsNone(got["ray"])
        # the old rays start beyond the ball, so the strap reaches out to it
        self.assertGreater(got["far"], 0.35)

    def test_nearer_rays_keep_the_strap_on_the_neck(self):
        got = self.probe('COLLAR["ray"] = 0.3')
        self.assertEqual(got["ray"], 0.3)
        self.assertLess(got["far"], 0.25)


if __name__ == "__main__":
    unittest.main(verbosity=2)
