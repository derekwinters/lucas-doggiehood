"""Dog textures for the active breed (coats, markings and trim colours from its breed.py),
generated with numpy + Pillow (no external images).

Round 3: the fur is baked from 3D onto the continuous skin's UVs, so it reads
the same on both sides of every UV seam and across the leg / neck / tail joins.

  <pre>_mask.png         R = strand detail, G = feathering (light), B = ear (dark)
  <pre>_<coat>_color.png one per coat (default, light, dark), from the mask
  <pre>_normal.png       tangent-space (OpenGL / glTF, +Y = +V), shared by all coats
  dog_trim_color.png    256 palette for the flat parts (eyes, nose, mouth, tongue, collar, tag)
pre = dog_fur (1024, the full model) or dog_fur_lod1 (512, the LOD's own unwrap).

How the fur is baked: every texel of a fur triangle gets its 3D position,
normal, UV tangent frame and the vertex's body-region weights (torso, head,
tail, legs, thigh, paw, ear). A periodic, anisotropic strand-noise tile (made
at real size, PPM pixels per metre) is sampled biplanar in 3D: for a fur flow
direction A (back and down on the body, down the legs, along the tail, down
the ears) the tile is projected on the two planes that contain A, weighted by
how squarely the surface faces each plane, so strands always run along A and
never get stretched across. Regions blend their flow directions by weight.
The normal map comes from the analytic 3D gradient of the same noise.

Usage: called by dh_dog.build(..., bake=True) (export_dog.py does this);
python3 dh_dog_tex.py <breed> only writes the trim palette (into <breed>/textures/).
"""
import os
import sys

import numpy as np
from PIL import Image

import dh_breed

HERE = os.path.dirname(os.path.abspath(__file__))
if __name__ == "__main__" and len(sys.argv) > 1:
    dh_breed.use(sys.argv[1])
B = dh_breed.get()
ATLAS = 1024
TRIM_PX = 256
PPM = 520                    # generation density, pixels per metre
SEED = 7031
PAD = 6                      # texels of bleed around every UV island

# UV layout: the skin is smart-projected into u in [0, SKIN_U]; the ears (both
# share one island, mirrored) use EAR_RECT. The trim faces use the trim palette.
SKIN_U = 0.84
ISLANDS = {"ear": ((0.855, 0.010, 0.995, 0.330), (0.42, 0.42))}
ATTRS = ["body", "head", "tail", "fleg", "bleg", "thigh", "paw", "ear"]
FLOW = B.FUR_FLOW        # fur flow direction per group (Blender axes), and the regions in each group

# sRGB hex: main coat, feathering (chest, belly, leg backs, tail underside), ears
COATS = B.COATS
MARK = B.FUR_MARKINGS     # where feathering and ear colour start / end (authored y or z)
TC = B.TRIM_COLOURS       # the trim palette
# The strand look. A breed may set any of these keys in its optional FUR_STRAND field; the defaults
# are the Golden's long coat. Per noise layer the blur across / along the strands (cm), then the
# layer mix (fine, mid, clump), the normal-map strength and the strand light/dark contrast.
STRAND = {"fine": (0.10, 0.8), "mid": (0.35, 2.0), "clump": (1.1, 3.5), "low": (6.0, 9.0),
          "mix": (0.40, 0.35, 0.35), "normal": 0.40, "contrast": 0.32}
STRAND.update(getattr(B, "FUR_STRAND", {}))
# Optional coat pattern: patches laid over the coat's ear/dark channel (a saddle, a mask),
# lightening it (an ear's inner face), or "pale" white markings over the feathering channel (a
# blaze, white legs, a tail tip). A breed without FUR_PATCHES bakes exactly as before.
PATCHES = list(getattr(B, "FUR_PATCHES", []))

# trim palette regions (u0, v0, u1, v1) in UV space
TRIM = {
    "eye":       (0.00, 0.50, 0.25, 0.75),
    "shine":     (0.25, 0.50, 0.50, 0.75),
    "nose":      (0.50, 0.50, 0.75, 0.75),
    "tag":       (0.75, 0.50, 1.00, 0.75),
    "mouth":     (0.00, 0.25, 0.25, 0.50),
    "tongue":    (0.25, 0.25, 0.50, 0.50),
    "collar":    (0.50, 0.25, 1.00, 0.50),
    "lip":       (0.00, 0.00, 0.25, 0.25),
}


def _hex(h):
    h = h.lstrip("#")
    return np.array([int(h[i:i + 2], 16) for i in (0, 2, 4)], dtype=np.float64) / 255.0


def _fft_blur(a, sig_x, sig_y):
    """Periodic Gaussian blur (sigmas in pixels) via FFT: wraps in both axes."""
    h, w = a.shape
    fy = np.fft.fftfreq(h)[:, None]
    fx = np.fft.fftfreq(w)[None, :]
    g = np.exp(-2 * (np.pi ** 2) * ((fx * sig_x) ** 2 + (fy * sig_y) ** 2))
    return np.real(np.fft.ifft2(np.fft.fft2(a) * g))


def _norm(a):
    a = a - a.mean()
    return a / (a.std() + 1e-9)


def strand_height(around_m, along_m, rng):
    """Height field for soft directional fur, x = around (U), y = along (V)."""
    w, h = max(16, int(round(around_m * PPM))), max(16, int(round(along_m * PPM)))
    px = PPM / 100.0                                       # pixels per cm
    S = STRAND
    fine = _norm(_fft_blur(rng.standard_normal((h, w)), S["fine"][0] * px, S["fine"][1] * px))
    mid = _norm(_fft_blur(rng.standard_normal((h, w)), S["mid"][0] * px, S["mid"][1] * px))
    clump = _norm(_fft_blur(rng.standard_normal((h, w)), S["clump"][0] * px, S["clump"][1] * px))
    low = _norm(_fft_blur(rng.standard_normal((h, w)), S["low"][0] * px, S["low"][1] * px))
    a, b, c = S["mix"]
    return a * fine + b * mid + c * clump, low


def _normal_from_height(hgt, strength):
    # rows go down the image = -V, so dh/dv = -d/drow
    dx = (np.roll(hgt, -1, 1) - np.roll(hgt, 1, 1)) * 0.5
    dy = -(np.roll(hgt, -1, 0) - np.roll(hgt, 1, 0)) * 0.5
    n = np.dstack([-dx * strength, -dy * strength, np.ones_like(hgt)])
    return n / np.linalg.norm(n, axis=2, keepdims=True)


def _resize(a, size, mode="F"):
    im = Image.fromarray(a.astype(np.float32), mode="F")
    return np.asarray(im.resize(size, Image.BICUBIC), dtype=np.float64)


def _smooth(e0, e1, x):
    t = np.clip((x - e0) / (e1 - e0), 0, 1)
    return t * t * (3 - 2 * t)


def coat_color(mask, coat):
    """Bake one coat from the mask. The same three-colour recipe could run in a
    Unity shader instead, so a coat becomes three colour parameters."""
    main, feath, earc = (_hex(h) for h in COATS[coat])
    det, f, e = mask[..., 0:1], mask[..., 1:2], mask[..., 2:3]
    col = main * (1 - f) + feath * f
    col = col * (1 - e) + earc * e
    shade = 1.0 - STRAND["contrast"] / 2 + STRAND["contrast"] * det       # strand light/dark
    col = col * shade
    # darker roots read as slightly more saturated: push warm in the dark strands
    col = col + (det - 0.5) * np.array([0.02, 0.0, -0.03])
    return (np.clip(col, 0, 1) * 255 + 0.5).astype(np.uint8)


def build_trim(texdir):
    S = TRIM_PX
    img = np.zeros((S, S, 3))
    img[:] = _hex("#808080")
    rng = np.random.default_rng(SEED + 1)

    def region(key):
        u0, v0, u1, v1 = TRIM[key]
        x0, x1 = int(u0 * S), int(u1 * S)
        y0, y1 = int((1 - v1) * S), int((1 - v0) * S)
        uu, vv = np.meshgrid((np.arange(x1 - x0) + 0.5) / (x1 - x0), 1 - (np.arange(y1 - y0) + 0.5) / (y1 - y0))
        return (slice(y0, y1), slice(x0, x1)), uu, vv

    sl, uu, vv = region("eye")         # v: 0 back, 1 front. Warm dark-brown iris ring, near-black pupil.
    iris, pupil = _hex(TC["iris"]), _hex(TC["pupil"])
    t = _smooth(0.55, 0.8, vv)[..., None]
    img[sl] = _hex(TC["eye"]) * (1 - t) + (iris * (1 - _smooth(0.86, 0.94, vv)[..., None])
                                          + pupil * _smooth(0.86, 0.94, vv)[..., None]) * t
    sl, uu, vv = region("shine")
    img[sl] = _hex(TC["shine"])
    sl, uu, vv = region("nose")         # leather: faint pebbling
    n = _fft_blur(rng.standard_normal(uu.shape), 0.8, 0.8)
    img[sl] = _hex(TC["nose"])[None, None] * (0.85 + 0.3 * _norm(n)[..., None] * 0.3)
    sl, uu, vv = region("tag")
    img[sl] = _hex(TC["tag"]) * (0.9 + 0.15 * vv[..., None])
    sl, uu, vv = region("mouth")
    img[sl] = _hex(TC["mouth"])
    sl, uu, vv = region("lip")          # black lip skin
    img[sl] = _hex(TC["lip"])
    sl, uu, vv = region("tongue")       # u 0.5 = top centre: a soft centre groove
    groove = 1 - 0.22 * np.exp(-((uu - 0.5) / 0.035) ** 2) * _smooth(0.1, 0.5, vv)
    img[sl] = _hex(TC["tongue"]) * groove[..., None]
    sl, uu, vv = region("collar")       # u around the strap section; stitching near both edges
    base = _hex(TC["collar"])
    edge = np.exp(-((np.minimum(uu, 1 - uu) - 0.06) / 0.012) ** 2)
    stitch = edge * (np.sin(vv * 2 * np.pi * 40) > 0.2)
    img[sl] = base * (1 - 0.18 * edge[..., None]) + _hex(TC["stitch"]) * 0.35 * stitch[..., None]
    Image.fromarray((np.clip(img, 0, 1) * 255 + 0.5).astype(np.uint8), "RGB").save(
        os.path.join(texdir, "dog_trim_color.png"))


def build_all(root):
    texdir = os.path.join(root, "textures")
    os.makedirs(texdir, exist_ok=True)
    build_trim(texdir)
    return texdir


# --------------------------------------------------------------------------
# 3D fur bake
# --------------------------------------------------------------------------
def _tile(rng):
    hgt, low = strand_height(1.0, 1.0, rng)            # 1 m x 1 m, periodic; rows = along, cols = across
    g_row = (np.roll(hgt, -1, 0) - np.roll(hgt, 1, 0)) * 0.5 * PPM
    g_col = (np.roll(hgt, -1, 1) - np.roll(hgt, 1, 1)) * 0.5 * PPM
    return hgt, low, g_row, g_col


def _samp(img, col, row):
    """Periodic bilinear sample; col/row in pixels."""
    h, w = img.shape
    c0 = np.floor(col).astype(np.int64)
    r0 = np.floor(row).astype(np.int64)
    fc, fr = col - c0, row - r0
    c0 %= w
    r0 %= h
    c1, r1 = (c0 + 1) % w, (r0 + 1) % h
    return ((img[r0, c0] * (1 - fc) + img[r0, c1] * fc) * (1 - fr) +
            (img[r1, c0] * (1 - fc) + img[r1, c1] * fc) * fr)


def _unit(v):
    return v / np.maximum(np.linalg.norm(v, axis=-1, keepdims=True), 1e-9)


def _biplanar(P, N, A, tile):
    hgt, low, g_row, g_col = tile
    A = np.asarray(A, float)
    A = A / np.linalg.norm(A)
    B1 = np.cross(A, [1.0, 0.0, 0.0])
    B1 /= np.linalg.norm(B1)
    B2 = np.cross(A, B1)
    h = np.zeros(len(P))
    lo = np.zeros(len(P))
    g = np.zeros((len(P), 3))
    w1 = np.abs(N @ B2) ** 3
    w2 = np.abs(N @ B1) ** 3
    ws = np.maximum(w1 + w2, 1e-6)
    for w, B, off in ((w1 / ws, B1, 0.0), (w2 / ws, B2, 0.37)):
        col = (P @ B + off) * PPM
        row = (P @ A + off * 0.61) * PPM
        h += w * _samp(hgt, col, row)
        lo += w * _samp(low, col, row)
        g += w[:, None] * (_samp(g_col, col, row)[:, None] * B + _samp(g_row, col, row)[:, None] * A)
    return h, lo, g


def rasterize(size, tris):
    """tris: dict P, N (n,3,3), UV (n,3,2), A (n,3,K). Per-texel position, normal,
    attributes and UV tangent frame; covered mask."""
    K = tris["A"].shape[2]
    out = {"P": np.zeros((size, size, 3)), "N": np.zeros((size, size, 3)), "A": np.zeros((size, size, K)),
           "T": np.zeros((size, size, 3)), "B": np.zeros((size, size, 3)), "m": np.zeros((size, size), bool)}
    for P, N, UV, A in zip(tris["P"], tris["N"], tris["UV"], tris["A"]):
        xy = np.stack([UV[:, 0] * size, (1 - UV[:, 1]) * size], 1)
        x0, y0 = np.floor(xy.min(0)).astype(int)
        x1, y1 = np.ceil(xy.max(0)).astype(int)
        x0, y0 = max(x0, 0), max(y0, 0)
        x1, y1 = min(x1, size - 1), min(y1, size - 1)
        if x1 < x0 or y1 < y0:
            continue
        gx, gy = np.meshgrid(np.arange(x0, x1 + 1) + 0.5, np.arange(y0, y1 + 1) + 0.5)
        (ax, ay), (bx, by), (cx, cy) = xy
        den = (by - cy) * (ax - cx) + (cx - bx) * (ay - cy)
        if abs(den) < 1e-12:
            continue
        l0 = ((by - cy) * (gx - cx) + (cx - bx) * (gy - cy)) / den
        l1 = ((cy - ay) * (gx - cx) + (ax - cx) * (gy - cy)) / den
        l2 = 1 - l0 - l1
        tol = -0.02
        ins = (l0 >= tol) & (l1 >= tol) & (l2 >= tol)
        if not ins.any():
            continue
        L = np.stack([l0[ins], l1[ins], l2[ins]], 1)
        L = np.clip(L, 0, None)
        L /= L.sum(1, keepdims=True)
        yy, xx = gy[ins].astype(int), gx[ins].astype(int)
        out["P"][yy, xx] = L @ P
        out["N"][yy, xx] = _unit(L @ N)
        out["A"][yy, xx] = L @ A
        e1, e2 = P[1] - P[0], P[2] - P[0]
        du1, dv1 = UV[1] - UV[0]
        du2, dv2 = UV[2] - UV[0]
        r = du1 * dv2 - du2 * dv1
        if abs(r) < 1e-14:
            continue
        out["T"][yy, xx] = (e1 * dv2 - e2 * dv1) / r
        out["B"][yy, xx] = (e2 * du1 - e1 * du2) / r
        out["m"][yy, xx] = True
    return out


def _dilate(arrs, m, iters):
    m = m.copy()
    for _ in range(iters):
        acc = [np.zeros_like(a) for a in arrs]
        cnt = np.zeros(m.shape)
        for dy, dx in ((0, 1), (0, -1), (1, 0), (-1, 0), (1, 1), (1, -1), (-1, 1), (-1, -1)):
            mm = np.roll(np.roll(m, dy, 0), dx, 1)
            cnt += mm
            for a, ac in zip(arrs, acc):
                sh = np.roll(np.roll(a, dy, 0), dx, 1)
                ac += sh * (mm[..., None] if a.ndim == 3 else mm)
        grow = (~m) & (cnt > 0)
        for a, ac in zip(arrs, acc):
            if a.ndim == 3:
                a[grow] = ac[grow] / cnt[grow][:, None]
            else:
                a[grow] = ac[grow] / cnt[grow]
        m |= grow
    return m


def fur_masks(P, N, A, low=None):
    """(feather, ear) per texel from authored position, normal and region weights (low: the coat's
    low-frequency noise, used only by FUR_PATCHES)."""
    x, y, z = P[:, 0], P[:, 1], P[:, 2]
    nx, ny, nz = N[:, 0], N[:, 1], N[:, 2]
    w = {k: A[:, i] for i, k in enumerate(ATTRS)}
    f = np.zeros(len(P))
    e = np.zeros(len(P))
    belly = 0.95 * _smooth(0.30, 0.80, -nz)
    bib = 0.90 * _smooth(0.15, 0.65, -ny) * _smooth(*MARK["bib_y"], y)
    rump = 0.35 * _smooth(0.3, 0.8, ny) * _smooth(*MARK["rump_y"], y)
    f += w["body"] * np.maximum.reduce([belly, bib, rump])
    muzzle = 0.80 * _smooth(*MARK["muzzle_y"], y) * (1 - 0.7 * _smooth(0.55, 0.92, nz))
    throat = 0.65 * _smooth(0.30, 0.80, -nz)
    f += w["head"] * np.maximum(muzzle, throat)
    e += w["head"] * 0.22 * _smooth(0.4, 0.8, nz) * _smooth(*MARK["crown_y"], y)
    legback = 0.90 * _smooth(0.15, 0.60, ny)
    pale = 0.35 * (1 - _smooth(*MARK["pale_z"], z))
    f += (w["fleg"] + w["bleg"]) * np.maximum(legback, pale)
    f += w["thigh"] * 0.90 * _smooth(0.05, 0.55, ny)
    f += w["paw"] * 0.45
    f += w["tail"] * 0.95 * _smooth(0.0, 0.5, -nz) * (0.2 + 0.8 * _smooth(*MARK["tail_tip_y"], y))
    e += w["ear"] * (1 - 0.25 * _smooth(*MARK["ear_tip_z"], z))
    if PATCHES:
        f, e = _patches(P, N, w, np.clip(f, 0, 1), np.clip(e, 0, 1), low)
    return np.clip(f, 0, 1), np.clip(e, 0, 1)


def _line_z(line, y):
    """A (y, z) polyline as z(y), held level past its ends."""
    ys, zs = zip(*sorted(line))
    return np.interp(y, ys, zs)


def _patches(P, N, w, f, e, low):
    """Apply the breed's FUR_PATCHES to the feathering channel f and the dark channel e. Each patch
    covers its regions' weights where the surface lies above (or below) a (y, z) line, where y and z ramp over (from, to), within
    |x| < x_max and where the normal's component n = (axis, from, to) ramps; its edge is `soft` m wide and moved up to
    `wobble` m by the coat's low-frequency noise. "dark" patches raise e to at least their
    strength; "light" ones scale e down by their strength; "pale" ones (white markings) raise f
    to at least their strength and scale e down by it, so the marking shows over a saddle.
    Returns (f, e); f is unchanged unless a patch is "pale"."""
    x, y, z = P[:, 0], P[:, 1], P[:, 2]
    lo = np.zeros(len(P)) if low is None else low
    for p in PATCHES:
        reg = sum(w[r] for r in p["regions"])
        s = p.get("soft", 0.03)
        cov = np.ones(len(P))
        if "line" in p:
            d = z + p.get("wobble", 0.0) * lo - _line_z(p["line"], y)
            cov *= _smooth(-s, s, d if p.get("above", True) else -d)
        for k, c in (("y", y), ("z", z)):
            if k in p:
                cov *= _smooth(p[k][0], p[k][1], c)
        if "x_max" in p:
            cov *= 1 - _smooth(p["x_max"] - s, p["x_max"] + s, np.abs(x))
        if "n" in p:
            ax, n0, n1 = p["n"]
            cov *= _smooth(n0, n1, N[:, "xyz".index(ax)])
        cov = np.clip(reg, 0, 1) * cov * p.get("strength", 1.0)
        op = p.get("op", "dark")
        if op == "dark":
            e = np.maximum(e, cov)
        elif op == "pale":
            f = np.maximum(f, cov)
            e = e * (1 - cov)
        else:
            e = e * (1 - cov)
    return f, e


def bake_fur(texdir, prefix, size, tris):
    """Bake mask, coat colours and normal map for one unwrap. tris as in rasterize()."""
    os.makedirs(texdir, exist_ok=True)
    tile = _tile(np.random.default_rng(SEED))
    R = rasterize(size, tris)
    m = R["m"]
    P, N, A, Tg, Bg = R["P"][m], R["N"][m], R["A"][m], R["T"][m], R["B"][m]
    A = A / np.maximum(A.sum(1, keepdims=True), 1e-9)
    hgt = np.zeros(len(P))
    low = np.zeros(len(P))
    grad = np.zeros((len(P), 3))
    for _g, (d, regions) in FLOW.items():
        wg = sum(A[:, ATTRS.index(r)] for r in regions)
        if wg.max() <= 0:
            continue
        h, lo, g = _biplanar(P, N, d, tile)
        hgt += wg * h
        low += wg * lo
        grad += wg[:, None] * g
    detail = np.clip(0.5 + 0.12 * hgt + 0.06 * low, 0, 1)
    gt = grad - N * (grad * N).sum(1, keepdims=True)
    nn = _unit(N - STRAND["normal"] / PPM * gt)
    Tt = _unit(Tg - N * (Tg * N).sum(1, keepdims=True))
    sign = np.sign((np.cross(N, Tt) * Bg).sum(1))
    sign[sign == 0] = 1
    Bt = np.cross(N, Tt) * sign[:, None]
    ts = np.stack([(nn * Tt).sum(1), (nn * Bt).sum(1), (nn * N).sum(1)], 1)
    feather, ear = fur_masks(P, N, A, low)
    img_d = np.full((size, size), 0.5)
    img_f = np.zeros((size, size))
    img_e = np.zeros((size, size))
    img_n = np.zeros((size, size, 3))
    img_n[..., 2] = 1
    img_d[m], img_f[m], img_e[m], img_n[m] = detail, feather, ear, ts
    _dilate([img_d, img_f, img_e, img_n], m, PAD + 4)
    img_n /= np.linalg.norm(img_n, axis=2, keepdims=True)
    mask = np.dstack([img_d, img_f, img_e])
    Image.fromarray((mask * 255 + 0.5).astype(np.uint8), "RGB").save(os.path.join(texdir, prefix + "_mask.png"))
    Image.fromarray(((img_n * 0.5 + 0.5) * 255 + 0.5).astype(np.uint8), "RGB").save(
        os.path.join(texdir, prefix + "_normal.png"))
    for coat in COATS:
        Image.fromarray(coat_color(mask, coat), "RGB").save(os.path.join(texdir, "%s_%s_color.png" % (prefix, coat)))
    return float(m.mean())


if __name__ == "__main__":
    print("textures ->", build_all(dh_breed.DIR))
