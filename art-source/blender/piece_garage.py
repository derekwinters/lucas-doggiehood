"""Upgrade piece, level 2: the GARAGE, an attached one-car garage on the
piece side (#780, decided on #817). It replaces the concept addition.

Local origin = the base's side attach point (concept name ATTACH_ADDITION):
on the ground, on the base side wall's OUTER face, centred on the garage
along Y. +X points away from the house. The side touching the house has no
wall (the base wall closes it).

- The sectional door faces the street (-Y), with a short sloped apron down
  from the slab to the ground. The driveway beyond it is a SEPARATE object
  (dh_scene.driveway), so it can change or be dropped (#803).
- A side window on the +X end wall, and a back door onto the patio.
- Gable roof, ridge along X (eaves front and back, gable end on +X), tied
  into the base exactly like the addition was: the base's per-piece
  overrides (wall height, pitch, tie-in) are reused, and the pitch is
  lowered so the ridge lands at the addition's ridge height
  (see dh_scene.fit_pieces), which keeps every base's solved tie-in valid.
- Paints only through the scheme's slots: siding, trim, roof, door
  (door_panel is the darkened door colour), so it always matches the house.
- Pieces appear finished right away (#772): there is no framing version.

Declares ATTACH_PATIO: the back edge of the garage's slab, centred on it.
"""

import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import dh_house_kit as K  # noqa: E402

DIMS = {
    "width": 3.5,          # out from the house (X): one car, 3.06 m clear inside
    "depth": 6.0,          # along the house side (Y)
    "wall_height": 2.7,    # floor line to top plate, as the addition
    "pitch": 23.4,         # default; dh_scene.fit_pieces sets it per base
    "tie_in": 0.0,         # how far the roof runs INTO the base (eave-side bases)
    "door_width": 2.6,     # sectional door
    "door_height": 2.3,
    "apron": 0.8,          # sloped concrete lip in front of the door
}

# The garage floor is the slab top (not the house's raised finished floor),
# so the sectional door starts at the slab.
DOOR_SILL = K.SLAB_TOP - K.FLOOR_Z


def build(palette, d=DIMS, name="piece_garage"):
    AW, AD = d["width"], d["depth"]
    zt = K.FLOOR_Z + d["wall_height"]
    y0, y1 = -AD / 2, AD / 2
    m = K.MeshBuilder(name, palette)
    # slab starts where the base slab's overhang stops, so the tops never overlap
    m.box((K.SLAB_OVERHANG, y0 - K.SLAB_OVERHANG, 0), (AW + K.SLAB_OVERHANG, y1 + K.SLAB_OVERHANG,
          K.SLAB_TOP), "concrete")
    O = K.Opening
    front = K.Wall((0, y0), (AW, y0), zt, [
        O("garage", AW / 2, d["door_width"], d["door_height"], sill=DOOR_SILL)], start_trim=0.001)
    end = K.Wall((AW, y0), (AW, y1), zt, [O("window", AD / 2, 1.2, 1.0, sill=1.0)])
    back = K.Wall((AW, y1), (0, y1), zt, [O("door", AW / 2, 1.0, 2.2)], end_trim=0.0)
    for w in (front, end, back):
        K.wall_finished(m, w, "siding")
    # apron: slab edge down to the ground, as wide as the door opening + trim
    hw = d["door_width"] / 2 + 0.15
    K.wedge_apron(m, AW / 2 - hw, AW / 2 + hw, y0 - K.SLAB_OVERHANG, d["apron"], K.SLAB_TOP - 0.01)
    # back step onto the patio
    m.box((AW / 2 - 0.65, y1 + K.SLAB_OVERHANG, 0), (AW / 2 + 0.65, y1 + K.SLAB_OVERHANG + 0.35,
          K.SLAB_TOP - 0.1), "concrete")
    roof = K.gable_roof(m, 0.0, AW, y0, y1, zt, d["pitch"], "finished", ridge_axis="x",
                        rake_start=d.get("tie_in", 0.0))
    K.gable_finished(m, end, lambda t: roof["top_z"](t - AD / 2), "siding", samples=[AD / 2])
    rise = roof["ridge_z"] - roof["z_eave"]
    if rise > 1.2:
        vh = min(0.5, rise * 0.4)
        K.gable_vent(m, end, AD / 2, zt + 0.15 + rise * 0.38, 0.6, vh, backing="door_panel")
    ob = m.build()
    att = K.attach_point("ATTACH_PATIO", (AW / 2 + K.SLAB_OVERHANG, AD / 2 + K.SLAB_OVERHANG, 0), ob)
    return ob, [att]
