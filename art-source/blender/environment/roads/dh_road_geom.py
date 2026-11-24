"""Road-kit geometry for every road tile type (#802; look A approved, #836), as 2-D
shapes in TILE-LOCAL metres: x east, y north (Core's X and Z), origin at the
tile centre. Pure Python + shapely, no Blender, so the geometry check
(check_geometry.py) can run on exactly what the builders mesh.

Every measurement comes from Core, read from the C# source at import time
(WorldDimensions.cs for the numbers, TileCatalog.cs for each type's road
edges), never re-typed here. Core's own derivations are followed exactly:

  road          6 m wide, centreline through the tile centre to each road edge
  curb          a flat concrete band CURB_W wide along the asphalt edge
                (Derek's lawn edge set 1, #801), inside the 0.75 m verge
  verge         lawn, from the curb to the sidewalk
  sidewalk      2 m, centreline RoadWidth/2 + GrassVergeWidth + SidewalkWidth/2
                = 4.75 m from the road centreline (Sidewalk / CrosswalkOffset)
  crosswalk     one band per road arm of a FourWay or Tee, CrosswalkWidth (3 m)
                along the arm, centred 4.75 m from the tile centre
                (TileCrosswalkGeometry); paint only on the asphalt
  bend          the road centreline turns on a RoadBendCornerRadius (5 m) arc
                centred R along each arm from the tile centre; sidewalks are
                concentric arcs at R -/+ 4.75 (WalkNetwork.BuildBendArc)
  cul-de-sac    the walk path runs straight along both sides to the tile
                centre, then round a CulDeSacBulbRadius (9 m) arc whose centre
                lies sqrt(9^2 - 4.75^2) back down the open road
                (WalkNetwork.BuildCulDeSacTurnaround). The pavement follows
                that walk path: the sidewalk is the path +-1 m and the asphalt
                is the region it encloses, eroded by the 1.75 m from the walk
                line to the curb, so dogs walk exactly where Core sends them.

Marking dimensions (MARKS) are not in Core; they are the kit's own and only
place paint, never change a footprint.
"""
import math
import os
import re
import random
import zlib

from shapely.geometry import LineString, Polygon, box, Point  # noqa: F401
from shapely.ops import unary_union, linemerge, substring

HERE = os.path.dirname(os.path.abspath(__file__))
CORE_WORLD = os.path.normpath(os.path.join(HERE, "..", "..", "..", "..", "Assets", "Scripts", "Core", "World"))


def _core_consts():
    src = open(os.path.join(CORE_WORLD, "WorldDimensions.cs")).read()
    return {k: float(v) for k, v in re.findall(r"public const float (\w+)\s*=\s*([-0-9.]+)f\s*;", src)}


def _core_edges():
    src = open(os.path.join(CORE_WORLD, "TileCatalog.cs")).read()
    out = {}
    for name, body in re.findall(r"new TileTypeDefinition\(TileType\.(\w+),\s*(.*?)\)\s*,", src, re.S):
        out[name] = tuple(re.findall(r"TileEdge\.(North|South|East|West)", body))
    return out


WD = _core_consts()
TILE_EDGES = _core_edges()                       # {type: ("North", ...)}, includes GreenSpace
ROAD_TYPES = [t for t, e in TILE_EDGES.items() if e]

TILE = WD["TileSize"]
HALF = TILE / 2
ROAD_HALF = WD["RoadWidth"] / 2
VERGE = WD["GrassVergeWidth"]
SW = WD["SidewalkWidth"]
OFF = ROAD_HALF + VERGE + SW / 2                 # Sidewalk offset == TileCrosswalkGeometry.CrosswalkOffset
CW = WD["CrosswalkWidth"]
BEND_R = WD["RoadBendCornerRadius"]
BULB_R = WD["CulDeSacBulbRadius"]
XW_ACROSS = WD["RoadWidth"] + 2 * VERGE         # TileCrosswalkGeometry's AcrossRoadSpan

CURB_W = 0.15                                    # flat curb band (dh_ground.CURB_W, Derek's edge set 1)
ARC_STEP = math.radians(3.0)                     # arc tessellation: 3 deg (sagitta 1.1 cm at 10.75 m)
OUT = 5.0                                        # paths run this far past the tile edge before clipping

DIRS = {"North": (0.0, 1.0), "South": (0.0, -1.0), "East": (1.0, 0.0), "West": (-1.0, 0.0)}

MARKS = {
    "centre_w": 0.10,          # one centre-line stripe
    "double_gap": 0.10,        # gap between the two stripes of a double line
    "dash": 2.0,               # dashed centre line: 2 m dash ...
    "dash_period": 6.0,        # ... every 6 m (divides 30, so tile edges fall mid-gap)
    "stop_gap": 0.6,           # crosswalk outer edge -> stop line
    "stop_w": 0.40,
    "lane_inset": 0.15,        # stop line starts this far from the centreline
    "curb_inset": 0.20,        # paint stops this far short of the curb
    "xw_line_w": 0.30,         # two-line crosswalk: line width
    "bar_w": 0.45,             # continental bars
    "bar_gap": 0.45,
    "cds_centre_end": 8.0,     # a cul-de-sac's centre line ends this far from the tile centre
}


def _v(a, s=1.0):
    return (a[0] * s, a[1] * s)


def _add(*ps):
    return (sum(p[0] for p in ps), sum(p[1] for p in ps))


def _perp(d):
    return (-d[1], d[0])


def arc_pts(c, r, a0, a1, step=ARC_STEP):
    """Points on the circle (c, r) from angle a0 to a1 (radians), inclusive."""
    n = max(2, int(math.ceil(abs(a1 - a0) / step)))
    return [(c[0] + r * math.cos(a0 + (a1 - a0) * i / n), c[1] + r * math.sin(a0 + (a1 - a0) * i / n))
            for i in range(n + 1)]


def _ang(p, c):
    return math.atan2(p[1] - c[1], p[0] - c[0])


def _minor(a0, a1):
    s = a1 - a0
    while s > math.pi:
        s -= 2 * math.pi
    while s < -math.pi:
        s += 2 * math.pi
    return a0, a0 + s


def _arc_between(p0, p1, c, r):
    a0, a1 = _minor(_ang(p0, c), _ang(p1, c))
    pts = arc_pts(c, r, a0, a1)
    pts[0], pts[-1] = p0, p1                     # weld exactly, like WalkNetwork.AddArc
    return pts


def kind_of(ttype):
    e = TILE_EDGES[ttype]
    if len(e) == 4:
        return "fourway"
    if len(e) == 3:
        return "tee"
    if len(e) == 2:
        a, b = (DIRS[x] for x in e)
        return "straight" if a[0] + b[0] == 0 and a[1] + b[1] == 0 else "turn"
    if len(e) == 1:
        return "culdesac"
    return "green"


TILE_BOX = box(-HALF, -HALF, HALF, HALF)


def _rect_local(d, s0, s1, l0, l1):
    """Rectangle in an arm's frame: along d from s0 to s1, lateral (left of d)
    from l0 to l1."""
    p = _perp(d)
    pts = [_add(_v(d, s), _v(p, l)) for s, l in ((s0, l0), (s1, l0), (s1, l1), (s0, l1))]
    return Polygon(pts)


class Layout:
    """Everything the builders and the checker need for one tile type."""

    def __init__(self, ttype):
        self.type = ttype
        self.kind = kind_of(ttype)
        self.edges = [DIRS[e] for e in TILE_EDGES[ttype]]
        self.edge_names = list(TILE_EDGES[ttype])
        self.centre_paths = []        # [(LineString, "fit" | "edge")]
        self.walk = []                # Core sidewalk centrelines (LineStrings), for checking
        self.crosswalk_arms = []      # arm directions that carry a crosswalk
        self.stop_arms = []
        self.corner_c = None
        self.bulb = None
        getattr(self, "_" + self.kind)()
        if self.kind != "green":
            self.asphalt = self.asphalt.intersection(TILE_BOX)
            curb_out = self.asphalt_raw.buffer(CURB_W, join_style="mitre", mitre_limit=10)
            self.curb = curb_out.difference(self.asphalt_raw).intersection(TILE_BOX)
            self.sidewalk_full = self.sidewalk_raw.difference(curb_out)      # not clipped to the tile
            self.sidewalk = self.sidewalk_full.intersection(TILE_BOX)
            self.curb_line = self._curb_centreline()

    # -- straight-family: FourWay, Tee, Straight ---------------------------
    def _arms(self):
        A = [_rect_local(d, -ROAD_HALF, HALF + OUT, -ROAD_HALF, ROAD_HALF) for d in self.edges]
        self.asphalt_raw = unary_union(A)
        self.asphalt = self.asphalt_raw
        sw = []
        has = lambda v: any(abs(v[0] - e[0]) < 1e-9 and abs(v[1] - e[1]) < 1e-9 for e in self.edges)  # noqa: E731
        for d in self.edges:
            p = _perp(d)
            for side in (1, -1):
                lat = _v(p, side)
                s0 = OFF if has(lat) else 0.0            # stop at the box corner, or run through
                ln = LineString([_add(_v(d, s0), _v(lat, OFF)), _add(_v(d, HALF + OUT), _v(lat, OFF))])
                self.walk.append(ln.intersection(TILE_BOX))
                sw.append(ln.buffer(SW / 2, cap_style="square", join_style="mitre"))
                if has(lat):                             # box corner: pave curb to sidewalk outer edge
                    sw.append(_rect_local(d, ROAD_HALF, OFF + SW / 2, side * ROAD_HALF, side * (OFF + SW / 2))
                              if side > 0 else
                              _rect_local(d, ROAD_HALF, OFF + SW / 2, -(OFF + SW / 2), -ROAD_HALF))
        if self.kind in ("fourway", "tee"):
            for d in self.edges:
                self.crosswalk_arms.append(d)
                self.stop_arms.append(d)
                for side in (1, -1):                     # crosswalk landing across the verge
                    l0, l1 = sorted((side * ROAD_HALF, side * (ROAD_HALF + VERGE)))
                    sw.append(_rect_local(d, OFF - SW / 2, OFF + SW / 2, l0, l1))
                # the Core crosswalk edge: box corner to box corner across this arm
                p = _perp(d)
                self.walk.append(LineString([_add(_v(d, OFF), _v(p, OFF)), _add(_v(d, OFF), _v(p, -OFF))]))
        self.sidewalk_raw = unary_union(sw)
        if self.kind == "straight":
            d = self.edges[0]
            self.centre_paths.append((LineString([_v(d, -HALF), _v(d, HALF)]), "fit"))
        else:
            for d in self.edges:
                self.centre_paths.append((LineString([_v(d, HALF), _v(d, self.stop_far())]), "edge"))

    def stop_far(self):
        return OFF + CW / 2 + MARKS["stop_gap"] + MARKS["stop_w"]

    _fourway = _tee = _straight = _arms

    # -- bend ----------------------------------------------------------------
    def _turn(self):
        a, b = self.edges
        R = BEND_R
        c = _add(_v(a, R), _v(b, R))
        self.corner_c = c
        cl = [_v(a, HALF + OUT), _v(a, R)] + _arc_between(_v(a, R), _v(b, R), c, R)[1:-1] + [_v(b, R),
                                                                                              _v(b, HALF + OUT)]
        centre = LineString(cl)
        self.asphalt_raw = centre.buffer(ROAD_HALF, cap_style="flat", join_style="round", quad_segs=30)
        self.asphalt = self.asphalt_raw
        sw = []
        for side, r in ((1, R - OFF), (-1, R + OFF)):   # inner (toward c), outer
            pa = _add(_v(a, R), _v(b, side * OFF))
            pb = _add(_v(b, R), _v(a, side * OFF))
            arc = _arc_between(pa, pb, c, abs(r))
            path = [_add(_v(a, HALF + OUT), _v(b, side * OFF))] + arc + [_add(_v(b, HALF + OUT), _v(a, side * OFF))]
            ln = LineString(path)
            self.walk.append(ln.intersection(TILE_BOX))
            sw.append(ln.buffer(SW / 2, cap_style="flat", join_style="round", quad_segs=30))
        self.sidewalk_raw = unary_union(sw)
        self.centre_paths.append((LineString([_v(a, HALF)] + cl[1:-1] + [_v(b, HALF)]), "fit"))

    # -- cul-de-sac ------------------------------------------------------------
    def _culdesac(self):
        d = self.edges[0]
        p = _perp(d)
        back = math.sqrt(BULB_R ** 2 - OFF ** 2)
        cc = _v(d, back)
        self.bulb = (cc, BULB_R)
        e1, e2 = _v(p, OFF), _v(p, -OFF)
        arc = _arc_between(e1, e2, cc, BULB_R)
        far = HALF + OUT
        path = [_add(_v(d, far), e1)] + arc + [_add(_v(d, far), e2)]
        ln = LineString(path)
        self.walk.append(ln.intersection(TILE_BOX))
        loop = Polygon(path)
        self.asphalt_raw = loop.buffer(-(OFF - ROAD_HALF), join_style="mitre", mitre_limit=10)
        self.asphalt = self.asphalt_raw
        self.sidewalk_raw = ln.buffer(SW / 2, cap_style="flat", join_style="round", quad_segs=30)
        self.centre_paths.append((LineString([_v(d, HALF), _v(d, MARKS["cds_centre_end"])]), "edge"))

    def _green(self):
        self.asphalt = self.curb = self.sidewalk = Polygon()
        self.curb_line = []

    # -- curb centreline (for strip meshing with continuous U) -----------------
    def _curb_centreline(self):
        ring = self.asphalt_raw.buffer(CURB_W / 2, join_style="mitre", mitre_limit=10)
        rings = [ring.exterior] + list(ring.interiors) if ring.geom_type == "Polygon" else \
            [g.exterior for g in ring.geoms]
        inner = TILE_BOX
        lines = []
        for r in rings:
            g = r.intersection(inner)
            g = linemerge(g) if g.geom_type == "MultiLineString" else g
            for ln in (g.geoms if hasattr(g, "geoms") else [g]):
                if ln.length < 1e-6:
                    continue
                # drop pieces that run ALONG the tile edge (the outside of an arm end)
                xs, ys = zip(*ln.coords)
                if (max(xs) - min(xs) < 1e-6 and abs(abs(xs[0]) - HALF) < 1e-6) or \
                        (max(ys) - min(ys) < 1e-6 and abs(abs(ys[0]) - HALF) < 1e-6):
                    continue
                lines.append(ln)
        return lines


# ---------------------------------------------------------------------------
# markings
# ---------------------------------------------------------------------------
def _dash_intervals(L, mode, pattern):
    """[(s0, s1)] along a path of length L for the centre-line pattern.
    "fit": both ends on tile edges; the period is stretched (<= a few %) so a
    whole number of periods fits and both ends fall mid-gap.
    "edge": s = 0 on the tile edge; world-phase period, cut at L."""
    if pattern == "double" or pattern == "solid":
        return [(0.0, L)]
    per, dash = MARKS["dash_period"], MARKS["dash"]
    if mode == "fit":
        n = max(1, round(L / per))
        per = L / n
    out = []
    k = 0
    while True:
        s0 = k * per + (per - dash) / 2
        if s0 >= L:
            break
        out.append((s0, min(s0 + dash, L)))
        k += 1
    return out


def centre_lines(lay, pattern):
    """[(LineString, width, swatch)] centre-line stripes."""
    out = []
    if pattern in (None, "none") or lay.kind == "green":
        return out
    for path, mode in lay.centre_paths:
        for s0, s1 in _dash_intervals(path.length, mode, pattern):
            seg = substring(path, s0, s1)
            if pattern == "double":
                o = MARKS["double_gap"] / 2 + MARKS["centre_w"] / 2
                for side in ("left", "right"):
                    out.append((seg.offset_curve(o if side == "left" else -o, quad_segs=30, join_style="mitre"),
                                MARKS["centre_w"], "yellow"))
            else:
                out.append((seg, MARKS["centre_w"] * (1.2 if pattern == "dashed" else 1.0), "yellow"))
    return out


def crosswalk_lines(lay, style):
    """[(LineString, width, swatch)] for every crosswalk on the tile. Paint
    covers the Core band's along-arm extent exactly (OFF +- CW/2) and stays
    on the asphalt (curb_inset short of each curb)."""
    out = []
    span = ROAD_HALF - MARKS["curb_inset"]
    lw, bw, bg = MARKS["xw_line_w"], MARKS["bar_w"], MARKS["bar_gap"]
    for d in lay.crosswalk_arms:
        p = _perp(d)
        s_in, s_out = OFF - CW / 2, OFF + CW / 2
        P = lambda s, l: _add(_v(d, s), _v(p, l))  # noqa: E731
        if style in ("two-line", "ladder"):
            for s in (s_in + lw / 2, s_out - lw / 2):
                out.append((LineString([P(s, -span), P(s, span)]), lw, "white"))
        if style in ("bars", "ladder"):
            b0, b1 = (s_in, s_out) if style == "bars" else (s_in + lw + 0.15, s_out - lw - 0.15)
            n = int((2 * span + bg) // (bw + bg))
            tot = n * bw + (n - 1) * bg
            for i in range(n):
                lc = -tot / 2 + bw / 2 + i * (bw + bg)
                out.append((LineString([P(b0, lc), P(b1, lc)]), bw, "white"))
    return out


def stop_lines(lay):
    out = []
    s = OFF + CW / 2 + MARKS["stop_gap"] + MARKS["stop_w"] / 2
    for d in lay.stop_arms:
        q = (-d[1], d[0])          # right-hand side of a vehicle heading into the tile (heading -d)
        a = _add(_v(d, s), _v(q, MARKS["lane_inset"]))
        b = _add(_v(d, s), _v(q, ROAD_HALF - MARKS["curb_inset"]))
        out.append((LineString([a, b]), MARKS["stop_w"], "white"))
    return out


# ---------------------------------------------------------------------------
# seeded per-tile wear: patches and sealed cracks (never repeat, never cross
# a tile border)
# ---------------------------------------------------------------------------
def features(lay, coord, wear):
    """wear = {"patches": (lo, hi), "cracks": (lo, hi), "seed": n}.
    Returns (patches [Polygon], cracks [(LineString, width)]), tile-local."""
    if lay.kind == "green" or not wear:
        return [], []
    rng = random.Random(zlib.crc32(("%d,%d,%d,%s" % (wear["seed"], coord[0], coord[1], lay.type)).encode()))
    safe = lay.asphalt.buffer(-0.35).intersection(box(-HALF + 0.5, -HALF + 0.5, HALF - 0.5, HALF - 0.5))
    minx, miny, maxx, maxy = safe.bounds
    patches = []
    for _ in range(rng.randint(*wear["patches"])):
        for _try in range(60):
            x, y = rng.uniform(minx, maxx), rng.uniform(miny, maxy)
            along_x = abs(x) > abs(y) if lay.kind != "straight" else abs(lay.edges[0][0]) > 0
            a, c = rng.uniform(1.0, 3.2), rng.uniform(0.8, 2.0)
            w, h = (a, c) if along_x else (c, a)
            r = box(x - w / 2, y - h / 2, x + w / 2, y + h / 2)
            if safe.contains(r) and not any(r.buffer(0.3).intersects(q) for q in patches):
                patches.append(r)
                break
    cracks = []
    inner = lay.asphalt.buffer(-0.2)
    for _ in range(rng.randint(*wear["cracks"])):
        for _try in range(60):
            x, y = rng.uniform(minx, maxx), rng.uniform(miny, maxy)
            if not safe.contains(Point(x, y)):
                continue
            ang = rng.uniform(0, 2 * math.pi)
            pts = [(x, y)]
            for _k in range(rng.randint(6, 16)):
                ang += rng.uniform(-0.6, 0.6)
                st = rng.uniform(0.25, 0.5)
                nxt = (pts[-1][0] + st * math.cos(ang), pts[-1][1] + st * math.sin(ang))
                if not inner.contains(Point(nxt)) or not TILE_BOX.buffer(-0.5).contains(Point(nxt)):
                    break
                pts.append(nxt)
            if len(pts) >= 4:
                cracks.append((LineString(pts), rng.uniform(0.045, 0.07)))
                break
    return patches, cracks


def tile_centre(coord):
    return (coord[0] * TILE, coord[1] * TILE)


# A valid 3 x 3 neighbourhood of mixed types (checked by check_geometry.py):
# every shared edge is road-to-road or gap-to-gap.
BLOCK = {
    (-1, 1): "TurnSE", (0, 1): "TeeSouth", (1, 1): "TurnSW",
    (-1, 0): "TeeEast", (0, 0): "FourWay", (1, 0): "TeeWest",
    (-1, -1): "CulDeSacNorth", (0, -1): "StraightNS", (1, -1): "TurnNE",
}
# The starter tile and its neighbours for the lot render.
STARTER = {
    (0, 0): "FourWay", (0, 1): "StraightNS", (0, -1): "StraightNS", (1, 0): "StraightEW", (-1, 0): "StraightEW",
    (-1, -1): "GreenSpace", (1, -1): "GreenSpace", (-1, 1): "GreenSpace", (1, 1): "GreenSpace",
}
