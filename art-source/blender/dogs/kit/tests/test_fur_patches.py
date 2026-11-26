"""The optional FUR_PATCHES breed field lays a pattern (a saddle, a mask) over the coat's dark channel;
without it a breed gets exactly the masks it got before.

  python3 art-source/blender/dogs/kit/tests/test_fur_patches.py               (a few seconds)

Plain Python (unittest, numpy, Pillow); no Blender. Each case imports kit/dh_dog_tex.py in its own
process (one breed per process) with the Golden's breed.py copied into a temporary folder, with or
without a FUR_PATCHES line appended, and runs fur_masks() on a fixed random set of texels.
"""
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
import hashlib, json, sys
sys.path.insert(0, %(kit)r)
import numpy as np
import dh_dog_tex as T
rng = np.random.default_rng(5)
n = 4000
P = np.column_stack([rng.uniform(-0.3, 0.3, n), rng.uniform(-0.9, 0.9, n), rng.uniform(0.0, 1.8, n)])
N = rng.normal(size=(n, 3)); N /= np.linalg.norm(N, axis=1, keepdims=True)
A = rng.random((n, len(T.ATTRS)))
low = rng.normal(size=n)
f0, e0 = T.fur_masks(P, N, A)                    # the old call, no noise passed
f1, e1 = T.fur_masks(P, N, A, low)
w = {k: A[:, i] / A.sum(1) for i, k in enumerate(T.ATTRS)}
dig = lambda *a: hashlib.sha1(b"".join(np.ascontiguousarray(x).tobytes() for x in a)).hexdigest()
print("PROBE " + json.dumps({
    "patches": len(T.PATCHES), "old_call": dig(f0, e0), "new_call": dig(f1, e1),
    "f": dig(f1), "e_mean": float(e1.mean()),
    "z_hi_e_min": float(e1[P[:, 2] > 1.2].min()),
    "z_lo_e": dig(e1[P[:, 2] < 1.0]),
}))
'''


class FurPatches(unittest.TestCase):

    def probe(self, patches):
        with tempfile.TemporaryDirectory(prefix="dh_fur_patches_") as tmp:
            shutil.copy(os.path.join(GOLDEN, "breed.py"), tmp)
            if patches is not None:
                with open(os.path.join(tmp, "breed.py"), "a") as fh:
                    fh.write("\nFUR_PATCHES = %r\n" % (patches,))
            r = subprocess.run([sys.executable, "-c", PROBE % {"kit": KIT}], capture_output=True, text=True,
                               timeout=300, env=dict(os.environ, DH_BREED=tmp))
        out = r.stdout + r.stderr
        self.assertEqual(r.returncode, 0, out[-3000:])
        line = [ln for ln in r.stdout.splitlines() if ln.startswith("PROBE ")]
        self.assertEqual(len(line), 1, out[-3000:])
        return json.loads(line[0][len("PROBE "):])

    def test_breed_without_fur_patches_gets_the_old_masks(self):
        got = self.probe(None)
        self.assertEqual(got["patches"], 0)
        self.assertEqual(got["old_call"], got["new_call"], "the noise argument changed a breed without patches")

    def test_dark_patch_raises_the_dark_channel_only(self):
        base = self.probe(None)
        body = ("body", "head", "tail", "fleg", "bleg", "thigh", "paw", "ear")
        got = self.probe([{"regions": body, "z": (1.0, 1.1)}])
        self.assertEqual(got["f"], base["f"], "a patch changed the feathering channel")
        self.assertGreater(got["e_mean"], base["e_mean"])
        self.assertGreater(got["z_hi_e_min"], 0.99, "a full-strength dark patch left texels light")
        self.assertEqual(got["z_lo_e"], base["z_lo_e"], "a patch changed texels outside its z range")

    def test_light_patch_lowers_the_dark_channel(self):
        base = self.probe(None)
        got = self.probe([{"regions": ("ear",), "op": "light", "strength": 1.0}])
        self.assertLess(got["e_mean"], base["e_mean"])

    def test_fur_patches_is_not_a_required_field(self):
        sys.path.insert(0, KIT)
        import dh_breed
        self.assertNotIn("FUR_PATCHES", dh_breed.REQUIRED)


if __name__ == "__main__":
    unittest.main(verbosity=2)
