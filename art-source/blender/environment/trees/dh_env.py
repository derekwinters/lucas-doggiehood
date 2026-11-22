"""Doggiehood environment kit (round 2, high-detail): trees, shrubs and foundation planting, yard
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
import zlib

HERE = os.path.dirname(os.path.abspath(__file__))
KIT = os.path.normpath(os.path.join(HERE, "..", ".."))   # art-source/blender: the committed house kit
sys.path.insert(0, KIT)
import bpy  # noqa: E402
import bmesh  # noqa: E402
from mathutils import Matrix, Vector, noise  # noqa: E402
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
    "spruce_a": "#22503A", "spruce_b": "#336A49", "spruce_c": "#2A5D42",       # spruce
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
# extra textures and material finishes
# ---------------------------------------------------------------------------
MULCH_TILE_M = 1.0


def mulch_mask():
    """Tileable greyscale mask of shredded-bark chips (1 m tile)."""
    from PIL import Image, ImageDraw
    os.makedirs(TEX_DIR, exist_ok=True)
    path = os.path.join(TEX_DIR, "mulch_mask.png")
    if os.path.exists(path):
        return path
    n = 256
    rng = random.Random(11)
    img = Image.new("L", (n, n), 150)
    d = ImageDraw.Draw(img)
    for _ in range(1400):
        x, y = rng.uniform(0, n), rng.uniform(0, n)
        L, w, a = rng.uniform(5, 14), rng.uniform(1.5, 3.5), rng.uniform(0, math.pi)
        v = rng.randint(90, 240)
        ca, sa = math.cos(a), math.sin(a)
        for ox in (-n, 0, n):
            for oy in (-n, 0, n):
                pts = [(x + ox + ca * L / 2 - sa * w, y + oy + sa * L / 2 + ca * w),
                       (x + ox - ca * L / 2 - sa * w, y + oy - sa * L / 2 + ca * w),
                       (x + ox - ca * L / 2 + sa * w, y + oy - sa * L / 2 - ca * w),
                       (x + ox + ca * L / 2 + sa * w, y + oy + sa * L / 2 - ca * w)]
                d.polygon(pts, fill=v)
    img.save(path)
    return path


FINISH = {   # key -> (roughness, metallic, emission strength)
    "lamp_glass": (0.08, 0.0, 1.2), "lamp_black": (0.45, 0.5, 0), "pole_grey": (0.35, 0.8, 0),
    "hydrant": (0.35, 0.0, 0), "hydrant_cap": (0.3, 0.85, 0), "bench_iron": (0.45, 0.5, 0),
    "mailbox": (0.4, 0.4, 0), "steel_edge": (0.45, 0.6, 0), "sign_green": (0.35, 0, 0),
    "stop_face": (0.35, 0, 0), "terracotta": (0.75, 0, 0), "bench_wood": (0.6, 0, 0),
    "metal": (0.35, 0.8, 0),
}


def _finish(mat, key):
    if key not in FINISH:
        return
    r, m, e = FINISH[key]
    b = mat.node_tree.nodes.get("Principled BSDF")
    b.inputs["Roughness"].default_value = r
    b.inputs["Metallic"].default_value = m
    if e:
        b.inputs["Emission Color"].default_value = K.hex_rgba(ENV[key])
        b.inputs["Emission Strength"].default_value = e


def seed_of(name):
    return zlib.crc32(name.encode()) & 0xFFFFFF


# ---------------------------------------------------------------------------
# builder: smooth-shaded, bevelled, textured where it helps
# ---------------------------------------------------------------------------
SMOOTH_ANGLE = 50.0     # props: bevels smooth, true corners stay crisp (auto-smooth by angle)
ORGANIC_ANGLE = 85.0    # plants: everything smooth except razor folds


class EnvBuilder(K.MeshBuilder):
    def __init__(self, name, palette=ENV, smooth=SMOOTH_ANGLE):
        super().__init__(name, palette)
        self.smooth = smooth
        self.textures = {}
        self.rng = random.Random(seed_of(name))
        self.noff = Vector((self.rng.uniform(0, 50), self.rng.uniform(0, 50), self.rng.uniform(0, 50)))

    def texture(self, key, path, uv_fn):
        self.textures[key] = (path, uv_fn)
        self._mi(key)

    def build(self, collection=None):
        bm = self.bm
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
            mat = K.material("DH_Env_%s" % key, hexc, texture=tex)
            _finish(mat, key)
            me.materials.append(mat)
        for p in me.polygons:
            p.use_smooth = True
        me.set_sharp_from_angle(angle=math.radians(self.smooth))
        ob = bpy.data.objects.new(self.name, me)
        (collection or bpy.context.scene.collection).objects.link(ob)
        return ob

    # --- low-level: rings ---------------------------------------------------
    def sweep(self, rings, mat, start=None, end=None, cap_start=False, cap_end=False, closed=True):
        """Skin a list of equal-length point rings. start/end: pole points that
        close the ends with a fan; cap_*: flat n-gon caps instead."""
        n = len(rings[0])
        verts = [tuple(p) for r in rings for p in r]
        faces = []
        m = n if closed else n - 1
        for k in range(len(rings) - 1):
            a, b = k * n, (k + 1) * n
            for i in range(m):
                j = (i + 1) % n
                faces.append((a + i, a + j, b + j, b + i))
        if start is not None:
            verts.append(tuple(start))
            s = len(verts) - 1
            faces += [(s, (i + 1) % n, i) for i in range(m)]
        elif cap_start:
            faces.append(tuple(reversed(range(n))))
        if end is not None:
            verts.append(tuple(end))
            e = len(verts) - 1
            base = (len(rings) - 1) * n
            faces += [(base + i, base + (i + 1) % n, e) for i in range(m)]
        elif cap_end:
            base = (len(rings) - 1) * n
            faces.append(tuple(range(base, base + n)))
        self._faces(verts, faces, mat)

    def lathe(self, profile, mat, n=24, c=(0, 0), flutes=0, flute_depth=0.0, rot=0.0):
        """Surface of revolution from a (r, z) profile listed bottom to top.
        r == 0 at either end closes it with a pole; otherwise flat caps."""
        cx, cy = c
        rings, start, end = [], None, None
        prof = list(profile)
        if prof[0][0] <= 1e-6:
            start = (cx, cy, prof[0][1])
            prof = prof[1:]
        if prof[-1][0] <= 1e-6:
            end = (cx, cy, prof[-1][1])
            prof = prof[:-1]
        for r, z in prof:
            ring = []
            for i in range(n):
                t = math.radians(rot) + 2 * math.pi * i / n
                rr = r * (1 - flute_depth * (0.5 + 0.5 * math.cos(flutes * t))) if flutes else r
                ring.append((cx + rr * math.cos(t), cy + rr * math.sin(t), z))
            rings.append(ring)
        self.sweep(rings, mat, start=start, end=end, cap_start=start is None, cap_end=end is None)

    def tube(self, pts, radii, mat, n=8, cap=True, flat=1.0):
        """Tapered tube along a polyline (parallel-transport frames); flat < 1
        squashes the cross-section (fronds)."""
        pts = [Vector(p) for p in pts]
        rings = []
        t0 = (pts[1] - pts[0]).normalized()
        ref = Vector((0, 0, 1)) if abs(t0.z) < 0.9 else Vector((1, 0, 0))
        u = t0.cross(ref).normalized()
        for k, p in enumerate(pts):
            if k == 0:
                t = t0
            elif k == len(pts) - 1:
                t = (pts[k] - pts[k - 1]).normalized()
            else:
                t = (pts[k + 1] - pts[k - 1]).normalized()
            u = (u - t * u.dot(t)).normalized()
            v = t.cross(u)
            r = radii[k]
            rings.append([p + (u * math.cos(2 * math.pi * i / n) + v * flat * math.sin(2 * math.pi * i / n)) * r
                          for i in range(n)])
        end = pts[-1] + (pts[-1] - pts[-2]).normalized() * radii[-1] * 0.6 if cap else None
        self.sweep(rings, mat, end=end, cap_start=cap)

    def blob(self, c, r, mat, nu=24, nv=12, e=1.0, amp=0.1, freq=1.6, flat_bottom=None, detail=0.0, detail_freq=9.0):
        """Smooth superquadric with low-frequency noise (canopy clusters,
        shrubs, stones). e < 1 squares it off (hedges, slabs)."""
        rx, ry, rz = (r, r, r) if isinstance(r, (int, float)) else r
        cx, cy, cz = c
        sp = lambda x, k: math.copysign(abs(x) ** k, x)  # noqa: E731
        rings = []

        def pt(u, v):
            d = Vector((sp(math.cos(v), e) * sp(math.cos(u), e), sp(math.cos(v), e) * sp(math.sin(u), e),
                        sp(math.sin(v), e)))
            q = Vector((d.x * rx, d.y * ry, d.z * rz))
            k = 1.0 + amp * noise.noise(q * freq / max(rx, ry, rz) * 1.5 + self.noff + Vector(c))
            if detail:
                k += detail * noise.noise(q * detail_freq + self.noff)
            q *= k
            z = q.z
            if flat_bottom is not None:
                z = max(z, -flat_bottom * rz)
            return (cx + q.x, cy + q.y, cz + z)

        for j in range(1, nv):
            v = -math.pi / 2 + math.pi * j / nv
            rings.append([pt(2 * math.pi * i / nu, v) for i in range(nu)])
        self.sweep(rings, mat, start=pt(0, -math.pi / 2), end=pt(0, math.pi / 2))

    def leaves(self, c, r, count, mats, size=0.14, aspect=0.5, shell=(0.97, 1.04), flat_bottom=None,
               up_bias=0.0, cup=0.14, tilt=0.08):
        """Scatter small leaf cards (4 tris, slightly folded) over an
        ellipsoid shell so canopies get a leafy silhouette."""
        rx, ry, rz = (r, r, r) if isinstance(r, (int, float)) else r
        rng = self.rng
        for _ in range(count):
            while True:
                d = Vector((rng.gauss(0, 1), rng.gauss(0, 1), rng.gauss(0, 1)))
                if d.length > 1e-3:
                    d.normalize()
                    break
            if flat_bottom is not None and d.z < -flat_bottom:
                d.z = -flat_bottom * rng.uniform(0.6, 1.0)
                d.normalize()
            d.z += up_bias
            k = rng.uniform(*shell)
            p = Vector((c[0] + d.x * rx * k, c[1] + d.y * ry * k, c[2] + d.z * rz * k))
            nrm = Vector((d.x / rx, d.y / ry, d.z / rz)).normalized()
            a = Vector((rng.gauss(0, 1), rng.gauss(0, 1), rng.gauss(0, 1)))
            t = (a - nrm * a.dot(nrm)).normalized()
            s = nrm.cross(t)
            L = size * rng.uniform(0.75, 1.25)
            w = L * aspect
            tiltv = nrm * L * tilt
            base, tip = p - t * L / 2, p + t * L / 2 + tiltv
            l, rgt = p - s * w / 2 + nrm * w * cup, p + s * w / 2 + nrm * w * cup
            mid = p + nrm * 0.0
            self._faces([base, l, tip, rgt, mid], [(0, 4, 1), (1, 4, 2), (2, 4, 3), (3, 4, 0)],
                        mats[rng.randrange(len(mats))])

    def bevel_solid(self, make, mat, offset=0.03, segs=3, side_mat=None):
        """Build a closed solid in a temp bmesh via make(bm), bevel all its
        edges, then copy it in."""
        tmp = bmesh.new()
        make(tmp)
        bmesh.ops.recalc_face_normals(tmp, faces=tmp.faces)
        bmesh.ops.bevel(tmp, geom=list(tmp.edges) + list(tmp.verts), offset=offset, segments=segs,
                        affect="EDGES", profile=0.5, clamp_overlap=True)
        mi = self._mi(mat)
        vmap = {v: self.bm.verts.new(v.co.copy()) for v in tmp.verts}
        for f in tmp.faces:
            try:
                nf = self.bm.faces.new([vmap[v] for v in f.verts])
                nf.material_index = mi
            except ValueError:
                pass
        tmp.free()

    def rbox(self, mn, mx, mat, bevel=0.02, segs=2):
        def make(tmp):
            bmesh.ops.create_cube(tmp, size=1.0)
            for vt in tmp.verts:
                vt.co = Vector(((vt.co.x + 0.5) * (mx[0] - mn[0]) + mn[0], (vt.co.y + 0.5) * (mx[1] - mn[1]) + mn[1],
                                (vt.co.z + 0.5) * (mx[2] - mn[2]) + mn[2]))
        self.bevel_solid(make, mat, bevel, segs)

    def rbeam(self, p0, p1, w, h, up, mat, bevel=0.01, segs=2):
        p0, p1, up = Vector(p0), Vector(p1), Vector(up)
        d = (p1 - p0).normalized()
        nn = (up - d * up.dot(d)).normalized()
        s = d.cross(nn).normalized()

        def make(tmp):
            vs = []
            for p in (p0, p1):
                for a, b in ((-1, -1), (1, -1), (1, 1), (-1, 1)):
                    vs.append(tmp.verts.new(p + s * a * w / 2 + nn * b * h / 2))
            for f in ((0, 1, 2, 3), (4, 7, 6, 5), (0, 4, 5, 1), (1, 5, 6, 2), (2, 6, 7, 3), (3, 7, 4, 0)):
                tmp.faces.new([vs[i] for i in f])
        self.bevel_solid(make, mat, bevel, segs)

    def rprism(self, pts, height, mat, bevel=0.02, segs=2, axis_off=None):
        """Bevelled prism: polygon pts (3D, CCW) extruded by axis_off (default +Z height)."""
        off = Vector(axis_off) if axis_off else Vector((0, 0, height))

        def make(tmp):
            a = [tmp.verts.new(Vector(p)) for p in pts]
            b = [tmp.verts.new(Vector(p) + off) for p in pts]
            n = len(pts)
            tmp.faces.new(list(reversed(a)))
            tmp.faces.new(b)
            for i in range(n):
                j = (i + 1) % n
                tmp.faces.new([a[i], a[j], b[j], b[i]])
        self.bevel_solid(make, mat, bevel, segs)

    def disc(self, c, r, z0, z1, mat, n=24, irregular=0.0):
        """Kept for the round-1 API (mulch rings): a bevelled, slightly
        irregular round slab."""
        pts = []
        for i in range(n):
            t = 2 * math.pi * i / n
            rr = r * (1 + irregular * noise.noise(Vector((math.cos(t), math.sin(t), 0)) * 1.3 + self.noff))
            pts.append((c[0] + rr * math.cos(t), c[1] + rr * math.sin(t), z0))
        self.rprism(pts, z1 - z0, mat, bevel=min(0.4 * (z1 - z0), 0.03), segs=2)


# ---------------------------------------------------------------------------
# 1. Trees
# ---------------------------------------------------------------------------
def _bend_path(p0, d, length, segs, rng, wobble=0.08, gravity=0.0):
    pts, p, d = [Vector(p0)], Vector(p0), Vector(d).normalized()
    step = length / segs
    for _ in range(segs):
        d = (d + Vector((rng.gauss(0, wobble), rng.gauss(0, wobble), rng.gauss(0, wobble) - gravity))).normalized()
        p = p + d * step
        pts.append(p.copy())
    return pts


def _rot_about(v, axis, ang):
    return Matrix.Rotation(ang, 3, Vector(axis).normalized()) @ Vector(v)


def _grow(b, p0, d, length, radius, depth, tips, mat, spread=(0.45, 0.8), kids=(2, 3), up=0.25,
          child_len=0.68, n=10, gravity=0.0):
    rng = b.rng
    segs = 5 if depth > 0 else 4
    pts = _bend_path(p0, d, length, segs, rng, gravity=gravity)
    radii = [radius * (1 - 0.45 * k / segs) for k in range(segs + 1)]
    b.tube(pts, radii, mat, n=max(5, n), cap=True)
    if depth == 0:
        tips.append((pts[-1], (pts[-1] - pts[-2]).normalized()))
        return
    tdir = (pts[-1] - pts[-2]).normalized()
    nk = rng.randint(*kids)
    phase = rng.uniform(0, 2 * math.pi)
    ref = Vector((0, 0, 1)) if abs(tdir.z) < 0.95 else Vector((1, 0, 0))
    side = tdir.cross(ref).normalized()
    for k in range(nk):
        ang = rng.uniform(*spread)
        cd = _rot_about(tdir, side, ang)
        cd = _rot_about(cd, tdir, phase + 2 * math.pi * k / nk)
        cd = (cd + Vector((0, 0, up))).normalized()
        start_i = segs - (0 if k == 0 else rng.randint(0, 1))
        _grow(b, pts[start_i], cd, length * child_len * rng.uniform(0.85, 1.1), radii[start_i] * 0.62, depth - 1,
              tips, mat, spread, kids, up, child_len, n - 2, gravity)


def _foliage(b, centres, mats, leaf_size=0.2, leaves_per=110, core=True, squash=0.8, amp=0.12, nu=28, nv=14,
             aspect=0.5, lumps=0.07, lump_freq=2.4):
    """Leaf clusters: a smooth, lumpy (cumulus-like) core per cluster, with
    leaf cards hugging its surface mostly in the core's own colour."""
    for (c, r) in centres:
        rr = (r, r, r * squash)
        k = b.rng.randrange(len(mats))
        if core:
            b.blob(c, (r * 0.92, r * 0.92, r * squash * 0.92), mats[k], nu=nu, nv=nv, amp=amp, detail=lumps,
                   detail_freq=lump_freq)
        b.leaves(c, rr, leaves_per, [mats[k]], size=leaf_size, aspect=aspect, shell=(0.9, 1.03))


def _crown_from_tips(b, tips, r, jitter=0.25, extra=0):
    cs = []
    for p, d in tips:
        q = p + d * r * 0.45 + Vector((0, 0, r * 0.3))
        cs.append((Vector((q.x + b.rng.uniform(-jitter, jitter), q.y + b.rng.uniform(-jitter, jitter), q.z)),
                   r * b.rng.uniform(0.85, 1.15)))
    return cs


def tree_maple(size="large", name=None):
    """Round deciduous shade tree (red maple / oak): a tapered trunk that
    forks into limbs, a full dome of overlapping leaf clusters."""
    s = 1.0 if size == "large" else 0.62
    b = EnvBuilder(name or "tree_maple_%s" % size, smooth=ORGANIC_ANGLE)
    trunk = _bend_path((0, 0, 0), (0, 0, 1), 3.0 * s, 4, b.rng, wobble=0.03)
    b.tube(trunk, [0.34 * s, 0.27 * s, 0.25 * s, 0.23 * s, 0.21 * s], "bark", n=14, cap=False)
    b.lathe([(0.5 * s, 0.0), (0.36 * s, 0.18 * s), (0.3 * s, 0.4 * s), (0.0, 0.4 * s)], "bark", n=14)  # root flare
    tips = []
    for k in range(4):
        a = 2 * math.pi * k / 4 + 0.4
        d = Vector((0.55 * math.cos(a), 0.55 * math.sin(a), 1.0))
        _grow(b, trunk[-1], d, 2.0 * s, 0.18 * s, 2, tips, "bark", spread=(0.35, 0.7), kids=(2, 2), up=0.35,
              child_len=0.62, n=10)
    cs = _crown_from_tips(b, tips, 1.25 * s)
    top = trunk[-1].z + 2.6 * s
    cs += [(Vector((0, 0, top)), 1.9 * s), (Vector((0.9 * s, 0.4 * s, top - 0.5 * s)), 1.5 * s),
           (Vector((-0.8 * s, -0.6 * s, top - 0.4 * s)), 1.5 * s)]
    _foliage(b, cs, ["leaf_a", "leaf_b", "leaf_c"], leaf_size=0.34 * s ** 0.5, leaves_per=int(150 * (1 if s == 1 else 0.6)))
    return b.build()


def tree_street(name=None):
    """Street tree (honey locust): clear 3 m trunk, high airy crown of
    smaller, lighter clusters."""
    b = EnvBuilder(name or "tree_street", smooth=ORGANIC_ANGLE)
    trunk = _bend_path((0, 0, 0), (0, 0, 1), 3.2, 4, b.rng, wobble=0.03)
    b.tube(trunk, [0.24, 0.19, 0.17, 0.16, 0.15], "bark", n=12, cap=False)
    b.lathe([(0.34, 0.0), (0.25, 0.14), (0.2, 0.32), (0.0, 0.32)], "bark", n=12)
    tips = []
    for k in range(3):
        a = 2 * math.pi * k / 3 + 0.9
        _grow(b, trunk[-1], (0.5 * math.cos(a), 0.5 * math.sin(a), 1.0), 1.4, 0.12, 2, tips, "bark",
              spread=(0.35, 0.65), kids=(2, 2), up=0.45, child_len=0.55, n=9)
    cs = _crown_from_tips(b, tips, 0.95)
    cs.append((Vector((0, 0, trunk[-1].z + 2.0)), 1.3))
    _foliage(b, cs, ["street_a", "street_b", "leaf_b"], leaf_size=0.28, leaves_per=110, squash=0.75, nu=24, nv=12)
    return b.build()


def tree_columnar(size="large", name=None):
    """Tall narrow upright hornbeam: a central leader with short upswept
    branches inside a dense, tapering column of clusters."""
    s = 1.0 if size == "large" else 0.6
    b = EnvBuilder(name or "tree_columnar_%s" % size, smooth=ORGANIC_ANGLE)
    H = 10.0 * s
    trunk = _bend_path((0, 0, 0), (0, 0, 1), H * 0.82, 6, b.rng, wobble=0.015)
    b.tube(trunk, [0.24 * s * (1 - 0.75 * k / 6) for k in range(7)], "bark", n=12)
    b.lathe([(0.34 * s, 0.0), (0.24 * s, 0.15 * s), (0.0, 0.3 * s)], "bark", n=12)
    cs = []
    rings = 9
    for i in range(rings):
        f = i / (rings - 1)
        z = 1.7 * s + (H - 2.4 * s) * f
        r = 1.0 * s * (1.0 - 0.6 * f ** 1.5)
        for k in range(3 if i < rings - 2 else 1):
            a = 2 * math.pi * k / 3 + i * 1.1
            off = r * 0.35 if i < rings - 2 else 0
            cs.append((Vector((off * math.cos(a), off * math.sin(a), z)), r * 0.85))
            if i < rings - 2 and k == 0:
                p = trunk[min(6, int(f * 6))]
                b.tube([p, Vector((off * math.cos(a), off * math.sin(a), z + 0.3 * s))], [0.07 * s, 0.04 * s], "bark", n=6)
    _foliage(b, cs, ["column_a", "column_b", "leaf_c"], leaf_size=0.2 * s ** 0.5, leaves_per=int(48 if s == 1 else 32),
             squash=1.15, nu=18, nv=9)
    return b.build()


def tree_spruce(size="large", name=None):
    """Spruce: a straight tapering trunk carrying layered whorls of drooping,
    flattened needle branches (fronds), narrowing to a pointed leader."""
    s = 1.0 if size == "large" else 0.55
    b = EnvBuilder(name or "tree_spruce_%s" % size, smooth=ORGANIC_ANGLE)
    rng = b.rng
    H = 10.0 * s
    b.lathe([(0.3 * s, 0), (0.24 * s, 0.2 * s), (0.2 * s, 1.0 * s), (0.04 * s, H - 0.6 * s), (0.0, H - 0.5 * s)],
            "bark_dark", n=12)
    levels = 13 if s == 1 else 10
    greens = ["spruce_a", "spruce_b", "spruce_c"]
    for i in range(levels):
        f = i / levels
        z = 0.9 * s + f * (H - 1.9 * s)
        L = (2.5 * s) * (1 - f) ** 0.85 + 0.35 * s
        nb = 8 if f < 0.7 else 6
        ph = rng.uniform(0, 2 * math.pi)
        for k in range(nb):
            a = ph + 2 * math.pi * k / nb + rng.uniform(-0.15, 0.15)
            d = Vector((math.cos(a), math.sin(a), 0))
            segs = 12
            pts, radii = [], []
            droop = 0.45 * L
            for j in range(segs + 1):
                t = j / segs
                pts.append(Vector((0, 0, z)) + d * (L * t) + Vector((0, 0, 0.18 * L * t - droop * t * t)))
                w = (0.36 * s + 0.14 * L) * math.sin(math.pi * min(1.0, 0.12 + t * 0.95)) ** 0.7
                w *= 1.0 if j % 2 else 0.68          # serrated edge: needle sprays along the branch
                radii.append(max(0.02 * s, w))
            b.tube(pts, radii, greens[(i + k) % 3], n=10, cap=True, flat=0.3)
    # leader
    b.lathe([(0.32 * s, H - 1.6 * s), (0.2 * s, H - 1.0 * s), (0.0, H + 0.2 * s)], "spruce_b", n=10)
    return b.build()


def tree_cherry(size="large", name=None):
    """Flowering cherry in bloom: short dark trunk forking low into wide,
    upswept limbs; a flat-topped umbrella of blossom clusters held at the
    branch tips so the branching shows underneath."""
    s = 1.0 if size == "large" else 0.62
    b = EnvBuilder(name or "tree_cherry_%s" % size, smooth=ORGANIC_ANGLE)
    trunk = _bend_path((0, 0, 0), (0.1, 0, 1), 1.3 * s, 3, b.rng, wobble=0.05)
    b.tube(trunk, [0.26 * s, 0.22 * s, 0.2 * s, 0.19 * s], "bark_cherry", n=12, cap=False)
    b.lathe([(0.38 * s, 0.0), (0.27 * s, 0.14 * s), (0.22 * s, 0.32 * s), (0.0, 0.32 * s)], "bark_cherry", n=12)
    tips = []
    for k in range(5):
        a = 2 * math.pi * k / 5 + 0.3
        _grow(b, trunk[-1], (math.cos(a), math.sin(a), 0.85), 1.9 * s, 0.13 * s, 2, tips, "bark_cherry",
              spread=(0.5, 0.9), kids=(2, 2), up=0.15, child_len=0.62, n=9)
    cs = []
    for p, d in tips:
        cs.append((p + Vector((0, 0, 0.35 * s)), 0.95 * s * b.rng.uniform(0.85, 1.1)))
    for (c, r) in cs:
        k = b.rng.randrange(3)
        cols = ["cherry_a", "cherry_b", "cherry_c"]
        b.blob(c, (r * 0.88, r * 0.88, r * 0.55), cols[k], nu=30, nv=14, amp=0.22, detail=0.1, detail_freq=3.0)
        b.leaves(c, (r * 0.95, r * 0.95, r * 0.6), int(110 if s == 1 else 70), [cols[k]],
                 size=0.2 * s ** 0.5, aspect=0.85, cup=0.25, shell=(0.9, 1.03))
    return b.build()


def tree_japanese_maple(name=None):
    """Small yard tree: multi-stem red Japanese maple with layered flat
    clouds of fine leaves."""
    b = EnvBuilder(name or "tree_japanese_maple", smooth=ORGANIC_ANGLE)
    tips = []
    for k, (a, lean) in enumerate(((0.3, 0.55), (2.4, 0.6), (4.3, 0.5))):
        _grow(b, (0.05 * math.cos(a), 0.05 * math.sin(a), 0), (lean * math.cos(a), lean * math.sin(a), 1.0), 1.1,
              0.08, 2, tips, "bark_dark", spread=(0.55, 0.95), kids=(2, 2), up=0.05, child_len=0.75, n=8)
    for p, d in tips:
        r = 0.7 * b.rng.uniform(0.85, 1.15)
        c = p + Vector((0, 0, 0.1))
        k = b.rng.randrange(2)
        cols = ["jmaple_a", "jmaple_b", "jmaple_c"]
        b.blob(c, (r * 0.95, r * 0.95, r * 0.32), cols[k], nu=28, nv=10, amp=0.12, detail=0.06, detail_freq=3.5)
        b.leaves(c, (r, r, r * 0.34), 90, [cols[k], cols[k + 1], cols[k]], size=0.14, aspect=0.8)
    return b.build()


# ---------------------------------------------------------------------------
# 2. Shrubs, flowers and beds
# ---------------------------------------------------------------------------
def _shrub(b, x, y, r, keys=("shrub_a", "shrub_b"), squash=0.8, leaves=160, z0=0.0):
    c = (x, y, z0 + r * squash * 0.8)
    b.blob(c, (r, r, r * squash), keys[b.rng.randrange(len(keys))], nu=26, nv=13, amp=0.12, flat_bottom=0.8,
           detail=0.03)
    b.leaves(c, (r * 1.02, r * 1.02, r * squash * 1.02), leaves, list(keys) + ["leaf_b"], size=0.11 + 0.06 * r,
             flat_bottom=0.6, shell=(0.95, 1.06))


def _flower(b, base, h, color, size=0.075, petals=5, lean=None):
    rng = b.rng
    lean = lean or Vector((rng.gauss(0, 0.12), rng.gauss(0, 0.12), 1)).normalized()
    p0 = Vector(base)
    mid = p0 + Vector((0, 0, h * 0.5)) + Vector((lean.x, lean.y, 0)) * h * 0.25
    top = p0 + lean * h
    b.tube([p0, mid, top], [0.012, 0.01, 0.008], "hosta", n=4, cap=False)
    face = (lean + Vector((0, 0, 0.6))).normalized()
    ref = Vector((1, 0, 0)) if abs(face.x) < 0.9 else Vector((0, 1, 0))
    u = face.cross(ref).normalized()
    v = face.cross(u)
    ph = rng.uniform(0, 6.28)
    for k in range(petals):
        a = ph + 2 * math.pi * k / petals
        dvec = u * math.cos(a) + v * math.sin(a)
        svec = u * -math.sin(a) + v * math.cos(a)
        tip = top + dvec * size + face * size * 0.25
        mid1 = top + dvec * size * 0.55
        w = size * 0.42
        self_pts = [top + face * 0.005, mid1 - svec * w + face * 0.02, tip, mid1 + svec * w + face * 0.02,
                    mid1 + face * 0.035]
        b._faces(self_pts, [(0, 1, 4), (1, 2, 4), (2, 3, 4), (3, 0, 4)], color)
    b.blob(tuple(top + face * 0.012), size * 0.28, "fl_yellow" if color != "fl_yellow" else "fl_orange", nu=8, nv=5,
           amp=0.0)


def _flowers(b, x, y, r, color, n=7, z0=0.0):
    """Perennial clump: a leafy mound with individual flower heads on stems."""
    c = (x, y, z0 + r * 0.22)
    b.blob(c, (r, r, r * 0.42), "hosta", nu=16, nv=8, amp=0.15, flat_bottom=0.5)
    b.leaves(c, (r * 1.02, r * 1.02, r * 0.45), 40, ["hosta", "shrub_b"], size=0.1, flat_bottom=0.4)
    for k in range(n):
        a = b.rng.uniform(0, 6.28)
        d = r * math.sqrt(b.rng.uniform(0, 0.55))
        _flower(b, (x + d * math.cos(a), y + d * math.sin(a), z0 + r * 0.3), r * b.rng.uniform(0.45, 0.75), color,
                size=0.055 + 0.04 * r)


def _grass_clump(b, x, y, h, r, z0=0.0, blades=60):
    rng = b.rng
    for i in range(blades):
        t = rng.uniform(0, 2 * math.pi)
        out = rng.uniform(0.25, 1.0)
        hh = h * rng.uniform(0.7, 1.0)
        dirv = Vector((math.cos(t), math.sin(t), 0))
        side = Vector((-math.sin(t), math.cos(t), 0))
        base = Vector((x, y, z0)) + dirv * 0.06 * r * rng.uniform(0, 1)
        segs = 6
        w0 = 0.03
        left, right = [], []
        for j in range(segs + 1):
            f = j / segs
            p = base + dirv * (r * out * f * f) + Vector((0, 0, hh * math.sin(f * math.pi / 2) * (1 - 0.25 * out * f * f)))
            w = w0 * (1 - f) + 0.003
            left.append(p - side * w)
            right.append(p + side * w)
        verts = [v for pair in zip(left, right) for v in pair]
        faces = [(2 * j, 2 * j + 1, 2 * j + 3, 2 * j + 2) for j in range(segs)]
        b._faces(verts, faces, "grass_orn" if i % 3 else "grass_orn_b")
        if i % 5 == 0:
            tipv = left[-1] * 0.5 + right[-1] * 0.5
            b.blob(tuple(tipv + Vector((0, 0, 0.1))), (0.05, 0.05, 0.16), "plume", nu=8, nv=6, amp=0.1)


def shrub_round(size="large", name=None):
    r = 0.62 if size == "large" else 0.42
    b = EnvBuilder(name or "shrub_round_%s" % size, smooth=ORGANIC_ANGLE)
    _shrub(b, 0, 0, r, leaves=320 if size == "large" else 200)
    return b.build()


def hedge_boxwood(length=3.0, name=None):
    """Clipped boxwood hedge run along X (origin at the base centre): a
    soft rounded-rectangle section, fine clipped bumps, dense small leaves."""
    b = EnvBuilder(name or "hedge_boxwood", smooth=ORGANIC_ANGLE)
    W, H, rc = 0.7, 0.9, 0.16
    prof = []                       # rounded rectangle in (y, z), CCW, flat base
    for (cy, cz, a0) in ((W / 2 - rc, rc, -90), (W / 2 - rc, H - rc, 0), (-W / 2 + rc, H - rc, 90),
                         (-W / 2 + rc, rc, 180)):
        for k in range(5):
            a = math.radians(a0 + 90 * k / 4)
            prof.append((cy + rc * math.cos(a), cz + rc * math.sin(a)))
    nx = max(12, int(length / 0.08))
    rings = []
    endr = 0.25
    for i in range(nx + 1):
        x = -length / 2 + length * i / nx
        e = max(0.0, abs(x) - (length / 2 - endr)) / endr
        sc = math.sqrt(max(0.05, 1 - e * e))
        ring = []
        for (py, pz) in prof:
            q = Vector((x, py * sc, (pz - H / 2) * sc + H / 2))
            n_ = 0.018 * noise.noise(q * 7.0 + b.noff) + 0.008 * noise.noise(q * 19.0 + b.noff)
            ring.append(q + Vector((0, py, pz - H / 2)).normalized() * n_ if (py or pz != H / 2) else q)
        rings.append(ring)
    b.sweep(rings, "boxwood", start=(-length / 2 - 0.01, 0, H / 2), end=(length / 2 + 0.01, 0, H / 2))
    for _ in range(int(length * 260)):
        x = b.rng.uniform(-length / 2 + 0.1, length / 2 - 0.1)
        side = b.rng.random()
        if side < 0.4:
            p = Vector((x, b.rng.choice((-1, 1)) * (W / 2 + 0.01), b.rng.uniform(0.1, H - 0.1)))
            nrm = Vector((0, math.copysign(1, p.y), 0))
        else:
            p = Vector((x, b.rng.uniform(-W / 2 + 0.05, W / 2 - 0.05), H + 0.01))
            nrm = Vector((0, 0, 1))
        b.leaves(tuple(p), 0.001, 1, ["boxwood", "boxwood_b", "boxwood_b"], size=0.07, shell=(1, 1))
    return b.build()


def grass_ornamental(name=None):
    b = EnvBuilder(name or "grass_ornamental", smooth=ORGANIC_ANGLE)
    _grass_clump(b, 0, 0, 1.05, 0.55, blades=90)
    return b.build()


def _mulch_slab(b, poly, h):
    b.texture("mulch_tex", tinted(mulch_mask(), ENV["mulch"], "mulch"),
              lambda co: (co.x / MULCH_TILE_M, co.y / MULCH_TILE_M))
    b.rprism([(x, y, 0) for x, y in poly], h, "mulch_tex", bevel=0.025, segs=2)


def _edged_bed(b, poly, edge="edging", h=0.07, edge_w=0.11, edge_h=0.13):
    """Textured mulch over the polygon (CCW) with a border of bevelled
    stone blocks, or a rolled steel strip."""
    _mulch_slab(b, poly, h)
    n = len(poly)
    for i in range(n):
        (ax, ay), (bx, by) = poly[i], poly[(i + 1) % n]
        L = math.hypot(bx - ax, by - ay)
        if edge == "steel":
            b.rbeam((ax, ay, edge_h / 2), (bx, by, edge_h / 2), 0.025, edge_h, (0, 0, 1), "steel_edge", bevel=0.008)
            continue
        k = max(1, int(L / 0.3))
        for j in range(k):
            f0, f1 = j / k + 0.01, (j + 1) / k - 0.01
            hh = edge_h * b.rng.uniform(0.9, 1.08)
            p0 = (ax + (bx - ax) * f0, ay + (by - ay) * f0, hh / 2)
            p1 = (ax + (bx - ax) * f1, ay + (by - ay) * f1, hh / 2)
            b.rbeam(p0, p1, edge_w, hh, (0, 0, 1), "edging" if j % 3 else "edging_dark", bevel=0.022, segs=2)


def bed_foundation(length=6.0, depth=1.3, name=None):
    """Foundation bed along a house front. Origin = middle of its BACK edge;
    the bed extends toward -Y. Stone edge, textured mulch, round shrubs and
    boxwood balls with flowering perennials in front, a grass at each end."""
    b = EnvBuilder(name or "bed_foundation", smooth=ORGANIC_ANGLE)
    x0, x1 = -length / 2, length / 2
    _edged_bed(b, [(x0, -depth), (x1, -depth), (x1, 0), (x0, 0)])
    yb = -depth * 0.42
    n = max(2, int(round((length - 1.6) / 1.35)))
    colors = ["fl_red", "fl_yellow", "fl_purple", "fl_white", "fl_pink"]
    for i in range(n + 1):
        x = x0 + 0.8 + i * (length - 1.6) / n
        if i in (0, n):
            _grass_clump(b, x, yb, 0.85, 0.42, z0=0.06, blades=45)
        elif i % 2:
            _shrub(b, x, yb + 0.05, 0.42, z0=0.06, leaves=140)
        else:
            _shrub(b, x, yb + 0.05, 0.36, keys=("boxwood", "boxwood_b"), z0=0.06, leaves=120)
        if i < n:
            _flowers(b, x + (length - 1.6) / n / 2, -depth * 0.72, 0.28, colors[i % len(colors)], z0=0.06)
    return b.build()


# ---------------------------------------------------------------------------
# 3. Path and yard
# ---------------------------------------------------------------------------
def stepping_stones(count=6, spacing=0.75, name=None):
    """Irregular rounded flagstones on a gentle S-curve; origin at the first
    stone, running toward +Y."""
    b = EnvBuilder(name or "stepping_stones", smooth=ORGANIC_ANGLE)
    for i in range(count):
        x = 0.35 * math.sin(i * 0.9)
        r = b.rng.uniform(0.27, 0.33)
        b.blob((x, i * spacing, 0.0), (r, r * b.rng.uniform(0.75, 0.95), 0.06), "stone_a" if i % 2 else "stone_b",
               nu=28, nv=8, e=0.45, amp=0.14, freq=1.2, flat_bottom=0.2, detail=0.015)
    return b.build()


def walk_border(length=4.0, width=0.6, name=None):
    """Flower border along one side of a front walk. Origin at the end nearest
    the street; runs toward +Y, bed on +X of the origin line."""
    b = EnvBuilder(name or "walk_border", smooth=ORGANIC_ANGLE)
    _edged_bed(b, [(0, 0), (width, 0), (width, length), (0, length)], edge="steel", h=0.06)
    colors = ["fl_red", "fl_white", "fl_yellow", "fl_purple", "fl_pink", "fl_orange"]
    y = 0.35
    i = 0
    while y < length - 0.3:
        if i % 3 == 2:
            _shrub(b, width / 2, y, 0.22, keys=("boxwood", "boxwood_b"), z0=0.05, leaves=70)
        else:
            _flowers(b, width / 2, y, 0.22, colors[i % len(colors)], n=6, z0=0.05)
        y += 0.5
        i += 1
    return b.build()


def mailbox(name=None):
    """Mailbox on a white post: rounded-top black box (door faces -Y, the
    street), red flag on a pivot, bevelled post and arm."""
    s = PROP_SCALE
    b = EnvBuilder(name or "mailbox")
    b.rbox((-0.055 * s, -0.055 * s, 0), (0.055 * s, 0.055 * s, 1.0 * s), "post_white", bevel=0.012)
    b.lathe([(0.065 * s, 1.0 * s), (0.065 * s, 1.02 * s), (0.0, 1.035 * s)], "post_white", n=4, rot=45)
    b.rbox((-0.12 * s, -0.2 * s, 0.96 * s), (0.12 * s, 0.22 * s, 1.01 * s), "post_white", bevel=0.01)
    hw, hb, z0 = 0.11 * s, 0.11 * s, 1.01 * s
    prof = [(-hw + 0.01, z0), (hw - 0.01, z0), (hw, z0 + 0.01)]
    prof += [(hw * math.cos(math.pi * k / 14), z0 + hb + hw * math.sin(math.pi * k / 14)) for k in range(15)]
    prof += [(-hw, z0 + 0.01)]
    y0, y1 = -0.27 * s, 0.24 * s
    rings = []
    for y, sc in ((y0, 0.93), (y0 + 0.008, 1.0), (y1 - 0.008, 1.0), (y1, 0.95)):
        rings.append([(x * sc, y, z0 + (z - z0) * sc + (1 - sc) * 0.02) for x, z in prof])
    b.sweep(rings, "mailbox", cap_start=True, cap_end=True)
    b.rbox((-0.02 * s, y0 - 0.012, z0 + hb + 0.03), (0.02 * s, y0, z0 + hb + 0.06), "metal", bevel=0.004)  # latch
    piv = (hw + 0.012, -0.02 * s, z0 + 0.1 * s)
    b.tube([piv, (piv[0] + 0.02, piv[1], piv[2])], [0.015, 0.015], "metal", n=10)
    b.rbeam((hw + 0.02, -0.02 * s, z0 + 0.08 * s), (hw + 0.02, -0.02 * s, z0 + 0.3 * s), 0.012, 0.03, (0, 1, 0), "flag",
            bevel=0.004)
    b.rbeam((hw + 0.02, -0.04 * s, z0 + 0.255 * s), (hw + 0.02, 0.12 * s, z0 + 0.255 * s), 0.012, 0.085, (0, 0, 1),
            "flag", bevel=0.004)
    return b.build()


def bed_corner(arm=2.6, width=0.9, name=None):
    """L-shaped garden bed for a yard corner (stone edge), origin at the
    outer corner; arms run +X and +Y."""
    b = EnvBuilder(name or "bed_corner", smooth=ORGANIC_ANGLE)
    poly = [(0, 0), (arm, 0), (arm, width), (width, width), (width, arm), (0, arm)]
    _edged_bed(b, poly)
    _shrub(b, width * 0.5, width * 0.5, 0.45, z0=0.06, leaves=160)
    _grass_clump(b, arm - 0.45, width * 0.5, 0.8, 0.38, z0=0.06, blades=45)
    _grass_clump(b, width * 0.5, arm - 0.45, 0.8, 0.38, z0=0.06, blades=45)
    for i, (x, y) in enumerate(((1.35, 0.45), (0.45, 1.35), (1.9, 0.5))):
        _flowers(b, x, y, 0.26, ["fl_purple", "fl_yellow", "fl_red"][i], z0=0.06)
    return b.build()


def planter(name=None):
    """Terracotta pot with a rolled rim, a round shrub and trailing flowers."""
    s = PROP_SCALE
    b = EnvBuilder(name or "planter", smooth=ORGANIC_ANGLE)
    b.lathe([(0.0, 0.0), (0.2 * s, 0.0), (0.22 * s, 0.02 * s), (0.3 * s, 0.44 * s), (0.31 * s, 0.45 * s),
             (0.35 * s, 0.46 * s), (0.36 * s, 0.5 * s), (0.35 * s, 0.54 * s), (0.31 * s, 0.55 * s),
             (0.29 * s, 0.52 * s), (0.0, 0.52 * s)], "terracotta", n=32)
    b.lathe([(0.0, 0.5 * s), (0.29 * s, 0.5 * s), (0.29 * s, 0.52 * s), (0.0, 0.53 * s)], "soil", n=32)
    _shrub(b, 0, 0, 0.3 * s, z0=0.5 * s, leaves=200)
    for i in range(8):
        t = 2 * math.pi * i / 8 + 0.2
        _flower(b, (0.27 * s * math.cos(t), 0.27 * s * math.sin(t), 0.53 * s), 0.08,
                "fl_pink" if i % 2 else "fl_white", size=0.07,
                lean=Vector((math.cos(t), math.sin(t), 0.4)).normalized())
    return b.build()


# ---------------------------------------------------------------------------
# 4. Street
# ---------------------------------------------------------------------------
VERGE_W = 0.75       # tree lawn between curb and sidewalk (road half 3 + verge 0.75 + sidewalk 2)
CURB_H = 0.15


def tree_lawn(length=6.0, name=None, with_tree=True):
    """Street tree in its tree lawn: bevelled curb + textured grass strip
    (0.75 m, today's verge) with a mulch ring and a street tree. Origin =
    curb line at the segment's middle; grass toward +Y."""
    b = EnvBuilder(name or "tree_lawn", smooth=ORGANIC_ANGLE)
    b.rbox((-length / 2, -0.15, -0.05), (length / 2, 0.0, CURB_H), "curb", bevel=0.03, segs=3)
    b.texture("lawn_tex", tinted(lawn_mask(), ENV["lawn"], "lawn"),
              lambda co: (co.x / LAWN_TILE_M, co.y / LAWN_TILE_M))
    b.box((-length / 2, 0.0, -0.05), (length / 2, VERGE_W, 0.04), "lawn_tex")
    b.texture("mulch_tex", tinted(mulch_mask(), ENV["mulch"], "mulch"),
              lambda co: (co.x / MULCH_TILE_M, co.y / MULCH_TILE_M))
    b.disc((0, VERGE_W / 2), 0.34, 0.04, 0.075, "mulch_tex", n=28, irregular=0.06)
    ob = b.build()
    if with_tree:
        t = tree_street()
        t.location = (0, VERGE_W / 2, 0.06)
        t.parent = ob
    return ob


def lamp_post(name=None):
    """Cast-iron lamp post: turned base, fluted pole, collar, glass
    lantern with a frame, cap and finial."""
    s = PROP_SCALE
    b = EnvBuilder(name or "lamp_post")
    base = [(0.0, 0.0), (0.2, 0.0), (0.2, 0.05), (0.18, 0.07), (0.17, 0.12), (0.14, 0.15), (0.15, 0.2),
            (0.12, 0.34), (0.1, 0.45), (0.12, 0.48), (0.12, 0.52), (0.085, 0.55), (0.08, 0.58)]
    b.lathe([(r * s, z * s) for r, z in base], "lamp_black", n=32)
    b.lathe([(0.072 * s, 0.58 * s), (0.06 * s, 3.3 * s)], "lamp_black", n=48, flutes=12, flute_depth=0.18)
    collar = [(0.075, 3.3), (0.1, 3.33), (0.1, 3.38), (0.07, 3.42), (0.12, 3.47), (0.15, 3.52), (0.15, 3.55)]
    b.lathe([(r * s, z * s) for r, z in collar], "lamp_black", n=32)
    # lantern: six-sided tapered glass between a base ring and a cap
    zg0, zg1 = 3.55 * s, 4.0 * s
    b.lathe([(0.0, zg0 + 0.01), (0.12 * s, zg0 + 0.01), (0.19 * s, zg1 - 0.01), (0.0, zg1 - 0.01)], "lamp_glass", n=6,
            rot=30)
    for k in range(6):
        a = math.radians(30 + 60 * k)
        b.tube([(0.125 * s * math.cos(a), 0.125 * s * math.sin(a), zg0), (0.2 * s * math.cos(a), 0.2 * s * math.sin(a), zg1)],
               [0.012 * s, 0.012 * s], "lamp_black", n=6)
    b.lathe([(0.0, zg0 - 0.03), (0.15 * s, zg0 - 0.03), (0.15 * s, zg0 + 0.02), (0.0, zg0 + 0.02)], "lamp_black", n=6,
            rot=30)
    cap = [(0.0, zg1 - 0.02), (0.25 * s, zg1 - 0.02), (0.26 * s, zg1 + 0.02), (0.17 * s, zg1 + 0.14 * s),
           (0.07 * s, zg1 + 0.24 * s), (0.05 * s, zg1 + 0.27 * s), (0.0, zg1 + 0.27 * s)]
    b.lathe(cap, "lamp_black", n=6, rot=30)
    b.blob((0, 0, zg1 + 0.33 * s), 0.05 * s, "lamp_black", nu=12, nv=8, amp=0)
    b.lathe([(0.02 * s, zg1 + 0.26 * s), (0.02 * s, zg1 + 0.3 * s)], "lamp_black", n=8)
    return b.build()


def _post(b, h, r=0.035):
    s = PROP_SCALE
    b.lathe([(0.0, 0.0), (r * 1.6 * s, 0.0), (r * 1.6 * s, 0.03), (r * s, 0.05), (r * s, h - 0.02),
             (r * 0.7 * s, h), (0.0, h + 0.01)], "pole_grey", n=16)


def stop_sign(name=None):
    """Stop sign: round galvanised post, bracket, bevelled red octagon with
    a white border and STOP, facing -Y."""
    s = PROP_SCALE
    b = EnvBuilder(name or "stop_sign")
    _post(b, 2.3 * s)
    R = 0.38 * s / math.cos(math.radians(22.5))
    zc = 2.1 * s
    oct_ = [(R * math.cos(math.radians(22.5 + 45 * i)), zc + R * math.sin(math.radians(22.5 + 45 * i)))
            for i in range(8)]
    T = 0.02 * s
    b.rprism([(x, -0.05 * s, z) for x, z in oct_], 0, "stop_red", bevel=0.008, segs=2, axis_off=(0, T, 0))
    for zz in (zc - 0.25 * s, zc + 0.25 * s):              # bracket bands
        b.rbox((-0.06 * s, -0.03 * s, zz - 0.02 * s), (0.06 * s, 0.045 * s, zz + 0.02 * s), "pole_grey", bevel=0.006)
    face = [(x * 0.985, -0.0515 * s, zc + (z - zc) * 0.985) for x, z in reversed(oct_)]
    tex = sign_texture("stop", "STOP", ENV["stop_red"], "#FFFFFF", size=(512, 512), border="#FFFFFF", octagon=True)
    b.texture("stop_face", tex, lambda co: (0.5 + co.x / (2 * R), 0.5 + (co.z - zc) / (2 * R)))
    b._faces(face, [tuple(range(8))], "stop_face")
    return b.build()


def street_sign(a="MAPLE ST", c="OAK AVE", name=None):
    """Street-name sign: round post with a cap, two bevelled green blades
    crossed in a cast bracket, white text on both faces of each blade."""
    s = PROP_SCALE
    b = EnvBuilder(name or "street_sign")
    _post(b, 2.85 * s)
    L, H, T = 0.95 * s, 0.2 * s, 0.022 * s
    for i, (txt, z, axis) in enumerate(((a, 2.5 * s, "x"), (c, 2.76 * s, "y"))):
        tex = sign_texture("blade_%d" % i, txt, ENV["sign_green"], "#FFFFFF", size=(768, 162), border="#FFFFFF")
        if axis == "x":
            b.rbox((-L / 2, -T / 2, z - H / 2), (L / 2, T / 2, z + H / 2), "sign_green", bevel=0.007)
        else:
            b.rbox((-T / 2, -L / 2, z - H / 2), (T / 2, L / 2, z + H / 2), "sign_green", bevel=0.007)
        b.lathe([(0.0, z - H / 2 - 0.03), (0.055 * s, z - H / 2 - 0.03), (0.06 * s, z - H / 2), (0.06 * s, z + H / 2),
                 (0.055 * s, z + H / 2 + 0.03), (0.0, z + H / 2 + 0.03)], "pole_grey", n=16)   # bracket clamp
        for sgn in (-1, 1):
            key = "blade_%d_%s" % (i, "a" if sgn < 0 else "b")
            o = sgn * (T / 2 + 0.0025)
            l2, h2 = L / 2 - 0.012, H / 2 - 0.012
            if axis == "x":
                q = [(-l2, o, z - h2), (l2, o, z - h2), (l2, o, z + h2), (-l2, o, z + h2)]
                fn = (lambda co, z=z, g=sgn, l2=l2, h2=h2: (0.5 - g * co.x / (2 * l2), 0.5 + (co.z - z) / (2 * h2)))
            else:
                q = [(o, l2, z - h2), (o, -l2, z - h2), (o, -l2, z + h2), (o, l2, z + h2)]
                fn = (lambda co, z=z, g=sgn, l2=l2, h2=h2: (0.5 + g * co.y / (2 * l2), 0.5 + (co.z - z) / (2 * h2)))
            b._faces(q, [(0, 1, 2, 3) if (sgn < 0) == (axis == "x") else (3, 2, 1, 0)], key)
            b.texture(key, tex, fn)
    return b.build()


def hydrant(name=None):
    """Fire hydrant: flanged base with bolts, barrel, bonnet with a
    pentagon operating nut, two hose nozzles and a big pumper nozzle (faces
    -Y) with hex caps."""
    s = PROP_SCALE
    b = EnvBuilder(name or "hydrant")
    body = [(0.0, 0.0), (0.18, 0.0), (0.18, 0.05), (0.15, 0.07), (0.15, 0.09), (0.125, 0.11), (0.12, 0.5),
            (0.115, 0.55), (0.16, 0.57), (0.16, 0.62), (0.13, 0.64), (0.12, 0.7), (0.09, 0.75), (0.05, 0.775),
            (0.0, 0.78)]
    b.lathe([(r * s, z * s) for r, z in body], "hydrant", n=32)
    b.lathe([(0.0, 0.77 * s), (0.04 * s, 0.77 * s), (0.04 * s, 0.84 * s), (0.0, 0.85 * s)], "hydrant_cap", n=5)
    for k in range(8):                                                       # flange bolts
        a = 2 * math.pi * k / 8 + 0.2
        for zb, rb in ((0.06, 0.165), (0.595, 0.145)):
            b.lathe([(0.0, zb * s), (0.016 * s, zb * s), (0.016 * s, (zb + 0.03) * s), (0.0, (zb + 0.035) * s)],
                    "hydrant_cap", n=6, c=(rb * s * math.cos(a), rb * s * math.sin(a)))
    for (dx, dy, r, L) in ((1, 0, 0.045, 0.2), (-1, 0, 0.045, 0.2), (0, -1, 0.07, 0.19)):
        z = 0.42 * s
        d = Vector((dx, dy, 0))
        p0 = Vector((0, 0, z))
        b.tube([p0, p0 + d * L * 0.85 * s], [r * 1.15 * s, r * s], "hydrant", n=20, cap=False)
        b.tube([p0 + d * L * 0.8 * s, p0 + d * L * s], [r * 1.25 * s, r * 1.25 * s], "hydrant_cap", n=6, cap=True)
        b.tube([p0 + d * L * s, p0 + d * (L + 0.03) * s], [r * 0.45 * s, r * 0.4 * s], "hydrant_cap", n=5, cap=True)
    return b.build()


def bench(name=None):
    """Park bench: bevelled wooden slats on curved cast-iron ends with
    scroll arms; faces -Y."""
    s = PROP_SCALE
    b = EnvBuilder(name or "bench")
    L = 1.6 * s
    for x in (-L / 2 + 0.14 * s, L / 2 - 0.14 * s):
        X = lambda y, z: Vector((x, y * s, z * s))  # noqa: E731
        b.tube([X(-0.24, 0.0), X(-0.23, 0.2), X(-0.22, 0.44)], [0.03 * s, 0.026 * s, 0.026 * s], "bench_iron", n=10)
        b.tube([X(0.2, 0.0), X(0.17, 0.25), X(0.16, 0.44), X(0.2, 0.62), X(0.25, 0.8), X(0.28, 0.92)],
               [0.03 * s, 0.027 * s, 0.026 * s, 0.024 * s, 0.022 * s, 0.02 * s], "bench_iron", n=10)
        b.tube([X(-0.27, 0.43), X(0.0, 0.42), X(0.2, 0.43)], [0.022 * s] * 3, "bench_iron", n=10, cap=True)
        arm = [X(0.18, 0.62), X(0.0, 0.66), X(-0.18, 0.66), X(-0.27, 0.62), X(-0.29, 0.55), X(-0.25, 0.5),
               X(-0.21, 0.53)]
        b.tube(arm, [0.022 * s] * len(arm), "bench_iron", n=10)
        b.tube([X(-0.23, 0.44), X(-0.25, 0.53)], [0.02 * s, 0.02 * s], "bench_iron", n=8)
        for fy in (-0.24, 0.2):
            b.lathe([(0.0, 0.0), (0.045 * s, 0.0), (0.04 * s, 0.02 * s), (0.0, 0.03 * s)], "bench_iron", n=12,
                    c=(x, fy * s))
    for i in range(5):                                              # seat slats
        y = -0.26 * s + i * 0.1 * s
        b.rbox((-L / 2, y, 0.45 * s), (L / 2, y + 0.082 * s, 0.485 * s), "bench_wood", bevel=0.008)
    for i in range(3):                                              # back slats
        z = 0.58 * s + i * 0.11 * s
        y = 0.2 * s + (z - 0.42 * s) * 0.42
        b.rbeam((-L / 2, y, z + 0.04 * s), (L / 2, y, z + 0.04 * s), 0.035 * s, 0.085 * s,
                (0, -0.42, 1), "bench_wood", bevel=0.008)
    return b.build()


# ---------------------------------------------------------------------------
# 5. Lawn
# ---------------------------------------------------------------------------
def lawn_patch(x0, x1, y0, y1, name="lawn", z=-0.01):
    """Ground slab with the tiled lawn texture (UVs in metres / LAWN_TILE_M)."""
    b = EnvBuilder(name)
    b.texture("lawn_tex", tinted(lawn_mask(), ENV["lawn"], "lawn"),
              lambda co: (co.x / LAWN_TILE_M, co.y / LAWN_TILE_M))
    b.box((x0, y0, z - 0.05), (x1, y1, z), "lawn_tex")
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
