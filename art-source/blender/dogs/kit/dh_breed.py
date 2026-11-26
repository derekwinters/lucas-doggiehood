"""The active breed for the shared dog kit.

A breed is a folder holding a breed.py of plain data (see ../golden-retriever/breed.py and
../README.md). Its outputs (textures/, exports/, review/, motion/exports/, motion/review/) are
written into that folder.

Every kit module reads the breed through get() when it is imported, so a script chooses the
breed with use() before it imports anything else from the kit:

    import dh_breed
    dh_breed.use(sys.argv[1])          # a folder name under dogs/ or a path to a breed folder
    import dh_dog as D

or the DH_BREED environment variable names it. One process builds one breed.

Invariant - a breed definition holds data only: numbers, strings, booleans, None, and tuples,
lists and dicts of them. No imports, no functions and no Blender calls. load() refuses anything
else.
"""
import importlib.util
import os
import sys

KIT = os.path.dirname(os.path.abspath(__file__))
DOGS = os.path.dirname(KIT)

# every name a breed.py must define (the kit reads all of them)
REQUIRED = (
    "NAME", "DISPLAY_NAME", "STATS_NAME", "MOTION_STATUS", "MATERIAL_NAMES", "MATERIAL_SHADING",
    "BUDGET", "SKIN_FACES", "LOD_SKIN_TRIS", "HEAD_DY", "OY", "JAW_REST_OPEN_DEG",
    "FIELD_GRID", "TORSO", "HEAD", "TAIL", "FRONT_X", "BACK_X", "BACK_THIGH_DX",
    "FRONT_UPPER", "FRONT_FORE", "FRONT_PASTERN", "FRONT_PAW", "BACK_THIGH", "BACK_GASKIN", "BACK_META",
    "BACK_PAW", "PAW", "LOFT_STYLE", "FIELD",
    "HINGE", "LIP_RUN", "LIP_DROP", "LIP_BAND", "MOUTH_MAX_OPEN_DEG", "MOUTH_ZONE", "JAW_TIP", "TONGUE",
    "EARS", "EYES", "NOSE", "COLLAR",
    "SPINE", "TAIL_BONES", "JOINTS", "TORSO_CHAIN", "SIT", "SIT_POSE",
    "SANITY", "MEASURE",
    "COATS", "COAT_LABELS", "FUR_FLOW", "FUR_MARKINGS", "TRIM_COLOURS",
    "GAITS", "SIT_DOWN", "PLAY_BOW", "RENDER",
)
_SCALARS = (bool, int, float, str, type(None))

B = None        # the loaded breed module
DIR = None      # its folder


def resolve(arg):
    """A breed folder from a name under dogs/ or a path."""
    for d in (arg, os.path.join(DOGS, arg)):
        if os.path.isfile(os.path.join(d, "breed.py")):
            return os.path.abspath(d)
    raise SystemExit("no breed.py for breed %r (looked in %s and %s)" % (arg, os.path.abspath(arg),
                                                                       os.path.join(DOGS, arg)))


def _plain(v):
    if isinstance(v, _SCALARS):
        return True
    if isinstance(v, (tuple, list)):
        return all(_plain(x) for x in v)
    if isinstance(v, dict):
        return all(isinstance(k, _SCALARS) and _plain(x) for k, x in v.items())
    return False


def problems(module):
    """Why a loaded breed module is not a valid breed definition ([] when it is)."""
    out = ["missing %s" % k for k in REQUIRED if not hasattr(module, k)]
    for k, v in vars(module).items():
        if k.startswith("__"):
            continue
        if not _plain(v):
            out.append("%s is %s, not plain data" % (k, type(v).__name__))
    return out


def load(folder):
    """Load folder/breed.py as a module and check it is plain data."""
    spec = importlib.util.spec_from_file_location("dh_breed_data", os.path.join(folder, "breed.py"))
    m = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(m)
    bad = problems(m)
    if bad:
        raise SystemExit("%s/breed.py is not a valid breed definition: %s" % (folder, "; ".join(bad)))
    return m


def use(arg):
    """Make the breed at arg (name or folder) the active one. Returns the breed module."""
    global B, DIR
    d = resolve(arg)
    if B is not None:
        if d != DIR:
            raise RuntimeError("breed %s is already loaded in this process (asked for %s)" % (DIR, d))
        return B
    B, DIR = load(d), d
    os.environ["DH_BREED"] = d          # child processes (render helpers) see the same breed
    return B


def get():
    """The active breed, chosen by use() or else by DH_BREED."""
    if B is None:
        name = os.environ.get("DH_BREED")
        if not name:
            sys.exit("no breed chosen: pass the breed (name under dogs/ or folder) or set DH_BREED")
        use(name)
    return B
