"""The dog bed's checks (#411), on plain numbers and a hand-written glTF: no Blender needed.

  python3 art-source/blender/items/bed/tests/test_bed_checks.py            (under a second)
  python3 -m unittest discover -s art-source/blender/items/bed/tests

check_bed.py holds the rules export_bed.py applies to the real export:
  - the bed fits its footprint (1.60 x 1.15 m, Derek's enlargement of today's 1.4 x 1.0 m
    graybox slab) and fills it,
  - it pivots on the ground at its footprint centre,
  - it is within the provisional per-mesh triangle backstop (#799: 6,000) and the bed's own
    target (2,000),
  - the rest area on top (at least REST_AREA_MIN) is flat,
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
    def test_the_bed_footprint_is_dereks_enlarged_size(self):
        # Derek on #411: about 15% bigger than today's 1.4 x 1.0 m slab
        self.assertEqual(C.FOOTPRINT_W, 1.60)
        self.assertEqual(C.FOOTPRINT_D, 1.15)

    def test_todays_graybox_slab_is_kept_for_comparison(self):
        self.assertEqual(C.SLAB_W, 1.4)
        self.assertEqual(C.SLAB_D, 1.0)

    def test_a_bed_exactly_the_footprint_size_fits(self):
        self.assertEqual(C.footprint_errors((1.60, 1.15, 0.3)), [])

    def test_a_bed_a_little_inside_the_footprint_fits(self):
        self.assertEqual(C.footprint_errors((1.56, 1.12, 0.3)), [])

    def test_wider_than_the_footprint_fails(self):
        self.assertTrue(C.footprint_errors((1.61, 1.15, 0.3)))

    def test_deeper_than_the_footprint_fails(self):
        self.assertTrue(C.footprint_errors((1.60, 1.155, 0.3)))

    def test_todays_slab_size_is_now_too_small(self):
        # a bed that leaves the footprint mostly empty does not count, today's slab included
        self.assertTrue(C.footprint_errors((1.4, 1.0, 0.3)))
        self.assertTrue(C.footprint_errors((1.60, 1.0, 0.3)))


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

    def test_the_beds_own_target_is_about_2000(self):
        # Derek on #411: trim the bed's triangles; the target sits well under the backstop
        self.assertEqual(C.TARGET_TRIS, 2000)
        self.assertEqual(C.budget_errors(2000, C.TARGET_TRIS), [])
        self.assertTrue(C.budget_errors(2001, C.TARGET_TRIS))

    def test_6000_passes_and_6001_fails(self):
        self.assertEqual(C.budget_errors(6000), [])
        self.assertTrue(C.budget_errors(6001))


class FlatTop(unittest.TestCase):
    def test_the_rest_area_is_big_enough_for_an_adult_lab(self):
        # the flat area a dog lies on grew with the bed (was 0.80 x 0.40 m)
        self.assertGreaterEqual(C.REST_AREA_MIN[0], 0.95)
        self.assertGreaterEqual(C.REST_AREA_MIN[1], 0.50)

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
