"""Procedural backyard-fence textures (numpy + PIL), same recipe as the tree
and street-prop textures: tileable surfaces from periodic (FFT) noise,
tangent-space normal maps derived from height, no external images, fixed
seeds so every run writes the same PNGs.

Written into this set's own textures/ folder (TEX_DIR), each stored once,
so the exported .gltf files reference them as ../textures/<name>.png.

  fence_cedar     1024 x 1024  EIGHT cedar boards side by side, one per
                  128 px column, each its own tone (pale honey to red-brown),
                  grain along V, knots, darkened board edges. A board's UVs
                  pick one column, so neighbouring boards differ in colour
                  without extra materials. Full colour (factor stays white).
  fence_paintwood 512 x 512    white exterior paint over wood: the grain still
                  shows in the bump, faint brush streaks. Near-white; tinted by
                  baseColorFactor.
  fence_rail      512 x 1024   weathered split cedar rail / post: grey-brown,
                  deep checks along the grain (V). Full colour.
  fence_wire      512 x 512    RGBA welded-wire mesh, 50 x 100 mm openings,
                  black vinyl-coated 5 mm wire; alpha is the mask. One repeat
                  is WIRE_TILE metres square.
  fence_metal     256 x 256    powder-coated hardware (hinges, latch, wall
                  brackets); near-white, tinted per material.
"""
import math
import os
import random
import sys

import numpy as np
from PIL import Image, ImageDraw

HERE = os.path.dirname(os.path.abspath(__file__))
# Noise helpers live in the committed tree module (../trees) and the normal /
# save helpers in the street-prop module (../street-props); imported, not copied.
sys.path.insert(0, os.path.normpath(os.path.join(HERE, "..", "trees")))
sys.path.insert(0, os.path.normpath(os.path.join(HERE, "..", "street-props")))
import dh_tree_tex as T  # noqa: E402
import dh_prop_tex as PT  # noqa: E402

TEX_DIR = os.path.join(HERE, "textures")
SS = 2                          # supersampling for drawn detail
CEDAR_COLS = 8                  # boards in the cedar texture
CEDAR_SIZE = 1024
PAINT_SIZE = 512
RAIL_SIZE = (512, 1024)         # (W, H): U across the rail, V along the grain
WIRE_SIZE = 512
WIRE_TILE = 0.5                 # metres per wire-texture repeat (set in dh_fences too)
WIRE_CELL = (0.05, 0.10)        # (horizontal, vertical) opening, metres
WIRE_D = 0.005                  # vinyl-coated wire diameter, metres
METAL_SIZE = 256

# Cedar board tones: freshly installed western red cedar varies board to
# board from honey through salmon to red-brown.
CEDAR_TONES = ["#C8996A", "#B97E55", "#D2A577", "#A86A45", "#C08A5E", "#B5764F", "#CDA06F", "#9E6443"]
CEDAR_DARK = "#7A4A2E"
RAIL_BASE, RAIL_DARK, RAIL_LIGHT = "#8C7A66", "#4E4034", "#B3A592"
WIRE_RGB = "#1D1F1E"


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


def cedar(seed=61):
    S = CEDAR_SIZE
    cw = S // CEDAR_COLS
    rng = random.Random(seed)
    grain = T._periodic_noise((S, S), 0.6, 24, rng, octaves=3)        # long streaks along V
    fine = T._periodic_noise((S, S), 3, 160, rng, octaves=2)
    ring = T._periodic_noise((S, S), 1.2, 60, rng, octaves=2)
    col = np.zeros((S, S, 3), np.float32)
    h = grain * 0.5 + fine * 0.5
    r = np.random.default_rng(seed)
    yy, xx = np.mgrid[0:S, 0:S].astype(np.float32)
    dark = np.array(T._hex(CEDAR_DARK), np.float32)
    for c in range(CEDAR_COLS):
        x0 = c * cw
        tone = np.array(T._hex(CEDAR_TONES[c]), np.float32)
        sl = slice(x0, x0 + cw)
        t = np.clip(grain[:, sl] * 0.7 + ring[:, sl] * 0.4 - 0.2, 0, 1)
        cc = tone * (0.86 + 0.22 * t[..., None]) * (0.95 + 0.08 * fine[:, sl, None])
        cc = cc + (dark - tone) * (np.clip(ring[:, sl] - 0.78, 0, 1) / 0.22)[..., None] * 0.45
        # at most one knot per board (wrapped in V so the tile still repeats)
        for _ in range(r.integers(0, 2)):
            kx, ky = x0 + r.uniform(0.3, 0.7) * cw, r.uniform(0, S)
            rad = r.uniform(4, 8)
            dy = np.abs(yy[:, sl] - ky)
            dy = np.minimum(dy, S - dy)
            d = np.sqrt(((xx[:, sl] - kx) * 1.0) ** 2 + (dy * 0.75) ** 2)
            k = np.clip(1 - d / rad, 0, 1)
            halo = np.clip(1 - d / (rad * 2.2), 0, 1) * 0.15
            cc = cc * (1 - (0.45 * k + halo)[..., None]) + dark * (0.45 * k)[..., None]
            h[:, sl] -= k * 0.5
        # board edges: a few px darker and rounded down in the bump
        e = np.minimum(xx[:, sl] - x0, x0 + cw - 1 - xx[:, sl])
        edge = np.clip(1 - e / 5.0, 0, 1)
        cc = cc * (1 - 0.28 * edge[..., None])
        h[:, sl] -= edge * 1.2
        col[:, sl] = cc
    return _save("fence_cedar", col, h, 2.4)


def paintwood(seed=63):
    S = PAINT_SIZE
    rng = random.Random(seed)
    grain = T._periodic_noise((S, S), 0.7, 30, rng, octaves=3)
    brush = T._periodic_noise((S, S), 2, 140, rng, octaves=2)
    mott = T._periodic_noise((S, S), 3, 3, rng, octaves=2)
    v = 0.93 + 0.03 * (mott - 0.5) + 0.02 * (brush - 0.5) - 0.025 * np.clip(grain - 0.7, 0, 1)
    h = grain * 0.7 + brush * 0.3
    col = PT._grey(np.clip(v, 0, 1)) * np.array([1.0, 0.995, 0.975])
    return _save("fence_paintwood", col, h, 1.4)


def rail(seed=65):
    W, H = RAIL_SIZE
    rng = random.Random(seed)
    ridges = T._periodic_noise((H, W), 0.8, 10, rng, octaves=3)
    detail = T._periodic_noise((H, W), 10, 70, rng, octaves=3)
    hh = np.clip(ridges * 0.75 + detail * 0.35, 0, None)
    hh = (hh - hh.min()) / (hh.max() - hh.min())
    checks = np.clip((0.32 - hh) / 0.14, 0, 1) ** 1.5               # deep splits along the grain
    b, dk, lt = (np.array(T._hex(c), np.float32) for c in (RAIL_BASE, RAIL_DARK, RAIL_LIGHT))
    col = b * (1 - checks[..., None]) + dk * checks[..., None]
    col = col + (lt - b) * np.clip((hh - 0.65) / 0.35, 0, 1)[..., None] * 0.7
    col = col * (0.9 + 0.18 * detail[..., None])
    return _save("fence_rail", col, hh - 0.9 * checks, 3.0)


def wire(seed=67):
    """Welded wire: vertical wires every WIRE_CELL[0], horizontal every
    WIRE_CELL[1]; drawn at SS x then downsampled; alpha = coverage."""
    S = WIRE_SIZE * SS
    px = S / WIRE_TILE                        # supersampled px per metre
    a = Image.new("L", (S, S), 0)
    d = ImageDraw.Draw(a)
    wpx = max(2, int(round(WIRE_D * px)))
    nx, ny = int(round(WIRE_TILE / WIRE_CELL[0])), int(round(WIRE_TILE / WIRE_CELL[1]))
    for i in range(nx):
        x = (i + 0.5) * S / nx
        d.rectangle([x - wpx / 2, 0, x + wpx / 2, S], fill=255)
    for j in range(ny):
        y = (j + 0.5) * S / ny
        d.rectangle([0, y - wpx / 2, S, y + wpx / 2], fill=255)
    al = np.asarray(a.resize((WIRE_SIZE, WIRE_SIZE), Image.LANCZOS), np.float32) / 255
    # widen coverage slightly so the 0.5 cutoff keeps a solid wire after downsampling
    al = np.clip(al * 1.6, 0, 1)
    rng = random.Random(seed)
    mott = T._periodic_noise((WIRE_SIZE, WIRE_SIZE), 6, 6, rng, octaves=2)
    base = np.array(T._hex(WIRE_RGB), np.float32)
    col = np.ones((WIRE_SIZE, WIRE_SIZE, 3), np.float32) * base * (0.9 + 0.25 * mott[..., None])
    return _save("fence_wire", col, al * 2.0, 2.0, alpha=al)


def metal(seed=69):
    S = METAL_SIZE
    rng = random.Random(seed)
    peel = T._periodic_noise((S, S), 40, 40, rng, octaves=2)
    mott = T._periodic_noise((S, S), 3, 3, rng, octaves=2)
    v = 0.9 + 0.05 * (mott - 0.5) + 0.03 * (peel - 0.5)
    return _save("fence_metal", PT._grey(np.clip(v, 0, 1)), peel, 1.2)


def build_all():
    return {"cedar": cedar(), "paintwood": paintwood(), "rail": rail(), "wire": wire(), "metal": metal()}


KEYS = ("cedar", "paintwood", "rail", "wire", "metal")


def paths():
    """Texture paths, generating them on first use."""
    p = {k: (os.path.join(TEX_DIR, "fence_%s_color.png" % k), os.path.join(TEX_DIR, "fence_%s_normal.png" % k))
         for k in KEYS}
    if not all(os.path.exists(a) and os.path.exists(b) for a, b in p.values()):
        return build_all()
    return p


if __name__ == "__main__":
    for k, v in build_all().items():
        print(k, v)
