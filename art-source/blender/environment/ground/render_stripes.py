"""Mow-stripe proof renders (concept only): Derek's pick, look C with the
chosen approach ("rec") and edge set 1, with ONE lot (the starter corner lot,
ranch at level 4, fence style C) striped by dh_mow_stripes, computed per
pixel from world position as the shader option would.

  stripes-tile-s100.png / -s050 / -s000   game camera on the whole starter
                                          tile (60 m, four quadrants) at
                                          strength 1.0, 0.5 and 0.0
  stripes-lot-s100.png / -s050 / -s000    the same, framed on the lot
  stripes-close-neighbour.png             8 m close-up where the lot meets
                                          the neighbouring lot (the tile
                                          border behind the side fence run);
                                          red pegs stand on the lot line
  stripes-close-sidewalk.png              8 m close-up where the lot meets
                                          the sidewalk, walkway and verge
  stripes-far.png                         3 x 3 tiles (180 m), strength 1.0
  stripes-diff.png                        |s100 - s000| x DIFF_GAIN: every
                                          pixel the stripes changed
  stripes-compare.png                     the three strengths side by side
  stripes_stats.json                      mean lawn colour inside the lot,
                                          striped vs unmowed, and the count
                                          of changed pixels

Usage: python3 render_stripes.py [OUT_DIR] [--samples 24]
"""
import json
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import dh_ground as G  # noqa: E402
import bpy  # noqa: E402
import dh_house_kit as K  # noqa: E402
import dh_mow_stripes as MS  # noqa: E402
import numpy as np  # noqa: E402
from PIL import Image, ImageDraw, ImageFont  # noqa: E402

OUT = sys.argv[1] if len(sys.argv) > 1 and not sys.argv[1].startswith("--") else os.path.join(HERE, "out")
SAMPLES = 24
for i, a in enumerate(sys.argv):
    if a == "--samples":
        SAMPLES = int(sys.argv[i + 1])
SAMPLES_DEFAULT = SAMPLES
LOOK = "c"                       # Derek's pick, 2026-11-24
APPROACH = "rec"
STRENGTHS = (1.0, 0.5, 0.0)
TILE_RES = (1600, 1200)
CLOSE_RES = (1200, 900)
FAR_RES = (1600, 1200)
CLOSE_HALF = 4.0                 # close-ups frame +-4 m
LOT_RES = (1500, 1100)
PEG = (0.04, 0.35)               # lot-line marker peg half-width, height
PEG_INSET = 0.4
DIFF_GAIN = 8.0
DIFF_THRESHOLD = 2               # 8-bit levels: a pixel counts as changed above this
LABEL_H = 64
FONT = "/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf"
BG = K.COMMON["ground_studio"]


def _clear():
    for o in list(bpy.data.objects):
        if o.type in ("CAMERA", "LIGHT"):
            bpy.data.objects.remove(o)


def _render(path, pts, res, margin=1.0, z_top=0.0):
    _clear()
    hints = []
    for x, y in pts:
        h = K.MeshBuilder("hint", {"g": "#000000"})
        h.box((x - 0.05, y - 0.05, 0), (x + 0.05, y + 0.05, 0.05), "g")
        hints.append(h.build())
    if z_top:
        h = K.MeshBuilder("hint", {"g": "#000000"})
        h.box((pts[0][0], pts[0][1], z_top), (pts[0][0] + 0.05, pts[0][1] + 0.05, z_top + 0.05), "g")
        hints.append(h.build())
    K.setup_render(path, res=res, bg=BG, samples=SAMPLES)
    K.frame_camera(hints, margin=margin, res=res)
    for h in hints:
        bpy.data.objects.remove(h)
    K.render()
    print("wrote", path, flush=True)


def _box(c, half):
    return [(c[0] - half, c[1] - half), (c[0] + half, c[1] + half), (c[0] - half, c[1] + half),
            (c[0] + half, c[1] - half)]


def main():
    os.makedirs(OUT, exist_ok=True)
    K.reset_scene()
    sc = G.lot_scene()
    P = sc["lot"]["P"]
    cx, cy = sc["tile_centre"]
    rect = MS.lawn_rect(P["quad"], road_x=P["side_road_x"], road_y=P["road_cl_y"])
    frame = MS.LotFrame.from_rect(*rect, across=(1.0, 0.0))      # the house front faces -Y: stripes run along Y
    lawn = []

    def set_lawn(strength):
        for o in lawn:
            bpy.data.objects.remove(o)
        lawn.clear()
        mat = G.lawn_material(LOOK, APPROACH, stripes=(frame, strength))
        lawn.extend(G.lawn_tiles(LOOK, APPROACH, sc["tiles"], mat=mat))

    paths, lot_paths = {}, {}
    qx0, qx1, qy0, qy1 = P["quad"]
    for s in STRENGTHS:
        set_lawn(s)
        p = os.path.join(OUT, "stripes-tile-s%03d.png" % round(s * 100))
        _render(p, _box((cx, cy), G.TILE_SIZE / 2), TILE_RES, margin=1.0, z_top=6.0)
        paths[s] = p
        p = os.path.join(OUT, "stripes-lot-s%03d.png" % round(s * 100))
        _render(p, [(qx0, qy0), (qx1, qy0), (qx0, qy1), (qx1, qy1)], LOT_RES, margin=1.02, z_top=6.0)
        lot_paths[s] = p
    set_lawn(1.0)
    x0, x1, y0, y1 = rect
    ym = (y0 + y1) / 2
    pegs = K.MeshBuilder("pegs", {"peg": "#E0302A"})
    for y in (ym - CLOSE_HALF + PEG_INSET, ym + CLOSE_HALF - PEG_INSET):      # the lot line x = x0
        pegs.box((x0 - PEG[0], y - PEG[0], 0), (x0 + PEG[0], y + PEG[0], PEG[1]), "peg")
    pegs = pegs.build()
    _render(os.path.join(OUT, "stripes-close-neighbour.png"), _box((x0, ym), CLOSE_HALF), CLOSE_RES, z_top=1.4)
    bpy.data.objects.remove(pegs)
    _render(os.path.join(OUT, "stripes-close-sidewalk.png"),
            _box((x0 + (x1 - x0) * 0.3, y0), CLOSE_HALF), CLOSE_RES)
    half = G.TILE_SIZE * 1.5
    _render(os.path.join(OUT, "stripes-far.png"), _box((cx, cy), half), FAR_RES, margin=1.01)

    # changed pixels and lot mean colour (striped vs unmowed)
    a = np.asarray(Image.open(paths[1.0]).convert("RGB"), np.float32)
    b = np.asarray(Image.open(paths[0.0]).convert("RGB"), np.float32)
    d = np.abs(a - b)
    changed = d.max(axis=2) > DIFF_THRESHOLD
    Image.fromarray(np.clip(d * DIFF_GAIN, 0, 255).astype(np.uint8)).save(os.path.join(OUT, "stripes-diff.png"))
    stats = {"stripe_w_m": MS.STRIPE_W, "amp_rgb": MS.AMP_RGB, "lot_lawn_rect": [round(v, 3) for v in rect],
             "changed_pixels": int(changed.sum()),
             "mean_rgb_inside_changed_region": {
                 "strength_1": [round(float(v), 2) for v in a[changed].mean(0)],
                 "strength_0": [round(float(v), 2) for v in b[changed].mean(0)]}}
    a5 = np.asarray(Image.open(paths[0.5]).convert("RGB"), np.float32)
    stats["mean_rgb_inside_changed_region"]["strength_0.5"] = [round(float(v), 2) for v in a5[changed].mean(0)]
    with open(os.path.join(OUT, "stripes_stats.json"), "w") as f:
        json.dump(stats, f, indent=1)
    print(json.dumps(stats))

    rows = [lot_paths, paths]
    ims = [[Image.open(rp[s]).convert("RGB") for s in STRENGTHS] for rp in rows]
    w, h = ims[0][0].size[0] // 2, ims[0][0].size[1] // 2
    th = int(ims[1][0].size[1] * w / ims[1][0].size[0])
    sh = Image.new("RGB", (w * 3, h + th + 2 * LABEL_H), BG)
    dr = ImageDraw.Draw(sh)
    font = ImageFont.truetype(FONT, 28)
    for i, s in enumerate(STRENGTHS):
        sh.paste(ims[0][i].resize((w, h)), (i * w, LABEL_H))
        dr.text((i * w + 16, 16), "stripe strength %.1f, the lot" % s, fill="#222222", font=font)
        sh.paste(ims[1][i].resize((w, th)), (i * w, h + 2 * LABEL_H))
        dr.text((i * w + 16, h + LABEL_H + 16), "stripe strength %.1f, the starter tile" % s, fill="#222222",
                font=font)
    sh.save(os.path.join(OUT, "stripes-compare.png"))
    print("DONE")


if __name__ == "__main__":
    main()
