"""Base house 7: "Modern farmhouse".

A narrow, deep storey-and-a-half block under a steep 50 deg FRONT gable (ridge along Y),
board-and-batten siding (vertical battens over the walls and gables), tall
mullioned windows, a big loft window in the front gable, and a WRAP porch
across the front and down the -X side under a low 14 deg roof that turns
the corner on a hip line. The wrap stops 3.5 m short of the back, and the
+X side and back stay clear, so the addition and the deck attach exactly
as on the other bases.

+X is an EAVE side (front-gable roof), so the addition's roof ties in.
"""

import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import dh_house_kit as K  # noqa: E402

DIMS = {
    "width": 7.2,
    "depth": 9.0,
    "wall_height": 4.6,      # storey and a half: loft windows show above the porch roof
    "pitch": 50.0,
    "eave": 0.3,
    "rake": 0.3,
    "porch_depth": 2.0,
    "porch_floor": 0.5,
    "porch_pitch": 14.0,
    "porch_eave_z": 2.85,     # top of the porch rafters at the porch beam line
    "wrap_back_gap": 3.5,     # side porch stops this far in front of the back wall
    "batten_spacing": 0.6,
}
NAME = "Modern farmhouse"
RENDER_SCHEME = "Greige Modern"
PALETTE = K.scheme_palette(RENDER_SCHEME)
FRONT_Y = -DIMS["depth"] / 2 - DIMS["porch_depth"]        # porch front line takes the setback
DECK_CX, DECK_W = K.deck_layout(DIMS["width"] / 2)
PIECES = {
    "addition": {"pitch": 50.0, "tie_in": 1.1},
    "deck": {"width": DECK_W},
    "patio": {},
}
BATTEN_W, BATTEN_OUT = 0.09, 0.05


def attach_locations(d=DIMS):
    return {"ATTACH_ADDITION": (d["width"] / 2, d["depth"] / 2 - K.ADDITION_BACK_INSET, 0),
            "ATTACH_DECK": (DECK_CX, d["depth"] / 2, 0)}


def _walls(d):
    W, D = d["width"], d["depth"]
    zt = K.FLOOR_Z + d["wall_height"]
    x0, x1, y0, y1 = -W / 2, W / 2, -D / 2, D / 2
    O = K.Opening
    up = 3.0                                    # loft window sill, clear of the porch roof
    front = K.Wall((x0, y0), (x1, y0), zt, [
        O("window", 1.5, 0.95, 1.9, sill=0.4),
        O("door", W / 2, 1.2, 2.35),
        O("window", W - 1.5, 0.95, 1.9, sill=0.4),
        O("window", 1.7, 1.0, 1.1, sill=up),
        O("window", W - 1.7, 1.0, 1.1, sill=up),
    ])
    right = K.Wall((x1, y0), (x1, y1), zt, [O("window", 1.6, 0.9, 1.6, sill=0.6),
                                            O("window", 7.6, 0.9, 1.1, sill=up)])
    t_door = x1 - DECK_CX
    back = K.Wall((x1, y1), (x0, y1), zt, [
        O("door", t_door, 1.1, 2.3),
        O("window", 5.6, 0.9, 1.6, sill=0.6),
        O("window", 5.6, 0.9, 1.1, sill=up),
    ])
    left = K.Wall((x0, y1), (x0, y0), zt, [
        O("window", 1.8, 0.9, 1.6, sill=0.6),
        O("window", 4.9, 0.95, 1.9, sill=0.4),
        O("window", 7.3, 0.95, 1.9, sill=0.4),
        O("window", 2.6, 1.0, 1.1, sill=up),
        O("window", 6.1, 1.0, 1.1, sill=up),
    ])
    return front, right, back, left


def _battens(m, w, top_z):
    """Board-and-batten: vertical battens on the solid parts of a wall,
    each running up to top_z(t) (wall top, or the gable's roof line)."""
    t = w.t_start + 0.3
    spans = [o.span() for o in w.openings]
    while t < w.t_end - 0.1:
        if not any(a - K.TRIM_W * 1.8 < t < b + K.TRIM_W * 1.8 for a, b in spans):
            w.wbox(m, t - BATTEN_W / 2, t + BATTEN_W / 2, -BATTEN_OUT, 0.0, K.SLAB_TOP + K.SKIRT_H,
                   top_z(t) - 0.04, "siding", cull_back=True)
        t += DIMS["batten_spacing"]


def _porch_roof(m, d, mode):
    """Wrap porch roof: front leg (plane A, rising +Y) and side leg (plane B,
    rising +X) at the same pitch and eave height meet on a hip line from
    the outer corner to the house corner."""
    W, D = d["width"], d["depth"]
    x0, x1, y0, y1 = -W / 2, W / 2, -D / 2, D / 2
    pd, ze, p = d["porch_depth"], d["porch_eave_z"], d["porch_pitch"]
    ye = y1 - d["wrap_back_gap"]
    A = K.RoofPlane((0, y0 - pd, ze), (1, 0, 0), (0, 1, 0), p)
    B = K.RoofPlane((x0 - pd, 0, ze), (0, 1, 0), (1, 0, 0), p)
    c = math.cos(math.radians(p))

    def va(y):
        return (y - (y0 - pd)) / c

    def vb(x):
        return (x - (x0 - pd)) / c

    if mode == "finished":
        ov, so = 0.3, 0.25
        A.slab(m, [(x0 - pd - ov, va(y0 - pd - ov)), (x1 + so, va(y0 - pd - ov)), (x1 + so, va(y0)), (x0, va(y0))],
               "roof", "trim")
        B.slab(m, [(y0 - pd - ov, vb(x0 - pd - ov)), (y0, vb(x0)), (ye + so, vb(x0)), (ye + so, vb(x0 - pd - ov))],
               "roof", "trim")
    else:
        tl = 0.2
        A.rafters(m, [(x0 - pd - tl, va(y0 - pd - tl)), (x1, va(y0 - pd - tl)), (x1, va(y0) - 0.05),
                      (x0, va(y0) - 0.05)])
        B.rafters(m, [(y0 - pd - tl + 0.4, vb(x0 - pd - tl)), (y0, vb(x0) - 0.05), (ye, vb(x0) - 0.05),
                      (ye, vb(x0 - pd - tl))])
    return ye


def build(mode, d=DIMS, palette=PALETTE, name=None):
    W, D = d["width"], d["depth"]
    zt = K.FLOOR_Z + d["wall_height"]
    x0, x1, y0, y1 = -W / 2, W / 2, -D / 2, D / 2
    pd, pf = d["porch_depth"], d["porch_floor"]
    ye = y1 - d["wrap_back_gap"]
    m = K.MeshBuilder(name or "base_house_7_%s" % mode, palette)
    walls = _walls(d)
    front, right, back, left = walls

    # --- foundation + porch deck (identical in both modes) ---------------
    K.slab(m, x0, x1, y0, y1)
    for a, b in (((x0 - pd, y0 - pd), (x1 + 0.1, y0)), ((x0 - pd, y0), (x0, ye))):   # porch: footing + boards
        m.box((a[0] + 0.1, a[1] + 0.1, 0), (b[0], b[1], pf - 0.08), "concrete")
        m.box((a[0], a[1], pf - 0.08), (b[0], b[1], pf), "deck")
    K.steps(m, -1.0, 1.0, y0 - pd, pf, n=2, run=0.32)
    m.box((DECK_CX - 0.7, y1, 0), (DECK_CX + 0.7, y1 + 0.6, K.FLOOR_Z - 0.25), "concrete")

    tanp = math.tan(math.radians(d["porch_pitch"]))
    z_beam = d["porch_eave_z"] - K.RAFTER_H / math.cos(math.radians(d["porch_pitch"]))
    posts = [(x, y0 - pd + 0.2) for x in (x0 - pd + 0.2, -1.3, 1.3, x1 - 0.15)]
    posts += [(x0 - pd + 0.2, y) for y in (y0 + 1.6, ye - 0.2)]

    if mode == "finished":
        for w in walls:
            K.wall_finished(m, w, "siding")
        roof = K.gable_roof(m, x0, x1, y0, y1, zt, d["pitch"], "finished", ridge_axis="y",
                            eave=d["eave"], rake=d["rake"])
        K.gable_finished(m, front, lambda t: roof["top_z"](x0 + t), "siding", samples=[W / 2])
        K.gable_finished(m, back, lambda t: roof["top_z"](x1 - t), "siding", samples=[W / 2])
        bz = lambda t: roof["top_z"](x0 + t) - K.RAFTER_H  # noqa: E731
        _battens(m, front, lambda t: max(zt, bz(t)))
        _battens(m, back, lambda t: max(zt, roof["top_z"](x1 - t) - K.RAFTER_H))
        _battens(m, left, lambda t: zt)
        _battens(m, right, lambda t: zt)
        K.gable_window(m, front, W / 2, zt + 1.5, 1.3)
        K.gable_window(m, back, W / 2, zt + 1.3, 1.0)
        for x, y in posts:
            m.box((x - 0.12, y - 0.12, pf), (x + 0.12, y + 0.12, z_beam), "trim")
        m.box((x0 - pd + 0.05, y0 - pd + 0.06, z_beam - 0.28), (x1 + 0.02, y0 - pd + 0.34, z_beam), "trim")
        m.box((x0 - pd + 0.06, y0 - pd + 0.05, z_beam - 0.28), (x0 - pd + 0.34, ye, z_beam), "trim")
        _porch_roof(m, d, mode)
        K.chimney_through_roof(m, x0 + 1.5, 2.2, zt, roof["ridge_z"] - 0.6, w=0.35, d=0.35, mat="shutter")
    else:
        K.floor_framing(m, x0, x1, y0, y1, skip_sheet=lambda r, c: (r >= 6 and c >= 1) or (r == 5 and c == 2))
        z_split = K.FLOOR_Z + 2.6
        lower, upper = zip(*[K.split_stories(w, z_split=z_split) for w in walls])
        for w in lower + upper:
            K.wall_framing(m, w)
        K.floor_framing(m, x0, x1, y0, y1, skip_sheet=lambda r, c: r <= 2 or (c == 0 and r >= 5),
                        z_bottom=z_split, z_top=z_split + K.PLATFORM_T)
        roof = K.gable_roof(m, x0, x1, y0, y1, zt, d["pitch"], "framing", ridge_axis="y")
        K.gable_framing(m, front, lambda t: roof["bottom_z"](x0 + t))
        K.gable_framing(m, back, lambda t: roof["bottom_z"](x1 - t))
        for x, y in posts:
            m.box((x - 0.07, y - 0.07, pf), (x + 0.07, y + 0.07, z_beam), "framing")
        m.box((x0 - pd + 0.1, y0 - pd + 0.1, z_beam - 0.24), (x1, y0 - pd + 0.3, z_beam), "framing_dark")
        m.box((x0 - pd + 0.1, y0 - pd + 0.1, z_beam - 0.24), (x0 - pd + 0.3, ye, z_beam), "framing_dark")
        _porch_roof(m, d, mode)
        K.brace(m, lower[1], 3.0, 5.6)
        K.brace(m, lower[3], 6.4, 3.8)
        K.brace(m, upper[2], 0.3, 2.6)

    ob = m.build()
    loc = attach_locations(d)
    return ob, [K.attach_point(k, loc[k], ob) for k in ("ATTACH_ADDITION", "ATTACH_DECK")]
