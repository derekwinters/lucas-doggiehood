"""The chew bone (#923): one of the three toys a dog can ask for (with the toy and the ball).

A classic stylized dog bone: a waisted shaft with two round knobs side by side at each end, lying
flat on the ground. The surface is one signed distance field, built from named dimensions: the
shaft and the four knobs are merged with a smooth minimum (so every join is a rounded fillet,
never an intersection line), meshed with surface nets, smoothed, decimated to TARGET_TRIS and then
projected back onto the exact field. Every step is deterministic.

Size. The game's dogs are bigger than real dogs: the adult Golden's top of back is 1.034 m in the
game against about 0.60 m on a real Golden, so DOG_SCALE = 1.72. The bone is a real large chew
bone for a big dog (REAL, about 17 cm long) multiplied by DOG_SCALE, so it reads at the right size
beside the dog and in its mouth.

Conventions (docs/engineering/blender-pipeline.md): metres, Z up, the long axis along X, the
pivot on the ground at the footprint centre. Object `chew_bone`, material `DH_Item_ChewBone`.

Texture: one 512 x 512 colour + tangent-space normal pair (seeded numpy + Pillow), pressed
nylon/rawhide with long fibres along the bone, box-projected at TEX_TILE metres per repeat.
"""
import math
import os
import random
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
ART = os.path.normpath(os.path.join(HERE, "..", ".."))          # art-source/blender
# The house kit (art-source/blender), the dog kit's mesher (dogs/kit) and the tree noise
# (environment/trees), all by relative path; nothing is copied.
for _p in (ART, os.path.join(ART, "dogs", "kit"), os.path.join(ART, "environment", "trees")):
    if _p not in sys.path:
        sys.path.insert(0, _p)
import bpy  # noqa: E402
import numpy as np  # noqa: E402
from PIL import Image  # noqa: E402
import dh_house_kit as K  # noqa: E402
import dh_dog_mesh as M  # noqa: E402
import dh_tree_tex as T  # noqa: E402

NAME = "chew_bone"
MATERIAL = "DH_Item_ChewBone"
TEX_DIR = os.path.join(HERE, "textures")

# --- size -----------------------------------------------------------------------------------------
DOG_SCALE = 1.72               # in-game adult dog / real dog (Golden top of back 1.034 m / 0.60 m)
REAL = {                       # a real large chew bone for a big dog, metres
    "length": 0.170,           # knob tip to knob tip
    "end_width": 0.058,        # across the two knobs of one end
    "knob_d": 0.032,           # one knob's diameter (also the bone's height lying flat)
    "shaft_d_mid": 0.019,      # the shaft at its waist
    "shaft_d_end": 0.026,      # the shaft where it meets the knobs
}
DIMS = {k: round(v * DOG_SCALE, 4) for k, v in REAL.items()}
KNOB_STRETCH = 1.12            # knobs slightly longer along the bone than across (stylized)
FILLET = 0.012                 # smooth-minimum radius where the shaft meets the knobs, m

# --- meshing --------------------------------------------------------------------------------------
GRID_H = 0.002                 # field grid spacing, m
GRID_MARGIN = 0.01             # empty grid border around the bone, m
TAUBIN_ITERS = 6
TARGET_TRIS = 300              # decimation target (this item's cap is 400, Derek on #923)
PROJECT_STEPS = 4              # Newton steps back onto the exact field after decimation
GRAD_EPS = 1e-4                # finite-difference step for the field gradient, m

# --- texture --------------------------------------------------------------------------------------
TEX_SIZE = 512
TEX_TILE = 0.16                # metres per texture repeat
TEX_SEED = 923
SS = 2                         # supersampling for the drawn pits
BASE = (232, 220, 196)         # warm bone white (#E8DCC4)
FIBRE_DARK = (196, 178, 146)   # the darker fibres
ROUGHNESS = 0.62
NORMAL_STRENGTH = 1.0
NORMAL_BAKE = 2.2              # height-to-normal gain when the normal map is painted
PITS = 260                     # tiny surface pits per tile
PIT_R = (1.0, 2.6)             # pit radius, px at TEX_SIZE


# ---------------------------------------------------------------------------------------------------
# the field
# ---------------------------------------------------------------------------------------------------
def _layout():
    L, R = DIMS["length"], DIMS["knob_d"] / 2
    rx = R * KNOB_STRETCH
    kx = L / 2 - rx                              # knob centre, along the bone
    ky = DIMS["end_width"] / 2 - R               # knob centre, across the bone
    return L, R, rx, kx, ky


def field(p):
    """Signed distance (m, approximately) to the bone surface; p is an (..., 3) array."""
    L, R, rx, kx, ky = _layout()
    x, y, z = p[..., 0], p[..., 1], p[..., 2] - R
    r0, r1 = DIMS["shaft_d_mid"] / 2, DIMS["shaft_d_end"] / 2
    t = np.clip(np.abs(x) / kx, 0.0, 1.0)
    shaft_r = r0 + (r1 - r0) * t * t
    shaft = np.maximum(np.hypot(y, z) - shaft_r, np.abs(x) - kx)
    f = shaft.astype(np.float32)
    for sx in (-1, 1):
        for sy in (-1, 1):
            q = np.sqrt(((x - sx * kx) / rx) ** 2 + ((y - sy * ky) / R) ** 2 + (z / R) ** 2)
            f = M.smin(f, ((q - 1.0) * R).astype(np.float32), FILLET)
    return f


def _gradient(p):
    g = np.empty_like(p)
    for ax in range(3):
        d = np.zeros(3)
        d[ax] = GRAD_EPS
        g[:, ax] = (field(p + d) - field(p - d)) / (2 * GRAD_EPS)
    return g


def _grid():
    L, R, rx, kx, ky = _layout()
    lo = np.array([-L / 2, -DIMS["end_width"] / 2, 0.0]) - GRID_MARGIN
    hi = np.array([L / 2, DIMS["end_width"] / 2, 2 * R]) + GRID_MARGIN
    n = np.ceil((hi - lo) / GRID_H).astype(int) + 1
    axes = [lo[i] + GRID_H * np.arange(n[i]) for i in range(3)]
    gx, gy, gz = np.meshgrid(*axes, indexing="ij")
    return field(np.stack([gx, gy, gz], -1)), lo


# ---------------------------------------------------------------------------------------------------
# the mesh
# ---------------------------------------------------------------------------------------------------
def _apply(ob, md):
    with bpy.context.temp_override(object=ob, active_object=ob, selected_objects=[ob]):
        bpy.ops.object.modifier_apply(modifier=md.name)


def _coords(me):
    co = np.empty(len(me.vertices) * 3)
    me.vertices.foreach_get("co", co)
    return co.reshape(-1, 3)


def _project(ob):
    """Move every vertex back onto the exact field's zero level."""
    me = ob.data
    co = _coords(me)
    for _ in range(PROJECT_STEPS):
        g = _gradient(co)
        co = co - (field(co)[:, None] * g) / np.maximum((g * g).sum(1, keepdims=True), 1e-12)
    me.vertices.foreach_set("co", co.ravel())
    me.update()


def _place_on_ground(ob):
    """Pivot on the ground at the footprint centre."""
    me = ob.data
    co = _coords(me)
    lo, hi = co.min(0), co.max(0)
    co -= np.array([(lo[0] + hi[0]) / 2, (lo[1] + hi[1]) / 2, lo[2]])
    me.vertices.foreach_set("co", co.ravel())
    me.update()


def _box_uvs(me):
    """Box projection at TEX_TILE m per repeat; the first UV axis always runs along the bone
    wherever the face allows, so the fibres follow its length."""
    uv = me.uv_layers.new(name="UVMap")
    co = _coords(me)
    out = np.empty((len(me.loops), 2))
    for poly in me.polygons:
        ax = int(np.argmax(np.abs(np.array(poly.normal))))
        a, b = {0: (1, 2), 1: (0, 2), 2: (0, 1)}[ax]
        for li in poly.loop_indices:
            c = co[me.loops[li].vertex_index]
            out[li] = (c[a] / TEX_TILE, c[b] / TEX_TILE)
    uv.data.foreach_set("uv", out.ravel())


def material(paths=None):
    mat = bpy.data.materials.get(MATERIAL)
    if mat:
        return mat
    col, nrm = paths or texture_paths()
    mat = bpy.data.materials.new(MATERIAL)
    mat.use_nodes = True
    nt = mat.node_tree
    bsdf = nt.nodes["Principled BSDF"]
    tc = nt.nodes.new("ShaderNodeTexImage")
    tc.image = bpy.data.images.load(col, check_existing=True)
    tn = nt.nodes.new("ShaderNodeTexImage")
    tn.image = bpy.data.images.load(nrm, check_existing=True)
    tn.image.colorspace_settings.name = "Non-Color"
    nm = nt.nodes.new("ShaderNodeNormalMap")
    nm.inputs["Strength"].default_value = NORMAL_STRENGTH
    nt.links.new(tc.outputs["Color"], bsdf.inputs["Base Color"])
    nt.links.new(tn.outputs["Color"], nm.inputs["Color"])
    nt.links.new(nm.outputs["Normal"], bsdf.inputs["Normal"])
    bsdf.inputs["Roughness"].default_value = ROUGHNESS
    mat.use_backface_culling = True                  # a closed solid: single-sided in glTF
    return mat


def build(paths=None, collection=None):
    """Build the chew bone into the scene and return its object."""
    col = collection or bpy.context.scene.collection
    f, origin = _grid()
    v, q = M.surface_nets(f, origin, GRID_H)
    v, q = M.largest_component(np.asarray(v), q)
    ob = M.mesh_from(NAME, v, q, col)
    M.taubin(ob, TAUBIN_ITERS)
    md = ob.modifiers.new("decimate", "DECIMATE")
    md.decimate_type = "COLLAPSE"
    md.use_collapse_triangulate = True
    md.ratio = TARGET_TRIS / float(2 * len(ob.data.polygons))
    _apply(ob, md)
    _project(ob)
    _place_on_ground(ob)
    ob.data.shade_smooth()
    _box_uvs(ob.data)
    ob.data.materials.append(material(paths))
    return ob


# ---------------------------------------------------------------------------------------------------
# the texture
# ---------------------------------------------------------------------------------------------------
def texture_paths(tex_dir=TEX_DIR):
    return (os.path.join(tex_dir, "chew_bone_color.png"), os.path.join(tex_dir, "chew_bone_normal.png"))


def _normal_from_height(h, strength):
    h = h * strength
    gx = (np.roll(h, -1, 1) - np.roll(h, 1, 1)) / 2
    gy = (np.roll(h, -1, 0) - np.roll(h, 1, 0)) / 2
    n = np.dstack([-gx, gy, np.ones_like(h)])
    n /= np.linalg.norm(n, axis=2, keepdims=True)
    return Image.fromarray(((n * 0.5 + 0.5) * 255).astype(np.uint8))


def _pits(rng):
    """Tiny round pits, wrapped so the tile stays seamless; returns 0..1 depth."""
    W = TEX_SIZE * SS
    out = np.zeros((W, W))
    for _ in range(PITS):
        cx, cy = rng.random() * W, rng.random() * W
        r = rng.uniform(*PIT_R) * SS
        n = int(math.ceil(r)) + 1
        ix = np.arange(int(cx) - n, int(cx) + n + 1)
        iy = np.arange(int(cy) - n, int(cy) + n + 1)
        d2 = (iy[:, None] - cy) ** 2 + (ix[None, :] - cx) ** 2
        win = np.ix_(iy % W, ix % W)
        out[win] = np.maximum(out[win], np.clip(1 - d2 / (r * r), 0, 1))
    img = Image.fromarray((out * 255).astype(np.uint8)).resize((TEX_SIZE, TEX_SIZE), Image.LANCZOS)
    return np.asarray(img, np.float64) / 255


def make_textures(tex_dir=TEX_DIR):
    """Write the colour and normal maps; returns their paths."""
    rng = random.Random(TEX_SEED)
    shape = (TEX_SIZE, TEX_SIZE)
    fibres = T._periodic_noise(shape, 40, 1.5, rng, octaves=3)      # long streaks along U
    fine = T._periodic_noise(shape, 90, 6, rng, octaves=2)
    mott = T._periodic_noise(shape, 2.5, 2.5, rng, octaves=3)
    pits = _pits(rng)
    dark = np.clip((fibres - 0.45) / 0.4, 0, 1) * 0.55 + 0.25 * (mott - 0.5) + 0.15 * pits
    dark = np.clip(dark, 0, 1)[..., None]
    col = np.array(BASE) * (1 - dark) + np.array(FIBRE_DARK) * dark
    col = col * (0.97 + 0.05 * fine[..., None])
    hgt = 0.6 * fibres + 0.25 * fine - 0.9 * pits
    os.makedirs(tex_dir, exist_ok=True)
    cpath, npath = texture_paths(tex_dir)
    Image.fromarray(np.clip(col, 0, 255).astype(np.uint8)).save(cpath)
    _normal_from_height(hgt, NORMAL_BAKE).save(npath)
    return cpath, npath


def tri_count(ob):
    return K.tri_count(ob)


if __name__ == "__main__":
    K.reset_scene()
    ob = build(make_textures())
    print(NAME, tri_count(ob), [round(d, 3) for d in ob.dimensions])
