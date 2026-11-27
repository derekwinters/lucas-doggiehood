"""Checks for the delivery package source asset (#410).

  python3 -m unittest discover -s art-source/blender/items/package/tests     (about 1 min)

Two groups:

  - Pure checks (no Blender): the truck-fit rule, the triangle budget and the size rule in
    package_checks.py. These always run.
  - Model checks (need bpy==5.3.0, skipped without it): the model built from dh_package.py in a
    fresh process measures what DIMS says, pivots on the ground at its footprint centre and is
    inside the budget; the committed .glb reads back as one single-sided, self-contained mesh of
    that size; and exporting twice into a temporary folder rewrites the committed export,
    stats and textures byte for byte (the reproducibility invariant in
    docs/engineering/blender-pipeline.md). Nothing is written into the repository.
"""
import hashlib
import importlib.util
import json
import os
import shutil
import struct
import subprocess
import sys
import tempfile
import unittest

TESTS = os.path.dirname(os.path.abspath(__file__))
HERE = os.path.dirname(TESTS)
sys.path.insert(0, HERE)

import package_checks as C  # noqa: E402

# every committed output of export_package.py, relative to the package folder
OUTPUTS = (
    "exports/package.glb",
    "exports/package_stats.json",
    "textures/package_cardboard_color.png",
    "textures/package_cardboard_normal.png",
    "textures/package_label_color.png",
)
MM = 0.001


def has_bpy():
    return importlib.util.find_spec("bpy") is not None


def sha(path):
    with open(path, "rb") as fh:
        return hashlib.sha256(fh.read()).hexdigest()


def run(args, timeout=900):
    r = subprocess.run([sys.executable] + args, capture_output=True, text=True, timeout=timeout)
    return r.returncode, r.stdout + r.stderr


def read_glb_json(path):
    with open(path, "rb") as fh:
        data = fh.read()
    magic, _version, _length = struct.unpack_from("<III", data, 0)
    assert magic == 0x46546C67, "not a GLB"
    chunk_len, chunk_type = struct.unpack_from("<II", data, 12)
    assert chunk_type == 0x4E4F534A, "first GLB chunk is not JSON"
    return json.loads(data[20:20 + chunk_len].decode("utf-8"))


# ---------------------------------------------------------------------------
# pure checks
# ---------------------------------------------------------------------------
class TruckFit(unittest.TestCase):

    def test_a_box_smaller_than_the_cargo_fits(self):
        self.assertTrue(C.fits_in_cargo((0.6, 0.5, 0.45), (1.0, 1.0, 1.0)))

    def test_a_box_too_long_for_both_plan_axes_does_not_fit(self):
        self.assertFalse(C.fits_in_cargo((0.6, 0.6, 0.45), (0.55, 1.0, 1.0)))

    def test_a_box_may_be_turned_a_quarter_to_fit(self):
        # 0.6 is too long for the 0.55 side, but turned 90 degrees the 0.5 side goes there
        self.assertTrue(C.fits_in_cargo((0.6, 0.5, 0.45), (0.55, 1.0, 1.0)))

    def test_a_box_too_tall_does_not_fit(self):
        self.assertFalse(C.fits_in_cargo((0.6, 0.5, 0.45), (1.0, 1.0, 0.40)))

    def test_clearance_is_kept_on_every_side(self):
        self.assertTrue(C.fits_in_cargo((0.6, 0.5, 0.45), (0.70, 0.60, 0.55), clearance=0.05))
        self.assertFalse(C.fits_in_cargo((0.6, 0.5, 0.45), (0.69, 0.60, 0.55), clearance=0.05))

    def test_an_unstated_height_checks_the_plan_only(self):
        self.assertTrue(C.fits_in_cargo((0.6, 0.5, 9.0), (1.0, 1.0, None)))
        self.assertFalse(C.fits_in_cargo((0.6, 0.6, 0.1), (0.55, 0.55, None)))

    def test_the_stated_truck_footprint_is_todays_truck(self):
        # DeliveryTruckFootprint (Core, #660 / #672): 3.25 m x 1.5 m at import, ModelScale 1.5
        self.assertAlmostEqual(C.TRUCK_FOOTPRINT[0], 4.875, places=6)
        self.assertAlmostEqual(C.TRUCK_FOOTPRINT[1], 2.25, places=6)


class Budget(unittest.TestCase):

    def test_the_backstop_is_the_provisional_6000(self):
        self.assertEqual(C.ART_BUDGET_TRIS, 6000)

    def test_6000_passes_and_6001_fails(self):
        self.assertTrue(C.within_budget(6000))
        self.assertFalse(C.within_budget(6001))


class Size(unittest.TestCase):

    def test_the_graybox_cube_is_0_6_m(self):
        self.assertAlmostEqual(C.GRAYBOX_SIZE, 0.6, places=6)

    def test_a_box_whose_longest_side_is_near_the_graybox_passes(self):
        self.assertTrue(C.near_graybox((0.6, 0.5, 0.45)))
        self.assertTrue(C.near_graybox((0.45, 0.64, 0.5)))

    def test_a_box_much_bigger_or_smaller_than_the_graybox_fails(self):
        self.assertFalse(C.near_graybox((0.9, 0.5, 0.45)))
        self.assertFalse(C.near_graybox((0.4, 0.3, 0.3)))


# ---------------------------------------------------------------------------
# model checks
# ---------------------------------------------------------------------------
MEASURE_SCRIPT = r'''
import json, sys
sys.path.insert(0, %(here)r)
import bpy
from mathutils import Vector
import dh_package as P
P.K.reset_scene()
ob = P.build()
bpy.context.view_layer.update()
pts = [ob.matrix_world @ Vector(c) for c in ob.bound_box]
print("MEASURE " + json.dumps({
    "min": [min(p[i] for p in pts) for i in range(3)],
    "max": [max(p[i] for p in pts) for i in range(3)],
    "tris": sum(len(p.vertices) - 2 for p in ob.data.polygons),
    "meshes": sum(1 for o in bpy.data.objects if o.type == "MESH"),
    "name": ob.name,
    "materials": [m.name for m in ob.data.materials],
    "dims": [P.DIMS["length"], P.DIMS["depth"], P.DIMS["height"]],
}))
'''


@unittest.skipUnless(has_bpy(), "needs bpy==5.3.0")
class Model(unittest.TestCase):

    @classmethod
    def setUpClass(cls):
        tmp = tempfile.mkdtemp(prefix="dh_package_")
        try:
            # build against throwaway textures so nothing is written into the repository
            code, out = run(["-c", "import sys; sys.path.insert(0, %r); import dh_package_tex as T; "
                                   "T.write_all(%r)" % (HERE, os.path.join(tmp, "textures"))])
            assert code == 0, out[-3000:]
            env_code = ("import os; os.environ['DH_PACKAGE_TEXTURES'] = %r\n"
                        % os.path.join(tmp, "textures"))
            code, out = run(["-c", env_code + MEASURE_SCRIPT % {"here": HERE}])
            assert code == 0, out[-3000:]
            line = [ln for ln in out.splitlines() if ln.startswith("MEASURE ")][-1]
            cls.m = json.loads(line[len("MEASURE "):])
        finally:
            shutil.rmtree(tmp, ignore_errors=True)

    def test_one_mesh_named_package(self):
        self.assertEqual(self.m["meshes"], 1)
        self.assertEqual(self.m["name"], "package")

    def test_size_matches_dims(self):
        size = [self.m["max"][i] - self.m["min"][i] for i in range(3)]
        for got, want in zip(size, self.m["dims"]):
            self.assertAlmostEqual(got, want, delta=MM)

    def test_size_is_near_todays_graybox(self):
        self.assertTrue(C.near_graybox(self.m["dims"]), self.m["dims"])

    def test_pivot_on_the_ground_at_the_footprint_centre(self):
        self.assertAlmostEqual(self.m["min"][2], 0.0, delta=MM)
        for i in (0, 1):
            self.assertAlmostEqual(self.m["min"][i] + self.m["max"][i], 0.0, delta=MM)

    def test_within_the_art_budget(self):
        self.assertTrue(C.within_budget(self.m["tris"]), self.m["tris"])

    def test_materials_carry_the_package_prefix(self):
        self.assertTrue(self.m["materials"])
        for name in self.m["materials"]:
            self.assertTrue(name.startswith("DH_Package_"), name)

    def test_fits_the_stated_truck_footprint(self):
        size = [self.m["max"][i] - self.m["min"][i] for i in range(3)]
        self.assertTrue(C.fits_in_cargo(size, C.TRUCK_FOOTPRINT + (None,)))


class CommittedExport(unittest.TestCase):
    """Reads the committed .glb back; no Blender needed."""

    @classmethod
    def setUpClass(cls):
        cls.j = read_glb_json(os.path.join(HERE, "exports", "package.glb"))
        with open(os.path.join(HERE, "exports", "package_stats.json")) as fh:
            cls.stats = json.load(fh)

    def test_one_mesh_on_one_node_named_package(self):
        self.assertEqual(len(self.j["meshes"]), 1)
        self.assertEqual([n["name"] for n in self.j["nodes"] if "mesh" in n], ["package"])

    def test_materials_single_sided_and_prefixed(self):
        for m in self.j["materials"]:
            self.assertTrue(m["name"].startswith("DH_Package_"), m["name"])
            self.assertFalse(m.get("doubleSided", False), m["name"])

    def test_images_are_embedded_for_a_web_viewer(self):
        self.assertTrue(self.j.get("images"))
        for im in self.j["images"]:
            self.assertNotIn("uri", im)
            self.assertIn("bufferView", im)

    def test_no_animations_and_no_skins(self):
        self.assertNotIn("animations", self.j)
        self.assertNotIn("skins", self.j)

    def test_size_in_the_file_matches_the_stats(self):
        # glTF is Y-up: Blender X, Y, Z (length, depth, height) -> glTF X, Z, Y
        acc = [self.j["accessors"][p["attributes"]["POSITION"]] for p in self.j["meshes"][0]["primitives"]]
        lo = [min(a["min"][i] for a in acc) for i in range(3)]
        hi = [max(a["max"][i] for a in acc) for i in range(3)]
        size = [hi[0] - lo[0], hi[2] - lo[2], hi[1] - lo[1]]
        for got, want in zip(size, self.stats["size_xyz_m"]):
            self.assertAlmostEqual(got, want, delta=MM)
        self.assertAlmostEqual(lo[1], 0.0, delta=MM)          # on the ground

    def test_stats_record_the_checks(self):
        s = self.stats
        self.assertTrue(C.within_budget(s["tris"]))
        self.assertEqual(s["budget_tris"], C.ART_BUDGET_TRIS)
        self.assertTrue(s["truck_footprint_fit"])
        self.assertTrue(s["all_checks_ok"])


@unittest.skipUnless(has_bpy(), "needs bpy==5.3.0")
class RebuildsByteIdentical(unittest.TestCase):

    def test_two_exports_match_the_committed_files(self):
        want = {f: sha(os.path.join(HERE, f)) for f in OUTPUTS}
        for i in range(2):
            tmp = tempfile.mkdtemp(prefix="dh_package_rebuild_")
            try:
                code, out = run([os.path.join(HERE, "export_package.py"), tmp])
                self.assertEqual(code, 0, out[-3000:])
                self.assertIn("ALL OK", out)
                got = {f: sha(os.path.join(tmp, f)) for f in OUTPUTS}
                differ = sorted(f for f in OUTPUTS if got[f] != want[f])
                print("rebuild %d of 2: %s" % (i + 1, "byte-identical" if not differ
                                               else "DIFFERS: " + ", ".join(differ)), flush=True)
                self.assertEqual(differ, [], "rebuild %d differs from the committed files" % (i + 1))
            finally:
                shutil.rmtree(tmp, ignore_errors=True)


if __name__ == "__main__":
    unittest.main(verbosity=2)
