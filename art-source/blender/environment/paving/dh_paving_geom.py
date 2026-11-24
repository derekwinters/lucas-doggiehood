"""Paving geometry for #803 (P1 approved, #851): the road kit's sidewalk cut into concrete
slabs, plus front walkways and driveways. Pure Python + shapely, no Blender,
so check_paving.py checks exactly what dh_paving.py meshes.

The sidewalk FOOTPRINT is not re-derived here: it is dh_road_geom.Layout's
`sidewalk` (the approved road kit's, #836), which matches Core exactly
(check_geometry.py). This module only decides where the JOINTS go.

Joint rules (every tile type):
  * a tile edge is always a joint, so joints meet across tile borders and a
    sidewalk never has a slab straddling two tiles;
  * a run of sidewalk is cut at fixed STATIONS first, then each stretch
    between two stations is divided into n = round(length / spacing) equal
    slabs (spacing = 1.5 or 2.0 m by option), so no sliver slab is left:
      FourWay / Tee box corner  the corner square [3.15, 5.75]^2 is its own
                                slabs: a 2 x 2 m main slab, the two 0.6 x 2 m
                                crosswalk landings, and a 0.6 m square; the
                                runs start at 5.75 m
      Tee closed side           stations at 3.75 and 5.75 m either side of
                                the stem, so the slab opposite each crosswalk
                                is exactly the 2 m landing; the 0.6 m landing
                                strip in the verge is its own slab
      Straight                  a station at the tile centre
      bend, outer arc           stations at the tangent points; radial joints
                                on the arc
      bend, inner corner        stations 5.75 m from the corner on each arm;
                                the rounded corner between them is one slab
      cul-de-sac                stations at the two kinks where the straight
                                runs meet the 9 m arc (cut on the bisector);
                                radial joints on the arc
  * joints are cut perpendicular to the run (radial on curves).

Slabs are found by polygonizing the sidewalk outline together with the joint
lines, so their union is the sidewalk exactly and they never overlap.

Each slab carries a MAPPING for its atlas UVs: a run slab maps u across the
2 m band and v along the run between its two stations (so the joint drawn on
the slot border lands on the slab's real edges, also on curves); a corner or
landing slab maps its bounding box.
"""
import math
import os
import sys
import zlib

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.normpath(os.path.join(HERE, "..", "roads")))
import dh_road_geom as R  # noqa: E402
from shapely.geometry import LineString, Point, Polygon, box  # noqa: E402,F401
from shapely.ops import polygonize, unary_union  # noqa: E402

HALF, OFF, SW, ROAD_HALF, VERGE = R.HALF, R.OFF, R.SW, R.ROAD_HALF, R.VERGE
BAND_IN = OFF - SW / 2                       # 3.75: sidewalk inner edge
BAND_OUT = OFF + SW / 2                      # 5.75: sidewalk outer edge
VERGE_IN = ROAD_HALF + R.CURB_W              # 3.15: verge side of the curb band
CUT_REACH = SW / 2 + 0.25                    # joint segments reach past the band edges
TANGENT_EPS = 0.3                            # chord used for a joint's direction (bisector at kinks)
SLIVER = 1e-6
N_VARIANTS = 8                               # dh_paving_tex.N_VARIANTS
TONE_RANGE = (0.95, 1.0)                     # per-slab vertex-colour tone
HASH_SEED = 803

# walkway and driveway
WALK_W = SW                                  # Core: WalkEdge(door, attach, FrontWalkway, SidewalkWidth)
DRIVE_FLARE = 0.6                            # flared apron: each side widens this much at the road edge
RIBBON_W = 0.6                               # ribbon driveway: each wheel strip
RIBBON_TRACK = 1.6                           # ribbon driveway: strip centre to strip centre
DRIVE_PANEL = 3.0                            # driveway slab target length
WALK_PANEL_MAX = 2.0                         # walkway slab target length (poured)
MIN_SLAB = 0.75                              # no run slab shorter than this (half the 1.5 m spacing)
DRIVE_SLABS = 2                              # driveway slabs across (2 x 1.5 m = 3.0 m)


# ---------------------------------------------------------------------------
# runs: (centreline LineString, [(s0, s1, mode)]) with mode "run" or "box"
# ---------------------------------------------------------------------------
def _divide(a, b, spacing):
    n = max(1, int(round((b - a) / spacing)))
    return [a + (b - a) * i / n for i in range(n + 1)]


def _intervals(stations_fixed, spacing, box_spans=(), soft=()):
    """Fixed stations -> sorted list of (s0, s1, mode). A stretch shorter
    than MIN_SLAB between two stations is a sliver: a SOFT station at either
    end of it is dropped, merging the sliver into its neighbour. Two hard
    stations closer than MIN_SLAB stay (check_paving reports the sliver)."""
    st = sorted(set(round(s, 9) for s in stations_fixed))
    soft = {round(s, 9) for s in soft}
    changed = True
    while changed:
        changed = False
        for a, b in zip(st, st[1:]):
            if b - a < MIN_SLAB - 1e-9:
                drop = a if a in soft else (b if b in soft else None)
                if drop is not None:
                    st.remove(drop)
                    changed = True
                    break
    out = []
    for a, b in zip(st, st[1:]):
        if any(abs(a - x0) < 1e-6 and abs(b - x1) < 1e-6 for x0, x1 in box_spans):
            out.append((a, b, "box"))
            continue
        pts = _divide(a, b, spacing)
        out += [(p, q, "run") for p, q in zip(pts, pts[1:])]
    return out


def _has(edges, v):
    return any(abs(v[0] - e[0]) < 1e-9 and abs(v[1] - e[1]) < 1e-9 for e in edges)


def _frontage_stations(line, frontage):
    """Distances along `line` of the frontage points that lie on its band."""
    out = []
    for p in frontage:
        q = Point(p)
        s = line.project(q)
        if 1e-6 < s < line.length - 1e-6 and q.distance(line.interpolate(s)) <= SW / 2 + 1e-6:
            out.append(s)
    return out


def runs_and_cuts(lay, spacing, frontage=()):
    """[(line, intervals)], [extra cut LineStrings] for one Layout.
    `frontage`: tile-local points on a sidewalk (the long-side edges, and a
    driveway's centre joint, of each lot's walkway and driveway); each one
    becomes a fixed joint station on the run it lies on."""
    runs, extra = [], []
    if lay.kind == "straight":                     # one run per side, the tile centre a SOFT joint
        d = lay.edges[0]
        p = R._perp(d)
        for side in (1, -1):
            lat = R._v(p, side)
            line = LineString([R._add(R._v(d, -HALF), R._v(lat, OFF)), R._add(R._v(d, HALF), R._v(lat, OFF))])
            runs.append((line, _intervals([0.0, HALF, 2 * HALF] + _frontage_stations(line, frontage), spacing,
                                          soft=[HALF])))
    elif lay.kind in ("fourway", "tee"):
        xw = True
        for d in lay.edges:
            p = R._perp(d)
            for side in (1, -1):
                lat = R._v(p, side)
                if _has(lay.edges, lat):
                    start, fixed = BAND_OUT, [BAND_OUT, HALF]
                    # corner square: a joint at 3.75 along this arm (the
                    # perpendicular arm adds the other)
                    extra.append(LineString([R._add(R._v(d, BAND_IN), R._v(lat, ROAD_HALF)),
                                             R._add(R._v(d, BAND_IN), R._v(lat, BAND_OUT + 0.2))]))
                else:
                    start, fixed = 0.0, [0.0, HALF]
                    if xw:
                        fixed += [BAND_IN, BAND_OUT]
                        # landing strip (in the verge) is its own slab
                        extra.append(LineString([R._add(R._v(d, BAND_IN - 0.05), R._v(lat, BAND_IN)),
                                                 R._add(R._v(d, BAND_OUT + 0.05), R._v(lat, BAND_IN))]))
                line = LineString([R._add(R._v(d, start), R._v(lat, OFF)), R._add(R._v(d, HALF), R._v(lat, OFF))])
                runs.append((line, _intervals([s - start for s in fixed] + _frontage_stations(line, frontage),
                                              spacing)))
    elif lay.kind == "turn":
        for i, w in enumerate(lay.walk):              # inner first, then outer (Layout._turn order)
            L = w.length
            if i == 0:                                # inner: the rounded corner is one slab
                k = HALF - BAND_OUT
                runs.append((w, _intervals([0.0, k, L - k, L] + _frontage_stations(w, frontage), spacing,
                                           box_spans=[(k, L - k)])))
            else:                                     # outer: stations at the tangent points
                k = HALF - R.BEND_R
                runs.append((w, _intervals([0.0, k, L - k, L] + _frontage_stations(w, frontage), spacing)))
    elif lay.kind == "culdesac":
        w = lay.walk[0]
        L = w.length
        runs.append((w, _intervals([0.0, HALF, L - HALF, L] + _frontage_stations(w, frontage), spacing)))
    return runs, extra


def _frame(line, s):
    """Point and unit tangent at distance s along line."""
    q = line.interpolate(s)
    tx = ty = 0.0
    for a, b in ((line.interpolate(max(0.0, s - TANGENT_EPS)), q), (q, line.interpolate(min(line.length,
                                                                                            s + TANGENT_EPS)))):
        dx, dy = b.x - a.x, b.y - a.y
        m = math.hypot(dx, dy)
        if m > 1e-9:                      # average the unit chords either side: the bisector at a kink
            tx, ty = tx + dx / m, ty + dy / m
    m = math.hypot(tx, ty)
    return (q.x, q.y), (tx / m, ty / m)


def cut_segment(line, s):
    (qx, qy), (tx, ty) = _frame(line, s)
    nx, ny = -ty, tx
    return LineString([(qx - nx * CUT_REACH, qy - ny * CUT_REACH), (qx + nx * CUT_REACH, qy + ny * CUT_REACH)])


def joint_lines(lay, spacing, frontage=()):
    runs, extra = runs_and_cuts(lay, spacing, frontage)
    cuts = list(extra)
    for line, iv in runs:
        L = line.length
        for s0, s1, _ in iv:
            for s in (s0, s1):
                if 1e-6 < s < L - 1e-6 or (s <= 1e-6 and not _at_tile_edge(line.coords[0])) or \
                        (s >= L - 1e-6 and not _at_tile_edge(line.coords[-1])):
                    cuts.append(cut_segment(line, s))
    return runs, cuts


def _at_tile_edge(p):
    return abs(abs(p[0]) - HALF) < 1e-6 or abs(abs(p[1]) - HALF) < 1e-6


# ---------------------------------------------------------------------------
# slabs
# ---------------------------------------------------------------------------
class Slab:
    __slots__ = ("poly", "mode", "line", "s0", "s1", "length", "width")

    def __init__(self, poly, mode, line=None, s0=0.0, s1=0.0, width=SW):
        self.poly, self.mode, self.line, self.s0, self.s1, self.width = poly, mode, line, s0, s1, width
        if mode == "run":
            self.length = s1 - s0
        else:
            x0, y0, x1, y1 = poly.bounds
            self.length = max(x1 - x0, y1 - y0)

    def uv(self, x, y):
        """Slab-local (u, v) in 0..1 for a tile-local point on this slab."""
        if self.mode == "run":
            s = self.line.project(Point(x, y))
            (qx, qy), (tx, ty) = _frame(self.line, s)
            lat = tx * (y - qy) - ty * (x - qx)
            u = (lat + self.width / 2) / self.width
            v = (s - self.s0) / (self.s1 - self.s0)
        else:
            x0, y0, x1, y1 = self.poly.bounds
            u, v = (x - x0) / (x1 - x0), (y - y0) / (y1 - y0)
        return (min(1.0, max(0.0, u)), min(1.0, max(0.0, v)))


def _classify(face, runs):
    c = face.representative_point()
    best = None
    for line, iv in runs:
        s = line.project(c)
        q = line.interpolate(s)
        dist = c.distance(q)
        if dist > SW / 2 + 1e-6:
            continue
        for s0, s1, mode in iv:
            if s0 + 1e-6 < s < s1 - 1e-6:
                if best is None or dist < best[0]:
                    best = (dist, Slab(face, "box") if mode == "box" else Slab(face, "run", line, s0, s1))
    return best[1] if best else Slab(face, "box")


def sidewalk_slabs(lay, spacing, frontage=()):
    """[Slab] for one Layout (tile-local)."""
    if lay.kind == "green":
        return []
    runs, cuts = joint_lines(lay, spacing, frontage)
    sw = lay.sidewalk
    rings = [sw.boundary] + cuts
    faces = [f for f in polygonize(unary_union(rings)) if f.area > SLIVER and sw.contains(f.representative_point())]
    return [_classify(f, runs) for f in faces]


def slab_hash(cx, cy, seed=HASH_SEED):
    """Seeded hash of a slab's WORLD centre (rounded to cm) -> (variant, tone).
    Integer arithmetic on rounded coordinates, so Core could reproduce it."""
    h = zlib.crc32(("%d,%d,%d" % (seed, int(round(cx * 100)), int(round(cy * 100)))).encode()) & 0xFFFFFFFF
    k = h % N_VARIANTS
    t = TONE_RANGE[0] + (TONE_RANGE[1] - TONE_RANGE[0]) * ((h >> 8) & 0xFF) / 255.0
    return k, t


# ---------------------------------------------------------------------------
# front walkway and driveway (any frame: Core or the lot scene)
# ---------------------------------------------------------------------------
def walkway(door, attach, width=WALK_W):
    """Core's front walkway (door -> attach on the sidewalk centreline,
    `width` wide) as the paved slab the art draws: from the door to the
    sidewalk's OUTER edge, so it butts the sidewalk with no overlap.
    Returns (polygon, axis LineString door -> sidewalk edge)."""
    dx, dy = attach[0] - door[0], attach[1] - door[1]
    L = math.hypot(dx, dy)
    ux, uy = dx / L, dy / L
    end = (attach[0] - ux * SW / 2, attach[1] - uy * SW / 2)
    axis = LineString([door, end])
    return axis.buffer(width / 2, cap_style="flat"), axis


def walkway_slabs(poly, axis, spacing, width=WALK_W):
    """Equal slabs along the walkway axis (poured concrete); u spans the
    walkway's own width."""
    out = []
    pts = _divide(0.0, axis.length, spacing)
    for s0, s1 in zip(pts, pts[1:]):
        out.append(Slab(_strip(axis, s0, s1, poly, width), "run", axis, s0, s1, width))
    return out


def _strip(axis, s0, s1, poly, width):
    a, b = axis.interpolate(s0), axis.interpolate(s1)
    seg = LineString([(a.x, a.y), (b.x, b.y)])
    return seg.buffer(width, cap_style="flat").intersection(poly)


def driveway(shape, x0, x1, y_road, y_apron):
    """Driveway in the lot frame (road along X at y_road - ROAD_HALF, lot
    toward +y). Returns {"apron": poly, "drive": [poly...]}: the apron is
    the curb cut across the verge (road edge -> sidewalk inner edge, under
    the curb band); the drive runs from the sidewalk's outer edge to the
    garage apron. The sidewalk itself always runs straight across.
      flared   one slab, the apron widening DRIVE_FLARE each side at the road
      straight a plain rectangle (also the apron)
      ribbon   two wheel strips on the drive, full-width apron"""
    y_curb = y_road
    y_in = y_road + VERGE
    y_out = y_in + SW
    if shape == "flared":
        apron = Polygon([(x0 - DRIVE_FLARE, y_curb), (x1 + DRIVE_FLARE, y_curb), (x1, y_in), (x0, y_in)])
    else:
        apron = box(x0, y_curb, x1, y_in)
    if shape == "ribbon":
        xc = (x0 + x1) / 2
        drive = [box(xc + s * RIBBON_TRACK / 2 - RIBBON_W / 2, y_out, xc + s * RIBBON_TRACK / 2 + RIBBON_W / 2, y_apron)
                 for s in (-1, 1)]
    else:
        drive = [box(x0, y_out, x1, y_apron)]
    return {"apron": apron, "drive": drive}


def drive_slabs(polys, spacing=DRIVE_PANEL, split_wide=True):
    """Concrete driveway slabs: each piece cut across every ~spacing m along
    y (compress-to-fit, equal slabs), and DRIVE_SLABS slabs across a piece
    wider than 2 m (the centre joint)."""
    out = []
    for p in polys:
        x0, y0, x1, y1 = p.bounds
        k = DRIVE_SLABS if split_wide and x1 - x0 > 2.0 else 1
        xs = [x0 + (x1 - x0) * i / k for i in range(k + 1)]
        ys = _divide(y0, y1, spacing)
        for xa, xb in zip(xs, xs[1:]):
            for ya, yb in zip(ys, ys[1:]):
                q = box(xa, ya, xb, yb).intersection(p)
                if q.area > SLIVER:
                    out.append(Slab(q, "box"))
    return out
