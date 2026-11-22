"""Export the approved concept house models as .glb files.

    python3 art-source/blender/export_houses.py art-source/blender/exports

Needs headless Blender as a Python module (`pip install bpy==5.3.0`).

For each base house N (1-10), in that base's RENDER_SCHEME:
  base-NN-framing.glb   the framing version of the base alone
  base-NN-level1.glb    the finished base
  base-NN-level2.glb    + the addition
  base-NN-level3.glb    + the deck
  base-NN-level4.glb    + the patio
and the three upgrade pieces on their own, with their default DIMS, in
base 1's scheme (Sage Cottage):
  piece-addition.glb, piece-deck.glb, piece-patio.glb

Roof-colour fix: a textured roof's material wires the tinted texture into
Base Color, and the glTF exporter then writes the texture but drops the
colour multiply (no baseColorFactor). After each export the GLB's JSON chunk
is patched so every `*_roof` material that has a baseColorTexture also
carries the scheme's roof colour as baseColorFactor, which is what URP's
Base Map x Base Color needs on import.
"""

import importlib
import json
import os
import struct
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import bpy  # noqa: E402,F401
import dh_house_kit as K  # noqa: E402
import dh_scene as S  # noqa: E402
import piece_addition  # noqa: E402
import piece_deck  # noqa: E402
import piece_patio  # noqa: E402

BASE_COUNT = 10
LEVELS = (1, 2, 3, 4)
PIECE_SCHEME = "Sage Cottage"
GLB_HEADER_LEN = 12
CHUNK_HEADER_LEN = 8
GLB_ALIGN = 4
ROOF_MATERIAL_SUFFIX = "_roof"


def patch_roof_color(path, palette):
    """Add the scheme's roof colour as baseColorFactor on textured roof
    materials that lack one. Returns the number of materials patched."""
    with open(path, "rb") as f:
        data = f.read()
    json_len = struct.unpack("<I", data[GLB_HEADER_LEN:GLB_HEADER_LEN + 4])[0]
    json_start = GLB_HEADER_LEN + CHUNK_HEADER_LEN
    doc = json.loads(data[json_start:json_start + json_len])
    rest = data[json_start + json_len:]
    fixed = 0
    for mat in doc.get("materials", []):
        pbr = mat.get("pbrMetallicRoughness", {})
        if (mat.get("name", "").endswith(ROOF_MATERIAL_SUFFIX)
                and "baseColorTexture" in pbr and "baseColorFactor" not in pbr):
            pbr["baseColorFactor"] = list(K.hex_rgba(palette["roof"]))
            mat["pbrMetallicRoughness"] = pbr
            fixed += 1
    if not fixed:
        return 0
    js = json.dumps(doc, separators=(",", ":")).encode()
    js += b" " * ((GLB_ALIGN - len(js) % GLB_ALIGN) % GLB_ALIGN)
    out = data[:GLB_HEADER_LEN] + struct.pack("<I", len(js)) + b"JSON" + js + rest
    out = out[:8] + struct.pack("<I", len(out)) + out[GLB_HEADER_LEN:]
    with open(path, "wb") as f:
        f.write(out)
    return fixed


def export(path, objects, palette):
    K.export_glb(path, objects)
    return patch_roof_color(path, palette)


def main(out_dir):
    os.makedirs(out_dir, exist_ok=True)
    written, patched = [], 0
    for n in range(1, BASE_COUNT + 1):
        base = importlib.import_module("base_house_%d" % n)
        pal = K.scheme_palette(base.RENDER_SCHEME)
        K.reset_scene()
        ob, _ = base.build("framing", palette=pal)
        path = os.path.join(out_dir, "base-%02d-framing.glb" % n)
        patched += export(path, [ob], pal)
        written.append(path)
        for lv in LEVELS:
            K.reset_scene()
            objs = S.build_level(base, pal, lv)
            path = os.path.join(out_dir, "base-%02d-level%d.glb" % (n, lv))
            patched += export(path, S.meshes(objs), pal)
            written.append(path)
    pal = K.scheme_palette(PIECE_SCHEME)
    for nm, mod in (("addition", piece_addition), ("deck", piece_deck), ("patio", piece_patio)):
        K.reset_scene()
        ob = mod.build(pal, name="piece_%s" % nm)
        if isinstance(ob, tuple):
            ob = ob[0]
        path = os.path.join(out_dir, "piece-%s.glb" % nm)
        patched += export(path, [ob], pal)
        written.append(path)
    print("exported %d files, patched %d roof materials" % (len(written), patched))


if __name__ == "__main__":
    if len(sys.argv) != 2:
        sys.exit(__doc__)
    main(sys.argv[1])
