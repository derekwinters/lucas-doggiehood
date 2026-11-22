"""Base house 3: "Colonial", a two-storey traditional with a covered porch.

Symmetrical two-storey box under a side gable (35 deg), full-width front
porch with a shed roof on four square columns, shuttered windows on both
floors, exterior brick chimney on the -X gable. +X is a gable end, so the
addition butts straight into it (no tie-in).

Framing is built storey by storey: ground-floor walls, a second-floor
platform, upper walls, then the roof, all from the same DIMS.
"""

import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import dh_house_kit as K  # noqa: E402

DIMS = {
    "width": 8.4,
    "depth": 7.0,
    "wall_height": K.TWO_STORY_WALL,
    "pitch": 35.0,
    "porch_depth": 1.8,
    "porch_floor": 0.5,
}
NAME = "Two-storey colonial"
RENDER_SCHEME = "Colonial White"
PALETTE = K.scheme_palette(RENDER_SCHEME)
FRONT_Y = -DIMS["depth"] / 2 - DIMS["porch_depth"]   # porch front line takes the setback
DECK_CX, DECK_W = K.deck_layout(DIMS["width"] / 2)
PIECES = {"addition": {}, "deck": {"width": DECK_W}, "patio": {}}
UP = K.STORY_H + K.PLATFORM_T     # second-floor level above FLOOR_Z


def attach_locations(d=DIMS):
    return {"ATTACH_ADDITION": (d["width"] / 2, d["depth"] / 2 - K.ADDITION_BACK_INSET, 0),
            "ATTACH_DECK": (DECK_CX, d["depth"] / 2, 0)}


def _walls(d):
    W, D = d["width"], d["depth"]
    zt = K.FLOOR_Z + d["wall_height"]
    x0, x1, y0, y1 = -W / 2, W / 2, -D / 2, D / 2
    O = K.Opening
    up = UP + 0.85
    front = K.Wall((x0, y0), (x1, y0), zt, [
        O("window", 1.6, 1.2, 1.4, sill=0.8, shutters=True),
        O("door", W / 2, 1.2, 2.3),
        O("window", W - 1.6, 1.2, 1.4, sill=0.8, shutters=True),
        O("window", 1.6, 1.1, 1.3, sill=up, shutters=True),
        O("window", W / 2, 1.1, 1.3, sill=up, shutters=True),
        O("window", W - 1.6, 1.1, 1.3, sill=up, shutters=True),
    ])
    right = K.Wall((x1, y0), (x1, y1), zt, [
        O("window", 6.2, 1.0, 1.2, sill=0.9),
        O("window", 2.0, 1.0, 1.2, sill=up, shutters=True),
        O("window", 5.0, 1.0, 1.2, sill=up, shutters=True),
    ])
    t_door = x1 - DECK_CX
    back = K.Wall((x1, y1), (x0, y1), zt, [
        O("window", 1.5, 1.1, 1.3, sill=0.85, mullions=False),
        O("door", t_door, 1.1, 2.3),
        O("window", W - 1.6, 1.1, 1.3, sill=0.85, mullions=False),
        O("window", 2.2, 1.0, 1.2, sill=up, mullions=False),
        O("window", W - 2.2, 1.0, 1.2, sill=up, mullions=False),
    ])
    left = K.Wall((x0, y1), (x0, y0), zt, [
        O("window", 1.3, 1.0, 1.2, sill=0.9, shutters=True, flowerbox=True),
        O("window", 5.7, 1.0, 1.2, sill=0.9, shutters=True, flowerbox=True),
        O("window", 1.3, 1.0, 1.2, sill=up, shutters=True),
        O("window", 5.7, 1.0, 1.2, sill=up, shutters=True),
    ])
    return front, right, back, left


def build(mode, d=DIMS, palette=PALETTE, name=None):
    W, D = d["width"], d["depth"]
    zt = K.FLOOR_Z + d["wall_height"]
    x0, x1, y0, y1 = -W / 2, W / 2, -D / 2, D / 2
    pd, pf = d["porch_depth"], d["porch_floor"]
    yp = y0 - pd
    m = K.MeshBuilder(name or "base_house_3_%s" % mode, palette)
    walls = _walls(d)
    front, right, back, left = walls

    # --- foundation + porch deck (identical in both modes) ---------------
    K.slab(m, x0, x1, y0, y1)
    m.box((x0 - 1.0, -0.8, 0), (x0, 0.8, K.SLAB_TOP), "concrete")              # chimney footing
    m.box((x0 - 0.1, yp, 0), (x1 + 0.1, y0, pf), "concrete")                     # porch floor
    K.steps(m, -1.1, 1.1, yp, pf, n=2, run=0.32)
    m.box((DECK_CX - 0.7, y1, 0), (DECK_CX + 0.7, y1 + 0.6, K.FLOOR_Z - 0.25), "concrete")

    posts = [x0 + 0.2, -1.45, 1.45, x1 - 0.2]
    z_beam = K.FLOOR_Z + K.STORY_H - 0.25
    porch_low, porch_high = z_beam + 0.35, K.FLOOR_Z + K.STORY_H + 0.35

    if mode == "finished":
        for w in walls:
            K.wall_finished(m, w, "siding")
        roof = K.gable_roof(m, x0, x1, y0, y1, zt, d["pitch"], "finished", ridge_axis="x")
        K.gable_finished(m, right, lambda t: roof["top_z"](t - D / 2), "siding", samples=[D / 2])
        K.gable_finished(m, left, lambda t: roof["top_z"](D / 2 - t), "siding", samples=[D / 2])
        K.gable_window(m, right, D / 2, zt + 1.15, 0.75)
        K.gable_window(m, left, 3.5, zt + 1.15, 0.75)
        K.chimney_exterior(m, x0, 0.0, roof["ridge_z"] + 0.7)
        # porch: square columns, beam, shed roof
        for x in posts:
            m.box((x - 0.13, yp + 0.12, pf), (x + 0.13, yp + 0.38, z_beam), "trim")
            m.box((x - 0.18, yp + 0.07, pf), (x + 0.18, yp + 0.43, pf + 0.2), "trim")
        m.box((x0, yp + 0.08, z_beam), (x1, yp + 0.42, z_beam + 0.3), "trim")
        K.shed_roof(m, x0, x1, yp + 0.25, y0, porch_low, porch_high, "finished", over=(0.15, 0.35))
    else:
        K.floor_framing(m, x0, x1, y0, y1, skip_sheet=lambda r, c: (r >= 4 and c >= 2))
        lower, upper = zip(*[K.split_stories(w) for w in walls])
        for w in lower:
            K.wall_framing(m, w)
        K.floor_framing(m, x0, x1, y0, y1, skip_sheet=lambda r, c: (r <= 1 and c <= 1) or (r >= 4 and c == 2),
                        z_bottom=K.FLOOR_Z + K.STORY_H, z_top=K.FLOOR_Z + UP)
        for w in upper:
            K.wall_framing(m, w)
        roof = K.gable_roof(m, x0, x1, y0, y1, zt, d["pitch"], "framing", ridge_axis="x")
        K.gable_framing(m, right, lambda t: roof["bottom_z"](t - D / 2))
        K.gable_framing(m, left, lambda t: roof["bottom_z"](D / 2 - t))
        for x in posts:
            m.box((x - 0.07, yp + 0.18, pf), (x + 0.07, yp + 0.32, z_beam), "framing")
        m.box((x0, yp + 0.15, z_beam), (x1, yp + 0.35, z_beam + 0.25), "framing_dark")
        K.shed_roof(m, x0, x1, yp + 0.25, y0, porch_low, porch_high, "framing")
        K.brace(m, lower[0], 0.3, 2.4)
        K.brace(m, lower[3], 6.7, 4.4)
        K.brace(m, upper[0], 6.0, 8.1)

    ob = m.build()
    loc = attach_locations(d)
    return ob, [K.attach_point(k, loc[k], ob) for k in ("ATTACH_ADDITION", "ATTACH_DECK")]
