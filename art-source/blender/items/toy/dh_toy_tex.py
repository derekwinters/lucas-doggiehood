"""The toy's one generated fabric texture pair (numpy + PIL, fixed seed).

  toy_fleece_color.png   near-white short-pile fleece, tinted per material by
                         the glTF baseColorFactor, so one image serves every
                         plush part
  toy_fleece_normal.png  its tangent-space normal map

Both tile seamlessly (periodic FFT noise) so the UVs can repeat them across the
seams of each part. Writes plain PNGs with no metadata chunks, so rebuilding
gives the same bytes.
"""
import os

import numpy as np
from PIL import Image

SIZE = 256                 # px per tile: the toy is small on screen, and its close-up still reads
SEED = 332                 # the issue number: one fixed seed, so the texture never changes by accident
PILE_FINE = 1.2            # px: blur of the fine fibre noise
PILE_CLUMP = 6.0           # px: blur of the soft clumps the pile lies in
CLUMP_MIX = 0.45           # share of the clump layer in the height field
SHADE_LO, SHADE_HI = 0.86, 1.0   # colour range: the tint shows through, darker in the pile's dips
NORMAL_STRENGTH = 2.2      # height-to-normal gain


def _periodic_noise(rng, size, blur_px):
    """Gaussian-blurred white noise, periodic in both axes (blurred in Fourier space)."""
    white = rng.standard_normal((size, size))
    f = np.fft.fftfreq(size)
    fx, fy = np.meshgrid(f, f)
    kernel = np.exp(-2.0 * (np.pi * blur_px) ** 2 * (fx ** 2 + fy ** 2))
    out = np.real(np.fft.ifft2(np.fft.fft2(white) * kernel))
    out -= out.min()
    return out / max(out.max(), 1e-12)


def height_field():
    rng = np.random.default_rng(SEED)
    fine = _periodic_noise(rng, SIZE, PILE_FINE)
    clump = _periodic_noise(rng, SIZE, PILE_CLUMP)
    h = (1.0 - CLUMP_MIX) * fine + CLUMP_MIX * clump
    h -= h.min()
    return h / max(h.max(), 1e-12)


def colour_image(h):
    v = SHADE_LO + (SHADE_HI - SHADE_LO) * h
    px = np.round(np.clip(v, 0, 1) * 255).astype(np.uint8)
    return Image.fromarray(np.stack([px, px, px], axis=-1), "RGB")


def normal_image(h):
    dx = (np.roll(h, -1, axis=1) - np.roll(h, 1, axis=1)) * 0.5 * NORMAL_STRENGTH
    dy = (np.roll(h, -1, axis=0) - np.roll(h, 1, axis=0)) * 0.5 * NORMAL_STRENGTH
    n = np.stack([-dx, dy, np.ones_like(h)], axis=-1)
    n /= np.linalg.norm(n, axis=-1, keepdims=True)
    px = np.round((n * 0.5 + 0.5) * 255).astype(np.uint8)
    return Image.fromarray(px, "RGB")


def write(tex_dir):
    """Write both PNGs into tex_dir; returns {"color": path, "normal": path}."""
    os.makedirs(tex_dir, exist_ok=True)
    h = height_field()
    paths = {"color": os.path.join(tex_dir, "toy_fleece_color.png"),
             "normal": os.path.join(tex_dir, "toy_fleece_normal.png")}
    colour_image(h).save(paths["color"], optimize=False)
    normal_image(h).save(paths["normal"], optimize=False)
    return paths


if __name__ == "__main__":
    import sys
    here = os.path.dirname(os.path.abspath(__file__))
    print(write(sys.argv[1] if len(sys.argv) > 1 else os.path.join(here, "textures")))
