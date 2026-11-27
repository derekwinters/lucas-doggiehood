#!/usr/bin/env python3
"""Doggiehood splash environment, hand-built as clean vector objects (the default scene).

Every environment object - the sky, the sun, each cloud, the street, every house, tree,
bush, fence, mailbox, flower and toy - is authored as crisp geometric primitives:
polygons with straight edges, plus circles, ellipses and simple arcs only where the
original is round (and the clouds, round by Derek's choice where the original's are blocky).
The original's corner sparkle is not drawn. The geometry lives in env_shapes.py, measured on
`Assets/Art/Splash/cover-art.png` (read-only); this module turns it into SVG.

Invariant: no traced, VTracer or bitmap-derived path appears in any environment layer;
every edge is an authored shape (self-checked by check_primitives()).

Each object is complete: the parts the dogs, the title or nearer objects hide are
authored too (the completions are listed in README.md). Objects stack into the
full-frame layers (sky, sun, clouds, street, houses-far, houses-left, houses-right, trees,
props) in the original's draw order; an object that stands behind something lower in the
stack (a tree behind a house) is clipped by that object's outline in its layer, and drawn
whole in its standalone file (objects/<id>.svg + .png).

Colour. Each face is one flat colour, or one linear gradient, measured from the original:
the median of the original's pixels that show the face (inside the face, not hidden by a
dog, the title or any face drawn above it), with outliers dropped. A face the original
hides entirely says where its colour comes from (`hid`: another face, or a colour).
Measured colours are computed at build time from the original, so a rebuild is
byte-identical.

`build_scene.py` writes everything; run `python3 art-source/splash/build_scene.py`.
"""
from __future__ import annotations

import math
import re
import xml.etree.ElementTree as ET
from functools import lru_cache

import numpy as np
from PIL import Image, ImageDraw

import build_title as bt

fmt = bt.fmt
W, H = 2816, 1536

# --------------------------------------------------------------------------
# Tuning
# --------------------------------------------------------------------------
FACE_ERODE = 1          # px of a face's edge not sampled for its colour
FACE_DE = 12.0          # samples further (Lab) than this from the face's median colour are dropped
FACE_MIN = 6            # visible px a face needs to be measured; fewer and it is hidden (needs `hid`)
FACE_FLAT = 0.04        # a `grad` face whose luma changes less than this per px is drawn flat
SEAM = 0.8              # px stroke each face carries in its own fill, so touching faces never show a seam
ROW_BIN = 8             # rows per stop of a vertical (`rows`) gradient
ROW_MIN = 12            # visible px a row bin needs to be measured
ROW_SMOOTH = 2          # bins of smoothing of a `rows` gradient
CURVE_SEGS = 48         # segments a circle / ellipse / arc is rasterised with (measuring and clipping only)
OBJECT_LONG = 2000      # standalone object PNGs are at least this long on the long side
OBJECT_PAD = 6          # original px around an object in its standalone crop
MAX_VERTS = 80          # an authored path has at most this many vertices per subpath (primitives check)
LAYERS = ["sky", "sun", "clouds", "street", "houses-far", "houses-left", "houses-right", "trees", "props"]


# --------------------------------------------------------------------------
# Authoring primitives (used by env_shapes.py). Colour options on every face:
#   c="#rrggbb"       a fixed colour
#   like="name"       the colour of another face (same object, or "object/name")
#   grad=True         a linear gradient fitted to what the original shows of the face
#   rows=True         a vertical gradient, one stop per ROW_BIN rows (the ground)
#   at=[pts]          measure the colour inside this outline instead of the face
#   hid=...           where the colour comes from if the original shows none of the face:
#                     "#rrggbb", or "name" / "object/name" of another face
#   op=0.5            opacity
#   added=True        a face the original does not have (its colour must come from c / like):
#                     it never hides the original's pixels from the faces it is drawn over, so
#                     adding it leaves every measured colour as it was
#   k=0.9             multiply the measured colour by k (a shade of another face, with like/hid)
#   of=[names]        measure over what the original shows of these faces (same object) together
#   src=face          a face moved from where the original shows it: measured (and hiding the
#                     original's pixels) as src, drawn where it is; an object's measure=[faces]
#                     does the same for all its faces (a moved mailbox)
#   q=(lo, hi)        with of/at: one tone of a faceted surface - the median of the samples whose
#                     lightness ranks in that quantile slice (other hues dropped first)
# --------------------------------------------------------------------------
def P(name, pts, **kw):
    """A polygon (straight edges)."""
    return dict(kind="poly", name=name, pts=[(float(x), float(y)) for x, y in pts], **kw)


def R(name, x0, y0, x1, y1, **kw):
    """An axis-aligned rectangle."""
    return P(name, [(x0, y0), (x1, y0), (x1, y1), (x0, y1)], **kw)


def C(name, cx, cy, r, **kw):
    """A circle."""
    return dict(kind="ellipse", name=name, cx=float(cx), cy=float(cy), rx=float(r), ry=float(r), rot=0.0, **kw)


def E(name, cx, cy, rx, ry, rot=0.0, **kw):
    """An ellipse, rotated by rot degrees about its centre."""
    return dict(kind="ellipse", name=name, cx=float(cx), cy=float(cy), rx=float(rx), ry=float(ry),
                rot=float(rot), **kw)


def A(name, pts, **kw):
    """A shape of straight edges and circular arcs: pts holds (x, y) corners and
    ("arc", r, x, y) entries, an arc of radius r (clockwise) to (x, y), or ("arcin", r, x, y),
    counter-clockwise (a concave side when the outline runs clockwise)."""
    return dict(kind="arcs", name=name, pts=list(pts), **kw)


def O(oid, layer, faces, behind=(), holes=False):
    """An object: its faces back to front. behind: objects drawn earlier in the stack that
    stand in front of this one (it is clipped by their outlines in its layer)."""
    names = [f["name"] for f in faces]
    dup = {n for n in names if names.count(n) > 1}
    assert not dup, f"{oid}: duplicate face names {sorted(dup)}"
    return dict(id=oid, layer=layer, faces=faces, behind=list(behind), holes=holes)


# --------------------------------------------------------------------------
# Geometry helpers
# --------------------------------------------------------------------------
def arc_points(p0, r, p1, segs=CURVE_SEGS, sweep=1):
    """Points along a circular arc of radius r from p0 to p1 (the short way, clockwise in
    screen space for SVG sweep-flag 1, counter-clockwise for 0), end point excluded."""
    if not sweep:
        pts = arc_points(p1, r, p0, segs, 1)
        return [p0] + pts[1:][::-1]
    (x0, y0), (x1, y1) = p0, p1
    dx, dy = x1 - x0, y1 - y0
    d = math.hypot(dx, dy)
    r = max(r, d / 2)
    mx, my = (x0 + x1) / 2, (y0 + y1) / 2
    h = math.sqrt(max(r * r - d * d / 4, 0))
    # centre to the right of the chord direction for a clockwise (sweep 1) short arc
    cx, cy = mx - h * dy / d, my + h * dx / d
    a0, a1 = math.atan2(y0 - cy, x0 - cx), math.atan2(y1 - cy, x1 - cx)
    while a1 < a0:
        a1 += 2 * math.pi
    n = max(2, int(segs * (a1 - a0) / (2 * math.pi)) + 2)
    return [(cx + r * math.cos(a0 + (a1 - a0) * t / n), cy + r * math.sin(a0 + (a1 - a0) * t / n))
            for t in range(n)]


def outline(f):
    """The face as a polygon (arcs and ellipses flattened): for measuring and clipping."""
    if f["kind"] == "poly":
        return f["pts"]
    if f["kind"] == "ellipse":
        c, s = math.cos(math.radians(f["rot"])), math.sin(math.radians(f["rot"]))
        out = []
        for k in range(CURVE_SEGS):
            a = 2 * math.pi * k / CURVE_SEGS
            x, y = f["rx"] * math.cos(a), f["ry"] * math.sin(a)
            out.append((f["cx"] + x * c - y * s, f["cy"] + x * s + y * c))
        return out
    pts, out = f["pts"], []
    for i, p in enumerate(pts):
        if isinstance(p[0], str):
            p = (p[2], p[3])
        q = pts[(i + 1) % len(pts)]
        if isinstance(q[0], str):
            out += arc_points(p, q[1], (q[2], q[3]), sweep=1 if q[0] == "arc" else 0)
        else:
            out.append(p)
    return out


def corners(f):
    """The corners of an arcs face (arc end points included)."""
    return [(p[2], p[3]) if isinstance(p[0], str) else p for p in f["pts"]]


def path_d(f):
    """SVG path data of a poly or arcs face."""
    if f["kind"] == "poly":
        return "M " + " L ".join(f"{fmt(x)} {fmt(y)}" for x, y in f["pts"]) + " Z"
    pts = f["pts"]
    d = f"M {fmt(pts[0][0])} {fmt(pts[0][1])}"
    for p in pts[1:]:
        if isinstance(p[0], str):
            d += f" A {fmt(p[1])} {fmt(p[1])} 0 0 {1 if p[0] == 'arc' else 0} {fmt(p[2])} {fmt(p[3])}"
        else:
            d += f" L {fmt(p[0])} {fmt(p[1])}"
    return d + " Z"


def raster(pts, size=(W, H)):
    im = Image.new("L", size, 0)
    if len(pts) >= 3:
        ImageDraw.Draw(im).polygon([(float(x), float(y)) for x, y in pts], fill=255)
    return np.asarray(im) > 0


def bounds(obj):
    xs, ys = [], []
    for f in obj["faces"]:
        for x, y in outline(f):
            xs.append(x)
            ys.append(y)
    return min(xs), min(ys), max(xs), max(ys)


# --------------------------------------------------------------------------
# The objects and the measuring
# --------------------------------------------------------------------------
@lru_cache(maxsize=1)
def objects():
    import env_shapes
    # objects are listed per layer, back to front; the stack follows LAYERS
    objs = sorted(env_shapes.OBJECTS, key=lambda o: LAYERS.index(o["layer"]))
    ids = [o["id"] for o in objs]
    assert len(set(ids)) == len(ids), "duplicate object ids"
    for o in objs:
        assert o["layer"] in LAYERS, f"{o['id']}: unknown layer {o['layer']}"
        for b in o["behind"]:
            assert b in ids[:ids.index(o["id"])], f"{o['id']}: behind {b}, which is not drawn before it"
    return objs


def obj(oid):
    return next(o for o in objects() if o["id"] == oid)


@lru_cache(maxsize=1)
def original():
    return np.asarray(Image.open(bt.ORIGINAL).convert("RGB"))


@lru_cache(maxsize=1)
def occluders():
    """The dogs, their shadows, the title and the original's corner sparkle are not environment:
    the original's pixels there are never measured (shared with trace_env)."""
    import trace_env as te
    return te.occluders()


def to_lab(rgb):
    import cv2
    return cv2.cvtColor((np.asarray(rgb, np.float32) / 255.0), cv2.COLOR_RGB2Lab)


@lru_cache(maxsize=None)
def silhouette(oid):
    """Full-frame bool: everything the object covers."""
    m = np.zeros((H, W), bool)
    for f in obj(oid)["faces"]:
        m |= raster(outline(f))
    return m


@lru_cache(maxsize=1)
def zbuffer():
    """(full-frame int: index of the face on top at each pixel or -1, [(object, face)])."""
    z = np.full((H, W), -1, np.int32)
    faces = []
    for o in objects():
        keep = np.ones((H, W), bool)
        for b in o["behind"]:
            keep &= ~silhouette(b)
        # a face moved off where the original shows it is measured where it was (src); a moved
        # object lists its faces as they were (measure), so no measured colour changes by the move
        for f in o.get("measure") or [g.get("src", g) for g in o["faces"]]:
            m = raster(outline(f)) & keep
            if f.get("op", 1.0) >= 0.5 and not f.get("added"):
                z[m] = len(faces)
            faces.append((o["id"], f["name"]))
    return z, faces


def face_key(oid, name):
    return name if "/" in name else f"{oid}/{name}"


@lru_cache(maxsize=1)
def visible_px():
    """{object/face: (ys, xs)} of the original pixels that show each face."""
    import cv2
    z, faces = zbuffer()
    occ = occluders()
    zz = np.where(occ, -1, z)
    out = {}
    order = np.argsort(zz, axis=None, kind="stable")
    flat = zz.ravel()[order]
    starts = np.searchsorted(flat, np.arange(len(faces)))
    ends = np.searchsorted(flat, np.arange(len(faces)), side="right")
    for i, (oid, name) in enumerate(faces):
        idx = order[starts[i]:ends[i]]
        ys, xs = np.divmod(idx, W)
        if len(ys) and FACE_ERODE:
            y0, x0 = ys.min(), xs.min()
            m = np.zeros((ys.max() - y0 + 3, xs.max() - x0 + 3), np.uint8)
            m[ys - y0 + 1, xs - x0 + 1] = 1
            e = cv2.erode(m, np.ones((2 * FACE_ERODE + 1,) * 2, np.uint8))
            ey, ex = np.nonzero(e)
            if len(ey) >= FACE_MIN:
                ys, xs = ey + y0 - 1, ex + x0 - 1
        out[f"{oid}/{name}"] = (ys, xs)
    return out


def samples(oid, f):
    """(xs, ys, rgb) the face's colour is measured from, or None when it is hidden."""
    if "at" in f:
        m = raster(f["at"]) & ~occluders()
        ys, xs = np.nonzero(m)
    elif "of" in f:
        vp = visible_px()
        parts = [vp[face_key(oid, n)] for n in f["of"]]
        ys = np.concatenate([p[0] for p in parts])
        xs = np.concatenate([p[1] for p in parts])
    else:
        ys, xs = visible_px()[f"{oid}/{f['name']}"]
    if len(ys) < FACE_MIN:
        return None
    rgb = original()[ys, xs].astype(np.float64)
    lab = to_lab(rgb[None].astype(np.uint8))[0]
    if "q" in f:
        # one tone of a faceted surface: drop other hues (chroma only, so every lightness
        # of the surface stays), then keep the slice q = (lo, hi) of the lightness ranks
        keep = np.linalg.norm(lab[:, 1:] - np.median(lab[:, 1:], axis=0), axis=-1) < FACE_DE
        lo, hi = np.quantile(lab[keep, 0], f["q"])
        keep &= (lab[:, 0] >= lo) & (lab[:, 0] <= hi)
        if keep.sum() < FACE_MIN:
            return None
        return xs[keep], ys[keep], rgb[keep]
    keep = np.linalg.norm(lab - np.median(lab, axis=0), axis=-1) < FACE_DE
    if keep.sum() < FACE_MIN:
        keep[:] = True
    return xs[keep], ys[keep], rgb[keep]


def hexc(rgb):
    return "#%02x%02x%02x" % tuple(int(v) for v in np.clip(np.round(rgb), 0, 255))


def fit_gradient(xs, ys, rgb, pts):
    """A linear gradient fitted (least squares) to colours rgb at (xs, ys), spanning the
    face pts: (ends or None when flat, colour at start, colour at end)."""
    lo, hi = np.percentile(rgb, 3, axis=0), np.percentile(rgb, 97, axis=0)
    A_ = np.stack([np.ones(len(xs)), xs, ys], -1).astype(np.float64)
    coef = np.linalg.lstsq(A_, rgb, rcond=None)[0]
    g = coef[1:] @ np.array([0.299, 0.587, 0.114])
    if np.hypot(*g) < FACE_FLAT or len(xs) < 40:
        c = np.median(rgb, axis=0)
        return None, c, c
    d = g / np.hypot(*g)
    cx, cy = float(np.mean(xs)), float(np.mean(ys))
    t = np.array(pts, np.float64) @ d - (cx * d[0] + cy * d[1])
    base = coef[0] + coef[1] * cx + coef[2] * cy
    along = coef[1] * d[0] + coef[2] * d[1]
    ends = [(cx + v * d[0], cy + v * d[1]) for v in (t.min(), t.max())]
    return ends, np.clip(base + along * t.min(), lo, hi), np.clip(base + along * t.max(), lo, hi)


def row_stops(xs, ys, rgb, pts):
    """A vertical gradient over the face's height: one stop per ROW_BIN rows, the median of
    the rows the original shows, interpolated over the rows it hides."""
    y_lo, y_hi = min(p[1] for p in pts), max(p[1] for p in pts)
    centres, cols = [], []
    for b0 in range(int(math.floor(ys.min() / ROW_BIN)) * ROW_BIN, int(ys.max()) + 1, ROW_BIN):
        sel = (ys >= b0) & (ys < b0 + ROW_BIN)
        if sel.sum() >= ROW_MIN:
            centres.append(b0 + ROW_BIN / 2)
            cols.append(np.median(rgb[sel], axis=0))
    cols = np.array(cols)
    if ROW_SMOOTH and len(cols) > 2 * ROW_SMOOTH:
        k = np.ones(2 * ROW_SMOOTH + 1) / (2 * ROW_SMOOTH + 1)
        pad = np.pad(cols, ((ROW_SMOOTH, ROW_SMOOTH), (0, 0)), mode="edge")
        cols = np.stack([np.convolve(pad[:, c], k, mode="valid") for c in range(3)], -1)
    stops = [(y_lo, cols[0])] + list(zip(centres, cols)) + [(y_hi, cols[-1])]
    return y_lo, y_hi, [((y - y_lo) / max(y_hi - y_lo, 1e-6), c) for y, c in stops if y_lo <= y <= y_hi]


@lru_cache(maxsize=1)
def colours():
    """{object/face: ("flat", rgb) | ("grad", ends, c0, c1) | ("rows", y0, y1, stops)}."""
    out, pending, missing = {}, [], []
    for o in objects():
        for f in o["faces"]:
            key = f"{o['id']}/{f['name']}"
            if "c" in f:
                out[key] = ("flat", np.array(bt_rgb(f["c"]), np.float64))
                continue
            if "like" in f:
                pending.append((key, face_key(o["id"], f["like"]), f.get("k", 1.0)))
                continue
            s = samples(o["id"], f)
            if s is None:
                hid = f.get("hid")
                if not hid:
                    missing.append(key)
                    continue
                if hid.startswith("#"):
                    out[key] = ("flat", np.array(bt_rgb(hid), np.float64) * f.get("k", 1.0))
                else:
                    pending.append((key, face_key(o["id"], hid), f.get("k", 1.0)))
                continue
            xs, ys, rgb = s
            pts = outline(f)
            if f.get("rows"):
                out[key] = ("rows", *row_stops(xs, ys, rgb, pts))
            elif f.get("grad"):
                ends, c0, c1 = fit_gradient(xs, ys, rgb, pts)
                out[key] = ("flat", c0) if ends is None else ("grad", ends, c0, c1)
            else:
                out[key] = ("flat", np.median(rgb, axis=0) * f.get("k", 1.0))
    assert not missing, f"the original shows none of these faces; say where their colour comes from (hid=): {missing}"
    for _ in range(4):
        for key, src, k in list(pending):
            if src in out:
                c = out[src]
                if k != 1.0:
                    c = scale_colour(c, k)
                out[key] = c
                pending.remove((key, src, k))
    assert not pending, f"unresolved colours: {pending}"
    return out


def bt_rgb(h):
    return [int(h[i:i + 2], 16) for i in (1, 3, 5)]


def scale_colour(c, k):
    if c[0] == "flat":
        return ("flat", c[1] * k)
    if c[0] == "grad":
        return ("grad", c[1], c[2] * k, c[3] * k)
    return ("rows", c[1], c[2], [(o, v * k) for o, v in c[3]])


# --------------------------------------------------------------------------
# SVG
# --------------------------------------------------------------------------
def fill_of(key, c):
    """(fill attribute value, <defs> for it)."""
    gid = key.replace("/", "-") + "-grad"
    if c[0] == "flat":
        return hexc(c[1]), ""
    if c[0] == "grad":
        (x1, y1), (x2, y2) = c[1]
        return f"url(#{gid})", (
            f'<linearGradient id="{gid}" x1="{fmt(x1)}" y1="{fmt(y1)}" x2="{fmt(x2)}" y2="{fmt(y2)}" '
            f'gradientUnits="userSpaceOnUse"><stop offset="0" stop-color="{hexc(c[2])}"/>'
            f'<stop offset="1" stop-color="{hexc(c[3])}"/></linearGradient>\n')
    _, y0, y1, stops = c
    st = "".join(f'<stop offset="{fmt(min(max(o, 0), 1))}" stop-color="{hexc(v)}"/>' for o, v in stops)
    return f"url(#{gid})", (f'<linearGradient id="{gid}" x1="0" y1="{fmt(y0)}" x2="0" y2="{fmt(y1)}" '
                            f'gradientUnits="userSpaceOnUse">{st}</linearGradient>\n')


def face_svg(oid, f):
    key = f"{oid}/{f['name']}"
    fill, defs = fill_of(key, colours()[key])
    op = f' opacity="{fmt(f["op"])}"' if f.get("op", 1.0) != 1.0 else ""
    seam = "" if f.get("op", 1.0) != 1.0 or f.get("seam") is False else (
        f' stroke="{fill}" stroke-width="{fmt(SEAM)}" stroke-linejoin="round"')
    fid = f'id="{oid}-{f["name"]}"'
    if f["kind"] == "ellipse":
        rot = f' transform="rotate({fmt(f["rot"])} {fmt(f["cx"])} {fmt(f["cy"])})"' if f["rot"] else ""
        if f["rx"] == f["ry"]:
            el = f'<circle {fid} cx="{fmt(f["cx"])}" cy="{fmt(f["cy"])}" r="{fmt(f["rx"])}"'
        else:
            el = (f'<ellipse {fid} cx="{fmt(f["cx"])}" cy="{fmt(f["cy"])}" rx="{fmt(f["rx"])}" '
                  f'ry="{fmt(f["ry"])}"{rot}')
        return f'{el} fill="{fill}"{seam}{op}/>', defs
    return f'<path {fid} d="{path_d(f)}" fill="{fill}"{seam}{op}/>', defs


def group(gid, body, extra=""):
    return f'<g id="{gid}" inkscape:label="{gid}"{extra}>{body}</g>\n'


def object_svg(oid):
    """(group, defs) of one object, complete (no clip)."""
    body, defs = "", ""
    for f in obj(oid)["faces"]:
        b, d = face_svg(oid, f)
        body += b
        defs += d
    return group(oid, body), defs


def clip_d(oids):
    """Path data: the canvas (with margin) minus the union of the objects' outlines."""
    import pathops
    def path_of(pts):
        p = pathops.Path()
        pen = p.getPen()
        pen.moveTo(pts[0])
        for q in pts[1:]:
            pen.lineTo(q)
        pen.closePath()
        return p
    m = 200
    canvas = path_of([(-m, -m), (W + m, -m), (W + m, H + m), (-m, H + m)])
    holes = pathops.Path()
    for oid in oids:
        for f in obj(oid)["faces"]:
            holes = pathops.op(holes, path_of([(round(x, 2), round(y, 2)) for x, y in outline(f)]),
                               pathops.PathOp.UNION)
    out = pathops.op(canvas, holes, pathops.PathOp.DIFFERENCE)
    d = []
    for verb, pts in out:
        if verb == pathops.PathVerb.MOVE:
            d.append(f"M {fmt(pts[0][0])} {fmt(pts[0][1])}")
        elif verb == pathops.PathVerb.LINE:
            d.append(f"L {fmt(pts[0][0])} {fmt(pts[0][1])}")
        elif verb == pathops.PathVerb.CLOSE:
            d.append("Z")
        else:
            raise ValueError(verb)
    return " ".join(d)


# --------------------------------------------------------------------------
# Sky and sun: a vertical gradient and a radial glow, fitted to the original's open sky
# --------------------------------------------------------------------------
SKY_BOTTOM = 760            # nothing below this is sky
SKY_STEP = 24               # rows per sky gradient stop
SUN_C = (1700, 92)          # sun disc centre and radius, measured on the original
SUN_R = 105
SUN_GLOW_R = 560            # glow fitted out to this radius
SUN_TAPER = 0.3             # share of the glow's radius over which it fades out


@lru_cache(maxsize=1)
def open_sky():
    """Full-frame bool: the original's pixels that show plain sky (no object, no occluder).
    Our clouds are round where the original's are blocky, so it is the original's clouds
    (env_shapes.ORIGINAL_CLOUDS), not ours, that are kept out."""
    import cv2
    import env_shapes
    z, faces = zbuffer()
    cloud = [i for i, (oid, _) in enumerate(faces) if obj(oid)["layer"] == "clouds"]
    solid = (z >= 0) & ~np.isin(z, cloud)
    for pts in env_shapes.ORIGINAL_CLOUDS:
        solid |= raster(pts)
    occ = cv2.dilate(occluders().astype(np.uint8), np.ones((49, 49), np.uint8)).astype(bool)
    obj_ = cv2.dilate(solid.astype(np.uint8), np.ones((9, 9), np.uint8)).astype(bool)
    lab = to_lab(original())
    yy = np.arange(H)[:, None]
    return ~occ & ~obj_ & (yy < SKY_BOTTOM) & (lab[..., 2] < -4)


@lru_cache(maxsize=1)
def sky_rows():
    o = original().astype(np.float64)
    yy, xx = np.mgrid[0:H, 0:W]
    sky = open_sky() & (np.hypot(xx - SUN_C[0], yy - SUN_C[1]) > SUN_GLOW_R)
    rows = []
    for y in range(0, SKY_BOTTOM, SKY_STEP):
        sel = sky[y:y + SKY_STEP]
        if sel.sum() > 200:
            rows.append((y + SKY_STEP / 2, np.median(o[y:y + SKY_STEP][sel], axis=0)))
    return rows


@lru_cache(maxsize=1)
def sun_fit():
    """(disc colour, [(offset, opacity)]): the glow's opacity per radius that best turns the
    fitted sky into the original's colour, in the disc's colour."""
    o = original().astype(np.float64)
    ys = np.array([r[0] for r in sky_rows()])
    cs = np.array([r[1] for r in sky_rows()])
    sky_col = np.stack([np.interp(np.arange(H), ys, cs[:, c]) for c in range(3)], -1)
    yy, xx = np.mgrid[0:H, 0:W]
    rr = np.hypot(xx - SUN_C[0], yy - SUN_C[1])
    core = np.median(o[rr < SUN_R - 6], axis=0)
    sky = open_sky()
    stops, step = [], 16
    for r0 in range(SUN_R + 4, SUN_GLOW_R, step):
        sel = (rr >= r0) & (rr < r0 + step) & sky
        if sel.sum() < 50:
            continue
        g, s = o[sel], sky_col[yy[sel]]
        d = core - s
        a = float(((g - s) * d).sum() / max((d * d).sum(), 1e-6))
        stops.append((r0 + step / 2, min(max(a, 0.0), 1.0)))
    out, last = [], 1.0
    for r, a in stops:
        last = min(last, a)
        o = r / SUN_GLOW_R
        # tapered to nothing over the outer SUN_TAPER of the glow, so its rim never shows
        out.append((o, round(last * min(1.0, (1 - o) / SUN_TAPER), 3)))
    out.append((1.0, 0.0))
    return core, out


def sky_parts():
    stops = [(r[0] / H, hexc(r[1])) for r in sky_rows()]
    stops = [(0.0, stops[0][1])] + stops + [(1.0, stops[-1][1])]
    st = "".join(f'<stop offset="{fmt(o)}" stop-color="{c}"/>' for o, c in stops)
    defs = f'<linearGradient id="sky-grad" x1="0" y1="0" x2="0" y2="{H}" gradientUnits="userSpaceOnUse">{st}</linearGradient>\n'
    return group("sky-gradient", f'<rect x="0" y="0" width="{W}" height="{H}" fill="url(#sky-grad)"/>'), defs


def sun_parts():
    core, stops = sun_fit()
    cx, cy = SUN_C
    st = "".join(f'<stop offset="{fmt(o)}" stop-color="{hexc(core)}" stop-opacity="{fmt(a)}"/>' for o, a in
                 [(SUN_R / SUN_GLOW_R, stops[0][1])] + stops)
    defs = (f'<radialGradient id="sun-glow-grad" cx="{cx}" cy="{cy}" r="{SUN_GLOW_R}" '
            f'gradientUnits="userSpaceOnUse">{st}</radialGradient>\n')
    body = (group("sun-glow", f'<circle cx="{cx}" cy="{cy}" r="{SUN_GLOW_R}" fill="url(#sun-glow-grad)"/>')
            + group("sun-disc", f'<circle cx="{cx}" cy="{cy}" r="{SUN_R}" fill="{hexc(core)}"/>'))
    return body, defs


def env_layers():
    """{layer name: SVG} of every environment layer."""
    import build_scene as bs
    sb, sd = sky_parts()
    ub, ud = sun_parts()
    out = {"sky": bs.layer_svg("sky", sb, sd), "sun": bs.layer_svg("sun", ub, ud)}
    for layer in LAYERS[2:]:
        body, defs = "", ""
        for o in objects():
            if o["layer"] != layer:
                continue
            g, d = object_svg(o["id"])
            defs += d
            if o["behind"]:
                cid = f"clip-{o['id']}"
                defs += (f'<clipPath id="{cid}" clipPathUnits="userSpaceOnUse">'
                         f'<path d="{clip_d(o["behind"])}"/></clipPath>\n')
                g = f'<g clip-path="url(#{cid})">\n{g}</g>\n'
            body += g
        out[layer] = bs.layer_svg(layer, body, defs)
    return out


def standalone(oid):
    """(svg, render width, box) of one object alone, viewBox cropped tight around it."""
    import build_scene as bs
    if oid == "sky":
        x0, y0, x1, y1 = 0, 0, W, H
        g, d = sky_parts()
        layer = "sky"
    elif oid == "sun":
        r = SUN_GLOW_R
        x0, y0, x1, y1 = SUN_C[0] - r, SUN_C[1] - r, SUN_C[0] + r, SUN_C[1] + r
        g, d = sun_parts()
        layer = "sun"
    else:
        x0, y0, x1, y1 = bounds(obj(oid))
        x0, y0 = math.floor(x0 - OBJECT_PAD), math.floor(y0 - OBJECT_PAD)
        x1, y1 = math.ceil(x1 + OBJECT_PAD), math.ceil(y1 + OBJECT_PAD)
        g, d = object_svg(oid)
        layer = obj(oid)["layer"]
    svg = bs.layer_svg(layer, g, d)
    w, h = x1 - x0, y1 - y0
    svg = re.sub(r'width="\d+" height="\d+" viewBox="0 0 \d+ \d+"',
                 f'width="{w}" height="{h}" viewBox="{x0} {y0} {w} {h}"', svg, count=1)
    width = OBJECT_LONG if w >= h else int(-(-OBJECT_LONG * w // h))
    return svg, width, (x0, y0, x1, y1)


def object_ids():
    return ["sky", "sun"] + [o["id"] for o in objects()]


def clear_caches():
    for f in (objects, original, occluders, silhouette, zbuffer, visible_px, colours, open_sky, sky_rows,
              sun_fit):
        f.cache_clear()


# --------------------------------------------------------------------------
# The primitives check
# --------------------------------------------------------------------------
ALLOWED = {"svg", "title", "desc", "defs", "g", "path", "rect", "circle", "ellipse", "linearGradient",
           "radialGradient", "stop", "clipPath"}
CMD = re.compile(r"[A-Za-z]")
NUM = re.compile(r"-?\d+(?:\.\d+)?")


def check_primitives(svg, name):
    """Every shape in svg is an authored primitive, not tracer output: only the allowed
    elements (no <image>, no embedded bitmap); path data only of straight lines (M L H V Z)
    and circular arcs (A) - no Bezier curves (C S Q T), which is what VTracer emits; at most
    MAX_VERTS vertices per subpath (a traced outline has hundreds), except a clip outline,
    which is built from the authored faces; coordinates with at most 2 decimals. Returns the
    number of shapes checked."""
    root = ET.fromstring(svg.encode("utf-8"))
    n = 0
    clip_ids = set()
    for el in root.iter():
        tag = el.tag.split("}")[-1]
        assert tag in ALLOWED, f"{name}: <{tag}> is not an authored primitive"
        if tag == "clipPath":
            clip_ids |= {c.get("id") for c in el.iter() if c.get("id")} | {id(c) for c in el.iter()}
        for k, v in el.attrib.items():
            assert "data:" not in v, f"{name}: embedded data in {tag}"
    for el in root.iter():
        tag = el.tag.split("}")[-1]
        if tag not in ("path", "rect", "circle", "ellipse"):
            continue
        n += 1
        if tag != "path":
            continue
        d = el.get("d", "")
        cmds = set(CMD.findall(d))
        assert cmds <= set("MLHVZAmlhvza"), f"{name}: {el.get('id')} has curve commands {sorted(cmds - set('MLHVZA'))}"
        for v in NUM.findall(d):
            assert len(v.split(".")[1]) <= 2 if "." in v else True, f"{name}: {el.get('id')} has coordinate {v}"
        if id(el) in clip_ids:
            continue
        for sub in re.split(r"(?=[Mm])", d):
            verts = len(re.findall(r"[LlAaHhVv]", sub)) + 1 if sub.strip() else 0
            assert verts <= MAX_VERTS, f"{name}: {el.get('id')} has a subpath of {verts} vertices"
    return n


# --------------------------------------------------------------------------
# The street layout check (README "Street layout invariants", street-1 ... street-7)
# --------------------------------------------------------------------------
STREET_TOL = 0.5          # px: how close two edges that should meet must be
STEPS_CENTRE_K = 0.05     # street-1: steps' centre within this share of the door's width of its centre
STEPS_DOOR_TOL = 1.0      # street-1: the door's bottom rests on the steps within this (px)
MB_OFF_MAX = 1.0          # street-4: post centre at most this many sidewalk widths onto the lawn
MB_GAP_MAX = 1.5          # street-4: footprint at most this many slabs nearer than its path
MB_OFF_TOL = 0.01         # street-4: every post foot this close (sidewalk widths) to the common offset ...
MB_SLAB_TOL = 0.01        # ... and this close (slabs) to the common distance nearer than its own path
MB_H_TOL = 0.01           # street-7: every mailbox's height within this share of the common height
MB_AREA_EPS = 0.01        # px2: overlaps smaller than this are rounding


def _pp(pts):
    import pathops
    p = pathops.Path()
    pen = p.getPen()
    pen.moveTo(tuple(pts[0]))
    for q in pts[1:]:
        pen.lineTo(tuple(q))
    pen.closePath()
    return p


def _union(polys):
    import pathops
    out = pathops.Path()
    for pts in polys:
        out = pathops.op(out, _pp(pts), pathops.PathOp.UNION)
    return out


def overlap(a, b):
    """Area (px2) where the polygon sets a and b overlap."""
    import pathops
    if not a or not b:
        return 0.0
    return abs(pathops.op(_union(a), _union(b), pathops.PathOp.INTERSECTION).area)


def _bbox(polys):
    xs = [x for pts in polys for x, _ in pts]
    ys = [y for pts in polys for _, y in pts]
    return min(xs), min(ys), max(xs), max(ys)


def _dist_to_poly(p, pts):
    """0 inside the polygon, else the distance to its outline."""
    x, y = p
    inside = False
    for (x0, y0), (x1, y1) in zip(pts, pts[1:] + pts[:1]):
        if (y0 > y) != (y1 > y) and x < x0 + (x1 - x0) * (y - y0) / (y1 - y0):
            inside = not inside
    if inside:
        return 0.0
    best = math.inf
    for (x0, y0), (x1, y1) in zip(pts, pts[1:] + pts[:1]):
        dx, dy = x1 - x0, y1 - y0
        t = 0.0 if dx == dy == 0 else max(0.0, min(1.0, ((x - x0) * dx + (y - y0) * dy) / (dx * dx + dy * dy)))
        best = min(best, math.hypot(x - x0 - t * dx, y - y0 - t * dy))
    return best


def _span_at(pts, y):
    """(min x, max x) where the horizontal line at height y crosses the polygon, or None."""
    xs = []
    for (x0, y0), (x1, y1) in zip(pts, pts[1:] + pts[:1]):
        if y0 != y1 and min(y0, y1) <= y <= max(y0, y1):
            xs.append(x0 + (x1 - x0) * (y - y0) / (y1 - y0))
    return (min(xs), max(xs)) if xs else None


def _top_at(pts, x):
    """The smallest y where the vertical line at x crosses the polygon, or None."""
    ys = []
    for (x0, y0), (x1, y1) in zip(pts, pts[1:] + pts[:1]):
        if x0 != x1 and min(x0, x1) <= x <= max(x0, x1):
            ys.append(y0 + (y1 - y0) * (x - x0) / (x1 - x0))
        elif x0 == x1 == x:
            ys += [y0, y1]
    return min(ys) if ys else None


def steps_foot(pts):
    """The two ends of a steps face's bottom edge: at its left and right ends, the lowest corner."""
    xs = [x for x, _ in pts]
    lo, hi = min(xs), max(xs)
    left = max((p for p in pts if p[0] - lo <= 5), key=lambda p: p[1])
    right = max((p for p in pts if hi - p[0] <= 5), key=lambda p: p[1])
    return left, right


def mailbox_parts(o):
    """(post footprint foot y, post x0, post x1, box top y, all outlines) of a mailbox object."""
    post = next(f for f in o["faces"] if f["name"] == "post")["pts"]
    allp = [outline(f) for f in o["faces"] if f["name"] != "shadow"]
    box = [outline(f) for f in o["faces"] if f["name"] not in ("shadow", "post", "post-side", "latch")
           and not f["name"].startswith("flag")]
    foot = max(y for _, y in post)
    bottom = [x for x, y in post if foot - y < 3]
    return foot, min(bottom), max(bottom), min(y for pts in box for _, y in pts), min(y for _, y in post), allp


def street_lots():
    import env_shapes as es
    return es.STREET


def check_street():
    """Check the street layout invariants (README) from the geometry, for every house in
    env_shapes.STREET. Returns a list of failures (empty when every rule holds)."""
    import env_shapes as es
    fails = []
    lots = es.STREET
    by_id = {o["id"]: o for o in objects()}
    face = lambda oid, n: next((f for f in by_id[oid]["faces"] if f["name"] == n), None)

    def polys(oid, names):
        return [outline(face(oid, n)) for n in names if face(oid, n)]

    # street-3: one mailbox per house, every mailbox someone's
    mbs = [l["mailbox"] for l in lots]
    all_mbs = sorted(o["id"] for o in objects() if o["id"].startswith("mailbox-"))
    if sorted(mbs) != all_mbs or len(set(mbs)) != len(mbs):
        fails.append(f"street-3: mailboxes per house {sorted(mbs)} != the mailboxes drawn {all_mbs}")
    doors = {l["house"]: outline(face(l["house"], l["door"])) for l in lots}
    steps = {l["house"]: polys(l["house"], l["steps"]) for l in lots}
    paths = {l["house"]: [outline(face(l["path"], "slab")), outline(face(l["path"], "edge"))] for l in lots}
    near_edge = {l["house"]: max(y for pts in paths[l["house"]] for _, y in pts) for l in lots}
    rule = {}
    for l in lots:
        h, side = l["house"], l["side"]
        dx0, dy0, dx1, dy1 = _bbox([doors[h]])
        # street-1
        has_steps = bool(l["steps"]) and all(face(h, n) for n in l["steps"])
        if not has_steps:
            fails.append(f"street-1: {h} has no front steps")
        else:
            fx0, fy0, fx1, fy1 = _bbox(polys(h, l["front"]))
            c, dc = (fx0 + fx1) / 2, (dx0 + dx1) / 2
            if abs(c - dc) > STEPS_CENTRE_K * (dx1 - dx0):
                fails.append(f"street-1: {h} steps centred at x {c:.1f}, its door at {dc:.1f} "
                             f"(off by {abs(c - dc):.1f}, allowed {STEPS_CENTRE_K * (dx1 - dx0):.1f})")
            sx0, sy0, sx1, sy1 = _bbox(steps[h])
            if not (min(sy0, fy0) - STEPS_DOOR_TOL <= dy1 <= sy1):
                fails.append(f"street-1: {h} door's bottom (y {dy1:g}) does not rest on its steps (y {min(sy0, fy0):g}-{sy1:g})")
        # street-2
        slab, edge = paths[h]
        joint = outline(face(l["path"], "joint"))
        if has_steps:
            for p in steps_foot(outline(face(h, l["foot"]))):
                top = _top_at(slab, p[0])
                if _dist_to_poly(p, slab) > STREET_TOL or top is None or p[1] > top + STREET_TOL:
                    fails.append(f"street-2: {h} steps' foot at {p} is not on its path's far edge")
            foot = steps_foot(outline(face(h, l["foot"])))
            far = min(y for _, y in foot)
        else:
            far = None
        top_y = min(y for _, y in slab)
        bot_y = max(y for _, y in edge)
        if far is not None and abs(top_y - far) > STREET_TOL:
            fails.append(f"street-2: {h} path's far edge at y {top_y:g}, its steps' foot at {far:g}")
        for y in (top_y + 0.1, (top_y + bot_y) / 2, bot_y - 0.1):
            sp = _span_at(slab, y) or _span_at(edge, y)
            e = es.lawn_edge(side, y)
            end = sp[1] if side == "l" else sp[0]
            if abs(end - e) > STREET_TOL:
                fails.append(f"street-2: {h} path ends at x {end:.1f} at y {y:.1f}, the sidewalk at {e:.1f}")
                break
        # every corner off the sidewalk is on the far edge, the slab's lower edge or the near edge,
        # or (steps seen from the side) along the steps' foot
        rows = (top_y, max(y for _, y in slab), bot_y)
        bent = [(x, y) for x, y in slab + edge
                if min(abs(y - r) for r in rows) > STREET_TOL and abs(x - es.lawn_edge(side, y)) > STREET_TOL
                and (far is None or not far - STREET_TOL <= y <= max(p[1] for p in foot) + STREET_TOL)]
        if bent:
            fails.append(f"street-2: {h} path is not straight (corner ({bent[0][0]:g}, {bent[0][1]:g}))")
        jy0, jy1 = min(y for _, y in joint), max(y for _, y in joint)
        if abs(jy0 - top_y) > STREET_TOL or abs(jy1 - bot_y) > STREET_TOL:
            fails.append(f"street-2: {h} joint line y {jy0:g}-{jy1:g}, its path's end y {top_y:g}-{bot_y:g}")
        # street-4 / 5 / 6
        m = by_id.get(l["mailbox"])
        if m is None:
            continue
        foot_y, px0, px1, top, _, allp = mailbox_parts(m)
        du = es.post_depth(side, foot_y, px1 - px0)
        fp = [(px0, foot_y - du), (px1, foot_y - du), (px1, foot_y), (px0, foot_y)]
        e, ww = es.lawn_edge(side, foot_y), es.walk_width(side, foot_y)
        on_lawn = px1 < e if side == "l" else px0 > e
        off = (e - (px0 + px1) / 2) / ww * (1 if side == "l" else -1)
        if not on_lawn or off > MB_OFF_MAX:
            fails.append(f"street-4: {l['mailbox']} post (x {px0:g}-{px1:g} at y {foot_y:g}) is not on the lawn "
                         f"just behind the sidewalk (edge x {e:.1f}, {off:.2f} sidewalk widths onto the lawn)")
        gap = es.slabs_between(side, near_edge[h], foot_y - du)
        if not 0 < gap <= MB_GAP_MAX:
            fails.append(f"street-4: {l['mailbox']} footprint is {gap:.2f} slabs nearer than {h}'s path "
                         f"(allowed 0-{MB_GAP_MAX})")
        between = [o for o in lots if o["side"] == side and o["house"] != h
                   and near_edge[h] < near_edge[o["house"]] < foot_y - du]
        if between:
            fails.append(f"street-4: {l['mailbox']} is nearer {between[0]['house']}'s path than its own")
        for o in lots:
            a = overlap([fp], paths[o["house"]] + steps[o["house"]])
            if a > MB_AREA_EPS:
                fails.append(f"street-5: {l['mailbox']} post stands on {o['house']}'s path or steps ({a:.1f} px2)")
        # street-4 / street-7 in ground units: the post foot's offset from the sidewalk's lawn edge
        # (sidewalk widths), its distance nearer than its own path's near edge (slabs), and its
        # height from foot to box top over the sidewalk's width at its foot (the street's scale there)
        rule[l["mailbox"]] = (off, es.slabs_between(side, near_edge[h], foot_y), (foot_y - top) / ww)
    # street-4 / street-7: one placement and one height for every mailbox, on both sides
    if rule:
        med = [sorted(v[k] for v in rule.values())[len(rule) // 2] for k in range(3)]
        for mid, (off, gap, hh) in rule.items():
            if abs(off - med[0]) > MB_OFF_TOL:
                fails.append(f"street-4: {mid} post foot is {off:.3f} sidewalk widths onto the lawn, "
                             f"the others' {med[0]:.3f}")
            if abs(gap - med[1]) > MB_SLAB_TOL:
                fails.append(f"street-4: {mid} post foot is {gap:.3f} slabs nearer than its path, "
                             f"the others' {med[1]:.3f}")
            if abs(hh - med[2]) > MB_H_TOL * med[2]:
                fails.append(f"street-7: {mid} is {hh:.3f} sidewalk widths tall, the others {med[2]:.3f}")
    return fails


# --------------------------------------------------------------------------
# House perspective invariants (README: "House perspective invariants")
# --------------------------------------------------------------------------
PERSP_TOL = 0.5           # degrees: a horizontal, vertical or vanishing-point edge may be off by this ...
PERSP_PX = 0.02           # ... or its far end off the line by this many px (coordinates carry two decimals)
PERSP_FLAT = 35.0         # an edge within this many degrees of horizontal is a horizontal feature
PERSP_STEEP = 84.0        # an edge steeper than this is a vertical; in between, on a wall, a rake (free)
PLANES = ("front", "side", "tread", "oblique", "free")


def vanishing_point(side):
    """The street's vanishing point on that side: where the sidewalk's two edges meet."""
    import env_shapes as es
    return es.meet(es.LAWN_L, es.ROAD_L) if side == "l" else es.meet(es.ROAD_R, es.LAWN_R)


def _segments(f):
    """The straight edges of a poly or arcs face (an arc is no edge)."""
    if f["kind"] == "poly":
        pts = f["pts"]
        return list(zip(pts, pts[1:] + pts[:1]))
    if f["kind"] != "arcs":
        return []
    pts, out = f["pts"], []
    for i, q in enumerate(pts):
        nxt = pts[(i + 1) % len(pts)]
        if isinstance(nxt[0], str):
            continue
        p = (q[2], q[3]) if isinstance(q[0], str) else q
        out.append((p, nxt))
    return out


def _off_line(a, b, direction):
    """(angle in degrees, px) by which edge a-b misses the direction (dx, dy) through a."""
    ex, ey = b[0] - a[0], b[1] - a[1]
    n = math.hypot(*direction)
    ux, uy = direction[0] / n, direction[1] / n
    px = abs(ex * uy - ey * ux)
    dot = abs(ex * ux + ey * uy)
    return math.degrees(math.atan2(px, dot)), px


def edge_fault(a, b, plane, vp, recede=None):
    """Why edge a-b breaks its face's plane rule, or None. front: a horizontal feature is
    horizontal; side: it lies on a line through the vanishing point; tread (a horizontal
    surface): it is either; oblique (a sill's or box's top or end): level, plumb or along the face's
    `recede` offset; verticals are vertical on every plane; free faces are not checked."""
    dx, dy = b[0] - a[0], b[1] - a[1]
    if plane == "free" or math.hypot(dx, dy) < 1e-9:
        return None
    if plane == "oblique":           # a sill's or box's top or end (persp-6): level, plumb or along its offset
        for d in ((1, 0), (0, 1), recede):
            ang_d, px = _off_line(a, b, d)
            if ang_d <= PERSP_TOL or px <= PERSP_PX:
                return None
        return f"neither level, plumb nor along its offset {recede}"
    ang = math.degrees(math.atan2(abs(dy), abs(dx)))
    if ang >= PERSP_STEEP:
        d, px = _off_line(a, b, (0, 1))
        return None if d <= PERSP_TOL or px <= PERSP_PX else f"vertical off by {d:.2f} deg"
    if ang > PERSP_FLAT and plane != "tread":
        return None              # a rake (a gable's edge, a roof's hip, a chimney on the roof): free on a wall
    mid = ((a[0] + b[0]) / 2, (a[1] + b[1]) / 2)
    hd, hpx = _off_line(a, b, (1, 0))
    vd, vpx = _off_line(a, b, (mid[0] - vp[0], mid[1] - vp[1]))
    ok_h = hd <= PERSP_TOL or hpx <= PERSP_PX
    ok_v = vd <= PERSP_TOL or vpx <= PERSP_PX
    if plane == "front" and not ok_h:
        return f"not horizontal ({ang:.2f} deg)"
    if plane == "side" and not ok_v:
        return f"misses the vanishing point by {vd:.2f} deg"
    if plane == "tread" and not (ok_h or ok_v):
        return f"neither horizontal nor toward the vanishing point ({hd:.2f} / {vd:.2f} deg)"
    return None


def check_perspective():
    """Check the house perspective invariants (README) for every face of every house in
    env_shapes.STREET (near and far, both sides): each face's `plane` (front by default) sets
    the rule its horizontal features follow. Returns {house: [failures]} (empty when all hold)."""
    import env_shapes as es
    by_id = {o["id"]: o for o in objects()}
    out = {}
    for lot in es.STREET:
        h = lot["house"]
        vp = vanishing_point(lot["side"])
        fails = []
        for f in by_id[h]["faces"]:
            plane = f.get("plane", "front")
            assert plane in PLANES, f"{h}/{f['name']}: unknown plane {plane}"
            for a, b in _segments(f):
                why = edge_fault(a, b, plane, vp, f.get("recede"))
                if why:
                    fails.append(f"{f['name']} ({plane}) edge ({a[0]:g}, {a[1]:g})-({b[0]:g}, {b[1]:g}) {why}")
        if fails:
            out[h] = fails
    return out


# --------------------------------------------------------------------------
# Window sills and flower boxes (README: "Window sills and flower boxes")
# --------------------------------------------------------------------------
OVERHANG_K = 0.04        # door widths: a sill's or box's back overhangs its window frame this much each side
OFFSET_X = 0.065         # door widths: a flower box's front is its back moved this far sideways (away from
OFFSET_Y = 0.01          # the vanishing point) and this far down; a plain sill's by half as much
SILL_OFFSET = 0.5        # a plain sill's offset as a share of a flower box's
BOX_CAP_K = 0.09         # door widths: a flower box's sill band ...
BOX_H_K = 0.2            # ... and its box below it, tall
BOX_TOP_K = 1.12         # a top's colour over its front's (lighter) ...
BOX_END_K = 0.78         # ... an end's (darker)
WIN_ATTACH_TOL = 1.0     # px: the back top edge on the frame's bottom (win-1)
WIN_CENTRE_TOL = 0.5     # px: centred on its window (win-2)
WIN_REACH_TOL = 0.5      # px: beyond its overhang plus offset (win-2)
WIN_AREA_EPS = 0.5       # px2: an overlap smaller than this is a shared edge (win-3)
WIN_OFFSET_TOL = 0.002   # door widths: every box's offset the same (win-4)
WIN_PROP_TOL = 0.005     # door widths: every box's proportions the same (win-5)
WIN_ANGLE_TOL = 1.0      # deg: a sill's or box's depth, and a frame's, along its house's one oblique (win-7)
GLASS_BLUE = 30          # a near house's glass: blue at least this much over red, and over green (win-6)
SILL_PARTS = ("sill-top", "sill-end", "sill", "sill-front-end", "sill-front")


def frame_depth(pts, kind, of):
    """The depth a frame's face shows, from its geometry: (vector, side). An outer side face's
    depth edge is its top edge, running from its back (away from its frame) to its front (beside
    the frame's front); a reveal's is its bottom edge, from its back (by the glass or panel) to its
    front (the opening's edge), the top being the opening's level edge. side: +1 if the face is
    right of the centre of the face `of` (x0, y0, x1, y1) it belongs to, -1 if left."""
    xs = [x for x, _ in pts]
    x0, x1 = min(xs), max(xs)
    pick = min if kind == "outer" else max
    a = (x0, pick(y for x, y in pts if abs(x - x0) < 1e-6))
    b = (x1, pick(y for x, y in pts if abs(x - x1) < 1e-6))
    c = (of[0] + of[2]) / 2
    side = 1 if (x0 + x1) / 2 > c else -1
    near_c, far_c = (a, b) if abs(a[0] - c) < abs(b[0] - c) else (b, a)
    front, back = (near_c, far_c) if kind == "outer" else (far_c, near_c)
    return (front[0] - back[0], front[1] - back[1]), side


def along_oblique(d, sx):
    """Why depth vector d (front minus back) is not along its house's oblique (sx * OFFSET_X,
    OFFSET_Y), or None: the same sideways sign, the same angle within WIN_ANGLE_TOL (win-7)."""
    if d[0] == 0 or (d[0] > 0) != (sx > 0):
        return f"({d[0]:+.2f}, {d[1]:+.2f}) goes {'right' if d[0] > 0 else 'left'}, its house's depth " \
               f"{'right' if sx > 0 else 'left'}"
    ang = math.degrees(math.atan2(d[1], abs(d[0])))
    want = math.degrees(math.atan2(OFFSET_Y, OFFSET_X))
    if abs(ang - want) > WIN_ANGLE_TOL:
        return f"({d[0]:+.2f}, {d[1]:+.2f}) slants {ang:.1f} deg, its house's oblique {want:.1f} deg"
    return None


def windows(o):
    """The windows of a house: the prefix of every face named <window>-frame (not its door)."""
    return [f["name"][:-len("-frame")] for f in o["faces"]
            if f["name"].endswith("-frame") and "window" in f["name"]]


def check_windows():
    """Check the window sill and flower box invariants (README: win-1..win-6) for every window of
    every house in env_shapes.STREET, near and far, both sides. Returns (failures, windows
    checked)."""
    import env_shapes as es
    by_id = {o["id"]: o for o in objects()}
    fails, count, offsets, props = [], 0, {}, {}
    col = colours()
    for lot in es.STREET:
        h, side = lot["house"], lot["side"]
        o = by_id[h]
        faces = {f["name"]: f for f in o["faces"]}
        dw = _bbox([outline(faces[lot["door"]])])
        dw = dw[2] - dw[0]
        near = not h.startswith("house-far-")
        sx = es.HOUSE_DEPTH.get(h)
        has_sill = any(f"{n}-sill" in faces for n in windows(o))
        if has_sill and sx is None:
            fails.append(f"win-7: {h} has sills but no depth direction (HOUSE_DEPTH)")
            continue
        for nm, kind, of in es.FRAME_DEPTH.get(h, []):
            d, on = frame_depth(outline(faces[nm]), kind, _bbox([outline(faces[of])]))
            want = -sx if kind == "outer" else sx
            if on != want:
                fails.append(f"win-7: {h}/{nm} ({kind}) shows on the {'right' if on > 0 else 'left'} of {of}, "
                             f"its house's depth goes {'right' if sx > 0 else 'left'}")
            why = along_oblique(d, sx)
            if why:
                fails.append(f"win-7: {h}/{nm} ({kind}) depth edge {why}")
        doors = [outline(f) for n, f in faces.items() if n.startswith("door")]
        for n in windows(o):
            count += 1
            w = f"{h}/{n}"
            glass = col.get(f"{h}/{n}-glass")
            if near and glass is not None:
                r, g, b = (glass[1] if glass[0] == "flat" else glass[2])[:3]
                if not (b >= r + GLASS_BLUE and b >= g):
                    fails.append(f"win-6: {w} glass is {hexc((r, g, b))}, not blue")
            parts = {p: faces[f"{n}-{p}"] for p in SILL_PARTS if f"{n}-{p}" in faces}
            if not parts:
                continue
            fx0, fy0, fx1, fy1 = _bbox([outline(faces[f"{n}-frame"])])
            top = parts.get("sill-top")
            if top is None:
                fails.append(f"win-1: {w} sill has no top face")
                continue
            tp = outline(top)
            ty = min(y for _, y in tp)
            back = sorted(x for x, y in tp if abs(y - ty) < 1e-6)
            fy = max(y for _, y in tp)
            front = sorted(x for x, y in tp if abs(y - fy) < 1e-6)
            if abs(ty - fy1) > WIN_ATTACH_TOL:
                fails.append(f"win-1: {w} sill's back top edge at y {ty:g}, its frame's bottom at {fy1:g}")
            bx0, bx1 = back[0], back[-1]
            c, fc = (bx0 + bx1) / 2, (fx0 + fx1) / 2
            if abs(c - fc) > WIN_CENTRE_TOL:
                fails.append(f"win-2: {w} sill centred at x {c:.1f}, its window at {fc:.1f}")
            box = "sill-front" in parts
            k = 1.0 if box else SILL_OFFSET
            hang = OVERHANG_K * dw
            reach = hang + OFFSET_X * k * dw
            if abs((fx0 - bx0) - (bx1 - fx1)) > 2 * WIN_CENTRE_TOL or (fx0 - bx0) - hang > WIN_REACH_TOL \
                    or (bx1 - fx1) - hang > WIN_REACH_TOL:
                fails.append(f"win-2: {w} sill's back overhangs its frame by {fx0 - bx0:.1f} / {bx1 - fx1:.1f} px "
                             f"(allowed {hang:.1f}, equal)")
            if (fx0 - front[0]) - reach > WIN_REACH_TOL or (front[-1] - fx1) - reach > WIN_REACH_TOL:
                fails.append(f"win-2: {w} sill's front overhangs its frame by {fx0 - front[0]:.1f} / "
                             f"{front[-1] - fx1:.1f} px (allowed {reach:.1f})")
            ax0, _, ax1, _ = _bbox([outline(f) for f in parts.values()])
            reach += WIN_REACH_TOL
            if fx0 - ax0 > reach or ax1 - fx1 > reach:
                fails.append(f"win-2: {w} sill reaches {fx0 - ax0:.1f} / {ax1 - fx1:.1f} px past its frame "
                             f"(allowed {reach:.1f})")
            mine = [outline(f) for f in parts.values()]
            for dn, d in ((nm, outline(f)) for nm, f in faces.items() if nm.startswith("door")):
                a = overlap(mine, [d])
                if a > WIN_AREA_EPS:
                    fails.append(f"win-3: {w} sill overlaps {dn} ({a:.1f} px2)")
            for m in windows(o):
                if m == n:
                    continue
                theirs = [outline(f) for nm, f in faces.items() if nm.startswith(m + "-")]
                a = overlap(mine, theirs)
                if a > WIN_AREA_EPS:
                    fails.append(f"win-3: {w} sill overlaps {h}/{m} ({a:.1f} px2)")
            dx, dy = (front[0] - bx0) / dw / k, (fy - ty) / dw / k
            why = along_oblique((dx, dy), sx)
            if why:
                fails.append(f"win-7: {w} {'box' if box else 'sill'}'s offset {why}")
            ends = [parts[p] for p in ("sill-end", "sill-front-end") if p in parts]
            bc = (front[0] + front[-1]) / 2
            for e in ends:
                ex = _bbox([outline(e)])
                on = 1 if (ex[0] + ex[2]) / 2 > bc else -1
                if on != -sx:
                    fails.append(f"win-7: {w} {e['name'][len(n) + 1:]} shows on the {'right' if on > 0 else 'left'}, "
                                 f"its house's frames' side faces on the {'left' if sx > 0 else 'right'}")
            if abs(abs(dx) - OFFSET_X) > WIN_OFFSET_TOL or abs(dy - OFFSET_Y) > WIN_OFFSET_TOL:
                fails.append(f"win-4: {w} {'box' if box else 'sill'} offset ({abs(dx):.3f}, {dy:.3f}) door widths, "
                             f"every {'box' if box else 'sill'}'s ({OFFSET_X * k:g}, {OFFSET_Y * k:g}) "
                             f"(moved {dx * dw * k:+.1f}, {dy * dw * k:+.1f} px)")
            offsets[w] = (dx, dy)
            if box:
                band = _bbox([outline(parts["sill"])])
                bf = _bbox([outline(parts["sill-front"])])
                cap, hh = (band[3] - band[1]) / dw, (bf[3] - bf[1]) / dw
                if abs(cap - BOX_CAP_K) > WIN_PROP_TOL or abs(hh - BOX_H_K) > WIN_PROP_TOL:
                    fails.append(f"win-5: {w} box's band {cap:.3f} and box {hh:.3f} door widths tall, "
                                 f"every box's {BOX_CAP_K:g} and {BOX_H_K:g}")
                props[w] = (cap, hh)
            front_c = f"{n}-sill-front" if box else f"{n}-sill"
            shades = [("sill-top", BOX_TOP_K), ("sill-end", BOX_END_K)]
            if box:
                shades += [("sill-front-end", BOX_END_K), ("sill", 1.0)]
            for p, kk in shades:
                f = parts.get(p)
                if f is not None and (f.get("like") != front_c or abs(f.get("k", 1.0) - kk) > 1e-9):
                    fails.append(f"win-5: {w} {p} is not {kk:g} x {front_c} "
                                 f"(it is {f.get('k', 1.0):g} x {f.get('like')})")
            if box and "at" not in parts["sill-front"]:
                fails.append(f"win-5: {w} box front's colour is not measured where the original "
                             "shows it plainly lit (at=)")
            if box and "sill-front-end" not in parts:
                fails.append(f"win-5: {w} box has no end face")
    return fails, count


# --------------------------------------------------------------------------
# Ground contact and draw order of trees and bushes (README: "Trees, bushes and depth")
# --------------------------------------------------------------------------
DEPTH_OVERLAP = 20        # px: two objects overlapping on screen by at least this much are checked
DEPTH_TOL = 1.0           # px: ground lines this close are the same depth (either may be in front)
# Pairs left as the original draws them, each with its reason (README: Trees, bushes and depth)
DEPTH_EXEMPT = {("bush-green-house", "house-green/wall"):
                "stands against the wall above the steps; on the ground in front of the wall it would hide "
                "behind the steps and the mailbox (an open question for Derek)"}


WALLS = ("wall", "front-wall", "side-wall")     # a house's walls, each standing at its own depth


def ground_y(o):
    """Where a tree, bush or fence stands on the ground, as a height on screen (larger is nearer):
    a tree's trunk foot, a bush's lowest point, a fence's foot."""
    names = {f["name"]: f for f in o["faces"]}
    if "trunk" in names:
        return max(y for _, y in outline(names["trunk"]))
    return max(y for f in o["faces"] if f["name"] != "shadow" for _, y in outline(f))


def in_front(a, b, order):
    """Whether object a is drawn in front of object b where they overlap."""
    if order[a["id"]] > order[b["id"]]:
        return b["id"] not in a["behind"]
    return a["id"] in b["behind"]


def depth_units():
    """(object, label, ground y, full-frame mask) of everything the depth check compares: each
    tree and bush, each fence, and each house wall (a front wall and a side wall stand at their
    own depths: the foot of each)."""
    import env_shapes as es
    houses = {l["house"] for l in es.STREET}
    out = []
    for o in objects():
        if o["id"] in houses:
            for f in o["faces"]:
                if f["name"] in WALLS:
                    out.append((o, f"{o['id']}/{f['name']}", max(y for _, y in outline(f)), raster(outline(f))))
        elif o["id"].startswith(("tree-near", "bush-", "fence-")):
            out.append((o, o["id"], ground_y(o), silhouette(o["id"])))
    return out


def check_depth():
    """Every tree and bush stands on the ground at one depth (its ground line), and where it
    overlaps a fence or a house wall on screen the nearer of the two (the larger ground line) is
    drawn in front. Returns a list of failures (empty when every pair agrees)."""
    units = depth_units()
    order = {o["id"]: i for i, o in enumerate(objects())}
    fails = []
    for i, (a, la, ga, ma) in enumerate(units):
        if not a["id"].startswith(("tree-", "bush-")):
            continue
        for j, (b, lb, gb, mb) in enumerate(units):
            if b is a or (b["id"].startswith(("tree-", "bush-")) and j < i):
                continue
            n = int((ma & mb).sum())
            if n < DEPTH_OVERLAP or abs(ga - gb) <= DEPTH_TOL or (la, lb) in DEPTH_EXEMPT:
                continue
            (near, ln, gn), (far, lf, gf) = sorted([(a, la, ga), (b, lb, gb)], key=lambda u: -u[2])
            if not in_front(near, far, order):
                fails.append(f"{lf} (ground y {gf:g}) is drawn in front of {ln} (ground y {gn:g}), "
                             f"which stands nearer ({n} px overlap)")
    return fails
