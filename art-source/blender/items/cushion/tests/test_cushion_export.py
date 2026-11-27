"""The cushion's export: inside the slab footprint, a top a dog can rest on, inside the budget,
nothing extra, and reproducible (#412).

  python3 art-source/blender/items/cushion/tests/test_cushion_export.py      (about 20 s)
  python3 -m unittest discover -s art-source/blender/items/cushion/tests

Builds the cushion with export_cushion.py into a temporary folder twice (fresh processes,
bpy==5.3.0), reads the written .gltf + .bin back with plain Python and checks:
  - Invariant - the cushion fits today's slab footprint: its ground footprint is at most
    1.4 m (glTF X) by 1.0 m (glTF Z), centred on the pivot, so placement does not change.
  - it sits on the ground: the lowest point is at the pivot height (0), nothing below it.
  - a dog can rest on it: over the central rest area the top of the fabric is a near-level
    plateau, high enough to read as a cushion (not a flat blanket).
  - the triangle count is inside the art budget (#799) and matches the stats JSON.
  - the file holds only the cushion: one node, one mesh, flat colours (no images, textures,
    skins, animations, cameras or extensions) and nothing double-sided.
  - Invariant - the committed export is reproducible: a second build, in another fresh process,
    gives files byte-identical to the first and to the committed exports/.
Nothing is written into the repository.
"""
import json
import os
import shutil
import struct
import subprocess
import sys
import tempfile
import unittest

TESTS = os.path.dirname(os.path.abspath(__file__))
CUSHION = os.path.dirname(TESTS)
EXPORT = os.path.join(CUSHION, "export_cushion.py")
STEM = "item_cushion"
OUTPUTS = ("exports/%s.gltf" % STEM, "exports/%s.bin" % STEM, "exports/cushion_stats.json")

FOOTPRINT_X_M = 1.4          # today's DecorationView slab, long side (glTF / Unity X)
FOOTPRINT_Z_M = 1.0          # short side (glTF / Unity Z)
CENTRE_TOL_M = 0.005         # the footprint is centred on the pivot
GROUND_TOL_M = 0.001         # the lowest point sits on the pivot's ground plane
MAX_TRIS = 6000              # the provisional per-mesh backstop (#799)
FABRIC_MATERIAL = "DH_Item_cushion_fabric"
REST_HALF_X_M = 0.40         # the central rest area: 0.8 x 0.5 m, where a lying dog's body goes
REST_HALF_Z_M = 0.25
REST_FLAT_TOL_M = 0.05       # top heights over the rest area vary by at most this (tufting dimples)
REST_MIN_TOP_M = 0.15        # the rest area is at least this high: a cushion, not a blanket

COMP = {5120: ("b", 1), 5121: ("B", 1), 5122: ("h", 2), 5123: ("H", 2), 5125: ("I", 4), 5126: ("f", 4)}
NCOMP = {"SCALAR": 1, "VEC2": 2, "VEC3": 3, "VEC4": 4, "MAT4": 16}


def build(root):
    r = subprocess.run([sys.executable, EXPORT, root], capture_output=True, text=True, timeout=900)
    assert r.returncode == 0, (r.stdout + r.stderr)[-4000:]
    return r.stdout


def read(path, mode="rb"):
    with open(path, mode) as fh:
        return fh.read()


def accessor(j, binb, i):
    a = j["accessors"][i]
    v = j["bufferViews"][a["bufferView"]]
    fmt, size = COMP[a["componentType"]]
    n = NCOMP[a["type"]]
    stride = v.get("byteStride", size * n)
    off = v.get("byteOffset", 0) + a.get("byteOffset", 0)
    return [list(struct.unpack_from("<%d%s" % (n, fmt), binb, off + k * stride)) for k in range(a["count"])]


class CushionExport(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.tmp = tempfile.mkdtemp(prefix="dh_cushion_")
        cls.a = os.path.join(cls.tmp, "a")
        cls.b = os.path.join(cls.tmp, "b")
        build(cls.a)
        build(cls.b)
        cls.j = json.loads(read(os.path.join(cls.a, OUTPUTS[0]), "r"))
        cls.binb = read(os.path.join(cls.a, OUTPUTS[1]))
        cls.stats = json.loads(read(os.path.join(cls.a, OUTPUTS[2]), "r"))

    @classmethod
    def tearDownClass(cls):
        shutil.rmtree(cls.tmp, ignore_errors=True)

    def primitives(self):
        for m in self.j["meshes"]:
            for p in m["primitives"]:
                yield p

    def positions(self, material=None):
        pts = []
        for p in self.primitives():
            if material and self.j["materials"][p["material"]]["name"] != material:
                continue
            pts += accessor(self.j, self.binb, p["attributes"]["POSITION"])
        return pts

    def test_fits_slab_footprint(self):
        for n in self.j["nodes"]:     # positions are in model space only if no node moves them
            for key in ("translation", "rotation", "scale", "matrix"):
                self.assertNotIn(key, n, "node %s has a %s" % (n.get("name"), key))
        pts = self.positions()
        xs, zs = [p[0] for p in pts], [p[2] for p in pts]
        self.assertLessEqual(max(xs) - min(xs), FOOTPRINT_X_M)
        self.assertLessEqual(max(zs) - min(zs), FOOTPRINT_Z_M)
        self.assertLessEqual(abs(max(xs) + min(xs)) / 2, CENTRE_TOL_M)
        self.assertLessEqual(abs(max(zs) + min(zs)) / 2, CENTRE_TOL_M)

    def test_sits_on_the_ground(self):
        ys = [p[1] for p in self.positions()]          # glTF up is +Y
        self.assertLessEqual(abs(min(ys)), GROUND_TOL_M)

    def test_top_is_restable(self):
        fabric = self.positions(FABRIC_MATERIAL)
        self.assertTrue(fabric, "no %s primitive" % FABRIC_MATERIAL)
        mid = (max(p[1] for p in fabric) + min(p[1] for p in fabric)) / 2
        top = [p[1] for p in fabric
               if abs(p[0]) <= REST_HALF_X_M and abs(p[2]) <= REST_HALF_Z_M and p[1] > mid]
        self.assertTrue(top, "no top vertices over the rest area")
        self.assertLessEqual(max(top) - min(top), REST_FLAT_TOL_M)
        self.assertGreaterEqual(min(top), REST_MIN_TOP_M)

    def test_triangles_within_budget(self):
        tris = 0
        for p in self.primitives():
            self.assertEqual(p.get("mode", 4), 4)
            tris += self.j["accessors"][p["indices"]]["count"] // 3
        self.assertLessEqual(tris, MAX_TRIS)
        self.assertEqual(tris, self.stats["tris"]["total"])

    def test_nothing_extra(self):
        self.assertEqual([n.get("name") for n in self.j["nodes"]], [STEM])
        self.assertEqual(len(self.j["meshes"]), 1)
        for key in ("cameras", "skins", "animations", "images", "textures", "samplers",
                    "extensionsUsed", "extensionsRequired"):
            self.assertNotIn(key, self.j, key)
        for n in self.j["nodes"]:
            self.assertNotIn("extensions", n)
        for m in self.j["materials"]:
            self.assertTrue(m["name"].startswith("DH_Item_cushion_"), m["name"])
            self.assertFalse(m.get("doubleSided", False), m["name"])
            self.assertNotIn("extensions", m)

    def test_rebuild_is_byte_identical(self):
        for rel in OUTPUTS:
            a = read(os.path.join(self.a, rel))
            b = read(os.path.join(self.b, rel))
            self.assertEqual(a, b, "%s differs between two builds" % rel)
            committed = os.path.join(CUSHION, rel)
            self.assertTrue(os.path.exists(committed), "%s is not committed" % rel)
            self.assertEqual(a, read(committed), "%s differs from the committed file" % rel)


if __name__ == "__main__":
    unittest.main(verbosity=2)
