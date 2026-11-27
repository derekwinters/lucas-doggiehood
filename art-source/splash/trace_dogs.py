#!/usr/bin/env python3
"""Doggiehood splash dogs, vector-traced from the original art (the default dogs).

Each dog is cut out of `Assets/Art/Splash/cover-art.png` (read-only) with a precise alpha
mask, upscaled, flattened to a small per-dog palette and traced to smooth vector paths
with VTracer. The traced paths are grouped by colour family, so a part can still be
recoloured on its own:

    <dog>-outline         the dark cartoon outline and inner line work (the bottom shape
                          is the whole silhouette, so the outline edge is the mask edge)
    <dog>-fur-base, <dog>-fur-shade, <dog>-fur-deep, <dog>-fur-light, <dog>-white,
    <dog>-collar / <dog>-bandana, <dog>-tag, <dog>-mouth, <dog>-tongue, <dog>-ears-inner,
    <dog>-eyes-nose, <dog>-spots, <dog>-paw-pads, <dog>-shine      (whichever the dog has)

Pipeline (all deterministic, so a rebuild is byte-identical):

1. Mask. The hand-drawn silhouettes in build_dogs.py give a rough region per dog. Inside
   it, the dog is separated from the street either by flooding the background in from
   outside until it meets the dog's dark outline ("ink"), or, for dogs whose outline is
   broken or close to the background's colours, by GrabCut seeded from the silhouette.
   Dogs are cut front to back and each cut is removed from the dogs behind it, so no
   neighbouring dog's pixels leak in. A few hand-placed fixes (MASK_FIXES) patch spots
   where the original's outline is ambiguous.
2. Upscale the crop TRACE_SCALE x (Lanczos), flatten painted texture with mean-shift
   filtering, and quantize to the dog's palette (deterministic k-means in Lab).
3. Smooth the label map (blurred one-hot argmax), so regions have clean edges and no
   speckles, then trace each colour with VTracer as a stacked layer: every layer is the
   union of its colour and every colour above it, so neighbouring shapes overlap rather
   than leave hairline seams.
4. Hidden parts: the schnauzer's rump is hidden behind the golden retriever. It is
   completed by hand (SCHNAUZER_HIDDEN) in the traced palette, drawn only where the
   golden covers it, so the composite is unchanged and the standalone dog is whole.

The dog shadows are traced the same way from the original's road shadows (pixels clearly
darker than the rebuilt street under them), as one translucent layer.

`build_scene.py` writes everything; run `python3 art-source/splash/build_scene.py`.
"""
from __future__ import annotations

import io
import re
from functools import lru_cache

import numpy as np
from PIL import Image

import build_title as bt

fmt = bt.fmt

TRACE_SCALE = 4            # crops are traced at this multiple of the original's pixels
MEANSHIFT = (5, 12)        # mean-shift spatial / colour radius (flattens painted texture)
LABEL_SIGMA = 0.55         # blur of the one-hot label map before argmax (original px)
MASK_SIGMA = 1.0           # blur of the silhouette mask before thresholding (original px)
SPECKLE = 1.25             # VTracer filter_speckle: drop patches under SPECKLE^2 original px
CROP_PAD = 6               # original px around each dog's mask in its trace crop
ROUGH_GROW = 22            # how far beyond the hand silhouette a dog may extend (px)
TUCK_PX = 4                # a dog runs this far under a dog in front of it (original px)
SNAP_PX = 4                # GrabCut masks grow this far into adjoining outline ink (px)
PINHOLE = 40               # holes in a dog's mask smaller than this (px) are filled
ROUGH_SURE = 12            # erosion of the hand silhouette that is certainly dog (px)
EDGE_GRAD = 24.0           # Sobel magnitude of L (trace px) counted as an edge
TRANSITION_FRAC = 0.55     # a colour this often on edges is an anti-aliasing artefact
EDGE_BAND = 1.5            # original px inside the silhouette edge drawn as outline
DEBUG = {}
CHECK_TOL = 2              # mask check tolerance, in 2x render px

# Front to back: each dog's mask is removed from every dog behind it.
FRONT_TO_BACK = ["dog-golden", "dog-dalmatian", "dog-corgi", "dog-puppy", "dog-schnauzer"]

# Per dog: how the mask is cut, palette size, and the colour families. A palette colour is
# put in the family whose reference colour is nearest (in Lab). Families are stacked in the
# order listed; the first is the outline (the whole silhouette is filled with it).
DOGS = {
    "dog-golden": dict(method="ink", ink="warm", k=18, extra=["#ffffff"], families=[
        ("outline", ["#95340c", "#813419"]),
        ("fur-base", ["#fdb64b", "#fec654"]),
        ("fur-shade", ["#ef973e", "#dc7d36", "#e69752"]),
        ("fur-deep", ["#b8571e"]),
        ("fur-light", ["#fee071", "#f5ca78"]),
        ("bandana", ["#ee292a", "#ce1525", "#9d0c14", "#810c13"]),
        ("mouth", ["#5c201f"]),
        ("tongue", ["#ee6660"]),
        ("eyes-nose", ["#271d29", "#8b7170"]),
        ("shine", ["#ffffff"]),
    ]),
    "dog-dalmatian": dict(method="ink", ink="cool", k=14, families=[
        ("outline", ["#1e1a2b", "#32304d", "#3c3849", "#1a230e"]),
        ("white", ["#fefbf2", "#e3dade"]),
        ("fur-shade", ["#c9bcc8", "#b6a8b6"]),
        ("fur-deep", ["#807886", "#5a545f"]),
        ("collar", ["#e32329", "#730b16"]),
        ("mouth", ["#491420"]),
        ("tongue", ["#e0575e"]),
        ("spots", []),
        ("eyes-nose", []),
    ], split={"eyes-nose": [[(1985, 858), (2112, 858), (2112, 952), (1985, 952)]], "rest": "spots"}),
    "dog-corgi": dict(method="grabcut", ink="warm", drop_blue=True, k=18, extra=["#ffffff"], families=[
        ("outline", ["#882809"]),
        ("fur-base", ["#f78722"]),
        ("fur-shade", ["#d9651e", "#df924a"]),
        ("fur-deep", ["#b3370d", "#a5654a"]),
        ("fur-light", ["#f9a32e", "#fdbf39"]),
        ("white", ["#fde9c6"]),
        ("white-shade", ["#ecba9f"]),
        ("ears-inner", ["#fba195", "#f0868c"]),
        ("collar", ["#2280ce", "#093777"]),
        ("mouth", ["#641d17"]),
        ("tongue", ["#e55f61"]),
        ("eyes-nose", ["#221a27", "#6e6472"]),
        ("shine", ["#ffffff"]),
    ]),
    "dog-puppy": dict(method="grabcut", ink="warm", k=16, transition=0.9, edge_band=0.6, label_sigma=0.5,
                      scale=8, meanshift=(9, 16),
                      extra=["#e9787c", "#2e1610"], families=[
        ("outline", ["#7f3311", "#4d200f", "#755614"]),
        ("fur-base", ["#d37a39", "#df8b47"]),
        ("fur-shade", ["#b96031", "#c18762"]),
        ("fur-deep", ["#8f4e33"]),
        ("fur-light", ["#eaa158", "#f7c069"]),
        ("white", ["#fcdfab", "#fdebc0"]),
        ("white-shade", ["#e8b789"]),
        ("collar", ["#2d528f"]),
        ("tongue", ["#e9787c"]),
        ("eyes-nose", ["#2e1610"]),
    ], split={"eyes-nose": [[(720, 848), (812, 848), (812, 896), (720, 896)]]}),
    "dog-schnauzer": dict(method="grabcut", ink="cool", drop_blue=True, k=16, transition=0.6, extra=["#ffffff"], families=[
        ("outline", ["#3d343c", "#29222a", "#292b1e"]),
        ("fur-base", ["#73686f", "#867c83"]),
        ("fur-shade", ["#594f59"]),
        ("white", ["#dacdca", "#b8a8a8"]),
        ("white-shade", ["#a09290"]),
        ("ears-inner", ["#814c58"]),
        ("collar", ["#a62012", "#6e2011"]),
        ("tag", ["#eca21d"]),
        ("mouth", ["#3e1513"]),
        ("tongue", ["#da5e6b", "#ab4150"]),
        ("eyes-nose", []),
        ("shine", ["#ffffff"]),
    ], split={"eyes-nose": [[(1155, 795), (1237, 795), (1237, 862), (1155, 862)]]},
        # a speck of pale ground showing between the golden's ear and the schnauzer's back
        fixes=[("cut-pale", [(1312, 850), (1331, 850), (1331, 869), (1312, 869)])]),
}


# --------------------------------------------------------------------------
# Colour helpers
# --------------------------------------------------------------------------
def to_lab(rgb_u8):
    import cv2
    return cv2.cvtColor((np.asarray(rgb_u8, np.float32) / 255.0), cv2.COLOR_RGB2Lab)


def hex_lab(h):
    return to_lab(np.array([[[int(h[i:i + 2], 16) for i in (1, 3, 5)]]], np.uint8))[0, 0]


def disk(r):
    y, x = np.mgrid[-r:r + 1, -r:r + 1]
    return (x * x + y * y) <= r * r


@lru_cache(maxsize=1)
def original():
    return np.asarray(Image.open(bt.ORIGINAL).convert("RGB"))


# --------------------------------------------------------------------------
# 1. Masks
# --------------------------------------------------------------------------
def hand_silhouette(dog):
    """The hand-drawn dog's silhouette (with its outline) as a full-frame bool mask."""
    import build_dogs as bd
    import build_scene as bs
    fn = bd.dog_fns()[dog]
    _, ink, groups = bd.dog_parts(fn)
    sil = [p for _, parts in groups for p in parts if p[3].get("sil")]
    body = "".join(bd.draw_part(p, "#000000", dog, outline=True) for p in sil)
    a = np.asarray(bt.png_image(bt.render(bs.layer_svg(dog, body), bs.W)))[..., 3]
    return a > 127


def ink_map(kind, lab):
    """The dog's dark outline colour: warm brown for the warm-coated dogs, near-black
    grey/navy for the others (kept clear of the bluish road shadows)."""
    L, a, b = lab[..., 0], lab[..., 1], lab[..., 2]
    if kind == "warm":
        return (a > 15) & (b > 12) & (L < 52)
    return (L < 28) | ((L < 34) & (b > -10))


def flood_cut(rough, sure, ink, walls):
    """Flood the background in from the rough region's border, stopping at the ink."""
    from scipy import ndimage as ndi
    passable = rough & ~ink & ~sure
    seeds = passable & ~ndi.binary_erosion(rough | walls, disk(1))
    lbl, _ = ndi.label(passable)
    bg = np.isin(lbl, np.setdiff1d(np.unique(lbl[seeds]), [0]))
    return rough & ~bg


def grabcut_cut(rough, hand, sure, walls):
    import cv2
    o = original()
    ys, xs = np.nonzero(rough)
    x0, x1, y0, y1 = xs.min() - 4, xs.max() + 5, ys.min() - 4, ys.max() + 5
    tri = np.full(rough.shape, cv2.GC_BGD, np.uint8)
    tri[rough] = cv2.GC_PR_BGD
    tri[hand] = cv2.GC_PR_FGD
    tri[sure] = cv2.GC_FGD
    tri[walls] = cv2.GC_BGD
    t = np.ascontiguousarray(tri[y0:y1, x0:x1])
    img = np.ascontiguousarray(o[y0:y1, x0:x1, ::-1])
    cv2.setRNGSeed(0)
    cv2.grabCut(img, t, None, np.zeros((1, 65)), np.zeros((1, 65)), 8, cv2.GC_INIT_WITH_MASK)
    m = np.zeros(rough.shape, bool)
    m[y0:y1, x0:x1] = (t == cv2.GC_FGD) | (t == cv2.GC_PR_FGD)
    return m


def poly_mask(shape, pts):
    from PIL import ImageDraw
    im = Image.new("L", (shape[1], shape[0]), 0)
    ImageDraw.Draw(im).polygon([tuple(p) for p in pts], fill=255)
    return np.asarray(im) > 0


@lru_cache(maxsize=1)
def dog_masks():
    """Full-frame bool mask per dog, in the original's pixel space."""
    from scipy import ndimage as ndi
    lab = to_lab(original())
    walls = np.zeros(lab.shape[:2], bool)
    masks = {}
    for dog in FRONT_TO_BACK:
        cfg = DOGS[dog]
        hand = hand_silhouette(dog)
        rough = ndi.binary_dilation(hand, disk(ROUGH_GROW)) & ~walls
        sure = ndi.binary_erosion(hand, disk(ROUGH_SURE))
        if cfg["method"] == "ink":
            m = flood_cut(rough, sure, ink_map(cfg["ink"], lab), walls)
        else:
            m = grabcut_cut(rough, hand, sure, walls)
            if cfg.get("drop_blue"):
                # street-coloured (bluish) pockets GrabCut kept, e.g. road or shadow seen
                # between the legs (no fur on these dogs is that blue; collars are bluer and
                # noses darker)
                b = lab[..., 2]
                L = lab[..., 0]
                m &= ~((b < -9) & (b > -30) & (lab[..., 1] > 2) & (L > 28) & (L < 70))
            # GrabCut can stop just inside the outline: grow into the adjoining ink
            ink = ink_map(cfg["ink"], lab) & rough
            for _ in range(SNAP_PX):
                m |= ndi.binary_dilation(m) & ink
        m &= ~walls
        m = ndi.binary_opening(m, disk(1))
        lbl, n = ndi.label(m)
        sizes = ndi.sum(m, lbl, range(1, n + 1))
        m = np.isin(lbl, [i + 1 for i, s in enumerate(sizes) if s > 200])
        # pinholes (a stray pixel or two read as background) are filled; real gaps such as
        # road seen between the legs are much larger and stay open
        holes, n = ndi.label(ndi.binary_fill_holes(m) & ~m)
        sizes = ndi.sum(holes > 0, holes, range(1, n + 1))
        m |= np.isin(holes, [i + 1 for i, s in enumerate(sizes) if s < PINHOLE])
        for op, pts in cfg.get("fixes", []):
            p = poly_mask(m.shape, pts)
            if op == "cut-pale":          # only the pale (ground-coloured) pixels
                p &= lab[..., 0] > 70
            m = ((m | p) & ~walls) if op == "add" else (m & ~p)
        masks[dog] = m
        walls |= m
    return masks


# --------------------------------------------------------------------------
# 2-3. Quantize and trace
# --------------------------------------------------------------------------
def kmeans_lab(px, k, min_dist=9.0, iters=16):
    """Deterministic k-means: seeds are the most frequent colours, kept min_dist apart."""
    q = np.clip(px / np.array([100.0, 256.0, 256.0]) * 24 + np.array([0, 12, 12]), 0, 23).astype(int)
    key = (q[:, 0] * 24 + q[:, 1]) * 24 + q[:, 2]
    vals, inv, cnt = np.unique(key, return_inverse=True, return_counts=True)
    sums = np.zeros((len(vals), 3))
    np.add.at(sums, inv, px)
    means = sums / cnt[:, None]
    cents = []
    for i in np.argsort(-cnt, kind="stable"):
        if all(np.linalg.norm(means[i] - c) > min_dist for c in cents):
            cents.append(means[i])
        if len(cents) == k:
            break
    c = np.array(cents)
    sub = px[:: max(1, len(px) // 200000)]
    for _ in range(iters):
        lab_ = ((sub[:, None, :] - c[None]) ** 2).sum(-1).argmin(1)
        c = np.array([sub[lab_ == j].mean(0) if (lab_ == j).any() else c[j] for j in range(len(c))])
    return c


def smooth_labels(labels, n, inside, sigma):
    import cv2
    best = np.full(labels.shape, -1, np.int32)
    score = np.full(labels.shape, -1.0, np.float32)
    for j in range(n):
        s = cv2.GaussianBlur((labels == j).astype(np.float32), (0, 0), sigma)
        upd = s > score
        best[upd] = j
        score[upd] = s[upd]
    best[~inside] = -1
    return best


def vtrace(binary, scale=TRACE_SCALE):
    """Trace a bool image (True = shape) to SVG path data in its own pixel space."""
    import vtracer
    buf = io.BytesIO()
    Image.fromarray(np.where(binary, 0, 255).astype(np.uint8)).save(buf, "PNG")
    svg = vtracer.convert_raw_image_to_svg(
        buf.getvalue(), img_format="png", colormode="binary", mode="spline",
        filter_speckle=max(1, round(SPECKLE * scale)), corner_threshold=60, length_threshold=4.0,
        splice_threshold=45, path_precision=3)
    out = []
    for d, tx, ty in re.findall(r'<path d="([^"]+)" fill="[^"]+" transform="translate\(([-\d.]+),([-\d.]+)\)"', svg):
        assert set(re.findall(r"[A-Za-z]", d)) <= {"M", "C", "Z"}, "unexpected path command"
        out.append((d, float(tx), float(ty)))
    return out


def to_original(d, tx, ty, x0, y0, s):
    """Rewrite traced path data from trace-crop px into the original's pixel space."""
    toks = re.findall(r"[MCZ]|-?\d+(?:\.\d+)?", d)
    out, i = [], 0
    while i < len(toks):
        t = toks[i]
        if t in "MCZ":
            out.append(t)
            i += 1
            continue
        x, y = float(toks[i]), float(toks[i + 1])
        out.append(f"{fmt((x + tx) / s + x0)} {fmt((y + ty) / s + y0)}")
        i += 2
    return " ".join(out)


def hexc(rgb):
    return "#%02x%02x%02x" % tuple(int(v) for v in np.clip(np.round(rgb), 0, 255))


def regions(dog, up, flat, inside, box, tuck, front_near):
    """Split the dog into flat-coloured regions: [(family index, rgb, bool mask)].

    `tuck` is the strip that carries the dog a little way under a dog in front of it (so
    no hairline gap opens between them); it takes the colour of the nearest own pixel.
    `front_near` is where the dog borders a dog in front: no outline is drawn there."""
    from scipy import ndimage as ndi
    cfg = DOGS[dog]
    lab = to_lab(flat)
    cents = kmeans_lab(lab[inside & ~tuck], cfg["k"])
    extra = [hex_lab(h) for h in cfg.get("extra", [])]
    if extra:
        cents = np.vstack([cents, extra])
    dist = ((lab[..., None, :] - cents[None, None]) ** 2).sum(-1)
    labels = dist.argmin(-1)
    # transitional colours: clusters that live almost only on the anti-aliased edges
    # between two other colours. Their pixels go to the nearest remaining colour.
    import cv2
    g = cv2.GaussianBlur(lab[..., 0], (0, 0), 1.5)
    grad = np.hypot(cv2.Sobel(g, cv2.CV_32F, 1, 0), cv2.Sobel(g, cv2.CV_32F, 0, 1))
    edgy = grad > EDGE_GRAD
    trans = []
    for j in range(len(cents) - len(extra)):
        sel = (labels == j) & inside
        if sel.any() and edgy[sel].mean() > cfg.get("transition", TRANSITION_FRAC):
            trans.append(j)
    DEBUG.setdefault(dog, {})["edge_frac"] = [
        (hexc(up[(labels == j) & inside].mean(0)), round(float(edgy[(labels == j) & inside].mean()), 2))
        if ((labels == j) & inside).any() else None
        for j in range(len(cents))]
    DEBUG[dog]["trans"] = trans
    if trans:
        dist[..., trans] = np.inf
        labels = dist.argmin(-1)
    s = cfg.get("scale", TRACE_SCALE)
    labels = smooth_labels(labels, len(cents), inside, cfg.get("label_sigma", LABEL_SIGMA) * s)
    if tuck.any():
        _, (iy, ix) = ndi.distance_transform_edt(tuck | ~inside, return_indices=True)
        labels[tuck] = labels[iy[tuck], ix[tuck]]
    fams = cfg["families"]
    names = [f[0] for f in fams]
    refs = [(fi, hex_lab(h)) for fi, (_, hs) in enumerate(fams) for h in hs]
    fam_of = [min(refs, key=lambda r: float(np.linalg.norm(r[1] - c)))[0] for c in cents]
    out = []
    # the outline family is one colour; enclosed dark shapes can be split off by place
    ink = np.isin(labels, [j for j in range(len(cents)) if fam_of[j] == 0])
    band = inside & ~ndi.binary_erosion(inside, disk(round(cfg.get("edge_band", EDGE_BAND) * s)))
    band &= ~front_near
    ink |= band
    for j in range(len(cents)):
        labels[band & (labels == j)] = -1
    edge = inside & ~ndi.binary_erosion(inside, disk(3))
    comp, n = ndi.label(ink, structure=np.ones((3, 3)))
    on_edge = set(np.unique(comp[edge & ink]).tolist())
    split = cfg.get("split", {})
    moved = {}
    for i in range(1, n + 1):
        if i in on_edge:
            continue
        c = comp == i
        yy, xx = np.nonzero(c)
        cx, cy = xx.mean() / s + box[0], yy.mean() / s + box[1]
        target = None
        for fam, polys in split.items():
            if fam == "rest":
                continue
            if any(point_in(p, cx, cy) for p in polys):
                target = fam
                break
        if target is None and split.get("rest") and compact(yy, xx):
            target = split["rest"]
        if target:
            moved.setdefault(names.index(target), np.zeros_like(ink))
            moved[names.index(target)] |= c
            ink &= ~c
    out.append((0, colour(up, ink & ~tuck & ~band, ink), ink))
    for fi, mm in moved.items():
        out.append((fi, colour(up, mm & ~tuck, mm), mm))
    for j in range(len(cents)):
        if fam_of[j] == 0:
            continue
        mm = labels == j
        if mm.any():
            out.append((fam_of[j], colour(up, mm & ~tuck, mm), mm))
    return out


def colour(up, own, fallback):
    """Mean colour of a region's own pixels (not the strip tucked under another dog)."""
    sel = own if own.any() else fallback
    return up[sel].mean(0)


def compact(yy, xx):
    """A blob rather than a line: its narrow axis is at least a third of its long axis."""
    if len(xx) < 40:
        return False
    ev = np.linalg.eigvalsh(np.cov(np.vstack([xx, yy]).astype(float)))
    return ev[0] > 0 and ev[0] / ev[1] > 0.11


def point_in(poly, x, y):
    """Even-odd point-in-polygon test."""
    inside, n = False, len(poly)
    for i in range(n):
        (xa, ya), (xb, yb) = poly[i], poly[(i + 1) % n]
        if (ya > y) != (yb > y) and x < xa + (y - ya) * (xb - xa) / (yb - ya):
            inside = not inside
    return inside


def kernel(r):
    return disk(int(r)).astype(np.uint8)


def front_of(dog):
    """Union of the masks of the dogs standing in front of `dog`."""
    masks = dog_masks()
    out = np.zeros_like(masks[dog])
    for d in FRONT_TO_BACK[:FRONT_TO_BACK.index(dog)]:
        out |= masks[d]
    return out


@lru_cache(maxsize=None)
def trace(dog):
    """(crop box, [(family, [(hex, path d)])], trace-scale crop mask) for one dog."""
    import cv2
    cfg = DOGS[dog]
    s = cfg.get("scale", TRACE_SCALE)
    m = dog_masks()[dog]
    ys, xs = np.nonzero(m)
    x0, y0 = int(xs.min()) - CROP_PAD, int(ys.min()) - CROP_PAD
    x1, y1 = int(xs.max()) + CROP_PAD + 1, int(ys.max()) + CROP_PAD + 1
    crop = original()[y0:y1, x0:x1]
    size = ((x1 - x0) * s, (y1 - y0) * s)
    up = np.asarray(Image.fromarray(crop).resize(size, Image.LANCZOS)).astype(np.float64)
    mk = cv2.resize(m[y0:y1, x0:x1].astype(np.float32), size, interpolation=cv2.INTER_LINEAR)
    inside = cv2.GaussianBlur(mk, (0, 0), MASK_SIGMA * s) > 0.5
    front = front_of(dog)[y0:y1, x0:x1].astype(np.float32)
    front = cv2.resize(front, size, interpolation=cv2.INTER_LINEAR) > 0.5
    front_near = cv2.dilate(front.astype(np.uint8), kernel(2 * s)).astype(bool)
    # the strip under the dog in front, plus the dog's own last pixels beside it (which
    # carry the front dog's anti-aliased outline colour): both recoloured from inside
    tuck = cv2.dilate(inside.astype(np.uint8), kernel(TUCK_PX * s)).astype(bool) & front & ~inside
    tuck |= inside & front_near
    inside = inside | tuck
    flat = cv2.pyrMeanShiftFiltering(np.ascontiguousarray(up.astype(np.uint8)[..., ::-1]), *cfg.get("meanshift", MEANSHIFT))[..., ::-1]
    regs = regions(dog, up, flat, inside, (x0, y0), tuck, front_near)
    regs.sort(key=lambda r: (r[0], -int(r[2].sum())))
    names = [f[0] for f in DOGS[dog]["families"]]
    out = {}
    below = inside.copy()
    for i, (fi, col, mm) in enumerate(regs):
        shape = inside if i == 0 else below
        ds = [to_original(d, tx, ty, x0, y0, s) for d, tx, ty in vtrace(shape, s)]
        below = below & ~mm
        if ds:
            out.setdefault(fi, []).append((hexc(col), " ".join(ds)))
    families = [(names[f], out[f]) for f in sorted(out)]
    return (x0, y0, x1, y1), families, inside


# --------------------------------------------------------------------------
# 4. Hidden parts (completed by hand, drawn only where a dog in front covers them)
# --------------------------------------------------------------------------
# The schnauzer's rump and far hind leg are behind the golden retriever in the original.
# Smooth outlines (build_dogs.smooth) in the original's pixel space, filled with the traced
# fur colour and outlined in the traced outline colour, clipped to the golden's silhouette.
HIDDEN = {
    "dog-schnauzer": dict(behind="dog-golden", width=3.6, shapes=[
        ("rump", "fur-base", [(1250, 852), (1290, 851), (1326, 858), (1352, 872), (1367, 895), (1369, 924),
                              (1361, 950), (1346, 966), (1326, 970), (1300, 958), (1280, 928), (1262, 890)]),
        ("leg-far", "fur-shade", [(1336, 928), (1360, 932), (1366, 958), (1362, 984), (1346, 990),
                                  (1334, 978)]),
    ]),
}


def hidden_svg(dog, families):
    import build_dogs as bd
    h = HIDDEN.get(dog)
    if not h:
        return "", ""
    cols = {f: parts[0][0] for f, parts in families}
    ink = cols["outline"]
    clip_d = trace(h["behind"])[1][0][1][0][1]
    defs = f'<clipPath id="{dog}-hidden-clip"><path d="{clip_d}"/></clipPath>\n'
    body = "".join(
        f'<path id="{dog}-hidden-{name}" d="{bd.smooth(pts)}" fill="{cols[fam]}" stroke="{ink}" '
        f'stroke-width="{fmt(h["width"])}" stroke-linejoin="round"/>'
        for name, fam, pts in h["shapes"])
    return defs, (f'<g id="{dog}-hidden" inkscape:label="{dog}-hidden (completed behind {h["behind"]})" '
                  f'clip-path="url(#{dog}-hidden-clip)">{body}</g>\n')


# --------------------------------------------------------------------------
# 5. Shadows
# --------------------------------------------------------------------------
# A shadow pixel is clearly darker than the rebuilt street under it and tinted like the
# original's shadows (bluish purple on the road; darker green under the puppy on the lawn).
SHADOW_REACH = 90           # px around the dogs searched for their shadows
SHADOW_ROAD = dict(dl=0.5, b=-9, a=2.5)
SHADOW_LAWN_BOX = (640, 950, 840, 995)            # x0, y0, x1, y1 under the puppy
SHADOW_FILL = {"road": "#1d1a40", "lawn": "#1f3a10"}
SHADOW_RING = 8             # px ring around a shadow sampled as its unshaded ground
SHADOW_SCALE = 2            # shadows are traced at this multiple of the original's pixels


@lru_cache(maxsize=1)
def street_rgb():
    """The rebuilt street layer at 1x, the ground the shadows are measured against."""
    import build_scene as bs
    return np.asarray(bt.png_image(bt.render(bs.build_street(), bs.W)).convert("RGB"))


@lru_cache(maxsize=1)
def shadow_masks():
    """{dog: (kind, full-frame bool mask)} of each dog's shadow, cleaned."""
    import cv2
    from scipy import ndimage as ndi
    import build_scene as bs
    masks = dog_masks()
    dogs = np.zeros_like(next(iter(masks.values())))
    for m in masks.values():
        dogs |= m
    o = original()
    lab = to_lab(o)
    street = to_lab(street_rgb())
    near = cv2.dilate(dogs.astype(np.uint8), kernel(SHADOW_REACH)).astype(bool) & ~dogs
    road = ((lab[..., 0] < street[..., 0] - SHADOW_ROAD["dl"]) & (lab[..., 2] < SHADOW_ROAD["b"])
            & (lab[..., 1] > SHADOW_ROAD["a"]) & (street[..., 2] < -8) & (street[..., 0] < 56))
    x0, y0, x1, y1 = SHADOW_LAWN_BOX
    lawn = (lab[..., 1] < -12) & (lab[..., 0] < 64) & (lab[..., 2] > 20)
    box = np.zeros_like(lawn)
    box[y0:y1, x0:x1] = True
    out = {}
    for kind, raw in (("road", road & near & ~box), ("lawn", lawn & box & ~dogs)):
        m = ndi.binary_opening(raw, disk(2))
        # carry the shadow under the feet it touches, then close and fill
        m |= dogs & cv2.dilate(m.astype(np.uint8), kernel(6)).astype(bool)
        m = ndi.binary_closing(m, disk(5))
        m = ndi.binary_fill_holes(m)
        lbl, n = ndi.label(m)
        if not n:
            continue
        # each shadow piece belongs to the dog nearest to it
        dist = {d: ndi.distance_transform_edt(~mm) for d, mm in masks.items()}
        for i in range(1, n + 1):
            c = lbl == i
            if c.sum() < 300:
                continue
            owner = min(sorted(dist), key=lambda d: float(dist[d][c].min()))
            prev = out.get(owner, (kind, np.zeros_like(c)))[1]
            out[owner] = (kind, prev | c)
    return out


def shadow_opacity(kind, mask):
    """Opacity that darkens the ground as much as the original's shadow darkens its own
    ground: measured against the original's unshaded ground in a ring around the shadow."""
    import cv2
    dogs = np.any(list(dog_masks().values()), axis=0)
    ring = cv2.dilate(mask.astype(np.uint8), kernel(SHADOW_RING)).astype(bool) & ~mask & ~dogs
    o = original().astype(float)
    ground, shade_ = o[ring].mean(0), o[mask & ~dogs].mean(0)
    fill = np.array([int(SHADOW_FILL[kind][i:i + 2], 16) for i in (1, 3, 5)], float)
    alpha = float(((ground - shade_) * (ground - fill)).sum() / ((ground - fill) ** 2).sum())
    return round(min(max(alpha, 0.1), 0.9), 2)


@lru_cache(maxsize=1)
def shadow_parts():
    """[(shadow id, fill, opacity, path d)] in back-to-front dog order."""
    import cv2
    out = []
    s = SHADOW_SCALE
    for dog in [d for d in DOG_ORDER if d in shadow_masks()]:
        kind, m = shadow_masks()[dog]
        ys, xs = np.nonzero(m)
        x0, y0, x1, y1 = xs.min() - 4, ys.min() - 4, xs.max() + 5, ys.max() + 5
        up = cv2.resize(m[y0:y1, x0:x1].astype(np.float32), ((x1 - x0) * s, (y1 - y0) * s))
        shape = cv2.GaussianBlur(up, (0, 0), 2.0 * s) > 0.5
        ds = [to_original(d, tx, ty, int(x0), int(y0), s) for d, tx, ty in vtrace(shape, s)]
        out.append((dog.replace("dog-", "shadow-"), SHADOW_FILL[kind], shadow_opacity(kind, m), " ".join(ds)))
    return out


# --------------------------------------------------------------------------
# Layer documents (same interface as build_dogs)
# --------------------------------------------------------------------------
DOG_ORDER = ["dog-shadows", "dog-schnauzer", "dog-puppy", "dog-corgi", "dog-dalmatian", "dog-golden"]
STANDALONE_H = 3200
STANDALONE_PAD = 12


def dog_body(dog, hidden=True):
    """(<defs> body, drawable body) of one traced dog."""
    import build_scene as bs
    _, families, _ = trace(dog)
    defs, hid = hidden_svg(dog, families) if hidden else ("", "")
    body = hid
    for fam, parts in families:
        paths = "".join(f'<path d="{d}" fill="{c}"/>' for c, d in parts)
        body += bs.group(f"{dog}-{fam}", paths)
    return defs, body


def shadows_svg():
    import build_scene as bs
    body = "".join(bs.group(sid, f'<path d="{d}" fill="{fill}" opacity="{fmt(op)}"/>')
                   for sid, fill, op, d in shadow_parts())
    return bs.layer_svg("dog-shadows", body)


def dog_layers(hidden=True):
    import build_scene as bs
    layers = {"dog-shadows": shadows_svg()}
    for dog in DOG_ORDER[1:]:
        defs, body = dog_body(dog, hidden)
        layers[dog] = bs.layer_svg(dog, body, defs)
    return layers


def dog_names():
    return DOG_ORDER[1:]


def bbox(dog):
    """The standalone crop box: the traced dog, its hidden parts, and a margin."""
    x0, y0, x1, y1 = trace(dog)[0]
    for _, _, pts in HIDDEN.get(dog, {}).get("shapes", []):
        x0, y0 = min(x0, min(p[0] for p in pts)), min(y0, min(p[1] for p in pts))
        x1, y1 = max(x1, max(p[0] for p in pts)), max(y1, max(p[1] for p in pts))
    p = STANDALONE_PAD
    return x0 - p, y0 - p, x1 + p, y1 + p


def crop(dog_svg, dog):
    import build_dogs as bd
    return bd.crop_svg(dog_svg, bbox(dog), STANDALONE_H)


def mask_check(dog, layer_img, front_tol=TUCK_PX + 1):
    """Pixels of the traced dog (2x render, hidden parts excluded) outside its mask.

    Allowed: CHECK_TOL render px of anti-aliasing, and the strip (at most TUCK_PX) that
    runs under a dog in front. Returns the count of offending pixels (must be 0)."""
    import cv2
    m = dog_masks()[dog]
    allowed = m | (front_of(dog) & cv2.dilate(m.astype(np.uint8), kernel(front_tol)).astype(bool))
    k = layer_img.shape[0] // m.shape[0]
    allowed = np.kron(allowed, np.ones((k, k), bool))
    allowed = cv2.dilate(allowed.astype(np.uint8), kernel(CHECK_TOL)).astype(bool)
    return int(((layer_img > 16) & ~allowed).sum())


def hidden_check(dog, hidden_img):
    """Pixels of the hand-completed hidden parts not covered by the dog in front."""
    import cv2
    h = HIDDEN.get(dog)
    if not h:
        return 0
    m = dog_masks()[h["behind"]]
    k = hidden_img.shape[0] // m.shape[0]
    allowed = cv2.dilate(np.kron(m, np.ones((k, k), bool)).astype(np.uint8), kernel(CHECK_TOL)).astype(bool)
    return int(((hidden_img > 16) & ~allowed).sum())


def clear_caches():
    """Forget every cached mask and trace, so the next build recomputes from scratch."""
    for f in (original, dog_masks, trace, street_rgb, shadow_masks, shadow_parts):
        f.cache_clear()
