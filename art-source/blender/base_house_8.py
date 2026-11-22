"""Base house 8: "Storybook Tudor".

Steep 50 deg side-gabled main block with a steeper 58 deg front
CROSS-GABLE on the -X half that projects toward the street. The
cross-gable carries the storybook details: dark half-timbering in its
gable and upper wall, an arched front door with a trim ring, and a
mullioned window. Low walls under big roofs, flower boxes, brick chimney
outside the -X gable (behind the cross-gable).

+X is a gable end, so the addition butts in (no tie-in).
"""

import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import dh_house_kit as K  # noqa: E402

DIMS = {
    "width": 9.6,
    "depth": 7.6,           # main block
    "wall_height": 2.7,
    "pitch": 50.0,
    "cross_x1": -0.8,       # cross-gable spans x0 .. cross_x1
    "cross_proj": 1.4,      # cross-gable projects this far in front of the main front wall
    "cross_pitch": 58.0,
}
NAME = "Storybook Tudor"
RENDER_SCHEME = "Cream & Forest"
PALETTE = K.scheme_palette(RENDER_SCHEME)
FRONT_Y = -DIMS["depth"] / 2 - DIMS["cross_proj"]        # cross-gable front wall
DECK_CX, DECK_W = K.deck_layout(DIMS["width"] / 2)
DOOR_X = -DIMS["width"] / 2 + 1.35
PIECES = {"addition": {"pitch": 50.0, "wall_height": 2.5}, "deck": {"width": DECK_W}, "patio": {}}
TIMBER = "deck_dark"       # half-timber colour: the scheme's deck stain, darkened
TIMBER_W = 0.16


def _lines(d=DIMS):
    W, D = d["width"], d["depth"]
    return -W / 2, W / 2, -D / 2, D / 2, d["cross_x1"], -D / 2 - d["cross_proj"]


def attach_locations(d=DIMS):
    x0, x1, y0, y1, xc, yc = _lines(d)
    return {"ATTACH_ADDITION": (x1, y1 - K.ADDITION_BACK_INSET, 0), "ATTACH_DECK": (DECK_CX, y1, 0)}


def _walls(d):
    x0, x1, y0, y1, xc, yc = _lines(d)
    zt = K.FLOOR_Z + d["wall_height"]
    O = K.Opening
    cw = xc - x0
    front_c = K.Wall((x0, yc), (xc, yc), zt, [
        O("door", DOOR_X - x0, 1.25, 2.55, arched=True),
        O("window", cw - 1.15, 1.2, 1.3, sill=0.75, flowerbox=True),
    ])
    side_c = K.Wall((xc, yc), (xc, y0), zt, [O("window", d["cross_proj"] / 2, 0.6, 0.9, sill=1.0, mullions=False)],
                    end_trim=0.0)
    front_m = K.Wall((xc, y0), (x1, y0), zt, [
        O("window", 1.5, 1.3, 1.3, sill=0.75, flowerbox=True),
        O("window", 4.0, 1.3, 1.3, sill=0.75, flowerbox=True),
    ], start_trim=0.0)
    right = K.Wall((x1, y0), (x1, y1), zt, [O("window", 6.6, 0.8, 1.0, sill=1.0)])
    t_door = x1 - DECK_CX
    back = K.Wall((x1, y1), (x0, y1), zt, [
        O("door", t_door, 1.1, 2.3, arched=True),
        O("window", 6.4, 1.2, 1.2, sill=0.85),
        O("window", 8.6, 0.9, 1.0, sill=1.0),
    ])
    left = K.Wall((x0, y1), (x0, yc), zt, [
        O("window", 3.4, 0.9, 1.1, sill=0.9),
        O("window", 7.9, 0.8, 1.0, sill=1.0),
    ])
    return front_c, side_c, front_m, right, back, left


def _half_timber(m, w, zt, top_z, apex_t):
    """Dark timbers on the cross-gable: a girt at the wall top, posts and
    braces in the band below it, king post and raking braces in the gable."""
    def tb(t0, t1, z0, z1):
        w.wbox(m, t0, t1, -0.05, 0.0, z0, z1, TIMBER, cull_back=True)
    L = w.L
    tb(0.05, L - 0.05, zt - 0.05, zt + TIMBER_W - 0.05)               # girt at the wall plate
    tb(0.05, L - 0.05, zt - 0.95, zt - 0.95 + TIMBER_W)               # lower rail of the band
    for t in (0.35, L / 2 + 0.15, L - 0.35):                         # band posts (clear of the window)
        tb(t - TIMBER_W / 2, t + TIMBER_W / 2, zt - 0.95, zt)
    zap = top_z(apex_t) - 0.25
    tb(apex_t - TIMBER_W / 2, apex_t + TIMBER_W / 2, zt, zap)           # king post
    for frac in (0.3, 0.7):                                            # studs either side
        t = frac * L
        tb(t - TIMBER_W / 2, t + TIMBER_W / 2, zt, top_z(t) - 0.15)
    for ta, tb_ in ((0.5, apex_t - 0.1), (L - 0.5, apex_t + 0.1)):     # raking braces
        za = zt + 0.1
        zb = zt + (zap - zt) * 0.55
        a, b = w.p(ta, 0.0, za), w.p(tb_, 0.0, zb)
        m.beam(a + w.n * 0.03, b + w.n * 0.03, TIMBER_W, 0.06, w.n, TIMBER)


def build(mode, d=DIMS, palette=PALETTE, name=None):
    x0, x1, y0, y1, xc, yc = _lines(d)
    W, D = d["width"], d["depth"]
    zt = K.FLOOR_Z + d["wall_height"]
    m = K.MeshBuilder(name or "base_house_8_%s" % mode, palette)
    walls = _walls(d)
    front_c, side_c, front_m, right, back, left = walls
    left_gable = K.Wall((x0, y1), (x0, y0), zt, [])          # main-roof gable part of the -X wall
    cw = xc - x0
    y_cross_end = y0 + 3.0                                      # cross roof runs back until it dies in

    # --- foundation (identical in both modes) ---------------------------
    K.slab(m, x0, x1, y0, y1)
    K.slab(m, x0, xc, yc, y0 + 0.2)
    m.box((DOOR_X - 0.85, yc - 0.9, 0), (DOOR_X + 0.85, yc, K.FLOOR_Z - 0.18), "concrete")
    K.steps(m, DOOR_X - 0.65, DOOR_X + 0.65, yc - 0.9, K.FLOOR_Z - 0.18, n=1, run=0.4)
    m.box((DECK_CX - 0.7, y1, 0), (DECK_CX + 0.7, y1 + 0.6, K.FLOOR_Z - 0.25), "concrete")
    m.box((x0 - 1.0, 1.0, 0), (x0, 2.6, K.SLAB_TOP), "concrete")              # chimney footing

    if mode == "finished":
        for w in walls:
            K.wall_finished(m, w, "siding")
        roof = K.gable_roof(m, x0, x1, y0, y1, zt, d["pitch"], "finished", ridge_axis="x")
        cr = K.gable_roof(m, x0, xc, yc, y_cross_end, zt, d["cross_pitch"], "finished", ridge_axis="y",
                         rake_start=0.3, rake_end=0.0)
        K.gable_finished(m, right, lambda t: roof["top_z"](y0 + t), "siding", samples=[D / 2])
        K.gable_finished(m, left_gable, lambda t: roof["top_z"](y1 - t), "siding", samples=[D / 2])
        K.gable_finished(m, front_c, lambda t: cr["top_z"](x0 + t), "siding", samples=[cw / 2])
        K.gable_window(m, right, D / 2, zt + 1.4, 0.7)
        K.gable_window(m, left_gable, D / 2 + 1.2, zt + 1.6, 0.6)
        _half_timber(m, front_c, zt, lambda t: cr["top_z"](x0 + t) - K.RAFTER_H, cw / 2)
        K.chimney_exterior(m, x0, 1.8, roof["ridge_z"] + 0.9)
    else:
        K.floor_framing(m, x0, x1, y0, y1, skip_sheet=lambda r, c: (r >= 4 and c >= 3) or (r == 3 and c == 3))
        K.floor_framing(m, x0, xc, yc, y0 + 0.2, skip_sheet=lambda r, c: c >= 1)
        for w in walls:
            K.wall_framing(m, w)
        roof = K.gable_roof(m, x0, x1, y0, y1, zt, d["pitch"], "framing", ridge_axis="x")
        cr = K.gable_roof(m, x0, xc, yc, y_cross_end, zt, d["cross_pitch"], "framing", ridge_axis="y",
                          rake_start=0.3, rake_end=0.0)
        K.gable_framing(m, right, lambda t: roof["bottom_z"](y0 + t))
        K.gable_framing(m, left_gable, lambda t: roof["bottom_z"](y1 - t))
        K.gable_framing(m, front_c, lambda t: cr["bottom_z"](x0 + t))
        K.brace(m, front_m, 2.4, 4.9)
        K.brace(m, left, 5.0, 2.6)
        K.brace(m, right, 3.4, 5.8)

    ob = m.build()
    loc = attach_locations(d)
    return ob, [K.attach_point(k, loc[k], ob) for k in ("ATTACH_ADDITION", "ATTACH_DECK")]
