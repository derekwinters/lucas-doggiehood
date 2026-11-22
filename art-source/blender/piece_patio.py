"""Upgrade piece, level 4: the PATIO, a ground-level paved patio.

Local origin = the ADDITION's ATTACH_PATIO point: on the ground, at the back
edge of the addition's slab, centred on the addition. +Y points away from
the house. The patio is as wide as the addition, so it sits squarely behind
it; the deck's side stairs land on its house-side edge. Flush with the
ground (only a few cm proud so it reads as paving, not a platform).
"""

import os
import random
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import dh_house_kit as K  # noqa: E402

DIMS = {
    "width": 4.0,            # = piece_addition.DIMS["width"]
    "depth": 5.5,
    "paver": 0.9,
    "grout": 0.07,
    "height": 0.08,
    "border": 0.22,
}


def build(palette, d=DIMS, name="piece_patio", seed=7):
    PW, PD, h = d["width"], d["depth"], d["height"]
    x0, x1 = -PW / 2, PW / 2
    m = K.MeshBuilder(name, palette)
    b = d["border"]
    # border curb + grout bed
    m.box((x0, 0, 0), (x1, PD, h * 0.6), "stone_c")
    m.box((x0, 0, 0), (x0 + b, PD, h), "stone_c")
    m.box((x1 - b, 0, 0), (x1, PD, h), "stone_c")
    m.box((x0 + b, PD - b, 0), (x1 - b, PD, h), "stone_c")
    m.box((x0 + b, 0, 0), (x1 - b, b * 0.5, h), "stone_c")
    rng = random.Random(seed)
    p, g = d["paver"], d["grout"]
    ix0, ix1 = x0 + b + g, x1 - b - g
    iy0, iy1 = b * 0.5 + g, PD - b - g
    nx = max(1, int(round((ix1 - ix0) / p)))
    ny = max(1, int(round((iy1 - iy0) / p)))
    sx, sy = (ix1 - ix0) / nx, (iy1 - iy0) / ny
    for i in range(nx):
        for j in range(ny):
            key = "stone_a" if (i + j) % 2 == 0 else "stone_b"
            if rng.random() < 0.12:
                key = "stone_c"
            m.box((ix0 + i * sx + g / 2, iy0 + j * sy + g / 2, h * 0.6),
                  (ix0 + (i + 1) * sx - g / 2, iy0 + (j + 1) * sy - g / 2, h), key)
    return m.build()
