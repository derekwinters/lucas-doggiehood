"""Numerical check of the paving (#803; P1 approved, #851) against Core and against the
approved road kit, for every option's joint spacing:

  1  slabs: for every road tile type, the union of the slabs IS the road
     kit's sidewalk (whose footprint check_geometry.py verifies against
     Core), with no overlap and no slab outside its tile
  2  Core walk path: every Core sidewalk edge of every type is at least
     WALK_CLEAR_MIN inside the slab union (as check_geometry.py), so the
     bends' and cul-de-sacs' slabs carry Core's curved paths
  3  tile borders: on every shared edge of the 3 x 3 review block and of the
     starter map, the slab corners on the border match from both sides
     (joints line up), and the border is a joint on both
  4  landings: on every FourWay/Tee arm, each crosswalk landing in the verge
     is exactly one slab, covers Core's crosswalk walk edge +-1 m, and the
     Core crosswalk edge's ends lie on slabs
  5  curves: every joint on a bend's outer arc and on a cul-de-sac's arc is
     radial (within ARC_ANGLE_TOL); each cul-de-sac kink is cut on the
     bisector; each bend's inner corner is one slab
  6  front walkways: Core's four starter walkways (from core_dump) built by
     dh_paving_geom.walkway are WALK_W wide, perpendicular to their sidewalk,
     start at the door, cover the Core edge +-1 m and butt the sidewalk
     slabs (shared edge = WALK_W, no overlap)
  7  P1's lot rules on all 10 bases' level-4 lots (needs bpy): the walkway is
     the width of the bottom front step READ FROM THE BASE'S GEOMETRY,
     centred on it, starting at its front edge and butting the sidewalk;
     Core's walk-path centreline (door x) stays on it (clearance reported);
     the straight driveway keeps ONE width from the garage apron to the road
     edge, covers the garage door, and clears the walkway, the sidewalk and
     the asphalt
  8  REPORT ONLY: slab counts and sizes; heights for #839; house setback in
     the lot scene vs Core's starter doors
Writes paving_report.json; exits non-zero if 1-7 fail.
Usage: python3 check_paving.py [--no-lot]   (the lot part needs bpy)
"""
import json
import math
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
sys.path.insert(0, os.path.normpath(os.path.join(HERE, "..", "roads")))
import dh_paving_geom as PG  # noqa: E402
import dh_road_geom as R  # noqa: E402
from shapely.geometry import LineString, Point, box  # noqa: E402
from shapely.ops import unary_union  # noqa: E402

TOL = 1e-6
WALK_CLEAR_MIN = 0.94           # as check_geometry.py (Core chords cut <= 5 cm inside an arc)
ARC_ANGLE_TOL = math.radians(1.0)
CORE_JSON = os.path.join(HERE, "..", "roads", "core_geometry.json")
SPACINGS = {"p1": 1.5, "p2": 2.0, "p3": 2.0}     # mirrors dh_paving.OPTIONS (that module needs Blender)
SHAPES = ("flared", "straight", "ribbon")
BASES = range(1, 11)
ALIGN_TOL = 1e-3                # 1 mm: a walkway/driveway edge on a sidewalk joint


def _angle(a, b):
    """Unsigned angle between two undirected 2-D directions."""
    d = abs(math.atan2(a[0] * b[1] - a[1] * b[0], a[0] * b[0] + a[1] * b[1]))
    return min(d, math.pi - d)


def _dir(ln):
    (x0, y0), (x1, y1) = ln.coords[0], ln.coords[-1]
    m = math.hypot(x1 - x0, y1 - y0)
    return ((x1 - x0) / m, (y1 - y0) / m)


def _border_pts(slabs, axis, value):
    """Slab corner coordinates (the other axis) on the tile edge axis=value."""
    pts = set()
    for s in slabs:
        for p in getattr(s.poly, "geoms", [s.poly]):
            for x, y in p.exterior.coords:
                c = (x, y)[axis]
                if abs(c - value) < 1e-6:
                    pts.add(round((x, y)[1 - axis], 5))
    return pts


def check_tiles(spacing, core, fails, rep):
    out = {}
    for t in R.ROAD_TYPES:
        lay = R.Layout(t)
        slabs = PG.sidewalk_slabs(lay, spacing)
        u = unary_union([s.poly for s in slabs])
        tr = out[t] = {"slabs": len(slabs)}
        sd = u.symmetric_difference(lay.sidewalk).area
        ov = sum(s.poly.area for s in slabs) - u.area
        outside = sum(s.poly.difference(R.TILE_BOX).area for s in slabs)
        tr.update({"union_symdiff_m2": sd, "overlap_m2": round(ov, 9)})
        if sd > TOL or abs(ov) > TOL or outside > TOL:
            fails.append("%s @%.1f: slabs != sidewalk (symdiff %.2e, overlap %.2e, outside %.2e)" % (t, spacing, sd, ov,
                                                                                                    outside))
        runs = [s.length for s in slabs if s.mode == "run"]
        if min(runs) < PG.MIN_SLAB - 1e-9:
            fails.append("%s @%.1f: sliver slab %.3f m" % (t, spacing, min(runs)))
        tr.update({"run_slab_m": [round(min(runs), 3), round(max(runs), 3)],
                   "box_slabs": sorted(round(s.poly.area, 2) for s in slabs if s.mode == "box"),
                   "smallest_m2": round(min(s.poly.area for s in slabs), 3)})
        # 2: Core walk path on the slabs (tile edges are not "edges" of the pavement)
        edge = u.boundary.difference(R.TILE_BOX.exterior.buffer(1e-6))
        worst = 99.0
        for kind, ax, az, bx, bz, w in core["tiles"][t]["edges"]:
            ln = LineString([(ax, az), (bx, bz)])
            if kind == "Crosswalk":
                for p in ((ax, az), (bx, bz)):
                    if not u.buffer(1e-6).contains(Point(p)):
                        fails.append("%s crosswalk end %s not on a slab" % (t, p))
                continue
            n = max(2, int(ln.length / 0.1))
            for i in range(n + 1):
                p = ln.interpolate(i / n, normalized=True)
                worst = min(worst, edge.distance(p) if u.buffer(1e-6).contains(p) else -1.0)
        tr["walk_clearance_min"] = round(worst, 4)
        if worst < WALK_CLEAR_MIN:
            fails.append("%s @%.1f: Core walk path only %.4f m inside the slabs" % (t, spacing, worst))
        # 4: landings
        if lay.kind in ("fourway", "tee"):
            n_land = 0
            for d in lay.edges:
                p = R._perp(d)
                for side in (1, -1):
                    l0, l1 = sorted((side * PG.VERGE_IN, side * PG.BAND_IN))
                    land = R._rect_local(d, PG.BAND_IN, PG.BAND_OUT, l0, l1)
                    if not any(s.poly.symmetric_difference(land).area < TOL for s in slabs):
                        fails.append("%s landing %s/%d is not one slab" % (t, d, side))
                    n_land += 1
                cw = LineString([R._add(R._v(d, R.OFF), R._v(p, R.OFF)), R._add(R._v(d, R.OFF), R._v(p, -R.OFF))])
                band = cw.buffer(R.SW / 2, cap_style="flat").difference(lay.asphalt_raw.buffer(R.CURB_W,
                                                                                               join_style="mitre"))
                miss = band.difference(u).area
                if miss > TOL:
                    fails.append("%s crosswalk walk band not paved across the verge (%.4f m2)" % (t, miss))
            tr["landings_one_slab"] = n_land
        # 5: curves
        runs_, _ = PG.runs_and_cuts(lay, spacing)
        if lay.kind == "turn":
            c = lay.corner_c
            line, iv = runs_[1]
            k = R.HALF - R.BEND_R
            worst_a = 0.0
            for s0, _, _ in iv:
                if k + 1e-6 < s0 < line.length - k - 1e-6:
                    cut = PG.cut_segment(line, s0)
                    m = cut.interpolate(0.5, normalized=True)
                    worst_a = max(worst_a, _angle(_dir(cut), (m.x - c[0], m.y - c[1])))
            tr["outer_arc_joint_radial_err_deg"] = round(math.degrees(worst_a), 4)
            if worst_a > ARC_ANGLE_TOL:
                fails.append("%s outer-arc joint off radial by %.3f deg" % (t, math.degrees(worst_a)))
            inner = [s for s in slabs if s.mode == "box"]
            tr["inner_corner_slab_m2"] = [round(s.poly.area, 3) for s in inner]
            if len(inner) != 1 or not inner[0].poly.buffer(1e-6).contains(Point(c)):
                fails.append("%s inner corner is not one slab round the corner" % t)
        if lay.kind == "culdesac":
            cc, _ = lay.bulb
            line, iv = runs_[0]
            L = line.length
            worst_a, kink = 0.0, 0.0
            for s0, _, _ in iv:
                if R.HALF + 1e-6 < s0 < L - R.HALF - 1e-6:
                    cut = PG.cut_segment(line, s0)
                    m = cut.interpolate(0.5, normalized=True)
                    worst_a = max(worst_a, _angle(_dir(cut), (m.x - cc[0], m.y - cc[1])))
            for s, sgn in ((R.HALF, -1.0), (L - R.HALF, 1.0)):
                # true normals either side of the kink: the straight run's,
                # and the arc's (radial from the bulb centre)
                cut = _dir(PG.cut_segment(line, s))
                a, b = line.interpolate(s + sgn * 1.0), line.interpolate(s)
                n_in = (-(b.y - a.y), b.x - a.x)
                n_out = (b.x - cc[0], b.y - cc[1])
                kink = max(kink, abs(_angle(cut, n_in) - _angle(cut, n_out)) / 2)   # off the true bisector
            tr.update({"arc_joint_radial_err_deg": round(math.degrees(worst_a), 4),
                       "kink_bisector_err_deg": round(math.degrees(kink), 4)})
            if worst_a > ARC_ANGLE_TOL or kink > ARC_ANGLE_TOL:
                fails.append("%s cul-de-sac joints: radial %.3f, kink %.3f deg" % (t, math.degrees(worst_a),
                                                                                  math.degrees(kink)))
    rep["tiles@%.1f" % spacing] = out


def check_borders(spacing, tiles, label, fails, rep):
    cache = {t: PG.sidewalk_slabs(R.Layout(t), spacing) for t in set(tiles.values())}
    n = 0
    for (c, r), t in tiles.items():
        for dc, dr, axis in ((1, 0, 0), (0, 1, 1)):
            o = tiles.get((c + dc, r + dr))
            if o is None:
                continue
            a = _border_pts(cache[t], axis, R.HALF)
            b = _border_pts(cache[o], axis, -R.HALF)
            if a != b:
                fails.append("%s border %s|%s @%.1f: joints differ %s vs %s" % (label, t, o, spacing, sorted(a),
                                                                               sorted(b)))
            n += 1 if a else 0
    rep.setdefault("borders", {})["%s@%.1f" % (label, spacing)] = n


def check_core_walkways(core, fails, rep):
    lay = R.Layout("FourWay")
    out = []
    for spacing in sorted(set(SPACINGS.values())):
        u = unary_union([s.poly for s in PG.sidewalk_slabs(lay, spacing)])
        for hid, ax, az, bx, bz, w in core["starter_walkways"]:
            r = _walkway_rules((ax, az), (bx, bz), w, u, fails, "Core house %d @%.1f" % (hid, spacing))
            r["house"] = hid
            out.append(r)
    rep["core_walkways"] = out


def _walkway_rules(door, attach, width, sidewalk, fails, label):
    poly, axis = PG.walkway(door, attach, width)
    w = poly.area / axis.length
    d = _dir(axis)
    run = (1.0, 0.0) if abs(d[1]) > 0.5 else (0.0, 1.0)            # the sidewalk the walkway meets
    perp = abs(d[0] * run[0] + d[1] * run[1])
    core_edge = LineString([door, attach])
    cover = core_edge.buffer(width / 2 - 1e-3, cap_style="flat").difference(sidewalk).difference(poly).area
    over = poly.intersection(sidewalk).area
    shared = poly.buffer(1e-6).intersection(sidewalk.boundary).length
    r = {"label": label, "width": round(w, 6), "length": round(axis.length, 4), "perp_dot": round(perp, 9),
         "uncovered_m2": round(cover, 8), "overlap_m2": round(over, 8), "shared_edge_m": round(shared, 4)}
    if abs(w - PG.WALK_W) > TOL or perp > TOL or cover > TOL or over > TOL or abs(shared - width) > 1e-3 or \
            Point(door).distance(poly) > TOL:
        fails.append("%s walkway rule broken: %s" % (label, r))
    return r


def _run_joint_xs(slabs, y, tol=1e-6):
    """x of every joint (slab end) on the run whose centreline is at height y."""
    xs = set()
    for sl in slabs:
        if sl.mode != "run" or abs(sl.line.coords[0][1] - y) > tol or abs(sl.line.coords[-1][1] - y) > tol:
            continue
        for st in (sl.s0, sl.s1):
            xs.add(sl.line.interpolate(st).x)
    return sorted(xs)


def check_alignment(fails, rep):
    """P1 joint alignment on all 10 bases' level-4 lots: every walkway and
    driveway long edge, and the driveway's centre joint, lies on a sidewalk
    joint (ALIGN_TOL); the slab in front of each is exactly its width; the
    walkway and driveway cross joints are compress-to-fit at ~spacing (equal
    slabs, so full slabs meet the steps and the garage apron); the driveway
    is DRIVE_SLABS wide and its verge apron continues the same lines; no run
    slab is shorter than PG.MIN_SLAB."""
    sys.path.insert(0, os.path.normpath(os.path.join(HERE, "..", "ground")))
    import dh_paving as P       # needs bpy
    import dh_ground as G
    spacing = SPACINGS["p1"]
    lay = R.Layout("FourWay")
    out = {}
    for n in BASES:
        steps = P.front_steps(n)
        sc = G.lot_scene(base=n)
        lot = sc["lot"]
        cx, cy = sc["tile_centre"]
        fr = P.lot_frontage(steps, lot["dinfo"]["x"])
        pts = [(x - cx, R.OFF) for x in fr["walk"] + fr["drive"]]
        slabs = PG.sidewalk_slabs(lay, spacing, frontage=pts)
        joints = _run_joint_xs(slabs, R.OFF)
        miss = [round(x, 4) for x, _ in pts if min(abs(j - x) for j in joints) > ALIGN_TOL]
        # one sidewalk slab exactly between each pair of neighbouring lot lines
        # (walkway: its two edges; driveway: edge-centre and centre-edge)
        front = {k: all(any(sl.mode == "run" and abs(sl.length - (b - a)) < ALIGN_TOL and
                            abs(min(sl.line.interpolate(sl.s0).x, sl.line.interpolate(sl.s1).x) - (a - cx)) < ALIGN_TOL
                            for sl in slabs) for a, b in zip(v, v[1:])) for k, v in fr.items()}
        run_len = [sl.length for sl in slabs if sl.mode == "run"]
        uu = unary_union([sl.poly for sl in slabs])
        tiling = uu.symmetric_difference(lay.sidewalk).area + abs(sum(sl.poly.area for sl in slabs) - uu.area)
        # walkway and driveway slabs
        sx0, sx1 = steps["x"]
        start = ((sx0 + sx1) / 2, steps["y_front"] + P.WALK_TUCK)
        wpoly, axis = PG.walkway(start, (start[0], cy + R.OFF), sx1 - sx0)
        wsl = PG.walkway_slabs(wpoly, axis, spacing, sx1 - sx0)
        wlen = [sl.length for sl in wsl]
        dv = PG.driveway("straight", fr["drive"][0], fr["drive"][-1], cy + R.ROAD_HALF, lot["dinfo"]["y"][1])
        dsl = PG.drive_slabs(dv["drive"], spacing)
        asl = PG.drive_slabs([dv["apron"]], spacing)
        dxs = sorted({round(v, 6) for sl in dsl for v in (sl.poly.bounds[0], sl.poly.bounds[2])})
        axs = sorted({round(v, 6) for sl in asl for v in (sl.poly.bounds[0], sl.poly.bounds[2])})
        dlen = sorted({round(sl.poly.bounds[3] - sl.poly.bounds[1], 6) for sl in dsl})
        r = {"edges_off_joint": miss, "front_slab_exact": front, "tiling_err_m2": tiling,
             "sidewalk_run_slab_m": [round(min(run_len), 3), round(max(run_len), 3)],
             "walkway_slabs": len(wsl), "walkway_slab_m": [round(min(wlen), 3), round(max(wlen), 3)],
             "drive_x_lines": dxs, "apron_x_lines": axs, "drive_slab_m": dlen,
             "drive_slabs_across": len(dxs) - 1}
        want_x = sorted(round(x, 6) for x in fr["drive"])
        ok = (not miss and all(front.values()) and tiling < TOL and min(run_len) >= PG.MIN_SLAB - 1e-9 and
              max(wlen) - min(wlen) < 1e-6 and abs(wlen[0] - spacing) <= spacing / 2 and
              dxs == want_x and axs == want_x and len(dxs) - 1 == PG.DRIVE_SLABS and
              max(dlen) - min(dlen) < 1e-6 and abs(dlen[0] - spacing) <= spacing / 2)
        if not ok:
            fails.append("base %d alignment: %s" % (n, r))
        out["base_%d" % n] = r
    rep["alignment_p1"] = out


def check_lot(fails, rep):
    """The P1 lot rules on every base's level-4 lot (dh_ground.lot_scene(base=n)):
    walkway = the bottom front step (width read from the base's geometry,
    centred on it, starting at its front edge), butting the sidewalk; Core's
    walk-path centreline (the door's x, door -> sidewalk) on it; the straight
    driveway the same width from the garage apron to the road edge, covering
    the garage door, clear of the walkway, sidewalk and asphalt."""
    sys.path.insert(0, os.path.normpath(os.path.join(HERE, "..", "ground")))
    import dh_paving as P       # needs bpy
    import dh_ground as G
    import dh_scene as S
    from shapely import affinity
    lay = R.Layout("FourWay")
    spacing = SPACINGS["p1"]
    u = unary_union([s.poly for s in PG.sidewalk_slabs(lay, spacing)])
    out = {}
    for n in BASES:
        steps = P.front_steps(n)
        sc = G.lot_scene(base=n)
        lot = sc["lot"]
        cx, cy = sc["tile_centre"]
        uw = affinity.translate(u, cx, cy)
        asphalt = affinity.translate(lay.asphalt, cx, cy)
        sx0, sx1 = steps["x"]
        w = sx1 - sx0
        start = ((sx0 + sx1) / 2, steps["y_front"] + P.WALK_TUCK)
        wpoly, axis = PG.walkway(start, (start[0], cy + R.OFF), w)
        # Core's walk path: the door's x, from the door down to the sidewalk
        core_line = LineString([(steps["door_x"], steps["y_front"]), (steps["door_x"], cy + PG.BAND_OUT)])
        clear = w / 2 - abs(steps["door_x"] - (sx0 + sx1) / 2)
        on = wpoly.buffer(1e-6).contains(core_line)
        shared = wpoly.buffer(1e-6).intersection(uw.boundary).length
        over = wpoly.intersection(uw).area
        dx0, dx1 = lot["dinfo"]["x"]
        y_apron = lot["dinfo"]["y"][1]
        dv = PG.driveway("straight", dx0, dx1, cy + R.ROAD_HALF, y_apron)
        drive = unary_union(dv["drive"] + [dv["apron"]])
        g_d = S.fit_pieces(lot["B"])[0]
        gx = (dx0 + dx1) / 2
        door_half = g_d["door_width"] / 2
        widths = sorted({round(p.bounds[2] - p.bounds[0], 6) for p in dv["drive"] + [dv["apron"]]})
        r = {"step_width_m": round(w, 4), "step_mat": steps["mat"], "walkway_m": round(axis.length, 4),
             "core_centreline_on_walkway": on, "core_centreline_clearance_m": round(clear, 4),
             "walkway_sidewalk_shared_m": round(shared, 4), "walkway_sidewalk_overlap_m2": round(over, 8),
             "drive_widths_m": widths, "garage_door_m": g_d["door_width"],
             "drive_walkway_overlap_m2": round(drive.intersection(wpoly).area, 8),
             "drive_sidewalk_overlap_m2": round(drive.intersection(uw).area, 8),
             "drive_asphalt_overlap_m2": round(drive.intersection(asphalt).area, 8),
             "drive_y": [round(drive.bounds[1], 4), round(drive.bounds[3], 4)]}
        ok = (on and clear > 0 and abs(shared - w) < 1e-3 and over < TOL and len(widths) == 1 and
              widths[0] >= g_d["door_width"] and dx0 <= gx - door_half and gx + door_half <= dx1 and
              r["drive_walkway_overlap_m2"] < TOL and r["drive_sidewalk_overlap_m2"] < TOL and
              r["drive_asphalt_overlap_m2"] < TOL and abs(drive.bounds[1] - (cy + R.ROAD_HALF)) < 1e-4 and
              abs(drive.bounds[3] - y_apron) < 1e-4)
        if not ok:
            fails.append("base %d lot (P1): %s" % (n, r))
        out["base_%d" % n] = r
    rep["lot_p1"] = out


def main():
    core = json.load(open(CORE_JSON))
    fails, rep = [], {}
    for sp in sorted(set(SPACINGS.values())):
        check_tiles(sp, core, fails, rep)
        check_borders(sp, R.BLOCK, "block", fails, rep)
        check_borders(sp, R.STARTER, "starter", fails, rep)
    check_core_walkways(core, fails, rep)
    rep["core_setback"] = [{"house": h, "door_to_street_cl_m": round(abs(ax) if abs(az - bz) < 1e-3 else abs(az), 4),
                            "walkway_m": round(math.hypot(bx - ax, bz - az) - R.SW / 2, 4)}
                           for h, ax, az, bx, bz, w in core["starter_walkways"]]
    if "--no-lot" not in sys.argv:
        check_lot(fails, rep)
        check_alignment(fails, rep)
    rep["heights_m"] = {"lawn": 0.0, "asphalt": 0.010, "sidewalk_walkway_driveway": 0.012, "curb_band": 0.013,
                        "paint": 0.0145, "core_SidewalkSurfaceHeight": R.WD.get("SidewalkSurfaceHeight"),
                        "core_RoadSurfaceHeight": R.WD.get("RoadSurfaceHeight")}
    rep["fails"] = fails
    with open(os.path.join(HERE, "paving_report.json"), "w") as f:
        json.dump(rep, f, indent=1)
    for sp in sorted(set(SPACINGS.values())):
        T = rep["tiles@%.1f" % sp]
        print("spacing %.1f m: %d slabs over %d types; run slabs %.3f-%.3f m; borders block %d, starter %d" % (
            sp, sum(v["slabs"] for v in T.values()), len(T), min(v["run_slab_m"][0] for v in T.values()),
            max(v["run_slab_m"][1] for v in T.values()), rep["borders"]["block@%.1f" % sp],
            rep["borders"]["starter@%.1f" % sp]))
    print("walk clearance min %.4f" % min(v["walk_clearance_min"] for k in rep if k.startswith("tiles@")
                                         for v in rep[k].values()))
    for r in rep["core_walkways"][:4]:
        print("Core walkway", r)
    for k, v in rep.get("alignment_p1", {}).items():
        print("%s: sidewalk run slabs %s m, walkway %d x %s m, drive %d across x %s m" % (
            k, v["sidewalk_run_slab_m"], v["walkway_slabs"], v["walkway_slab_m"], v["drive_slabs_across"],
            v["drive_slab_m"]))
    for k, v in rep.get("lot_p1", {}).items():
        print("%s: step/walkway %.2f m (%s), Core centreline clearance %.2f m, walkway %.2f m long, drive %s m"
              " (garage door %.1f)" % (k, v["step_width_m"], v["step_mat"], v["core_centreline_clearance_m"],
                                       v["walkway_m"], v["drive_widths_m"], v["garage_door_m"]))
    print("FAILED:\n  " + "\n  ".join(fails) if fails else "ALL OK")
    return 1 if fails else 0


if __name__ == "__main__":
    sys.exit(main())
