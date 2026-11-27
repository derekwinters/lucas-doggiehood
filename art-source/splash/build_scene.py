#!/usr/bin/env python3
"""Rebuild the splash scene (background, environment and dogs) as layered vector art.

Deterministic: every output is written from scratch from the parameters in this file;
running it twice produces byte-identical files (self-checked).

    pip install fonttools cairosvg pillow numpy skia-pathops vtracer opencv-python-headless scipy
    python3 art-source/splash/build_scene.py              # build + self-check
    python3 art-source/splash/build_scene.py --no-review  # skip the review images
    python3 art-source/splash/build_scene.py --env traced # the previous traced environment
    python3 art-source/splash/build_scene.py --env hand   # the first hand-built environment

All coordinates are in the pixel space of Assets/Art/Splash/cover-art.png (2816x1536),
so every layer shares one full-frame canvas and the layers stack back together. Layer
PNGs are rendered at 2x (5632x3072), transparent outside each layer's content.

Layers (back to front): sky, sun, clouds, street, houses-far, houses-left, houses-right,
trees, props (hand-built clean vector objects in build_env.py / env_shapes.py; with
--env traced the previous trace of the original in trace_env.py, or with --env hand the
first, simplified hand-built scene below), then the dogs (dog-shadows, dog-schnauzer, dog-puppy, dog-corgi,
dog-dalmatian, dog-golden; traced from the original in trace_dogs.py, or with --dogs hand
the earlier hand-drawn set in build_dogs.py). scene/composite.svg stacks them all
with the title (title/doggiehood-title.svg) on top. Each dog is also written as a
tight-cropped standalone SVG + PNG in dogs/.
"""
from __future__ import annotations

import argparse
import hashlib
import math
import re
import sys
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE))
import build_title as bt  # noqa: E402  (shared helpers: fmt, render, png_image, save_png, label_font)

fmt = bt.fmt
OUT = HERE / "scene"
DOGS_OUT = HERE / "dogs"
REVIEW = HERE / "review"
ORIGINAL = bt.ORIGINAL
TITLE_SVG = bt.OUT / "doggiehood-title.svg"

W, H = 2816, 1536          # canvas, original-image px
SCALE = 2                  # layer PNG export scale -> 5632x3072
LAYER_ORDER = ["sky", "sun", "clouds", "street", "houses-far", "houses-left",
               "houses-right", "trees", "props"]

# --------------------------------------------------------------------------
# Palette (sampled from the original, then cleaned up)
# --------------------------------------------------------------------------
SKY_STOPS = [(0.0, "#55d0fd"), (0.16, "#66d6fb"), (0.30, "#94e3f6"), (0.40, "#c0f3f4"),
             (0.46, "#d2f8f3"), (1.0, "#d2f8f3")]
HAZE = "#c6f1f2"            # far objects fade toward this
SUN = dict(cx=1700, cy=75, r=108, core="#fdf7c8", rim="#fbeea0", glow="#fffbe0")
CLOUD = dict(side="#e3f4fc", under="#c4e7f8", edge="#b3dbef")
CREAM = "#f6d6a2"
GLASS = "#5aa7d8"
GLASS_HI = "#a9dcf5"
STEP_TOP, STEP_FRONT = "#ddd0cc", "#b3a6b2"
ROAD_NEAR, ROAD_FAR = "#55586c", "#8d93a5"
SIDEWALK, SIDEWALK_JOINT, CURB = "#d6c9cf", "#b9adb8", "#a49db2"
LAWN_NEAR, LAWN_FAR = "#72b52a", "#a8d65a"
PATH = "#d3c6cc"
OUTLINE_K = 0.70            # edge stroke = fill darkened by this factor
EDGE_W = 3.0

# --------------------------------------------------------------------------
# Street geometry (edges read off the original; polylines from far to near)
# --------------------------------------------------------------------------
HORIZON_Y = 700
ROAD_L = [(1327, 700), (449, 1536)]                          # road's left edge
ROAD_R = [(1560, 700), (2450, 1536)]                         # road's right edge
WALK_L = [(1262, 700), (1050, 800), (820, 900), (0, 1440), (-160, 1545)]   # left sidewalk / lawn
WALK_R = [(1625, 700), (1850, 800), (2120, 900), (2816, 1440), (2960, 1545)]  # right sidewalk / lawn


# --------------------------------------------------------------------------
# Colour + SVG primitives
# --------------------------------------------------------------------------
def rgb(h):
    return [int(h[i:i + 2], 16) for i in (1, 3, 5)]


def hexc(c):
    return "#%02x%02x%02x" % tuple(max(0, min(255, round(v))) for v in c)


def mix(a, b, t):
    """Blend colour a toward b by t (0 = a, 1 = b)."""
    ca, cb = rgb(a), rgb(b)
    return hexc([x + (y - x) * t for x, y in zip(ca, cb)])


def shade(h, k):
    return hexc([v * k for v in rgb(h)])


def P(pts, close=True):
    d = "M " + " L ".join(f"{fmt(x)} {fmt(y)}" for x, y in pts)
    return d + (" Z" if close else "")


def edge_attrs(fill, edge=None, sw=EDGE_W):
    if edge is False:
        return f'fill="{fill}"'
    e = edge or shade(fill, OUTLINE_K)
    return f'fill="{fill}" stroke="{e}" stroke-width="{fmt(sw)}" stroke-linejoin="round"'


def poly(pts, fill, edge=None, sw=EDGE_W, extra=""):
    return f'<path d="{P(pts)}" {edge_attrs(fill, edge, sw)}{extra}/>'


def rect(x0, y0, x1, y1, fill, edge=None, sw=EDGE_W, r=0.0, extra=""):
    rr = f' rx="{fmt(r)}"' if r else ""
    return (f'<rect x="{fmt(x0)}" y="{fmt(y0)}" width="{fmt(x1 - x0)}" height="{fmt(y1 - y0)}"{rr} '
            f'{edge_attrs(fill, edge, sw)}{extra}/>')


def ellipse(cx, cy, rx, ry, fill, edge=None, sw=EDGE_W, rot=0.0, extra=""):
    t = f' transform="rotate({fmt(rot)} {fmt(cx)} {fmt(cy)})"' if rot else ""
    return (f'<ellipse cx="{fmt(cx)}" cy="{fmt(cy)}" rx="{fmt(rx)}" ry="{fmt(ry)}"{t} '
            f'{edge_attrs(fill, edge, sw)}{extra}/>')


def group(gid, body, label=None, extra=""):
    return f'<g id="{gid}" inkscape:label="{label or gid}"{extra}>{body}</g>\n'


def line_x(pl, y):
    """x of a far-to-near polyline at height y (extrapolates past its ends)."""
    for (x0, y0), (x1, y1) in zip(pl, pl[1:]):
        if y <= y1 or (x1, y1) == pl[-1]:
            return x0 + (x1 - x0) * (y - y0) / (y1 - y0)
    raise AssertionError("unreachable")


def edge_pts(pl, y0, y1):
    """The polyline's points between heights y0 and y1 (inclusive, interpolated ends)."""
    pts = [(line_x(pl, y0), y0)] + [(x, y) for x, y in pl if y0 < y < y1] + [(line_x(pl, y1), y1)]
    return pts


def svg_doc(body, defs="", title="Doggiehood splash scene"):
    return ('<?xml version="1.0" encoding="UTF-8"?>\n'
            '<svg xmlns="http://www.w3.org/2000/svg" '
            'xmlns:inkscape="http://www.inkscape.org/namespaces/inkscape" '
            f'width="{W}" height="{H}" viewBox="0 0 {W} {H}">\n'
            f'<title>{title}</title>\n'
            '<desc>Doggiehood splash, rebuilt as vector layers. '
            'Generated by art-source/splash/build_scene.py.</desc>\n'
            f'<defs>\n{defs}</defs>\n{body}</svg>\n')


def layer_svg(name, body, defs=""):
    return svg_doc(f'<g id="{name}" inkscape:label="{name}" inkscape:groupmode="layer">\n'
                   f'{body}</g>\n', defs, title=f"Doggiehood splash - {name}")


def lin_grad(gid, stops, x1=0, y1=0, x2=0, y2=1, units=None):
    u = f' gradientUnits="{units}"' if units else ""
    s = "".join(f'<stop offset="{fmt(o)}" stop-color="{c}"/>' for o, c in stops)
    return (f'<linearGradient id="{gid}" x1="{fmt(x1)}" y1="{fmt(y1)}" x2="{fmt(x2)}" '
            f'y2="{fmt(y2)}"{u}>{s}</linearGradient>\n')


# --------------------------------------------------------------------------
# Sky, sun, clouds
# --------------------------------------------------------------------------
def build_sky():
    defs = lin_grad("sky-grad", SKY_STOPS, 0, 0, 0, H, "userSpaceOnUse")
    return layer_svg("sky", f'<rect x="0" y="0" width="{W}" height="{H}" fill="url(#sky-grad)"/>', defs)


def build_sun():
    s = SUN
    defs = (f'<radialGradient id="sun-glow-grad" cx="{s["cx"]}" cy="{s["cy"]}" r="{fmt(s["r"] * 4.2)}" '
            f'gradientUnits="userSpaceOnUse">'
            f'<stop offset="0" stop-color="{s["glow"]}" stop-opacity="0.95"/>'
            f'<stop offset="0.3" stop-color="{s["glow"]}" stop-opacity="0.55"/>'
            f'<stop offset="0.65" stop-color="{s["glow"]}" stop-opacity="0.15"/>'
            f'<stop offset="1" stop-color="{s["glow"]}" stop-opacity="0"/></radialGradient>\n'
            f'<radialGradient id="sun-disc-grad" cx="{s["cx"] - 20}" cy="{s["cy"] - 20}" r="{s["r"]}" '
            f'gradientUnits="userSpaceOnUse"><stop offset="0" stop-color="#fffbe6"/>'
            f'<stop offset="1" stop-color="{s["core"]}"/></radialGradient>\n')
    body = (group("sun-glow", f'<circle cx="{s["cx"]}" cy="{s["cy"]}" r="{fmt(s["r"] * 4.2)}" '
                              f'fill="url(#sun-glow-grad)"/>')
            + group("sun-disc", f'<circle cx="{s["cx"]}" cy="{s["cy"]}" r="{s["r"]}" fill="url(#sun-disc-grad)" '
                                f'stroke="{s["rim"]}" stroke-width="4"/>'))
    return layer_svg("sun", body, defs)


# Faceted slab clouds, as in the original: a bright stepped face lit from the upper
# right, over a large translucent pale-blue underside. Polygons are (lit, underside).
CLOUDS = {
    "cloud-top-left": (
        [(298, -4), (484, -4), (490, 62), (590, 64), (600, 96), (650, 118), (652, 142), (722, 150),
         (722, 172), (652, 168), (592, 126), (490, 122), (440, 102), (352, 96), (298, 42)],
        [(98, -4), (298, -4), (298, 42), (352, 96), (440, 102), (490, 122), (592, 126), (652, 168),
         (722, 172), (722, 178), (520, 180), (290, 164), (98, 48)]),
    "cloud-top-mid": (
        [(2034, 128), (2034, 96), (2076, 92), (2076, 70), (2150, 40), (2150, -4), (2300, -4), (2300, 44),
         (2240, 50), (2150, 92), (2150, 132)],
        [(2034, 128), (2150, 132), (2236, 94), (2300, 50), (2300, 44), (2240, 50), (2150, 92)]),
    "cloud-top-right": (
        [(2340, 196), (2340, 160), (2386, 156), (2386, 124), (2436, 96), (2436, 84), (2516, 80), (2516, 56),
         (2600, 42), (2600, -4), (2820, -4), (2820, 78), (2622, 76), (2622, 122), (2520, 126), (2440, 160),
         (2440, 168), (2386, 196)],
        [(2340, 196), (2386, 196), (2440, 168), (2440, 160), (2520, 126), (2622, 122), (2622, 76),
         (2820, 78), (2820, 120), (2700, 150), (2520, 212)]),
    "cloud-behind-title-left": (
        [(905, 262), (905, 205), (960, 196), (1050, 196), (1050, 240), (960, 262)],
        [(905, 262), (960, 262), (1050, 240), (1050, 262)]),
    "cloud-behind-title-right": (
        [(1870, 280), (1870, 222), (1930, 214), (1996, 214), (1996, 256), (1930, 280)],
        [(1870, 280), (1930, 280), (1996, 256), (1996, 280)]),
}


def build_clouds():
    defs = lin_grad("cloud-lit", [(0, "#ffffff"), (1, CLOUD["side"])], 1, 0, 0, 1)
    body = ""
    for cid, (lit, under) in CLOUDS.items():
        body += group(cid, poly(under, CLOUD["under"], False, extra=' opacity="0.8"')
                      + poly(lit, "url(#cloud-lit)", CLOUD["edge"], 1.5, extra=' opacity="0.97"'))
    return layer_svg("clouds", body, defs)


# --------------------------------------------------------------------------
# Street: lawns, sidewalks, curbs, road, front paths
# --------------------------------------------------------------------------
def build_street():
    defs = (lin_grad("road-grad", [(0, ROAD_FAR), (0.25, mix(ROAD_FAR, ROAD_NEAR, 0.55)), (1, ROAD_NEAR)],
                     0, HORIZON_Y, 0, H, "userSpaceOnUse")
            + lin_grad("lawn-grad", [(0, LAWN_FAR), (0.35, "#8cc63c"), (1, LAWN_NEAR)],
                       0, HORIZON_Y, 0, H, "userSpaceOnUse")
            + lin_grad("walk-grad", [(0, mix(SIDEWALK, HAZE, 0.4)), (1, SIDEWALK)],
                       0, HORIZON_Y, 0, H, "userSpaceOnUse"))
    y0, yb = HORIZON_Y, H + 2
    lawn_l = [(-2, y0 - 40), (line_x(WALK_L, y0), y0 - 2)] + edge_pts(WALK_L, y0, yb)[1:] + [(-2, yb)]
    lawn_r = ([(line_x(WALK_R, y0), y0 - 2), (W + 2, y0 - 40), (W + 2, yb)]
              + list(reversed(edge_pts(WALK_R, y0, yb)))[:-1])
    far_lawn = [(880, y0 - 18), (1980, y0 - 18), (1980, y0 + 30), (880, y0 + 30)]
    walk_l = edge_pts(WALK_L, y0, yb) + list(reversed(edge_pts(ROAD_L, y0, yb)))
    walk_r = edge_pts(ROAD_R, y0, yb) + list(reversed(edge_pts(WALK_R, y0, yb)))
    road = edge_pts(ROAD_L, y0, yb) + list(reversed(edge_pts(ROAD_R, y0, yb)))

    def curb(pl, side):
        # a strip on the road side of the curb line, widening toward the viewer
        return poly([(line_x(pl, y0), y0), (line_x(pl, yb), yb), (line_x(pl, yb) + side * 26, yb),
                     (line_x(pl, y0) + side * 2, y0)], CURB, False)

    # sidewalk joints: across each sidewalk, spaced in perspective toward the horizon
    joints = []
    for k in range(1, 16):
        y = y0 + (H - y0) * (1 - 0.82 ** k) / (1 - 0.82 ** 15)
        w = 1 + 3 * (y - y0) / (H - y0)
        for a, b in ((WALK_L, ROAD_L), (ROAD_R, WALK_R)):
            joints.append(f'<path d="M {fmt(line_x(a, y))} {fmt(y)} L {fmt(line_x(b, y))} {fmt(y)}" '
                          f'stroke="{SIDEWALK_JOINT}" stroke-width="{fmt(w)}" fill="none"/>')
    paths = (poly([(300, 985), (line_x(WALK_L, 985) + 10, 985), (line_x(WALK_L, 1032) + 10, 1032),
                   (290, 1032)], PATH, shade(PATH, 0.85), 2)
             + poly([(line_x(WALK_R, 1000) - 10, 1000), (2650, 1000), (2660, 1046),
                     (line_x(WALK_R, 1046) - 10, 1046)], PATH, shade(PATH, 0.85), 2))
    body = (group("lawns", poly(lawn_l, "url(#lawn-grad)", False) + poly(lawn_r, "url(#lawn-grad)", False)
                  + poly(far_lawn, LAWN_FAR, False))
            + group("front-paths", paths)
            + group("sidewalks", poly(walk_l, "url(#walk-grad)", False) + poly(walk_r, "url(#walk-grad)", False)
                    + "".join(joints))
            + group("road", poly(road, "url(#road-grad)", False))
            + group("curbs", curb(ROAD_L, +1) + curb(ROAD_R, -1)))
    return layer_svg("street", body, defs)


# --------------------------------------------------------------------------
# Houses
# --------------------------------------------------------------------------
def window(x0, y0, x1, y1, frame=CREAM, sill=True, haze=0.0):
    f = mix(frame, HAZE, haze)
    g = mix(GLASS, HAZE, haze)
    m = max(3.0, (x1 - x0) * 0.14)
    out = rect(x0, y0, x1, y1, f, sw=2.5, r=2)
    out += rect(x0 + m, y0 + m, x1 - m, y1 - m, g, shade(g, 0.75), 2)
    out += poly([(x0 + m, y0 + m), (x1 - m, y0 + m), (x0 + m, y0 + m + (y1 - y0) * 0.45)],
                mix(GLASS_HI, HAZE, haze), False)
    mid = (y0 + y1) / 2
    out += rect(x0 + m, mid - m * 0.35, x1 - m, mid + m * 0.35, f, False)
    if sill:
        out += rect(x0 - m * 0.8, y1 - 1, x1 + m * 0.8, y1 + m * 1.2, shade(f, 0.88), sw=2)
    return out


def door(x0, y0, x1, y1, colour, frame=CREAM, haze=0.0):
    f = mix(frame, HAZE, haze)
    c = mix(colour, HAZE, haze)
    m = max(3.0, (x1 - x0) * 0.13)
    out = rect(x0, y0, x1, y1, f, sw=2.5)
    out += rect(x0 + m, y0 + m, x1 - m, y1, c, sw=2.5)
    out += rect(x0 + m * 2, y0 + m * 2, x1 - m * 2, y0 + (y1 - y0) * 0.45, shade(c, 0.9), False)
    out += ellipse(x1 - m * 2.2, y0 + (y1 - y0) * 0.55, m * 0.75, m * 0.75, mix("#f2a23a", HAZE, haze), sw=1.5)
    return out


def steps(x0, y0, x1, n, step_h, grow, haze=0.0):
    """n steps descending toward the viewer from (x0..x1, y0), each `grow` px wider."""
    out = ""
    for k in range(n):
        a, b = x0 - grow * k, x1 + grow * k
        y = y0 + k * step_h
        out += rect(a, y, b, y + step_h * 0.35, mix(STEP_TOP, HAZE, haze), sw=2)
        out += rect(a, y + step_h * 0.35, b, y + step_h, mix(STEP_FRONT, HAZE, haze), sw=2)
    return out


def chimney(x0, y0, x1, y1, haze=0.0):
    b = mix("#b4513b", HAZE, haze)
    cap = mix("#9e9bad", HAZE, haze)
    return (rect(x0, y0, x1, y1, b) + rect((x0 + x1) / 2, y0, x1, y1, shade(b, 0.85), False)
            + rect(x0 - (x1 - x0) * 0.08, y0 - (x1 - x0) * 0.08, x1 + (x1 - x0) * 0.08,
                   y0 + (x1 - x0) * 0.25, cap))


def house(h):
    """A gable-fronted house facing the road.

    ax, ay: gable apex. fn/ff: facade x on the road side / far side. eave: y of the
    eave (trim band top). base: y where the walls meet the lawn. side_x: far end of the
    visible side wall. roof_x, ridge_y: far end of the roof ridge. Details are absolute."""
    hz = h.get("haze", 0.0)
    C = lambda c: mix(c, HAZE, hz)  # noqa: E731
    ax, ay, fn, ff, eave, base = h["ax"], h["ay"], h["fn"], h["ff"], h["eave"], h["base"]
    s = 1 if ff > fn else -1          # direction away from the road
    wall, roof = C(h["wall"]), C(h["roof"])
    side = C(h.get("side", shade(h["wall"], 0.8)))
    barge = C(h.get("barge", shade(h["roof"], 1.25)))
    bw = h.get("barge_w", 14)
    ov = h.get("overhang", bw * 0.9)
    trim_h = h.get("trim_h", (base - eave) * 0.11)
    side_x = h.get("side_x", ff)
    roof_x = h.get("roof_x", side_x + s * ov)
    ridge_y = h.get("ridge_y", ay)
    near_drop = h.get("eave_drop", 6)
    out = []
    for c in h.get("chimneys", []):
        out.append(chimney(*c, haze=hz))
    if side_x != ff:
        out.append(poly([(ff, eave), (side_x, eave), (side_x, base), (ff, base)], side))
        out.append(rect(min(ff, side_x), eave, max(ff, side_x), eave + trim_h * 0.8, C(shade(CREAM, 0.85)), sw=2))
    # roof plane (from the apex back to the far end)
    out.append(poly([(ax, ay), (roof_x, ridge_y), (roof_x, eave + near_drop),
                     (ff + s * ov * 0.2, eave + near_drop)], roof))
    for k in range(h.get("shingles", 2)):   # a few shingle dashes for texture
        t = (k + 1) / (h.get("shingles", 2) + 1)
        yy = ay + (eave - ay) * t
        xa = ax + (ff - ax) * t + s * bw * 2
        out.append(f'<path d="M {fmt(xa)} {fmt(yy)} l {fmt(s * bw * 3)} 0" stroke="{shade(roof, 0.8)}" '
                   f'stroke-width="{fmt(bw * 0.5)}" stroke-linecap="round"/>')
    # gable wall
    out.append(poly([(fn, eave), (ax, ay + bw * 0.5), (ff, eave), (ff, base), (fn, base)], wall))
    out.append(rect(min(fn, ff) - 4, eave, max(fn, ff) + 4, eave + trim_h, C(CREAM)))
    # barge boards along both gable edges
    t = eave + near_drop - ay
    for ex in (fn - s * ov, ff + s * ov * 0.3):
        out.append(f'<path d="M {fmt(ax)} {fmt(ay)} L {fmt(ex)} {fmt(ay + t)}" stroke="{barge}" '
                   f'stroke-width="{fmt(bw)}" stroke-linecap="round" fill="none"/>')
    for wdw in h.get("windows", []):
        out.append(window(*wdw, haze=hz))
    for d in h.get("doors", []):
        out.append(door(*d, haze=hz))
    for st in h.get("steps", []):
        out.append(steps(*st, haze=hz))
    return group(h["id"], "".join(out))


def auto_house(hid, ax, ay, fn, ff, eave, base, side_x, wall, roof, haze, door_c="#c25e36", **kw):
    """A far house: details placed proportionally on the gable facade."""
    lo, hi = min(fn, ff), max(fn, ff)
    fw = hi - lo
    hh = base - eave
    dw = fw * 0.22
    dx = lo + fw * 0.5 - dw / 2
    wins = [(lo + fw * 0.08, eave + hh * 0.3, lo + fw * 0.08 + fw * 0.16, eave + hh * 0.7),
            (hi - fw * 0.08 - fw * 0.16, eave + hh * 0.3, hi - fw * 0.08, eave + hh * 0.7)]
    gw = (ax - fw * 0.07, ay + (eave - ay) * 0.42, ax + fw * 0.07, ay + (eave - ay) * 0.85)
    return dict(id=hid, ax=ax, ay=ay, fn=fn, ff=ff, eave=eave, base=base, side_x=side_x, wall=wall,
                roof=roof, haze=haze, barge_w=max(3.0, fw * 0.07), shingles=0,
                windows=wins + [gw], doors=[(dx, eave + hh * 0.3, dx + dw, base, door_c)], **kw)


HOUSES_LEFT = [
    dict(id="house-red", ax=300, ay=200, fn=435, ff=100, eave=535, base=930, side_x=-20, roof_x=-20,
         ridge_y=195, wall="#ea4f48", side="#c8433f", roof="#454972", barge="#5a5f8c", barge_w=40,
         overhang=36, trim_h=45, shingles=3,
         windows=[(258, 355, 322, 500), (150, 645, 195, 820), (355, 640, 410, 795)],
         doors=[(215, 640, 335, 940, "#c25e36")],
         steps=[(225, 925, 385, 3, 26, 14)]),
    dict(id="house-yellow", ax=752, ay=482, fn=832, ff=695, eave=600, base=850, side_x=430, roof_x=430,
         ridge_y=436, wall="#f9c94e", side="#d89a3a", roof="#8a6d6d", barge="#9a7468", barge_w=16,
         overhang=14, trim_h=24,
         windows=[(435, 650, 525, 775), (792, 650, 818, 742), (735, 525, 760, 585)],
         doors=[(738, 645, 786, 842, "#d2643f")],
         steps=[(705, 815, 850, 3, 22, 10)]),
    dict(id="house-teal", ax=930, ay=487, fn=972, ff=880, eave=612, base=790, side_x=818, roof_x=812,
         ridge_y=487, wall="#4aa9a9", side="#3f8f97", roof="#5c7da0", barge="#7896ba", barge_w=12,
         overhang=12, trim_h=18,
         windows=[(884, 650, 902, 730), (940, 650, 960, 730), (920, 530, 938, 585)],
         doors=[(905, 650, 935, 782, "#c25e36")],
         steps=[(903, 780, 975, 2, 18, 8)]),
]

HOUSES_RIGHT = [
    dict(id="house-green", ax=2630, ay=200, fn=2505, ff=2960, eave=560, base=975, side_x=2960, roof_x=2960,
         ridge_y=200, wall="#86b93b", roof="#9d3b36", barge="#d9583f", barge_w=46, overhang=40, trim_h=32,
         shingles=3,
         windows=[(2615, 360, 2680, 505), (2525, 645, 2580, 800), (2740, 650, 2830, 840)],
         doors=[(2600, 640, 2715, 945, "#c4473c")],
         steps=[(2540, 930, 2700, 3, 30, 14)]),
    dict(id="house-blue", ax=2255, ay=385, fn=2190, ff=2345, eave=590, base=840, side_x=2505, roof_x=2540,
         ridge_y=385, wall="#4bb6c0", side="#3d8fa8", roof="#3f789d", barge="#5c9cc6", barge_w=16,
         overhang=14, trim_h=24, chimneys=[(2440, 305, 2535, 470)],
         windows=[(2198, 650, 2222, 752), (2292, 650, 2330, 765), (2455, 650, 2505, 765),
                  (2240, 470, 2266, 540)],
         doors=[(2230, 645, 2280, 836, "#d2643f")],
         steps=[(2175, 822, 2270, 3, 20, 8)]),
    dict(id="house-pink", ax=2047, ay=495, fn=2005, ff=2100, eave=615, base=790, side_x=2192, roof_x=2196,
         ridge_y=495, wall="#f2a3b4", side="#d77c98", roof="#ac6a7c", barge="#c98aa0", barge_w=11,
         overhang=10, trim_h=16,
         windows=[(2010, 650, 2026, 726), (2068, 650, 2084, 730), (2036, 540, 2051, 590)],
         doors=[(2030, 650, 2064, 780, "#c25e36")]),
]

HOUSES_FAR = [
    auto_house("house-far-yellow", 1890, 525, 1860, 1952, 620, 752, 2002, "#f6d06a", "#a08070", 0.22,
               chimneys=[(1962, 490, 2002, 548)]),
    auto_house("house-far-pink", 1743, 565, 1724, 1762, 612, 702, 1802, "#f0a0b0", "#c070a0", 0.4),
    auto_house("house-far-blue", 1680, 578, 1656, 1705, 625, 702, 1730, "#f4c890", "#6080b0", 0.45),
    auto_house("house-far-salmon", 1140, 548, 1192, 1102, 615, 730, 975, "#e8906a", "#b06672", 0.28,
               chimneys=[(978, 515, 1016, 565)]),
    auto_house("house-far-rose", 1262, 586, 1286, 1240, 625, 702, 1222, "#f2a090", "#c090b0", 0.45),
]


def build_houses(houses, name):
    return layer_svg(name, "".join(house(h) for h in houses))


# --------------------------------------------------------------------------
# Trees (low-poly), with trees that stand behind a near house masked by it
# --------------------------------------------------------------------------
JITTER = [1.0, 0.94, 0.99, 0.95, 1.03, 0.93, 0.99, 0.92, 1.02, 0.96, 0.97, 0.94, 1.01]
GREENS = ("#5a9a2a", "#86c43a", "#3f7f24")


def lowpoly_tree(tid, cx, cy, rx, ry, trunk=None, haze=0.0, n=13, mask=None, phase=0):
    C = lambda c: mix(c, HAZE, haze)  # noqa: E731
    pts = []
    for k in range(n):
        a = -math.pi / 2 + 2 * math.pi * k / n
        j = JITTER[(k + phase) % len(JITTER)]
        pts.append((cx + rx * j * math.cos(a), cy + ry * j * math.sin(a)))
    out = []
    if trunk:
        x0, y0, x1, y1 = trunk
        out.append(rect(x0, y0, x1, y1, C("#7a4b3a")))
        out.append(rect((x0 + x1) / 2, y0, x1, y1, C("#5f3a2e"), False))
    out.append(poly(pts, C(GREENS[0]), C("#336b1e"), 3))
    c2 = (cx - rx * 0.15, cy - ry * 0.12)
    for k in range(n):
        p, q = pts[k], pts[(k + 1) % n]
        ang = math.atan2((p[1] + q[1]) / 2 - cy, (p[0] + q[0]) / 2 - cx)
        lit = -math.cos(ang + math.pi / 4)        # light from the upper left
        col = GREENS[1] if lit > 0.3 else (GREENS[2] if lit < -0.4 else None)
        if col:
            out.append(poly([c2, p, q], C(col), False, extra=' opacity="0.7"'))
    extra = f' clip-path="url(#{mask})"' if mask else ""
    return group(tid, "".join(out), extra=extra)


def poplar(tid, cx, cy, rx, ry, haze):
    C = lambda c: mix(c, HAZE, haze)  # noqa: E731
    return group(tid, rect(cx - 1.5, cy + ry * 0.6, cx + 1.5, cy + ry * 1.5, C("#7a4b3a"), False)
                 + ellipse(cx, cy, rx, ry, C(GREENS[0]), C("#336b1e"), 1.5))


TREES_FAR = [
    ("tree-behind-red-house", 438, 440, 58, 105, None, 0.3, ("house-red", "house-yellow")),
    ("tree-far-left-1", 1068, 640, 74, 72, (1055, 690, 1082, 762), 0.2, None),
    ("tree-far-left-2", 1180, 600, 50, 55, (1172, 640, 1188, 700), 0.35, None),
    ("tree-far-left-3", 1228, 645, 30, 45, (1222, 675, 1234, 705), 0.4, None),
    ("tree-far-left-4", 1352, 622, 32, 36, (1347, 645, 1357, 692), 0.45, None),
    ("tree-far-right-1", 1612, 630, 30, 35, (1607, 655, 1617, 692), 0.45, None),
    ("tree-far-right-2", 1730, 615, 40, 45, (1724, 645, 1736, 745), 0.35, None),
    ("tree-far-right-3", 1810, 630, 50, 50, (1803, 665, 1817, 752), 0.3, None),
    ("tree-far-right-4", 1866, 610, 84, 120, None, 0.3, ("house-far-yellow", "house-pink", "house-far-pink")),
    ("tree-far-right-5", 1945, 650, 48, 86, (1940, 725, 1956, 792), 0.2, None),
]
POPLARS = [("poplar-1", 1290, 665, 8, 20, 0.45), ("poplar-2", 1375, 665, 8, 20, 0.45),
           ("poplar-3", 1556, 665, 9, 22, 0.45), ("poplar-4", 1580, 668, 9, 22, 0.45)]
TREES_NEAR = [
    ("tree-near-left", 82, 740, 112, 228, (42, 900, 92, 1125), 0.0),
    ("tree-near-yellow-house", 657, 692, 60, 122, (648, 800, 690, 905), 0.0),
    ("bush-tree-blue-house", 2170, 688, 42, 72, None, 0.05),
]
HORIZON_BLOBS = [(1235, 650, 40, 30), (1290, 625, 50, 40), (1440, 610, 60, 45), (1500, 640, 50, 35),
                 (1590, 615, 55, 40), (1650, 640, 45, 32), (1380, 650, 40, 30)]


def house_silhouette(h):
    """Bounding outline of a house (used to mask trees that stand behind it)."""
    s = 1 if h["ff"] > h["fn"] else -1
    bw = h.get("barge_w", 14)
    xs = [h["fn"] - s * bw * 2, h.get("roof_x", h.get("side_x", h["ff"]))]
    top = h.get("ridge_y", h["ay"]) - bw
    apex = (h["ax"], h["ay"] - bw)
    near_eave = (h["fn"] - s * bw * 2, h["eave"] + bw)
    pts = [apex, near_eave, (near_eave[0], h["base"]), (xs[1], h["base"]), (xs[1], top)]
    return pts


def outside_of(polys):
    """SVG path data for the whole canvas minus the union of `polys` (a hole-cut clip region)."""
    import pathops
    canvas = pathops.Path()
    pen = canvas.getPen()
    for x, y in [(0, 0), (W, 0), (W, H), (0, H)]:
        (pen.lineTo if x or y else pen.moveTo)((x, y))
    pen.closePath()
    holes = pathops.Path()
    for pts in polys:
        one = pathops.Path()
        hp = one.getPen()
        hp.moveTo(pts[0])
        for q in pts[1:]:
            hp.lineTo(q)
        hp.closePath()
        holes = pathops.op(holes, one, pathops.PathOp.UNION)
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


def build_trees():
    all_h = {h["id"]: h for h in HOUSES_LEFT + HOUSES_RIGHT + HOUSES_FAR}
    defs, far = "", ""
    for i, (tid, cx, cy, rx, ry, trunk, hz, behind) in enumerate(TREES_FAR):
        mask = None
        if behind:
            mask = f"mask-{tid}"
            # The canopy continues down behind every house it overlaps, so only its top shows
            # above the rooflines (a tree behind the house, never a crown floating on the roof).
            # A clipPath (canvas minus the house silhouettes) rather than a <mask>: CairoSVG
            # masks by alpha, not luminance, so a black-on-white mask would clip nothing there.
            defs += (f'<clipPath id="{mask}" clipPathUnits="userSpaceOnUse">'
                     f'<path d="{outside_of([house_silhouette(all_h[b]) for b in behind])}"/></clipPath>\n')
        far += lowpoly_tree(tid, cx, cy, rx, ry, trunk, hz, mask=mask, phase=i)
    far += "".join(poplar(*p) for p in POPLARS)
    horizon = "".join(ellipse(x, y, rx, ry, "#8fcfe0", False) for x, y, rx, ry in HORIZON_BLOBS)
    horizon += rect(1200, 640, 1720, 700, "#8fcfe0", False)
    near = "".join(lowpoly_tree(tid, cx, cy, rx, ry, trunk, hz, phase=3 + i)
                   for i, (tid, cx, cy, rx, ry, trunk, hz) in enumerate(TREES_NEAR))
    body = group("horizon-treeline", horizon) + group("trees-far", far) + group("trees-near", near)
    return layer_svg("trees", body, defs)


# --------------------------------------------------------------------------
# Props
# --------------------------------------------------------------------------
def mailbox(pid, x0, y0, x1, y1, post, flag, open_front=False, haze=0.0):
    C = lambda c: mix(c, HAZE, haze)  # noqa: E731
    px0, px1, py1 = post
    fx0, fy0, fx1, fy1 = flag
    bw = x1 - x0
    hgt = y1 - y0
    out = rect(px0, y1 - 4, px1, py1, C("#8a4b2b"))
    out += rect((px0 + px1) / 2, y1 - 4, px1, py1, C("#6d3920"), False)
    out += (f'<path d="M {fmt(x0)} {fmt(y1)} L {fmt(x0)} {fmt(y0 + hgt * 0.35)} '
            f'Q {fmt(x0)} {fmt(y0)} {fmt(x0 + hgt * 0.35)} {fmt(y0)} L {fmt(x1 - hgt * 0.35)} {fmt(y0)} '
            f'Q {fmt(x1)} {fmt(y0)} {fmt(x1)} {fmt(y0 + hgt * 0.35)} L {fmt(x1)} {fmt(y1)} Z" '
            f'{edge_attrs(C("#4690d1"), C("#285c94"), 3)}/>')
    out += rect(x0 + bw * 0.04, y0 + hgt * 0.15, x0 + bw * 0.22, y1 - hgt * 0.06, C("#2f6eb0"), C("#285c94"), 2,
                r=bw * 0.06)
    out += rect(x0 + bw * 0.3, y0 + hgt * 0.1, x1 - bw * 0.05, y0 + hgt * 0.28, C("#7ab8ea"), False,
                r=hgt * 0.08, extra=' opacity="0.7"')
    if open_front:
        out += rect(x1 - bw * 0.25, y0 + hgt * 0.1, x1 + bw * 0.08, y1 - hgt * 0.1, C("#f2b43a"), C("#b9802a"), 2)
        out += rect(x1 - bw * 0.2, y0 + hgt * 0.25, x1 + bw * 0.03, y0 + hgt * 0.4, C("#fff3d6"), False)
    out += rect(fx0, fy0, fx1, fy1, C("#f1621a"), C("#b8440e"), 2)
    out += rect(fx0, fy0, fx1 + (fx1 - fx0) * 0.9, fy0 + (fy1 - fy0) * 0.25, C("#f1621a"), C("#b8440e"), 2)
    return group(pid, out)


def fence(fid, x0, x1, y_top, y_bot, n, haze=0.0, slope=0.0):
    """Picket fence: n rounded pickets between x0..x1; y_top/y_bot at x0, tops follow slope."""
    C = lambda c: mix(c, HAZE, haze)  # noqa: E731
    pw = (x1 - x0) / n
    out = ""
    for k in range(n):
        a = x0 + k * pw + pw * 0.08
        b = a + pw * 0.84
        t = y_top + slope * (a - x0)
        bt_ = y_bot + slope * (a - x0)
        out += (f'<path d="M {fmt(a)} {fmt(bt_)} L {fmt(a)} {fmt(t + pw * 0.4)} '
                f'Q {fmt((a + b) / 2)} {fmt(t - pw * 0.2)} {fmt(b)} {fmt(t + pw * 0.4)} L {fmt(b)} {fmt(bt_)} Z" '
                f'{edge_attrs(C("#b6744d" if k % 2 else "#a8673f"), C("#6f3f25"), 2)}/>')
    return group(fid, out)


def bush(bid, lobes, haze=0.0):
    """A bush made of overlapping low-poly lobes (cx, cy, rx, ry)."""
    body = "".join(lowpoly_tree(f"{bid}-lobe-{i + 1}", cx, cy, rx, ry, None, haze, n=11, phase=i * 2)
                   for i, (cx, cy, rx, ry) in enumerate(lobes))
    return group(bid, body)


def flower(fid, x, y, s, petal, centre="#ffd23a"):
    out = (f'<path d="M {fmt(x)} {fmt(y)} q {fmt(-s * 0.2)} {fmt(s * 1.2)} 0 {fmt(s * 2.2)}" '
           f'stroke="#3f8a2a" stroke-width="{fmt(s * 0.22)}" fill="none"/>')
    out += ellipse(x - s * 0.5, y + s * 1.5, s * 0.45, s * 0.2, "#4f9a2c", False, rot=-30)
    for k in range(5):
        a = 2 * math.pi * k / 5 - math.pi / 2
        out += ellipse(x + s * 0.55 * math.cos(a), y + s * 0.55 * math.sin(a), s * 0.42, s * 0.42, petal, sw=1.5)
    out += ellipse(x, y, s * 0.3, s * 0.3, centre, sw=1.5)
    return group(fid, out)


def bone(bid, cx, cy, length, thick, rot, colour, spots=None):
    k = thick
    out = rect(cx - length / 2, cy - k * 0.5, cx + length / 2, cy + k * 0.5, colour, sw=2.5, r=k * 0.3)
    for sx in (-1, 1):
        for sy in (-1, 1):
            out += ellipse(cx + sx * length / 2, cy + sy * k * 0.45, k * 0.55, k * 0.55, colour, sw=2.5)
    out += rect(cx - length / 2, cy - k * 0.48, cx + length / 2, cy + k * 0.48, colour, False)
    for (dx, dy) in (spots or []):
        out += ellipse(cx + dx, cy + dy, k * 0.12, k * 0.12, shade(colour, 0.7), False)
    shadow = ellipse(cx + 6, cy + k * 0.9, length * 0.62, k * 0.45, "#000000", False, extra=' opacity="0.12"')
    return group(bid, shadow + f'<g transform="rotate({fmt(rot)} {fmt(cx)} {fmt(cy)})">{out}</g>')


def bowl(bid, cx, cy, w, h, colour):
    out = ellipse(cx + 6, cy + h * 0.55, w * 0.6, h * 0.35, "#000000", False, extra=' opacity="0.12"')
    out += (f'<path d="M {fmt(cx - w / 2)} {fmt(cy)} L {fmt(cx - w * 0.38)} {fmt(cy + h * 0.5)} '
            f'L {fmt(cx + w * 0.38)} {fmt(cy + h * 0.5)} L {fmt(cx + w / 2)} {fmt(cy)} Z" '
            f'{edge_attrs(colour, shade(colour, 0.65), 2.5)}/>')
    out += ellipse(cx, cy, w / 2, h * 0.28, shade(colour, 1.12), shade(colour, 0.65), 2.5)
    out += ellipse(cx, cy, w * 0.36, h * 0.17, "#7a4a2a", False)
    return group(bid, out)


def build_props():
    mailboxes = (mailbox("mailbox-left", 392, 852, 598, 962, (455, 500, 1150), (468, 815, 508, 925), True)
                 + mailbox("mailbox-right", 2322, 852, 2505, 962, (2400, 2440, 1150), (2382, 815, 2424, 920))
                 + mailbox("mailbox-far-left-1", 965, 730, 1002, 760, (978, 988, 800), (985, 718, 992, 742),
                           haze=0.2)
                 + mailbox("mailbox-far-left-2", 1186, 694, 1230, 720, (1204, 1210, 760), (1210, 682, 1216, 704),
                           haze=0.3)
                 + mailbox("mailbox-far-left-3", 1300, 670, 1328, 688, (1312, 1316, 700), (1318, 662, 1322, 676),
                           haze=0.45)
                 + mailbox("mailbox-far-right-1", 1664, 684, 1700, 700, (1680, 1684, 740), (1688, 676, 1692, 690),
                           haze=0.4)
                 + mailbox("mailbox-far-right-2", 1720, 697, 1764, 718, (1740, 1746, 760), (1750, 684, 1756, 705),
                           haze=0.35)
                 + mailbox("mailbox-far-right-3", 1835, 720, 1910, 765, (1868, 1880, 840), (1858, 700, 1868, 735),
                           haze=0.2))
    fences = (fence("fence-yellow-house", 820, 880, 745, 832, 5)
              + fence("fence-blue-house-left", 2085, 2192, 730, 842, 7)
              + fence("fence-blue-house-right", 2362, 2505, 775, 852, 8)
              + fence("fence-green-house", 2700, 2836, 910, 1150, 4, slope=-0.25))
    bushes = (bush("bush-front-left", [(70, 1050, 90, 90), (210, 1000, 95, 70), (150, 1130, 150, 110),
                                       (40, 1180, 70, 70)])
              + bush("bush-blue-house", [(2305, 852, 42, 48)])
              + bush("bush-green-house", [(2760, 925, 55, 45)])
              + bush("bush-far-left", [(1100, 728, 40, 30)], 0.2)
              + bush("bush-far-right", [(1790, 720, 34, 26)], 0.3))
    flowers = (flower("flower-white", 158, 1252, 18, "#ffffff")
               + flower("flower-pink-left", 272, 1196, 18, "#f2508a")
               + flower("flower-red-right", 2155, 935, 15, "#f05a28")
               + flower("flower-pink-right", 2240, 910, 17, "#f27ab0"))
    toys = (bone("toy-bone-orange", 615, 1182, 70, 26, -18, "#f0752e", [(-14, -2), (6, 3), (20, -3)])
            + bone("toy-bone-blue", 662, 1212, 66, 26, -8, "#3c86d8")
            + bowl("toy-bowl", 2555, 1172, 82, 46, "#ef7a2c")
            + bone("toy-bone-red", 2675, 1158, 72, 26, 14, "#d9502e"))
    body = (group("bushes", bushes) + group("fences", fences) + group("mailboxes", mailboxes)
            + group("flowers", flowers) + group("toys", toys))
    return layer_svg("props", body)


# --------------------------------------------------------------------------
# Composite, rendering, review, checks
# --------------------------------------------------------------------------
def inner(svg):
    """The <defs> body and drawable body of one of our SVG documents."""
    d = re.search(r"<defs>\n?(.*?)</defs>\n?(.*)</svg>", svg, re.S)
    return d.group(1), d.group(2)


DOG_SOURCES = ("traced", "hand")
DOGS_SRC = "traced"         # --dogs: traced from the original (default) or the hand-drawn set


def dog_source(kind=None):
    """The module that draws the dogs: trace_dogs (traced) or build_dogs (hand-drawn).
    Both offer DOG_ORDER, STANDALONE_H, dog_names(), dog_layers(), bbox(), crop()."""
    if (kind or DOGS_SRC) == "hand":
        import build_dogs as mod
    else:
        import trace_dogs as mod
    return mod


def build_composite(layers):
    src = dog_source()
    defs, body = "", ""
    for name in LAYER_ORDER + src.DOG_ORDER:
        d, b = inner(layers[name])
        defs += d
        body += b
    td, tb = inner(TITLE_SVG.read_text(encoding="utf-8"))
    # keep the title's ids distinct from the scene's
    tb = tb.replace('id="', 'id="title-').replace('url(#', 'url(#title-')
    td = td.replace('id="', 'id="title-').replace('url(#', 'url(#title-')
    body += f'<g id="title" inkscape:label="title" inkscape:groupmode="layer">\n{tb}</g>\n'
    return svg_doc(body, defs + td, title="Doggiehood splash - composite")


ENV_SOURCES = ("built", "traced", "hand")
ENV_SRC = "built"           # --env: clean authored objects (default), traced from the original, or the first hand-built scene
OBJECTS_OUT = HERE / "objects"
ENV_RING_MIN = 50           # env-only: visible px of a ground object around a dog needed to compare with it
ENV_HOLE_DE = 15.0          # env-only: mean colour where a dog was vs the ground around it (Lab)
ENV_DARK = 0.01             # env-only: at most this share of very dark pixels where a dog / the title was


def build_all():
    if ENV_SRC == "built":
        import build_env as be
        return be.env_layers()
    if ENV_SRC == "traced":
        import trace_env as te
        return te.env_layers()
    return build_hand()


def build_hand():
    return {"sky": build_sky(), "sun": build_sun(), "clouds": build_clouds(), "street": build_street(),
            "houses-far": build_houses(HOUSES_FAR, "houses-far"),
            "houses-left": build_houses(HOUSES_LEFT, "houses-left"),
            "houses-right": build_houses(HOUSES_RIGHT, "houses-right"),
            "trees": build_trees(), "props": build_props()}


def checker(size, sq=32):
    w, h = size
    yy, xx = np.mgrid[0:h, 0:w]
    a = (((xx // sq) + (yy // sq)) % 2).astype(np.uint8)
    arr = np.where(a[..., None] == 1, np.array([224, 224, 230], np.uint8), np.array([250, 250, 252], np.uint8))
    return Image.fromarray(arr.astype(np.uint8), "RGB").convert("RGBA")


def comparison_sheet(names, images, captions, out, row_h=520):
    """One row per dog: the same box cut from each image, side by side, labelled."""
    src = dog_source()
    gap, head = 16, 44
    rows = []
    for name in names:
        x0, y0, x1, y1 = (round(v) for v in src.bbox(name))
        k = row_h / (y1 - y0)
        size = (round((x1 - x0) * k), row_h)
        rows.append((name, [im.crop((x0, y0, x1, y1)).resize(size, Image.LANCZOS) for im in images]))
    width = max(sum(c.width for c in r[1]) for r in rows) + (len(images) + 1) * gap
    sheet = Image.new("RGB", (width, len(rows) * (row_h + head + gap) + gap), "#ffffff")
    d = ImageDraw.Draw(sheet)
    f = bt.label_font(30)
    y = gap
    for name, cells in rows:
        d.text((gap, y), f"{name}: {captions}", font=f, fill="#222222")
        x = gap
        for c in cells:
            sheet.paste(c, (x, y + head))
            x += c.width + gap
        y += row_h + head + gap
    bt.save_png(sheet, REVIEW / out)


def dogs_review(names, composite_png):
    """Each dog: the original crop (left) beside the rebuild in the rebuilt scene (right)."""
    orig = Image.open(ORIGINAL).convert("RGB")
    comparison_sheet(names, [orig, composite_png.convert("RGB")], "original (left), rebuild (right)",
                     "dogs-vs-original.png")


def hand_vs_traced_review(names, scene_layers, composite_png):
    """Each dog: the original, the previous hand-drawn dog and the traced dog, each in the
    rebuilt scene, same box."""
    import build_dogs as bd
    global DOGS_SRC
    keep, DOGS_SRC = DOGS_SRC, "hand"
    try:
        hand = bt.png_image(bt.render(build_composite(dict(scene_layers, **bd.dog_layers())), W))
    finally:
        DOGS_SRC = keep
    orig = Image.open(ORIGINAL).convert("RGB")
    comparison_sheet(names, [orig, hand.convert("RGB"), composite_png.convert("RGB")],
                     "original, hand-drawn (previous), traced (new)", "dogs-hand-vs-traced.png", row_h=600)


def build_review(pngs, composite_png):
    REVIEW.mkdir(parents=True, exist_ok=True)
    orig = Image.open(ORIGINAL).convert("RGB")
    half = (W // 2, H // 2)
    sheet = Image.new("RGB", (half[0], half[1] * 2 + 20), "#ffffff")
    sheet.paste(orig.resize(half, Image.LANCZOS), (0, 0))
    sheet.paste(composite_png.convert("RGB").resize(half, Image.LANCZOS), (0, half[1] + 20))
    bt.save_png(sheet, REVIEW / "scene-vs-original.png")
    # layer sheet: each layer alone on a checkerboard, 3 per row, labelled
    tw, th, cols, head = 704, 384, 3, 44
    names = list(pngs)
    rows = math.ceil(len(names) / cols)
    sh = Image.new("RGB", (cols * tw + (cols + 1) * 12, rows * (th + head) + 12), "#ffffff")
    d = ImageDraw.Draw(sh)
    f = bt.label_font(30)
    for i, name in enumerate(names):
        x = 12 + (i % cols) * (tw + 12)
        y = 12 + (i // cols) * (th + head)
        cb = checker((tw, th), 16)
        cb.alpha_composite(pngs[name].resize((tw, th), Image.LANCZOS))
        sh.paste(cb.convert("RGB"), (x, y + head - 6))
        d.text((x, y), name, font=f, fill="#222222")
    bt.save_png(sh, REVIEW / "scene-layers-sheet.png")


def check_layer(name, img):
    """Full-frame canvas at 2x; every layer but the sky is transparent outside its content."""
    assert img.size == (W * SCALE, H * SCALE), f"{name}: size {img.size}"
    a = np.asarray(img)[..., 3]
    # the soft dog shadows are translucent by design; everything else has solid content
    assert a.max() == 255 or (name == "dog-shadows" and a.max() > 0), f"{name}: layer is empty"
    if name == "sky":
        assert a.min() == 255, "sky must cover the whole canvas"
        return 0.0
    clear = float((a == 0).mean())
    assert clear > 0.02, f"{name}: no transparent area outside its content"
    border = np.concatenate([a[0], a[-1], a[:, 0], a[:, -1]])
    assert (border == 0).any(), f"{name}: no transparent pixel on the canvas edge"
    return clear


def check_standalone(name, img):
    """Tight crop: at least STANDALONE_H tall, transparent all round its edge, not empty."""
    assert img.height >= 3000 and img.height >= dog_source().STANDALONE_H - 1, f"{name}: standalone only {img.size}"
    a = np.asarray(img)[..., 3]
    border = np.concatenate([a[0], a[-1], a[:, 0], a[:, -1]])
    assert border.max() == 0, f"{name}: standalone touches its edge"
    assert a.max() == 255, f"{name}: standalone is empty"
    return img.size


def digest(paths):
    return {p.relative_to(HERE).as_posix(): hashlib.sha256(p.read_bytes()).hexdigest() for p in sorted(paths)}


STREET_OK = ""              # the street layout check's summary (--env built)


def build(review=True):
    global STREET_OK
    if ENV_SRC == "built":
        import build_env as be
        fails = be.check_street()
        assert not fails, "street layout (README: Street layout invariants):\n  " + "\n  ".join(fails)
        STREET_OK = (f"; street layout: {len(be.street_lots())} houses with steps centred on their doors, "
                     "a straight path to the sidewalk and one mailbox each, beside and never on a path, "
                     "all by one rule at one height")
        persp = be.check_perspective()
        assert not persp, "house perspective (README: House perspective invariants):\n  " + "\n  ".join(
            f"{h}: {f}" for h, fs in persp.items() for f in fs)
        win, nwin = be.check_windows()
        assert not win, "window sills and flower boxes (README: Window sills and flower boxes):\n  " + "\n  ".join(win)
        depth = be.check_depth()
        assert not depth, "trees, bushes and depth (README):\n  " + "\n  ".join(depth)
        STREET_OK += (f"; house perspective: every edge of {len(be.street_lots())} houses level, plumb or on "
                      "its side's vanishing point; "
                      f"{nwin} windows checked, every sill and flower box attached to its own window, its depth "
                      "along its house's one oblique like its frames; "
                      "trees and bushes stand on the ground in draw order")
    OUT.mkdir(parents=True, exist_ok=True)
    layers = build_all()
    pngs, clear = {}, {}
    for name in LAYER_ORDER:
        (OUT / f"{name}.svg").write_text(layers[name], encoding="utf-8")
        img = bt.png_image(bt.render(layers[name], W * SCALE))
        clear[name] = check_layer(name, img)
        bt.save_png(img, OUT / f"{name}.png")
        pngs[name] = img
    scene_layers = dict(layers)
    env_png = bt.png_image(bt.render(env_only(layers), W))
    holes = check_env_only(env_png)
    objects = build_objects() if ENV_SRC == "traced" else (build_env_objects(layers) if ENV_SRC == "built" else {})
    src = dog_source()
    DOGS_OUT.mkdir(parents=True, exist_ok=True)
    dogs = src.dog_layers()
    names = src.dog_names()
    sizes, leaks = {}, {}
    if DOGS_SRC == "traced":
        leaks = check_traced(src)
    for name in src.DOG_ORDER:
        (OUT / f"{name}.svg").write_text(dogs[name], encoding="utf-8")
        img = bt.png_image(bt.render(dogs[name], W * SCALE))
        clear[name] = check_layer(name, img)
        bt.save_png(img, OUT / f"{name}.png")
        pngs[name] = img
        if name in names:
            svg, width, _ = src.crop(dogs[name], name)
            (DOGS_OUT / f"{name}.svg").write_text(svg, encoding="utf-8")
            simg = bt.png_image(bt.render(svg, width))
            sizes[name] = check_standalone(name, simg)
            bt.save_png(simg, DOGS_OUT / f"{name}.png")
    layers = dict(layers, **dogs)
    comp = build_composite(layers)
    ids = re.findall(r' id="([^"]+)"', comp)
    dupes = sorted({i for i in ids if ids.count(i) > 1})
    assert not dupes, f"composite has duplicate ids: {dupes}"
    (OUT / "composite.svg").write_text(comp, encoding="utf-8")
    comp_png = bt.png_image(bt.render(comp, W))
    assert comp_png.size == (W, H)
    assert np.asarray(comp_png)[..., 3].min() == 255, "composite has transparent holes"
    bt.save_png(comp_png, OUT / "composite.png")
    if review:
        build_review(pngs, comp_png)
        bt.save_png(env_png.convert("RGB"), REVIEW / "env-only.png")
        dogs_review(names, comp_png)
        if DOGS_SRC == "traced":
            hand_vs_traced_review(names, scene_layers, comp_png)
        if ENV_SRC == "traced":
            env_hand_vs_traced_review(comp_png)
        if ENV_SRC == "built":
            env_built_review(comp_png)
            objects_sheet()
    return clear, sizes, leaks, objects, holes


def env_only(layers):
    """The environment alone (no dogs, no title), all layers stacked."""
    defs, body = "", ""
    for name in LAYER_ORDER:
        d, b = inner(layers[name])
        defs += d
        body += b
    return svg_doc(body, defs, title="Doggiehood splash - environment only")


def check_env_only(img):
    """The environment alone is opaque everywhere, and where a dog or the title stood it
    shows believable ground, not a hole: no very dark patch, and under each dog a mean
    colour close to the ground just around it."""
    import trace_dogs as td
    import trace_env as te
    a = np.asarray(img)
    assert a[..., 3].min() == 255, "env-only has transparent pixels"
    lab = te.to_lab(a[..., :3])
    masks = dict(td.dog_masks())
    # each hidden pixel is compared with the same ground (road, sidewalk or lawn) visible
    # around the dog, not with whatever stands there (steps, a tree trunk), so the mix of
    # what is around a dog does not decide the result
    vis = te.visible_map()
    regs = te.regions()
    ground = np.full(vis.shape, -1)
    for i, oid in enumerate(te.ORDER):
        if te.is_ground(oid):
            ground[regs[oid][0]] = i
    out = {}
    for name, m in masks.items():
        inside = te.erode(m, 3)
        ring = te.dilate(m, 30) & ~te.dilate(m, 8)
        for other in masks.values():
            ring &= ~te.dilate(other, 8)
        assert (ground[inside] >= 0).all(), f"env-only: no ground under part of {name}"
        total, count = 0.0, 0
        for i in np.unique(ground[inside]):
            part, near = inside & (ground == i), ring & (vis == i)
            if near.sum() < ENV_RING_MIN:
                continue
            total += float(np.linalg.norm(lab[part].mean(0) - lab[near].mean(0))) * part.sum()
            count += int(part.sum())
        assert count > 0.5 * inside.sum(), f"env-only: too little ground visible around {name}"
        de = total / count
        dark = float((lab[inside][:, 0] < 20).mean())
        assert de < ENV_HOLE_DE, f"env-only: where {name} stood differs from the ground around it (dE {de:.1f})"
        assert dark < ENV_DARK, f"env-only: dark hole where {name} stood ({dark:.1%})"
        out[name] = de
    t = te.occluders() & ~np.any(list(masks.values()), axis=0)
    dark = float((lab[t][:, 0] < 20).mean())
    assert dark < ENV_DARK, f"env-only: dark hole where the title stood ({dark:.1%})"
    return out


def build_objects():
    """Tight standalone crops of the notable environment objects (objects/)."""
    import trace_env as te
    OBJECTS_OUT.mkdir(parents=True, exist_ok=True)
    sizes = {}
    items = [(o, *te.standalone(o)[:2]) for o in te.STANDALONE] + [("sun", *te.sun_standalone())]
    for name, svg, width in items:
        (OBJECTS_OUT / f"{name}.svg").write_text(svg, encoding="utf-8")
        img = bt.png_image(bt.render(svg, width))
        a = np.asarray(img)[..., 3]
        assert max(img.size) >= te.OBJECT_LONG, f"object {name}: only {img.size}"
        assert a.max() == 255, f"object {name}: empty"
        border = np.concatenate([a[0], a[-1], a[:, 0], a[:, -1]])
        assert name == "sun" or (border == 0).any(), f"object {name}: no transparent edge"
        bt.save_png(img, OBJECTS_OUT / f"{name}.png")
        sizes[name] = img.size
    return sizes


# Regions compared in review/env-hand-vs-traced.png: (label, box in original px)
ENV_REVIEW = [("left houses", (0, 150, 1000, 1000)), ("right houses", (1880, 150, 2816, 1000)),
              ("far street", (900, 430, 2000, 860)),
              ("mailboxes and toys, left", (360, 780, 760, 1260)),
              ("mailboxes and toys, right", (2280, 780, 2816, 1260)),
              ("trees and bushes", (0, 440, 800, 1300))]


def env_hand_vs_traced_review(composite_png):
    """Per region: the original, the hand-built scene and the traced scene (each with the
    traced dogs and the title), same box."""
    global ENV_SRC
    keep, ENV_SRC = ENV_SRC, "hand"
    try:
        hand_layers = dict(build_all(), **dog_source().dog_layers())
        hand = bt.png_image(bt.render(build_composite(hand_layers), W)).convert("RGB")
    finally:
        ENV_SRC = keep
    orig = Image.open(ORIGINAL).convert("RGB")
    gap, head, row_h = 16, 44, 520
    rows = []
    for label, box in ENV_REVIEW:
        x0, y0, x1, y1 = box
        k = row_h / (y1 - y0)
        size = (round((x1 - x0) * k), row_h)
        rows.append((label, [im.crop(box).resize(size, Image.LANCZOS) for im in (orig, hand, composite_png.convert("RGB"))]))
    width = max(sum(c.width for c in r[1]) for r in rows) + 4 * gap
    sheet = Image.new("RGB", (width, len(rows) * (row_h + head + gap) + gap), "#ffffff")
    d = ImageDraw.Draw(sheet)
    f = bt.label_font(30)
    y = gap
    for label, cells in rows:
        d.text((gap, y), f"{label}: original, hand-built (previous), traced (new)", font=f, fill="#222222")
        x = gap
        for c in cells:
            sheet.paste(c, (x, y + head))
            x += c.width + gap
        y += row_h + head + gap
    bt.save_png(sheet, REVIEW / "env-hand-vs-traced.png")


PRIMS = 0                   # authored shapes counted by the primitives check (--env built)
OBJECT_HOLE = 4             # an enclosed transparent area this large (px, at 1x) in an object is a hole


def build_env_objects(layers):
    """Every environment object alone (objects/), cropped tight, transparent background; and
    the primitives check over every layer and object: only authored shapes, no traced paths."""
    from scipy import ndimage as ndi
    import build_env as be
    global PRIMS
    OBJECTS_OUT.mkdir(parents=True, exist_ok=True)
    PRIMS = sum(be.check_primitives(layers[n], n) for n in LAYER_ORDER)
    sizes = {}
    names = be.object_ids()
    for name in names:
        svg, width, (x0, y0, x1, y1) = be.standalone(name)
        PRIMS += be.check_primitives(svg, f"objects/{name}.svg")
        (OBJECTS_OUT / f"{name}.svg").write_text(svg, encoding="utf-8")
        img = bt.png_image(bt.render(svg, width))
        a = np.asarray(img)[..., 3]
        assert max(img.size) >= be.OBJECT_LONG, f"object {name}: only {img.size}"
        assert a.max() == 255, f"object {name}: empty"
        if name != "sky":
            border = np.concatenate([a[0], a[-1], a[:, 0], a[:, -1]])
            assert (border == 0).any(), f"object {name}: no transparent edge"
            if not (name != "sun" and be.obj(name)["holes"]):
                solid = a > 128
                holes = ndi.binary_fill_holes(solid) & ~solid
                k = img.width / (x1 - x0)
                big = holes.sum() / (k * k)
                assert big < OBJECT_HOLE, f"object {name}: has a hole ({big:.0f} px)"
        bt.save_png(img, OBJECTS_OUT / f"{name}.png")
        sizes[name] = img.size
    # objects/ holds exactly the built objects
    for p in OBJECTS_OUT.glob("*"):
        if p.stem not in names:
            p.unlink()
    return sizes


# Spots compared in review/env-built-vs-original.png: (label, box in original px). The first
# four are the ones Derek circled.
ENV_SPOTS = [("top-left cloud", (60, 0, 760, 200)),
             ("dark-roofed house behind the D", (60, 300, 520, 560)),
             ("yellow house fence and wall above the corgi", (680, 620, 960, 900)),
             ("right of the final D: blue house roof and sky", (2120, 200, 2560, 520)),
             ("red house", (0, 180, 480, 1000)),
             ("green house", (2420, 180, 2816, 1000)),
             ("far street, left", (900, 480, 1400, 800)),
             ("far street, right", (1600, 460, 2120, 800)),
             ("mailbox and toys, left", (340, 800, 760, 1260)),
             ("mailbox and toys, right", (2280, 800, 2816, 1260))]
# The previous traced composite, for comparison. It is not rebuilt and not in git: copy it into
# review/ from the shared folder to get the middle column; without it the sheet has two columns.
ENV_TRACED = REVIEW / "env-traced-composite.png"


def env_built_review(composite_png):
    """Per spot: the original, the previous traced scene (when review/env-traced-composite.png
    is present) and the built scene (each with the dogs and the title), same box, at 2-3x."""
    orig = Image.open(ORIGINAL).convert("RGB")
    built = composite_png.convert("RGB")
    if ENV_TRACED.exists():
        images, names = (orig, Image.open(ENV_TRACED).convert("RGB"), built), "original, traced (previous), built (new)"
    else:
        images, names = (orig, built), "original, built (new)"
    gap, head = 16, 44
    rows = []
    for label, box in ENV_SPOTS:
        x0, y0, x1, y1 = box
        k = min(3.0, max(2.0, 640 / (y1 - y0)), 1500 / (x1 - x0))
        size = (round((x1 - x0) * k), round((y1 - y0) * k))
        rows.append((label, k, [im.crop(box).resize(size, Image.LANCZOS) for im in images]))
    width = max(sum(c.width for c in r[2]) for r in rows) + (len(images) + 1) * gap
    height = sum(r[2][0].height + head + gap for r in rows) + gap
    sheet = Image.new("RGB", (width, height), "#ffffff")
    d = ImageDraw.Draw(sheet)
    f = bt.label_font(30)
    y = gap
    for label, k, cells in rows:
        d.text((gap, y), f"{label} ({k:.1f}x): {names}", font=f, fill="#222222")
        x = gap
        for c in cells:
            sheet.paste(c, (x, y + head))
            x += c.width + gap
        y += cells[0].height + head + gap
    bt.save_png(sheet, REVIEW / "env-built-vs-original.png")


def objects_sheet():
    """Every standalone object on a checkerboard, labelled."""
    import build_env as be
    names = be.object_ids()
    tw, th, cols, head = 300, 240, 8, 30
    rows = math.ceil(len(names) / cols)
    sh = Image.new("RGB", (cols * (tw + 12) + 12, rows * (th + head + 12) + 12), "#ffffff")
    d = ImageDraw.Draw(sh)
    f = bt.label_font(18)
    for i, name in enumerate(names):
        x = 12 + (i % cols) * (tw + 12)
        y = 12 + (i // cols) * (th + head + 12)
        img = Image.open(OBJECTS_OUT / f"{name}.png").convert("RGBA")
        k = min(tw / img.width, th / img.height)
        img = img.resize((max(1, round(img.width * k)), max(1, round(img.height * k))), Image.LANCZOS)
        cb = checker((tw, th), 12)
        cb.alpha_composite(img, ((tw - img.width) // 2, (th - img.height) // 2))
        sh.paste(cb.convert("RGB"), (x, y + head))
        d.text((x, y + 4), name, font=f, fill="#222222")
    bt.save_png(sh, REVIEW / "objects-sheet.png")


def check_traced(td):
    """Every traced dog stays inside its mask (bar anti-aliasing and the strip tucked
    under a dog in front), and hand-completed hidden parts stay under the dog in front."""
    leaks = {}
    plain = td.dog_layers(hidden=False)
    for name in td.dog_names():
        a = np.asarray(bt.png_image(bt.render(plain[name], W * SCALE)))[..., 3]
        n = td.mask_check(name, a)
        assert n == 0, f"{name}: {n} traced px outside its mask"
        defs, hid = td.hidden_svg(name, td.trace(name)[1])
        if hid:
            ha = np.asarray(bt.png_image(bt.render(layer_svg(name, hid, defs), W * SCALE)))[..., 3]
            assert ha.max() > 0, f"{name}: hidden parts render empty"
            h = td.hidden_check(name, ha)
            assert h == 0, f"{name}: {h} hidden-part px not covered by the dog in front"
        leaks[name] = n
    return leaks


def main():
    ap = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    ap.add_argument("--no-review", action="store_true", help="skip the review/ comparison images")
    ap.add_argument("--no-repeat-check", action="store_true", help="skip the rebuild-is-identical check")
    ap.add_argument("--dogs", choices=DOG_SOURCES, default="traced",
                    help="traced from the original (default) or the previous hand-drawn dogs")
    ap.add_argument("--env", choices=ENV_SOURCES, default="built",
                    help="clean authored objects (default), the previous trace of the original, "
                         "or the first hand-built environment")
    args = ap.parse_args()
    global DOGS_SRC, ENV_SRC
    DOGS_SRC = args.dogs
    ENV_SRC = args.env
    clear, sizes, leaks, objects, holes = build(review=not args.no_review)
    reviews = ["scene-vs-original.png", "scene-layers-sheet.png", "dogs-vs-original.png", "env-only.png"]
    if DOGS_SRC == "traced":
        reviews.append("dogs-hand-vs-traced.png")
    if ENV_SRC == "traced":
        reviews.append("env-hand-vs-traced.png")
    if ENV_SRC == "built":
        reviews += ["env-built-vs-original.png", "objects-sheet.png"]
    outputs = list(OUT.glob("*")) + list(DOGS_OUT.glob("*")) + (
        [REVIEW / r for r in reviews] if not args.no_review else [])
    if ENV_SRC in ("traced", "built"):
        outputs += list(OBJECTS_OUT.glob("*"))
    first = digest(outputs)
    if not args.no_repeat_check:
        if DOGS_SRC == "traced":
            dog_source().clear_caches()     # re-trace from scratch, not from memory
        if ENV_SRC == "traced":
            import trace_env as te
            te.clear_caches()
        if ENV_SRC == "built":
            import build_env as be
            be.clear_caches()
        build(review=not args.no_review)
        second = digest(outputs)
        assert first == second, "rebuild is not byte-identical: " + str(
            [k for k in first if first[k] != second.get(k)])
    print(f"ok: {len(clear)} layers at {W * SCALE}x{H * SCALE}, composite {W}x{H} opaque, unique ids, "
          "transparent outside content: " + ", ".join(f"{k} {v:.0%}" for k, v in clear.items() if k != "sky")
          + "; standalone dogs: " + ", ".join(f"{k} {w}x{h}" for k, (w, h) in sizes.items())
          + " (transparent edges)"
          + (f"; traced dogs inside their masks ({', '.join(f'{k} {v} px out' for k, v in leaks.items())})"
             if leaks else "")
          + "; env-only opaque, no holes where the dogs were (dE vs ground around: "
          + ", ".join(f"{k} {v:.1f}" for k, v in holes.items()) + ")"
          + (f"; {len(objects)} standalone objects >= 2000 px" if objects else "")
          + (f", no holes; {PRIMS} authored primitives (no traced paths) in the layers and objects"
             if ENV_SRC == "built" else "")
          + STREET_OK + f"; {len(first)} files reproducible")
    return 0


if __name__ == "__main__":
    sys.exit(main())
