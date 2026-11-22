"""Base house 10: "Queen Anne" (small Victorian).

Two-storey block under a steep 45 deg FRONT gable (ridge along Y) with
king-post gable trim, an octagonal corner TURRET at the front -X corner
that rises above the eaves under a tall pointed roof with a finial, and a
front porch from the turret to the +X corner: turned posts, a spindle
frieze and corner brackets under a low shed roof.

+X is an eave side, but the walls are two storeys tall, so the addition's
roof stays under the main eave and butts into the wall (no tie-in).
"""

import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import dh_house_kit as K  # noqa: E402

DIMS = {
    "width": 8.0,
    "depth": 8.6,
    "wall_height": K.TWO_STORY_WALL,
    "pitch": 45.0,
    "eave": 0.35,
    "rake": 0.35,
    "turret_r": 1.55,         # octagon circumradius
    "turret_off": (0.2, -0.2),  # turret centre relative to the front -X wall corner
    "turret_top": 8.7,        # turret wall top (above the main eave)
    "turret_roof": 3.9,       # turret roof height
    "porch_depth": 2.0,
    "porch_floor": 0.5,
}
NAME = "Queen Anne"
RENDER_SCHEME = "Buttercream"
PALETTE = K.scheme_palette(RENDER_SCHEME)
FRONT_Y = -DIMS["depth"] / 2 - DIMS["porch_depth"]        # porch front line
DECK_CX, DECK_W = K.deck_layout(DIMS["width"] / 2)
DOOR_X = 0.9
PIECES = {"addition": {"pitch": 40.0}, "deck": {"width": DECK_W}, "patio": {}}
UP = K.STORY_H + K.PLATFORM_T


def attach_locations(d=DIMS):
    return {"ATTACH_ADDITION": (d["width"] / 2, d["depth"] / 2 - K.ADDITION_BACK_INSET, 0),
            "ATTACH_DECK": (DECK_CX, d["depth"] / 2, 0)}


def _walls(d):
    W, D = d["width"], d["depth"]
    zt = K.FLOOR_Z + d["wall_height"]
    x0, x1, y0, y1 = -W / 2, W / 2, -D / 2, D / 2
    O = K.Opening
    up = UP + 0.8
    front = K.Wall((x0, y0), (x1, y0), zt, [
        O("door", DOOR_X - x0, 1.15, 2.4),
        O("window", W - 1.5, 1.1, 1.6, sill=0.6),
        O("window", 3.9, 1.0, 1.4, sill=up, shutters=True),
        O("window", W - 1.5, 1.0, 1.4, sill=up, shutters=True),
    ])
    right = K.Wall((x1, y0), (x1, y1), zt, [
        O("window", 1.3, 1.0, 1.3, sill=0.8),
        O("window", 4.4, 1.0, 1.3, sill=up),
    ])
    t_door = x1 - DECK_CX
    back = K.Wall((x1, y1), (x0, y1), zt, [
        O("door", t_door, 1.1, 2.3),
        O("window", 6.4, 1.0, 1.3, sill=0.8, mullions=False),
        O("window", 4.0, 1.0, 1.3, sill=up, mullions=False),
    ])
    left = K.Wall((x0, y1), (x0, y0), zt, [
        O("window", 2.0, 1.0, 1.4, sill=0.7, shutters=True),
        O("window", 4.8, 0.9, 0.9, sill=1.2, mullions=False),
        O("window", 3.4, 1.0, 1.4, sill=up, shutters=True),
    ])
    return front, right, back, left


def _octagon(cx, cy, r):
    return [(cx + r * math.cos(math.radians(22.5 + 45 * i)), cy + r * math.sin(math.radians(22.5 + 45 * i)))
            for i in range(8)]


def _turret(m, d, mode):
    W, D = d["width"], d["depth"]
    cx, cy = -W / 2 + d["turret_off"][0], -D / 2 + d["turret_off"][1]
    r, zt = d["turret_r"], d["turret_top"]
    oc = _octagon(cx, cy, r)
    m.prism([(x, y, 0) for x, y in _octagon(cx, cy, r + K.SLAB_OVERHANG)], (0, 0, K.SLAB_TOP), "concrete")
    faces = [K.Wall(oc[i], oc[(i + 1) % 8], zt, [], end_trim=0.0) for i in range(8)]
    # faces that look at the street or the -X side get windows
    show = [f for f in faces if f.n.y < -0.3 or f.n.x < -0.3]
    if mode == "finished":
        m.prism([(x, y, K.SLAB_TOP) for x, y in oc], (0, 0, zt - K.SLAB_TOP), "siding")
        for z in (K.FLOOR_Z + K.STORY_H + 0.1, zt - 0.25):                      # storey band + frieze
            m.prism([(x, y, z) for x, y in _octagon(cx, cy, r + 0.05)], (0, 0, 0.2), "trim")
        for f in show:
            tc = f.L / 2
            for zb, h in ((K.FLOOR_Z + 0.8, 1.4), (K.FLOOR_Z + UP + 0.8, 1.3), (zt - 1.15, 0.6)):
                f.wbox(m, tc - 0.37, tc + 0.37, -K.TRIM_OUT, 0.02, zb - 0.1, zb + h + 0.1, "trim", cull_back=True)
                f.wbox(m, tc - 0.27, tc + 0.27, -K.TRIM_OUT - 0.02, 0.0, zb, zb + h, "glass", cull_back=True)
        er = r + 0.3
        base = [(x, y, zt) for x, y in _octagon(cx, cy, er)]
        apex = (cx, cy, zt + d["turret_roof"])
        for i in range(8):
            a, b = base[i], base[(i + 1) % 8]
            m.prism([a, b, apex], (0, 0, 0.0001), "roof", "roof")
        m.prism([(x, y, zt - 0.12) for x, y in _octagon(cx, cy, er)], (0, 0, 0.12), "trim")
        m.box((cx - 0.07, cy - 0.07, apex[2] - 0.1), (cx + 0.07, cy + 0.07, apex[2] + 0.55), "metal")
        m.box((cx - 0.13, cy - 0.13, apex[2] + 0.12), (cx + 0.13, cy + 0.13, apex[2] + 0.3), "metal")
    else:
        for f in faces:
            f.z_base = K.FLOOR_Z
            for zb, za in ((K.FLOOR_Z, K.FLOOR_Z + K.STORY_H), (K.FLOOR_Z + UP, zt)):
                f.wbox(m, 0.0, f.L, 0.03, 0.19, za - 0.1, za, "framing_dark")
                f.wbox(m, 0.0, f.L, 0.03, 0.19, zb, zb + 0.1, "framing_dark")
                for t in (0.06, f.L / 2):
                    f.wbox(m, t - 0.06, t + 0.06, 0.03, 0.19, zb + 0.1, za - 0.1, "framing")
        m.prism([(x, y, K.FLOOR_Z + K.STORY_H) for x, y in _octagon(cx, cy, r - 0.1)], (0, 0, K.PLATFORM_T),
                "subfloor")
        apex = (cx, cy, zt + d["turret_roof"] - 0.2)
        for x, y in _octagon(cx, cy, r + 0.2):
            m.beam((x, y, zt + 0.05), apex, 0.1, 0.16, (0, 0, 1), "framing")


def build(mode, d=DIMS, palette=PALETTE, name=None):
    W, D = d["width"], d["depth"]
    zt = K.FLOOR_Z + d["wall_height"]
    x0, x1, y0, y1 = -W / 2, W / 2, -D / 2, D / 2
    pd, pf = d["porch_depth"], d["porch_floor"]
    yp = y0 - pd
    xp0 = x0 + d["turret_off"][0] + d["turret_r"] * 0.85          # porch starts at the turret
    m = K.MeshBuilder(name or "base_house_10_%s" % mode, palette)
    walls = _walls(d)
    front, right, back, left = walls

    # --- foundation + porch floor (identical in both modes) ---------------
    K.slab(m, x0, x1, y0, y1)
    m.box((xp0, yp, 0), (x1 + 0.1, y0, pf - 0.08), "concrete")
    m.box((xp0 - 0.1, yp, pf - 0.08), (x1 + 0.1, y0, pf), "deck")
    K.steps(m, DOOR_X - 0.8, DOOR_X + 0.8, yp, pf, n=2, run=0.32)
    m.box((DECK_CX - 0.7, y1, 0), (DECK_CX + 0.7, y1 + 0.6, K.FLOOR_Z - 0.25), "concrete")

    z_beam = K.FLOOR_Z + 2.65
    posts = [xp0 + 0.3, DOOR_X - 1.0, DOOR_X + 1.0, x1 - 0.15]
    _turret(m, d, mode)
    if mode == "finished":
        for w in walls:
            K.wall_finished(m, w, "siding")
        roof = K.gable_roof(m, x0, x1, y0, y1, zt, d["pitch"], "finished", ridge_axis="y",
                            eave=d["eave"], rake=d["rake"])
        K.gable_finished(m, front, lambda t: roof["top_z"](x0 + t), "siding", samples=[W / 2])
        K.gable_finished(m, back, lambda t: roof["top_z"](x1 - t), "siding", samples=[W / 2])
        K.gable_window(m, front, W / 2, zt + 1.3, 0.9)
        # king-post gable trim at the apex
        bz = lambda t: roof["top_z"](x0 + t) - K.RAFTER_H  # noqa: E731
        z_col = zt + 2.4
        half = (bz(W / 2) - z_col) / math.tan(math.radians(d["pitch"]))
        front.wbox(m, W / 2 - half, W / 2 + half, -0.09, 0.0, z_col, z_col + 0.14, "trim", cull_back=True)
        front.wbox(m, W / 2 - 0.07, W / 2 + 0.07, -0.09, 0.0, z_col, bz(W / 2), "trim", cull_back=True)
        for s in (-1, 1):
            a = front.p(W / 2 + s * half * 0.75, -0.06, z_col + 0.1)
            b = front.p(W / 2 + s * 0.05, -0.06, z_col + (bz(W / 2) - z_col) * 0.75)
            m.beam(a, b, 0.1, 0.08, -front.n, "trim")
        # porch: turned posts, brackets, spindle frieze, shed roof
        for x in posts:
            m.box((x - 0.1, yp + 0.12, pf), (x + 0.1, yp + 0.32, z_beam), "trim")
            m.box((x - 0.15, yp + 0.07, pf + 0.9), (x + 0.15, yp + 0.37, pf + 1.05), "trim")
            for s in (-1, 1):
                if xp0 + 0.2 < x + s * 0.5 < x1:
                    m.beam((x + s * 0.08, yp + 0.22, z_beam - 0.45), (x + s * 0.5, yp + 0.22, z_beam - 0.02),
                           0.08, 0.06, (0, 1, 0), "trim")
        m.box((xp0, yp + 0.07, z_beam), (x1, yp + 0.37, z_beam + 0.25), "trim")
        m.box((xp0, yp + 0.15, z_beam - 0.5), (x1, yp + 0.29, z_beam - 0.42), "trim")
        x = xp0 + 0.45
        while x < x1 - 0.3:
            m.box((x - 0.025, yp + 0.19, z_beam - 0.42), (x + 0.025, yp + 0.25, z_beam), "trim")
            x += 0.3
        K.shed_roof(m, xp0, x1, yp + 0.22, y0, z_beam + 0.45, z_beam + 0.95, "finished", over=(0.15, 0.3))
    else:
        K.floor_framing(m, x0, x1, y0, y1, skip_sheet=lambda r, c: (r >= 5 and c >= 2) or (r == 4 and c == 3))
        lower, upper = zip(*[K.split_stories(w) for w in walls])
        for w in lower + upper:
            K.wall_framing(m, w)
        K.floor_framing(m, x0, x1, y0, y1, skip_sheet=lambda r, c: (r <= 1 and c <= 1) or (r >= 5 and c == 2),
                        z_bottom=K.FLOOR_Z + K.STORY_H, z_top=K.FLOOR_Z + UP)
        roof = K.gable_roof(m, x0, x1, y0, y1, zt, d["pitch"], "framing", ridge_axis="y")
        K.gable_framing(m, front, lambda t: roof["bottom_z"](x0 + t))
        K.gable_framing(m, back, lambda t: roof["bottom_z"](x1 - t))
        for x in posts:
            m.box((x - 0.07, yp + 0.15, pf), (x + 0.07, yp + 0.29, z_beam), "framing")
        m.box((xp0, yp + 0.12, z_beam), (x1, yp + 0.32, z_beam + 0.24), "framing_dark")
        K.shed_roof(m, xp0, x1, yp + 0.22, y0, z_beam + 0.45, z_beam + 0.95, "framing")
        K.brace(m, lower[1], 3.0, 5.6)
        K.brace(m, upper[3], 1.0, 3.6)
        K.brace(m, lower[2], 5.4, 7.6)

    ob = m.build()
    loc = attach_locations(d)
    return ob, [K.attach_point(k, loc[k], ob) for k in ("ATTACH_ADDITION", "ATTACH_DECK")]
