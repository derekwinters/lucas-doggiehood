"""Checks on the blanket export (#413).

  python3 -m unittest discover -s art-source/blender/items/blanket/tests -v

Two groups:

  - BlanketExportFits (plain Python, no Blender): reads the committed exports/blanket.glb
    and checks that the blanket lies flat on the ground inside today's graybox slab footprint
    (DecorationView: 1.4 x 1.0 m), pivots on the centre of that footprint, stays within the
    provisional per-mesh triangle backstop (#799) and is self-contained: one .glb with its
    textures embedded, like the houses and the package. The numbers are read from the glTF accessors, so this checks
    exactly what the game will import (#932), not the Blender scene.
  - BlanketRebuildsByteIdentical (needs bpy==5.3.0): runs export_blanket.py twice into
    temporary folders, each in a fresh process, and requires every output to match the
    committed files byte for byte. Nothing is written into the repository.

Invariant - the blanket never leaves the slab footprint: every vertex of the export lies
inside the 1.4 x 1.0 m rectangle centred on the pivot, on or above the ground.
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
SET = os.path.dirname(TESTS)
GLB = os.path.join(SET, "exports", "blanket.glb")
STATS = os.path.join(SET, "exports", "blanket_stats.json")

SLAB_W = 1.4            # DecorationView's graybox slab, across (Blender X, glTF X)
SLAB_D = 1.0            # ... and deep (Blender Y, glTF -Z)
GROUND_TOL = 0.001      # the lowest vertex sits on the ground within 1 mm
CENTRE_TOL = 0.02       # the pivot is within 2 cm of the footprint's centre
FLAT_MAX_H = 0.10       # "lies flat": nothing stands taller than 10 cm off the grass
TRI_BACKSTOP = 6000     # provisional per-mesh backstop (#799)

OUTPUTS = ["exports/blanket.glb", "exports/blanket_stats.json",
           "textures/blanket_plaid_color.png", "textures/blanket_weave_normal.png",
           "textures/blanket_back_color.png"]


def load():
    """(json, binary chunk) of the .glb."""
    with open(GLB, "rb") as fh:
        data = fh.read()
    assert data[:4] == b"glTF", "not a binary glTF"
    ln = struct.unpack_from("<I", data, 12)[0]
    j = json.loads(data[20:20 + ln].decode())
    off = 20 + ln
    bl = struct.unpack_from("<I", data, off)[0]
    return j, data[off + 8:off + 8 + bl]


def positions(j, binb, acc):
    a = j["accessors"][acc]
    bv = j["bufferViews"][a["bufferView"]]
    off = bv.get("byteOffset", 0) + a.get("byteOffset", 0)
    return [struct.unpack_from("<3f", binb, off + 12 * i) for i in range(a["count"])]


def all_positions():
    j, binb = load()
    pts = []
    for mesh in j["meshes"]:
        for p in mesh["primitives"]:
            pts += positions(j, binb, p["attributes"]["POSITION"])
    return pts


def triangles(j):
    n = 0
    for mesh in j["meshes"]:
        for p in mesh["primitives"]:
            assert p.get("mode", 4) == 4
            n += j["accessors"][p["indices"]]["count"] // 3
    return n


class BlanketExportFits(unittest.TestCase):

    def test_every_vertex_is_inside_the_slab_footprint(self):
        pts = all_positions()
        xs = [p[0] for p in pts]
        zs = [p[2] for p in pts]       # glTF Z = -Blender Y
        self.assertLessEqual(max(xs) - min(xs), SLAB_W)
        self.assertLessEqual(max(zs) - min(zs), SLAB_D)
        self.assertLessEqual(max(abs(x) for x in xs), SLAB_W / 2)
        self.assertLessEqual(max(abs(z) for z in zs), SLAB_D / 2)

    def test_pivot_is_the_footprint_centre(self):
        pts = all_positions()
        for axis in (0, 2):
            c = (max(p[axis] for p in pts) + min(p[axis] for p in pts)) / 2
            self.assertLess(abs(c), CENTRE_TOL, "axis %d centre %.3f" % (axis, c))

    def test_it_lies_flat_on_the_ground(self):
        ys = [p[1] for p in all_positions()]   # glTF Y = up
        self.assertGreaterEqual(min(ys), -GROUND_TOL)
        self.assertLessEqual(min(ys), GROUND_TOL)
        self.assertLessEqual(max(ys), FLAT_MAX_H)

    def test_within_the_triangle_backstop(self):
        j, _ = load()
        self.assertLessEqual(triangles(j), TRI_BACKSTOP)

    def test_stats_record_the_export(self):
        j, _ = load()
        with open(STATS) as fh:
            s = json.load(fh)
        self.assertEqual(s["tris"], triangles(j))
        pts = all_positions()
        size = [max(p[i] for p in pts) - min(p[i] for p in pts) for i in range(3)]
        # stats are in Blender axes: x across, y deep, z up
        for got, want in zip(s["size_xyz_m"], (size[0], size[2], size[1])):
            self.assertAlmostEqual(got, want, places=3)

    def test_textures_are_embedded(self):
        j, _ = load()
        self.assertTrue(j.get("images"))
        for im in j["images"]:
            self.assertNotIn("uri", im)
            self.assertIn("bufferView", im)
        self.assertEqual(len(j["buffers"]), 1)
        self.assertNotIn("uri", j["buffers"][0])


def has_bpy():
    return importlib.util.find_spec("bpy") is not None


def sha(path):
    with open(path, "rb") as fh:
        return hashlib.sha256(fh.read()).hexdigest()


@unittest.skipUnless(has_bpy(), "needs bpy==5.3.0")
class BlanketRebuildsByteIdentical(unittest.TestCase):

    def test_two_fresh_rebuilds_match_the_committed_files(self):
        want = {f: sha(os.path.join(SET, f)) for f in OUTPUTS}
        for i in range(2):
            tmp = tempfile.mkdtemp(prefix="dh_blanket_")
            try:
                r = subprocess.run([sys.executable, os.path.join(SET, "export_blanket.py"), tmp],
                                   capture_output=True, text=True, timeout=600)
                self.assertEqual(r.returncode, 0, (r.stdout + r.stderr)[-4000:])
                self.assertIn("ALL OK", r.stdout)
                got = {f: sha(os.path.join(tmp, f)) for f in OUTPUTS}
                differ = sorted(f for f in OUTPUTS if got[f] != want[f])
                print("rebuild %d: %s" % (i + 1, "byte-identical" if not differ else "DIFFERS: " + ", ".join(differ)))
                self.assertEqual(differ, [])
            finally:
                shutil.rmtree(tmp, ignore_errors=True)


if __name__ == "__main__":
    unittest.main(verbosity=2)
