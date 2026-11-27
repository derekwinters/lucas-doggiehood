"""Procedural dog-bed textures (numpy + PIL), the street props' recipe: tileable surfaces from
periodic (FFT) noise, tangent-space normal maps derived from height.

Two 512 x 512 colour + normal pairs, written into this folder's textures/ (or <root>/textures/):
  cover    the bolster and base cover: a plain-weave canvas (THREADS threads per repeat) with
           slubby yarn and faint mottling. Light and near-neutral on purpose: the material
           multiplies it by the cover colour (exported as glTF baseColorFactor).
  fleece   the cushion top: a soft sherpa fleece of small nubbly curls, cream, used as is.

Deterministic: fixed seeds; the PNGs carry no metadata chunks.
"""
import os
import random
import sys

import numpy as np
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
# dh_tree_tex (periodic noise) is the committed tree module in environment/trees.
sys.path.insert(0, os.path.normpath(os.path.join(HERE, "..", "..", "environment", "trees")))
import dh_tree_tex as T  # noqa: E402

SIZE = 512
THREADS = 48                 # cover: threads across one texture repeat
COVER_SEED = 4111
FLEECE_SEED = 4112
COVER_BASE = 0.86            # cover: mean grey of the light, tintable map
COVER_WEAVE = 0.10           # cover: light/dark swing between thread crowns and gaps
COVER_SLUB = 0.06            # cover: thick/thin yarn variation
COVER_MOTTLE = 0.04
COVER_NORMAL = 2.2           # normal-map strength (height units per texel)
FLEECE_RGB = (212, 196, 170) # cushion fleece, oatmeal cream
FLEECE_CURL_SCALE = 34       # curls across one repeat
FLEECE_SHADE = 0.16          # light/dark swing between curl tops and hollows
FLEECE_NORMAL = 3.0
NAMES = ("cover", "fleece")


def _normal_from_height(hh, strength):
    hh = hh * strength
    gx = (np.roll(hh, -1, 1) - np.roll(hh, 1, 1)) / 2
    gy = (np.roll(hh, -1, 0) - np.roll(hh, 1, 0)) / 2
    n = np.dstack([-gx, gy, np.ones_like(hh)])
    n /= np.linalg.norm(n, axis=2, keepdims=True)
    return Image.fromarray(np.round((n * 0.5 + 0.5) * 255).astype(np.uint8))


def _save_pair(tex_dir, name, rgb, height, strength):
    os.makedirs(tex_dir, exist_ok=True)
    c = os.path.join(tex_dir, "bed_%s_color.png" % name)
    n = os.path.join(tex_dir, "bed_%s_normal.png" % name)
    Image.fromarray(np.clip(np.round(rgb), 0, 255).astype(np.uint8)).save(c)
    _normal_from_height(height, strength).save(n)
    return c, n


def cover(size=SIZE):
    """Plain weave: warp (along U) and weft (along V) threads alternate over and under."""
    rng = random.Random(COVER_SEED)
    yy, xx = np.mgrid[0:size, 0:size] / float(size)
    u, v = xx * THREADS, yy * THREADS
    across_warp = np.sin(np.pi * (u % 1.0))      # 0 at the gap, 1 at a warp thread's crown
    across_weft = np.sin(np.pi * (v % 1.0))
    over = ((np.floor(u) + np.floor(v)) % 2).astype(bool)
    # the thread on top bulges along its own length between crossings
    warp = across_warp * (0.55 + 0.45 * np.sin(np.pi * (v % 1.0)))
    weft = across_weft * (0.55 + 0.45 * np.sin(np.pi * (u % 1.0)))
    h = np.where(over, warp, weft)
    slub_u = T._periodic_noise((size, size), 1, 24, rng, octaves=2)     # varies along V per warp
    slub_v = T._periodic_noise((size, size), 24, 1, rng, octaves=2)
    slub = np.where(over, slub_u, slub_v)
    mottle = T._periodic_noise((size, size), 3, 3, rng, octaves=3)
    g = COVER_BASE + COVER_WEAVE * (h - 0.5) + COVER_SLUB * (slub - 0.5) + COVER_MOTTLE * (mottle - 0.5)
    rgb = np.dstack([g, g, g * 0.985]) * 255.0
    return rgb, h + 0.35 * slub


def fleece(size=SIZE):
    """Sherpa: small rounded curls (periodic noise, sharpened into lumps) over a soft base."""
    rng = random.Random(FLEECE_SEED)
    curls = T._periodic_noise((size, size), FLEECE_CURL_SCALE, FLEECE_CURL_SCALE, rng, octaves=2)
    lumps = np.clip((curls - 0.35) / 0.5, 0, 1) ** 0.7
    soft = T._periodic_noise((size, size), 4, 4, rng, octaves=3)
    shade = 1.0 + FLEECE_SHADE * (lumps - 0.6) + 0.04 * (soft - 0.5)
    rgb = np.dstack([np.full((size, size), c, float) for c in FLEECE_RGB]) * shade[..., None]
    return rgb, lumps


def build_all(tex_dir):
    """Write both pairs into tex_dir. Returns {name: (colour path, normal path)}."""
    out = {}
    for name, fn, strength in (("cover", cover, COVER_NORMAL), ("fleece", fleece, FLEECE_NORMAL)):
        rgb, h = fn()
        out[name] = _save_pair(tex_dir, name, rgb, h, strength)
    return out


if __name__ == "__main__":
    print(build_all(sys.argv[1] if len(sys.argv) > 1 else os.path.join(HERE, "textures")))
