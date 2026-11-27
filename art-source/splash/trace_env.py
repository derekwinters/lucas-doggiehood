#!/usr/bin/env python3
"""Doggiehood splash environment, vector-traced from the original art (the default scene).

The environment (clouds, street, houses, trees, props) is cut out of
`Assets/Art/Splash/cover-art.png` (read-only) object by object, completed where the dogs,
the title and nearer objects hide it, and traced to vector paths with VTracer, the same
way as the dogs (trace_dogs.py). The sky stays a clean gradient and the sun a radial
glow, both fitted to the original.

Pipeline (all deterministic, so a rebuild is byte-identical):

1. Objects. OBJECTS lists every object back to front (the stacking order of the layers).
   Each has a rough shape: the matching hand-built object from build_scene.py, or an
   explicit polygon / ellipse where the hand-built shape is too far off.
2. Visible map. A watershed on the original, seeded with each object's rough visible
   region (eroded) and with the sky, snaps every object to the original's colour edges.
   The occluders - the dogs, their shadows, the title (with its drop shadow) and the
   corner sparkle - are a separate label that is never traced.
3. Completion. Each object's full region is what it shows plus what of its rough shape
   is hidden by something drawn above it (a nearer object, a dog, the title). Hidden
   pixels are filled from the object's own visible pixels (push-pull membrane fill of the
   colour, so the road continues as road, a wall as wall), so no layer has a hole.
4. Trace. Per object: crop, upscale, mean-shift flatten, quantize to a small palette
   (deterministic k-means in Lab), fold anti-aliasing colours, smooth the label map and
   trace each colour with VTracer as a stacked layer. Paths are grouped by colour cluster
   inside one group per object.

`build_scene.py` writes everything; run `python3 art-source/splash/build_scene.py`.
"""
from __future__ import annotations

import io
import re
import xml.etree.ElementTree as ET
from functools import lru_cache

import numpy as np
from PIL import Image

import build_title as bt

fmt = bt.fmt
W, H = 2816, 1536

# --------------------------------------------------------------------------
# Tuning
# --------------------------------------------------------------------------
OCC_GROW = 3               # px the dog / shadow masks are grown before they are cut out
TITLE_GROW = 10            # px the title's alpha is grown (the original's softer outline and shadow)
SPARKLE = [(2508, 1240), (2640, 1240), (2640, 1370), (2508, 1370)]   # image-generator mark
SKY_BOTTOM = 760           # nothing below this is sky
SEED_RING = 12             # px ring around a tree / prop whose colour is its background
SEED_DE = 14.0             # tree / prop seeds differ at least this much (Lab) from it
SKY_RECLAIM_DE = 10.0      # near-house pixels this close (Lab) to the sky's row colour, joined to it, are sky
SKY_CLEAR = 10             # px around every rough object that is not seeded as sky
OBJ_FRINGE = 4             # px of an object next to a dog / the title filled like its hidden part
KNOWN_TRIM = 2             # px of an object's visible edge not used to fill its hidden part
ROW_TRIM = 8               # ... for the street, filled row by row (soft shadow edges stay out)
GROUND_CORE_DE = 12.0      # per row, ground pixels this close (Lab) to its main colour set the gradient
GROUND_ROW_MIN = 8         # visible px a row needs to measure the ground's colour
GROUND_OUTLIER_ROWS = 80   # a row's ground colour is checked against the rows this close ...
GROUND_OUTLIER_DE = 15.0   # ... and dropped (interpolated) when this far (Lab) from their median
GROUND_SMOOTH = 3.0        # rows of smoothing of the ground's fitted gradient
GROUND_STOP = 12           # original rows per gradient stop of the ground
GROUND_DETAIL_DE = 7.0     # ground pixels this far (Lab) from the gradient are traced as detail
GROUND_DETAIL_K = 10       # palette size of the ground's detail
ROAD_SPECK = 30            # road detail mostly this close to a dog is a dog speck
GROUND_MIN = 3             # ground detail smaller than this (original px, squared) is dropped
ROW_MIN = 300.0            # known px a street fill window needs to be trusted fully
ROW_WINDOW = 140           # px along a row the street fill averages over
STREET_FRINGE = 14         # px around a dog where off-colour ground is refilled ...
STREET_DE = 12.0           # ... when it is this far (Lab) from the local ground colour
TUCK = 3                   # px an object runs under whatever is drawn above it
MIN_PART = 60              # visible specks smaller than this (px) are dropped
MEANSHIFT = (4, 10)        # spatial / colour radius of the flattening filter (trace px)
LABEL_SIGMA = 0.6          # blur of the one-hot label map before argmax (original px)
SPECKLE = 1.5              # VTracer filter_speckle: drop patches under SPECKLE^2 original px
EDGE_GRAD = 24.0           # Sobel magnitude of L (trace px) counted as an edge
TRANSITION_FRAC = 0.6      # a colour this often on edges is an anti-aliasing artefact
TRACE_LONG = 1600          # an object is traced at about this many px on its long side
TRACE_SCALE = (1, 8)       # ... with the trace scale clamped to this range
CROP_PAD = 4               # original px around an object in its trace crop
CLUSTER_DE = 20.0          # palette colours closer than this (Lab) share a colour group
OBJECT_LONG = 2000         # standalone object PNGs are at least this long on the long side
OBJECT_PAD = 10            # original px around an object in its standalone crop
TITLE_TIGHT_ALPHA = 128    # in TITLE_TIGHT windows the title occluder is only its letters (alpha above this) ...
TITLE_TIGHT_GROW = 4       # ... grown by this many px, not its soft drop shadow
FAR_RECLAIM_DE = 8.0       # far-object pixels this close (Lab) to the sky's row colour, joined to it, are sky
FAR_SMOOTH = 1.5           # px (Gaussian sigma) a far tree's outline is smoothed by
FAR_SKY_WINDOW = 61        # px window of the local sky colour a far object's sky-like pixels are compared with
FAR_PIECE = 0.1            # a far object's detached piece smaller than this share of its main part is dropped
FAR_OPEN = 1               # px radius of the opening that cuts slivers off a far object's outline
CLIP_EDGE = 1.5            # px inside a clip outline refilled from the object's interior (no anti-aliased rim)
FACE_STROKE = 1.0          # px stroke each hand-built face carries in its own fill, so neighbours overlap (no seam)
FACE_ERODE = 1             # px of a face's outline not sampled for its colour
FACE_DE = 12.0             # face samples further (Lab) than this from the face's median colour are dropped
FACE_MIN = 20              # sampled px a face needs
FACE_FLAT = 0.05           # a face whose colour changes less than this per px (luma) is drawn flat
CLOUD_ALPHA = 0.8          # opacity of a hand-built cloud's material over the sky behind it

# Street geometry: the road's edges (where the sidewalk meets the kerb) and the lawn edges,
# far-to-near polylines measured on the original's unhidden stretches
VANISH = (1490, 690)
ROAD_L = [(1321, 740), (447, 1530)]
ROAD_R = [(1621, 740), (2247, 1340), (2437, 1530)]
LAWN_L = [(304, 1320), (108, 1440)]
LAWN_R = [(2600, 1320), (2788, 1440)]
KERB_FACE, KERB_SHADOW = 0.068, 0.095   # kerb face / its shadow line: width per px below the horizon


def xat(pl, y):
    """x of a far-to-near polyline at height y (extrapolated past its ends)."""
    for (x0, y0), (x1, y1) in zip(pl, pl[1:]):
        if y <= y1 or (x1, y1) == pl[-1]:
            return x0 + (x1 - x0) * (y - y0) / (y1 - y0)
    raise AssertionError("unreachable")


def _street_polys():
    top, bot = 688, H + 40
    ys = sorted({top, bot} | {y for pl in (ROAD_L, ROAD_R) for _, y in pl if top < y < bot})

    def strip(pa, a, pb, b):
        # between offsets a and b (times the depth below the horizon) of polylines pa, pb
        return ([(xat(pa, y) + a * (y - VANISH[1]), y) for y in ys]
                + [(xat(pb, y) + b * (y - VANISH[1]), y) for y in reversed(ys)])

    road = strip(ROAD_L, 0, ROAD_R, 0)
    walk_l = strip(LAWN_L, 0, ROAD_L, 0)
    walk_r = strip(ROAD_R, 0, LAWN_R, 0)
    lawn_l = [(-40, 600), (xat(LAWN_L, top) + 30, 600)] + [(xat(LAWN_L, y), y) for y in (top, bot)] + [(-40, bot)]
    lawn_r = ([(xat(LAWN_R, top) - 30, 600), (W + 40, 600), (W + 40, bot)]
              + [(xat(LAWN_R, y), y) for y in (bot, top)])
    return dict(lawn_l=lawn_l, lawn_r=lawn_r, walk_l=walk_l, walk_r=walk_r, road=road,
                kerb_l_face=strip(ROAD_L, 0, ROAD_L, KERB_FACE),
                kerb_l_shadow=strip(ROAD_L, KERB_FACE, ROAD_L, KERB_SHADOW),
                kerb_r_face=strip(ROAD_R, -KERB_FACE, ROAD_R, 0),
                kerb_r_shadow=strip(ROAD_R, -KERB_SHADOW, ROAD_R, -KERB_FACE))


STREET = _street_polys()

# --------------------------------------------------------------------------
# Hand-built parts, measured on the original. Where the original hides most of something
# that has clean geometric faces (the yellow house's front steps behind the puppy, the two
# clouds behind the title), a traced completion reads as a ragged patch: these are drawn
# instead as flat faces (polygons in the original's pixel space), each coloured from what
# the original shows of it (a gradient fitted to its visible pixels).
# --------------------------------------------------------------------------
# The yellow house's foundation, front steps and front path (house-yellow), back to front.
# The puppy hides their lower half and the corgi's ear the path's right end.
STEPS_BOX = (690, 826, 905, 905)       # in here house-yellow is these faces only ...
STEPS_WALL = (690, 826, 730, 842)      # ... and what the original shows of its wall above the foundation
STEPS = [
    ("foundation", [(652, 848), (694, 838.5), (727.7, 829.5), (721, 872), (694, 883.5), (652, 890.5)]),
    ("landing-front", [(727.7, 828), (776, 828), (776, 872), (721, 872)]),
    ("step-1-tread", [(728, 828), (776, 828), (828, 813.3), (831, 810.5), (820, 810), (810, 809),
                      (800, 809.5), (790, 812.5), (776, 815.5), (760, 819.5), (745, 823), (729, 826)]),
    ("step-1-riser", [(776, 828), (837, 810.7), (837, 827.3), (820, 832), (800, 840), (776, 846)]),
    ("step-2-tread", [(776, 846), (800, 840), (820, 832), (840, 827), (855, 826.5),
                      (855, 830), (840, 833.5), (830, 837.5), (820, 841), (810, 843.5), (800, 847.5), (776, 856)]),
    ("step-2-riser", [(776, 856), (800, 847.5), (810, 843.5), (820, 841), (830, 837.5), (840, 833.5),
                      (855, 830), (855, 851.7), (840, 857), (800, 871), (776, 876)]),
    ("path", [(776, 876), (800, 871), (840, 857), (855, 851.7), (870, 863.5), (900, 863.5), (900, 879.5),
              (776, 879.5)]),
    ("path-edge", [(776, 879.5), (900, 879.5), (900, 884.5), (776, 884.5)]),
]

# The two clouds behind the title, as voxel blocks like the original's clouds: a frontal
# face, the side face turned to the street's vanishing point (lit white on the sun's side)
# and the underside. What the title hides continues the faces the original shows; the
# right cloud's underside, hidden entirely, takes its material from the underside of
# cloud-top-mid (the cloud on the same side of the sun).
CLOUDS = {
    "cloud-behind-title-left": [
        ("underside", [(888, 258.2), (1032, 249.2), (1084, 288.2), (940, 297.2)], None),
        ("side", [(1032, 198.5), (1084, 237.5), (1084, 288.2), (1032, 249.2)], None),
        ("front", [(888, 221.5), (966, 217.5), (966, 202.5), (1032, 198.5), (1032, 249.2), (888, 258.2)], None),
    ],
    "cloud-behind-title-right": [
        ("underside", [(1911, 290), (2028, 290), (1985, 325), (1868, 325)],
         [(2120, 90), (2190, 75), (2160, 110), (2110, 112)]),       # sampled on cloud-top-mid
        ("side", [(1911, 220.5), (1911, 290), (1868, 325), (1868, 255.5)], None),
        ("front", [(1911, 220.5), (2025, 222.5), (2028, 290), (1911, 290)], None),
    ],
}

# Windows where the title occluder is the letters only: the original shows the far street
# right below them, which the title's grown drop shadow would otherwise hide (and the
# trace would complete with a guessed shape).
TITLE_TIGHT = [(1760, 440, 2040, 560)]
TITLE_TIGHT_OBJECTS = {"tree-far-right-4", "house-far-yellow"}

# Clips: within box, the object is only what lies inside the outline; what the cut leaves
# over goes to the nearest other label (or to `give`), and the band just inside the outline
# is refilled from the interior. (box, outline, give)
CLIPS = {
    # the tree by the yellow house: its canopy's right edge and its trunk, as the original
    # draws them (no slivers of the yellow wall, no grass rim round the trunk)
    "tree-near-yellow-house": [((640, 560, 760, 930), [
        (600, 540), (672, 540), (672, 575), (691, 588), (703, 600), (710, 618), (717, 636), (722, 648),
        (727, 660), (732, 678), (737, 696), (741, 714), (742.5, 723), (741, 732), (737, 744), (733, 756),
        (728, 768), (723, 780), (719, 792), (714, 804), (708, 816), (700, 819.5), (692, 820.5), (690.5, 823),
        (690.5, 874), (691.5, 890), (692.5, 906), (694, 910), (655, 910), (656, 895), (658, 878), (661, 850),
        (663, 823), (660, 820.5), (654, 819.5), (648, 817.5), (642, 814.5), (600, 812)], None)],
    # the yellow house's fence: it stands behind the front steps, so it ends where they begin
    "fence-yellow-house": [((812, 800, 895, 860), [
        (700, 700), (900, 700), (900, 827.5), (836, 827.5), (836, 811), (700, 811)], None)],
    # the green house: nothing left of its roof's outer edge and its wall (the blue house's
    # roof and chimney show there)
    "house-green": [((2380, 150, 2700, 700), [
        (2650.5, 150), (2900, 150), (2900, 700), (2506, 700), (2506, 600), (2501, 596), (2501, 577),
        (2456.2, 577)], "house-blue")],
    # a far tree: not the strip of the yellow far house's eave beside it
    "tree-far-right-5": [((1993, 600, 2015, 650), [(1900, 560), (1993, 560), (1993, 700), (1900, 700)], None)],
}


# Ground the original shows plain, where traced detail would only be specks off a dog's
# soft edge or the rim of the steps above it: the lawn below the yellow house's foundation
# and steps, beside the puppy (the tree's shadow left of x 702 stays).
PLAIN = {"lawn-left": [[(692, 868), (790, 868), (790, 930), (702, 930), (702, 896), (692, 892)]]}


def far_object(oid):
    """The distant objects in the haze (far houses, far trees, the horizon treeline)."""
    return OBJ[oid]["layer"] == "houses-far" or oid.startswith(("tree-far-", "poplar-", "horizon-"))

# --------------------------------------------------------------------------
# Objects, back to front (the stacking order of the layers). shape: a list of primitives
# whose union is the object's rough full shape - ("hand", hand layer, group id) for the
# matching hand-built object, ("poly", pts), ("ellipse", cx, cy, rx, ry), ("rect", x0, y0,
# x1, y1). erode: px of the rough shape that is surely the object (watershed seed).
# behind: objects that stand in front of this one although they are lower in the stack
# (a tree behind a house); the object never covers them.
# --------------------------------------------------------------------------
def _o(oid, layer, *shape, erode=6, **kw):
    return dict(id=oid, layer=layer, shape=list(shape), erode=erode, **kw)


def _tree(oid, cx, cy, rx, ry, trunk=None, extra=(), **kw):
    parts = [("ellipse", cx, cy, rx, ry)] + ([("rect", *trunk)] if trunk else []) + list(extra)
    return _o(oid, "trees", *parts, erode=kw.pop("erode", max(3, min(rx, ry) // 4)), **kw)


def _mailbox(oid, box, post, flag=None, erode=3):
    parts = [("rect", *box), ("rect", *post)] + ([("rect", *flag)] if flag else [])
    return _o(oid, "props", *parts, erode=erode)


OBJECTS = [
    # clouds
    _o("cloud-top-left", "clouds", ("hand", "clouds", "cloud-top-left"), erode=10),
    _o("cloud-top-mid", "clouds", ("hand", "clouds", "cloud-top-mid"), erode=8),
    _o("cloud-top-right", "clouds", ("hand", "clouds", "cloud-top-right"), erode=10),
    _o("cloud-behind-title-left", "clouds", ("hand", "clouds", "cloud-behind-title-left"), erode=6),
    _o("cloud-behind-title-right", "clouds", ("hand", "clouds", "cloud-behind-title-right"), erode=6),
    # street
    _o("lawn-left", "street", ("poly", STREET["lawn_l"]), erode=8),
    _o("lawn-right", "street", ("poly", STREET["lawn_r"]), erode=8),
    _o("sidewalk-left", "street", ("poly", STREET["walk_l"]), erode=6),
    _o("sidewalk-right", "street", ("poly", STREET["walk_r"]), erode=6),
    _o("road", "street", ("poly", STREET["road"]), erode=6),
    _o("kerb-left", "street", ("poly", STREET["kerb_l_face"]), erode=2, ground=False),
    _o("kerb-left-shadow", "street", ("poly", STREET["kerb_l_shadow"]), erode=1, ground=False),
    _o("kerb-right", "street", ("poly", STREET["kerb_r_face"]), erode=2, ground=False),
    _o("kerb-right-shadow", "street", ("poly", STREET["kerb_r_shadow"]), erode=1, ground=False),
    # far houses
    _o("house-far-salmon", "houses-far", ("poly", [(962, 512), (1002, 512), (1002, 540), (1135, 536),
                                                    (1166, 600), (1162, 745), (960, 745)]), erode=6),
    _o("house-far-rose", "houses-far", ("poly", [(1215, 600), (1266, 581), (1296, 640), (1292, 724),
                                                  (1215, 724)]), erode=4),
    _o("house-far-blue", "houses-far", ("poly", [(1652, 612), (1682, 575), (1714, 600), (1722, 702),
                                                  (1656, 702)]), erode=4),
    _o("house-far-pink", "houses-far", ("poly", [(1724, 602), (1748, 562), (1778, 588), (1792, 600),
                                                  (1792, 706), (1724, 706)]), erode=4),
    _o("house-far-yellow", "houses-far", ("poly", [(1850, 625), (1884, 520), (1953, 524), (1953, 486),
                                                    (2003, 486), (2003, 530), (2016, 560), (2016, 720),
                                                    (1945, 762), (1854, 762)]), erode=6),
    # near houses, left (far to near)
    _o("house-teal", "houses-left", ("hand", "houses-left", "house-teal"), erode=10),
    _o("house-yellow", "houses-left", ("hand", "houses-left", "house-yellow"), erode=12),
    _o("house-red", "houses-left", ("hand", "houses-left", "house-red"), erode=16),
    # near houses, right (far to near)
    _o("house-pink", "houses-right", ("hand", "houses-right", "house-pink"), erode=10),
    _o("house-blue", "houses-right", ("poly", [(2252, 374), (2512, 374), (2512, 792), (2442, 802),
                                                (2442, 852), (2172, 852), (2172, 604), (2160, 600)]),
       ("rect", 2430, 296, 2546, 460), erode=12),
    _o("house-green", "houses-right", ("hand", "houses-right", "house-green"), erode=16),
    # trees (the horizon line of distant trees first)
    _o("horizon-treeline", "trees", ("poly", [(1268, 702), (1268, 640), (1300, 600), (1340, 596),
                                               (1390, 590), (1430, 610), (1462, 638), (1530, 636),
                                               (1560, 598), (1610, 596), (1650, 630), (1662, 702)]),
       erode=4, behind=["house-far-rose", "house-far-blue"]),
    _tree("tree-far-left-1", 1070, 638, 72, 62, (1048, 690, 1080, 790)),
    _tree("tree-far-left-2", 1190, 596, 42, 52, (1178, 640, 1196, 722)),
    _tree("tree-far-left-3", 1228, 642, 24, 45, (1222, 680, 1235, 726)),
    _tree("tree-far-left-4", 1345, 635, 32, 32, (1339, 660, 1353, 706)),
    _tree("poplar-1", 1382, 674, 7, 18),
    _tree("poplar-3", 1556, 664, 8, 26),
    _tree("poplar-4", 1574, 667, 8, 24),
    _tree("tree-far-right-1", 1610, 628, 28, 32, (1604, 650, 1616, 694)),
    _tree("tree-far-right-2", 1720, 604, 22, 30, behind=["house-far-blue", "house-far-pink"]),
    _tree("tree-far-right-3", 1785, 640, 38, 48, (1774, 670, 1792, 746)),
    # (plus its crown's top right, which the ellipse misses: the original shows it below the title)
    _tree("tree-far-right-4", 1862, 565, 78, 88, (1824, 620, 1852, 702),
          extra=[("poly", [(1855, 490), (1880, 476), (1918, 476), (1935, 500), (1948, 525), (1948, 545),
                           (1880, 545)])],
          behind=["house-far-yellow", "house-pink"]),
    _tree("tree-far-right-5", 1948, 652, 48, 84, (1939, 725, 1958, 792)),
    _tree("tree-behind-red-house", 430, 402, 56, 76, behind=["house-red", "house-yellow"]),
    _o("tree-near-yellow-house", "trees", ("poly", [(652, 572), (690, 590), (730, 640), (746, 720),
                                                     (738, 780), (700, 820), (697, 912), (655, 912),
                                                     (652, 820), (612, 790), (600, 720), (612, 640)]),
       erode=8),
    _o("tree-near-left", "trees", ("poly", [(0, 518), (55, 514), (112, 545), (152, 600), (180, 680),
                                             (184, 780), (162, 860), (112, 905), (72, 908), (72, 1062),
                                             (0, 1062)]), erode=10),
    # props
    _o("bush-front-left", "props", ("poly", [(0, 1008), (40, 994), (110, 955), (200, 958), (256, 994),
                                              (302, 1058), (310, 1125), (276, 1152), (200, 1166),
                                              (142, 1186), (126, 1246), (70, 1264), (0, 1264)]), erode=8),
    _o("bush-far-left", "props", ("ellipse", 1125, 728, 38, 25), erode=3),
    _o("bush-far-right", "props", ("ellipse", 1832, 722, 36, 28), erode=3),
    _o("bush-tree-blue-house", "props", ("ellipse", 2172, 688, 42, 68), erode=6),
    _o("bush-blue-house", "props", ("ellipse", 2300, 846, 40, 46), erode=5),
    _o("bush-green-house", "props", ("ellipse", 2540, 935, 36, 56), erode=5),
    _o("fence-yellow-house", "props", ("rect", 818, 742, 882, 835), erode=4),
    _o("fence-blue-house-left", "props", ("rect", 2083, 728, 2195, 842), erode=5),
    _o("fence-blue-house-right", "props", ("rect", 2362, 772, 2505, 852), erode=5),
    _o("fence-green-house", "props", ("poly", [(2700, 915), (2816, 900), (2816, 1152), (2700, 1122)]),
       erode=6),
    _mailbox("mailbox-far-right-1", (1620, 668, 1652, 692), (1630, 690, 1638, 712)),
    _mailbox("mailbox-far-right-2", (1668, 677, 1703, 701), (1680, 698, 1689, 724)),
    _mailbox("mailbox-far-right-3", (1717, 689, 1769, 723), (1737, 720, 1749, 770)),
    _mailbox("mailbox-far-right-4", (1832, 721, 1913, 769), (1865, 765, 1883, 802)),
    _mailbox("mailbox-far-left-3", (1297, 671, 1331, 701), (1309, 698, 1319, 736)),
    _mailbox("mailbox-far-left-2", (1184, 691, 1233, 729), (1202, 725, 1214, 766)),
    _mailbox("mailbox-far-left-1", (965, 724, 1051, 793), (994, 780, 1016, 832), erode=5),
    _mailbox("mailbox-left", (390, 848, 602, 964), (452, 955, 503, 1146), (466, 813, 511, 905), erode=8),
    _mailbox("mailbox-right", (2316, 848, 2507, 964), (2392, 955, 2443, 1146), (2378, 808, 2429, 926),
             erode=8),
    _o("flower-white", "props", ("ellipse", 160, 1264, 33, 42), erode=4),
    _o("flower-pink-left", "props", ("ellipse", 275, 1212, 33, 46), erode=4),
    _o("flower-red-right", "props", ("ellipse", 2155, 945, 23, 23), erode=3),
    _o("flower-pink-right", "props", ("ellipse", 2240, 916, 26, 36), erode=3),
    _o("toy-bone-orange", "props", ("ellipse", 617, 1186, 53, 29), erode=4),
    _o("toy-bone-blue", "props", ("ellipse", 662, 1215, 55, 27), erode=4),
    _o("toy-bowl", "props", ("ellipse", 2570, 1198, 63, 36), erode=4),
    _o("toy-bone-red", "props", ("ellipse", 2676, 1165, 58, 33), erode=4),
]

TRACED_LAYERS = ["clouds", "street", "houses-far", "houses-left", "houses-right", "trees", "props"]


# --------------------------------------------------------------------------
# Helpers
# --------------------------------------------------------------------------
def disk(r):
    r = int(round(r))
    y, x = np.mgrid[-r:r + 1, -r:r + 1]
    return ((x * x + y * y) <= r * r).astype(np.uint8)


def dilate(m, r):
    import cv2
    return cv2.dilate(m.astype(np.uint8), disk(r)).astype(bool) if r > 0 else m.copy()


def erode(m, r):
    import cv2
    return cv2.erode(m.astype(np.uint8), disk(r)).astype(bool) if r > 0 else m.copy()


def to_lab(rgb_u8):
    import cv2
    return cv2.cvtColor((np.asarray(rgb_u8, np.float32) / 255.0), cv2.COLOR_RGB2Lab)


def hexc(rgb):
    return "#%02x%02x%02x" % tuple(int(v) for v in np.clip(np.round(rgb), 0, 255))


@lru_cache(maxsize=1)
def original():
    return np.asarray(Image.open(bt.ORIGINAL).convert("RGB"))


def poly_mask(pts):
    from PIL import ImageDraw
    im = Image.new("L", (W, H), 0)
    ImageDraw.Draw(im).polygon([(float(x), float(y)) for x, y in pts], fill=255)
    return np.asarray(im) > 0


@lru_cache(maxsize=None)
def hand_layer(layer):
    import build_scene as bs
    return {"clouds": bs.build_clouds, "street": bs.build_street, "trees": bs.build_trees,
            "props": bs.build_props,
            "houses-far": lambda: bs.build_houses(bs.HOUSES_FAR, "houses-far"),
            "houses-left": lambda: bs.build_houses(bs.HOUSES_LEFT, "houses-left"),
            "houses-right": lambda: bs.build_houses(bs.HOUSES_RIGHT, "houses-right")}[layer]()


def hand_group_alpha(layer, gid, unclip=True):
    """Alpha (full frame, 1x) of one group of a hand-built layer, rendered on its own."""
    import build_scene as bs
    svg = hand_layer(layer)
    defs, _ = bs.inner(svg)
    ET.register_namespace("", "http://www.w3.org/2000/svg")
    ET.register_namespace("inkscape", "http://www.inkscape.org/namespaces/inkscape")
    root = ET.fromstring(svg.encode("utf-8"))
    el = next(e for e in root.iter() if e.get("id") == gid)
    if unclip and "clip-path" in el.attrib:
        del el.attrib["clip-path"]          # trees behind houses: their whole canopy
    body = ET.tostring(el, encoding="unicode")
    a = np.asarray(bt.png_image(bt.render(bs.svg_doc(body, defs), W)))[..., 3]
    return a > 96


def primitive(kind, *args):
    if kind == "hand":
        return hand_group_alpha(*args)
    if kind == "poly":
        return poly_mask(args[0])
    if kind == "rect":
        x0, y0, x1, y1 = args
        return poly_mask([(x0, y0), (x1, y0), (x1, y1), (x0, y1)])
    if kind == "ellipse":
        cx, cy, rx, ry = args
        yy, xx = np.mgrid[0:H, 0:W]
        return ((xx - cx) / rx) ** 2 + ((yy - cy) / ry) ** 2 <= 1
    raise ValueError(kind)


@lru_cache(maxsize=None)
def rough(oid):
    """The object's rough full shape (full-frame bool)."""
    m = np.zeros((H, W), bool)
    for p in OBJ[oid]["shape"]:
        m |= primitive(*p)
    return m


OBJ = {o["id"]: o for o in OBJECTS}
ORDER = [o["id"] for o in OBJECTS]


def depth_order():
    """ORDER, with every object that stands behind lower-stacked objects moved back to just
    before the first of them (the order things are really in, far to near)."""
    out = [o for o in ORDER if not OBJ[o].get("behind")]
    for o in ORDER:
        b = OBJ[o].get("behind")
        if b:
            out.insert(min(out.index(x) for x in b), o)
    return out


# --------------------------------------------------------------------------
# 1-2. Occluders and the visible map
# --------------------------------------------------------------------------
@lru_cache(maxsize=1)
def occluders():
    """Everything that is not environment: dogs (incl. the puppy), dog shadows, the title
    and its drop shadow, the corner sparkle. Full-frame bool."""
    import build_scene as bs
    import trace_dogs as td
    m = np.zeros((H, W), bool)
    for d in td.dog_masks().values():
        m |= d
    for _, s in td.shadow_masks().values():
        m |= s
    m = dilate(m, OCC_GROW)
    d, b = bs.inner(bs.TITLE_SVG.read_text(encoding="utf-8"))
    ta = np.asarray(bt.png_image(bt.render(bs.svg_doc(b, d), W)))[..., 3]
    t = dilate(ta > 8, TITLE_GROW)
    tight = dilate(ta > TITLE_TIGHT_ALPHA, TITLE_TIGHT_GROW)
    for x0, y0, x1, y1 in TITLE_TIGHT:
        t[y0:y1, x0:x1] = tight[y0:y1, x0:x1]
    m |= t
    m |= poly_mask(SPARKLE)
    return m


@lru_cache(maxsize=1)
def visible_map():
    """Label per pixel: index into ORDER of the object the original shows there, -1 for
    sky, -2 for an occluder."""
    import cv2
    from scipy import ndimage as ndi
    occ = occluders()
    n = len(ORDER)
    top = np.full((H, W), -1, np.int32)
    for oid in depth_order():
        top[rough(oid)] = ORDER.index(oid)
    markers = np.zeros((H, W), np.int32)
    anything = top >= 0
    lab = to_lab(cv2.GaussianBlur(original(), (0, 0), 1.0))
    for i, oid in enumerate(ORDER):
        seed = erode(top == i, OBJ[oid]["erode"]) & ~occ
        if OBJ[oid]["layer"] in ("trees", "props"):
            # small things on a plain background: only pixels that differ from the
            # background around the rough shape are surely the object
            ring = dilate(rough(oid), SEED_RING) & ~dilate(rough(oid), 2) & ~occ
            if ring.any():
                bg = np.median(lab[ring], axis=0)
                distinct = seed & (np.linalg.norm(lab - bg, axis=-1) > SEED_DE)
                if distinct.sum() >= 0.15 * seed.sum():
                    seed = distinct
        markers[seed] = i + 1
    yy = np.arange(H)[:, None]
    sky = ~dilate(anything, SKY_CLEAR) & ~occ & (yy < SKY_BOTTOM)
    markers[sky] = n + 1
    markers[occ] = n + 2
    img = cv2.pyrMeanShiftFiltering(np.ascontiguousarray(original()[..., ::-1]), 4, 10)
    cv2.watershed(img, markers)
    # watershed lines (-1) go to the neighbouring label that is most common
    lab = markers.copy()
    lines = lab == -1
    lab[lines] = 0
    _, (iy, ix) = ndi.distance_transform_edt(lab == 0, return_indices=True)
    lab = lab[iy, ix]
    out = lab - 1
    out[lab == n + 1] = -1
    out[lab == n + 2] = -2
    # clipped objects: what the watershed gave them outside their outline goes to the label
    # named for it, or to the nearest other label
    for oid, clips in CLIPS.items():
        i = ORDER.index(oid)
        for box, pts, give in clips:
            cut = (out == i) & clip_out(box, pts)
            if not cut.any():
                continue
            if give:
                out[cut] = ORDER.index(give)
            else:
                other = (out != i) & (out != -2)
                _, (iy, ix) = ndi.distance_transform_edt(~other, return_indices=True)
                out[cut] = out[iy, ix][cut]
    # sky the watershed gave to a near house (its rough shape runs past the roof into the
    # sky, where no sky is seeded): sky-coloured pixels joined to the sky go back to it
    near = np.isin(out, [i for i, o in enumerate(ORDER) if OBJ[o]["layer"] in ("houses-left", "houses-right")])
    olab = to_lab(original())
    ref = np.full((H, 3), np.nan)
    for y in range(SKY_BOTTOM):
        if sky[y].sum() >= 8:
            ref[y] = np.median(olab[y][sky[y]], axis=0)
    ys = np.nonzero(~np.isnan(ref[:, 0]))[0]
    ref = np.stack([np.interp(np.arange(H), ys, ref[ys, c]) for c in range(3)], -1)
    skyish = near & (np.linalg.norm(olab - ref[:, None, :], axis=-1) < SKY_RECLAIM_DE) & (yy < SKY_BOTTOM)
    lbl, _ = ndi.label(skyish | (out == -1))
    keep = np.unique(lbl[out == -1])
    out[skyish & np.isin(lbl, keep[keep > 0])] = -1
    # the same for the far objects in the haze (a band of sky above a far roof, a sliver
    # beside a far tree), in a pass of their own so the near houses are not affected
    # (compared with the sky right around them: near the sun the sky's colour changes along
    # a row)
    import cv2
    far = np.isin(out, [i for i, o in enumerate(ORDER) if far_object(o)])
    sk = (out == -1).astype(np.float32)
    ksz = (FAR_SKY_WINDOW, FAR_SKY_WINDOW)
    den = cv2.boxFilter(sk, -1, ksz, normalize=False)
    loc = cv2.boxFilter(olab * sk[..., None], -1, ksz, normalize=False) / np.maximum(den, 1e-6)[..., None]
    skyish = (far & (den > 0) & (np.linalg.norm(olab - loc, axis=-1) < FAR_RECLAIM_DE)
              & (yy < SKY_BOTTOM))
    lbl, _ = ndi.label(skyish | (out == -1))
    keep = np.unique(lbl[out == -1])
    out[skyish & np.isin(lbl, keep[keep > 0])] = -1
    return out


def tight_windows():
    m = np.zeros((H, W), bool)
    for x0, y0, x1, y1 in TITLE_TIGHT:
        m[y0:y1, x0:x1] = True
    return m


def clip_out(box, pts):
    """Full-frame bool: inside box but outside the outline pts."""
    x0, y0, x1, y1 = box
    m = np.zeros((H, W), bool)
    m[y0:y1, x0:x1] = True
    return m & ~poly_mask(pts)


def clip_edge(oid):
    """Full-frame bool: the band just inside each clip outline of oid (within its box)."""
    m = np.zeros((H, W), bool)
    for (x0, y0, x1, y1), pts, _ in CLIPS.get(oid, []):
        p = poly_mask(pts)
        band = p & ~erode(p, CLIP_EDGE)
        m[y0:y1, x0:x1] |= band[y0:y1, x0:x1]
    return m


# --------------------------------------------------------------------------
# 3. Completion: each object's full region, and its colours with the hidden part filled
# --------------------------------------------------------------------------
def push_pull(img, known):
    """Fill the unknown pixels of img (H x W x 3 float) from the known ones: a smooth
    membrane-like fill (pyramid push-pull), exact on the known pixels."""
    import cv2
    levels = []
    I = img * known[..., None]
    K = known.astype(np.float32)
    while True:
        levels.append((I, K))
        h, w = K.shape
        if h <= 2 and w <= 2:
            break
        size = (max(1, (w + 1) // 2), max(1, (h + 1) // 2))
        I = cv2.resize(I, size, interpolation=cv2.INTER_AREA)
        K = cv2.resize(K, size, interpolation=cv2.INTER_AREA)
    F = None
    for I, K in reversed(levels):
        own = I / np.maximum(K, 1e-6)[..., None]
        if F is None:
            F = own
            continue
        U = cv2.resize(F, (K.shape[1], K.shape[0]), interpolation=cv2.INTER_LINEAR)
        w = np.clip(K * 4, 0, 1)[..., None]
        F = own * w + U * (1 - w)
    return F


@lru_cache(maxsize=1)
def regions():
    """{object id: (full-frame bool region, full-frame bool visible part)}."""
    from scipy import ndimage as ndi
    vis = visible_map()
    occ = vis == -2
    n = len(ORDER)
    out = {}
    for i, oid in enumerate(ORDER):
        v = vis == i
        lbl, k = ndi.label(v)
        if k:
            sizes = ndi.sum(v, lbl, range(1, k + 1))
            v = np.isin(lbl, [j + 1 for j, s in enumerate(sizes) if s >= MIN_PART])
        later = (vis > i) & (vis < n)
        for b in OBJ[oid].get("behind", []):
            later &= vis != ORDER.index(b)
        covered = later | occ
        # (the far objects right under the letters in a TITLE_TIGHT window are not completed
        # under them: the original shows them right up to their own edge there)
        if oid in TITLE_TIGHT_OBJECTS:
            covered &= ~(occ & tight_windows())
        r = v | (rough(oid) & covered) | (dilate(v, TUCK) & covered)
        r = ndi.binary_opening(r, np.ones((3, 3))) | v
        for box, pts, _ in CLIPS.get(oid, []):
            r &= ~clip_out(box, pts)
            v &= r
        if oid in CLOUDS:
            # drawn as hand-built faces: the region is their outline
            r = faces_mask(CLOUDS[oid])
            v &= r
        elif oid == "house-yellow":
            # around the steps the hidden part is the hand-built faces, not the rough shape
            x0, y0, x1, y1 = STEPS_BOX
            box = np.zeros((H, W), bool)
            box[y0:y1, x0:x1] = True
            wall = np.zeros((H, W), bool)
            wx0, wy0, wx1, wy1 = STEPS_WALL
            wall[wy0:wy1, wx0:wx1] = True
            r = (r & ~box) | (r & box & v & wall) | faces_mask(STEPS)
            v &= r
        elif far_object(oid):
            # without slivers of a neighbour; a tree's outline smoothed too (a house keeps
            # its corners)
            if OBJ[oid]["layer"] == "trees":
                r = ndi.gaussian_filter(r.astype(np.float32), FAR_SMOOTH) > 0.5
            r = ndi.binary_opening(r, disk(FAR_OPEN).astype(bool))
            lbl, k = ndi.label(r)
            if k > 1:
                sizes = ndi.sum(r, lbl, range(1, k + 1))
                r = np.isin(lbl, [j + 1 for j, s in enumerate(sizes)
                                  if s >= max(MIN_PART, FAR_PIECE * sizes.max())])
            v &= r
        out[oid] = (r, v)
    # anything hidden under a dog that no object completes is ground: give it to the nearest
    # street object, so the street never shows a hole
    have = np.zeros((H, W), bool)
    for r, _ in out.values():
        have |= r
    yy = np.arange(H)[:, None]
    gap = occ & ~have & (yy >= SKY_BOTTOM - 70)
    if gap.any():
        street = [o for o in ORDER if OBJ[o]["layer"] == "street"]
        dist = np.stack([ndi.distance_transform_edt(~out[o][0]) for o in street])
        near = dist.argmin(0)
        for j, o in enumerate(street):
            r, v = out[o]
            out[o] = (r | (gap & (near == j)), v)
    return out


def is_ground(oid):
    return OBJ[oid]["layer"] == "street" and OBJ[oid].get("ground", True)


def trimmed(v, r):
    k = erode(v, r)
    return k if k.sum() > 0.3 * v.sum() else v


def row_median(img, known):
    """Each row: the median of the row's known pixels (rows without any interpolated)."""
    ys = [y for y in range(img.shape[0]) if known[y].sum() >= 8]
    med = np.array([np.median(img[y][known[y]], axis=0) for y in ys])
    rows = np.stack([np.interp(np.arange(img.shape[0]), ys, med[:, c]) for c in range(3)], -1)
    return np.broadcast_to(rows[:, None, :], img.shape).astype(np.float32)


def row_fill(img, known):
    """Mean of the known pixels within ROW_WINDOW px along the same rows (a few rows high),
    falling back to the push-pull fill where a window has none."""
    import cv2
    k = known.astype(np.float32)
    ksize = (2 * ROW_WINDOW + 1, 5)
    num = cv2.boxFilter(img * k[..., None], -1, ksize, normalize=False, borderType=cv2.BORDER_CONSTANT)
    den = cv2.boxFilter(k, -1, ksize, normalize=False, borderType=cv2.BORDER_CONSTANT)
    pp = push_pull(img, known)
    w = np.clip(den / ROW_MIN, 0, 1)[..., None]
    return num / np.maximum(den, 1e-6)[..., None] * w + pp * (1 - w)


def object_box(oid, pad=CROP_PAD):
    r = regions()[oid][0]
    ys, xs = np.nonzero(r)
    return (max(0, int(xs.min()) - pad), max(0, int(ys.min()) - pad),
            min(W, int(xs.max()) + pad + 1), min(H, int(ys.max()) + pad + 1))


def plate(oid):
    """(box, crop RGB float with the hidden part filled and the outside extended, region)."""
    from scipy import ndimage as ndi
    r, v = regions()[oid]
    x0, y0, x1, y1 = object_box(oid)
    rc, vc = r[y0:y1, x0:x1], v[y0:y1, x0:x1]
    img = original()[y0:y1, x0:x1].astype(np.float32)
    if is_ground(oid):
        # ground: hidden pixels take the mean of the visible ground nearby in the same rows
        # (the road darkens toward the viewer row by row); visible pixels close to a dog
        # that are far off that local colour (soft shadow edges, fur fringes) are refilled
        vc = vc.copy()
        occ = (visible_map() == -2)[y0:y1, x0:x1]
        fill = row_median if oid == "road" else row_fill
        filled = fill(img, trimmed(vc, ROW_TRIM))
        near = dilate(occ, STREET_FRINGE) & vc
        off = np.linalg.norm(to_lab(np.clip(img, 0, 255).astype(np.uint8)) -
                             to_lab(np.clip(filled, 0, 255).astype(np.uint8)), axis=-1) > STREET_DE
        vc &= ~(near & off)
        filled = fill(img, trimmed(vc, ROW_TRIM))
    else:
        # the few px of an object right next to a dog or the title carry that occluder's
        # soft outline (fur, a shadow edge): they are filled like the hidden part
        # a clip outline's anti-aliased rim (the neighbour's colour mixed in) likewise
        vc = vc & ~clip_edge(oid)[y0:y1, x0:x1]
        fringe = vc & dilate((visible_map() == -2)[y0:y1, x0:x1], OBJ_FRINGE)
        if (vc & ~fringe).sum() >= 0.5 * vc.sum():
            vc = vc & ~fringe
        known = erode(vc, KNOWN_TRIM)
        if known.sum() < 0.5 * vc.sum():
            known = vc
        filled = push_pull(img, known)
    out = np.where(vc[..., None], img, filled)
    # outside the region: the nearest region colour, so resampling never bleeds in
    _, (iy, ix) = ndi.distance_transform_edt(~rc, return_indices=True)
    out = out[iy, ix]
    return (x0, y0, x1, y1), out, rc


# --------------------------------------------------------------------------
# 4. Trace
# --------------------------------------------------------------------------
def colour_name(rgb):
    import colorsys
    r, g, b = (float(c) / 255 for c in rgb)
    h, l, s = colorsys.rgb_to_hls(r, g, b)
    if l < 0.18:
        return "dark"
    if s < 0.18 or max(r, g, b) - min(r, g, b) < 0.08:
        return "white" if l > 0.85 else ("grey" if l > 0.35 else "dark")
    hue = h * 360
    for lim, name in ((15, "red"), (40, "orange"), (65, "yellow"), (160, "green"), (195, "teal"),
                      (250, "blue"), (290, "purple"), (340, "pink"), (361, "red")):
        if hue < lim:
            base = name
            break
    if base in ("orange", "red") and l < 0.4:
        base = "brown"
    return base


def faces_mask(faces):
    """Full-frame bool: the union of hand-built faces (name, outline, ...)."""
    m = np.zeros((H, W), bool)
    for f in faces:
        m |= poly_mask(f[1])
    return m


@lru_cache(maxsize=1)
def backdrop():
    """Full-frame RGB (float) of the fitted sky with the sun's glow: what a cloud is seen
    against."""
    import build_scene as bs
    defs, body = "", ""
    for svg in (sky_svg(bs.layer_svg, bs.lin_grad), sun_svg(bs.layer_svg, bs.group)):
        d, b = bs.inner(svg)
        defs += d
        body += b
    img = bt.png_image(bt.render(bs.svg_doc(body, defs), W)).convert("RGB")
    return np.asarray(img).astype(np.float64)


def face_samples(oid, pts):
    """(xs, ys, rgb) of the original px that show a face (outline pts): inside it (outline
    eroded), hidden by no dog, no title and no object above oid; then only those near the
    face's median colour (a stray neighbour pixel never steers it)."""
    vis = visible_map()
    m = erode(poly_mask(pts), FACE_ERODE) & (vis != -2) & (vis <= ORDER.index(oid))
    ys, xs = np.nonzero(m)
    rgb = original()[ys, xs]
    lab = to_lab(rgb[None])[0]
    keep = np.linalg.norm(lab - np.median(lab, axis=0), axis=-1) < FACE_DE
    assert keep.sum() >= FACE_MIN, f"{oid}: a face at {pts[0]} shows only {int(keep.sum())} px"
    return xs[keep], ys[keep], rgb[keep].astype(np.float64)


def fit_gradient(xs, ys, rgb, pts, lo, hi):
    """A linear gradient fitted (least squares, per channel) to colours rgb at (xs, ys),
    spanning the outline pts: ((x1, y1), (x2, y2)) or None when flat, and its two end colours
    (clamped to lo..hi)."""
    A = np.stack([np.ones(len(xs)), xs, ys], -1).astype(np.float64)
    coef = np.linalg.lstsq(A, rgb, rcond=None)[0]          # rows: constant, per x, per y
    g = coef[1:] @ np.array([0.299, 0.587, 0.114])
    if np.hypot(*g) < FACE_FLAT:
        c = np.clip(np.median(rgb, axis=0), lo, hi)
        return None, c, c
    d = g / np.hypot(*g)
    cx, cy = float(np.mean(xs)), float(np.mean(ys))
    t = np.array(pts, np.float64) @ d - (cx * d[0] + cy * d[1])
    base = coef[0] + coef[1] * cx + coef[2] * cy
    along = coef[1] * d[0] + coef[2] * d[1]
    ends = [(cx + v * d[0], cy + v * d[1]) for v in (t.min(), t.max())]
    return ends, np.clip(base + along * t.min(), lo, hi), np.clip(base + along * t.max(), lo, hi)


def face_colour(oid, face):
    """The gradient of one hand-built face. Steps: fitted to what the original shows of the
    face. Clouds: the cloud's material (solved from what the original shows, as CLOUD_ALPHA
    of it over the sky) over the fitted sky and sun glow, so the sky shows through as in
    the original."""
    name, pts = face[0], face[1]
    if oid not in CLOUDS:
        xs, ys, rgb = face_samples(oid, pts)
        return fit_gradient(xs, ys, rgb, pts, np.percentile(rgb, 5, axis=0), np.percentile(rgb, 95, axis=0))
    a, bg = CLOUD_ALPHA, backdrop()
    xs, ys, rgb = face_samples(oid, face[2] or pts)
    material = np.median((rgb - (1 - a) * bg[ys, xs]) / a, axis=0)
    fy, fx = np.nonzero(poly_mask(pts))
    seen = np.clip(a * material + (1 - a) * bg[fy, fx], 0, 255)
    return fit_gradient(fx, fy, seen, pts, 0, 255)


def face_groups(oid, faces):
    """([(group name, [(fill, path d, extra attributes)])], <defs>) of hand-built faces, back
    to front; each face is stroked in its own fill so neighbouring faces overlap."""
    groups, defs = [], ""
    for face in faces:
        name, pts = face[0], face[1]
        ends, c0, c1 = face_colour(oid, face)
        if ends is None:
            fill = hexc(c0)
        else:
            gid = f"{oid}-{name}-grad"
            (x1, y1), (x2, y2) = ends
            defs += (f'<linearGradient id="{gid}" x1="{fmt(x1)}" y1="{fmt(y1)}" x2="{fmt(x2)}" y2="{fmt(y2)}" '
                     f'gradientUnits="userSpaceOnUse"><stop offset="0" stop-color="{hexc(c0)}"/>'
                     f'<stop offset="1" stop-color="{hexc(c1)}"/></linearGradient>\n')
            fill = f"url(#{gid})"
        d = "M " + " L ".join(f"{fmt(x)} {fmt(y)}" for x, y in pts) + " Z"
        extra = f' stroke="{fill}" stroke-width="{fmt(FACE_STROKE)}" stroke-linejoin="round"'
        groups.append((name, [(fill, d, extra)]))
    return groups, defs


@lru_cache(maxsize=None)
def trace(oid):
    """(crop box, [(group name, [(fill, path d[, extra attributes])])], <defs>) of one object,
    back to front."""
    import cv2
    import trace_dogs as td
    if oid in CLOUDS:
        # drawn entirely as hand-built faces
        groups, defs = face_groups(oid, CLOUDS[oid])
        return object_box(oid), groups, defs
    box, img, rc = plate(oid)
    x0, y0, x1, y1 = box
    long_side = max(x1 - x0, y1 - y0)
    s = int(min(max(round(TRACE_LONG / long_side), TRACE_SCALE[0]), TRACE_SCALE[1]))
    size = ((x1 - x0) * s, (y1 - y0) * s)
    up = np.asarray(Image.fromarray(np.clip(img, 0, 255).round().astype(np.uint8)).resize(size, Image.LANCZOS))
    mk = cv2.resize(rc.astype(np.float32), size, interpolation=cv2.INTER_LINEAR)
    inside = cv2.GaussianBlur(mk, (0, 0), 0.5 * s) > 0.5
    flat = cv2.pyrMeanShiftFiltering(np.ascontiguousarray(up[..., ::-1]), *MEANSHIFT)[..., ::-1]
    lab = to_lab(flat)
    area = float(rc.sum())
    k = int(min(16, max(5, round(4 + np.sqrt(area) / 14))))
    quant, defs, base = inside, "", None
    if is_ground(oid):
        # ground: a vertical gradient fitted row by row is the base; only what stands out
        # from it (path edges, kerb lines, joints, grass tufts) is traced on top
        from scipy import ndimage as ndi
        # the gradient follows the ground's own main colour (front paths, kerb lines and
        # grass tufts would otherwise drag whole rows toward them)
        # colour: only what the original shows is measured (the hidden part is filled
        # from it, so it would only echo it back), and a row that shows mostly something
        # else - a front path crossing the lawn - is left to the rows around it
        ilab = to_lab(np.clip(img, 0, 255).astype(np.uint8))
        vrc = regions()[oid][1][y0:y1, x0:x1] & rc
        rows = np.full((img.shape[0], 3), np.nan)
        rlab = np.full((img.shape[0], 3), np.nan)
        for y in range(img.shape[0]):
            sel = vrc[y]
            if sel.sum() < GROUND_ROW_MIN:
                continue
            for _ in range(2):      # trimmed: keep the pixels near the row's main colour
                m = np.median(ilab[y][sel], axis=0)
                keep = sel & (np.linalg.norm(ilab[y] - m, axis=-1) < GROUND_CORE_DE)
                if keep.sum() >= 3:
                    sel = keep
            rows[y] = np.median(img[y][sel], axis=0)
            rlab[y] = np.median(ilab[y][sel], axis=0)
        ok = ~np.isnan(rows[:, 0])
        for y in np.nonzero(ok)[0]:
            near = ok.copy()
            near[:max(0, y - GROUND_OUTLIER_ROWS)] = False
            near[y + GROUND_OUTLIER_ROWS + 1:] = False
            if np.linalg.norm(rlab[y] - np.median(rlab[near], axis=0)) > GROUND_OUTLIER_DE:
                ok[y] = False
        if not ok.any():
            ok = ~np.isnan(rows[:, 0])
        yy = np.arange(img.shape[0])
        rows = np.stack([ndi.gaussian_filter1d(np.interp(yy, yy[ok], rows[ok, c]), GROUND_SMOOTH)
                         for c in range(3)], -1)
        model = np.stack([np.interp((np.arange(size[1]) + 0.5) / s - 0.5, yy, rows[:, c]) for c in range(3)], -1)
        mlab = to_lab(np.clip(model, 0, 255).astype(np.uint8)[None])[0]
        de = np.linalg.norm(lab - mlab[:, None, :], axis=-1)
        # hidden ground (under the dogs, the title, nearer objects) is the plain gradient:
        # detail is only traced where the original shows it
        vis_up = cv2.resize(regions()[oid][1][y0:y1, x0:x1].astype(np.uint8), size,
                            interpolation=cv2.INTER_NEAREST).astype(bool)
        quant = inside & vis_up & (de > GROUND_DETAIL_DE)
        quant = ndi.binary_opening(quant, disk(max(1, s // 2)).astype(bool))
        lbl, nl = ndi.label(quant)
        if nl:
            sz = ndi.sum(quant, lbl, range(1, nl + 1))
            # specks of dog left on the ground (claws, fur, shadow fringes) are dropped too
            occ = cv2.resize(dilate((visible_map() == -2)[y0:y1, x0:x1], ROAD_SPECK).astype(np.uint8), size,
                             interpolation=cv2.INTER_NEAREST).astype(bool)
            nearo = ndi.mean(occ, lbl, range(1, nl + 1)) if oid == "road" else np.zeros(nl)
            # detail that a house, tree or prop drawn above hides anyway (mostly stray
            # pixels the cut gave the ground) is left out: the ground there is plain
            vis = visible_map()[y0:y1, x0:x1]
            under = cv2.resize(((vis > ORDER.index(oid)) | (vis == -1)).astype(np.uint8), size,
                               interpolation=cv2.INTER_NEAREST).astype(bool)
            hid = ndi.mean(under, lbl, range(1, nl + 1))
            quant = np.isin(lbl, [j + 1 for j, v in enumerate(sz) if v >= (GROUND_MIN * s) ** 2
                                  and nearo[j] < 0.5 and hid[j] < 0.7])
        if oid in PLAIN:
            plain = np.zeros((H, W), bool)
            for pts in PLAIN[oid]:
                plain |= poly_mask(pts)
            quant &= ~cv2.resize(plain[y0:y1, x0:x1].astype(np.uint8), size,
                                 interpolation=cv2.INTER_NEAREST).astype(bool)
        gid = f"{oid}-grad"
        step = max(1, GROUND_STOP)
        stops = "".join(f'<stop offset="{fmt(y / max(1, img.shape[0] - 1))}" stop-color="{hexc(rows[y])}"/>'
                        for y in list(range(0, img.shape[0], step)) + [img.shape[0] - 1])
        defs = (f'<linearGradient id="{gid}" x1="0" y1="{y0}" x2="0" y2="{y0 + img.shape[0] - 1}" '
                f'gradientUnits="userSpaceOnUse">{stops}</linearGradient>\n')
        base = f"url(#{gid})"
        k = GROUND_DETAIL_K
    if not quant.any():
        quant = inside if base is None else quant
    cents = td.kmeans_lab(lab[quant], k, min_dist=6.0) if quant.any() else np.zeros((1, 3))
    dist = ((lab[..., None, :] - cents[None, None]) ** 2).sum(-1)
    labels = dist.argmin(-1)
    g = cv2.GaussianBlur(lab[..., 0], (0, 0), 1.5)
    grad = np.hypot(cv2.Sobel(g, cv2.CV_32F, 1, 0), cv2.Sobel(g, cv2.CV_32F, 0, 1)) / s
    edgy = grad > EDGE_GRAD / 4
    trans = [j for j in range(len(cents))
             if ((labels == j) & quant).sum() and edgy[(labels == j) & quant].mean() > TRANSITION_FRAC]
    if base is None and trans and len(trans) < len(cents):
        dist[..., trans] = np.inf
        labels = dist.argmin(-1)
    labels = td.smooth_labels(labels, len(cents), quant, LABEL_SIGMA * s)
    upf = up.astype(np.float64)
    regs = []
    for j in range(len(cents)):
        mm = labels == j
        if mm.sum() > (SPECKLE * s) ** 2:
            regs.append((upf[mm].mean(0), mm))
    # colour groups: each colour joins the first (largest) group within CLUSTER_DE of it
    regs.sort(key=lambda r: -int(r[1].sum()))
    groups = []
    for col, mm in regs:
        lc = to_lab(np.array([[col]], np.uint8))[0, 0]
        for gr in groups:
            if np.linalg.norm(gr["lab"] - lc) < CLUSTER_DE:
                gr["items"].append((col, mm))
                break
        else:
            groups.append(dict(lab=lc, items=[(col, mm)], col=col))
    groups.sort(key=lambda gr: -sum(int(m.sum()) for _, m in gr["items"]))
    out, below, first, used = [], quant.copy(), True, {}
    if base is not None:
        ds = [td.to_original(d, tx, ty, x0, y0, s) for d, tx, ty in td.vtrace(inside, s)]
        out.append(("ground", [(base, " ".join(ds))]))
        used["ground"] = 1
        first = False
    for gr in groups:
        paths = []
        for col, mm in gr["items"]:
            shape = inside if first else below
            first = False
            ds = [td.to_original(d, tx, ty, x0, y0, s) for d, tx, ty in td.vtrace(shape, s)]
            below = below & ~mm
            if ds:
                paths.append((hexc(col), " ".join(ds)))
        if paths:
            name = colour_name(gr["col"])
            used[name] = used.get(name, 0) + 1
            out.append((name if used[name] == 1 else f"{name}-{used[name]}", paths))
    if oid == "house-yellow":
        # the foundation, front steps and path, hand-built over the trace
        groups, fdefs = face_groups(oid, STEPS)
        out += groups
        defs += fdefs
    return box, out, defs


# --------------------------------------------------------------------------
# Sky and sun: fitted to the original
# --------------------------------------------------------------------------
SUN_C = (1700, 92)          # sun disc centre and radius, measured on the original
SUN_R = 105
SUN_GLOW_R = 560            # glow fitted out to this radius
SKY_STEP = 24               # rows per sky gradient stop


@lru_cache(maxsize=1)
def sky_rows():
    """Per-row sky colour (median of the original's sky away from the sun), as [(y, rgb)]."""
    vis = visible_map()
    o = original().astype(np.float64)
    yy, xx = np.mgrid[0:H, 0:W]
    far = np.hypot(xx - SUN_C[0], yy - SUN_C[1]) > SUN_GLOW_R
    lab = to_lab(original())
    sky = (vis == -1) & far & ~dilate(vis == -2, 24) & (lab[..., 2] < -4)
    rows = []
    for y in range(0, SKY_BOTTOM, SKY_STEP):
        sel = sky[y:y + SKY_STEP]
        if sel.sum() > 200:
            rows.append((y + SKY_STEP / 2, np.median(o[y:y + SKY_STEP][sel], axis=0)))
    return rows


def sky_model():
    """Full-frame RGB of the fitted sky gradient (without the sun)."""
    ys = np.array([r[0] for r in sky_rows()])
    cs = np.array([r[1] for r in sky_rows()])
    col = np.stack([np.interp(np.arange(H), ys, cs[:, c]) for c in range(3)], -1)
    return np.broadcast_to(col[:, None, :], (H, W, 3))


@lru_cache(maxsize=1)
def sun_fit():
    """(disc colour, [(offset, opacity)]) of the glow: opacity per radius that best turns
    the fitted sky into the original's colour, with the disc colour as the glow colour."""
    vis = visible_map()
    o = original().astype(np.float64)
    sky = sky_model()
    yy, xx = np.mgrid[0:H, 0:W]
    rr = np.hypot(xx - SUN_C[0], yy - SUN_C[1])
    disc = rr < SUN_R - 6
    core = np.median(o[disc], axis=0)
    stops = []
    step = 16
    for r0 in range(SUN_R + 4, SUN_GLOW_R, step):
        sel = (rr >= r0) & (rr < r0 + step) & (vis == -1)
        if sel.sum() < 50:
            continue
        g, s = o[sel], sky[sel]
        d = core - s
        a = float(((g - s) * d).sum() / max((d * d).sum(), 1e-6))
        stops.append((r0 + step / 2, min(max(a, 0.0), 1.0)))
    # monotone falloff, ending at zero
    out, last = [], 1.0
    for r, a in stops:
        last = min(last, a)
        out.append((r / SUN_GLOW_R, round(last, 3)))
    out.append((1.0, 0.0))
    return hexc(core), out


def sky_svg(layer_svg, lin_grad):
    stops = [(r[0] / H, hexc(r[1])) for r in sky_rows()]
    stops = [(0.0, stops[0][1])] + stops + [(1.0, stops[-1][1])]
    defs = lin_grad("sky-grad", stops, 0, 0, 0, H, "userSpaceOnUse")
    return layer_svg("sky", f'<rect x="0" y="0" width="{W}" height="{H}" fill="url(#sky-grad)"/>', defs)


def sun_svg(layer_svg, group):
    core, stops = sun_fit()
    cx, cy = SUN_C
    st = "".join(f'<stop offset="{fmt(o)}" stop-color="{core}" stop-opacity="{fmt(a)}"/>' for o, a in
                 [(SUN_R / SUN_GLOW_R, stops[0][1])] + stops)
    defs = (f'<radialGradient id="sun-glow-grad" cx="{cx}" cy="{cy}" r="{SUN_GLOW_R}" '
            f'gradientUnits="userSpaceOnUse">{st}</radialGradient>\n')
    body = (group("sun-glow", f'<circle cx="{cx}" cy="{cy}" r="{SUN_GLOW_R}" fill="url(#sun-glow-grad)"/>')
            + group("sun-disc", f'<circle cx="{cx}" cy="{cy}" r="{SUN_R}" fill="{core}"/>'))
    return layer_svg("sun", body, defs)


# --------------------------------------------------------------------------
# Layer documents
# --------------------------------------------------------------------------
def object_body(oid):
    import build_scene as bs
    _, groups, _ = trace(oid)
    inner = "".join(bs.group(f"{oid}-{name}", "".join(f'<path d="{p[1]}" fill="{p[0]}"{p[2] if len(p) > 2 else ""}/>'
                                                      for p in paths),
                             label=f"{oid}-{name}") for name, paths in groups)
    return bs.group(oid, inner)


def object_defs(oid):
    return trace(oid)[2]


def env_layers():
    """{layer name: SVG} for every environment layer (sky and sun included)."""
    import build_scene as bs
    out = {"sky": sky_svg(bs.layer_svg, bs.lin_grad), "sun": sun_svg(bs.layer_svg, bs.group)}
    for layer in TRACED_LAYERS:
        objs = [o for o in ORDER if OBJ[o]["layer"] == layer]
        out[layer] = bs.layer_svg(layer, "".join(object_body(o) for o in objs),
                                  "".join(object_defs(o) for o in objs))
    return out


# Objects exported as tight standalone crops (objects/<id>.svg + .png).
STANDALONE = ["house-red", "house-yellow", "house-teal", "house-pink", "house-blue", "house-green",
              "mailbox-left", "mailbox-right", "mailbox-far-left-1", "mailbox-far-left-2", "mailbox-far-left-3",
              "mailbox-far-right-1", "mailbox-far-right-2", "mailbox-far-right-3", "mailbox-far-right-4",
              "toy-bone-orange", "toy-bone-blue", "toy-bowl", "toy-bone-red",
              "tree-near-left", "tree-near-yellow-house", "tree-far-left-1", "tree-far-right-4",
              "tree-far-right-5", "bush-front-left", "bush-tree-blue-house", "bush-blue-house",
              "bush-green-house", "fence-yellow-house", "fence-blue-house-left", "fence-blue-house-right",
              "fence-green-house", "cloud-top-left", "cloud-top-mid", "cloud-top-right",
              "cloud-behind-title-left", "cloud-behind-title-right"]


def standalone(oid):
    """(svg, render width, box) of one object alone, viewBox cropped tight around it."""
    import build_scene as bs
    x0, y0, x1, y1 = object_box(oid, OBJECT_PAD)
    svg = bs.layer_svg(OBJ[oid]["layer"], object_body(oid), object_defs(oid))
    w, h = x1 - x0, y1 - y0
    svg = re.sub(r'width="\d+" height="\d+" viewBox="0 0 \d+ \d+"',
                 f'width="{w}" height="{h}" viewBox="{x0} {y0} {w} {h}"', svg, count=1)
    width = OBJECT_LONG if w >= h else int(-(-OBJECT_LONG * w // h))
    return svg, width, (x0, y0, x1, y1)


def sun_standalone():
    import build_scene as bs
    r = SUN_GLOW_R
    x0, y0 = SUN_C[0] - r, SUN_C[1] - r
    svg = sun_svg(bs.layer_svg, bs.group)
    svg = re.sub(r'width="\d+" height="\d+" viewBox="0 0 \d+ \d+"',
                 f'width="{2 * r}" height="{2 * r}" viewBox="{x0} {y0} {2 * r} {2 * r}"', svg, count=1)
    return svg, OBJECT_LONG


def clear_caches():
    for f in (original, hand_layer, rough, occluders, visible_map, regions, trace, sky_rows, sun_fit, backdrop):
        f.cache_clear()
