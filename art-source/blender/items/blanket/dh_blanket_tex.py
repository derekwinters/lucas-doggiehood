"""Procedural blanket textures (numpy + PIL), same recipe as the environment sets: tileable,
no external images, fixed seeds, so every run writes the same PNGs.

  blanket_plaid_<colourway>_color
                        512 x 512  one repeat (sett) of a woven wool tartan, PLAID_TILE metres
                        square, one texture per colourway in blanket_spec.COLOURWAYS (SETTS
                        below). Every colourway uses the same sett proportions: a dominant
                        ground, a second block colour, a light line and a thin accent line,
                        woven as a 2/2 twill so where a warp and a weft stripe cross the colours
                        mix thread by thread, like a real plaid. A faint brushed-wool mottle
                        (same seed for every colourway).
  blanket_weave_normal  512 x 512  tangent-space normal map of the same twill (thread ridges on
                        the diagonal), from the weave's height. Shared by every colourway.
  blanket_back_color    256 x 256  the reverse side: a cream fleece with soft lumps, BACK_TILE
                        metres square. Shared by every colourway.

Written into <root>/textures/ so the exported .gltf references them as ../textures/<name>.png.
"""
import os
import random
import sys

import numpy as np
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
ENV = os.path.normpath(os.path.join(HERE, "..", "..", "environment"))
# Noise from the committed tree module, the normal helper from the street-prop module:
# imported, not copied (as the fence textures do).
sys.path.insert(0, os.path.join(ENV, "trees"))
sys.path.insert(0, os.path.join(ENV, "street-props"))
import dh_tree_tex as T  # noqa: E402
import dh_prop_tex as PT  # noqa: E402

sys.path.insert(0, HERE)
import blanket_spec as S  # noqa: E402

PLAID_SIZE = 512
PLAID_TILE = 0.32               # metres per plaid repeat
THREAD_PX = 4                   # one thread in pixels (2.5 mm at PLAID_TILE); divides PLAID_SIZE
TWILL = 4                       # 2/2 twill: the weave repeats every 4 threads
BACK_SIZE = 256
BACK_TILE = 0.25                # metres per fleece repeat
PLAID_SEED = 413
BACK_SEED = 4131
WEAVE_NORMAL_STRENGTH = 2.2
MOTTLE_AMOUNT = 0.10            # brushed-wool brightness variation, +- fraction
BACK_LUMP_AMOUNT = 0.12

BACK_CREAM = "#ECE2CD"
# One sett layout for every colourway, as (slot, width in threads), symmetric about its middle;
# used for warp and weft. Slots: G ground (dominant), B second block, L light line, A accent line.
SETT = [("G", 30), ("B", 22), ("L", 4), ("B", 22), ("G", 30), ("A", 3),
        ("G", 12), ("L", 12), ("G", 12), ("A", 3)]
# Slot colours per colourway (keys match blanket_spec.COLOURWAYS). The grounds are far apart so
# the four read differently at the game camera (tests/test_blanket.py checks the dominant colours).
SETTS = {
    "navy":   {"G": "#22385E", "B": "#3D78AE", "L": "#E6D9BC", "A": "#C8963A"},   # approved first look
    "red":    {"G": "#A3272B", "B": "#24503A", "L": "#E9D9A6", "A": "#1E2C4A"},   # red and green tartan
    "green":  {"G": "#2C6A2E", "B": "#8DB25E", "L": "#EDE3CA", "A": "#C9B27A"},   # green and cream
    "purple": {"G": "#7F7C86", "B": "#5E3A7A", "L": "#E6E1EA", "A": "#B79AD6"},   # grey and purple
}


def _rgb(h):
    h = h.lstrip("#")
    return np.array([int(h[i:i + 2], 16) for i in (0, 2, 4)], float)


def _stripe_lut(size, colours):
    """Colour of each thread across one repeat, the sett scaled to fill `size` pixels."""
    threads = size // THREAD_PX
    total = sum(w for _, w in SETT)
    lut = []
    for slot, w in SETT:
        lut += [_rgb(colours[slot])] * w
    idx = (np.arange(threads) * total) // threads
    return np.stack([lut[i] for i in idx])


def plaid(root, key):
    n = PLAID_SIZE
    lut = _stripe_lut(n, SETTS[key])
    t = np.arange(n) // THREAD_PX
    warp = lut[t][None, :, :].repeat(n, 0)          # threads along V, colour by column
    weft = lut[t][:, None, :].repeat(n, 1)          # threads along U, colour by row
    tx, ty = np.meshgrid(t, t)
    warp_up = ((tx + ty) % TWILL) < TWILL // 2      # 2/2 twill diagonal
    col = np.where(warp_up[..., None], warp, weft)
    rng = random.Random(PLAID_SEED)
    mott = T._periodic_noise((n, n), 6, 6, rng, octaves=3)
    col = col * (1 + MOTTLE_AMOUNT * (mott[..., None] * 2 - 1))
    # thread height: each thread is a ridge across its width, the up-thread sits higher
    px = np.arange(n) % THREAD_PX
    bump = np.sin(np.pi * (px + 0.5) / THREAD_PX)
    hgt = np.where(warp_up, bump[None, :].repeat(n, 0), bump[:, None].repeat(n, 1)) \
        + np.where(warp_up, 0.35, 0.0)
    tex = os.path.join(root, "textures")
    os.makedirs(tex, exist_ok=True)
    c = os.path.join(tex, "blanket_plaid_%s_color.png" % key)
    nm = os.path.join(tex, "blanket_weave_normal.png")
    Image.fromarray(np.clip(col, 0, 255).astype(np.uint8)).save(c)
    PT._normal_from_height(hgt, WEAVE_NORMAL_STRENGTH).save(nm)
    return c, nm


def back(root):
    n = BACK_SIZE
    rng = random.Random(BACK_SEED)
    lumps = T._periodic_noise((n, n), 10, 10, rng, octaves=3)
    fine = T._periodic_noise((n, n), 40, 40, rng, octaves=2)
    v = 1 + BACK_LUMP_AMOUNT * ((lumps * 0.7 + fine * 0.3) * 2 - 1)
    col = _rgb(BACK_CREAM)[None, None, :] * v[..., None]
    tex = os.path.join(root, "textures")
    os.makedirs(tex, exist_ok=True)
    p = os.path.join(tex, "blanket_back_color.png")
    Image.fromarray(np.clip(col, 0, 255).astype(np.uint8)).save(p)
    return p


def make_all(root):
    """Write every texture into <root>/textures/.
    Returns {"plaid": {colourway: path}, "normal": path, "back": path}."""
    plaids = {}
    nm = None
    for key, _ in S.COLOURWAYS:
        plaids[key], nm = plaid(root, key)
    return {"plaid": plaids, "normal": nm, "back": back(root)}


if __name__ == "__main__":
    print(make_all(sys.argv[1] if len(sys.argv) > 1 else HERE))
