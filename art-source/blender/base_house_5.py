"""Base house 5: "Dutch colonial", a gambrel-roofed house.

One storey of wall under a two-pitch gambrel roof (steep 66 deg lower
slopes, shallow 24 deg upper slopes) so the second floor lives inside the
roof; the side gables are the classic barn-like pentagon with two upstairs
windows. Exterior brick chimney on the -X gable. +X is a gable end, so the
addition butts in (no tie-in).
"""

import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import dh_house_kit as K  # noqa: E402

DIMS = {
    "width": 8.4,
    "depth": 8.0,
    "wall_height": K.STORY_H,
    "lower_pitch": 66.0,
    "lower_run": 1.3,       # horizontal run of each steep lower slope
    "upper_pitch": 24.0,
    "eave": 0.2,            # steep slopes: keep the eave short so it clears the windows
    "rake": 0.3,
    "dormer_width": 4.6,      # front shed dormer (Dutch colonial signature)
    "dormer_setback": 0.35,   # dormer face behind the front wall line
    "dormer_pitch": 10.0,
}
NAME = "Dutch colonial (gambrel)"
RENDER_SCHEME = "Coastal Blue"
PALETTE = K.scheme_palette(RENDER_SCHEME)
FRONT_Y = -DIMS["depth"] / 2
DECK_CX, DECK_W = K.deck_layout(DIMS["width"] / 2)
PIECES = {"addition": {}, "deck": {"width": DECK_W}, "patio": {}}


def attach_locations(d=DIMS):
    return {"ATTACH_ADDITION": (d["width"] / 2, d["depth"] / 2 - K.ADDITION_BACK_INSET, 0),
            "ATTACH_DECK": (DECK_CX, d["depth"] / 2, 0)}


def roof_profile(d=DIMS):
    zt = K.FLOOR_Z + d["wall_height"]
    tl, tu = math.tan(math.radians(d["lower_pitch"])), math.tan(math.radians(d["upper_pitch"]))
    rl = K.RAFTER_H / math.cos(math.radians(d["lower_pitch"]))
    ru = K.RAFTER_H / math.cos(math.radians(d["upper_pitch"]))
    z_e = zt + rl
    lr = d["lower_run"]
    z_k = z_e + lr * tl
    half = d["depth"] / 2

    def top_z(y):
        s = half - abs(y)
        return z_e + s * tl if s <= lr else z_k + (s - lr) * tu

    def bottom_z(y):
        s = half - abs(y)
        return top_z(y) - (rl if s <= lr else ru)

    return {"z_e": z_e, "z_k": z_k, "ridge_z": top_z(0.0), "top_z": top_z, "bottom_z": bottom_z,
            "zt": zt}


def _walls(d):
    W, D = d["width"], d["depth"]
    zt = K.FLOOR_Z + d["wall_height"]
    x0, x1, y0, y1 = -W / 2, W / 2, -D / 2, D / 2
    O = K.Opening
    front = K.Wall((x0, y0), (x1, y0), zt, [
        O("window", 1.7, 1.3, 1.4, sill=0.8, shutters=True, flowerbox=True),
        O("door", W / 2, 1.2, 2.3),
        O("window", W - 1.7, 1.3, 1.4, sill=0.8, shutters=True, flowerbox=True),
    ])
    right = K.Wall((x1, y0), (x1, y1), zt, [O("window", 0.8, 0.9, 1.2, sill=0.9)])
    t_door = x1 - DECK_CX
    back = K.Wall((x1, y1), (x0, y1), zt, [
        O("window", 1.5, 1.1, 1.2, sill=0.9),
        O("door", t_door, 1.1, 2.3),
        O("window", W - 1.8, 1.3, 1.2, sill=0.9),
    ])
    left = K.Wall((x0, y1), (x0, y0), zt, [
        O("window", 1.6, 1.0, 1.2, sill=0.9, shutters=True),
        O("window", D - 1.4, 1.0, 1.2, sill=0.9, shutters=True),
    ])
    return front, right, back, left


def _dormer(m, d, pr, y0, mode):
    """Front shed dormer on the steep lower slope: a framed face with two
    windows, triangular cheeks and a low shed roof that dies into the
    upper slope. Its face sits dormer_setback behind the front wall."""
    hw = d["dormer_width"] / 2
    tl = math.tan(math.radians(d["lower_pitch"]))
    tu = math.tan(math.radians(d["upper_pitch"]))
    td = math.tan(math.radians(d["dormer_pitch"]))
    yk = y0 + d["lower_run"]
    yf = y0 + d["dormer_setback"]
    z_roof_f = pr["z_e"] + d["dormer_setback"] * tl      # lower slope where the face sits
    z_df = pr["z_k"] + 0.2                                # dormer wall top at the face
    # where the dormer roof meets the upper slope
    y_meet = (z_df - pr["z_k"] + yk * tu - yf * td) / (tu - td)
    z_meet = z_df + (y_meet - yf) * td
    face = K.Wall((hw, yf), (-hw, yf), z_df, [
        K.Opening("window", hw - 1.15, 1.0, 1.1, sill=z_roof_f + K.ROOF_T + 0.4 - K.FLOOR_Z),
        K.Opening("window", hw + 1.15, 1.0, 1.1, sill=z_roof_f + K.ROOF_T + 0.4 - K.FLOOR_Z),
    ], end_trim=0.0, z_base=z_roof_f)
    # the face line is drawn +X -> -X so its outward normal points -Y
    face.a, face.b = face.b, face.a
    face.dir = -face.dir
    face.n = -face.n
    if mode == "finished":
        face.z_bottom = z_roof_f - 0.05
        K.wall_finished(m, face, "siding")
        for sx in (-1, 1):
            x = sx * hw
            pts = [(x, yf, z_roof_f - 0.05), (x, yf, z_df), (x, y_meet, z_meet), (x, yk, pr["z_k"])]
            m.prism(pts, (-sx * K.WALL_T, 0, 0), "siding")
        # rafter-top heights match the main roof's at y_meet; run 0.2 past it so the back edge is buried
        K.shed_roof(m, -hw, hw, yf, y_meet + 0.2, z_df, z_meet + 0.2 * td, "finished", over=(0.18, 0.3))
    else:
        face.z_base = z_roof_f
        K.wall_framing(m, face)
        for sx in (-1, 1):
            x = sx * (hw - K.STUD_W / 2)
            for yy in (yf + 0.4, (yf + yk) / 2 + 0.2):
                zr = pr["z_e"] + (yy - y0) * tl if yy < yk else pr["z_k"]
                m.box((x - K.STUD_W / 2, yy - 0.06, zr), (x + K.STUD_W / 2, yy + 0.06,
                      z_df + (yy - yf) * td - 0.05), "framing")
        K.shed_roof(m, -hw, hw, yf, y_meet, z_df, z_meet, "framing")


def build(mode, d=DIMS, palette=PALETTE, name=None):
    W, D = d["width"], d["depth"]
    x0, x1, y0, y1 = -W / 2, W / 2, -D / 2, D / 2
    pr = roof_profile(d)
    zt = pr["zt"]
    lr = d["lower_run"]
    m = K.MeshBuilder(name or "base_house_5_%s" % mode, palette)
    walls = _walls(d)
    front, right, back, left = walls

    K.slab(m, x0, x1, y0, y1)
    m.box((x0 - 1.0, -0.8, 0), (x0, 0.8, K.SLAB_TOP), "concrete")
    m.box((-1.0, y0 - 1.0, 0), (1.0, y0, K.FLOOR_Z - 0.18), "concrete")
    K.steps(m, -0.8, 0.8, y0 - 1.0, K.FLOOR_Z - 0.18, n=1, run=0.4)
    m.box((DECK_CX - 0.7, y1, 0), (DECK_CX + 0.7, y1 + 0.6, K.FLOOR_Z - 0.25), "concrete")

    planes = []
    for ye, inward in ((y0, (0, 1, 0)), (y1, (0, -1, 0))):
        low = K.RoofPlane((0, ye, pr["z_e"]), (1, 0, 0), inward, d["lower_pitch"])
        yk = ye + inward[1] * lr
        up = K.RoofPlane((0, yk, pr["z_k"]), (1, 0, 0), inward, d["upper_pitch"])
        planes.append((low, up))
    vk = planes[0][0].v_for_run(lr)
    vr = planes[0][1].v_for_run(D / 2 - lr)
    samples_r = [lr, D / 2, D - lr]          # t along the side walls where the slope changes

    if mode == "finished":
        for w in walls:
            K.wall_finished(m, w, "siding")
        rk, ov = d["rake"], d["eave"]
        ve = -planes[0][0].v_for_run(ov)
        for low, up in planes:
            low.slab(m, [(x0 - rk, ve), (x1 + rk, ve), (x1 + rk, vk), (x0 - rk, vk)], "roof", "trim")
            up.slab(m, [(x0 - rk, 0), (x1 + rk, 0), (x1 + rk, vr), (x0 - rk, vr)], "roof", "trim")
        for yk in (y0 + lr, y1 - lr):        # knuckle trim + ridge cap
            m.beam((x0 - rk - 0.03, yk, pr["z_k"] + K.ROOF_T * 0.7), (x1 + rk + 0.03, yk, pr["z_k"] + K.ROOF_T * 0.7),
                   0.3, 0.2, (0, 0, 1), "roof_cap")
        m.beam((x0 - rk - 0.04, 0, pr["ridge_z"] + K.ROOF_T * 0.9), (x1 + rk + 0.04, 0, pr["ridge_z"] + K.ROOF_T * 0.9),
               K.RIDGE_CAP_W, K.RIDGE_CAP_H, (0, 0, 1), "roof_cap")
        K.gable_finished(m, right, lambda t: pr["top_z"](t - D / 2), "siding", samples=samples_r)
        K.gable_finished(m, left, lambda t: pr["top_z"](D / 2 - t), "siding", samples=samples_r)
        for w, flip in ((right, 1), (left, -1)):
            for t in (D / 2 - 1.5, D / 2 + 1.5):
                K.gable_window(m, w, t, zt + 1.75, 1.0)
            K.gable_window(m, w, D / 2, pr["z_k"] + 0.55, 0.5)
        K.chimney_exterior(m, x0, 0.0, pr["ridge_z"] + 0.6)
        _dormer(m, d, pr, y0, mode)
    else:
        K.floor_framing(m, x0, x1, y0, y1, skip_sheet=lambda r, c: (r >= 5 and c >= 2) or (r == 4 and c == 3))
        for w in walls:
            K.wall_framing(m, w)
        K.floor_framing(m, x0, x1, y0, y1, skip_sheet=lambda r, c: (r <= 1) or (c == 0 and r >= 4),
                        z_bottom=zt, z_top=zt + K.PLATFORM_T)
        vt = -planes[0][0].v_for_run(K.RAFTER_TAIL)
        for low, up in planes:
            low.rafters(m, [(x0, vt), (x1, vt), (x1, vk - 0.03), (x0, vk - 0.03)])
            up.rafters(m, [(x0, 0.03), (x1, 0.03), (x1, vr - 0.04), (x0, vr - 0.04)])
        for yk in (y0 + lr, y1 - lr):        # knuckle purlins + ridge board
            m.beam((x0, yk, pr["z_k"] - 0.3), (x1, yk, pr["z_k"] - 0.3), 0.14, 0.2, (0, 0, 1), "framing_dark")
        m.beam((x0, 0, pr["ridge_z"] - 0.15), (x1, 0, pr["ridge_z"] - 0.15), 0.08, K.RAFTER_H * 1.4, (0, 0, 1),
               "framing_dark")
        K.gable_framing(m, right, lambda t: pr["bottom_z"](t - D / 2))
        K.gable_framing(m, left, lambda t: pr["bottom_z"](D / 2 - t))
        K.brace(m, front, 0.3, 2.4)
        K.brace(m, left, 7.7, 5.4)
        K.brace(m, right, 2.0, 4.4)
        _dormer(m, d, pr, y0, mode)

    ob = m.build()
    loc = attach_locations(d)
    return ob, [K.attach_point(k, loc[k], ob) for k in ("ATTACH_ADDITION", "ATTACH_DECK")]
