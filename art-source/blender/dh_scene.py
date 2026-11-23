"""Scene helpers shared by the render drivers: fitting the upgrade pieces
to a base, the level-4 placement rule + clearances, the lot scene, the
dog stand-in, and the garage's driveway (a separate, decorative ground
object). The level-2 piece is the GARAGE (#780, #817); the concept addition
module is kept only for comparison. Every base module exposes DIMS, NAME, RENDER_SCHEME, FRONT_Y,
PIECES, attach_locations() and build(mode, palette=..., name=...)."""

import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bpy  # noqa: E402
import dh_house_kit as K  # noqa: E402
import piece_addition  # noqa: E402
import piece_garage  # noqa: E402
import piece_deck  # noqa: E402
import piece_patio  # noqa: E402

# Starting corner lot, in LOT terms (see README): s = from the side road's
# centreline into the lot, d = from the faced road's centreline into the lot.
LOT_SIZE = 30.0
FENCE_S_ROAD = 7.75
FENCE_S_FAR = LOT_SIZE - 2.0
FENCE_D_REAR = LOT_SIZE - 2.0
SIDEWALK_D = 5.75
FRONT_SETBACK = 3.75             # sidewalk outer edge -> the base's FRONT_Y line (was 2.75; +1.0 m, #773)
FRONT_D = SIDEWALK_D + FRONT_SETBACK   # = 9.5 from the faced road's centreline
CURB_D = 3.0                     # road half-width: the curb line
DRIVE_W = 3.0                    # proposed one-car driveway width (#803 Open)
DRIVE_JOINT = 3.0                # control-joint spacing along the driveway
DOG = (1.26, 1.50, 1.58)          # Cube Pets adult dog, in-game (w, l, h)


def fit_pieces(base):
    """Per-base piece parameters: garage/deck/patio dims merged with the
    base's PIECES overrides, plus the deck stair gap and stair position
    solved so the deck's side stairs land on the patio.

    The garage reuses the base's addition overrides (wall height, pitch,
    tie-in, solved per base so the roof dies into the main roof) and lowers
    the pitch so its deeper roof keeps the addition's ridge height; that
    keeps every solved tie-in valid. PIECES["garage"] overrides win."""
    loc = base.attach_locations()
    a_over = base.PIECES.get("addition", {})
    add_ref = dict(piece_addition.DIMS, **a_over)
    add_d = dict(piece_garage.DIMS, **{k: v for k, v in a_over.items() if k in ("wall_height", "tie_in")})
    rise = add_ref["depth"] / 2 * math.tan(math.radians(add_ref["pitch"]))
    add_d["pitch"] = round(math.degrees(math.atan2(rise, add_d["depth"] / 2)), 2)
    add_d.update(base.PIECES.get("garage", {}))
    deck_d = dict(piece_deck.DIMS, **base.PIECES.get("deck", {}))
    patio_d = dict(piece_patio.DIMS, **base.PIECES.get("patio", {}))
    patio_d["width"] = add_d["width"]
    ax, ay, _ = loc["ATTACH_ADDITION"]
    dx, dy, _ = loc["ATTACH_DECK"]
    gap = (ax + K.SLAB_OVERHANG) - (dx + deck_d["width"] / 2)
    py0 = ay + add_d["depth"] / 2 + K.SLAB_OVERHANG
    py1 = py0 + patio_d["depth"]
    sw = deck_d["stair_width"]
    lo = max(py0, dy) + sw / 2 + 0.15
    hi = min(py1, dy + deck_d["depth"]) - sw / 2 - 0.15
    assert 0.5 <= gap <= 1.3, "deck stairs can't bridge %.2f m to the patio" % gap
    assert lo <= hi, "deck and patio don't overlap enough for the stairs"
    deck_d.update(stair_gap=gap, stair_y=(lo + hi) / 2 - dy, landing_height=patio_d["height"])
    return add_d, deck_d, patio_d


def build_level(base, palette, level, mode="finished", name=None):
    """Base + the first (level - 1) pieces of the fixed path. Returns all
    objects (meshes and attach empties); the base is first."""
    add_d, deck_d, patio_d = fit_pieces(base)
    house, att = base.build(mode, palette=palette, name=name)
    objs = [house] + att
    if level >= 2:
        a, a_att = piece_garage.build(palette, d=add_d)
        a.location = att[0].location.copy()
        objs += [a] + a_att
    if level >= 3:
        dk = piece_deck.build(palette, d=deck_d)
        dk.location = att[1].location.copy()
        objs.append(dk)
    if level >= 4:
        pt = piece_patio.build(palette, d=patio_d)
        pt.location = att[0].location + a_att[0].location
        objs.append(pt)
    return objs


def driveway(base, g_d=None, name="driveway", palette=None):
    """PROPOSAL (#803 Open): a plain one-car concrete driveway from the
    garage apron to the curb, as its own object in base-local coordinates
    (same pivot as the house). It skips the sidewalk, which runs through
    unchanged, so it is two slabs: front yard and grass verge. Ground
    decoration only: no walk-network meaning."""
    if g_d is None:
        g_d = fit_pieces(base)[0]
    ax, ay, _ = base.attach_locations()["ATTACH_ADDITION"]
    cx = ax + g_d["width"] / 2
    y_apron = ay - g_d["depth"] / 2 - K.SLAB_OVERHANG - g_d["apron"]
    road_cl = base.FRONT_Y - FRONT_D
    walk_in, walk_out = road_cl + SIDEWALK_D, road_cl + SIDEWALK_D - 2.0
    curb = road_cl + CURB_D
    pal = dict(palette or K.COMMON)
    pal["drive"] = pal.get("concrete", K.COMMON["concrete"])
    pal["drive_joint"] = K.darken(pal["drive"], 0.82)
    m = K.MeshBuilder(name, pal)
    x0, x1 = cx - DRIVE_W / 2, cx + DRIVE_W / 2
    z = 0.025
    for ya, yb in ((walk_in, y_apron), (curb, walk_out)):
        if yb - ya < 0.05:
            continue
        m.box((x0, ya, -0.03), (x1, yb, z), "drive")
        n = int((yb - ya) / DRIVE_JOINT)
        for i in range(1, n + 1):
            yj = ya + (yb - ya) * i / (n + 1)
            m.box((x0 + 0.03, yj - 0.02, z), (x1 - 0.03, yj + 0.02, z + 0.006), "drive_joint")
    m.box((cx - 0.02, walk_in, z), (cx + 0.02, y_apron, z + 0.006), "drive_joint")
    return m.build(), {"x": (x0, x1), "y": (curb, y_apron), "walk": (walk_out, walk_in)}


def meshes(objs):
    return [o for o in objs if o.type == "MESH"]


def composite_bounds(objs):
    bpy.context.view_layer.update()
    xs, ys = [], []
    for o in meshes(objs):
        for v in o.data.vertices:
            w = o.matrix_world @ v.co
            xs.append(w.x)
            ys.append(w.y)
    return (min(xs), max(xs), min(ys), max(ys))


def level4_placement(bounds, front_y, addition_toward_road=True):
    """THE PLACEMENT RULE: centre the level-4 composite across the fence
    envelope; the base's FRONT_Y line keeps FrontSetback."""
    x0, x1, y0, y1 = bounds
    road_cl_y = front_y - FRONT_D
    s_mid = (FENCE_S_ROAD + FENCE_S_FAR) / 2
    x_mid = (x0 + x1) / 2
    if addition_toward_road:
        s_pivot = s_mid + x_mid
        side_road_x = s_pivot
        quad_x = (side_road_x - LOT_SIZE, side_road_x)
        fence_x = (side_road_x - FENCE_S_FAR, side_road_x - FENCE_S_ROAD)
    else:
        s_pivot = s_mid - x_mid
        side_road_x = -s_pivot
        quad_x = (side_road_x, side_road_x + LOT_SIZE)
        fence_x = (side_road_x + FENCE_S_ROAD, side_road_x + FENCE_S_FAR)
    road_edge = fence_x[1] if addition_toward_road else fence_x[0]
    far_edge = fence_x[0] if addition_toward_road else fence_x[1]
    clear = {
        "road_side_to_fence": round(abs(road_edge - (x1 if addition_toward_road else x0)), 2),
        "far_side_to_fence": round(abs((x0 if addition_toward_road else x1) - far_edge), 2),
        "front_to_sidewalk": round(y0 - (road_cl_y + SIDEWALK_D), 2),
        "rear_to_fence": round((road_cl_y + FENCE_D_REAR) - y1, 2),
    }
    return {"addition_toward_road": addition_toward_road, "pivot_s_from_side_road": round(s_pivot, 2),
            "side_road_x": side_road_x, "road_cl_y": road_cl_y,
            "quad": (quad_x[0], quad_x[1], road_cl_y, road_cl_y + LOT_SIZE),
            "fence": (fence_x[0], fence_x[1], 0.0, road_cl_y + FENCE_D_REAR),
            "composite": [round(v, 2) for v in bounds], "clearance": clear,
            "fits": all(v >= 0 for v in clear.values())}


def dog_box(loc, yaw_deg=0):
    pal = {"dog": "#D99A52", "dog_dark": "#8A5530", "trim": "#FFF7EA"}
    m = K.MeshBuilder("dog_scale_box", pal)
    w, l, h = DOG
    leg = 0.24
    for sx in (-1, 1):
        for sy in (-1, 1):
            cx, cy = sx * (w / 2 - leg / 2 - 0.08), sy * (l / 2 - leg / 2 - 0.1) + 0.12
            m.box((cx - leg / 2, cy - leg / 2, 0), (cx + leg / 2, cy + leg / 2, 0.42), "dog")
    m.box((-w * 0.38, -l / 2 + 0.45, 0.38), (w * 0.38, l / 2, 0.98), "dog")
    m.box((-w / 2, -l / 2, 0.78), (w / 2, -l / 2 + 0.72, h - 0.12), "dog")
    m.box((-w * 0.22, -l / 2 - 0.12, 0.8), (w * 0.22, -l / 2 + 0.05, 1.08), "trim")
    m.box((-0.08, -l / 2 - 0.16, 1.0), (0.08, -l / 2 - 0.1, 1.08), "dog_dark")
    for sx in (-1, 1):
        m.box((sx * (w / 2 - 0.12) - 0.13, -l / 2 + 0.2, h - 0.12),
              (sx * (w / 2 - 0.12) + 0.13, -l / 2 + 0.48, h), "dog_dark")
    m.beam((0, l / 2 - 0.05, 0.9), (0, l / 2 + 0.25, 1.2), 0.12, 0.12, (0, -1, 1), "dog")
    ob = m.build()
    ob.location = loc
    ob.rotation_euler = (0, 0, math.radians(yaw_deg))
    return ob


def street(road_cl_y, x_range=(-150, 150), walk_xs=(), front_y=None, side_road_x=None, grass=True):
    """Grass, the faced road and its sidewalks (+ optional side road),
    front walkways at walk_xs from the sidewalk to front_y."""
    g = K.MeshBuilder("street", dict(K.COMMON))
    xa, xb = x_range
    if grass:
        g.box((xa, road_cl_y - 80, -0.06), (xb, road_cl_y + 120, -0.01), "grass")
    for off in (3.75, -5.75):
        g.box((xa, road_cl_y + off, -0.05), (xb, road_cl_y + off + 2, 0.03), "sidewalk")
        if side_road_x is not None:
            g.box((side_road_x + off, road_cl_y - 80, -0.05), (side_road_x + off + 2, road_cl_y + 120, 0.03),
                  "sidewalk")
    g.box((xa, road_cl_y - 3, -0.05), (xb, road_cl_y + 3, 0.04), "road")
    if side_road_x is not None:
        g.box((side_road_x - 3, road_cl_y - 80, -0.05), (side_road_x + 3, road_cl_y + 120, 0.04), "road")
    for x in walk_xs:
        g.box((x - 0.6, road_cl_y + SIDEWALK_D, -0.05), (x + 0.6, front_y, 0.03), "sidewalk")
    return g.build()


def lot_lines(P, house_half_w, addition_w, addition_side=1):
    ln = K.MeshBuilder("lot_lines", dict(K.COMMON))
    t, z = 0.16, 0.07

    def seg(ax, ay, bx, by, key, dash=None):
        L = math.hypot(bx - ax, by - ay)
        n = int(L / dash) if dash else 1
        for i in range(n):
            if dash and i % 2:
                continue
            f0, f1 = i / n, (i + 1) / n
            px0, py0 = ax + (bx - ax) * f0, ay + (by - ay) * f0
            px1, py1 = ax + (bx - ax) * f1, ay + (by - ay) * f1
            ln.box((min(px0, px1) - t / 2, min(py0, py1) - t / 2, 0),
                   (max(px0, px1) + t / 2, max(py0, py1) + t / 2, z), key)

    qx0, qx1, qy0, qy1 = P["quad"]
    for a, b in (((qx0, qy0), (qx1, qy0)), ((qx1, qy0), (qx1, qy1)), ((qx1, qy1), (qx0, qy1)),
                 ((qx0, qy1), (qx0, qy0))):
        seg(*a, *b, "lot_line", dash=0.8)
    fx0, fx1, fy0, fy1 = P["fence"]
    seg(fx0, fy0, fx0, fy1, "fence_line")
    seg(fx0, fy1, fx1, fy1, "fence_line")
    seg(fx1, fy1, fx1, fy0, "fence_line")
    seg(fx0, fy0, -house_half_w, fy0, "fence_line")
    seg(house_half_w + addition_w, fy0, fx1, fy0, "fence_line")
    return ln.build()


def frame_hint(x, y):
    h = K.MeshBuilder("frame_hint", {"g": "#000000"})
    h.box((x - 0.5, y - 0.5, 0), (x + 0.5, y + 0.5, 0.1), "g")
    return h.build()
