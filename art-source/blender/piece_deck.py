"""Upgrade piece, level 3: the DECK, a raised wooden deck at the back.

Local origin = the base's ATTACH_DECK point: on the ground, on the back
wall's outer face, centred on the back door. +Y points away from the house.
Deck surface sits just under the base's finished floor (FLOOR_Z), with
stairs at the back centre that land on the patio.
"""

import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import dh_house_kit as K  # noqa: E402

DIMS = {
    "width": 6.0,
    "depth": 4.0,
    "top": K.FLOOR_Z - 0.05,
    "rail_height": 0.95,
    "stair_width": 1.6,
    # Stairs leave the deck's +X side (the addition side) and land on the
    # patio, which sits behind the addition. stair_gap = horizontal distance
    # from the deck edge to the patio edge; the treads split it evenly.
    "stair_y": 2.7,          # stair centre, measured out from the house wall
    "stair_gap": 0.9,
    "stair_treads": 2,
    "landing_height": 0.08,  # patio surface height (piece_patio.DIMS["height"])
}



def build(palette, d=DIMS, name="piece_deck"):
    DW, DD, top = d["width"], d["depth"], d["top"]
    x0, x1 = -DW / 2, DW / 2
    m = K.MeshBuilder(name, palette)
    board_t, rim_h = 0.07, 0.2
    z_boards = top - board_t
    z_frame = z_boards - rim_h
    # posts (go to the ground)
    for x in (x0 + 0.1, 0.0, x1 - 0.1):
        m.box((x - 0.1, DD - 0.3, 0), (x + 0.1, DD - 0.1, z_frame + 0.02), "deck_dark")
    for x in (x0 + 0.1, x1 - 0.1):
        m.box((x - 0.1, DD / 2 - 0.1, 0), (x + 0.1, DD / 2 + 0.1, z_frame + 0.02), "deck_dark")
    # rim boards (fascia) round the three open sides + ledger at the house
    m.box((x0, DD - 0.06, z_frame), (x1, DD, z_boards), "deck_dark")
    m.box((x0, 0.0, z_frame), (x0 + 0.06, DD, z_boards), "deck_dark")
    m.box((x1 - 0.06, 0.0, z_frame), (x1, DD, z_boards), "deck_dark")
    m.box((x0, 0.0, z_frame), (x1, 0.06, z_boards), "deck_dark")
    # joists, visible from below the rim
    x = x0 + 0.6
    while x < x1 - 0.3:
        m.box((x - 0.05, 0.06, z_frame), (x + 0.05, DD - 0.06, z_boards), "deck_dark")
        x += 0.6
    # boards, parallel to the house, small gaps
    bw, gap = 0.30, 0.04
    y = 0.02
    while y < DD - 0.01:
        m.box((x0 - 0.03, y, z_boards), (x1 + 0.03, min(y + bw, DD + 0.03), top), "deck")
        y += bw + gap
    # railing on the two sides and the back, gap at the stairs
    rh, sw = d["rail_height"], d["stair_width"]
    zr0, zr1 = top, top + rh
    posts = []

    def rail_run(a, b):
        a, b = K.Vector((a[0], a[1], 0)), K.Vector((b[0], b[1], 0))
        L = (b - a).length
        dirv = (b - a).normalized()
        n_posts = max(1, int(round(L / 1.5)))
        for i in range(n_posts + 1):
            p = a + dirv * (L * i / n_posts)
            posts.append(p)
        # top rail, bottom rail
        side = K.Vector((-dirv.y, dirv.x, 0))
        for z, h, wd in ((zr1 - 0.04, 0.09, 0.16), (zr0 + 0.12, 0.06, 0.08)):
            m.beam(a + K.Vector((0, 0, z)), b + K.Vector((0, 0, z)), wd, h, (0, 0, 1), "trim")
        # balusters
        nb = int(L / 0.28)
        for i in range(1, nb):
            p = a + dirv * (L * i / nb)
            m.box((p.x - 0.03, p.y - 0.03, zr0 + 0.12), (p.x + 0.03, p.y + 0.03, zr1 - 0.04), "trim")
        return side

    inset = 0.08
    sy = d["stair_y"]
    rail_run((x0 + inset, 0.15), (x0 + inset, DD - inset))
    rail_run((x0 + inset, DD - inset), (x1 - inset, DD - inset))
    # +X side: opening for the stairs
    rail_run((x1 - inset, 0.15), (x1 - inset, sy - sw / 2))
    rail_run((x1 - inset, sy + sw / 2), (x1 - inset, DD - inset))
    seen = set()
    for p in posts:
        key = (round(p.x, 2), round(p.y, 2))
        if key in seen:
            continue
        seen.add(key)
        m.box((p.x - 0.07, p.y - 0.07, top), (p.x + 0.07, p.y + 0.07, zr1 + 0.08), "trim")
    # stairs down the +X side onto the patio: solid stepped blocks
    n = d["stair_treads"]
    run = d["stair_gap"] / n
    rise = (top - d["landing_height"]) / (n + 1)
    for i in range(1, n + 1):
        zt = top - rise * i
        m.box((x1, sy - sw / 2, 0), (x1 + run * i, sy + sw / 2, zt), "deck")
    # stringers
    for y in (sy - sw / 2 - 0.08, sy + sw / 2):
        m.beam((x1 - 0.02, y + 0.04, top - 0.08), (x1 + d["stair_gap"], y + 0.04, 0.05),
               0.08, 0.2, (-1, 0, 1), "deck_dark")
    return m.build()
