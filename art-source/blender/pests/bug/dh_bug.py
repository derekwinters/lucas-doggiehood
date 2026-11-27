"""Doggiehood bug (#334): a chunky cartoon fly and its looping WingFlap clip.

Everything is built from the named numbers below; there is no randomness, so the same script gives
the same model and clip every run. Conventions follow docs/engineering/blender-pipeline.md:
1 unit = 1 m (in-game size), front -Y, up +Z, pivot at the centre of the thorax (the bug flies, so
there is no ground contact). The game moves the root along its flight path; the clip only flaps
the wings and bobs the body under it.

Objects (each a glTF node):
  pest_bug              root empty, never animated
    pest_bug_body       head, eyes, thorax, striped abdomen, legs, antennae (one mesh); bobs
      pest_bug_wing_L   left wing (+X, the bug faces -Y), hinged on the thorax; flaps
      pest_bug_wing_R   right wing (-X)
"""
import math
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.normpath(os.path.join(HERE, "..", "..")))   # the house kit (read-only)
import bpy  # noqa: E402
from mathutils import Euler, Vector  # noqa: E402
import dh_house_kit as K  # noqa: E402

NAME = "pest_bug"
CLIP = "WingFlap"
FPS = 30

# --- Palette (sRGB hex). A green-bottle fly: reads against grey, brown and terracotta roofs. ---
COLOURS = {
    "body": ("#2E9A6E", 0.45),       # head + thorax, glossy
    "band_light": ("#5CC48E", 0.5),  # abdomen stripes
    "band_dark": ("#1E6B50", 0.5),
    "eye_white": ("#F6F3EA", 0.25),
    "pupil": ("#1B1C22", 0.2),
    "shine": ("#FFFFFF", 0.2),       # the catch-light in each eye
    "leg": ("#2A2D33", 0.7),         # legs and antennae
    "wing": ("#D8ECF4", 0.3),        # pale blue-white, opaque
}

# --- Body (metres). Each part is an ellipsoid: centre (x, y, z), radii (x, y, z), pitch (deg). ---
SPHERE_SEGMENTS = 12          # around
SPHERE_RINGS = 8              # pole to pole
SMALL_SEGMENTS, SMALL_RINGS = 8, 5    # pupils and antenna tips
TINY_SEGMENTS, TINY_RINGS = 6, 4      # catch-lights
THORAX = ((0.0, 0.0, 0.0), (0.12, 0.12, 0.11), 0.0)
HEAD = ((0.0, -0.165, 0.03), (0.105, 0.095, 0.10), 0.0)
ABDOMEN = ((0.0, 0.20, -0.02), (0.115, 0.165, 0.10), -8.0)
ABDOMEN_BANDS = 5             # alternating light / dark stripes, front to back
EYE_X, EYE_Y, EYE_Z, EYE_R = 0.062, -0.215, 0.085, 0.072      # big cartoon eyes on the head front
PUPIL_FORWARD, PUPIL_R = 0.052, 0.040                          # pupil sits this far out along the eye
PUPIL_IN = 0.010                                               # pupils look a touch cross-eyed: friendly
SHINE_OFFSET, SHINE_R = (0.012, -0.012, 0.022), 0.012          # catch-light, from the pupil centre

# --- Legs: three pairs under the thorax, two segments each (hip, knee, foot). ---
LEG_SIDES = 5
LEG_R = (0.016, 0.013, 0.010)
LEGS = (   # (hip y, knee (x, y, z), foot (x, y, z)) for the +X side; mirrored for -X
    (-0.06, (0.13, -0.11, -0.09), (0.15, -0.15, -0.17)),
    (0.00, (0.15, 0.00, -0.08), (0.18, 0.01, -0.17)),
    (0.06, (0.13, 0.10, -0.09), (0.15, 0.15, -0.17)),
)
HIP_X, HIP_Z = 0.06, -0.08

# --- Antennae: short stalks with a ball tip, from the top of the head. ---
ANTENNA_BASE = (0.03, -0.20, 0.115)
ANTENNA_TIP = (0.07, -0.27, 0.21)
ANTENNA_R = (0.010, 0.007)
ANTENNA_BALL_R = 0.022

# --- Wings: a flat teardrop, hinged at the top of the thorax, swept back. ---
WING_HINGE = (0.045, 0.01, 0.10)  # +X side; mirrored for the right wing
WING_LENGTH = 0.30
WING_WIDTH = 0.15                 # widest across
WING_THICK = 0.012                # a closed, thin solid, so it needs no double-sided material
WING_OUTLINE_STEPS = 10
WING_SWEEP_DEG = 38.0             # back from straight out sideways
WING_WIDE_AT = 0.62               # widest point along the wing (0 hinge .. 1 tip)

# --- The WingFlap clip: 24 frames at 30 fps (0.8 s), frame 24 == frame 0. ---
CLIP_FRAMES = 24
FLAP_BEATS = 4                    # wing beats per loop (6 frames each, 5 per second)
FLAP_REST_DEG = -20.0             # wing roll at the middle of the beat (negative = up)
FLAP_AMPLITUDE_DEG = 45.0         # +/- around it: from 65 deg up (frame 0) to 25 deg down
BOB_BEATS = 1                     # body bobs per loop
BOB_AMPLITUDE_M = 0.025           # +/- height
PITCH_AMPLITUDE_DEG = 4.0         # the body rocks nose-up / nose-down with the bob


# ---------------------------------------------------------------------------
# Mesh pieces: plain vertex / face lists, faces carry a material key
# ---------------------------------------------------------------------------
class Mesh:
    def __init__(self):
        self.v, self.f, self.m, self.smooth = [], [], [], []

    def add(self, verts, faces, mats, smooth=True):
        o = len(self.v)
        self.v += [tuple(p) for p in verts]
        self.f += [tuple(i + o for i in fc) for fc in faces]
        self.m += mats if isinstance(mats, list) else [mats] * len(faces)
        self.smooth += [smooth] * len(faces)


def ellipsoid(centre, radii, pitch_deg, mat, segs=SPHERE_SEGMENTS, rings=SPHERE_RINGS, band_mats=None):
    """Lat-long ellipsoid. With band_mats, faces alternate materials in bands along its Y axis."""
    rot = Euler((math.radians(pitch_deg), 0, 0)).to_matrix()
    c = Vector(centre)
    verts = [(0, -1, 0)]
    for r in range(1, rings):
        a = math.pi * r / rings
        for s in range(segs):
            b = 2 * math.pi * s / segs
            verts.append((math.sin(a) * math.cos(b), -math.cos(a), math.sin(a) * math.sin(b)))
    verts.append((0, 1, 0))
    last = len(verts) - 1
    faces, ys = [], []
    for s in range(segs):
        faces.append((0, 1 + (s + 1) % segs, 1 + s))
        ys.append(-1.0)
    for r in range(rings - 2):
        for s in range(segs):
            a0, a1 = 1 + r * segs + s, 1 + r * segs + (s + 1) % segs
            faces.append((a0, a1, a1 + segs, a0 + segs))
            ys.append(-math.cos(math.pi * (r + 1.5) / rings))
    for s in range(segs):
        b = 1 + (rings - 2) * segs
        faces.append((b + s, b + (s + 1) % segs, last))
        ys.append(1.0)
    faces = [tuple(reversed(fc)) for fc in faces]       # outward normals
    world = [c + rot @ Vector((p[0] * radii[0], p[1] * radii[1], p[2] * radii[2])) for p in verts]
    if band_mats:
        n = len(band_mats)
        mats = [band_mats[min(n - 1, int((y + 1) / 2 * n))] for y in ys]
    else:
        mats = mat
    return world, faces, mats


def tube(points, radii, mat, sides=LEG_SIDES):
    """A tapered tube along a polyline, capped at both ends."""
    pts = [Vector(p) for p in points]
    verts, faces = [], []
    for k, p in enumerate(pts):
        d = (pts[min(k + 1, len(pts) - 1)] - pts[max(k - 1, 0)]).normalized()
        ref = Vector((0, 0, 1)) if abs(d.z) < 0.9 else Vector((1, 0, 0))
        u = d.cross(ref).normalized()
        w = d.cross(u)
        for s in range(sides):
            a = 2 * math.pi * s / sides
            verts.append(p + (u * math.cos(a) + w * math.sin(a)) * radii[k])
    for k in range(len(pts) - 1):
        for s in range(sides):
            a0, a1 = k * sides + s, k * sides + (s + 1) % sides
            faces.append((a0, a1, a1 + sides, a0 + sides))
    faces.append(tuple(reversed(range(sides))))
    faces.append(tuple(range((len(pts) - 1) * sides, len(pts) * sides)))
    return verts, faces, mat


def mirror(p):
    return (-p[0], p[1], p[2])


def body_mesh():
    m = Mesh()
    m.add(*ellipsoid(*THORAX, "body"))
    m.add(*ellipsoid(*HEAD, "body"))
    bands = ["band_light" if i % 2 == 0 else "band_dark" for i in range(ABDOMEN_BANDS)]
    m.add(*ellipsoid(*ABDOMEN, None, band_mats=bands))
    for sx in (1, -1):
        eye = Vector((sx * EYE_X, EYE_Y, EYE_Z))
        m.add(*ellipsoid(tuple(eye), (EYE_R,) * 3, 0.0, "eye_white"))
        look = Vector((-sx * PUPIL_IN, -1.0, 0.0)).normalized()
        pupil = eye + look * PUPIL_FORWARD
        m.add(*ellipsoid(tuple(pupil), (PUPIL_R,) * 3, 0.0, "pupil", SMALL_SEGMENTS, SMALL_RINGS))
        shine = pupil + Vector((sx * SHINE_OFFSET[0], SHINE_OFFSET[1], SHINE_OFFSET[2]))
        m.add(*ellipsoid(tuple(shine), (SHINE_R,) * 3, 0.0, "shine", TINY_SEGMENTS, TINY_RINGS))
        for hip_y, knee, foot in LEGS:
            hip = (HIP_X, hip_y, HIP_Z)
            pts = [hip, knee, foot]
            if sx < 0:
                pts = [mirror(p) for p in pts]
            m.add(*tube(pts, LEG_R, "leg"))
        base, tip = ANTENNA_BASE, ANTENNA_TIP
        if sx < 0:
            base, tip = mirror(base), mirror(tip)
        m.add(*tube([base, tip], ANTENNA_R, "leg"))
        m.add(*ellipsoid(tip, (ANTENNA_BALL_R,) * 3, 0.0, "leg", SMALL_SEGMENTS, SMALL_RINGS))
    return m


def wing_mesh(side):
    """The wing in its object's space: hinge at the origin, reaching out (+X for side 1) and back."""
    n = WING_OUTLINE_STEPS
    sweep = math.radians(WING_SWEEP_DEG)
    span, across = [], []
    for i in range(n + 1):
        u = i / n
        if u <= WING_WIDE_AT:
            w = math.sin(0.5 * math.pi * u / WING_WIDE_AT)
        else:
            w = math.cos(0.5 * math.pi * (u - WING_WIDE_AT) / (1 - WING_WIDE_AT)) ** 0.6
        span.append(u * WING_LENGTH)
        across.append(0.5 * WING_WIDTH * w)
    # outline loop: leading edge hinge->tip, trailing edge tip->hinge (skip duplicated ends)
    loop = [(span[i], -across[i]) for i in range(n + 1)] + [(span[i], across[i]) for i in range(n - 1, 0, -1)]
    L = len(loop)
    verts = []
    for z in (WING_THICK / 2, -WING_THICK / 2):
        for x, y in loop:
            # sweep back: rotate the out-along-X wing toward +Y
            xr = x * math.cos(sweep) - y * math.sin(sweep)
            yr = x * math.sin(sweep) + y * math.cos(sweep)
            verts.append((side * xr, yr, z))
    faces = []
    # top and bottom as quad strips between the two edges, closed with triangles at the ends
    top = []
    for i in range(1, n - 1):
        a, b = i, i + 1                          # leading edge
        c, d = L - i - 1, L - i                  # trailing edge partners of b and a
        top.append((a, b, c, d))
    top.append((0, 1, L - 1))
    top.append((n - 1, n, n + 1))
    for f in top:
        f = f if side > 0 else tuple(reversed(f))
        faces.append(f)                                 # top (+Z)
        faces.append(tuple(reversed([i + L for i in f])))  # bottom
    for i in range(L):
        j = (i + 1) % L
        f = (j, i, i + L, j + L)
        faces.append(f if side > 0 else tuple(reversed(f)))
    m = Mesh()
    m.add(verts, faces, "wing", smooth=False)
    return m


# ---------------------------------------------------------------------------
# Blender objects
# ---------------------------------------------------------------------------
def material(name, hex_color, roughness):
    """A flat-colour, single-sided material (default specular, so the glTF needs no extension)."""
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    bsdf = mat.node_tree.nodes["Principled BSDF"]
    col = K.hex_rgba(hex_color)
    bsdf.inputs["Base Color"].default_value = col
    bsdf.inputs["Roughness"].default_value = roughness
    mat.diffuse_color = col
    mat.use_backface_culling = True
    return mat


def materials():
    return {k: material("DH_Pest_bug_%s" % k, h, r) for k, (h, r) in COLOURS.items()}


def to_object(name, mesh, mats, parent=None, location=(0, 0, 0)):
    me = bpy.data.meshes.new(name)
    me.from_pydata(mesh.v, [], mesh.f)
    keys = []
    for k in mesh.m:
        if k not in keys:
            keys.append(k)
    for k in keys:
        me.materials.append(mats[k])
    for p, k, s in zip(me.polygons, mesh.m, mesh.smooth):
        p.material_index = keys.index(k)
        p.use_smooth = s
    me.validate()
    me.update()
    ob = bpy.data.objects.new(name, me)
    bpy.context.scene.collection.objects.link(ob)
    ob.parent = parent
    ob.location = location
    return ob


def build(name=NAME):
    """Build the bug at rest (frame 0 of the clip) and return {"root", "body", "wing_L", "wing_R"}."""
    mats = materials()
    root = bpy.data.objects.new(name, None)
    root.empty_display_type = "PLAIN_AXES"
    bpy.context.scene.collection.objects.link(root)
    body = to_object(name + "_body", body_mesh(), mats, parent=root)
    wl = to_object(name + "_wing_L", wing_mesh(1), mats, parent=body, location=WING_HINGE)
    wr = to_object(name + "_wing_R", wing_mesh(-1), mats, parent=body, location=mirror(WING_HINGE))
    for o in (body, wl, wr):
        o.rotation_mode = "XYZ"
    parts = {"root": root, "body": body, "wing_L": wl, "wing_R": wr}
    pose(parts, 0)
    bpy.context.view_layer.update()
    return parts


# ---------------------------------------------------------------------------
# The WingFlap clip
# ---------------------------------------------------------------------------
def clip_values(frame):
    """The clip at `frame` (any number; it repeats every CLIP_FRAMES): body z, body pitch and
    wing roll, in metres / radians. Computed from the phase in the loop, so frame N == frame 0."""
    ph = (frame % CLIP_FRAMES) / CLIP_FRAMES
    flap = math.radians(FLAP_REST_DEG - FLAP_AMPLITUDE_DEG * math.cos(2 * math.pi * FLAP_BEATS * ph))
    bob = BOB_AMPLITUDE_M * math.sin(2 * math.pi * BOB_BEATS * ph)
    pitch = math.radians(PITCH_AMPLITUDE_DEG * math.cos(2 * math.pi * BOB_BEATS * ph))
    return bob, pitch, flap


def pose(parts, frame):
    """Set the parts' transforms to the clip at `frame` (also used to pose still renders)."""
    bob, pitch, flap = clip_values(frame)
    parts["body"].location = (0.0, 0.0, bob)
    parts["body"].rotation_euler = (pitch, 0.0, 0.0)
    # a roll about +Y by a negative angle raises a +X (left) wing tip; the right wing mirrors it
    parts["wing_L"].rotation_euler = (0.0, flap, 0.0)
    parts["wing_R"].rotation_euler = (0.0, -flap, 0.0)


def make_clip(parts):
    """Key every frame 0..CLIP_FRAMES (linear) into one action, one slot per animated object."""
    sc = bpy.context.scene
    sc.render.fps = FPS
    sc.frame_start, sc.frame_end = 0, CLIP_FRAMES
    act = bpy.data.actions.new(CLIP)
    act.use_fake_user = True
    animated = (parts["body"], parts["wing_L"], parts["wing_R"])
    for ob in animated:
        ob.animation_data_create().action = act
    for f in range(CLIP_FRAMES + 1):
        pose(parts, f)
        parts["body"].keyframe_insert("location", frame=f)
        for ob in animated:
            ob.keyframe_insert("rotation_euler", frame=f)
    for fc in all_fcurves(act):
        for kp in fc.keyframe_points:
            kp.interpolation = "LINEAR"
    pose(parts, 0)
    return {"name": CLIP, "frames": CLIP_FRAMES, "fps": FPS, "loop": True,
            "duration_s": round(CLIP_FRAMES / FPS, 4), "flap_beats": FLAP_BEATS,
            "flap_deg": [FLAP_REST_DEG - FLAP_AMPLITUDE_DEG, FLAP_REST_DEG + FLAP_AMPLITUDE_DEG],
            "bob_m": [-BOB_AMPLITUDE_M, BOB_AMPLITUDE_M], "pitch_deg": PITCH_AMPLITUDE_DEG}


def all_fcurves(act):
    """F-curves of an action, for both slotted (layered) and legacy actions."""
    if getattr(act, "layers", None):
        out = []
        for layer in act.layers:
            for strip in layer.strips:
                for bag in strip.channelbags:
                    out += list(bag.fcurves)
        return out
    return list(act.fcurves)
