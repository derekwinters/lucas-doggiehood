"""The blanket's size and colourways (#413), in one place, as plain Python (no Blender).

Read by the model (dh_blanket.py), the textures (dh_blanket_tex.py), the export and its checks
(export_blanket.py), the renders, the viewer and the tests, so changing the size or adding a
colourway is an edit here only.

CLOTH_W x CLOTH_D is the woven cloth (Derek, 2026-11-27: about 2.0 x 1.4 m, so an adult Golden
Retriever lying flat fits on it). The fringe sticks out FRINGE_LEN past each short end, so the
placement footprint is FOOTPRINT_W x FOOTPRINT_D, centred on the pivot.
"""

CLOTH_W = 2.0               # metres across (Blender X), fringe excluded
CLOTH_D = 1.4               # metres deep (Blender Y)
FRINGE_LEN = 0.06           # tassel length past each short end

FOOTPRINT_W = CLOTH_W + 2 * FRINGE_LEN
FOOTPRINT_D = CLOTH_D

# (key, label). The first is the default (renders, viewer). Each key gets its own export
# exports/blanket_<key>.glb and colour texture textures/blanket_plaid_<key>_color.png; the setts
# are in dh_blanket_tex.SETTS. The game picks one at random when a blanket is gifted (#932).
COLOURWAYS = [
    ("navy", "Navy & blue"),
    ("red", "Red & green tartan"),
    ("green", "Green & cream"),
    ("purple", "Grey & purple"),
]
DEFAULT = COLOURWAYS[0][0]
