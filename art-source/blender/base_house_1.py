"""Base house 1: the side-gabled cottage (colours come from the scheme).

Side-gabled (ridge along X), so both side walls are gable ends: the addition
butts cleanly into the +X gable wall under the rake. Exterior brick chimney
on the -X gable, shuttered windows with flower boxes, a little gabled hood
over the front door.

build(mode) with mode "finished" or "framing" -> (mesh object, [attach empties]).
Both modes are generated from DIMS below and the shared kit helpers, so they
share footprint, pivot, floor height, wall heights and roof pitch exactly.
"""

import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import dh_house_kit as K  # noqa: E402

DIMS = {
    "width": 7.5,            # X, outer wall lines
    "depth": 7.0,            # Y, outer wall lines
    "wall_height": 3.0,      # floor to top of top plate
    "pitch": 40.0,           # degrees
    "addition_center_y": -0.25,  # where the addition centres on the +X wall
    "chimney_y": 1.4,
}

PALETTE = K.scheme_palette("Sage Cottage")   # default scheme for the concept renders + GLBs
NAME = "Side-gable cottage"
RENDER_SCHEME = "Sage Cottage"
FRONT_Y = -DIMS["depth"] / 2      # the line placed at FrontSetback
PIECES = {"addition": {}, "deck": {"width": 6.0}, "patio": {}}


def attach_locations(d=DIMS):
    return {"ATTACH_ADDITION": (d["width"] / 2, d["addition_center_y"], 0),
            "ATTACH_DECK": (0, d["depth"] / 2, 0)}



def _walls(d):
    W, D = d["width"], d["depth"]
    zt = K.FLOOR_Z + d["wall_height"]
    x0, x1, y0, y1 = -W / 2, W / 2, -D / 2, D / 2
    O = K.Opening
    front = K.Wall((x0, y0), (x1, y0), zt, [
        O("window", 1.45, 1.3, 1.25, sill=0.85, shutters=True, flowerbox=True),
        O("door", W / 2, 1.3, 2.35),
        O("window", W - 1.45, 1.3, 1.25, sill=0.85, shutters=True, flowerbox=True),
    ])
    right = K.Wall((x1, y0), (x1, y1), zt, [
        O("window", D / 2, 1.2, 1.2, sill=0.9, shutters=True),
    ])
    back = K.Wall((x1, y1), (x0, y1), zt, [
        O("window", 1.5, 1.2, 1.2, sill=0.9),
        O("door", W / 2, 1.2, 2.3),
        O("window", W - 1.5, 1.2, 1.2, sill=0.9),
    ])
    left = K.Wall((x0, y1), (x0, y0), zt, [
        O("window", 5.0, 1.2, 1.2, sill=0.9, shutters=True, flowerbox=True),
    ])
    return front, right, back, left


def build(mode, d=DIMS, palette=PALETTE, name=None):
    W, D = d["width"], d["depth"]
    zt = K.FLOOR_Z + d["wall_height"]
    x0, x1, y0, y1 = -W / 2, W / 2, -D / 2, D / 2
    m = K.MeshBuilder(name or "base_house_1_%s" % mode, palette)

    # --- foundation (identical in both modes) ---------------------------
    K.slab(m, x0, x1, y0, y1)
    cy = d["chimney_y"]
    m.box((x0 - 1.0, cy - 0.8, 0), (x0, cy + 0.8, K.SLAB_TOP), "concrete")   # chimney footing
    m.box((-1.05, y0 - 1.15, 0), (1.05, y0, K.FLOOR_Z - 0.18), "concrete")   # front stoop
    m.box((-0.8, y0 - 1.6, 0), (0.8, y0 - 1.15, (K.FLOOR_Z - 0.18) / 2), "concrete")
    m.box((-0.8, y1, 0), (0.8, y1 + 0.6, K.FLOOR_Z - 0.25), "concrete")      # back step

    walls = _walls(d)
    front, right, back, left = walls

    if mode == "finished":
        for w in walls:
            K.wall_finished(m, w, "siding")
        roof = K.gable_roof(m, x0, x1, y0, y1, zt, d["pitch"], "finished", ridge_axis="x")
        # gable infill on the side walls, up to the roof plane (t -> y)
        K.gable_finished(m, right, lambda t: roof["top_z"](t - D / 2), "siding", samples=[D / 2])
        K.gable_finished(m, left, lambda t: roof["top_z"](D / 2 - t), "siding", samples=[D / 2])
        K.gable_window(m, right, D / 2, zt + 1.3, 0.8)
        K.gable_window(m, left, 4.4, zt + 1.05, 0.7)
        _chimney(m, x0, cy, roof["ridge_z"])
        _door_hood(m, y0, zt)
    else:
        K.floor_framing(m, x0, x1, y0, y1,
                        skip_sheet=lambda r, c: (r >= 4 and c >= 2) or (r == 3 and c == 3))
        for w in walls:
            K.wall_framing(m, w)
        roof = K.gable_roof(m, x0, x1, y0, y1, zt, d["pitch"], "framing", ridge_axis="x")
        K.gable_framing(m, right, lambda t: roof["bottom_z"](t - D / 2))
        K.gable_framing(m, left, lambda t: roof["bottom_z"](D / 2 - t))
        # temporary diagonal braces: a strong "being built" cue
        _brace(m, front, 0.3, 2.6)
        _brace(m, left, 6.7, 3.0)
        _brace(m, right, 0.3, 2.4)

    ob = m.build()
    loc = attach_locations(d)
    attach = [K.attach_point(k, loc[k], ob) for k in ("ATTACH_ADDITION", "ATTACH_DECK")]
    return ob, attach


def _chimney(m, x0, cy, ridge_z):
    # wide lower base, stepped shoulder, then the stack past the ridge line
    m.box((x0 - 0.9, cy - 0.7, K.SLAB_TOP), (x0 + 0.02, cy + 0.7, 3.2), "brick")
    m.box((x0 - 0.75, cy - 0.55, 3.2), (x0 + 0.02, cy + 0.55, 3.55), "brick")
    m.box((x0 - 0.7, cy - 0.45, 3.55), (x0 - 0.1, cy + 0.45, ridge_z + 0.75), "brick")
    m.box((x0 - 0.8, cy - 0.55, ridge_z + 0.75), (x0 - 0.0, cy + 0.55, ridge_z + 0.92), "concrete")


def _door_hood(m, y0, zt):
    """Shed canopy over the front door, tucked under the main eave."""
    z_door_top = K.FLOOR_Z + 2.35
    z_wall = z_door_top + 0.42       # canopy meets the wall here
    proj, drop, hw = 0.9, 0.28, 1.05
    a = (-hw, y0, z_wall)
    pts = [(-hw, y0 + 0.02, z_wall), (hw, y0 + 0.02, z_wall),
           (hw, y0 - proj, z_wall - drop), (-hw, y0 - proj, z_wall - drop)]
    m.prism(pts, (0, 0, K.ROOF_T), "roof", "trim")
    for x in (-hw + 0.12, hw - 0.12):
        m.beam((x, y0 - 0.02, z_wall - 0.6), (x, y0 - proj + 0.15, z_wall - drop + 0.02), 0.1, 0.1,
               (1, 0, 0), "trim")


def _brace(m, w, t0, t1):
    a = w.p(t0, -0.03, K.FLOOR_Z + K.PLATE_H + 0.05)
    b = w.p(t1, -0.03, w.z_top - 2 * K.PLATE_H - 0.05)
    m.beam(a, b, 0.12, 0.05, -w.n, "framing_dark")
