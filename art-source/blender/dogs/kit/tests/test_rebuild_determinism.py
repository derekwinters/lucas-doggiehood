"""Building a breed twice from the same sources gives byte-identical outputs (#894).

  python3 art-source/blender/dogs/kit/tests/test_rebuild_determinism.py         (about 6 min)
  python3 -m unittest discover -s art-source/blender/dogs/kit/tests

Invariant - building a breed twice from the same sources gives byte-identical exports, textures
and reports, every run and in fresh processes.

Two tests, both needing bpy==5.3.0 and both running the kit in subprocesses (fresh processes):

  - QuadriFlowIsRepeatable (about 1 min): the body's QuadriFlow retopology, run QF_REPEATS times
    on the same mesh in one process, gives the same vertices every time. Before the fix about one
    run in five came out different (QuadriFlow inside Blender reads uninitialised heap memory, see
    kit/dh_dog_mesh.py), so this catches the problem quickly.
  - GoldenRebuildsByteIdentical (about 5 min): the Golden's breed.py, copied into a temporary
    folder, is built (model + motion) DH_REBUILDS times (default 2), each build in fresh
    processes, and every output must be byte-identical to the Golden's committed files. Set
    DH_REBUILDS=20 for the long proof. Nothing is written into the repository.
"""
import hashlib
import importlib.util
import os
import shutil
import subprocess
import sys
import tempfile
import unittest

TESTS = os.path.dirname(os.path.abspath(__file__))
KIT = os.path.dirname(TESTS)
GOLDEN = os.path.join(os.path.dirname(KIT), "golden-retriever")
QF_REPEATS = 12

# every output of model + motion that is committed for the Golden (relative to the breed folder)
OUTPUTS = (
    ["exports/dog_golden_%s.%s" % (c, e) for c in ("default", "light", "dark", "lod1") for e in ("gltf", "bin")]
    + ["exports/dog_golden_skeleton.json", "exports/dog_stats.json"]
    + ["textures/dog_fur_%s%s.png" % (lod, k) for lod in ("", "lod1_")
       for k in ("default_color", "light_color", "dark_color", "mask", "normal")]
    + ["textures/dog_trim_color.png"]
    + ["motion/exports/dog_golden_anim.gltf", "motion/exports/dog_golden_anim.bin",
       "motion/exports/dog_golden_anim_clips.json", "motion/review/motion-contact.txt"]
)

# Run in a fresh process: the body mesh up to the QuadriFlow input, then QuadriFlow QF_REPEATS
# times on copies of that same mesh, printing a hash of each result.
QF_SCRIPT = r'''
import hashlib, sys
sys.path.insert(0, %(kit)r)
import dh_breed
dh_breed.use(%(golden)r)
import bpy
import numpy as np
import dh_dog as D
import dh_dog_mesh as M
import dh_dog_skin as SK
v, q = M.surface_nets(SK.field(), SK.ORIGIN, SK.GRID_H)
v, q = M.largest_component(np.asarray(v), q)
src = M.mesh_from("_src", v, q, bpy.context.scene.collection)
M.taubin(src, 10)
M.voxel_clean(src, SK.VOXEL)
M.taubin(src, 4)
for i in range(%(n)d):
    ob = src.copy()
    ob.data = src.data.copy()
    bpy.context.scene.collection.objects.link(ob)
    M.quadriflow(ob, D.B.SKIN_FACES)
    co = np.empty(len(ob.data.vertices) * 3)
    ob.data.vertices.foreach_get("co", co)
    print("QF", hashlib.sha1(co.tobytes()).hexdigest(), flush=True)
    me = ob.data
    bpy.data.objects.remove(ob)
    bpy.data.meshes.remove(me)
'''


def has_bpy():
    return importlib.util.find_spec("bpy") is not None


def sha(path):
    with open(path, "rb") as fh:
        return hashlib.sha256(fh.read()).hexdigest()


def run(args, timeout=1800):
    r = subprocess.run([sys.executable] + args, capture_output=True, text=True, timeout=timeout)
    return r.returncode, r.stdout + r.stderr


@unittest.skipUnless(has_bpy(), "needs bpy==5.3.0")
class QuadriFlowIsRepeatable(unittest.TestCase):

    def test_quadriflow_gives_the_same_mesh_every_run(self):
        code, out = run(["-c", QF_SCRIPT % {"kit": KIT, "golden": GOLDEN, "n": QF_REPEATS}])
        self.assertEqual(code, 0, out[-3000:])
        hashes = [ln.split()[1] for ln in out.splitlines() if ln.startswith("QF ")]
        self.assertEqual(len(hashes), QF_REPEATS, out[-3000:])
        self.assertEqual(len(set(hashes)), 1, "QuadriFlow gave %d different meshes in %d runs"
                         % (len(set(hashes)), QF_REPEATS))


@unittest.skipUnless(has_bpy(), "needs bpy==5.3.0")
class GoldenRebuildsByteIdentical(unittest.TestCase):

    def test_every_rebuild_matches_the_committed_files(self):
        rebuilds = int(os.environ.get("DH_REBUILDS", "2"))
        want = {f: sha(os.path.join(GOLDEN, f)) for f in OUTPUTS}
        tmp = tempfile.mkdtemp(prefix="dh_rebuild_")
        try:
            folder = os.path.join(tmp, "golden-retriever")
            os.makedirs(folder)
            shutil.copy(os.path.join(GOLDEN, "breed.py"), folder)
            for i in range(rebuilds):
                code, out = run([os.path.join(KIT, "build_breed.py"), folder, "model", "motion"])
                self.assertEqual(code, 0, out[-6000:])
                self.assertEqual(out.count("ALL OK"), 2, out[-6000:])
                got = {f: sha(os.path.join(folder, f)) for f in OUTPUTS}
                differ = sorted(f for f in OUTPUTS if got[f] != want[f])
                print("rebuild %d of %d: %s" % (i + 1, rebuilds, "byte-identical" if not differ
                                                else "DIFFERS: " + ", ".join(differ)), flush=True)
                self.assertEqual(differ, [], "rebuild %d of %d differs from the committed files"
                                 % (i + 1, rebuilds))
        finally:
            shutil.rmtree(tmp, ignore_errors=True)


if __name__ == "__main__":
    unittest.main(verbosity=2)
