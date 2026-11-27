"""Checks for the dog toy (#332): budget, pivot, glTF read-back and byte-for-byte rebuilds.

  python3 art-source/blender/items/toy/tests/test_toy.py          (about 30 s)
  python3 -m unittest discover -s art-source/blender/items/toy/tests

Needs bpy==5.3.0 (skipped without it). Every build runs export_toy.py in a fresh process into a
temporary folder, so nothing is written into the repository.

Invariant - every committed model is reproducible from its script in the repo
(docs/engineering/blender-pipeline.md): two fresh exports are byte-identical to each other and to
the committed exports/ and textures/ files.
"""
import importlib.util
import json
import os
import subprocess
import sys
import tempfile
import unittest

TESTS = os.path.dirname(os.path.abspath(__file__))
TOY = os.path.dirname(TESTS)
EXPORT = os.path.join(TOY, "export_toy.py")
NAME = "toy_duck"
# Provisional per-mesh backstop of the art budget (#799). Written out here, not read from the
# script, so raising the script's constant cannot quietly loosen the check.
TRI_BACKSTOP = 6000
# every file export_toy.py writes (relative to its output root)
OUTPUTS = ["exports/%s.gltf" % NAME, "exports/%s.bin" % NAME, "exports/toy_stats.json",
           "textures/toy_fleece_color.png", "textures/toy_fleece_normal.png"]
PIVOT_TOL = 0.002   # metres


def has_bpy():
    return importlib.util.find_spec("bpy") is not None


def export_to(root):
    r = subprocess.run([sys.executable, EXPORT, root], capture_output=True, text=True)
    if r.returncode != 0:
        raise AssertionError("export_toy.py failed:\n" + r.stdout[-3000:] + r.stderr[-3000:])
    return r.stdout


@unittest.skipUnless(has_bpy(), "needs bpy==5.3.0")
class ToyExport(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.tmp = tempfile.TemporaryDirectory()
        cls.a = os.path.join(cls.tmp.name, "a")
        cls.b = os.path.join(cls.tmp.name, "b")
        cls.out_a = export_to(cls.a)
        export_to(cls.b)
        cls.stats = json.load(open(os.path.join(cls.a, "exports", "toy_stats.json")))[NAME]
        cls.gltf = json.load(open(os.path.join(cls.a, "exports", NAME + ".gltf")))

    @classmethod
    def tearDownClass(cls):
        cls.tmp.cleanup()

    def test_export_checks_pass(self):
        self.assertIn("ALL OK", self.out_a)

    def test_within_triangle_backstop(self):
        self.assertGreater(self.stats["tris"], 0)
        self.assertLessEqual(self.stats["tris"], TRI_BACKSTOP)

    def test_pivot_on_ground_at_footprint_centre(self):
        lo, hi = self.stats["bounds_min_m"], self.stats["bounds_max_m"]
        self.assertAlmostEqual(lo[2], 0.0, delta=PIVOT_TOL)       # sits on the ground
        self.assertAlmostEqual(lo[0] + hi[0], 0.0, delta=PIVOT_TOL)
        self.assertAlmostEqual(lo[1] + hi[1], 0.0, delta=PIVOT_TOL)

    def test_one_mesh_named_for_the_toy(self):
        self.assertEqual([n["name"] for n in self.gltf["nodes"]], [NAME])
        self.assertEqual(len(self.gltf["meshes"]), 1)

    def test_materials_carry_the_toy_prefix(self):
        names = [m["name"] for m in self.gltf["materials"]]
        self.assertTrue(names)
        for n in names:
            self.assertTrue(n.startswith("DH_Toy_"), n)

    def test_textures_are_referenced_not_embedded(self):
        imgs = self.gltf.get("images", [])
        self.assertTrue(imgs)
        for im in imgs:
            self.assertIn("uri", im)
            self.assertTrue(im["uri"].startswith("../textures/"), im["uri"])
            self.assertTrue(os.path.isfile(os.path.join(self.a, "exports", im["uri"])), im["uri"])

    def test_two_fresh_exports_are_byte_identical(self):
        for rel in OUTPUTS:
            with open(os.path.join(self.a, rel), "rb") as fa, open(os.path.join(self.b, rel), "rb") as fb:
                self.assertEqual(fa.read(), fb.read(), rel)

    def test_committed_files_match_a_fresh_export(self):
        for rel in OUTPUTS:
            with open(os.path.join(self.a, rel), "rb") as fa, open(os.path.join(TOY, rel), "rb") as fc:
                self.assertEqual(fa.read(), fc.read(), rel)


if __name__ == "__main__":
    unittest.main()
