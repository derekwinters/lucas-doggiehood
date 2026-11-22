"""Base house 4: "Craftsman bungalow".

Low front-facing gable (26 deg) with deep eaves, an INSET front porch under
the main roof carried on tapered columns over stone piers, knee braces in
the gable eaves, exposed rafter tails along the sides, exterior chimney on
the -X side. The porch front line (not the recessed front wall) takes the
FrontSetback. +X is an eave side, so the addition roof ties in.
"""

import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import dh_house_kit as K  # noqa: E402

DIMS = {
    "width": 8.6,
    "depth": 10.0,          # porch front line to back wall
    "porch_depth": 2.2,     # inset porch, under the main roof
    "wall_height": 2.9,
    "pitch": 26.0,
    "eave": 0.75,           # deep side eaves
    "rake": 0.6,
    "porch_floor": 0.5,
    "dormer_y": -1.0,       # gabled dormer on the -X (street-facing) slope
    "dormer_width": 2.2,
    "dormer_inset": 0.5,    # dormer face this far in from the -X wall line
    "dormer_wall": 0.95,    # dormer face height above the main roof surface (ridge stays under the main ridge)
}
NAME = "Craftsman bungalow"
RENDER_SCHEME = "Charcoal Craftsman"
PALETTE = K.scheme_palette(RENDER_SCHEME)
FRONT_Y = -DIMS["depth"] / 2
DECK_CX, DECK_W = K.deck_layout(DIMS["width"] / 2)
PIECES = {
    "addition": {"wall_height": 2.6, "pitch": 26.0, "tie_in": 2.5},
    "deck": {"width": DECK_W},
    "patio": {},
}
STONE = "#8E8A83"


def attach_locations(d=DIMS):
    return {"ATTACH_ADDITION": (d["width"] / 2, d["depth"] / 2 - K.ADDITION_BACK_INSET, 0),
            "ATTACH_DECK": (DECK_CX, d["depth"] / 2, 0)}


def _walls(d):
    W, D = d["width"], d["depth"]
    zt = K.FLOOR_Z + d["wall_height"]
    x0, x1, y1 = -W / 2, W / 2, D / 2
    yf = -D / 2 + d["porch_depth"]           # recessed front wall
    L = y1 - yf
    O = K.Opening
    front = K.Wall((x0, yf), (x1, yf), zt, [
        O("window", 1.9, 1.8, 1.4, sill=0.8),
        O("door", W / 2, 1.1, 2.3),
        O("window", W - 1.9, 1.8, 1.4, sill=0.8),
    ])
    right = K.Wall((x1, yf), (x1, y1), zt, [
        O("window", 0.9, 1.0, 1.2, sill=0.9),
        O("window", L - 0.8, 1.0, 1.2, sill=0.9),
    ])
    t_door = x1 - DECK_CX
    back = K.Wall((x1, y1), (x0, y1), zt, [
        O("window", 1.4, 1.1, 1.2, sill=0.9),
        O("door", t_door, 1.1, 2.3),
        O("window", W - 2.0, 1.3, 1.2, sill=0.9),
    ])
    left = K.Wall((x0, y1), (x0, yf), zt, [
        O("window", 1.5, 1.1, 1.2, sill=0.9, flowerbox=True),
        O("window", L - 1.4, 1.4, 1.3, sill=0.85, flowerbox=True),
    ])
    return front, right, back, left


def _dormer(m, d, roof, mode):
    """Gabled dormer on the -X slope, ridge along X. Its roof runs inward
    until its ridge dies into the main roof; the cheeks stop where the main
    roof reaches the dormer eave."""
    W = d["width"]
    tanp = math.tan(math.radians(d["pitch"]))
    yd, hw = d["dormer_y"], d["dormer_width"] / 2
    xf = -W / 2 + d["dormer_inset"]
    z_rf = roof["top_z"](xf)
    z_de = z_rf + d["dormer_wall"]
    dz_ridge = hw * tanp
    x_cheek = xf + (z_de - z_rf) / tanp
    x_end = xf + (z_de + dz_ridge + K.RAFTER_H - z_rf) / tanp + 0.1
    face = K.Wall((xf, yd + hw), (xf, yd - hw), z_de,
                  [K.Opening("window", hw, 1.3, 0.58, sill=z_rf + K.ROOF_T + 0.12 - K.FLOOR_Z, mullions=True)],
                  end_trim=0.0, z_base=z_rf)
    dr = K.gable_roof(m, xf, x_end, yd - hw, yd + hw, z_de, d["pitch"], mode, ridge_axis="x",
                      eave=0.12, rake_start=0.22, rake_end=0.0)
    if mode == "finished":
        face.z_bottom = z_rf - 0.1
        K.wall_finished(m, face, "siding")
        K.gable_finished(m, face, lambda t: dr["top_z"](yd + hw - t), "siding", samples=[hw])
        for sy in (-1, 1):
            y = yd + sy * hw
            m.prism([(xf, y, z_rf - 0.1), (xf, y, z_de), (x_cheek, y, z_de)], (0, -sy * K.WALL_T, 0), "siding")
    else:
        K.wall_framing(m, face)
        K.gable_framing(m, face, lambda t: dr["bottom_z"](yd + hw - t))


def build(mode, d=DIMS, palette=PALETTE, name=None):
    W, D = d["width"], d["depth"]
    zt = K.FLOOR_Z + d["wall_height"]
    x0, x1, y0, y1 = -W / 2, W / 2, -D / 2, D / 2
    yf = y0 + d["porch_depth"]
    pf = d["porch_floor"]
    pal = dict(palette, stone=STONE)
    m = K.MeshBuilder(name or "base_house_4_%s" % mode, pal)
    walls = _walls(d)
    front, right, back, left = walls
    # pseudo-wall on the porch front line: carries the front gable infill
    porch_line = K.Wall((x0, y0), (x1, y0), zt, [], end_trim=0.0)
    beam_h = 0.42

    # --- foundation + porch (identical in both modes) --------------------
    K.slab(m, x0, x1, yf, y1)
    m.box((x0 - 0.12, y0, 0), (x1 + 0.12, yf, pf), "concrete")                  # porch floor
    K.steps(m, -1.0, 1.0, y0, pf, n=2, run=0.32)
    m.box((x0 - 1.0, 0.4, 0), (x0, 2.0, K.SLAB_TOP), "concrete")                # chimney footing
    m.box((DECK_CX - 0.7, y1, 0), (DECK_CX + 0.7, y1 + 0.6, K.FLOOR_Z - 0.25), "concrete")
    cols = [x0 + 0.35, -1.45, 1.45, x1 - 0.35]
    for x in cols:                                                               # stone piers
        m.box((x - 0.32, y0 + 0.05, pf), (x + 0.32, y0 + 0.69, pf + 0.95), "stone")

    def roof_c(t):   # porch-line / back-wall t -> cross coordinate x
        return x0 + t

    if mode == "finished":
        for w in walls:
            K.wall_finished(m, w, "siding")
        roof = K.gable_roof(m, x0, x1, y0, y1, zt, d["pitch"], "finished", ridge_axis="y",
                            eave=d["eave"], rake=d["rake"])
        K.gable_finished(m, porch_line, lambda t: roof["top_z"](roof_c(t)), "siding", samples=[W / 2])
        K.gable_finished(m, back, lambda t: roof["top_z"](x1 - t), "siding", samples=[W / 2])
        K.gable_vent(m, porch_line, W / 2, zt + 0.95, 1.3, 0.8)                        # louvred gable vent
        K.gable_window(m, back, W / 2, zt + 0.95, 0.8)
        # porch beam + tapered columns on the stone piers
        m.box((x0, y0 + 0.02, zt - beam_h), (x1, y0 + 0.4, zt), "trim")
        zc0, zc1 = pf + 0.95, zt - beam_h
        for x in cols:
            b, t = 0.24, 0.15
            yc = y0 + 0.37
            m.hexahedron([(x - b, yc - b, zc0), (x + b, yc - b, zc0), (x + b, yc + b, zc0), (x - b, yc + b, zc0),
                          (x - t, yc - t, zc1), (x + t, yc - t, zc1), (x + t, yc + t, zc1), (x - t, yc + t, zc1)],
                         "trim")
        # knee braces under the front and back rakes
        tanp = math.tan(math.radians(d["pitch"]))
        for yy, sgn in ((y0, -1), (y1, 1)):
            for x in (-2.6, 2.6):
                zr = zt + (W / 2 - abs(x)) * tanp
                m.beam((x, yy, zr - 0.85), (x, yy + sgn * 0.5, zr + 0.1), 0.12, 0.12, (1, 0, 0), "trim")
                m.box((x - 0.06, min(yy, yy + sgn * 0.55), zr - 0.05), (x + 0.06, max(yy, yy + sgn * 0.55), zr + 0.12),
                      "trim")
        # exposed rafter tails along both eave sides (skipped where the addition ties in)
        add_y = attach_locations(d)["ATTACH_ADDITION"][1]
        y = y0 + 0.35
        while y < y1:
            for xs in (-1, 1):
                if xs > 0 and abs(y - add_y) < 2.7:
                    continue
                xw = xs * W / 2
                xe = xs * (W / 2 + d["eave"] - 0.05)
                zw = zt + 0.05
                ze = zt + 0.05 - (d["eave"] - 0.05) * tanp
                m.beam((xw, y, zw), (xe, y, ze), 0.09, 0.15, (0, 0, 1), "trim")
            y += 0.75
        K.chimney_exterior(m, x0, 1.2, roof["ridge_z"] + 0.5)
        _dormer(m, d, roof, mode)
    else:
        K.floor_framing(m, x0, x1, yf, y1, skip_sheet=lambda r, c: (r >= 4 and c >= 2) or (r == 3 and c == 3))
        for w in walls:
            K.wall_framing(m, w)
        roof = K.gable_roof(m, x0, x1, y0, y1, zt, d["pitch"], "framing", ridge_axis="y")
        K.gable_framing(m, porch_line, lambda t: roof["bottom_z"](roof_c(t)))
        K.gable_framing(m, back, lambda t: roof["bottom_z"](x1 - t))
        m.box((x0, y0 + 0.08, zt - beam_h), (x1, y0 + 0.34, zt), "framing_dark")     # porch header
        for x in cols:                                                           # temporary posts
            m.box((x - 0.07, y0 + 0.3, pf + 0.95), (x + 0.07, y0 + 0.44, zt - beam_h), "framing")
        K.brace(m, left, 0.4, 2.8)
        K.brace(m, front, 0.3, 2.4)
        K.brace(m, right, 4.0, 6.6)
        _dormer(m, d, roof, mode)

    ob = m.build()
    loc = attach_locations(d)
    return ob, [K.attach_point(k, loc[k], ob) for k in ("ATTACH_ADDITION", "ATTACH_DECK")]
