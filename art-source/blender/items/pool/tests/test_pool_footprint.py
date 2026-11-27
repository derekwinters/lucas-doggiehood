"""The exported backyard pools match their approved sizes (#741).

  python3 -m unittest discover -s art-source/blender/items/pool/tests

Plain Python, no Blender. For each approved shape (pool_core.SHAPES: a 5 m round pool and a
6 x 3 m oval), reads exports/pool_<shape>.glb (vertex data from its binary chunk) and
exports/pool_<shape>_stats.json, and compares them with pool_core: the water size Derek set, the
footprint the dimensions add up to, and PoolPlacement.cs's height and water height (read at run
time, so no measurement is re-typed).

Invariant - each pool model, steps included, stays within its recorded footprint and never stands
taller than PoolPlacement.PoolHeight.

Checks, per shape:
  - water: the water surface spans exactly the approved size, at PoolWaterSurfaceHeight
  - height: the mesh spans exactly ground (0) to PoolHeight
  - footprint: the mesh's extent equals the footprint pool_core derives and the stats record
    (bounding radius from the origin for the circle, x/y extents for the oval), and the steps
    reach that footprint's edge on their side, so they are inside it, not added outside it
  - triangles: the GLB's count equals the stats file's and is within the art budget (#799)
Plus a guard that the parsed Core figures follow PoolPlacement's own derivation.
"""
import json
import math
import os
import struct
import sys
import unittest

TESTS = os.path.dirname(os.path.abspath(__file__))
POOL = os.path.dirname(TESTS)
sys.path.insert(0, POOL)
import pool_core as PC  # noqa: E402

EXPORTS = os.path.join(POOL, "exports")
TOL = 1e-4                       # metres; float32 vertex positions
WATER_MATERIAL = "DH_Item_pool_water"   # items name materials DH_Item_<item>_<slot>
STEP_MATERIALS = ("DH_Item_pool_step", "DH_Item_pool_riser")
APPROVED_WATER = {"circle": (5.0, 5.0), "oval": (6.0, 3.0)}   # Derek, #741 review
COMPONENT_BYTES = {5121: 1, 5123: 2, 5125: 4, 5126: 4}
COMPONENT_FMT = {5121: "B", 5123: "H", 5125: "I", 5126: "f"}
TYPE_COUNT = {"SCALAR": 1, "VEC2": 2, "VEC3": 3, "VEC4": 4}


def _read_glb(path):
    with open(path, "rb") as f:
        data = f.read()
    magic, _version, _length = struct.unpack_from("<4sII", data, 0)
    assert magic == b"glTF", "not a GLB"
    off, js, binary = 12, None, None
    while off < len(data):
        n, kind = struct.unpack_from("<II", data, off)
        chunk = data[off + 8:off + 8 + n]
        if kind == 0x4E4F534A:
            js = json.loads(chunk.decode("utf-8"))
        elif kind == 0x004E4942:
            binary = chunk
        off += 8 + n
    return js, binary


def _accessor(js, binary, index):
    acc = js["accessors"][index]
    view = js["bufferViews"][acc["bufferView"]]
    ct, n = acc["componentType"], TYPE_COUNT[acc["type"]]
    size = COMPONENT_BYTES[ct] * n
    stride = view.get("byteStride", size)
    base = view.get("byteOffset", 0) + acc.get("byteOffset", 0)
    fmt = "<%d%s" % (n, COMPONENT_FMT[ct])
    return [struct.unpack_from(fmt, binary, base + i * stride) for i in range(acc["count"])]


def _primitives(path):
    """(material name, positions in Blender axes, triangle count) per primitive. The glTF is Y-up
    (Blender x, y, z -> glTF x, z, -y) with transforms applied, so this undoes the axis swap."""
    js, binary = _read_glb(path)
    out = []
    for mesh in js["meshes"]:
        for prim in mesh["primitives"]:
            pos = [(x, -z, y) for x, y, z in _accessor(js, binary, prim["attributes"]["POSITION"])]
            tris = (js["accessors"][prim["indices"]]["count"] if "indices" in prim else len(pos)) // 3
            mat = js["materials"][prim["material"]]["name"] if "material" in prim else None
            out.append((mat, pos, tris))
    return out


class CoreFiguresParse(unittest.TestCase):
    def test_parsed_figures_follow_poolplacements_own_derivation(self):
        c = PC.core_consts()
        self.assertAlmostEqual(c["PoolHeight"], (c["DogModelBodyOffsetY"] + c["DogModelBodyTopY"]) * c["AdultDogScale"],
                               places=6)
        self.assertAlmostEqual(c["PoolWaterSurfaceHeight"], c["PoolHeight"] * (1 - c["PoolWaterDropFraction"]),
                               places=6)

    def test_both_approved_shapes_ship(self):
        self.assertEqual(tuple(PC.SHAPES), ("circle", "oval"))


class _ShapeChecks:
    SHAPE = None

    @classmethod
    def setUpClass(cls):
        cls.core = PC.core_consts()
        cls.prims = _primitives(os.path.join(EXPORTS, "pool_%s.glb" % cls.SHAPE))
        cls.verts = [v for _m, pos, _t in cls.prims for v in pos]
        cls.expect = PC.footprint(cls.SHAPE)
        with open(os.path.join(EXPORTS, "pool_%s_stats.json" % cls.SHAPE)) as f:
            cls.stats = json.load(f)

    def _of(self, materials):
        return [v for m, pos, _t in self.prims if m in materials for v in pos]

    def test_water_spans_the_approved_size_at_the_core_water_height(self):
        water = self._of((WATER_MATERIAL,))
        self.assertTrue(water, "no %s primitive" % WATER_MATERIAL)
        size = (max(v[0] for v in water) - min(v[0] for v in water), max(v[1] for v in water) - min(v[1] for v in water))
        self.assertAlmostEqual(size[0], APPROVED_WATER[self.SHAPE][0], delta=TOL)
        self.assertAlmostEqual(size[1], APPROVED_WATER[self.SHAPE][1], delta=TOL)
        for v in water:
            self.assertAlmostEqual(v[2], self.core["PoolWaterSurfaceHeight"], delta=TOL)

    def test_height_is_pool_height_from_the_ground(self):
        self.assertAlmostEqual(min(v[2] for v in self.verts), 0.0, delta=TOL)
        self.assertAlmostEqual(max(v[2] for v in self.verts), self.core["PoolHeight"], delta=TOL)
        self.assertAlmostEqual(self.stats["height_m"], self.core["PoolHeight"], delta=TOL)

    def test_extent_is_the_derived_and_recorded_footprint(self):
        xs, ys = [v[0] for v in self.verts], [v[1] for v in self.verts]
        got = {"x": [min(xs), max(xs)], "y": [min(ys), max(ys)],
               "radius": max(math.hypot(v[0], v[1]) for v in self.verts)}
        for key, rec in (("x", "footprint_x_m"), ("y", "footprint_y_m")):
            for i in range(2):
                self.assertAlmostEqual(got[key][i], self.expect[key][i], delta=TOL)
                self.assertAlmostEqual(self.stats[rec][i], self.expect[key][i], delta=TOL)
        self.assertAlmostEqual(got["radius"], self.expect["radius"], delta=TOL)
        self.assertAlmostEqual(self.stats["bounding_radius_m"], self.expect["radius"], delta=TOL)

    def test_triangles_recorded_and_within_budget(self):
        tris = sum(t for _m, _p, t in self.prims)
        self.assertEqual(tris, self.stats["tris"])
        self.assertLessEqual(tris, PC.TRI_BUDGET)


class CirclePool(_ShapeChecks, unittest.TestCase):
    SHAPE = "circle"

    def test_steps_reach_the_bounding_radius(self):
        steps = self._of(STEP_MATERIALS)
        self.assertTrue(steps, "no step primitives")
        self.assertAlmostEqual(max(math.hypot(v[0], v[1]) for v in steps), self.expect["radius"], delta=TOL)


class OvalPool(_ShapeChecks, unittest.TestCase):
    SHAPE = "oval"

    def test_steps_reach_the_front_edge_of_the_footprint(self):
        steps = self._of(STEP_MATERIALS)
        self.assertTrue(steps, "no step primitives")
        self.assertAlmostEqual(min(v[1] for v in steps), self.expect["y"][0], delta=TOL)


if __name__ == "__main__":
    unittest.main()
