"""Shape library: the lofted volumes the continuous skin is blended from (the breed's profiles).

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

B = D.B

FRONT_X = B.FRONT_X
BACK_X = B.BACK_X

# the breed's profiles (dh_breed): torso / head (y, z, half width, top, bottom, exponent),
# tail and leg segments (y, z, half width, top/front, bottom/back)
TORSO = B.TORSO
TAIL = B.TAIL
FRONT_UPPER = B.FRONT_UPPER
FRONT_FORE = B.FRONT_FORE
FRONT_PASTERN = B.FRONT_PASTERN
FRONT_PAW = B.FRONT_PAW               # heel y, toe y
BACK_THIGH = B.BACK_THIGH
BACK_GASKIN = B.BACK_GASKIN
BACK_META = B.BACK_META
BACK_PAW = B.BACK_PAW
STYLE = B.LOFT_STYLE

# joint positions (authored), for the skeleton
JOINTS = {k: tuple(v) for k, v in B.JOINTS.items()}


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
    P = B.PAW
    p = D.Part(name)
    pr = []
    for i in range(m):
        ph = math.pi * (i + 0.5) / m
        k = math.sin(ph)
        yy = (y0 + y1) / 2 + math.cos(ph) * (y0 - y1) / 2
        pr.append(Ring((x, yy, P["z"]), P["half_width"] * k ** 0.7, P["top"] * k ** 0.8,
                       P["bottom"] * min(1.0, k ** 0.3), P["e"]))
    p.loft(pr, n, X, (0, 0, 1, 1), tangents=[-Y] * m, dome=0.6)
    return p


def head_volume(n=40):
    """The breed's head (jaw included), moved back by HEAD_DY."""
    rings = [Ring((0, y + HEAD_DY, z), a, t, b, e) for y, z, a, t, b, e in B.HEAD]
    return _loft("v_head", rings, n, dome=STYLE["head_dome"])


def volumes(n=40):
    """{name: Part} of every volume in the continuous skin."""
    v = {}
    v["torso"] = _loft("v_torso", [Ring((0, y, z), a, t, b, e) for y, z, a, t, b, e in TORSO], n,
                       dome=STYLE["torso_dome"])
    v["head"] = head_volume(n)
    te, td = STYLE["tail"]
    v["tail"] = _loft("v_tail", [Ring((0, y, z), a, t, b, te) for y, z, a, t, b in TAIL], n, dome=td)
    fe, fd = STYLE["front_leg"]
    he, hd = STYLE["back_thigh"]
    be, bd = STYLE["back_leg"]
    dx = B.BACK_THIGH_DX
    m = B.PAW["rings"]
    for sx, s in ((1, "L"), (-1, "R")):
        fx, bx = sx * FRONT_X, sx * BACK_X
        v["front_upper_" + s] = _loft("v_fu" + s, [Ring((fx, y, z), a, f, b, fe) for y, z, a, f, b in FRONT_UPPER], n, dome=fd)
        v["front_fore_" + s] = _loft("v_ff" + s, [Ring((fx, y, z), a, f, b, fe) for y, z, a, f, b in FRONT_FORE], n, dome=fd)
        v["front_pastern_" + s] = _loft("v_fp" + s, [Ring((fx, y, z), a, f, b, fe) for y, z, a, f, b in FRONT_PASTERN], n, dome=fd)
        v["front_paw_" + s] = _paw("v_fpaw" + s, fx, FRONT_PAW[0], FRONT_PAW[1], n, m)
        v["back_thigh_" + s] = _loft("v_bt" + s, [Ring((bx + sx * dx, y, z), a, f, b, he) for y, z, a, f, b in BACK_THIGH], n, dome=hd)
        v["back_gaskin_" + s] = _loft("v_bg" + s, [Ring((bx, y, z), a, f, b, be) for y, z, a, f, b in BACK_GASKIN], n, dome=bd)
        v["back_meta_" + s] = _loft("v_bm" + s, [Ring((bx, y, z), a, f, b, be) for y, z, a, f, b in BACK_META], n, dome=bd)
        v["back_paw_" + s] = _paw("v_bpaw" + s, bx, BACK_PAW[0], BACK_PAW[1], n, m)
    return v


# smooth-union recipe (the breed's): (group, k, [(volume, fillet k)]) - each group is folded with
# smin, then groups are folded into the result with the group's k
FIELD = B.FIELD
