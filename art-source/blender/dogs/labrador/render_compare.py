"""Labrador-only extra: the Lab beside the approved Golden (front, side, 3/4 and a side overlay).

  python3 art-source/blender/dogs/labrador/render_compare.py [golden_folder] [out_dir]

golden_folder defaults to ../golden-retriever; out_dir to this folder's out/. The Lab is built by
the kit (yellow coat, rest pose); the Golden is imported from its committed default-coat glTF, so
the Golden is exactly the approved file. Every tile of a row uses the same orthographic camera and
scale, so sizes compare directly. One process, 7 renders (well under EEVEE-on-lavapipe's limit).
"""
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
KIT = os.path.join(os.path.dirname(HERE), "kit")
sys.path.insert(0, KIT)
GOLDEN = os.path.abspath(sys.argv[1]) if len(sys.argv) > 1 else os.path.join(os.path.dirname(HERE), "golden-retriever")
OUT_ARG = sys.argv[2] if len(sys.argv) > 2 else os.path.join(HERE, "out")
sys.argv = [sys.argv[0], HERE, OUT_ARG]
os.environ["DH_BREED"] = HERE
import render_dog as RDG  # noqa: E402  (chooses this breed from argv, sets up lavapipe)
import numpy as np  # noqa: E402
from PIL import Image, ImageDraw  # noqa: E402

R, D = RDG.R, RDG.D
OUT = RDG.OUT
TMP = RDG.TMP
GOLDEN_GLTF = os.path.join(GOLDEN, "exports", "dog_golden_default.gltf")
BOX = ((-0.45, -1.00, 0.0), (0.45, 1.16, 1.62))       # holds both dogs, rest pose, standing
VIEWS = [("Front", 0, 0, (620, 620)), ("Side", -90, 0, (1000, 620)), ("3/4 front", -40, 14, (760, 620))]
TEAL = (40, 110, 200)


def tile(which, yaw, pitch, res, transparent=False):
    path = os.path.join(TMP, "cmp_%s_%d_%d%s.png" % (which, yaw, pitch, "_t" if transparent else ""))
    RDG.scene(path, res, samples=48, floor_hex=None if transparent else R.FLOOR)
    if transparent:
        RDG.bpy.context.scene.render.film_transparent = True
    if which == "lab":
        RDG.dog()
    else:
        RDG._import_gltf(GOLDEN_GLTF)
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
    rows = []
    for name, yaw, pitch, res in VIEWS:
        rows.append((name, tile("golden", yaw, pitch, res), tile("lab", yaw, pitch, res)))
    # side overlay: the Golden's silhouette drawn over the Lab
    res = VIEWS[1][3]
    g_t = tile("golden", -90, 0, res, transparent=True)
    l_t = tile("lab", -90, 0, res, transparent=True)
    base = Image.new("RGB", res, RDG.BG)
    base.paste(l_t, (0, 0), l_t)
    px = np.asarray(base).copy()
    px[outline(g_t.split()[3])] = TEAL
    overlay = Image.fromarray(px)
    W = max(r[1].width + r[2].width for r in rows)
    H = sum(r[1].height + head_h for r in rows) + res[1] + head_h + 90
    sheet = Image.new("RGB", (W, H), RDG.BG)
    d = ImageDraw.Draw(sheet)
    y = 0
    for name, g, lab in rows:
        x0 = (W - g.width - lab.width) // 2
        d.text((16, y + 10), name, fill=RDG.INK, font=RDG.font(26, True))
        sheet.paste(g, (x0, y + head_h))
        sheet.paste(lab, (x0 + g.width, y + head_h))
        d.text((x0 + g.width // 2, y + 14), "Golden Retriever (approved, Default)", fill=RDG.SUB,
               font=RDG.font(20, True), anchor="ma")
        d.text((x0 + g.width + lab.width // 2, y + 14), "Labrador (Yellow)", fill=RDG.SUB,
               font=RDG.font(20, True), anchor="ma")
        y += g.height + head_h
    d.text((16, y + 10), "Side, overlaid: the Labrador, with the Golden's outline in blue",
           fill=RDG.INK, font=RDG.font(26, True))
    sheet.paste(overlay, ((W - res[0]) // 2, y + head_h))
    y += res[1] + head_h
    st = RDG.stats() or {}
    m = st.get("measurements_m", {})
    d.text((16, y + 14), "Same orthographic scale in every row. Labrador: nose to rump %.2f m, nose to tail tip %.2f m, "
           "top of head %.2f m, top of back %.2f m, width incl. ears %.2f m." % (
               m.get("nose_to_rump", 0), m.get("nose_to_tail_tip", 0), m.get("top_of_head", 0),
               m.get("back_height", 0), m.get("width_incl_ears", 0)), fill=RDG.SUB, font=RDG.font(19))
    d.text((16, y + 46), "Golden: 1.49 m, 1.94 m, 1.58 m, 1.03 m, 0.74 m.", fill=RDG.SUB, font=RDG.font(19))
    sheet = RDG.header(sheet, "Labrador vs Golden Retriever",
                       "Broader skull with a stop, shorter square muzzle, small close ears, thick level otter tail, "
                       "deeper body, short smooth coat.")
    sheet.save(os.path.join(OUT, "lab-vs-golden.png"))
    print("rendered compare")


if __name__ == "__main__":
    main()
