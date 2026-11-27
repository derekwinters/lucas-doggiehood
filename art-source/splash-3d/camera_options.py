"""Sheet comparing splash camera options (no bpy).

    python3 art-source/splash-3d/camera_options.py OUT.png LABEL=RENDER_DIR [LABEL=RENDER_DIR ...]

Each RENDER_DIR is a render_splash.py output (splash-render-sky.png and
house_screen_boxes.json). The original painting comes first; each option is labelled with
its lens, where the camera stands and whether the far houses were pulled closer.
"""
import json
import os
import sys

from PIL import Image, ImageDraw

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
from review import BG, FG, ORIGINAL, font  # noqa: E402

TILE_W, TILE_H = 936, 510       # each panel (a third of the original)
LABEL_H = 64
COLS = 2
PAD = 16


def label_of(name, d):
    with open(os.path.join(d, "house_screen_boxes.json")) as f:
        j = json.load(f)
    v = j["view"]
    pulled = any(m < -0.01 for m in j["notes"]["moved"].values())
    line1 = f"{name}: {v['lens_mm']:.1f} mm lens ({v['hfov_deg']:.0f} deg wide)"
    y = 0.0 if abs(v["y"]) < 0.05 else v["y"]
    line2 = (f"camera {y:.1f} m down the street, {v['z']:.1f} m high; "
             + ("far houses pulled to 4 m gaps" if pulled else "painted spacing"))
    return line1, line2


def main():
    out = sys.argv[1]
    opts = [a.split("=", 1) for a in sys.argv[2:]]
    panels = [(("Original painting", "cover-art.png"), Image.open(ORIGINAL).convert("RGB"))]
    for name, d in opts:
        panels.append((label_of(name, d), Image.open(os.path.join(d, "splash-render-sky.png")).convert("RGB")))
    rows = (len(panels) + COLS - 1) // COLS
    sheet = Image.new("RGB", (COLS * (TILE_W + PAD) + PAD, rows * (TILE_H + LABEL_H + PAD) + PAD), BG)
    dr = ImageDraw.Draw(sheet)
    f1, f2 = font(24), font(19)
    for i, ((l1, l2), im) in enumerate(panels):
        x = PAD + (i % COLS) * (TILE_W + PAD)
        y = PAD + (i // COLS) * (TILE_H + LABEL_H + PAD)
        dr.text((x + 4, y + 4), l1, fill=FG, font=f1)
        dr.text((x + 4, y + 34), l2, fill=(190, 198, 206), font=f2)
        sheet.paste(im.resize((TILE_W, TILE_H), Image.LANCZOS), (x, y + LABEL_H))
    sheet.save(out)


if __name__ == "__main__":
    main()
