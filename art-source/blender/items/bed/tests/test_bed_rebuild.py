"""Exporting the dog bed (#411) from its scripts rewrites the committed files byte for byte.

  python3 art-source/blender/items/bed/tests/test_bed_rebuild.py           (under a minute)
  python3 -m unittest discover -s art-source/blender/items/bed/tests

Invariant - every committed model is reproducible from its script in the repo
(docs/engineering/blender-pipeline.md). Needs bpy==5.3.0: a different bpy can change the bytes.

export_bed.py is run DH_REBUILDS times (default 2), each in a fresh process, into a temporary
folder; every export, texture, the stats file and the colour palette must match the committed one. Nothing is
written into the repository.
"""
import hashlib
import importlib.util
import os
import subprocess
import sys
import tempfile
import unittest

TESTS = os.path.dirname(os.path.abspath(__file__))
BED = os.path.dirname(TESTS)
OUTPUTS = ("bed_colours.json", "exports/dog_bed.gltf", "exports/dog_bed.bin", "exports/bed_stats.json",
           "textures/bed_cover_color.png", "textures/bed_cover_normal.png",
           "textures/bed_fleece_color.png", "textures/bed_fleece_normal.png")


def has_bpy():
    return importlib.util.find_spec("bpy") is not None


def sha(path):
    with open(path, "rb") as fh:
        return hashlib.sha256(fh.read()).hexdigest()


@unittest.skipUnless(has_bpy(), "needs bpy==5.3.0")
class Rebuild(unittest.TestCase):
    def test_every_export_matches_the_committed_files(self):
        for f in OUTPUTS:
            self.assertTrue(os.path.isfile(os.path.join(BED, f)), "not committed: " + f)
        want = {f: sha(os.path.join(BED, f)) for f in OUTPUTS}
        rebuilds = int(os.environ.get("DH_REBUILDS", "2"))
        for i in range(rebuilds):
            with tempfile.TemporaryDirectory(prefix="dh_bed_rebuild_") as tmp:
                r = subprocess.run([sys.executable, os.path.join(BED, "export_bed.py"), tmp],
                                   capture_output=True, text=True, timeout=600)
                out = r.stdout + r.stderr
                self.assertEqual(r.returncode, 0, out[-3000:])
                self.assertIn("ALL OK", r.stdout, out[-3000:])
                differ = sorted(f for f in OUTPUTS if sha(os.path.join(tmp, f)) != want[f])
                self.assertEqual(differ, [], "rebuild %d of %d differs" % (i + 1, rebuilds))


if __name__ == "__main__":
    unittest.main(verbosity=2)
