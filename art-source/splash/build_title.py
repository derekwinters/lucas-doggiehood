#!/usr/bin/env python3
"""Rebuild the DOGGIEHOOD splash title as a layered vector (SVG) plus PNG exports.

Deterministic: every output is written from scratch from the font file and the
parameters below; running it twice produces byte-identical files (self-checked).

    pip install fonttools cairosvg pillow numpy
    python3 art-source/splash/build_title.py          # build + self-check
    python3 art-source/splash/build_title.py --no-review   # skip review images

Letterforms: Fredoka (SIL OFL 1.1, fonts/OFL.txt), SemiBold instance, converted to
plain SVG paths -- the SVGs have no font dependency. Paw prints are our own
shapes (see paw_shapes()).

Coordinates are in the pixel space of Assets/Art/Splash/cover-art.png, so the
rebuild can be laid directly over the original for comparison.
"""
from __future__ import annotations

import argparse
import hashlib
import io
import math
import re
import sys
from pathlib import Path

import cairosvg
import numpy as np
import pathops
from fontTools.pens.boundsPen import BoundsPen
from fontTools.pens.recordingPen import RecordingPen
from fontTools.pens.svgPathPen import SVGPathPen
from fontTools.pens.transformPen import TransformPen
from fontTools.ttLib import TTFont
from fontTools.varLib import instancer
from PIL import Image

HERE = Path(__file__).resolve().parent
REPO = HERE.parent.parent
FONT = HERE / "fonts" / "Fredoka-Variable.ttf"
ORIGINAL = REPO / "Assets" / "Art" / "Splash" / "cover-art.png"
OUT = HERE / "title"
REVIEW = HERE / "review"

# --------------------------------------------------------------------------
# Layout (original-image pixel space)
# --------------------------------------------------------------------------
VIEW = [560, 100, 1720, 470]  # x, y, w, h of the artboard; fit_view() tightens it
VIEW0 = list(VIEW)
CAP_PX = 174.0                # cap height of the letter fill, px
FONT_WEIGHT = 600             # Fredoka wght axis (SemiBold; effects add the weight)
FONT_WIDTH = 100              # Fredoka wdth axis

# Letters sit on a gentle arch: a circle of radius ARC_R whose top is at
# (ARC_CX, ARC_TOP); each letter is rotated to the arch's tangent.
ARC_CX, ARC_TOP, ARC_R = 1420.0, 398.0, 3600.0

# (glyph, colour scheme, centre x, extra dy, scale, extra rotation deg, x-stretch)
# The small dy/scale/rotation tweaks give the hand-set "bounce" of the original.
LETTERS = [
    ("D", "warm", 740, -6, 1.06, 4.0, 0.98),
    ("O", "warm", 906, 0, 1.03, 0.0, 1.00),   # first O: solid, holds the paw
    ("G", "warm", 1083, -2, 1.02, 0.0, 1.00),
    ("G", "warm", 1240, 0, 1.00, 0.0, 1.00),
    ("dotlessi", "warm", 1368, 0, 1.00, 0.0, 1.12),
    ("E", "warm", 1488, 0, 1.00, 0.0, 1.02),
    ("H", "cool", 1642, 0, 1.00, 0.0, 0.96),
    ("O", "cool", 1800, 0, 0.99, 0.0, 0.93),
    ("O", "cool", 1950, 2, 1.00, 0.0, 0.93),
    ("D", "cool", 2112, 6, 1.05, 3.0, 0.98),
]
SOLID_O_INDEX = 1   # this letter drops its counter and carries the brown paw
I_INDEX = 4         # paw "dot" sits above this letter
I_STEM_PX = 118.0   # visible height of the dotless i stem, px
PAW_I_R = 40.0      # half-size of the paw that dots the i, px
PAW_I_GAP = 6.0     # navy outline visible between the i-dot paw and the stem, px (as in the original)
PAW_I_EXTRUDE = 5.0  # the i-dot paw's own (shallower) rim extrusion, px
PAW_O = (0, -2, 58)   # brown paw inside the solid O (dx, dy from centre), px

# Paw-in-the-O variants (#841 review: variant A reads as a face to some eyes).
# The committed default stays A until Derek chooses; --o-paw selects another.
O_PAW_VARIANTS = {
    "A": dict(label="A - current", shape="classic", rot=0.0, r=58.0, dy=-2.0),
    "B": dict(label="B - rotated 28 deg", shape="classic", rot=28.0, r=58.0, dy=-2.0),
    "C": dict(label="C - reshaped (wide pad, toes close)", shape="wide", rot=0.0, r=62.0, dy=-4.0),
    "D": dict(label="D - rotated + reshaped", shape="wide", rot=28.0, r=62.0, dy=-2.0),
    "E": dict(label="E - no paw in the O", shape=None, rot=0.0, r=0.0, dy=0.0),
}
O_PAW_DEFAULT = "A"

# Highlights: one oval per letter, clipped to the letter's fill inset by HL_MARGIN
HL_MARGIN = 6.0           # clip: clear space between a highlight and the fill edge, px
HL_FIT_SLACK = 2.0        # ovals are fitted this much further in, so the clip never visibly cuts one
HL_SIZE = (17.0, 10.0)    # preferred oval radii, px (shrunk to fit narrow strokes)
HL_TILT = -35.0           # preferred oval tilt relative to the letter, degrees

# Effect thicknesses, px (all measured outward from the glyph outline)
EMBOLDEN = 3.5     # extra weight added to the font
RIM = 9.0          # inner coloured rim
OUTLINE = 9.0     # dark navy outer outline
EXTRUDE = 10.0      # rim/outline thickness added at the bottom (3D depth)
DEPTH = 6.0        # navy drop below the outline
BAND_W = 96.0      # backing band behind the letters (outline layers only)
SHADOW = (0.0, 16.0, 0.22)  # soft drop shadow dx, dy, opacity
INNER_LIGHT = 2.5  # light line just inside the rim
BEVEL = 2.5        # fill sits this far up, so the light line shows mostly along the bottom

# Colours sampled from the original art
NAVY = "#273e64"
SCHEMES = {
    "warm": dict(top="#ffdf6a", mid="#fbc552", bottom="#f2902f", rim="#a9491b",
                 rim_dark="#6e3014", light="#ffe79a", glint="#ffffff"),
    "cool": dict(top="#8ee7fb", mid="#62c4ec", bottom="#3f97d6", rim="#205f9a",
                 rim_dark="#163e66", light="#a6ecff", glint="#ffffff"),
}
PAW_BROWN = "#8f4520"

# Flat / screen-print palette (4 colours: navy, orange, blue, brown)
FLAT_KEYLINE = 4.0  # navy line separating the i-dot paw from the i in the flat version
FLAT = dict(navy="#1f3358", warm="#f7a531", cool="#4fb0e6", brown="#8f4520")

SKY_TOP, SKY_BOTTOM = "#8fdff6", "#b0f0f0"   # sampled sky behind the title

BIG_W = 6000
PREVIEW_W = 1600


# --------------------------------------------------------------------------
# Geometry
# --------------------------------------------------------------------------
def load_font() -> TTFont:
    font = TTFont(str(FONT))
    return instancer.instantiateVariableFont(
        font, {"wght": FONT_WEIGHT, "wdth": FONT_WIDTH}, inplace=False)


def fmt(v: float) -> str:
    s = f"{v:.2f}".rstrip("0").rstrip(".")
    return "0" if s in ("-0", "") else s


def split_contours(glyphset, name):
    rec = RecordingPen()
    glyphset[name].draw(rec)
    contours, cur = [], []
    for op, args in rec.value:
        cur.append((op, args))
        if op in ("closePath", "endPath"):
            contours.append(cur)
            cur = []
    return contours


def signed_area(contour):
    pts = [a[-1] for op, a in contour if a]
    return sum(x0 * y1 - x1 * y0 for (x0, y0), (x1, y1) in zip(pts, pts[1:] + pts[:1])) / 2


def contour_area(contour):
    return abs(signed_area(contour))


def outer_contours(contours):
    """Contours wound like the biggest one, i.e. the outsides without the counters."""
    sign = signed_area(max(contours, key=contour_area)) > 0
    return [c for c in contours if (signed_area(c) > 0) == sign]


def letter_matrix(cx, base_y, scale, rot_deg, stretch, gcx, gbase):
    """Affine (font units -> px): glyph bottom-centre lands on (cx, base_y)."""
    a = math.radians(rot_deg)
    cs, sn = math.cos(a), math.sin(a)
    sx, sy = scale * stretch, -scale     # flip font y-up to SVG y-down
    # p = R * S * (q - g) + c
    m = (cs * sx, sn * sx, -sn * sy, cs * sy)
    e = cx - (m[0] * gcx + m[2] * gbase)
    f = base_y - (m[1] * gcx + m[3] * gbase)
    return (m[0], m[1], m[2], m[3], e, f)


def apply(m, x, y):
    return (m[0] * x + m[2] * y + m[4], m[1] * x + m[3] * y + m[5])


def path_d(contours, matrix):
    pen = SVGPathPen(None, ntos=fmt)
    tpen = TransformPen(pen, matrix)
    for c in contours:
        for op, args in c:
            getattr(tpen, op)(*args)
    return pen.getCommands()


def build_letters(o_paw=O_PAW_DEFAULT):
    solid_o = O_PAW_VARIANTS[o_paw]["shape"] is not None
    font = load_font()
    gs = font.getGlyphSet()
    cap = font["OS/2"].sCapHeight
    k = CAP_PX / cap
    letters = []
    for idx, (gname, scheme, cx, dy, sc, rot, stretch) in enumerate(LETTERS):
        contours = split_contours(gs, gname)
        if idx == SOLID_O_INDEX and solid_o:
            contours = [max(contours, key=contour_area)]
        bp = BoundsPen(gs)
        gs[gname].draw(bp)
        x0, y0, x1, y1 = bp.bounds
        gcx = (x0 + x1) / 2
        scale = k * sc
        if gname == "dotlessi":
            scale = I_STEM_PX / (y1 - y0) * sc
        # arch: baseline point and tangent angle at cx
        ang = math.asin((cx - ARC_CX) / ARC_R)
        base_y = ARC_TOP + ARC_R * (1 - math.cos(ang)) + dy
        rot_total = math.degrees(ang) + rot
        m = letter_matrix(cx, base_y, scale, rot_total, stretch, gcx, 0)
        centre = apply(m, gcx, (y0 + y1) / 2)
        top = apply(m, gcx, y1)
        fill_pp = fill_region(contours, m)
        hl_clip = inset(fill_pp, HL_MARGIN)
        hl_fit = inset(fill_pp, HL_MARGIN + HL_FIT_SLACK)
        letters.append(dict(idx=idx, glyph=gname, scheme=scheme, d=path_d(contours, m),
                            fill_pp=fill_pp, o_paw=o_paw, hl_clip=hl_clip, hl_fit=hl_fit,
                            hl_clip_d=pp_d(hl_clip),
                            d_solid=path_d(outer_contours(contours), m),
                            m=m, bounds=(x0, y0, x1, y1), centre=centre, top=top,
                            rot=rot_total, scale=scale * stretch, height=scale * (y1 - y0),
                            width=scale * stretch * (x1 - x0)))
    return letters


def to_pathops(contours, matrix):
    pp = pathops.Path()
    tpen = TransformPen(pp.getPen(), matrix)
    for c in contours:
        for op, args in c:
            getattr(tpen, op)(*args)
    return pp


def fill_region(contours, m):
    """The visible fill of a letter as a pathops path: the glyph grown by
    (EMBOLDEN - INNER_LIGHT) and shifted up by BEVEL, exactly as the fill layer draws it."""
    glyph = to_pathops(contours, m)
    grow = pathops.Path()
    grow.addPath(glyph)
    grow.stroke(2 * (EMBOLDEN - INNER_LIGHT), pathops.LineCap.ROUND_CAP, pathops.LineJoin.ROUND_JOIN, 4)
    grow.convertConicsToQuads()
    region = pathops.op(glyph, grow, pathops.PathOp.UNION)
    region.transform(1, 0, 0, 1, 0, -BEVEL)
    return region


def inset(region, margin):
    """region shrunk by margin px on every side (region minus a band along its edge)."""
    band = pathops.Path()
    band.addPath(region)
    band.stroke(2 * margin, pathops.LineCap.ROUND_CAP, pathops.LineJoin.ROUND_JOIN, 4)
    band.convertConicsToQuads()
    return pathops.op(region, band, pathops.PathOp.DIFFERENCE)


def pp_d(pp):
    pen = SVGPathPen(None, ntos=fmt)
    pp.draw(pen)
    return pen.getCommands()


def paw_shapes(cx, cy, r, rot=0.0, shape="classic"):
    """Our own paw print: 4 toe ovals + a main pad. r = half-size, px.

    classic: rounded 3-lobed pad, toes in a wide fan.
    wide:    wider heart/triangle pad that fills more of the space, toes arched
             close around it (no "two eyes + open mouth" spacing)."""
    def P(x, y):
        a = math.radians(rot)
        x, y = x * r, y * r
        return (cx + x * math.cos(a) - y * math.sin(a), cy + x * math.sin(a) + y * math.cos(a))

    def pt(x, y):
        px, py = P(x, y)
        return f"{fmt(px)} {fmt(py)}"

    if shape == "wide":
        pad = ("M " + pt(0, -0.22)
               + " C " + pt(0.34, -0.22) + ", " + pt(0.70, 0.16) + ", " + pt(0.80, 0.44)
               + " C " + pt(0.90, 0.72) + ", " + pt(0.66, 0.90) + ", " + pt(0.42, 0.82)
               + " C " + pt(0.26, 0.77) + ", " + pt(0.14, 0.88) + ", " + pt(0, 0.88)
               + " C " + pt(-0.14, 0.88) + ", " + pt(-0.26, 0.77) + ", " + pt(-0.42, 0.82)
               + " C " + pt(-0.66, 0.90) + ", " + pt(-0.90, 0.72) + ", " + pt(-0.80, 0.44)
               + " C " + pt(-0.70, 0.16) + ", " + pt(-0.34, -0.22) + ", " + pt(0, -0.22) + " Z")
        toe_spec = [(-0.80, -0.16, 0.19, 0.25, -38), (-0.31, -0.58, 0.20, 0.26, -12),
                    (0.31, -0.58, 0.20, 0.26, 12), (0.80, -0.16, 0.19, 0.25, 38)]
    else:
        pad = ("M " + pt(-0.62, 0.62)
               + " C " + pt(-0.86, 0.28) + ", " + pt(-0.42, -0.12) + ", " + pt(0, -0.12)
               + " C " + pt(0.42, -0.12) + ", " + pt(0.86, 0.28) + ", " + pt(0.62, 0.62)
               + " C " + pt(0.48, 0.86) + ", " + pt(0.2, 0.78) + ", " + pt(0, 0.72)
               + " C " + pt(-0.2, 0.78) + ", " + pt(-0.48, 0.86) + ", " + pt(-0.62, 0.62) + " Z")
        toe_spec = [(-0.76, -0.30, 0.21, 0.28, -28), (-0.28, -0.72, 0.22, 0.30, -8),
                    (0.28, -0.72, 0.22, 0.30, 8), (0.76, -0.30, 0.21, 0.28, 28)]
    toes = []
    for (x, y, rx, ry, a) in toe_spec:
        px, py = P(x, y)
        toes.append((px, py, rx * r, ry * r, a + rot))
    return pad, toes


def paw_svg(pad, toes, attrs):
    out = [f'<path d="{pad}" {attrs}/>']
    for (x, y, rx, ry, a) in toes:
        out.append(f'<ellipse cx="{fmt(x)}" cy="{fmt(y)}" rx="{fmt(rx)}" ry="{fmt(ry)}" '
                   f'transform="rotate({fmt(a)} {fmt(x)} {fmt(y)})" {attrs}/>')
    return "".join(out)


# --------------------------------------------------------------------------
# SVG assembly
# --------------------------------------------------------------------------
def layer(lid, label, body, extra=""):
    return (f'<g id="{lid}" inkscape:label="{label}" inkscape:groupmode="layer"{extra}>\n'
            f'{body}\n</g>\n')


def svg_doc(body, defs=""):
    x, y, w, h = VIEW
    return ('<?xml version="1.0" encoding="UTF-8"?>\n'
            f'<svg xmlns="http://www.w3.org/2000/svg" '
            f'xmlns:inkscape="http://www.inkscape.org/namespaces/inkscape" '
            f'width="{w}" height="{h}" viewBox="{x} {y} {w} {h}">\n'
            '<title>DOGGIEHOOD title</title>\n'
            '<desc>Doggiehood splash title, rebuilt as vector. Letterforms: Fredoka '
            '(SIL Open Font License 1.1). Generated by art-source/splash/build_title.py.</desc>\n'
            f'<defs>\n{defs}</defs>\n{body}</svg>\n')


def stroke(colour, width, extra=""):
    return (f'fill="{colour}" stroke="{colour}" stroke-width="{fmt(width)}" '
            f'stroke-linejoin="round" stroke-linecap="round"{extra}')


PAW_PAD_BOTTOM = 0.80   # lowest point of the classic pad, in paw half-sizes


def i_paw_geom(letters):
    """Paw dotting the i, raised so its rim (and its extrusion) clears the stem's rim
    by PAW_I_GAP of navy outline -- the gap the original has."""
    i = letters[I_INDEX]
    tx, ty = i["top"]
    r = PAW_I_R
    rim = EMBOLDEN + RIM
    lift = PAW_PAD_BOTTOM * r + 2 * rim + PAW_I_EXTRUDE + PAW_I_GAP
    return paw_shapes(tx, ty - lift, r, rot=i["rot"])


def o_paw_geom(letters):
    """Paw inside the solid first O, or None for variant E (no paw)."""
    v = O_PAW_VARIANTS[letters[0]["o_paw"]]
    if v["shape"] is None:
        return None, None
    o = letters[SOLID_O_INDEX]
    cx, cy = o["centre"]
    return paw_shapes(cx, cy + v["dy"], v["r"], rot=o["rot"] + v["rot"], shape=v["shape"])


def build_layered(letters, only=None):
    """only: set of top-level layer ids to keep (used by the self-checks)."""
    defs = []
    for name, sc in SCHEMES.items():
        defs.append(f'<linearGradient id="grad-{name}" x1="0" y1="0" x2="0" y2="1">'
                    f'<stop offset="0" stop-color="{sc["top"]}"/>'
                    f'<stop offset="0.5" stop-color="{sc["mid"]}"/>'
                    f'<stop offset="1" stop-color="{sc["bottom"]}"/></linearGradient>\n')
    defs.append('<linearGradient id="grad-glint" x1="0" y1="0" x2="0" y2="1">'
                '<stop offset="0" stop-color="#ffffff" stop-opacity="0.95"/>'
                '<stop offset="1" stop-color="#ffffff" stop-opacity="0.55"/></linearGradient>\n')

    ipad, itoes = i_paw_geom(letters)
    opad, otoes = o_paw_geom(letters)
    o_out = EMBOLDEN + RIM + OUTLINE
    o_rim = EMBOLDEN + RIM

    def all_shapes(attrs, dy=0.0, paw=True):
        t = f' transform="translate(0 {fmt(dy)})"' if dy else ""
        body = backing_band(letters) + "".join(f'<path d="{L["d_solid"]}"/>' for L in letters)
        if paw:
            body += paw_svg(ipad, itoes, "")
        return f'<g{t} {attrs}>{body}</g>'

    shadow = all_shapes(stroke(NAVY, 2 * o_out, f' opacity="{SHADOW[2]}"'),
                        dy=EXTRUDE + DEPTH + SHADOW[1])
    shadow = f'<g transform="translate({fmt(SHADOW[0])} 0)">{shadow}</g>' if SHADOW[0] else shadow
    depth = all_shapes(stroke(NAVY, 2 * o_out), dy=EXTRUDE + DEPTH)
    outline = all_shapes(stroke(NAVY, 2 * o_out)) + all_shapes(stroke(NAVY, 2 * o_out), dy=EXTRUDE)

    rim_dark, rim, light, fill, glints = [], [], [], [], []
    for L in letters:
        sc = SCHEMES[L["scheme"]]
        rim_dark.append(f'<path d="{L["d"]}" transform="translate(0 {fmt(EXTRUDE)})" '
                        f'{stroke(sc["rim_dark"], 2 * o_rim)}/>')
        rim.append(f'<path d="{L["d"]}" {stroke(sc["rim"], 2 * o_rim)}/>')
        light.append(f'<path d="{L["d"]}" {stroke(sc["light"], 2 * EMBOLDEN)}/>')
        fill.append(f'<path id="fill-{L["idx"]}" d="{L["d"]}" transform="translate(0 {fmt(-BEVEL)})" '
                    f'fill="url(#grad-{L["scheme"]})" '
                    f'stroke="url(#grad-{L["scheme"]})" stroke-width="{fmt(2 * (EMBOLDEN - INNER_LIGHT))}" '
                    f'stroke-linejoin="round"/>')
        glints.append(glint_svg(L, o_paw_keepout(letters)))
    # paw dot on the i: rim + fill in the warm scheme
    w = SCHEMES["warm"]
    rim_dark.append(f'<g transform="translate(0 {fmt(PAW_I_EXTRUDE)})" {stroke(w["rim_dark"], 2 * o_rim)}>'
                    f'{paw_svg(ipad, itoes, "")}</g>')
    rim.append(f'<g {stroke(w["rim"], 2 * o_rim)}>{paw_svg(ipad, itoes, "")}</g>')
    paws = (f'<g id="paw-i-dot" inkscape:label="paw-i-dot" fill="url(#grad-warm)">'
            f'{paw_svg(ipad, itoes, "")}</g>')
    if opad:
        paws += (f'<g id="paw-in-o" inkscape:label="paw-in-o" fill="{PAW_BROWN}">'
                 f'{paw_svg(opad, otoes, "")}</g>')
    paw_glint, paw_clip = paw_glints(ipad, itoes)
    defs.append(paw_clip)
    for L in letters:
        defs.append(f'<clipPath id="hl-clip-{L["idx"]}"><path d="{L["hl_clip_d"]}"/></clipPath>\n')

    layers = [
        ("drop-shadow", shadow),
        ("depth", depth),
        ("outer-outline", outline),
        ("rim", f'<g id="rim-extrude" inkscape:label="rim-extrude">{"".join(rim_dark)}</g>'
                f'<g id="rim-edge" inkscape:label="rim-edge">{"".join(rim)}</g>'
                f'<g id="rim-inner-light" inkscape:label="rim-inner-light">{"".join(light)}</g>'),
        ("fill", "".join(fill)),
        ("paws", paws),
        ("highlights", "".join(glints) + paw_glint),
    ]
    body = "".join(layer(lid, lid, content) for lid, content in layers if only is None or lid in only)
    return svg_doc(body, "".join(defs))


PAD_ELLIPSE = {"classic": (0.0, 0.34, 0.78, 0.48), "wide": (0.0, 0.33, 0.88, 0.58)}


def o_paw_keepout(letters):
    """Ellipses (cx, cy, rx, ry, rot) a highlight must stay out of: the paw in the O
    (toes + an ellipse around the pad), grown by HL_MARGIN."""
    v = O_PAW_VARIANTS[letters[0]["o_paw"]]
    if v["shape"] is None:
        return []
    pad, toes = o_paw_geom(letters)
    o = letters[SOLID_O_INDEX]
    cx, cy = o["centre"]
    cy += v["dy"]
    r, rot = v["r"], o["rot"] + v["rot"]
    ux, uy, urx, ury = PAD_ELLIPSE[v["shape"]]
    a = math.radians(rot)
    px = cx + ux * r * math.cos(a) - uy * r * math.sin(a)
    py = cy + ux * r * math.sin(a) + uy * r * math.cos(a)
    out = [(px, py, urx * r + HL_MARGIN, ury * r + HL_MARGIN, rot)]
    out += [(x, y, rx + HL_MARGIN, ry + HL_MARGIN, t) for (x, y, rx, ry, t) in toes]
    return out


def in_ellipse(x, y, e):
    cx, cy, rx, ry, rot = e
    a = math.radians(rot)
    dx, dy = x - cx, y - cy
    u = dx * math.cos(a) + dy * math.sin(a)
    v = -dx * math.sin(a) + dy * math.cos(a)
    return (u / rx) ** 2 + (v / ry) ** 2 <= 1.0


def fit_highlight(L, keepout=()):
    """Place ONE oval highlight near the letter's top-left, fully inside the fill
    inset by HL_MARGIN. Tries the preferred size/tilt first, then smaller/flatter
    tilts, nearest to the anchor point. Deterministic grid search."""
    clip = L["hl_fit"]
    m = L["m"]
    x0, y0, x1, y1 = L["bounds"]
    w, h = x1 - x0, y1 - y0
    fx, fy = GLINT_POS.get(L["glyph"], (0.2, 0.86))
    ax, ay = apply(m, x0 + fx * w, y0 + fy * h)
    s = CAP_PX / 174.0
    angles = [k * math.pi / 36 for k in range(72)]

    def fits(cx, cy, rx, ry, rot):
        a = math.radians(rot)
        ca, sa = math.cos(a), math.sin(a)
        for t in angles:
            for f in (1.0, 0.5):
                ex, ey = rx * f * math.cos(t), ry * f * math.sin(t)
                qx, qy = cx + ex * ca - ey * sa, cy + ex * sa + ey * ca
                if not clip.contains((qx, qy)) or any(in_ellipse(qx, qy, e) for e in keepout):
                    return False
        return clip.contains((cx, cy))

    offsets = sorted(((dx, dy) for dx in range(-40, 41, 2) for dy in range(-40, 41, 2)),
                     key=lambda o: (o[0] ** 2 + o[1] ** 2, o))
    for k in (1.0, 0.9, 0.8, 0.7, 0.6, 0.5):
        rx, ry = HL_SIZE[0] * s * k, HL_SIZE[1] * s * k
        for tilt in (HL_TILT, -20.0, -5.0, -55.0, -75.0):
            rot = L["rot"] + tilt
            for dx, dy in offsets:
                if fits(ax + dx, ay + dy, rx, ry, rot):
                    return ax + dx, ay + dy, rx, ry, rot
    raise AssertionError(f"no room for a highlight in letter {L['idx']} ({L['glyph']})")


def glint_svg(L, keepout=()):
    """One glossy oval highlight per letter, clipped to the letter's inset fill."""
    cx, cy, rx, ry, rot = fit_highlight(L, keepout if L["idx"] == SOLID_O_INDEX else ())
    # clip on a wrapping group: on the ellipse itself the clip would rotate with it
    return (f'<g clip-path="url(#hl-clip-{L["idx"]})">'
            f'<ellipse id="highlight-{L["idx"]}" cx="{fmt(cx)}" cy="{fmt(cy)}" rx="{fmt(rx)}" '
            f'ry="{fmt(ry)}" transform="rotate({fmt(rot)} {fmt(cx)} {fmt(cy)})" '
            f'fill="url(#grad-glint)"/></g>')


# highlight anchor as a fraction of the glyph bounds (x from left, y from bottom)
GLINT_POS = {"D": (0.24, 0.80), "O": (0.27, 0.80), "G": (0.28, 0.80), "dotlessi": (0.45, 0.80),
             "E": (0.24, 0.84), "H": (0.14, 0.82)}


def paw_glints(pad, toes):
    """Small gloss on the two middle toes of the i-dot paw, clipped inside those toes."""
    out, clip = [], ['<clipPath id="hl-clip-paw">']
    for (x, y, rx, ry, a) in toes[1:3]:
        clip.append(f'<ellipse cx="{fmt(x)}" cy="{fmt(y)}" rx="{fmt(rx - 2)}" ry="{fmt(ry - 2)}" '
                    f'transform="rotate({fmt(a)} {fmt(x)} {fmt(y)})"/>')
        gx, gy = x - rx * 0.25, y - ry * 0.35
        out.append(f'<ellipse cx="{fmt(gx)}" cy="{fmt(gy)}" rx="{fmt(rx * 0.32)}" ry="{fmt(ry * 0.22)}" '
                   f'transform="rotate({fmt(a - 30)} {fmt(gx)} {fmt(gy)})" fill="#ffffff" opacity="0.85"/>')
    clip.append('</clipPath>\n')
    return f'<g clip-path="url(#hl-clip-paw)">{"".join(out)}</g>', "".join(clip)


def backing_band(letters):
    """A thick line through the letter centres, hidden behind the letters, so the
    outline is one connected silhouette with no slivers of background between letters."""
    pts = []
    for L in letters:
        if L["idx"] == I_INDEX:
            pts.append(L["top"])   # also bridges the notch between the G, the i and its paw
        pts.append(L["centre"])
    d = "M " + " L ".join(f"{fmt(x)} {fmt(y)}" for x, y in pts)
    return (f'<path d="{d}" fill="none" stroke-width="{fmt(BAND_W)}" '
            f'stroke-linejoin="round" stroke-linecap="round"/>')


def build_flat(letters):
    """Screen-print version: solid colours only, 4 inks, no gradients/gloss/shadow."""
    ipad, itoes = i_paw_geom(letters)
    opad, otoes = o_paw_geom(letters)
    o_out = EMBOLDEN + RIM + OUTLINE
    shapes = (backing_band(letters) + "".join(f'<path d="{L["d_solid"]}"/>' for L in letters)
              + paw_svg(ipad, itoes, ""))
    outline = (f'<g {stroke(FLAT["navy"], 2 * o_out)}>{shapes}</g>'
               f'<g transform="translate(0 {fmt(EXTRUDE + DEPTH)})" {stroke(FLAT["navy"], 2 * o_out)}>{shapes}</g>')
    fills = []
    for L in letters:
        c = FLAT[L["scheme"]]
        fills.append(f'<path d="{L["d"]}" {stroke(c, 2 * (EMBOLDEN + RIM * 0.45))}/>')
    paws = (f'<g id="paw-i-dot-keyline" {stroke(FLAT["navy"], 2 * FLAT_KEYLINE)}>'
            f'{paw_svg(ipad, itoes, "")}</g>'
            f'<g id="paw-i-dot" fill="{FLAT["warm"]}">{paw_svg(ipad, itoes, "")}</g>')
    if opad:
        paws += f'<g id="paw-in-o" fill="{FLAT["brown"]}">{paw_svg(opad, otoes, "")}</g>'
    body = (layer("outer-outline", "outer-outline (navy ink)", outline)
            + layer("fill", "fill (orange + blue inks)", "".join(fills))
            + layer("paws", "paws (brown + orange inks)", paws))
    return svg_doc(body)


# --------------------------------------------------------------------------
# Rendering + checks
# --------------------------------------------------------------------------
def render(svg: str, width: int) -> bytes:
    return cairosvg.svg2png(bytestring=svg.encode("utf-8"), output_width=width)


def png_image(data: bytes) -> Image.Image:
    return Image.open(io.BytesIO(data)).convert("RGBA")


def save_png(img: Image.Image, path: Path):
    # strip any timestamps/metadata so the bytes are reproducible
    img.save(path, format="PNG", optimize=True)


def on_background(img: Image.Image, colour) -> Image.Image:
    bg = Image.new("RGBA", img.size, colour)
    bg.alpha_composite(img)
    return bg.convert("RGB")


def sky(size):
    w, h = size
    t = np.linspace(0, 1, h)[:, None]
    c0 = np.array([int(SKY_TOP[i:i + 2], 16) for i in (1, 3, 5)], float)
    c1 = np.array([int(SKY_BOTTOM[i:i + 2], 16) for i in (1, 3, 5)], float)
    rows = (c0 * (1 - t) + c1 * t).astype(np.uint8)
    arr = np.repeat(rows[:, None, :], w, axis=1)
    return Image.fromarray(arr, "RGB").convert("RGBA")


def build_review(layered_svg: str, flat_png: Image.Image, preview: Image.Image):
    REVIEW.mkdir(parents=True, exist_ok=True)
    x, y, w, h = VIEW
    orig = Image.open(ORIGINAL).convert("RGB").crop((x, y, x + w, y + h))
    rebuild = png_image(render(layered_svg, w))
    bg = sky(rebuild.size)
    bg.alpha_composite(rebuild)
    gap = 24
    sheet = Image.new("RGB", (w, 2 * h + gap), "#ffffff")
    sheet.paste(orig, (0, 0))
    sheet.paste(bg.convert("RGB"), (0, h + gap))
    save_png(sheet, REVIEW / "title-vs-original.png")
    # overlay: rebuild at 50% over the original, for alignment checks
    ov = Image.blend(orig.convert("RGBA"), Image.alpha_composite(orig.convert("RGBA"), rebuild), 0.5)
    save_png(ov.convert("RGB"), REVIEW / "title-overlay-original.png")
    pad = 60
    for name, colour in (("title-on-white.png", "#ffffff"), ("title-on-navy-shirt.png", "#1b2233"),
                         ("title-on-black-shirt.png", "#111111")):
        canvas = Image.new("RGBA", (preview.width + 2 * pad, preview.height + 2 * pad), colour)
        canvas.alpha_composite(preview, (pad, pad))
        save_png(canvas.convert("RGB"), REVIEW / name)
    canvas = Image.new("RGBA", (flat_png.width + 2 * pad, flat_png.height + 2 * pad), "#ffffff")
    canvas.alpha_composite(flat_png, (pad, pad))
    save_png(canvas.convert("RGB"), REVIEW / "title-flat-on-white.png")


def label_font(size):
    from PIL import ImageFont
    return ImageFont.truetype(str(FONT), size)


def i_dot_review(layered_svg: str):
    """Close-up of the i and its paw dot: original (left) vs rebuild (right), 3x."""
    from PIL import ImageDraw
    x, y, w, h = VIEW
    box = (1280, 135, 1460, 440)           # original-image px around the i
    k = 3
    orig = Image.open(ORIGINAL).convert("RGB").crop(box)
    orig = orig.resize((orig.width * k, orig.height * k), Image.LANCZOS)
    reb = png_image(render(layered_svg, w * k))
    reb = reb.crop(((box[0] - x) * k, (box[1] - y) * k, (box[2] - x) * k, (box[3] - y) * k))
    bg = sky(reb.size)
    bg.alpha_composite(reb)
    pad, top = 20, 70
    sheet = Image.new("RGB", (2 * orig.width + 3 * pad, orig.height + top + pad), "#ffffff")
    sheet.paste(orig, (pad, top))
    sheet.paste(bg.convert("RGB"), (2 * pad + orig.width, top))
    d = ImageDraw.Draw(sheet)
    f = label_font(40)
    d.text((pad, 14), "original", font=f, fill="#222222")
    d.text((2 * pad + orig.width, 14), "rebuild", font=f, fill="#222222")
    save_png(sheet, REVIEW / "i-dot-vs-original.png")


def o_paw_variant_review():
    """Whole title once per paw-in-the-O option (A-E), labelled, plus one small preview each."""
    from PIL import ImageDraw
    rows = []
    for key, v in O_PAW_VARIANTS.items():
        letters = build_letters(key)
        img = png_image(render(build_layered(letters), PREVIEW_W))
        canvas = Image.new("RGBA", (img.width + 40, img.height + 40), "#ffffff")
        canvas.alpha_composite(img, (20, 20))
        save_png(canvas.convert("RGB"), REVIEW / f"o-paw-variant-{key}.png")
        rows.append((v["label"], canvas.convert("RGB")))
    f = label_font(44)
    head = 70
    W = rows[0][1].width
    sheet = Image.new("RGB", (W, sum(r.height + head for _, r in rows)), "#ffffff")
    d = ImageDraw.Draw(sheet)
    yy = 0
    for label, r in rows:
        d.text((24, yy + 14), label, font=f, fill="#222222")
        sheet.paste(r, (0, yy + head))
        yy += r.height + head
    save_png(sheet, REVIEW / "o-paw-variants.png")


def check_highlights_contained(letters):
    """Every highlight pixel must sit on fully opaque letter (or i-dot paw) fill."""
    w = 3000
    hl = np.asarray(png_image(render(build_layered(letters, only={"highlights"}), w)))[..., 3]
    ref = np.asarray(png_image(render(build_layered(letters, only={"fill", "paws"}), w)))[..., 3]
    outside = (hl > 0) & (ref < 255)
    assert hl.max() > 0, "highlights layer is empty"
    assert outside.sum() == 0, f"{int(outside.sum())} highlight pixels fall outside the fill"
    # the clip is only a safety net: the ovals are placed so it never cuts one
    unclipped_svg = re.sub(r' clip-path="url\(#hl-clip-[^)]*\)"', "", build_layered(letters, only={"highlights"}))
    unclipped = np.asarray(png_image(render(unclipped_svg, w)))[..., 3]
    cut = np.abs(unclipped.astype(int) - hl.astype(int)) > 8
    assert cut.sum() == 0, f"the highlight clip visibly cuts {int(cut.sum())} px off an oval"
    n = build_layered(letters).count('id="highlight-')
    assert n == len(letters), f"expected one highlight per letter, found {n}"
    return int((hl > 0).sum())


def check_i_gap(letters):
    """The i-dot paw must not touch the stem: between them, across the stem's width,
    there are >= PAW_I_GAP-1 px with no rim/fill/paw (that is where the navy outline
    shows), and in the full render those pixels are the outline navy."""
    x, y, w, h = VIEW
    k = 2
    bare = np.asarray(png_image(render(build_layered(letters, only={"rim", "fill", "paws"}), w * k)))
    full = np.asarray(png_image(render(build_layered(letters), w * k)))
    i = letters[I_INDEX]
    tx, ty = i["top"]
    ink = np.array([int(NAVY[j:j + 2], 16) for j in (1, 3, 5)])
    worst = None
    for dx in range(-10, 11, 2):
        col = round((tx + dx - x) * k)
        row = round((ty - y) * k)          # stem fill top
        while bare[row, col, 3] > 0:        # walk up through the stem's rim
            row -= 1
        gap_rows = []
        while bare[row, col, 3] == 0:
            gap_rows.append(row)
            row -= 1
        gap = len(gap_rows) / k
        assert gap >= PAW_I_GAP - 1, f"i-dot paw is only {gap:.1f}px from the stem at dx={dx}"
        navy = np.abs(full[gap_rows, col, :3].astype(int) - ink).max(axis=1)
        assert (navy <= 24).all() and (full[gap_rows, col, 3] == 255).all(), \
            "gap between i-dot paw and stem is not solid outline navy"
        worst = gap if worst is None else min(worst, gap)
    return worst


def check_no_holes(svg: str, label: str):
    """No pinholes: every not-fully-opaque pixel must connect to the image border.
    An enclosed sliver would print as shirt colour inside the outline."""
    from PIL import ImageDraw
    a = np.asarray(png_image(render(svg, 3000)))[..., 3]
    mask = Image.fromarray(np.where(a < 255, 255, 0).astype(np.uint8), "L").copy()
    ImageDraw.floodfill(mask, (0, 0), 128)
    holes = int((np.asarray(mask) == 255).sum())
    assert holes == 0, f"{label}: {holes} px of background enclosed inside the outline"


def check_png(img: Image.Image, min_w: int, label: str, outline_ink: str):
    a = np.asarray(img)[..., 3]
    assert img.width >= min_w, f"{label}: width {img.width} < {min_w}"
    border = np.concatenate([a[0], a[-1], a[:, 0], a[:, -1]])
    assert border.max() == 0, f"{label}: artwork touches the edge (border alpha max {border.max()})"
    # no fringe: every visible pixel that touches the transparent background must be
    # the outline ink (the outer edge is pure outline/shadow colour, never sky or fill)
    rgb = np.asarray(img)[..., :3].astype(int)
    clear = a == 0
    near = clear.copy()
    near[1:] |= clear[:-1]; near[:-1] |= clear[1:]
    near[:, 1:] |= clear[:, :-1]; near[:, :-1] |= clear[:, 1:]
    edge = near & (a >= 32)
    ink = np.array([int(outline_ink[i:i + 2], 16) for i in (1, 3, 5)])
    dist = np.abs(rgb[edge] - ink).max(axis=1)
    assert edge.sum() > 0 and dist.max() <= 24, \
        f"{label}: {int((dist > 24).sum())} edge pixels are not the outline colour (max diff {dist.max()})"


def flat_colour_count(img: Image.Image) -> int:
    a = np.asarray(img)
    opaque = a[a[..., 3] == 255][:, :3]
    cols, counts = np.unique(opaque.reshape(-1, 3), axis=0, return_counts=True)
    # ignore anti-alias blends: count colours covering >0.5% of opaque pixels
    return int((counts > 0.005 * len(opaque)).sum())


def fit_view(letters, margin=10):
    """Tighten VIEW to the artwork's bounds (+margin px), in whole pixels."""
    x, y, w, h = VIEW
    probe = png_image(render(build_layered(letters), w))
    l, t, r, b = probe.getbbox()
    VIEW[:] = [x + l - margin, y + t - margin, (r - l) + 2 * margin, (b - t) + 2 * margin]


def build(review=True, o_paw=O_PAW_DEFAULT):
    OUT.mkdir(parents=True, exist_ok=True)
    letters = build_letters(o_paw)
    if VIEW == VIEW0:
        fit_view(letters)
    layered = build_layered(letters)
    flat = build_flat(letters)
    (OUT / "doggiehood-title.svg").write_text(layered, encoding="utf-8")
    (OUT / "doggiehood-title-flat.svg").write_text(flat, encoding="utf-8")
    big = png_image(render(layered, BIG_W))
    preview = png_image(render(layered, PREVIEW_W))
    flat_big = png_image(render(flat, BIG_W))
    flat_preview = png_image(render(flat, PREVIEW_W))
    save_png(big, OUT / "doggiehood-title-6000.png")
    save_png(preview, OUT / "doggiehood-title-preview.png")
    save_png(flat_big, OUT / "doggiehood-title-flat-6000.png")
    save_png(flat_preview, OUT / "doggiehood-title-flat-preview.png")
    for img, label, ink in ((big, "6000 png", NAVY), (flat_big, "flat 6000 png", FLAT["navy"])):
        check_png(img, BIG_W, label, ink)
    n = flat_colour_count(flat_big)
    assert n <= 4, f"flat version uses {n} colours"
    check_no_holes(layered, "layered")
    check_no_holes(flat, "flat")
    hl_px = check_highlights_contained(letters)
    gap = check_i_gap(letters)
    if review:
        build_review(layered, flat_preview, preview)
        i_dot_review(layered)
        o_paw_variant_review()
    return big, flat_big, dict(flat_colours=n, highlight_px=hl_px, i_gap=gap)


def digest(paths):
    h = {}
    for p in sorted(paths):
        h[p.relative_to(HERE).as_posix()] = hashlib.sha256(p.read_bytes()).hexdigest()
    return h


def main():
    ap = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    ap.add_argument("--no-review", action="store_true", help="skip the review/ comparison images")
    ap.add_argument("--no-repeat-check", action="store_true", help="skip the rebuild-is-identical check")
    ap.add_argument("--o-paw", choices=sorted(O_PAW_VARIANTS), default=O_PAW_DEFAULT,
                    help="paw-in-the-O option to export (default %(default)s; see O_PAW_VARIANTS)")
    args = ap.parse_args()
    big, flat_big, info = build(review=not args.no_review, o_paw=args.o_paw)
    outputs = list(OUT.glob("*")) + (list(REVIEW.glob("*")) if not args.no_review else [])
    first = digest(outputs)
    if not args.no_repeat_check:
        build(review=not args.no_review, o_paw=args.o_paw)
        second = digest(outputs)
        assert first == second, "rebuild is not byte-identical: " + str(
            [k for k in first if first[k] != second.get(k)])
    print(f"ok: {BIG_W}px export {big.width}x{big.height}, flat {flat_big.width}x{flat_big.height}, "
          f"transparent border, no fringe, no enclosed holes, flat colours {info['flat_colours']} (<= 4), "
          f"10 highlights inside the fill ({info['highlight_px']} px checked), "
          f"i-dot gap {info['i_gap']:.1f}px navy (>= {PAW_I_GAP - 1:g}), o-paw {args.o_paw}, "
          f"{len(first)} files reproducible")
    return 0


if __name__ == "__main__":
    sys.exit(main())
