"""Numerical check of the road kit's footprint against Core (#802), for every
road tile type. No Blender needed.

The Core side is not re-derived here: core_geometry.json is written by
running Core's own code (core_dump/, a tiny .NET console app that compiles
Assets/Scripts/Core and calls MapWalkNetwork.BuildFrom,
TileCrosswalkGeometry.RectanglesFor, TileRoadGeometry.SegmentsFor,
TileGeometry.TreeWorldPositionsFor and TileMap.CanPlace). Refresh it with
--refresh (needs the .NET 8 SDK). The kit side is dh_road_geom, the exact
shapes dh_roads.py meshes, and every look's markings.

Checks (tolerance TOL unless noted):
  1  cross-sections of every road arm, at 12, 20 and 29.999 m from the tile
     centre: asphalt [-3, 3], curb [3, 3.15] each side, lawn verge, sidewalk
     [3.75, 5.75] each side
  2  tile borders: the cross-section on every road edge is identical for every
     type, so any two tiles meet seamlessly
  3  the asphalt covers every Core TileRoadSegment's straight part
  4  every Core walk-network sidewalk edge lies on the sidewalk: every sample
     point (10 cm apart) is at least WALK_CLEAR_MIN inside the 2 m band and
     WALK_CURB_MIN from the curb. Ideal is 1.0 and 1.6 m; Core walks round a
     curve on straight chords (<= 2 m, WalkNetwork.AddArc), which cut up to
     5 cm inside the true arc the pavement follows
  5  crosswalk paint: one band per Core crosswalk rectangle, covering exactly
     its along-arm extent, inside the asphalt, for every look's style
  6  every painted marking of every look lies on the asphalt
  7  stop lines lie in the right-hand (approach) lane
  8  bends: asphalt edge radii 2 and 8 m, sidewalk centre radii 0.25 and 9.75
     m about the corner centre (Core's arc nodes lie on them)
  9  the 3 x 3 review block is a valid map (Core's TileMap.CanPlace)
 10  REPORT ONLY: Core's open-space trees that stand on the kit's pavement
Writes geometry_report.json next to this file; exits non-zero if 1-9 fail.
Usage: python3 check_geometry.py [--refresh]
"""
import json
import math
import os
import subprocess
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import dh_road_geom as R  # noqa: E402
from shapely.geometry import LineString, Point, box  # noqa: E402
from shapely.ops import unary_union  # noqa: E402

TOL = 1e-4
WALK_CLEAR_MIN = 0.94
WALK_CURB_MIN = 1.54
CORE_JSON = os.path.join(HERE, "core_geometry.json")
LOOK_MARKS = {   # mirrors dh_roads.LOOKS (that module needs Blender)
    "a": {"centre": "double", "crosswalk": "bars"},
    "b": {"centre": "dashed", "crosswalk": "two-line"},
    "c": {"centre": "none", "crosswalk": "ladder"},
}
SECTION_AT = (12.0, 20.0, R.HALF - 1e-3)
EXPECT = {
    "asphalt": [(-3.0, 3.0)],
    "curb": [(-3.15, -3.0), (3.0, 3.15)],
    "sidewalk": [(-5.75, -3.75), (3.75, 5.75)],
}


def _intervals(geom, d, s):
    """Lateral intervals of `geom` on the line across arm d at distance s."""
    p = R._perp(d)
    a = R._add(R._v(d, s), R._v(p, -12))
    b = R._add(R._v(d, s), R._v(p, 12))
    g = geom.intersection(LineString([a, b]))
    out = []
    for ln in getattr(g, "geoms", [g]):
        if ln.is_empty or ln.geom_type != "LineString":
            continue
        ts = sorted(((q[0] - a[0]) * p[0] + (q[1] - a[1]) * p[1]) - 12 for q in ln.coords)
        out.append((round(ts[0], 4), round(ts[-1], 4)))
    return sorted(out)


def _close(a, b):
    return len(a) == len(b) and all(abs(x[0] - y[0]) < 1e-3 and abs(x[1] - y[1]) < 1e-3 for x, y in zip(a, b))


def main():
    if "--refresh" in sys.argv:
        out = subprocess.run(["dotnet", "run"], cwd=os.path.join(HERE, "core_dump"), capture_output=True, text=True,
                             check=True).stdout
        open(CORE_JSON, "w").write(out)
    core = json.load(open(CORE_JSON))
    fails, rep = [], {"constants": {k: R.WD[k] for k in sorted(R.WD)}, "tiles": {}}
    rep["constants"]["sidewalk_offset"] = R.OFF
    if abs(core["StreetCorridorInset"] - (R.OFF + R.SW / 2)) > TOL:
        fails.append("StreetCorridorInset %s != %s" % (core["StreetCorridorInset"], R.OFF + R.SW / 2))
    edge_sig = {}
    for t in R.ROAD_TYPES:
        lay = R.Layout(t)
        C = core["tiles"][t]
        tr = rep["tiles"][t] = {"kind": lay.kind}
        pave = unary_union([lay.asphalt, lay.curb])
        # 1 + 2: cross-sections
        for d, name in zip(lay.edges, lay.edge_names):
            for s in SECTION_AT:
                got = {k: _intervals(getattr(lay, k), d, s) for k in EXPECT}
                ok = all(_close(got[k], EXPECT[k]) for k in EXPECT)
                if not ok:
                    fails.append("%s %s section at %.3f: %s" % (t, name, s, got))
                if s == SECTION_AT[-1]:
                    edge_sig[(t, name)] = got
            tr["sections_ok"] = tr.get("sections_ok", True) and ok
        # 3: Core road segments' straight parts are asphalt
        for cx, cz, orient, length, width in C["segments"]:
            ns = orient == "NorthSouth"
            hl = length / 2
            # skip the part next to the tile centre where a bend or dead end shapes the road
            skip = {"turn": R.BEND_R + R.ROAD_HALF, "culdesac": 16.0}.get(lay.kind, 0.0)
            if ns:
                y0, y1 = sorted((cz - hl, cz + hl))
                y0, y1 = (max(y0, skip), y1) if cz > 0 else (y0, min(y1, -skip))
                rect = box(cx - width / 2, y0, cx + width / 2, y1)
            else:
                x0, x1 = sorted((cx - hl, cx + hl))
                x0, x1 = (max(x0, skip), x1) if cx > 0 else (x0, min(x1, -skip))
                rect = box(x0, cz - width / 2, x1, cz + width / 2)
            miss = rect.difference(lay.asphalt).area
            if miss > 1e-6:
                fails.append("%s segment %s not covered by asphalt (%.6f m2)" % (t, (cx, cz), miss))
        # 4: walk network
        worst_cover, min_clear, n_side, n_cross = 99.0, 99.0, 0, 0
        for kind, ax, az, bx, bz, w in C["edges"]:
            ln = LineString([(ax, az), (bx, bz)])
            if kind == "Crosswalk":
                n_cross += 1
                for p in ((ax, az), (bx, bz)):
                    if not lay.sidewalk.buffer(1e-6).contains(Point(p)):
                        fails.append("%s crosswalk end %s not on sidewalk" % (t, p))
                continue
            n_side += 1
            n = max(2, int(ln.length / 0.1))
            for i in range(n + 1):
                p = ln.interpolate(i / n, normalized=True)
                inside = lay.sidewalk_full.contains(p)
                worst_cover = min(worst_cover, lay.sidewalk_full.exterior.distance(p) if inside and
                                  lay.sidewalk_full.geom_type == "Polygon" else
                                  (min(g.boundary.distance(p) for g in lay.sidewalk_full.geoms if g.contains(p))
                                   if inside else -1.0))
            min_clear = min(min_clear, ln.distance(pave))
        tr.update({"walk_sidewalk_edges": n_side, "walk_crosswalk_edges": n_cross,
                   "walk_clearance_min": round(worst_cover, 4), "walk_to_curb_min": round(min_clear, 4)})
        if worst_cover < WALK_CLEAR_MIN:
            fails.append("%s walk path only %.4f m inside the sidewalk" % (t, worst_cover))
        if min_clear < WALK_CURB_MIN:
            fails.append("%s walk path %.4f m from curb" % (t, min_clear))
        # 5 + 6 + 7: markings
        rects = C["crosswalks"]
        tr["core_crosswalks"] = len(rects)
        for look, M in LOOK_MARKS.items():
            xw = R.crosswalk_lines(lay, M["crosswalk"])
            for cxr, czr, spx, spz in rects:
                band = box(cxr - spx / 2, czr - spz / 2, cxr + spx / 2, czr + spz / 2)
                paint = unary_union([ln.buffer(w / 2, cap_style="flat") for ln, w, _ in xw
                                     if ln.buffer(w / 2, cap_style="flat").intersects(band.buffer(-0.01))])
                pb = paint.bounds
                along_ns = spz < spx       # a north/south arm's band is thin along Z
                lo, hi = (pb[1], pb[3]) if along_ns else (pb[0], pb[2])
                elo, ehi = (czr - spz / 2, czr + spz / 2) if along_ns else (cxr - spx / 2, cxr + spx / 2)
                if abs(lo - elo) > TOL or abs(hi - ehi) > TOL:
                    fails.append("%s look %s crosswalk %s paint along [%.4f, %.4f] != [%.4f, %.4f]"
                                 % (t, look, (cxr, czr), lo, hi, elo, ehi))
                if not band.buffer(TOL).contains(paint):
                    fails.append("%s look %s crosswalk paint leaves the Core band" % (t, look))
            if len(rects) != len(lay.crosswalk_arms):
                fails.append("%s crosswalk count %d != Core %d" % (t, len(lay.crosswalk_arms), len(rects)))
            marks = R.centre_lines(lay, M["centre"]) + xw + R.stop_lines(lay)
            for ln, w, _ in marks:
                poly = ln.buffer(w / 2, cap_style="flat", join_style="mitre")
                if not lay.asphalt.buffer(TOL).contains(poly):
                    fails.append("%s look %s marking off the asphalt at %s" % (t, look, ln.coords[0]))
        for d, (ln, w, _) in zip(lay.stop_arms, R.stop_lines(lay)):
            q = (-d[1], d[0])
            lat = [(p[0] * q[0] + p[1] * q[1]) for p in ln.coords]
            if not (min(lat) > 0 and max(lat) <= R.ROAD_HALF):
                fails.append("%s stop line not in the approach lane" % t)
        # 8: bends
        if lay.kind == "turn":
            c = lay.corner_c
            radii = sorted({round(math.dist(p, c), 3) for p in lay.asphalt.exterior.coords
                            if (p[0] - c[0]) * (c[0]) <= 1e-9 and (p[1] - c[1]) * (c[1]) <= 1e-9})
            tr["bend_asphalt_radii"] = [radii[0], radii[-1]]
            if abs(radii[0] - (R.BEND_R - R.ROAD_HALF)) > 0.01 or abs(radii[-1] - (R.BEND_R + R.ROAD_HALF)) > 0.01:
                fails.append("%s bend asphalt radii %s" % (t, radii[:3] + radii[-3:]))
            core_r = sorted({round(math.dist((ax, az), c), 3) for _, ax, az, bx, bz, _ in C["edges"]
                             if (ax - c[0]) * c[0] <= 1e-9 and (az - c[1]) * c[1] <= 1e-9})
            tr["bend_core_walk_radii"] = core_r
        if lay.kind == "culdesac":
            d = lay.edges[0]
            tip = min((p[0] * d[0] + p[1] * d[1]) for p in lay.asphalt.exterior.coords)
            sw_tip = min((p[0] * d[0] + p[1] * d[1]) for p in lay.sidewalk.exterior.coords)
            core_tip = min(min(ax * d[0] + az * d[1], bx * d[0] + bz * d[1]) for _, ax, az, bx, bz, _ in C["edges"])
            tr.update({"asphalt_cap_tip": round(tip, 4), "sidewalk_outer_tip": round(sw_tip, 4),
                       "core_walk_tip": round(core_tip, 4)})
        # 10: trees (report)
        allpave = unary_union([lay.asphalt, lay.curb, lay.sidewalk])
        on = [tp for tp in C["trees"] if allpave.contains(Point(tp[2], tp[3]))]
        tr["core_trees"] = len(C["trees"])
        tr["core_trees_on_kit_pavement"] = len(on)
        if C["trees"]:
            tr["core_tree_min_dist_to_pavement"] = round(min(allpave.distance(Point(tp[2], tp[3]))
                                                             for tp in C["trees"]), 3)
        tr["areas_m2"] = {k: round(getattr(lay, k).area, 3) for k in ("asphalt", "curb", "sidewalk")}
    # 2: borders
    sigs = {json.dumps(v) for v in edge_sig.values()}
    rep["tile_edge_signatures"] = len(sigs)
    if len(sigs) != 1:
        fails.append("tile-edge cross-sections differ: %d variants" % len(sigs))
    rep["tile_edge_section"] = next(iter(edge_sig.values()))
    # 9: block
    rep["block_valid"] = core["block"]
    if not all(core["block"].values()):
        fails.append("review block invalid: %s" % core["block"])
    rep["fails"] = fails
    with open(os.path.join(HERE, "geometry_report.json"), "w") as f:
        json.dump(rep, f, indent=1)
    for t, tr in rep["tiles"].items():
        print("%-14s %s" % (t, {k: v for k, v in tr.items() if k not in ("areas_m2",)}))
    print("tile-edge section (all %d road edges identical): %s" % (len(edge_sig), rep["tile_edge_section"]))
    print("ALL OK" if not fails else "FAILED (%d):\n  %s" % (len(fails), "\n  ".join(fails[:30])))
    return 1 if fails else 0


if __name__ == "__main__":
    sys.exit(main())
