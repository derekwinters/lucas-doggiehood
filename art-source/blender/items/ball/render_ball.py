"""Review renders for the dog ball (#333), EEVEE, headless.

  python3 render_ball.py <shot> [outdir]     shot = grass | sidewalk | closeup | colours | sheet | all

Writes <outdir>/<shot>.png (default: out/ next to this file, git-ignored). Textures and exports
must exist (run export_ball.py first). Renders are not byte-reproducible.

  grass, sidewalk   the game camera (orthographic, pitch 45, yaw -40) at its closest zoom
                    (CameraController.MinZoom: a 12 m tall view on a 16:9 screen), the ball beside
                    the approved adult Labrador (yellow coat, its committed export), on the approved
                    lawn (ground/) and sidewalk concrete (paving/), in RENDER_VARIANT. An enlarged
                    inset shows the pixels around the ball.
  closeup           every colour variant side by side, straight on, on a neutral floor
  colours           every colour variant (dh_ball_tex.VARIANTS) on grass and on the sidewalk, at
                    the game camera, with the measured colour difference (CIE76 delta E) between
                    each ball's pixels and the ground around it
  sheet             grass + sidewalk + closeup + colours on one page (renders any that are missing)

Without a GPU, EEVEE runs on Mesa's lavapipe (Vulkan on the CPU); this script points the Vulkan
loader at it when VK_ICD_FILENAMES is unset and the file exists.
"""
import math
import os
import sys
import zlib

if "VK_ICD_FILENAMES" not in os.environ and os.path.exists("/opt/lavapipe/lvp_icd.json"):
    os.environ["VK_ICD_FILENAMES"] = "/opt/lavapipe/lvp_icd.json"

HERE = os.path.dirname(os.path.abspath(__file__))
ART = os.path.normpath(os.path.join(HERE, "..", ".."))                 # art-source/blender
sys.path.insert(0, HERE)
sys.path.insert(0, ART)
sys.path.insert(0, os.path.join(ART, "dogs", "kit"))                    # shared render helpers
import bpy  # noqa: E402
import numpy as np  # noqa: E402
from bpy_extras.object_utils import world_to_camera_view  # noqa: E402
from mathutils import Vector  # noqa: E402
from PIL import Image, ImageDraw, ImageFont  # noqa: E402

import dh_ball as B  # noqa: E402
import dh_ball_tex as BT  # noqa: E402
import dh_dog_render as R  # noqa: E402

SHOT = sys.argv[1] if len(sys.argv) > 1 else "all"
OUT = os.path.abspath(sys.argv[2]) if len(sys.argv) > 2 else os.path.join(HERE, "out")
TMP = os.path.join(OUT, "_parts")
os.makedirs(TMP, exist_ok=True)

DOG = os.path.join(ART, "dogs", "labrador", "exports", "dog_labrador_yellow.gltf")
LAWN = os.path.join(ART, "environment", "ground", "textures", "lawn_c_cells8_%s.png")
CONCRETE = os.path.join(ART, "environment", "paving", "textures", "paving_concrete_classic_%s.png")

# game camera (Core: CameraRigConfig.PitchDegrees, CameraController.MinZoom; yaw as the other sets)
CAM_PITCH, CAM_YAW = 45, -40
MIN_ZOOM = 6.0                         # orthographic half-height, metres
GAME_RES = (1920, 1080)
SAMPLES = 48
RENDER_VARIANT = "blue"                # the colour shown in the grass and sidewalk shots
SUN_ENERGY = 2.6                       # a little under the dog renders' 3.4, so the light concrete doesn't clip
INSET_BOX = (300, 200)                 # source pixels around the ball, enlarged into the inset
INSET_ZOOM = 3

# ground (atlas layouts as committed: ground/dh_ground_tex.ATLASES["cells8"], paving CELL/PAD)
LAWN_CELL_M, LAWN_GRID, LAWN_CELL_PX, LAWN_PAD_PX, LAWN_W, LAWN_H = 3.0, 4, 224, 16, 1024, 512
SLAB_M, SLAB_GRID, SLAB_CELL_PX, SLAB_PAD_PX, SLAB_W, SLAB_H = 2.0, 4, 240, 8, 1024, 512
GROUND_HALF = 21.0                     # a multiple of both the 3 m lawn cell and the 2 m slab
SIDEWALK_Y = (-4.0, -2.0)              # a 2 m sidewalk along X
PAVE_Z = 0.01                          # paving 1 cm proud of the lawn (ground edge set C1)
HASH_SEED = 333

# placements: (dog xy, dog yaw, ball xy)
ON_GRASS = ((0.0, 0.6), -25, (0.42, -0.25))
ON_SIDEWALK = ((0.0, -2.75), -25, (0.42, -3.6))

F1 = "/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf"
F2 = "/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf"
BG = (241, 236, 227)
INK = (40, 36, 32)
SUB = (105, 98, 90)
INSET_EDGE = (255, 255, 255)


def font(sz, bold=False):
    return ImageFont.truetype(F1 if bold else F2, sz)


# ---------------------------------------------------------------------------
# scene
# ---------------------------------------------------------------------------
def _image_mat(name, pattern, rough):
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    nt = m.node_tree
    b = nt.nodes["Principled BSDF"]
    tc = nt.nodes.new("ShaderNodeTexImage")
    tc.image = bpy.data.images.load(pattern % "color", check_existing=True)
    tn = nt.nodes.new("ShaderNodeTexImage")
    tn.image = bpy.data.images.load(pattern % "normal", check_existing=True)
    tn.image.colorspace_settings.name = "Non-Color"
    nm = nt.nodes.new("ShaderNodeNormalMap")
    nt.links.new(tc.outputs["Color"], b.inputs["Base Color"])
    nt.links.new(tn.outputs["Color"], nm.inputs["Color"])
    nt.links.new(nm.outputs["Normal"], b.inputs["Normal"])
    b.inputs["Roughness"].default_value = rough
    return m


def _variant(ix, iy, n):
    return (zlib.crc32(("%d,%d,%d" % (HASH_SEED, ix, iy)).encode()) & 0xFFFFFFFF) % n


def _atlas_uv(k, fx, fy, grid, cell, pad, w, h):
    r, c = divmod(k, grid)
    slot = cell + 2 * pad
    return ((c * slot + pad + fx * cell) / w, 1.0 - (r * slot + pad + (1.0 - fy) * cell) / h)


def _tiles(name, x0, x1, y0, y1, size, z, uvfn, mat):
    verts, faces, uvs = [], [], []
    nx, ny = int(round((x1 - x0) / size)), int(round((y1 - y0) / size))
    for i in range(nx):
        for j in range(ny):
            ax, ay = x0 + i * size, y0 + j * size
            k = len(verts)
            verts += [(ax, ay, z), (ax + size, ay, z), (ax + size, ay + size, z), (ax, ay + size, z)]
            faces.append((k, k + 1, k + 2, k + 3))
            uvs.append([uvfn(i, j, fx, fy) for fx, fy in ((0, 0), (1, 0), (1, 1), (0, 1))])
    me = bpy.data.meshes.new(name)
    me.from_pydata(verts, [], faces)
    uvl = me.uv_layers.new(name="UVMap")
    for poly, fuv in zip(me.polygons, uvs):
        for li, u in zip(poly.loop_indices, fuv):
            uvl.data[li].uv = u
    me.materials.append(mat)
    ob = bpy.data.objects.new(name, me)
    bpy.context.scene.collection.objects.link(ob)
    return ob


def ground():
    """The approved lawn (3 m cells into its 8-slot atlas) with a 2 m concrete sidewalk on top."""
    lawn = _image_mat("lawn", LAWN, 0.95)
    conc = _image_mat("sidewalk", CONCRETE, 0.9)
    g = GROUND_HALF
    _tiles("lawn", -g, g, -g, g, LAWN_CELL_M, 0.0,
           lambda i, j, fx, fy: _atlas_uv(_variant(i, j, 8), fx, fy, LAWN_GRID, LAWN_CELL_PX, LAWN_PAD_PX,
                                          LAWN_W, LAWN_H), lawn)
    _tiles("sidewalk", -g, g, SIDEWALK_Y[0], SIDEWALK_Y[1], SLAB_M, PAVE_Z,
           lambda i, j, fx, fy: _atlas_uv(_variant(i, 99 + j, 8), fx, fy, SLAB_GRID, SLAB_CELL_PX, SLAB_PAD_PX,
                                          SLAB_W, SLAB_H), conc)


def dog(at, yaw):
    before = set(bpy.data.objects)
    bpy.ops.import_scene.gltf(filepath=DOG)
    new = [o for o in bpy.data.objects if o not in before]
    for r in (o for o in new if o.parent is None):
        r.location = Vector(r.location) + Vector(at)
        r.rotation_euler = (r.rotation_euler[0], r.rotation_euler[1], r.rotation_euler[2] + math.radians(yaw))
    bpy.context.view_layer.update()
    return new


def ball(at, key=RENDER_VARIANT, spin=(0, 0, 0)):
    """The ball in colour variant key, resting on the ground at `at`, spun about its centre."""
    ob = B.build(key)
    ob.rotation_euler = tuple(math.radians(a) for a in spin)
    c = Vector((0, 0, BT.DIAMETER_M / 2))
    ob.location = Vector(at) + c - ob.rotation_euler.to_matrix() @ c
    bpy.context.view_layer.update()
    return ob


def ball_centre(ob):
    return ob.matrix_world @ Vector((0, 0, BT.DIAMETER_M / 2))


def game_camera(target, res=GAME_RES, zoom=MIN_ZOOM):
    """The game's orthographic camera centred on target, at the given zoom (half-height in metres)."""
    sc = bpy.context.scene
    cd = bpy.data.cameras.new("Cam")
    cd.type = "ORTHO"
    cd.sensor_fit = "VERTICAL"
    cd.ortho_scale = 2 * zoom
    cd.clip_end = 400
    cam = bpy.data.objects.new("Cam", cd)
    sc.collection.objects.link(cam)
    cam.rotation_euler = (math.radians(90 - CAM_PITCH), 0, math.radians(CAM_YAW))
    cam.location = Vector(target) + cam.rotation_euler.to_matrix() @ Vector((0, 0, 40))
    sc.camera = cam
    bpy.context.view_layer.update()
    return cam


def to_px(cam, p, res):
    co = world_to_camera_view(bpy.context.scene, cam, Vector(p))
    return co.x * res[0], (1 - co.y) * res[1]


def new_scene(path, res, samples=SAMPLES, bg=R.BG):
    R.reset()
    R.setup(path, res=res, samples=samples, bg=bg, energy=SUN_ENERGY)


# ---------------------------------------------------------------------------
# measuring
# ---------------------------------------------------------------------------
def _lab(rgb):
    c = np.asarray(rgb, np.float64) / 255.0
    c = np.where(c <= 0.04045, c / 12.92, ((c + 0.055) / 1.055) ** 2.4)
    xyz = c @ np.array([[0.4124, 0.2126, 0.0193], [0.3576, 0.7152, 0.1192], [0.1805, 0.0722, 0.9505]])
    xyz = xyz / np.array([0.95047, 1.0, 1.08883])
    f = np.where(xyz > 0.008856, np.cbrt(xyz), 7.787 * xyz + 16 / 116)
    return np.stack([116 * f[..., 1] - 16, 500 * (f[..., 0] - f[..., 1]), 200 * (f[..., 1] - f[..., 2])], -1)


def contrast(img, centre, r_px):
    """CIE76 delta E between the mean colour inside the ball's disc and of the ring of ground
    around it (1.5 to 3 radii out)."""
    a = np.asarray(img.convert("RGB"), np.float64)
    yy, xx = np.mgrid[0:a.shape[0], 0:a.shape[1]]
    d = np.hypot(xx - centre[0], yy - centre[1])
    inside = a[d <= r_px * 0.8].mean(0)
    around = a[(d >= r_px * 1.5) & (d <= r_px * 3.0)].mean(0)
    return float(np.linalg.norm(_lab(inside) - _lab(around)))


def inset(img, centre, box=INSET_BOX, zoom=INSET_ZOOM):
    x0, y0 = int(centre[0] - box[0] / 2), int(centre[1] - box[1] / 2)
    crop = img.crop((x0, y0, x0 + box[0], y0 + box[1]))
    return crop.resize((box[0] * zoom, box[1] * zoom), Image.NEAREST), (x0, y0)


# ---------------------------------------------------------------------------
# shots
# ---------------------------------------------------------------------------
def header(img, title, sub):
    W = img.width
    out = Image.new("RGB", (W, img.height + 96), BG)
    d = ImageDraw.Draw(out)
    d.text((24, 16), title, fill=INK, font=font(32, True))
    d.text((24, 58), sub, fill=SUB, font=font(22))
    out.paste(img, (0, 96))
    return out


def gamecam(where):
    (dxy, dyaw, bxy) = ON_GRASS if where == "grass" else ON_SIDEWALK
    path = os.path.join(TMP, "game_%s.png" % where)
    new_scene(path, GAME_RES)
    ground()
    z = PAVE_Z if where == "sidewalk" else 0.0
    dog((dxy[0], dxy[1], z), dyaw)
    b = ball((bxy[0], bxy[1], z))
    target = ((dxy[0] + bxy[0]) / 2, (dxy[1] + bxy[1]) / 2, 0.25)
    cam = game_camera(target)
    R.render()
    img = Image.open(path).convert("RGB")
    c = to_px(cam, ball_centre(b), GAME_RES)
    r_px = BT.DIAMETER_M / 2 * GAME_RES[1] / (2 * MIN_ZOOM)
    de = contrast(img, c, r_px)
    ins, (x0, y0) = inset(img, c)
    d = ImageDraw.Draw(img)
    d.rectangle((x0, y0, x0 + INSET_BOX[0], y0 + INSET_BOX[1]), outline=INSET_EDGE, width=2)
    W = img.width
    sheet = Image.new("RGB", (W, img.height + ins.height + 70), BG)
    sheet.paste(img, (0, 0))
    dd = ImageDraw.Draw(sheet)
    dd.text((20, img.height + 18), "White box, enlarged %dx (nearest pixel): the ball is %.1f px across at this zoom; "
            "ball vs surrounding %s: delta E %.0f" % (INSET_ZOOM, 2 * r_px, where, de), fill=INK, font=font(24, True))
    sheet.paste(ins, ((W - ins.width) // 2, img.height + 70))
    title = "Dog ball on the %s, game camera at closest zoom" % ("grass" if where == "grass" else "sidewalk")
    sub = ("Orthographic, pitch 45, yaw -40, 12 m tall view (MinZoom 6) at %dx%d. Ball %.1f cm, felt %s, "
           "beside the approved adult Labrador (yellow)." % (GAME_RES[0], GAME_RES[1], BT.DIAMETER_M * 100,
                                                            BT.VARIANTS[RENDER_VARIANT]))
    out = header(sheet, title, sub)
    out.save(os.path.join(OUT, "gamecam-%s.png" % where))
    print("gamecam-%s: ball %.1f px, delta E %.1f" % (where, 2 * r_px, de))
    return out


CLOSEUP_RES = (3000, 620)
CLOSEUP_SPIN = (90, 0, 30)             # shows the seam's S-curve
CLOSEUP_GAP = 1.5                      # centre spacing, in diameters
CLOSEUP_MARGIN = 1.08
COLOUR_ROW_SPACING = 0.7               # metres between the balls in the colours shot
COLOUR_ROW_X = 1.6
COLOUR_ROWS = {"grass": (-0.9, 0.0), "sidewalk": (-3.0, PAVE_Z)}
COLOUR_CELL = (110, 80)                # source pixels per ball in the colours shot
COLOUR_ZOOM = 4


def closeup():
    keys = list(BT.VARIANTS)
    path = os.path.join(TMP, "closeup.png")
    new_scene(path, CLOSEUP_RES, samples=96)
    R.floor(12, "#C9C3B8")
    gap = BT.DIAMETER_M * CLOSEUP_GAP
    balls = [ball(((i - (len(keys) - 1) / 2) * gap, 0, 0), k, CLOSEUP_SPIN) for i, k in enumerate(keys)]
    R.camera(balls, 0, 18, CLOSEUP_RES, ortho=True, margin=CLOSEUP_MARGIN)   # straight on: equal columns
    R.render()
    img = Image.open(path).convert("RGB")
    d = ImageDraw.Draw(img)
    col_w = img.width // len(keys)
    for i, k in enumerate(keys):
        d.text((i * col_w + 24, 18), "%s  %s" % (k, BT.VARIANTS[k]), fill=INK, font=font(30, True))
    out = header(img, "Dog ball close-up: the six felt colours, white seam",
                 "%.0f cm across, %d triangles; one mesh and normal map shared, one colour map per variant"
                 % (BT.DIAMETER_M * 100, B.SEGMENTS * 2 + (B.RINGS - 2) * B.SEGMENTS * 2))
    out.save(os.path.join(OUT, "closeup.png"))
    return out


def colours():
    keys = list(BT.VARIANTS)
    path = os.path.join(TMP, "colours.png")
    new_scene(path, GAME_RES)
    ground()
    dog((0.0, 0.0, 0.0), -25)
    balls = {}
    for row, (y, z) in COLOUR_ROWS.items():
        for i, k in enumerate(keys):
            x = (i - (len(keys) - 1) / 2) * COLOUR_ROW_SPACING + COLOUR_ROW_X
            balls[row, k] = ball((x, y, z), k)
    cam = game_camera((COLOUR_ROW_X, -1.6, 0.2))
    R.render()
    img = Image.open(path).convert("RGB")
    r_px = BT.DIAMETER_M / 2 * GAME_RES[1] / (2 * MIN_ZOOM)
    tiles, results = [], {}
    for row in COLOUR_ROWS:
        for k in keys:
            c = to_px(cam, ball_centre(balls[row, k]), GAME_RES)
            results[row, k] = contrast(img, c, r_px)
            t, _ = inset(img, c, COLOUR_CELL, COLOUR_ZOOM)
            tiles.append(t)
    tw, th = COLOUR_CELL[0] * COLOUR_ZOOM, COLOUR_CELL[1] * COLOUR_ZOOM
    lab_w = 280
    sheet = Image.new("RGB", (lab_w + tw * len(keys), 60 + (th + 70) * 2), BG)
    d = ImageDraw.Draw(sheet)
    for i, k in enumerate(keys):
        d.text((lab_w + i * tw + 14, 16), "%s %s" % (k, BT.VARIANTS[k]), fill=INK, font=font(24, True))
    for r, row in enumerate(COLOUR_ROWS):
        y = 60 + r * (th + 70)
        d.text((16, y + th // 2 - 14), "on the " + row, fill=INK, font=font(26, True))
        for i, k in enumerate(keys):
            sheet.paste(tiles[r * len(keys) + i], (lab_w + i * tw, y))
            d.text((lab_w + i * tw + 14, y + th + 12), "delta E %.0f" % results[row, k], fill=INK, font=font(24))
    out = header(sheet, "The six felt colours at the game camera (closest zoom, enlarged %dx)" % COLOUR_ZOOM,
                 "Delta E (CIE76): ball pixels vs the ground around them in the render; higher = easier to spot."
                 " The finder ring is red (#FF2A2A).")
    out.save(os.path.join(OUT, "colour-variants.png"))
    for k, v in results.items():
        print("%-9s %-8s delta E %.1f" % (k[0], k[1], v))
    return out


def sheet():
    parts = []
    for name, fn in (("gamecam-grass", lambda: gamecam("grass")), ("gamecam-sidewalk", lambda: gamecam("sidewalk")),
                     ("closeup", closeup), ("colour-variants", colours)):
        p = os.path.join(OUT, name + ".png")
        parts.append(Image.open(p).convert("RGB") if os.path.exists(p) else fn())
    W = 1600
    parts = [im.resize((W, int(im.height * W / im.width)), Image.LANCZOS) for im in parts]
    out = Image.new("RGB", (W, sum(im.height for im in parts) + 20 * (len(parts) - 1)), BG)
    y = 0
    for im in parts:
        out.paste(im, (0, y))
        y += im.height + 20
    out.save(os.path.join(OUT, "ball-sheet.png"))


SHOTS = {"grass": lambda: gamecam("grass"), "sidewalk": lambda: gamecam("sidewalk"), "closeup": closeup,
         "colours": colours, "sheet": sheet}

if __name__ == "__main__":
    if SHOT not in SHOTS and SHOT != "all":
        sys.exit(__doc__)
    for s in (SHOTS if SHOT == "all" else [SHOT]):
        SHOTS[s]()
