"""The bug's export: one looping WingFlap clip, inside the budget, nothing extra, no collider (#334).

  python3 art-source/blender/pests/bug/tests/test_bug_export.py           (about 30 s)
  python3 -m unittest discover -s art-source/blender/pests/bug/tests

Builds the bug with export_bug.py into a temporary folder (a fresh process, bpy==5.3.0), reads the
written .gltf + .bin back with plain Python and checks:
  - Invariant - the wing-flap clip loops on its first frame: one animation, "WingFlap", sampled every
    frame (frames + 1 samples), and every channel's last sample equals its first.
  - the wings flap (both, several beats a loop) and the body bobs a little; the root node is not
    animated, so the game alone moves the bug along its flight path.
  - Invariant - the bug has no collider: the house underneath stays the tap target. The file holds
    only the bug's own nodes (root, body, two wings) and meshes; no cameras, lights, extensions or
    images.
  - the triangle count is inside the art budget (#799) and matches the stats JSON.
  - Invariant - the committed export is reproducible: a second build, in another fresh process,
    gives files byte-identical to the first and to the committed exports/.
Nothing is written into the repository.
"""
import json
import math
import os
import shutil
import struct
import subprocess
import sys
import tempfile
import unittest

TESTS = os.path.dirname(os.path.abspath(__file__))
BUG = os.path.dirname(TESTS)
EXPORT = os.path.join(BUG, "export_bug.py")
STEM = "pest_bug"
OUTPUTS = ("exports/%s.gltf" % STEM, "exports/%s.bin" % STEM, "exports/bug_stats.json")

CLIP = "WingFlap"
FPS = 30
ROOT_NODE = "pest_bug"
NODES = {"pest_bug", "pest_bug_body", "pest_bug_wing_L", "pest_bug_wing_R"}
WINGS = ("pest_bug_wing_L", "pest_bug_wing_R")
BODY = "pest_bug_body"
MAX_TRIS = 6000              # the provisional per-mesh backstop (#799), applied to the whole bug
LOOP_TOL = 1e-6              # last sample vs first, per component
WING_MIN_SWING_DEG = 60.0    # each wing's peak-to-peak flap
WING_MIN_BEATS = 3           # flaps per loop
BOB_MIN_M, BOB_MAX_M = 0.01, 0.10   # the body's peak-to-peak bob


def build(root):
    r = subprocess.run([sys.executable, EXPORT, root], capture_output=True, text=True, timeout=900)
    assert r.returncode == 0, (r.stdout + r.stderr)[-4000:]
    return r.stdout


COMP = {5120: ("b", 1), 5121: ("B", 1), 5122: ("h", 2), 5123: ("H", 2), 5125: ("I", 4), 5126: ("f", 4)}
NCOMP = {"SCALAR": 1, "VEC2": 2, "VEC3": 3, "VEC4": 4, "MAT4": 16}


def accessor(j, binb, i):
    a = j["accessors"][i]
    v = j["bufferViews"][a["bufferView"]]
    fmt, size = COMP[a["componentType"]]
    n = NCOMP[a["type"]]
    stride = v.get("byteStride", size * n)
    off = v.get("byteOffset", 0) + a.get("byteOffset", 0)
    return [list(struct.unpack_from("<%d%s" % (n, fmt), binb, off + k * stride)) for k in range(a["count"])]


def quat_angle_deg(q0, q1):
    d = abs(sum(a * b for a, b in zip(q0, q1)))
    return math.degrees(2 * math.acos(min(1.0, d)))


class BugExport(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.tmp = tempfile.mkdtemp(prefix="dh_bug_")
        cls.a = os.path.join(cls.tmp, "a")
        build(cls.a)
        cls.j = json.load(open(os.path.join(cls.a, OUTPUTS[0])))
        cls.binb = open(os.path.join(cls.a, OUTPUTS[1]), "rb").read()
        cls.stats = json.load(open(os.path.join(cls.a, OUTPUTS[2])))
        cls.names = [n.get("name") for n in cls.j["nodes"]]

    @classmethod
    def tearDownClass(cls):
        shutil.rmtree(cls.tmp, ignore_errors=True)

    def clip(self):
        anims = self.j.get("animations", [])
        self.assertEqual([a["name"] for a in anims], [CLIP])
        return anims[0]

    def channels(self):
        a = self.clip()
        for ch in a["channels"]:
            s = a["samplers"][ch["sampler"]]
            yield (self.names[ch["target"]["node"]], ch["target"]["path"],
                   accessor(self.j, self.binb, s["input"]), accessor(self.j, self.binb, s["output"]))

    def test_one_clip_sampled_every_frame(self):
        frames = self.clip()["extras"]["frames"]
        self.assertTrue(self.clip()["extras"]["loop"])
        for node, path, t, out in self.channels():
            self.assertEqual(len(t), frames + 1, "%s %s" % (node, path))
            self.assertAlmostEqual(t[0][0], 0.0, places=5)
            self.assertAlmostEqual(t[-1][0], frames / FPS, places=4)

    def test_clip_loops_on_first_frame(self):
        for node, path, t, out in self.channels():
            for a, b in zip(out[0], out[-1]):
                self.assertLessEqual(abs(a - b), LOOP_TOL, "%s %s: first %s last %s" % (node, path, out[0], out[-1]))

    def test_wings_flap(self):
        rot = {node: out for node, path, t, out in self.channels() if path == "rotation"}
        for w in WINGS:
            self.assertIn(w, rot)
            qs = rot[w]
            swing = max(quat_angle_deg(a, b) for a in qs for b in qs)
            self.assertGreaterEqual(swing, WING_MIN_SWING_DEG, w)
            ang = [quat_angle_deg(qs[0], q) for q in qs]
            beats = sum(1 for k in range(1, len(ang) - 1) if ang[k] > ang[k - 1] and ang[k] >= ang[k + 1])
            self.assertGreaterEqual(beats, WING_MIN_BEATS, w)

    def test_body_bobs(self):
        tr = {node: out for node, path, t, out in self.channels() if path == "translation"}
        self.assertIn(BODY, tr)
        ys = [p[1] for p in tr[BODY]]                    # glTF up is +Y
        self.assertGreaterEqual(max(ys) - min(ys), BOB_MIN_M)
        self.assertLessEqual(max(ys) - min(ys), BOB_MAX_M)

    def test_root_is_not_animated(self):
        self.assertNotIn(ROOT_NODE, [node for node, path, t, out in self.channels()])

    def test_no_collider_and_nothing_extra(self):
        self.assertEqual(sorted(self.names), sorted(NODES))
        self.assertEqual([self.names[i] for i in self.j["scenes"][0]["nodes"]], [ROOT_NODE])
        self.assertEqual(len(self.j["meshes"]), 3)
        for key in ("cameras", "skins", "images", "textures", "extensionsUsed", "extensionsRequired"):
            self.assertNotIn(key, self.j, key)
        for n in self.j["nodes"]:
            self.assertNotIn("extensions", n)
            self.assertNotIn("collider", n.get("name", "").lower())

    def test_triangles_within_budget(self):
        tris = 0
        for m in self.j["meshes"]:
            for p in m["primitives"]:
                self.assertEqual(p.get("mode", 4), 4)
                tris += self.j["accessors"][p["indices"]]["count"] // 3
        self.assertLessEqual(tris, MAX_TRIS)
        self.assertEqual(tris, self.stats["tris"]["total"])

    def test_rebuild_is_byte_identical_and_matches_committed(self):
        b = os.path.join(self.tmp, "b")
        build(b)
        for rel in OUTPUTS:
            first = open(os.path.join(self.a, rel), "rb").read()
            self.assertEqual(first, open(os.path.join(b, rel), "rb").read(), "rebuild differs: " + rel)
            committed = os.path.join(BUG, rel)
            self.assertTrue(os.path.exists(committed), "not committed: " + rel)
            self.assertEqual(first, open(committed, "rb").read(), "committed differs: " + rel)


if __name__ == "__main__":
    unittest.main(verbosity=2)
