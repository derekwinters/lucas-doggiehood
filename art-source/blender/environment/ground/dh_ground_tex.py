"""Procedural lawn and lot-ground textures (numpy + PIL) for the #801
CONCEPT round. Same recipe as the tree, street-prop and fence textures:
tileable by construction (periodic FFT noise, strokes drawn with
wrap-around, wrap-around blur), tangent-space normal maps derived from
height, no external images, fixed seeds so every run writes the same PNGs.
Written into this set's own textures/ folder (TEX_DIR), each stored once.

The lawn is sampled in WORLD space (UV = world position / period), so the
texture is continuous across every 60 m map tile and lot; nothing here is a
per-tile 0..1 texture.

Three lawn looks (LAWNS). What reads from the game camera is detail at
about 5-30 cm (a single blade is far below a screen pixel), so the looks
differ at that scale:

  lawn_a  "Natural turf"   realistic mid green, small clumps, fine blades,
                           faint clover and dry patches. 4 m period.
  lawn_b  "Soft painted"   brighter stylized green near today's
                           Palette.GrassHex (#7ED957), big soft patches and
                           soft dabs, little fine detail. 8 m period.
  lawn_c  "Crisp tufts"    deeper green, crisp light tufts (2-3 blades
                           fanning), the stylized cozy-game look. 3 m period.

Per look, texture sets for the ways of hiding repeats (ATLASES):

  lawn_<x>_{color,normal}           1024^2, one period. Plain tiling, and
                                    the base for approach (a), macro.
  lawn_<x>_wang_{color,normal}      approach (b): 2048^2 atlas, 2 x 2 slots
                                    of 1024 (960 px cell + 32 px padding):
                                    the base plus 3 full-strength
                                    SHARED-EDGE variants, one period each.
  lawn_<x>_cells_{color,normal}     approach (d), Derek's idea: 2048^2
                                    atlas, 4 x 4 slots of 512 (448 + 32):
                                    16 LIGHTLY textured shared-edge variants
                                    (contrast CELL_CONTRAST), each one 3 m
                                    world cell.
  lawn_<x>_cells1k_{color,normal}   the same 16 at 1024^2 (224 + 16).

Shared-edge: every variant equals the base byte for byte in a band along
all four edges (check_atlas() verifies it), so ANY variant can sit next to
ANY other on every side with no seam. The padding around each atlas slot is
the slot's own wrap-around continuation, which (because of the shared band)
is identical for every slot; so even mip levels coarse enough to bleed past
the padding pull in the same colours the cell would have continued with.

Shared:
  ground_macro      256^2 RGB, period MACRO_M = 211 m, very low frequency
                    (19-53 m blobs). Stored as 0.5 x a tint multiplier,
                    the convention of URP Lit's Detail Albedo map (which
                    multiplies by 2 x detail), so it plugs into the stock
                    material on UV1. Mid-grey = no change.
  ground_fringe     512 x 128 RGBA grass fringe (alpha = mask), tinted per
                    lawn. For the "grass fringe" edge variant.
  ground_mulch      512^2 bark-mulch chips, 1 m period.
  ground_concrete   512^2 broom-finished concrete, near-white, tinted per
                    surface.
"""
import math
import os
import random
import sys

import numpy as np
from PIL import Image, ImageDraw, ImageFilter

HERE = os.path.dirname(os.path.abspath(__file__))
# Noise helpers live in the committed tree module (../trees) and the normal
# helper in the street-prop module (../street-props); imported, not copied.
sys.path.insert(0, os.path.normpath(os.path.join(HERE, "..", "trees")))
sys.path.insert(0, os.path.normpath(os.path.join(HERE, "..", "street-props")))
import dh_tree_tex as T  # noqa: E402
import dh_prop_tex as PT  # noqa: E402

TEX_DIR = os.path.join(HERE, "textures")
SS = 2                          # supersampling for drawn strokes
LAWN_SIZE = 1024                # px per period, plain and (b)
CELL_M = 3.0                    # (d): metres per world cell
CELL_CONTRAST = 0.55            # (d): "very light texturing", detail scaled toward the mean
VARIANT_BAND_IN = 0.12          # fraction of a period from each edge that is exactly the base
VARIANT_BAND_OUT = 0.20         # ... and where the variant has fully taken over
VARIANT_JITTER = 0.05           # noise on that boundary, so it is never a straight line
CHECK_FRAC = 0.06               # band checked byte for byte (inside BAND_IN - JITTER = 0.07)
MACRO_SIZE = 256
MACRO_M = 211.0                 # metres per macro period: larger than a 3 x 3 block of tiles
MACRO_CYCLES = (4, 11)          # blobs per macro period: 53 m and 19 m
MACRO_DARK = (0.86, 0.91, 0.84)     # multiplier at the macro's low end (deeper, cooler green)
MACRO_LIGHT = (1.10, 1.07, 0.88)    # ... and high end (lighter, drier, yellower)
FRINGE_SIZE = (512, 128)        # (W, H): U along the edge, V up the blades
FRINGE_M = 0.5                  # metres of edge per fringe repeat
FRINGE_H = 0.08                 # fringe card height in metres
MULCH_SIZE = 512
MULCH_M = 1.0
CONCRETE_SIZE = 512
CONCRETE_M = 1.0

# Each look: period, colours (base, dark, light, dry, clover), the stroke
# recipe and the normal strength. Lengths are in metres, so they read the
# same whatever the texture's period.
LAWNS = {
    "a": {
        "key": "lawn_a", "label": "A. Natural turf", "tile_m": 4.0, "seed": 801,
        "base": "#6C9C45", "dark": "#4E7E33", "light": "#94BC5A", "dry": "#A3A45C", "clover": "#5E9A4E",
        "clump_cycles_per_m": 7.0, "patch_cycles_per_m": 0.6, "clump_amp": 0.16, "patch_amp": 0.07,
        "strokes_per_m2": 2600, "stroke_len_m": (0.012, 0.03), "stroke_w_m": 0.0035, "stroke_amp": 0.2,
        "tufts": False, "dry_amt": 0.18, "clover_amt": 0.35, "normal": 2.2,
    },
    "b": {
        "key": "lawn_b", "label": "B. Soft painted", "tile_m": 8.0, "seed": 802,
        "base": "#7CCB55", "dark": "#62AE45", "light": "#9ADB6C", "dry": "#B2CF62", "clover": "#74C25A",
        "clump_cycles_per_m": 1.4, "patch_cycles_per_m": 0.35, "clump_amp": 0.10, "patch_amp": 0.12,
        "strokes_per_m2": 260, "stroke_len_m": (0.07, 0.13), "stroke_w_m": 0.025, "stroke_amp": 0.13,
        "tufts": False, "dry_amt": 0.12, "clover_amt": 0.0, "normal": 0.8, "soft": 1,
    },
    "c": {
        "key": "lawn_c", "label": "C. Crisp tufts", "tile_m": 3.0, "seed": 803,
        "base": "#5D983D", "dark": "#43752C", "light": "#8EC75C", "dry": "#9DAF55", "clover": "#55904A",
        "clump_cycles_per_m": 5.0, "patch_cycles_per_m": 0.8, "clump_amp": 0.14, "patch_amp": 0.06,
        "strokes_per_m2": 240, "stroke_len_m": (0.045, 0.08), "stroke_w_m": 0.009, "stroke_amp": 0.42,
        "tufts": True, "dry_amt": 0.08, "clover_amt": 0.0, "normal": 3.2,
    },
}

# The atlases, by set name: (columns, rows, px per cell, padding px per
# side, metres per cell or None for the look's own period, contrast). Every
# slot is cell + 2 x padding, a power of two:
#   cells8   (d) THE CHOSEN SET: 4 x 2 = base + 7 light variants, 3 m each,
#            224 px (75 px/m) + 16 px padding -> 1024 x 512
#   wang     (b) 2 x 2 = base + 3 full-strength variants, one period each, 2048^2
#   cells    (d) 4 x 4 = base + 15 light variants, 448 px (149 px/m), 2048^2
#   cells1k  (d) the same 16 at 1024^2 (its slots 0-7 are exactly cells8)
# Fewer variants (8, 12) of the 4 x 4 atlases are tested by using only the
# first N slots. The comparison sets are built on demand (--all).
ATLASES = {
    "cells8": (4, 2, 224, 16, CELL_M, CELL_CONTRAST),
    "wang": (2, 2, 960, 32, None, 1.0),
    "cells": (4, 4, 448, 32, CELL_M, CELL_CONTRAST),
    "cells1k": (4, 4, 224, 16, CELL_M, CELL_CONTRAST),
}
# What `python3 dh_ground_tex.py` builds and what is kept in textures/:
# Derek's pick (look C, 2026-11-24) in the chosen approach, the macro map and
# the paving concrete (edge set 1 needs nothing else).
KEEP = ("c_cells8", "macro", "concrete")

FRINGE_BASE = "#E8F2DC"
MULCH_BASE, MULCH_DARK, MULCH_LIGHT = "#6A4630", "#3B2619", "#93694A"
CONCRETE_GREY = 0.92


def _rgb(h):
    return np.array(T._hex(h), np.float32)


def _save(name, col, hgt, strength, alpha=None):
    os.makedirs(TEX_DIR, exist_ok=True)
    c = os.path.join(TEX_DIR, "%s_color.png" % name)
    n = os.path.join(TEX_DIR, "%s_normal.png" % name)
    rgb = np.clip(col, 0, 255).astype(np.uint8)
    if alpha is None:
        Image.fromarray(rgb).save(c)
    else:
        a = (np.clip(alpha, 0, 1) * 255).astype(np.uint8)
        Image.fromarray(np.dstack([rgb, a]), "RGBA").save(c)
    PT._normal_from_height(hgt, strength).save(n)
    return c, n


def _wrapped_line(draws, S, pts, fill, width):
    """Draw a polyline on every layer in `draws`, repeated at the +-S offsets
    it crosses, so the result tiles."""
    xs = [p[0] for p in pts]
    ys = [p[1] for p in pts]
    offs_x = [0] + ([S] if min(xs) < width else []) + ([-S] if max(xs) > S - width else [])
    offs_y = [0] + ([S] if min(ys) < width else []) + ([-S] if max(ys) > S - width else [])
    for ox in offs_x:
        for oy in offs_y:
            q = [(x + ox, y + oy) for x, y in pts]
            for d, f in zip(draws, fill):
                d.line(q, fill=f, width=width, joint="curve")


def _wrap_blur(im, r):
    """Gaussian blur that wraps around the edges, so the tile stays seamless."""
    a = np.asarray(im)
    p = int(r * 4)
    big = Image.fromarray(np.pad(a, p, mode="wrap")).filter(ImageFilter.GaussianBlur(r))
    return Image.fromarray(np.asarray(big)[p:-p, p:-p])


def _strokes(L, rng, S, px_per_m):
    """Two supersampled stroke layers (light, dark) in 0..1 for lawn L."""
    lit = Image.new("L", (S, S), 0)
    drk = Image.new("L", (S, S), 0)
    dl, dd = ImageDraw.Draw(lit), ImageDraw.Draw(drk)
    area = (S / px_per_m) ** 2
    n = int(L["strokes_per_m2"] * area)
    w = max(1, int(round(L["stroke_w_m"] * px_per_m)))
    lo, hi = L["stroke_len_m"]
    for _ in range(n):
        x, y = rng.uniform(0, S), rng.uniform(0, S)
        ln = rng.uniform(lo, hi) * px_per_m
        v = int(rng.uniform(150, 255))
        light = rng.random() < 0.62
        layer = (dl,) if light else (dd,)
        if L["tufts"]:
            # a tuft: 2-3 blades fanning from one root, seen from above
            base_a = rng.uniform(0, 2 * math.pi)
            for k in range(rng.choice((2, 3, 3))):
                a = base_a + (k - 1) * rng.uniform(0.3, 0.55)
                l2 = ln * rng.uniform(0.7, 1.0)
                mid = (x + math.cos(a) * l2 * 0.5 + math.cos(a + 0.4) * l2 * 0.08,
                       y + math.sin(a) * l2 * 0.5 + math.sin(a + 0.4) * l2 * 0.08)
                tip = (x + math.cos(a) * l2, y + math.sin(a) * l2)
                _wrapped_line(layer, S, [(x, y), mid, tip], (v,), w)
        else:
            a = rng.uniform(0, math.pi)
            tip = (x + math.cos(a) * ln, y + math.sin(a) * ln)
            _wrapped_line(layer, S, [(x, y), tip], (v,), w)
    soft = L.get("soft", 0)
    if soft:
        lit, drk = _wrap_blur(lit, soft * SS), _wrap_blur(drk, soft * SS)
    out = []
    for im in (lit, drk):
        a = np.asarray(im.resize((S // SS, S // SS), Image.LANCZOS), np.float32) / 255.0
        out.append(np.clip(a, 0, 1))
    return out


def _lawn_arrays(opt, seed, N=LAWN_SIZE, tile=None, contrast=1.0):
    """Colour (float RGB) and height for lawn `opt` drawn with `seed`, N px
    square covering `tile` metres (default: the option's repeat). The colour
    mean is exactly the option's base colour, so every mip level and every
    variant reads as the same green from far away. contrast < 1 flattens it
    toward that mean (the lightly textured cells of approach d)."""
    L = LAWNS[opt]
    rng = random.Random(seed)
    tile = tile or L["tile_m"]
    clump = T._periodic_noise((N, N), L["clump_cycles_per_m"] * tile, L["clump_cycles_per_m"] * tile, rng,
                              octaves=2)
    patch = T._periodic_noise((N, N), max(1.0, L["patch_cycles_per_m"] * tile),
                              max(1.0, L["patch_cycles_per_m"] * tile), rng, octaves=2)
    fine = T._periodic_noise((N, N), N / 6, N / 6, rng, octaves=1)
    dry = T._periodic_noise((N, N), max(1.0, 1.1 * tile), max(1.0, 1.1 * tile), rng, octaves=2)
    clov = T._periodic_noise((N, N), max(1.0, 1.6 * tile), max(1.0, 1.6 * tile), rng, octaves=2)
    lit, drk = _strokes(L, rng, N * SS, N * SS / tile)

    base, dark, light = _rgb(L["base"]), _rgb(L["dark"]), _rgb(L["light"])
    col = np.ones((N, N, 3), np.float32) * base
    v = L["clump_amp"] * (clump - 0.5) * 2 + L["patch_amp"] * (patch - 0.5) * 2 + 0.03 * (fine - 0.5)
    col = col * (1 + v[..., None])
    dm = np.clip((dry - 0.62) / 0.3, 0, 1) * L["dry_amt"]
    col = col + (_rgb(L["dry"]) - col) * dm[..., None]
    cm = np.clip((clov - 0.68) / 0.2, 0, 1) * L["clover_amt"]
    col = col + (_rgb(L["clover"]) - col) * cm[..., None]
    sa = L["stroke_amp"]
    col = col + (light - col) * (lit * sa * 2.2)[..., None]
    col = col + (dark - col) * (drk * sa * 1.6)[..., None]
    col = col * (base / col.reshape(-1, 3).mean(0))
    col = base + (col - base) * contrast
    hgt = (clump * 0.6 + lit * 1.0 - drk * 0.6 + fine * 0.2) * contrast
    return col, hgt


def _edge_mask(seed, N=LAWN_SIZE):
    """0 in a band along all four edges (where every variant equals the base
    texture), 1 in the interior; the transition is broken up by tileable
    noise so it never draws a straight line."""
    rng = random.Random(seed)
    ii = np.arange(N, dtype=np.float32)
    dx = np.minimum(ii, N - 1 - ii) / N
    d = np.minimum(dx[None, :], dx[:, None])
    jit = (T._periodic_noise((N, N), 6, 6, rng, octaves=2) - 0.5) * 2 * VARIANT_JITTER
    t = np.clip((d + jit - VARIANT_BAND_IN) / (VARIANT_BAND_OUT - VARIANT_BAND_IN), 0, 1)
    m = t * t * (3 - 2 * t)
    m[d < VARIANT_BAND_IN - VARIANT_JITTER] = 0.0
    return m


def _normal_arr(hgt, strength):
    return np.asarray(PT._normal_from_height(hgt, strength))


def lawn(opt):
    """The plain base pair for look `opt`: one period, LAWN_SIZE square."""
    L = LAWNS[opt]
    col, hgt = _lawn_arrays(opt, L["seed"])
    return _save(L["key"], col, hgt, L["normal"])


def atlas_slot_px(kind):
    _, _, C, pad, _, _ = ATLASES[kind]
    return C + 2 * pad


def atlas_size_px(kind):
    cols, rows = ATLASES[kind][:2]
    s = atlas_slot_px(kind)
    return cols * s, rows * s


def lawn_atlas(opt, kind):
    """Approach (b) ("wang") or (d) ("cells") atlas for look `opt`. Slot 0
    is the base; every other slot is a variant that equals it in the shared
    band. Each cell's normal is computed on the cell itself (wrapping), then
    every slot is padded by its own wrap-around."""
    L = LAWNS[opt]
    G, R, C, pad, cell_m, contrast = ATLASES[kind]
    seed0 = L["seed"] if kind == "wang" else L["seed"] + 5000
    slot = atlas_slot_px(kind)
    W, H = atlas_size_px(kind)
    cols = np.zeros((H, W, 3), np.uint8)
    nrms = np.zeros((H, W, 3), np.uint8)
    col0, h0 = _lawn_arrays(opt, seed0, N=C, tile=cell_m, contrast=contrast)
    for k in range(G * R):
        if k == 0:
            col, hgt = col0, h0
        else:
            ck, hk = _lawn_arrays(opt, seed0 + 37 * k, N=C, tile=cell_m, contrast=contrast)
            m = _edge_mask(seed0 + 9000 + k, N=C)
            col = col0 * (1 - m[..., None]) + ck * m[..., None]
            hgt = h0 * (1 - m) + hk * m
        r, c = divmod(k, G)
        c8 = np.clip(col, 0, 255).astype(np.uint8)
        n8 = _normal_arr(hgt, L["normal"])
        pw = ((pad, pad), (pad, pad), (0, 0))
        cols[r * slot:(r + 1) * slot, c * slot:(c + 1) * slot] = np.pad(c8, pw, mode="wrap")
        nrms[r * slot:(r + 1) * slot, c * slot:(c + 1) * slot] = np.pad(n8, pw, mode="wrap")
    os.makedirs(TEX_DIR, exist_ok=True)
    cp = os.path.join(TEX_DIR, "%s_%s_color.png" % (L["key"], kind))
    np_ = os.path.join(TEX_DIR, "%s_%s_normal.png" % (L["key"], kind))
    Image.fromarray(cols).save(cp)
    Image.fromarray(nrms).save(np_)
    return cp, np_


def check_atlas(opt, kind):
    """Every slot must equal slot 0 over the shared band (cell plus padding)
    and each cell must tile with itself. Returns a list of failures."""
    L = LAWNS[opt]
    G, R, C, pad, _, _ = ATLASES[kind]
    slot = atlas_slot_px(kind)
    px = pad + int(C * CHECK_FRAC)
    bad = []
    for which in ("color", "normal"):
        a = np.asarray(Image.open(os.path.join(TEX_DIR, "%s_%s_%s.png" % (L["key"], kind, which))))
        sl = [a[r * slot:(r + 1) * slot, c * slot:(c + 1) * slot] for r in range(R) for c in range(G)]
        for k in range(1, G * R):
            for name, s in (("top", np.s_[:px]), ("bottom", np.s_[-px:]), ("left", np.s_[:, :px]),
                            ("right", np.s_[:, -px:])):
                if not np.array_equal(sl[0][s], sl[k][s]):
                    bad.append("%s_%s slot %d %s %s" % (L["key"], kind, k, which, name))
    return bad


def macro(seed=811):
    """Repeat-hiding layer, RGB, stored as 0.5 x tint multiplier (URP Lit
    Detail Albedo convention: albedo x 2 x detail)."""
    S = MACRO_SIZE
    rng = random.Random(seed)
    a = T._periodic_noise((S, S), MACRO_CYCLES[0], MACRO_CYCLES[0], rng, octaves=1)
    b = T._periodic_noise((S, S), MACRO_CYCLES[1], MACRO_CYCLES[1], rng, octaves=1)
    m = a * 0.65 + b * 0.35
    m = np.clip((m - m.mean()) / (m.std() * 3.0) + 0.5, 0, 1)
    lo, hi = np.array(MACRO_DARK, np.float32), np.array(MACRO_LIGHT, np.float32)
    mult = lo + (hi - lo) * m[..., None]
    os.makedirs(TEX_DIR, exist_ok=True)
    p = os.path.join(TEX_DIR, "ground_macro.png")
    Image.fromarray(np.clip(mult * 0.5 * 255, 0, 255).astype(np.uint8)).save(p)
    return p


def fringe(seed=813):
    """Blades growing up from the bottom edge (V = 0 at the root). Tiles in U."""
    W, H = FRINGE_SIZE
    Ws, Hs = W * SS, H * SS
    rng = random.Random(seed)
    a = Image.new("L", (Ws, Hs), 0)
    v = Image.new("L", (Ws, Hs), 0)
    da, dv = ImageDraw.Draw(a), ImageDraw.Draw(v)
    for _ in range(420):
        x = rng.uniform(0, Ws)
        h = rng.uniform(0.35, 0.95) * Hs
        lean = rng.uniform(-0.35, 0.35) * h
        w0 = rng.uniform(3.0, 5.0) * SS
        val = int(rng.uniform(150, 255))
        for ox in (-Ws, 0, Ws):
            poly = [(x + ox - w0 / 2, Hs), (x + ox + w0 / 2, Hs), (x + ox + lean + 0.6, Hs - h),
                    (x + ox + lean - 0.6, Hs - h)]
            da.polygon(poly, fill=255)
            dv.polygon(poly, fill=val)
    al = np.asarray(a.resize((W, H), Image.LANCZOS), np.float32) / 255.0
    al = np.clip(al * 1.4, 0, 1)
    val = np.asarray(v.resize((W, H), Image.LANCZOS), np.float32) / 255.0
    val = np.where(al > 0.01, val / np.maximum(al / 1.4, 1e-3), 0.8)
    yy = np.linspace(0, 1, H, dtype=np.float32)[:, None]        # 0 at top row, 1 at the root
    shade = 0.62 + 0.38 * (1 - yy)                               # roots darker, tips lighter
    g = np.clip(0.55 + 0.45 * val, 0, 1) * shade
    col = _rgb(FRINGE_BASE) * g[..., None]
    # bleed colour under transparent pixels so mips don't pull in black
    rgba = Image.fromarray(np.dstack([np.clip(col, 0, 255), al * 255]).astype(np.uint8), "RGBA")
    col = np.asarray(T.bleed(rgba), np.float32)[..., :3]
    return _save("ground_fringe", col, al * (1 - yy) * 2.0, 1.5, alpha=al)


def mulch(seed=815):
    S = MULCH_SIZE * SS
    rng = random.Random(seed)
    im = Image.new("L", (S, S), 60)
    d = ImageDraw.Draw(im)
    px = S / MULCH_M
    for _ in range(5200):
        x, y = rng.uniform(0, S), rng.uniform(0, S)
        ln = rng.uniform(0.015, 0.05) * px
        w = max(2, int(rng.uniform(0.004, 0.012) * px))
        a = rng.uniform(0, math.pi)
        _wrapped_line((d,), S, [(x, y), (x + math.cos(a) * ln, y + math.sin(a) * ln)], (int(rng.uniform(70, 255)),),
                      w)
    chips = np.asarray(im.resize((MULCH_SIZE, MULCH_SIZE), Image.LANCZOS), np.float32) / 255.0
    n = T._periodic_noise((MULCH_SIZE, MULCH_SIZE), 6, 6, rng, octaves=2)
    b, dk, lt = _rgb(MULCH_BASE), _rgb(MULCH_DARK), _rgb(MULCH_LIGHT)
    col = dk + (b - dk) * np.clip(chips * 1.6, 0, 1)[..., None]
    col = col + (lt - col) * (np.clip(chips - 0.75, 0, 1) * 2)[..., None]
    col = col * (0.92 + 0.16 * n[..., None])
    return _save("ground_mulch", col, chips + n * 0.3, 3.0)


def concrete(seed=817):
    S = CONCRETE_SIZE
    rng = random.Random(seed)
    broom = T._periodic_noise((S, S), 2, 90, rng, octaves=2)     # fine streaks across (U)
    mott = T._periodic_noise((S, S), 3, 3, rng, octaves=2)
    r = np.random.default_rng(seed)
    speck = (r.random((S, S)) > 0.985).astype(np.float32)
    v = CONCRETE_GREY + 0.04 * (mott - 0.5) + 0.025 * (broom - 0.5) - 0.06 * speck
    return _save("ground_concrete", PT._grey(np.clip(v, 0, 1)), broom * 0.6 + speck * 0.4, 1.0)


def _builders():
    b = {}
    for opt in LAWNS:
        b[opt] = (lambda o=opt: lawn(o))
        for kind in ATLASES:
            b["%s_%s" % (opt, kind)] = (lambda o=opt, k=kind: lawn_atlas(o, k))
    b.update({"macro": macro, "fringe": fringe, "mulch": mulch, "concrete": concrete})
    return b


def paths():
    """Every texture path, by key, WITHOUT building anything. Keys: "<x>"
    (plain), "<x>_<atlas>", "fringe", "mulch", "concrete" -> (colour,
    normal); "macro" -> path."""
    p = {}
    for opt, L in LAWNS.items():
        p[opt] = (os.path.join(TEX_DIR, "%s_color.png" % L["key"]), os.path.join(TEX_DIR, "%s_normal.png" % L["key"]))
        for kind in ATLASES:
            p["%s_%s" % (opt, kind)] = (os.path.join(TEX_DIR, "%s_%s_color.png" % (L["key"], kind)),
                                        os.path.join(TEX_DIR, "%s_%s_normal.png" % (L["key"], kind)))
    for k in ("fringe", "mulch", "concrete"):
        p[k] = (os.path.join(TEX_DIR, "ground_%s_color.png" % k), os.path.join(TEX_DIR, "ground_%s_normal.png" % k))
    p["macro"] = os.path.join(TEX_DIR, "ground_macro.png")
    return p


def need(key):
    """One texture's path(s), building it first if it is missing (the
    comparison sets are not kept in textures/, so renders of them rebuild
    them on demand, byte for byte)."""
    v = paths()[key]
    files = (v,) if isinstance(v, str) else v
    if not all(os.path.exists(f) for f in files):
        _builders()[key]()
    return v


def build(keys):
    return {k: _builders()[k]() for k in keys}


if __name__ == "__main__":
    keys = list(_builders()) if "--all" in sys.argv else list(KEEP)
    for k, v in build(keys).items():
        print(k, v)
    atl = [k.split("_", 1) for k in keys if "_" in k]
    fails = [f for opt, kind in atl for f in check_atlas(opt, kind)]
    print("shared edges OK" if not fails else "SHARED EDGE FAILURES: %s" % fails[:20])
    if fails:
        sys.exit(1)
