"""The optional FUR_STRAND breed field sets the fur strand look; without it a breed gets the old one.

  python3 art-source/blender/dogs/kit/tests/test_fur_strand.py               (a few seconds)
  python3 -m unittest discover -s art-source/blender/dogs/kit/tests

Plain Python (unittest, numpy, Pillow); no Blender. Each case imports kit/dh_dog_tex.py in its own
process (one breed per process), with a breed folder in a temporary directory: the Golden's
breed.py as it is (no FUR_STRAND), or with a FUR_STRAND line appended. Nothing is written into the
repository.

FUR_STRAND is optional. Its defaults are the constants dh_dog_tex.py had built in before the field
existed (the Golden's long coat), so a breed without it bakes exactly the textures it baked before.
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

# the strand look dh_dog_tex.py had built in before FUR_STRAND (the Golden's long coat)
OLD = {"fine": (0.10, 0.8), "mid": (0.35, 2.0), "clump": (1.1, 3.5), "low": (6.0, 9.0),
       "mix": (0.40, 0.35, 0.35), "normal": 0.40, "contrast": 0.32}
# a short, smooth coat (every key differs from OLD)
SHORT = {"fine": (0.07, 0.30), "mid": (0.16, 0.45), "clump": (0.40, 0.7), "low": (5.0, 7.0),
         "mix": (0.60, 0.35, 0.10), "normal": 0.16, "contrast": 0.20}
AROUND_M, ALONG_M, SEED = 0.05, 0.08, 11    # a small strand tile, fixed seed
OLD_SHADE = "0.84 + 0.32 * det"             # dh_dog_tex.coat_color's strand light/dark before FUR_STRAND
NEW_SHADE = '1.0 - S["contrast"] / 2 + S["contrast"] * det'

# Run in a fresh process: print the kit's STRAND, a hash of strand_height() and of coat_color()
# for a fixed input, and the same two hashes computed here from the expected look (expect, and
# shade: the strand light/dark expression; for the old look, the literal it was before).
PROBE = r'''
import hashlib, json, sys
sys.path.insert(0, %(kit)r)
import numpy as np
import dh_dog_tex as T
S = %(expect)r


def ref_height(rng):
    w, h = max(16, int(round(%(around)r * T.PPM))), max(16, int(round(%(along)r * T.PPM)))
    px = T.PPM / 100.0
    lay = {k: T._norm(T._fft_blur(rng.standard_normal((h, w)), S[k][0] * px, S[k][1] * px))
           for k in ("fine", "mid", "clump", "low")}
    a, b, c = S["mix"]
    return a * lay["fine"] + b * lay["mid"] + c * lay["clump"], lay["low"]


def digest(*arrays):
    return hashlib.sha1(b"".join(np.ascontiguousarray(x).tobytes() for x in arrays)).hexdigest()


mask = np.random.default_rng(3).random((8, 8, 3))
coat = next(iter(T.COATS))
main, feath, earc = (T._hex(c) for c in T.COATS[coat])
det, f, e = mask[..., 0:1], mask[..., 1:2], mask[..., 2:3]
col = (main * (1 - f) + feath * f) * (1 - e) + earc * e
col = col * (%(shade)s) + (det - 0.5) * np.array([0.02, 0.0, -0.03])
ref_coat = (np.clip(col, 0, 1) * 255 + 0.5).astype(np.uint8)
print("PROBE " + json.dumps({
    "strand": T.STRAND,
    "height": digest(*T.strand_height(%(around)r, %(along)r, np.random.default_rng(%(seed)r))),
    "ref_height": digest(*ref_height(np.random.default_rng(%(seed)r))),
    "coat": digest(T.coat_color(mask, coat)),
    "ref_coat": digest(ref_coat),
}))
'''


def plain(d):
    """d with tuples as lists, as it comes back through JSON."""
    return json.loads(json.dumps(d))


class FurStrand(unittest.TestCase):

    def probe(self, fur_strand, expect, shade=NEW_SHADE):
        """Import dh_dog_tex for the Golden's breed.py plus fur_strand (None: no FUR_STRAND line)."""
        with tempfile.TemporaryDirectory(prefix="dh_fur_strand_") as tmp:
            shutil.copy(os.path.join(GOLDEN, "breed.py"), tmp)
            if fur_strand is not None:
                with open(os.path.join(tmp, "breed.py"), "a") as fh:
                    fh.write("\nFUR_STRAND = %r\n" % (fur_strand,))
            env = dict(os.environ, DH_BREED=tmp)
            code = PROBE % {"kit": KIT, "expect": expect, "around": AROUND_M, "along": ALONG_M,
                            "seed": SEED, "shade": shade}
            r = subprocess.run([sys.executable, "-c", code], capture_output=True, text=True,
                               timeout=300, env=env)
        out = r.stdout + r.stderr
        self.assertEqual(r.returncode, 0, out[-3000:])
        line = [ln for ln in r.stdout.splitlines() if ln.startswith("PROBE ")]
        self.assertEqual(len(line), 1, out[-3000:])
        return json.loads(line[0][len("PROBE "):])

    def test_breed_without_fur_strand_gets_the_old_constants(self):
        got = self.probe(None, OLD, OLD_SHADE)
        self.assertEqual(got["strand"], plain(OLD))
        self.assertEqual(got["height"], got["ref_height"], "strand noise differs from the old look")
        self.assertEqual(got["coat"], got["ref_coat"], "strand light/dark differs from the old look")

    def test_breed_with_fur_strand_gets_its_own_values(self):
        got = self.probe(SHORT, SHORT)
        self.assertEqual(got["strand"], plain(SHORT))
        self.assertEqual(got["height"], got["ref_height"], "strand noise does not use FUR_STRAND")
        self.assertEqual(got["coat"], got["ref_coat"], "strand light/dark does not use FUR_STRAND")

    def test_partial_fur_strand_keeps_the_other_defaults(self):
        part = {"normal": 0.16, "contrast": 0.20}
        got = self.probe(part, dict(OLD, **part))
        self.assertEqual(got["strand"], plain(dict(OLD, **part)))
        self.assertEqual(got["coat"], got["ref_coat"])

    def test_fur_strand_is_not_a_required_field(self):
        sys.path.insert(0, KIT)
        import dh_breed
        self.assertNotIn("FUR_STRAND", dh_breed.REQUIRED)


if __name__ == "__main__":
    unittest.main(verbosity=2)
