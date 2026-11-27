"""Frenchton: the breed definition for the shared dog kit (../kit/).

Data only: numbers, strings, tuples, lists and dicts. No imports and no Blender calls; the kit
checks that when it loads this file (kit/dh_breed.py). Started from the Beagle's breed.py (a small
dog; ../golden-retriever/breed.py documents every field), with the German Shepherd's erect-ear
keys and the Puggle's gaits; the values that make a Frenchton a Frenchton are marked "Frenchton:".

What changes from the Beagle:
  - size: a little smaller than the Beagle and the Puggle, compact and square. The body and legs
    are mapped from the Beagle's about y 0.10 and the paw-centre height (z 0.054): lengths x0.86,
    heights x0.9; the torso is x1.02 as wide, its depth above the spine x0.88 and below it
    x0.8 (keeping the topline): a muscular dog that is lighter and a bit leggier than a French
    Bulldog, not barrel-chested, with a level topline and a slight tuck-up;
  - head: Boston-style - a broad, square skull (squarer section, flatter top), a well-defined
    stop and a short, square muzzle: x0.68 the Beagle's length in front of the stop (a little
    snout, clearly longer than a French Bulldog's flat face); the head group is x0.95 the
    Beagle's and carried 0.025 m higher, so the head stays big for the body; big, round,
    wide-set eyes;
  - ears: upright bat ears set toward the corners of the skull, broad at the base and tapering to
    a rounded point, flaring a little outward (the kit's optional erect-ear keys, as the Shepherd);
  - tail: a short, thick stub set low on the croup, carried down and back;
  - coat: short, fine and smooth (the Lab's / Beagle's FUR_STRAND); a Boston "tuxedo" pattern
    in FUR_PATCHES shared by every coat: white blaze, white muzzle band, white chin, throat and
    chest, white front feet; darker muzzle sides; soft brindle stripes over the dark channel
    (they only show on the brindle coat). Default = black and white, Dark = brindle and white,
    Light = fawn and white.
Kept: the mouth (lips wrap the corners), the one continuous skin, the rig, the collar and a solved
sit that rests on the ground.

Conventions: metres, Blender axes (front -Y, up +Z, the dog's left +X), authored coordinates
(before the OY shift that puts the origin mid-span). Head-group points (head, jaw, ears, eyes,
nose, tongue) are authored before the HEAD_DY move-back; the kit adds HEAD_DY. FUR_PATCHES
positions are on the built body (head points include HEAD_DY).

Build it:  python3 art-source/blender/dogs/kit/build_breed.py frenchton
"""

# ----------------------------------------------------------------------------- names
NAME = "dog_frenchton"                    # object and file name stem (dog_frenchton_body, dog_frenchton_default.gltf)
DISPLAY_NAME = "Frenchton"
STATS_NAME = "Frenchton (adult) concept, round 1"
MOTION_STATUS = "Frenchton motion clips, first pass (concept)"
MATERIAL_NAMES = ["DH_Dog_Fur_%s", "DH_Dog_Gloss", "DH_Dog_Soft"]   # fur (per coat), gloss, soft
# Frenchton: a short, fine, glossy coat (a little more sheen than the Beagle's 0.64 / 0.38)
MATERIAL_SHADING = {"fur": (0.6, 0.4), "gloss": (0.30, 0.35), "soft": (0.55, 0.35)}

# ----------------------------------------------------------------------------- budgets
BUDGET = {"full": 4000, "lod1": 1500}     # triangles per file
SKIN_FACES = 1490                         # QuadriFlow target (quads) for the body
LOD_SKIN_TRIS = 820                       # the LOD body: the full retopology decimated to this

# ----------------------------------------------------------------------------- placement
HEAD_DY = 0.0279               # the Beagle's x the head scale
OY = 0.03                       # origin under the middle of the nose-to-rump span
JAW_REST_OPEN_DEG = 12.0       # the file's default pose opens the jaw this much

# ----------------------------------------------------------------------------- body shape
FIELD_GRID = {"h": 0.010, "origin": (-0.34, -0.64, -0.02), "extent": (0.68, 1.26, 1.14), "voxel": 0.0068}

# torso + neck: (y, z, half width, top, bottom, exponent)
# Frenchton: the Beagle's torso mapped (see the header): compact and square, muscular but not
# barrel-chested; a level topline at about 0.64 m, a slight tuck-up; a short, strong neck.
TORSO = [
    (0.4172, 0.5078, 0.0643, 0.0412, 0.0432, 2.0),
    (0.4075, 0.516, 0.1628, 0.1014, 0.1094, 2.2),
    (0.3752, 0.5127, 0.2125, 0.1299, 0.121, 2.5),
    (0.33, 0.5064, 0.2296, 0.1362, 0.1181, 2.6),
    (0.2849, 0.4996, 0.2296, 0.1394, 0.1123, 2.6),    # hip loop
    (0.214, 0.4992, 0.2331, 0.1362, 0.1123, 2.6),
    (0.1366, 0.492, 0.2416, 0.1362, 0.1238, 2.6),    # loin, a little tuck-up
    (0.0527, 0.4924, 0.2485, 0.1394, 0.1544, 2.6),
    (-0.0247, 0.4928, 0.2502, 0.1426, 0.1797, 2.6),    # ribcage
    (-0.0828, 0.5, 0.2442, 0.1426, 0.1855, 2.6),    # shoulder loop
    (-0.1408, 0.5212, 0.2296, 0.1394, 0.1797, 2.5),
    (-0.1763, 0.5563, 0.2091, 0.1331, 0.159, 2.4),    # front of chest
    (-0.1988, 0.6059, 0.2005, 0.1267, 0.1394, 2.3),    # neck base
    (-0.2182, 0.6504, 0.1885, 0.1172, 0.1181, 2.2),    # collar loop
    (-0.2278, 0.6944, 0.1714, 0.1045, 0.1008, 2.1),
    (-0.2344, 0.7394, 0.1491, 0.0919, 0.0864, 2.0),    # neck top, inside the skull
]

# head with the lower jaw: (y, z, half width, top, bottom, exponent)
# Frenchton: the Beagle's head x0.95, the skull 1.05x as wide with a squarer section
# (exponent >= 2.4) and a flatter top (x0.96); a well-defined stop; the muzzle in
# front of the stop x0.68 as long, 1.04x as wide, square (exponent 2.7).
HEAD = [
    (-0.0827, 0.8596, 0.0628, 0.0536, 0.0559, 2.0),
    (-0.1067, 0.8676, 0.1676, 0.1379, 0.1357, 2.4),
    (-0.1545, 0.8732, 0.243, 0.1839, 0.1716, 2.4),
    (-0.2224, 0.8755, 0.2664, 0.2053, 0.1955, 2.4),    # skull: broad, square section
    (-0.2942, 0.8715, 0.2614, 0.1976, 0.2035, 2.4),
    (-0.3541, 0.858, 0.2346, 0.1685, 0.2091, 2.4),
    (-0.3995, 0.8333, 0.191, 0.1149, 0.2091, 2.45),    # the stop: well defined
    (-0.4266, 0.8077, 0.166, 0.0862, 0.2011, 2.7),
    (-0.4646, 0.7981, 0.1594, 0.0798, 0.1915, 2.7),    # muzzle: short, square, deep
    (-0.5026, 0.7934, 0.146, 0.0766, 0.1723, 2.7),
    (-0.5297, 0.7901, 0.1062, 0.0591, 0.1213, 2.4),
    (-0.5422, 0.7901, 0.0481, 0.0319, 0.0526, 2.0),
]

# tail: (y, z, half width, top, bottom)
# Frenchton: a short, thick stub (about 0.12 m showing), set low on the croup below the topline
# and carried down and back, tapering to a blunt tip.
TAIL = [
    (0.4050, 0.5480, 0.054, 0.056, 0.052),
    (0.4367, 0.5389, 0.052, 0.054, 0.050),
    (0.4684, 0.5298, 0.049, 0.051, 0.047),
    (0.5002, 0.5207, 0.045, 0.047, 0.043),
    (0.5319, 0.5116, 0.040, 0.042, 0.038),
    (0.5588, 0.5039, 0.033, 0.035, 0.031),
    (0.5799, 0.4978, 0.024, 0.026, 0.022),
    (0.5953, 0.4934, 0.012, 0.014, 0.010),
]

# legs: the Beagle's legs mapped with the body (lengths x0.86, heights x0.9), widths x1.03.
# Frenchton: straight, sturdy, muscular legs; a little more leg shows under the shallower chest.
FRONT_X = 0.1474
BACK_X = 0.1593
BACK_THIGH_DX = 0.021
FRONT_UPPER = [(-0.084, 0.5598, 0.083, 0.0917, 0.1003),
    (-0.1119, 0.4567, 0.0857, 0.0969, 0.0926),
    (-0.0933, 0.3727, 0.0839, 0.083, 0.0873),
    (-0.0686, 0.3056, 0.0796, 0.0787, 0.0873)]
FRONT_FORE = [(-0.0643, 0.3224, 0.0796, 0.0787, 0.0873),
    (-0.0705, 0.2497, 0.0744, 0.0727, 0.0744),
    (-0.0766, 0.1769, 0.0692, 0.0675, 0.0657),
    (-0.0809, 0.1266, 0.0657, 0.0631, 0.0614)]
FRONT_PASTERN = [(-0.0809, 0.1322, 0.0649, 0.0614, 0.0597),
    (-0.0852, 0.1043, 0.0623, 0.0588, 0.0571),
    (-0.0933, 0.0819, 0.0631, 0.0597, 0.0562)]
FRONT_PAW = (-0.05, -0.1955)        # heel y, toe y
BACK_THIGH = [(0.3165, 0.5166, 0.1168, 0.1125, 0.1384),
    (0.301, 0.4454, 0.1298, 0.1211, 0.1687),
    (0.2732, 0.3727, 0.1046, 0.0977, 0.1453),
    (0.2484, 0.3168, 0.0839, 0.0796, 0.1056),
    (0.2391, 0.2888, 0.0744, 0.07, 0.0787)]
BACK_GASKIN = [(0.2422, 0.3001, 0.0727, 0.07, 0.083),
    (0.2762, 0.2497, 0.0657, 0.0571, 0.083),
    (0.3104, 0.1993, 0.058, 0.0493, 0.0649),
    (0.3319, 0.1686, 0.0537, 0.0468, 0.0606)]
BACK_META = [(0.3332, 0.1769, 0.0527, 0.0468, 0.0588),
    (0.327, 0.1266, 0.0537, 0.0484, 0.0519),
    (0.3196, 0.0819, 0.0562, 0.0511, 0.0493)]
BACK_PAW = (0.3382, 0.1895)           # heel y, toe y
PAW = {"z": 0.0544, "half_width": 0.084, "top": 0.0544, "bottom": 0.0544, "e": 2.6, "rings": 7}

LOFT_STYLE = {"torso_dome": 0.35, "head_dome": 0.5, "tail": (2.0, 0.7), "front_leg": (2.2, 0.3),
              "back_thigh": (2.3, 0.3), "back_leg": (2.2, 0.3), "ear": (2.0, 0.9)}

# the Beagle's fillets; Frenchton: a smaller one where the low stub tail leaves the croup
FIELD = [
    ("core", 0.0, [("torso", 0.0), ("head", 0.036), ("tail", 0.035)]),
    ("front_L", 0.048, [("front_upper_L", 0.0), ("front_fore_L", 0.028), ("front_pastern_L", 0.02), ("front_paw_L", 0.024)]),
    ("front_R", 0.048, [("front_upper_R", 0.0), ("front_fore_R", 0.028), ("front_pastern_R", 0.02), ("front_paw_R", 0.024)]),
    ("back_L", 0.04, [("back_thigh_L", 0.0), ("back_gaskin_L", 0.032), ("back_meta_L", 0.024), ("back_paw_L", 0.024)]),
    ("back_R", 0.04, [("back_thigh_R", 0.0), ("back_gaskin_R", 0.032), ("back_meta_R", 0.024), ("back_paw_R", 0.024)]),
]

# ----------------------------------------------------------------------------- muzzle and mouth
# Frenchton: the Beagle's mouth moved with the head (the parts in front of the stop with the
# shorter muzzle)
HINGE = (-0.3501, 0.688)
LIP_RUN = 0.2621
LIP_DROP = 0.0504
LIP_BAND = 0.0109
MOUTH_MAX_OPEN_DEG = 34.0
MOUTH_ZONE = {'below_hinge': 0.1756, 'behind_hinge': 0.0798}
JAW_TIP = (-0.4933, 0.6481)

TONGUE = {
    "pivot": (-0.3779, 0.696),
    "rings": [(-0.3779, 0.0096, 0.0495, 0.0127), (-0.4234, 0.0127, 0.0671, 0.016), (-0.47, 0.0143, 0.0702, 0.0167), (-0.5124, 0.0127, 0.0671, 0.0152), (-0.5373, -0.0096, 0.0559, 0.0127), (-0.5482, -0.0415, 0.0415, 0.0112)],
    "lod_rings": (0, 2, 4, 5),
    "e": 2.6,
}

# ----------------------------------------------------------------------------- ears, eyes, nose, collar
# Frenchton: upright bat ears (x, y, z, half width, half thickness) - rooted on the top corners of
# the skull, 0.2 m tall, broad at the base, tapering to a rounded point and flaring outward.
# The kit's optional erect-ear keys, as the German Shepherd's: the width runs across the head,
# the opening faces forward and a little out; the clips lay the ears back rather than swing them.
EARS = {
    "rings": [(0.172, -0.235, 1.005, 0.08, 0.03), (0.188, -0.239, 1.05, 0.09, 0.028), (0.205, -0.243, 1.098, 0.084, 0.024), (0.221, -0.246, 1.142, 0.066, 0.019), (0.235, -0.248, 1.178, 0.044, 0.015), (0.245, -0.249, 1.2, 0.016, 0.01)],
    "lod_rings": (0, 1, 3, 4, 5),
    "bones": [(0.172, -0.235, 1.005), (0.205, -0.243, 1.098), (0.245, -0.249, 1.2)],
    "side": (1.0, 0.50, 0.0),
    "swing": (-0.7, -1.0, 1.0),
}
# Frenchton: big, round, wide-set eyes (the family look, rounder and a touch bigger than the
# Beagle's, set wider on the broad skull)
EYES = {"ray_x": 0.12, "ray_z": 0.9098, "face_bias": 0.6, "inset": 0.010, "radii": (0.058, 0.034, 0.063),
        "shine_offset": (0.0252, 0.0269, 0.0151), "shine_radii": (0.0118, 0.0101, 0.0118)}
# Frenchton: a broad black nose on the short muzzle
NOSE = {"ray_z": 0.8189, "offset": (0.0101, 0.0050), "length": 0.0320, "radii": (0.08, 0.046, 0.036), "e": 2.5}
COLLAR = {"ring": 13, "gap": 0.012, "gap_lod": 0.019, "strap": (0.029, 0.011), "e": 3.0,
          "tag_radii": (0.033, 0.007), "tag_out": 0.017, "tag_drop": 0.035}

# ----------------------------------------------------------------------------- skeleton
SPINE = {'hips': ((0.2849, 0.4734), (0.401, 0.5022)), 'spine_1': ((0.2849, 0.4734), (0.0979, 0.4734)), 'spine_2': ((0.0979, 0.4734), (-0.0956, 0.4878)), 'spine_3': ((-0.0956, 0.4878), (-0.2021, 0.5886)), 'neck': ((-0.2021, 0.5886), (-0.2235, 0.689)), 'head': ((-0.2235, 0.689), (-0.4147, 0.8316))}
# Frenchton: the tail bones follow the short stub
TAIL_BONES = [(0.405, 0.548), (0.4526, 0.5344), (0.5002, 0.5207), (0.5477, 0.5071), (0.5953, 0.4934)]
JOINTS = {'front_shoulder': (-0.115, 0.4734), 'front_elbow': (-0.0667, 0.3056), 'front_wrist': (-0.0815, 0.1211), 'front_paw': (-0.0964, 0.0628), 'front_toe': (-0.1862, 0.0398), 'back_hip': (0.301, 0.4734), 'back_stifle': (0.2422, 0.2944), 'back_hock': (0.335, 0.1686), 'back_foot': (0.3178, 0.0705), 'back_toe': (0.2019, 0.0398)}
TORSO_CHAIN = [(0.401, 0.5022), (0.2849, 0.4734), (0.0979, 0.4734), (-0.0956, 0.4878), (-0.2021, 0.5886), (-0.2021, 0.689)]

# ----------------------------------------------------------------------------- sit pose
SIT = {'hips': -45.5543, 'drop': -0.2854, 'shoulder': -8.0, 'stifle_z': 0.075, 'hock_z': 0.0208, 'foot_pitch': 28.2212, 'tail': -9.664}
SIT_POSE = {"spine_3": 4, "neck": 28, "head": 24, "tail_2": 12, "tail_3": 8}

# ----------------------------------------------------------------------------- checks
SANITY = {"head_min_z": 0.47, "tail_min_y": 0.32}
# Frenchton: the back is measured behind the skull and ahead of the croup
MEASURE = {"rump_min_abs_x": 0.085, "back_y": (0.03, 0.33)}

# ----------------------------------------------------------------------------- coat
# sRGB hex: main coat, "feathering" (here only the white markings: FUR_PATCHES clears the kit's
# own paler underside with "plain" and lays the white with "pale"), dark channel (the brindle
# stripes, the darker muzzle sides and the ears). The keys are the game's coat slots (proposed,
# Derek still choosing): Default = black and white (Boston tuxedo); Dark = brindle and white;
# Light = cream fawn and white with a slightly darker muzzle. On Default the dark channel is the
# coat's black, so the stripes do not show; on Light it is only a little deeper than the fawn.
COATS = {
    "default": ('#211D1C', '#F3F0EA', '#1A1716'),
    "dark":    ('#6E4C33', '#F1EDE5', '#241A15'),
    "light":   ('#D8B282', '#F5F1E9', '#C59C6C'),
}
COAT_LABELS = {"default": "Default (black and white)", "dark": "Dark (brindle and white)",
               "light": "Light (fawn and white)"}
FUR_FLOW = {
    "body": ((0.0, 1.0, -0.25), ("body", "head")),
    "leg": ((0.0, 0.15, -1.0), ("fleg", "bleg", "thigh", "paw")),
    "tail": ((0.0, 0.6, -1.0), ("tail",)),
    "ear": ((0.0, 0.0, 1.0), ("ear",)),
}
FUR_MARKINGS = {"bib_y": (-0.09, -0.195), "rump_y": (0.375, 0.45), "muzzle_y": (-0.40, -0.45),
                "crown_y": (-0.27, -0.19), "pale_z": (0.068, 0.18), "tail_tip_y": (0.48, 0.56),
                "ear_tip_z": (2.5, 2.6)}      # Frenchton: the ears are the coat colour to the tip
# Frenchton: the Lab's / Beagle's short, smooth coat
FUR_STRAND = {"fine": (0.07, 0.30), "mid": (0.16, 0.45), "clump": (0.40, 0.7), "low": (5.0, 7.0),
              "mix": (0.60, 0.35, 0.10), "normal": 0.16, "contrast": 0.20}
FUR_PATCHES = [
    # no kit underside: the coat is solid apart from its own white markings (kit, optional "plain")
    {"regions": ("body", "head", "tail", "fleg", "bleg", "thigh", "paw", "ear"), "op": "plain"},
    # brindle: soft stripes over the dark channel, slanting back up the body, neck and skull. Each
    # stripe is a dark patch in front of a slanted line and a light patch in front of a second line
    # a little further forward; laid from the tail forward so a later one cannot clear an earlier one
    {"regions": ('body', 'thigh', 'head', 'tail'), "line": [(-0.44, -3.11), (1.56, 4.11)], "soft": 0.025, "wobble": 0.07, "strength": 0.6},
    {"regions": ('body', 'thigh', 'head', 'tail'), "op": "light", "line": [(-0.452, -3.11), (1.548, 4.11)], "soft": 0.025, "wobble": 0.07},
    {"regions": ('body', 'thigh', 'head', 'tail'), "line": [(-0.481, -3.01), (1.519, 4.01)], "soft": 0.025, "wobble": 0.07, "strength": 0.6},
    {"regions": ('body', 'thigh', 'head', 'tail'), "op": "light", "line": [(-0.493, -3.01), (1.507, 4.01)], "soft": 0.025, "wobble": 0.07},
    {"regions": ('body', 'thigh', 'head', 'tail'), "line": [(-0.515, -3.02), (1.485, 4.02)], "soft": 0.025, "wobble": 0.07, "strength": 0.6},
    {"regions": ('body', 'thigh', 'head', 'tail'), "op": "light", "line": [(-0.525, -3.02), (1.475, 4.02)], "soft": 0.025, "wobble": 0.07},
    {"regions": ('body', 'thigh', 'head', 'tail'), "line": [(-0.551, -2.27), (1.449, 3.27)], "soft": 0.025, "wobble": 0.07, "strength": 0.6},
    {"regions": ('body', 'thigh', 'head', 'tail'), "op": "light", "line": [(-0.566, -2.27), (1.434, 3.27)], "soft": 0.025, "wobble": 0.07},
    {"regions": ('body', 'thigh', 'head', 'tail'), "line": [(-0.58, -3.56), (1.42, 4.56)], "soft": 0.025, "wobble": 0.07, "strength": 0.6},
    {"regions": ('body', 'thigh', 'head', 'tail'), "op": "light", "line": [(-0.589, -3.56), (1.411, 4.56)], "soft": 0.025, "wobble": 0.07},
    {"regions": ('body', 'thigh', 'head', 'tail'), "line": [(-0.616, -3.87), (1.384, 4.87)], "soft": 0.025, "wobble": 0.07, "strength": 0.6},
    {"regions": ('body', 'thigh', 'head', 'tail'), "op": "light", "line": [(-0.624, -3.87), (1.376, 4.87)], "soft": 0.025, "wobble": 0.07},
    {"regions": ('body', 'thigh', 'head', 'tail'), "line": [(-0.658, -3.21), (1.342, 4.21)], "soft": 0.025, "wobble": 0.07, "strength": 0.6},
    {"regions": ('body', 'thigh', 'head', 'tail'), "op": "light", "line": [(-0.672, -3.21), (1.328, 4.21)], "soft": 0.025, "wobble": 0.07},
    {"regions": ('body', 'thigh', 'head', 'tail'), "line": [(-0.685, -3.05), (1.315, 4.05)], "soft": 0.025, "wobble": 0.07, "strength": 0.6},
    {"regions": ('body', 'thigh', 'head', 'tail'), "op": "light", "line": [(-0.693, -3.05), (1.307, 4.05)], "soft": 0.025, "wobble": 0.07},
    {"regions": ('body', 'thigh', 'head', 'tail'), "line": [(-0.71, -2.54), (1.29, 3.54)], "soft": 0.025, "wobble": 0.07, "strength": 0.6},
    {"regions": ('body', 'thigh', 'head', 'tail'), "op": "light", "line": [(-0.72, -2.54), (1.28, 3.54)], "soft": 0.025, "wobble": 0.07},
    {"regions": ('body', 'thigh', 'head', 'tail'), "line": [(-0.734, -2.89), (1.266, 3.89)], "soft": 0.025, "wobble": 0.07, "strength": 0.6},
    {"regions": ('body', 'thigh', 'head', 'tail'), "op": "light", "line": [(-0.746, -2.89), (1.254, 3.89)], "soft": 0.025, "wobble": 0.07},
    {"regions": ('body', 'thigh', 'head', 'tail'), "line": [(-0.773, -3.25), (1.227, 4.25)], "soft": 0.025, "wobble": 0.07, "strength": 0.6},
    {"regions": ('body', 'thigh', 'head', 'tail'), "op": "light", "line": [(-0.786, -3.25), (1.214, 4.25)], "soft": 0.025, "wobble": 0.07},
    {"regions": ('body', 'thigh', 'head', 'tail'), "line": [(-0.806, -2.92), (1.194, 3.92)], "soft": 0.025, "wobble": 0.07, "strength": 0.6},
    {"regions": ('body', 'thigh', 'head', 'tail'), "op": "light", "line": [(-0.82, -2.92), (1.18, 3.92)], "soft": 0.025, "wobble": 0.07},
    {"regions": ('body', 'thigh', 'head', 'tail'), "line": [(-0.835, -3.89), (1.165, 4.89)], "soft": 0.025, "wobble": 0.07, "strength": 0.6},
    {"regions": ('body', 'thigh', 'head', 'tail'), "op": "light", "line": [(-0.852, -3.89), (1.148, 4.89)], "soft": 0.025, "wobble": 0.07},
    {"regions": ('body', 'thigh', 'head', 'tail'), "line": [(-0.875, -2.67), (1.125, 3.67)], "soft": 0.025, "wobble": 0.07, "strength": 0.6},
    {"regions": ('body', 'thigh', 'head', 'tail'), "op": "light", "line": [(-0.889, -2.67), (1.111, 3.67)], "soft": 0.025, "wobble": 0.07},
    {"regions": ('body', 'thigh', 'head', 'tail'), "line": [(-0.903, -2.23), (1.097, 3.23)], "soft": 0.025, "wobble": 0.07, "strength": 0.6},
    {"regions": ('body', 'thigh', 'head', 'tail'), "op": "light", "line": [(-0.914, -2.23), (1.086, 3.23)], "soft": 0.025, "wobble": 0.07},
    {"regions": ('body', 'thigh', 'head', 'tail'), "line": [(-0.941, -3.62), (1.059, 4.62)], "soft": 0.025, "wobble": 0.07, "strength": 0.6},
    {"regions": ('body', 'thigh', 'head', 'tail'), "op": "light", "line": [(-0.953, -3.62), (1.047, 4.62)], "soft": 0.025, "wobble": 0.07},
    {"regions": ('body', 'thigh', 'head', 'tail'), "line": [(-0.971, -3.63), (1.029, 4.63)], "soft": 0.025, "wobble": 0.07, "strength": 0.6},
    {"regions": ('body', 'thigh', 'head', 'tail'), "op": "light", "line": [(-0.988, -3.63), (1.012, 4.63)], "soft": 0.025, "wobble": 0.07},
    {"regions": ('body', 'thigh', 'head', 'tail'), "line": [(-0.995, -3.74), (1.005, 4.74)], "soft": 0.025, "wobble": 0.07, "strength": 0.6},
    {"regions": ('body', 'thigh', 'head', 'tail'), "op": "light", "line": [(-1.005, -3.74), (0.995, 4.74)], "soft": 0.025, "wobble": 0.07},
    {"regions": ('body', 'thigh', 'head', 'tail'), "line": [(-1.028, -2.82), (0.972, 3.82)], "soft": 0.025, "wobble": 0.07, "strength": 0.6},
    {"regions": ('body', 'thigh', 'head', 'tail'), "op": "light", "line": [(-1.045, -2.82), (0.955, 3.82)], "soft": 0.025, "wobble": 0.07},
    {"regions": ('body', 'thigh', 'head', 'tail'), "line": [(-1.053, -3.5), (0.947, 4.5)], "soft": 0.025, "wobble": 0.07, "strength": 0.6},
    {"regions": ('body', 'thigh', 'head', 'tail'), "op": "light", "line": [(-1.067, -3.5), (0.933, 4.5)], "soft": 0.025, "wobble": 0.07},
    {"regions": ('body', 'thigh', 'head', 'tail'), "line": [(-1.082, -2.7), (0.918, 3.7)], "soft": 0.025, "wobble": 0.07, "strength": 0.6},
    {"regions": ('body', 'thigh', 'head', 'tail'), "op": "light", "line": [(-1.091, -2.7), (0.909, 3.7)], "soft": 0.025, "wobble": 0.07},
    {"regions": ('body', 'thigh', 'head', 'tail'), "line": [(-1.123, -2.31), (0.877, 3.31)], "soft": 0.025, "wobble": 0.07, "strength": 0.6},
    {"regions": ('body', 'thigh', 'head', 'tail'), "op": "light", "line": [(-1.138, -2.31), (0.862, 3.31)], "soft": 0.025, "wobble": 0.07},
    {"regions": ('body', 'thigh', 'head', 'tail'), "line": [(-1.152, -2.21), (0.848, 3.21)], "soft": 0.025, "wobble": 0.07, "strength": 0.6},
    {"regions": ('body', 'thigh', 'head', 'tail'), "op": "light", "line": [(-1.161, -2.21), (0.839, 3.21)], "soft": 0.025, "wobble": 0.07},
    {"regions": ('body', 'thigh', 'head', 'tail'), "line": [(-1.19, -3.11), (0.81, 4.11)], "soft": 0.025, "wobble": 0.07, "strength": 0.6},
    {"regions": ('body', 'thigh', 'head', 'tail'), "op": "light", "line": [(-1.2, -3.11), (0.8, 4.11)], "soft": 0.025, "wobble": 0.07},
    {"regions": ('body', 'thigh', 'head', 'tail'), "line": [(-1.222, -3.42), (0.778, 4.42)], "soft": 0.025, "wobble": 0.07, "strength": 0.6},
    {"regions": ('body', 'thigh', 'head', 'tail'), "op": "light", "line": [(-1.232, -3.42), (0.768, 4.42)], "soft": 0.025, "wobble": 0.07},
    {"regions": ('body', 'thigh', 'head', 'tail'), "line": [(-1.249, -2.31), (0.751, 3.31)], "soft": 0.025, "wobble": 0.07, "strength": 0.6},
    {"regions": ('body', 'thigh', 'head', 'tail'), "op": "light", "line": [(-1.263, -2.31), (0.737, 3.31)], "soft": 0.025, "wobble": 0.07},
    {"regions": ('body', 'thigh', 'head', 'tail'), "line": [(-1.28, -2.59), (0.72, 3.59)], "soft": 0.025, "wobble": 0.07, "strength": 0.6},
    {"regions": ('body', 'thigh', 'head', 'tail'), "op": "light", "line": [(-1.29, -2.59), (0.71, 3.59)], "soft": 0.025, "wobble": 0.07},
    # the legs a little darker (brindle legs read darker than the body)
    {"regions": ("fleg", "bleg"), "strength": 0.35},
    # darker muzzle sides (the flews and cheeks in front of the stop)
    {"regions": ("head",), "y": (-0.375, -0.395), "strength": 1.0},
    # white ("pale", kit, optional): a narrow blaze between the eyes ...
    {"regions": ("head",), "op": "pale", "x_max": 0.03, "y": (-0.325, -0.35), "z": (0.8, 0.84), "soft": 0.012},
    # ... widening into a white muzzle above the lip line (the lips and flews stay dark)
    {"regions": ("head",), "op": "pale", "x_max": 0.13, "y": (-0.385, -0.405), "z": (0.695, 0.73), "soft": 0.025,
     "line": [(-2.0, 0.865), (2.0, 0.865)], "above": False},
    # white chin and throat, white chest
    {"regions": ("head",), "op": "pale", "n": ("z", -0.45, -0.75), "y": (-0.32, -0.36)},
    {"regions": ("body",), "op": "pale", "n": ("z", -0.15, -0.45), "y": (-0.06, -0.12)},
    {"regions": ("body",), "op": "pale", "n": ("y", -0.30, -0.70), "y": (-0.1, -0.15)},
    # white front feet
    {"regions": ("fleg", "paw"), "op": "pale", "z": (0.17, 0.12), "y": (0.08, 0.0)},
]
TRIM_COLOURS = {"eye": "#1C1411", "iris": "#4A2C1A", "pupil": "#140E0B", "shine": "#FFFFFF", "nose": "#1E1A19",
                "tag": "#D6AC45", "mouth": "#5E2B2C", "lip": "#2C2120", "tongue": "#E7848A",
                "collar": "#C3282E", "stitch": "#E8B0A8"}

# ----------------------------------------------------------------------------- motion
# Frenchton: the legs are about the Puggle's length (0.90x the Beagle's), so the Puggle's
# strides and cycles
GAITS = {
    "Walk": {"frames": 24, "speed": 0.60,
             "phase": {"HL": 0.0, "FL": 0.22, "HR": 0.5, "FR": 0.72},
             "duty": {"HL": 0.62, "HR": 0.62, "FL": 0.64, "FR": 0.64},
             "lift": {"F": 0.050, "H": 0.040}, "roll": {"F": (28.0, 0.22), "H": (24.0, 0.22)},
             "curl": {"F": 40.0, "H": 18.0}, "fold": {"F": 30.0, "H": -14.0},
             "center": {"F": -0.0135, "H": 0.0}, "win": 1, "sigma": 1.2, "k": {"F": 0.85, "H": 0.45},
             "notes": "four-beat lateral-sequence walk; touchdowns LH 0.00, LF 0.22, RH 0.50, RF 0.72 "
                      "of the cycle; duty 0.62 hind / 0.64 fore"},
    "Run": {"frames": 13, "speed": 2.56,
            "phase": {"HR": 0.0, "HL": 0.11, "FR": 0.40, "FL": 0.50},
            "duty": {"HR": 0.30, "HL": 0.30, "FR": 0.30, "FL": 0.30},
            "lift": {"F": 0.10, "H": 0.077}, "roll": {"F": (35.0, 0.3), "H": (30.0, 0.3)},
            "curl": {"F": 75.0, "H": 30.0}, "fold": {"F": 55.0, "H": -25.0},
            "center": {"F": -0.036, "H": -0.0585}, "suspension": (0.0225, 0.90, 0.30), "win": 1, "sigma": 1.0,
            "k": {"F": 0.9, "H": 0.6}, "land": 0.985, "reach": {"F": 0.965, "H": 0.95},
            "notes": "transverse gallop, right-hind lead: touchdowns RH 0.00, LH 0.11, RF 0.40, LF 0.50; "
                     "duty 0.30; gathered suspension 0.80-1.00"},
}
# Frenchton: rock_radius is the hip-to-shoulder span (0.42 m); steps scaled with the legs
SIT_DOWN = {'rock_radius': 0.4, 'dip': 0.016, 'hind_step_lift': 0.0225, 'front_step_lift': 0.032}
PLAY_BOW = {"elbow_z": 0.088, "wrist_z": 0.073}

# ----------------------------------------------------------------------------- review renders
RENDER = {
    "head_box": ((-0.33, -0.54, 0.55), (0.33, 0.0, 1.27)),
    "mouth_box": ((-0.17, -0.53, 0.58), (0.17, -0.30, 0.78)),
    "hips_box": {"rest": ((-0.34, 0.02, 0.0), (0.34, 0.62, 0.92)), "sit": ((-0.34, -0.12, 0.0), (0.34, 0.85, 0.75))},
    "body_box": ((-0.35, -0.58, 0.0), (0.35, 0.58, 1.27)),
    "chest_box": ((-0.36, -0.54, 0.0), (0.36, 0.18, 1.27)),
    "chest_cut_y": -0.05,
    "join_views": [("Shoulder", "front_shoulder_L", (0.015, 0.04, -0.09), (0.19, 0.23, 0.27), -72, 6),
                   ("Hip / haunch", "back_hip_L", (0.015, 0.02, -0.12), (0.19, 0.25, 0.29), -118, 8),
                   ("Tail base", "tail_1", (0.0, 0.03, -0.02), (0.17, 0.20, 0.22), -150, 18)],
    "sit_closeups": {"hind": ("back_hock_L", 0.42, 0.40), "front": ("front_paw_L", 0.32, 0.28)},
    "before": {},
    "captions": {
        "turnaround": ("Frenchton - turnaround (orthographic)",
                       "Round 1 on the shared kit: a small, compact, muscular dog with a square Boston-style skull, "
                       "a short muzzle, upright bat ears and a low stub tail. Black and white tuxedo (Default)."),
        "gamecam": ("Frenchton at the game camera",
                    "Orthographic, 45 deg pitch, yaw -40. Approved base 1 (level 1), mailbox and hydrant for scale."),
        "face": ("Frenchton - face, 3/4",
                 "Square skull, defined stop, short square muzzle with a white band and blaze; big round eyes; "
                 "upright bat ears; the Golden's mouth; jaw open 12 deg."),
        "coats": ("Coats and puppy",
                  "Default (black and white), Dark (brindle and white) and Light (fawn and white). One mesh: a coat "
                  "is a fur colour map; normal map shared. Puppy = adult x 0.55."),
        "splash": ("3D concept vs splash", "No Frenchton splash art yet."),
        "mouth": ("Mouth", "The Golden's mouth on the short muzzle: lips meet at the corners on the hinge."),
        "mouth_rows": ("Open (default pose, jaw 12 deg)", "Closed (jaw bone at 0, tongue hidden)"),
        "hips": ("Hip study - hind leg (skinned), sit",
                 "Red dots = joints: hips, hip, stifle, hock, foot (both legs), tail base."),
        "hips_notes": ["The Beagle's hind-leg build, compact and muscular: thigh into a rounded haunch, stifle, "
                       "lower thigh back to the hock,",
                       "and a short, near-vertical rear foot. The stub tail leaves the croup low and points down.",
                       "Sit: the hips drop and tilt so the rump rests on the ground; hock and rear foot lie flat, "
                       "front paws stay planted.",
                       "Contact is measured on the skinned mesh and checked at export (see sit.png)."],
        "joins": ("Joins - shoulder, hip / haunch, tail base",
                  "One skin: the volumes blend with a smooth minimum, then retopologise."),
        "joins_rows": ("Rest", "Bent (sit): the junctions stay smooth"),
        "sit": ("Sit - ground contact",
                "Side views from a level camera at ground height; the dark line is the ground plane."),
        "chest": ("Chest", "Orthographic: front, front section, side, 3/4."),
        "chest_row": "Frenchton: a muscular chest, not barrel-shaped, on straight legs",
    },
}
