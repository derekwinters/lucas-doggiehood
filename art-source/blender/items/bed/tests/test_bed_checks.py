"""The dog bed's checks (#411), on plain numbers and a hand-written glTF: no Blender needed.

  python3 art-source/blender/items/bed/tests/test_bed_checks.py            (under a second)
  python3 -m unittest discover -s art-source/blender/items/bed/tests

check_bed.py holds the rules export_bed.py applies to the real export:
  - the bed fits today's graybox slab footprint (1.4 x 1.0 m, DecorationView) and fills it,
  - it pivots on the ground at its footprint centre,
  - it is within the provisional per-mesh triangle backstop (#799: 6,000),
  - the rest area on top is flat,
  - the glTF's images resolve inside textures/, its materials carry the DH_Item_Bed_ prefix and
    it carries the ATTACH_REST node.
"""
import os
import sys
import tempfile
import unittest

TESTS = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.dirname(TESTS))
import check_bed as C  # noqa: E402


class Footprint(unittest.TestCase):
    def test_the_slab_numbers_are_todays_graybox(self):
        self.assertEqual(C.SLAB_W, 1.4)
        self.assertEqual(C.SLAB_D, 1.0)

    def test_a_bed_exactly_the_slab_size_fits(self):
        self.assertEqual(C.footprint_errors((1.4, 1.0, 0.3)), [])

    def test_a_bed_a_little_inside_the_slab_fits(self):
        self.assertEqual(C.footprint_errors((1.37, 0.97, 0.3)), [])

    def test_wider_than_the_slab_fails(self):
        self.assertTrue(C.footprint_errors((1.41, 1.0, 0.3)))

    def test_deeper_than_the_slab_fails(self):
        self.assertTrue(C.footprint_errors((1.4, 1.005, 0.3)))

    def test_much_smaller_than_the_slab_fails(self):
        # "about today's slab footprint": a bed that leaves the slab mostly empty does not count
        self.assertTrue(C.footprint_errors((1.2, 1.0, 0.3)))
        self.assertTrue(C.footprint_errors((1.4, 0.8, 0.3)))


class Pivot(unittest.TestCase):
    def test_centred_on_the_ground_passes(self):
        self.assertEqual(C.pivot_errors((-0.7, -0.5, 0.0), (0.7, 0.5, 0.3)), [])

    def test_off_centre_fails(self):
        self.assertTrue(C.pivot_errors((-0.6, -0.5, 0.0), (0.8, 0.5, 0.3)))

    def test_floating_or_sunk_fails(self):
        self.assertTrue(C.pivot_errors((-0.7, -0.5, 0.01), (0.7, 0.5, 0.3)))
        self.assertTrue(C.pivot_errors((-0.7, -0.5, -0.01), (0.7, 0.5, 0.3)))


class Budget(unittest.TestCase):
    def test_the_backstop_is_799s_provisional_6000(self):
        self.assertEqual(C.BUDGET_TRIS, 6000)

    def test_6000_passes_and_6001_fails(self):
        self.assertEqual(C.budget_errors(6000), [])
        self.assertTrue(C.budget_errors(6001))


class FlatTop(unittest.TestCase):
    def test_samples_all_at_the_rest_height_pass(self):
        self.assertEqual(C.flat_top_errors([0.17, 0.171, 0.169, 0.172], 0.17), [])

    def test_a_bump_fails(self):
        self.assertTrue(C.flat_top_errors([0.17, 0.17, 0.20, 0.17], 0.17))

    def test_a_missed_sample_fails(self):
        # a ray that found no cushion (None) is a hole in the rest area
        self.assertTrue(C.flat_top_errors([0.17, None, 0.17], 0.17))

    def test_no_samples_fails(self):
        self.assertTrue(C.flat_top_errors([], 0.17))


class Gltf(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory(prefix="dh_bed_check_")
        root = self.tmp.name
        self.tex = os.path.join(root, "textures")
        self.exp = os.path.join(root, "exports")
        os.makedirs(self.tex)
        os.makedirs(self.exp)
        open(os.path.join(self.tex, "bed_cover_color.png"), "wb").close()
        self.path = os.path.join(self.exp, "dog_bed.gltf")

    def tearDown(self):
        self.tmp.cleanup()

    def good(self):
        return {"images": [{"uri": "../textures/bed_cover_color.png"}],
                "materials": [{"name": "DH_Item_Bed_Cover"}],
                "nodes": [{"name": "dog_bed", "mesh": 0, "children": [1]}, {"name": "ATTACH_REST"}]}

    def test_a_good_gltf_passes(self):
        self.assertEqual(C.gltf_errors(self.good(), self.path, self.tex), [])

    def test_an_absolute_image_uri_fails(self):
        j = self.good()
        j["images"][0]["uri"] = os.path.join(self.tex, "bed_cover_color.png")
        self.assertTrue(C.gltf_errors(j, self.path, self.tex))

    def test_an_embedded_image_fails(self):
        j = self.good()
        j["images"][0] = {"bufferView": 3, "mimeType": "image/png"}
        self.assertTrue(C.gltf_errors(j, self.path, self.tex))

    def test_a_missing_image_fails(self):
        j = self.good()
        j["images"][0]["uri"] = "../textures/nope.png"
        self.assertTrue(C.gltf_errors(j, self.path, self.tex))

    def test_a_material_without_the_set_prefix_fails(self):
        j = self.good()
        j["materials"][0]["name"] = "Material.001"
        self.assertTrue(C.gltf_errors(j, self.path, self.tex))

    def test_no_rest_node_fails(self):
        j = self.good()
        j["nodes"] = j["nodes"][:1]
        self.assertTrue(C.gltf_errors(j, self.path, self.tex))


if __name__ == "__main__":
    unittest.main(verbosity=2)
