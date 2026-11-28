"""French Bulldog: the breed definition for the shared dog kit (../kit/).

Data only: numbers, strings, tuples, lists and dicts. No imports and no Blender calls; the kit
checks that when it loads this file (kit/dh_breed.py). Started from the Beagle round 1 (a smaller
dog on the Lab's build; ../golden-retriever/breed.py documents every field); the values that make
a Frenchie a Frenchie are marked "Frenchie:".

What changes from the Beagle:
  - size: smaller again. The body and legs are mapped down from the Beagle's (lengths x0.80 about
    y 0.09, heights x0.80 about a torso centre lowered from 0.52 m to 0.42 m, the rear raised up to
    2 cm, widths kept, so the body is broad for its size); the front legs are scaled to the
    shoulder (x0.80) and the hind legs a touch longer (x0.84), so the rump sits a little high;
  - head: big, broad and square, with a flat skull, a very short muzzle under a deep stop, a soft
    nose roll above the muzzle, a broad square jaw and a wide black nose; big round eyes set low
    and wide;
  - ears: large erect bat ears, broad at the base with wide rounded tips, set high and wide and
    opening forward (EARS["side"], EARS["swing"], as the Shepherd's);
  - body: compact and stocky, a broad deep chest, a short back with a slight rise to the loin and
    a tucked belly; wide-set front legs;
  - tail: a very short stub, set low and carried down;
  - coat: short, smooth and fine (the Lab's FUR_STRAND); Default = fawn with a dark mask and dark
    ears (FUR_PATCHES).
Kept: the mouth (lips wrap the corners), the one continuous skin, the rig, the collar and a solved
sit that rests on the ground.

Conventions: metres, Blender axes (front -Y, up +Z, the dog's left +X), authored coordinates
(before the OY shift that puts the origin mid-span). Head-group points (head, jaw, ears, eyes,
nose, tongue) are authored before the HEAD_DY move-back; the kit adds HEAD_DY.

Build it:  python3 art-source/blender/dogs/kit/build_breed.py french-bulldog
"""

# ----------------------------------------------------------------------------- names
NAME = "dog_frenchie"                     # object and file name stem (dog_frenchie_body, dog_frenchie_default.gltf)
DISPLAY_NAME = "French Bulldog"
STATS_NAME = "French Bulldog (adult) concept, round 1"
MOTION_STATUS = "French Bulldog motion clips, first pass (concept)"
MATERIAL_NAMES = ["DH_Dog_Fur_%s", "DH_Dog_Gloss", "DH_Dog_Soft"]   # fur (per coat), gloss, soft
# Frenchie: a short, fine, close coat with a soft sheen, near the Lab's (0.60, 0.42)
MATERIAL_SHADING = {"fur": (0.62, 0.40), "gloss": (0.30, 0.35), "soft": (0.55, 0.35)}

# ----------------------------------------------------------------------------- budgets
BUDGET = {"full": 4000, "lod1": 1500}     # triangles per file
SKIN_FACES = 1400                         # QuadriFlow target (quads) for the body. Frenchie: QuadriFlow overshoots unevenly (1440 gave 4,004 tris, over budget); 1400 gives 3,688 (README)
LOD_SKIN_TRIS = 820                       # the LOD body: the full retopology decimated to this

# ----------------------------------------------------------------------------- placement
HEAD_DY = 0.025                # head group moved back by this after building
OY = 0.024                     # origin under the middle of the nose-to-rump span
JAW_REST_OPEN_DEG = 12.0       # the file's default pose opens the jaw this much

# ----------------------------------------------------------------------------- body shape
FIELD_GRID = {"h": 0.010, "origin": (-0.34, -0.52, -0.02), "extent": (0.68, 1.02, 1.02), "voxel": 0.0068}

# torso + neck: (y, z, half width, top, bottom, exponent)
# Frenchie: the Beagle's torso mapped down (y x0.80, depth x0.80 top / x0.88 bottom, centre
# 0.42 m, widths kept): a short back that rises a little to the loin and croup (0.57 m at the
# withers, 0.59 m at the croup), a broad, deep chest, a tucked belly; a short, thick neck.
TORSO = [
    (0.393, 0.4656, 0.063, 0.0416, 0.0528, 2.0),
    (0.384, 0.4656, 0.1596, 0.1024, 0.1338, 2.2),
    (0.354, 0.4592, 0.2083, 0.1312, 0.1478, 2.5),
    (0.312, 0.4506, 0.2251, 0.1376, 0.1443, 2.6),
    (0.270, 0.4418, 0.2251, 0.1408, 0.1373, 2.6),    # hip loop
    (0.204, 0.4381, 0.2285, 0.1376, 0.1373, 2.6),
    (0.132, 0.4275, 0.2369, 0.1376, 0.1514, 2.6),    # loin, tucked up
    (0.054, 0.4231, 0.2436, 0.1408, 0.2050, 2.6),
    (-0.018, 0.4200, 0.2480, 0.1440, 0.2400, 2.6),   # ribcage, deep
    (-0.072, 0.4264, 0.2470, 0.1440, 0.2450, 2.6),   # shoulder loop
    (-0.126, 0.4456, 0.2360, 0.1408, 0.2350, 2.5),
    (-0.159, 0.4776, 0.2280, 0.1344, 0.2050, 2.4),   # front of chest
    (-0.180, 0.5224, 0.2084, 0.1280, 0.1704, 2.3),   # neck base
    (-0.198, 0.5631, 0.1959, 0.1184, 0.1443, 2.2),   # collar loop
    (-0.207, 0.6038, 0.1781, 0.1056, 0.1232, 2.1),
    (-0.213, 0.6453, 0.1550, 0.0928, 0.1056, 2.0),   # neck top, inside the skull
]

# head with the lower jaw: (y, z, half width, top, bottom, exponent)
# Frenchie: a big, square head - skull 0.51 m across and flat on top (high exponents); the
# forehead drops steeply at a deep stop onto a very short, broad muzzle (8 cm in front of the
# stop) with a slight rise just ahead of the stop (the nose roll) and a broad, square jaw.
HEAD = [
    (-0.090, 0.735, 0.060, 0.055, 0.050, 2.0),
    (-0.115, 0.735, 0.170, 0.165, 0.140, 2.2),
    (-0.160, 0.735, 0.235, 0.210, 0.170, 2.4),
    (-0.220, 0.735, 0.255, 0.220, 0.180, 2.6),
    (-0.280, 0.732, 0.255, 0.212, 0.195, 2.7),
    (-0.330, 0.728, 0.240, 0.190, 0.205, 2.8),
    (-0.365, 0.722, 0.215, 0.085, 0.200, 3.0),    # the stop: deep
    (-0.395, 0.720, 0.205, 0.088, 0.200, 3.0),    # the nose roll
    (-0.425, 0.718, 0.190, 0.070, 0.190, 2.9),    # muzzle: very short, broad, square
    (-0.450, 0.716, 0.150, 0.055, 0.165, 2.5),
    (-0.465, 0.716, 0.070, 0.028, 0.085, 2.0),
]

# tail: (y, z, half width, top, bottom)
# Frenchie: a very short, thick stub (15 cm from its root inside the rump), set low on the
# rump and carried down - low enough that the sitting dog's stub reaches the ground.
TAIL = [
    (0.355, 0.445, 0.040, 0.040, 0.040),
    (0.385, 0.426, 0.043, 0.043, 0.043),
    (0.404, 0.401, 0.041, 0.041, 0.041),
    (0.418, 0.373, 0.036, 0.036, 0.036),
    (0.428, 0.345, 0.028, 0.028, 0.028),
    (0.434, 0.318, 0.016, 0.016, 0.016),
]

# legs: the Beagle's legs mapped with the body (y x0.80); front legs x0.80 and hind legs x0.84 in
# height above the paw. Frenchie: short, sturdy, wide-set front legs (widths x1.05 of the
# Beagle's) and muscular thighs (x1.03); the stifle sits 1.5 cm further forward than the straight
# mapping gives, so the hind leg keeps enough bend for PlayBow's raised rump.
FRONT_X = 0.155
BACK_X = 0.152
BACK_THIGH_DX = 0.018
FRONT_UPPER = [(-0.0732, 0.4971, 0.0843, 0.0832, 0.0911), (-0.0991, 0.4051, 0.0869, 0.0880, 0.0840),
               (-0.0818, 0.3301, 0.0851, 0.0754, 0.0793), (-0.0588, 0.2703, 0.0807, 0.0714, 0.0793)]
FRONT_FORE = [(-0.0548, 0.2852, 0.0807, 0.0714, 0.0793), (-0.0606, 0.2203, 0.0755, 0.0660, 0.0674),
              (-0.0663, 0.1554, 0.0702, 0.0613, 0.0596), (-0.0703, 0.1105, 0.0667, 0.0573, 0.0557)]
FRONT_PASTERN = [(-0.0703, 0.1155, 0.0659, 0.0557, 0.0542), (-0.0743, 0.0906, 0.0633, 0.0534, 0.0518),
                 (-0.0818, 0.0706, 0.0640, 0.0542, 0.0510)]
FRONT_PAW = (-0.0415, -0.1769)        # heel y, toe y
BACK_THIGH = [(0.2994, 0.4762, 0.1163, 0.1002, 0.1233), (0.2850, 0.4099, 0.1293, 0.1079, 0.1503),
              (0.2511, 0.3423, 0.1042, 0.0870, 0.1295), (0.2230, 0.2903, 0.0836, 0.0710, 0.0941),
              (0.2124, 0.2642, 0.0741, 0.0624, 0.0701)]
BACK_GASKIN = [(0.2152, 0.2747, 0.0671, 0.0578, 0.0685), (0.2539, 0.2278, 0.0606, 0.0471, 0.0685),
               (0.2937, 0.1809, 0.0535, 0.0407, 0.0535), (0.3138, 0.1523, 0.0495, 0.0386, 0.0500)]
BACK_META = [(0.3150, 0.1601, 0.0486, 0.0386, 0.0485), (0.3092, 0.1133, 0.0495, 0.0399, 0.0428),
             (0.3022, 0.0716, 0.0519, 0.0421, 0.0407)]
BACK_PAW = (0.3196, 0.1813)           # heel y, toe y
PAW = {"z": 0.046, "half_width": 0.074, "top": 0.046, "bottom": 0.046, "e": 2.6, "rings": 7}

LOFT_STYLE = {"torso_dome": 0.35, "head_dome": 0.5, "tail": (2.0, 0.7), "front_leg": (2.2, 0.3),
              "back_thigh": (2.3, 0.3), "back_leg": (2.2, 0.3), "ear": (1.8, 0.7)}

# Frenchie: the Beagle's fillets x0.8 (a smaller dog); a small one at the stub tail so it is not
# swallowed by the rump
FIELD = [
    ("core", 0.0, [("torso", 0.0), ("head", 0.030), ("tail", 0.025)]),
    ("front_L", 0.038, [("front_upper_L", 0.0), ("front_fore_L", 0.022), ("front_pastern_L", 0.016), ("front_paw_L", 0.019)]),
    ("front_R", 0.038, [("front_upper_R", 0.0), ("front_fore_R", 0.022), ("front_pastern_R", 0.016), ("front_paw_R", 0.019)]),
    ("back_L", 0.032, [("back_thigh_L", 0.0), ("back_gaskin_L", 0.026), ("back_meta_L", 0.019), ("back_paw_L", 0.019)]),
    ("back_R", 0.032, [("back_thigh_R", 0.0), ("back_gaskin_R", 0.026), ("back_meta_R", 0.019), ("back_paw_R", 0.019)]),
]

# ----------------------------------------------------------------------------- muzzle and mouth
# Frenchie: the Golden's mouth on the very short muzzle - the corners sit under the eyes, inside
# the mask, and the lip line runs 0.155 m forward to the front of the muzzle
HINGE = (-0.31, 0.625)
LIP_RUN = 0.155
LIP_DROP = 0.022
LIP_BAND = 0.0095
MOUTH_MAX_OPEN_DEG = 34.0
MOUTH_ZONE = {"below_hinge": 0.15, "behind_hinge": 0.06}
JAW_TIP = (-0.44, 0.585)

TONGUE = {
    "pivot": (-0.325, 0.627),
    "rings": [(-0.325, 0.008, 0.045, 0.011), (-0.355, 0.011, 0.060, 0.014), (-0.390, 0.012, 0.063, 0.015),
              (-0.425, 0.011, 0.060, 0.014), (-0.452, -0.008, 0.050, 0.011), (-0.468, -0.035, 0.037, 0.010)],
    "lod_rings": (0, 2, 4, 5),
    "e": 2.6,
}

# ----------------------------------------------------------------------------- ears, eyes, nose, collar
# Frenchie: bat ears - erect, 0.24 m root to tip, 0.18 m across at the base and 0.22 m at their
# widest, holding their width to a broad rounded tip; set high and wide on the flat skull,
# leaning out about 25 deg and opening forward. "side" runs the width across the head; "swing"
# turns the clips' ear motion around so an erect ear lays back (as the Shepherd's).
EARS = {
    "rings": [(0.150, -0.200, 0.910, 0.092, 0.030), (0.171, -0.202, 0.958, 0.106, 0.028),
              (0.191, -0.204, 1.006, 0.110, 0.025), (0.211, -0.206, 1.054, 0.102, 0.021),
              (0.229, -0.208, 1.098, 0.076, 0.016), (0.242, -0.209, 1.125, 0.028, 0.010)],
    "lod_rings": (0, 1, 3, 4, 5),
    "bones": [(0.150, -0.200, 0.910), (0.191, -0.204, 1.006), (0.242, -0.209, 1.125)],
    "side": (1.0, 0.35, 0.0),
    "swing": (-0.7, -1.0, 1.0),
}
# Frenchie: big round eyes, set low (at the stop) and wide apart, looking forward
EYES = {"ray_x": 0.130, "ray_z": 0.830, "face_bias": 0.6, "inset": 0.007, "radii": (0.050, 0.030, 0.056),
        "shine_offset": (0.024, 0.025, 0.014), "shine_radii": (0.011, 0.009, 0.011)}
# Frenchie: a wide black nose at the top of the short muzzle
NOSE = {"ray_z": 0.730, "offset": (0.008, 0.004), "length": 0.030, "radii": (0.080, 0.046, 0.036), "e": 2.6}
COLLAR = {"ring": 13, "gap": 0.011, "gap_lod": 0.017, "strap": (0.027, 0.010), "e": 3.0,
          "tag_radii": (0.031, 0.0065), "tag_out": 0.015, "tag_drop": 0.032}

# ----------------------------------------------------------------------------- skeleton
SPINE = {
    "hips": ((0.270, 0.436), (0.378, 0.466)),
    "spine_1": ((0.270, 0.436), (0.096, 0.4255)),
    "spine_2": ((0.096, 0.4255), (-0.084, 0.433)),
    "spine_3": ((-0.084, 0.433), (-0.183, 0.522)),
    "neck": ((-0.183, 0.522), (-0.215, 0.660)),
    "head": ((-0.215, 0.660), (-0.390, 0.740)),
}
# Frenchie: the tail bones follow the short, low stub
TAIL_BONES = [(0.360, 0.442), (0.388, 0.424), (0.406, 0.398), (0.420, 0.370), (0.434, 0.318)]
JOINTS = {
    "front_shoulder": (-0.102, 0.420), "front_elbow": (-0.057, 0.2703), "front_wrist": (-0.0709, 0.1055),
    "front_paw": (-0.0847, 0.0536), "front_toe": (-0.1682, 0.033),
    "back_hip": (0.285, 0.436), "back_stifle": (0.2152, 0.2694), "back_hock": (0.3166, 0.1523),
    "back_foot": (0.3006, 0.061), "back_toe": (0.1928, 0.0324),
}
TORSO_CHAIN = [(0.378, 0.466), (0.270, 0.436), (0.096, 0.4255), (-0.084, 0.433), (-0.183, 0.522), (-0.183, 0.660)]

# ----------------------------------------------------------------------------- sit pose
# Solved against the skinned mesh by kit/solve_sit.py (re-run after changing the shape or rig).
# Frenchie: solve_sit printed hock_z 0.0107 (full-model hocks -1.9 mm), but the LOD's decimated
# hock then sat 11.5 mm down and failed the 1 cm check; hock_z is set 5 mm higher by hand (full
# +3.5 mm, LOD -6.1 mm). Everything else is exactly as solved.
SIT = {"hips": -44.3533, "drop": -0.2648, "shoulder": -8.0, "stifle_z": 0.065, "hock_z": 0.0157, "foot_pitch": 30.0087, "tail": -49.5933}
SIT_POSE = {"spine_3": 4, "neck": 28, "head": 24, "tail_2": 12, "tail_3": 6}

# ----------------------------------------------------------------------------- lie pose (Rest)
# Solved against the skinned mesh by kit/solve_lie.py (paste its output here after changing the
# shape or the rig). The hip tilt, the stifle height and tail_2 / tail_3 are chosen; the rest are
# solved.
LIE = {"hips": 0.0, "drop": -0.1835, "elbow_z": 0.0546, "wrist_z": 0.052, "stifle_z": 0.065, "hock_z": 0.0125, "foot_pitch": 30.0087, "tail": -18.0, "tail_2": 12, "tail_3": 6}

# ----------------------------------------------------------------------------- checks
SANITY = {"head_min_z": 0.42, "tail_min_y": 0.33}
MEASURE = {"rump_min_abs_x": 0.07, "back_y": (0.0, 0.30)}

# ----------------------------------------------------------------------------- coat
# sRGB hex: main coat, "feathering" (the slightly paler underside: chest, belly, insides of the
# legs), dark channel (the ears, and with FUR_PATCHES the mask). The key is the game's coat slot:
# Default = fawn with a dark mask. The French Bulldog has exactly one coat (Derek, 2026-11-26;
# #915): no Light, Dark or Black.
COATS = {
    "default": ("#C99A62", "#DDBB8C", "#2A2321"),
}
COAT_LABELS = {"default": "Default (fawn, black mask)"}
FUR_FLOW = {
    "body": ((0.0, 1.0, -0.25), ("body", "head")),
    "leg": ((0.0, 0.15, -1.0), ("fleg", "bleg", "thigh", "paw")),
    "tail": ((0.0, 0.6, -1.0), ("tail",)),
    "ear": ((0.0, 0.0, 1.0), ("ear",)),
}
# Frenchie: no darker crown (crown_y beyond the head); the ears are dark to the tip
FUR_MARKINGS = {"bib_y": (-0.07, -0.16), "rump_y": (0.31, 0.37), "muzzle_y": (-0.33, -0.40),
                "crown_y": (5.0, 6.0), "pale_z": (0.055, 0.15), "tail_tip_y": (0.40, 0.46),
                "ear_tip_z": (2.5, 2.6)}
# Frenchie: the Lab's short, dense, smooth coat
FUR_STRAND = {"fine": (0.07, 0.30), "mid": (0.16, 0.45), "clump": (0.40, 0.7), "low": (5.0, 7.0),
              "mix": (0.60, 0.35, 0.10), "normal": 0.16, "contrast": 0.20}
# Frenchie: the dark mask over the muzzle, laid over the coat's dark channel (kit FUR_PATCHES)
FUR_PATCHES = [
    # the mask: the muzzle in front of the stop ...
    {"regions": ("head",), "y": (-0.325, -0.37)},
    # ... and up the face around the eyes, leaving the forehead and the outer cheeks fawn
    {"regions": ("head",), "y": (-0.29, -0.34), "z": (0.90, 0.85), "x_max": 0.19, "soft": 0.035},
    # the flews: the lips back to the mouth corners
    {"regions": ("head",), "y": (-0.25, -0.31), "z": (0.70, 0.63)},
    # the inner face of the ears is paler
    {"regions": ("ear",), "op": "light", "n": ("y", -0.25, -0.7), "strength": 0.35},
]
TRIM_COLOURS = {"eye": "#1C1411", "iris": "#3A2416", "pupil": "#140E0B", "shine": "#FFFFFF", "nose": "#24201F",
                "tag": "#D6AC45", "mouth": "#5E2B2C", "lip": "#2C2120", "tongue": "#E7848A",
                "collar": "#C3282E", "stitch": "#E8B0A8"}

# ----------------------------------------------------------------------------- motion
# Frenchie: shorter legs again (x0.80 of the Beagle's above the paw), so strides x0.80 of the
# Beagle's on slightly shorter cycles; lifts and stance offsets scaled with the legs.
GAITS = {
    "Walk": {"frames": 24, "speed": 0.53,
             "phase": {"HL": 0.0, "FL": 0.22, "HR": 0.5, "FR": 0.72},
             "duty": {"HL": 0.62, "HR": 0.62, "FL": 0.64, "FR": 0.64},
             "lift": {"F": 0.044, "H": 0.036}, "roll": {"F": (28.0, 0.22), "H": (24.0, 0.22)},
             "curl": {"F": 40.0, "H": 18.0}, "fold": {"F": 30.0, "H": -14.0},
             "center": {"F": -0.012, "H": 0.0}, "win": 1, "sigma": 1.2, "k": {"F": 0.85, "H": 0.45},
             "notes": "four-beat lateral-sequence walk; touchdowns LH 0.00, LF 0.22, RH 0.50, RF 0.72 "
                      "of the cycle; duty 0.62 hind / 0.64 fore"},
    "Run": {"frames": 13, "speed": 2.2,
            "phase": {"HR": 0.0, "HL": 0.11, "FR": 0.40, "FL": 0.50},
            "duty": {"HR": 0.30, "HL": 0.30, "FR": 0.30, "FL": 0.30},
            "lift": {"F": 0.088, "H": 0.068}, "roll": {"F": (35.0, 0.3), "H": (30.0, 0.3)},
            "curl": {"F": 75.0, "H": 30.0}, "fold": {"F": 55.0, "H": -25.0},
            "center": {"F": -0.032, "H": -0.052}, "suspension": (0.02, 0.90, 0.30), "win": 1, "sigma": 1.0,
            "k": {"F": 0.9, "H": 0.6}, "land": 0.985, "reach": {"F": 0.965, "H": 0.95},
            "notes": "transverse gallop, right-hind lead: touchdowns RH 0.00, LH 0.11, RF 0.40, LF 0.50; "
                     "duty 0.30; gathered suspension 0.80-1.00"},
}
# Frenchie: rock_radius is the hip-to-shoulder span (0.39 m); steps scaled with the legs. The stub
# hangs down, so on the way down it swings forward under the rump (tail_lift -45 deg, no lag)
# instead of up; with the kit default (+18 deg, 6 frames) it dips 14 mm into the ground.
SIT_DOWN = {"rock_radius": 0.387, "dip": 0.014, "hind_step_lift": 0.02, "front_step_lift": 0.029,
            "tail_lift": -45, "tail_lag": 0}
PLAY_BOW = {"elbow_z": 0.078, "wrist_z": 0.065}

# ----------------------------------------------------------------------------- review renders
RENDER = {
    "head_box": ((-0.30, -0.50, 0.45), (0.30, 0.02, 1.16)),
    "mouth_box": ((-0.15, -0.50, 0.50), (0.15, -0.26, 0.72)),
    "hips_box": {"rest": ((-0.28, 0.02, 0.0), (0.28, 0.52, 0.70)), "sit": ((-0.28, -0.10, 0.0), (0.28, 0.72, 0.52))},
    "body_box": ((-0.30, -0.52, 0.0), (0.30, 0.52, 1.16)),
    "chest_box": ((-0.30, -0.50, 0.0), (0.30, 0.16, 1.16)),
    "chest_cut_y": -0.05,
    "join_views": [("Shoulder", "front_shoulder_L", (0.012, 0.03, -0.07), (0.15, 0.18, 0.22), -72, 6),
                   ("Hip / haunch", "back_hip_L", (0.012, 0.016, -0.10), (0.15, 0.20, 0.23), -118, 8),
                   ("Tail base", "tail_1", (0.0, 0.02, -0.02), (0.14, 0.16, 0.18), -150, 24)],
    "sit_closeups": {"hind": ("back_hock_L", 0.36, 0.34), "front": ("front_paw_L", 0.27, 0.24)},
    "before": {},
    "captions": {
        "turnaround": ("French Bulldog - turnaround (orthographic)",
                       "Round 1 on the shared kit: a small, stocky dog with big bat ears, a flat face with a deep "
                       "stop, a broad deep chest and a stub tail. Fawn with a black mask (Default)."),
        "gamecam": ("French Bulldog at the game camera",
                    "Orthographic, 45 deg pitch, yaw -40. Approved base 1 (level 1), mailbox and hydrant for scale."),
        "face": ("French Bulldog - face, 3/4",
                 "Bat ears, flat broad skull, deep stop, very short square muzzle with a black mask and a wide "
                 "nose; big round eyes set low; the Golden's mouth; jaw open 12 deg."),
        "coats": ("Coat and puppy",
                  "Default (fawn with a black mask). One mesh: a coat is a fur colour map; normal map shared. "
                  "Puppy = adult x 0.55."),
        "splash": ("3D concept vs splash", "No French Bulldog splash art yet."),
        "mouth": ("Mouth", "The Golden's mouth on the very short muzzle: lips meet at the corners on the hinge."),
        "mouth_rows": ("Open (default pose, jaw 12 deg)", "Closed (jaw bone at 0, tongue hidden)"),
        "hips": ("Hip study - hind leg (skinned), sit",
                 "Red dots = joints: hips, hip, stifle, hock, foot (both legs), tail base."),
        "hips_notes": ["The Beagle's hind-leg build, shorter and a touch longer than the front legs, so the rump "
                       "sits a little high;",
                       "a short, near-vertical rear foot. The stub tail leaves the rump low.",
                       "Sit: the hips drop and tilt so the rump rests on the ground; hock and rear foot lie flat, "
                       "front paws stay planted.",
                       "Contact is measured on the skinned mesh and checked at export (see sit.png)."],
        "joins": ("Joins - shoulder, hip / haunch, tail base",
                  "One skin: the volumes blend with a smooth minimum, then retopologise."),
        "joins_rows": ("Rest", "Bent (sit): the junctions stay smooth"),
        "sit": ("Sit - ground contact",
                "Side views from a level camera at ground height; the dark line is the ground plane."),
        "chest": ("Chest", "Orthographic: front, front section, side, 3/4."),
        "chest_row": "French Bulldog: a broad, deep chest on short, wide-set front legs",
    },
}
