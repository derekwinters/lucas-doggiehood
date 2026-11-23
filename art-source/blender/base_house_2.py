"""Base house 2: "Ranch", a wide single-storey ranch.

Long, low hip roof (22 deg), a big picture window, a small brick chimney
through the roof. Rebuilt for #785 / #817: the built-in garage end at -X is
gone (that bay is living space now, with a pair of windows in the front and
a second window on the -X end), and the ranch's garage arrives as its
level-2 piece on the +X side like every other base. The footprint stays
11.4 x 7.2 m, so the front line, attach points and deck are unchanged. The
+X side is an EAVE side, so the garage's roof runs in (tie_in) until it dies
into the main roof slope. The +X wall has no window: the garage covers
almost all of it from level 2.

build(mode) with mode "finished" or "framing" -> (mesh object, [attach empties]).
"""

import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import dh_house_kit as K  # noqa: E402

DIMS = {
    "width": 11.4,
    "depth": 7.2,
    "wall_height": 2.8,
    "pitch": 22.0,
    "eave": 0.5,
}
NAME = "Ranch"
RENDER_SCHEME = "Dove Gray"
PALETTE = K.scheme_palette(RENDER_SCHEME)
FRONT_Y = -DIMS["depth"] / 2
DOOR_X = -DIMS["width"] / 2 + 6.0      # front door centre (for the front walk)
DECK_CX, DECK_W = K.deck_layout(DIMS["width"] / 2)
PIECES = {
    "addition": {"wall_height": 2.6, "pitch": 22.0, "tie_in": 2.6},
    "deck": {"width": DECK_W},
    "patio": {},
}


def attach_locations(d=DIMS):
    return {"ATTACH_ADDITION": (d["width"] / 2, d["depth"] / 2 - K.ADDITION_BACK_INSET, 0),
            "ATTACH_DECK": (DECK_CX, d["depth"] / 2, 0)}


def _walls(d):
    W, D = d["width"], d["depth"]
    zt = K.FLOOR_Z + d["wall_height"]
    x0, x1, y0, y1 = -W / 2, W / 2, -D / 2, D / 2
    O = K.Opening
    front = K.Wall((x0, y0), (x1, y0), zt, [
        O("window", 1.25, 1.2, 1.2, sill=0.9, shutters=True),
        O("window", 2.95, 1.2, 1.2, sill=0.9, shutters=True),
        O("window", 4.55, 0.9, 1.1, sill=1.0),
        O("door", 6.0, 1.1, 2.3),
        O("window", 8.15, 2.4, 1.4, sill=0.75, flowerbox=True),
        O("window", 10.3, 1.0, 1.2, sill=0.9),
    ])
    right = K.Wall((x1, y0), (x1, y1), zt, [])
    t_door = x1 - DECK_CX
    back = K.Wall((x1, y1), (x0, y1), zt, [
        O("window", 1.0, 1.0, 1.2, sill=0.9),
        O("door", t_door, 1.1, 2.3),
        O("window", 6.7, 1.2, 1.2, sill=0.9),
        O("window", 9.6, 1.0, 1.0, sill=1.1),
    ])
    left = K.Wall((x0, y1), (x0, y0), zt, [O("window", 1.6, 1.0, 1.0, sill=1.1),
                                          O("window", 3.8, 1.0, 1.0, sill=1.1)])
    return front, right, back, left


def build(mode, d=DIMS, palette=PALETTE, name=None):
    W, D = d["width"], d["depth"]
    zt = K.FLOOR_Z + d["wall_height"]
    x0, x1, y0, y1 = -W / 2, W / 2, -D / 2, D / 2
    m = K.MeshBuilder(name or "base_house_2_%s" % mode, palette)
    front, right, back, left = walls = _walls(d)

    # --- foundation (identical in both modes) ---------------------------
    K.slab(m, x0, x1, y0, y1)
    dx = x0 + 6.0
    m.box((dx - 0.9, y0 - 1.0, 0), (dx + 0.9, y0, K.FLOOR_Z - 0.18), "concrete")
    K.steps(m, dx - 0.7, dx + 0.7, y0 - 1.0, K.FLOOR_Z - 0.18, n=1, run=0.4)
    m.box((DECK_CX - 0.7, y1, 0), (DECK_CX + 0.7, y1 + 0.6, K.FLOOR_Z - 0.25), "concrete")

    if mode == "finished":
        for w in walls:
            K.wall_finished(m, w, "siding")
        roof = K.hip_roof(m, x0, x1, y0, y1, zt, d["pitch"], "finished", eave=d["eave"])
        K.chimney_through_roof(m, -1.2, 0.9, zt, roof["ridge_z"] + 0.75)
    else:
        K.floor_framing(m, x0, x1, y0, y1, skip_sheet=lambda r, c: (r >= 4 and c >= 3) or (r == 3 and c == 4))
        for w in walls:
            K.wall_framing(m, w)
        K.hip_roof(m, x0, x1, y0, y1, zt, d["pitch"], "framing", eave=d["eave"])
        K.brace(m, front, 4.9, 7.4)
        K.brace(m, left, 6.9, 4.6)
        K.brace(m, right, 0.3, 2.6)

    ob = m.build()
    loc = attach_locations(d)
    return ob, [K.attach_point(k, loc[k], ob) for k in ("ATTACH_ADDITION", "ATTACH_DECK")]
