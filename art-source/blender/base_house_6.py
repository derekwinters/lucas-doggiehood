"""Base house 6: "Split-level", two wings half a storey apart.

A tall two-level wing on the -X side under a front-facing 38 deg gable
(it projects 1.0 m toward the street) beside a low one-storey living wing
on the +X side under a low 18 deg side gable. The tall wing's upper floor sits
half a storey above the living wing's floor, so its windows are stacked at
two in-between heights: the stepped roofline is the silhouette. The front
door is in the living wing, clear of the tall wing's projection.

+X (living wing) is a gable end, so the addition butts in (no tie-in).
"""

import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import dh_house_kit as K  # noqa: E402

DIMS = {
    "width": 11.0,          # both wings, -X wall to +X wall
    "depth": 7.8,           # tall wing, front to back (the living wing is 1.0 m shallower)
    "tall_width": 5.0,      # tall wing, -X part
    "tall_wall": 4.7,       # tall wing wall height above FLOOR_Z
    "tall_pitch": 38.0,
    "upper_floor": 1.75,    # tall wing's upper level, above FLOOR_Z (the half-storey offset)
    "low_setback": 1.6,     # living wing front is this far behind the tall wing's front
    "low_wall": 2.5,
    "low_pitch": 18.0,
}
NAME = "Split-level"
RENDER_SCHEME = "Navy Saltbox"
PALETTE = K.scheme_palette(RENDER_SCHEME)
FRONT_Y = -DIMS["depth"] / 2                     # tall wing's front wall
DECK_CX, DECK_W = K.deck_layout(DIMS["width"] / 2)
DOOR_X = -DIMS["width"] / 2 + DIMS["tall_width"] + 4.5
PIECES = {"addition": {}, "deck": {"width": DECK_W}, "patio": {}}


def _lines(d=DIMS):
    W, D = d["width"], d["depth"]
    x0, x1, y0, y1 = -W / 2, W / 2, -D / 2, D / 2
    xm = x0 + d["tall_width"]
    return x0, x1, y0, y1, xm, y0 + d["low_setback"]


def attach_locations(d=DIMS):
    x0, x1, y0, y1, xm, yl = _lines(d)
    return {"ATTACH_ADDITION": (x1, y1 - K.ADDITION_BACK_INSET, 0), "ATTACH_DECK": (DECK_CX, y1, 0)}


def _walls(d):
    x0, x1, y0, y1, xm, yl = _lines(d)
    zt_t = K.FLOOR_Z + d["tall_wall"]
    zt_l = K.FLOOR_Z + d["low_wall"]
    O = K.Opening
    up = d["upper_floor"] + 0.75          # upper-level window sill
    lo = 0.15                              # lower-level window sill (half below the living floor)
    tw = d["tall_width"]
    tall = [
        K.Wall((x0, y0), (xm, y0), zt_t, [
            O("window", tw / 2, 2.0, 1.1, sill=lo, flowerbox=True),
            O("window", tw / 2, 2.0, 1.2, sill=up, shutters=True),
        ]),
        K.Wall((xm, y0), (xm, y1), zt_t, []),          # mostly behind the living wing's roof

        K.Wall((xm, y1), (x0, y1), zt_t, [
            O("window", 2.4, 1.2, 1.0, sill=lo + 0.1),
            O("window", 2.4, 1.2, 1.2, sill=up),
        ]),
        K.Wall((x0, y1), (x0, y0), zt_t, [
            O("window", 2.0, 1.0, 1.0, sill=lo + 0.1),
            O("window", 5.6, 1.0, 1.2, sill=up, shutters=True),
        ]),
    ]
    t_door = DOOR_X - xm
    t_back = x1 - DECK_CX
    low = [
        K.Wall((xm, yl), (x1, yl), zt_l, [
            O("door", t_door, 1.1, 2.3),
            O("window", 2.0, 2.6, 1.4, sill=0.75, mullions=True, shutters=True, flowerbox=True),
        ], start_trim=0.0),
        K.Wall((x1, yl), (x1, y1), zt_l, [O("window", 1.2, 1.0, 1.1, sill=0.95)]),
        K.Wall((x1, y1), (xm, y1), zt_l, [
            O("window", 0.95, 1.0, 1.1, sill=0.95),
            O("door", t_back, 1.1, 2.3),
            O("window", 5.1, 1.0, 1.1, sill=0.95),
        ], end_trim=0.0),
    ]
    return tall, low


def build(mode, d=DIMS, palette=PALETTE, name=None):
    x0, x1, y0, y1, xm, yl = _lines(d)
    zt_t = K.FLOOR_Z + d["tall_wall"]
    zt_l = K.FLOOR_Z + d["low_wall"]
    m = K.MeshBuilder(name or "base_house_6_%s" % mode, palette)
    tall, low = _walls(d)

    # --- foundation (identical in both modes) ---------------------------
    K.slab(m, x0, xm, y0, y1)
    K.slab(m, xm, x1, yl, y1)
    m.box((DOOR_X - 0.9, yl - 1.0, 0), (DOOR_X + 0.9, yl, K.FLOOR_Z - 0.18), "concrete")
    K.steps(m, DOOR_X - 0.7, DOOR_X + 0.7, yl - 1.0, K.FLOOR_Z - 0.18, n=1, run=0.4)
    m.box((DECK_CX - 0.7, y1, 0), (DECK_CX + 0.7, y1 + 0.6, K.FLOOR_Z - 0.25), "concrete")

    tall_ridge = None
    if mode == "finished":
        for w in tall + low:
            K.wall_finished(m, w, "siding")
        rt = K.gable_roof(m, x0, xm, y0, y1, zt_t, d["tall_pitch"], "finished", ridge_axis="y")
        f, r, b, l = tall
        K.gable_finished(m, f, lambda t: rt["top_z"](x0 + t), "siding", samples=[d["tall_width"] / 2])
        K.gable_finished(m, b, lambda t: rt["top_z"](xm - t), "siding", samples=[d["tall_width"] / 2])
        K.gable_vent(m, f, d["tall_width"] / 2, zt_t + 0.6, 0.9, 0.5)
        rl = K.gable_roof(m, xm, x1, yl, y1, zt_l, d["low_pitch"], "finished", ridge_axis="x",
                          rake_start=0.0)
        lf, lr, lb = low
        K.gable_finished(m, lr, lambda t: rl["top_z"](yl + t), "siding", samples=[(y1 - yl) / 2])
        K.gable_window(m, lr, (y1 - yl) / 2, zt_l + 0.6, 0.5)
        tall_ridge = rt["ridge_z"]
        K.chimney_through_roof(m, x0 + 1.2, 1.6, zt_t, tall_ridge + 0.5)
    else:
        z_split = K.FLOOR_Z + d["upper_floor"]
        K.floor_framing(m, x0, xm, y0, y1, skip_sheet=lambda r, c: r >= 5 and c >= 1)
        K.floor_framing(m, xm, x1, yl, y1, skip_sheet=lambda r, c: (r >= 4 and c >= 1) or (r == 3 and c == 2))
        lower, upper = zip(*[K.split_stories(w, z_split=z_split - K.PLATFORM_T) for w in tall])
        for w in list(lower) + list(upper) + low:
            K.wall_framing(m, w)
        K.floor_framing(m, x0, xm, y0, y1, skip_sheet=lambda r, c: r <= 1 or (r >= 5 and c == 0),
                        z_bottom=z_split - K.PLATFORM_T, z_top=z_split)
        rt = K.gable_roof(m, x0, xm, y0, y1, zt_t, d["tall_pitch"], "framing", ridge_axis="y")
        f, r, b, l = tall
        K.gable_framing(m, f, lambda t: rt["bottom_z"](x0 + t))
        K.gable_framing(m, b, lambda t: rt["bottom_z"](xm - t))
        rl = K.gable_roof(m, xm, x1, yl, y1, zt_l, d["low_pitch"], "framing", ridge_axis="x", rake_start=0.0)
        K.gable_framing(m, low[1], lambda t: rl["bottom_z"](yl + t))
        K.brace(m, low[0], 3.0, 5.2)
        K.brace(m, upper[3], 1.0, 3.4)
        K.brace(m, lower[0], 0.3, 1.6)

    ob = m.build()
    loc = attach_locations(d)
    return ob, [K.attach_point(k, loc[k], ob) for k in ("ATTACH_ADDITION", "ATTACH_DECK")]
