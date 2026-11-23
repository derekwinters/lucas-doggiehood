"""Export the approved concept house models as .glb files.

    python3 art-source/blender/export_houses.py art-source/blender/exports

Needs headless Blender as a Python module (`pip install bpy==5.3.0`).

For each base house N (1-10), in that base's RENDER_SCHEME:
  base-NN-framing.glb   the framing version of the base alone
  base-NN-level1.glb    the finished base
  base-NN-level2.glb    + the garage (#780, #817)
  base-NN-level3.glb    + the deck
  base-NN-level4.glb    + the patio (behind the garage)
  driveway-base-NN.glb  the decorative driveway on its own (base-local
                        coordinates, same pivot as the house)
and the upgrade pieces on their own, with their default DIMS, in base 1's
scheme (Sage Cottage):
  piece-garage.glb, piece-deck.glb, piece-patio.glb
  piece-addition.glb    the superseded concept addition, kept for comparison
                        (dh_scene still derives each garage's roof pitch from it)

Roofs: the embedded roof image is the tinted copy for the base's scheme
(textures/roof_<style>_<HEX>.png), wired straight into the glTF base colour
with no baseColorFactor, so every roof carries exactly one tint and viewers
show it as rendered in Blender.
"""

import importlib
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import bpy  # noqa: E402,F401
import dh_house_kit as K  # noqa: E402
import dh_scene as S  # noqa: E402
import piece_addition  # noqa: E402
import piece_garage  # noqa: E402
import piece_deck  # noqa: E402
import piece_patio  # noqa: E402

BASE_COUNT = 10
LEVELS = (1, 2, 3, 4)
PIECE_SCHEME = "Sage Cottage"


def main(out_dir):
    os.makedirs(out_dir, exist_ok=True)
    written = []
    for n in range(1, BASE_COUNT + 1):
        base = importlib.import_module("base_house_%d" % n)
        pal = K.scheme_palette(base.RENDER_SCHEME)
        K.reset_scene()
        ob, _ = base.build("framing", palette=pal)
        path = os.path.join(out_dir, "base-%02d-framing.glb" % n)
        K.export_glb(path, [ob])
        written.append(path)
        for lv in LEVELS:
            K.reset_scene()
            objs = S.build_level(base, pal, lv)
            path = os.path.join(out_dir, "base-%02d-level%d.glb" % (n, lv))
            K.export_glb(path, S.meshes(objs))
            written.append(path)
        K.reset_scene()
        dw, _ = S.driveway(base, palette=pal)
        path = os.path.join(out_dir, "driveway-base-%02d.glb" % n)
        K.export_glb(path, [dw])
        written.append(path)
    pal = K.scheme_palette(PIECE_SCHEME)
    for nm, mod in (("garage", piece_garage), ("addition", piece_addition),
                    ("deck", piece_deck), ("patio", piece_patio)):
        K.reset_scene()
        ob = mod.build(pal, name="piece_%s" % nm)
        if isinstance(ob, tuple):
            ob = ob[0]
        path = os.path.join(out_dir, "piece-%s.glb" % nm)
        K.export_glb(path, [ob])
        written.append(path)
    print("exported %d files" % len(written))


if __name__ == "__main__":
    if len(sys.argv) != 2:
        sys.exit(__doc__)
    main(sys.argv[1])
