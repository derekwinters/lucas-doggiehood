"""The backyard pools' dimensions and plan geometry (#741). Plain Python, no Blender, so the
generator (dh_pool) and the check (tests/) share one source.

Two approved shapes, sized by their WATER surface (Derek's review on #741): a 5 m round pool and a
6 x 3 m oval (a rounded rectangle). Height and water height come from PoolPlacement.cs, read at run
time so no measurement is re-typed: PoolPlacement derives them from the rendered adult dog, as
`public const` expressions over its own earlier constants, which core_consts() evaluates in order.
"""
import math
import os
import re

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.normpath(os.path.join(HERE, "..", "..", "..", ".."))
POOL_PLACEMENT_CS = os.path.join(REPO, "Assets", "Scripts", "Core", "World", "PoolPlacement.cs")

# Provisional per-model triangle budget from #799, until its on-device numbers are set.
TRI_BUDGET = 6000

SHAPES = ("circle", "oval")      # both approved; the game picks one per pool (#926)
CIRCLE_WATER_D = 5.0             # Derek: "try a 5m water circle"
OVAL_WATER_L = 6.0               # Derek: "a 6m x 3m oval with small radius corners", long axis X
OVAL_WATER_W = 3.0
OVAL_CORNER_R = 0.45             # water-surface corner radius
CIRCLE_SEGMENTS = 48             # sides round the circle (low-poly)
CORNER_SEGMENTS = 6              # sides per rounded corner of the oval

# Rings of the model are the water outline offset outward by these distances.
WALL_T = 0.08                    # wall thickness
RIM_LIP = 0.025                  # rim cap overhang past each face of the wall
D_LINER = 0.0                    # liner face = water edge
D_WALL = WALL_T                  # wall outer face
D_RIM_OUT = WALL_T + RIM_LIP
D_RIM_IN = -RIM_LIP

# Steps against the outside wall, on the camera's side. The game camera (yaw -40) sits at plan
# angle 230 deg from the pool; the circle's steps are turned STEPS_TURN_DEG off that line so one
# side of them shows. The oval's are in the middle of its front (-Y) long side.
GAME_CAMERA_PLAN_DEG = 230.0
STEPS_TURN_DEG = 30.0
CIRCLE_STEPS_DEG = GAME_CAMERA_PLAN_DEG + STEPS_TURN_DEG
STEP_COUNT = 3                   # treads; even rises from the ground, over the treads, to the rim
STEP_TREAD = 0.26                # depth of each tread
STEP_WIDTH = 0.90                # across the steps
STEP_EMBED = 0.02                # the top column starts this far into the wall, so no seam shows

_CONST = re.compile(r"public const (?:float|int) (\w+)\s*=\s*([^;]+);")
_FLOAT_LITERAL = re.compile(r"(\d+(?:\.\d+)?)f\b")
_SAFE_EXPR = re.compile(r"^[\w\s.+\-*/()]+$")


def core_consts(path=POOL_PLACEMENT_CS):
    """{name: value} for every numeric `public const` in PoolPlacement.cs, evaluated in order."""
    with open(path) as f:
        src = f.read()
    out = {}
    for name, expr in _CONST.findall(src):
        expr = _FLOAT_LITERAL.sub(r"\1", expr).strip()
        if not _SAFE_EXPR.match(expr):
            raise ValueError("cannot read PoolPlacement.%s = %s" % (name, expr))
        out[name] = float(eval(expr, {"__builtins__": {}}, dict(out)))
    return out


def water_half_extents(shape):
    if shape == "circle":
        return CIRCLE_WATER_D / 2, CIRCLE_WATER_D / 2
    return OVAL_WATER_L / 2, OVAL_WATER_W / 2


def outline(shape, d):
    """The water outline offset outward by d, as (x, y) points counter-clockwise from +X. Every
    offset gives the same count, so rings at different offsets pair up point for point."""
    if shape == "circle":
        r = CIRCLE_WATER_D / 2 + d
        return [(r * math.cos(2 * math.pi * i / CIRCLE_SEGMENTS), r * math.sin(2 * math.pi * i / CIRCLE_SEGMENTS))
                for i in range(CIRCLE_SEGMENTS)]
    cx, cy, rc = OVAL_WATER_L / 2 - OVAL_CORNER_R, OVAL_WATER_W / 2 - OVAL_CORNER_R, OVAL_CORNER_R + d
    pts = []
    for q, (sx, sy) in enumerate(((1, 1), (-1, 1), (-1, -1), (1, -1))):
        for k in range(CORNER_SEGMENTS + 1):
            a = math.pi / 2 * (q + k / CORNER_SEGMENTS)
            pts.append((sx * cx + rc * math.cos(a), sy * cy + rc * math.sin(a)))
    return pts


def steps_anchor(shape):
    """(point on the wall's outer face, outward unit normal, how far that face curves back across
    the steps' width) where the steps stand."""
    if shape == "circle":
        r = CIRCLE_WATER_D / 2 + D_WALL
        a = math.radians(CIRCLE_STEPS_DEG)
        n = (math.cos(a), math.sin(a))
        return (r * n[0], r * n[1]), n, r - math.sqrt(r * r - (STEP_WIDTH / 2) ** 2)
    return (0.0, -(OVAL_WATER_W / 2 + D_WALL)), (0.0, -1.0), 0.0


def steps_point(shape, along, across):
    """A plan point on the steps: `along` out from the wall face, `across` sideways."""
    (px, py), n, _sag = steps_anchor(shape)
    return (px + n[0] * along - n[1] * across, py + n[1] * along + n[0] * across)


def footprint(shape):
    """What the model covers in plan: the rim's outer ring plus the steps' outer corners.
    {"x": [min, max], "y": [min, max], "radius": furthest point from the origin}."""
    pts = outline(shape, D_RIM_OUT)
    pts += [steps_point(shape, STEP_COUNT * STEP_TREAD, s * STEP_WIDTH / 2) for s in (-1, 1)]
    return {"x": [min(p[0] for p in pts), max(p[0] for p in pts)],
            "y": [min(p[1] for p in pts), max(p[1] for p in pts)],
            "radius": max(math.hypot(*p) for p in pts)}
