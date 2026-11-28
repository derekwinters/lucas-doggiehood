"""Build a breed with the shared dog kit: one command per breed.

  python3 art-source/blender/dogs/kit/build_breed.py <breed> [step ...]

<breed> is a folder name under art-source/blender/dogs/ (e.g. golden-retriever) or a path to a
folder holding a breed.py. Steps (default: model motion):

  model                 export_dog.py: textures, exports, every model check, stats (prints ALL OK)
  motion                export_anim.py: the clips, per-frame contact checks, glTF read-back (ALL OK)
  solve-sit             solve_sit.py: print a new SIT for breed.py (after changing the shape or rig)
  solve-lie             solve_lie.py: print a new LIE for breed.py (the lying pose; same rule)
  viewer                export_viewer.py: web-viewer data (git-ignored)
  render <shot> [dir]   render_dog.py: one review sheet (see render_dog.py for the shots)
  video <Clip> [dir]    render_motion.py: one clip's review video
  sheet <Clip> [dir]    render_motion.py: one clip's contact sheet

Each step runs in its own Python process, as when run on its own, and the command stops at the
first step that fails (exit status non-zero). Render one sheet or clip per command: EEVEE on
lavapipe can crash after about 100 frames in one process.
"""
import os
import subprocess
import sys

KIT = os.path.dirname(os.path.abspath(__file__))
SCRIPTS = {"model": "export_dog.py", "motion": "export_anim.py", "solve-sit": "solve_sit.py",
           "solve-lie": "solve_lie.py",
           "viewer": "export_viewer.py", "render": "render_dog.py", "video": "render_motion.py",
           "sheet": "render_motion.py"}
ARGS = {"render": (1, 2), "video": (1, 2), "sheet": (1, 2)}      # (required, optional) arguments


def plan(argv):
    """[(step, [command])] for build_breed.py's arguments."""
    if not argv:
        raise SystemExit(__doc__)
    breed, rest = argv[0], list(argv[1:]) or ["model", "motion"]
    out = []
    while rest:
        step = rest.pop(0)
        if step not in SCRIPTS:
            raise SystemExit("unknown step %r\n\n%s" % (step, __doc__))
        need, opt = ARGS.get(step, (0, 0))
        args = []
        while rest and rest[0] not in SCRIPTS and len(args) < need + opt:
            args.append(rest.pop(0))
        if len(args) < need:
            raise SystemExit("%s needs %d argument(s)" % (step, need))
        mode = [step] if step in ("video", "sheet") else []
        out.append((step, [sys.executable, os.path.join(KIT, SCRIPTS[step]), breed] + mode + args))
    return out


def main(argv):
    for step, cmd in plan(argv):
        print("==> %s: %s" % (step, " ".join(cmd[1:])), flush=True)
        r = subprocess.run(cmd)
        if r.returncode:
            print("==> %s FAILED (exit %d)" % (step, r.returncode), flush=True)
            return r.returncode
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
