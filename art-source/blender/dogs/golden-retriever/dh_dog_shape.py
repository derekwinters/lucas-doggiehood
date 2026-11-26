"""Round 3 shape library: the lofted volumes the continuous skin is blended from.

All coordinates are authored coordinates (before the OY shift), Blender axes:
front -Y, up +Z, metres. Each volume is a closed loft (no UVs needed: the
continuous skin is unwrapped and baked after meshing). The field recipe in
FIELD says which volumes are smooth-unioned and with what fillet size.
"""
import math

from mathutils import Vector

import dh_dog as D

X, Y = D.X, D.Y
Ring = D.Ring
HEAD_DY = D.HEAD_DY

FRONT_X = 0.165
BACK_X = 0.180

# (y, z, half width, top/front, bottom/back, exponent)
TORSO = [
    (0.625, 0.84, 0.07, 0.06, 0.07, 2.0),
    (0.610, 0.84, 0.18, 0.15, 0.18, 2.2),
    (0.560, 0.83, 0.235, 0.20, 0.20, 2.4),
    (0.490, 0.82, 0.255, 0.215, 0.195, 2.5),
    (0.420, 0.81, 0.255, 0.22, 0.180, 2.5),     # hip loop
    (0.310, 0.81, 0.260, 0.215, 0.180, 2.5),
    (0.190, 0.80, 0.270, 0.215, 0.200, 2.5),    # loin, tucked up
    (0.060, 0.80, 0.272, 0.22, 0.250, 2.5),
    (-0.060, 0.80, 0.272, 0.225, 0.292, 2.5),   # ribcage (round 4: narrower, shallower)
    (-0.150, 0.81, 0.264, 0.225, 0.302, 2.5),   # shoulder loop
    (-0.240, 0.84, 0.250, 0.22, 0.295, 2.5),
    (-0.295, 0.89, 0.228, 0.21, 0.262, 2.4),    # front of chest
    (-0.330, 0.96, 0.220, 0.20, 0.230, 2.3),    # neck base
    (-0.360, 1.04, 0.205, 0.18, 0.195, 2.2),    # collar loop
    (-0.375, 1.12, 0.185, 0.16, 0.165, 2.1),
    (-0.385, 1.20, 0.160, 0.14, 0.14, 2.0),     # neck top, inside the skull
]

TAIL = [
    (0.560, 0.875, 0.070, 0.070, 0.064),
    (0.640, 0.860, 0.074, 0.090, 0.060),
    (0.750, 0.805, 0.082, 0.125, 0.054),
    (0.855, 0.740, 0.084, 0.140, 0.048),
    (0.950, 0.685, 0.076, 0.128, 0.042),
    (1.030, 0.650, 0.058, 0.092, 0.034),
    (1.085, 0.640, 0.026, 0.045, 0.018),
]

# legs: (y, z, half width, front, back); x is per leg
FRONT_UPPER = [(-0.150, 0.92, 0.090, 0.10, 0.11), (-0.195, 0.77, 0.092, 0.105, 0.10),
               (-0.165, 0.62, 0.090, 0.090, 0.095), (-0.125, 0.50, 0.086, 0.085, 0.095)]
FRONT_FORE = [(-0.118, 0.53, 0.086, 0.085, 0.095), (-0.128, 0.40, 0.080, 0.078, 0.080),
              (-0.138, 0.27, 0.074, 0.072, 0.070), (-0.145, 0.18, 0.071, 0.068, 0.066)]
FRONT_PASTERN = [(-0.145, 0.19, 0.070, 0.066, 0.064), (-0.152, 0.14, 0.068, 0.064, 0.062),
                 (-0.165, 0.10, 0.070, 0.066, 0.062)]
FRONT_PAW = (-0.095, -0.330)          # heel y, toe y

BACK_THIGH = [(0.470, 0.86, 0.125, 0.12, 0.15), (0.445, 0.75, 0.140, 0.13, 0.185),
              (0.400, 0.62, 0.112, 0.105, 0.160), (0.360, 0.52, 0.090, 0.085, 0.115),
              (0.345, 0.47, 0.080, 0.075, 0.085)]
BACK_GASKIN = [(0.350, 0.49, 0.078, 0.075, 0.090), (0.405, 0.40, 0.070, 0.060, 0.090),
               (0.460, 0.31, 0.062, 0.052, 0.070), (0.495, 0.255, 0.058, 0.050, 0.066)]
BACK_META = [(0.497, 0.27, 0.057, 0.050, 0.064), (0.487, 0.18, 0.058, 0.052, 0.056),
             (0.475, 0.10, 0.062, 0.056, 0.054)]
BACK_PAW = (0.505, 0.265)

# joint positions (authored), for the skeleton
JOINTS = {
    "front_shoulder": (-0.200, 0.80), "front_elbow": (-0.122, 0.50), "front_wrist": (-0.146, 0.17),
    "front_paw": (-0.170, 0.075), "front_toe": (-0.315, 0.045),
    "back_hip": (0.445, 0.80), "back_stifle": (0.350, 0.48), "back_hock": (0.500, 0.255),
    "back_foot": (0.472, 0.085), "back_toe": (0.285, 0.045),
}


def _densify(rings, k=3):
    """Catmull-Rom between ring specs so the field has no ring-to-ring facets."""
    if len(rings) < 3:
        return rings
    out = []
    m = len(rings)
    for i in range(m - 1):
        p0, p1, p2, p3 = rings[max(i - 1, 0)], rings[i], rings[i + 1], rings[min(i + 2, m - 1)]
        for j in range(k):
            t = j / k
            w = (-0.5 * t ** 3 + t * t - 0.5 * t, 1.5 * t ** 3 - 2.5 * t * t + 1, -1.5 * t ** 3 + 2 * t * t + 0.5 * t,
                 0.5 * t ** 3 - 0.5 * t * t)
            f = lambda a: w[0] * a(p0) + w[1] * a(p1) + w[2] * a(p2) + w[3] * a(p3)
            out.append(Ring(Vector(f(lambda r: r.c)), max(0.005, f(lambda r: r.ra)), max(0.005, f(lambda r: r.rt)),
                            max(0.005, f(lambda r: r.rb)), f(lambda r: r.e)))
    out.append(rings[-1])
    return out


def _loft(name, rings, n, side=X, dome=0.35, tangents=None):
    p = D.Part(name)
    p.loft(_densify(rings), n, side, (0, 0, 1, 1), dome=dome, tangents=tangents)
    return p


def _paw(name, x, y0, y1, n, m):
    p = D.Part(name)
    pr = []
    for i in range(m):
        ph = math.pi * (i + 0.5) / m
        k = math.sin(ph)
        yy = (y0 + y1) / 2 + math.cos(ph) * (y0 - y1) / 2
        pr.append(Ring((x, yy, 0.064), 0.098 * k ** 0.7, 0.064 * k ** 0.8, 0.064 * min(1.0, k ** 0.3), 2.6))
    p.loft(pr, n, X, (0, 0, 1, 1), tangents=[-Y] * m, dome=0.6)
    return p


def head_volume(n=40):
    """The round-2 head spec (jaw included), moved back by HEAD_DY."""
    spec = [
        (-0.195, 1.335, 0.07, 0.07, 0.07, 2.0),
        (-0.225, 1.34, 0.19, 0.17, 0.17, 2.1),
        (-0.285, 1.345, 0.27, 0.22, 0.215, 2.2),
        (-0.370, 1.345, 0.300, 0.24, 0.245, 2.2),
        (-0.460, 1.335, 0.295, 0.23, 0.255, 2.2),
        (-0.545, 1.315, 0.265, 0.195, 0.265, 2.3),
        (-0.610, 1.28, 0.225, 0.15, 0.255, 2.4),
        (-0.670, 1.245, 0.190, 0.12, 0.235, 2.5),
        (-0.740, 1.235, 0.175, 0.112, 0.215, 2.6),
        (-0.805, 1.23, 0.152, 0.10, 0.185, 2.5),
        (-0.848, 1.225, 0.105, 0.072, 0.13, 2.2),
        (-0.868, 1.225, 0.050, 0.04, 0.06, 2.0),
    ]
    rings = [Ring((0, y + HEAD_DY, z), a, t, b, e) for y, z, a, t, b, e in spec]
    return _loft("v_head", rings, n, dome=0.5)


def volumes(n=40):
    """{name: Part} of every volume in the continuous skin."""
    v = {}
    v["torso"] = _loft("v_torso", [Ring((0, y, z), a, t, b, e) for y, z, a, t, b, e in TORSO], n)
    v["head"] = head_volume(n)
    v["tail"] = _loft("v_tail", [Ring((0, y, z), a, t, b, 2.1) for y, z, a, t, b in TAIL], n, dome=0.7)
    for sx, s in ((1, "L"), (-1, "R")):
        fx, bx = sx * FRONT_X, sx * BACK_X
        v["front_upper_" + s] = _loft("v_fu" + s, [Ring((fx, y, z), a, f, b, 2.2) for y, z, a, f, b in FRONT_UPPER], n, dome=0.3)
        v["front_fore_" + s] = _loft("v_ff" + s, [Ring((fx, y, z), a, f, b, 2.2) for y, z, a, f, b in FRONT_FORE], n, dome=0.3)
        v["front_pastern_" + s] = _loft("v_fp" + s, [Ring((fx, y, z), a, f, b, 2.2) for y, z, a, f, b in FRONT_PASTERN], n, dome=0.3)
        v["front_paw_" + s] = _paw("v_fpaw" + s, fx, FRONT_PAW[0], FRONT_PAW[1], n, 7)
        v["back_thigh_" + s] = _loft("v_bt" + s, [Ring((bx + sx * 0.025, y, z), a, f, b, 2.3) for y, z, a, f, b in BACK_THIGH], n, dome=0.3)
        v["back_gaskin_" + s] = _loft("v_bg" + s, [Ring((bx, y, z), a, f, b, 2.2) for y, z, a, f, b in BACK_GASKIN], n, dome=0.3)
        v["back_meta_" + s] = _loft("v_bm" + s, [Ring((bx, y, z), a, f, b, 2.2) for y, z, a, f, b in BACK_META], n, dome=0.3)
        v["back_paw_" + s] = _paw("v_bpaw" + s, bx, BACK_PAW[0], BACK_PAW[1], n, 7)
    return v


# smooth-union recipe: (group, [(volume, fillet k)]) - each group is folded with smin,
# then groups are folded into the result with the group's k
FIELD = [
    ("core", 0.0, [("torso", 0.0), ("head", 0.045), ("tail", 0.05)]),
    ("front_L", 0.06, [("front_upper_L", 0.0), ("front_fore_L", 0.035), ("front_pastern_L", 0.025), ("front_paw_L", 0.03)]),
    ("front_R", 0.06, [("front_upper_R", 0.0), ("front_fore_R", 0.035), ("front_pastern_R", 0.025), ("front_paw_R", 0.03)]),
    ("back_L", 0.05, [("back_thigh_L", 0.0), ("back_gaskin_L", 0.04), ("back_meta_L", 0.03), ("back_paw_L", 0.03)]),
    ("back_R", 0.05, [("back_thigh_R", 0.0), ("back_gaskin_R", 0.04), ("back_meta_R", 0.03), ("back_paw_R", 0.03)]),
]
