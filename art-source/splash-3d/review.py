"""Make the review images comparing the 3D render with the original splash (no bpy).

    python3 art-source/splash-3d/review.py RENDER_DIR OUT_DIR

Reads RENDER_DIR/splash-render.png, splash-render-sky.png and turnaround-*.png; writes
side-by-side.png, overlay.png, edges-overlay.png, houses-sheet.png and turnaround.png.
"""
import glob
import os
import sys

import numpy as np
from PIL import Image, ImageDraw, ImageFilter, ImageFont

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import splash_data as SD  # noqa: E402

REPO = os.path.abspath(os.path.join(HERE, "..", ".."))
ORIGINAL = os.path.join(REPO, "Assets", "Art", "Splash", "cover-art.png")
OUT_W = 1408              # review images are half the original's size
CROP_PAD = 40             # px around a house's crop (original px)
SHEET_H = 360             # each house crop's height on the sheet
LABEL_H = 34
BG = (32, 34, 40)
FG = (240, 240, 240)


def font(size):
    for p in ("/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf",
              "/usr/share/fonts/truetype/liberation/LiberationSans-Bold.ttf"):
        if os.path.exists(p):
            return ImageFont.truetype(p, size)
    return ImageFont.load_default()


def house_box(h):
    g, w = h["gable"], h.get("wing") or {}
    xs = [g["x0"], g["x1"], g["x0"] - g["overhang"], g["x1"] + g["overhang"]]
    if w:
        xs.append(w["x_out"])
    y0 = min(g["roof_top"], w.get("ridge", 9999)) - CROP_PAD
    for c in h.get("chimney", {}).values() if h.get("chimney") else []:
        y0 = min(y0, c[1] - CROP_PAD)
    x0 = max(0, min(xs) - CROP_PAD)
    x1 = min(SD.W, max(xs) + CROP_PAD)
    y1 = min(SD.H, h["ground"] + CROP_PAD * 2)
    return int(x0), int(max(0, y0)), int(x1), int(y1)


def main():
    rdir, odir = sys.argv[1], sys.argv[2]
    os.makedirs(odir, exist_ok=True)
    orig = Image.open(ORIGINAL).convert("RGB")
    rend = Image.open(os.path.join(rdir, "splash-render-sky.png")).convert("RGB").resize(orig.size, Image.LANCZOS)
    rend_a = Image.open(os.path.join(rdir, "splash-render.png")).convert("RGBA").resize(orig.size, Image.LANCZOS)
    hh = int(OUT_W * SD.H / SD.W)
    f = font(22)

    # side by side: original on top, render below
    sbs = Image.new("RGB", (OUT_W, hh * 2 + LABEL_H * 2), BG)
    d = ImageDraw.Draw(sbs)
    d.text((10, 6), "Original splash (cover-art.png)", fill=FG, font=f)
    sbs.paste(orig.resize((OUT_W, hh), Image.LANCZOS), (0, LABEL_H))
    d.text((10, hh + LABEL_H + 6), "3D render, solved splash camera (Blender EEVEE, toon materials)", fill=FG, font=f)
    sbs.paste(rend.resize((OUT_W, hh), Image.LANCZOS), (0, hh * 1 + LABEL_H * 2))
    sbs.save(os.path.join(odir, "side-by-side.png"))

    # 50 % blend
    Image.blend(orig, rend, 0.5).resize((OUT_W, hh), Image.LANCZOS).save(os.path.join(odir, "overlay.png"))

    # edges: the render's edges in magenta over the greyed original
    a = np.asarray(rend_a)
    alpha = a[..., 3] > 8
    lum = np.asarray(rend.convert("L").filter(ImageFilter.FIND_EDGES)) > 28
    sil = np.asarray(Image.fromarray((alpha * 255).astype(np.uint8)).filter(ImageFilter.FIND_EDGES)) > 0
    edges = lum | sil
    base = np.asarray(orig.convert("L").convert("RGB")).astype(np.float32) * 0.75 + 40
    base[edges] = (255, 0, 200)
    Image.fromarray(base.clip(0, 255).astype(np.uint8)).save(
        os.path.join(odir, "edges-overlay.png"))

    # per-house sheet: original | render, labelled
    tiles = []
    for h in SD.HOUSES:
        box = house_box(h)
        o = orig.crop(box)
        r = rend.crop(box)
        k = SHEET_H / o.height
        size = (max(1, int(o.width * k)), SHEET_H)
        o, r = o.resize(size, Image.LANCZOS), r.resize(size, Image.LANCZOS)
        t = Image.new("RGB", (size[0] * 2 + 12, SHEET_H + LABEL_H), BG)
        t.paste(o, (0, LABEL_H))
        t.paste(r, (size[0] + 12, LABEL_H))
        ImageDraw.Draw(t).text((6, 6), f"{h['label']}  -  original | 3D", fill=FG, font=f)
        tiles.append(t)
    cols = 2
    cw = max(t.width for t in tiles)
    rows = (len(tiles) + cols - 1) // cols
    sheet = Image.new("RGB", (cw * cols + 20 * (cols + 1), rows * (SHEET_H + LABEL_H + 20) + 20), BG)
    for i, t in enumerate(tiles):
        sheet.paste(t, (20 + (i % cols) * (cw + 20), 20 + (i // cols) * (SHEET_H + LABEL_H + 20)))
    sheet.save(os.path.join(odir, "houses-sheet.png"))

    # turnaround sheet
    tps = sorted(glob.glob(os.path.join(rdir, "turnaround-*.png")))
    if tps:
        ims = [Image.open(p).convert("RGBA") for p in tps]
        tw, th = 600, int(600 * ims[0].height / ims[0].width)
        cols = 3
        rows = (len(ims) + cols - 1) // cols
        ts = Image.new("RGB", (cols * tw, rows * (th + LABEL_H)), (200, 226, 240))
        dd = ImageDraw.Draw(ts)
        for i, (p, im) in enumerate(zip(tps, ims)):
            x, y = (i % cols) * tw, (i // cols) * (th + LABEL_H)
            bg = Image.new("RGBA", im.size, (186, 224, 242, 255))
            bg.alpha_composite(im)
            ts.paste(bg.convert("RGB").resize((tw, th), Image.LANCZOS), (x, y + LABEL_H))
            dd.text((x + 8, y + 6), os.path.basename(p)[11:-4], fill=(20, 30, 40), font=f)
        ts.save(os.path.join(odir, "turnaround.png"))


if __name__ == "__main__":
    main()
