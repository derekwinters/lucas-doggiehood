"""German Shepherd: the breed definition for the shared dog kit (../kit/).

Data only: numbers, strings, tuples, lists and dicts. No imports and no Blender calls; the kit
checks that when it loads this file (kit/dh_breed.py). Started as a copy of the approved Golden
Retriever's breed.py (../golden-retriever/breed.py documents every field); the values that make a
Shepherd a Shepherd are marked "GSD:".

What changes from the Golden:
  - head: a slightly narrower skull with a flat forehead, little stop, and a longer muzzle that
    narrows to a wedge;
  - ears: tall, erect and pointed, set high and opening forward (EARS["side"], EARS["swing"]);
  - body: a topline that slopes gently from high withers down to a lower croup, a light ruff at
    the neck, a little more tuck-up;
  - hind legs: more angulated (stifle further forward, hock further back, rear feet set back),
    same hip and paw heights;
  - tail: long and bushy, carried low in a gentle curve;
  - coat: medium length (FUR_STRAND between the Lab's and the Golden's), black-and-tan with a
    saddle and a dark mask (FUR_PATCHES), and solid black.
Kept: the mouth (lips wrap the corners), the one continuous skin, the front legs, the rig, the
collar, the eyes and a solved sit that rests on the ground.

Conventions: metres, Blender axes (front -Y, up +Z, the dog's left +X), authored coordinates
(before the OY shift that puts the origin mid-span). Head-group points (head, jaw, ears, eyes,
nose, tongue) are authored before the HEAD_DY move-back; the kit adds HEAD_DY.

Build it:  python3 art-source/blender/dogs/kit/build_breed.py german-shepherd
"""

# ----------------------------------------------------------------------------- names
NAME = "dog_shepherd"                     # object and file name stem (dog_shepherd_body, dog_shepherd_default.gltf)
DISPLAY_NAME = "German Shepherd"
STATS_NAME = "German Shepherd (adult) concept, round 1"
MOTION_STATUS = "German Shepherd motion clips, first pass (concept)"
MATERIAL_NAMES = ["DH_Dog_Fur_%s", "DH_Dog_Gloss", "DH_Dog_Soft"]   # fur (per coat), gloss, soft
# (roughness, specular IOR level) per material. GSD: a medium coat with a little more sheen than
# the Golden's (0.82, 0.25) and less than the Lab's (0.60, 0.42), so the black shows its form.
MATERIAL_SHADING = {"fur": (0.70, 0.34), "gloss": (0.30, 0.35), "soft": (0.55, 0.35)}

# ----------------------------------------------------------------------------- budgets
BUDGET = {"full": 4000, "lod1": 1500}     # triangles per file
SKIN_FACES = 1420                         # QuadriFlow target (quads) for the body. GSD: QuadriFlow overshoots unevenly on this shape (1500 went over budget); 1420 gives 3,044 body tris (README)
LOD_SKIN_TRIS = 820                       # the LOD body: the full retopology decimated to this

# ----------------------------------------------------------------------------- placement
HEAD_DY = 0.035                # head group moved back by this after building
OY = 0.115                     # GSD: the longer muzzle moves the nose-to-rump midpoint forward
JAW_REST_OPEN_DEG = 12.0       # the file's default pose opens the jaw this much

# ----------------------------------------------------------------------------- body shape
FIELD_GRID = {"h": 0.012, "origin": (-0.42, -0.96, -0.02), "extent": (0.84, 2.08, 1.62), "voxel": 0.008}

# torso + neck: (y, z, half width, top, bottom, exponent)
# GSD: the topline slopes gently from the withers (1.09 m) to the croup (1.00 m), which falls away
# to a lower tail set; a little more tuck-up; the neck is fuller (a light ruff).
TORSO = [
    (0.625, 0.80, 0.07, 0.06, 0.07, 2.0),
    (0.610, 0.80, 0.18, 0.14, 0.17, 2.2),
    (0.560, 0.80, 0.235, 0.180, 0.19, 2.4),
    (0.490, 0.80, 0.255, 0.198, 0.180, 2.5),
    (0.420, 0.80, 0.255, 0.208, 0.168, 2.5),    # hip loop
    (0.310, 0.80, 0.258, 0.214, 0.160, 2.5),
    (0.190, 0.80, 0.262, 0.220, 0.180, 2.5),    # loin, tucked up
    (0.060, 0.80, 0.270, 0.230, 0.245, 2.5),
    (-0.060, 0.81, 0.272, 0.236, 0.300, 2.5),   # ribcage
    (-0.150, 0.82, 0.265, 0.242, 0.310, 2.5),   # shoulder loop (withers)
    (-0.240, 0.85, 0.254, 0.238, 0.300, 2.5),
    (-0.295, 0.90, 0.236, 0.226, 0.268, 2.4),   # front of chest
    (-0.330, 0.97, 0.234, 0.218, 0.238, 2.3),   # neck base, ruff
    (-0.360, 1.05, 0.222, 0.198, 0.208, 2.2),   # collar loop, ruff
    (-0.375, 1.13, 0.196, 0.172, 0.176, 2.1),
    (-0.385, 1.20, 0.165, 0.146, 0.146, 2.0),   # neck top, inside the skull
]

# head with the lower jaw: (y, z, half width, top, bottom, exponent)
# GSD: skull 0.575 m across (Golden 0.60), flat on top; the forehead runs into the muzzle with
# little stop; the muzzle is 3.2 cm longer than the Golden's, less deep, and narrows to a wedge.
HEAD = [
    (-0.195, 1.335, 0.07, 0.07, 0.07, 2.0),
    (-0.225, 1.34, 0.185, 0.165, 0.165, 2.1),
    (-0.285, 1.345, 0.262, 0.215, 0.210, 2.25),
    (-0.370, 1.345, 0.288, 0.230, 0.240, 2.3),
    (-0.460, 1.335, 0.280, 0.215, 0.250, 2.3),
    (-0.545, 1.315, 0.248, 0.180, 0.255, 2.3),
    (-0.615, 1.285, 0.208, 0.142, 0.240, 2.35),  # little stop
    (-0.685, 1.258, 0.170, 0.114, 0.212, 2.4),
    (-0.755, 1.244, 0.144, 0.102, 0.190, 2.4),   # wedge muzzle
    (-0.825, 1.236, 0.116, 0.092, 0.162, 2.3),
    (-0.878, 1.232, 0.082, 0.070, 0.118, 2.2),
    (-0.900, 1.232, 0.040, 0.036, 0.054, 2.0),
]

# tail: (y, z, half width, top, bottom)
# GSD: long and bushy, set low on the sloping croup, hanging in a gentle curve to about hock
# height with a slight lift at the tip.
TAIL = [
    (0.560, 0.845, 0.066, 0.066, 0.066),
    (0.640, 0.808, 0.078, 0.084, 0.084),
    (0.715, 0.745, 0.088, 0.096, 0.104),
    (0.782, 0.665, 0.094, 0.100, 0.112),
    (0.832, 0.580, 0.092, 0.098, 0.110),
    (0.873, 0.500, 0.080, 0.086, 0.094),
    (0.915, 0.440, 0.056, 0.060, 0.062),
    (0.955, 0.422, 0.022, 0.024, 0.022),
]

# legs: the Golden's front legs; GSD: a more angulated hind leg (stifle 1.5 cm forward, hock and
# rear foot 3 cm back, lower thigh 3.2 cm longer), a little broader through the thigh
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
BACK_THIGH = [(0.470, 0.86, 0.128, 0.12, 0.16), (0.440, 0.75, 0.145, 0.13, 0.195),
              (0.390, 0.62, 0.116, 0.105, 0.170), (0.348, 0.52, 0.092, 0.085, 0.122),
              (0.334, 0.47, 0.080, 0.075, 0.088)]
BACK_GASKIN = [(0.338, 0.495, 0.078, 0.075, 0.092), (0.405, 0.41, 0.070, 0.060, 0.092),
               (0.475, 0.32, 0.062, 0.052, 0.072), (0.525, 0.258, 0.058, 0.050, 0.066)]
BACK_META = [(0.527, 0.27, 0.057, 0.050, 0.064), (0.517, 0.18, 0.058, 0.052, 0.056),
             (0.505, 0.10, 0.062, 0.056, 0.054)]
BACK_PAW = (0.535, 0.295)             # heel y, toe y
PAW = {"z": 0.064, "half_width": 0.098, "top": 0.064, "bottom": 0.064, "e": 2.6, "rings": 7}

LOFT_STYLE = {"torso_dome": 0.35, "head_dome": 0.5, "tail": (2.1, 0.7), "front_leg": (2.2, 0.3),
              "back_thigh": (2.3, 0.3), "back_leg": (2.2, 0.3), "ear": (1.7, 0.6)}

FIELD = [
    ("core", 0.0, [("torso", 0.0), ("head", 0.045), ("tail", 0.05)]),
    ("front_L", 0.06, [("front_upper_L", 0.0), ("front_fore_L", 0.035), ("front_pastern_L", 0.025), ("front_paw_L", 0.03)]),
    ("front_R", 0.06, [("front_upper_R", 0.0), ("front_fore_R", 0.035), ("front_pastern_R", 0.025), ("front_paw_R", 0.03)]),
    ("back_L", 0.05, [("back_thigh_L", 0.0), ("back_gaskin_L", 0.04), ("back_meta_L", 0.03), ("back_paw_L", 0.03)]),
    ("back_R", 0.05, [("back_thigh_R", 0.0), ("back_gaskin_R", 0.04), ("back_meta_R", 0.03), ("back_paw_R", 0.03)]),
]

# ----------------------------------------------------------------------------- muzzle and mouth
# GSD: the Golden's mouth on the longer muzzle (the lip line runs 3 cm further forward)
HINGE = (-0.53, 1.12)
LIP_RUN = 0.36
LIP_DROP = 0.055
LIP_BAND = 0.013
MOUTH_MAX_OPEN_DEG = 34.0
MOUTH_ZONE = {"below_hinge": 0.22, "behind_hinge": 0.10}
JAW_TIP = (-0.81, 1.07)

TONGUE = {
    "pivot": (-0.565, 1.13),
    "rings": [(-0.565, 0.012, 0.056, 0.016), (-0.645, 0.016, 0.076, 0.020), (-0.745, 0.018, 0.078, 0.021),
              (-0.835, 0.016, 0.074, 0.019), (-0.882, -0.012, 0.062, 0.016), (-0.900, -0.052, 0.046, 0.014)],
    "lod_rings": (0, 2, 4, 5),
    "e": 2.6,
}

# ----------------------------------------------------------------------------- ears, eyes, nose, collar
# GSD: tall erect ears (0.36 m root to tip, 0.21 m wide at the base), set high on the skull,
# leaning a little out and opening forward. "side" turns the ear's width to run across the head
# (the kit's default runs it front-to-back, for a hanging ear); "swing" turns the clips' ear
# motion (written for hanging ears) around so an erect ear lays back and flicks out.
EARS = {
    "rings": [(0.140, -0.380, 1.480, 0.100, 0.042), (0.160, -0.388, 1.560, 0.122, 0.040),
              (0.183, -0.396, 1.645, 0.104, 0.033), (0.207, -0.404, 1.725, 0.072, 0.026),
              (0.229, -0.410, 1.790, 0.040, 0.018), (0.244, -0.414, 1.835, 0.012, 0.010)],
    "lod_rings": (0, 1, 3, 4, 5),
    "bones": [(0.140, -0.380, 1.480), (0.183, -0.396, 1.645), (0.244, -0.414, 1.835)],
    "side": (1.0, 0.50, 0.0),
    "swing": (-0.7, -1.0, 1.0),
}
EYES = {"ray_x": 0.150, "ray_z": 1.400, "face_bias": 0.6, "inset": 0.008, "radii": (0.056, 0.033, 0.064),
        "shine_offset": (0.026, 0.028, 0.016), "shine_radii": (0.012, 0.010, 0.012)}
NOSE = {"ray_z": 1.268, "offset": (0.012, 0.006), "length": 0.040, "radii": (0.080, 0.052, 0.042), "e": 2.4}
COLLAR = {"ring": 13, "gap": 0.014, "gap_lod": 0.022, "strap": (0.034, 0.012), "e": 3.0,
          "tag_radii": (0.038, 0.008), "tag_out": 0.020, "tag_drop": 0.042}

# ----------------------------------------------------------------------------- skeleton
SPINE = {
    "hips": ((0.42, 0.80), (0.60, 0.82)),
    "spine_1": ((0.42, 0.80), (0.13, 0.80)),
    "spine_2": ((0.13, 0.80), (-0.17, 0.82)),
    "spine_3": ((-0.17, 0.82), (-0.335, 0.96)),
    "neck": ((-0.335, 0.96), (-0.37, 1.15)),
    "head": ((-0.37, 1.15), (-0.62, 1.30)),
}
# GSD: the tail bones follow the low, curved tail
TAIL_BONES = [(0.58, 0.835), (0.71, 0.752), (0.805, 0.625), (0.873, 0.500), (0.955, 0.422)]
JOINTS = {
    "front_shoulder": (-0.200, 0.80), "front_elbow": (-0.122, 0.50), "front_wrist": (-0.146, 0.17),
    "front_paw": (-0.170, 0.075), "front_toe": (-0.315, 0.045),
    "back_hip": (0.445, 0.80), "back_stifle": (0.335, 0.485), "back_hock": (0.530, 0.255),
    "back_foot": (0.502, 0.085), "back_toe": (0.315, 0.045),
}
TORSO_CHAIN = [(0.60, 0.82), (0.42, 0.80), (0.13, 0.80), (-0.17, 0.82), (-0.335, 0.96), (-0.335, 1.15)]

# ----------------------------------------------------------------------------- sit pose
SIT = {"hips": -60.7943, "drop": -0.5414, "shoulder": -8.0, "stifle_z": 0.1, "hock_z": 0.0406, "foot_pitch": 14.715, "tail": 92.9265}
SIT_POSE = {"spine_3": 4, "neck": 28, "head": 24, "tail_2": 12, "tail_3": 6}

# ----------------------------------------------------------------------------- lie pose (Rest)
# Solved against the skinned mesh by kit/solve_lie.py (paste its output here after changing the
# shape or the rig). The hip tilt, the stifle height and tail_2 / tail_3 are chosen; the rest are
# solved.
LIE = {"hips": 0.0, "drop": -0.5197, "elbow_z": 0.0841, "wrist_z": 0.0735, "stifle_z": 0.1, "hock_z": 0.0406, "foot_pitch": 14.715, "tail": 8.736, "tail_2": 12, "tail_3": 6}

# ----------------------------------------------------------------------------- checks
SANITY = {"head_min_z": 0.85, "tail_min_y": 0.45}
MEASURE = {"rump_min_abs_x": 0.10, "back_y": (0.0, 0.6)}

# ----------------------------------------------------------------------------- coat
# sRGB hex: main coat, "feathering" (the paler underside: chest, belly, leg backs, tail
# underside), ears / dark channel (the ears, and with FUR_PATCHES the saddle and mask).
# The keys are the game's coat slots: Default = classic black-and-tan, Black = solid black.
COATS = {
    "default": ("#AA6A38", "#CC9C66", "#221D1A"),
    "black":   ("#2A2725", "#302C29", "#252220"),
}
COAT_LABELS = {"default": "Default (black-and-tan)", "black": "Black"}
FUR_FLOW = {
    "body": ((0.0, 1.0, -0.30), ("body", "head")),
    "leg": ((0.0, 0.15, -1.0), ("fleg", "bleg", "thigh", "paw")),
    "tail": ((0.0, 0.6, -1.0), ("tail",)),
    "ear": ((0.0, 0.0, 1.0), ("ear",)),
}
# GSD: the ears are dark to the tip (ear_tip_z is above any ear)
FUR_MARKINGS = {"bib_y": (-0.12, -0.26), "rump_y": (0.50, 0.60), "muzzle_y": (-0.57, -0.69),
                "crown_y": (-0.47, -0.36), "pale_z": (0.08, 0.26), "tail_tip_y": (0.62, 0.76),
                "ear_tip_z": (2.5, 2.6)}
# GSD: the medium coat, between the Lab's short coat and the Golden's long one
FUR_STRAND = {"fine": (0.085, 0.55), "mid": (0.26, 1.2), "clump": (0.75, 2.0), "low": (5.5, 8.0),
              "mix": (0.50, 0.35, 0.22), "normal": 0.28, "contrast": 0.26}
# GSD: the black-and-tan pattern, laid over the coat's dark channel (kit FUR_PATCHES). On the
# Black coat every channel is black, so the same mask gives a solid coat.
FUR_PATCHES = [
    # the saddle: the back, the top of the neck and the upper flanks and croup
    {"regions": ("body", "thigh"), "line": [(-0.42, 1.24), (-0.36, 1.13), (-0.30, 1.03), (-0.20, 0.93),
                                           (-0.05, 0.85), (0.20, 0.83), (0.40, 0.84), (0.55, 0.83),
                                           (0.65, 0.80)],
     "soft": 0.035, "wobble": 0.030},
    # the tail: dark over the top and back, a little tan underneath near the root
    {"regions": ("tail",), "n": ("y", -0.75, -0.1), "strength": 0.95},
    # the mask: the muzzle, and up between the eyes to the dark crown
    {"regions": ("head",), "y": (-0.56, -0.64)},
    {"regions": ("head",), "z": (1.36, 1.44), "x_max": 0.075, "y": (-0.36, -0.46), "soft": 0.03},
    {"regions": ("head",), "z": (1.49, 1.57), "strength": 0.9},
    # the back of the skull, behind the ears, joins the dark top of the neck
    {"regions": ("head",), "z": (1.28, 1.40), "n": ("y", 0.15, 0.55), "strength": 0.9},
    # the inner face of the ears is paler
    {"regions": ("ear",), "op": "light", "n": ("y", -0.25, -0.7), "strength": 0.55},
]
TRIM_COLOURS = {"eye": "#1C1411", "iris": "#4A2C1A", "pupil": "#140E0B", "shine": "#FFFFFF", "nose": "#24201F",
                "tag": "#D6AC45", "mouth": "#5E2B2C", "lip": "#2C2120", "tongue": "#E7848A",
                "collar": "#C3282E", "stitch": "#E8B0A8"}

# ----------------------------------------------------------------------------- motion
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
# GSD: the low tail swings back clear of the ground on the way down and trails the hips by fewer
# frames (kit defaults: tail_lift 18 deg, tail_lag 6 frames). Lowest SitDown point -6.1 mm (README)
SIT_DOWN = {"rock_radius": 0.62, "dip": 0.025, "hind_step_lift": 0.035, "front_step_lift": 0.05,
            "tail_lift": 60, "tail_lag": 0}
PLAY_BOW = {"elbow_z": 0.115, "wrist_z": 0.095}

# ----------------------------------------------------------------------------- review renders
RENDER = {
    "head_box": ((-0.38, -0.80, 1.00), (0.38, -0.12, 1.88)),
    "mouth_box": ((-0.2, -0.80, 0.98), (0.2, -0.45, 1.20)),
    "hips_box": {"rest": ((-0.40, 0.05, 0.0), (0.40, 1.00, 1.0)), "sit": ((-0.40, -0.10, 0.0), (0.40, 1.25, 0.80))},
    "body_box": ((-0.4, -0.95, 0.0), (0.4, 1.05, 1.88)),
    "chest_box": ((-0.42, -0.85, 0.0), (0.42, 0.30, 1.88)),
    "chest_cut_y": -0.08,
    "join_views": [("Shoulder", "front_shoulder_L", (0.02, 0.05, -0.12), (0.24, 0.30, 0.34), -72, 6),
                   ("Hip / haunch", "back_hip_L", (0.02, 0.03, -0.16), (0.24, 0.33, 0.38), -118, 8),
                   ("Tail base", "tail_1", (0.0, 0.04, -0.10), (0.22, 0.26, 0.30), -150, 24)],
    "sit_closeups": {"hind": ("back_hock_L", 0.60, 0.55), "front": ("front_paw_L", 0.45, 0.40)},
    "before": {},
    "captions": {
        "turnaround": ("German Shepherd - turnaround (orthographic)",
                       "Round 1 on the shared kit: erect ears, long wedge muzzle, sloping back, angulated hind legs, "
                       "low bushy tail. Black-and-tan (Default)."),
        "gamecam": ("German Shepherd at the game camera",
                    "Orthographic, 45 deg pitch, yaw -40. Approved base 1 (level 1), mailbox and hydrant for scale."),
        "face": ("German Shepherd - face, 3/4",
                 "Erect ears, flat forehead with little stop, long wedge muzzle with a dark mask; "
                 "the Golden's mouth; jaw open 12 deg."),
        "coats": ("Coats and puppy",
                  "Default (black-and-tan) and Black. One mesh: a coat is a fur colour map; normal map shared. "
                  "Puppy = adult x 0.55."),
        "splash": ("3D concept vs splash", "No German Shepherd splash art yet."),
        "mouth": ("Mouth", "The Golden's mouth on the longer muzzle: lips meet at the corners on the hinge."),
        "mouth_rows": ("Open (default pose, jaw 12 deg)", "Closed (jaw bone at 0, tongue hidden)"),
        "hips": ("Hip study - hind leg (skinned), sit",
                 "Red dots = joints: hips, hip, stifle, hock, foot (both legs), tail base."),
        "hips_notes": ["A more angulated hind leg than the Golden's: the stifle sits further forward and the hock "
                       "and rear foot further back,",
                       "with a longer lower thigh; hip and paw heights unchanged. The low bushy tail leaves the "
                       "sloping croup.",
                       "Sit: the hips drop and tilt so the rump rests on the ground; hock and rear foot lie flat, "
                       "front paws stay planted.",
                       "Contact is measured on the skinned mesh and checked at export (see sit.png)."],
        "joins": ("Joins - shoulder, hip / haunch, tail base",
                  "One skin: the volumes blend with a smooth minimum, then retopologise."),
        "joins_rows": ("Rest", "Bent (sit): the junctions stay smooth"),
        "sit": ("Sit - ground contact",
                "Side views from a level camera at ground height; the dark line is the ground plane."),
        "chest": ("Chest", "Orthographic: front, front section, side, 3/4."),
        "chest_row": "German Shepherd: the Golden's chest under a fuller neck ruff",
    },
}
