"""Checks for the dog toy designs (#332): budget, pivot, glTF read-back and byte-for-byte rebuilds.

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
# Every design, by object / file name: the five toys Derek approved (the plush duck was rejected
# and is not built).
NAMES = ["toy_disc", "toy_chew_cone", "toy_hotdog", "toy_rubber_chicken", "toy_rope_knot"]
# Provisional per-mesh backstop of the art budget (#799). Written out here, not read from the
# script, so raising the script's constant cannot quietly loosen the check.
TRI_BACKSTOP = 6000
# every file export_toy.py writes (relative to its output root)
OUTPUTS = (["exports/%s.%s" % (n, e) for n in NAMES for e in ("gltf", "bin")]
           + ["exports/toy_stats.json", "textures/toy_fleece_color.png", "textures/toy_fleece_normal.png"])
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
        cls.stats = json.load(open(os.path.join(cls.a, "exports", "toy_stats.json")))
        cls.gltf = {n: json.load(open(os.path.join(cls.a, "exports", n + ".gltf"))) for n in NAMES}

    @classmethod
    def tearDownClass(cls):
        cls.tmp.cleanup()

    def test_export_checks_pass(self):
        self.assertIn("ALL OK", self.out_a)

    def test_every_design_is_exported(self):
        self.assertEqual(sorted(self.stats), sorted(NAMES))

    def test_no_stray_exports(self):
        """Only the five designs' files (and the stats) are written: no leftover duck."""
        written = sorted(os.listdir(os.path.join(self.a, "exports")))
        self.assertEqual(written, sorted([n + e for n in NAMES for e in (".gltf", ".bin")] + ["toy_stats.json"]))

    def test_within_triangle_backstop(self):
        for n in NAMES:
            with self.subTest(n):
                self.assertGreater(self.stats[n]["tris"], 0)
                self.assertLessEqual(self.stats[n]["tris"], TRI_BACKSTOP)

    def test_pivot_on_ground_at_footprint_centre(self):
        for n in NAMES:
            with self.subTest(n):
                lo, hi = self.stats[n]["bounds_min_m"], self.stats[n]["bounds_max_m"]
                self.assertAlmostEqual(lo[2], 0.0, delta=PIVOT_TOL)       # sits on the ground
                self.assertAlmostEqual(lo[0] + hi[0], 0.0, delta=PIVOT_TOL)
                self.assertAlmostEqual(lo[1] + hi[1], 0.0, delta=PIVOT_TOL)

    def test_one_mesh_named_for_the_toy(self):
        for n in NAMES:
            with self.subTest(n):
                self.assertEqual([x["name"] for x in self.gltf[n]["nodes"]], [n])
                self.assertEqual(len(self.gltf[n]["meshes"]), 1)

    def test_materials_carry_the_toy_prefix(self):
        for n in NAMES:
            names = [m["name"] for m in self.gltf[n]["materials"]]
            self.assertTrue(names, n)
            for m in names:
                self.assertTrue(m.startswith("DH_Toy_"), m)

    def test_textures_are_referenced_not_embedded(self):
        for n in NAMES:
            for im in self.gltf[n].get("images", []):
                self.assertIn("uri", im)
                self.assertTrue(im["uri"].startswith("../textures/"), im["uri"])
                self.assertTrue(os.path.isfile(os.path.join(self.a, "exports", im["uri"])), im["uri"])

    def test_the_disc_is_solid(self):
        """The flying disc must not read as the red finder ring (#602): no hole through its middle."""
        self.assertTrue(self.stats["toy_disc"]["covers_centre"])

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
