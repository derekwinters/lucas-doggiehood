"""Procedural textures for the delivery package (#410), numpy + Pillow, no Blender needed.

  python3 dh_package_tex.py [tex_dir]      (default: textures/ next to this file)

Same recipe as the street props (environment/street-props/dh_prop_tex.py): tileable surfaces
from periodic (FFT-filtered) noise drawn at real size, a tangent-space normal map derived from
height, fixed seeds, and PNGs saved with no metadata chunks, so a rebuild is byte-identical.

  package_cardboard_color.png    256 x 256, tileable kraft board: mottling, fibre streaks along V
  package_cardboard_normal.png   its normal map: fibre grain and a faint corrugation ripple along V
  package_label_color.png        256 x 160 plain shipping label: border, address bars, a barcode;
                                 no words, no logo, no brand (product-scope: nothing real-world)
"""
import os
import sys

import numpy as np
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
TEX_DIR = os.path.join(HERE, "textures")

CARD_PX = 256
CARD_SEED = 410
KRAFT = (196, 154, 104)          # #C49A68, kraft board
KRAFT_MOTTLE = 0.07              # +- brightness from low-frequency mottling
KRAFT_FIBRE = 0.025              # +- brightness from fibre streaks
CARD_RIPPLES = 16                # corrugation ripples per texture repeat (one repeat = 0.5 m)
CARD_RIPPLE_HEIGHT = 0.35
CARD_NORMAL_STRENGTH = 1.0

LABEL_W, LABEL_H = 256, 160
LABEL_PAPER = (243, 240, 232)    # #F3F0E8
LABEL_INK = (58, 60, 66)         # #3A3C42
LABEL_GREY = (150, 152, 156)
LABEL_SEED = 930
LABEL_BORDER = 6
# address bars: (x0, y0, x1, y1) in label pixels, top-left origin
LABEL_BARS = ((20, 22, 150, 34), (20, 44, 120, 54), (20, 62, 136, 72), (20, 80, 96, 90))
LABEL_BARCODE = (20, 108, 236, 146)
LABEL_RULE = (14, 99, 242, 101)


def _periodic_noise(shape, fy, fx, rng, power=2.0):
    """Tileable noise in [0, 1]: white noise low-passed in frequency space, with `fy` / `fx` the
    rough number of features per repeat along each axis."""
    h, w = shape
    white = rng.standard_normal((h, w))
    ky = np.fft.fftfreq(h)[:, None] * h / max(fy, 1e-6)
    kx = np.fft.fftfreq(w)[None, :] * w / max(fx, 1e-6)
    filt = 1.0 / (1.0 + (ky * ky + kx * kx) ** (power / 2))
    n = np.real(np.fft.ifft2(np.fft.fft2(white) * filt))
    n -= n.min()
    return n / max(n.max(), 1e-12)


def _normal_from_height(hh, strength):
    hh = hh * strength
    gx = (np.roll(hh, -1, 1) - np.roll(hh, 1, 1)) / 2
    gy = (np.roll(hh, -1, 0) - np.roll(hh, 1, 0)) / 2
    n = np.dstack([-gx, gy, np.ones_like(hh)])
    n /= np.linalg.norm(n, axis=2, keepdims=True)
    return Image.fromarray(np.round((n * 0.5 + 0.5) * 255).astype(np.uint8))


def _save(img, path):
    img.save(path, format="PNG", optimize=False)
    return path


def cardboard(tex_dir):
    rng = np.random.default_rng(CARD_SEED)
    s = CARD_PX
    mottle = _periodic_noise((s, s), 3, 3, rng)
    fibre = _periodic_noise((s, s), 24, 80, rng)          # streaks run along V (image rows)
    fine = _periodic_noise((s, s), 80, 80, rng)
    v = 1.0 + KRAFT_MOTTLE * 2 * (mottle - 0.5) + KRAFT_FIBRE * 2 * (fibre - 0.5)
    col = np.dstack([v * c for c in KRAFT])
    ripple = 0.5 + 0.5 * np.cos(2 * np.pi * CARD_RIPPLES * np.arange(s) / s)[:, None] * np.ones((1, s))
    height = CARD_RIPPLE_HEIGHT * ripple + 0.6 * fibre + 0.4 * fine
    c = _save(Image.fromarray(np.clip(np.round(col), 0, 255).astype(np.uint8)),
              os.path.join(tex_dir, "package_cardboard_color.png"))
    n = _save(_normal_from_height(height, CARD_NORMAL_STRENGTH), os.path.join(tex_dir, "package_cardboard_normal.png"))
    return c, n


def label(tex_dir):
    rng = np.random.default_rng(LABEL_SEED)
    im = np.zeros((LABEL_H, LABEL_W, 3), np.float64)
    im[:, :] = LABEL_PAPER
    b = LABEL_BORDER
    im[:b, :] = im[-b:, :] = im[:, :b] = im[:, -b:] = LABEL_GREY
    for x0, y0, x1, y1 in LABEL_BARS:
        im[y0:y1, x0:x1] = LABEL_GREY
    x0, y0, x1, y1 = LABEL_RULE
    im[y0:y1, x0:x1] = LABEL_INK
    x0, y0, x1, y1 = LABEL_BARCODE
    x = x0
    while x < x1:
        w = int(rng.integers(1, 4))
        if rng.random() < 0.6:
            im[y0:y1, x:min(x + w, x1)] = LABEL_INK
        x += w + int(rng.integers(1, 3))
    return _save(Image.fromarray(im.astype(np.uint8)), os.path.join(tex_dir, "package_label_color.png"))


def write_all(tex_dir=TEX_DIR):
    os.makedirs(tex_dir, exist_ok=True)
    c, n = cardboard(tex_dir)
    return {"cardboard": (c, n), "label": label(tex_dir)}


def paths(tex_dir=TEX_DIR):
    return {"cardboard": (os.path.join(tex_dir, "package_cardboard_color.png"),
                          os.path.join(tex_dir, "package_cardboard_normal.png")),
            "label": os.path.join(tex_dir, "package_label_color.png")}


if __name__ == "__main__":
    for k, v in write_all(sys.argv[1] if len(sys.argv) > 1 else TEX_DIR).items():
        print(k, v)
