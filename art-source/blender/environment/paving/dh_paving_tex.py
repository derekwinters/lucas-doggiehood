"""Procedural paving textures (numpy + PIL) for #803 (P1 approved, #851):
concrete slabs with joints, pavers and flagstone. Same recipe as the lawn
and road textures: tileable or slot-padded by construction, tangent-space
normal maps from height, no external images, fixed seeds, so every run
writes the same PNGs into this set's own textures/.

CONCRETE PANEL ATLAS (one per option): ATLAS_W x ATLAS_H, ATLAS_COLS x
ATLAS_ROWS slots of SLOT px (a CELL px panel + PAD px of padding). Every slot
is ONE whole concrete slab: the mesh maps each slab (sidewalk panel, walkway
panel, driveway panel) onto one slot, so the JOINT is drawn on the slot's
border (half a joint on each side; two neighbouring slabs make one joint).
That keeps joints in the texture, where mipmapping fades them smoothly with
distance, instead of as sub-pixel geometry that would shimmer. The 8 slots
are 8 light variants of the same slab (tone, mottling, a stain or hairline
crack on a few), picked per slab by a hash of its world position, so no
pattern repeats. Padding repeats the slot's border (the joint colour), so
coarse mips bleed only joint colour.

  finish   broom   fine streaks across the walking direction (U)
           smooth  steel-trowelled, soft swirl mottling
           exposed light exposed aggregate (small pebbles)
  joint    tooled  a rounded groove with a smooth edger margin either side
           sawn    a thin straight cut, no margin

WORLD-TILED (one each, used only by the option that needs it):
  paving_pavers     PAVER_SIZE^2, PAVER_M per period: charcoal-grey running
                    bond pavers 0.50 x 0.25 m with sand joints
  paving_flagstone  FLAG_SIZE^2, FLAG_M per period: irregular warm flagstones
                    (wrapped Voronoi) in mortar
"""
import os
import random
import sys

import numpy as np
from PIL import Image, ImageDraw

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.normpath(os.path.join(HERE, "..", "trees")))
sys.path.insert(0, os.path.normpath(os.path.join(HERE, "..", "street-props")))
sys.path.insert(0, os.path.normpath(os.path.join(HERE, "..", "roads")))
import dh_tree_tex as T  # noqa: E402
import dh_prop_tex as PT  # noqa: E402
import dh_road_tex as RT  # noqa: E402  (wrap blur and band-limited noise)

TEX_DIR = os.path.join(HERE, "textures")

CELL, PAD = 240, 8
SLOT = CELL + 2 * PAD
ATLAS_COLS, ATLAS_ROWS = 4, 2
ATLAS_W, ATLAS_H = ATLAS_COLS * SLOT, ATLAS_ROWS * SLOT       # 1024 x 512
N_VARIANTS = ATLAS_COLS * ATLAS_ROWS
PANEL_M = 2.0                  # the slab size the cell is drawn for (a 2 m sidewalk)
PAVER_SIZE, PAVER_M = 512, 2.0
FLAG_SIZE, FLAG_M = 512, 3.0
SS = 2

CONCRETES = {
    "classic": {"tone": "#D6D2C8", "finish": "broom", "joint": "tooled", "seed": 841},
    "sawn": {"tone": "#C3C2BD", "finish": "smooth", "joint": "sawn", "seed": 843},
    "warm": {"tone": "#D5CAB4", "finish": "exposed", "joint": "tooled", "seed": 845},
}


def _rgb(h):
    return np.array(T._hex(h), np.float32)


def _save(name, col, hgt, strength):
    os.makedirs(TEX_DIR, exist_ok=True)
    c = os.path.join(TEX_DIR, "%s_color.png" % name)
    n = os.path.join(TEX_DIR, "%s_normal.png" % name)
    Image.fromarray(np.clip(col, 0, 255).astype(np.uint8)).save(c)
    PT._normal_from_height(hgt, strength).save(n)
    return c, n


def _joint_profile(d_px, style):
    """(colour multiplier, height) at distance d_px from the slab edge."""
    px_m = CELL / PANEL_M
    if style == "tooled":
        g = 0.016 * px_m          # half of a ~3 cm tooled groove (reads at game distance)
        m = 0.045 * px_m          # edger margin, ~4.5 cm
        groove = np.clip(1.0 - d_px / max(g, 0.5), 0, 1)
        margin = ((d_px > g) & (d_px < g + m)).astype(np.float32)
        lip = np.exp(-((d_px - (g + m)) / 0.8) ** 2)
        col = 1.0 - 0.50 * groove + 0.04 * margin - 0.05 * lip
        h = -1.0 * groove + 0.15 * margin
        return col, h, margin
    g = 0.009 * px_m              # sawn: ~1.8 cm visual cut, half per side
    groove = np.clip(1.0 - d_px / max(g, 0.5), 0, 1)
    return 1.0 - 0.42 * groove, -0.8 * groove, np.zeros_like(d_px)


def _panel(C, k, rng, r):
    """One slab, CELL x CELL, variant k: (rgb, height)."""
    S = CELL
    tone = _rgb(C["tone"]) * (1.0 + r.uniform(-0.025, 0.025))
    fine = RT._wrap_gauss(r.standard_normal((S, S)), 0.7)
    fine /= np.abs(fine).max()
    mott = RT._highpass_noise(S, rng, 2, 10)
    v = 1.0 + 0.035 * mott + 0.03 * fine
    h = 0.15 * fine
    if C["finish"] == "broom":
        streak = RT._wrap_gauss(r.standard_normal((S, S)), 0.6)
        streak = np.repeat(streak.mean(axis=1, keepdims=True), S, axis=1) * 0.5 + \
            RT._wrap_gauss(r.standard_normal((S, S)), 0.6) * 0.15
        streak = streak / (np.abs(streak).max() + 1e-9)
        v += 0.06 * streak
        h += 0.5 * streak
    elif C["finish"] == "smooth":
        swirl = RT._highpass_noise(S, rng, 6, 20)
        v += 0.025 * swirl
        h += 0.1 * swirl
    else:                                                     # exposed aggregate
        v = v[..., None] * np.ones(3)[None, None, :]
        for frac, hue in ((0.035, (0.80, 0.77, 0.72)), (0.025, (1.07, 1.04, 0.98)), (0.012, (0.66, 0.60, 0.52))):
            peb = (r.random((S, S)) < frac).astype(np.float32)
            peb = np.clip(RT._wrap_gauss(peb, 0.7) * 3.0, 0, 1)
            v = v * (1 - 0.8 * peb[..., None]) + 0.8 * peb[..., None] * np.array(hue)[None, None, :]
            h += 0.6 * peb
    if np.ndim(v) == 2:
        v = v[..., None] * np.ones(3)[None, None, :]
    # a soft stain on some variants, a hairline crack on one
    if k in (2, 5):
        yy, xx = np.mgrid[0:S, 0:S]
        cx, cy, rad = r.uniform(0.25, 0.75) * S, r.uniform(0.25, 0.75) * S, r.uniform(0.12, 0.25) * S
        st = np.exp(-(((xx - cx) ** 2 + (yy - cy) ** 2) / rad ** 2) ** 1.5)
        v *= (1.0 - 0.06 * st)[..., None]
    if k == 6:
        im = Image.new("L", (S * SS, S * SS), 0)
        d = ImageDraw.Draw(im)
        x, y = r.uniform(0.2, 0.4) * S * SS, 0.0
        pts = [(x, y)]
        while y < S * SS:
            y += r.uniform(8, 20)
            x += r.uniform(-6, 6)
            pts.append((x, y))
        d.line(pts, fill=255, width=2)
        cr = np.asarray(im.resize((S, S), Image.LANCZOS), np.float32) / 255.0
        v *= (1.0 - 0.35 * cr)[..., None]
        h -= 0.6 * cr
    # joints on all four edges (distance to the nearest edge, in px)
    yy, xx = np.mgrid[0:S, 0:S].astype(np.float32) + 0.5
    d = np.minimum(np.minimum(xx, S - xx), np.minimum(yy, S - yy))
    jc, jh, margin = _joint_profile(d, C["joint"])
    if C["joint"] == "tooled":
        smooth = RT._wrap_gauss(v[..., 0], 2.0)[..., None] * np.ones(3)[None, None, :]
        v = v * (1 - margin[..., None]) + smooth * margin[..., None]
        h = h * (1 - margin) + 0.1 * margin
    v = v * jc[..., None]
    h = h + jh
    return tone[None, None, :] * v, h


def concrete_atlas(key):
    C = CONCRETES[key]
    rng = random.Random(C["seed"])
    r = np.random.default_rng(C["seed"])
    col = np.zeros((ATLAS_H, ATLAS_W, 3), np.float32)
    hgt = np.zeros((ATLAS_H, ATLAS_W), np.float32)
    for k in range(N_VARIANTS):
        c, h = _panel(C, k, rng, r)
        row, cc = divmod(k, ATLAS_COLS)
        y0, x0 = row * SLOT, cc * SLOT
        col[y0:y0 + SLOT, x0:x0 + SLOT] = np.pad(c, ((PAD, PAD), (PAD, PAD), (0, 0)), mode="edge")
        hgt[y0:y0 + SLOT, x0:x0 + SLOT] = np.pad(h, PAD, mode="edge")
    return _save("paving_concrete_%s" % key, col, hgt, 2.5)


def slot_uv(k, u, v):
    """Atlas UV (V up) for slot k at slab-local (u, v) in 0..1."""
    row, c = divmod(k, ATLAS_COLS)
    x = (c * SLOT + PAD + u * CELL) / ATLAS_W
    y = 1.0 - (row * SLOT + PAD + (1.0 - v) * CELL) / ATLAS_H
    return (x, y)


def pavers():
    """Running-bond pavers, 0.50 x 0.25 m, tileable at PAVER_M."""
    S = PAVER_SIZE
    px = S / PAVER_M
    r = np.random.default_rng(851)
    rng = random.Random(851)
    W, H = 0.50 * px, 0.25 * px
    yy, xx = np.mgrid[0:S, 0:S].astype(np.float32) + 0.5
    row = np.floor(yy / H)
    xo = xx + (row % 2) * W / 2
    colx = np.floor(xo / W)
    ncol = int(round(S / W))
    colx = colx % ncol
    du = np.minimum(xo % W, W - xo % W)
    dv = np.minimum(yy % H, H - yy % H)
    d = np.minimum(du, dv)
    base = _rgb("#5E5F62")
    ids = (row * 97 + colx * 31).astype(np.int64)
    tones = r.uniform(0.86, 1.12, 4096)
    t = tones[ids % 4096]
    fine = RT._wrap_gauss(r.standard_normal((S, S)), 0.7)
    fine /= np.abs(fine).max()
    mott = RT._highpass_noise(S, rng, 8, 40)
    joint = np.clip(1.0 - (d - 1.0) / 1.2, 0, 1)
    bevel = np.clip(1.0 - (d - 1.0) / 3.0, 0, 1)
    v = t * (1 + 0.04 * fine + 0.03 * mott) * (1 - 0.12 * bevel)
    col = base[None, None, :] * v[..., None]
    sand = _rgb("#A8A08E")
    col = col * (1 - joint[..., None]) + sand[None, None, :] * (0.85 + 0.1 * fine[..., None]) * joint[..., None]
    h = 0.2 * fine - 1.0 * joint - 0.4 * bevel
    return _save("paving_pavers", col, h, 3.0)


def flagstone():
    """Irregular flagstones (Voronoi on a wrapped jittered grid) in mortar."""
    S = FLAG_SIZE
    px = S / FLAG_M
    r = np.random.default_rng(853)
    rng = random.Random(853)
    n = 9                                         # 9 x 9 stones per 3 m: ~0.33 m stones
    pts = [((i + r.uniform(0.15, 0.85)) * S / n, (j + r.uniform(0.15, 0.85)) * S / n)
           for j in range(n) for i in range(n)]
    pts = np.array(pts, np.float32)
    yy, xx = np.mgrid[0:S, 0:S].astype(np.float32) + 0.5
    d1 = np.full((S, S), 1e9, np.float32)
    d2 = np.full((S, S), 1e9, np.float32)
    idx = np.zeros((S, S), np.int64)
    for k, (px_, py_) in enumerate(pts):
        for ox in (-S, 0, S):
            for oy in (-S, 0, S):
                dd = np.hypot(xx - px_ - ox, yy - py_ - oy)
                closer = dd < d1
                d2 = np.where(closer, d1, np.minimum(d2, dd))
                idx = np.where(closer, k, idx)
                d1 = np.where(closer, dd, d1)
    edge = (d2 - d1) / 2.0                         # ~ distance to the cell border
    mortar_w = 0.022 * px
    mortar = np.clip(1.0 - (edge - mortar_w) / 1.2, 0, 1)
    pal = [_rgb(h) for h in ("#B8A587", "#A89A84", "#C2B394", "#9E8F78", "#B5AA97", "#A6927A")]
    stone = np.stack([pal[int(k) % len(pal)] for k in r.integers(0, 6 * 101, len(pts))])
    tone = r.uniform(0.92, 1.06, len(pts))
    fine = RT._wrap_gauss(r.standard_normal((S, S)), 0.8)
    fine /= np.abs(fine).max()
    mott = RT._highpass_noise(S, rng, 6, 30)
    col = stone[idx] * (tone[idx] * (1 + 0.05 * fine + 0.05 * mott))[..., None]
    mc = _rgb("#8E887C")
    col = col * (1 - mortar[..., None]) + mc[None, None, :] * (0.95 + 0.05 * fine[..., None]) * mortar[..., None]
    h = 0.3 * fine + 0.3 * mott - 1.0 * mortar - 0.3 * np.clip(1.0 - (edge - mortar_w) / 4.0, 0, 1)
    return _save("paving_flagstone", col, h, 3.0)


def paths():
    p = {"concrete_" + k: tuple(os.path.join(TEX_DIR, "paving_concrete_%s_%s.png" % (k, s)) for s in ("color", "normal"))
         for k in CONCRETES}
    for k in ("pavers", "flagstone"):
        p[k] = tuple(os.path.join(TEX_DIR, "paving_%s_%s.png" % (k, s)) for s in ("color", "normal"))
    return p


def _builders():
    b = {"concrete_" + k: (lambda k=k: concrete_atlas(k)) for k in CONCRETES}
    b.update({"pavers": pavers, "flagstone": flagstone})
    return b


def need(key):
    v = paths()[key]
    if not all(os.path.exists(f) for f in v):
        _builders()[key]()
    return v


def check():
    """Atlas padding equals each slot's border; world textures wrap."""
    bad = []
    for k in CONCRETES:
        if not os.path.exists(paths()["concrete_" + k][0]):
            continue
        a = np.asarray(Image.open(paths()["concrete_" + k][0]), np.int16)
        for s in range(N_VARIANTS):
            row, c = divmod(s, ATLAS_COLS)
            sl = a[row * SLOT:(row + 1) * SLOT, c * SLOT:(c + 1) * SLOT]
            if not (np.array_equal(sl[:PAD, PAD:-PAD], np.repeat(sl[PAD:PAD + 1, PAD:-PAD], PAD, 0)) and
                    np.array_equal(sl[PAD:-PAD, :PAD], np.repeat(sl[PAD:-PAD, PAD:PAD + 1], PAD, 1))):
                bad.append((k, s))
    for k in ("pavers", "flagstone"):
        if not os.path.exists(paths()[k][0]):
            continue
        a = np.asarray(Image.open(paths()[k][0]), np.float32).mean(axis=2)
        inner = np.abs(np.diff(a, axis=1)).mean()
        edge = np.abs(a[:, 0] - a[:, -1]).mean()
        if edge > 1.6 * inner + 1.0:
            bad.append((k, float(edge), float(inner)))
    return bad


KEEP = ("concrete_classic",)    # Derek's pick, P1 (#851); the rest build on demand (need) or with --all

if __name__ == "__main__":
    for k, f in _builders().items():
        if k in KEEP or "--all" in sys.argv:
            print(k, f())
    bad = check()
    print("padding + wrap OK" if not bad else "FAILED: %s" % bad)
    if bad:
        sys.exit(1)
