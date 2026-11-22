"""Doggiehood environment kit: trees, shrubs and foundation planting, yard
and path pieces, street furniture, and the tileable lawn texture.

Same conventions as the house kit (dh_house_kit): 1 Blender unit = 1 m
in-game, Z up, front (street side) faces -Y, every piece's origin is on the
ground at its base centre unless noted. Each builder returns one mesh
object. Materials are named DH_Env_<slot>.

Props that people/dogs use (mailbox, bench, signs, lamp, hydrant) are drawn
at about 1.15x real size, the same factor the houses use (2.35 m doors),
so they sit right next to the 1.58 m Cube Pets dog.
"""

import math
import os
import random
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
KIT = os.path.normpath(os.path.join(HERE, "..", ".."))   # art-source/blender: the committed house kit
sys.path.insert(0, KIT)
import bpy  # noqa: E402
import bmesh  # noqa: E402
from mathutils import Matrix, Vector  # noqa: E402
import dh_house_kit as K  # noqa: E402

PROP_SCALE = 1.15          # human-use props, matching the houses' door scale
# Lawn/mulch/sign textures are made on demand by the street renders only;
# they go to the git-ignored out/ folder, not next to the committed atlases.
TEX_DIR = os.path.join(HERE, "out", "env-textures")

ENV = {
    "_id": "Env",
    # bark
    "bark": "#6B4F3A", "bark_dark": "#4E3A2B", "bark_light": "#8C7A68", "bark_cherry": "#5C3B33",
    # foliage
    "leaf_a": "#4E8A3A", "leaf_b": "#5F9A44", "leaf_c": "#3F7A33",           # maple / oak
    "street_a": "#6FA246", "street_b": "#7FB050",                              # street tree (honey locust)
    "column_a": "#3E6E35", "column_b": "#4A7C3C",                              # columnar hornbeam
    "spruce_a": "#2C563A", "spruce_b": "#3F7550", "spruce_c": "#346647",       # spruce
    "cherry_a": "#F2B6C6", "cherry_b": "#E89AB0", "cherry_c": "#F5C4D2",       # flowering cherry
    "jmaple_a": "#8E2A28", "jmaple_b": "#B23A32", "jmaple_c": "#CC5240",                              # Japanese maple
    "shrub_a": "#4C7F3B", "shrub_b": "#5A8E45", "boxwood": "#3F6B33", "boxwood_b": "#477538",
    "grass_orn": "#A3B56A", "grass_orn_b": "#86994F", "plume": "#D8C99A",
    "hosta": "#6E9C55",
    # flowers
    "fl_red": "#E6584F", "fl_yellow": "#F2C14E", "fl_purple": "#9B7FC0", "fl_white": "#F4F2EC",
    "fl_pink": "#E86AA0", "fl_orange": "#EE8A3C",
    # ground materials
    "mulch": "#5A3A28", "mulch_b": "#4A2F21", "edging": "#A9A39A", "edging_dark": "#8E887F",
    "steel_edge": "#4A4A4A", "stone_a": "#B8B2A7", "stone_b": "#A49E93", "lawn": "#6FA84A",
    "soil": "#4B3426", "curb": "#C9C4BA", "sidewalk": K.COMMON["sidewalk"], "road": K.COMMON["road"],
    # props
    "post_white": "#F2F0EA", "post_wood": "#8C6A4A", "mailbox": "#2E3136", "flag": "#C8312C",
    "terracotta": "#B5603E", "terracotta_d": "#93492D",
    "lamp_black": "#26292E", "lamp_glass": "#FFE9B0",
    "sign_green": "#1E6B3E", "sign_white": "#F4F4F0", "stop_red": "#C1272D", "pole_grey": "#9EA3A8",
    "hydrant": "#C23B2E", "hydrant_cap": "#BFC3C7",
    "bench_wood": "#A0714A", "bench_iron": "#2B2E33",
    "metal": "#B9824C",
}

# ---------------------------------------------------------------------------
# textures: greyscale masks x colour, exactly like the roof textures
# ---------------------------------------------------------------------------
LAWN_TILE_M = 4.0          # one lawn texture tile covers 4 m x 4 m (64 px/m)
LAWN_PX = 256


def lawn_mask():
    """Tileable greyscale lawn mask (one LAWN_TILE_M tile): soft low-frequency patches,
    fine speckle and short blade strokes. Multiplied by the lawn colour."""
    from PIL import Image
    os.makedirs(TEX_DIR, exist_ok=True)
    path = os.path.join(TEX_DIR, "lawn_mask.png")
    if os.path.exists(path):
        return path
    n = LAWN_PX
    rng = random.Random(5)
    # low-frequency mottling from a few wrap-around blobs (no straight waves, so no stripes)
    import array
    base = array.array("f", [0.9] * (n * n))
    for _ in range(14):
        cx, cy, r, a = rng.randrange(n), rng.randrange(n), rng.uniform(25, 60), rng.uniform(-0.05, 0.05)
        for y in range(n):
            dy = min(abs(y - cy), n - abs(y - cy))
            if dy > r:
                continue
            for x in range(n):
                dx = min(abs(x - cx), n - abs(x - cx))
                d2 = (dx * dx + dy * dy) / (r * r)
                if d2 < 1:
                    base[y * n + x] += a * (1 - d2) ** 2
    img = Image.new("L", (n, n))
    px = img.load()
    for y in range(n):
        for x in range(n):
            px[x, y] = max(0, min(255, int(255 * (base[y * n + x] + rng.uniform(-0.035, 0.035)))))
    # short blade flecks in random directions, low contrast, wrapping at the edges
    for _ in range(3200):
        x, y = rng.randrange(n), rng.randrange(n)
        ang = rng.uniform(0, math.pi)
        L = rng.randint(2, 4)
        f = rng.choice((0.9, 0.93, 1.06, 1.09))
        for i in range(L):
            xx, yy = int(x + i * math.cos(ang)) % n, int(y + i * math.sin(ang)) % n
            px[xx, yy] = max(0, min(255, int(px[xx, yy] * f)))
    img.save(path)
    return path


def tinted(mask_path, hex_color, tag):
    from PIL import Image, ImageChops
    path = os.path.join(TEX_DIR, "%s_%s.png" % (tag, hex_color.lstrip("#").upper()))
    if not os.path.exists(path):
        mask = Image.open(mask_path).convert("RGB")
        ImageChops.multiply(mask, Image.new("RGB", mask.size, hex_color)).save(path)
    return path


def sign_texture(name, text, bg, fg, size=(512, 128), border=None, octagon=False):
    """Text texture for sign faces (street-name blades, STOP)."""
    from PIL import Image, ImageDraw, ImageFont
    os.makedirs(TEX_DIR, exist_ok=True)
    path = os.path.join(TEX_DIR, "sign_%s.png" % name)
    img = Image.new("RGB", size, bg)
    d = ImageDraw.Draw(img)
    W, H = size
    if border:
        b = int(min(W, H) * 0.06)
        if octagon:
            pts = []
            for i in range(8):
                a = math.radians(22.5 + 45 * i)
                r = W / 2 / math.cos(math.radians(22.5)) - b * 1.4
                pts.append((W / 2 + r * math.cos(a), H / 2 + r * math.sin(a)))
            d.polygon(pts, outline=border, width=b)
        else:
            d.rectangle((b, b, W - b - 1, H - b - 1), outline=border, width=max(2, b // 2))
    font = ImageFont.truetype("/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf", int(H * (0.25 if octagon else 0.55)))
    tw = d.textlength(text, font=font)
    bb = d.textbbox((0, 0), text, font=font)
    d.text(((W - tw) / 2, (H - (bb[3] - bb[1])) / 2 - bb[1]), text, fill=fg, font=font)
    img.save(path)
    return path


# ---------------------------------------------------------------------------
# builder with per-material planar UVs + textures
# ---------------------------------------------------------------------------
class EnvBuilder(K.MeshBuilder):
    """MeshBuilder plus textured materials: `textures` maps a material key
    to (image path, uv_fn(co) -> (u, v))."""

    def __init__(self, name, palette=ENV):
        super().__init__(name, palette)
        self.textures = {}
        self.rng = random.Random(hash(name) & 0xFFFF)

    def texture(self, key, path, uv_fn):
        self.textures[key] = (path, uv_fn)
        self._mi(key)

    def build(self, collection=None):
        bm = self.bm
        bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-5)
        bmesh.ops.triangulate(bm, faces=bm.faces)
        bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
        if self.textures:
            uv = bm.loops.layers.uv.verify()
            for f in bm.faces:
                key = self.mat_names[f.material_index]
                if key in self.textures:
                    fn = self.textures[key][1]
                    for loop in f.loops:
                        loop[uv].uv = fn(loop.vert.co)
        me = bpy.data.meshes.new(self.name)
        bm.to_mesh(me)
        bm.free()
        for key in self.mat_names:
            tex = self.textures.get(key, (None,))[0]
            hexc = self.palette.get(key, "#FFFFFF") if tex is None else "#FFFFFF"
            me.materials.append(K.material("DH_Env_%s" % key, hexc, texture=tex))
        for p in me.polygons:
            p.use_smooth = False
        ob = bpy.data.objects.new(self.name, me)
        (collection or bpy.context.scene.collection).objects.link(ob)
        return ob

    # --- primitives ---------------------------------------------------------
    def cyl(self, c, z0, z1, r0, r1=None, n=8, mat="bark", cap=True, rot=0.0):
        r1 = r0 if r1 is None else r1
        cx, cy = c
        a = [math.radians(rot) + 2 * math.pi * i / n for i in range(n)]
        bot = [(cx + r0 * math.cos(t), cy + r0 * math.sin(t), z0) for t in a]
        top = [(cx + r1 * math.cos(t), cy + r1 * math.sin(t), z1) for t in a]
        faces = [(i, (i + 1) % n, n + (i + 1) % n, n + i) for i in range(n)]
        if cap:
            faces += [tuple(reversed(range(n))), tuple(range(n, 2 * n))]
        self._faces(bot + top, faces, mat)

    def tube(self, p0, p1, r0, r1, n=6, mat="bark"):
        """Tapered cylinder between two 3D points (branches)."""
        p0, p1 = Vector(p0), Vector(p1)
        d = (p1 - p0).normalized()
        a = Vector((0, 0, 1)) if abs(d.z) < 0.9 else Vector((1, 0, 0))
        u = d.cross(a).normalized()
        v = d.cross(u).normalized()
        ring = lambda p, r: [p + (u * math.cos(2 * math.pi * i / n) + v * math.sin(2 * math.pi * i / n)) * r
                             for i in range(n)]  # noqa: E731
        vs = ring(p0, r0) + ring(p1, r1)
        faces = [(i, (i + 1) % n, n + (i + 1) % n, n + i) for i in range(n)] + [tuple(range(n, 2 * n))]
        self._faces(vs, faces, mat)

    def blob(self, c, r, mat, subdiv=2, squash=1.0, jitter=0.08, flat_bottom=None):
        """Lumpy icosphere: canopies, shrubs. r = (rx, ry, rz) or a float."""
        rx, ry, rz = (r, r, r * squash) if isinstance(r, (int, float)) else r
        res = bmesh.ops.create_icosphere(self.bm, subdivisions=subdiv, radius=1.0,
                                         matrix=Matrix.Identity(4), calc_uvs=False)
        mi = self._mi(mat)
        verts = res["verts"]
        faces = {f for v in verts for f in v.link_faces}
        for f in faces:
            f.material_index = mi
        for vt in verts:
            k = 1.0 + self.rng.uniform(-jitter, jitter)
            co = vt.co * k
            z = co.z * rz
            if flat_bottom is not None:
                z = max(z, -flat_bottom * rz)
            vt.co = Vector((c[0] + co.x * rx, c[1] + co.y * ry, c[2] + z))

    def cone(self, c, z0, z1, r, mat, n=10, jag=0.0):
        """Cone (spruce tiers); jag > 0 alternates the rim radius for a needled edge."""
        cx, cy = c
        rim = []
        for i in range(n):
            t = 2 * math.pi * i / n
            rr = r * (1 - jag if i % 2 else 1)
            rim.append((cx + rr * math.cos(t), cy + rr * math.sin(t), z0 + (0.06 * r if i % 2 else 0)))
        apex = (cx, cy, z1)
        faces = [(i, (i + 1) % n, n) for i in range(n)] + [tuple(reversed(range(n)))]
        self._faces(rim + [apex], faces, mat)

    def rounded_box(self, mn, mx, mat, bevel=0.12, segs=2):
        tmp = bmesh.new()
        bmesh.ops.create_cube(tmp, size=1.0)
        sx, sy, sz = (mx[0] - mn[0], mx[1] - mn[1], mx[2] - mn[2])
        for vt in tmp.verts:
            vt.co = Vector(((vt.co.x + 0.5) * sx + mn[0], (vt.co.y + 0.5) * sy + mn[1], (vt.co.z + 0.5) * sz + mn[2]))
        bmesh.ops.bevel(tmp, geom=list(tmp.edges) + list(tmp.verts), offset=bevel, segments=segs,
                        affect="EDGES", profile=0.5)
        vmap = {}
        mi = self._mi(mat)
        for vt in tmp.verts:
            vmap[vt] = self.bm.verts.new(vt.co.copy())
        for f in tmp.faces:
            try:
                nf = self.bm.faces.new([vmap[v] for v in f.verts])
                nf.material_index = mi
            except ValueError:
                pass
        tmp.free()

    def disc(self, c, r, z0, z1, mat, n=12, irregular=0.0):
        cx, cy = c
        pts = []
        for i in range(n):
            t = 2 * math.pi * i / n
            rr = r * (1 + self.rng.uniform(-irregular, irregular))
            pts.append((cx + rr * math.cos(t), cy + rr * math.sin(t), z0))
        self.prism(pts, (0, 0, z1 - z0), mat)


def _pick(b, keys):
    return keys[b.rng.randrange(len(keys))]


# ---------------------------------------------------------------------------
# 1. Trees
# ---------------------------------------------------------------------------
def _trunk(b, h, r, mat="bark", lean=(0.0, 0.0)):
    b.cyl((0, 0), 0, h, r, r * 0.65, n=7, mat=mat)
    b.cyl((0, 0), 0, 0.25, r * 1.45, r, n=7, mat=mat, cap=False)       # root flare


def _canopy(b, cx, cy, z, rad, n, keys, subdiv=2, squash=0.8, spread=0.55):
    """Clustered blobs: one big core plus n satellites around the upper half."""
    b.blob((cx, cy, z), (rad, rad, rad * squash), keys[0], subdiv=subdiv)
    for i in range(n):
        t = 2 * math.pi * i / n + b.rng.uniform(-0.3, 0.3)
        d = rad * spread * b.rng.uniform(0.85, 1.1)
        zz = z + rad * squash * b.rng.uniform(-0.15, 0.45)
        rr = rad * b.rng.uniform(0.5, 0.65)
        b.blob((cx + d * math.cos(t), cy + d * math.sin(t), zz), (rr, rr, rr * squash), keys[(i + 1) % len(keys)],
               subdiv=subdiv)


def tree_maple(size="large", name=None):
    """Round deciduous shade tree (red maple / oak): full, wide dome."""
    s = 1.0 if size == "large" else 0.62
    b = EnvBuilder(name or "tree_maple_%s" % size)
    h_trunk = 3.4 * s
    _trunk(b, h_trunk + 1.2 * s, 0.28 * s)
    for t in (0.4, 2.5, 4.4):                                                       # main limbs
        b.tube((0, 0, h_trunk * 0.8), (1.5 * s * math.cos(t), 1.5 * s * math.sin(t), h_trunk + 1.4 * s),
               0.13 * s, 0.07 * s, n=5)
    _canopy(b, 0, 0, h_trunk + 2.4 * s, 3.0 * s, 6, ["leaf_a", "leaf_b", "leaf_c"])
    return b.build()


def tree_street(name=None):
    """Street tree (honey locust): high, airy crown with a clear 2.6 m trunk
    so the sidewalk stays walkable; lighter green."""
    b = EnvBuilder(name or "tree_street")
    _trunk(b, 3.6, 0.2)
    for t in (0.9, 3.0, 5.0):
        b.tube((0, 0, 2.8), (1.2 * math.cos(t), 1.2 * math.sin(t), 4.4), 0.1, 0.05, n=5)
    _canopy(b, 0, 0, 5.0, 2.2, 5, ["street_a", "street_b"], squash=0.7, spread=0.7)
    return b.build()


def tree_columnar(size="large", name=None):
    """Tall narrow columnar tree (upright hornbeam / Lombardy-style)."""
    s = 1.0 if size == "large" else 0.6
    b = EnvBuilder(name or "tree_columnar_%s" % size)
    _trunk(b, 1.6 * s, 0.2 * s)
    H = 10.0 * s
    rings = 5
    for i in range(rings):
        f = i / (rings - 1)
        z = 1.6 * s + (H - 2.2 * s) * (0.12 + 0.8 * f)
        r = 1.15 * s * (1.0 - 0.55 * f ** 1.6)
        b.blob((b.rng.uniform(-0.08, 0.08) * s, b.rng.uniform(-0.08, 0.08) * s, z), (r, r, 1.5 * s),
               "column_a" if i % 2 == 0 else "column_b", subdiv=2, jitter=0.07)
    return b.build()


def tree_spruce(size="large", name=None):
    """Spruce: stacked jagged cones, dark blue-green."""
    s = 1.0 if size == "large" else 0.55
    b = EnvBuilder(name or "tree_spruce_%s" % size)
    b.cyl((0, 0), 0, 1.4 * s, 0.24 * s, 0.18 * s, n=6, mat="bark_dark")
    H = 10.0 * s
    tiers = 4
    for i in range(tiers):
        f = i / tiers
        z0 = 1.0 * s + f * (H - 2.6 * s)
        z1 = H if i == tiers - 1 else z0 + 3.4 * s * (1 - 0.35 * f)
        r = 2.4 * s * (1 - 0.7 * f)
        b.cone((0, 0), z0, z1, r, ["spruce_a", "spruce_b"][i % 2], n=14, jag=0.2)
    return b.build()


def tree_cherry(size="large", name=None):
    """Flowering cherry in bloom: low, wide, umbrella crown, pink, dark bark."""
    s = 1.0 if size == "large" else 0.62
    b = EnvBuilder(name or "tree_cherry_%s" % size)
    _trunk(b, 1.6 * s, 0.22 * s, mat="bark_cherry")
    for t in (0.2, 1.4, 2.6, 3.8, 5.0):
        b.tube((0, 0, 1.4 * s), (1.9 * s * math.cos(t), 1.9 * s * math.sin(t), 3.0 * s), 0.11 * s, 0.05 * s,
               n=5, mat="bark_cherry")
    _canopy(b, 0, 0, 3.8 * s, 2.4 * s, 7, ["cherry_a", "cherry_b", "cherry_c"], squash=0.62, spread=0.85)
    return b.build()


def tree_japanese_maple(name=None):
    """Small yard tree: multi-stem Japanese maple, red, layered."""
    b = EnvBuilder(name or "tree_japanese_maple")
    for t, lean in ((0.3, 0.7), (2.4, 0.8), (4.3, 0.6)):
        b.tube((0, 0, 0), (lean * math.cos(t), lean * math.sin(t), 1.6), 0.09, 0.06, n=5, mat="bark_dark")
    for (x, y, z, r, k) in ((0.7, 0.3, 1.7, 0.95, "jmaple_a"), (-0.7, 0.5, 1.8, 0.9, "jmaple_a"),
                            (0.1, -0.7, 1.6, 0.9, "jmaple_a"), (0, 0.1, 2.3, 1.05, "jmaple_b"),
                            (-0.1, 0, 2.85, 0.65, "jmaple_c")):
        b.blob((x, y, z), (r * 1.15, r * 1.15, r * 0.42), k, subdiv=2, jitter=0.06)
    return b.build()


# ---------------------------------------------------------------------------
# 2. Shrubs and foundation planting
# ---------------------------------------------------------------------------
def _shrub(b, x, y, r, keys=("shrub_a", "shrub_b"), squash=0.8):
    b.blob((x, y, r * squash * 0.8), (r, r, r * squash), _pick(b, list(keys)), subdiv=2, jitter=0.1, flat_bottom=0.8)


def _flowers(b, x, y, r, color):
    """Perennial clump: low leafy mound with a cap of colour."""
    b.blob((x, y, r * 0.3), (r, r, r * 0.45), "hosta", subdiv=1, jitter=0.12, flat_bottom=0.6)
    for i in range(5):
        t = 2 * math.pi * i / 5 + b.rng.uniform(0, 0.6)
        d = r * 0.45
        b.blob((x + d * math.cos(t), y + d * math.sin(t), r * 0.62), (r * 0.32, r * 0.32, r * 0.2), color,
               subdiv=1, jitter=0.1)


def _grass_clump(b, x, y, h, r):
    n = 26
    for i in range(n):
        t = 2 * math.pi * i / n + b.rng.uniform(-0.15, 0.15)
        lean = r * b.rng.uniform(0.6, 1.0)
        hh = h * b.rng.uniform(0.75, 1.0)
        base = (x + 0.12 * r * math.cos(t), y + 0.12 * r * math.sin(t), 0.0)
        tip = (x + lean * math.cos(t), y + lean * math.sin(t), hh)
        w = 0.09
        px, py = -math.sin(t) * w, math.cos(t) * w
        key = "grass_orn" if i % 3 else "grass_orn_b"
        b._faces([(base[0] - px, base[1] - py, 0), (base[0] + px, base[1] + py, 0),
                  (base[0], base[1], 0.02), tip], [(0, 1, 3), (1, 2, 3), (2, 0, 3)], key)
        if i % 3 == 0:
            b.blob((tip[0], tip[1], tip[2] + 0.06), (0.07, 0.07, 0.16), "plume", subdiv=1, jitter=0.05)


def shrub_round(size="large", name=None):
    r = 0.62 if size == "large" else 0.42
    b = EnvBuilder(name or "shrub_round_%s" % size)
    _shrub(b, 0, 0, r)
    return b.build()


def hedge_boxwood(length=3.0, name=None):
    """Clipped boxwood hedge run, origin at the centre of its base; runs along X."""
    b = EnvBuilder(name or "hedge_boxwood")
    b.rounded_box((-length / 2, -0.35, 0), (length / 2, 0.35, 0.85), "boxwood", bevel=0.18, segs=2)
    n = int(length / 0.6)
    for i in range(n):                                   # a few soft bumps so it isn't a brick
        x = -length / 2 + 0.3 + i * (length - 0.6) / max(1, n - 1)
        b.blob((x, b.rng.uniform(-0.06, 0.06), 0.82), (0.3, 0.26, 0.1), "boxwood_b", subdiv=1, jitter=0.05)
    return b.build()


def grass_ornamental(name=None):
    b = EnvBuilder(name or "grass_ornamental")
    _grass_clump(b, 0, 0, 1.05, 0.55)
    return b.build()


def _edged_bed(b, poly, edge="edging", h=0.08, edge_w=0.1, edge_h=0.14):
    """Mulch bed over the polygon (CCW, z=0) with a border of edging
    blocks (stone) or a steel strip."""
    b.prism([(x, y, 0) for x, y in poly], (0, 0, h), "mulch")
    n = len(poly)
    for i in range(n):
        (ax, ay), (bx, by) = poly[i], poly[(i + 1) % n]
        L = math.hypot(bx - ax, by - ay)
        if edge == "steel":
            b.beam((ax, ay, edge_h / 2), (bx, by, edge_h / 2), 0.03, edge_h, (0, 0, 1), "steel_edge")
            continue
        k = max(1, int(L / 0.32))
        for j in range(k):
            f0, f1 = j / k + 0.008, (j + 1) / k - 0.008
            p0 = (ax + (bx - ax) * f0, ay + (by - ay) * f0, edge_h / 2)
            p1 = (ax + (bx - ax) * f1, ay + (by - ay) * f1, edge_h / 2)
            b.beam(p0, p1, edge_w, edge_h, (0, 0, 1), "edging" if j % 3 else "edging_dark")


def bed_foundation(length=6.0, depth=1.3, name=None):
    """Foundation bed along a house front. Origin = middle of its BACK edge
    (put it on the wall line / slab edge); the bed extends toward -Y.
    Mulch with a stone edge; a rhythm of round shrubs with perennials
    between, an ornamental grass at each end."""
    b = EnvBuilder(name or "bed_foundation")
    x0, x1 = -length / 2, length / 2
    _edged_bed(b, [(x0, -depth), (x1, -depth), (x1, 0), (x0, 0)])
    yb = -depth * 0.45
    n = max(2, int(round((length - 1.6) / 1.35)))
    colors = ["fl_red", "fl_yellow", "fl_purple", "fl_white", "fl_pink"]
    for i in range(n + 1):
        x = x0 + 0.8 + i * (length - 1.6) / n
        if i in (0, n):
            _grass_clump(b, x, yb, 0.85, 0.42)
        elif i % 2:
            _shrub(b, x, yb + 0.05, 0.42)
        else:
            _shrub(b, x, yb + 0.05, 0.36, keys=("boxwood", "boxwood_b"))
        if i < n:
            _flowers(b, x + (length - 1.6) / n / 2, -depth * 0.72, 0.28, colors[i % len(colors)])
    return b.build()


# ---------------------------------------------------------------------------
# 3. Path and yard
# ---------------------------------------------------------------------------
def stepping_stones(count=6, spacing=0.75, name=None):
    """Irregular flat stones on a gentle S-curve; origin at the first stone,
    running toward +Y."""
    b = EnvBuilder(name or "stepping_stones")
    for i in range(count):
        x = 0.35 * math.sin(i * 0.9)
        b.disc((x, i * spacing), b.rng.uniform(0.27, 0.33), 0, 0.05, "stone_a" if i % 2 else "stone_b",
               n=7, irregular=0.18)
    return b.build()


def walk_border(length=4.0, width=0.6, name=None):
    """Flower border along one side of a front walk. Origin at the walk-side
    end nearest the street; runs toward +Y, bed on +X of the origin line."""
    b = EnvBuilder(name or "walk_border")
    _edged_bed(b, [(0, 0), (width, 0), (width, length), (0, length)], edge="steel", h=0.06)
    colors = ["fl_red", "fl_white", "fl_yellow", "fl_purple", "fl_pink", "fl_orange"]
    y = 0.35
    i = 0
    while y < length - 0.3:
        if i % 3 == 2:
            _shrub(b, width / 2, y, 0.25, keys=("boxwood", "boxwood_b"))
        else:
            _flowers(b, width / 2, y, 0.24, colors[i % len(colors)])
        y += 0.5
        i += 1
    return b.build()


def mailbox(name=None):
    """Mailbox on a white post, black box with a red flag (box faces -Y,
    the street)."""
    s = PROP_SCALE
    b = EnvBuilder(name or "mailbox")
    b.box((-0.06 * s, -0.06 * s, 0), (0.06 * s, 0.06 * s, 1.05 * s), "post_white")
    b.box((-0.14 * s, -0.15 * s, 0.98 * s), (0.14 * s, 0.25 * s, 1.03 * s), "post_white")
    z0 = 1.03 * s
    hw, L, hb = 0.12 * s, 0.5 * s, 0.13 * s
    prof = [(-hw, z0), (hw, z0), (hw, z0 + hb)] + [(hw * math.cos(math.pi * i / 6), z0 + hb + hw * math.sin(math.pi * i / 6))
                                                    for i in range(1, 6)] + [(-hw, z0 + hb)]
    b.prism([(x, -0.3 * s, z) for x, z in prof], (0, L, 0), "mailbox")
    b.box((hw, 0.0, z0 + 0.1 * s), (hw + 0.02 * s, 0.03 * s, z0 + 0.32 * s), "flag")
    b.box((hw, 0.03 * s, z0 + 0.24 * s), (hw + 0.02 * s, 0.14 * s, z0 + 0.32 * s), "flag")
    return b.build()


def bed_corner(arm=2.6, width=0.9, name=None):
    """L-shaped garden bed for a yard corner (stone edge), origin at the
    outer corner; arms run +X and +Y."""
    b = EnvBuilder(name or "bed_corner")
    poly = [(0, 0), (arm, 0), (arm, width), (width, width), (width, arm), (0, arm)]
    _edged_bed(b, poly)
    _shrub(b, width * 0.5, width * 0.5, 0.45)
    _grass_clump(b, arm - 0.45, width * 0.5, 0.8, 0.38)
    _grass_clump(b, width * 0.5, arm - 0.45, 0.8, 0.38)
    for i, (x, y) in enumerate(((1.35, 0.45), (0.45, 1.35), (1.9, 0.5))):
        _flowers(b, x, y, 0.26, ["fl_purple", "fl_yellow", "fl_red"][i])
    return b.build()


def planter(name=None):
    """Terracotta planter with a round shrub and trailing flowers (porch/stoop)."""
    s = PROP_SCALE
    b = EnvBuilder(name or "planter")
    b.cyl((0, 0), 0, 0.5 * s, 0.24 * s, 0.32 * s, n=10, mat="terracotta")
    b.cyl((0, 0), 0.46 * s, 0.56 * s, 0.35 * s, 0.35 * s, n=10, mat="terracotta_d")
    b.cyl((0, 0), 0.52 * s, 0.53 * s, 0.3 * s, 0.3 * s, n=10, mat="soil")
    b.blob((0, 0, 0.82 * s), (0.3 * s, 0.3 * s, 0.3 * s), "shrub_b", subdiv=2)
    for i in range(6):
        t = 2 * math.pi * i / 6
        b.blob((0.3 * s * math.cos(t), 0.3 * s * math.sin(t), 0.6 * s), (0.1 * s, 0.1 * s, 0.08 * s),
               "fl_pink" if i % 2 else "fl_white", subdiv=1)
    return b.build()


# ---------------------------------------------------------------------------
# 4. Street
# ---------------------------------------------------------------------------
VERGE_W = 0.75       # tree lawn between curb and sidewalk (road half 3 + verge 0.75 + sidewalk 2)
CURB_H = 0.15


def tree_lawn(length=6.0, name=None, with_tree=True):
    """Street tree in its tree lawn: a curb + grass strip segment (0.75 m,
    today's verge) with a mulch ring and a street tree. Origin = curb line
    at the segment's middle, on the road edge; grass toward +Y."""
    b = EnvBuilder(name or "tree_lawn")
    b.box((-length / 2, -0.15, 0), (length / 2, 0.0, CURB_H), "curb")
    b.box((-length / 2, 0.0, 0), (length / 2, VERGE_W, 0.04), "lawn_tex")
    b.texture("lawn_tex", tinted(lawn_mask(), ENV["lawn"], "lawn"),
              lambda co: (co.x / LAWN_TILE_M, co.y / LAWN_TILE_M))
    b.disc((0, VERGE_W / 2), 0.36, 0.04, 0.07, "mulch", n=10)
    ob = b.build()
    if with_tree:
        t = tree_street()
        t.location = (0, VERGE_W / 2, 0.05)
        t.parent = ob
    return ob


def lamp_post(name=None):
    """Classic black lamp post with a lantern head."""
    s = PROP_SCALE
    b = EnvBuilder(name or "lamp_post")
    b.cyl((0, 0), 0, 0.5 * s, 0.17 * s, 0.13 * s, n=8, mat="lamp_black")
    b.cyl((0, 0), 0.5 * s, 3.5 * s, 0.07 * s, 0.055 * s, n=8, mat="lamp_black")
    b.cyl((0, 0), 3.5 * s, 3.6 * s, 0.14 * s, 0.14 * s, n=8, mat="lamp_black")
    b.cyl((0, 0), 3.6 * s, 4.05 * s, 0.13 * s, 0.2 * s, n=8, mat="lamp_glass", rot=22.5)
    b.cyl((0, 0), 4.05 * s, 4.32 * s, 0.27 * s, 0.05 * s, n=8, mat="lamp_black", rot=22.5)
    b.cyl((0, 0), 4.32 * s, 4.42 * s, 0.04 * s, 0.0, n=6, mat="lamp_black")
    return b.build()


def stop_sign(name=None):
    """Stop sign: grey pole, red octagon with a white border and STOP,
    facing -Y (oncoming traffic)."""
    s = PROP_SCALE
    b = EnvBuilder(name or "stop_sign")
    b.box((-0.03 * s, -0.03 * s, 0), (0.03 * s, 0.03 * s, 2.15 * s), "pole_grey")
    R = 0.38 * s / math.cos(math.radians(22.5))
    zc = 2.1 * s
    oct_ = [(R * math.cos(math.radians(22.5 + 45 * i)), zc + R * math.sin(math.radians(22.5 + 45 * i)))
            for i in range(8)]
    b.prism([(x, -0.045 * s, z) for x, z in oct_], (0, 0.02 * s, 0), "pole_grey")
    face = [(x, -0.047 * s, z) for x, z in reversed(oct_)]
    tex = sign_texture("stop", "STOP", ENV["stop_red"], "#FFFFFF", size=(256, 256), border="#FFFFFF", octagon=True)
    b.texture("stop_face", tex, lambda co: (0.5 + co.x / (2 * R), 0.5 + (co.z - zc) / (2 * R)))
    b._faces(face, [tuple(range(8))], "stop_face")
    return b.build()


def street_sign(a="MAPLE ST", c="OAK AVE", name=None):
    """Street-name sign: two green blades crossed on a grey pole, white text
    on both faces of each blade."""
    s = PROP_SCALE
    b = EnvBuilder(name or "street_sign")
    b.box((-0.03 * s, -0.03 * s, 0), (0.03 * s, 0.03 * s, 2.75 * s), "pole_grey")
    L, H, T = 0.95 * s, 0.2 * s, 0.02 * s
    for i, (txt, z, axis) in enumerate(((a, 2.5 * s, "x"), (c, 2.75 * s, "y"))):
        tex = sign_texture("blade_%d" % i, txt, ENV["sign_green"], "#FFFFFF", size=(512, 108), border="#FFFFFF")
        if axis == "x":
            b.box((-L / 2, -T / 2, z - H / 2), (L / 2, T / 2, z + H / 2), "sign_green")
        else:
            b.box((-T / 2, -L / 2, z - H / 2), (T / 2, L / 2, z + H / 2), "sign_green")
        # two text faces per blade; u runs left-to-right as seen from each side
        for sgn in (-1, 1):
            key = "blade_%d_%s" % (i, "a" if sgn < 0 else "b")
            o = sgn * (T / 2 + 0.002)
            if axis == "x":
                q = [(-L / 2, o, z - H / 2), (L / 2, o, z - H / 2), (L / 2, o, z + H / 2), (-L / 2, o, z + H / 2)]
                fn = (lambda co, z=z, g=sgn: (0.5 - g * co.x / L, 0.5 + (co.z - z) / H))
            else:
                q = [(o, L / 2, z - H / 2), (o, -L / 2, z - H / 2), (o, -L / 2, z + H / 2), (o, L / 2, z + H / 2)]
                fn = (lambda co, z=z, g=sgn: (0.5 + g * co.y / L, 0.5 + (co.z - z) / H))
            b._faces(q, [(0, 1, 2, 3) if (sgn < 0) == (axis == "x") else (3, 2, 1, 0)], key)
            b.texture(key, tex, fn)
    return b.build()


def hydrant(name=None):
    s = PROP_SCALE
    b = EnvBuilder(name or "hydrant")
    b.cyl((0, 0), 0, 0.08 * s, 0.17 * s, 0.17 * s, n=10, mat="hydrant")
    b.cyl((0, 0), 0.08 * s, 0.58 * s, 0.12 * s, 0.11 * s, n=10, mat="hydrant")
    b.cyl((0, 0), 0.58 * s, 0.64 * s, 0.15 * s, 0.15 * s, n=10, mat="hydrant")
    b.cyl((0, 0), 0.64 * s, 0.76 * s, 0.12 * s, 0.05 * s, n=10, mat="hydrant_cap")
    b.cyl((0, 0), 0.76 * s, 0.81 * s, 0.03 * s, 0.03 * s, n=6, mat="hydrant_cap")
    for dx, dy, r in ((1, 0, 0.055), (-1, 0, 0.055), (0, -1, 0.075)):       # nozzles; the big one faces the street
        b.tube((0, 0, 0.42 * s), (dx * 0.2 * s, dy * 0.2 * s, 0.42 * s), r * s, r * s, n=8, mat="hydrant_cap")
    return b.build()


def bench(name=None):
    """Park bench: wooden slats on cast-iron ends; faces -Y."""
    s = PROP_SCALE
    b = EnvBuilder(name or "bench")
    L = 1.6 * s
    for x in (-L / 2 + 0.12 * s, L / 2 - 0.12 * s):
        b.box((x - 0.035 * s, -0.25 * s, 0), (x + 0.035 * s, -0.2 * s, 0.45 * s), "bench_iron")
        b.box((x - 0.035 * s, 0.15 * s, 0), (x + 0.035 * s, 0.2 * s, 0.45 * s), "bench_iron")
        b.box((x - 0.035 * s, -0.26 * s, 0.4 * s), (x + 0.035 * s, 0.22 * s, 0.45 * s), "bench_iron")
        b.beam((x, 0.18 * s, 0.42 * s), (x, 0.28 * s, 0.9 * s), 0.07 * s, 0.05 * s, (0, -1, 0), "bench_iron")
        b.box((x - 0.035 * s, -0.27 * s, 0.6 * s), (x + 0.035 * s, -0.05 * s, 0.64 * s), "bench_iron")
    for i in range(4):                                              # seat slats
        y = -0.24 * s + i * 0.115 * s
        b.box((-L / 2, y, 0.45 * s), (L / 2, y + 0.09 * s, 0.49 * s), "bench_wood")
    for i in range(3):                                              # back slats
        z = 0.58 * s + i * 0.11 * s
        y = 0.2 * s + (z - 0.42 * s) * 0.21
        b.box((-L / 2, y - 0.02 * s, z), (L / 2, y + 0.02 * s, z + 0.08 * s), "bench_wood")
    return b.build()


# ---------------------------------------------------------------------------
# 5. Lawn
# ---------------------------------------------------------------------------
def lawn_patch(x0, x1, y0, y1, name="lawn", z=-0.01):
    """Ground slab with the tiled lawn texture (UVs in metres / LAWN_TILE_M)."""
    b = EnvBuilder(name)
    b.box((x0, y0, z - 0.05), (x1, y1, z), "lawn_tex")
    b.texture("lawn_tex", tinted(lawn_mask(), ENV["lawn"], "lawn"),
              lambda co: (co.x / LAWN_TILE_M, co.y / LAWN_TILE_M))
    return b.build()


# name -> (builder, category, label)
PIECES = {
    "tree-maple-large": (lambda: tree_maple("large"), "trees", "Maple (large)"),
    "tree-maple-small": (lambda: tree_maple("small"), "trees", "Maple (young)"),
    "tree-columnar-large": (lambda: tree_columnar("large"), "trees", "Columnar (large)"),
    "tree-columnar-small": (lambda: tree_columnar("small"), "trees", "Columnar (young)"),
    "tree-spruce-large": (lambda: tree_spruce("large"), "trees", "Spruce (large)"),
    "tree-spruce-small": (lambda: tree_spruce("small"), "trees", "Spruce (young)"),
    "tree-cherry-large": (lambda: tree_cherry("large"), "trees", "Cherry (large)"),
    "tree-cherry-small": (lambda: tree_cherry("small"), "trees", "Cherry (young)"),
    "tree-japanese-maple": (tree_japanese_maple, "trees", "Japanese maple (yard)"),
    "shrub-round-large": (lambda: shrub_round("large"), "plants", "Round shrub"),
    "shrub-round-small": (lambda: shrub_round("small"), "plants", "Round shrub (small)"),
    "hedge-boxwood": (hedge_boxwood, "plants", "Boxwood hedge run 3 m"),
    "grass-ornamental": (grass_ornamental, "plants", "Ornamental grass"),
    "bed-foundation": (bed_foundation, "plants", "Foundation bed 6 m"),
    "walk-border": (walk_border, "plants", "Walkway flower border 4 m"),
    "bed-corner": (bed_corner, "plants", "Garden-bed corner"),
    "stepping-stones": (stepping_stones, "plants", "Stepping stones"),
    "planter": (planter, "plants", "Planter"),
    "mailbox": (mailbox, "plants", "Mailbox on post"),
    "tree-lawn": (tree_lawn, "street", "Street tree in tree lawn"),
    "lamp-post": (lamp_post, "street", "Lamp post"),
    "street-sign": (street_sign, "street", "Street-name sign"),
    "stop-sign": (stop_sign, "street", "Stop sign"),
    "hydrant": (hydrant, "street", "Fire hydrant"),
    "bench": (bench, "street", "Bench"),
}
