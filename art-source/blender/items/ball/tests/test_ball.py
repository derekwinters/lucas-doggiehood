"""Checks for the dog ball (#333).

  python3 -m unittest discover -s art-source/blender/items/ball/tests      (about 1 min)
  python3 -m pytest art-source/blender/items/ball/tests

Invariant - the committed ball (every colour's export and texture) is rebuilt byte for byte from
the scripts in this folder, every run and in fresh processes.

The ball comes in the six felt colours Derek picked, like the dogs' coats: one shared mesh and
normal map, one colour map and one export per colour.

The seam and texture tests need only numpy and Pillow. The export tests need bpy==5.3.0 and are
skipped without it; they run export_ball.py twice in fresh processes into temporary folders, so
nothing is written into the repository.
"""
import hashlib
import importlib.util
import json
import os
import shutil
import subprocess
import sys
import tempfile
import unittest

import numpy as np

TESTS = os.path.dirname(os.path.abspath(__file__))
BALL = os.path.dirname(TESTS)
sys.path.insert(0, BALL)

import dh_ball_tex as BT  # noqa: E402

# Derek's picks (2026-11-27): no optic yellow, no teal.
PICKED = {"blue": "#2F7FD8", "orange": "#F2711C", "red": "#D8302A", "magenta": "#E0358F",
          "purple": "#7A4FC9", "navy": "#24409A"}
EXPORTS = sorted(["ball_%s.%s" % (k, e) for k in PICKED for e in ("gltf", "bin")] + ["ball_stats.json"])
TEXTURES = sorted(["ball_felt_%s_color.png" % k for k in PICKED] + ["ball_felt_normal.png"])
COMMITTED = ["exports/" + f for f in EXPORTS] + ["textures/" + f for f in TEXTURES]


def sha(path):
    with open(path, "rb") as f:
        return hashlib.sha256(f.read()).hexdigest()


def has_bpy():
    return importlib.util.find_spec("bpy") is not None


def rgb(h):
    return np.array([int(h[i:i + 2], 16) for i in (1, 3, 5)], np.float64)


class SeamCurve(unittest.TestCase):
    def test_seam_lies_on_the_ball_and_closes(self):
        p = BT.seam_points(720)
        self.assertTrue(np.allclose(np.linalg.norm(p, axis=1), 1.0, atol=1e-9))
        step = np.linalg.norm(np.diff(np.vstack([p, p[:1]]), axis=0), axis=1)
        self.assertLess(step.max(), 4 * np.median(step))   # no jump: the loop closes on itself

    def test_seam_splits_the_ball_into_two_equal_lobes(self):
        # the curve is symmetric, so as much felt lies on one side as the other
        rng = np.random.default_rng(1)
        d = rng.normal(size=(20000, 3))
        d /= np.linalg.norm(d, axis=1, keepdims=True)
        side = BT.seam_side(d)
        self.assertAlmostEqual(float(np.mean(side > 0)), 0.5, delta=0.02)

    def test_seam_distance_is_zero_on_the_seam_and_large_in_a_lobe(self):
        on = BT.seam_points(97)
        self.assertLess(float(BT.seam_angle(on).max()), 0.01)
        lobe = np.array([[0.0, 0.0, 1.0]])            # the centre of a lobe, under the seam's top arch
        self.assertGreater(float(BT.seam_angle(lobe)[0]), 0.3)


class GameSize(unittest.TestCase):
    def test_the_game_ball_is_25_cm_a_readable_exaggeration_of_a_real_tennis_ball(self):
        # Derek's review: the real 6.7 cm ball is barely visible at the game camera; of the 20, 25
        # and 30 cm options he picked 25 cm (about 3.7x real).
        self.assertAlmostEqual(BT.REAL_DIAMETER_M, 0.067, places=4)
        self.assertAlmostEqual(BT.DIAMETER_M, 0.25, places=4)


class Variants(unittest.TestCase):
    def test_the_six_picked_colours_in_order(self):
        self.assertEqual(list(BT.VARIANTS.items()), list(PICKED.items()))

    def test_one_colour_map_per_variant_and_one_shared_normal_map(self):
        self.assertEqual(sorted(BT.TEXTURES), TEXTURES)


class Textures(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.dirs = [tempfile.mkdtemp(), tempfile.mkdtemp()]
        for d in cls.dirs:
            BT.build(d)

    @classmethod
    def tearDownClass(cls):
        for d in cls.dirs:
            shutil.rmtree(d)

    def test_textures_rebuild_byte_for_byte(self):
        a, b = self.dirs
        self.assertEqual(sorted(os.listdir(a)), TEXTURES)
        for f in TEXTURES:
            self.assertEqual(sha(os.path.join(a, f)), sha(os.path.join(b, f)), f)
            self.assertEqual(sha(os.path.join(a, f)), sha(os.path.join(BALL, "textures", f)), f)

    def test_each_variant_is_its_felt_colour_with_the_white_seam(self):
        from PIL import Image
        seam = rgb(BT.SEAM_HEX)
        ang = None
        for k, h in PICKED.items():
            col = np.asarray(Image.open(os.path.join(self.dirs[0], "ball_felt_%s_color.png" % k)).convert("RGB"),
                             np.float64)
            if ang is None:
                d = BT.texel_directions(col.shape[1], col.shape[0])
                ang = BT.seam_angle(d.reshape(-1, 3)).reshape(col.shape[:2])
            self.assertLess(np.abs(col[ang > 2 * BT.SEAM_HALF_ANGLE].mean(0) - rgb(h)).max(), 12, k)
            self.assertLess(np.abs(col[ang < 0.3 * BT.SEAM_HALF_ANGLE].mean(0) - seam).max(), 12, k)


@unittest.skipUnless(has_bpy(), "needs bpy==5.3.0")
class Export(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.roots = [tempfile.mkdtemp() for _ in range(2)]
        for r in cls.roots:
            subprocess.run([sys.executable, os.path.join(BALL, "export_ball.py"), r], check=True,
                           stdout=subprocess.DEVNULL)
        with open(os.path.join(cls.roots[0], "exports", "ball_stats.json")) as f:
            cls.stats = json.load(f)

    @classmethod
    def tearDownClass(cls):
        for r in cls.roots:
            shutil.rmtree(r)

    def test_writes_one_export_per_variant_and_nothing_else(self):
        self.assertEqual(sorted(os.listdir(os.path.join(self.roots[0], "exports"))), EXPORTS)

    def test_two_runs_are_byte_identical_and_match_the_committed_files(self):
        for rel in COMMITTED:
            a, b = (os.path.join(r, rel) for r in self.roots)
            self.assertEqual(sha(a), sha(b), rel)
            self.assertEqual(sha(a), sha(os.path.join(BALL, rel)), rel)

    def test_within_the_art_budget(self):
        self.assertLessEqual(self.stats["tris"], BT.TRI_BUDGET)

    def test_game_size_with_the_pivot_on_the_ground(self):
        s = self.stats
        for v in s["size_xyz_m"]:
            self.assertAlmostEqual(v, BT.DIAMETER_M, delta=0.002)
        lo, hi = s["bbox_min_m"], s["bbox_max_m"]
        self.assertAlmostEqual(lo[2], 0.0, places=4)          # rests on the ground at its origin
        self.assertAlmostEqual(lo[0] + hi[0], 0.0, places=4)  # centred over the origin
        self.assertAlmostEqual(lo[1] + hi[1], 0.0, places=4)

    def test_each_variant_is_named_like_the_dog_coats_and_links_its_own_colour_map(self):
        v = self.stats["variants"]
        self.assertEqual(list(v), list(PICKED))
        for k, h in PICKED.items():
            s = v[k]
            self.assertEqual(s["hex"], h)
            self.assertEqual(s["object"], "ball")
            self.assertEqual(s["materials"], ["DH_Item_Ball_" + k.capitalize()])
            self.assertEqual(s["images"], ["../textures/ball_felt_%s_color.png" % k, "../textures/ball_felt_normal.png"])
            self.assertTrue(s["uris_ok"] and s["textured_ok"] and s["single_sided_ok"], k)
