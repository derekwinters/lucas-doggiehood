"""Chihuahua: the breed definition for the shared dog kit (../kit/).

Data only: numbers, strings, tuples, lists and dicts. No imports and no Blender calls; the kit
checks that when it loads this file (kit/dh_breed.py). The body and legs are the Beagle's
(round-1 concept) mapped down with explicit factors; the head, ears, tail and coat are new.
../golden-retriever/breed.py documents every field; the Chihuahua's choices are marked
"Chihuahua:".

What changes from the Beagle:
  - size: the smallest dog so far. Body lengths x0.70; the torso's depths x0.62 about a centre
    lowered from 0.52 m to 0.36 m, widths x0.62; the legs x0.69 in height (from the ground) and
    about x0.5 in width (fine-boned, straight); the feet x0.62 long and x0.5 wide (dainty); a
    little more tuck-up (the loin's underline x0.8 more);
  - neck: longer and carried higher than the mapped Beagle's, slightly arched;
  - head: a big round apple-dome skull (0.39 m across, so the head is big even for the family),
    a pronounced stop and a short, fine, slightly pointed muzzle; very large round eyes set wide;
  - ears: large, erect, broad at the base, set high and flaring out at about 45 deg
    (EARS["side"], EARS["swing"], as the German Shepherd's);
  - tail: thin and tapering, carried up in a sickle curve over the back, not touching it;
  - coat: short and smooth (the Lab's FUR_STRAND). Default = fawn, Black = black and tan, laid out
    by FUR_PATCHES (dark channel = the black's black, the fawn's slightly deeper top colour).
Kept: the mouth (lips wrap the corners), the one continuous skin, the rig, the collar (scaled
down) and a solved sit that rests on the ground.

Conventions: metres, Blender axes (front -Y, up +Z, the dog's left +X), authored coordinates
(before the OY shift that puts the origin mid-span). Head-group points (head, jaw, ears, eyes,
nose, tongue) are authored before the HEAD_DY move-back; the kit adds HEAD_DY.

Build it:  python3 art-source/blender/dogs/kit/build_breed.py chihuahua
"""

# ----------------------------------------------------------------------------- names
NAME = "dog_chihuahua"                    # object and file name stem (dog_chihuahua_body, dog_chihuahua_default.gltf)
DISPLAY_NAME = "Chihuahua"
STATS_NAME = "Chihuahua (adult) concept, round 1"
MOTION_STATUS = "Chihuahua motion clips, first pass (concept)"
MATERIAL_NAMES = ["DH_Dog_Fur_%s", "DH_Dog_Gloss", "DH_Dog_Soft"]   # fur (per coat), gloss, soft
# Chihuahua: a short, smooth, glossy coat, close to the Lab's (0.60, 0.42)
MATERIAL_SHADING = {"fur": (0.62, 0.40), "gloss": (0.30, 0.35), "soft": (0.55, 0.35)}

# ----------------------------------------------------------------------------- budgets
BUDGET = {"full": 4000, "lod1": 1500}     # triangles per file
SKIN_FACES = 1450                         # QuadriFlow target (quads) for the body. Chihuahua: QuadriFlow drops the thin tail at some targets; 1450 keeps it and the budget (README)
LOD_SKIN_TRIS = 820                       # the LOD body: the full retopology decimated to this

# ----------------------------------------------------------------------------- placement
HEAD_DY = 0.0                  # Chihuahua: the head group is authored in place
OY = 0.07                      # origin under the middle of the nose-to-rump span
JAW_REST_OPEN_DEG = 12.0       # the file's default pose opens the jaw this much

# ----------------------------------------------------------------------------- body shape
FIELD_GRID = {"h": 0.0070, "origin": (-0.252, -0.504, -0.021), "extent": (0.504, 0.924, 0.903), "voxel": 0.0060}

# torso + neck: (y, z, half width, top, bottom, exponent)
# Chihuahua: the Beagle's torso mapped down (rings 0-11: y x0.62 - 0.0178, depths x0.62, widths x0.66): compact, a little longer than tall, a
# level topline at about 0.47 m, moderate tuck-up; the neck (rings 12-15) longer and higher,
# slightly arched, so the head is carried up.
TORSO = [
    (0.2729, 0.3798, 0.0416, 0.0322, 0.0372, 2.0),
    (0.2659, 0.3798, 0.1053, 0.0794, 0.0942, 2.2),
    (0.2426, 0.3749, 0.1375, 0.1017, 0.1042, 2.5),
    (0.21, 0.3699, 0.1486, 0.1066, 0.1017, 2.6),
    (0.1775, 0.365, 0.1486, 0.1091, 0.0967, 2.6),
    (0.1263, 0.365, 0.1508, 0.1066, 0.0774, 2.6),
    (0.0706, 0.36, 0.1564, 0.1066, 0.0853, 2.6),
    (0.0101, 0.36, 0.1608, 0.1091, 0.1063, 2.6),
    (-0.0457, 0.36, 0.1619, 0.1116, 0.1548, 2.6),
    (-0.0876, 0.365, 0.158, 0.1116, 0.1597, 2.6),
    (-0.1294, 0.3798, 0.1486, 0.1091, 0.1548, 2.5),
    (-0.155, 0.4046, 0.1353, 0.1042, 0.1369, 2.4),
    (-0.178, 0.452, 0.118, 0.098, 0.108, 2.3),
    (-0.207, 0.503, 0.11, 0.09, 0.094, 2.2),
    (-0.236, 0.552, 0.1, 0.082, 0.082, 2.1),
    (-0.252, 0.598, 0.088, 0.072, 0.072, 2.0),
]

# head with the lower jaw: (y, z, half width, top, bottom, exponent)
# Chihuahua: the apple dome - a round skull 0.39 m across and 0.31 m deep, about as long as it is
# wide; a pronounced stop; a short (9 cm), fine muzzle narrowing to a slightly pointed tip. The
# ring centres fall only gently toward the muzzle: a steep fall at the stop folds the loft's
# underside and leaves a fin under the jaw.
HEAD = [
    (-0.14, 0.645, 0.045, 0.065, 0.025, 2.0),
    (-0.158, 0.645, 0.12, 0.138, 0.082, 2.1),
    (-0.19, 0.64, 0.17, 0.184, 0.098, 2.15),
    (-0.235, 0.632, 0.195, 0.21, 0.1, 2.15),
    (-0.282, 0.624, 0.193, 0.211, 0.096, 2.2),
    (-0.322, 0.618, 0.175, 0.188, 0.096, 2.3),
    (-0.352, 0.614, 0.135, 0.118, 0.094, 2.4),
    (-0.372, 0.612, 0.1, 0.066, 0.088, 2.5),
    (-0.398, 0.611, 0.08, 0.051, 0.079, 2.5),
    (-0.422, 0.61, 0.064, 0.042, 0.068, 2.4),
    (-0.44, 0.608, 0.046, 0.034, 0.05, 2.3),
    (-0.452, 0.608, 0.022, 0.018, 0.026, 2.0),
]

# tail: (y, z, half width, top, bottom)
# Chihuahua: thin and tapering (9 cm at the root, 3 cm at the tip; thinner and QuadriFlow drops
# it), about 0.36 m long: it leaves the top of the croup, rises and curves forward in a sickle
# over the back, the tip about 0.2 m above the topline.
TAIL = [
    (0.241, 0.44, 0.044, 0.046, 0.044),
    (0.265, 0.482, 0.041, 0.043, 0.041),
    (0.282, 0.527, 0.038, 0.04, 0.038),
    (0.287, 0.575, 0.035, 0.037, 0.035),
    (0.275, 0.622, 0.031, 0.033, 0.031),
    (0.25, 0.66, 0.027, 0.029, 0.027),
    (0.217, 0.684, 0.023, 0.024, 0.023),
    (0.181, 0.69, 0.02, 0.021, 0.02),
    (0.149, 0.678, 0.016, 0.017, 0.016),
]

# legs: the Beagle's legs x0.69 in height and x0.62 fore/aft (the hind leg x0.87 about the hip),
# widths about x0.5. Chihuahua: slender, straight, fine-boned legs on small dainty feet.
FRONT_X = 0.115
BACK_X = 0.11
BACK_THIGH_DX = 0.0139
FRONT_UPPER = [(-0.0885, 0.355, 0.0419, 0.0463, 0.0506), (-0.1086, 0.346, 0.0433, 0.0489, 0.0467), (-0.0952, 0.2816, 0.0424, 0.0419, 0.0441), (-0.0773, 0.2302, 0.0402, 0.0397, 0.0441)]
FRONT_FORE = [(-0.0742, 0.243, 0.0371, 0.0367, 0.0407), (-0.0787, 0.1873, 0.0347, 0.0339, 0.0347), (-0.0831, 0.1315, 0.0323, 0.0314, 0.0306), (-0.0862, 0.0929, 0.0306, 0.0294, 0.0286)]
FRONT_PASTERN = [(-0.0862, 0.0972, 0.029, 0.0274, 0.0267), (-0.0893, 0.0758, 0.0278, 0.0263, 0.0255), (-0.0952, 0.0587, 0.0282, 0.0267, 0.0251)]
FRONT_PAW = (-0.0639, -0.1688)        # heel y, toe y
BACK_THIGH = [(0.2047, 0.3919, 0.059, 0.0568, 0.0699), (0.1891, 0.3373, 0.0655, 0.0612, 0.0852), (0.1611, 0.2816, 0.0528, 0.0493, 0.0734), (0.136, 0.2387, 0.0424, 0.0402, 0.0533), (0.1266, 0.2173, 0.0375, 0.0354, 0.0397)]
BACK_GASKIN = [(0.1297, 0.2259, 0.0353, 0.034, 0.0403), (0.1641, 0.1873, 0.0319, 0.0277, 0.0403), (0.1986, 0.1487, 0.0282, 0.0239, 0.0315), (0.2203, 0.1251, 0.0261, 0.0227, 0.0294)]
BACK_META = [(0.2216, 0.1315, 0.0236, 0.0209, 0.0263), (0.2154, 0.0929, 0.024, 0.0216, 0.0232), (0.2078, 0.0587, 0.0251, 0.0228, 0.022)]
BACK_PAW = (0.2267, 0.1602)          # heel y, toe y
PAW = {'z': 0.0375, 'half_width': 0.042, 'top': 0.0375, 'bottom': 0.0375, 'e': 2.6, 'rings': 7}

LOFT_STYLE = {"torso_dome": 0.35, "head_dome": 0.6, "tail": (2.0, 0.7), "front_leg": (2.2, 0.3),
              "back_thigh": (2.3, 0.3), "back_leg": (2.2, 0.3), "ear": (1.8, 0.6)}

# Chihuahua: the Beagle's fillets x0.7 (a smaller dog)
FIELD = [
    ("core", 0.0, [("torso", 0.0), ("head", 0.026), ("tail", 0.028)]),
    ("front_L", 0.034, [("front_upper_L", 0.0), ("front_fore_L", 0.020), ("front_pastern_L", 0.014), ("front_paw_L", 0.016)]),
    ("front_R", 0.034, [("front_upper_R", 0.0), ("front_fore_R", 0.020), ("front_pastern_R", 0.014), ("front_paw_R", 0.016)]),
    ("back_L", 0.028, [("back_thigh_L", 0.0), ("back_gaskin_L", 0.022), ("back_meta_L", 0.016), ("back_paw_L", 0.016)]),
    ("back_R", 0.028, [("back_thigh_R", 0.0), ("back_gaskin_R", 0.022), ("back_meta_R", 0.016), ("back_paw_R", 0.016)]),
]

# ----------------------------------------------------------------------------- muzzle and mouth
# Chihuahua: the Golden's mouth, scaled onto the short muzzle (the lip line keeps its slope)
HINGE = (-0.346, 0.585)
LIP_RUN = 0.096
LIP_DROP = 0.019
LIP_BAND = 0.0072
MOUTH_MAX_OPEN_DEG = 34.0
MOUTH_ZONE = {"below_hinge": 0.070, "behind_hinge": 0.040}
JAW_TIP = (-0.425, 0.556)

TONGUE = {
    "pivot": (-0.352, 0.590),
    "rings": [(-0.352, 0.0056, 0.0302, 0.008), (-0.3742, 0.0074, 0.0409, 0.0101), (-0.401, 0.0083, 0.0429, 0.0106), (-0.4254, 0.0074, 0.0409, 0.0096), (-0.4398, -0.0056, 0.0341, 0.008), (-0.446, -0.024, 0.0253, 0.0071)],
    "lod_rings": (0, 2, 4, 5),
    "e": 2.6,
}

# ----------------------------------------------------------------------------- ears, eyes, nose, collar
# Chihuahua: big erect ears, 0.19 m root to tip and 0.18 m across at the base, set high on the
# dome and flaring out at about 45 deg, opening forward and out (the German Shepherd's "side" /
# "swing" keys: the ear's width runs across, and the clips lay it back rather than swing it).
EARS = {
    "rings": [(0.105, -0.238, 0.795, 0.082, 0.024), (0.1276, -0.2346, 0.8204, 0.09, 0.021), (0.1553, -0.2304, 0.8514, 0.078, 0.018), (0.183, -0.2262, 0.8825, 0.057, 0.014), (0.2094, -0.2222, 0.9121, 0.033, 0.01), (0.2308, -0.2189, 0.9361, 0.01, 0.007)],
    "lod_rings": (0, 1, 3, 4, 5),
    "bones": [(0.105, -0.238, 0.795), (0.1553, -0.2304, 0.8514), (0.2308, -0.2189, 0.9361)],
    "side": (0.72, 0.45, -0.68),
    "swing": (-0.7, -1.0, 1.0),
}
# Chihuahua: very large, round, slightly prominent eyes, set wide
EYES = {"ray_x": 0.106, "ray_z": 0.700, "face_bias": 0.6, "inset": 0.008, "radii": (0.045, 0.029, 0.049),
        "shine_offset": (0.022, 0.023, 0.013), "shine_radii": (0.0105, 0.009, 0.0105)}
NOSE = {"ray_z": 0.618, "offset": (0.004, 0.002), "length": 0.016, "radii": (0.040, 0.026, 0.020), "e": 2.5}
# Chihuahua: the collar strap and tag scaled down (x0.7)
COLLAR = {"ring": 13, "gap": 0.0085, "gap_lod": 0.013, "strap": (0.021, 0.0078), "e": 3.0,
          "tag_radii": (0.024, 0.005), "tag_out": 0.012, "tag_drop": 0.025}

# ----------------------------------------------------------------------------- skeleton
SPINE = {
    'hips': ((0.1775, 0.36), (0.2612, 0.3798)),
    'spine_1': ((0.1775, 0.36), (0.0427, 0.36)),
    'spine_2': ((0.0427, 0.36), (-0.0968, 0.3699)),
    'spine_3': ((-0.0968, 0.3699), (-0.178, 0.452)),
    'neck': ((-0.178, 0.452), (-0.24, 0.572)),
    'head': ((-0.24, 0.572), (-0.35, 0.655)),
}
# Chihuahua: the tail bones follow the sickle
TAIL_BONES = [(0.241, 0.44), (0.282, 0.527), (0.275, 0.622), (0.217, 0.684), (0.149, 0.678)]
JOINTS = {
    'front_shoulder': (-0.1108, 0.3588),
    'front_elbow': (-0.076, 0.2302),
    'front_wrist': (-0.0867, 0.0887),
    'front_paw': (-0.0974, 0.044),
    'front_toe': (-0.1375, 0.0264),
    'back_hip': (0.1891, 0.3588),
    'back_stifle': (0.1297, 0.2216),
    'back_hock': (0.2235, 0.1251),
    'back_foot': (0.206, 0.0499),
    'back_toe': (0.1335, 0.0264),
}
TORSO_CHAIN = [(0.2612, 0.3798), (0.1775, 0.36), (0.0427, 0.36), (-0.0968, 0.3699), (-0.178, 0.452), (-0.178, 0.572)]

# ----------------------------------------------------------------------------- sit pose
SIT = {"hips": -54.0199, "drop": -0.235, "shoulder": -8.0, "stifle_z": 0.055, "hock_z": -0.0085, "foot_pitch": 37.8667, "tail": 178.5769}
SIT_POSE = {'spine_3': 4, 'neck': 20, 'head': 18, 'tail_2': -30, 'tail_3': -30}

# ----------------------------------------------------------------------------- lie pose (Rest)
# Solved against the skinned mesh by kit/solve_lie.py (paste its output here after changing the
# shape or the rig). The hip tilt, the stifle height and tail_2 / tail_3 are chosen; the rest are
# solved.
LIE = {"hips": 0.0, "drop": -0.2074, "elbow_z": 0.0369, "wrist_z": 0.0386, "stifle_z": 0.055, "hock_z": -0.0085, "foot_pitch": 37.8667, "tail": -116.1646, "tail_2": -30, "tail_3": -30}

# ----------------------------------------------------------------------------- checks
# Chihuahua: the sickle tail's tip comes forward over the back, so tail weights may reach further
# forward than the hips
SANITY = {"head_min_z": 0.40, "tail_min_y": 0.15}
# Chihuahua: the back is measured behind the neck and ahead of the tail
MEASURE = {"rump_min_abs_x": 0.050, "back_y": (0.0, 0.20)}

# ----------------------------------------------------------------------------- coat
# sRGB hex: main coat, "feathering" (the paler underside: chest, belly, inner legs), dark channel
# (with FUR_PATCHES the top of the dog: the back, head, ears, top of the muzzle and tail). The keys
# are the game's coat slots: Default = fawn (the dark channel a slightly deeper fawn, so the top
# is shaded and the points are paler); Black = black and tan (the dark channel black, the points
# tan).
COATS = {
    "default": ('#D8AA6F', '#EEDCBC', '#C99760'),
    "black":   ('#B8743C', '#C98B52', '#1D1917'),
}
COAT_LABELS = {"default": "Default (fawn)", "black": "Black (black and tan)"}
FUR_FLOW = {
    "body": ((0.0, 1.0, -0.25), ("body", "head")),
    "leg": ((0.0, 0.15, -1.0), ("fleg", "bleg", "thigh", "paw")),
    "tail": ((0.0, 0.0, 1.0), ("tail",)),
    "ear": ((0.0, 0.0, 1.0), ("ear",)),
}
# Chihuahua: the ears are the dark channel to the tip (ear_tip_z is above any ear)
FUR_MARKINGS = {"bib_y": (-0.06, -0.14), "rump_y": (0.26, 0.31), "muzzle_y": (-0.37, -0.40),
                "crown_y": (-0.24, -0.18), "pale_z": (0.05, 0.13), "tail_tip_y": (0.30, 0.34),
                "ear_tip_z": (2.5, 2.6)}
# Chihuahua: the Lab's short, smooth coat
FUR_STRAND = {"fine": (0.07, 0.30), "mid": (0.16, 0.45), "clump": (0.40, 0.7), "low": (5.0, 7.0),
              "mix": (0.60, 0.35, 0.10), "normal": 0.16, "contrast": 0.20}
# Chihuahua: the black-and-tan pattern over the dark channel (kit FUR_PATCHES; the fawn coat
# shows it as soft shading). Tan points: eyebrow pips, cheeks and the sides of the muzzle, the
# chest, the lower legs and the underside of the tail.
FUR_PATCHES = [
    {'regions': ('body', 'thigh'), 'line': [(-0.24, 0.44), (-0.17, 0.36), (-0.08, 0.29), (0.05, 0.27), (0.18, 0.27), (0.26, 0.29), (0.33, 0.32)], 'soft': 0.02, 'wobble': 0.012},
    {'regions': ('body',), 'op': 'light', 'n': ('y', -0.35, -0.75), 'y': (-0.1, -0.15), 'soft': 0.02},
    {'regions': ('fleg',), 'z': (0.2, 0.26)},
    {'regions': ('bleg',), 'z': (0.2, 0.26), 'n': ('y', 0.3, -0.2)},
    {'regions': ('tail',)},
    {'regions': ('tail',), 'op': 'light', 'n': ('y', 0.35, 0.8), 'z': (0.52, 0.46)},
    {'regions': ('head',)},
    {'regions': ('head',), 'op': 'light', 'line': [(-0.46, 0.615), (-0.38, 0.64), (-0.33, 0.665), (-0.26, 0.64), (-0.16, 0.6)], 'above': False, 'soft': 0.014},
    {'regions': ('head',), 'y': (-0.355, -0.37), 'n': ('z', 0.45, 0.75)},
    {'regions': ('head',), 'op': 'light', 'y': (-0.29, -0.31), 'z': (0.745, 0.755), 'line': [(-0.4, 0.79), (-0.2, 0.79)], 'above': False, 'soft': 0.014},
    {'regions': ('head',), 'x_max': 0.04, 'z': (0.7, 0.72), 'soft': 0.018},
    {'regions': ('head',), 'n': ('x', 0.55, 0.72), 'z': (0.7, 0.72)},
    {'regions': ('head',), 'n': ('x', -0.55, -0.72), 'z': (0.7, 0.72)},
    {'regions': ('ear',), 'op': 'light', 'n': ('y', -0.25, -0.7), 'strength': 0.45},
]
TRIM_COLOURS = {"eye": "#1A1210", "iris": "#3E2416", "pupil": "#120C0A", "shine": "#FFFFFF", "nose": "#2A2220",
                "tag": "#D6AC45", "mouth": "#5E2B2C", "lip": "#2C2120", "tongue": "#E7848A",
                "collar": "#C3282E", "stitch": "#E8B0A8"}

# ----------------------------------------------------------------------------- motion
# Chihuahua: a small dog takes quick short steps. Strides are about the legs' scale of the
# Beagle's (legs x0.69) on shorter cycles; lifts and stance offsets scaled with the legs.
GAITS = {
    "Walk": {'frames': 22, 'speed': 0.5, 'phase': {'HL': 0.0, 'FL': 0.22, 'HR': 0.5, 'FR': 0.72}, 'duty': {'HL': 0.62, 'HR': 0.62, 'FL': 0.64, 'FR': 0.64}, 'lift': {'F': 0.038, 'H': 0.031}, 'roll': {'F': (28.0, 0.22), 'H': (24.0, 0.22)}, 'curl': {'F': 40.0, 'H': 18.0}, 'fold': {'F': 30.0, 'H': -14.0}, 'center': {'F': -0.01, 'H': 0.0}, 'win': 1, 'sigma': 1.2, 'k': {'F': 0.85, 'H': 0.45}, 'notes': 'four-beat lateral-sequence walk; touchdowns LH 0.00, LF 0.22, RH 0.50, RF 0.72 of the cycle; duty 0.62 hind / 0.64 fore'},
    "Run": {'frames': 12, 'speed': 2.1, 'phase': {'HR': 0.0, 'HL': 0.11, 'FR': 0.4, 'FL': 0.5}, 'duty': {'HR': 0.3, 'HL': 0.3, 'FR': 0.3, 'FL': 0.3}, 'lift': {'F': 0.076, 'H': 0.059}, 'roll': {'F': (35.0, 0.3), 'H': (30.0, 0.3)}, 'curl': {'F': 75.0, 'H': 30.0}, 'fold': {'F': 55.0, 'H': -25.0}, 'center': {'F': -0.028, 'H': -0.045}, 'suspension': (0.017, 0.9, 0.3), 'win': 1, 'sigma': 1.0, 'k': {'F': 0.9, 'H': 0.6}, 'land': 0.985, 'reach': {'F': 0.965, 'H': 0.95}, 'notes': 'transverse gallop, right-hind lead: touchdowns RH 0.00, LH 0.11, RF 0.40, LF 0.50; duty 0.30; gathered suspension 0.80-1.00'},
}
# Chihuahua: rock_radius = -SIT["drop"] / sin(-SIT["hips"]), so the hips reach the sit without sinking
# below it on the way down (the Beagle's rule gave about the same); steps scaled with the legs
SIT_DOWN = {'rock_radius': 0.29, 'dip': 0.012, 'hind_step_lift': 0.017, 'front_step_lift': 0.025}
PLAY_BOW = {"elbow_z": 0.068, "wrist_z": 0.056}

# ----------------------------------------------------------------------------- review renders
RENDER = {
    'head_box': ((-0.3, -0.44, 0.44), (0.3, -0.02, 1.0)),
    'mouth_box': ((-0.1, -0.42, 0.5), (0.1, -0.25, 0.68)),
    'hips_box': {'rest': ((-0.22, 0.02, 0.0), (0.22, 0.46, 0.78)), 'sit': ((-0.22, -0.08, 0.0), (0.22, 0.66, 0.48))},
    'body_box': ((-0.26, -0.44, 0.0), (0.26, 0.44, 1.0)),
    'chest_box': ((-0.26, -0.42, 0.0), (0.26, 0.14, 1.0)),
    'chest_cut_y': -0.04,
    'join_views': [('Shoulder', 'front_shoulder_L', (0.01, 0.028, -0.063), (0.133, 0.161, 0.189), -72, 6), ('Hip / haunch', 'back_hip_L', (0.01, 0.014, -0.084), (0.133, 0.175, 0.203), -118, 8), ('Tail base', 'tail_1', (0.0, 0.014, 0.03), (0.13, 0.15, 0.17), -150, 24)],
    'sit_closeups': {'hind': ('back_hock_L', 0.31, 0.29), 'front': ('front_paw_L', 0.24, 0.21)},
    'before': {},
    'captions': {'turnaround': ('Chihuahua - turnaround (orthographic)', 'Round 1 on the shared kit: the smallest dog - apple-dome head, short fine muzzle, big round eyes, large erect ears, fine legs, sickle tail over the back. Fawn (Default).'), 'gamecam': ('Chihuahua at the game camera', 'Orthographic, 45 deg pitch, yaw -40. Approved base 1 (level 1), mailbox and hydrant for scale.'), 'face': ('Chihuahua - face, 3/4', "Apple-dome skull, pronounced stop, short fine muzzle; very large round eyes set wide; big erect ears flaring out; the Golden's mouth; jaw open 12 deg."), 'coats': ('Coats and puppy', 'Default (fawn) and Black (black and tan). One mesh: a coat is a fur colour map; normal map shared. Puppy = adult x 0.55.'), 'splash': ('3D concept vs splash', 'No Chihuahua splash art yet.'), 'mouth': ('Mouth', "The Golden's mouth on the short muzzle: lips meet at the corners on the hinge."), 'mouth_rows': ('Open (default pose, jaw 12 deg)', 'Closed (jaw bone at 0, tongue hidden)'), 'hips': ('Hip study - hind leg (skinned), sit', 'Red dots = joints: hips, hip, stifle, hock, foot (both legs), tail base.'), 'hips_notes': ["The Beagle's hind-leg build, finer and in proportion to a smaller dog: thigh, stifle, lower thigh back to the hock,", 'and a short rear foot. The thin sickle tail leaves the top of the croup.', 'Sit: the hips drop and tilt so the rump rests on the ground; hock and rear foot lie flat, front paws stay planted.', 'Contact is measured on the skinned mesh and checked at export (see sit.png).'], 'joins': ('Joins - shoulder, hip / haunch, tail base', 'One skin: the volumes blend with a smooth minimum, then retopologise.'), 'joins_rows': ('Rest', 'Bent (sit): the junctions stay smooth'), 'sit': ('Sit - ground contact', 'Side views from a level camera at ground height; the dark line is the ground plane.'), 'chest': ('Chest', 'Orthographic: front, front section, side, 3/4.'), 'chest_row': 'Chihuahua: a compact chest on fine, straight legs'},
}
