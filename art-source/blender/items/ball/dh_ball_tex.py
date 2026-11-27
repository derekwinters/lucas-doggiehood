"""The dog ball's numbers, seam curve and procedural textures (numpy + PIL, no bpy), for #333.

The ball is a tennis-style dog ball: two dumbbell-shaped felt pieces joined by one curved white
rubber seam. The seam is the classic tennis-ball curve on the unit sphere,

    x = a cos t + b cos 3t,   y = a sin t - b sin 3t,   z = 2 sqrt(ab) sin 2t,   a + b = 1,

which lies exactly on the sphere and splits it into two equal halves.

The textures are one equirectangular map pair (TEX_W x TEX_H) matching the UV sphere in
dh_ball.py: column x is longitude 2 pi (x + 0.5) / TEX_W, row y (top = 0) is latitude
pi (1 - (y + 0.5) / TEX_H) - pi / 2. The felt fuzz and mottling are evaluated in 3D on the ball
(sums of seeded sinusoids), so nothing seams at u = 0 / 1 and nothing stretches at the poles.
All texture detail (seam width, felt wavelengths, groove depth) is drawn for a real tennis ball
(REAL_DIAMETER_M); the game ball (DIAMETER_M) is the same ball scaled up, so its maps don't change.

The ball comes in the felt colours in VARIANTS, like the dogs' coats: every variant shares the mesh
and the normal map and has its own colour map.

  ball_felt_<variant>_color.png  felt colour (mottled, light fibre tufts) and the white seam
  ball_felt_normal.png           tangent-space (OpenGL, +Y = +V) normal map: felt fuzz and the seam
                                 groove, shared by every variant

Usage: python3 dh_ball_tex.py [outdir]   (default: textures/ next to this file)
"""
import math
import os
import sys

import numpy as np
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
TEX_DIR = os.path.join(HERE, "textures")

# --- the ball ---------------------------------------------------------------
REAL_DIAMETER_M = 0.067        # a real tennis ball (ITF 6.54 - 6.86 cm); the textures are drawn for this
DIAMETER_M = 0.25              # the game ball: about 3.7x real, picked by Derek for readability (see README)
TRI_BUDGET = 6000              # provisional per-mesh backstop (#799)

# --- colours (sRGB hex) -----------------------------------------------------
# The felt colours Derek picked from the palette renders (2026-11-27; not optic yellow or teal, which
# fade into the lawn). See README.md, "Colours". All share the white seam.
VARIANTS = {
    "blue": "#2F7FD8",
    "orange": "#F2711C",
    "red": "#D8302A",
    "magenta": "#E0358F",
    "purple": "#7A4FC9",
    "navy": "#24409A",
}
SEAM_HEX = "#EEEBE2"
NORMAL_TEXTURE = "ball_felt_normal.png"
COLOR_TEXTURE = "ball_felt_%s_color.png"
TEXTURES = tuple(COLOR_TEXTURE % k for k in VARIANTS) + (NORMAL_TEXTURE,)

# --- seam -------------------------------------------------------------------
SEAM_B = 0.27                  # the curve's b (a = 1 - b): how far the seam swings towards the poles
SEAM_SAMPLES = 1440            # points on the curve used for the distance field
SEAM_HALF_W_M = 0.0030         # half the visible white seam band, on the real-size ball
SEAM_HALF_ANGLE = SEAM_HALF_W_M / (REAL_DIAMETER_M / 2)
SEAM_EDGE = 0.012              # radians over which felt fades into the seam (anti-aliased edge)
GROOVE_DEPTH_MM = 0.7          # the seam sits below the felt nap

# --- texture ----------------------------------------------------------------
TEX_W, TEX_H = 1024, 512
SEED = 333
CHUNK = 32768                  # texels per distance-field chunk
FUZZ_WAVES = 64                # sinusoids in the fine fibre fuzz
FUZZ_LAMBDA_MM = (0.5, 1.6)    # fibre-fuzz wavelengths
MOTTLE_WAVES = 24
MOTTLE_LAMBDA_MM = (3.5, 11.0)
FUZZ_TONE = 0.05               # colour variation from the fuzz
MOTTLE_TONE = 0.035            # colour variation from the mottling
TUFT_LIGHTEN = 0.22            # how much lighter the brightest fibre tufts are
TUFT_THRESHOLD = 1.6           # fuzz value (in sigmas) above which a tuft lightens
FUZZ_HEIGHT_MM = 0.18
MOTTLE_HEIGHT_MM = 0.10
SEAM_GRAIN = 0.025             # faint rubber grain on the seam
NORMAL_STRENGTH = 1.0
POLE_COS_MIN = 0.05            # clamp on cos(latitude) when converting du to millimetres


def hex_rgb(h):
    h = h.lstrip("#")
    return np.array([int(h[i:i + 2], 16) for i in (0, 2, 4)], np.float64) / 255.0


# ---------------------------------------------------------------------------
# seam
# ---------------------------------------------------------------------------
def seam_points(n=SEAM_SAMPLES, b=SEAM_B):
    """n points evenly spaced in t on the seam curve, as unit vectors (n, 3)."""
    a = 1.0 - b
    t = np.arange(n) * (2 * math.pi / n)
    return np.stack([a * np.cos(t) + b * np.cos(3 * t), a * np.sin(t) - b * np.sin(3 * t),
                     2 * math.sqrt(a * b) * np.sin(2 * t)], 1)


def _nearest(d):
    """(angle to the seam, index of the nearest seam sample) for unit vectors d (n, 3)."""
    c = seam_points()
    best = np.empty(len(d))
    idx = np.empty(len(d), np.int64)
    for i in range(0, len(d), CHUNK):
        dots = d[i:i + CHUNK] @ c.T
        k = np.argmax(dots, 1)
        idx[i:i + CHUNK] = k
        best[i:i + CHUNK] = dots[np.arange(len(k)), k]
    return np.arccos(np.clip(best, -1.0, 1.0)), idx


def seam_angle(d):
    """Angle in radians from each unit vector in d (n, 3) to the seam."""
    return _nearest(np.asarray(d, np.float64))[0]


def seam_side(d):
    """+1 or -1 for each unit vector: which of the two felt pieces it lies on."""
    d = np.asarray(d, np.float64)
    _, k = _nearest(d)
    c = seam_points()
    tang = np.roll(c, -1, 0) - np.roll(c, 1, 0)
    bin_ = np.cross(c, tang)
    return np.sign(np.einsum("ij,ij->i", d, bin_[k]))


# ---------------------------------------------------------------------------
# textures
# ---------------------------------------------------------------------------
def texel_directions(w=TEX_W, h=TEX_H):
    """Unit vector on the ball for every texel centre, (h, w, 3), row 0 at the top (v = 1)."""
    lon = 2 * math.pi * (np.arange(w) + 0.5) / w
    lat = math.pi * (1.0 - (np.arange(h) + 0.5) / h) - math.pi / 2
    cl = np.cos(lat)[:, None]
    return np.stack([cl * np.cos(lon)[None, :], cl * np.sin(lon)[None, :],
                     np.repeat(np.sin(lat)[:, None], w, 1)], 2)


def _waves(rng, p, n, lam_mm):
    """Sum of n seeded 3D sinusoids on the ball surface, normalised to unit variance."""
    r_mm = REAL_DIAMETER_M * 500.0
    dirs = rng.normal(size=(n, 3))
    dirs /= np.linalg.norm(dirs, axis=1, keepdims=True)
    lam = np.exp(rng.uniform(math.log(lam_mm[0]), math.log(lam_mm[1]), n))
    k = dirs * (2 * math.pi * r_mm / lam)[:, None]
    ph = rng.uniform(0, 2 * math.pi, n)
    out = np.zeros(len(p))
    for i in range(n):
        out += np.sin(p @ k[i] + ph[i])
    return out * math.sqrt(2.0 / n)


def _smoothstep(e0, e1, x):
    t = np.clip((x - e0) / (e1 - e0), 0.0, 1.0)
    return t * t * (3 - 2 * t)


def _fields(w=TEX_W, h=TEX_H):
    """The seeded noise fields and the seam distance on every texel (shared by every variant)."""
    rng = np.random.default_rng(SEED)
    p = texel_directions(w, h).reshape(-1, 3)
    fuzz = _waves(rng, p, FUZZ_WAVES, FUZZ_LAMBDA_MM)
    mottle = _waves(rng, p, MOTTLE_WAVES, MOTTLE_LAMBDA_MM)
    grain = _waves(rng, p, FUZZ_WAVES // 2, FUZZ_LAMBDA_MM)
    ang = seam_angle(p)
    felt_w = _smoothstep(SEAM_HALF_ANGLE - SEAM_EDGE / 2, SEAM_HALF_ANGLE + SEAM_EDGE / 2, ang)
    return {"w": w, "h": h, "fuzz": fuzz, "mottle": mottle, "grain": grain, "ang": ang, "felt_w": felt_w}


def colour_map(f, felt_hex):
    """Colour map (h, w, 3) uint8 for one felt colour."""
    tuft = np.clip(f["fuzz"] - TUFT_THRESHOLD, 0.0, None)
    tuft -= tuft.mean()
    tone = 1.0 + FUZZ_TONE * f["fuzz"] + MOTTLE_TONE * f["mottle"]
    felt = hex_rgb(felt_hex)[None, :] * tone[:, None]
    felt = felt + (1.0 - felt) * (TUFT_LIGHTEN * tuft)[:, None]
    seam = hex_rgb(SEAM_HEX)[None, :] * (1.0 + SEAM_GRAIN * f["grain"])[:, None]
    col = seam + (felt - seam) * f["felt_w"][:, None]
    return np.clip(np.round(col * 255.0), 0, 255).astype(np.uint8).reshape(f["h"], f["w"], 3)


def normal_map(f):
    """The shared tangent-space normal map (h, w, 3) uint8."""
    w, h = f["w"], f["h"]
    groove = GROOVE_DEPTH_MM * np.exp(-(f["ang"] / SEAM_HALF_ANGLE) ** 2)
    height = (FUZZ_HEIGHT_MM * f["fuzz"] + MOTTLE_HEIGHT_MM * f["mottle"]) * f["felt_w"] - groove
    height = height.reshape(h, w)
    lat = math.pi * (1.0 - (np.arange(h) + 0.5) / h) - math.pi / 2
    r_mm = REAL_DIAMETER_M * 500.0
    dx = (2 * math.pi * r_mm / w) * np.maximum(np.cos(lat), POLE_COS_MIN)[:, None]
    dy = math.pi * r_mm / h
    dh_du = (np.roll(height, -1, 1) - np.roll(height, 1, 1)) / (2 * dx)
    up = np.vstack([height[:1], height[:-1]])          # row above = larger v
    down = np.vstack([height[1:], height[-1:]])
    dh_dv = (up - down) / (2 * dy)
    n = np.stack([-NORMAL_STRENGTH * dh_du, -NORMAL_STRENGTH * dh_dv, np.ones_like(height)], 2)
    n /= np.linalg.norm(n, axis=2, keepdims=True)
    return np.clip(np.round((n * 0.5 + 0.5) * 255.0), 0, 255).astype(np.uint8)


def _paths(outdir, variants):
    return {"normal": os.path.join(outdir, NORMAL_TEXTURE),
            "color": {k: os.path.join(outdir, COLOR_TEXTURE % k) for k in variants}}


def build(outdir=TEX_DIR, variants=VARIANTS):
    """Write every variant's colour map and the shared normal map into outdir.
    Returns {"normal": path, "color": {variant: path}}."""
    os.makedirs(outdir, exist_ok=True)
    f = _fields()
    p = _paths(outdir, variants)
    Image.fromarray(normal_map(f), "RGB").save(p["normal"])
    for k, h in variants.items():
        Image.fromarray(colour_map(f, h), "RGB").save(p["color"][k])
    return p


def paths(outdir=TEX_DIR):
    """The texture paths in outdir, building them first if any is missing."""
    p = _paths(outdir, VARIANTS)
    if not all(os.path.isfile(v) for v in [p["normal"]] + list(p["color"].values())):
        return build(outdir)
    return p


def variant(tex, key):
    """{"color": path, "normal": path} of one variant, from paths() or build()."""
    return {"color": tex["color"][key], "normal": tex["normal"]}


if __name__ == "__main__":
    out = build(sys.argv[1] if len(sys.argv) > 1 else TEX_DIR)
    print("\n".join([out["normal"]] + list(out["color"].values())))
