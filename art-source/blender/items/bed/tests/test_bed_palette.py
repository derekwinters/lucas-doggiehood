"""The dog bed's colour palette (#411): ten named cover colours the game can tint a bed with.

  python3 art-source/blender/items/bed/tests/test_bed_palette.py           (under a second)
  python3 -m unittest discover -s art-source/blender/items/bed/tests

The cover and base maps are neutral, so a bed's colour is its materials' base colour
(glTF baseColorFactor), the same way the houses' roof masks take the scheme colour. export_bed.py
writes the palette to bed_colours.json from dh_bed.PALETTE, and check_bed.palette_errors() checks
it. No Blender needed: the committed JSON and glTF are read as plain files.
"""
import json
import os
import sys
import unittest

TESTS = os.path.dirname(os.path.abspath(__file__))
BED = os.path.dirname(TESTS)
sys.path.insert(0, BED)
import check_bed as C  # noqa: E402

PALETTE_JSON = os.path.join(BED, "bed_colours.json")
GLTF = os.path.join(BED, "exports", "dog_bed.gltf")


def entry(name, cover, base):
    return {"name": name, "cover": cover, "base": base}


def good_doc():
    hexes = ["#6E5B82", "#4F6F96", "#6B7280", "#3F4248", "#B5603A",
             "#9E3B36", "#C9A03E", "#C48A8F", "#2F7A7E", "#6B4A35"]
    return {"default": "C0", "colours": [entry("C%d" % i, h, C.darken(h)) for i, h in enumerate(hexes)]}


class PaletteRules(unittest.TestCase):
    """palette_errors() on hand-written palettes."""

    def test_a_good_palette_passes(self):
        self.assertEqual(C.palette_errors(good_doc()), [])

    def test_it_must_have_ten_colours(self):
        doc = good_doc()
        doc["colours"] = doc["colours"][:9]
        self.assertTrue(C.palette_errors(doc))

    def test_a_bad_hex_fails(self):
        doc = good_doc()
        doc["colours"][3]["cover"] = "#12345"
        self.assertTrue(C.palette_errors(doc))

    def test_duplicate_names_fail(self):
        doc = good_doc()
        doc["colours"][2]["name"] = doc["colours"][1]["name"]
        self.assertTrue(C.palette_errors(doc))

    def test_two_near_identical_colours_fail(self):
        doc = good_doc()
        doc["colours"][1]["cover"] = "#6F5C83"          # one step off the first
        doc["colours"][1]["base"] = C.darken("#6F5C83")
        self.assertTrue(C.palette_errors(doc))

    def test_a_cover_that_disappears_into_the_lawn_fails(self):
        doc = good_doc()
        doc["colours"][4]["cover"] = C.LAWN_HEX
        doc["colours"][4]["base"] = C.darken(C.LAWN_HEX)
        self.assertTrue(C.palette_errors(doc))

    def test_a_base_not_darker_than_its_cover_fails(self):
        doc = good_doc()
        doc["colours"][5]["base"] = doc["colours"][5]["cover"]
        self.assertTrue(C.palette_errors(doc))

    def test_the_default_must_be_in_the_palette(self):
        doc = good_doc()
        doc["default"] = "Nope"
        self.assertTrue(C.palette_errors(doc))


class CommittedPalette(unittest.TestCase):
    """The committed bed_colours.json and the glTF export agree with it."""

    @classmethod
    def setUpClass(cls):
        with open(PALETTE_JSON) as fh:
            cls.doc = json.load(fh)

    def test_the_committed_palette_passes_every_rule(self):
        self.assertEqual(C.palette_errors(self.doc), [])

    def test_it_has_ten_colours(self):
        self.assertEqual(len(self.doc["colours"]), 10)

    def test_heather_plum_is_kept_and_is_the_default(self):
        self.assertEqual(self.doc["default"], "Heather Plum")
        plum = [c for c in self.doc["colours"] if c["name"] == "Heather Plum"]
        self.assertEqual(len(plum), 1)
        self.assertEqual(plum[0]["cover"], "#6E5B82")

    def test_every_base_is_the_cover_darkened_by_the_shared_factor(self):
        for c in self.doc["colours"]:
            self.assertEqual(c["base"], C.darken(c["cover"]), c["name"])

    def test_the_export_is_tinted_with_the_default_colour(self):
        with open(GLTF) as fh:
            j = json.load(fh)
        mats = {m["name"]: m["pbrMetallicRoughness"].get("baseColorFactor") for m in j["materials"]}
        plum = [c for c in self.doc["colours"] if c["name"] == self.doc["default"]][0]
        for slot, mat in (("cover", "DH_Item_Bed_Cover"), ("base", "DH_Item_Bed_Base")):
            want = C.hex_linear(plum[slot])
            for got, w in zip(mats[mat][:3], want):
                self.assertAlmostEqual(got, w, places=4, msg=mat)

    def test_the_cushion_is_not_tinted(self):
        with open(GLTF) as fh:
            j = json.load(fh)
        cushion = [m for m in j["materials"] if m["name"] == "DH_Item_Bed_Cushion"][0]
        self.assertEqual(cushion["pbrMetallicRoughness"].get("baseColorFactor", [1, 1, 1, 1])[:3], [1, 1, 1])


if __name__ == "__main__":
    unittest.main(verbosity=2)
