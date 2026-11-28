"""Labrador Retriever: the breed definition for the shared dog kit (../kit/).

Data only: numbers, strings, tuples, lists and dicts. No imports and no Blender calls; the kit
checks that when it loads this file (kit/dh_breed.py). Started as a copy of the approved Golden
Retriever's breed.py (../golden-retriever/breed.py documents every field); the values that make a
Lab a Lab are marked "Lab:".

What changes from the Golden:
  - head: broader, flatter skull, a defined stop, and a shorter, wider, squarer muzzle;
  - ears: smaller, set a little higher, hanging close to the cheeks;
  - tail: the thick, round, evenly tapering "otter tail", carried level, no plume;
  - body: a little wider and deeper through the ribs and loin (less tuck-up), a thicker neck, and
    slightly heavier legs of the same length;
  - coat: short, dense and smooth (FUR_STRAND: short strands, a flat normal map, a glossier fur
    material) with no feathering; Yellow, Black and Chocolate.
Kept: the mouth (lips wrap the corners), the one continuous skin, the hind-leg build, the rig,
the collar, the eyes and the solved sit that rests on the ground.

Conventions: metres, Blender axes (front -Y, up +Z, the dog's left +X), authored coordinates
(before the OY shift that puts the origin mid-span). Head-group points (head, jaw, ears, eyes,
nose, tongue) are authored before the HEAD_DY move-back; the kit adds HEAD_DY.

Build it:  python3 art-source/blender/dogs/kit/build_breed.py labrador
"""

# ----------------------------------------------------------------------------- names
NAME = "dog_labrador"                     # object and file name stem (dog_labrador_body, dog_labrador_yellow.gltf)
DISPLAY_NAME = "Labrador Retriever"
STATS_NAME = "Labrador Retriever (adult) concept, round 1"
MOTION_STATUS = "Labrador Retriever motion clips, first pass (concept)"
MATERIAL_NAMES = ["DH_Dog_Fur_%s", "DH_Dog_Gloss", "DH_Dog_Soft"]   # fur (per coat), gloss, soft
# (roughness, specular IOR level) per material. Lab: the short, oily coat is glossier than the
# Golden's (0.82, 0.25), so even the black coat shows its form through a soft sheen.
MATERIAL_SHADING = {"fur": (0.60, 0.42), "gloss": (0.30, 0.35), "soft": (0.55, 0.35)}

# ----------------------------------------------------------------------------- budgets
BUDGET = {"full": 4000, "lod1": 1500}     # triangles per file
SKIN_FACES = 1530                         # QuadriFlow target (quads) for the body. Lab: QuadriFlow drops the otter tail at some targets (1500 does); 1530 keeps it (README)
LOD_SKIN_TRIS = 820                       # the LOD body: the full retopology decimated to this

# ----------------------------------------------------------------------------- placement
HEAD_DY = 0.035                # head group moved back by this after building
OY = 0.085                     # Lab: shorter muzzle, so the nose-to-rump midpoint moves back
JAW_REST_OPEN_DEG = 12.0       # the file's default pose opens the jaw this much

# ----------------------------------------------------------------------------- body shape
FIELD_GRID = {"h": 0.012, "origin": (-0.44, -0.92, -0.02), "extent": (0.88, 2.04, 1.62), "voxel": 0.008}

# torso + neck: (y, z, half width, top, bottom, exponent)
# Lab: about 1.2-1.5 cm wider each side, 1.5-2 cm deeper under the ribs, a fuller loin (less
# tuck-up) and a thicker neck; the top line keeps the Golden's height.
TORSO = [
    (0.625, 0.84, 0.075, 0.065, 0.075, 2.0),
    (0.610, 0.84, 0.19, 0.16, 0.19, 2.2),
    (0.560, 0.83, 0.248, 0.205, 0.21, 2.5),
    (0.490, 0.82, 0.268, 0.215, 0.205, 2.6),
    (0.420, 0.81, 0.268, 0.22, 0.195, 2.6),     # hip loop
    (0.310, 0.81, 0.272, 0.215, 0.200, 2.6),
    (0.190, 0.80, 0.282, 0.215, 0.228, 2.6),    # loin, only a little tuck-up
    (0.060, 0.80, 0.285, 0.22, 0.272, 2.6),
    (-0.060, 0.80, 0.285, 0.225, 0.310, 2.6),   # ribcage, deeper
    (-0.150, 0.81, 0.277, 0.225, 0.318, 2.6),   # shoulder loop
    (-0.240, 0.84, 0.262, 0.22, 0.310, 2.5),
    (-0.295, 0.89, 0.240, 0.21, 0.275, 2.4),    # front of chest
    (-0.330, 0.96, 0.232, 0.20, 0.242, 2.3),    # neck base
    (-0.360, 1.04, 0.218, 0.185, 0.205, 2.2),   # collar loop
    (-0.375, 1.12, 0.198, 0.165, 0.175, 2.1),
    (-0.385, 1.20, 0.172, 0.145, 0.15, 2.0),    # neck top, inside the skull
]

# head with the lower jaw: (y, z, half width, top, bottom, exponent)
# Lab: skull 0.64 m across (Golden 0.60) and flatter on top (higher exponent); the forehead drops
# steeply at the stop (y -0.53 to -0.63) onto a level, boxy muzzle that is 3.6 cm shorter and
# about 2.5 cm wider each side than the Golden's.
HEAD = [
    (-0.195, 1.335, 0.075, 0.07, 0.07, 2.0),
    (-0.225, 1.34, 0.20, 0.172, 0.17, 2.1),
    (-0.285, 1.345, 0.29, 0.222, 0.215, 2.3),
    (-0.370, 1.345, 0.320, 0.238, 0.245, 2.4),
    (-0.460, 1.340, 0.316, 0.226, 0.255, 2.4),
    (-0.530, 1.325, 0.288, 0.198, 0.265, 2.4),
    (-0.580, 1.300, 0.240, 0.140, 0.265, 2.5),   # the stop
    (-0.630, 1.268, 0.212, 0.104, 0.250, 2.7),
    (-0.700, 1.258, 0.202, 0.098, 0.232, 2.8),   # muzzle: level and square
    (-0.765, 1.252, 0.182, 0.094, 0.205, 2.8),
    (-0.810, 1.248, 0.130, 0.072, 0.145, 2.4),
    (-0.832, 1.248, 0.058, 0.04, 0.065, 2.0),
]

# tail: (y, z, half width, top, bottom)
# Lab: the otter tail - round in section, thick where it leaves the rump, tapering evenly to a
# point, carried level with a slight droop. No plume (the Golden's is 0.14 m deep at its widest).
TAIL = [
    (0.560, 0.872, 0.082, 0.082, 0.076),
    (0.650, 0.864, 0.080, 0.080, 0.074),
    (0.750, 0.842, 0.074, 0.074, 0.068),
    (0.850, 0.814, 0.064, 0.064, 0.060),
    (0.940, 0.790, 0.050, 0.050, 0.047),
    (1.005, 0.774, 0.034, 0.034, 0.032),
    (1.045, 0.768, 0.016, 0.016, 0.015),
]

# legs: same lengths and joints as the Golden; Lab: a little heavier (about +6 mm half width on
# the forearm and gaskin, +1 cm on the thigh)
FRONT_X = 0.172
BACK_X = 0.186
BACK_THIGH_DX = 0.025
FRONT_UPPER = [(-0.150, 0.92, 0.096, 0.106, 0.116), (-0.195, 0.77, 0.099, 0.112, 0.107),
               (-0.165, 0.62, 0.097, 0.096, 0.101), (-0.125, 0.50, 0.092, 0.091, 0.101)]
FRONT_FORE = [(-0.118, 0.53, 0.092, 0.091, 0.101), (-0.128, 0.40, 0.086, 0.084, 0.086),
              (-0.138, 0.27, 0.080, 0.078, 0.076), (-0.145, 0.18, 0.076, 0.073, 0.071)]
FRONT_PASTERN = [(-0.145, 0.19, 0.075, 0.071, 0.069), (-0.152, 0.14, 0.072, 0.068, 0.066),
                 (-0.165, 0.10, 0.073, 0.069, 0.065)]
FRONT_PAW = (-0.095, -0.330)          # heel y, toe y
BACK_THIGH = [(0.470, 0.86, 0.135, 0.13, 0.16), (0.445, 0.75, 0.150, 0.14, 0.195),
              (0.400, 0.62, 0.121, 0.113, 0.168), (0.360, 0.52, 0.097, 0.092, 0.122),
              (0.345, 0.47, 0.086, 0.081, 0.091)]
BACK_GASKIN = [(0.350, 0.49, 0.084, 0.081, 0.096), (0.405, 0.40, 0.076, 0.066, 0.096),
               (0.460, 0.31, 0.067, 0.057, 0.075), (0.495, 0.255, 0.062, 0.054, 0.070)]
BACK_META = [(0.497, 0.27, 0.061, 0.054, 0.068), (0.487, 0.18, 0.062, 0.056, 0.060),
             (0.475, 0.10, 0.065, 0.059, 0.057)]
BACK_PAW = (0.505, 0.265)             # heel y, toe y
PAW = {"z": 0.064, "half_width": 0.100, "top": 0.064, "bottom": 0.064, "e": 2.6, "rings": 7}

LOFT_STYLE = {"torso_dome": 0.35, "head_dome": 0.5, "tail": (2.0, 0.7), "front_leg": (2.2, 0.3),
              "back_thigh": (2.3, 0.3), "back_leg": (2.2, 0.3), "ear": (2.2, 0.8)}

# Lab: a wider fillet where the thick tail leaves the rump
FIELD = [
    ("core", 0.0, [("torso", 0.0), ("head", 0.045), ("tail", 0.06)]),
    ("front_L", 0.06, [("front_upper_L", 0.0), ("front_fore_L", 0.035), ("front_pastern_L", 0.025), ("front_paw_L", 0.03)]),
    ("front_R", 0.06, [("front_upper_R", 0.0), ("front_fore_R", 0.035), ("front_pastern_R", 0.025), ("front_paw_R", 0.03)]),
    ("back_L", 0.05, [("back_thigh_L", 0.0), ("back_gaskin_L", 0.04), ("back_meta_L", 0.03), ("back_paw_L", 0.03)]),
    ("back_R", 0.05, [("back_thigh_R", 0.0), ("back_gaskin_R", 0.04), ("back_meta_R", 0.03), ("back_paw_R", 0.03)]),
]

# ----------------------------------------------------------------------------- muzzle and mouth
# Lab: the same mouth as the Golden's on a muzzle 3.6 cm shorter
HINGE = (-0.53, 1.12)
LIP_RUN = 0.297
LIP_DROP = 0.055
LIP_BAND = 0.013
MOUTH_MAX_OPEN_DEG = 34.0
MOUTH_ZONE = {"below_hinge": 0.22, "behind_hinge": 0.10}
JAW_TIP = (-0.75, 1.07)

TONGUE = {
    "pivot": (-0.565, 1.13),
    "rings": [(-0.565, 0.012, 0.062, 0.016), (-0.633, 0.016, 0.084, 0.020), (-0.715, 0.018, 0.088, 0.021),
              (-0.787, 0.016, 0.084, 0.019), (-0.828, -0.012, 0.070, 0.016), (-0.846, -0.052, 0.052, 0.014)],
    "lod_rings": (0, 2, 4, 5),
    "e": 2.6,
}

# ----------------------------------------------------------------------------- ears, eyes, nose, collar
# Lab: ears 0.26 m long (Golden 0.385) and 0.21 m wide (0.27), rooted a little higher on the
# broader skull and hanging close to the cheek instead of swinging out
EARS = {
    "rings": [(0.272, -0.408, 1.505, 0.062, 0.020), (0.312, -0.425, 1.470, 0.094, 0.026),
              (0.336, -0.443, 1.410, 0.104, 0.028), (0.340, -0.455, 1.340, 0.098, 0.028),
              (0.330, -0.462, 1.285, 0.074, 0.024), (0.318, -0.466, 1.250, 0.036, 0.017)],
    "lod_rings": (0, 1, 3, 4, 5),
    "bones": [(0.274, -0.408, 1.505), (0.340, -0.455, 1.36), (0.318, -0.466, 1.250)],
}
# Lab: eyes a touch wider apart and higher, on the face of the stop
EYES = {"ray_x": 0.160, "ray_z": 1.400, "face_bias": 0.6, "inset": 0.008, "radii": (0.056, 0.033, 0.064),
        "shine_offset": (0.026, 0.028, 0.016), "shine_radii": (0.012, 0.010, 0.012)}
# Lab: a broader nose on the square muzzle
NOSE = {"ray_z": 1.285, "offset": (0.012, 0.006), "length": 0.040, "radii": (0.092, 0.056, 0.044), "e": 2.5}
COLLAR = {"ring": 13, "gap": 0.014, "gap_lod": 0.022, "strap": (0.034, 0.012), "e": 3.0,
          "tag_radii": (0.038, 0.008), "tag_out": 0.020, "tag_drop": 0.042}

# ----------------------------------------------------------------------------- skeleton
SPINE = {
    "hips": ((0.42, 0.80), (0.60, 0.84)),
    "spine_1": ((0.42, 0.80), (0.13, 0.80)),
    "spine_2": ((0.13, 0.80), (-0.17, 0.82)),
    "spine_3": ((-0.17, 0.82), (-0.335, 0.96)),
    "neck": ((-0.335, 0.96), (-0.37, 1.15)),
    "head": ((-0.37, 1.15), (-0.62, 1.30)),
}
# Lab: the tail bones follow the level otter tail
TAIL_BONES = [(0.58, 0.872), (0.69, 0.857), (0.80, 0.826), (0.91, 0.796), (1.045, 0.768)]
JOINTS = {
    "front_shoulder": (-0.200, 0.80), "front_elbow": (-0.122, 0.50), "front_wrist": (-0.146, 0.17),
    "front_paw": (-0.170, 0.075), "front_toe": (-0.315, 0.045),
    "back_hip": (0.445, 0.80), "back_stifle": (0.350, 0.48), "back_hock": (0.500, 0.255),
    "back_foot": (0.472, 0.085), "back_toe": (0.285, 0.045),
}
TORSO_CHAIN = [(0.60, 0.84), (0.42, 0.80), (0.13, 0.80), (-0.17, 0.82), (-0.335, 0.96), (-0.335, 1.15)]

# ----------------------------------------------------------------------------- sit pose
# Solved against the skinned mesh by kit/solve_sit.py (re-run after changing the shape or rig).
# Lab: solve_sit printed hock_z 0.0428 (full-model hocks +1.1 mm), but the LOD's decimated hock
# then sat 11.7 mm up and failed the 1 cm check; hock_z is set 4 mm lower by hand (full -3.1 mm,
# LOD +7.6 mm). Everything else is exactly as solved.
SIT = {"hips": -60.419, "drop": -0.5385, "shoulder": -8.0, "stifle_z": 0.1, "hock_z": 0.0388, "foot_pitch": 14.0107, "tail": 44.9241}
SIT_POSE = {"spine_3": 4, "neck": 28, "head": 24, "tail_2": 12, "tail_3": 6}

# ----------------------------------------------------------------------------- lie pose (Rest)
# Solved against the skinned mesh by kit/solve_lie.py (paste its output here after changing the
# shape or the rig). The hip tilt, the stifle height and tail_2 / tail_3 are chosen; the rest are
# solved.
LIE = {"hips": 0.0, "drop": -0.4956, "elbow_z": 0.0923, "wrist_z": 0.074, "stifle_z": 0.1, "hock_z": 0.0418, "foot_pitch": 14.0107, "tail": -54.9882, "tail_2": 12, "tail_3": 6}

# ----------------------------------------------------------------------------- checks
SANITY = {"head_min_z": 0.85, "tail_min_y": 0.45}
MEASURE = {"rump_min_abs_x": 0.10, "back_y": (0.0, 0.6)}

# ----------------------------------------------------------------------------- coat
# sRGB hex: main coat, "feathering" (here only a slightly paler underside: chest, belly, insides
# of the legs, muzzle; a Lab has no feathering), ears (barely darker). Coat keys are the file
# names; the mapping to the game's Default / Light / Dark / Black is an open question (README).
COATS = {
    "yellow":    ("#D6A86A", "#E4C48F", "#C6975A"),
    "black":     ("#2E2C2A", "#353230", "#292726"),
    "chocolate": ("#5E3B25", "#6D4730", "#53331F"),
}
COAT_LABELS = {"yellow": "Yellow", "black": "Black (charcoal)", "chocolate": "Chocolate"}
FUR_FLOW = {
    "body": ((0.0, 1.0, -0.25), ("body", "head")),
    "leg": ((0.0, 0.15, -1.0), ("fleg", "bleg", "thigh", "paw")),
    "tail": ((0.0, 1.0, -0.15), ("tail",)),
    "ear": ((0.0, -0.15, -1.0), ("ear",)),
}
FUR_MARKINGS = {"bib_y": (-0.12, -0.26), "rump_y": (0.50, 0.60), "muzzle_y": (-0.56, -0.66),
                "crown_y": (-0.47, -0.36), "pale_z": (0.08, 0.26), "tail_tip_y": (0.62, 0.76),
                "ear_tip_z": (1.30, 1.22)}
# Lab: the short, dense, smooth coat - strands about 3-4 mm long instead of 8-35 mm, almost no
# clumping, a much flatter normal map and less light/dark strand contrast (kit default: Golden)
FUR_STRAND = {"fine": (0.07, 0.30), "mid": (0.16, 0.45), "clump": (0.40, 0.7), "low": (5.0, 7.0),
              "mix": (0.60, 0.35, 0.10), "normal": 0.16, "contrast": 0.20}
TRIM_COLOURS = {"eye": "#1C1411", "iris": "#4A2C1A", "pupil": "#140E0B", "shine": "#FFFFFF", "nose": "#24201F",
                "tag": "#D6AC45", "mouth": "#5E2B2C", "lip": "#2C2120", "tongue": "#E7848A",
                "collar": "#C3282E", "stitch": "#E8B0A8"}

# ----------------------------------------------------------------------------- motion
# Same leg lengths as the Golden, so the same gaits (strides already suit these legs).
GAITS = {
    "Walk": {"frames": 30, "speed": 0.80,
             "phase": {"HL": 0.0, "FL": 0.22, "HR": 0.5, "FR": 0.72},
             "duty": {"HL": 0.62, "HR": 0.62, "FL": 0.64, "FR": 0.64},
             "lift": {"F": 0.075, "H": 0.06}, "roll": {"F": (28.0, 0.22), "H": (24.0, 0.22)},
             "curl": {"F": 40.0, "H": 18.0}, "fold": {"F": 30.0, "H": -14.0},
             "center": {"F": -0.02, "H": 0.0}, "win": 1, "sigma": 1.2, "k": {"F": 0.85, "H": 0.45},
             "notes": "four-beat lateral-sequence walk; touchdowns LH 0.00, LF 0.22, RH 0.50, RF 0.72 "
                      "of the cycle; duty 0.62 hind / 0.64 fore"},
    "Run": {"frames": 16, "speed": 3.5,
            "phase": {"HR": 0.0, "HL": 0.11, "FR": 0.40, "FL": 0.50},
            "duty": {"HR": 0.30, "HL": 0.30, "FR": 0.30, "FL": 0.30},
            "lift": {"F": 0.16, "H": 0.12}, "roll": {"F": (35.0, 0.3), "H": (30.0, 0.3)},
            "curl": {"F": 75.0, "H": 30.0}, "fold": {"F": 55.0, "H": -25.0},
            "center": {"F": -0.06, "H": -0.10}, "suspension": (0.035, 0.90, 0.30), "win": 1, "sigma": 1.0,
            "k": {"F": 0.9, "H": 0.6}, "land": 0.985, "reach": {"F": 0.965, "H": 0.95},
            "notes": "transverse gallop, right-hind lead: touchdowns RH 0.00, LH 0.11, RF 0.40, LF 0.50; "
                     "duty 0.30; gathered suspension 0.80-1.00"},
}
SIT_DOWN = {"rock_radius": 0.62, "dip": 0.025, "hind_step_lift": 0.035, "front_step_lift": 0.05}
PLAY_BOW = {"elbow_z": 0.115, "wrist_z": 0.095}

# ----------------------------------------------------------------------------- review renders
RENDER = {
    "head_box": ((-0.38, -0.74, 1.00), (0.38, -0.12, 1.60)),
    "mouth_box": ((-0.21, -0.74, 0.98), (0.21, -0.45, 1.20)),
    "hips_box": {"rest": ((-0.40, 0.05, 0.0), (0.40, 0.95, 1.0)), "sit": ((-0.40, -0.10, 0.0), (0.40, 1.25, 0.80))},
    "body_box": ((-0.4, -0.95, 0.0), (0.4, 1.05, 1.62)),
    "chest_box": ((-0.42, -0.85, 0.0), (0.42, 0.30, 1.62)),
    "chest_cut_y": -0.08,
    "join_views": [("Shoulder", "front_shoulder_L", (0.02, 0.05, -0.12), (0.24, 0.30, 0.34), -72, 6),
                   ("Hip / haunch", "back_hip_L", (0.02, 0.03, -0.16), (0.24, 0.33, 0.38), -118, 8),
                   ("Tail base", "tail_1", (0.0, 0.04, -0.06), (0.22, 0.26, 0.26), -150, 24)],
    "sit_closeups": {"hind": ("back_hock_L", 0.60, 0.55), "front": ("front_paw_L", 0.45, 0.40)},
    "before": {},
    "captions": {
        "turnaround": ("Labrador Retriever - turnaround (orthographic)",
                       "Round 1 on the shared kit: broad skull with a stop, short square muzzle, small close ears, "
                       "otter tail, short smooth coat. Yellow coat."),
        "gamecam": ("Labrador Retriever at the game camera",
                    "Orthographic, 45 deg pitch, yaw -40. Approved base 1 (level 1), mailbox and hydrant for scale."),
        "face": ("Labrador Retriever - face, 3/4",
                 "Broad skull, defined stop, shorter and wider muzzle; the Golden's mouth (lips wrap the corners); "
                 "jaw open 12 deg."),
        "coats": ("Coats and puppy",
                  "Yellow, Black (charcoal with a sheen), Chocolate. One mesh: a coat is a fur colour map; "
                  "normal map shared. Puppy = adult x 0.55."),
        "splash": ("3D concept vs splash", "No Labrador splash art yet."),
        "mouth": ("Mouth", "The Golden's mouth on the shorter muzzle: lips meet at the corners on the hinge."),
        "mouth_rows": ("Open (default pose, jaw 12 deg)", "Closed (jaw bone at 0, tongue hidden)"),
        "hips": ("Hip study - hind leg (skinned), sit",
                 "Red dots = joints: hips, hip, stifle, hock, foot (both legs), tail base."),
        "hips_notes": ["The Golden's approved hind-leg build, a little heavier: thigh into a rounded haunch, "
                       "stifle, lower thigh back to the hock,",
                       "and a near-vertical rear foot. The thick otter tail blends into the rump with a wider fillet.",
                       "Sit: the hips drop and tilt so the rump rests on the ground; hock and rear foot lie flat, "
                       "front paws stay planted.",
                       "Contact is measured on the skinned mesh and checked at export (see sit.png)."],
        "joins": ("Joins - shoulder, hip / haunch, tail base",
                  "One skin: the volumes blend with a smooth minimum, then retopologise."),
        "joins_rows": ("Rest", "Bent (sit): the junctions stay smooth"),
        "sit": ("Sit - ground contact",
                "Side views from a level camera at ground height; the dark line is the ground plane."),
        "chest": ("Chest", "Orthographic: front, front section, side, 3/4."),
        "chest_row": "Labrador: a little wider and deeper than the Golden",
    },
}
