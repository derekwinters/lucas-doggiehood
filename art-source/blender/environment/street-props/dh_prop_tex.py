"""Procedural street-prop textures (numpy + PIL), same recipe as the tree and
landscaping textures: drawn at 2x, LANCZOS-downsampled, tileable surfaces
built from periodic (FFT) noise, normal maps derived from height.

Written straight into the shared textures/ folder (TEX_DIR) so the exported
.gltf files reference them as ../textures/<name>.png.

Tileable surfaces (512 x 512 colour + tangent-space normal). The colour maps
are light and near-neutral on purpose: each prop material multiplies one by
its own paint colour (exported as glTF baseColorFactor), so one texture
serves many colours.
  paint      powder-coat / enamel: orange-peel, faint mottling, fine scratches
  galv       galvanised steel: spangle crystals (sign backs, posts, poles)
  concrete   broom-finished concrete with pores and aggregate
  wood       slat timber, grain along V
  plastic    HDPE bin plastic, fine stipple

Label atlas (2048 x 2048 colour + normal, 4 x 4 cells of 512 px, opaque).
Each face sits inside its cell at the rect given in LABELS (cell-normalised,
top-left origin); the geometry maps its own outline onto that rect.
   0 stop            1 yield             2 speed limit 25     3 dogs-at-play diamond
   4 SLOW plaque     5 pedestrian xing   6 down-arrow plaque  7 dead end
   8 no parking      9 bus stop         10 dog-waste panel   11 recycling label
  12 newspaper front 13 manhole cover   14 storm-drain grate  15 road paint
"""
import math
import os
import random

import numpy as np
from PIL import Image, ImageDraw, ImageFont

import sys

HERE = os.path.dirname(os.path.abspath(__file__))
# dh_tree_tex (noise helpers) is the committed round-3 tree module in ../trees.
sys.path.insert(0, os.path.normpath(os.path.join(HERE, "..", "trees")))
import dh_tree_tex as T  # noqa: E402

TEX_DIR = os.path.join(HERE, "textures")   # the committed shared textures
SS = 2
TILE = 512
CELL = 512
GRID = 4
FONT = "/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf"
_hex = T._hex

# sign colours (MUTCD-like, slightly softened for the palette)
RED = "#C1272D"
WHITE = "#F4F3EE"
BLACK = "#1E1F22"
YELLOW = "#F2C21B"
YGREEN = "#C9DC2E"
BLUE = "#1F5BAA"
GREEN = "#1F5E3B"
NAVY = "#2D4E8C"
IRON = "#3A3B3D"

# cell -> (index, rect x0, y0, x1, y1) ; rect in cell units, top-left origin
DIAMOND = (0.04, 0.04, 0.96, 0.96)
LABELS = {
    "stop": (0, (0.04, 0.04, 0.96, 0.96)),
    "yield": (1, (0.04, 0.10, 0.96, 0.10 + 0.92 * 0.8660)),
    "speed25": (2, (0.132, 0.04, 0.868, 0.96)),
    "dogs": (3, DIAMOND),
    "slow_plaque": (4, (0.04, 0.155, 0.96, 0.845)),
    "ped": (5, DIAMOND),
    "arrow_plaque": (6, (0.04, 0.27, 0.96, 0.73)),
    "dead_end": (7, DIAMOND),
    "no_parking": (8, (0.193, 0.04, 0.807, 0.96)),
    "bus_stop": (9, (0.155, 0.04, 0.845, 0.96)),
    "waste": (10, (0.155, 0.04, 0.845, 0.96)),
    "recycle": (11, (0.04, 0.04, 0.96, 0.96)),
    "news": (12, (0.12, 0.04, 0.88, 0.96)),
    "manhole": (13, (0.02, 0.02, 0.98, 0.98)),
    "storm": (14, (0.20, 0.04, 0.80, 0.96)),
    "paint": (15, (0.04, 0.04, 0.96, 0.96)),
}


def label_uv(key, s, t):
    """(s, t) in [0,1] over the label's rect (s left->right, t bottom->top)
    -> atlas UV (V up)."""
    i, (x0, y0, x1, y1) = LABELS[key]
    col, row = i % GRID, i // GRID
    x = x0 + (x1 - x0) * s
    y = y1 - (y1 - y0) * t
    return ((col + x) / GRID, 1 - (row + y) / GRID)


# ---------------------------------------------------------------------------
# tileable surfaces
# ---------------------------------------------------------------------------
def _normal_from_height(hh, strength):
    hh = hh * strength
    gx = (np.roll(hh, -1, 1) - np.roll(hh, 1, 1)) / 2
    gy = (np.roll(hh, -1, 0) - np.roll(hh, 1, 0)) / 2
    n = np.dstack([-gx, gy, np.ones_like(hh)])
    n /= np.linalg.norm(n, axis=2, keepdims=True)
    return Image.fromarray(((n * 0.5 + 0.5) * 255).astype(np.uint8))


def _save_pair(name, col, hgt, strength):
    os.makedirs(TEX_DIR, exist_ok=True)
    c = os.path.join(TEX_DIR, "prop_%s_color.png" % name)
    n = os.path.join(TEX_DIR, "prop_%s_normal.png" % name)
    Image.fromarray(np.clip(col, 0, 255).astype(np.uint8)).save(c)
    _normal_from_height(hgt, strength).save(n)
    return c, n


def _grey(v):
    return np.dstack([v, v, v]) * 255.0


def paint(size=TILE, seed=41):
    rng = random.Random(seed)
    mott = T._periodic_noise((size, size), 3, 3, rng, octaves=3)
    peel = T._periodic_noise((size, size), 70, 70, rng, octaves=2)
    v = 0.90 + 0.05 * (mott - 0.5) + 0.025 * (peel - 0.5)
    h = peel * 1.0
    # faint hairline scratches (wrapped)
    r = np.random.default_rng(seed)
    scr = Image.new("L", (size * SS, size * SS), 0)
    d = ImageDraw.Draw(scr)
    W = size * SS
    for _ in range(40):
        x, y = r.random(2) * W
        a = r.random() * math.pi
        L = r.uniform(0.03, 0.12) * W
        for ox in (-W, 0, W):
            for oy in (-W, 0, W):
                d.line([(x + ox, y + oy), (x + ox + math.cos(a) * L, y + oy + math.sin(a) * L)], fill=255, width=2)
    s = np.asarray(scr.resize((size, size), Image.LANCZOS), np.float32) / 255
    v = v + 0.05 * s
    h = h - 0.6 * s
    return _save_pair("paint", _grey(np.clip(v, 0, 1)), h, 1.6)


def galv(size=TILE, seed=43):
    """Spangle: wrapped Voronoi crystals, each its own grey + facet slope."""
    r = np.random.default_rng(seed)
    n = 260
    pts = r.random((n, 2)) * size
    tone = r.uniform(0.72, 0.82, n)
    slope = r.uniform(-1, 1, (n, 2))
    yy, xx = np.mgrid[0:size, 0:size].astype(np.float32)
    best = np.full((size, size), 1e9, np.float32)
    idx = np.zeros((size, size), np.int32)
    for i, (px, py) in enumerate(pts):
        dx = np.abs(xx - px)
        dy = np.abs(yy - py)
        dx = np.minimum(dx, size - dx)
        dy = np.minimum(dy, size - dy)
        dd = dx * dx + dy * dy
        m = dd < best
        best[m] = dd[m]
        idx[m] = i
    v = tone[idx]
    rng = random.Random(seed)
    fine = T._periodic_noise((size, size), 90, 90, rng, octaves=2)
    v = v * (0.95 + 0.1 * fine)
    h = (slope[idx][..., 0] * xx + slope[idx][..., 1] * yy) / size * 0.6 + fine * 0.4
    col = _grey(np.clip(v, 0, 1)) * np.array([0.98, 1.0, 1.02])
    return _save_pair("galv", col, h, 1.2)


def concrete(size=TILE, seed=45):
    rng = random.Random(seed)
    big = T._periodic_noise((size, size), 4, 4, rng, octaves=3)
    broom = T._periodic_noise((size, size), 160, 3, rng, octaves=2)      # fine streaks along x
    agg = T._periodic_noise((size, size), 60, 60, rng, octaves=2)
    r = np.random.default_rng(seed)
    pores = r.random((size, size)) > 0.992
    v = 0.80 + 0.07 * (big - 0.5) + 0.04 * (broom - 0.5) + 0.05 * (agg - 0.5)
    v[pores] *= 0.7
    h = broom * 0.5 + agg * 0.5 - pores * 0.8
    col = _grey(np.clip(v, 0, 1)) * np.array([1.0, 0.99, 0.96])
    return _save_pair("concrete", col, h, 2.0)


def wood(size=TILE, seed=47):
    """Slat timber, grain along V (image y); light so a stain tint works."""
    rng = random.Random(seed)
    g = T._periodic_noise((size, size), 0.7, 40, rng, octaves=3)
    fine = T._periodic_noise((size, size), 2, 120, rng, octaves=2)
    knots = T._periodic_noise((size, size), 4, 4, rng, octaves=1)
    base, dark, light = (np.array(_hex(c), np.float32) for c in ("#D9B48C", "#A97B55", "#EDD0A8"))
    t = np.clip(g * 0.8 + fine * 0.35 - 0.1, 0, 1)
    col = dark + (light - dark) * t[..., None]
    col = col * 0.55 + base * 0.45
    k = np.clip((knots - 0.9) / 0.1, 0, 1)
    col = col * (1 - 0.35 * k[..., None])
    return _save_pair("wood", col, g * 0.5 + fine * 0.5 - k * 0.6, 2.2)


def plastic(size=TILE, seed=49):
    rng = random.Random(seed)
    st = T._periodic_noise((size, size), 110, 110, rng, octaves=2)
    mott = T._periodic_noise((size, size), 3, 3, rng, octaves=2)
    v = 0.88 + 0.03 * (st - 0.5) + 0.04 * (mott - 0.5)
    return _save_pair("plastic", _grey(np.clip(v, 0, 1)), st, 1.4)


# ---------------------------------------------------------------------------
# label atlas
# ---------------------------------------------------------------------------
class Cell:
    """Drawing context for one atlas cell at supersampled resolution, with a
    parallel height image. Coordinates are in rect units (0..1 over the
    label's rect, top-left origin)."""

    def __init__(self, key, bg):
        self.key = key
        self.S = CELL * SS
        self.col = Image.new("RGB", (self.S, self.S), _hex(bg))
        self.hgt = Image.new("L", (self.S, self.S), 128)
        self.dc = ImageDraw.Draw(self.col)
        self.dh = ImageDraw.Draw(self.hgt)
        _, self.r = LABELS[key]

    def p(self, x, y):
        x0, y0, x1, y1 = self.r
        return ((x0 + (x1 - x0) * x) * self.S, (y0 + (y1 - y0) * y) * self.S)

    def pts(self, ps):
        return [self.p(x, y) for x, y in ps]

    @property
    def w(self):
        return (self.r[2] - self.r[0]) * self.S

    @property
    def h(self):
        return (self.r[3] - self.r[1]) * self.S

    def poly(self, ps, fill, height=None):
        q = self.pts(ps)
        self.dc.polygon(q, fill=_hex(fill))
        if height is not None:
            self.dh.polygon(q, fill=height)

    def ellipse(self, cx, cy, rx, ry, fill, height=None, width=0, outline=None):
        a, b = self.p(cx - rx, cy - ry), self.p(cx + rx, cy + ry)
        if outline:
            self.dc.ellipse([a, b], outline=_hex(outline), width=int(width))
            if height is not None:
                self.dh.ellipse([a, b], outline=height, width=int(width))
        else:
            self.dc.ellipse([a, b], fill=_hex(fill))
            if height is not None:
                self.dh.ellipse([a, b], fill=height)

    def line(self, ps, fill, width, height=None, round_=True):
        q = self.pts(ps)
        wpx = int(width * self.w)
        self.dc.line(q, fill=_hex(fill), width=wpx, joint="curve")
        if height is not None:
            self.dh.line(q, fill=height, width=wpx, joint="curve")
        if round_:
            for x, y in (q[0], q[-1]):
                self.dc.ellipse([x - wpx / 2, y - wpx / 2, x + wpx / 2, y + wpx / 2], fill=_hex(fill))

    def text(self, s, cx, cy, height_frac, fill, squash=0.86, max_w=0.9):
        """Centred text; height_frac = cap height as a fraction of the rect
        height; condensed horizontally like highway lettering."""
        px = int(height_frac * self.h / 0.72)
        f = ImageFont.truetype(FONT, px)
        bb = f.getbbox(s)
        tw, th = bb[2] - bb[0], bb[3] - bb[1]
        im = Image.new("L", (tw + 8, th + 8), 0)
        ImageDraw.Draw(im).text((4 - bb[0], 4 - bb[1]), s, fill=255, font=f)
        nw = int(im.width * squash)
        if nw > max_w * self.w:
            nw = int(max_w * self.w)
        im = im.resize((max(1, nw), im.height), Image.LANCZOS)
        x, y = self.p(cx, cy)
        self.col.paste(Image.new("RGB", im.size, _hex(fill)), (int(x - im.width / 2), int(y - im.height / 2)), im)

    def done(self):
        return (self.col.resize((CELL, CELL), Image.LANCZOS), self.hgt.resize((CELL, CELL), Image.LANCZOS))


def _reg_poly(n, rot_deg, scale=1.0, cx=0.5, cy=0.5):
    return [(cx + 0.5 * scale * math.cos(math.radians(rot_deg + 360 * i / n)),
             cy - 0.5 * scale * math.sin(math.radians(rot_deg + 360 * i / n))) for i in range(n)]


def _octagon(scale):
    R = 0.5 * scale / math.cos(math.radians(22.5))
    return [(0.5 + R * math.cos(math.radians(22.5 + 45 * i)), 0.5 - R * math.sin(math.radians(22.5 + 45 * i)))
            for i in range(8)]


def _diamond(scale):
    a = 0.5 * scale
    return [(0.5, 0.5 - a), (0.5 + a, 0.5), (0.5, 0.5 + a), (0.5 - a, 0.5)]


def _rect(inset_x, inset_y=None):
    iy = inset_x if inset_y is None else inset_y
    return [(inset_x, iy), (1 - inset_x, iy), (1 - inset_x, 1 - iy), (inset_x, 1 - iy)]


def _rim(c, outline, h_edge=200):
    """Rolled outer edge of a sign blade, in the height map only."""
    q = c.pts(outline)
    c.dh.line(q + [q[0]], fill=h_edge, width=int(0.022 * c.w), joint="curve")


def _rounded(c, inset, radius, fill, height=None):
    a, b = c.p(inset, inset * c.w / c.h), c.p(1 - inset, 1 - inset * c.w / c.h)
    c.dc.rounded_rectangle([a, b], radius=int(radius * c.w), fill=_hex(fill))
    if height is not None:
        c.dh.rounded_rectangle([a, b], radius=int(radius * c.w), fill=height)


def _bordered_rect(c, field, border, b=0.035, inset=0.035, radius=0.06):
    """US regulatory/warning rect: field colour with a thin border line."""
    _rounded(c, 0.0, radius, field)
    _rounded(c, inset, radius * 0.8, border)
    _rounded(c, inset + b, radius * 0.6, field)
    _rim(c, _rect(0.0))


def _bordered_diamond(c, field, border):
    c.poly(_diamond(1.0), field)
    c.poly(_diamond(0.93), border)
    c.poly(_diamond(0.875), field)
    _rim(c, _diamond(1.0))


# --- icons ---------------------------------------------------------------
def _dog_icon(c, cx, cy, s, fill, flip=False, ball=True):
    """Side-view running dog (head up, tail up, ears flopping), in rect
    units; s = overall length. A ball ahead of it."""
    k = -1 if flip else 1

    def P(x, y):
        return (cx + k * x * s, cy + y * s * c.w / c.h)
    lw = 0.085 * s
    # body: chest deeper than the loin
    c.poly([P(-0.30, -0.10), P(-0.05, -0.13), P(0.18, -0.15), P(0.27, -0.10), P(0.28, 0.04), P(0.16, 0.10),
            P(-0.05, 0.06), P(-0.28, 0.05), P(-0.36, -0.02)], fill)
    c.ellipse(*P(0.18, -0.03), 0.12 * s, 0.13 * s * c.w / c.h, fill)
    c.ellipse(*P(-0.27, -0.03), 0.10 * s, 0.09 * s * c.w / c.h, fill)
    # neck + head + snout + nose + ear
    c.poly([P(0.16, -0.12), P(0.30, -0.30), P(0.40, -0.26), P(0.32, -0.04)], fill)
    c.ellipse(*P(0.37, -0.30), 0.095 * s, 0.085 * s * c.w / c.h, fill)
    c.poly([P(0.40, -0.36), P(0.56, -0.31), P(0.57, -0.24), P(0.42, -0.21)], fill)
    c.poly([P(0.30, -0.36), P(0.22, -0.30), P(0.20, -0.18), P(0.28, -0.24)], fill)
    # legs: front pair reaching forward, back pair pushing off
    c.line([P(0.22, 0.04), P(0.36, 0.20), P(0.48, 0.26)], fill, lw)
    c.line([P(0.14, 0.06), P(0.18, 0.24), P(0.28, 0.33)], fill, lw)
    c.line([P(-0.24, 0.02), P(-0.30, 0.20), P(-0.46, 0.27)], fill, lw)
    c.line([P(-0.32, 0.0), P(-0.44, 0.12), P(-0.56, 0.18)], fill, lw)
    # tail up
    c.line([P(-0.34, -0.06), P(-0.46, -0.20), P(-0.50, -0.32)], fill, 0.06 * s)
    if ball:
        c.ellipse(*P(0.66, 0.24), 0.065 * s, 0.065 * s * c.w / c.h, fill)


def _walker(c, cx, cy, s, fill):
    """Pedestrian (MUTCD W11-2 style), s = figure height."""
    def P(x, y):
        return (cx + x * s, cy + y * s)
    c.ellipse(*P(0.04, -0.40), 0.075 * s, 0.075 * s, fill)
    w = 0.10
    c.line([P(0.02, -0.29), P(-0.02, 0.02)], fill, w * s)                    # torso
    c.line([P(-0.02, 0.02), P(-0.17, 0.22), P(-0.25, 0.44)], fill, w * s)    # back leg
    c.line([P(-0.02, 0.02), P(0.12, 0.22), P(0.14, 0.44)], fill, w * s)      # front leg
    c.line([P(0.14, 0.44), P(0.21, 0.44)], fill, 0.07 * s)
    c.line([P(0.02, -0.24), P(-0.16, -0.08), P(-0.22, 0.06)], fill, 0.075 * s)  # arms
    c.line([P(0.02, -0.24), P(0.16, -0.10), P(0.24, 0.0)], fill, 0.075 * s)


def _arrow_down_left(c, fill):
    c.line([(0.62, 0.30), (0.40, 0.66)], fill, 0.07)
    c.poly([(0.28, 0.86), (0.31, 0.52), (0.52, 0.66)], fill)


def _bus(c, cx, cy, s, fill, ink):
    def P(x, y):
        return c.p(cx + x * s, cy + y * s * c.w / c.h)
    a, b = P(-0.42, -0.30), P(0.42, 0.26)
    c.dc.rounded_rectangle([a, b], radius=int(0.08 * s * c.w), fill=_hex(fill))
    for i in range(3):                                   # side windows
        x0 = -0.36 + i * 0.22
        c.dc.rounded_rectangle([P(x0, -0.22), P(x0 + 0.17, -0.04)], radius=int(0.02 * s * c.w), fill=_hex(ink))
    c.dc.rounded_rectangle([P(0.30, -0.22), P(0.38, 0.08)], radius=int(0.02 * s * c.w), fill=_hex(ink))
    for x in (-0.24, 0.22):                              # wheels
        q = P(x, 0.27)
        r = 0.085 * s * c.w
        c.dc.ellipse([q[0] - r, q[1] - r, q[0] + r, q[1] + r], fill=_hex(ink))
        c.dc.ellipse([q[0] - r * 0.45, q[1] - r * 0.45, q[0] + r * 0.45, q[1] + r * 0.45], fill=_hex(fill))


def _chasing_arrows(c, cx, cy, R, fill):
    """Three bent arrows round a triangle (recycling mark)."""
    for k in range(3):
        a0 = math.radians(90 + 120 * k + 18)
        a1 = math.radians(90 + 120 * k + 102)
        p0 = (cx + R * math.cos(a0), cy - R * math.sin(a0) * c.w / c.h)
        p1 = (cx + R * math.cos(a1), cy - R * math.sin(a1) * c.w / c.h)
        c.line([p0, p1], fill, 0.075)
        d = (p1[0] - p0[0], (p1[1] - p0[1]) * c.h / c.w)
        L = math.hypot(*d)
        ux, uy = d[0] / L, d[1] / L
        nx, ny = -uy, ux
        hl, hw = 0.11, 0.08
        tip = (p1[0] + ux * hl * 0.6, p1[1] + uy * hl * 0.6 * c.w / c.h)
        base = (p1[0] - ux * hl * 0.4, p1[1] - uy * hl * 0.4 * c.w / c.h)
        c.poly([tip, (base[0] + nx * hw, base[1] + ny * hw * c.w / c.h),
                (base[0] - nx * hw, base[1] - ny * hw * c.w / c.h)], fill)


def _bag(c, cx, cy, s, fill):
    c.poly([(cx - 0.5 * s, cy - 0.2 * s), (cx + 0.5 * s, cy - 0.2 * s), (cx + 0.42 * s, cy + 0.6 * s),
            (cx - 0.42 * s, cy + 0.6 * s)], fill)
    c.line([(cx - 0.3 * s, cy - 0.2 * s), (cx - 0.18 * s, cy - 0.55 * s), (cx, cy - 0.2 * s)], fill, 0.025)
    c.line([(cx + 0.3 * s, cy - 0.2 * s), (cx + 0.18 * s, cy - 0.55 * s), (cx, cy - 0.2 * s)], fill, 0.025)


# --- faces ---------------------------------------------------------------
def face_stop():
    c = Cell("stop", RED)
    c.poly(_octagon(1.0), RED)
    c.poly(_octagon(0.935), WHITE)
    c.poly(_octagon(0.89), RED)
    _rim(c, _octagon(1.0))
    c.text("STOP", 0.5, 0.5, 0.30, WHITE, squash=0.9, max_w=0.78)
    return c.done()


def face_yield():
    c = Cell("yield", RED)
    tri = [(0.0, 0.0), (1.0, 0.0), (0.5, 1.0)]
    c.poly(tri, RED)
    # inner white triangle, wide red border (~ 1/6 of the width)
    cxg, cyg = 0.5, 1 / 3
    inner = [(cxg + (x - cxg) * 0.66, cyg + (y - cyg) * 0.66) for x, y in tri]
    c.poly(inner, WHITE)
    _rim(c, tri)
    c.text("YIELD", 0.5, 0.25, 0.13, RED, squash=0.88, max_w=0.5)
    return c.done()


def face_speed25():
    c = Cell("speed25", WHITE)
    _bordered_rect(c, WHITE, BLACK, b=0.03, inset=0.035, radius=0.07)
    c.text("SPEED", 0.5, 0.17, 0.115, BLACK)
    c.text("LIMIT", 0.5, 0.34, 0.115, BLACK)
    c.text("25", 0.5, 0.66, 0.36, BLACK, squash=0.82)
    return c.done()


def face_dogs():
    c = Cell("dogs", YELLOW)
    _bordered_diamond(c, YELLOW, BLACK)
    _dog_icon(c, 0.47, 0.52, 0.46, BLACK)
    return c.done()


def face_slow_plaque():
    c = Cell("slow_plaque", YELLOW)
    _bordered_rect(c, YELLOW, BLACK, b=0.03, inset=0.03, radius=0.06)
    c.text("SLOW", 0.5, 0.32, 0.27, BLACK)
    c.text("DOGS AT PLAY", 0.5, 0.71, 0.17, BLACK, squash=0.8, max_w=0.86)
    return c.done()


def face_ped():
    c = Cell("ped", YGREEN)
    _bordered_diamond(c, YGREEN, BLACK)
    _walker(c, 0.5, 0.52, 0.56, BLACK)
    return c.done()


def face_arrow_plaque():
    c = Cell("arrow_plaque", YGREEN)
    _bordered_rect(c, YGREEN, BLACK, b=0.03, inset=0.03, radius=0.05)
    _arrow_down_left(c, BLACK)
    return c.done()


def face_dead_end():
    c = Cell("dead_end", YELLOW)
    _bordered_diamond(c, YELLOW, BLACK)
    c.text("DEAD", 0.5, 0.40, 0.13, BLACK, max_w=0.5)
    c.text("END", 0.5, 0.60, 0.13, BLACK, max_w=0.5)
    return c.done()


def face_no_parking():
    c = Cell("no_parking", WHITE)
    _bordered_rect(c, WHITE, RED, b=0.03, inset=0.04, radius=0.08)
    c.text("NO", 0.5, 0.13, 0.08, RED)
    c.text("PARKING", 0.5, 0.25, 0.075, RED, squash=0.8)
    cx, cy, R = 0.5, 0.60, 0.33
    ry = R * c.w / c.h
    c.text("P", cx, cy + 0.005, 0.32, BLACK, squash=1.0)
    c.ellipse(cx, cy, R, ry, None, width=0.075 * c.w, outline=RED)
    d = R * 0.707
    c.line([(cx - d, cy - d * c.w / c.h), (cx + d, cy + d * c.w / c.h)], RED, 0.075, round_=False)
    return c.done()


def face_bus_stop():
    c = Cell("bus_stop", BLUE)
    _rounded(c, 0.0, 0.07, BLUE)
    _rounded(c, 0.035, 0.06, WHITE)
    _rounded(c, 0.06, 0.05, BLUE)
    _rim(c, _rect(0.0))
    _bus(c, 0.5, 0.36, 0.72, WHITE, BLUE)
    c.text("BUS", 0.5, 0.66, 0.11, WHITE)
    c.text("STOP", 0.5, 0.82, 0.11, WHITE)
    return c.done()


def face_waste():
    c = Cell("waste", GREEN)
    _rounded(c, 0.0, 0.05, GREEN, 150)
    _rounded(c, 0.04, 0.04, "#2B7A4E", 140)
    _dog_icon(c, 0.40, 0.31, 0.62, WHITE, ball=False)
    _bag(c, 0.82, 0.31, 0.12, WHITE)
    c.text("PLEASE", 0.5, 0.58, 0.07, WHITE)
    c.text("CLEAN UP", 0.5, 0.69, 0.07, WHITE)
    c.text("AFTER", 0.5, 0.80, 0.07, WHITE)
    c.text("YOUR DOG", 0.5, 0.91, 0.07, WHITE)
    return c.done()


def face_recycle():
    c = Cell("recycle", "#2D6FC2")
    _rounded(c, 0.03, 0.08, "#2A66B4", 140)
    _chasing_arrows(c, 0.5, 0.40, 0.25, WHITE)
    c.text("RECYCLE", 0.5, 0.82, 0.12, WHITE, squash=0.85)
    return c.done()


def face_news():
    """Newspaper box front: header, window with a folded paper behind,
    coin-slot plate and pull handle. Body paint is navy."""
    c = Cell("news", NAVY)
    c.poly(_rect(0.0), NAVY, 128)
    c.poly([(0.0, 0.0), (1.0, 0.0), (1.0, 0.16), (0.0, 0.16)], "#C8312C", 140)
    c.text("NEWS", 0.5, 0.08, 0.09, WHITE)
    # window frame + glass
    c.poly([(0.07, 0.21), (0.93, 0.21), (0.93, 0.67), (0.07, 0.67)], "#C9CCD0", 150)
    c.poly([(0.11, 0.24), (0.89, 0.24), (0.89, 0.64), (0.11, 0.64)], "#3E4A56", 95)
    # paper inside (seen through the glass, darkened)
    c.poly([(0.16, 0.30), (0.84, 0.28), (0.85, 0.62), (0.15, 0.62)], "#B9B6AA")
    c.poly([(0.16, 0.30), (0.84, 0.28), (0.84, 0.34), (0.16, 0.36)], "#5A5650")
    for i in range(6):
        y = 0.40 + i * 0.035
        c.line([(0.20, y), (0.48 if i % 2 else 0.52, y)], "#7D7A72", 0.012, round_=False)
        c.line([(0.56, y), (0.80, y)], "#7D7A72", 0.012, round_=False)
    c.poly([(0.11, 0.24), (0.40, 0.24), (0.20, 0.64), (0.11, 0.64)], "#56626E")   # glare band
    # coin plate + handle
    c.poly([(0.58, 0.72), (0.88, 0.72), (0.88, 0.90), (0.58, 0.90)], "#B7BBC0", 165)
    c.poly([(0.70, 0.76), (0.76, 0.76), (0.76, 0.84), (0.70, 0.84)], "#2A2C30", 100)
    c.poly([(0.14, 0.72), (0.46, 0.72), (0.46, 0.77), (0.14, 0.77)], "#9EA3A8", 175)
    return c.done()


def face_manhole():
    c = Cell("manhole", "#4A4B4C")
    c.ellipse(0.5, 0.5, 0.5, 0.5, "#6A6B6D", 150)
    c.ellipse(0.5, 0.5, 0.46, 0.46, "#545557", 120)
    # raised waffle grid inside a ring
    S = c.S
    grid = Image.new("L", (S, S), 0)
    gd = ImageDraw.Draw(grid)
    step = int(0.055 * c.w)
    for k in range(-20, 21):
        x = S / 2 + k * step
        gd.line([(x, 0), (x, S)], fill=255, width=int(step * 0.32))
        gd.line([(0, x), (S, x)], fill=255, width=int(step * 0.32))
    mask = Image.new("L", (S, S), 0)
    a, b = c.p(0.12, 0.12), c.p(0.88, 0.88)
    ImageDraw.Draw(mask).ellipse([a, b], fill=255)
    inner = Image.new("L", (S, S), 0)
    a2, b2 = c.p(0.30, 0.38), c.p(0.70, 0.62)
    ImageDraw.Draw(inner).rounded_rectangle([a2, b2], radius=int(0.05 * c.w), fill=255)
    m = np.minimum(np.asarray(grid), np.asarray(mask)) * (1 - np.asarray(inner) / 255.0)
    m = Image.fromarray(m.astype(np.uint8))
    c.col.paste(Image.new("RGB", (S, S), _hex("#77787A")), (0, 0), m)
    c.hgt.paste(Image.new("L", (S, S), 175), (0, 0), m)
    c.ellipse(0.5, 0.5, 0.40, 0.40, None, 165, width=0.025 * c.w, outline="#5E5F61")
    c.text("SEWER", 0.5, 0.5, 0.085, "#666769", squash=1.0)
    hm = Image.new("L", (S, S), 0)
    ImageDraw.Draw(hm).rounded_rectangle([a2, b2], radius=int(0.05 * c.w), fill=255)
    c.hgt.paste(Image.new("L", (S, S), 150), (0, 0), hm)
    # pick holes
    for x in (0.2, 0.8):
        c.ellipse(x, 0.5, 0.022, 0.022, "#1E1E1F", 60)
    return c.done()


def face_storm():
    c = Cell("storm", "#3A3B3C")
    c.poly(_rect(0.0), "#47484A", 150)
    c.poly(_rect(0.06, 0.04), "#151617", 60)
    n = 9
    for i in range(n):
        x0 = 0.06 + (0.88 / n) * i + 0.025
        c.poly([(x0, 0.04), (x0 + 0.88 / n - 0.05, 0.04), (x0 + 0.88 / n - 0.05, 0.96), (x0, 0.96)], "#505153", 170)
    for y in (0.34, 0.66):
        c.poly([(0.06, y - 0.02), (0.94, y - 0.02), (0.94, y + 0.02), (0.06, y + 0.02)], "#505153", 170)
    return c.done()


def face_paint():
    """Worn white road paint: asphalt speckle shows through."""
    c = Cell("paint", "#E9E7E0")
    r = np.random.default_rng(51)
    col = np.asarray(c.col).astype(np.float32)
    rng = random.Random(51)
    wear = T._periodic_noise((c.S, c.S), 6, 6, rng, octaves=3)
    sp = r.random((c.S, c.S))
    m = (sp < 0.04 + 0.25 * np.clip((wear - 0.62) / 0.38, 0, 1))
    col[m] = _hex("#6E6E6C")
    col *= (0.95 + 0.05 * wear)[..., None]
    c.col = Image.fromarray(col.clip(0, 255).astype(np.uint8))
    c.hgt = Image.fromarray((128 + 30 * (~m) - 10).astype(np.uint8))
    return c.done()


FACES = [face_stop, face_yield, face_speed25, face_dogs, face_slow_plaque, face_ped, face_arrow_plaque,
         face_dead_end, face_no_parking, face_bus_stop, face_waste, face_recycle, face_news, face_manhole,
         face_storm, face_paint]


def label_atlas():
    rng = random.Random(53)
    A = CELL * GRID
    col = Image.new("RGB", (A, A))
    hgt = Image.new("L", (A, A))
    for i, fn in enumerate(FACES):
        c, h = fn()
        col.paste(c, ((i % GRID) * CELL, (i // GRID) * CELL))
        hgt.paste(h, ((i % GRID) * CELL, (i // GRID) * CELL))
    # retroreflective sheeting micro-texture over everything
    sheet = T._periodic_noise((A, A), 400, 400, rng, octaves=1)
    hh = np.asarray(hgt, np.float32) / 255.0 + 0.02 * sheet
    os.makedirs(TEX_DIR, exist_ok=True)
    pc = os.path.join(TEX_DIR, "prop_labels_color.png")
    pn = os.path.join(TEX_DIR, "prop_labels_normal.png")
    col.save(pc)
    _normal_from_height(hh, 6.0).save(pn)
    return pc, pn


def build_all():
    out = {"paint": paint(), "galv": galv(), "concrete": concrete(), "wood": wood(), "plastic": plastic(),
           "labels": label_atlas()}
    return out


def paths():
    """Texture paths, generating them on first use."""
    keys = ("paint", "galv", "concrete", "wood", "plastic", "labels")
    p = {k: (os.path.join(TEX_DIR, "prop_%s_color.png" % k), os.path.join(TEX_DIR, "prop_%s_normal.png" % k))
         for k in keys}
    if not all(os.path.exists(a) and os.path.exists(b) for a, b in p.values()):
        return build_all()
    return p


if __name__ == "__main__":
    for k, v in build_all().items():
        print(k, v)
