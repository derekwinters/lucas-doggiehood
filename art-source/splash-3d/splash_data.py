"""Measured data for the 3D splash houses (no bpy here; plain Python).

Everything in this file is measured on the splash art, `Assets/Art/Splash/cover-art.png`
(2816 x 1536 px). Screen coordinates are pixels in that image, x to the right, y down.
Colours are the median of the original's pixels on each surface (lit front faces), as
sRGB hex.

The 3D builder (`build_scene.py`) turns each screen-space shape into real geometry by
back-projecting it through the solved splash camera (`camera()` below), which gives each
house its real size and proportions: its front is measured as painted (facing the viewer).
The builder then turns every house a quarter turn so its front faces the road, as real and
game houses do (#935), and places it along its side of the street. Depths the painting
cannot tell us (how deep a house is, how far its side wing sits back) are the metre values
in `DEPTH` and per house in `depth`.
"""
import math

W, H = 2816, 1536

# --------------------------------------------------------------------------
# Camera solve inputs (the painted street edges, measured on the original)
# --------------------------------------------------------------------------
# Each edge is a polyline (far, middle, near) of a line that runs along the street.
STREET_EDGES = {
    "lawn_l": [(1316, 700), (304, 1320), (108, 1440)],   # lawn / sidewalk edge, left
    "road_l": [(1321, 740), (612, 1380), (443, 1536)],   # sidewalk / kerb edge, left
    "kerb_l": [(1324, 740), (667, 1380), (510, 1536)],   # kerb foot, left
    "road_r": [(1621, 740), (2247, 1340), (2440, 1536)],
    "kerb_r": [(1618, 740), (2205, 1340), (2398, 1536)],
    "lawn_r": [(1640, 700), (2600, 1320), (2788, 1440)],
}
# Two sidewalk expansion joints one slab apart (y of each joint), per side.
JOINTS = {"l": (1418.0, 1253.0), "r": (1422.0, 1256.0)}

CAMERA_HEIGHT = 2.8      # m: sets the scale. Gives a 5.9 m road, 1.43 m sidewalks, ~2.4 m near doors.
# Focal length in pixels. The square-slab solve gives ~1700 px (see camera()), but at that
# width every house's street-side wall shows wide and hides the next house's wing, which
# the painting never shows. A longer lens thins those walls. Chosen after comparing
# 1700 / 2400 / 3200 renders (README, "Camera").
FOCAL_PX = 3400.0


def _vp_lsq(edges):
    """Least-squares vanishing point of the street edges (each segment weighted by length)."""
    a11 = a12 = a22 = b1 = b2 = 0.0
    for pts in edges.values():
        for (x0, y0), (x1, y1) in zip(pts, pts[1:]):
            dx, dy = x1 - x0, y1 - y0
            ln = math.hypot(dx, dy)
            nx, ny = -dy / ln, dx / ln
            c = nx * x0 + ny * y0
            a11 += ln * nx * nx
            a12 += ln * nx * ny
            a22 += ln * ny * ny
            b1 += ln * nx * c
            b2 += ln * ny * c
    det = a11 * a22 - a12 * a12
    return ((b1 * a22 - b2 * a12) / det, (a11 * b2 - a12 * b1) / det)


def _edge_slope(pts, vp):
    """X / h of a street edge (its lateral offset over the camera height), from its near point."""
    x, y = pts[-1]
    return (x - vp[0]) / (y - vp[1])


def camera(focal_px=None):
    """The solved splash camera.

    One-point perspective with level verticals means the camera looks straight down the
    street with zero pitch and zero roll; the horizon sits above the frame's centre, which a
    real camera gets from a vertical lens shift. Returns a dict with the vanishing point
    (cx, vy), the focal length in px, the height in m and the derived numbers."""
    vp = _vp_lsq(STREET_EDGES)
    cx, vy = vp
    h = CAMERA_HEIGHT
    s = {k: _edge_slope(p, vp) for k, p in STREET_EDGES.items()}
    walk_l = (s["road_l"] - s["lawn_l"]) * h
    walk_r = (s["lawn_r"] - s["road_r"]) * h
    # square slabs: slab length (depth between the two joints) equals the sidewalk width
    f_sq = []
    for side, walk in (("l", walk_l), ("r", walk_r)):
        y_near, y_far = JOINTS[side]
        f_sq.append(walk / (h * (1.0 / (y_far - vy) - 1.0 / (y_near - vy))))
    f = focal_px or FOCAL_PX
    return {
        "cx": cx, "vy": vy, "f": f, "h": h,
        "f_square_slabs": sum(f_sq) / 2,
        "f_square_slabs_lr": f_sq,
        "road_l": s["road_l"] * h, "road_r": s["road_r"] * h,
        "kerb_l": s["kerb_l"] * h, "kerb_r": s["kerb_r"] * h,
        "lawn_l": s["lawn_l"] * h, "lawn_r": s["lawn_r"] * h,
        "walk_l": walk_l, "walk_r": walk_r,
        "lens_mm": f * 36.0 / W, "sensor_mm": 36.0,
        "shift_x": (W / 2 - cx) / W, "shift_y": (vy - H / 2) / W,
        "hfov_deg": math.degrees(2 * math.atan(W / 2 / f)),
    }


def depth_of(cam, y_ground):
    """Distance along the street (m) of a ground line drawn at screen y."""
    return cam["f"] * cam["h"] / (y_ground - cam["vy"])


def unproject(cam, x, y, d):
    """Screen (x, y) on the plane square to the street at distance d -> world (X, Z)."""
    return ((x - cam["cx"]) * d / cam["f"], cam["h"] + (cam["vy"] - y) * d / cam["f"])


def project(cam, X, Y, Z):
    """World (X right, Y along the street, Z up) -> screen (x, y)."""
    return (cam["cx"] + cam["f"] * X / Y, cam["vy"] - cam["f"] * (Z - cam["h"]) / Y)


# --------------------------------------------------------------------------
# Default depths (m) for what the painting does not show
# --------------------------------------------------------------------------
DEPTH = {
    "gable": 4.0,          # how deep the front-gable block (a projecting bay) runs back
    "wing_inner": "x0",    # where the wing ends beside the bay: "x0" (beside the bay only),
                           # "apex" or "x1" (behind the whole bay)
    "wing_setback": 0.6,   # the side wing's front sits this far behind the gable's front
    "wing": 6.0,           # the side wing's depth (its ridge is at the middle)
    "wing_width": 6.0,     # a wing that runs off the image: its width (m)
    "overhang_front": 0.12,
    "overhang_back": 0.3,
}

# --------------------------------------------------------------------------
# The eleven houses. Screen px on the original; colours measured on it.
#   side      L / R of the street (a wing runs away from the street)
#   ground    y of the front wall's foot (sets the house's depth)
#   gable     the front-gable block: x0, x1 (walls), apex (x, y) of the wall, eave_y of the
#             wall at the corners (left, right), roof_top: y of the roof's apex top, overhang
#             (px, sideways beyond the walls at the eaves)
#   wing      the set-back side block: x_out (outer end on screen; off-image runs on),
#             top (wall top y), ridge (y of its ridge as drawn), roof_out (roof's outer x)
#   trim      (y0, y1): the trim band at the gable's foot
#   windows   frame (x0, y0, x1, y1), glass, mull (y0, y1) or None, sill None|"sill"|"box",
#             on "gable"|"wing"
# --------------------------------------------------------------------------
HOUSES = [
    # ------------------------------------------------------------ left side, near to far
    dict(
        key="red", label="Red (near left)", side="L", ground=966,
        gable=dict(x0=112, x1=433, apex=(300, 266), eave_y=(545, 553), roof_top=196, overhang=34),
        wing=dict(x_out=-420, top=540, ridge=196),
        trim=(540, 572),
        windows=[
            dict(frame=(258.5, 361, 322.5, 499), glass=(273, 383.5, 313, 487), mull=(428, 435)),
            dict(frame=(139, 642, 197, 827), glass=(150, 655, 186, 815), mull=(726, 734), sill="box",
                 sill_y=(832, 866)),
            dict(frame=(354, 642, 412, 800), glass=(365, 655, 402, 790), mull=(712, 724), sill="box",
                 sill_y=(805, 839)),
        ],
        door=dict(frame=(225, 639, 340, 946), panel=(240, 654, 325, 946), knob=(299, 799)),
        steps=dict(n=2, width=1.45),
        skylight=(100, 256, 199, 301),
        colours=dict(wall="#f5584e", wing="#a72e41", roof="#484c7b", barge="#6a6f9b", soffit="#40446d",
                     trim="#f7d8a7", frame="#f7d2a3", glass="#56aad8", door="#c76236", knob="#f9ae41",
                     sill="#e08f53", steps="#ddccbf", foundation="#83748f", skylight="#5c5f8d"),
    ),
    dict(
        key="yellow", label="Yellow (left)", side="L", ground=872,
        gable=dict(x0=692, x1=820, apex=(724, 436), eave_y=(588, 604), roof_top=414, overhang=16),
        wing=dict(x_out=396, top=604, ridge=430),
        foundation=842,
        trim=(588, 608),
        windows=[
            dict(frame=(735, 509, 766, 574), glass=(744, 513, 758, 566), mull=(538, 542)),
            dict(frame=(792, 657, 822, 740), glass=(796, 660, 809, 735), mull=(692, 697), sill="sill",
                 sill_y=(741, 748)),
            dict(frame=(432, 650, 530, 782), glass=(447, 662, 516, 760), mull=(706, 716), on="wing",
                 colour="#bf6f36", glass_colour="#4878bd"),
        ],
        door=dict(frame=(735, 647, 787, 828), panel=(745, 655, 780, 828), knob=(751, 726)),
        steps=dict(n=2, width=1.1),
        colours=dict(wall="#f9cb55", wing="#cd8e2d", roof="#896a6a", barge="#977071", soffit="#e2a75b",
                     trim="#f7dea5", frame="#fadf9a", glass="#6596bf", door="#dc7552", knob="#f9a052",
                     sill="#ecac54", steps="#e6dcd0", foundation="#79565a", fascia="#634f56"),
    ),
    dict(
        key="teal", label="Teal (left)", side="L", ground=822,
        gable=dict(x0=882, x1=963, apex=(928, 520), eave_y=(608, 612), roof_top=483, overhang=12),
        wing=dict(x_out=786, top=613, ridge=488),
        trim=(610, 624),
        windows=[
            dict(frame=(917, 541, 932, 589), glass=(920, 545, 929, 585), mull=(563, 566)),
            dict(frame=(887, 650, 905, 729), glass=(892, 656, 902, 722), mull=(689, 693), sill="sill",
                 sill_y=(730, 740)),
            dict(frame=(939, 650, 955, 720), glass=(943, 655, 952, 714), mull=(683, 687), sill="sill",
                 sill_y=(721, 732)),
        ],
        door=dict(frame=(907, 650, 934, 781), panel=(913, 656, 929, 781), knob=None),
        steps=dict(n=2, width=0.95),
        colours=dict(wall="#72bdb1", wing="#58a2a6", roof="#718eab", barge="#77a0b5", soffit="#6d8899",
                     trim="#efcea1", frame="#e2bb8d", glass="#7aa3b2", door="#d0845a", knob="#f2a23a",
                     sill="#e1b07a", steps="#e6dacf", foundation="#98919c"),
    ),
    dict(
        key="far_salmon", label="Far salmon (left)", side="L", ground=748,
        gable=dict(x0=1124, x1=1160, apex=(1140, 552), eave_y=(604, 622), roof_top=546, overhang=6),
        wing=dict(x_out=958, top=628, ridge=546),
        trim=(626, 637),
        windows=[
            dict(frame=(1132, 589, 1140, 612), glass=(1134, 589, 1138, 611), mull=(594, 596)),
            dict(frame=(1144, 654, 1150, 696), glass=(1146, 656, 1148, 694), mull=(674, 677)),
            dict(frame=(1097, 698, 1124, 735), glass=(1101, 702, 1120, 730), mull=None, on="wing",
                 colour="#f9a283", glass_colour="#c7a9a4"),
        ],
        door=dict(frame=(1131, 660, 1143, 730), panel=(1134, 664, 1140, 730), knob=None),
        steps=dict(n=1, width=0.0),
        chimney=dict(front=(975, 528, 1016, 575), cap=(970, 517, 1020, 531)),
        colours=dict(wall="#f69d8e", gable="#f8a988", wing="#d3867a", roof="#bd949c", barge="#b19589",
                     soffit="#977d8b", trim="#fce8b3", frame="#fce8b3", glass="#aeb7be", door="#de8d76",
                     knob="#f2a23a", sill="#fce8b3", steps="#ccc4ba", foundation="#afa9a0",
                     chimney="#ba8882", cap="#b2bac5", fascia="#977d8b"),
    ),
    dict(
        key="far_rose", label="Far rose (left)", side="L", ground=700,
        gable=dict(x0=1240, x1=1284, apex=(1270, 590), eave_y=(640, 640), roof_top=584, overhang=5),
        wing=dict(x_out=1205, top=640, ridge=584),
        trim=(635, 644),
        windows=[
            dict(frame=(1264, 614, 1272, 628), glass=(1265, 614, 1270, 627), mull=None),
            dict(frame=(1244, 654, 1250, 684), glass=(1245, 655, 1249, 683), mull=None),
            dict(frame=(1256, 654, 1262, 698), glass=(1257, 655, 1260, 696), mull=None),
            dict(frame=(1274, 654, 1280, 682), glass=(1276, 655, 1278, 680), mull=None),
        ],
        door=dict(frame=(1262, 653, 1270, 699), panel=(1263, 655, 1269, 699), knob=None),
        steps=dict(n=1, width=0.0),
        colours=dict(wall="#f9c8a5", gable="#f6c6bf", wing="#f4bb9e", roof="#c1b4c8", barge="#c3c0cf",
                     soffit="#a69bac", trim="#fce3b2", frame="#fce3b2", glass="#cfc4c5", door="#dbb5ab",
                     knob="#f2a23a", sill="#fce3b2", steps="#c8c4bd", foundation="#aca9a3", fascia="#a69bac"),
    ),
    # ------------------------------------------------------------ right side, near to far
    dict(
        key="green", label="Green (near right)", side="R", ground=975,
        gable=dict(x0=2505, x1=2800, apex=(2648, 278), eave_y=(560, 560), roof_top=202, overhang=34),
        wing=dict(x_out=3300, top=546, ridge=202),
        trim=(546, 580),
        windows=[
            dict(frame=(2615, 363, 2680, 502), glass=(2628, 380, 2670, 490), mull=(426, 442)),
            dict(frame=(2523, 647, 2586, 804), glass=(2533, 662, 2571, 794), mull=(719, 731), sill="box",
                 sill_y=(809, 842)),
            dict(frame=(2740, 647, 2830, 835), glass=(2753, 664, 2817, 822), mull=(732, 746), sill="box",
                 sill_y=(840, 873), on="wing"),
        ],
        door=dict(frame=(2599, 641, 2712, 950), panel=(2630, 657, 2696, 950), knob=(2639, 808)),
        steps=dict(n=2, width=1.45),
        colours=dict(wall="#84b83a", wing="#79ad35", roof="#9e3d38", barge="#d95f48", soffit="#7d2d2e",
                     trim="#f1c395", frame="#eec291", glass="#44a2d4", door="#c24e3d", knob="#f78f2e",
                     sill="#ba6641", steps="#dbcbc1", foundation="#7d7189"),
    ),
    dict(
        key="blue", label="Blue (right)", side="R", ground=830,
        gable=dict(x0=2190, x1=2347, apex=(2250, 420), eave_y=(595, 600), roof_top=382, overhang=14),
        wing=dict(x_out=2560, top=606, ridge=382),
        trim=(588, 608),
        windows=[
            dict(frame=(2246, 480, 2274, 562), glass=(2252, 488, 2268, 556), mull=(512, 519)),
            dict(frame=(2195, 649, 2223, 745), glass=(2204, 658, 2216, 738), mull=(693, 700), sill="sill",
                 sill_y=(746, 758)),
            dict(frame=(2292, 647, 2329, 759), glass=(2302, 658, 2318, 748), mull=(698, 707), sill="sill",
                 sill_y=(760, 772)),
            dict(frame=(2455, 650, 2542, 771), glass=(2467, 661, 2530, 761), mull=(704, 714), on="wing",
                 colour="#a57d5d", glass_colour="#3877a2"),
        ],
        door=dict(frame=(2228, 646, 2283, 830), panel=(2241, 655, 2273, 830), knob=(2266, 742)),
        steps=dict(n=2, width=1.1),
        chimney=dict(front=(2465, 336, 2538, 452), cap=(2429, 303, 2545, 338)),
        colours=dict(wall="#61bcb6", wing="#3b8391", roof="#40779b", barge="#64a0b5", soffit="#4b6e83",
                     trim="#f1c191", frame="#f3c88f", glass="#4f9fd0", door="#d56538", knob="#f2a23a",
                     sill="#d4a065", steps="#e5dbd3", foundation="#938696", chimney="#c35a3d",
                     cap="#85819a", fascia="#32587d"),
    ),
    dict(
        key="pink", label="Pink (right)", side="R", ground=790,
        gable=dict(x0=2003, x1=2088, apex=(2038, 512), eave_y=(606, 606), roof_top=474, overhang=10),
        wing=dict(x_out=2200, top=612, ridge=481),
        trim=(606, 619),
        windows=[
            dict(frame=(2035, 540, 2050, 587), glass=(2040, 546, 2046, 582), mull=(560, 565)),
            dict(frame=(2010, 651, 2026, 714), glass=(2015, 655, 2022, 707), mull=(680, 684), sill="sill",
                 sill_y=(715, 727)),
            dict(frame=(2064, 651, 2081, 728), glass=(2069, 656, 2077, 722), mull=(687, 691), sill="sill",
                 sill_y=(729, 740)),
        ],
        door=dict(frame=(2030, 649, 2061, 790), panel=(2041, 657, 2055, 790), knob=None),
        steps=dict(n=1, width=1.0),
        skylight=(2101, 562, 2136, 578),
        colours=dict(wall="#ed96a2", wing="#b85b7a", roof="#ad697b", barge="#c78197", soffit="#9d647b",
                     trim="#f6c594", frame="#fcd29e", glass="#72aed2", door="#d16c68", knob="#f2a23a",
                     sill="#ce8055", steps="#e2d9ce", foundation="#c8bdb9", skylight="#bc7484", fascia="#af677b"),
    ),
    dict(
        key="far_yellow", label="Far yellow (right)", side="R", ground=762,
        gable=dict(x0=1868, x1=1913, apex=(1888, 540), eave_y=(620, 620), roof_top=526, overhang=6),
        wing=dict(x_out=2012, top=628, ridge=527),
        trim=(620, 632),
        porch=700,
        windows=[
            dict(frame=(1890, 575, 1899, 602), glass=(1892, 575, 1898, 600), mull=(584, 586)),
            dict(frame=(1874, 654, 1880, 696), glass=(1875, 656, 1879, 695), mull=(675, 678)),
        ],
        door=dict(frame=(1884, 653, 1900, 726), panel=(1886, 656, 1896, 726), knob=None),
        steps=dict(n=3, width=0.0),
        chimney=dict(front=(1961, 500, 2001, 560), cap=(1957, 488, 2004, 502)),
        colours=dict(wall="#f7d574", gable="#f8d779", wing="#b78c6b", roof="#a5988a", barge="#c9a482",
                     soffit="#e7ad6f", trim="#f3d597", frame="#f3d597", glass="#87bdca", door="#c8a48b",
                     knob="#f2a23a", sill="#f3d597", steps="#c8c4bd", foundation="#aca9a3", porch="#f1bb6c",
                     chimney="#b2847f", cap="#b0b8c1", fascia="#967f7f"),
    ),
    dict(
        key="far_pink", label="Far pink (right)", side="R", ground=722,
        gable=dict(x0=1729, x1=1756, apex=(1742, 571), eave_y=(632, 632), roof_top=565, overhang=4),
        wing=dict(x_out=1850, top=632, ridge=565),
        trim=(628, 637),
        windows=[
            dict(frame=(1742, 600, 1748, 618), glass=(1744, 600, 1747, 617), mull=None),
            dict(frame=(1733, 650, 1738, 682), glass=(1734, 652, 1737, 681), mull=None),
            dict(frame=(1836, 656, 1850, 678), glass=(1838, 657, 1848, 677), mull=None, on="wing"),
        ],
        door=dict(frame=(1739, 651, 1748, 700), panel=(1740, 652, 1746, 700), knob=None),
        steps=dict(n=2, width=0.0),
        colours=dict(wall="#f9b9b3", gable="#fabdc1", wing="#dab0ba", roof="#dba3c2", barge="#c196b0",
                     soffit="#bc8ca7", trim="#f9eabf", frame="#f9eabf", glass="#a8c8c8", door="#e3b9a7",
                     knob="#f2a23a", sill="#f9eabf", steps="#c8c4bd", foundation="#aca9a3", fascia="#bc8ca7"),
    ),
    dict(
        key="far_blue", label="Far blue (right)", side="R", ground=700,
        gable=dict(x0=1656, x1=1704, apex=(1675, 586), eave_y=(638, 638), roof_top=579, overhang=5),
        wing=dict(x_out=1748, top=638, ridge=581),
        trim=(636, 640),
        windows=[
            dict(frame=(1674, 606, 1682, 622), glass=(1675, 606, 1680, 621), mull=None),
            dict(frame=(1664, 652, 1670, 678), glass=(1666, 653, 1669, 676), mull=None),
            dict(frame=(1686, 652, 1692, 678), glass=(1687, 653, 1691, 676), mull=None),
        ],
        door=dict(frame=(1672, 650, 1686, 700), panel=(1674, 652, 1684, 700), knob=None),
        steps=dict(n=1, width=0.0),
        colours=dict(wall="#eed3ab", gable="#92c4d1", wing="#d3cea9", roof="#a2b7c8", barge="#97a3b7",
                     soffit="#b5b191", trim="#eed8b2", frame="#eed8b2", glass="#9cc9d4", door="#d2b595",
                     knob="#f2a23a", sill="#eed8b2", steps="#d2c9bd", foundation="#b5ada3", fascia="#b5b191"),
    ),
]

# --------------------------------------------------------------------------
# Props (positions on the original; sizes from it)
#   trees: trunk foot (x, y), crown centre (x, y), crown radii (rx, ry) px, colours
# --------------------------------------------------------------------------
TREES = [
    # near and mid trees, faceted crowns
    dict(key="tree_near_left", foot=(85, 1100), crown=(70, 700), r=(115, 190), trunk_w=60,
         leaf="#5f9a27", lit="#6fb318", bark="#703635"),
    dict(key="tree_near_yellow", foot=(675, 905), crown=(670, 690), r=(68, 118), trunk_w=26,
         leaf="#5b8f2f", lit="#7dba2f", bark="#814839"),
    dict(key="tree_behind_red", foot=(420, 700), crown=(420, 420), r=(62, 84), trunk_w=18, behind=True,
         leaf="#9ac96a", lit="#b7db74", bark="#7a5a3a"),
    dict(key="tree_far_left_1", foot=(1068, 790), crown=(1068, 640), r=(72, 64), trunk_w=16,
         leaf="#7bb05a", lit="#98d15f", bark="#9b705f"),
    dict(key="tree_far_left_2", foot=(1183, 720), crown=(1183, 600), r=(37, 52), trunk_w=8,
         leaf="#a7d585", lit="#bfee92", bark="#bb9c8a"),
    dict(key="tree_far_left_3", foot=(1232, 724), crown=(1232, 640), r=(20, 38), trunk_w=5,
         leaf="#9db99a", lit="#aeceaa", bark="#ad9a8e"),
    dict(key="tree_far_left_4", foot=(1352, 693), crown=(1352, 622), r=(29, 32), trunk_w=5,
         leaf="#a4d698", lit="#c2f2a2", bark="#a3afa7"),
    dict(key="tree_far_right_1", foot=(1613, 695), crown=(1613, 625), r=(24, 32), trunk_w=5,
         leaf="#a5d592", lit="#c3f39b", bark="#adafa0"),
    dict(key="tree_far_right_3", foot=(1787, 748), crown=(1787, 645), r=(37, 52), trunk_w=8,
         leaf="#87b177", lit="#9ecc80", bark="#c0927f"),
    dict(key="tree_far_right_4", foot=(1863, 722), crown=(1863, 560), r=(82, 82), trunk_w=12,
         leaf="#91c489", lit="#a9da98", bark="#ac8b79"),
    dict(key="tree_far_right_5", foot=(1948, 790), crown=(1948, 640), r=(47, 70), trunk_w=12,
         leaf="#70a74b", lit="#86c15a", bark="#ae795e"),
    dict(key="tree_blue_house", foot=(2165, 823), crown=(2165, 700), r=(36, 72), trunk_w=9,
         leaf="#7db121", lit="#87bf24", bark="#7a4b3a"),
]
POPLARS = [  # tall thin far trees: foot (x, y), height px, width px
    dict(key="poplar_1", foot=(1290, 697), h=45, w=11, leaf="#9de292", bark="#a3afa7"),
    dict(key="poplar_2", foot=(1374, 692), h=40, w=13, leaf="#a0d79f", bark="#a3afa7"),
    dict(key="poplar_3", foot=(1557, 692), h=51, w=14, leaf="#a3db92", bark="#a0c9c3"),
    dict(key="poplar_4", foot=(1578, 697), h=51, w=20, leaf="#a0d594", bark="#9ebfb4"),
    dict(key="poplar_5", foot=(1650, 692), h=38, w=10, leaf="#aee486", bark="#bad3c4"),
]
BUSHES = [  # foot (x, y) and radii (rx, ry) px
    dict(key="bush_front_left", foot=(140, 1275), r=(160, 120), leaf="#2f7f33", lit="#589f28"),
    dict(key="bush_far_left", foot=(1103, 779), r=(22, 28), leaf="#71a761", lit="#7ab469"),
    dict(key="bush_far_left_2", foot=(1056, 779), r=(14, 25), leaf="#7bac5d", lit="#85ba64"),
    dict(key="bush_far_right", foot=(1833, 751), r=(33, 20), leaf="#a3d452", lit="#b0e559"),
    dict(key="bush_blue_right", foot=(2478, 814), r=(36, 24), leaf="#458424", lit="#4b8f27"),
    dict(key="bush_blue", foot=(2310, 883), r=(38, 40), leaf="#498b21", lit="#4f9624"),
    dict(key="bush_green", foot=(2548, 952), r=(46, 34), leaf="#4e9937", lit="#54a53b"),
    dict(key="bush_far_right_2", foot=(1713, 701), r=(19, 22), leaf="#a7cf84", lit="#b4e08f"),
]
FENCES = [  # a picket run square to the street: x0..x1 at ground y, picket top y at x0
    dict(key="fence_yellow", x=(818, 882), ground=835, top=748, colour="#ca8456"),
    dict(key="fence_yellow_side", x=(505, 655), ground=848, top=786, colour="#c47442"),
    dict(key="fence_blue_left", x=(2086, 2190), ground=842, top=737, colour="#d0824c"),
    dict(key="fence_blue_right", x=(2362, 2506), ground=878, top=782, colour="#cb7642"),
    dict(key="fence_green", x=(2700, 2840), ground=1156, top=908, colour="#ba673b"),
]
GROUND_COLOURS = dict(
    grass="#8ac02b", grass_far="#a9cf7a", paving="#d8cacd", joint="#b7a9b7", asphalt="#6a6c7e",
    kerb="#94829b", gutter="#595974", path="#dcd2cf", path_edge="#a39fa0", post="#884829",
    mailbox="#4687c9", mailbox_dark="#3a6fae", flag="#e0452f", treeline="#99dee9",
)
