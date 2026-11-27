"""Named dimensions of the Blender delivery truck (#924). Plain Python, no Blender.

Units are metres at in-game size, Blender axes: front faces -Y, up is +Z, so the
truck's left side is +X and its right (kerb) side is -X. The pivot is on the
ground at the centre of the footprint.

The footprint is NOT re-typed here: LENGTH and WIDTH are read from Core's
DeliveryTruckFootprint.cs (the Kenney "delivery" model's import size times
ModelScale, #547/#660), so the new model keeps today's crosswalk fit (#660) and
lane fit (#672). HEIGHT matches the Kenney model at the same scale.
"""
import os
import re

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.normpath(os.path.join(HERE, "..", "..", "..", ".."))
CORE_WORLD = os.path.join(REPO, "Assets", "Scripts", "Core", "World")
CORE_ART = os.path.join(REPO, "Assets", "Scripts", "Core", "Art")


def _const(path, name):
    src = open(path, encoding="utf-8").read()
    m = re.search(r"const\s+(?:float|int)\s+%s\s*=\s*([0-9.]+)f?\s*;" % name, src)
    if not m:
        raise ValueError("%s not found in %s" % (name, path))
    return float(m.group(1))


def core_footprint():
    """(length, width) of today's truck body in metres, from Core."""
    f = os.path.join(CORE_WORLD, "DeliveryTruckFootprint.cs")
    scale = _const(f, "ModelScale")
    return _const(f, "ModelLengthAtImportScale") * scale, _const(f, "ModelWidthAtImportScale") * scale


def core_road_width():
    return _const(os.path.join(CORE_WORLD, "WorldDimensions.cs"), "RoadWidth")


def core_max_tris_per_mesh():
    """The per-mesh triangle cap Unity enforces today (ArtBudget, LowPolyBudgetTests)."""
    return int(_const(os.path.join(CORE_ART, "ArtBudget.cs"), "MaxTrianglesPerMesh"))


LENGTH, WIDTH = core_footprint()          # 4.875 x 2.25 m
HALF_L = LENGTH / 2
HALF_W = WIDTH / 2
KENNEY_HEIGHT_AT_IMPORT = 1.65            # delivery.fbx body top (Z max), measured in Blender
KENNEY_MODEL_SCALE = 1.5                  # DeliveryTruckFootprint.ModelScale
HEIGHT = round(KENNEY_HEIGHT_AT_IMPORT * KENNEY_MODEL_SCALE, 4)   # 2.475 m

# Art budget (#799): provisional 6,000-triangle backstop for the whole truck. Each
# mesh also stays under today's enforced ArtBudget.MaxTrianglesPerMesh (2,000).
BUDGET_TRIS = 6000

# --- Wheels -----------------------------------------------------------------
WHEEL_RADIUS = 0.42
WHEEL_WIDTH = 0.30
WHEEL_SIDES = 24
WHEEL_TRACK_X = 0.90                      # wheel centre, either side of the centreline
AXLE_FRONT_Y = -1.50
AXLE_REAR_Y = 1.05
AXLE_Z = WHEEL_RADIUS                     # the tyre touches the ground
HUB_RADIUS = 0.25
HUB_SIDES = 10
HUB_PROUD = 0.012                         # hub face past the tyre's outer sidewall
CAP_RADIUS = 0.07
CAP_SIDES = 6
CAP_PROUD = 0.035
LUG_COUNT = 5
LUG_RING = 0.15
LUG_SIZE = 0.04
LUG_PROUD = 0.025

WHEEL_PIVOTS = {
    "wheel_front_left": (WHEEL_TRACK_X, AXLE_FRONT_Y, AXLE_Z),
    "wheel_front_right": (-WHEEL_TRACK_X, AXLE_FRONT_Y, AXLE_Z),
    "wheel_rear_left": (WHEEL_TRACK_X, AXLE_REAR_Y, AXLE_Z),
    "wheel_rear_right": (-WHEEL_TRACK_X, AXLE_REAR_Y, AXLE_Z),
}

# --- Body -------------------------------------------------------------------
BODY_BOTTOM = AXLE_Z                      # sill line = wheel-arch spring line
ARCH_RADIUS = 0.50
ARCH_SEGMENTS = 8

CAB_FRONT_Y = -2.30
CAB_REAR_Y = -0.78                        # 2 cm into the cargo box, so no coplanar faces
CAB_HALF_W = 1.02
CAB_PROFILE = [                           # (y, z), front bumper face to cab roof
    (CAB_FRONT_Y, 1.22), (-2.22, 1.30),   # hood front edge
    (-1.62, 1.38),                        # windscreen base
    (-1.22, 2.18),                        # windscreen top
    (-1.10, 2.26), (CAB_REAR_Y, 2.26),    # roof
]
WINDSCREEN = ((-1.62, 1.38), (-1.22, 2.18))
WINDSCREEN_INSET = 0.07                   # glass stops this far short of every edge
GLASS_T = 0.012
SIDE_WINDOW = {"z0": 1.45, "z1": 2.08, "rear_y": -0.92, "front_inset": 0.10}

BOX_FRONT_Y = -0.80
BOX_REAR_Y = 2.32                         # the rear face the door closes into
BOX_CHAMFER = 0.10                        # top front edge of the box
HOLD_DEPTH = 0.60                         # open-door cargo hold, behind the door
POST_X = 0.95                             # inner face of the rear door posts

CHASSIS = {"half_w": 0.80, "y0": -2.20, "y1": 2.20, "z0": 0.30}
WELL_HALF_W = WHEEL_TRACK_X - WHEEL_WIDTH / 2 - 0.01     # dark wheel-well liner, inside the tyres

BUMPER_FRONT = {"y1": -2.28, "z0": 0.28, "z1": 0.60, "half_w": 1.04}
BUMPER_REAR = {"y0": 2.28, "z0": 0.28, "z1": 0.50, "half_w": 1.08}
GRILLE = {"half_w": 0.55, "z0": 0.70, "z1": 1.12}
HEADLIGHT = {"x0": 0.68, "x1": 0.97, "z0": 0.86, "z1": 1.12}
FRONT_PROUD = 0.025                       # grille / headlights stand off the cab front
TAILLIGHT = {"x0": 0.98, "x1": 1.10, "z0": 0.62, "z1": 1.00, "proud": 0.015}
MIRROR = {"y0": -1.66, "y1": -1.58, "z0": 1.50, "z1": 1.86, "arm_z": 1.68, "arm_h": 0.05,
          "head_x0": HALF_W - 0.05}
CAB_HANDLE = {"y0": -1.02, "y1": -0.90, "z0": 1.25, "z1": 1.30, "proud": 0.015}

# --- Rear door (one swing door, hinged on the truck's left, +X) ----------------
DOOR_WIDTH = 2 * POST_X                   # 1.90 m: fills the opening between the posts
DOOR_BOTTOM = 0.55                        # top of the cargo floor sill
DOOR_TOP = 2.30
DOOR_T = 0.05
DOOR_HINGE = (POST_X, BOX_REAR_Y, DOOR_BOTTOM)   # the left post's inner edge, at the rear face
# Open = this rotation about the hinge's vertical (Blender +Z) axis. The door
# swings out to lie straight back behind the truck along its left side, so an
# open door stays inside the truck's width and leaves the kerb side clear.
DOOR_OPEN_DEG = -90.0
DOOR_HANDLE = {"x0": -1.80, "x1": -1.76, "z0": 0.55, "z1": 1.05, "proud": 0.03}
DOOR_HINGE_KNUCKLE = {"size": 0.05, "z": (0.25, 1.45)}    # heights above the door bottom
# z: rib heights as fractions of the door height
DOOR_RIB = {"z": (0.33, 0.66), "h": 0.04, "proud": 0.008, "inset": 0.08}

# --- Materials: slot -> (hex, roughness) ---------------------------------------
# body is white so Unity's material colour multiply gives exactly
# Palette.CarColorHex (#601); everything else is untinted.
MATERIALS = {
    "body": ("#FFFFFF", 0.45),
    "glass": ("#1F2A33", 0.12),
    "tyre": ("#262628", 0.90),
    "trim": ("#3A3C40", 0.70),
    "hub": ("#B9BDC2", 0.35),
    "headlight": ("#F2EFE2", 0.20),
    "taillight": ("#B3242A", 0.30),
}
MATERIAL_PREFIX = "DH_Truck_"

def core_car_colors():
    """[(name, hex)] of Palette.CarColorHex (#601), in index order, read from Core."""
    src = open(os.path.join(CORE_ART, "Palette.cs"), encoding="utf-8").read()
    block = src[src.index("CarColors ="):]
    block = block[:block.index("};")]
    return [(n.strip(), "#" + h) for h, n in re.findall(r'"#([0-9A-Fa-f]{6})",\s*//\s*([^\n]+)', block)]


# The seven car colours, for the review renders and the viewer.
CAR_COLORS = core_car_colors()
