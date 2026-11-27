"""The splash environment's objects, authored as geometric primitives (used by build_env.py).

All coordinates are in the pixel space of Assets/Art/Splash/cover-art.png (2816x1536),
measured on the original. Each object lists its faces back to front; colours are measured
from the original by build_env.py unless a face says otherwise (see build_env.P).
OBJECTS is in draw order within each layer.
"""
import math

import pathops

from build_env import SEAM, A, C, E, O, P, R, SUN_C, W  # noqa: F401
from build_env import (BOX_CAP_K, BOX_END_K, BOX_H_K, BOX_TOP_K, OFFSET_X, OFFSET_Y, OVERHANG_K,  # noqa: F401
                       SILL_OFFSET)

OBJECTS = []


def add(*objs):
    OBJECTS.extend(objs)


# ==========================================================================
# Clouds: soft, puffy, rounded cartoon clouds - a deliberate change from the original's
# blocky voxel clouds (Derek's call, #841). Each cloud is a chain of overlapping circular
# lobes on a gently rounded base, drawn as one clean silhouette (the union of the lobes,
# cut by the base) in three flat tones, back to front:
#   underside - the whole silhouette, in the pale shaded blue (it shows as the lower band);
#   mid       - the part the lobes still cover when shifted a little up and toward the sun;
#   lit       - the part they cover when shifted further: the white lit top and sun side.
# Every outline is the exact boundary of those circles (arcs), so the tones share edges
# and no seam or sliver opens between lobes. The colours are the tones the previous blocky
# clouds measured on the original (their lit sides, fronts and undersides); the two near
# the sun keep its slightly greener cast.
# ==========================================================================
# Where the original's blocky clouds are (their outlines, as measured for the previous
# version). The sky and the sun's glow are fitted to the original's open sky, so these stay
# out of that fit whatever shape our clouds have (build_env.open_sky).
ORIGINAL_CLOUDS = [
    [(100, -10), (505, -10), (540, 22), (542, 65), (652, 65), (700, 100), (710, 115), (710, 132), (733, 147),
     (733, 175), (705, 176), (580, 178), (547, 163), (343, 163), (100, 52)],
    [(2141, -10), (2299, -10), (2299, 50), (2237, 92), (2210, 92), (2145, 131), (2036, 127), (2036, 100),
     (2049, 90), (2075, 76), (2076, 50)],
    [(2618, -10), (2826, -10), (2826, 100), (2520, 213), (2340, 200), (2340, 165), (2385, 143), (2385, 108),
     (2392, 103), (2437, 82), (2515, 86), (2516, 60), (2520, 55)],
    [(888, 221.5), (966, 217.5), (966, 202.5), (1032, 198.5), (1084, 237.5), (1084, 288.2), (940, 297.2),
     (888, 258.2)],
    [(1911, 220.5), (2025, 222.5), (2028, 290), (1985, 325), (1868, 325), (1868, 255.5)],
]
CLOUD_TONES = {               # (underside, mid, lit)
    "cool": ("#bce6f9", "#d3effb", "#f9fdfe"),
    "warm": ("#c6e7ef", "#daf2f6", "#f9fdfc"),
    "behind": ("#b7e5f9", "#cdedfa", "#f8fcfd"),
}
CLOUD_LIFT = 0.2              # lit shift, as a share of the cloud's biggest lobe radius
CLOUD_SUNWARD = 0.6           # lit shift toward the sun, as a share of its upward shift
CLOUD_MID = 0.45              # the mid tone's shift, as a share of the lit shift

CLOUD_SEGS = 360      # polygon segments a lobe is flattened to for the boolean ops
CLOUD_EPS = 0.03      # px: a vertex this close to a circle lies on it
CLOUD_ARC = 90.0      # degrees: an arc is written in pieces no longer than this
CLOUD_MIN_AREA = 40.0 # px2: a piece of a tone smaller than this is a sliver (the build stops)


def _poly_path(pts):
    p = pathops.Path()
    pen = p.getPen()
    pen.moveTo(pts[0])
    for q in pts[1:]:
        pen.lineTo(q)
    pen.closePath()
    return p


def _circle_pts(cx, cy, r, n=CLOUD_SEGS, ph=0.0):
    return [(cx + r * math.cos(2 * math.pi * (k + ph) / n), cy + r * math.sin(2 * math.pi * (k + ph) / n))
            for k in range(n)]


def _circle3(a, b, c):
    (ax, ay), (bx, by), (cx, cy) = a, b, c
    d = 2 * (ax * (by - cy) + bx * (cy - ay) + cx * (ay - by))
    ux = ((ax * ax + ay * ay) * (by - cy) + (bx * bx + by * by) * (cy - ay) + (cx * cx + cy * cy) * (ay - by)) / d
    uy = ((ax * ax + ay * ay) * (cx - bx) + (bx * bx + by * by) * (ax - cx) + (cx * cx + cy * cy) * (bx - ax)) / d
    return ux, uy, math.hypot(ax - ux, ay - uy)


def _union(paths):
    out = paths[0]
    for p in paths[1:]:
        out = pathops.op(out, p, pathops.PathOp.UNION)
    return out


def _contours(path):
    cs, cur = [], []
    for verb, pts in path:
        if verb == pathops.PathVerb.MOVE:
            cur = [pts[0]]
        elif verb == pathops.PathVerb.LINE:
            cur.append(pts[0])
        elif verb == pathops.PathVerb.CLOSE:
            if len(cur) > 1 and cur[-1] == cur[0]:
                cur.pop()
            cs.append(cur)
        else:
            raise ValueError(verb)
    return cs


def _area(pts):
    return 0.5 * sum(x0 * y1 - x1 * y0 for (x0, y0), (x1, y1) in zip(pts, pts[1:] + pts[:1]))


def _on(p, c):
    tol = CLOUD_EPS + (c[3] if len(c) > 3 else c[2] * (1 - math.cos(math.pi / CLOUD_SEGS)))
    return abs(math.hypot(p[0] - c[0], p[1] - c[1]) - c[2]) < tol


def _to_arcs(pts, circles):
    """A flattened outline whose edges lie on known circles -> arcs-face points: corners
    where the outline changes circle, each run on one circle written as arcs of at most
    CLOUD_ARC degrees (straight edges where no circle fits)."""
    n = len(pts)
    def label(i):
        a, b = pts[i], pts[(i + 1) % n]
        m = ((a[0] + b[0]) / 2, (a[1] + b[1]) / 2)
        best = None
        for k, c in enumerate(circles):
            if _on(a, c) and _on(b, c):
                d = abs(math.hypot(m[0] - c[0], m[1] - c[1]) - c[2])
                if best is None or d < best[0]:
                    best = (d, k)
        return None if best is None else best[1]
    labels = [label(i) for i in range(n)]
    # start at a change of label so no run wraps round the start
    s = next((i for i in range(n) if labels[i] != labels[i - 1]), 0)
    order = list(range(s, n)) + list(range(s))
    runs = []
    for i in order:
        if runs and runs[-1][0] == labels[i] and labels[i] is not None:
            runs[-1][1].append(i)
        else:
            runs.append((labels[i], [i]))
    out = []
    for lab, idx in runs:
        p0 = pts[idx[0]]
        if not out:
            out.append((round(p0[0], 2), round(p0[1], 2)))
        if lab is None:
            for i in idx:
                q = pts[(i + 1) % n]
                out.append((round(q[0], 2), round(q[1], 2)))
            continue
        cx, cy, r = circles[lab][:3]
        end = pts[(idx[-1] + 1) % n]
        a0 = math.atan2(p0[1] - cy, p0[0] - cx)
        # signed sweep along the run (sum of the small steps)
        sweep, prev = 0.0, a0
        for i in idx[1:] + [None]:
            q = end if i is None else pts[i]
            a = math.atan2(q[1] - cy, q[0] - cx)
            da = (a - prev + math.pi) % (2 * math.pi) - math.pi
            sweep += da
            prev = a
        k = max(1, math.ceil(abs(math.degrees(sweep)) / CLOUD_ARC - 1e-9))
        tag = "arc" if sweep > 0 else "arcin"
        for j in range(1, k + 1):
            if j == k:
                x, y = end
            else:
                a = a0 + sweep * j / k
                x, y = cx + r * math.cos(a), cy + r * math.sin(a)
            out.append((tag, round(r, 2), round(x, 2), round(y, 2)))
    # drop pieces that round to nothing (a run of a single tiny step at a corner)
    def end(p):
        return (p[2], p[3]) if isinstance(p[0], str) else p
    kept = [out[0]]
    for p in out[1:]:
        if end(p) != end(kept[-1]):
            kept.append(p)
    if end(kept[-1]) != kept[0]:
        kept.append(kept[0])
    return kept


def cloud_regions(lobes, base, lit, mid):
    # Skia's path ops occasionally give up on a near-degenerate input; flattening the
    # circles from a different starting angle (deterministic) gets round it
    for ph in (0.0, 0.25, 0.5, 0.75, 0.125, 0.375):
        try:
            return _cloud_regions(lobes, base, lit, mid, ph)
        except pathops.PathOpsError:
            pass
    raise RuntimeError("cloud: path ops failed")


def _cloud_regions(lobes, base, lit, mid, ph):
    """(silhouette, mid region, lit region) as lists of arcs-face point lists.
    lobes: [(cx, cy, r)]; base: three points the gently rounded base runs through;
    lit / mid: (dx, dy) the lobes are shifted by (toward the sun and up) - the lit face
    (and the mid face) is the part of the cloud the shifted lobes still cover."""
    bc = _circle3(*base)
    circles = [tuple(map(float, l)) for l in lobes]
    fill = [(c[0], c[1]) for c in circles] + [(circles[-1][0], max(p[1] for p in base) + 80),
                                              (circles[0][0], max(p[1] for p in base) + 80)]
    body = _union([_poly_path(_circle_pts(*c, ph=ph)) for c in circles] + [_poly_path(fill)])
    # the base: the region above the base circle's lower arc, over the cloud's width
    x0 = min(c[0] - c[2] for c in circles) - 20
    x1 = max(c[0] + c[2] for c in circles) + 20
    a0 = math.atan2(math.sqrt(bc[2] ** 2 - (x1 - bc[0]) ** 2), x1 - bc[0])
    a1 = math.atan2(math.sqrt(bc[2] ** 2 - (x0 - bc[0]) ** 2), x0 - bc[0])
    m = max(48, int(CLOUD_SEGS * (a1 - a0) / (2 * math.pi)) + 1)
    arc = [(bc[0] + bc[2] * math.cos(a0 + (a1 - a0) * t / m), bc[1] + bc[2] * math.sin(a0 + (a1 - a0) * t / m))
           for t in range(m + 1)]
    above = arc + [(x0, -400.0), (x1, -400.0)]
    body = pathops.op(body, _poly_path(above), pathops.PathOp.INTERSECTION)
    bc = bc + (bc[2] * (1 - math.cos((a1 - a0) / m / 2)),)
    allc = circles + [bc]
    def shifted(d):
        # the lobes shifted straight up and shifted up toward the sun: the straight copy keeps
        # the top notches between lobes lit, so no crescent of shade opens along the top
        cs = [(c[0] + dx, c[1] + d[1], c[2]) for dx in (0.0, d[0]) for c in circles]
        u = _union([_poly_path(_circle_pts(*c, ph=ph)) for c in cs])
        return u, cs
    out = []
    sil = _contours(body)
    assert len(sil) == 1, f"cloud silhouette is {len(sil)} pieces"
    out.append([_to_arcs(sil[0], allc)])
    for d in (mid, lit):
        u, cs = shifted(d)
        reg = pathops.op(body, u, pathops.PathOp.INTERSECTION)
        pieces = _contours(reg)
        thin = [round(abs(_area(c)), 1) for c in pieces if abs(_area(c)) < CLOUD_MIN_AREA]
        assert not thin, f"cloud: slivers of {thin} px2 in a tone; move the lobes"
        out.append([_to_arcs(c, allc + cs) for c in pieces])
    return out


def cloud(oid, lobes, base, tones):
    """A round cloud: lobes [(cx, cy, r)] left to right, base three points its gently
    rounded bottom runs through, tones a CLOUD_TONES key. Lit from the sun's side."""
    sx = 1.0 if SUN_C[0] > sum(c[0] for c in lobes) / len(lobes) else -1.0
    h = CLOUD_LIFT * max(c[2] for c in lobes)
    lit = (sx * CLOUD_SUNWARD * h, -h)
    mid = (lit[0] * CLOUD_MID, lit[1] * CLOUD_MID)
    sil, mids, lits = cloud_regions(lobes, base, lit, mid)
    under, midc, litc = CLOUD_TONES[tones]
    faces = [A("underside", sil[0], c=under)]
    for name, regs, col in (("mid", mids, midc), ("lit", lits, litc)):
        assert regs, f"{oid}: no {name} face"
        faces += [A(name if i == 0 else f"{name}-{i + 1}", pts, c=col) for i, pts in enumerate(regs)]
    return O(oid, "clouds", faces)


add(
    cloud("cloud-top-left", [(160, 158, 30), (222, 134, 46), (300, 108, 62), (385, 96, 72), (470, 104, 64),
                             (548, 120, 52), (618, 138, 40), (684, 158, 30)],
          [(135, 176), (420, 180), (712, 176)], "cool"),
    cloud("cloud-top-mid", [(2066, 116, 22), (2110, 92, 38), (2170, 70, 52), (2232, 82, 44), (2276, 114, 22)],
          [(2046, 128), (2170, 131), (2298, 127)], "warm"),
    # runs off the right edge, its base rising toward the street's vanishing point as before
    cloud("cloud-top-right", [(2376, 192, 22), (2425, 160, 38), (2490, 135, 55), (2570, 118, 66),
                              (2660, 105, 70), (2750, 98, 66), (2830, 118, 56)],
          [(2350, 206), (2600, 186), (2840, 160)], "cool"),
    # behind the title, which hides most of them
    cloud("cloud-behind-title-left", [(908, 282, 18), (942, 262, 32), (986, 240, 44), (1035, 246, 40),
                                      (1068, 275, 22)],
          [(890, 291), (985, 297), (1088, 289)], "behind"),
    cloud("cloud-behind-title-right", [(1886, 308, 18), (1918, 286, 32), (1962, 262, 42), (2005, 280, 32),
                                       (2032, 306, 20)],
          [(1868, 318), (1950, 325), (2048, 316)], "warm"),
)


# ==========================================================================
# Street: lawns, sidewalks, kerbs, road (far-to-near edge lines measured on the original)
# ==========================================================================
def xat(pl, y):
    """x of a far-to-near polyline at height y (extrapolated past its ends)."""
    for (x0, y0), (x1, y1) in zip(pl, pl[1:]):
        if y <= y1 or (x1, y1) == pl[-1]:
            return round(x0 + (x1 - x0) * (y - y0) / (y1 - y0), 2)
    raise AssertionError("unreachable")


def between(pa, pb, y0, y1):
    """The strip between polylines pa and pb from height y0 to y1."""
    ys = sorted({y0, y1} | {y for pl in (pa, pb) for _, y in pl if y0 < y < y1})
    return [(xat(pa, y), y) for y in ys] + [(xat(pb, y), y) for y in reversed(ys)]


TOP, BOT = 686, 1546
ROAD_L = [(1321, 740), (612, 1380), (443, 1536)]            # sidewalk's edge at the kerb, left
KERB_L = [(1324, 740), (667, 1380), (510, 1536)]            # kerb face's foot, left
GUTTER_L = [(1327, 740), (700, 1387), (547, 1536)]          # kerb's shadow line on the road, left
ROAD_R = [(1621, 740), (2247, 1340), (2440, 1536)]
KERB_R = [(1618, 740), (2205, 1340), (2398, 1536)]
GUTTER_R = [(1615, 740), (2165, 1340), (2350, 1536)]
LAWN_L = [(1316, 700), (304, 1320), (108, 1440)]           # lawn's edge at the sidewalk, left
LAWN_R = [(1640, 700), (2600, 1320), (2788, 1440)]


def joint(pa, pb, y, h):
    """A sidewalk joint: a band h px tall across the strip between pa and pb at height y."""
    return [(xat(pa, y), y), (xat(pb, y), y), (xat(pb, y + h), y + h), (xat(pa, y + h), y + h)]


# Expansion joints. A joint runs straight across the sidewalk, square to the street, so in
# one-point perspective it is a horizontal line from edge to edge. The slabs are all the same
# size, so the joints are evenly spaced along the street: their spacing on screen
# shrinks toward the point where the sidewalk's two edges meet (the street's vanishing point
# as this sidewalk is drawn - its edges meet near (1470, 606) on the left and (1454, 580) on
# the right, above the road's visible end at y 700). With y_h that point's height, a joint's
# 1 / (y - y_h) steps by the same amount from slab to slab. The original's two joints on each
# side (joint-1, joint-2) are one slab apart and set that step; every other joint continues
# their series, near to far, up to where the paving begins. (The far cross street's sidewalks,
# where the far-turns lead, lie behind the horizon treeline; only the corners show, so they
# carry no joints.)
# Invariant: every sidewalk joint is horizontal, spans the sidewalk's full width, and sits on
# the one-slab series its sidewalk's two original joints define.
JOINT_FAR = 713          # the farthest joint's top edge stays below this (the paving starts at 712)
JOINT_MIN_H = 0.25       # a far joint is never thinner than this (px, plus the face's seam stroke)


def meet(pa, pb):
    """Where the far segments of two far-to-near edge lines meet (x, y)."""
    (ax0, ay0), (ax1, ay1) = pa[0], pa[1]
    (bx0, by0), (bx1, by1) = pb[0], pb[1]
    sa, sb = (ax1 - ax0) / (ay1 - ay0), (bx1 - bx0) / (by1 - by0)
    y = (bx0 - ax0 + sa * ay0 - sb * by0) / (sa - sb)
    return ax0 + sa * (y - ay0), y


def joint_series(pa, pb, near, far, seam):
    """[(y, h)] of a sidewalk's joints, near to far: the joint (y, h) near and the joint at
    height far one slab beyond it, then on (both ways) while the joint shows on the paving.
    A joint's drawn thickness (h plus the seam stroke) shrinks with distance like its spacing."""
    yh = meet(pa, pb)[1]
    (y0, h0), u0 = near, near[0] - yh
    step = 1 / (far - yh) - 1 / u0
    out = []
    n = 0
    while 1 / u0 - (n + 1) * step > 0 and yh + 1 / (1 / u0 - (n + 1) * step) < BOT:
        n += 1                                   # joints nearer than the first one, if any show
    for i in range(-n, 1000):
        u = 1 / (1 / u0 + i * step)
        y = yh + u
        if y < JOINT_FAR:
            break
        h = h0 if i == 0 else max(round((h0 + seam) * u / u0 - seam, 2), JOINT_MIN_H)
        out.append((round(y, 2), h))
    return out


JOINTS_L = joint_series(LAWN_L, ROAD_L, (1418, 9), 1253, SEAM)
JOINTS_R = joint_series(ROAD_R, LAWN_R, (1422, 9), 1256, SEAM)
ORIGINAL_JOINTS = {1418, 1253, 1422, 1256}      # joint-1 and joint-2 on each side (measured)


def joint_faces(pa, pb, series, original):
    """The new joints' faces (the two original joints, measured, are listed by the caller)."""
    faces, n = [], 3
    for y, h in series:
        if round(y) in original and abs(y - round(y)) < 0.05:
            continue
        faces.append(P(f"joint-{n}", joint(pa, pb, y, h), like="joint-1", added=True))
        n += 1
    return faces


def lawn_edge(side, y):
    """x of the sidewalk's lawn edge at height y (side "l" / "r"), along the far-turn above 712."""
    pl = LAWN_L if side == "l" else LAWN_R
    if y >= 712:
        return xat(pl, y)
    turn = [(1330, 700), (xat(pl, 712), 712)] if side == "l" else [(1615, 700), (xat(pl, 712), 712)]
    return xat(turn, y)


add(
    O("lawn-left", "street", [P("grass", [(-10, 600), (1250, 640), (xat(LAWN_L, TOP), TOP), (xat(LAWN_L, BOT), BOT),
                                          (-10, BOT)], rows=True)]),
    O("lawn-right", "street", [P("grass", [(xat(LAWN_R, TOP), TOP), (1700, 640), (W + 10, 600), (W + 10, BOT),
                                           (xat(LAWN_R, BOT), BOT)], rows=True)]),
    O("lawn-far", "street", [P("grass", [(1150, 690), (1800, 690), (1800, 714), (1150, 714)], grad=True)]),
    O("sidewalk-left", "street", [
        P("paving", between(LAWN_L, ROAD_L, 712, BOT), rows=True),
        P("far-turn", [(xat(LAWN_L, 712), 712), (1330, 700), (1380, 696), (1425, 694), (1425, 699), (1395, 702),
                       (1360, 712), (xat(ROAD_L, 745), 745)], grad=True),
        P("joint-1", joint(LAWN_L, ROAD_L, 1418, 9), hid="#b9adbb"),
        P("joint-2", joint(LAWN_L, ROAD_L, 1253, 7), hid="joint-1"),
        *joint_faces(LAWN_L, ROAD_L, JOINTS_L, ORIGINAL_JOINTS),
    ]),
    O("sidewalk-right", "street", [
        P("paving", between(ROAD_R, LAWN_R, 712, BOT), rows=True),
        P("far-turn", [(xat(ROAD_R, 745), 745), (1600, 722), (1565, 703), (1535, 699), (1535, 694), (1575, 695),
                       (1615, 700), (xat(LAWN_R, 712), 712)], grad=True),
        P("joint-1", joint(ROAD_R, LAWN_R, 1422, 9), hid="sidewalk-left/joint-1"),
        P("joint-2", joint(ROAD_R, LAWN_R, 1256, 7), hid="joint-1"),
        *joint_faces(ROAD_R, LAWN_R, JOINTS_R, ORIGINAL_JOINTS),
    ]),
    O("road", "street", [
        # the road ends at the horizon, its far corners rounded where the sidewalks turn
        P("asphalt", between(GUTTER_L, GUTTER_R, 745, BOT)
          + [(1600, 722), (1565, 703), (1540, 697), (1420, 697), (1395, 700), (1360, 712)], rows=True),
    ]),
    O("kerb-left", "street", [P("face", between(ROAD_L, KERB_L, 740, BOT), rows=True),
                              P("shadow", between(KERB_L, GUTTER_L, 740, BOT), rows=True)]),
    O("kerb-right", "street", [P("face", between(KERB_R, ROAD_R, 740, BOT), rows=True),
                               P("shadow", between(GUTTER_R, KERB_R, 740, BOT), rows=True)]),
)


# ==========================================================================
# Building parts
# ==========================================================================
# The houses' door frames (measured): each house's front steps are centred on its door (street-1)
RED_DOOR = (225, 639, 340, 946)
YELLOW_DOOR = (735, 647, 787, 828)
TEAL_DOOR = (907, 650, 934, 781)
PINK_DOOR = (2030, 649, 2061, 790)
BLUE_DOOR = (2228, 646, 2283, 830)
GREEN_DOOR = (2599, 641, 2712, 950)
# the steps' faces: a near house's (seen from the side), a far house's (from the front)
NEAR_STEPS = ["steps-side", "step-1-tread", "step-1-riser", "step-2-tread", "step-2-riser"]
FAR_STEPS = ["steps", "steps-riser"]
FAR_STEPS_2 = FAR_STEPS + ["steps-2", "steps-2-riser"]
FAR_STEPS_3 = FAR_STEPS_2 + ["steps-3", "steps-3-riser"]


def moved(f, fn):
    """A copy of face f with every point mapped by fn; a face the original shows is still measured
    where it was (src), so moving it changes no measured colour."""
    g = dict(f)
    if f["kind"] == "ellipse":
        g["cx"], g["cy"] = fn(f["cx"], f["cy"])
    else:
        g["pts"] = [(p[0], p[1], *fn(p[2], p[3])) if isinstance(p[0], str) else fn(*p) for p in f["pts"]]
    if not f.get("added"):
        g["src"] = f.get("src", f)
    return g


def shift(faces, dx, dy=0.0):
    """Faces moved by (dx, dy): the street-layout pass centres steps on their doors (#841)."""
    r2 = lambda v: round(v, 2)
    return [moved(f, lambda x, y: (r2(x + dx), r2(y + dy))) for f in faces]


def centre_on(frame, faces, front):
    """The steps' faces, moved sideways so the face(s) named in front are centred on the door
    frame (x0, y0, x1, y1): street-1."""
    xs = [x for f in faces if f["name"] in front for x, _ in f["pts"]]
    return shift(faces, round((frame[0] + frame[2]) / 2 - (min(xs) + max(xs)) / 2, 2))


def window(n, frame, glass, mull=None, sill=None, sill_front=None, reveal=None, glare=True, **kw):
    """A window: frame and glass outlines (rect tuples or point lists), an optional mullion
    (y0, y1) across the glass, a sill (top face, front face) and a darker reveal strip down
    the glass's right side (x0)."""
    def poly(name, b, **k):
        return R(name, *b, **k) if len(b) == 4 and not isinstance(b[0], tuple) else P(name, b, **k)
    out = [poly(f"{n}-frame", frame, **kw), poly(f"{n}-glass", glass, hid="#4f9bd8")]
    if glare and len(glass) == 4 and not isinstance(glass[0], tuple):
        x0, y0, x1, y1 = glass
        w, h = x1 - x0, y1 - y0
        out.append(P(f"{n}-glare", [(x0 + w * 0.45, y0), (x0 + w * 0.8, y0), (x0, y0 + h * 0.62),
                                    (x0, y0 + h * 0.3)], hid=f"{n}-glass", k=1.12))
    if reveal is not None:
        x0, y0, x1, y1 = glass
        out.append(R(f"{n}-reveal", reveal, y0, x1, y1, hid=f"{n}-glass", k=0.8))
    if mull:
        if len(glass) == 4 and not isinstance(glass[0], tuple):
            out.append(R(f"{n}-mullion", glass[0], mull[0], glass[2], mull[1], like=f"{n}-frame"))
        else:
            out.append(P(f"{n}-mullion", mull, like=f"{n}-frame"))
    if sill:
        out.append(poly(f"{n}-sill", sill, hid=f"{n}-frame", k=0.95))
    if sill_front:
        out.append(poly(f"{n}-sill-front", sill_front, hid=f"{n}-frame", k=0.8))
    return out


def door(n, frame, panel, reveal=None, top=None, knob=None, side=None):
    """A door: frame, panel, the darker reveal down its right side, a shaded band at its top,
    the knob (cx, cy, rx, ry) and the frame's shaded side (all rect tuples)."""
    out = []
    if side:
        out.append(R(f"{n}-side", *side))
    out += [R(f"{n}-frame", *frame), R(f"{n}-panel", *panel)]
    if reveal:
        out.append(R(f"{n}-reveal", *reveal, hid=f"{n}-panel", k=0.75))
    if top:
        out.append(R(f"{n}-top", *top, hid=f"{n}-panel", k=0.85))
    if knob:
        out.append(E(f"{n}-knob", *knob, hid="#f2a23a"))
    return out


# ==========================================================================
# Houses, left side of the street (far to near)
# ==========================================================================
add(
    O("house-red", "houses-left", [
        # the roof's left plane runs off the image; the near tree hides its lower part
        P("roof", [(-10, 203), (312, 196), (122, 505), (130, 540), (-10, 542)], grad=True),
        P("roof-ridge-shade-1", [(35, 333), (0, 368), (-10, 378), (-10, 371), (0, 361), (31, 330)],
          hid="#3b3e63"),
        P("roof-ridge-shade-2", [(100, 400), (66, 442), (-10, 442), (-10, 447), (69, 447), (103, 404)],
          like="roof-ridge-shade-1"),
        P("skylight-top", [(100, 290), (115, 257), (199, 256), (177, 290)]),
        P("skylight-side", [(177, 290), (199, 256), (200, 268), (177, 300)]),
        P("skylight-front", [(100, 290), (177, 290), (177, 300), (100, 302)]),
        P("side-wall", [(-10, 536), (114, 536), (114, 966), (-10, 966)], hid="#a72e40"),
        P("soffit", [(312, 242), (458, 553), (432, 553), (297, 268)]),
        P("barge-right", [(312, 196), (470, 547), (460, 556), (312, 242)], grad=True),
        P("wall", [(297, 266), (433, 552), (433, 966), (112, 966), (112, 545), (128, 538)], grad=True),
        P("wall-shade", [(297, 268), (433, 553), (413, 553), (290, 282)]),
        P("barge-left", [(306, 196), (312, 196), (312, 242), (297, 266), (138, 536), (128, 540), (118, 505)],
          grad=True),
        P("trim", [(102, 536), (433, 551), (433, 584), (104, 567)], grad=True),
        P("trim-shadow", [(112, 567), (433, 584), (433, 600), (112, 584)]),
        *window("gable-window", [(257, 352), (321, 370), (324, 501), (260, 497)],
                [(272, 378), (312, 389), (314, 488), (274, 486)],
                mull=[(271, 425), (316, 431), (316, 438), (271, 432)]),
        *window("window-left", (139, 642, 197, 827), (150, 655, 186, 815), mull=(726, 734),
                sill=[(140, 825), (220, 825), (218, 837), (140, 837)],
                sill_front=[(140, 837), (218, 837), (215, 858), (137, 858)]),
        P("window-right-shadow", [(350, 818), (430, 818), (415, 842), (345, 842)]),
        *window("window-right", (354, 642, 412, 800), (365, 655, 402, 790), mull=(712, 724), reveal=394,
                sill=[(356, 790), (431, 790), (431, 801), (356, 801)],
                sill_front=[(350, 801), (431, 801), (430, 820), (350, 820)]),
        *door("door", RED_DOOR, (240, 654, 325, 946), reveal=(302, 654, 325, 946),
              top=(240, 654, 302, 682), knob=(299, 799, 8.5, 11), side=(212, 639, 226, 946)),
        # front steps: the left side face, two steps (tread, riser) down toward the street,
        # centred on the door; the mailbox hides their right ends
        *centre_on(RED_DOOR, [
            P("steps-side", [(210, 947), (281, 948), (281, 994), (304, 999), (306, 1036), (210, 1040)]),
            P("step-1-tread", plane="tread", pts=[(218, 946), (281, 948), (387, 919), (310, 921)]),
            P("step-1-riser", plane="side", pts=[(281, 948), (387, 919), (390, 958), (281, 994)]),
            P("step-2-tread", plane="tread", pts=[(281, 994), (302, 999), (429, 961), (390, 958)]),
            P("step-2-riser", plane="side", pts=[(302, 999), (429, 961), (429, 991), (306, 1036)]),
        ], ["steps-side"]),
    ]),
)

# The yellow house's front steps, foundation and path (measured on the original; the puppy
# hides their lower half, the corgi's ear the path's right end)
YELLOW_STEPS = [
    ("foundation", [(652, 848), (694, 838.5), (727.7, 829.5), (721, 872), (694, 883.5), (652, 890.5)]),
    ("landing-front", [(727.7, 828), (776, 828), (776, 872), (721, 872)]),
    ("step-1-tread", [(728, 828), (776, 828), (828, 813.3), (831, 810.5), (820, 810), (810, 809),
                      (800, 809.5), (790, 812.5), (776, 815.5), (760, 819.5), (745, 823), (729, 826)]),
    ("step-1-riser", [(776, 828), (837, 810.7), (837, 827.3), (820, 832), (800, 840), (776, 846)]),
    ("step-2-tread", [(776, 846), (800, 840), (820, 832), (840, 827), (855, 826.5),
                      (855, 830), (840, 833.5), (830, 837.5), (820, 841), (810, 843.5), (800, 847.5), (776, 856)]),
    ("step-2-riser", [(776, 856), (800, 847.5), (810, 843.5), (820, 841), (830, 837.5), (840, 833.5),
                      (855, 830), (855, 851.7), (840, 857), (800, 871), (776, 876)]),
]

_landing = [x for x, _ in YELLOW_STEPS[1][1]]
YELLOW_DX = round((YELLOW_DOOR[0] + YELLOW_DOOR[2]) / 2 - (min(_landing) + max(_landing)) / 2, 2)

add(
    O("house-teal", "houses-left", [
        P("roof", [(786, 493), (936, 483), (884, 612), (817, 621), (786, 621)], grad=True),
        P("roof-dash", [(845, 524), (849, 519), (875, 518), (871, 524)], hid="roof", k=0.85),
        P("side-wall", [(817, 616), (884, 610), (884, 822), (817, 822)], grad=True),
        P("side-wall-shadow", [(817, 616), (884, 610), (884, 628), (817, 636)]),
        P("soffit", [(932, 506), (968, 616), (963, 616), (928, 520)]),
        P("barge-right", [(938, 483), (978, 618), (967, 619), (932, 506)]),
        P("wall", [(928, 520), (963, 612), (963, 822), (882, 822), (882, 608)], grad=True),
        P("barge-left", [(936, 483), (940, 487), (928, 520), (890, 606), (879, 611)]),
        P("trim", [(880, 607), (965, 614), (965, 627), (880, 622)]),
        *window("gable-window", (917, 541, 932, 589), (920, 545, 929, 585), mull=(563, 566), glare=False),
        *window("window-left", (887, 650, 905, 729), (892, 656, 902, 722), mull=(689, 693), glare=False,
                sill=(884, 727, 908, 737)),
        *door("door", TEAL_DOOR, (913, 656, 929, 781), reveal=(925, 656, 929, 781)),
        *window("window-right", (939, 650, 955, 720), (943, 655, 952, 714), mull=(683, 687), glare=False,
                sill=(937, 718, 962, 729)),
        *centre_on(TEAL_DOOR, [
            P("steps-side", [(904, 786), (936, 787), (936, 801), (951, 802), (951, 815), (904, 815)]),
            P("step-1-tread", plane="tread", pts=[(904, 786), (930, 778), (969, 778), (936, 787)]),
            P("step-1-riser", plane="side", pts=[(936, 787), (969, 778), (970, 792), (936, 801)]),
            P("step-2-tread", plane="tread", pts=[(936, 801), (970, 792), (982, 792), (951, 802)]),
            P("step-2-riser", plane="side", pts=[(951, 802), (982, 792), (982, 807), (951, 815)]),
        ], ["steps-side"]),
    ]),
    O("house-yellow", "houses-left", [
        # the red house hides its left end and the title its gable's peak
        P("roof", [(396, 441), (453, 437), (725, 418), (683, 590), (396, 590)], grad=True),
        P("roof-dash-1", [(472, 494), (478, 485), (525, 485), (520, 494)], hid="roof", k=0.8),
        P("roof-dash-2", [(558, 550), (563, 542), (624, 542), (619, 550)], like="roof-dash-1"),
        P("fascia", [(396, 585), (698, 585), (698, 607), (396, 607)]),
        P("side-wall", [(396, 604), (694, 604), (694, 842), (396, 842)], grad=True),
        P("side-foundation", [(396, 842), (694, 842), (694, 872), (396, 872)], hid="#735258"),
        *window("side-window", (432, 650, 530, 782), (447, 662, 515, 706), glare=False),
        R("side-window-glass-2", 449, 716, 516, 760, like="side-window-glass"),
        P("soffit", [(724, 440), (730, 436), (818, 600), (806, 600)]),
        P("barge-right", [(725, 412), (836, 600), (820, 604), (730, 436)], grad=True),
        P("wall", [(724, 440), (806, 598), (820, 610), (820, 842), (692, 842), (692, 588), (696, 588)],
          grad=True),
        P("barge-left", [(725, 412), (730, 436), (724, 440), (697, 589), (682, 591)], grad=True),
        P("trim", [(694, 583), (820, 594), (820, 612), (694, 605)]),
        *window("gable-window", (735, 509, 766, 574), (744, 513, 758, 566), mull=(538, 542), glare=False),
        *door("door", YELLOW_DOOR, (745, 655, 780, 828), reveal=(772, 655, 780, 828),
              knob=(751, 726, 4, 5)),
        *window("window", (792, 657, 822, 740), (796, 660, 809, 735), mull=(692, 697), glare=False,
                sill=(790, 738, 824, 745)),
        # the steps centred on the door; the foundation's right end follows the landing
        moved(P("foundation", YELLOW_STEPS[0][1]), lambda x, y: (round(x + YELLOW_DX, 2) if x > 720 else x, y)),
        *shift([P(n, pts, **({"plane": "side"} if "riser" in n else {"plane": "tread"} if "tread" in n else {}))
                for n, pts in YELLOW_STEPS[1:]], YELLOW_DX),
    ]),
)


# ==========================================================================
# Houses, right side of the street (far to near)
# ==========================================================================
add(
    O("house-pink", "houses-right", [
        # the title hides the gable's peak, the blue house the roof's right end
        P("roof", [(2031, 474), (2200, 488), (2200, 611), (2084, 611)], grad=True),
        P("roof-skylight", [(2101, 578), (2106, 562), (2131, 562), (2136, 578)]),
        P("eave", [(2084, 608), (2200, 608), (2200, 624), (2088, 624)]),
        P("side-wall", [(2086, 612), (2200, 612), (2200, 800), (2086, 800)], grad=True),
        P("barge-right", [(2031, 474), (2088, 608), (2079, 610), (2032, 493)]),
        P("wall", [(2040, 512), (2079, 606), (2088, 612), (2088, 790), (2003, 790), (2003, 612), (2005, 606)],
          grad=True),
        P("wall-shade", [(2040, 512), (2005, 606), (2015, 606), (2044, 518)]),
        P("barge-left", [(2031, 474), (2032, 493), (1996, 610), (1987, 612)]),
        P("soffit-left", [(2032, 493), (2040, 512), (2005, 606), (1996, 610)]),
        P("trim", [(2001, 608), (2088, 603), (2088, 616), (2003, 622)]),
        *window("gable-window", (2035, 540, 2050, 587), (2040, 546, 2046, 582), mull=(560, 565), glare=False),
        *window("window-left", (2010, 651, 2026, 714), (2015, 655, 2022, 707), mull=(680, 684), glare=False,
                sill=(2005, 714, 2029, 726)),
        *door("door", PINK_DOOR, (2041, 657, 2055, 790)),
        *window("window-right", (2064, 651, 2081, 728), (2069, 656, 2077, 722), mull=(687, 691), glare=False,
                sill=(2060, 724, 2085, 735)),
        *centre_on(PINK_DOOR, [
            P("steps-tread", [(1993, 777), (2050, 777), (2050, 785), (1993, 785)]),
            P("steps-front", [(1993, 785), (2050, 785), (2050, 800), (1993, 800)]),
        ], ["steps-tread", "steps-front"]),
    ]),
    O("house-blue", "houses-right", [
        # the green house hides the roof's right end and the side wall's far corner
        P("roof", [(2247, 382), (2560, 382), (2560, 592), (2330, 592)], grad=True),
        P("roof-dash-1", [(2301, 417), (2306, 410), (2367, 410), (2362, 417)], hid="roof", k=0.85),
        P("roof-dash-2", [(2326, 476), (2331, 469), (2388, 469), (2383, 476)], like="roof-dash-1"),
        P("roof-dash-3", [(2428, 502), (2433, 495), (2492, 495), (2487, 502)], like="roof-dash-1"),
        P("roof-dash-4", [(2350, 521), (2410, 521), (2421, 543), (2361, 543)], like="roof-dash-1"),
        P("eave", [(2338, 590), (2560, 590), (2560, 610), (2341, 610)]),
        P("chimney-side", plane="side", pts=[(2439, 336), (2466, 336), (2466, 452), (2439, 402)]),
        P("chimney-front", [(2465, 336), (2538, 336), (2538, 452), (2465, 452)]),
        P("chimney-cap-top", [(2429, 303), (2545, 303), (2545, 322), (2429, 322)]),
        P("chimney-cap-front", [(2429, 322), (2545, 322), (2545, 338), (2429, 338)]),
        P("side-wall", [(2345, 606), (2560, 606), (2560, 800), (2345, 800)], grad=True),
        *window("side-window", (2455, 650, 2530, 771), (2467, 661, 2530, 761), mull=(704, 714), glare=False),
        P("soffit", [(2246, 411), (2256, 433), (2201, 595), (2185, 597)]),
        P("barge-right", [(2247, 382), (2340, 600), (2333, 604), (2246, 411)]),
        P("wall", [(2248, 416), (2337, 600), (2347, 604), (2347, 830), (2190, 830), (2190, 600), (2201, 595), (2256, 433)],
          grad=True),
        P("wall-shade", [(2256, 433), (2201, 595), (2215, 595), (2260, 445)]),
        P("barge-left", [(2247, 382), (2246, 411), (2185, 597), (2173, 600)]),
        P("trim", [(2190, 594), (2336, 582), (2340, 602), (2192, 613)]),
        P("trim-shadow", [(2192, 612), (2340, 601), (2340, 610), (2192, 620)]),
        *window("gable-window", [(2245, 483), (2274, 478), (2274, 560), (2246, 565)],
                [(2252, 489), (2268, 486), (2268, 555), (2252, 558)],
                mull=[(2252, 513), (2268, 511), (2268, 518), (2252, 520)], glare=False),
        *window("window-left", (2195, 649, 2223, 745), (2204, 658, 2216, 738), mull=(693, 700), glare=False,
                sill=(2190, 743, 2227, 755)),
        *door("door", BLUE_DOOR, (2241, 655, 2273, 830), reveal=(2266, 655, 2273, 830)),
        *window("window-right", (2292, 647, 2329, 759), (2302, 658, 2318, 748), mull=(698, 707), glare=False,
                sill=(2287, 757, 2333, 769)),
        *centre_on(BLUE_DOOR, [
            P("steps-side", [(2226, 839), (2262, 832), (2267, 834), (2267, 885), (2212, 885), (2212, 862),
                             (2226, 859)]),
            P("step-1-tread", plane="tread", pts=[(2175, 824), (2210, 822), (2262, 832), (2226, 839), (2175, 828)]),
            P("step-1-riser", plane="side", pts=[(2175, 828), (2226, 839), (2226, 859), (2175, 846)]),
            P("step-2-tread", plane="tread", pts=[(2159, 845), (2175, 844), (2226, 859), (2212, 862), (2159, 848)]),
            P("step-2-riser", plane="side", pts=[(2159, 848), (2212, 862), (2212, 884), (2159, 866)]),
        ], ["steps-side"]),
    ]),
    O("house-green", "houses-right", [
        # the roof and walls run off the image on the right
        P("roof", [(2628, 202), (2826, 203), (2826, 516)], grad=True),
        P("roof-shade-1", [(2738, 296), (2826, 308), (2826, 326), (2752, 322)]),
        P("roof-shade-2", [(2795, 390), (2826, 394), (2826, 420), (2805, 418)], like="roof-shade-1"),
        P("barge-right", [(2628, 202), (2826, 515), (2826, 566), (2626, 249)], grad=True),
        P("soffit", [(2626, 249), (2642, 274), (2509, 560), (2484, 560)]),
        P("wall-shade", [(2642, 274), (2655, 295), (2530, 560), (2509, 560)]),
        P("wall", [(2655, 295), (2826, 562), (2826, 975), (2505, 975), (2505, 560), (2530, 560)], grad=True),
        P("barge-left", [(2628, 202), (2626, 249), (2476, 578), (2465, 560)], grad=True),
        P("trim", [(2502, 561), (2826, 532), (2826, 568), (2505, 593)], grad=True),
        P("trim-shadow", [(2505, 592), (2826, 568), (2826, 582), (2505, 604)]),
        *window("gable-window", [(2616, 366), (2679, 360), (2680, 500), (2614, 505)],
                [(2628, 382), (2670, 378), (2670, 488), (2628, 491)],
                mull=[(2628, 427), (2670, 424), (2670, 440), (2628, 443)]),
        P("gable-window-side", [(2672, 361), (2679, 360), (2680, 500), (2672, 501)], hid="#a07e64"),
        *window("window-left", (2523, 647, 2586, 804), (2533, 662, 2571, 794), mull=(719, 731), reveal=None,
                sill=(2497, 797, 2588, 806), sill_front=(2497, 806, 2588, 827)),
        P("window-left-shadow", [(2522, 827), (2592, 827), (2592, 848), (2530, 840)]),
        R("window-left-inner", 2533, 662, 2540, 794, hid="#a98674"),
        R("window-left-side", 2581, 647, 2586, 804, hid="window-left-inner"),
        P("door-shadow", [(2712, 641), (2727, 641), (2727, 950), (2712, 950)]),
        *door("door", GREEN_DOOR, (2630, 657, 2696, 950), top=(2643, 657, 2696, 688),
              knob=(2639, 808, 9, 12), side=(2706, 641, 2721, 950)),
        R("door-reveal-left", 2612, 657, 2630, 950),
        R("door-panel-shade", 2630, 657, 2643, 950),
        *window("window-right", (2740, 647, 2830, 835), (2753, 664, 2830, 822), mull=(732, 746),
                sill=(2717, 827, 2830, 837), sill_front=(2717, 837, 2830, 866)),
        R("window-right-inner", 2753, 664, 2765, 822, hid="window-left-inner"),
        P("window-right-shadow", [(2740, 866), (2830, 866), (2830, 882), (2745, 882)]),
        *centre_on(GREEN_DOOR, [
            P("steps-side", [(2658, 962), (2698, 950), (2698, 1047), (2632, 1047), (2632, 1008), (2658, 1003)]),
            P("step-1-tread", plane="tread", pts=[(2550, 929), (2610, 929), (2698, 950), (2658, 962), (2550, 932)]),
            P("step-1-riser", plane="side", pts=[(2550, 932), (2658, 962), (2658, 1003), (2550, 967)]),
            P("step-2-tread", plane="tread", pts=[(2505, 968), (2550, 967), (2658, 1003), (2632, 1008), (2505, 972)]),
            P("step-2-riser", plane="side", pts=[(2505, 972), (2632, 1008), (2632, 1047), (2505, 1000)]),
        ], ["steps-side"]),
    ]),
)


# ==========================================================================
# Far houses (small, in the haze). Each is authored face by face from the original (zoomed
# crops with a pixel grid): roof plane with its eave band and darker shingle streaks, the
# barge boards, the gable front with its window, the cream trim, the front wall with its
# windows and door, the side wall, the steps. Each surface takes the original's tones:
# its colour is measured at a clean spot of that surface (`at`), its shaded parts are the
# same colour darkened. A side wall that is hidden or broken up by trees is one flat
# colour, measured where it shows (or, hidden entirely, a shade of the front wall).
# ==========================================================================
FAR_FRAME = 1.5       # a far window's frame width
FAR_SHADE = 0.86      # an eave band / shaded strip: the surface's colour times this
FAR_STREAK = 0.94     # a roof's darker shingle streak


def fwin(n, x0, y0, x1, y1, mull=None, vmull=None, arch=False, frame=FAR_FRAME, glass_at=None):
    """A far window: cream frame (the house's trim colour), glass, an optional mullion across
    (y0, y1) or down (x0, x1), an arched top when arch is set."""
    fx0, fy0, fx1, fy1 = x0 - frame, y0 - frame, x1 + frame, y1 + frame
    if arch:
        r, fr = (x1 - x0) / 2, (fx1 - fx0) / 2
        out = [A(f"{n}-frame", [(fx0, fy1), (fx0, fy0 + fr), ("arc", fr, fx1, fy0 + fr), (fx1, fy1)],
                 like="trim"),
               A(f"{n}-glass", [(x0, y1), (x0, y0 + r), ("arc", r, x1, y0 + r), (x1, y1)],
                 **({"at": glass_at} if glass_at else {}), hid="#8cb8d4")]
    else:
        out = [R(f"{n}-frame", fx0, fy0, fx1, fy1, like="trim"),
               R(f"{n}-glass", x0, y0, x1, y1, **({"at": glass_at} if glass_at else {}), hid="#8cb8d4")]
    if mull:
        out.append(R(f"{n}-mullion", x0, mull[0], x1, mull[1], like="trim"))
    if vmull:
        out.append(R(f"{n}-mullion-v", vmull[0], y0 + (x1 - x0) / 2 if arch else y0, vmull[1], y1,
                     like="trim"))
    return out


def fdoor(n, frame, panel, shade_x=None):
    """A far door: cream surround, panel (measured), its darker right part from shade_x."""
    out = [R(f"{n}-frame", *frame, like="trim"), R(f"{n}-panel", *panel, hid="#c4664a")]
    if shade_x is not None:
        out.append(R(f"{n}-shade", shade_x, panel[1], panel[2], panel[3], like=f"{n}-panel", k=FAR_SHADE))
    return out


def fsteps(x0, y0, x1, y1, y_mid, at=None, like=None):
    """Two grey steps seen from the front: the lit tread, the darker riser below."""
    return [R("steps", x0, y0, x1, y_mid, **({"at": at} if at else {"like": like})),
            R("steps-riser", x0, y_mid, x1, y1, like="steps", k=FAR_SHADE)]


def door_steps(frame, width, y0, y1, n, **colour):
    """n grey steps seen from the front, width px wide and centred on the door frame, from the
    door's sill (y0) down to the ground (y1): each a lit tread above its darker riser, the first
    named like fsteps' (steps, steps-riser), the others steps-2, steps-2-riser, ..."""
    r2 = lambda v: round(v, 2)
    cx = (frame[0] + frame[2]) / 2
    x0, x1, hs = r2(cx - width / 2), r2(cx + width / 2), (y1 - y0) / n
    add_ = {"added": True} if colour.get("added") else {}
    out = []
    for i in range(n):
        a, m, b = r2(y0 + i * hs), r2(y0 + (i + 0.5) * hs), r2(y0 + (i + 1) * hs)
        nm = "steps" if i == 0 else f"steps-{i + 1}"
        out += [R(nm, x0, a, x1, m, **(colour if i == 0 else {"like": "steps", **add_})),
                R(f"{nm}-riser", x0, m, x1, b, like="steps", k=FAR_SHADE, **add_)]
    return out


FAR_SALMON_DOOR = (1131, 660, 1143, 730)
FAR_ROSE_DOOR = (1261.7, 653, 1270.5, 699)
FAR_BLUE_DOOR = (1671.5, 650, 1686, 700)
FAR_PINK_DOOR = (1739, 651, 1748.5, 700)
FAR_YELLOW_DOOR = (1884, 653, 1899.5, 726)
FAR_BLUE_STEPS_W = 44        # 3 doors wide: the original's (56) reached the sidewalk
FAR_YELLOW_STEPS_W = 37      # 2.4 doors wide, like the salmon house's (the narrowest far steps)

add(
    O("house-far-salmon", "houses-far", [
        R("chimney", 975, 528, 1016, 575, at=[(980, 535), (1003, 535), (1003, 548), (980, 548)]),
        R("chimney-side", 1006, 528, 1016, 575, plane="side", at=[(1008, 535), (1014, 535), (1014, 548), (1008, 548)]),
        R("chimney-cap", 970, 517, 1020, 531, at=[(976, 519), (1014, 519), (1014, 526), (976, 526)]),
        R("chimney-cap-shade", 972, 527, 1018, 531, like="chimney-cap", k=FAR_SHADE),
        P("side-wall", [(958, 628), (1126, 628), (1126, 748), (958, 748)],
          at=[(965, 640), (995, 640), (995, 700), (965, 700)]),
        P("roof", [(1141, 546), (958, 550), (958, 621), (1130, 621)],
          at=[(1035, 556), (1095, 556), (1095, 571), (1035, 571)]),
        P("roof-streak-1", [(1012, 565), (1034, 565), (1030, 568), (1016, 568)], like="roof", k=FAR_STREAK),
        P("roof-streak-2", [(986, 591), (1016, 591), (1012, 594), (990, 594)], like="roof", k=FAR_STREAK),
        R("eave", 958, 617, 1126, 631, at=[(964, 620), (995, 620), (995, 628), (964, 628)]),
        P("gable", [(1140, 550), (1160, 622), (1160, 748), (1124, 748), (1124, 602)],
          at=[(1141, 602), (1147, 602), (1147, 620), (1141, 620)]),
        R("front-wall", 1124, 636, 1157, 748, at=[(1145, 700), (1154, 700), (1154, 722), (1145, 722)]),
        R("bay", 1097, 698, 1131, 735, at=[(1114, 701), (1128, 701), (1128, 712), (1114, 712)]),
        R("trim", 1122, 626, 1158, 637, at=[(1138, 628), (1154, 628), (1154, 635), (1138, 635)]),
        P("barge-left", [(1140, 546), (1142, 551), (1127, 604), (1121, 604)], like="barge-right"),
        P("barge-right", [(1139, 546), (1145, 546), (1167, 633), (1159, 633)],
          at=[(1151, 591), (1154, 591), (1154, 599), (1151, 599)]),
        *fwin("gable-window", 1134, 587, 1138.5, 611, mull=(594.5, 596.5), arch=True),
        *fwin("window", 1145.5, 656, 1148.5, 694, mull=(674.5, 677)),
        *fdoor("door", FAR_SALMON_DOOR, (1134, 664, 1140.5, 730)),
        *centre_on(FAR_SALMON_DOOR, fsteps(1143, 727, 1172, 741, 734,
                                           at=[(1150, 729), (1165, 729), (1165, 733), (1150, 733)]), FAR_STEPS),
    ]),
    O("house-far-rose", "houses-far", [
        # the side wall runs left behind the trees; it shows only beside the front's lower part
        P("side-wall", [(1205, 640), (1252, 640), (1252, 700), (1205, 700)],
          at=[(1236, 690), (1246, 690), (1246, 698), (1236, 698)]),
        P("roof", [(1270, 584), (1205, 586), (1205, 642), (1256, 642)],
          at=[(1228, 588), (1258, 588), (1258, 600), (1228, 600)]),
        P("roof-streak", [(1232, 607), (1250, 607), (1247, 611), (1235, 611)], like="roof", k=FAR_STREAK),
        R("eave", 1205, 636, 1252, 643, like="roof", k=FAR_SHADE),
        P("gable", [(1270, 587), (1284, 640), (1252, 640), (1252, 632)],
          at=[(1272, 618), (1277, 618), (1277, 632), (1272, 632)]),
        R("front-wall", 1240, 643, 1283, 700, at=[(1272, 684), (1280, 684), (1280, 696), (1272, 696)]),
        R("trim", 1250, 635, 1284, 644, at=[(1258, 637), (1278, 637), (1278, 641), (1258, 641)]),
        P("barge-left", [(1270, 584), (1268, 591), (1251, 641), (1248, 641)], like="barge-right"),
        P("barge-right", [(1270, 584), (1273, 584), (1290, 643), (1283, 643)],
          at=[(1279, 615), (1281, 615), (1281, 622), (1279, 622)]),
        *fwin("gable-window", 1265, 612, 1270, 627, arch=True),
        *fwin("window-1", 1245, 655, 1249, 683),
        *fwin("window-2", 1257, 655, 1260.5, 698),
        *fdoor("door", FAR_ROSE_DOOR, (1263.3, 655, 1269, 699)),
        *fwin("window-3", 1275.5, 655, 1278.5, 680),
        *centre_on(FAR_ROSE_DOOR, fsteps(1248, 698, 1293, 708, 703,
                                         at=[(1262, 699), (1285, 699), (1285, 702), (1262, 702)]), FAR_STEPS),
    ]),
    O("house-far-blue", "houses-far", [
        P("side-wall", [(1700, 638), (1748, 638), (1748, 700), (1700, 700)],
          at=[(1706, 642), (1712, 642), (1712, 654), (1706, 654)]),
        # the roof's far end is hipped: the tree behind shows over the side wall
        P("roof", [(1672, 579), (1698, 579), (1713, 640), (1700, 640)],
          at=[(1676, 582), (1695, 582), (1695, 590), (1676, 590)]),
        P("roof-streak", [(1684, 600), (1696, 600), (1694, 604), (1686, 604)], like="roof", k=FAR_STREAK),
        R("eave", 1706, 638, 1748, 643, like="side-wall", k=FAR_SHADE),
        R("corner", 1649, 638, 1658, 700, at=[(1651, 660), (1656, 660), (1656, 690), (1651, 690)]),
        P("gable", [(1673, 583), (1700, 638), (1656, 638)],
          at=[(1665, 626), (1690, 626), (1690, 634), (1665, 634)]),
        R("front-wall", 1658, 640, 1706, 700, at=[(1694, 650), (1700, 650), (1700, 676), (1694, 676)]),
        R("trim", 1656, 635.5, 1704, 640.5, at=[(1665, 637), (1690, 637), (1690, 639), (1665, 639)]),
        P("barge-left", [(1670, 579), (1675, 584), (1657, 643), (1650, 643)],
          at=[(1660, 614), (1664, 614), (1664, 622), (1660, 622)]),
        *fwin("gable-window", 1675, 603, 1680, 621, arch=True,
              glass_at=[(1676, 613), (1679, 613), (1679, 620), (1676, 620)]),
        A("gable-window-glow", [(1675, 611), (1675, 605.5), ("arc", 2.5, 1680, 605.5), (1680, 611)],
          at=[(1676, 606), (1679, 606), (1679, 610), (1676, 610)]),
        *fwin("window-1", 1665.5, 653, 1669, 676),
        *fwin("window-2", 1687, 653, 1691, 676),
        *fdoor("door", FAR_BLUE_DOOR, (1673.5, 652, 1684.5, 700), shade_x=1679),
        # the steps, centred on the door, 3 doors wide (the original's ran on to the sidewalk)
        *[dict(f, src=o) for f, o in zip(
            door_steps(FAR_BLUE_DOOR, FAR_BLUE_STEPS_W, 698, 710, 1,
                       at=[(1656, 701), (1670, 701), (1670, 703), (1656, 703)]),
            fsteps(1652, 698, 1708, 710, 704, at=[(1656, 701), (1670, 701), (1670, 703), (1656, 703)]))],
    ]),
    O("house-far-pink", "houses-far", [
        # the side wall runs right under the trees; its one window shows beside the big tree's trunk
        P("side-wall", [(1753, 632), (1850, 632), (1850, 722), (1753, 722)],
          at=[(1755, 700), (1775, 700), (1775, 712), (1755, 712)]),
        *fwin("side-window", 1838.5, 657, 1848.5, 677, frame=1,
              glass_at=[(1841, 664), (1846, 664), (1846, 674), (1841, 674)]),
        P("roof", [(1741, 565), (1850, 567), (1850, 632), (1757, 632)],
          at=[(1752, 572), (1785, 572), (1785, 590), (1752, 590)]),
        P("roof-streak", [(1764, 596), (1782, 596), (1779, 600), (1767, 600)], like="roof", k=FAR_STREAK),
        R("eave", 1757, 626, 1850, 634, like="roof", k=FAR_SHADE),
        P("gable", [(1742, 569), (1758, 632), (1729, 632)],
          at=[(1735, 619), (1752, 619), (1752, 627), (1735, 627)]),
        R("front-wall", 1730, 636, 1753, 722, at=[(1732, 639), (1751, 639), (1751, 648), (1732, 648)]),
        R("trim", 1729, 628.5, 1754, 637, at=[(1733, 630), (1750, 630), (1750, 635), (1733, 635)]),
        P("barge-left", [(1740, 565), (1743, 571), (1730, 632), (1723, 636)],
          at=[(1731, 606), (1733.5, 606), (1733.5, 614), (1731, 614)]),
        *fwin("gable-window", 1743.5, 598, 1747, 617, arch=True),
        *fwin("window", 1734.5, 651.5, 1737, 681),
        *fdoor("door", FAR_PINK_DOOR, (1740.5, 652.5, 1746, 700)),
        # the steps (grey like the rose house's) run from the door's sill down to the ground: two
        # steps, as wide as the original's single one, centred on the door
        *[dict(f, src=o) if o else f for f, o in zip(
            door_steps(FAR_PINK_DOOR, 34, 700, 728, 2, like="house-far-rose/steps"),
            fsteps(1726, 718, 1760, 728, 723, like="house-far-rose/steps") + [None, None])],
    ]),
    O("house-far-yellow", "houses-far", [
        R("chimney", 1961, 500, 2001, 560, at=[(1975, 510), (1995, 510), (1995, 540), (1975, 540)]),
        R("chimney-lit", 1961, 500, 1968, 560, plane="side", at=[(1962, 510), (1966, 510), (1966, 540), (1962, 540)]),
        R("chimney-cap", 1957, 488, 2004, 502, hid="#a8a6b4"),
        R("chimney-cap-shade", 1959, 499, 2002, 502, like="chimney-cap", k=FAR_SHADE),
        R("side-wall", 1912, 628, 2012, 770, at=[(1990, 640), (2003, 640), (2003, 683), (1990, 683)]),
        P("roof", [(1887, 527), (2012, 530), (2012, 620), (1915, 620)],
          at=[(1905, 535), (1950, 535), (1950, 548), (1905, 548)]),
        P("roof-streak", [(1930, 551), (1958, 551), (1956, 557), (1932, 557)], like="roof", k=FAR_STREAK),
        R("eave", 1912, 617, 2012, 630, at=[(1988, 618), (2003, 618), (2003, 626), (1988, 626)]),
        P("gable", [(1888, 537), (1913, 620), (1913, 632), (1868, 632), (1868, 620)],
          at=[(1878, 605), (1885, 605), (1885, 618), (1878, 618)]),
        R("front-wall", 1869, 632, 1912, 762, at=[(1871, 636), (1908, 636), (1908, 650), (1871, 650)]),
        R("porch", 1869, 700, 1912, 762, at=[(1896, 708), (1908, 708), (1908, 722), (1896, 722)]),
        R("trim", 1866, 620, 1913, 632, at=[(1872, 622), (1905, 622), (1905, 630), (1872, 630)]),
        # the left barge: a tan edge, the dark board, a lit strip on its underside
        P("barge-left-edge", [(1881, 526), (1886, 526), (1863, 623), (1855, 623)], like="barge-right"),
        P("barge-left", [(1886, 526), (1888, 536), (1869, 622), (1862, 622)],
          at=[(1868, 598), (1872, 598), (1872, 604), (1868, 604)]),
        P("barge-left-soffit", [(1888, 536), (1893, 541), (1876, 622), (1869, 622)],
          at=[(1877, 598), (1879, 598), (1879, 604), (1877, 604)]),
        P("barge-right", [(1886, 527), (1893, 530), (1919, 619), (1912, 619)],
          at=[(1902, 573), (1905, 573), (1905, 581), (1902, 581)]),
        *fwin("gable-window", 1892, 572, 1897.5, 600, mull=(584, 586), arch=True),
        *fwin("window", 1875, 656, 1879, 695, mull=(675, 678)),
        *fdoor("door", FAR_YELLOW_DOOR, (1886, 656, 1896, 726), shade_x=1892),
        # added: the door stands above the porch's foot, so three steps lead down to the ground
        # (the original shows none), grey like the rose house's, centred on the door
        *door_steps(FAR_YELLOW_DOOR, FAR_YELLOW_STEPS_W, 726, 762, 3, like="house-far-rose/steps", added=True),
    ]),
)


# ==========================================================================
# House perspective (#841; README "House perspective invariants"). The scene is one-point
# perspective: every wall is a front face (parallel to the picture plane: its horizontals are
# horizontal) or a side face (receding: its horizontals lie on lines through the street's
# vanishing point); steps' treads are horizontal surfaces, their edges one or the other; sills'
# and flower boxes' tops and ends, and the frames' depth faces, are oblique (one oblique per house,
# README win-7). The measured shapes above stay as measured; this pass redraws the faces the
# original paints at stray angles, each still measured where the original shows it (src), so no colour changes.
# build_env.check_perspective() checks every edge of every house.
# ==========================================================================
VP = {"l": meet(LAWN_L, ROAD_L), "r": meet(ROAD_R, LAWN_R)}


def toward(vp, p, k):
    """Point p scaled by k about the vanishing point: k < 1 is farther away, k > 1 nearer."""
    return (round(vp[0] + (p[0] - vp[0]) * k, 2), round(vp[1] + (p[1] - vp[1]) * k, 2))


def xon(p, q, y):
    """x where the line through p and q is at height y."""
    return round(p[0] + (q[0] - p[0]) * (y - p[1]) / (q[1] - p[1]), 2)


def redrawn(f, pts, **kw):
    """Face f with new corners; still measured where the original shows it (src)."""
    g = dict(f, pts=[(float(x), float(y)) for x, y in pts], **kw)
    if not f.get("added"):
        g["src"] = f.get("src", f)
    return g


def rect(x0, y0, x1, y1):
    return [(x0, y0), (x1, y0), (x1, y1), (x0, y1)]


def _house(oid):
    return next(o for o in OBJECTS if o["id"] == oid)


def _face(oid, name):
    return next(f for f in _house(oid)["faces"] if f["name"] == name)


def refit(oid, swap):
    """Redraw a house's faces: swap maps a face's name to its new corners, or to the faces
    that take its place (an empty list drops it)."""
    o = _house(oid)
    out = []
    for f in o["faces"]:
        r = swap.get(f["name"], f)
        if r is f:
            out.append(f)
        elif r and isinstance(r[0], dict):
            out += r
        elif r:
            out.append(redrawn(f, r))
    names = [f["name"] for f in out]
    assert len(set(names)) == len(names), f"{oid}: duplicate faces after refit"
    assert not set(swap) - {f["name"] for f in o["faces"]}, f"{oid}: refit names unknown faces"
    o["faces"] = out


def persp_steps(oid, side, door, xa, x1, x2, y0, y1, y2, k, was=None):
    """A near house's two front steps, descending sideways toward the street, in perspective.
    The steps' end face toward the viewer (steps-side, a front face) is a stepped profile from
    the wall-side end xa to the street-side risers at x1 and x2, the treads' tops at y0 and y1
    and the foot at y2, centred under the door (street-1); the treads and risers recede from it
    toward the vanishing point to the steps' back, k times as far from it (k < 1). was: the face
    each one replaces (default its own name), still measured where the original shows it.
    Returns the swap for refit()."""
    vp = VP[side]
    dx = (door[0] + door[2]) / 2 - (xa + x2) / 2
    xa, x1, x2 = (round(v + dx, 2) for v in (xa, x1, x2))
    B = lambda x, y: toward(vp, (x, y), k)
    geo = {
        "steps-side": ([(xa, y0), (x1, y0), (x1, y1), (x2, y1), (x2, y2), (xa, y2)], "front"),
        "step-1-tread": ([(xa, y0), (x1, y0), B(x1, y0), B(xa, y0)], "tread"),
        "step-1-riser": ([(x1, y0), B(x1, y0), B(x1, y1), (x1, y1)], "side"),
        "step-2-tread": ([(x1, y1), (x2, y1), B(x2, y1), B(x1, y1)], "tread"),
        "step-2-riser": ([(x2, y1), B(x2, y1), B(x2, y2), (x2, y2)], "side"),
    }
    was = was or {}
    out = {}
    for n, (pts, pl) in geo.items():
        g = redrawn(_face(oid, was.get(n, n)), pts, plane=pl, name=n)
        g["src"] = dict(g["src"], name=n)          # measured as the face it replaces, under its new name
        out[was.get(n, n)] = [g]
    return out


# Each house's depth direction (README: "Each house's depth direction", persp-6, win-4): which way
# a feature standing out of the wall moves on screen as it comes nearer, +1 right, -1 left,
# measured where its frames and door surround show their depth (FRAME_DEPTH) and on the original.
# Every protruding feature on the house shows its depth along that one oblique,
# (HOUSE_DEPTH * OFFSET_X, OFFSET_Y) per door width it stands out. The far houses show none and
# carry no sill, so they have no entry.
HOUSE_DEPTH = {"house-red": 1, "house-yellow": 1, "house-teal": 1,
               "house-pink": -1, "house-blue": -1, "house-green": -1}

# The faces that show a frame's or door surround's depth, per house: (face, kind, of). kind "outer":
# the frame's own side face, beside its front (it shows on the side away from the depth direction);
# "reveal": the opening's inner side next to the glass or panel (on the side toward it). of: the
# face it belongs to, whose centre says which side it is on.
FRAME_DEPTH = {
    "house-red": [("door-side", "outer", "door-frame"), ("door-reveal", "reveal", "door-panel"),
                  ("window-right-reveal", "reveal", "window-right-glass")],
    "house-yellow": [("door-reveal", "reveal", "door-panel")],
    "house-teal": [("door-reveal", "reveal", "door-panel")],
    "house-pink": [],
    "house-blue": [("door-reveal", "reveal", "door-panel")],
    "house-green": [("door-side", "outer", "door-frame"), ("window-left-side", "outer", "window-left-frame"),
                    ("gable-window-side", "outer", "gable-window-frame"),
                    ("door-reveal-left", "reveal", "door-panel"),
                    ("window-left-inner", "reveal", "window-left-glass"),
                    ("window-right-inner", "reveal", "window-right-glass")],
}


def sill_box(oid, side, door, n, cap=None, box=None, shadow=None):
    """A window's sill (or flower box) as a solid attached to its window (README: "Window sills and
    flower boxes", win-1..win-5): its back top edge on the window frame's bottom, centred on the
    window with OVERHANG_K door widths' overhang each side; its front the back moved along its
    house's one oblique (HOUSE_DEPTH * OFFSET_X sideways and OFFSET_Y down, door widths, for a box;
    SILL_OFFSET of it for a sill), so it shows a thin top and its end on the same side as the
    house's frames show theirs (win-7). `side` is the street side (unused: the house's frames, not
    the vanishing point, set the direction). cap: a plain sill's band height (px, as measured). box: a flower box, given as the
    rectangle (x0, y0, x1, y1) where the original shows its front plainly lit; its colour is
    measured there and its band, top and end take it (win-5); its band and box heights are
    BOX_CAP_K and BOX_H_K door widths. shadow: the face of the shadow the box casts on the wall,
    moved with the box's front and drawn under it. Returns the swap for refit()."""
    dw = door[2] - door[0]
    fr = _face(oid, f"{n}-frame")["pts"]
    fx0, fx1 = min(x for x, _ in fr), max(x for x, _ in fr)
    hang = OVERHANG_K * dw
    x0, x1 = round(fx0 - hang, 2), round(fx1 + hang, 2)
    y0 = max(y for _, y in fr)
    k = 1.0 if box else SILL_OFFSET
    dx = round(HOUSE_DEPTH[oid] * OFFSET_X * k * dw, 2)
    dy = round(OFFSET_Y * k * dw, 2)
    y1 = round(y0 + (BOX_CAP_K * dw if box else cap), 2)
    y2 = round(y1 + BOX_H_K * dw, 2) if box else y1
    F = lambda x, y: (round(x + dx, 2), round(y + dy, 2))
    xe = x0 if dx > 0 else x1                       # the end the frames show their side faces on
    ob = dict(plane="oblique", recede=(dx, dy), added=True)
    out = []
    if shadow:
        old = _face(oid, f"{n}-sill-front")["pts"]
        ox = (min(x for x, _ in old) + max(x for x, _ in old)) / 2
        sdx, sdy = (x0 + x1) / 2 + dx - ox, y2 + dy - max(y for _, y in old)
        sh = _face(oid, shadow)
        out.append(redrawn(sh, [(round(x + sdx, 2), round(y + sdy, 2)) for x, y in sh["pts"]]))
    face = f"{n}-sill-front" if box else f"{n}-sill"      # the front whose colour the others shade
    out.append(P(f"{n}-sill-top", [(x0, y0), (x1, y0), F(x1, y0), F(x0, y0)], like=face, k=BOX_TOP_K, **ob))
    out.append(P(f"{n}-sill-end", [(xe, y0), F(xe, y0), F(xe, y1), (xe, y1)], like=face, k=BOX_END_K, **ob))
    if box:
        out.append(P(f"{n}-sill-front-end", [(xe, y1), F(xe, y1), F(xe, y2), (xe, y2)],
                     like=f"{n}-sill-front", k=BOX_END_K, **ob))
    band = dict(like=face, k=1.0) if box else {}
    out.append(redrawn(_face(oid, f"{n}-sill"), [F(x0, y0), F(x1, y0), F(x1, y1), F(x0, y1)], **band))
    swap = {f"{n}-sill": out}
    if box:
        swap[f"{n}-sill-front"] = [redrawn(_face(oid, f"{n}-sill-front"),
                                           [F(x0, y1), F(x1, y1), F(x1, y2), F(x0, y2)],
                                           at=rect(*box), k=1.0)]
    if shadow:
        swap[shadow] = []
    return swap


def frame_depths(oid, moved_to=None):
    """The faces that show a house's frames' and door surround's depth (FRAME_DEPTH), redrawn along
    its one oblique (win-7): each an upright strip whose depth edge (an outer side face's top, a
    reveal's bottom; README "Each house's depth direction") runs from its back to its front by
    (HOUSE_DEPTH * w, w * OFFSET_Y / OFFSET_X) for its width w, the back edge higher. moved_to: a
    face's new rectangle (x0, y0, x1, y1), for one on the wrong side. Returns the swap for refit()."""
    sx, s = HOUSE_DEPTH[oid], OFFSET_Y / OFFSET_X
    out = {}
    for name, kind, _ in FRAME_DEPTH[oid]:
        f = _face(oid, name)
        x0, y0, x1, y1 = (moved_to or {}).get(name) or (min(x for x, _ in f["pts"]), min(y for _, y in f["pts"]),
                                                         max(x for x, _ in f["pts"]), max(y for _, y in f["pts"]))
        w = x1 - x0
        r = round(w * s, 2)
        if kind == "outer":      # the back edge away from the frame, toward the side face's side
            pts = [(x0, y0 - r), (x1, y0), (x1, y1), (x0, y1 - r)] if sx > 0 else \
                  [(x0, y0), (x1, y0 - r), (x1, y1 - r), (x0, y1)]
        else:                    # the back edge by the glass or panel; the top is the opening's edge
            pts = [(x0, y0), (x1, y0), (x1, y1), (x0, y1 - r)] if sx > 0 else \
                  [(x0, y0), (x1, y0), (x1, y1 - r), (x0, y1)]
        out[name] = [redrawn(f, pts, plane="oblique", recede=(sx * w, r))]
    return out


def bottom_cut(pts, i, y):
    """Corners i and i + 1 of an outline (a board's end cut) slid along their other edges to height
    y, so the cut is level."""
    n = len(pts)
    a, b = pts[i], pts[(i + 1) % n]
    pa, pb = pts[(i - 1) % n], pts[(i + 2) % n]
    out = list(pts)
    out[i] = (xon(pa, a, y), y)
    out[(i + 1) % n] = (xon(b, pb, y), y)
    return out


def _pts(oid, name):
    return _face(oid, name)["pts"]


def shifted(oid, name, dx):
    return [(round(x + dx, 2), y) for x, y in _pts(oid, name)]


# --- red house: the roof's ridge and eave, the skylight, the trim band and its shadow level; the
# gable window square; both flower boxes solid (the right one's shadow moves with it); the steps
refit("house-red", {
    "roof": [(-10, 196), (312, 196), (122, 505), (130, 540), (-10, 540)],
    "skylight-top": [(100, 290), (115, 256.5), (199, 256.5), (177, 290)],
    "skylight-side": [(177, 290), (199, 256.5), (199, 268), (177, 301)],
    "skylight-front": rect(100, 290, 177, 301),
    "side-wall": rect(-10, 540, 114, 966),
    "wall": [(297, 266), (433, 552), (433, 966), (112, 966), (112, 540), (128, 540)],
    "barge-left": bottom_cut(_pts("house-red", "barge-left"), 4, 540),
    "trim": rect(103, 540, 433, 572),
    "trim-shadow": rect(112, 572, 433, 588.5),
    "gable-window-frame": rect(258.5, 361, 322.5, 499),
    "gable-window-glass": rect(273, 383.5, 313, 487),
    "gable-window-mullion": rect(273, 428, 313, 435),
    "window-left-glass": [dict(_face("house-red", "window-left-glass"), like="window-right-glass")],
    **sill_box("house-red", "l", RED_DOOR, "window-left", box=(150, 838, 200, 847)),
    **sill_box("house-red", "l", RED_DOOR, "window-right", box=(386, 802, 420, 811), shadow="window-right-shadow"),
    **frame_depths("house-red"),
    **persp_steps("house-red", "l", RED_DOOR, 234.5, 305.5, 329.5, 947.5, 996.5, 1036, 0.9),
})

# --- teal house: the roof's ridge and eave, the side wall's top and its shadow, the trim, the
# barge boards' feet level; the sills solid; the steps
refit("house-teal", {
    "roof": [(786, 488), (934, 488), (936, 483), (884, 612), (884, 616.5), (786, 616.5)],
    "roof-dash": [(845, 524), (849, 519), (875, 519), (871, 524)],
    "side-wall": rect(817, 613, 884, 822),
    "side-wall-shadow": rect(817, 616.5, 884, 632),
    "barge-right": bottom_cut(_pts("house-teal", "barge-right"), 1, 618.5),
    "barge-left": bottom_cut(_pts("house-teal", "barge-left"), 3, 610.5),
    "trim": rect(880, 610.5, 965, 624.5),
    **sill_box("house-teal", "l", TEAL_DOOR, "window-left", 10),
    **sill_box("house-teal", "l", TEAL_DOOR, "window-right", 11),
    **frame_depths("house-teal"),
    **persp_steps("house-teal", "l", TEAL_DOOR, 897, 929, 944, 786.5, 801.5, 815, 0.94),
})

# --- yellow house: the roof's ridge, the barge boards' cuts, the trim and the front wall's
# foundation level; the sill solid; the steps rebuilt from the original's wavy ones (the landing
# is the first step), centred under the door
refit("house-yellow", {
    "roof": [(396, 429.5), (720.8, 429.5), (725, 418), (683, 590), (396, 590)],
    "soffit": [(724, 436), (730, 436), (818, 600), (806, 600)],
    "wall": [(724, 436), (806, 598), (820, 610), (820, 842), (692, 842), (692, 588), (696, 588)],
    "barge-left": [(725, 412), (730, 436), (724, 436), (696.82, 590), (682.24, 590)],
    "barge-right": [(725, 412), (837.18, 602), (818.93, 602), (730, 436)],
    "trim": rect(694, 588.5, 820, 608.5),
    "foundation": rect(692, 842, 724, 872),
    **sill_box("house-yellow", "l", YELLOW_DOOR, "window", 7),
    **frame_depths("house-yellow"),
    **persp_steps("house-yellow", "l", YELLOW_DOOR, 723.5, 778.5, 798.5, 828, 852, 876, 0.9,
                  was={"steps-side": "landing-front"}),
})

# --- pink house: the roof's ridge, the barge boards' and soffit's cuts, the wall's corner, the
# trim; the sills solid (its steps are seen from the front, level already)
refit("house-pink", {
    "roof": [(2031, 474), (2034, 481), (2200, 481), (2200, 611), (2084, 611)],
    "barge-right": [(2031, 474), (2088.43, 609), (2078.6, 609), (2031, 493)],
    "barge-left": [(2031, 474), (2031, 493), (1995.7, 611), (1987.32, 611)],
    "soffit-left": [(2031, 493), (2040, 512), (2004.26, 608), (1996.6, 608)],
    "wall": [(2040, 512), (2079, 606), (2088, 606), (2088, 790), (2003, 790), (2003, 612), (2005, 606)],
    "trim": rect(2001, 605.5, 2088, 619),
    **sill_box("house-pink", "r", PINK_DOOR, "window-left", 12),
    **sill_box("house-pink", "r", PINK_DOOR, "window-right", 11),
})

# --- blue house: the chimney's side face toward the vanishing point; the barge boards' and
# soffit's cuts, the wall's corners, the trim and its shadow level; the gable window square; the
# sills solid; the steps
_bl = _pts("house-blue", "barge-left")
refit("house-blue", {
    "chimney-side": [(2439, 336.66), (2466, 330), (2466, 452), (2439, 402)],
    "soffit": bottom_cut(_pts("house-blue", "soffit"), 2, 596),
    "barge-right": [(2247, 382), (2340.85, 602), (2332.1, 602), (2247, 411)],
    "barge-left": [(2247, 382), (2247, 411), (xon((2247, 411), _bl[2], 598.5), 598.5),
                   (xon(_bl[0], _bl[3], 598.5), 598.5)],
    "wall": [(2248, 416), (2337, 600), (2347, 600), (2347, 830), (2190, 830), (2190, 595), (2201, 595),
             (2256, 433)],
    "trim": rect(2190, 588, 2340, 607.5),
    "trim-shadow": rect(2192, 607.5, 2340, 616),
    "gable-window-frame": rect(2245.5, 480.5, 2274, 562.5),
    "gable-window-glass": rect(2252, 487.5, 2268, 556.5),
    "gable-window-mullion": rect(2252, 512, 2268, 519),
    **sill_box("house-blue", "r", BLUE_DOOR, "window-left", 12),
    **sill_box("house-blue", "r", BLUE_DOOR, "window-right", 12),
    # the door's reveal on the panel's left, as the original paints it: the side the house's depth
    # goes (win-7); it ran down the right
    **frame_depths("house-blue", {"door-reveal": (2241, 655, 2248, 830)}),
    **persp_steps("house-blue", "r", BLUE_DOOR, 2283, 2242, 2228, 835.5, 860.5, 885, 0.933),
})

# --- green house (Derek's struck-through bands): the roof's shingle shades, the barge boards'
# apex, the trim band and its shadow level; the gable window square; both flower boxes solid,
# their shadows moved with them (the left one's foot level); the steps
refit("house-green", {"window-left-shadow": [(2522, 827), (2592, 827), (2592, 848), (2530, 848)]})
refit("house-green", {
    "roof": [(2626, 202), (2826, 202), (2826, 516)],
    "roof-shade-1": [(2738, 302), (2826, 302), (2826, 324), (2752, 324)],
    "roof-shade-2": [(2795, 392), (2826, 392), (2826, 419), (2805, 419)],
    "barge-right": [(2626, 202), (2826, 515), (2826, 566), (2626, 249)],
    "barge-left": [(2626, 202), (2626, 249), (2476, 578), (2465, 560)],
    "trim": rect(2502, 546.5, 2826, 580.5),
    "trim-shadow": rect(2505, 580.5, 2826, 592.5),
    "gable-window-frame": rect(2615, 363, 2679.5, 502.5),
    "gable-window-glass": rect(2628, 380, 2670, 489.5),
    "gable-window-mullion": rect(2628, 425.5, 2670, 441.5),
    "gable-window-side": rect(2672, 363, 2679.5, 502.5),
    **sill_box("house-green", "r", GREEN_DOOR, "window-left", box=(2506, 808, 2540, 817),
               shadow="window-left-shadow"),
    **sill_box("house-green", "r", GREEN_DOOR, "window-right", box=(2740, 844, 2790, 857),
               shadow="window-right-shadow"),
    **frame_depths("house-green"),
    **persp_steps("house-green", "r", GREEN_DOOR, 2688.5, 2648.5, 2622.5, 956, 1005.5, 1047, 0.9),
})

# --- far houses: each roof's ridge level with its gable's apex; the far pink house's left barge
# and the far yellow house's right barge cut level; the chimneys' side faces toward the
# vanishing point
refit("house-far-salmon", {
    "roof": [(1141, 546), (958, 546), (958, 621), (1130, 621)],
    "chimney-side": [(1006, 528), (1016, 529.68), (1016, 575.67), (1006, 575)],
})
refit("house-far-rose", {"roof": [(1270, 584), (1205, 584), (1205, 642), (1256, 642)]})
refit("house-far-pink", {
    "roof": [(1741, 565), (1850, 565), (1850, 632), (1757, 632)],
    "barge-left": [(1740, 565), (1743, 571), (1729.57, 634), (1723.48, 634)],
})
refit("house-far-yellow", {
    "roof": [(1887, 527), (2012, 527), (2012, 620), (1915, 620)],
    "barge-right": [(1886, 527), (1893, 527), (1919, 619), (1912, 619)],
    "chimney-lit": [(1961, 500), (1968, 498.9), (1968, 559.73), (1961, 560)],
})


# ==========================================================================
# Trees and bushes: low-poly crowns (a few flat facets) on trunks
# ==========================================================================
JITTER = [1.0, 0.95, 1.02, 0.93, 0.99, 1.03, 0.94, 1.0, 0.96, 1.02, 0.95, 0.98]


def crown_pts(cx, cy, rx, ry, n=11, phase=0):
    import math
    out = []
    for k in range(n):
        a = -math.pi / 2 + 2 * math.pi * k / n
        j = JITTER[(k + phase) % len(JITTER)]
        out.append((round(cx + rx * j * math.cos(a), 1), round(cy + ry * j * math.sin(a), 1)))
    return out


def crown(prefix, cx, cy, rx, ry, n=11, phase=0, light="right", hid=None):
    """A low-poly crown: the outline, a lit facet on the sun's side and a shaded facet below."""
    pts = crown_pts(cx, cy, rx, ry, n, phase)
    c = (round(cx + (-0.15 if light == "right" else 0.15) * rx, 1), round(cy - 0.05 * ry, 1))
    lit = [p for p in pts if (p[0] - cx) * (1 if light == "right" else -1) > -0.2 * rx and p[1] < cy + 0.2 * ry]
    shade = [p for p in pts if p[1] > cy + 0.3 * ry]
    kw = {"hid": hid} if hid else {}
    faces = [P(f"{prefix}", pts, **kw)]
    # the facets are shades of the crown's measured colour (a far crown is too small to
    # measure each facet on its own)
    if len(lit) >= 2:
        faces.append(P(f"{prefix}-lit", [c] + lit, like=f"{prefix}", k=1.08))
    if len(shade) >= 2:
        faces.append(P(f"{prefix}-shade", [(round(cx, 1), round(cy + 0.1 * ry, 1))] + shade, like=f"{prefix}",
                       k=0.88))
    return faces


def trunk(x0, y0, x1, y1, name="trunk"):
    m = (x0 + x1) / 2
    return [P(name, [(x0, y0), (x1, y0), (x1 - 1, y1), (x0 + 1, y1)], hid="#7a4b3a"),
            P(f"{name}-side", [(m, y0), (x1, y0), (x1 - 1, y1), (m, y1)], hid=name, k=0.8)]


def tree(oid, cx, cy, rx, ry, tr=None, n=11, phase=0, behind=(), layer="trees", light="right", hid=None):
    return O(oid, layer, (trunk(*tr) if tr else []) + crown("crown", cx, cy, rx, ry, n, phase, light, hid),
             behind=behind)


# --------------------------------------------------------------------------
# Faceted far crowns: the original's far trees are low-poly - a dozen or so flat facets in
# three tones, light on the sun's side and on top, dark underneath and away from the sun.
# A crown is an outline ring, an inner ring and a centre; the band between the rings is cut
# into facets (each split once more where it spans enough of the outline) and the inner
# ring into triangles to the centre, so the facets tile the crown exactly. Each facet's
# tone follows which way it faces (its centroid's direction from the centre against the
# light), and each tone is measured on the original: the median of that lightness third of
# the crown's pixels (other hues dropped).
# --------------------------------------------------------------------------
SUN_X = 1700                       # the sun's x: far trees left of it are lit from the right
TREE_TONES = {"light": (0.55, 0.82), "mid": (0.33, 0.62), "dark": (0.1, 0.36)}
TREE_TONE_K = {"light": 1.1, "mid": 1.0, "dark": 0.85}   # fallback shades of a hidden crown
TREE_LIGHT = (0.6, -0.8)           # light direction (x toward the sun, y up)
TREE_LIT_S = 0.3                   # a facet facing the light more than this is light ...
TREE_DARK_S = -0.25                # ... less than this dark, in between mid
INNER_F = [0.52, 0.6, 0.46, 0.56, 0.5, 0.62, 0.48]      # inner ring radius, share of the outline's


TONE_FACES = {}                    # {tree id: {tone: "tree id/facet"}}: the facet that measures each tone


def facet_crown(prefix, cx, cy, rx, ry, n=14, phase=0, inner=5, pts=None, hid=None, split=True, tones=None):
    """tones: {tone: "object/face"} to take the tones from another tree (when this crown shows
    too little of itself to measure them)."""
    import math
    outer = list(pts) if pts else crown_pts(cx, cy, rx, ry, n, phase)
    n = len(outer)
    sx = 1 if SUN_X > cx else -1
    lx, ly = TREE_LIGHT[0] * sx, TREE_LIGHT[1]
    c = (round(cx + 0.1 * rx * lx, 1), round(cy + 0.1 * ry * ly, 1))
    idx = [(round(j * n / inner) + phase) % n for j in range(inner)]
    idx = sorted(set(idx))
    m = len(idx)
    ring = []
    for j, i in enumerate(idx):
        f = INNER_F[(j + phase) % len(INNER_F)]
        ox, oy = outer[i]
        ring.append((round(c[0] + (ox - c[0]) * f, 1), round(c[1] + (oy - c[1]) * f, 1)))
    polys = []
    for j in range(m):
        a, b = idx[j], idx[(j + 1) % m]
        seg = [outer[(a + t) % n] for t in range(((b - a) % n) + 1)]
        if split and len(seg) >= 4:
            h = len(seg) // 2
            polys.append(("band", [ring[j]] + seg[:h + 1]))
            polys.append(("band", [ring[j]] + seg[h:] + [ring[(j + 1) % m]]))
        else:
            polys.append(("band", [ring[j]] + seg + [ring[(j + 1) % m]]))
    for j in range(m):
        polys.append(("top", [c, ring[j], ring[(j + 1) % m]]))
    names = [f"{prefix}-facet-{i + 1}" for i in range(len(polys))]
    every = [prefix] + names
    base = {"hid": hid} if hid else {}
    faces = [P(prefix, outer, of=every, q=TREE_TONES["mid"], **base)]
    first = {}
    for name, (kind, poly) in zip(names, polys):
        gx = sum(p[0] for p in poly) / len(poly)
        gy = sum(p[1] for p in poly) / len(poly)
        dx, dy = (gx - c[0]) / rx, (gy - c[1]) / ry
        d = math.hypot(dx, dy) or 1.0
        sdot = (dx * lx + dy * ly) / d
        if kind == "top":
            sdot = 0.5 * sdot + 0.25
        tone = "light" if sdot > TREE_LIT_S else ("dark" if sdot < TREE_DARK_S else "mid")
        if tones and tone in tones:
            faces.append(P(name, poly, like=tones[tone]))
        elif tone in first:
            faces.append(P(name, poly, like=first[tone]))
        else:
            first[tone] = name
            faces.append(P(name, poly, of=every, q=TREE_TONES[tone], hid=prefix, k=TREE_TONE_K[tone]))
    TONE_FACES[cx, cy] = first
    return faces


def ftree(oid, cx, cy, rx, ry, tr=None, n=14, phase=0, inner=5, pts=None, behind=(), hid=None, split=True,
          trunk_at=None, trunk_like=None, extra=(), tones_of=None):
    """A far tree: a clean trunk (measured; trunk_at where the crown and haze muddle it) under a
    faceted crown."""
    t = trunk(*tr) if tr else []
    if t and trunk_at:
        t[0]["at"] = trunk_at
    if t and trunk_like:
        t[0]["like"] = trunk_like
        t[1]["like"] = "trunk"
    tones = {k: f"{tones_of}/{v}" for k, v in TONE_FACES[tones_of].items()} if tones_of else None
    faces = facet_crown("crown", cx, cy, rx, ry, n, phase, inner, pts, hid, split, tones)
    TONE_FACES[oid] = TONE_FACES.pop((cx, cy))
    return O(oid, "trees", t + list(extra) + faces, behind=behind)


add(
    O("horizon-treeline", "trees", [
        # a hazy blue band of blocky lobes; each lobe's sun side is a little lighter
        P("trees", [(1262, 702), (1262, 592), (1272, 585), (1286, 584), (1292, 595), (1306, 597), (1313, 603),
                    (1322, 603), (1330, 608), (1360, 606), (1378, 618), (1382, 625), (1396, 621), (1410, 622),
                    (1418, 632), (1418, 650), (1430, 646), (1452, 645), (1462, 651), (1472, 658), (1477, 673),
                    (1488, 673), (1490, 658), (1496, 642), (1512, 640), (1522, 633), (1528, 618), (1540, 612),
                    (1560, 610), (1566, 618), (1576, 626), (1585, 628), (1600, 615), (1640, 612), (1648, 604),
                    (1660, 598), (1665, 702)],
          grad=True),
        P("lit-1", [(1286, 584), (1292, 595), (1306, 597), (1313, 603), (1300, 612), (1288, 600)],
          like="trees", k=1.02),
        P("lit-2", [(1396, 621), (1410, 622), (1418, 632), (1418, 642), (1405, 634)], like="lit-1"),
        P("lit-3", [(1452, 645), (1462, 651), (1472, 658), (1477, 673), (1466, 668), (1455, 656)], like="lit-1"),
        P("lit-4", [(1512, 640), (1522, 633), (1528, 618), (1540, 612), (1545, 625), (1528, 640), (1515, 648)],
          like="lit-1"),
        P("lit-5", [(1560, 610), (1566, 618), (1576, 626), (1585, 628), (1580, 640), (1565, 632)], like="lit-1"),
    ], behind=["house-far-rose", "house-far-blue"]),
    ftree("tree-far-left-1", 1068, 645, 72, 66, (1061, 690, 1086, 792), inner=7, pts=[
        (1077, 576), (1098, 582), (1105, 591), (1123, 598), (1131, 611), (1140, 640), (1138, 660), (1130, 680),
        (1122, 695), (1100, 705), (1065, 708), (1045, 700), (1030, 690), (1012, 672), (996, 660), (999, 640),
        (1010, 628), (1022, 610), (1033, 594), (1052, 585)]),
    ftree("tree-far-left-2", 1184, 606, 38, 58, (1179, 655, 1188, 722), n=14, phase=4,
          trunk_at=[(1182, 672), (1186, 672), (1186, 700), (1182, 700)]),
    ftree("tree-far-left-3", 1232, 640, 21, 39, (1228, 670, 1236, 726), n=12, phase=1, inner=4),
    ftree("tree-far-left-4", 1352, 623, 31, 34, (1349, 640, 1357, 694), n=13, phase=3),
    # poplar-1 and -2 stand on the treeline: their thin trunks show only its blue, so they take
    # tree-far-left-4's trunk colour
    ftree("poplar-1", 1289, 661, 5.5, 9, (1288, 668, 1290.5, 697), n=8, inner=3, split=False,
          trunk_like="tree-far-left-4/trunk"),
    ftree("poplar-2", 1374, 662, 7, 10, (1373, 670, 1375.5, 692), n=8, phase=1, inner=3, split=False,
          trunk_like="tree-far-left-4/trunk"),
    ftree("poplar-3", 1557, 657, 7, 16, (1555.5, 670, 1558, 692), n=8, phase=2, inner=3, split=False),
    ftree("poplar-4", 1578, 663, 10, 18, (1577, 678, 1580, 697), n=8, phase=3, inner=3, split=False),
    ftree("poplar-5", 1650, 662, 5, 8.5, (1649, 669, 1651, 692), n=8, phase=4, inner=3, split=False),
    ftree("tree-far-right-1", 1613, 625, 25, 30, (1606, 645, 1613, 696), n=13, phase=5),
    ftree("tree-far-right-2", 1716, 606, 21, 35, n=12, phase=6, inner=4,
          behind=["house-far-blue", "house-far-pink"],
          hid="tree-far-right-1/crown", tones_of="tree-far-right-1"),
    # stands on the lawn in front of the far blue house (its foot was 6 px above that house's
    # walls' foot, so it read as behind them though drawn in front: moved down onto the ground,
    # still measured where the original shows it); the far pink house's front, nearer, hides its edge
    dict(tree("bush-far-right-2", 1713, 673, 19, 22, n=9, phase=7, behind=["house-far-pink"]),
         faces=shift(tree("bush-far-right-2", 1713, 673, 19, 22, n=9, phase=7)["faces"], 0, 7.5)),
    ftree("tree-far-right-3", 1787, 641, 38, 49, (1778, 680, 1793, 750), n=14, phase=8),
    ftree("tree-far-right-4", 1866, 565, 83, 85, (1853, 600, 1868, 725), n=16, phase=9, inner=6,
          extra=[P("branch", [(1817, 629), (1826, 628), (1857, 652), (1857, 664)], like="trunk")],
          behind=["house-far-yellow"]),
    ftree("tree-far-right-5", 1948, 652, 46, 84, (1940, 725, 1958, 792), n=15, phase=10),
    O("tree-behind-red-house", "trees", [
        P("trunk", [(418, 455), (432, 455), (432, 560), (418, 560)], hid="#7a5a3a"),
        P("crown", [(379, 338), (410, 337), (426, 352), (445, 376), (474, 389), (479, 412), (482, 440),
                    (470, 468), (440, 482), (405, 482), (372, 468), (356, 425), (362, 375)],
          of=["crown", "crown-dark", "crown-lit-1", "crown-lit-2", "crown-shade"], q=TREE_TONES["mid"],
          hid="#b6d66a"),
        P("crown-dark", [(379, 338), (392, 350), (380, 372), (362, 375)],
          of=["crown", "crown-dark", "crown-lit-1", "crown-lit-2", "crown-shade"], q=TREE_TONES["dark"],
          hid="crown", k=0.88),
        P("crown-lit-1", [(392, 350), (410, 337), (426, 352), (445, 376), (442, 382), (420, 368), (400, 362)],
          of=["crown", "crown-dark", "crown-lit-1", "crown-lit-2", "crown-shade"], q=TREE_TONES["light"],
          hid="crown", k=1.08),
        P("crown-lit-2", [(445, 376), (474, 389), (479, 412), (462, 408), (447, 396)], like="crown-lit-1"),
        P("crown-shade", [(356, 425), (400, 440), (440, 470), (405, 482), (372, 468)], like="crown-dark"),
    ], behind=["house-red", "house-yellow"]),
    O("tree-near-yellow-house", "trees", [
        P("trunk", [(655, 812), (692, 812), (692, 910), (654, 910)]),
        P("trunk-side", [(679, 812), (692, 812), (692, 910), (679, 910)]),
        P("crown", [(667, 574), (688, 583), (701, 594), (717, 633), (731, 669), (740, 710), (735, 751),
                    (722, 787), (706, 819), (681, 823), (654, 819), (635, 796), (617, 760), (601, 705),
                    (617, 651), (633, 596), (649, 585)]),
        P("crown-lit", [(667, 574), (688, 583), (701, 594), (717, 633), (731, 669), (740, 710), (735, 751),
                        (690, 712), (672, 640), (660, 582)]),
        P("crown-shade", [(601, 705), (681, 715), (735, 751), (722, 787), (706, 819), (681, 823), (654, 819),
                          (635, 796), (617, 760)]),
        P("crown-deep", [(617, 760), (662, 790), (706, 819), (681, 823), (654, 819), (635, 796)]),
    ]),
    O("tree-near-left", "trees", [
        P("trunk", [(-10, 900), (31, 900), (31, 1100), (-10, 1100)]),
        P("trunk-side", [(31, 900), (57, 900), (52, 1100), (31, 1100)]),
        P("crown", [(-10, 532), (62, 517), (102, 542), (150, 607), (165, 645), (180, 745), (165, 780),
                    (152, 815), (135, 850), (115, 890), (75, 905), (30, 907), (-10, 905)]),
        P("crown-lit", [(62, 517), (102, 542), (150, 607), (165, 645), (180, 745), (150, 812), (128, 760),
                        (108, 690), (85, 600)]),
        P("crown-dark", [(-10, 538), (50, 582), (5, 586), (-10, 588)]),
        P("crown-shade", [(-10, 700), (35, 715), (118, 750), (138, 770), (150, 812), (135, 850), (115, 890),
                          (75, 905), (30, 907), (-10, 905)]),
    ]),
)

add(
    O("bush-front-left", "props", [
        P("shadow", [(-10, 1240), (112, 1236), (132, 1196), (152, 1180), (200, 1168), (248, 1164), (288, 1152),
                     (310, 1150), (310, 1172), (205, 1205), (132, 1245), (60, 1274), (-10, 1278)]),
        P("bush", [(-10, 1098), (24, 1092), (44, 1032), (60, 1004), (88, 980), (120, 972), (176, 974),
                   (212, 984), (240, 1004), (256, 1024), (280, 1044), (296, 1064), (304, 1092), (302, 1132),
                   (288, 1156), (248, 1168), (200, 1172), (152, 1184), (132, 1200), (128, 1220), (112, 1240),
                   (72, 1252), (24, 1258), (-10, 1262)]),
        P("lobe-top", [(44, 1032), (60, 1004), (88, 980), (120, 972), (176, 974), (212, 984), (240, 1004),
                       (256, 1024), (270, 1044), (215, 1050), (170, 1040), (130, 1040), (90, 1056), (50, 1070)]),
        P("lobe-right", [(216, 1050), (256, 1024), (280, 1044), (296, 1064), (304, 1092), (296, 1110),
                         (260, 1100), (225, 1080)]),
        P("facet-1", [(120, 1076), (134, 1062), (176, 1080), (182, 1098), (160, 1108)]),
        P("facet-2", [(200, 1036), (222, 1032), (232, 1056), (210, 1060)]),
        P("facet-3", [(40, 1126), (80, 1124), (112, 1158), (86, 1176), (48, 1176), (40, 1150)]),
        P("lobe-front", [(-10, 1104), (30, 1096), (52, 1128), (40, 1150), (-10, 1160)]),
        P("blossom", [(244, 1102), (256, 1099), (262, 1110), (250, 1117), (242, 1111)], hid="#f6d33a"),
    ]),
)


# ==========================================================================
# Props: bushes, fences, mailboxes, flowers, toys
# ==========================================================================
def bush(oid, cx, cy, rx, ry, n=11, phase=0, behind=()):
    return O(oid, "props", crown("leaves", cx, cy, rx, ry, n, phase), behind=behind)


def pickets(oid, boards, bottom, back, rounded=True, behind=()):
    """A picket fence: the dark back (rails and the gaps between boards), then each board's
    front (x0, x1, top) with a rounded top and its darker side strip."""
    bx0, by0, bx1, by1 = back
    faces = [R("back", bx0, by0, bx1, by1, hid="#8f4f35")]
    for i, (x0, x1, top) in enumerate(boards):
        r = (x1 - x0) / 2
        if rounded:
            faces.append(A(f"board-{i + 1}", [(x0, bottom), (x0, top + r), ("arc", r, x1, top + r), (x1, bottom)],
                           hid="board-1" if i else "#c98355"))
        else:
            faces.append(P(f"board-{i + 1}", [(x0, bottom), (x0, top + r), ((x0 + x1) / 2, top), (x1, top + r),
                                              (x1, bottom)], hid="board-1" if i else "#c98355"))
    return O(oid, "props", faces, behind=behind)


# A raised US mailbox flag, measured at the near mailboxes' size (s=1) in original px:
# a thin arm pivoting on the box's side, its rounded foot on a rivet, ending in a plate
# that points to the box's back, its free end a little taller with a small notch.
FLAG_ARM_W = 18        # arm width
FLAG_ARM_LEN = 103     # pivot to the plate's top edge
FLAG_PLATE_LEN = 64    # plate length from the arm's front edge
FLAG_PLATE_H = 36      # plate height at the arm
FLAG_FLARE = 4         # the free end is this much taller above and below
FLAG_NOTCH = 8         # depth of the notch in the free end
FLAG_SHADE = 0.65      # the dark lower facet starts this far down the plate
FLAG_LIGHT_W = 5       # the arm's lit edge strip (on the box's front side)
FLAG_RIVET_R = 5.5     # pivot rivet radius
FLAG_RIVET_MIN = 0.3   # below this scale the rivet is dropped (too small to read)
FLAG_RIVET_K = 0.6     # the rivet is the arm's dark colour darkened by this


def shade_of(hex_, k):
    """hex_ colour multiplied by k."""
    return "#" + "".join(f"{min(255, round(int(hex_[i:i + 2], 16) * k)):02x}" for i in (1, 3, 5))


def flag(cx, pivot_y, s, back, dark, light):
    """The faces of a raised mailbox flag. cx, pivot_y: the arm's centre line and pivot;
    s: scale (1 = near mailbox); back: +1 when the box's back is to the right (the plate
    points that way), -1 to the left; dark, light: the low-poly shading colours."""
    r2 = lambda v: round(v, 2)
    hw = FLAG_ARM_W * s / 2
    xl, xr = r2(cx - hw), r2(cx + hw)
    root = xl if back > 0 else xr                       # plate starts at the arm's front edge

    def u(d):                                           # x at distance d from root, toward the back
        return r2(root + back * d * s)
    top = r2(pivot_y - FLAG_ARM_LEN * s)
    bot = r2(top + FLAG_PLATE_H * s)
    mid = r2(top + FLAG_PLATE_H * s / 2)
    shade = r2(top + FLAG_PLATE_H * s * FLAG_SHADE)
    fl = FLAG_FLARE * s
    end, notch = u(FLAG_PLATE_LEN), u(FLAG_PLATE_LEN - FLAG_NOTCH)
    light_x = (r2(xr - FLAG_LIGHT_W * s), xr) if back < 0 else (xl, r2(xl + FLAG_LIGHT_W * s))
    faces = [
        A("flag-arm", [(xl, mid), (xr, mid), (xr, r2(pivot_y)), ("arc", r2(hw), xl, r2(pivot_y))], c=dark),
        R("flag-arm-light", light_x[0], bot, light_x[1], r2(pivot_y), c=light),
        P("flag-plate", [(root, top), (end, r2(top - fl)), (notch, mid), (end, r2(bot + fl)), (root, bot)],
          c=light),
        # the dark facet: the plate's lower band, cut level across into the notch's lower edge
        P("flag-plate-shade", [(root, shade), (r2(notch + (end - notch) * (shade - mid) / (bot + fl - mid)), shade),
                               (end, r2(bot + fl)), (root, bot)], c=dark),
    ]
    if s >= FLAG_RIVET_MIN:
        rr = FLAG_RIVET_R * s
        faces += [C("flag-rivet", r2(cx), r2(pivot_y), r2(rr), c=shade_of(dark, FLAG_RIVET_K)),
                  C("flag-rivet-light", r2(cx - rr * 0.3), r2(pivot_y - rr * 0.3), r2(rr * 0.38), c=light)]
    return faces


# flag colours (dark, light), measured on the original's flags
FLAG_COLOURS = {
    "mailbox-left": ("#c43d20", "#f89810"),
    "mailbox-right": ("#dc292c", "#eb7641"),
    "mailbox-far-left-1": ("#ca755d", "#eb9e76"),
    "mailbox-far-left-2": ("#e38d63", "#f5c9a7"),
    "mailbox-far-left-3": ("#cd9278", "#f0b08a"),
    "mailbox-far-right-1": ("#e09973", "#e0a58a"),
    "mailbox-far-right-2": ("#d9977e", "#f2d1a4"),
    "mailbox-far-right-3": ("#e68666", "#f8d1b3"),
    "mailbox-far-right-4": ("#e06a47", "#efb872"),
}
NEAR_BOX_H = 110       # the near mailboxes' body height: flag scale 1

# A closed US rural mailbox in three-quarter view, measured at the near mailboxes' size (s=1)
# in original px. The box is modelled as a solid - flat vertical sides under a half-cylinder
# top, length along the screen's x (the box stands square to the street), depth along the
# street - and drawn as its visible flat-shaded faces: the half-cylinder top as facets graded
# from the dark side up to a lit band, the near long side, a thin base flange with rivets, and
# at the front end the arch-shaped door (flat bottom, straight sides, semicircular top) facing
# the road, with a raised rim standing a little proud of the body and a latch tab at its top.
# The depth axis points away from the street's vanishing point (one-point perspective), so the
# door is seen from the road side; MB_K sets how far it is turned toward the viewer.
MB_VP = (1470, 700)    # the street's vanishing point (the road's far end)
MB_K = 0.75            # depth foreshortening, near mailboxes (a depth px drawn this long)
MB_K_FAR = 0.8         # ... far mailboxes: turned a little more so the small doors still read
MB_LOOK_DOWN = 0.3     # the depth axis falls at least this steeply (we look down on the box)
MB_HALF_W = 0.36       # the box's half-width (the top's radius) as a share of its height
MB_FACETS = 8          # facets across the half-cylinder top (near) ...
MB_FACETS_FAR = 6      # ... and far
MB_ARCH_SEGS = 12      # straight segments in the door's semicircular top (near) ...
MB_ARCH_SEGS_FAR = 8   # ... and far
MB_LIP = 2.5           # how far the door's rim stands proud of the body
MB_RIM = 4             # the rim's width
MB_LIP_MIN = 0.6       # ... at least this (far mailboxes)
MB_RIM_MIN = 1.0       # ... at least this, so the smallest doors still read as arches
MB_FLANGE_H = 5        # the base flange's height ...
MB_FLANGE_OUT = 1.5    # ... and how far it stands out from the side
MB_FLANGE_MIN = 0.3    # below this scale the flange is dropped
MB_RIVET_R = 2.4       # base-flange rivet radius
MB_RIVETS = (0.1, 0.48, 0.86)   # rivets along the near side's flange, as shares of its length
MB_RIVET_MIN = 0.6     # below this scale the rivets are dropped
MB_TAB = (0.06, 0.36, 0.26, 2.5)   # latch tab: from / to across the door (share of the radius,
                                # toward the near side), height below the top (share of the
                                # radius), height above the rim (px)
MB_TAB_MIN = 0.2       # below this scale the latch tab is dropped
MB_FLAG_BACK = 6       # the flag plate's end sits this far (px) in from the box's back
MB_FLAG_DIP = 1.5      # the raised plate's lower edge sits this far below the box's top (no sliver)
# one blue family for every mailbox: lit top band, dark side, door face, door rim (and the
# base flange), latch tab (and the rivets)
MB_COLOURS = dict(band="#5cbcee", side="#3f78bf", door="#5aa3dd", rim="#2c5a96", tab="#1f3f6e")
MB_POST = ("#8a4b2b", 0.8)          # the far mailboxes' post colour and its side's shade
MB_HAZE = "#cfe3f2"    # far mailboxes are mixed toward this ...
MB_POST_HAZE = "#c8b49e"            # ... their posts toward this warm haze (stays brown)
MB_HAZE_K = 0.45       # ... by this times (1 - scale)


def mix(a, b, t):
    """Colour a mixed toward b by t (0..1)."""
    return "#" + "".join(f"{round(int(a[i:i + 2], 16) * (1 - t) + int(b[i:i + 2], 16) * t):02x}"
                         for i in (1, 3, 5))


def mailbox_box(body, faces_right, s=1.0):
    """(faces, flag pivot (x, y)) of a closed rural mailbox filling body = (x0, y0, x1, y1)
    (door, rim and flange included), its door at the right end when faces_right."""
    import math
    x0, y0, x1, y1 = body
    r2 = lambda v: round(v, 2)
    far = s < 0.6
    haze = max(0.0, min(1.0, (1 - s) * MB_HAZE_K))
    col = {k: mix(v, MB_HAZE, haze) for k, v in MB_COLOURS.items()}
    # the depth axis: away from the vanishing point (toward the viewer), never looking up
    cx, cy = (x0 + x1) / 2, (y0 + y1) / 2
    dx, dy = abs(cx - MB_VP[0]), max(0.0, cy - MB_VP[1])
    dy = max(dy, dx * MB_LOOK_DOWN)
    n = math.hypot(dx, dy)
    k = MB_K_FAR if far else MB_K
    evx, evy = -k * dx / n, k * dy / n           # authored with the door on the right
    facets = MB_FACETS_FAR if far else MB_FACETS
    segs = MB_ARCH_SEGS_FAR if far else MB_ARCH_SEGS
    lip = max(MB_LIP * s, MB_LIP_MIN)
    rw = max(MB_RIM * s, MB_RIM_MIN)

    def build(L, sc):
        b, hs = MB_HALF_W * sc, (1 - MB_HALF_W) * sc

        def p(u, v, w):
            return (u + v * evx, v * evy - w)

        def arch(r, w0, u):           # the door's outline in its plane: near bottom, up, over, down
            pts = [p(u, r, w0), p(u, r, hs)]
            pts += [p(u, r * math.cos(math.pi * i / segs), hs + r * math.sin(math.pi * i / segs))
                    for i in range(1, segs)]
            return pts + [p(u, -r, hs), p(u, -r, w0)]
        faces = []
        th = [math.pi * i / facets for i in range(facets + 1)]
        for i in range(facets):
            a, c = th[i], th[i + 1]
            m = (a + c) / 2
            if math.cos(m) + evy * math.sin(m) <= 0:     # faces away from the viewer
                continue
            va, wa = b * math.cos(a), hs + b * math.sin(a)
            vc, wc = b * math.cos(c), hs + b * math.sin(c)
            faces.append(("top-" + str(i + 1), [p(0, va, wa), p(L, va, wa), p(L, vc, wc), p(0, vc, wc)],
                          mix(col["side"], col["band"], min(1.0, math.sin(m) * 1.08))))
        faces.append(("side", [p(0, b, 0), p(L, b, 0), p(L, b, hs), p(0, b, hs)], col["side"]))
        if s >= MB_FLANGE_MIN:
            fo, fh = MB_FLANGE_OUT * s, MB_FLANGE_H * s
            faces.append(("flange", [p(-fo, b + fo, 0), p(L, b + fo, 0), p(L, b + fo, fh), p(-fo, b + fo, fh)],
                          col["rim"]))
        faces.append(("door-rim", arch(b + lip, 0, L), col["rim"]))
        faces.append(("door", arch(b + lip - rw, rw, L), col["door"]))
        extra = []
        if s >= MB_TAB_MIN:
            t0, t1, td, tu = MB_TAB
            top = hs + b + lip
            extra.append(("latch", [p(L, b * t1, top - b * td), p(L, b * t0, top - b * td),
                                    p(L, b * t0, top + tu * s), p(L, b * t1, top + tu * s)], col["tab"]))
        rivets = []
        if s >= MB_RIVET_MIN:
            fo = MB_FLANGE_OUT * s
            rivets = [p(L * f, b + fo, MB_FLANGE_H * s / 2) for f in MB_RIVETS]
        return faces, extra, rivets, p(0, b, 0), p(L, b, 0)

    def box(fs):
        xs = [x for _, pts, _ in fs for x, _ in pts]
        ys = [y for _, pts, _ in fs for _, y in pts]
        return min(xs), min(ys), max(xs), max(ys)
    sc = y1 - y0                      # the model's height, refined until the drawn box fits body
    for _ in range(6):
        bx0, by0, bx1, by1 = box(build(0, sc)[0])
        sc *= (y1 - y0) / (by1 - by0)
    bx0, by0, bx1, by1 = box(build(0, sc)[0])
    L = (x1 - x0) - (bx1 - bx0)
    faces, extra, rivets, back, front = build(L, sc)
    ox, oy = x0 - bx0, y0 - by0
    fx = (lambda x: r2(ox + x)) if faces_right else (lambda x: r2(x0 + x1 - ox - x))
    fy = lambda y: r2(oy + y)
    out = [P(name, [(fx(x), fy(y)) for x, y in pts], c=c) for name, pts, c in faces + extra]
    for i, (x, y) in enumerate(rivets):
        out.append(C(f"rivet-{i + 1}", fx(x), fy(y), r2(MB_RIVET_R * s), c=col["tab"]))
    # the flag pivots on the near side, near the back: its plate's end just in from the back
    reach = (FLAG_ARM_W / 2 + FLAG_PLATE_LEN + MB_FLAG_BACK) * s
    bxs = back[0] + reach
    pivot = (fx(bxs), r2(y0 + (FLAG_ARM_LEN - FLAG_PLATE_H + MB_FLAG_DIP) * s))
    return out, pivot


def rural_mailbox(oid, body, faces_right, ground=(), post=None):
    """A mailbox object: ground faces (shadow, post: measured colours), or for a far one a
    post rectangle (x0, y0, x1, y1) in the hazed post colours; the box; the flag."""
    x0, y0, x1, y1 = body
    s = (y1 - y0) / NEAR_BOX_H
    faces = list(ground)
    if post:
        px0, py0, px1, py1 = post
        haze = max(0.0, min(1.0, (1 - s) * MB_HAZE_K))
        faces += [R("post", px0, py0, px1, py1, c=mix(MB_POST[0], MB_POST_HAZE, haze)),
                  R("post-side", (px0 + px1) / 2, py0, px1, py1,
                    c=mix(shade_of(MB_POST[0], MB_POST[1]), MB_POST_HAZE, haze))]
    box, (fx, fy) = mailbox_box(body, faces_right, s)
    return O(oid, "props", faces + box + flag(fx, fy, s, -1 if faces_right else 1, *FLAG_COLOURS[oid]))


# Every house's mailbox stands by one rule in ground space (#841, README street-4 / street-7):
# on the lawn beside its own path, on the path's side nearer the viewer, its post's foot MB_OFF
# sidewalk widths from the sidewalk's lawn edge and MB_GAP slabs nearer than its path's near edge,
# MB_HEIGHT sidewalk widths tall from the post's foot to the box's top. Every mailbox is the same
# real-world size, so on screen it scales only with depth, by the sidewalk's width at its foot.
# The three numbers are the near two mailboxes' own (left 0.41 / 1.37 / 1.040, right 0.31 / 1.19 /
# 1.035), averaged, so those two barely move. The far and added ones are one model on a post:
# box length MB_NEW_ASPECT x its height, post MB_NEW_POST x it from the box to the foot, post
# MB_NEW_POST_W px wide at the near mailboxes' box height (NEAR_BOX_H).
MB_OFF = 0.36          # post foot centre, sidewalk widths onto the lawn from the sidewalk's edge
MB_GAP = 1.28          # post foot, slabs nearer than its own path's near edge
MB_HEIGHT = 1.04       # post foot to box top, sidewalk widths
MB_NEW_ASPECT = 1.65   # box length / height (the near mailboxes: 1.58 and 1.67)
MB_NEW_POST = 1.66     # box bottom to post foot / box height (the near mailboxes: 1.66 and 1.64)
MB_NEW_POST_W = 43     # post width at box height NEAR_BOX_H (the near mailboxes' posts: 43-48 px)


def walk_width(side, y):
    """The sidewalk's width on screen at height y."""
    return abs(xat(ROAD_L if side == "l" else ROAD_R, y) - xat(LAWN_L if side == "l" else LAWN_R, y))


# Depth on the ground, as on the sidewalk: y_h is where that sidewalk's edges meet, and one slab
# (the original's joint-1 to joint-2) is one step of 1 / (y - y_h). The slabs are taken as
# square (as long as the sidewalk is wide), which turns a width on screen into a depth.
YH = {"l": meet(LAWN_L, ROAD_L)[1], "r": meet(ROAD_R, LAWN_R)[1]}
SLAB_STEP = {"l": 1 / (1253 - YH["l"]) - 1 / (1418 - YH["l"]), "r": 1 / (1256 - YH["r"]) - 1 / (1422 - YH["r"])}


def nearer(side, y, slabs):
    """The height on screen of the ground line `slabs` slabs nearer than the one at height y."""
    return YH[side] + 1 / (1 / (y - YH[side]) - slabs * SLAB_STEP[side])


def slabs_between(side, y_far, y_near):
    """How many slabs the ground line at y_near is nearer than the one at y_far."""
    return (1 / (y_far - YH[side]) - 1 / (y_near - YH[side])) / SLAB_STEP[side]


def post_depth(side, y, w):
    """How tall on screen a square post's footprint w px wide at height y is (its depth)."""
    u = y - YH[side]
    return w * u * u * SLAB_STEP[side] / walk_width(side, y)


def mailbox_spot(side, path_near):
    """(post foot centre x, foot y, on-screen height) of the mailbox of the house whose path's near
    edge is at height path_near: the one rule (MB_OFF, MB_GAP, MB_HEIGHT)."""
    foot_y = nearer(side, path_near, MB_GAP)
    ww = walk_width(side, foot_y)
    return lawn_edge(side, foot_y) + (-1 if side == "l" else 1) * MB_OFF * ww, foot_y, MB_HEIGHT * ww


def street_mailbox(oid, side, path_near, flags=None, was=None):
    """A mailbox on a post, placed and sized by the rule (mailbox_spot); its door faces the road.
    flags: (near, far) mailbox ids whose flag colours it is between (an added mailbox; one of the
    original's keeps its measured flag colours). was: (body, post) of the original's mailbox, which
    is still measured where the original shows it."""
    cx, foot_y, height = mailbox_spot(side, path_near)
    h = height / (1 + MB_NEW_POST)
    s = h / NEAR_BOX_H
    length, post_w = MB_NEW_ASPECT * h, MB_NEW_POST_W * s
    r2 = lambda v: round(v, 2)
    y1 = r2(foot_y - MB_NEW_POST * h)
    foot_y = r2(foot_y)
    body = (r2(cx - length / 2), r2(foot_y - height), r2(cx + length / 2), y1)
    if flags:
        near, far = flags
        s_near = 1.0 if near == "mailbox-left" else (962 - 850) / NEAR_BOX_H
        s_far = {"mailbox-far-left-1": 46, "mailbox-far-right-4": 38}[far] / NEAR_BOX_H
        t = max(0.0, min(1.0, (s_near - s) / (s_near - s_far)))
        FLAG_COLOURS[oid] = tuple(mix(a, b, t) for a, b in zip(FLAG_COLOURS[near], FLAG_COLOURS[far]))
    mb = rural_mailbox(oid, body, side == "l", post=(r2(cx - post_w / 2), y1, r2(cx + post_w / 2), foot_y))
    if was:
        mb["measure"] = rural_mailbox(oid, was[0], side == "l", post=was[1])["faces"]
    else:
        for f in mb["faces"]:
            f["added"] = True             # not in the original: it changes no measured colour
    return mb


def place_mailbox(oid, side, body, faces_right, path_near, ground):
    """One of the original's near mailboxes (box body and ground faces as measured), moved and
    scaled about its post's foot onto the rule's spot and height (mailbox_spot). It is still
    measured where the original shows it."""
    old = rural_mailbox(oid, body, faces_right, ground=ground)
    pts = next(f for f in ground if f["name"] == "post")["pts"]
    py1 = max(y for _, y in pts)
    foot = [x for x, y in pts if py1 - y < 3]
    px0, px1 = min(foot), max(foot)
    cx, foot_y, height = mailbox_spot(side, path_near)
    k = height / (py1 - body[1])
    r2 = lambda v: round(v, 2)
    T = lambda x, y: (r2(cx + (x - (px0 + px1) / 2) * k), r2(foot_y + (y - py1) * k))
    new = rural_mailbox(oid, (*T(body[0], body[1]), *T(body[2], body[3])), faces_right,
                        ground=[moved(f, T) for f in ground])
    new["measure"] = old["faces"]
    return new


add(
    bush("bush-far-left", 1103, 750, 22, 30, phase=1),
    bush("bush-far-left-2", 1056, 755, 14, 26, n=9, phase=2),
    bush("bush-far-right", 1832, 732, 34, 20, phase=2),
    # the tree between the pink and blue houses stands in the side yard: its trunk's foot on the
    # lawn at y 825, nearer than the pink house's side wall (800), farther than the blue house's
    # front (830) and the fence (842), so the fence hides its trunk and the blue house the right of
    # its crown (README: Trees, bushes and depth)
    O("bush-tree-blue-house", "props", trunk(2159, 740, 2171, 825) + crown("leaves", 2165, 690, 38, 66, 13, 3),
      behind=["house-blue"]),
    # behind the green house's front (its foot is above the wall's): the green house hides its edge
    bush("bush-blue-house-right", 2478, 790, 36, 24, phase=4, behind=["house-green"]),
    bush("bush-blue-house", 2310, 845, 40, 40, phase=5),
    # as the original draws it: against the green house's wall above the steps (its foot 23 px above
    # the wall's; README: Trees, bushes and depth, the one exception)
    bush("bush-green-house", 2548, 918, 46, 35, phase=6),
    # fences (the steps beside them stand in front)
    pickets("fence-yellow-house", [(820, 831, 749), (836, 847, 748), (853, 863, 745), (869, 879, 743)], 835,
            (818, 760, 882, 835), behind=["house-yellow"]),
    # the tree by the yellow house stands nearer (its trunk's foot at 910): it hides the fence
    pickets("fence-yellow-house-side", [(509, 526, 798), (554, 584, 786), (600, 640, 792)], 848,
            (505, 800, 655, 848), rounded=False, behind=["tree-near-yellow-house"]),
    pickets("fence-blue-house-left", [(2089, 2101, 731), (2104, 2118, 735), (2121, 2135, 738), (2138, 2151, 741),
                                      (2154, 2168, 744), (2171, 2185, 747)], 842, (2086, 745, 2190, 842),
            behind=["house-blue"]),
    pickets("fence-blue-house-right", [(2366, 2383, 773), (2390, 2412, 778), (2420, 2441, 782),
                                       (2448, 2473, 787), (2480, 2503, 795)], 878, (2362, 790, 2506, 878)),
    O("fence-green-house", "props", [
        P("board-1-side", [(2733, 926), (2748, 930), (2748, 1128), (2733, 1124)], hid="#8a3f32"),
        P("board-1", [(2700, 930), (2721, 908), (2738, 926), (2738, 1122), (2700, 1110)], hid="#bc6b3c"),
        P("board-2-side", [(2786, 938), (2806, 944), (2806, 1150), (2786, 1146)], like="board-1-side"),
        P("board-2", [(2748, 932), (2776, 918), (2790, 938), (2790, 1146), (2748, 1132)], like="board-1"),
        P("board-3", [(2804, 942), (2816, 936), (2826, 940), (2826, 1156), (2804, 1152)], like="board-1"),
    ]),
)


def flower(oid, cx, cy, r, petals, stem_to, leaves=()):
    import math
    faces = [P("stem", [(cx - 2, cy), (cx + 2, cy), (cx + 2, stem_to), (cx - 2, stem_to)], hid="#3f8a2a")]
    for i, (lx, ly, lrx, lry, rot) in enumerate(leaves):
        faces.append(E(f"leaf-{i + 1}", lx, ly, lrx, lry, rot, hid="#3f8a2a"))
    for k in range(petals):
        a = 2 * math.pi * k / petals - math.pi / 2
        faces.append(C(f"petal-{k + 1}", round(cx + r * 0.62 * math.cos(a), 1), round(cy + r * 0.62 * math.sin(a), 1),
                       round(r * 0.5, 1), hid="petal-1" if k else "#ffffff"))
    faces.append(E("centre", cx, cy, r * 0.42, r * 0.32, hid="#f7c21a"))
    return O(oid, "props", faces)


add(
    flower("flower-white", 160, 1255, 20, 5, 1285, leaves=[(148, 1282, 10, 4, -20), (174, 1282, 10, 4, 20)]),
    flower("flower-pink-left", 272, 1202, 22, 5, 1235, leaves=[(262, 1230, 10, 4, -25), (288, 1230, 11, 4, 25)]),
    flower("flower-red-right", 2155, 938, 13, 5, 958, leaves=[(2148, 956, 6, 3, -20), (2162, 956, 6, 3, 20)]),
    flower("flower-pink-right", 2240, 905, 15, 5, 935, leaves=[(2230, 930, 8, 3, -25), (2252, 930, 8, 3, 25)]),
    O("toy-bone-orange", "props", [
        P("side", [(565, 1205), (590, 1209), (620, 1201), (650, 1188), (668, 1175), (668, 1182), (650, 1196),
                   (620, 1210), (590, 1216), (566, 1214)]),
        P("top", [(565, 1205), (580, 1185), (610, 1172), (635, 1170), (645, 1162), (660, 1162), (668, 1172),
                  (668, 1176), (650, 1189), (620, 1202), (590, 1209)]),
        E("spot-1", 604, 1183, 5, 3.5, -10, hid="#4a2a22"),
        E("spot-2", 590, 1193, 5, 3.5, -10, like="spot-1"),
        E("spot-3", 612, 1191, 5, 3.5, -10, like="spot-1"),
        E("spot-4", 643, 1172, 4, 3, -10, like="spot-1"),
        E("spot-5", 655, 1172, 4, 3, -10, like="spot-1"),
        E("cream-spot", 629, 1184, 9, 5.5, -10, hid="#f6e9d2"),
    ]),
    O("toy-bone-blue", "props", [
        P("side", [(611, 1205), (625, 1198), (640, 1197), (680, 1191), (705, 1203), (713, 1212), (710, 1222),
                   (660, 1232), (650, 1238), (630, 1238), (625, 1226), (612, 1220)]),
        P("top", [(611, 1205), (625, 1198), (640, 1197), (680, 1191), (705, 1203), (713, 1212), (705, 1220),
                  (660, 1225), (640, 1227), (628, 1220), (612, 1214)]),
        E("spot", 670, 1205, 14, 5.5, -4, hid="#f4e04a"),
    ]),
    O("toy-bowl", "props", [
        P("shadow", [(2560, 1225), (2620, 1215), (2640, 1230), (2600, 1240), (2565, 1238)], hid="#5c9a2a"),
        P("side", [(2510, 1190), (2520, 1213), (2560, 1230), (2600, 1232), (2627, 1215), (2627, 1200),
                   (2600, 1212), (2560, 1212), (2520, 1196)]),
        P("rim", [(2510, 1190), (2535, 1173), (2575, 1171), (2610, 1178), (2627, 1200), (2600, 1212),
                  (2560, 1212), (2520, 1196)]),
        P("inside", [(2528, 1188), (2545, 1180), (2580, 1180), (2602, 1186), (2610, 1198), (2595, 1203),
                     (2560, 1203), (2530, 1195)]),
        E("kibble-1", 2548, 1188, 7, 4, hid="#3a8fa8"),
        E("kibble-2", 2572, 1196, 6, 3.5, like="kibble-1"),
    ]),
    O("toy-bone-red", "props", [
        P("shadow", [(2640, 1190), (2730, 1192), (2740, 1205), (2650, 1205)], hid="#5c9a2a"),
        P("bone", [(2622, 1168), (2630, 1143), (2655, 1140), (2667, 1150), (2668, 1165), (2706, 1165),
                   (2712, 1160), (2726, 1166), (2731, 1186), (2722, 1196), (2690, 1195), (2650, 1190),
                   (2626, 1184)]),
        P("bone-top", [(2626, 1166), (2632, 1146), (2655, 1143), (2664, 1152), (2650, 1166), (2640, 1172)]),
        P("gap", [(2657, 1173), (2700, 1173), (2697, 1185), (2660, 1183)], hid="#6aa62a"),
    ]),
)


# Draw order within a layer (far to near) where it differs from the order added above
FIRST = ["house-teal", "house-yellow", "house-red"]
OBJECTS.sort(key=lambda o: FIRST.index(o["id"]) if o["id"] in FIRST else -1)


# Front paths from each house's steps across the lawn to the sidewalk (street layer). Each
# ends at the sidewalk's lawn edge with a subtle joint line, in the sidewalk joints' colour,
# where the path meets the sidewalk.
PATH_JOINT_K = 0.6       # the path's joint line is this share of a sidewalk joint's thickness there


def path_joint(side, y0, y1):
    """The joint line where a path meets the sidewalk: a thin band along the lawn edge from y0 to y1."""
    pa, pb = (LAWN_L, ROAD_L) if side == "l" else (ROAD_R, LAWN_R)
    yh = meet(pa, pb)[1]
    u0 = (1418 if side == "l" else 1422) - yh
    t = max(round(PATH_JOINT_K * (9 + SEAM) * ((y0 + y1) / 2 - yh) / u0 - SEAM, 2), JOINT_MIN_H)
    ys = [y0] + ([712] if y0 < 712 < y1 else []) + [y1]
    r2 = lambda v: round(v, 2)
    return ([(r2(lawn_edge(side, y) - t / 2), y) for y in ys]
            + [(r2(lawn_edge(side, y) + t / 2), y) for y in reversed(ys)])


def path(oid, top, edge, side, y0, y1, like=None, added=False):
    """A front path: its top slab, the edge face below it, the joint line at the sidewalk (y0..y1).
    like: another path whose colours it takes (where the original shows no path to measure);
    added: a path the original does not have (see build_env: it changes no measured colour)."""
    kw = (lambda f: {"like": f"{like}/{f}"}) if like else (lambda f: {})
    side_obj = "sidewalk-left" if side == "l" else "sidewalk-right"
    add_ = {"added": True} if added else {}
    return O(oid, "street", [P("slab", top, **kw("slab"), **add_),
                             P("edge", edge, **(kw("edge") or {"hid": "slab", "k": 0.85}), **add_),
                             P("joint", path_joint(side, y0, y1), like=f"{side_obj}/joint-1", added=True)])


def steps_foot(house, name, side):
    """The foot of a house's steps, where its path starts: the bottom edge of the steps face
    `name`, as points from its near (lowest) end to its far end. Steps seen from the front have
    a level foot; the path's house end is then the steps' end away from the street."""
    pts = next(f for f in next(o for o in OBJECTS if o["id"] == house)["faces"] if f["name"] == name)["pts"]
    xs = [x for x, _ in pts]
    lo, hi = min(xs), max(xs)
    a = max((i for i, p in enumerate(pts) if p[0] - lo <= 5), key=lambda i: pts[i][1])
    b = max((i for i, p in enumerate(pts) if hi - p[0] <= 5), key=lambda i: pts[i][1])
    ring = pts + pts
    one = ring[a:a + (b - a) % len(pts) + 1]                  # a .. b one way round
    two = ring[b:b + (a - b) % len(pts) + 1][::-1]            # a .. b the other way
    chain = max(one, two, key=lambda c: sum(y for _, y in c) / len(c))
    if abs(chain[0][1] - chain[-1][1]) < 1e-6:                # level: the end away from the street
        return [chain[0] if (chain[0][0] < chain[-1][0]) == (side == "l") else chain[-1]]
    return chain if chain[0][1] > chain[-1][1] else chain[::-1]


def street_path(oid, side, house, foot, y_slab, y1, like=None, added=False, was=None):
    """A front path (street-2): from the foot of the house's steps (face `foot`) straight across the
    lawn to the sidewalk, its far edge level with the steps' foot's far end, its slab down to
    y_slab and its edge face to y1. was: the path's (slab, edge) as the original shows them, where
    they are still measured."""
    e = lambda y: lawn_edge(side, y)
    ft = steps_foot(house, foot, side)
    top, (xn, yn) = ft[-1][1], ft[0]
    turn = lambda ya, yb: [(e(712), 712)] if ya < 712 < yb else []
    slab = ft + [(e(top), top)] + turn(top, y_slab) + [(e(y_slab), y_slab)] + ([(xn, y_slab)] if y_slab > yn else [])
    edge = [(xn, y_slab), (e(y_slab), y_slab)] + turn(y_slab, y1) + [(e(y1), y1), (xn, y1)]
    o = path(oid, slab, edge, side, top, y1, like=like, added=added)
    if was:
        for f, pts in zip(o["faces"], was):
            f["src"] = dict(f, pts=[(float(x), float(y)) for x, y in pts])
    return o


L_, R_ = (lambda y: xat(LAWN_L, y)), (lambda y: xat(LAWN_R, y))
_paths = [
    street_path("path-red-house", "l", "house-red", "step-2-riser", 1036, 1042, was=(
        [(306, 1036), (321, 1028), (456, 990), (L_(988), 988), (L_(1036), 1036)],
        [(306, 1036), (L_(1036), 1036), (L_(1042), 1042), (306, 1042)])),
    street_path("path-yellow-house", "l", "house-yellow", "step-2-riser", 879.5, 884.5, was=(
        [(776, 876), (800, 871), (840, 857), (855, 851.7), (870, 863.5), (L_(863.5), 863.5), (L_(879.5), 879.5),
         (776, 879.5)],
        [(776, 879.5), (L_(879.5), 879.5), (L_(884.5), 884.5), (776, 884.5)])),
    street_path("path-teal-house", "l", "house-teal", "step-2-riser", 815, 818, was=(
        [(951, 806), (L_(806), 806), (L_(813), 813), (951, 813)],
        [(951, 813), (L_(813), 813), (L_(816), 816), (951, 816)])),
    # the original's pink-house path is grass-coloured (it measures as lawn), so it takes the
    # teal house's path colours, like the far paths
    street_path("path-pink-house", "r", "house-pink", "steps-front", 807, 810, like="path-teal-house", was=(
        [(R_(800), 800), (1993, 800), (1993, 807), (R_(807), 807)],
        [(R_(807), 807), (1993, 807), (1993, 810), (R_(810), 810)])),
    street_path("path-blue-house", "r", "house-blue", "step-2-riser", 884, 889, was=(
        [(R_(864), 864), (2159, 862), (2212, 882), (R_(882), 882)],
        [(R_(882), 882), (2212, 882), (2212, 887), (R_(887), 887)])),
    street_path("path-green-house", "r", "house-green", "step-2-riser", 1047, 1053, was=(
        [(R_(1000), 1000), (2505, 1000), (2632, 1047), (R_(1047), 1047)],
        [(R_(1047), 1047), (2632, 1047), (2632, 1053), (R_(1053), 1053)])),
    # the far houses' paths (the original shows none: added, in the teal house's path colours);
    # their depth (y extent) scaled from the teal and pink houses' paths
    street_path("path-far-salmon-house", "l", "house-far-salmon", "steps-riser", 745.5, 747,
                like="path-teal-house", added=True),
    street_path("path-far-rose-house", "l", "house-far-rose", "steps-riser", 711.5, 713,
                like="path-teal-house", added=True),
    street_path("path-far-blue-house", "r", "house-far-blue", "steps-riser", 714, 715.7,
                like="path-teal-house", added=True),
    street_path("path-far-pink-house", "r", "house-far-pink", "steps-2-riser", 732.6, 734.6,
                like="path-teal-house", added=True),
    street_path("path-far-yellow-house", "r", "house-far-yellow", "steps-3-riser", 767.7, 770.1,
                like="path-teal-house", added=True),
]
_at = next(i for i, o in enumerate(OBJECTS) if o["id"] == "road")
OBJECTS[_at:_at] = _paths


def path_near(oid):
    """The height of a path's near edge (its edge face's lowest point)."""
    return max(y for f in next(o for o in OBJECTS if o["id"] == oid)["faces"] if f["name"] in ("slab", "edge")
               for _, y in f["pts"])


# Every house's mailbox, placed by the one rule beside its own path (street-4, street-7). The two near
# ones are the original's, measured (post and shadow on the ground); the far ones the original's
# moved, re-made at the rule's size and still measured where the original shows them; the yellow
# and blue houses' are added.
_mailboxes = [
    place_mailbox("mailbox-left", "l", (390, 850, 564, 960), True, path_near("path-red-house"), ground=[
        P("shadow", [(430, 1120), (550, 1125), (535, 1148), (407, 1150)], hid="#4f8d24"),
        P("post", [(455, 955), (500, 955), (500, 1143), (457, 1143)]),
        P("post-side", [(485, 955), (500, 955), (500, 1143), (485, 1143)])]),
    place_mailbox("mailbox-right", "r", (2318, 850, 2505, 962), False, path_near("path-green-house"), ground=[
        P("shadow", [(2400, 1120), (2530, 1122), (2560, 1148), (2410, 1150)], hid="#4f8d24"),
        P("post", [(2393, 955), (2443, 955), (2443, 1146), (2395, 1146)]),
        P("post-side", [(2425, 955), (2443, 955), (2443, 1146), (2425, 1146)])]),
    street_mailbox("mailbox-far-left-1", "l", path_near("path-teal-house"),
                   was=((965, 731, 1047, 777), (995, 777, 1013, 832))),
    street_mailbox("mailbox-far-left-2", "l", path_near("path-far-salmon-house"),
                   was=((1186, 696, 1228, 721), (1203, 721, 1212, 766))),
    street_mailbox("mailbox-far-left-3", "l", path_near("path-far-rose-house"),
                   was=((1300, 676, 1330, 693), (1310, 693, 1315, 720))),
    street_mailbox("mailbox-far-right-1", "r", path_near("path-far-blue-house"),
                   was=((1622, 674, 1650, 690), (1633, 690, 1637, 710))),
    street_mailbox("mailbox-far-right-2", "r", path_near("path-far-pink-house"),
                   was=((1667, 681, 1695, 697), (1678, 697, 1684, 722))),
    street_mailbox("mailbox-far-right-3", "r", path_near("path-far-yellow-house"),
                   was=((1720, 693, 1765, 718), (1740, 718, 1747, 768))),
    street_mailbox("mailbox-far-right-4", "r", path_near("path-pink-house"),
                   was=((1835, 725, 1908, 763), (1866, 763, 1881, 802))),
    street_mailbox("mailbox-yellow-house", "l", path_near("path-yellow-house"),
                   flags=("mailbox-left", "mailbox-far-left-1")),
    street_mailbox("mailbox-blue-house", "r", path_near("path-blue-house"),
                   flags=("mailbox-right", "mailbox-far-right-4")),
]
_at = next(i for i, o in enumerate(OBJECTS) if o["id"] == "fence-green-house") + 1
OBJECTS[_at:_at] = _mailboxes


# ==========================================================================
# Street layout: each house's door, front steps, path and mailbox (#841). check_street() in
# build_env.py verifies the "Street layout invariants" in README.md against this table.
# ==========================================================================


def lot(house, side, path, mailbox, steps, front=None, foot=None):
    """One house's lot: its steps' faces, the face(s) centred under its door (front) and the
    face whose bottom edge is the steps' foot, where its path starts."""
    return dict(house=house, side=side, door="door-frame", steps=steps, front=front or steps,
                foot=foot or (steps[-1] if steps else None), path=path, mailbox=mailbox)


STREET = [
    lot("house-red", "l", "path-red-house", "mailbox-left", NEAR_STEPS, ["steps-side"], "step-2-riser"),
    lot("house-yellow", "l", "path-yellow-house", "mailbox-yellow-house", NEAR_STEPS, ["steps-side"],
        "step-2-riser"),
    lot("house-teal", "l", "path-teal-house", "mailbox-far-left-1", NEAR_STEPS, ["steps-side"], "step-2-riser"),
    lot("house-far-salmon", "l", "path-far-salmon-house", "mailbox-far-left-2", FAR_STEPS),
    lot("house-far-rose", "l", "path-far-rose-house", "mailbox-far-left-3", FAR_STEPS),
    lot("house-far-blue", "r", "path-far-blue-house", "mailbox-far-right-1", FAR_STEPS),
    lot("house-far-pink", "r", "path-far-pink-house", "mailbox-far-right-2", FAR_STEPS_2),
    lot("house-far-yellow", "r", "path-far-yellow-house", "mailbox-far-right-3", FAR_STEPS_3),
    lot("house-pink", "r", "path-pink-house", "mailbox-far-right-4", ["steps-tread", "steps-front"]),
    lot("house-blue", "r", "path-blue-house", "mailbox-blue-house", NEAR_STEPS, ["steps-side"], "step-2-riser"),
    lot("house-green", "r", "path-green-house", "mailbox-right", NEAR_STEPS, ["steps-side"], "step-2-riser"),
]
