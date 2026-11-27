#!/usr/bin/env python3
"""Doggiehood splash dogs, hand-drawn as vector layers (the earlier version).

The committed dogs are traced from the original (trace_dogs.py); these hand-drawn dogs
stay available with `build_scene.py --dogs hand`, and their silhouettes are the rough
regions trace_dogs.py starts its masks from.

Each dog is drawn by hand as smooth closed outlines (Catmull-Rom splines through points
read off the original splash, in its 2816 x 1536 pixel space) and grouped by part, so a
part can be recoloured or pulled out on its own:

    <dog>-outline   the dark cartoon outline: every silhouette part, filled and stroked in
                    the dog's outline colour, behind everything else
    <dog>-legs-far, <dog>-tail, <dog>-body, <dog>-legs, <dog>-markings / <dog>-spots,
    <dog>-collar / <dog>-bandana, <dog>-ears, <dog>-head, <dog>-face, <dog>-eyes,
    <dog>-nose, <dog>-mouth, <dog>-tongue

Shading shapes are clipped to the dog's own silhouette (`<dog>-clip`), so they can be
drawn loosely and still never spill past the outline.

This module only describes the dogs. `build_scene.py` writes them: one full-frame layer
per dog (plus `dog-shadows`), a tight standalone crop of each dog, and the review
images. Run `python3 art-source/splash/build_scene.py` to rebuild everything.
"""
from __future__ import annotations

import re

import build_title as bt

fmt = bt.fmt

OUTLINE_W = 5.0       # outer cartoon outline, px outside the silhouette (original px)
INNER_W = 2.6         # lines between parts
DOG_ORDER = ["dog-shadows", "dog-schnauzer", "dog-puppy", "dog-corgi", "dog-dalmatian", "dog-golden"]
STANDALONE_H = 3200   # standalone crops are rendered at least this tall (px)
STANDALONE_PAD = 12   # transparent margin around a standalone crop (original px)


# --------------------------------------------------------------------------
# Spline helpers
# --------------------------------------------------------------------------
def smooth(pts, closed=True, t=1.0):
    """Catmull-Rom spline through `pts` as cubic-Bezier path data.

    A point given as (x, y, "c") is a corner: the curve passes through it without
    smoothing (used for fur tufts and ear tips)."""
    n = len(pts)
    if closed:
        get = lambda i: pts[i % n]  # noqa: E731
        segs = range(n)
    else:
        get = lambda i: pts[min(max(i, 0), n - 1)]  # noqa: E731
        segs = range(n - 1)
    d = [f"M {fmt(pts[0][0])} {fmt(pts[0][1])}"]
    for i in segs:
        p0, p1, p2, p3 = get(i - 1), get(i), get(i + 1), get(i + 2)
        k1 = 0 if len(p1) > 2 else t
        k2 = 0 if len(p2) > 2 else t
        c1 = (p1[0] + (p2[0] - p0[0]) * k1 / 6, p1[1] + (p2[1] - p0[1]) * k1 / 6)
        c2 = (p2[0] - (p3[0] - p1[0]) * k2 / 6, p2[1] - (p3[1] - p1[1]) * k2 / 6)
        d.append(f"C {fmt(c1[0])} {fmt(c1[1])} {fmt(c2[0])} {fmt(c2[1])} {fmt(p2[0])} {fmt(p2[1])}")
    return " ".join(d) + (" Z" if closed else "")


C = "c"   # corner marker: (x, y, C)


# A part is one of
#   ("s", pts, fill, opts)                     smooth closed shape
#   ("e", (cx, cy, rx, ry, rot), fill, opts)   ellipse
#   ("l", pts, colour, opts)                   open smooth line (opts: w = width)
# opts: sil=True   part of the silhouette (gets the thick outline, defines the clip)
#       edge=False no thin outline; edge_c / ew = thin outline colour / width
#       op=<opacity>; eye=True adds a catch-light; clip=True clips to the silhouette
def S(pts, fill, **o):
    return ("s", pts, fill, o)


def E(cx, cy, rx, ry, fill, rot=0, **o):
    return ("e", (cx, cy, rx, ry, rot), fill, o)


def L(pts, colour, w=INNER_W, **o):
    return ("l", pts, colour, dict(o, w=w))


def shade(pts, fill, op=1.0):
    """A shading shape: no edge, clipped to the dog's silhouette."""
    o = dict(edge=False, clip=True)
    if op != 1.0:
        o["op"] = op
    return S(pts, fill, **o)


def geometry(part, outline=False, ink="#000", ow=OUTLINE_W):
    """The bare drawing element (no paint) for a part."""
    kind, geo = part[0], part[1]
    if kind == "e":
        cx, cy, rx, ry, rot = geo
        t = f' transform="rotate({fmt(rot)} {fmt(cx)} {fmt(cy)})"' if rot else ""
        return f'<ellipse cx="{fmt(cx)}" cy="{fmt(cy)}" rx="{fmt(rx)}" ry="{fmt(ry)}"{t}'
    return f'<path d="{smooth(geo, closed=kind != "l")}"'


def draw_part(part, ink, dog, outline=False, ow=OUTLINE_W):
    line = LINE.get(dog, ink)
    kind, geo, col, o = part
    op = f' opacity="{fmt(o["op"])}"' if "op" in o else ""
    clip = f' clip-path="url(#{dog}-clip)"' if o.get("clip") else ""
    g = geometry(part)
    if outline:
        w = o.get("ow", ow)
        return (f'{g} fill="{ink}" stroke="{ink}" stroke-width="{fmt(w * 2)}" '
                'stroke-linejoin="round" stroke-linecap="round"/>')
    if kind == "l":
        return (f'{g} fill="none" stroke="{col}" stroke-width="{fmt(o["w"])}" '
                f'stroke-linecap="round" stroke-linejoin="round"{op}{clip}/>')
    if o.get("edge", True):
        attrs = (f'fill="{col}" stroke="{o.get("edge_c", line)}" stroke-width="{fmt(o.get("ew", INNER_W))}" '
                 'stroke-linejoin="round"')
    else:
        attrs = f'fill="{col}"'
    out = f"{g} {attrs}{op}{clip}/>"
    if kind == "e" and o.get("eye"):
        cx, cy, rx, ry, _ = geo
        out += (f'<circle cx="{fmt(cx - rx * 0.28)}" cy="{fmt(cy - ry * 0.4)}" r="{fmt(rx * 0.36)}" '
                'fill="#ffffff"/>')
    return out


def part_bounds(part):
    kind, geo = part[0], part[1]
    if kind == "e":
        cx, cy, rx, ry, _ = geo
        r = max(rx, ry)
        return cx - r, cy - r, cx + r, cy + r
    xs, ys = [p[0] for p in geo], [p[1] for p in geo]
    return min(xs), min(ys), max(xs), max(ys)


# --------------------------------------------------------------------------
# Shared colours
# --------------------------------------------------------------------------
BLACK_EYE = "#221d2c"
NOSE = "#242134"
NOSE_HI = "#6c6a80"
TONGUE, TONGUE_E = "#f36268", "#c23e4c"
MOUTH = "#5f231d"
RED, RED_D, RED_HI = "#ea2228", "#a8161d", "#ff6a5a"
BLUE, BLUE_D, BLUE_HI = "#1a6fc0", "#0b3f80", "#58a4e6"
GOLD, GOLD_D = "#f9b12a", "#b0700e"
SIL = dict(sil=True)
# thin lines between a dog's parts: a darker shade of its coat, softer than the outline
LINE = {"dog-golden": "#c0642a", "dog-corgi": "#b4501c", "dog-schnauzer": "#3e3b48",
        "dog-dalmatian": "#3a3e54", "dog-puppy": "#a0522a"}


# --------------------------------------------------------------------------
# The dogs (points in the original's pixel space)
# --------------------------------------------------------------------------
def golden():
    ink = "#7c3714"
    gold, gold_d, gold_dd, light, cream = "#ffc557", "#f2a03c", "#d97a2e", "#ffd877", "#ffe7a0"
    return "dog-golden", ink, [
        ("legs-far", [
            # the hind leg seen between the front legs
            S([(1452, 1150), (1490, 1150), (1490, 1215), (1484, 1240), (1462, 1242), (1452, 1226), (1458, 1196)],
              gold_d, **SIL),
            S([(1604, 1110), (1640, 1084), (1650, 1130), (1650, 1175), (1640, 1194), (1612, 1196), (1604, 1176)],
              gold_d, **SIL),
            L([(1622, 1180), (1623, 1194)], ink, 2.2), L([(1634, 1178), (1636, 1193)], ink, 2.2),
            L([(1468, 1226), (1468, 1240)], ink, 2.2),
        ]),
        ("tail", [
            S([(1600, 723, C), (1640, 724), (1668, 742), (1684, 751, C), (1690, 770), (1706, 793), (1719, 810, C),
               (1716, 840), (1708, 862), (1701, 884, C), (1696, 908), (1680, 924), (1648, 934), (1610, 935),
               (1592, 915), (1610, 894), (1630, 868), (1641, 840), (1643, 806), (1636, 772), (1620, 744)],
              gold, **SIL),
            shade([(1612, 742), (1640, 780), (1646, 830), (1636, 870), (1610, 900), (1660, 900), (1690, 870),
                   (1700, 830), (1690, 800), (1668, 770), (1640, 745)], gold_d),
            L([(1648, 760), (1668, 796), (1676, 836)], gold_dd, 2.4),
            L([(1680, 800), (1694, 830), (1694, 866)], gold_dd, 2.4),
            L([(1650, 880), (1668, 900), (1690, 910)], gold_dd, 2.4),
        ]),
        ("body", [
            S([(1344, 900), (1420, 926), (1500, 920), (1550, 900), (1600, 920), (1634, 940), (1642, 990),
               (1644, 1040), (1640, 1082), (1606, 1110), (1586, 1112), (1578, 1150), (1540, 1165), (1470, 1160),
               (1430, 1152), (1380, 1140), (1352, 1130), (1338, 1100), (1330, 1080, C), (1336, 1060),
               (1332, 1030), (1336, 990)], gold, edge=False, **SIL),
            shade([(1560, 960), (1600, 940), (1645, 960), (1650, 1090), (1590, 1115), (1560, 1110), (1575, 1060),
                   (1580, 1010)], gold_d),
            shade([(1350, 1120), (1440, 1140), (1520, 1140), (1570, 1150), (1560, 1175), (1440, 1170),
                   (1350, 1150)], gold_d),
            L([(1342, 1075), (1356, 1086, C), (1350, 1100)], ink, 2.2),
            L([(1420, 1140), (1432, 1124, C), (1446, 1142)], gold_dd, 2.4),
            L([(1480, 1146), (1494, 1126, C), (1506, 1146)], gold_dd, 2.4),
            L([(1600, 1000), (1606, 1040), (1600, 1080)], gold_dd, 2.4),
        ]),
        ("legs", [
            # raised front leg (viewer's left)
            S([(1356, 1120), (1420, 1140), (1430, 1170), (1428, 1215), (1438, 1236), (1443, 1258), (1428, 1276),
               (1404, 1280), (1384, 1272), (1372, 1252), (1368, 1220), (1362, 1180), (1354, 1146)], gold, **SIL),
            shade([(1410, 1150), (1432, 1160), (1430, 1220), (1440, 1250), (1420, 1240), (1412, 1200)], gold_d),
            # planted front leg (viewer's right)
            S([(1488, 1140), (1574, 1130), (1568, 1180), (1552, 1225), (1532, 1280), (1524, 1310), (1520, 1340),
               (1522, 1358), (1510, 1378), (1484, 1384), (1462, 1376), (1454, 1360), (1462, 1336), (1476, 1300),
               (1486, 1240), (1488, 1190)], gold, **SIL),
            shade([(1540, 1140), (1580, 1140), (1560, 1210), (1534, 1290), (1520, 1350), (1508, 1340),
                   (1520, 1280), (1538, 1210)], gold_d),
            L([(1384, 1258), (1390, 1274)], ink, 2.4), L([(1402, 1262), (1406, 1278)], ink, 2.4),
            L([(1420, 1258), (1424, 1274)], ink, 2.4),
            L([(1474, 1364), (1478, 1381)], ink, 2.4), L([(1490, 1364), (1494, 1383)], ink, 2.4),
            L([(1506, 1360), (1508, 1378)], ink, 2.4),
        ]),
        ("markings", [
            # light chest fur under the bandana
            shade([(1350, 1030), (1420, 1080), (1440, 1125), (1480, 1080), (1540, 1010), (1560, 1060),
                   (1540, 1120), (1470, 1140), (1400, 1130), (1350, 1100)], light),
            L([(1380, 1110), (1394, 1124, C), (1400, 1108)], gold_dd, 2.2),
            L([(1530, 1080), (1520, 1100, C), (1540, 1106)], gold_dd, 2.2),
        ]),
        ("bandana", [
            S([(1340, 948), (1380, 975), (1430, 990), (1490, 982), (1540, 950), (1556, 930), (1572, 912),
               (1596, 888, C), (1592, 914), (1600, 940, C), (1572, 936), (1550, 945), (1536, 990), (1516, 1030),
               (1480, 1075), (1434, 1120, C), (1400, 1080), (1360, 1030), (1342, 990)], RED, edge_c=RED_D,
              **SIL),
            S([(1372, 1000), (1430, 1018), (1500, 1004), (1534, 975), (1510, 1035), (1470, 1080), (1434, 1110),
               (1402, 1070)], "#dc1a24", edge=False),
            L([(1365, 990), (1430, 1006), (1500, 996), (1536, 962)], RED_HI, 3.0, op=0.7),
            S([(1550, 918), (1572, 906), (1580, 926), (1560, 938)], "#c81820", edge_c=RED_D),
        ]),
        ("head", [
            S([(1352, 736), (1380, 724), (1406, 718), (1440, 718), (1480, 726), (1515, 741), (1538, 790),
               (1540, 850), (1530, 900), (1500, 935), (1460, 960), (1420, 966), (1380, 955), (1352, 935),
               (1346, 890), (1344, 850), (1346, 800)], gold, **SIL),
            shade([(1380, 724), (1440, 722), (1500, 735), (1470, 750), (1420, 744)], light, op=0.8),
        ]),
        ("ears", [
            S([(1356, 734), (1340, 742), (1300, 770), (1286, 800), (1279, 830), (1278, 860), (1281, 876, C),
               (1300, 878), (1322, 852), (1342, 808), (1360, 766)], gold_d, **SIL),
            shade([(1300, 790), (1330, 760), (1350, 760), (1330, 820), (1308, 860), (1292, 860)], gold_dd, 0.55),
            S([(1512, 740), (1540, 750), (1566, 766), (1590, 784), (1606, 798), (1604, 836), (1598, 862),
               (1590, 886, C), (1566, 884), (1544, 856), (1530, 820), (1520, 780)], gold_d, **SIL),
            shade([(1550, 780), (1590, 800), (1596, 850), (1584, 876), (1560, 860), (1546, 820)], gold_dd, 0.55),
        ]),
        ("face", [
            S([(1366, 840), (1390, 822), (1430, 818), (1476, 826), (1506, 846), (1512, 872), (1494, 892),
               (1440, 902), (1380, 898), (1356, 876)], light, edge=False),
            L([(1364, 761), (1374, 755), (1386, 756)], gold_dd, 3.2),
            L([(1466, 760), (1478, 760), (1488, 767)], gold_dd, 3.2),
        ]),
        ("eyes", [E(1377, 799, 10, 14, BLACK_EYE, eye=True, edge=False),
                  E(1472, 805, 10, 13.5, BLACK_EYE, eye=True, edge=False)]),
        ("nose", [S([(1372, 838), (1400, 829), (1428, 832), (1438, 846), (1428, 866), (1406, 880), (1384, 872),
                     (1370, 856)], NOSE, edge=False),
                  E(1396, 840, 11, 5, NOSE_HI, edge=False, op=0.7)]),
        ("mouth", [
            S([(1360, 896), (1406, 902), (1460, 896), (1490, 878), (1484, 906), (1464, 934), (1440, 950),
               (1410, 958), (1380, 946), (1364, 920)], MOUTH),
            L([(1348, 858), (1352, 874), (1362, 892)], ink, 3.0),
            L([(1494, 872), (1490, 878)], ink, 3.0),
        ]),
        ("tongue", [S([(1376, 914), (1410, 908), (1444, 916), (1446, 946), (1432, 974), (1410, 984),
                       (1390, 976), (1376, 948)], TONGUE, edge_c=TONGUE_E, ew=2.4),
                    L([(1411, 922), (1410, 958)], TONGUE_E, 2.4)]),
    ]


def corgi():
    ink = "#7a3410"
    org, org_d, cream, pink = "#f68522", "#db651d", "#fee5c5", "#ffa7a3"
    return "dog-corgi", ink, [
        ("legs-far", [
            # far hind leg under the belly
            S([(1090, 1170), (1126, 1170), (1132, 1196), (1122, 1210), (1098, 1208), (1088, 1192)], org_d, **SIL),
        ]),
        ("tail", [S([(1110, 1036), (1116, 1010), (1129, 993, C), (1144, 1006), (1146, 1026), (1136, 1040)],
                    org, **SIL)]),
        ("body", [
            S([(1030, 1046), (1060, 1032), (1100, 1026), (1140, 1032), (1170, 1060), (1190, 1100), (1196, 1140),
               (1186, 1166), (1160, 1188), (1120, 1196), (1070, 1196), (1020, 1204), (960, 1210), (910, 1210),
               (888, 1190), (884, 1160), (874, 1130), (900, 1100), (980, 1090), (1036, 1060)], org, edge=False,
              **SIL),
            shade([(1140, 1060), (1180, 1080), (1200, 1140), (1170, 1185), (1110, 1196), (1150, 1150),
                   (1160, 1100)], org_d, 0.8),
            L([(1050, 1048), (1080, 1040), (1100, 1046)], "#ffb062", 3.0, op=0.8),
        ]),
        ("legs", [
            # front leg reaching forward (viewer's left)
            S([(884, 1186), (906, 1200), (926, 1220), (906, 1240), (892, 1260), (870, 1271), (846, 1268),
               (832, 1252), (836, 1236), (852, 1222)], org, **SIL),
            # front leg under the chest (viewer's right)
            S([(960, 1196), (1006, 1186), (1008, 1222), (1020, 1250), (1020, 1272), (1008, 1288), (980, 1291),
               (958, 1282), (954, 1262), (966, 1236)], org, **SIL),
            # hind leg kicked back
            S([(1136, 1104), (1176, 1108), (1196, 1128), (1208, 1156), (1208, 1180), (1194, 1190), (1178, 1184),
               (1164, 1160), (1140, 1142)], org, **SIL),
        ]),
        ("markings", [
            # white chest and socks
            S([(890, 1140), (950, 1152), (1000, 1140), (1010, 1180), (1000, 1210), (960, 1214), (920, 1214),
               (892, 1200)], cream, edge=False, clip=True),
            S([(834, 1236), (852, 1226), (874, 1242), (886, 1262), (870, 1274), (840, 1268)], cream,
              edge=False, clip=True),
            S([(956, 1258), (990, 1252), (1016, 1262), (1022, 1290), (980, 1294), (954, 1280)], cream,
              edge=False, clip=True),
            S([(1170, 1164), (1198, 1150), (1214, 1170), (1206, 1192), (1180, 1190)], cream, edge=False, clip=True),
            L([(850, 1250), (852, 1264)], ink, 2.2), L([(864, 1252), (866, 1268)], ink, 2.2),
            L([(978, 1276), (980, 1290)], ink, 2.2), L([(994, 1274), (996, 1290)], ink, 2.2),
            L([(1190, 1178), (1192, 1189)], ink, 2.2),
            E(850, 1258, 5, 4, pink, edge=False, op=0.8), E(988, 1282, 6, 4, pink, edge=False, op=0.8),
        ]),
        ("collar", [
            S([(873, 1118), (920, 1140), (976, 1132), (1016, 1106), (1032, 1066), (1040, 1048), (1044, 1080),
               (1034, 1110), (1010, 1136), (972, 1154), (920, 1158), (880, 1140)], BLUE, edge_c=BLUE_D, **SIL),
            L([(890, 1136), (930, 1148), (976, 1142), (1014, 1120), (1034, 1084)], BLUE_HI, 3.4, op=0.85),
            L([(935, 1140), (942, 1146), (936, 1150)], "#d8ecff", 2.4),
            L([(962, 1138), (968, 1144), (962, 1148)], "#d8ecff", 2.4),
        ]),
        ("ears", [
            S([(845, 857, C), (868, 878), (898, 912), (918, 944), (880, 986), (846, 988), (836, 960), (832, 916)],
              org, **SIL),
            S([(850, 880), (876, 906), (898, 934), (874, 960), (854, 962), (848, 930)], pink, edge=False),
            S([(1040, 864, C), (1050, 900), (1048, 940), (1040, 976), (1030, 1006), (990, 990), (962, 940),
               (990, 906)], org, **SIL),
            S([(1032, 886), (1036, 940), (1028, 978), (996, 960), (980, 936), (1004, 910)], pink, edge=False),
        ]),
        ("head", [
            S([(846, 980), (880, 950), (920, 936), (960, 934), (1000, 950), (1030, 986), (1040, 1030),
               (1036, 1066), (1010, 1096), (970, 1116), (920, 1126), (880, 1124), (856, 1104), (840, 1070),
               (831, 1036), (836, 1004)], org, **SIL),
        ]),
        ("face", [
            # white blaze down the forehead into the muzzle and cheeks
            S([(904, 944), (930, 942), (930, 970), (940, 1000), (968, 1034), (1010, 1062), (1018, 1080),
               (990, 1104), (950, 1118), (900, 1124), (866, 1116), (846, 1090), (838, 1060), (842, 1036),
               (870, 1028), (900, 1000), (902, 970)], cream, edge=False),
            L([(855, 990), (866, 984), (878, 986)], org_d, 3.0),
            L([(930, 982), (942, 978), (954, 982)], org_d, 3.0),
        ]),
        ("eyes", [E(870, 1006, 7.5, 10.5, BLACK_EYE, eye=True, edge=False),
                  E(939, 1009, 8, 10.5, BLACK_EYE, eye=True, edge=False)]),
        ("nose", [S([(846, 1044), (866, 1036), (886, 1040), (888, 1054), (872, 1064), (852, 1062)], NOSE,
                    edge=False),
                  E(862, 1042, 8, 3.5, NOSE_HI, edge=False, op=0.7)]),
        ("mouth", [S([(860, 1072), (900, 1076), (936, 1066), (952, 1058), (946, 1080), (930, 1102), (906, 1118),
                      (880, 1120), (866, 1100)], MOUTH),
                   L([(952, 1058), (958, 1054)], ink, 3.0)]),
        ("tongue", [S([(872, 1094), (896, 1086), (918, 1090), (920, 1108), (902, 1120), (882, 1120)], TONGUE,
                      edge_c=TONGUE_E, ew=2),
                    L([(897, 1094), (897, 1112)], TONGUE_E, 2)]),
    ]


def schnauzer():
    ink = "#2b2a36"
    grey, grey_d, dark, light, beard = "#7d7985", "#615d69", "#5c5864", "#e6e3e8", "#d0cdd5"
    pink = "#c98a94"
    return "dog-schnauzer", ink, [
        ("legs-far", [
            S([(1300, 930), (1330, 924), (1340, 960), (1338, 980), (1312, 982), (1302, 962)], grey_d, **SIL),
            S([(1312, 966), (1336, 964), (1338, 980), (1312, 982)], beard, edge=False),
        ]),
        ("body", [
            S([(1172, 900), (1230, 880), (1262, 862), (1300, 870), (1330, 900), (1336, 940), (1310, 968),
               (1280, 984), (1240, 984), (1200, 976), (1178, 950)], grey, edge=False, **SIL),
            shade([(1270, 880), (1330, 900), (1336, 950), (1290, 984), (1280, 940)], grey_d, 0.8),
            S([(1186, 924), (1212, 934), (1240, 926), (1246, 948), (1226, 968), (1198, 964), (1186, 948)],
              beard, edge=False, clip=True),
        ]),
        ("legs", [
            # front leg mid-step (paw lifted and curled) and planted front leg
            S([(1180, 948), (1208, 952), (1212, 982), (1228, 996), (1237, 1010), (1228, 1022), (1206, 1022),
               (1196, 1008), (1186, 986)], grey, **SIL),
            S([(1240, 952), (1270, 950), (1272, 990), (1272, 1020), (1276, 1036), (1262, 1044), (1240, 1043),
               (1236, 1024), (1240, 995)], grey, **SIL),
            S([(1200, 996), (1226, 994), (1238, 1010), (1228, 1024), (1204, 1024), (1196, 1008)], beard,
              edge=False, clip=True),
            S([(1236, 1022), (1274, 1022), (1278, 1044), (1240, 1046)], beard, edge=False, clip=True),
            L([(1214, 1012), (1215, 1021)], ink, 2), L([(1255, 1034), (1256, 1043)], ink, 2),
        ]),
        ("collar", [
            S([(1176, 900), (1205, 912), (1240, 898), (1256, 872), (1264, 868), (1262, 886), (1244, 910),
               (1206, 924), (1174, 912)], RED, edge_c=RED_D, **SIL),
            E(1210, 932, 10, 10, GOLD, edge_c=GOLD_D, ew=2.2, sil=True, ow=3),
            E(1207, 929, 3.5, 3.5, "#ffe9a0", edge=False),
        ]),
        ("ears", [
            # folded flaps, tips turned down and out
            S([(1127, 764, C), (1150, 752), (1168, 748), (1186, 768), (1176, 778), (1158, 786), (1140, 796, C),
               (1133, 780)], dark, **SIL),
            S([(1146, 768), (1166, 759), (1162, 776), (1146, 787)], pink, edge=False, op=0.8),
            S([(1214, 764), (1222, 748), (1236, 740, C), (1260, 748), (1283, 758, C), (1270, 772),
               (1262, 792, C), (1248, 782), (1230, 776)], dark, **SIL),
            S([(1238, 752), (1262, 757), (1256, 780), (1244, 770)], pink, edge=False, op=0.8),
        ]),
        ("head", [
            S([(1156, 772), (1185, 766), (1220, 764), (1248, 772), (1262, 795), (1262, 828), (1270, 842, C), (1262, 852),
               (1268, 866, C), (1258, 876), (1250, 892, C), (1240, 892), (1230, 906, C), (1218, 902), (1204, 912, C),
               (1192, 904), (1178, 908, C), (1168, 896), (1154, 892, C), (1150, 880), (1136, 874, C),
               (1142, 862), (1132, 852, C), (1142, 842), (1136, 830, C), (1148, 820), (1152, 796)], grey, **SIL),
        ]),
        ("face", [
            # bushy brows and the beard
            S([(1146, 797), (1154, 782, C), (1164, 788), (1176, 779, C), (1186, 789), (1197, 797),
               (1180, 803), (1160, 803)], light, edge=False),
            S([(1198, 797), (1207, 782, C), (1218, 786), (1230, 777, C), (1238, 788), (1248, 795),
               (1230, 803), (1210, 803)], light, edge=False),
            S([(1138, 832), (1150, 828), (1176, 826), (1200, 830), (1228, 828), (1254, 834), (1270, 842),
               (1262, 856), (1268, 866), (1254, 878), (1246, 888, C), (1236, 888), (1228, 900, C), (1216, 896), (1204, 906, C),
               (1192, 898), (1180, 902, C), (1170, 890), (1156, 886, C), (1152, 874), (1140, 868, C),
               (1144, 858)], beard, edge=False, clip=True),
            L([(1144, 860), (1140, 870, C), (1150, 868)], light, 2.4),
            L([(1252, 860), (1258, 870, C), (1248, 872)], light, 2.4),
        ]),
        ("eyes", [E(1171, 810, 7, 8, BLACK_EYE, eye=True, edge=False),
                  E(1217, 812, 7, 8, BLACK_EYE, eye=True, edge=False)]),
        ("nose", [S([(1162, 834), (1180, 828), (1196, 832), (1196, 846), (1182, 852), (1166, 848)], NOSE,
                    edge=False),
                  E(1176, 833, 6, 2.5, NOSE_HI, edge=False, op=0.7)]),
        ("mouth", [S([(1168, 864), (1194, 868), (1220, 862), (1216, 878), (1200, 888), (1180, 888)], MOUTH)]),
        ("tongue", [S([(1176, 874), (1192, 870), (1206, 874), (1202, 886), (1184, 888)], TONGUE, edge=False)]),
    ]


def dalmatian():
    ink = "#262a3e"
    white, shade_c, spot, ear = "#fbfaf6", "#d2d0da", "#2c2b3a", "#3a3849"
    return "dog-dalmatian", ink, [
        ("legs-far", [
            S([(1826, 1066), (1852, 1080), (1858, 1120), (1860, 1186), (1868, 1210), (1852, 1214), (1836, 1210),
               (1838, 1190), (1834, 1130), (1826, 1100)], shade_c, **SIL),
            S([(1990, 1110), (2020, 1110), (2046, 1146), (2068, 1184), (2062, 1212), (2046, 1236), (2028, 1246),
               (2018, 1236), (2030, 1214), (2040, 1194), (2020, 1170), (2000, 1146)], white, **SIL),
            L([(1846, 1202), (1848, 1212)], ink, 2.2), L([(2030, 1232), (2026, 1242)], ink, 2.2),
            E(2019, 1139, 5, 7, spot, edge=False),
        ]),
        ("tail", [S([(1806, 956), (1784, 944), (1764, 916), (1757, 880), (1764, 850), (1779, 831, C),
                     (1778, 860), (1778, 900), (1790, 930), (1810, 946)], white, **SIL)]),
        ("body", [
            S([(1800, 952), (1854, 944), (1904, 952), (1934, 960), (1960, 990), (2010, 1020), (2028, 1040),
               (2028, 1090), (2004, 1116), (1960, 1126), (1920, 1112), (1880, 1086), (1840, 1074),
               (1800, 1078), (1770, 1072), (1768, 1030), (1782, 984)], white, **SIL),
            shade([(1880, 1076), (1930, 1100), (1980, 1110), (2024, 1090), (2004, 1116), (1960, 1126),
                   (1920, 1112), (1880, 1090)], shade_c),
            shade([(1960, 990), (2028, 1040), (2030, 1090), (2010, 1050), (1980, 1030)], shade_c, 0.7),
        ]),
        ("legs", [
            # hind leg stretched back (viewer's left)
            S([(1776, 1020), (1790, 1070), (1784, 1086), (1756, 1106), (1748, 1150), (1734, 1168), (1712, 1166),
               (1710, 1146), (1722, 1108), (1740, 1070), (1762, 1036)], white, edge=False, **SIL),
            # long front leg
            S([(1888, 1090), (1946, 1100), (1944, 1140), (1952, 1200), (1972, 1286), (1994, 1306), (1998, 1328),
               (1976, 1333), (1954, 1322), (1950, 1296), (1926, 1230), (1904, 1160)], white, edge=False, **SIL),
            shade([(1922, 1110), (1946, 1100), (1944, 1160), (1960, 1240), (1975, 1290), (1955, 1280),
                   (1936, 1220), (1928, 1160)], shade_c, 0.8),
            shade([(1740, 1090), (1784, 1084), (1760, 1104), (1748, 1150), (1730, 1150)], shade_c, 0.7),
            L([(1975, 1312), (1977, 1328)], ink, 2.2), L([(1720, 1152), (1724, 1166)], ink, 2.2),
            # where the legs meet the body (the legs are fill-only so they merge into it)
            L([(1890, 1098), (1898, 1132), (1906, 1164)], "#3a3e54", 2.6),
            L([(1792, 1078), (1772, 1092), (1758, 1112)], "#3a3e54", 2.6),
        ]),
        ("spots", [
            E(1844, 963, 12, 12, spot, edge=False, clip=True), E(1786, 1007, 8, 12, spot, edge=False, rot=10),
            E(1866, 1012, 7, 11, spot, edge=False), E(1904, 993, 11, 9, spot, edge=False, rot=20),
            E(1890, 1046, 4, 6, spot, edge=False), E(1772, 1054, 5, 8, spot, edge=False),
            E(1911, 1068, 10, 15, spot, edge=False), E(1983, 1068, 8, 11, spot, edge=False),
            E(1926, 1111, 6, 9, spot, edge=False), E(1922, 1175, 6, 9, spot, edge=False),
            E(1833, 1021, 4, 6, spot, edge=False), E(1804, 1068, 4, 5, spot, edge=False),
            E(1736, 1092, 3.5, 5, spot, edge=False), E(1958, 972, 5, 6, spot, edge=False),
            E(1946, 1250, 4, 6, spot, edge=False), E(2040, 1186, 3.5, 5, spot, edge=False),
        ]),
        ("collar", [
            S([(1926, 952), (1940, 980), (1970, 1006), (2004, 1020), (2026, 1008), (2034, 996), (2036, 1014),
               (2020, 1034), (1996, 1038), (1964, 1026), (1938, 1004), (1922, 980)], RED, edge_c=RED_D, **SIL),
            L([(1934, 972), (1960, 1000), (1996, 1016), (2024, 1010)], RED_HI, 2.8, op=0.7),
            E(2002, 1040, 6, 8, RED_D, edge_c=ink, ew=2),
        ]),
        ("head", [
            S([(1952, 856), (1962, 822), (2004, 810), (2046, 818), (2076, 834), (2082, 870), (2076, 900),
               (2098, 912), (2100, 936), (2080, 954), (2066, 984), (2034, 994), (2002, 984), (1966, 962),
               (1950, 930), (1946, 890)], white, **SIL),
            shade([(1960, 940), (2000, 975), (2040, 990), (2070, 980), (2040, 1000), (1990, 990), (1955, 960)],
                  shade_c, 0.8),
            E(1960, 914, 3, 3, spot, edge=False), E(1970, 926, 2.5, 2.5, spot, edge=False),
        ]),
        ("ears", [
            S([(1882, 858), (1904, 836), (1962, 820), (1970, 844), (1948, 892), (1926, 925), (1904, 932),
               (1888, 914), (1882, 886)], ear, **SIL),
            S([(2058, 822), (2104, 836), (2124, 860), (2134, 882), (2134, 910), (2120, 914), (2100, 886),
               (2082, 856)], ear, **SIL),
        ]),
        ("face", [L([(1990, 855), (2000, 850), (2011, 852)], ink, 3.0),
                  L([(2060, 852), (2070, 849), (2079, 853)], ink, 3.0)]),
        ("eyes", [E(2002, 889, 8, 12, BLACK_EYE, eye=True, edge=False),
                  E(2070, 884, 7, 11, BLACK_EYE, eye=True, edge=False)]),
        ("nose", [S([(2052, 924), (2076, 916), (2100, 920), (2100, 934), (2082, 944), (2060, 940)], NOSE,
                    edge=False),
                  E(2072, 920, 8, 3, NOSE_HI, edge=False, op=0.7)]),
        ("mouth", [S([(1996, 946), (2030, 955), (2066, 950), (2062, 968), (2046, 984), (2026, 986),
                      (2008, 972)], MOUTH),
                   L([(1986, 938), (1992, 944)], ink, 3.0)]),
        ("tongue", [S([(2020, 970), (2042, 963), (2060, 966), (2054, 980), (2036, 988), (2022, 982)], TONGUE,
                      edge_c=TONGUE_E, ew=2)]),
    ]


def puppy():
    ink = "#7a3a1a"
    tan, tan_d, cream, ear = "#e3964c", "#c7722f", "#fdebc0", "#944a2e"
    ow = 2.6
    return "dog-puppy", ink, [
        ("tail", [S([(708, 950), (690, 947), (671, 941, C), (674, 953), (690, 965), (708, 969)], tan,
                    sil=True, ow=ow)]),
        ("body", [
            S([(742, 898), (762, 914), (766, 930), (764, 950), (768, 970), (740, 973), (712, 971), (704, 958),
               (708, 942), (718, 926), (730, 910)], tan, sil=True, ow=ow),
            shade([(720, 940), (742, 932), (756, 946), (752, 968), (722, 970)], "#d07c38"),
        ]),
        ("legs", [
            S([(760, 915), (774, 918), (774, 950), (772, 972), (756, 973), (760, 950)], cream, sil=True, ow=ow,
              ew=1.4),
            S([(772, 916), (786, 916), (786, 940), (788, 965), (776, 966), (774, 940)], cream, sil=True, ow=ow,
              ew=1.4),
            S([(732, 964), (746, 962), (751, 971), (734, 973)], cream, sil=True, ow=ow, ew=1.4),
        ]),
        ("collar", [S([(740, 896), (752, 908), (766, 913), (782, 912), (786, 906), (787, 914), (782, 919),
                       (764, 919), (748, 913), (738, 902)], BLUE, edge_c=BLUE_D, ew=1.2, sil=True, ow=ow)]),
        ("head", [S([(740, 866), (752, 851), (772, 847), (786, 852), (796, 866), (805, 880), (805, 889),
                     (796, 897), (784, 905), (766, 906), (750, 898), (742, 884)], tan, sil=True, ow=ow)]),
        ("face", [S([(768, 852), (776, 852), (782, 866), (796, 874), (806, 884), (800, 896), (786, 904),
                     (770, 904), (762, 894), (772, 880)], cream, edge=False)]),
        ("ears", [
            S([(734, 858), (742, 851), (754, 855), (756, 865), (750, 873), (740, 874), (735, 868)], ear,
              sil=True, ow=ow, ew=1.4),
            S([(780, 851), (790, 847), (801, 852), (804, 857), (797, 865), (790, 860), (784, 860)], ear,
              sil=True, ow=ow, ew=1.4),
        ]),
        ("eyes", [E(772, 872, 3.2, 4.2, BLACK_EYE, eye=True, edge=False)]),
        ("nose", [E(797, 881, 7, 5, NOSE, edge=False), E(795, 879, 2.6, 1.3, NOSE_HI, edge=False, op=0.7)]),
        ("mouth", [S([(772, 892), (784, 896), (796, 893), (792, 902), (782, 905), (775, 900)], MOUTH, ew=1.2)]),
        ("tongue", [S([(784, 900), (790, 899), (791, 908), (786, 910)], TONGUE, edge=False)]),
    ]


DOGS = [schnauzer, puppy, corgi, dalmatian, golden]

# Soft shadows on the ground under each dog: (id, smooth outline points)
SHADOWS = [
    ("shadow-schnauzer", [(1180, 1020), (1240, 1030), (1290, 1010), (1340, 980), (1350, 1000), (1300, 1040),
                          (1240, 1052), (1185, 1040)]),
    ("shadow-puppy", [(668, 966), (720, 962), (790, 966), (796, 976), (740, 982), (676, 976)]),
    ("shadow-corgi", [(840, 1272), (900, 1250), (1000, 1250), (1100, 1214), (1190, 1206), (1220, 1230),
                      (1150, 1280), (1040, 1310), (920, 1306), (850, 1292)]),
    ("shadow-golden", [(1350, 1296), (1440, 1280), (1560, 1240), (1640, 1200), (1676, 1226), (1660, 1340),
                       (1600, 1390), (1500, 1400), (1400, 1396), (1356, 1350)]),
    ("shadow-dalmatian", [(1720, 1176), (1800, 1214), (1880, 1220), (1960, 1300), (2030, 1330), (2080, 1250),
                          (2096, 1200), (2060, 1260), (2010, 1300), (1940, 1260), (1860, 1196), (1760, 1160)]),
]
SHADOW_FILL, SHADOW_OP, SHADOW_BLUR = "#1d1a40", 0.4, 4


# --------------------------------------------------------------------------
# Build the layer documents
# --------------------------------------------------------------------------
def dog_parts(fn):
    dog, ink, groups = fn()
    return dog, ink, groups


def dog_body(fn):
    """(dog id, <defs> body, drawable body) for one dog."""
    dog, ink, groups = dog_parts(fn)
    sil = [p for _, parts in groups for p in parts if p[3].get("sil")]
    clip = (f'<clipPath id="{dog}-clip">'
            + "".join(geometry(p) + "/>" for p in sil) + "</clipPath>\n")
    import build_scene as bs
    out = bs.group(f"{dog}-outline", "".join(draw_part(p, ink, dog, outline=True) for p in sil))
    for name, parts in groups:
        out += bs.group(f"{dog}-{name}", "".join(draw_part(p, ink, dog) for p in parts))
    return dog, clip, out


def dog_bbox(fn):
    _, _, groups = dog_parts(fn)
    bs_ = [part_bounds(p) for _, parts in groups for p in parts]
    pad = OUTLINE_W + STANDALONE_PAD
    return (min(b[0] for b in bs_) - pad, min(b[1] for b in bs_) - pad,
            max(b[2] for b in bs_) + pad, max(b[3] for b in bs_) + pad)


def shadows_svg():
    import build_scene as bs
    defs = (f'<filter id="dog-shadow-blur" x="-0.2" y="-0.5" width="1.4" height="2">'
            f'<feGaussianBlur stdDeviation="{SHADOW_BLUR}"/></filter>\n')
    body = "".join(bs.group(sid, f'<path d="{smooth(pts)}" fill="{SHADOW_FILL}" opacity="{fmt(SHADOW_OP)}" '
                                 'filter="url(#dog-shadow-blur)"/>')
                   for sid, pts in SHADOWS)
    return bs.layer_svg("dog-shadows", body, defs)


def dog_layers():
    import build_scene as bs
    layers = {"dog-shadows": shadows_svg()}
    for fn in DOGS:
        dog, defs, body = dog_body(fn)
        layers[dog] = bs.layer_svg(dog, body, defs)
    assert set(layers) == set(DOG_ORDER)
    return layers


def dog_fns():
    return {fn()[0]: fn for fn in DOGS}


def crop_svg(dog_svg, box, height):
    """A full-frame dog SVG with its viewBox cropped to `box`, and the render width that
    makes it at least `height` tall."""
    x0, y0, x1, y1 = box
    w, h = x1 - x0, y1 - y0
    svg = re.sub(r'width="\d+" height="\d+" viewBox="0 0 \d+ \d+"',
                 f'width="{fmt(w)}" height="{fmt(h)}" viewBox="{fmt(x0)} {fmt(y0)} {fmt(w)} {fmt(h)}"',
                 dog_svg, count=1)
    width = int(-(-height * w // h)) + 1
    return svg, width, (x0, y0, x1, y1)


def standalone(dog_svg, fn):
    """The same dog with its viewBox cropped tight around it, and the render width that
    makes it at least STANDALONE_H tall."""
    return crop_svg(dog_svg, dog_bbox(fn), STANDALONE_H)


# The interface build_scene.py uses for either set of dogs (hand-drawn here, traced in
# trace_dogs.py): dog_names(), dog_layers(), bbox(name), crop(svg, name).
def dog_names():
    return [fn()[0] for fn in DOGS]


def bbox(name):
    return dog_bbox(dog_fns()[name])


def crop(dog_svg, name):
    return standalone(dog_svg, dog_fns()[name])
