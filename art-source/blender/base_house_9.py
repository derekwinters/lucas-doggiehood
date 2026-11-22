"""Base house 9: "Spanish mission".

Long stucco block under a low 18 deg hip roof (reads as clay tile in the
Adobe Tile scheme; any scheme's roof style works), with a centred front
LOGGIA: three round arches across the front, one on each side, and a
curvilinear mission parapet with a little bell niche rising above it. A
low tile-capped courtyard wall encloses the front garden on the -X side.
Arched windows and arched front door, stucco chimney.

+X is a hip (eave) side, so the addition's roof ties in.
"""

import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import dh_house_kit as K  # noqa: E402

DIMS = {
    "width": 10.4,
    "depth": 7.4,
    "wall_height": 3.0,
    "pitch": 18.0,
    "eave": 0.45,
    "loggia_width": 4.8,
    "loggia_depth": 2.0,
    "arcade_top": 3.35,     # top of the arcade band (flat loggia roof behind the parapet)
    "arch_spring": 2.25,
    "parapet_peak": 1.45,   # parapet rises this far above the arcade band at its centre
    "court_wall": 1.15,     # courtyard wall height
}
NAME = "Spanish mission"
RENDER_SCHEME = "Adobe Tile"
PALETTE = K.scheme_palette(RENDER_SCHEME)
FRONT_Y = -DIMS["depth"] / 2 - DIMS["loggia_depth"]      # loggia / courtyard wall front line
DECK_CX, DECK_W = K.deck_layout(DIMS["width"] / 2)
PIECES = {
    "addition": {"pitch": 18.0, "wall_height": 2.6, "tie_in": 1.5},
    "deck": {"width": DECK_W},
    "patio": {},
}
ARCADE_DEPTH = 0.35
PIER = 0.45


def attach_locations(d=DIMS):
    return {"ATTACH_ADDITION": (d["width"] / 2, d["depth"] / 2 - K.ADDITION_BACK_INSET, 0),
            "ATTACH_DECK": (DECK_CX, d["depth"] / 2, 0)}


def _walls(d):
    W, D = d["width"], d["depth"]
    zt = K.FLOOR_Z + d["wall_height"]
    x0, x1, y0, y1 = -W / 2, W / 2, -D / 2, D / 2
    O = K.Opening
    front = K.Wall((x0, y0), (x1, y0), zt, [
        O("window", 1.5, 1.1, 1.6, sill=0.7, arched=True, mullions=False, flowerbox=True),
        O("door", W / 2, 1.3, 2.55, arched=True),
        O("window", W / 2 - 1.55, 0.8, 1.4, sill=0.8, arched=True, mullions=False),
        O("window", W / 2 + 1.55, 0.8, 1.4, sill=0.8, arched=True, mullions=False),
        O("window", W - 1.5, 1.1, 1.6, sill=0.7, arched=True, mullions=False, flowerbox=True),
    ])
    right = K.Wall((x1, y0), (x1, y1), zt, [O("window", 1.3, 0.9, 1.3, sill=0.9, arched=True, mullions=False)])
    t_door = x1 - DECK_CX
    back = K.Wall((x1, y1), (x0, y1), zt, [
        O("door", t_door, 1.1, 2.3),
        O("window", 6.8, 1.1, 1.3, sill=0.9, arched=True, mullions=False),
        O("window", 9.2, 0.9, 1.2, sill=1.0, arched=True, mullions=False),
    ])
    left = K.Wall((x0, y1), (x0, y0), zt, [
        O("window", 2.2, 1.0, 1.4, sill=0.8, arched=True, mullions=False),
        O("window", 5.2, 1.0, 1.4, sill=0.8, arched=True, mullions=False),
    ])
    return front, right, back, left


def _loggia_faces(d):
    D = d["depth"]
    y0 = -D / 2
    hw, ld = d["loggia_width"] / 2, d["loggia_depth"]
    yf = y0 - ld
    front = K.Wall((-hw, yf), (hw, yf), d["arcade_top"], [])
    left = K.Wall((-hw, y0), (-hw, yf), d["arcade_top"], [])
    right = K.Wall((hw, yf), (hw, y0), d["arcade_top"], [])
    return front, left, right


def _parapet(m, w, z0, peak):
    """Mission parapet on top of the arcade face: shoulders, two scrolls
    and a raised centre with a bell niche."""
    L = w.L
    tc = L / 2
    prof = [(0.0, 0.3), (0.55, 0.3), (0.75, 0.55), (1.05, 0.7), (1.35, 0.75), (1.55, 0.95),
            (1.65, 1.15), (tc - 0.55, peak - 0.1), (tc - 0.3, peak), (tc, peak)]
    pts = [(t, z0 + h) for t, h in prof] + [(L - t, z0 + h) for t, h in reversed(prof[:-1])]
    poly = [(L, z0), (0.0, z0)] + pts
    m.prism([w.p(t, 0, z) for t, z in poly], -w.n * ARCADE_DEPTH, "siding", "trim")
    # bell niche: a small dark arch in the raised centre
    r = 0.28
    zs = z0 + peak - 0.75
    niche = [(tc - r, z0 + peak - 1.15)] + K.arch_points(tc, zs, r) + [(tc + r, z0 + peak - 1.15)]
    niche = [niche[0]] + niche[1:-1] + [niche[-1]]
    m.prism([w.p(t, -0.01, z) for t, z in niche], w.n * 0.02, "door_panel")


def build(mode, d=DIMS, palette=PALETTE, name=None):
    W, D = d["width"], d["depth"]
    zt = K.FLOOR_Z + d["wall_height"]
    x0, x1, y0, y1 = -W / 2, W / 2, -D / 2, D / 2
    hw, ld = d["loggia_width"] / 2, d["loggia_depth"]
    yf = y0 - ld
    za = d["arcade_top"]
    m = K.MeshBuilder(name or "base_house_9_%s" % mode, palette)
    walls = _walls(d)
    front, right, back, left = walls
    lf, ll, lr = _loggia_faces(d)

    # --- foundation, loggia floor, courtyard wall footing (both modes) ----
    K.slab(m, x0, x1, y0, y1)
    m.box((-hw, yf, 0), (hw, y0, K.FLOOR_Z - 0.2), "stone_a")
    K.steps(m, -0.9, 0.9, yf, K.FLOOR_Z - 0.2, n=1, run=0.4, mat="stone_a")
    m.box((DECK_CX - 0.7, y1, 0), (DECK_CX + 0.7, y1 + 0.6, K.FLOOR_Z - 0.25), "concrete")
    m.box((x0 - 0.1, yf - 0.05, 0), (-hw, yf + 0.3, 0.12), "concrete")
    m.box((x0 - 0.1, yf, 0), (x0 + 0.25, y0, 0.12), "concrete")
    m.box((-hw - 2.6, yf + 0.4, 0), (-hw - 0.5, y0 - 0.2, 0.05), "stone_b")     # courtyard paving

    if mode == "finished":
        for w in walls:
            K.wall_finished(m, w, "siding")
        roof = K.hip_roof(m, x0, x1, y0, y1, zt, d["pitch"], "finished", eave=d["eave"])
        K.arcade(m, lf, 0.0, lf.L, K.FLOOR_Z - 0.2, d["arch_spring"], za, 3, PIER, ARCADE_DEPTH)
        K.arcade(m, ll, 0.0, ll.L - ARCADE_DEPTH, K.FLOOR_Z - 0.2, d["arch_spring"], za, 1, PIER, ARCADE_DEPTH)
        K.arcade(m, lr, ARCADE_DEPTH, lr.L, K.FLOOR_Z - 0.2, d["arch_spring"], za, 1, PIER, ARCADE_DEPTH)
        m.box((-hw + 0.1, yf + 0.1, za - 0.25), (hw - 0.1, y0, za - 0.05), "siding")             # flat loggia roof
        _parapet(m, lf, za, d["parapet_peak"])
        for w, t0, t1 in ((ll, 0.0, ll.L), (lr, 0.0, lr.L)):                                    # side copings
            w.wbox(m, t0, t1, -0.05, ARCADE_DEPTH + 0.05, za, za + 0.12, "trim")
        # courtyard wall with a tile cap
        h = d["court_wall"]
        m.box((x0, yf, 0.12), (-hw, yf + 0.25, h), "siding")
        m.box((x0, yf, 0.12), (x0 + 0.25, y0, h), "siding")
        m.box((x0 - 0.06, yf - 0.06, h), (-hw, yf + 0.31, h + 0.12), "roof_cap")
        m.box((x0 - 0.06, yf - 0.06, h), (x0 + 0.31, y0, h + 0.12), "roof_cap")
        K.chimney_through_roof(m, 2.6, 1.2, zt, roof["ridge_z"] + 0.7, w=0.8, d=0.6, mat="siding")
        m.box((2.15, 0.85, roof["ridge_z"] + 0.85), (3.05, 1.55, roof["ridge_z"] + 1.0), "roof_cap")
    else:
        K.floor_framing(m, x0, x1, y0, y1, skip_sheet=lambda r, c: (r >= 4 and c >= 3) or (r == 3 and c == 4))
        for w in walls:
            K.wall_framing(m, w)
        K.hip_roof(m, x0, x1, y0, y1, zt, d["pitch"], "framing", eave=d["eave"])
        # loggia: posts at the piers, headers, flat-roof joists; parapet studs
        ow = (lf.L - 4 * PIER) / 3
        for i in range(4):
            x = -hw + PIER / 2 + i * (PIER + ow)
            m.box((x - 0.08, yf + 0.1, K.FLOOR_Z - 0.2), (x + 0.08, yf + 0.26, za - 0.25), "framing")
        for y in (y0 - 0.25,):
            for x in (-hw + PIER / 2, hw - PIER / 2):
                m.box((x - 0.08, y - 0.08, K.FLOOR_Z - 0.2), (x + 0.08, y + 0.08, za - 0.25), "framing")
        m.box((-hw, yf + 0.06, za - 0.49), (hw, yf + 0.3, za - 0.25), "framing_dark")
        for x in (-hw + 0.1, hw - 0.26):
            m.box((x, yf, za - 0.49), (x + 0.16, y0, za - 0.25), "framing_dark")
        x = -hw + 0.5
        while x < hw - 0.3:
            m.box((x - 0.06, yf + 0.3, za - 0.25), (x + 0.06, y0, za - 0.05), "framing")
            x += K.JOIST_SPACING
        t = 0.3
        while t < lf.L - 0.2:
            h = d["parapet_peak"] * max(0.0, 1 - abs(t - lf.L / 2) / (lf.L / 2)) * 0.9 + 0.25
            lf.wbox(m, t - 0.06, t + 0.06, 0.1, 0.26, za - 0.25, za + h, "framing")
            t += K.STUD_SPACING
        K.brace(m, front, 0.3, 1.0)
        K.brace(m, left, 6.8, 4.0)
        K.brace(m, right, 2.6, 5.6)

    ob = m.build()
    loc = attach_locations(d)
    return ob, [K.attach_point(k, loc[k], ob) for k in ("ATTACH_ADDITION", "ATTACH_DECK")]
