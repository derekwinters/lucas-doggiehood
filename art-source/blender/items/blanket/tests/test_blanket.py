"""Checks on the blanket exports (#413).

  python3 -m unittest discover -s art-source/blender/items/blanket/tests -v

Three groups:

  - BlanketExportFits (plain Python, no Blender): reads every committed exports/blanket_<colourway>.glb
    and checks that the blanket lies flat on the ground inside its own placement footprint
    (blanket_spec.FOOTPRINT_W x FOOTPRINT_D, the cloth plus its fringe), pivots on the centre of
    that footprint, is big enough for an adult Golden Retriever lying flat (the approved Golden's
    measured nose-to-rump length and width, from its dog_stats.json), stays within the provisional
    per-mesh triangle backstop (#799) and is self-contained: one .glb with its textures embedded,
    like the houses and the package. The numbers are read from the glTF accessors, so this checks
    exactly what the game will import (#932), not the Blender scene.
  - BlanketColourways (plain Python): there are four colourways, each has its own .glb, colour
    texture and stats entry, every .glb carries the same mesh (only the plaid differs), and the
    plaid textures' dominant colours (their average, which is what the game camera sees) are
    clearly apart.
  - BlanketRebuildsByteIdentical (needs bpy==5.3.0): runs export_blanket.py twice into
    temporary folders, each in a fresh process, and requires every output to match the
    committed files byte for byte. Nothing is written into the repository.

Invariant - the blanket never leaves its placement footprint: every vertex of every colourway's
export lies inside the FOOTPRINT_W x FOOTPRINT_D rectangle centred on the pivot, on or above the
ground.
"""
import hashlib
import importlib.util
import itertools
import json
import os
import shutil
import struct
import subprocess
import sys
import tempfile
import unittest

from PIL import Image

TESTS = os.path.dirname(os.path.abspath(__file__))
SET = os.path.dirname(TESTS)
sys.path.insert(0, SET)
import blanket_spec as S  # noqa: E402  (plain Python: the size and the colourway list)

STATS = os.path.join(SET, "exports", "blanket_stats.json")
GOLDEN_STATS = os.path.normpath(os.path.join(SET, "..", "..", "dogs", "golden-retriever", "exports",
                                             "dog_stats.json"))

GROUND_TOL = 0.001      # the lowest vertex sits on the ground within 1 mm
CENTRE_TOL = 0.02       # the pivot is within 2 cm of the footprint's centre
FLAT_MAX_H = 0.10       # "lies flat": nothing stands taller than 10 cm off the grass
TRI_BACKSTOP = 6000     # provisional per-mesh backstop (#799)
COLOURWAY_COUNT = 4     # Derek: several colour options, one picked at random when gifted
DOMINANT_MIN_DIST = 60  # RGB distance (0-255 per channel) between any two dominant colours


def glb_path(key):
    return os.path.join(SET, "exports", "blanket_%s.glb" % key)


def plaid_path(key):
    return os.path.join(SET, "textures", "blanket_plaid_%s_color.png" % key)


OUTPUTS = (["exports/blanket_%s.glb" % k for k, _ in S.COLOURWAYS]
           + ["textures/blanket_plaid_%s_color.png" % k for k, _ in S.COLOURWAYS]
           + ["exports/blanket_stats.json", "textures/blanket_weave_normal.png",
              "textures/blanket_back_color.png"])


def load(path):
    """(json, binary chunk) of a .glb."""
    with open(path, "rb") as fh:
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


def all_positions(path):
    j, binb = load(path)
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


def size(pts):
    """Blender axes: x across, y deep, z up (glTF is x, z = -y, y = up)."""
    sx = max(p[0] for p in pts) - min(p[0] for p in pts)
    sz = max(p[1] for p in pts) - min(p[1] for p in pts)
    sy = max(p[2] for p in pts) - min(p[2] for p in pts)
    return sx, sy, sz


class BlanketExportFits(unittest.TestCase):

    def each(self):
        """(colourway, .glb path) for every colourway."""
        return [(key, glb_path(key)) for key, _ in S.COLOURWAYS]

    def test_every_vertex_is_inside_the_footprint(self):
        for key, path in self.each():
            pts = all_positions(path)
            xs = [p[0] for p in pts]
            zs = [p[2] for p in pts]       # glTF Z = -Blender Y
            self.assertLessEqual(max(abs(x) for x in xs), S.FOOTPRINT_W / 2 + 1e-4)
            self.assertLessEqual(max(abs(z) for z in zs), S.FOOTPRINT_D / 2 + 1e-4)

    def test_it_fills_the_footprint(self):
        """The footprint #932 places by is the blanket's real size, not a loose box."""
        for key, path in self.each():
            sx, sy, _ = size(all_positions(path))
            self.assertAlmostEqual(sx, S.FOOTPRINT_W, delta=0.02)
            self.assertAlmostEqual(sy, S.FOOTPRINT_D, delta=0.02)

    def test_an_adult_golden_lying_flat_fits_on_the_cloth(self):
        with open(GOLDEN_STATS) as fh:
            g = json.load(fh)["measurements_m"]
        self.assertGreaterEqual(S.CLOTH_W, g["nose_to_rump"])
        self.assertGreaterEqual(S.CLOTH_D, g["width_incl_ears"])
        for key, path in self.each():
            sx, sy, _ = size(all_positions(path))
            self.assertGreaterEqual(sx, g["nose_to_rump"])
            self.assertGreaterEqual(sy, g["width_incl_ears"])

    def test_pivot_is_the_footprint_centre(self):
        for key, path in self.each():
            pts = all_positions(path)
            for axis in (0, 2):
                c = (max(p[axis] for p in pts) + min(p[axis] for p in pts)) / 2
                self.assertLess(abs(c), CENTRE_TOL, "axis %d centre %.3f" % (axis, c))

    def test_it_lies_flat_on_the_ground(self):
        for key, path in self.each():
            ys = [p[1] for p in all_positions(path)]   # glTF Y = up
            self.assertGreaterEqual(min(ys), -GROUND_TOL)
            self.assertLessEqual(min(ys), GROUND_TOL)
            self.assertLessEqual(max(ys), FLAT_MAX_H)

    def test_within_the_triangle_backstop(self):
        for key, path in self.each():
            self.assertLessEqual(triangles(load(path)[0]), TRI_BACKSTOP)

    def test_stats_record_each_export(self):
        with open(STATS) as fh:
            s = json.load(fh)
        for key, path in self.each():
            j, _ = load(path)
            rec = s["colourways"][key]
            self.assertEqual(rec["tris"], triangles(j))
            for got, want in zip(rec["size_xyz_m"], size(all_positions(path))):
                self.assertAlmostEqual(got, want, places=3)

    def test_textures_are_embedded(self):
        for key, path in self.each():
            j, _ = load(path)
            self.assertTrue(j.get("images"))
            for im in j["images"]:
                self.assertNotIn("uri", im)
                self.assertIn("bufferView", im)
            self.assertEqual(len(j["buffers"]), 1)
            self.assertNotIn("uri", j["buffers"][0])


def dominant(path):
    """The colour a plaid reads as from the game camera: at that distance the texture is
    mipmapped down to (about) its average, so the average RGB is the dominant colour."""
    im = Image.open(path).convert("RGB")
    n = im.width * im.height
    return tuple(sum(c * k for k, c in enumerate(h)) / n
                 for h in (im.getchannel(i).histogram() for i in range(3)))


class BlanketColourways(unittest.TestCase):

    def test_there_are_four_colourways_with_navy_first(self):
        keys = [k for k, _ in S.COLOURWAYS]
        self.assertEqual(len(keys), COLOURWAY_COUNT)
        self.assertEqual(len(set(keys)), COLOURWAY_COUNT)
        self.assertEqual(keys[0], "navy")           # the approved first look stays the default

    def test_every_colourway_has_its_export_texture_and_stats(self):
        with open(STATS) as fh:
            s = json.load(fh)
        for key, _ in S.COLOURWAYS:
            with self.subTest(colourway=key):
                self.assertTrue(os.path.isfile(glb_path(key)))
                self.assertTrue(os.path.isfile(plaid_path(key)))
                self.assertIn(key, s["colourways"])

    def test_colourways_share_one_mesh(self):
        """Only the plaid changes, so #932 can swap the file or the material per instance."""
        ref = all_positions(glb_path(S.COLOURWAYS[0][0]))
        for key, _ in S.COLOURWAYS[1:]:
            with self.subTest(colourway=key):
                self.assertEqual(all_positions(glb_path(key)), ref)

    def test_each_plaid_material_is_named_for_its_colourway(self):
        for key, _ in S.COLOURWAYS:
            with self.subTest(colourway=key):
                names = {m["name"] for m in load(glb_path(key))[0]["materials"]}
                self.assertIn("DH_Item_blanket_plaid_%s" % key.capitalize(), names)
                self.assertIn("DH_Item_blanket_back", names)

    def test_dominant_colours_are_clearly_apart(self):
        doms = {k: dominant(plaid_path(k)) for k, _ in S.COLOURWAYS}
        for (a, ca), (b, cb) in itertools.combinations(doms.items(), 2):
            d = sum((x - y) ** 2 for x, y in zip(ca, cb)) ** 0.5
            self.assertGreaterEqual(d, DOMINANT_MIN_DIST, "%s %s vs %s %s: %.0f" % (
                a, [round(x) for x in ca], b, [round(x) for x in cb], d))


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
