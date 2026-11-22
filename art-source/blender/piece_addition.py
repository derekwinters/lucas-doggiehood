"""Upgrade piece, level 2: the ADDITION, an extra room on the side.

Local origin = the base's ATTACH_ADDITION point: on the ground, on the base
side wall's OUTER face, centred on the addition. +X points away from the
house. The side touching the house has no wall (the base wall closes it).
Uses the base's palette keys (siding/roof/...) so it can wear each base's
colours.
"""

import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import dh_house_kit as K  # noqa: E402

DIMS = {
    "width": 4.0,        # out from the house (X)
    "depth": 4.5,        # along the house side (Y)
    "wall_height": 2.7,  # a little lower than a base storey so it reads as added on
    "pitch": 30.0,
    # How far the addition's roof runs INTO the base (m). 0 = it butts into
    # a gable-end wall (base 1). On an eave side (hip / front-gable bases)
    # the roof must run in until it dies into the main roof slope.
    "tie_in": 0.0,
}


def build(palette, d=DIMS, name="piece_addition"):
    AW, AD = d["width"], d["depth"]
    zt = K.FLOOR_Z + d["wall_height"]
    y0, y1 = -AD / 2, AD / 2
    m = K.MeshBuilder(name, palette)
    # slab starts where the base slab's overhang stops, so the tops never overlap
    m.box((K.SLAB_OVERHANG, y0 - K.SLAB_OVERHANG, 0), (AW + K.SLAB_OVERHANG, y1 + K.SLAB_OVERHANG,
          K.SLAB_TOP), "concrete")
    O = K.Opening
    front = K.Wall((0, y0), (AW, y0), zt, [
        O("window", AW / 2, 1.3, 1.2, sill=0.85, shutters=True, flowerbox=True)], start_trim=0.001)
    end = K.Wall((AW, y0), (AW, y1), zt, [O("window", AD / 2, 1.5, 1.2, sill=0.85)])
    back = K.Wall((AW, y1), (0, y1), zt, [O("window", AW / 2, 1.2, 1.2, sill=0.9)], end_trim=0.0)
    for w in (front, end, back):
        K.wall_finished(m, w, "siding")
    roof = K.gable_roof(m, 0.0, AW, y0, y1, zt, d["pitch"], "finished", ridge_axis="x",
                        rake_start=d.get("tie_in", 0.0))
    K.gable_finished(m, end, lambda t: roof["top_z"](t - AD / 2), "siding", samples=[AD / 2])
    K.gable_window(m, end, AD / 2, zt + 0.72, 0.55)
    ob = m.build()
    # the patio hangs off the addition: back edge of its slab, centred on it
    att = K.attach_point("ATTACH_PATIO", (AW / 2 + K.SLAB_OVERHANG, AD / 2 + K.SLAB_OVERHANG, 0), ob)
    return ob, [att]
