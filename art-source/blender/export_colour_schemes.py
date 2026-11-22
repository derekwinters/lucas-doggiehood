"""Write colour-schemes.json from dh_house_kit.COLOR_SCHEMES (round 2, the
approved 12), so later work can read the schemes without Blender.

    python3 art-source/blender/export_colour_schemes.py [out_path]

out_path defaults to art-source/blender/colour-schemes.json. The kit imports
bpy, so this needs the same headless Blender module as the other scripts.
Round 1 (COLOR_SCHEMES_BRIGHT) is superseded and is not written.
"""

import json
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import bpy  # noqa: E402,F401  (registers bmesh, which the kit imports)
import dh_house_kit as K  # noqa: E402

DEFAULT_OUT = os.path.join(HERE, "colour-schemes.json")
JSON_INDENT = 2


def document():
    return {
        "source": "art-source/blender/dh_house_kit.py COLOR_SCHEMES (round 2)",
        "generated_by": "art-source/blender/export_colour_schemes.py",
        "slots": ["siding", "roof", "roof_style", "trim", "shutter", "door", "chimney",
                  "flowerbox", "flower_a", "flower_b", "deck", "patio_a", "patio_b"],
        "derived": {
            "darken_factor": K.DERIVED_DARKEN,
            "note": "roof_cap and door_panel are the slot colour x darken_factor; "
                    "see scheme_palette() in dh_house_kit.py for the rest",
        },
        "schemes": [dict(s) for s in K.COLOR_SCHEMES],
    }


def main(out_path):
    with open(out_path, "w") as f:
        json.dump(document(), f, indent=JSON_INDENT)
        f.write("\n")
    print("wrote %d schemes to %s" % (len(K.COLOR_SCHEMES), out_path))


if __name__ == "__main__":
    main(sys.argv[1] if len(sys.argv) > 1 else DEFAULT_OUT)
