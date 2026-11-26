"""Golden Retriever: the breed definition for the shared dog kit (../kit/).

Data only: numbers, strings, tuples, lists and dicts. No imports and no Blender calls; the kit
checks that when it loads this file (kit/dh_breed.py). Everything the kit builds, rigs, textures,
animates and checks comes from here, so a new breed is a copy of this file with its own values
(see ../README.md).

Conventions: metres, Blender axes (front -Y, up +Z, the dog's left +X), authored coordinates
(before the OY shift that puts the origin mid-span). Head-group points (head, jaw, ears, eyes,
nose, tongue) are authored before the HEAD_DY move-back; the kit adds HEAD_DY.

Build it:  python3 art-source/blender/dogs/kit/build_breed.py golden-retriever
"""

# ----------------------------------------------------------------------------- names
NAME = "dog_golden"                       # object and file name stem (dog_golden_body, dog_golden_default.gltf)
DISPLAY_NAME = "Golden Retriever"
STATS_NAME = "Golden Retriever (adult) concept, round 4"
MOTION_STATUS = "Golden Retriever motion clips, first pass (concept)"
MATERIAL_NAMES = ["DH_Dog_Fur_%s", "DH_Dog_Gloss", "DH_Dog_Soft"]   # fur (per coat), gloss, soft
# (roughness, specular IOR level) per material
MATERIAL_SHADING = {"fur": (0.82, 0.25), "gloss": (0.30, 0.35), "soft": (0.55, 0.35)}

# ----------------------------------------------------------------------------- budgets
BUDGET = {"full": 4000, "lod1": 1500}     # triangles per file
SKIN_FACES = 1500                         # QuadriFlow target (quads) for the body
LOD_SKIN_TRIS = 820                       # the LOD body: the full retopology decimated to this

# ----------------------------------------------------------------------------- placement
HEAD_DY = 0.035                # head group moved back by this after building
OY = 0.10                      # shift so the origin is under the middle of the nose-to-rump span
JAW_REST_OPEN_DEG = 12.0       # the file's default pose opens the jaw this much

# ----------------------------------------------------------------------------- body shape
# The field the continuous skin is meshed from: grid spacing, origin and extent (m), and the
# voxel size of the manifold clean-up. The grid must hold the whole standing body.
FIELD_GRID = {"h": 0.012, "origin": (-0.42, -0.92, -0.02), "extent": (0.84, 2.04, 1.62), "voxel": 0.008}

# torso + neck: (y, z, half width, top, bottom, exponent)
TORSO = [
    (0.625, 0.84, 0.07, 0.06, 0.07, 2.0),
    (0.610, 0.84, 0.18, 0.15, 0.18, 2.2),
    (0.560, 0.83, 0.235, 0.20, 0.20, 2.4),
    (0.490, 0.82, 0.255, 0.215, 0.195, 2.5),
    (0.420, 0.81, 0.255, 0.22, 0.180, 2.5),     # hip loop
    (0.310, 0.81, 0.260, 0.215, 0.180, 2.5),
    (0.190, 0.80, 0.270, 0.215, 0.200, 2.5),    # loin, tucked up
    (0.060, 0.80, 0.272, 0.22, 0.250, 2.5),
    (-0.060, 0.80, 0.272, 0.225, 0.292, 2.5),   # ribcage (round 4: narrower, shallower)
    (-0.150, 0.81, 0.264, 0.225, 0.302, 2.5),   # shoulder loop
    (-0.240, 0.84, 0.250, 0.22, 0.295, 2.5),
    (-0.295, 0.89, 0.228, 0.21, 0.262, 2.4),    # front of chest
    (-0.330, 0.96, 0.220, 0.20, 0.230, 2.3),    # neck base
    (-0.360, 1.04, 0.205, 0.18, 0.195, 2.2),    # collar loop
    (-0.375, 1.12, 0.185, 0.16, 0.165, 2.1),
    (-0.385, 1.20, 0.160, 0.14, 0.14, 2.0),     # neck top, inside the skull
]

# head with the lower jaw: (y, z, half width, top, bottom, exponent)
HEAD = [
    (-0.195, 1.335, 0.07, 0.07, 0.07, 2.0),
    (-0.225, 1.34, 0.19, 0.17, 0.17, 2.1),
    (-0.285, 1.345, 0.27, 0.22, 0.215, 2.2),
    (-0.370, 1.345, 0.300, 0.24, 0.245, 2.2),
    (-0.460, 1.335, 0.295, 0.23, 0.255, 2.2),
    (-0.545, 1.315, 0.265, 0.195, 0.265, 2.3),
    (-0.610, 1.28, 0.225, 0.15, 0.255, 2.4),
    (-0.670, 1.245, 0.190, 0.12, 0.235, 2.5),
    (-0.740, 1.235, 0.175, 0.112, 0.215, 2.6),
    (-0.805, 1.23, 0.152, 0.10, 0.185, 2.5),
    (-0.848, 1.225, 0.105, 0.072, 0.13, 2.2),
    (-0.868, 1.225, 0.050, 0.04, 0.06, 2.0),
]

# tail: (y, z, half width, top, bottom)
TAIL = [
    (0.560, 0.875, 0.070, 0.070, 0.064),
    (0.640, 0.860, 0.074, 0.090, 0.060),
    (0.750, 0.805, 0.082, 0.125, 0.054),
    (0.855, 0.740, 0.084, 0.140, 0.048),
    (0.950, 0.685, 0.076, 0.128, 0.042),
    (1.030, 0.650, 0.058, 0.092, 0.034),
    (1.085, 0.640, 0.026, 0.045, 0.018),
]

# legs: x of each leg's centre line (the thigh sits BACK_THIGH_DX further out), and the
# segments as (y, z, half width, front, back)
FRONT_X = 0.165
BACK_X = 0.180
BACK_THIGH_DX = 0.025
FRONT_UPPER = [(-0.150, 0.92, 0.090, 0.10, 0.11), (-0.195, 0.77, 0.092, 0.105, 0.10),
               (-0.165, 0.62, 0.090, 0.090, 0.095), (-0.125, 0.50, 0.086, 0.085, 0.095)]
FRONT_FORE = [(-0.118, 0.53, 0.086, 0.085, 0.095), (-0.128, 0.40, 0.080, 0.078, 0.080),
              (-0.138, 0.27, 0.074, 0.072, 0.070), (-0.145, 0.18, 0.071, 0.068, 0.066)]
FRONT_PASTERN = [(-0.145, 0.19, 0.070, 0.066, 0.064), (-0.152, 0.14, 0.068, 0.064, 0.062),
                 (-0.165, 0.10, 0.070, 0.066, 0.062)]
FRONT_PAW = (-0.095, -0.330)          # heel y, toe y
BACK_THIGH = [(0.470, 0.86, 0.125, 0.12, 0.15), (0.445, 0.75, 0.140, 0.13, 0.185),
              (0.400, 0.62, 0.112, 0.105, 0.160), (0.360, 0.52, 0.090, 0.085, 0.115),
              (0.345, 0.47, 0.080, 0.075, 0.085)]
BACK_GASKIN = [(0.350, 0.49, 0.078, 0.075, 0.090), (0.405, 0.40, 0.070, 0.060, 0.090),
               (0.460, 0.31, 0.062, 0.052, 0.070), (0.495, 0.255, 0.058, 0.050, 0.066)]
BACK_META = [(0.497, 0.27, 0.057, 0.050, 0.064), (0.487, 0.18, 0.058, 0.052, 0.056),
             (0.475, 0.10, 0.062, 0.056, 0.054)]
BACK_PAW = (0.505, 0.265)             # heel y, toe y
# a paw: rings centred at height z, max half width / top / bottom, superellipse exponent
PAW = {"z": 0.064, "half_width": 0.098, "top": 0.064, "bottom": 0.064, "e": 2.6, "rings": 7}

# superellipse exponent and end-cap dome of each loft
LOFT_STYLE = {"torso_dome": 0.35, "head_dome": 0.5, "tail": (2.1, 0.7), "front_leg": (2.2, 0.3),
              "back_thigh": (2.3, 0.3), "back_leg": (2.2, 0.3), "ear": (2.2, 0.8)}

# smooth-union recipe: (group, group fillet k, [(volume, fillet k)]); each group is folded with
# the smooth minimum, then the groups are folded into the result with the group's k (m)
FIELD = [
    ("core", 0.0, [("torso", 0.0), ("head", 0.045), ("tail", 0.05)]),
    ("front_L", 0.06, [("front_upper_L", 0.0), ("front_fore_L", 0.035), ("front_pastern_L", 0.025), ("front_paw_L", 0.03)]),
    ("front_R", 0.06, [("front_upper_R", 0.0), ("front_fore_R", 0.035), ("front_pastern_R", 0.025), ("front_paw_R", 0.03)]),
    ("back_L", 0.05, [("back_thigh_L", 0.0), ("back_gaskin_L", 0.04), ("back_meta_L", 0.03), ("back_paw_L", 0.03)]),
    ("back_R", 0.05, [("back_thigh_R", 0.0), ("back_gaskin_R", 0.04), ("back_meta_R", 0.03), ("back_paw_R", 0.03)]),
]

# ----------------------------------------------------------------------------- muzzle and mouth
HINGE = (-0.53, 1.12)          # (y, z) of the jaw hinge axis = the mouth corners
LIP_RUN = 0.33                 # the lip line runs this far forward from the corner ...
LIP_DROP = 0.055               # ... and falls this much
LIP_BAND = 0.013               # dark lip band either side of the lip line
MOUTH_MAX_OPEN_DEG = 34.0      # side webs cover openings up to this
MOUTH_ZONE = {"below_hinge": 0.22, "behind_hinge": 0.10}   # where the lip cut may happen
JAW_TIP = (-0.78, 1.07)        # (y, z) of the jaw bone's tail

# tongue: pivot (y, z) and rings (y, height above the closed lip plane, half width, thickness)
TONGUE = {
    "pivot": (-0.565, 1.13),
    "rings": [(-0.565, 0.012, 0.060, 0.016), (-0.640, 0.016, 0.082, 0.020), (-0.730, 0.018, 0.086, 0.021),
              (-0.810, 0.016, 0.082, 0.019), (-0.855, -0.012, 0.068, 0.016), (-0.874, -0.052, 0.050, 0.014)],
    "lod_rings": (0, 2, 4, 5),
    "e": 2.6,
}

# ----------------------------------------------------------------------------- ears, eyes, nose, collar
# left ear (the right is mirrored): rings (x, y, z, half width, thickness), and the ear_1 / ear_2
# bone chain (x, y, z) from the root (the ear's pivot) to the tip
EARS = {
    "rings": [(0.262, -0.400, 1.500, 0.070, 0.020), (0.303, -0.425, 1.450, 0.115, 0.030),
              (0.328, -0.450, 1.370, 0.135, 0.032), (0.337, -0.465, 1.270, 0.130, 0.032),
              (0.330, -0.475, 1.180, 0.105, 0.028), (0.314, -0.480, 1.115, 0.055, 0.020)],
    "lod_rings": (0, 1, 3, 4, 5),
    "bones": [(0.265, -0.40, 1.50), (0.335, -0.465, 1.30), (0.314, -0.48, 1.115)],
}
# eyes sit on the face: a ray from the front at (+-ray_x, ray_z) finds the head surface
EYES = {"ray_x": 0.150, "ray_z": 1.385, "face_bias": 0.6, "inset": 0.008, "radii": (0.058, 0.034, 0.066),
        "shine_offset": (0.026, 0.028, 0.016), "shine_radii": (0.012, 0.010, 0.012)}
NOSE = {"ray_z": 1.26, "offset": (0.012, 0.006), "length": 0.040, "radii": (0.084, 0.054, 0.042), "e": 2.4}
# the collar hugs the skin around TORSO[ring]
COLLAR = {"ring": 13, "gap": 0.014, "gap_lod": 0.022, "strap": (0.034, 0.012), "e": 3.0,
          "tag_radii": (0.038, 0.008), "tag_out": 0.020, "tag_drop": 0.042}

# ----------------------------------------------------------------------------- skeleton
# (y, z) of the axial bones' heads and tails. neck tail, head and the ear / jaw bones belong to
# the head group (the kit adds HEAD_DY). The jaw's head is HINGE.
SPINE = {
    "hips": ((0.42, 0.80), (0.60, 0.84)),
    "spine_1": ((0.42, 0.80), (0.13, 0.80)),
    "spine_2": ((0.13, 0.80), (-0.17, 0.82)),
    "spine_3": ((-0.17, 0.82), (-0.335, 0.96)),
    "neck": ((-0.335, 0.96), (-0.37, 1.15)),
    "head": ((-0.37, 1.15), (-0.62, 1.30)),
}
TAIL_BONES = [(0.58, 0.875), (0.70, 0.835), (0.82, 0.765), (0.95, 0.685), (1.085, 0.64)]   # tail_1..4 joints
# leg joints (y, z); x is FRONT_X / BACK_X
JOINTS = {
    "front_shoulder": (-0.200, 0.80), "front_elbow": (-0.122, 0.50), "front_wrist": (-0.146, 0.17),
    "front_paw": (-0.170, 0.075), "front_toe": (-0.315, 0.045),
    "back_hip": (0.445, 0.80), "back_stifle": (0.350, 0.48), "back_hock": (0.500, 0.255),
    "back_foot": (0.472, 0.085), "back_toe": (0.285, 0.045),
}
# the torso's skin weights run along this (y, z) polyline: hips, spine_1-3, neck
TORSO_CHAIN = [(0.60, 0.84), (0.42, 0.80), (0.13, 0.80), (-0.17, 0.82), (-0.335, 0.96), (-0.335, 1.15)]

# ----------------------------------------------------------------------------- sit pose
# Solved against the skinned mesh by kit/solve_sit.py (paste its output here after changing the
# shape or the rig). The hip tilt and stifle height are chosen; the rest are solved.
SIT = {"hips": -60.7943, "drop": -0.5414, "shoulder": -8.0, "stifle_z": 0.1, "hock_z": 0.0388,
       "foot_pitch": 15.3206, "tail": 73.6184}
# the neck and head counter-rotate so the head is level; the tail lies on the ground (degrees)
SIT_POSE = {"spine_3": 4, "neck": 28, "head": 24, "tail_2": 12, "tail_3": 6}

# ----------------------------------------------------------------------------- checks
# weight sanity: head / jaw weights only above head_min_z, tail weights only behind tail_min_y
SANITY = {"head_min_z": 0.85, "tail_min_y": 0.45}
# measurements: the rump is the back of the body more than rump_min_abs_x off the midline (beside
# the tail); the top of the back is measured over back_y
MEASURE = {"rump_min_abs_x": 0.10, "back_y": (0.0, 0.6)}

# ----------------------------------------------------------------------------- coat
# sRGB hex: main coat, feathering (chest, belly, leg backs, tail underside), ears
COATS = {
    # round 2: the round-1 Default became Light; Default is a richer gold between it and Dark
    "default": ("#C4843C", "#E2B472", "#9A5B26"),
    "light":   ("#D29A50", "#EACB8E", "#AE6F32"),
    "dark":    ("#9C5424", "#C2814A", "#723A18"),
}
COAT_LABELS = {"default": "Default (rich gold)", "light": "Light (warm gold)", "dark": "Dark (red-gold)"}
# fur flow direction per group (Blender axes), and the body regions in each group
FUR_FLOW = {
    "body": ((0.0, 1.0, -0.35), ("body", "head")),
    "leg": ((0.0, 0.15, -1.0), ("fleg", "bleg", "thigh", "paw")),
    "tail": ((0.0, 0.85, -0.5), ("tail",)),
    "ear": ((0.0, -0.15, -1.0), ("ear",)),
}
# where the feathering and the darker ears start and end (authored y or z, m; each pair is the
# smoothstep's start and end)
FUR_MARKINGS = {"bib_y": (-0.12, -0.26), "rump_y": (0.50, 0.60), "muzzle_y": (-0.57, -0.69),
                "crown_y": (-0.47, -0.36), "pale_z": (0.08, 0.26), "tail_tip_y": (0.62, 0.76),
                "ear_tip_z": (1.20, 1.12)}
# the trim palette (sRGB hex)
TRIM_COLOURS = {"eye": "#1C1411", "iris": "#4A2C1A", "pupil": "#140E0B", "shine": "#FFFFFF", "nose": "#24201F",
                "tag": "#D6AC45", "mouth": "#5E2B2C", "lip": "#2C2120", "tongue": "#E7848A",
                "collar": "#C3282E", "stitch": "#E8B0A8"}

# ----------------------------------------------------------------------------- motion
# In-place gaits (kit/dh_dog_gait.py): cycle frames at 30 fps, ground speed (m/s), and per leg the
# touchdown phase and duty factor; lift (m), roll (deg, fraction of stance), curl / fold (deg) per
# fore (F) / hind (H) leg; stance centre offsets (m); IK lower-segment follow factor k; reach;
# suspension = (root lift m, phase, width) of an airborne bounce.
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
# SitDown: the root drops by rock_radius * sin(hip tilt) so the shoulders keep their height,
# with a small extra dip; paw step heights (m)
SIT_DOWN = {"rock_radius": 0.62, "dip": 0.025, "hind_step_lift": 0.035, "front_step_lift": 0.05}
# PlayBow: elbow and wrist joint heights (m) with the forearms along the ground
PLAY_BOW = {"elbow_z": 0.115, "wrist_z": 0.095}

# ----------------------------------------------------------------------------- review renders
# Framing boxes (world, rest pose) and the captions on the review sheets (kit/render_dog.py).
# "before" rows come from this folder's render_round2.py / render_round3.py, which run the
# earlier rounds' builders from <DH_DOG_ROUNDS>/<rounds> (not in the repository).
RENDER = {
    "head_box": ((-0.36, -0.76, 1.00), (0.36, -0.12, 1.60)),
    "mouth_box": ((-0.2, -0.76, 0.98), (0.2, -0.45, 1.20)),
    "hips_box": {"rest": ((-0.40, 0.05, 0.0), (0.40, 0.95, 1.0)), "sit": ((-0.40, -0.10, 0.0), (0.40, 1.25, 0.80))},
    "body_box": ((-0.4, -0.95, 0.0), (0.4, 1.05, 1.62)),
    "chest_box": ((-0.42, -0.85, 0.0), (0.42, 0.30, 1.62)),
    "chest_cut_y": -0.08,
    # (name, joint the close-up centres on, offset from it, box half size, yaw, pitch)
    "join_views": [("Shoulder", "front_shoulder_L", (0.02, 0.05, -0.12), (0.24, 0.30, 0.34), -72, 6),
                   ("Hip / haunch", "back_hip_L", (0.02, 0.03, -0.16), (0.24, 0.33, 0.38), -118, 8),
                   ("Tail base", "tail_1", (0.0, 0.04, -0.06), (0.22, 0.26, 0.26), -150, 24)],
    # sit close-ups: (joint, half length, height)
    "sit_closeups": {"hind": ("back_hock_L", 0.60, 0.55), "front": ("front_paw_L", 0.45, 0.40)},
    "before": {
        "mouth": {"script": "render_round2.py", "rounds": "round2/scripts", "label": "Round 2 - before (open)"},
        "joins": {"script": "render_round3.py", "rounds": "round3/scripts", "label": "Before - round 3 (rest)"},
        "chest": {"script": "render_round3.py", "rounds": "round3/scripts", "label": "Before - round 3"},
    },
    "captions": {
        "turnaround": ("Golden Retriever - turnaround (orthographic)",
                       "Round 4: one continuous skinned body, slimmer chest. Rest pose: standing, mouth open 12 deg. "
                       "Default coat."),
        "gamecam": ("Golden Retriever at the game camera",
                    "Orthographic, 45 deg pitch, yaw -40. Approved base 1 (level 1), mailbox and hydrant for scale."),
        "face": ("Golden Retriever - face, 3/4",
                 "Round-2 mouth on the skinned body (round 4): the head, jaw and neck are one mesh; jaw open 12 deg."),
        "coats": ("Coats and puppy",
                  "One mesh: a coat is a material swap (its own fur colour map; normal map shared). Puppy = adult x 0.55."),
        "splash": ("3D concept vs splash golden",
                   "Kept: big dark eyes + catch-light, black nose, open mouth + pink tongue, floppy darker ears, "
                   "red at the neck, feathered tail."),
        "mouth": ("Mouth - round 3",
                  "Same mouth as round 2, now cut into the one skin: lips meet at the corners on the hinge, "
                  "the jaw is skinned to the jaw bone."),
        "mouth_rows": ("Round 3 - open (default pose, jaw 12 deg)", "Round 3 - closed (jaw bone at 0, tongue hidden)"),
        "hips": ("Hip study - hind leg (skinned), round 4 sit",
                 "Red dots = joints: hips, hip, stifle, hock, foot (both legs), tail base."),
        "hips_notes": ["Hind leg rebuilt: the thigh angles forward and down from the hip into a rounded haunch, "
                       "the stifle (knee) bends,",
                       "the lower thigh angles back to the hock, and the rear foot rises nearly vertically from the "
                       "paw to the hock.",
                       "Sit (round 4): the hips drop and tilt so the rump rests on the ground; the hock and rear foot "
                       "lie flat, the front paws",
                       "stay planted. Contact is measured on the skinned mesh and checked at export (see sit.png)."],
        "joins": ("Joins - shoulder, hip / haunch, tail base",
                  "Same cameras before and after. One skin: the volumes blend with a smooth minimum, then retopologise."),
        "joins_rows": ("After - round 4: slimmer chest, same filleted junctions (rest)",
                       "After - round 4, bent (sit): the junctions stay smooth"),
        "sit": ("Sit - ground contact (round 4)",
                "Side views from a level camera at ground height; the dark line is the ground plane."),
        "chest": ("Chest - round 3 vs round 4",
                  "Same orthographic cameras. Chest 0.60 -> 0.53 m wide at the shoulder, 0.60 -> 0.50 m at the ribs, "
                  "brisket 3.6 cm higher; head, legs, height and length unchanged."),
        "chest_row": "After - round 4: narrower, shallower chest",
    },
}
