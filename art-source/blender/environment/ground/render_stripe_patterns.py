"""The mow-stripe patterns (Derek, 2026-11-24): front-to-back, side-to-side,
diagonal-left and diagonal-right, on the same lot as render_stripes.py, at
strength 1.0. Also an aliasing check for each pattern at the fixed game camera.

The renders in review/ were made when there was one diagonal, named
"diagonal": it is today's diagonal-left (+45). diagonal-right is its mirror
image and was not rendered.

  patterns-lot-<pattern>.png      the lot at the game camera
  patterns-tile-<pattern>.png     the whole starter tile (60 m)
  patterns-compare.png            the patterns side by side (lot and tile rows)
  patterns-close-diagonal.png     8 m close-up of diagonal-left at the lot line to the
                                  neighbouring lot; red pegs stand on the line
  patterns-alias-far-<pattern>.png  a 400 x 300 px crop of the 180 m view with NO
                                  anti-aliasing (1 sample), stripes only (x DIFF_GAIN)
  patterns_stats.json             per pattern and view: the stripes' screen period and
                                  angle (from the 2-D spectrum of striped - unmowed), and
                                  the aliasing error: how much the 1-sample stripe image
                                  differs from the 24-sample one, as a fraction of the
                                  stripe signal

Usage: python3 render_stripe_patterns.py [OUT_DIR] [--samples 24]
"""
import json
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import render_stripes as RS  # noqa: E402
import dh_ground as G  # noqa: E402
import bpy  # noqa: E402
import dh_house_kit as K  # noqa: E402
import dh_mow_stripes as MS  # noqa: E402
import numpy as np  # noqa: E402
from PIL import Image, ImageDraw, ImageFont  # noqa: E402

OUT = RS.OUT
NOAA_SAMPLES = 1                 # EEVEE with one sample: no anti-aliasing, like a bare mobile render
ALIAS_VIEWS = ("lot", "tile", "far")
CROP = (400, 300)                # alias evidence crop, centred on the lot


def main():
    os.makedirs(OUT, exist_ok=True)
    K.reset_scene()
    sc = G.lot_scene()
    P = sc["lot"]["P"]
    cx, cy = sc["tile_centre"]
    rect = MS.lawn_rect(P["quad"], road_x=P["side_road_x"], road_y=P["road_cl_y"])
    frame = MS.LotFrame.from_rect(*rect, across=(1.0, 0.0))
    qx0, qx1, qy0, qy1 = P["quad"]
    lawn = []

    def set_lawn(strength, pattern):
        for o in lawn:
            bpy.data.objects.remove(o)
        lawn.clear()
        mat = G.lawn_material(RS.LOOK, RS.APPROACH, stripes=(frame, strength, pattern))
        lawn.extend(G.lawn_tiles(RS.LOOK, RS.APPROACH, sc["tiles"], mat=mat))

    views = {
        "lot": ([(qx0, qy0), (qx1, qy0), (qx0, qy1), (qx1, qy1)], RS.LOT_RES, 1.02, 6.0),
        "tile": (RS._box((cx, cy), G.TILE_SIZE / 2), RS.TILE_RES, 1.0, 6.0),
        "far": (RS._box((cx, cy), G.TILE_SIZE * 1.5), RS.FAR_RES, 1.01, 0.0),
    }

    def shoot(path, view, samples):
        pts, res, margin, z_top = views[view]
        RS.SAMPLES = samples
        RS._render(path, pts, res, margin=margin, z_top=z_top)
        return np.asarray(Image.open(path).convert("RGB"), np.float32)

    tmp = os.path.join(OUT, "_patterns_tmp")
    os.makedirs(tmp, exist_ok=True)
    base = {}
    set_lawn(0.0, "front-to-back")
    for view in ALIAS_VIEWS:
        for smp in (RS.SAMPLES_DEFAULT, NOAA_SAMPLES):
            base[view, smp] = shoot(os.path.join(tmp, "base-%s-%d.png" % (view, smp)), view, smp)

    stats = {}
    for pat in MS.PATTERNS:
        set_lawn(1.0, pat)
        stats[pat] = {}
        for view in ALIAS_VIEWS:
            keep = view in ("lot", "tile")
            p_aa = os.path.join(OUT if keep else tmp, "patterns-%s-%s.png" % (view, pat))
            d_aa = shoot(p_aa, view, RS.SAMPLES_DEFAULT) - base[view, RS.SAMPLES_DEFAULT]
            d_no = shoot(os.path.join(tmp, "noaa-%s-%s.png" % (view, pat)), view, NOAA_SAMPLES) - \
                base[view, NOAA_SAMPLES]
            g_aa, g_no = d_aa.mean(2), d_no.mean(2)
            sig = np.abs(g_aa) > RS.DIFF_THRESHOLD
            per, ang = _period(g_aa)
            stats[pat][view] = {
                "screen_period_px": round(per, 1), "stripe_px": round(per / 2, 1),
                "screen_angle_deg": round(ang, 1),
                "alias_error": round(float(np.abs(g_no - g_aa)[sig].mean() / np.abs(g_aa)[sig].mean()), 3),
                "stripe_pixels": int(sig.sum())}
            print(pat, view, stats[pat][view], flush=True)
            if view == "far":
                ys, xs = np.nonzero(sig)
                y, x = int(np.median(ys)), int(np.median(xs))
                y0, x0 = max(0, y - CROP[1] // 2), max(0, x - CROP[0] // 2)
                crop = np.clip(128 + d_no[y0:y0 + CROP[1], x0:x0 + CROP[0]] * RS.DIFF_GAIN, 0, 255)
                Image.fromarray(crop.astype(np.uint8)).resize((CROP[0] * 2, CROP[1] * 2), Image.NEAREST).save(
                    os.path.join(OUT, "patterns-alias-far-%s.png" % pat))
    RS.SAMPLES = RS.SAMPLES_DEFAULT

    set_lawn(1.0, "diagonal-left")
    x0, x1, y0, y1 = rect
    ym = (y0 + y1) / 2
    pegs = K.MeshBuilder("pegs", {"peg": "#E0302A"})
    for y in (ym - RS.CLOSE_HALF + RS.PEG_INSET, ym + RS.CLOSE_HALF - RS.PEG_INSET):
        pegs.box((x0 - RS.PEG[0], y - RS.PEG[0], 0), (x0 + RS.PEG[0], y + RS.PEG[0], RS.PEG[1]), "peg")
    pegs = pegs.build()
    RS._render(os.path.join(OUT, "patterns-close-diagonal.png"), RS._box((x0, ym), RS.CLOSE_HALF), RS.CLOSE_RES,
               z_top=1.4)
    bpy.data.objects.remove(pegs)

    with open(os.path.join(OUT, "patterns_stats.json"), "w") as f:
        json.dump({"stripe_w_m": MS.STRIPE_W, "amp_rgb": MS.AMP_RGB, "patterns": MS.PATTERNS,
                   "noaa_samples": NOAA_SAMPLES, "aa_samples": RS.SAMPLES_DEFAULT, "views": stats}, f, indent=1)

    pats = list(MS.PATTERNS)
    rows = [[Image.open(os.path.join(OUT, "patterns-%s-%s.png" % (v, p))).convert("RGB") for p in pats]
            for v in ("lot", "tile")]
    w, h = rows[0][0].size[0] // 2, rows[0][0].size[1] // 2
    th = int(rows[1][0].size[1] * w / rows[1][0].size[0])
    sh = Image.new("RGB", (w * len(pats), h + th + 2 * RS.LABEL_H), RS.BG)
    dr = ImageDraw.Draw(sh)
    font = ImageFont.truetype(RS.FONT, 28)
    for i, p in enumerate(pats):
        sh.paste(rows[0][i].resize((w, h)), (i * w, RS.LABEL_H))
        dr.text((i * w + 16, 16), "%s, the lot" % p, fill="#222222", font=font)
        sh.paste(rows[1][i].resize((w, th)), (i * w, h + 2 * RS.LABEL_H))
        dr.text((i * w + 16, h + RS.LABEL_H + 16), "%s, the starter tile" % p, fill="#222222", font=font)
    sh.save(os.path.join(OUT, "patterns-compare.png"))
    print("DONE")


def _period(g):
    """Dominant screen period (px) and the stripes' screen angle (degrees from
    horizontal, the direction the stripes RUN) from the 2-D spectrum."""
    g = g - g.mean()
    F = np.abs(np.fft.fft2(g))
    F[0, 0] = 0
    for i in range(-2, 3):
        for j in range(-2, 3):
            F[i, j] = 0
    i, j = np.unravel_index(np.argmax(F), F.shape)
    H, W = g.shape
    fy = (i if i < H / 2 else i - H) / H
    fx = (j if j < W / 2 else j - W) / W
    per = 1.0 / np.hypot(fx, fy)
    run = (np.degrees(np.arctan2(fy, fx)) + 90.0) % 180.0      # stripes run perpendicular to the wave vector
    return float(per), float(run)


if __name__ == "__main__":
    main()
