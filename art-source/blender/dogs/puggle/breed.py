"""Puggle: the breed definition for the shared dog kit (../kit/).

Data only: numbers, strings, tuples, lists and dicts. No imports and no Blender calls; the kit
checks that when it loads this file (kit/dh_breed.py). Started from the Beagle's breed.py (the
closest build: a small dog with short legs and hanging ears; ../golden-retriever/breed.py
documents every field); the values that make a Puggle a Puggle are marked "Puggle:".

What changes from the Beagle:
  - size: about the Beagle's size, a little smaller and stockier. The body and legs are mapped
    from the Beagle's about y 0.10 and the paw centre height (z 0.054): lengths x0.92, heights
    x0.90; the torso is x1.12 as wide and x0.96 as deep (so relatively deeper: a broad chest and
    a chunky body), the legs x1.06 as wide and set x1.10 wider apart;
  - head: rounder than the Beagle's (a fuller, rounder skull) with a short muzzle: the muzzle in
    front of the stop is x0.70 the Beagle's (longer than a Pug's, shorter than a Beagle's); the head
    group moves with the neck top and is carried a little higher;
  - ears: medium, soft and hanging, x0.62 the Beagle's length, set a little higher, darker;
  - tail: curls up and forward over the back in a loose loop (not a Pug's tight curl), clear of
    the back; carried in the sit, not laid on the ground;
  - coat: short and smooth (the Lab's / Beagle's FUR_STRAND); Default = fawn with a black mask on
    the muzzle, darker ears, a slightly paler underside and soft brow wrinkles (FUR_PATCHES).
Kept: the mouth (lips wrap the corners), the one continuous skin, the rig, the collar and a sit
that rests on the ground.

Conventions: metres, Blender axes (front -Y, up +Z, the dog's left +X), authored coordinates
(before the OY shift that puts the origin mid-span). Head-group points (head, jaw, ears, eyes,
nose, tongue) are authored before the HEAD_DY move-back; the kit adds HEAD_DY.

Build it:  python3 art-source/blender/dogs/kit/build_breed.py puggle
"""

# ----------------------------------------------------------------------------- names
NAME = "dog_puggle"                       # object and file name stem (dog_puggle_body, dog_puggle_default.gltf)
DISPLAY_NAME = "Puggle"
STATS_NAME = "Puggle (adult) concept, round 1"
MOTION_STATUS = "Puggle motion clips, first pass (concept)"
MATERIAL_NAMES = ["DH_Dog_Fur_%s", "DH_Dog_Gloss", "DH_Dog_Soft"]   # fur (per coat), gloss, soft
# Puggle: a short, smooth coat with a soft sheen (the Beagle's)
MATERIAL_SHADING = {"fur": (0.64, 0.38), "gloss": (0.30, 0.35), "soft": (0.55, 0.35)}

# ----------------------------------------------------------------------------- budgets
BUDGET = {"full": 4000, "lod1": 1500}     # triangles per file
SKIN_FACES = 1490                         # QuadriFlow target (quads) for the body
LOD_SKIN_TRIS = 820                       # the LOD body: the full retopology decimated to this

# ----------------------------------------------------------------------------- placement
HEAD_DY = 0.0294               # the Beagle's
OY = 0.07                      # origin under the middle of the nose-to-rump span
JAW_REST_OPEN_DEG = 12.0       # the file's default pose opens the jaw this much

# ----------------------------------------------------------------------------- body shape
FIELD_GRID = {"h": 0.010, "origin": (-0.40, -0.66, -0.02), "extent": (0.80, 1.30, 1.20), "voxel": 0.0068}

# torso + neck: (y, z, half width, top, bottom, exponent)
# Puggle: the Beagle's torso mapped (y x0.92, z x0.90 about the paw centre), x1.12 as wide and
# x0.96 as deep: compact and chunky, a broad, deep chest, a level topline at about 0.66 m.
TORSO = [
    (0.4393, 0.5022, 0.0706, 0.0499, 0.0576, 2.0),
    (0.4289, 0.5022, 0.1788, 0.1229, 0.1459, 2.2),
    (0.3944, 0.4950, 0.2333, 0.1574, 0.1613, 2.5),
    (0.3461, 0.4878, 0.2521, 0.1651, 0.1574, 2.6),
    (0.2978, 0.4806, 0.2521, 0.1690, 0.1498, 2.6),    # hip loop
    (0.2219, 0.4806, 0.2559, 0.1651, 0.1498, 2.6),
    (0.1391, 0.4734, 0.2653, 0.1651, 0.1651, 2.6),    # loin
    (0.0494, 0.4734, 0.2728, 0.1690, 0.2058, 2.6),
    (-0.0334, 0.4734, 0.2747, 0.1728, 0.2396, 2.6),   # ribcage, deep
    (-0.0955, 0.4806, 0.2681, 0.1728, 0.2473, 2.6),   # shoulder loop
    (-0.1576, 0.5022, 0.2521, 0.1690, 0.2396, 2.5),
    (-0.1956, 0.5382, 0.2296, 0.1613, 0.2120, 2.4),   # front of chest, broad
    (-0.2197, 0.5886, 0.2202, 0.1536, 0.1859, 2.3),   # neck base
    (-0.2404, 0.6344, 0.2070, 0.1421, 0.1574, 2.2),   # collar loop
    (-0.2507, 0.6802, 0.1882, 0.1267, 0.1344, 2.1),
    (-0.2577, 0.7269, 0.1637, 0.1114, 0.1152, 2.0),   # neck top, inside the skull
]

# head with the lower jaw: (y, z, half width, top, bottom, exponent)
# Puggle: a rounder skull than the Beagle's (fuller crown, rounder section), a moderate stop and a
# short, broad muzzle (x0.70 the Beagle's in front of the stop), a little flew.
HEAD = [
    (-0.0981, 0.8703, 0.063, 0.0588, 0.0588, 2.0),
    (-0.1233, 0.8787, 0.176, 0.1633, 0.1428, 2.0),
    (-0.1736, 0.8846, 0.2533, 0.2177, 0.1806, 2.05),
    (-0.2451, 0.8871, 0.2778, 0.2431, 0.2058, 2.05),
    (-0.3207, 0.8829, 0.2726, 0.2340, 0.2142, 2.1),
    (-0.3837, 0.8686, 0.2446, 0.1996, 0.2201, 2.2),
    (-0.4315, 0.8426, 0.1992, 0.1361, 0.2201, 2.35),   # the stop
    (-0.4609, 0.8157, 0.1747, 0.0907, 0.2117, 2.4),
    (-0.5021, 0.8056, 0.1660, 0.0840, 0.2000, 2.35),   # muzzle: short, broad, soft-cornered
    (-0.5432, 0.8006, 0.1480, 0.0806, 0.1780, 2.3),
    (-0.5726, 0.7972, 0.1060, 0.0622, 0.1250, 2.2),
    (-0.5861, 0.7972, 0.0506, 0.0336, 0.0554, 2.0),
]

# tail: (y, z, half width, top, bottom)
# Puggle: a loose curl up and forward over the back: it leaves the top of the croup rising
# steeply, arcs over (top about 0.96 m) and comes forward and down, the tip about 8 cm above the
# back (clear of it in every clip). Thick at the root, tapering.
TAIL = [
    (0.3930, 0.5950, 0.066, 0.066, 0.066),
    (0.4320, 0.6650, 0.064, 0.064, 0.064),
    (0.4520, 0.7450, 0.062, 0.062, 0.062),
    (0.4370, 0.8250, 0.059, 0.059, 0.059),
    (0.3920, 0.8820, 0.056, 0.056, 0.056),
    (0.3300, 0.9050, 0.053, 0.053, 0.053),
    (0.2680, 0.8930, 0.049, 0.049, 0.049),
    (0.2220, 0.8580, 0.044, 0.044, 0.044),
    (0.1980, 0.8120, 0.037, 0.037, 0.037),
    (0.1920, 0.7700, 0.026, 0.026, 0.026),
]

# legs: the Beagle's legs mapped with the body (y x0.92, z x0.90), widths x1.06 and set x1.10
# wider apart. Puggle: short, sturdy legs under a broad chest.
FRONT_X = 0.159
BACK_X = 0.1718
BACK_THIGH_DX = 0.021
FRONT_UPPER = [(-0.0969, 0.5598, 0.0854, 0.0854, 0.0935), (-0.1267, 0.4567, 0.0882, 0.0903, 0.0863),
               (-0.1068, 0.3727, 0.0864, 0.0774, 0.0814), (-0.0803, 0.3057, 0.0819, 0.0733, 0.0814)]
FRONT_FORE = [(-0.0757, 0.3224, 0.0819, 0.0733, 0.0814), (-0.0823, 0.2497, 0.0765, 0.0678, 0.0693),
              (-0.0890, 0.1770, 0.0712, 0.0629, 0.0612), (-0.0936, 0.1267, 0.0676, 0.0588, 0.0572)]
FRONT_PASTERN = [(-0.0936, 0.1323, 0.0668, 0.0572, 0.0557), (-0.0982, 0.1043, 0.0641, 0.0548, 0.0532),
                 (-0.1068, 0.0819, 0.0650, 0.0557, 0.0524)]
FRONT_PAW = (-0.0604, -0.2161)        # heel y, toe y
BACK_THIGH = [(0.3316, 0.5166, 0.1202, 0.1048, 0.1290), (0.3150, 0.4455, 0.1336, 0.1129, 0.1572),
              (0.2853, 0.3727, 0.1077, 0.0911, 0.1355), (0.2587, 0.3168, 0.0864, 0.0742, 0.0984),
              (0.2488, 0.2888, 0.0765, 0.0653, 0.0733)]
BACK_GASKIN = [(0.2521, 0.3001, 0.0748, 0.0653, 0.0774), (0.2885, 0.2497, 0.0676, 0.0532, 0.0774),
               (0.3250, 0.1994, 0.0597, 0.0460, 0.0605), (0.3481, 0.1686, 0.0552, 0.0436, 0.0564)]
BACK_META = [(0.3495, 0.1770, 0.0543, 0.0436, 0.0548), (0.3429, 0.1267, 0.0552, 0.0451, 0.0484),
             (0.3349, 0.0819, 0.0579, 0.0476, 0.0460)]
BACK_PAW = (0.3548, 0.1958)           # heel y, toe y
PAW = {"z": 0.0544, "half_width": 0.086, "top": 0.0544, "bottom": 0.0544, "e": 2.6, "rings": 7}

LOFT_STYLE = {"torso_dome": 0.35, "head_dome": 0.5, "tail": (2.0, 0.7), "front_leg": (2.2, 0.3),
              "back_thigh": (2.3, 0.3), "back_leg": (2.2, 0.3), "ear": (2.0, 0.9)}

FIELD = [
    ("core", 0.0, [("torso", 0.0), ("head", 0.036), ("tail", 0.04)]),
    ("front_L", 0.048, [("front_upper_L", 0.0), ("front_fore_L", 0.028), ("front_pastern_L", 0.02), ("front_paw_L", 0.024)]),
    ("front_R", 0.048, [("front_upper_R", 0.0), ("front_fore_R", 0.028), ("front_pastern_R", 0.02), ("front_paw_R", 0.024)]),
    ("back_L", 0.04, [("back_thigh_L", 0.0), ("back_gaskin_L", 0.032), ("back_meta_L", 0.024), ("back_paw_L", 0.024)]),
    ("back_R", 0.04, [("back_thigh_R", 0.0), ("back_gaskin_R", 0.032), ("back_meta_R", 0.024), ("back_paw_R", 0.024)]),
]

# ----------------------------------------------------------------------------- muzzle and mouth
# Puggle: the Beagle's mouth moved with the head, on the short muzzle
HINGE = (-0.3795, 0.6897)
LIP_RUN = 0.2621
LIP_DROP = 0.0504
LIP_BAND = 0.0109
MOUTH_MAX_OPEN_DEG = 34.0
MOUTH_ZONE = {"below_hinge": 0.1848, "behind_hinge": 0.084}
JAW_TIP = (-0.5332, 0.6477)

TONGUE = {
    "pivot": (-0.4088, 0.6981),
    "rings": [(-0.4088, 0.0101, 0.0521, 0.0134), (-0.4574, 0.0134, 0.0706, 0.0168), (-0.5079, 0.0151, 0.0739, 0.0176),
              (-0.5539, 0.0134, 0.0706, 0.0160), (-0.5809, -0.0101, 0.0588, 0.0134), (-0.5926, -0.0437, 0.0437, 0.0118)],
    "lod_rings": (0, 2, 4, 5),
    "e": 2.6,
}

# ----------------------------------------------------------------------------- ears, eyes, nose, collar
# Puggle: medium, soft hanging ears - x0.62 the Beagle's length (about 0.23 m), set a little
# higher on the rounder skull, reaching about the mouth corner
EARS = {
    "rings": [(0.2280, -0.2849, 0.9877, 0.0532, 0.0168), (0.2641, -0.3003, 0.9617, 0.0902, 0.0218),
              (0.2860, -0.3129, 0.9070, 0.1124, 0.0235), (0.2935, -0.3227, 0.8445, 0.1183, 0.0235),
              (0.2925, -0.3299, 0.7898, 0.1020, 0.0218), (0.2798, -0.3334, 0.7559, 0.0547, 0.0168)],
    "lod_rings": (0, 1, 3, 4, 5),
    "bones": [(0.2299, -0.2863, 0.9877), (0.2898, -0.3178, 0.8757), (0.2798, -0.3334, 0.7559)],
}
# Puggle: the Beagle's big soft eyes
EYES = {"ray_x": 0.1411, "ray_z": 0.9232, "face_bias": 0.6, "inset": 0.007, "radii": (0.0538, 0.0319, 0.0613),
        "shine_offset": (0.0252, 0.0269, 0.0151), "shine_radii": (0.0118, 0.0101, 0.0118)}
NOSE = {"ray_z": 0.8275, "offset": (0.0101, 0.0050), "length": 0.0336, "radii": (0.0790, 0.0487, 0.0370), "e": 2.5}
COLLAR = {"ring": 13, "gap": 0.012, "gap_lod": 0.019, "strap": (0.030, 0.011), "e": 3.0,
          "tag_radii": (0.034, 0.007), "tag_out": 0.017, "tag_drop": 0.036,
          "ray": 0.3}       # Puggle (kit, optional): the strap's rays start 0.3 m out, inside the curled tail

# Puggle (kit, optional): the clips' tail rotations per world axis. Their lifts (about X) are
# written for a tail carried out behind and would drive the curl into the back, so they are
# dropped to a fifth; the side-to-side wag (about Z) is kept.
TAIL_SWING = (0.2, 1.0, 1.0)

# ----------------------------------------------------------------------------- skeleton
SPINE = {
    "hips": ((0.2978, 0.4734), (0.4220, 0.5022)),
    "spine_1": ((0.2978, 0.4734), (0.0977, 0.4734)),
    "spine_2": ((0.0977, 0.4734), (-0.1093, 0.4878)),
    "spine_3": ((-0.1093, 0.4878), (-0.2232, 0.5886)),
    "neck": ((-0.2232, 0.5886), (-0.2451, 0.7149)),
    "head": ((-0.2451, 0.7149), (-0.4456, 0.8409)),
}
# Puggle: the tail bones follow the curl
TAIL_BONES = [(0.393, 0.595), (0.452, 0.745), (0.392, 0.882), (0.268, 0.893), (0.192, 0.770)]
JOINTS = {
    "front_shoulder": (-0.1300, 0.4734), "front_elbow": (-0.0783, 0.3057), "front_wrist": (-0.0942, 0.1211),
    "front_paw": (-0.1101, 0.0629), "front_toe": (-0.2062, 0.0398),
    "back_hip": (0.3150, 0.4734), "back_stifle": (0.2521, 0.2944), "back_hock": (0.3514, 0.1686),
    "back_foot": (0.3329, 0.0705), "back_toe": (0.2090, 0.0398),
}
TORSO_CHAIN = [(0.4220, 0.5022), (0.2978, 0.4734), (0.0977, 0.4734), (-0.1093, 0.4878), (-0.2232, 0.5886), (-0.2232, 0.6891)]

# ----------------------------------------------------------------------------- sit pose
SIT = {"hips": -43.0212, "drop": -0.29, "shoulder": -8.0, "stifle_z": 0.08, "hock_z": 0.0208, "foot_pitch": 26.9228, "tail": 0.0}
# Puggle: the curled tail is carried in the sit (kit, optional "tail_on_ground"): it turns with the
# hips and stays clear of the ground, so SIT["tail"] is chosen, not solved
SIT_POSE = {"spine_3": 4, "neck": 28, "head": 24, "tail_2": 0, "tail_3": 0, "tail_on_ground": False}

# ----------------------------------------------------------------------------- checks
SANITY = {"head_min_z": 0.48, "tail_min_y": 0.14}
# Puggle: the back is measured behind the skull and ahead of the curled tail
MEASURE = {"rump_min_abs_x": 0.085, "back_y": (0.06, 0.22)}

# ----------------------------------------------------------------------------- coat
# sRGB hex: main coat, "feathering" (the paler underside: chest, belly, leg backs), dark channel
# (the black mask on the muzzle and the darker ears, by FUR_PATCHES). The keys are the game's coat
# slots (Derek, 2026-11-26): Default = fawn with a black mask; Black = all black (a very dark
# charcoal like the Lab's and the Shepherd's black, the mask blending in); Light = apricot / light
# cream fawn, still with the black mask and darker ears. Another coat is one more entry here.
COATS = {
    "default": ("#CFA068", "#E9D3AA", "#2B221D"),
    "black":   ("#2C2927", "#34302D", "#232020"),
    "light":   ("#EBCF9F", "#F6EAD2", "#2B221D"),
}
COAT_LABELS = {"default": "Default (fawn, black mask)", "black": "Black", "light": "Light (apricot, black mask)"}
FUR_FLOW = {
    "body": ((0.0, 1.0, -0.25), ("body", "head")),
    "leg": ((0.0, 0.15, -1.0), ("fleg", "bleg", "thigh", "paw")),
    "tail": ((0.0, 0.25, 1.0), ("tail",)),
    "ear": ((0.0, -0.15, -1.0), ("ear",)),
}
FUR_MARKINGS = {"bib_y": (-0.08, -0.18), "rump_y": (0.345, 0.415), "muzzle_y": (-0.47, -0.52),
                "crown_y": (-0.29, -0.20), "pale_z": (0.068, 0.17), "tail_tip_y": (0.30, 0.26),
                "ear_tip_z": (0.80, 0.76)}
# Puggle: the Beagle's short, smooth coat
FUR_STRAND = {"fine": (0.07, 0.30), "mid": (0.16, 0.45), "clump": (0.40, 0.7), "low": (5.0, 7.0),
              "mix": (0.60, 0.35, 0.10), "normal": 0.16, "contrast": 0.20}
# Puggle: the mask. The muzzle is dark in front of the stop; the ears a dark brown (the kit's dark
# ears lightened a little); soft darker brow wrinkles across the forehead above the eyes.
FUR_PATCHES = [
    # no darker crown (the kit's): a clean fawn skull
    {"regions": ("head",), "op": "light", "strength": 1.0},
    # ears: dark brown rather than black
    {"regions": ("ear",), "op": "light", "strength": 0.32},
    # the black mask: the muzzle in front of the stop, fading up toward the eyes
    {"regions": ("head",), "y": (-0.465, -0.535), "soft": 0.02},
    # brow wrinkles: two soft, darker folds across the front of the skull above the eyes
    {"regions": ("head",), "line": [(-1.0, 0.992), (1.0, 0.992)], "z": (1.016, 1.004), "soft": 0.010,
     "x_max": 0.085, "n": ("y", -0.15, -0.45), "strength": 0.24},
    {"regions": ("head",), "line": [(-1.0, 1.030), (1.0, 1.030)], "z": (1.052, 1.040), "soft": 0.010,
     "x_max": 0.065, "n": ("y", -0.15, -0.45), "strength": 0.20},
]
TRIM_COLOURS = {"eye": "#1C1411", "iris": "#4A2C1A", "pupil": "#140E0B", "shine": "#FFFFFF", "nose": "#24201F",
                "tag": "#D6AC45", "mouth": "#5E2B2C", "lip": "#2C2120", "tongue": "#E7848A",
                "collar": "#C3282E", "stitch": "#E8B0A8"}

# ----------------------------------------------------------------------------- motion
# Puggle: legs 0.90x the Beagle's: strides 0.90x, a touch quicker cycles; lifts and stance
# offsets scaled with the legs.
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
# Puggle: rock_radius is the hip-to-shoulder span (0.42 m); steps scaled with the legs. The curled
# tail simply turns with the hips (kit, optional: no swing-clear lift, no lag), which keeps it
# clear of the back all the way down
SIT_DOWN = {"rock_radius": 0.42, "dip": 0.016, "hind_step_lift": 0.0225, "front_step_lift": 0.032,
            "tail_lift": 0, "tail_lag": 0}
PLAY_BOW = {"elbow_z": 0.088, "wrist_z": 0.073}

# ----------------------------------------------------------------------------- review renders
RENDER = {
    "head_box": ((-0.33, -0.56, 0.56), (0.33, 0.0, 1.17)),
    "mouth_box": ((-0.17, -0.56, 0.60), (0.17, -0.32, 0.80)),
    "hips_box": {"rest": ((-0.36, 0.02, 0.0), (0.36, 0.68, 1.0)), "sit": ((-0.36, -0.12, 0.0), (0.36, 0.95, 0.80))},
    "body_box": ((-0.37, -0.62, 0.0), (0.37, 0.62, 1.16)),
    "chest_box": ((-0.38, -0.58, 0.0), (0.38, 0.20, 1.16)),
    "chest_cut_y": -0.05,
    "join_views": [("Shoulder", "front_shoulder_L", (0.015, 0.04, -0.09), (0.19, 0.23, 0.27), -72, 6),
                   ("Hip / haunch", "back_hip_L", (0.015, 0.02, -0.12), (0.19, 0.25, 0.29), -118, 8),
                   ("Tail base", "tail_1", (0.0, 0.0, 0.08), (0.20, 0.22, 0.24), -150, 24)],
    "sit_closeups": {"hind": ("back_hock_L", 0.42, 0.40), "front": ("front_paw_L", 0.32, 0.28)},
    "before": {},
    "captions": {
        "turnaround": ("Puggle - turnaround (orthographic)",
                       "Round 1 on the shared kit: a small, chunky dog with short legs, a round head, a short "
                       "muzzle with a black mask, medium hanging ears and a tail curled over the back. Fawn (Default)."),
        "gamecam": ("Puggle at the game camera",
                    "Orthographic, 45 deg pitch, yaw -40. Approved base 1 (level 1), mailbox and hydrant for scale."),
        "face": ("Puggle - face, 3/4",
                 "Round skull with soft brow wrinkles; short, broad muzzle with a black mask; big soft eyes; "
                 "medium hanging ears; the Golden's mouth; jaw open 12 deg."),
        "coats": ("Coats and puppy",
                  "Default (fawn, black mask), Black and Light (apricot, black mask). One mesh: a coat is a fur colour map; normal map shared. "
                  "Puppy = adult x 0.55."),
        "splash": ("3D concept vs splash", "No Puggle splash art yet."),
        "mouth": ("Mouth", "The Golden's mouth on the short muzzle: lips meet at the corners on the hinge."),
        "mouth_rows": ("Open (default pose, jaw 12 deg)", "Closed (jaw bone at 0, tongue hidden)"),
        "hips": ("Hip study - hind leg (skinned), sit",
                 "Red dots = joints: hips, hip, stifle, hock, foot (both legs), tail base."),
        "hips_notes": ["The Beagle's hind-leg build, shorter and sturdier: thigh into a rounded haunch, stifle, "
                       "lower thigh back to the hock,",
                       "and a short, near-vertical rear foot. The tail leaves the top of the croup and curls over.",
                       "Sit: the hips drop and tilt so the rump rests on the ground; hock and rear foot lie flat, "
                       "front paws stay planted; the curled tail is carried clear of the ground.",
                       "Contact is measured on the skinned mesh and checked at export (see sit.png)."],
        "joins": ("Joins - shoulder, hip / haunch, tail base",
                  "One skin: the volumes blend with a smooth minimum, then retopologise."),
        "joins_rows": ("Rest", "Bent (sit): the junctions stay smooth"),
        "sit": ("Sit - ground contact",
                "Side views from a level camera at ground height; the dark line is the ground plane."),
        "chest": ("Chest", "Orthographic: front, front section, side, 3/4."),
        "chest_row": "Puggle: a broad, deep chest on short legs",
    },
}
