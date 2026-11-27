"""The built dog bed (#411) fits the slab, sits on the ground, stays in budget and has a flat
padded top a dog can lie on.

  python3 art-source/blender/items/bed/tests/test_bed_model.py             (a few seconds)
  python3 -m unittest discover -s art-source/blender/items/bed/tests

Needs bpy (==5.3.0). The bed is built in a fresh process (dh_bed.build()) and measured there:
its world bounds, its triangle count, the ATTACH_REST empty, and rays cast straight down over
the rest area (DIMS["rest_area"], centred on ATTACH_REST), which must all land on the cushion at
the rest height. Textures go to a temporary folder; nothing is written into the repository.

Invariant - the bed never leaves today's slab footprint (1.4 x 1.0 m), so placing a bed instead
of the graybox slab moves nothing.
"""
import importlib.util
import json
import os
import subprocess
import sys
import tempfile
import unittest

TESTS = os.path.dirname(os.path.abspath(__file__))
BED = os.path.dirname(TESTS)
sys.path.insert(0, BED)
import check_bed as C  # noqa: E402

PROBE = r'''
import json, sys
sys.path.insert(0, %(bed)r)
import bpy
from mathutils import Vector
import dh_bed as BD
import dh_house_kit as K
K.reset_scene()
ob = BD.build(tex_dir=%(tex)r)
bpy.context.view_layer.update()
meshes = [o for o in [ob] + list(ob.children_recursive) if o.type == "MESH"]
pts = [o.matrix_world @ v.co for o in meshes for v in o.data.vertices]
lo = [min(p[i] for p in pts) for i in range(3)]
hi = [max(p[i] for p in pts) for i in range(3)]
rest = [o for o in ob.children_recursive if o.name == BD.REST_NAME]
rest_loc = list(rest[0].matrix_world.translation) if rest else None
dg = bpy.context.evaluated_depsgraph_get()
rw, rd = BD.DIMS["rest_area"]
c = Vector(rest_loc) if rest else Vector((0, 0, 0))
zs = []
n = 9
for i in range(n):
    for k in range(n):
        o = Vector((c.x - rw / 2 + rw * i / (n - 1), c.y - rd / 2 + rd * k / (n - 1), 5.0))
        hit, loc, nrm, idx, hob, mat = bpy.context.scene.ray_cast(dg, o, Vector((0, 0, -1)))
        zs.append(loc.z if hit else None)
def top_at(x, y):
    hit, loc, *_ = bpy.context.scene.ray_cast(dg, Vector((x, y, 5.0)), Vector((0, 0, -1)))
    return loc.z if hit else None
print("PROBE " + json.dumps({
    "lo": lo, "hi": hi, "tris": sum(K.tri_count(o) for o in meshes), "rest": rest_loc,
    "rest_z": BD.DIMS["rest_z"], "zs": zs, "name": ob.name,
    "back_top": top_at(0.0, hi[1] - BD.DIMS["bolster_w"] / 2),
    "front_top": top_at(0.0, lo[1] + BD.DIMS["bolster_w"] / 2),
    "side_top": top_at(hi[0] - BD.DIMS["bolster_w"] / 2, 0.0),
    "materials": sorted({m.name for o in meshes for m in o.data.materials}),
}))
'''


def has_bpy():
    return importlib.util.find_spec("bpy") is not None


@unittest.skipUnless(has_bpy(), "needs bpy==5.3.0")
class BuiltBed(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        with tempfile.TemporaryDirectory(prefix="dh_bed_model_") as tmp:
            r = subprocess.run([sys.executable, "-c", PROBE % {"bed": BED, "tex": tmp}], capture_output=True,
                               text=True, timeout=600)
        out = r.stdout + r.stderr
        if r.returncode != 0:
            raise AssertionError(out[-3000:])
        line = [ln for ln in r.stdout.splitlines() if ln.startswith("PROBE ")]
        if len(line) != 1:
            raise AssertionError(out[-3000:])
        cls.m = json.loads(line[0][len("PROBE "):])

    def size(self):
        return [self.m["hi"][i] - self.m["lo"][i] for i in range(3)]

    def test_object_name(self):
        self.assertEqual(self.m["name"], "dog_bed")

    def test_fits_the_slab_footprint(self):
        self.assertEqual(C.footprint_errors(self.size()), [], self.size())

    def test_pivots_on_the_ground_at_the_footprint_centre(self):
        self.assertEqual(C.pivot_errors(self.m["lo"], self.m["hi"]), [], (self.m["lo"], self.m["hi"]))

    def test_within_the_triangle_backstop(self):
        self.assertEqual(C.budget_errors(self.m["tris"]), [], self.m["tris"])

    def test_rest_point_is_on_the_cushion_top_at_the_centre_line(self):
        rest = self.m["rest"]
        self.assertIsNotNone(rest, "no ATTACH_REST empty")
        self.assertAlmostEqual(rest[0], 0.0, places=3)
        self.assertAlmostEqual(rest[2], self.m["rest_z"], places=3)
        # the cushion sits inside the bolster, so the rest point is inside the footprint
        self.assertLess(abs(rest[1]), C.SLAB_D / 2)

    def test_the_rest_area_is_flat_at_the_rest_height(self):
        self.assertEqual(C.flat_top_errors(self.m["zs"], self.m["rest_z"]), [], self.m["zs"])

    def test_the_bolster_stands_above_the_cushion_and_dips_at_the_front(self):
        rz = self.m["rest_z"]
        self.assertGreater(self.m["back_top"], rz + 0.08)
        self.assertGreater(self.m["side_top"], rz + 0.08)
        # the front is the low step-in edge: lower than the back, still above the cushion
        self.assertLess(self.m["front_top"], self.m["back_top"] - 0.04)
        self.assertGreater(self.m["front_top"], rz)

    def test_materials_carry_the_set_prefix(self):
        self.assertTrue(self.m["materials"])
        for name in self.m["materials"]:
            self.assertTrue(name.startswith(C.MATERIAL_PREFIX), name)


if __name__ == "__main__":
    unittest.main(verbosity=2)
