"""Chihuahua-only extra: the Chihuahua beside the approved Beagle and Labrador (front, side, 3/4 and a
side overlay).

  python3 art-source/blender/dogs/chihuahua/render_compare.py [dogs_folder] [out_dir]

dogs_folder (holding labrador/ and beagle/) defaults to this folder's parent; out_dir to this
folder's out/. DH_BEAGLE optionally points at another Beagle folder. The Chihuahua is built by the
kit (default coat, rest pose); the Lab and the Beagle are imported from their committed default-coat
glTFs, so they are exactly the approved files.
Every tile of a row uses the same orthographic camera and scale, so sizes compare directly. One
process, 11 renders (under EEVEE-on-lavapipe's limit).
"""
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
KIT = os.path.join(os.path.dirname(HERE), "kit")
sys.path.insert(0, KIT)
DOGS = os.path.abspath(sys.argv[1]) if len(sys.argv) > 1 else os.path.dirname(HERE)
OUT_ARG = sys.argv[2] if len(sys.argv) > 2 else os.path.join(HERE, "out")
sys.argv = [sys.argv[0], HERE, OUT_ARG]
os.environ["DH_BREED"] = HERE
import render_dog as RDG  # noqa: E402  (chooses this breed from argv, sets up lavapipe)
import numpy as np  # noqa: E402
from PIL import Image, ImageDraw  # noqa: E402

R, D = RDG.R, RDG.D
OUT = RDG.OUT
TMP = RDG.TMP
BEAGLE = os.environ.get("DH_BEAGLE", os.path.join(DOGS, "beagle"))
GLTF = {"lab": os.path.join(DOGS, "labrador", "exports", "dog_labrador_yellow.gltf"),
        "beagle": os.path.join(BEAGLE, "exports", "dog_beagle_default.gltf")}
NAMES = {"lab": "Labrador (Yellow)", "beagle": "Beagle (Default)", "chi": "Chihuahua (Default)"}
BOX = ((-0.45, -0.98, 0.0), (0.45, 1.00, 1.62))       # holds all three dogs, rest pose, standing
VIEWS = [("Front", 0, 0, (700, 680)), ("Side", -90, 0, (900, 680)), ("3/4 front", -40, 14, (760, 680))]   # landscape tiles: the kit camera fits the width
OUTLINE = {"beagle": (40, 110, 200), "lab": (200, 60, 150)}


def tile(which, yaw, pitch, res, transparent=False):
    path = os.path.join(TMP, "cmp_%s_%d_%d%s.png" % (which, yaw, pitch, "_t" if transparent else ""))
    RDG.scene(path, res, samples=48, floor_hex=None if transparent else R.FLOOR)
    if transparent:
        RDG.bpy.context.scene.render.film_transparent = True
    if which == "chi":
        RDG.dog()
    else:
        RDG._import_gltf(GLTF[which])
    RDG.R.camera([R.box_object(*BOX)], yaw, pitch, res, ortho=True, margin=1.04)
    R.render()
    return Image.open(path).convert("RGBA" if transparent else "RGB")


def outline(alpha, width=3):
    a = np.asarray(alpha) > 127
    e = np.zeros_like(a)
    for dy in range(-width, width + 1):
        for dx in range(-width, width + 1):
            if dx * dx + dy * dy <= width * width:
                e |= np.roll(np.roll(a, dy, 0), dx, 1) != a
    return e


def main():
    head_h = 46
    order = ("lab", "beagle", "chi")
    rows = [(name, [tile(w, yaw, pitch, res) for w in order]) for name, yaw, pitch, res in VIEWS]
    res = VIEWS[1][3]
    t = {w: tile(w, -90, 0, res, transparent=True) for w in order}
    base = Image.new("RGB", res, RDG.BG)
    base.paste(t["chi"], (0, 0), t["chi"])
    px = np.asarray(base).copy()
    for w in ("beagle", "lab"):
        px[outline(t[w].split()[3])] = OUTLINE[w]
    overlay = Image.fromarray(px)
    W = max(sum(im.width for im in ims) for _, ims in rows)
    H = sum(ims[0].height + head_h for _, ims in rows) + res[1] + head_h + 90
    sheet = Image.new("RGB", (W, H), RDG.BG)
    d = ImageDraw.Draw(sheet)
    y = 0
    for name, ims in rows:
        x = (W - sum(im.width for im in ims)) // 2
        d.text((16, y + 10), name, fill=RDG.INK, font=RDG.font(26, True))
        for w, im in zip(order, ims):
            sheet.paste(im, (x, y + head_h))
            d.text((x + im.width // 2, y + 14), NAMES[w], fill=RDG.SUB, font=RDG.font(20, True), anchor="ma")
            x += im.width
        y += ims[0].height + head_h
    d.text((16, y + 10), "Side, overlaid: the Chihuahua, with the Beagle's outline in blue and the Lab's in magenta",
           fill=RDG.INK, font=RDG.font(26, True))
    sheet.paste(overlay, ((W - res[0]) // 2, y + head_h))
    y += res[1] + head_h
    m = (RDG.stats() or {}).get("measurements_m", {})
    d.text((16, y + 14), "Same orthographic scale in every row. Chihuahua: nose to rump %.2f m, nose to tail tip %.2f m, "
           "top of head %.2f m, top of back %.2f m, width incl. ears %.2f m." % (
               m.get("nose_to_rump", 0), m.get("nose_to_tail_tip", 0), m.get("top_of_head", 0),
               m.get("back_height", 0), m.get("width_incl_ears", 0)), fill=RDG.SUB, font=RDG.font(19))
    d.text((16, y + 46), "Beagle: 1.17, 1.29, 1.16, 0.70, 0.62 m.   Labrador: 1.46, 1.85, 1.58, 1.035, 0.74 m.",
           fill=RDG.SUB, font=RDG.font(19))
    sheet = RDG.header(sheet, "Chihuahua vs Beagle vs Labrador",
                       "The smallest dog: apple-dome head, short fine muzzle, big round eyes, large erect ears, fine "
                       "legs, sickle tail over the back. Fawn.")
    sheet.save(os.path.join(OUT, "chihuahua-vs-beagle-vs-lab.png"))
    print("rendered compare")


if __name__ == "__main__":
    main()
