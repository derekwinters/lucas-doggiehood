"""Checks for the chew bone (#923): shape, size beside the adult dog, triangle budget, export
read-back and byte-for-byte rebuilds.

  python3 art-source/blender/items/chew-bone/tests/test_chew_bone.py        (under a minute)
  python3 -m unittest discover -s art-source/blender/items/chew-bone/tests

Needs bpy (==5.3.0). Every build runs in its own fresh process. Nothing is written into the
repository: the exports go to temporary folders and are compared with the committed files.

Invariant - the chew bone is sized against the in-game adult dog, not against a real dog: its
length is a fixed share of the Golden's in-game nose-to-rump length.
Invariant - every committed chew-bone file is rebuilt byte for byte by export_chew_bone.py.
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

TESTS = os.path.dirname(os.path.abspath(__file__))
HERE = os.path.dirname(TESTS)                       # items/chew-bone
ART = os.path.normpath(os.path.join(HERE, "..", ".."))   # art-source/blender
DOG_STATS = os.path.join(ART, "dogs", "golden-retriever", "exports", "dog_stats.json")

BUDGET = 6000                  # provisional per-mesh backstop (#799)
ITEM_CAP = 400                 # this item's own cap: small, never seen up close (Derek on #923)
# the bone's length as a share of the adult Golden's in-game nose-to-rump length
LENGTH_SHARE = (0.15, 0.25)
KNOB_TO_SHAFT_MIN = 1.8        # width across a knobbed end over the shaft's width at its middle
GROUND_TOL = 0.0005            # m: the lowest vertex sits on the ground
CENTRE_TOL = 0.001             # m: the pivot is the footprint centre
COMMITTED = ["exports/chew_bone.gltf", "exports/chew_bone.bin", "exports/chew_bone_stats.json",
             "textures/chew_bone_color.png", "textures/chew_bone_normal.png"]
REBUILDS = 2

PROBE = r'''
import json, sys
sys.path.insert(0, %(here)r)
import bpy, bmesh
import dh_chew_bone as CB
import dh_house_kit as K
K.reset_scene()
ob = CB.build()
bpy.context.view_layer.update()
co = [ob.matrix_world @ v.co for v in ob.data.vertices]
xs, ys, zs = [c.x for c in co], [c.y for c in co], [c.z for c in co]
L = max(xs) - min(xs)
mid = max(abs(c.y) for c in co if abs(c.x) < 0.05 * L)
end = max(abs(c.y) for c in co if abs(c.x) > 0.40 * L)
bm = bmesh.new()
bm.from_mesh(ob.data)
print("PROBE " + json.dumps({
    "objects": [o.name for o in bpy.data.objects if o.type == "MESH"],
    "name": ob.name, "tris": K.tri_count(ob),
    "size": [L, max(ys) - min(ys), max(zs) - min(zs)],
    "centre": [(max(xs) + min(xs)) / 2, (max(ys) + min(ys)) / 2], "min_z": min(zs),
    "mid_width": 2 * mid, "end_width": 2 * end,
    "non_manifold": sum(1 for e in bm.edges if not e.is_manifold),
    "materials": [m.name for m in ob.data.materials],
    "uv_layers": len(ob.data.uv_layers),
    "smooth": all(p.use_smooth for p in ob.data.polygons),
    "length_dim": CB.DIMS["length"],
}))
'''


def has_bpy():
    return importlib.util.find_spec("bpy") is not None


def sha(path):
    with open(path, "rb") as fh:
        return hashlib.sha256(fh.read()).hexdigest()


def run(args, timeout=900):
    r = subprocess.run([sys.executable] + args, capture_output=True, text=True, timeout=timeout)
    return r.returncode, r.stdout + r.stderr


def probe():
    code, out = run(["-c", PROBE % {"here": HERE}])
    lines = [ln for ln in out.splitlines() if ln.startswith("PROBE ")]
    if code != 0 or not lines:
        raise AssertionError("probe failed:\n" + out[-4000:])
    return json.loads(lines[-1][6:])


@unittest.skipUnless(has_bpy(), "needs bpy==5.3.0")
class ChewBoneShape(unittest.TestCase):

    @classmethod
    def setUpClass(cls):
        cls.p = probe()

    def test_one_mesh_named_chew_bone(self):
        self.assertEqual(self.p["objects"], ["chew_bone"])

    def test_within_the_triangle_budget(self):
        self.assertLessEqual(self.p["tris"], BUDGET)

    def test_within_the_item_triangle_cap(self):
        self.assertLessEqual(self.p["tris"], ITEM_CAP)

    def test_sized_against_the_adult_dog(self):
        with open(DOG_STATS) as fh:
            dog = json.load(fh)["measurements_m"]["nose_to_rump"]
        share = self.p["size"][0] / dog
        self.assertGreaterEqual(share, LENGTH_SHARE[0], "bone too short beside the dog: %.3f" % share)
        self.assertLessEqual(share, LENGTH_SHARE[1], "bone too long beside the dog: %.3f" % share)
        self.assertAlmostEqual(self.p["size"][0], self.p["length_dim"], delta=0.002)

    def test_two_knobbed_ends_wider_than_the_shaft(self):
        self.assertGreaterEqual(self.p["end_width"] / self.p["mid_width"], KNOB_TO_SHAFT_MIN)

    def test_lies_flat_wider_than_tall(self):
        self.assertGreater(self.p["size"][1], self.p["size"][2])

    def test_sits_on_the_ground_at_its_footprint_centre(self):
        self.assertAlmostEqual(self.p["min_z"], 0.0, delta=GROUND_TOL)
        self.assertAlmostEqual(self.p["centre"][0], 0.0, delta=CENTRE_TOL)
        self.assertAlmostEqual(self.p["centre"][1], 0.0, delta=CENTRE_TOL)

    def test_closed_smooth_surface_with_uvs(self):
        self.assertEqual(self.p["non_manifold"], 0)
        self.assertTrue(self.p["smooth"])
        self.assertEqual(self.p["uv_layers"], 1)

    def test_one_item_material(self):
        self.assertEqual(self.p["materials"], ["DH_Item_ChewBone"])


@unittest.skipUnless(has_bpy(), "needs bpy==5.3.0")
class ChewBoneExport(unittest.TestCase):

    def export_into(self, root):
        code, out = run([os.path.join(HERE, "export_chew_bone.py"), root])
        self.assertEqual(code, 0, out[-4000:])
        self.assertIn("ALL OK", out, out[-4000:])

    def test_gltf_reads_back_with_relative_textures(self):
        tmp = tempfile.mkdtemp(prefix="dh_bone_")
        try:
            self.export_into(tmp)
            path = os.path.join(tmp, "exports", "chew_bone.gltf")
            with open(path) as fh:
                j = json.load(fh)
            self.assertEqual([n["name"] for n in j["nodes"]], ["chew_bone"])
            self.assertEqual(len(j["meshes"]), 1)
            self.assertEqual([m["name"] for m in j["materials"]], ["DH_Item_ChewBone"])
            m = j["materials"][0]
            self.assertIn("baseColorTexture", m["pbrMetallicRoughness"])
            self.assertIn("normalTexture", m)
            self.assertFalse(m.get("doubleSided", False))
            tex = os.path.realpath(os.path.join(tmp, "textures"))
            uris = sorted(im["uri"] for im in j["images"])
            self.assertEqual(uris, ["../textures/chew_bone_color.png", "../textures/chew_bone_normal.png"])
            for u in uris:
                f = os.path.realpath(os.path.join(os.path.dirname(path), u))
                self.assertEqual(os.path.dirname(f), tex)
                self.assertTrue(os.path.isfile(f), f)
        finally:
            shutil.rmtree(tmp, ignore_errors=True)

    def test_every_rebuild_matches_the_committed_files(self):
        want = {f: sha(os.path.join(HERE, f)) for f in COMMITTED}
        for i in range(REBUILDS):
            tmp = tempfile.mkdtemp(prefix="dh_bone_")
            try:
                self.export_into(tmp)
                got = {f: sha(os.path.join(tmp, f)) for f in COMMITTED}
                differ = sorted(f for f in COMMITTED if got[f] != want[f])
                self.assertEqual(differ, [], "rebuild %d of %d differs from the committed files"
                                 % (i + 1, REBUILDS))
            finally:
                shutil.rmtree(tmp, ignore_errors=True)


if __name__ == "__main__":
    unittest.main(verbosity=2)
