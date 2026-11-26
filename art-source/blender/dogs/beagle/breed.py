"""Beagle: the breed definition for the shared dog kit (../kit/).

Data only: numbers, strings, tuples, lists and dicts. No imports and no Blender calls; the kit
checks that when it loads this file (kit/dh_breed.py). Started from the approved Labrador's
breed.py (../golden-retriever/breed.py documents every field); the values that make a Beagle a
Beagle are marked "Beagle:".

What changes from the Lab:
  - size: a smaller dog. The body is mapped down from the Lab's (lengths x0.75, widths x0.84,
    depths x0.80 about a torso centre lowered from 0.80 m to 0.52 m) and the legs are shortened
    more than the body (x0.62 above the paws), so the legs are short for the body; the head
    group is scaled x0.84, so the head is big for the body (the family look);
  - head: a broad, domed skull, a moderate stop and a medium, square muzzle with a little flew;
    bigger eyes (relative to the head) set a touch wider;
  - ears: long, low-set, hanging close to the cheeks, wide, with rounded tips (longer and wider
    than the Lab's, relative to the head);
  - tail: medium length, carried up (the "flag"): it leaves the top of the croup at about 50 deg
    and curves up to about 84 deg, with a white tip;
  - coat: short, dense and smooth (the Lab's FUR_STRAND); Default = black, tan and white
    tricolour, Light = lemon and white, both laid out by FUR_PATCHES, with white markings from
    the kit's optional "pale" patch op.
Kept: the mouth (lips wrap the corners), the one continuous skin, the rig, the collar and a solved
sit that rests on the ground.

Conventions: metres, Blender axes (front -Y, up +Z, the dog's left +X), authored coordinates
(before the OY shift that puts the origin mid-span). Head-group points (head, jaw, ears, eyes,
nose, tongue) are authored before the HEAD_DY move-back; the kit adds HEAD_DY.

Build it:  python3 art-source/blender/dogs/kit/build_breed.py beagle
"""

# ----------------------------------------------------------------------------- names
NAME = "dog_beagle"                       # object and file name stem (dog_beagle_body, dog_beagle_default.gltf)
DISPLAY_NAME = "Beagle"
STATS_NAME = "Beagle (adult) concept, round 1"
MOTION_STATUS = "Beagle motion clips, first pass (concept)"
MATERIAL_NAMES = ["DH_Dog_Fur_%s", "DH_Dog_Gloss", "DH_Dog_Soft"]   # fur (per coat), gloss, soft
# Beagle: a short, close coat with a soft sheen, between the Lab's (0.60, 0.42) and the
# Shepherd's (0.70, 0.34)
MATERIAL_SHADING = {"fur": (0.64, 0.38), "gloss": (0.30, 0.35), "soft": (0.55, 0.35)}

# ----------------------------------------------------------------------------- budgets
BUDGET = {"full": 4000, "lod1": 1500}     # triangles per file
SKIN_FACES = 1490                         # QuadriFlow target (quads) for the body. Beagle: QuadriFlow overshoots unevenly and drops a thin tail (README); 1490 keeps the tail within budget
LOD_SKIN_TRIS = 820                       # the LOD body: the full retopology decimated to this

# ----------------------------------------------------------------------------- placement
HEAD_DY = 0.0294               # Beagle: the Lab's 0.035 x the head scale 0.84
OY = 0.09                      # origin under the middle of the nose-to-rump span
JAW_REST_OPEN_DEG = 12.0       # the file's default pose opens the jaw this much

# ----------------------------------------------------------------------------- body shape
FIELD_GRID = {"h": 0.010, "origin": (-0.36, -0.76, -0.02), "extent": (0.72, 1.44, 1.24), "voxel": 0.0068}

# torso + neck: (y, z, half width, top, bottom, exponent)
# Beagle: the Lab's sturdy torso mapped down (y x0.75, width x0.84, depth x0.80, centre 0.52 m):
# a level topline at about 0.70 m, a deep chest reaching about to the elbows, a little tuck-up;
# a short, strong neck.
TORSO = [
    (0.4688, 0.552, 0.063, 0.052, 0.060, 2.0),
    (0.4575, 0.552, 0.1596, 0.128, 0.152, 2.2),
    (0.4200, 0.544, 0.2083, 0.164, 0.168, 2.5),
    (0.3675, 0.536, 0.2251, 0.172, 0.164, 2.6),
    (0.3150, 0.528, 0.2251, 0.176, 0.156, 2.6),    # hip loop
    (0.2325, 0.528, 0.2285, 0.172, 0.156, 2.6),
    (0.1425, 0.520, 0.2369, 0.172, 0.172, 2.6),    # loin, a little tuck-up
    (0.0450, 0.520, 0.2436, 0.176, 0.2144, 2.6),
    (-0.0450, 0.520, 0.2453, 0.180, 0.2496, 2.6),  # ribcage, deep
    (-0.1125, 0.528, 0.2394, 0.180, 0.2576, 2.6),  # shoulder loop
    (-0.1800, 0.552, 0.2251, 0.176, 0.2496, 2.5),
    (-0.2213, 0.592, 0.2050, 0.168, 0.2208, 2.4),  # front of chest
    (-0.2475, 0.648, 0.1966, 0.160, 0.1936, 2.3),  # neck base
    (-0.2700, 0.6989, 0.1848, 0.148, 0.164, 2.2),  # collar loop
    (-0.2812, 0.7497, 0.1680, 0.132, 0.140, 2.1),
    (-0.2888, 0.8016, 0.1462, 0.116, 0.120, 2.0),  # neck top, inside the skull
]

# head with the lower jaw: (y, z, half width, top, bottom, exponent)
# Beagle: skull 0.53 m across and domed (taller crown, rounder section than the Lab's); a
# moderate stop; a medium muzzle, square and deep with a little flew, a little longer than the
# Lab's relative to the skull.
HEAD = [
    (-0.1292, 0.9150, 0.063, 0.0588, 0.0588, 2.0),
    (-0.1544, 0.9234, 0.168, 0.1512, 0.1428, 2.1),
    (-0.2047, 0.9293, 0.2436, 0.2016, 0.1806, 2.15),
    (-0.2762, 0.9318, 0.2671, 0.2251, 0.2058, 2.15),
    (-0.3518, 0.9276, 0.2621, 0.2167, 0.2142, 2.2),
    (-0.4148, 0.9133, 0.2352, 0.1848, 0.2201, 2.3),
    (-0.4626, 0.8873, 0.1915, 0.1260, 0.2201, 2.45),   # the stop
    (-0.5046, 0.8604, 0.1680, 0.0907, 0.2117, 2.6),
    (-0.5634, 0.8503, 0.1613, 0.0840, 0.2016, 2.7),    # muzzle: square, with flews
    (-0.6222, 0.8453, 0.1478, 0.0806, 0.1814, 2.7),
    (-0.6642, 0.8419, 0.1075, 0.0622, 0.1277, 2.4),
    (-0.6835, 0.8419, 0.0487, 0.0336, 0.0554, 2.0),
]

# tail: (y, z, half width, top, bottom)
# Beagle: medium length (0.44 m), carried up: it leaves the top of the croup at about 50 deg and
# curves up to about 84 deg; thick at the root, tapering to a brush tip. ("top" is the side facing
# back: the brush.)
TAIL = [
    (0.4180, 0.6550, 0.066, 0.068, 0.066),
    (0.4563, 0.7048, 0.064, 0.070, 0.062),
    (0.4903, 0.7577, 0.060, 0.068, 0.057),
    (0.5197, 0.8133, 0.056, 0.064, 0.053),
    (0.5442, 0.8711, 0.051, 0.059, 0.048),
    (0.5638, 0.9309, 0.045, 0.052, 0.042),
    (0.5782, 0.9920, 0.035, 0.040, 0.033),
    (0.5875, 1.0542, 0.018, 0.020, 0.017),
]

# legs: the Lab's legs, shortened x0.62 above the paws (fore/aft offsets x0.72 about each leg's
# top joint), widths x0.84, paws x0.85. Beagle: short, straight, sturdy legs.
FRONT_X = 0.1445
BACK_X = 0.1562
BACK_THIGH_DX = 0.021
FRONT_UPPER = [(-0.1140, 0.6160, 0.0806, 0.0890, 0.0974), (-0.1464, 0.5014, 0.0832, 0.0941, 0.0899),
               (-0.1248, 0.4081, 0.0815, 0.0806, 0.0848), (-0.0960, 0.3336, 0.0773, 0.0764, 0.0848)]
FRONT_FORE = [(-0.0910, 0.3522, 0.0773, 0.0764, 0.0848), (-0.0982, 0.2714, 0.0722, 0.0706, 0.0722),
              (-0.1054, 0.1906, 0.0672, 0.0655, 0.0638), (-0.1104, 0.1347, 0.0638, 0.0613, 0.0596)]
FRONT_PASTERN = [(-0.1104, 0.1409, 0.0630, 0.0596, 0.0580), (-0.1154, 0.1099, 0.0605, 0.0571, 0.0554),
                 (-0.1248, 0.0850, 0.0613, 0.0580, 0.0546)]
FRONT_PAW = (-0.0744, -0.2436)        # heel y, toe y
BACK_THIGH = [(0.3517, 0.5680, 0.1134, 0.1092, 0.1344), (0.3337, 0.4889, 0.1260, 0.1176, 0.1638),
              (0.3014, 0.4081, 0.1016, 0.0949, 0.1411), (0.2725, 0.3460, 0.0815, 0.0773, 0.1025),
              (0.2617, 0.3149, 0.0722, 0.0680, 0.0764)]
BACK_GASKIN = [(0.2653, 0.3274, 0.0706, 0.0680, 0.0806), (0.3049, 0.2714, 0.0638, 0.0554, 0.0806),
               (0.3446, 0.2155, 0.0563, 0.0479, 0.0630), (0.3697, 0.1813, 0.0521, 0.0454, 0.0588)]
BACK_META = [(0.3712, 0.1906, 0.0512, 0.0454, 0.0571), (0.3640, 0.1347, 0.0521, 0.0470, 0.0504),
             (0.3553, 0.0850, 0.0546, 0.0496, 0.0479)]
BACK_PAW = (0.3770, 0.2041)           # heel y, toe y
PAW = {"z": 0.0544, "half_width": 0.084, "top": 0.0544, "bottom": 0.0544, "e": 2.6, "rings": 7}

LOFT_STYLE = {"torso_dome": 0.35, "head_dome": 0.5, "tail": (2.0, 0.7), "front_leg": (2.2, 0.3),
              "back_thigh": (2.3, 0.3), "back_leg": (2.2, 0.3), "ear": (2.0, 0.9)}

# Beagle: the Lab's fillets x0.8 (a smaller dog), and a wider one where the upright tail leaves
# the croup
FIELD = [
    ("core", 0.0, [("torso", 0.0), ("head", 0.036), ("tail", 0.05)]),
    ("front_L", 0.048, [("front_upper_L", 0.0), ("front_fore_L", 0.028), ("front_pastern_L", 0.02), ("front_paw_L", 0.024)]),
    ("front_R", 0.048, [("front_upper_R", 0.0), ("front_fore_R", 0.028), ("front_pastern_R", 0.02), ("front_paw_R", 0.024)]),
    ("back_L", 0.04, [("back_thigh_L", 0.0), ("back_gaskin_L", 0.032), ("back_meta_L", 0.024), ("back_paw_L", 0.024)]),
    ("back_R", 0.04, [("back_thigh_R", 0.0), ("back_gaskin_R", 0.032), ("back_meta_R", 0.024), ("back_paw_R", 0.024)]),
]

# ----------------------------------------------------------------------------- muzzle and mouth
# Beagle: the Golden's mouth, head-scaled, on the medium muzzle, with a slightly deeper lip drop
HINGE = (-0.4106, 0.7344)
LIP_RUN = 0.2621
LIP_DROP = 0.0504
LIP_BAND = 0.0109
MOUTH_MAX_OPEN_DEG = 34.0
MOUTH_ZONE = {"below_hinge": 0.1848, "behind_hinge": 0.084}
JAW_TIP = (-0.6079, 0.6924)

TONGUE = {
    "pivot": (-0.4399, 0.7428),
    "rings": [(-0.4399, 0.0101, 0.0521, 0.0134), (-0.4996, 0.0134, 0.0706, 0.0168), (-0.5718, 0.0151, 0.0739, 0.0176),
              (-0.6374, 0.0134, 0.0706, 0.0160), (-0.6760, -0.0101, 0.0588, 0.0134), (-0.6928, -0.0437, 0.0437, 0.0118)],
    "lod_rings": (0, 2, 4, 5),
    "e": 2.6,
}

# ----------------------------------------------------------------------------- ears, eyes, nose, collar
# Beagle: long, low-set hanging ears - rooted at about eye level, 0.37 m long, 0.27 m wide,
# hanging close to the cheeks below the jaw line, with a round tip
EARS = {
    "rings": [(0.2160, -0.3160, 1.0074, 0.0605, 0.0168), (0.2540, -0.3380, 0.9654, 0.1025, 0.0218),
              (0.2770, -0.3560, 0.8772, 0.1277, 0.0235), (0.2850, -0.3700, 0.7764, 0.1344, 0.0235),
              (0.2839, -0.3803, 0.6882, 0.1159, 0.0218), (0.2705, -0.3853, 0.6336, 0.0622, 0.0168)],
    "lod_rings": (0, 1, 3, 4, 5),
    "bones": [(0.2180, -0.3180, 1.0074), (0.2810, -0.3630, 0.8268), (0.2705, -0.3853, 0.6336)],
}
# Beagle: big soft eyes (1.14x the Lab's relative to the head), set a touch wider
EYES = {"ray_x": 0.1411, "ray_z": 0.9679, "face_bias": 0.6, "inset": 0.007, "radii": (0.0538, 0.0319, 0.0613),
        "shine_offset": (0.0252, 0.0269, 0.0151), "shine_radii": (0.0118, 0.0101, 0.0118)}
NOSE = {"ray_z": 0.8722, "offset": (0.0101, 0.0050), "length": 0.0336, "radii": (0.0790, 0.0487, 0.0370), "e": 2.5}
COLLAR = {"ring": 13, "gap": 0.012, "gap_lod": 0.019, "strap": (0.030, 0.011), "e": 3.0,
          "tag_radii": (0.034, 0.007), "tag_out": 0.017, "tag_drop": 0.036}

# ----------------------------------------------------------------------------- skeleton
SPINE = {
    "hips": ((0.315, 0.52), (0.45, 0.552)),
    "spine_1": ((0.315, 0.52), (0.0975, 0.52)),
    "spine_2": ((0.0975, 0.52), (-0.1275, 0.536)),
    "spine_3": ((-0.1275, 0.536), (-0.2513, 0.648)),
    "neck": ((-0.2513, 0.648), (-0.2762, 0.7596)),
    "head": ((-0.2762, 0.7596), (-0.4861, 0.8856)),
}
# Beagle: the tail bones follow the upright tail
TAIL_BONES = [(0.418, 0.655), (0.4903, 0.7577), (0.5442, 0.8711), (0.5782, 0.992), (0.5875, 1.0542)]
JOINTS = {
    "front_shoulder": (-0.1500, 0.5200), "front_elbow": (-0.0938, 0.3336), "front_wrist": (-0.1111, 0.1285),
    "front_paw": (-0.1284, 0.0638), "front_toe": (-0.2328, 0.0382),
    "back_hip": (0.3337, 0.5200), "back_stifle": (0.2653, 0.3211), "back_hock": (0.3733, 0.1813),
    "back_foot": (0.3532, 0.0723), "back_toe": (0.2185, 0.0382),
}
TORSO_CHAIN = [(0.45, 0.552), (0.315, 0.52), (0.0975, 0.52), (-0.1275, 0.536), (-0.2513, 0.648), (-0.2513, 0.7596)]

# ----------------------------------------------------------------------------- sit pose
SIT = {"hips": -43.0212, "drop": -0.317, "shoulder": -8.0, "stifle_z": 0.08, "hock_z": 0.0208, "foot_pitch": 26.9228, "tail": 179.4375}
SIT_POSE = {"spine_3": 4, "neck": 28, "head": 24, "tail_2": 12, "tail_3": 6}

# ----------------------------------------------------------------------------- checks
SANITY = {"head_min_z": 0.52, "tail_min_y": 0.33}
# Beagle: the back is measured behind the skull and ahead of the upright tail
MEASURE = {"rump_min_abs_x": 0.085, "back_y": (0.05, 0.40)}

# ----------------------------------------------------------------------------- coat
# sRGB hex: main coat, "feathering" (here the white: chest, belly, legs, muzzle, blaze, tail tip),
# dark channel (with FUR_PATCHES the saddle and the top of the tail). The keys are the game's coat
# slots: Default = black, tan and white tricolour; Light = lemon and white (the "dark" channel is
# a slightly deeper lemon, so the saddle reads as a soft lemon patch with no black).
COATS = {
    "default": ("#B5733A", "#F2EDE3", "#1F1A17"),
    "light":   ("#E3C68E", "#F4F0E7", "#D9B677"),
}
COAT_LABELS = {"default": "Default (tricolour)", "light": "Light (lemon and white)"}
FUR_FLOW = {
    "body": ((0.0, 1.0, -0.25), ("body", "head")),
    "leg": ((0.0, 0.15, -1.0), ("fleg", "bleg", "thigh", "paw")),
    "tail": ((0.0, 0.25, 1.0), ("tail",)),
    "ear": ((0.0, -0.15, -1.0), ("ear",)),
}
FUR_MARKINGS = {"bib_y": (-0.09, -0.195), "rump_y": (0.375, 0.45), "muzzle_y": (-0.50, -0.56),
                "crown_y": (-0.32, -0.23), "pale_z": (0.068, 0.18), "tail_tip_y": (0.48, 0.56),
                "ear_tip_z": (0.70, 0.64)}
# Beagle: the Lab's short, dense, smooth coat
FUR_STRAND = {"fine": (0.07, 0.30), "mid": (0.16, 0.45), "clump": (0.40, 0.7), "low": (5.0, 7.0),
              "mix": (0.60, 0.35, 0.10), "normal": 0.16, "contrast": 0.20}
# Beagle: the tricolour pattern. Dark patches lay the saddle over the coat's dark channel; "pale"
# patches (kit, optional) raise the white channel and clear the dark one (white wins).
FUR_PATCHES = [
    # tan head and ears: clear the kit's darker crown and dark ears
    {"regions": ("head", "ear"), "op": "light", "strength": 1.0},
    # the saddle: the back from the withers to the tail, down the flanks to about mid-ribs
    {"regions": ("body",), "line": [(-0.20, 0.78), (-0.15, 0.64), (-0.09, 0.56), (0.02, 0.52), (0.18, 0.51),
                                    (0.30, 0.53), (0.40, 0.56), (0.47, 0.60)],
     "soft": 0.025, "wobble": 0.025},
    # the tail: dark above the root, white tip below
    {"regions": ("tail",), "z": (0.66, 0.72)},
    {"regions": ("tail",), "op": "pale", "z": (0.93, 0.97)},
    # white: the muzzle, a blaze up the forehead, the throat and chest, the legs and feet
    {"regions": ("head",), "op": "pale", "y": (-0.505, -0.545), "soft": 0.02},
    {"regions": ("head",), "op": "pale", "x_max": 0.035, "y": (-0.20, -0.27), "soft": 0.015},
    {"regions": ("head",), "op": "pale", "n": ("z", -0.25, -0.55)},
    {"regions": ("body",), "op": "pale", "n": ("y", -0.35, -0.75), "y": (-0.14, -0.20)},
    {"regions": ("fleg", "paw"), "op": "pale", "z": (0.42, 0.36)},
    {"regions": ("bleg", "paw"), "op": "pale", "z": (0.27, 0.21)},
]
TRIM_COLOURS = {"eye": "#1C1411", "iris": "#5A341E", "pupil": "#140E0B", "shine": "#FFFFFF", "nose": "#24201F",
                "tag": "#D6AC45", "mouth": "#5E2B2C", "lip": "#2C2120", "tongue": "#E7848A",
                "collar": "#C3282E", "stitch": "#E8B0A8"}

# ----------------------------------------------------------------------------- motion
# Beagle: shorter legs take shorter, quicker strides. Strides are 0.66x the Lab's (legs 0.62x
# above the paw), cycles a little shorter; lifts and stance offsets scaled with the legs.
GAITS = {
    "Walk": {"frames": 26, "speed": 0.61,
             "phase": {"HL": 0.0, "FL": 0.22, "HR": 0.5, "FR": 0.72},
             "duty": {"HL": 0.62, "HR": 0.62, "FL": 0.64, "FR": 0.64},
             "lift": {"F": 0.055, "H": 0.045}, "roll": {"F": (28.0, 0.22), "H": (24.0, 0.22)},
             "curl": {"F": 40.0, "H": 18.0}, "fold": {"F": 30.0, "H": -14.0},
             "center": {"F": -0.015, "H": 0.0}, "win": 1, "sigma": 1.2, "k": {"F": 0.85, "H": 0.45},
             "notes": "four-beat lateral-sequence walk; touchdowns LH 0.00, LF 0.22, RH 0.50, RF 0.72 "
                      "of the cycle; duty 0.62 hind / 0.64 fore"},
    "Run": {"frames": 14, "speed": 2.64,
            "phase": {"HR": 0.0, "HL": 0.11, "FR": 0.40, "FL": 0.50},
            "duty": {"HR": 0.30, "HL": 0.30, "FR": 0.30, "FL": 0.30},
            "lift": {"F": 0.11, "H": 0.085}, "roll": {"F": (35.0, 0.3), "H": (30.0, 0.3)},
            "curl": {"F": 75.0, "H": 30.0}, "fold": {"F": 55.0, "H": -25.0},
            "center": {"F": -0.04, "H": -0.065}, "suspension": (0.025, 0.90, 0.30), "win": 1, "sigma": 1.0,
            "k": {"F": 0.9, "H": 0.6}, "land": 0.985, "reach": {"F": 0.965, "H": 0.95},
            "notes": "transverse gallop, right-hind lead: touchdowns RH 0.00, LH 0.11, RF 0.40, LF 0.50; "
                     "duty 0.30; gathered suspension 0.80-1.00"},
}
# Beagle: rock_radius is the hip-to-shoulder span (0.48 m); steps scaled with the legs
SIT_DOWN = {"rock_radius": 0.465, "dip": 0.018, "hind_step_lift": 0.025, "front_step_lift": 0.036}
PLAY_BOW = {"elbow_z": 0.098, "wrist_z": 0.081}

# ----------------------------------------------------------------------------- review renders
RENDER = {
    "head_box": ((-0.32, -0.63, 0.60), (0.32, -0.02, 1.22)),
    "mouth_box": ((-0.17, -0.63, 0.64), (0.17, -0.36, 0.84)),
    "hips_box": {"rest": ((-0.33, 0.05, 0.0), (0.33, 0.72, 1.16)), "sit": ((-0.33, -0.10, 0.0), (0.33, 1.00, 0.66))},
    "body_box": ((-0.34, -0.72, 0.0), (0.34, 0.72, 1.22)),
    "chest_box": ((-0.35, -0.66, 0.0), (0.35, 0.22, 1.22)),
    "chest_cut_y": -0.06,
    "join_views": [("Shoulder", "front_shoulder_L", (0.015, 0.04, -0.09), (0.19, 0.23, 0.27), -72, 6),
                   ("Hip / haunch", "back_hip_L", (0.015, 0.02, -0.12), (0.19, 0.25, 0.29), -118, 8),
                   ("Tail base", "tail_1", (0.0, 0.02, 0.0), (0.17, 0.20, 0.22), -150, 24)],
    "sit_closeups": {"hind": ("back_hock_L", 0.45, 0.42), "front": ("front_paw_L", 0.34, 0.30)},
    "before": {},
    "captions": {
        "turnaround": ("Beagle - turnaround (orthographic)",
                       "Round 1 on the shared kit: a smaller hound with short legs, a broad domed skull, square "
                       "muzzle, long low-set ears and an upright white-tipped tail. Tricolour (Default)."),
        "gamecam": ("Beagle at the game camera",
                    "Orthographic, 45 deg pitch, yaw -40. Approved base 1 (level 1), mailbox and hydrant for scale."),
        "face": ("Beagle - face, 3/4",
                 "Broad domed skull, moderate stop, square muzzle with a white blaze; big soft eyes; long "
                 "hanging ears; the Golden's mouth; jaw open 12 deg."),
        "coats": ("Coats and puppy",
                  "Default (black, tan and white tricolour) and Light (lemon and white). One mesh: a coat is a fur "
                  "colour map; normal map shared. Puppy = adult x 0.55."),
        "splash": ("3D concept vs splash", "No Beagle splash art yet."),
        "mouth": ("Mouth", "The Golden's mouth on the medium square muzzle: lips meet at the corners on the hinge."),
        "mouth_rows": ("Open (default pose, jaw 12 deg)", "Closed (jaw bone at 0, tongue hidden)"),
        "hips": ("Hip study - hind leg (skinned), sit",
                 "Red dots = joints: hips, hip, stifle, hock, foot (both legs), tail base."),
        "hips_notes": ["The Lab's hind-leg build on shorter legs: thigh into a rounded haunch, stifle, lower "
                       "thigh back to the hock,",
                       "and a short, near-vertical rear foot. The upright tail leaves the top of the croup.",
                       "Sit: the hips drop and tilt so the rump rests on the ground; hock and rear foot lie flat, "
                       "front paws stay planted.",
                       "Contact is measured on the skinned mesh and checked at export (see sit.png)."],
        "joins": ("Joins - shoulder, hip / haunch, tail base",
                  "One skin: the volumes blend with a smooth minimum, then retopologise."),
        "joins_rows": ("Rest", "Bent (sit): the junctions stay smooth"),
        "sit": ("Sit - ground contact",
                "Side views from a level camera at ground height; the dark line is the ground plane."),
        "chest": ("Chest", "Orthographic: front, front section, side, 3/4."),
        "chest_row": "Beagle: a deep, compact chest on short legs",
    },
}
