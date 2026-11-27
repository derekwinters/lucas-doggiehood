"""The shared dog kit is general: it builds and checks a breed that is not the Golden Retriever.

  python3 art-source/blender/dogs/kit/tests/test_breed_generality.py        (about 5 min)
  python3 -m unittest discover -s art-source/blender/dogs/kit/tests

Plain Python (unittest); Blender (bpy==5.3.0) only for the build test, which runs the kit's own
scripts in subprocesses. Nothing is written into the repository: the throwaway breed and all its
outputs live in a temporary folder that is deleted afterwards (set DH_KEEP_TEST_BREED=1 to keep
it and print where it is).

The throwaway breed is the Golden's definition with deliberately different proportions and
colours, made as data only (no per-breed code):
  - legs 0.8x as long above the paws (the body, head and tail lower to match),
  - ears 1.3x as big about their root,
  - two coats (black, chocolate) instead of three,
  - its own name, so every file name differs,
  - walk and run ground speeds 0.8x (same cycle length, so a stride in proportion to the legs; with
    the Golden's stride the shorter front legs cannot reach and the IK-reach check fails).
Its sit and its lie are re-solved with the kit's solve_sit.py and solve_lie.py (both depend on the
proportions), then the kit's model and motion exports must run every check and report ALL OK:
triangle budgets, the sit-contact check, the per-frame motion contact and foot-slide check, the
lying-contact check, and the glTF read-back.
"""
import ast
import importlib.util
import json
import os
import shutil
import subprocess
import sys
import tempfile
import unittest

TESTS = os.path.dirname(os.path.abspath(__file__))
KIT = os.path.dirname(TESTS)
GOLDEN = os.path.join(os.path.dirname(KIT), "golden-retriever")
sys.path.insert(0, KIT)

import dh_breed  # noqa: E402

LEG_SCALE = 0.8         # leg length above the paws
PAW_TOP = 0.10          # z below which the legs are paws (kept as they are)
LEG_TOP = 0.80          # hip / shoulder joint height of the Golden
EAR_SCALE = 1.3
DZ = (LEG_TOP - PAW_TOP) * (LEG_SCALE - 1.0)       # the body moves down by this much (negative)


def golden():
    return dh_breed.load(GOLDEN)


def leg_z(z):
    return z if z <= PAW_TOP else PAW_TOP + (z - PAW_TOP) * LEG_SCALE


def throwaway_breed():
    """{name: value} for the throwaway breed, from the Golden's data."""
    g = golden()
    d = {k: v for k, v in vars(g).items() if not k.startswith("__")}
    d.update(NAME="dog_testbreed", DISPLAY_NAME="Test Breed", STATS_NAME="Test Breed (kit generality test)",
             MOTION_STATUS="Test Breed motion clips (kit generality test)")
    # legs: shorter above the paws
    for k in ("FRONT_UPPER", "FRONT_FORE", "FRONT_PASTERN", "BACK_THIGH", "BACK_GASKIN", "BACK_META"):
        d[k] = [(y, leg_z(z)) + tuple(r) for y, z, *r in d[k]]
    d["JOINTS"] = {k: (y, leg_z(z)) for k, (y, z) in g.JOINTS.items()}
    # the body, head, tail and everything on them sit lower by DZ
    d["TORSO"] = [(y, z + DZ) + tuple(r) for y, z, *r in g.TORSO]
    d["HEAD"] = [(y, z + DZ) + tuple(r) for y, z, *r in g.HEAD]
    d["TAIL"] = [(y, z + DZ) + tuple(r) for y, z, *r in g.TAIL]
    d["SPINE"] = {k: ((h[0], h[1] + DZ), (t[0], t[1] + DZ)) for k, (h, t) in g.SPINE.items()}
    d["TAIL_BONES"] = [(y, z + DZ) for y, z in g.TAIL_BONES]
    d["TORSO_CHAIN"] = [(y, z + DZ) for y, z in g.TORSO_CHAIN]
    d["HINGE"] = (g.HINGE[0], g.HINGE[1] + DZ)
    d["JAW_TIP"] = (g.JAW_TIP[0], g.JAW_TIP[1] + DZ)
    d["TONGUE"] = dict(g.TONGUE, pivot=(g.TONGUE["pivot"][0], g.TONGUE["pivot"][1] + DZ))
    d["EYES"] = dict(g.EYES, ray_z=g.EYES["ray_z"] + DZ)
    d["NOSE"] = dict(g.NOSE, ray_z=g.NOSE["ray_z"] + DZ)
    d["SANITY"] = dict(g.SANITY, head_min_z=g.SANITY["head_min_z"] + DZ)
    # ears: lowered with the head, then scaled about their root
    rx, ry, rz = g.EARS["bones"][0]
    rz += DZ

    def ear(x, y, z):
        return (rx + (x - rx) * EAR_SCALE, ry + (y - ry) * EAR_SCALE, rz + (z + DZ - rz) * EAR_SCALE)
    d["EARS"] = dict(g.EARS, rings=[ear(x, y, z) + (w * EAR_SCALE, t) for x, y, z, w, t in g.EARS["rings"]],
                     bones=[ear(*b) for b in g.EARS["bones"]])
    d["FUR_MARKINGS"] = dict(g.FUR_MARKINGS, pale_z=tuple(leg_z(z) for z in g.FUR_MARKINGS["pale_z"]),
                             ear_tip_z=tuple(rz + (z + DZ - rz) * EAR_SCALE for z in g.FUR_MARKINGS["ear_tip_z"]))
    # coats: two, and not gold
    d["COATS"] = {"black": ("#2A2522", "#3B342F", "#1D1916"), "chocolate": ("#5C3A24", "#7A5236", "#45291A")}
    d["COAT_LABELS"] = {"black": "Black", "chocolate": "Chocolate"}
    # gaits: shorter legs take shorter strides (same cycle, slower ground speed)
    d["GAITS"] = {k: dict(v, speed=v["speed"] * LEG_SCALE) for k, v in g.GAITS.items()}
    return d


def write_breed(folder, d):
    """Write d as a breed.py of plain literals (repr); returns the path."""
    os.makedirs(folder, exist_ok=True)
    path = os.path.join(folder, "breed.py")
    with open(path, "w") as fh:
        fh.write('"""Throwaway breed for the kit generality test (written by test_breed_generality.py)."""\n')
        for k, v in d.items():
            fh.write("%s = %r\n" % (k, v))
    return path


def run(args, timeout=1200):
    r = subprocess.run([sys.executable] + args, capture_output=True, text=True, timeout=timeout)
    return r.returncode, r.stdout + r.stderr


def has_bpy():
    return importlib.util.find_spec("bpy") is not None


class BreedIsDataOnly(unittest.TestCase):
    """Invariant: a breed definition holds data only (dh_breed refuses anything else)."""

    def test_golden_breed_is_plain_data(self):
        self.assertEqual(dh_breed.problems(golden()), [])

    def test_breed_with_code_is_refused(self):
        with tempfile.TemporaryDirectory() as tmp:
            d = {k: v for k, v in vars(golden()).items() if not k.startswith("__")}
            write_breed(tmp, d)
            with open(os.path.join(tmp, "breed.py"), "a") as fh:
                fh.write("import math\n\ndef stride(speed):\n    return speed * math.pi\n")
            with self.assertRaises(SystemExit) as cm:
                dh_breed.load(tmp)
            self.assertIn("math is module", str(cm.exception))
            self.assertIn("stride is function", str(cm.exception))

    def test_breed_missing_a_field_is_refused(self):
        with tempfile.TemporaryDirectory() as tmp:
            d = {k: v for k, v in vars(golden()).items() if not k.startswith("__") and k != "GAITS"}
            write_breed(tmp, d)
            with self.assertRaises(SystemExit) as cm:
                dh_breed.load(tmp)
            self.assertIn("missing GAITS", str(cm.exception))

    def test_throwaway_breed_differs_and_is_plain_data(self):
        d = throwaway_breed()
        g = golden()
        self.assertNotEqual(d["FRONT_FORE"], g.FRONT_FORE)
        self.assertNotEqual(d["EARS"], g.EARS)
        self.assertNotEqual(d["COATS"], g.COATS)
        with tempfile.TemporaryDirectory() as tmp:
            write_breed(tmp, d)
            self.assertEqual(dh_breed.problems(dh_breed.load(tmp)), [])


@unittest.skipUnless(has_bpy(), "needs bpy==5.3.0")
class KitBuildsAnotherBreed(unittest.TestCase):
    """The kit builds the throwaway breed and every check runs and passes, with no per-breed code."""

    def test_throwaway_breed_builds_and_passes_every_check(self):
        tmp = tempfile.mkdtemp(prefix="dh_test_breed_")
        keep = os.environ.get("DH_KEEP_TEST_BREED") == "1"
        try:
            folder = os.path.join(tmp, "test-breed")
            d = throwaway_breed()
            write_breed(folder, d)
            # 1. re-solve the sit for these proportions (the kit's solver, data in, data out)
            code, out = run([os.path.join(KIT, "build_breed.py"), folder, "solve-sit"])
            self.assertEqual(code, 0, out[-3000:])
            line = [ln for ln in out.splitlines() if ln.startswith("SIT = ")][-1]
            d["SIT"] = ast.literal_eval(line[len("SIT = "):])
            write_breed(folder, d)
            # ... and the lie
            code, out = run([os.path.join(KIT, "build_breed.py"), folder, "solve-lie"])
            self.assertEqual(code, 0, out[-3000:])
            line = [ln for ln in out.splitlines() if ln.startswith("LIE = ")][-1]
            d["LIE"] = ast.literal_eval(line[len("LIE = "):])
            write_breed(folder, d)
            # 2. model + motion: every check must run and pass
            code, out = run([os.path.join(KIT, "build_breed.py"), folder, "model", "motion"])
            self.assertEqual(code, 0, out[-6000:])
            self.assertEqual(out.count("ALL OK"), 2, out[-6000:])
            # the checks ran on this breed's files, under its own names
            exp = os.path.join(folder, "exports")
            stats = json.load(open(os.path.join(exp, "dog_stats.json")))
            self.assertTrue(stats["all_checks_ok"])
            self.assertEqual(sorted(stats["files"]), sorted(
                ["dog_testbreed_black.gltf", "dog_testbreed_chocolate.gltf", "dog_testbreed_lod1.gltf",
                 "dog-testbreed-viewer.glb"]))
            for f, s in stats["files"].items():
                self.assertEqual(s["errors"], [], f)
            self.assertLessEqual(stats["full"]["total_tris"], d["BUDGET"]["full"])
            self.assertLessEqual(stats["lod1"]["total_tris"], d["BUDGET"]["lod1"])
            self.assertEqual(set(stats["sit_contact_m"]), {"full", "lod1"})
            for k in ("dog_fur_black_color.png", "dog_fur_chocolate_color.png", "dog_fur_lod1_black_color.png"):
                self.assertIn(k, stats["textures"])
            # shorter legs really are shorter: the top of the back is lower than the Golden's
            gold = json.load(open(os.path.join(GOLDEN, "exports", "dog_stats.json")))
            self.assertLess(stats["measurements_m"]["back_height"], gold["measurements_m"]["back_height"] - 0.1)
            clips = json.load(open(os.path.join(folder, "motion", "exports", "dog_testbreed_anim_clips.json")))
            self.assertTrue(clips["all_checks_ok"])
            self.assertEqual(set(clips["clips"]), {"Idle", "Walk", "Run", "SitDown", "SitIdle", "TailWag", "PlayBow",
                                                  "Swim", "Shake", "LieDown", "LieIdle"})
            for name, c in clips["clips"].items():
                self.assertTrue(c["check"]["ok"], name)
            report = open(os.path.join(folder, "motion", "review", "motion-contact.txt")).read()
            self.assertIn("Lying contact (the LIE pose", report)
            self.assertIn("glTF read back (dog_testbreed_anim.gltf): OK", report)
            print("\nthrowaway breed: back height %.3f m (Golden %.3f), %d tris (LOD %d), SIT %s"
                  % (stats["measurements_m"]["back_height"], gold["measurements_m"]["back_height"],
                     stats["full"]["total_tris"], stats["lod1"]["total_tris"], d["SIT"]))
            print("sit contact (full):", stats["sit_contact_m"]["full"])
            print("LIE %s, lying contact: %s" % (d["LIE"], clips["lie_contact_m"]))
            print("worst plant slide per clip:", {k: c["check"].get("worst_plant_slide_m") for k, c in
                                                   clips["clips"].items()})
        finally:
            if keep:
                print("kept the throwaway breed in", tmp)
            else:
                shutil.rmtree(tmp, ignore_errors=True)


if __name__ == "__main__":
    unittest.main(verbosity=2)
