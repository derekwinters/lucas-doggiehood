"""Procedural road textures (numpy + PIL) for the #802 road kit (look A approved, #836).

Same recipe as the lawn, tree, street-prop and fence textures: tileable by
construction (white noise and periodic FFT noise, wrap-around blur),
tangent-space normal maps derived from height, no external images, fixed
seeds so every run writes the same PNGs into this set's own textures/.

ASPHALT (one colour + normal pair per tone, ASPHALT_SIZE^2):

  Sampled in WORLD space, UV0 = world position / ASPHALT_M, like the lawn,
  so it is continuous across every tile border and every tile type.
  ASPHALT_M = 6.4 m (80 px/m) on purpose does NOT divide the 60 m tile, so
  the texture lands at a different phase on every tile.

  Repeat hiding by construction: the texture holds ONLY detail finer than
  about 0.5 m (aggregate grain, small stones, pits, faint sub-metre
  mottling). Nothing at the scale of the period exists in it, so there is
  nothing for the eye to find repeating. All large-scale variation comes
  from things that do not repeat: the shared ground macro on UV1 (211 m)
  and the seeded per-tile patches and sealed cracks (dh_roads.py).

  asphalt_fresh   dark, near-black fresh blacktop, fine and even
  asphalt_mid     medium charcoal, light wear
  asphalt_worn    weathered light grey, exposed pale aggregate, ravelling pits

MARKINGS ATLAS (MARK_W x MARK_H, colour + normal), one per paint finish:

  4 swatches of MARK_SWATCH px = 1 m x 1 m each, every swatch tileable in U
  and V: 0 white paint, 1 yellow paint, 2 crack sealant ("tar snake"),
  3 curb concrete (the flat curb band). Paint is OPAQUE: in the worn finish the wear shows as the
  look's own asphalt colour, so no alpha test or blending is needed.
  A marking quad is cut into <= 1 m pieces along its length, each sampling
  the full U range of its swatch, so a long line is continuous (U tiles).

  road_markings_clean   crisp thermoplastic-like paint
  road_markings_worn    worn paint, asphalt showing through (tuned to the
                        weathered asphalt)

Derek picked look A (#836): only asphalt_fresh and road_markings_clean are
kept in textures/ (KEEP). The others are rebuilt on demand, byte for byte,
by need() or by running this script with --all.
"""
import os
import random
import sys

import numpy as np
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
# Noise helpers from the committed tree module, normal helper from the
# street props: imported by relative path, not copied.
sys.path.insert(0, os.path.normpath(os.path.join(HERE, "..", "trees")))
sys.path.insert(0, os.path.normpath(os.path.join(HERE, "..", "street-props")))
import dh_tree_tex as T  # noqa: E402
import dh_prop_tex as PT  # noqa: E402

TEX_DIR = os.path.join(HERE, "textures")

ASPHALT_SIZE = 512             # px per period
ASPHALT_M = 6.4                # metres per period (80 px/m); not a divisor of 60 m
MARK_SWATCH = 128              # px per 1 m swatch
MARK_M = 1.0                   # metres per swatch
MARK_SWATCHES = ("white", "yellow", "sealant", "curb")
MARK_W, MARK_H = MARK_SWATCH * len(MARK_SWATCHES), MARK_SWATCH

# Asphalt tones. base = sRGB mean colour; grain = fine aggregate contrast;
# stones = fraction of pixels that are a light stone; pits = fraction that
# are a dark pit; mott = sub-metre mottling; warm = R-B shift of the stones.
ASPHALTS = {
    "fresh": {"base": "#3B3D40", "grain": 0.10, "stones": 0.010, "stone_lift": 0.35, "pits": 0.004,
              "mott": 0.035, "normal": 1.4, "seed": 821},
    "mid": {"base": "#55575A", "grain": 0.13, "stones": 0.030, "stone_lift": 0.40, "pits": 0.010,
            "mott": 0.05, "normal": 1.8, "seed": 823},
    "worn": {"base": "#76777A", "grain": 0.10, "stones": 0.040, "stone_lift": 0.22, "pits": 0.018,
             "mott": 0.06, "normal": 2.0, "seed": 825},
}

PAINT = {
    "white": "#F1F0EA",
    "yellow": "#E6B530",
    "sealant": "#1E1F21",
    "curb": "#B9B3A9",          # the lawn round's curb: concrete #C9C3B8 x 0.92 (dh_ground)
}
FINISHES = {
    # wear = fraction of the swatch where asphalt shows through; holes take
    # the colour of `hole` (the asphalt the finish is tuned for)
    "clean": {"wear": 0.0, "hole": None, "seed": 831},
    "worn": {"wear": 0.20, "hole": "worn", "seed": 833},
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


def _wrap_gauss(a, sigma):
    """Separable Gaussian blur with wrap-around (keeps the tile seamless)."""
    if sigma <= 0:
        return a
    r = int(np.ceil(sigma * 3))
    k = np.exp(-0.5 * (np.arange(-r, r + 1) / sigma) ** 2)
    k /= k.sum()
    out = np.zeros_like(a)
    for i, w in enumerate(k):
        out += w * np.roll(a, i - r, axis=0)
    a2 = out
    out = np.zeros_like(a2)
    for i, w in enumerate(k):
        out += w * np.roll(a2, i - r, axis=1)
    return out


def _highpass_noise(S, rng, lo_cycles, hi_cycles):
    """Periodic noise with energy only between lo and hi cycles per period:
    nothing at the period scale, so nothing that reads as a repeat."""
    r = np.random.default_rng(rng.randrange(1 << 30))
    fy = np.fft.fftfreq(S)[:, None] * S
    fx = np.fft.fftfreq(S)[None, :] * S
    f = np.sqrt(fx ** 2 + fy ** 2)
    band = ((f >= lo_cycles) & (f <= hi_cycles)).astype(np.float64) / np.maximum(f, 1) ** 0.5
    ph = np.exp(2j * np.pi * r.random((S, S)))
    out = np.real(np.fft.ifft2(band * ph))
    out -= out.mean()
    return out / (np.abs(out).max() + 1e-9)


def asphalt(tone):
    A = ASPHALTS[tone]
    S = ASPHALT_SIZE
    rng = random.Random(A["seed"])
    r = np.random.default_rng(A["seed"])
    # fine aggregate: white noise softened to ~1-2 px (1-3 cm)
    grain = _wrap_gauss(r.standard_normal((S, S)), 0.7)
    grain /= np.abs(grain).max()
    # light stones: sparse, 1-3 px, slightly warm
    stones = (r.random((S, S)) < A["stones"]).astype(np.float64)
    stones = np.clip(_wrap_gauss(stones, 0.6) * 3.0, 0, 1)
    # dark pits (ravelling)
    pits = (r.random((S, S)) < A["pits"]).astype(np.float64)
    pits = np.clip(_wrap_gauss(pits, 0.9) * 4.0, 0, 1)
    # sub-metre mottling only (>= 12 cycles per 6.4 m period = <= 0.53 m)
    mott = _highpass_noise(S, rng, 12, 48)
    base = _rgb(A["base"])
    v = 1.0 + A["grain"] * grain + A["mott"] * mott - 0.35 * pits
    col = base[None, None, :] * v[..., None]
    lift = np.array([1.0 + A["stone_lift"] * 1.05, 1.0 + A["stone_lift"], 1.0 + A["stone_lift"] * 0.9])
    col = col * (1 - stones[..., None]) + np.minimum(base * lift, 255)[None, None, :] * stones[..., None]
    hgt = 0.5 * grain + 0.8 * stones - 1.0 * pits
    return _save("asphalt_%s" % tone, col, hgt, A["normal"])


def _swatch_noise(rng, S, lo, hi):
    return _highpass_noise(S, rng, lo, hi)


def markings(finish):
    F = FINISHES[finish]
    S = MARK_SWATCH
    rng = random.Random(F["seed"])
    r = np.random.default_rng(F["seed"])
    cols, hgts = [], []
    hole = _rgb(ASPHALTS[F["hole"]]["base"]) if F["hole"] else None
    for key in MARK_SWATCHES:
        c = _rgb(PAINT[key])
        fine = _wrap_gauss(r.standard_normal((S, S)), 0.8)
        fine /= np.abs(fine).max()
        if key == "curb":
            broom = _swatch_noise(rng, S, 20, 60)
            v = 1.0 + 0.03 * fine + 0.03 * _swatch_noise(rng, S, 2, 8) + 0.02 * broom
            col = c[None, None, :] * v[..., None]
            h = 0.5 + 0.2 * broom
        elif key == "sealant":
            v = 1.0 + 0.10 * fine
            col = c[None, None, :] * v[..., None]
            h = 0.4 + 0.1 * fine
        else:
            v = 1.0 + 0.025 * fine + 0.02 * _swatch_noise(rng, S, 3, 12)
            col = c[None, None, :] * v[..., None]
            h = 0.6 + 0.05 * fine                       # paint stands a hair proud
            if F["wear"] > 0:
                wear_n = 0.6 * _swatch_noise(rng, S, 2, 6) + 0.4 * _swatch_noise(rng, S, 8, 30)
                speck = _wrap_gauss(r.standard_normal((S, S)), 0.6)
                w = wear_n + 0.2 * speck / (np.abs(speck).max() + 1e-9)
                thr = np.quantile(w, 1.0 - F["wear"])
                m = np.clip((w - thr) / 0.25, 0, 1) ** 1.5        # 1 = asphalt showing
                agg = hole[None, None, :] * (1.0 + 0.12 * fine[..., None])
                col = col * (1 - m[..., None]) + agg * m[..., None]
                h = h * (1 - m) + (0.2 + 0.1 * fine) * m
        cols.append(col)
        hgts.append(h)
    col = np.concatenate(cols, axis=1)
    hgt = np.concatenate(hgts, axis=1)
    return _save("road_markings_%s" % finish, col, hgt, 2.0)


def paths():
    p = {}
    for t in ASPHALTS:
        p["asphalt_" + t] = tuple(os.path.join(TEX_DIR, "asphalt_%s_%s.png" % (t, k)) for k in ("color", "normal"))
    for f in FINISHES:
        p["markings_" + f] = tuple(os.path.join(TEX_DIR, "road_markings_%s_%s.png" % (f, k))
                                   for k in ("color", "normal"))
    return p


def _builders():
    b = {"asphalt_" + t: (lambda t=t: asphalt(t)) for t in ASPHALTS}
    b.update({"markings_" + f: (lambda f=f: markings(f)) for f in FINISHES})
    return b


KEEP = ("asphalt_fresh", "markings_clean")    # Derek's pick, look A (#836); the rest build on demand


def need(key):
    v = paths()[key]
    if not all(os.path.exists(f) for f in v):
        _builders()[key]()
    return v


def swatch_uv(key, u, v):
    """Atlas UV (V up) for swatch `key` at swatch-local (u, v) in 0..1."""
    i = MARK_SWATCHES.index(key)
    return ((i + u) / len(MARK_SWATCHES), v)


def check_tileable():
    """Every asphalt texture and every swatch must wrap: compare the
    wrapped edge step with the interior step statistics."""
    bad = []
    for key, (c, _) in paths().items():
        if not os.path.exists(c):
            continue
        a = np.asarray(Image.open(c), np.float32).mean(axis=2)
        blocks = [a] if key.startswith("asphalt") else np.split(a, len(MARK_SWATCHES), axis=1)
        for i, b in enumerate(blocks):
            inner = np.abs(np.diff(b, axis=1)).mean()
            edge = np.abs(b[:, 0] - b[:, -1]).mean()
            inner_v = np.abs(np.diff(b, axis=0)).mean()
            edge_v = np.abs(b[0, :] - b[-1, :]).mean()
            if edge > 1.6 * inner + 1.0 or edge_v > 1.6 * inner_v + 1.0:
                bad.append((key, i, round(float(edge), 2), round(float(inner), 2)))
    return bad


if __name__ == "__main__":
    keys = list(_builders()) if "--all" in sys.argv else list(KEEP)
    for k in keys:
        print(k, _builders()[k]())
    bad = check_tileable()
    print("tileable OK" if not bad else "NOT TILEABLE: %s" % bad)
    if bad:
        sys.exit(1)
