"""Procedural tree textures (numpy + PIL): leaf-sprig atlases with alpha,
a cherry-blossom atlas, a spruce needle-spray atlas, and a tileable bark
colour + normal map. All atlases are 1024 x 1024 RGBA, edge-padded so
mip-mapping and alpha clipping don't show dark fringes.

Atlas layouts
  leaf / blossom atlases: 2 x 2 cells (512 px each), one sprig per cell;
      sprig grows from the bottom centre of its cell toward the top.
  spruce atlas: 4 rows of 1024 x 256 fronds; the frond runs left (trunk
      end) to right (tip).
"""
import math
import os
import random

import numpy as np
from PIL import Image, ImageDraw, ImageFilter

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(HERE, "textures")   # the committed leaf/blossom/needle atlases and bark maps
ATLAS = 1024
SS = 2                      # supersampling for anti-aliased edges


def _hex(h):
    h = h.lstrip("#")
    return tuple(int(h[i:i + 2], 16) for i in (0, 2, 4))


def _jitter(rgb, rng, amt=0.12, hue=6):
    k = 1 + rng.uniform(-amt, amt)
    dr, dg = rng.uniform(-hue, hue), rng.uniform(-hue, hue)
    r, g, b = rgb
    return (int(max(0, min(255, r * k + dr))), int(max(0, min(255, g * k + dg))), int(max(0, min(255, b * k))))


def _mix(a, b, t):
    return tuple(int(a[i] + (b[i] - a[i]) * t) for i in range(3))


def bleed(img, passes=6):
    """Fill RGB under transparent pixels with nearby leaf colour (push-pull),
    so filtering/mips never pull in black or white fringes."""
    a = np.asarray(img).astype(np.float32)
    rgb, al = a[..., :3], a[..., 3:4] / 255.0
    acc_rgb, acc_a = rgb * al, al.copy()
    out = rgb.copy()
    filled = al[..., 0] > 0.5
    pre = Image.fromarray((acc_rgb).clip(0, 255).astype(np.uint8))
    for r in (2, 4, 8, 16, 32, 64)[:passes]:
        br = np.asarray(Image.fromarray(acc_rgb.clip(0, 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(r))).astype(np.float32)
        ba = np.asarray(Image.fromarray((acc_a[..., 0] * 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(r))).astype(np.float32)[..., None] / 255.0
        est = br / np.maximum(ba, 1e-3)
        mask = (~filled) & (ba[..., 0] > 0.004)
        out[mask] = est[mask]
        filled = filled | mask
    out[~filled] = rgb[al[..., 0] > 0.5].mean(axis=0) if (al[..., 0] > 0.5).any() else 0
    res = np.concatenate([out.clip(0, 255), a[..., 3:4]], axis=-1).astype(np.uint8)
    return Image.fromarray(res, "RGBA")


# ---------------------------------------------------------------------------
# leaf shapes (unit leaf: base at (0,0), tip at (0,1), local coords)
# ---------------------------------------------------------------------------
def _lobed(lobes, width_deg, base_r, c_y, n=140):
    """Polar lobed outline around (0, c_y); angles measured from 'up'."""
    pts = []
    for i in range(n):
        th = -180 + 360 * i / n
        r = base_r
        for ang, L in lobes:
            k = max(0.0, 1 - abs(th - ang) / width_deg)
            r = max(r, base_r + (L - base_r) * k ** 1.6)
        if abs(th) > 150:                                 # stem sinus
            r = min(r, base_r * 0.75)
        a = math.radians(90 - th)
        pts.append((math.cos(a) * r, c_y + math.sin(a) * r))
    return pts


def leaf_outline(shape, rng):
    if shape == "maple":        # palmate, 5 pointed lobes (+ small basal pair)
        return _lobed([(-125, 0.3), (-80, 0.47), (-38, 0.55), (0, 0.6), (38, 0.55), (80, 0.47), (125, 0.3)],
                      24, 0.26, 0.4)
    if shape == "jmaple":       # 7 narrow, deep lobes
        return _lobed([(-120, 0.38), (-80, 0.5), (-40, 0.58), (0, 0.62), (40, 0.58), (80, 0.5), (120, 0.38)],
                      15, 0.13, 0.36)
    pts = []                    # ovate / narrow, finely serrated
    n = 32
    for i in range(n + 1):
        t = i / n
        w = 0.36 * math.sin(math.pi * t ** 0.85) * (1.0 if shape == "ovate" else 0.8)
        pts.append((w * (1.07 if i % 2 else 1.0), t))
    return pts + [(-x, y) for x, y in reversed(pts[1:-1])]


def draw_leaf(d, origin, angle, size, shape, base_rgb, rng, vein_rgb=None):
    ox, oy = origin
    ca, sa = math.cos(angle), math.sin(angle)
    tr = lambda p: (ox + (p[0] * ca - p[1] * sa) * size, oy - (p[0] * sa + p[1] * ca) * size)  # noqa: E731
    col = _jitter(base_rgb, rng)
    poly = [tr(p) for p in leaf_outline(shape, rng)]
    d.polygon(poly, fill=col + (255,))
    # soft two-tone: a lighter half on one side of the midrib
    light = _mix(col, (255, 255, 220), 0.07)
    half = [tr(p) for p in leaf_outline(shape, rng) if p[0] >= 0]
    if len(half) > 2:
        d.polygon(half, fill=light + (255,))
    vc = vein_rgb or _mix(col, (255, 255, 200), 0.22)
    w = max(1, int(size * 0.025))
    if shape in ("maple", "jmaple"):
        cy = 0.4 if shape == "maple" else 0.36
        d.line([tr((0, 0)), tr((0, cy))], fill=vc + (255,), width=w)
        angs = (-80, -38, 0, 38, 80) if shape == "maple" else (-120, -80, -40, 0, 40, 80, 120)
        for ang in angs:
            a = math.radians(90 - ang)
            L = 0.5 if shape == "maple" else 0.52
            d.line([tr((0, cy)), tr((math.cos(a) * L, cy + math.sin(a) * L))], fill=vc + (255,), width=max(1, w - 1))
    else:
        d.line([tr((0, 0)), tr((0, 0.92))], fill=vc + (255,), width=w)
    if shape not in ("maple", "jmaple"):
        for k in range(1, 7):
            y = k / 8
            for sgn in (-1, 1):
                d.line([tr((0, y)), tr((sgn * 0.26 * math.sin(math.pi * y), y + 0.1))], fill=vc + (255,),
                       width=max(1, w - 1))


def _stem(d, S, rng, col, width, segs=8, step=0.085):
    base = (S * 0.5, S * 0.985)
    pts, x, y, ang = [base], base[0], base[1], math.radians(90 + rng.uniform(-8, 8))
    for _ in range(segs):
        ang += math.radians(rng.uniform(-10, 10))
        x += math.cos(ang) * S * step
        y -= math.sin(ang) * S * step
        pts.append((x, y))
    d.line(pts, fill=col + (255,), width=width, joint="curve")
    return pts


def _region_points(S, rng, n, cx=0.5, cy=0.42, rx=0.36, ry=0.4, min_d=0.0):
    out = []
    tries = 0
    while len(out) < n and tries < n * 60:
        tries += 1
        a, r = rng.uniform(0, 2 * math.pi), math.sqrt(rng.random())
        x, y = cx + math.cos(a) * rx * r, cy + math.sin(a) * ry * r * (1.0 if math.sin(a) < 0 else 0.85)
        if all((x - p[0]) ** 2 + (y - p[1]) ** 2 > min_d ** 2 for p in out):
            out.append((x, y))
    return [(x * S, y * S) for x, y in out]


def sprig_cell(shape, greens, stem_rgb, rng, cell=512, n_leaves=(18, 24), leaf_size=(0.17, 0.23), pinnate=False):
    """A leafy sprig: a curved stem with twigs to leaves that fill a
    teardrop region of the cell (dense centre, open ragged edge)."""
    S = cell * SS
    img = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    pts = _stem(d, S, rng, stem_rgb, int(S * 0.013))
    nL = rng.randint(*n_leaves)
    reach = 0.47 - leaf_size[1] * 1.02            # keep every leaf inside its cell: no clipped edges
    spots = _region_points(S, rng, nL, cy=0.5, rx=reach, ry=reach, min_d=0.06 if not pinnate else 0.12)
    leaves = []
    for (lx, ly) in spots:
        k = min(range(len(pts)), key=lambda i: (pts[i][0] - lx) ** 2 + (pts[i][1] - ly) ** 2 + (i < 2) * 1e9)
        px, py = pts[k]
        d.line([(px, py), (lx, ly)], fill=stem_rgb + (255,), width=int(S * 0.006))
        out_ang = math.atan2(-(ly - py), lx - px) if (lx, ly) != (px, py) else math.pi / 2
        leaves.append(((lx, ly), out_ang - math.pi / 2 + rng.uniform(-0.35, 0.35), S * rng.uniform(*leaf_size)))
    for (o, a, sz) in sorted(leaves, key=lambda l: -l[0][1]):
        g = greens[rng.randrange(len(greens))]
        if pinnate:
            ca, sa = math.cos(a + math.pi / 2), math.sin(a + math.pi / 2)
            for q in range(7):
                t = 0.12 + q * 0.13
                cx, cy = o[0] + ca * sz * t, o[1] - sa * sz * t
                for sg in (-1, 1):
                    draw_leaf(d, (cx, cy), a + sg * 1.1, sz * 0.3, "narrow", g, rng)
            d.line([o, (o[0] + ca * sz, o[1] - sa * sz)], fill=stem_rgb + (255,), width=int(S * 0.005))
        else:
            draw_leaf(d, o, a, sz, shape, g, rng)
    return img.resize((cell, cell), Image.LANCZOS)


def blossom_cell(rng, cell=512):
    S = cell * SS
    img = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    stem = (92, 58, 48)
    base = (S * 0.5, S * 0.98)
    pts, x, y, ang = [base], base[0], base[1], math.radians(90 + rng.uniform(-8, 8))
    for _ in range(7):
        ang += math.radians(rng.uniform(-12, 12))
        x += math.cos(ang) * S * 0.1
        y -= math.sin(ang) * S * 0.1
        pts.append((x, y))
    d.line(pts, fill=stem + (255,), width=int(S * 0.014), joint="curve")
    pinks = [_hex("#F6B8CA"), _hex("#F0A2B9"), _hex("#FAD3DE"), _hex("#FBE3EA"), _hex("#EE93AE")]
    leafg = [_hex("#7E8C46"), _hex("#8A7A44")]
    centres = _region_points(S, rng, 90, cy=0.47, rx=0.38, ry=0.4, min_d=0.045)
    for (cx_, cy_) in centres[::3]:
        k = min(range(1, len(pts)), key=lambda i: (pts[i][0] - cx_) ** 2 + (pts[i][1] - cy_) ** 2)
        d.line([pts[k], (cx_, cy_)], fill=stem + (255,), width=int(S * 0.006))
    # a few bronze-green young leaves behind the flowers
    for _ in range(5):
        o = centres[rng.randrange(len(centres))]
        draw_leaf(d, o, rng.uniform(0, 6.28), S * 0.12, "ovate", leafg[rng.randrange(2)], rng)
    rng.shuffle(centres)
    for (cx, cy) in centres:
        col = pinks[rng.randrange(len(pinks))]
        R = S * rng.uniform(0.03, 0.042)
        ph = rng.uniform(0, 6.28)
        for p in range(5):
            a = ph + p * 2 * math.pi / 5
            px, py = cx + math.cos(a) * R * 0.62, cy + math.sin(a) * R * 0.62
            pc = _jitter(col, rng, 0.05, 3)
            d.ellipse((px - R * 0.55, py - R * 0.55, px + R * 0.55, py + R * 0.55), fill=pc + (255,))
            nx, ny = cx + math.cos(a) * R * 1.12, cy + math.sin(a) * R * 1.12      # notch
            d.ellipse((nx - R * 0.12, ny - R * 0.12, nx + R * 0.12, ny + R * 0.12), fill=(0, 0, 0, 0))
        d.ellipse((cx - R * 0.28, cy - R * 0.28, cx + R * 0.28, cy + R * 0.28), fill=_hex("#D9637F") + (255,))
        for s in range(6):
            a = s * 1.05 + ph
            d.ellipse((cx + math.cos(a) * R * 0.3 - 2, cy + math.sin(a) * R * 0.3 - 2, cx + math.cos(a) * R * 0.3 + 2,
                       cy + math.sin(a) * R * 0.3 + 2), fill=_hex("#F2D36B") + (255,))
    return img.resize((cell, cell), Image.LANCZOS)


def frond_cell(rng, w=1024, h=256):
    """Spruce frond: a twig left->right with side branchlets carrying
    needles; widest a third of the way out, tapering to the tip."""
    W, H = w * SS, h * SS
    img = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    twig = (86, 62, 44)
    darks = [_hex("#1F4A33"), _hex("#25553A"), _hex("#2C6143")]
    tips = [_hex("#4C8A55"), _hex("#5B9A5E")]
    cy = H * 0.5
    d.line([(0, cy), (W * 0.98, cy)], fill=twig + (255,), width=int(H * 0.04))

    def needles(x0, y0, x1, y1, L, dens, col_list, tipcol, width):
        n = int(math.hypot(x1 - x0, y1 - y0) / dens)
        dx, dy = (x1 - x0) / max(1, n), (y1 - y0) / max(1, n)
        ln = math.hypot(dx, dy) or 1
        ux, uy = dx / ln, dy / ln
        for i in range(n):
            px, py = x0 + dx * i, y0 + dy * i
            f = i / max(1, n)
            for sg in (-1, 1):
                a = math.radians(rng.uniform(40, 65)) * sg
                vx, vy = ux * math.cos(a) - uy * math.sin(a), ux * math.sin(a) + uy * math.cos(a)
                l = L * (0.8 + 0.4 * rng.random()) * (1 - 0.4 * f)
                c = tipcol if f > 0.75 and rng.random() < 0.7 else col_list[rng.randrange(len(col_list))]
                d.line([(px, py), (px + vx * l, py + vy * l)], fill=c + (255,), width=width)

    for i in range(16):
        f = (i + 0.5) / 16
        x = W * f * 0.95
        env = math.sin(math.pi * min(1.0, 0.1 + f * 0.95)) ** 0.7
        for sg in (-1, 1):
            L = H * 0.44 * env * rng.uniform(0.8, 1.0)
            ang = math.radians(rng.uniform(35, 55))
            x1, y1 = x + math.cos(ang) * L, cy + sg * math.sin(ang) * L
            d.line([(x, cy), (x1, y1)], fill=twig + (255,), width=int(H * 0.012))
            needles(x, cy, x1, y1, H * 0.075, H * 0.014, darks, tips[rng.randrange(2)], max(2, int(H * 0.011)))
    needles(0, cy, W * 0.98, cy, H * 0.09, H * 0.016, darks, tips[0], max(2, int(H * 0.012)))
    return img.resize((w, h), Image.LANCZOS)


def leaf_atlas(name, shape, greens, stem="#5A4632", seed=1, **kw):
    path = os.path.join(OUT, "%s.png" % name)
    rng = random.Random(seed)
    atlas = Image.new("RGBA", (ATLAS, ATLAS), (0, 0, 0, 0))
    for i in range(4):
        c = sprig_cell(shape, [_hex(g) for g in greens], _hex(stem), rng, **kw)
        atlas.alpha_composite(c, ((i % 2) * 512, (i // 2) * 512))
    bleed(atlas).save(path)
    return path


def blossom_atlas(name="cherry_blossom_atlas", seed=3):
    path = os.path.join(OUT, name + ".png")
    rng = random.Random(seed)
    atlas = Image.new("RGBA", (ATLAS, ATLAS), (0, 0, 0, 0))
    for i in range(4):
        atlas.alpha_composite(blossom_cell(rng), ((i % 2) * 512, (i // 2) * 512))
    bleed(atlas).save(path)
    return path


def spruce_atlas(name="spruce_needle_atlas", seed=4):
    path = os.path.join(OUT, name + ".png")
    rng = random.Random(seed)
    atlas = Image.new("RGBA", (ATLAS, ATLAS), (0, 0, 0, 0))
    for i in range(4):
        atlas.alpha_composite(frond_cell(rng), (0, i * 256))
    bleed(atlas).save(path)
    return path


def _periodic_noise(shape, scale_y, scale_x, rng, octaves=4):
    """Tileable fractal noise via random Fourier phases (periodic by construction)."""
    H, W = shape
    fy = np.fft.fftfreq(H)[:, None] * H
    fx = np.fft.fftfreq(W)[None, :] * W
    out = np.zeros(shape)
    for o in range(octaves):
        k = 2 ** o
        f = np.sqrt((fy / (scale_y * k)) ** 2 + (fx / (scale_x * k)) ** 2)
        amp = np.exp(-(f - 1) ** 2 * 2.5) / (k ** 0.9)
        ph = np.exp(2j * np.pi * np.random.default_rng(rng.randrange(1 << 30)).random(shape))
        out += np.real(np.fft.ifft2(amp * ph))
    out -= out.min()
    return out / out.max()


def bark(name="bark", base="#6B5242", dark="#3E2E24", light="#9A8471", seed=5, size=(512, 1024)):
    """Tileable bark (u around the trunk, v along it): vertical ridges and
    fissures + a matching tangent-space normal map."""
    W, H = size
    rng = random.Random(seed)
    ridges = _periodic_noise((H, W), 2.0, 6, rng, octaves=3)          # long vertical plates
    detail = _periodic_noise((H, W), 24, 40, rng, octaves=3)
    h = np.clip(ridges * 0.8 + detail * 0.3, 0, None)
    h = (h - h.min()) / (h.max() - h.min())
    fiss = np.clip((0.5 - h) / 0.3, 0, 1) ** 1.2                     # fissures where height is low
    b, dk, lt = (np.array(_hex(c), dtype=np.float32) for c in (base, dark, light))
    col = b[None, None] * (1 - fiss[..., None]) + dk[None, None] * fiss[..., None]
    col = col + (lt - b)[None, None] * np.clip((h - 0.7) / 0.3, 0, 1)[..., None] * 0.6
    col *= (0.92 + 0.16 * detail)[..., None]
    Image.fromarray(col.clip(0, 255).astype(np.uint8)).save(os.path.join(OUT, name + "_color.png"))
    hh = (h - 0.6 * fiss) * 9.0
    gx = (np.roll(hh, -1, 1) - np.roll(hh, 1, 1)) / 2
    gy = (np.roll(hh, -1, 0) - np.roll(hh, 1, 0)) / 2
    n = np.dstack([-gx, gy, np.ones_like(hh)])
    n /= np.linalg.norm(n, axis=2, keepdims=True)
    Image.fromarray(((n * 0.5 + 0.5) * 255).astype(np.uint8)).save(os.path.join(OUT, name + "_normal.png"))
    return os.path.join(OUT, name + "_color.png"), os.path.join(OUT, name + "_normal.png")


ATLASES = {
    "maple": dict(shape="maple", greens=["#4E8A3A", "#5F9A44", "#3F7A33", "#6AA24A"], seed=1, n_leaves=(22, 28),
                  leaf_size=(0.15, 0.2)),
    "street": dict(shape="ovate", greens=["#6FA246", "#7FB050", "#64983F", "#5C8F3A"], seed=6,
                   n_leaves=(26, 32), leaf_size=(0.12, 0.16)),
    "column": dict(shape="ovate", greens=["#3E6E35", "#4A7C3C", "#355F2E"], seed=7, n_leaves=(26, 32),
                   leaf_size=(0.12, 0.16)),
    "jmaple": dict(shape="jmaple", greens=["#A8342E", "#C0453A", "#8E2A28", "#CC5240"], stem="#4A2A22", seed=8,
                   n_leaves=(22, 28), leaf_size=(0.14, 0.18)),
}


def build_all():
    os.makedirs(OUT, exist_ok=True)
    out = {}
    for k, kw in ATLASES.items():
        kw = dict(kw)
        out[k] = leaf_atlas("%s_leaf_atlas" % k, kw.pop("shape"), kw.pop("greens"), **kw)
    out["cherry"] = blossom_atlas()
    out["spruce"] = spruce_atlas()
    out["bark"] = bark()
    out["bark_cherry"] = bark("bark_cherry", base="#5C3B33", dark="#33201C", light="#86625A", seed=9)
    out["bark_spruce"] = bark("bark_spruce", base="#5A4436", dark="#30241C", light="#80695A", seed=10)
    return out


if __name__ == "__main__":
    import time
    t = time.time()
    print(build_all(), "%.1fs" % (time.time() - t))
