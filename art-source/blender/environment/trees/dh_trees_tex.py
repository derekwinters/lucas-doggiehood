"""Textured game trees (round 3): alpha-cut leaf cards on a light branch
skeleton, standard game-tree technique.

- Leaf / blossom / needle atlases and tileable bark come from dh_tree_tex.
- Canopy: V-folded cards (4 tris) carrying one sprig cell each, placed in
  leaf clusters along the branch tips, oriented outward, sprig tips pointing
  out so the crown edge is ragged and lets light through.
- Normals: every card vertex gets a custom normal bent toward the crown
  volume (70 % whole-crown ellipsoid + 30 % its own cluster, then 15 % of
  the card's own normal), so the canopy shades as one soft mass instead of
  hundreds of noisy planes. Card winding is set so the geometric front
  faces outward, matching the bent normals.
- Materials: alpha clip (Math: Round on the texture alpha -> glTF
  alphaMode MASK, cutoff 0.5), double-sided; bark has a normal map.

Units / axes as the rest of the kit: metres, Z up, origin at the trunk base.
"""
import math
import os
import random
import sys
import zlib

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
KIT = os.path.normpath(os.path.join(HERE, "..", ".."))   # art-source/blender: the committed house kit
sys.path.insert(0, KIT)
import bpy  # noqa: E402
from mathutils import Vector  # noqa: E402
import dh_house_kit as K  # noqa: E402
import dh_env as E  # noqa: E402  (reuses its branch-growth helpers)
import dh_tree_tex as T  # noqa: E402

BARK_TILE_W, BARK_TILE_H = 0.6, 1.2     # metres covered by one bark texture tile
_TEX = None


def textures():
    global _TEX
    if _TEX is None:
        need = [os.path.join(T.OUT, f) for f in ("maple_leaf_atlas.png", "cherry_blossom_atlas.png",
                                                  "spruce_needle_atlas.png", "bark_color.png")]
        _TEX = T.build_all() if not all(os.path.exists(p) for p in need) else {
            **{k: os.path.join(T.OUT, "%s_leaf_atlas.png" % k) for k in T.ATLASES},
            "cherry": os.path.join(T.OUT, "cherry_blossom_atlas.png"),
            "spruce": os.path.join(T.OUT, "spruce_needle_atlas.png"),
            **{b: (os.path.join(T.OUT, b + "_color.png"), os.path.join(T.OUT, b + "_normal.png"))
               for b in ("bark", "bark_cherry", "bark_spruce")}}
    return _TEX


INNER_SHADE = 0.6       # interior cards: base colour x this (cheap self-shadowing / volume)


def leaf_material(name, path, factor=1.0):
    mat = bpy.data.materials.get(name)
    if mat:
        return mat
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    nt = mat.node_tree
    bsdf = nt.nodes["Principled BSDF"]
    tex = nt.nodes.new("ShaderNodeTexImage")
    tex.image = bpy.data.images.load(path, check_existing=True)
    tex.image.alpha_mode = "CHANNEL_PACKED"
    rnd = nt.nodes.new("ShaderNodeMath")
    rnd.operation = "ROUND"                       # -> glTF alphaMode MASK (cutoff 0.5)
    if factor < 1.0:                              # -> glTF baseColorFactor
        mix = nt.nodes.new("ShaderNodeMix")
        mix.data_type = "RGBA"
        mix.blend_type = "MULTIPLY"
        mix.inputs["Factor"].default_value = 1.0
        mix.inputs[7].default_value = (factor, factor, factor * 0.95, 1.0)
        nt.links.new(tex.outputs["Color"], mix.inputs[6])
        nt.links.new(mix.outputs[2], bsdf.inputs["Base Color"])
    else:
        nt.links.new(tex.outputs["Color"], bsdf.inputs["Base Color"])
    nt.links.new(tex.outputs["Alpha"], rnd.inputs[0])
    nt.links.new(rnd.outputs[0], bsdf.inputs["Alpha"])
    bsdf.inputs["Roughness"].default_value = 0.65
    if "Specular IOR Level" in bsdf.inputs:
        bsdf.inputs["Specular IOR Level"].default_value = 0.1
    mat.use_backface_culling = False              # -> glTF doubleSided
    for attr, val in (("surface_render_method", "DITHERED"), ("use_transparent_shadow", True),
                      ("blend_method", "CLIP")):
        try:
            setattr(mat, attr, val)
        except (AttributeError, TypeError):
            pass
    return mat


def bark_material(name, paths):
    mat = bpy.data.materials.get(name)
    if mat:
        return mat
    col, nrm = paths
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    nt = mat.node_tree
    bsdf = nt.nodes["Principled BSDF"]
    tc = nt.nodes.new("ShaderNodeTexImage")
    tc.image = bpy.data.images.load(col, check_existing=True)
    tn = nt.nodes.new("ShaderNodeTexImage")
    tn.image = bpy.data.images.load(nrm, check_existing=True)
    tn.image.colorspace_settings.name = "Non-Color"
    nm = nt.nodes.new("ShaderNodeNormalMap")
    nm.inputs["Strength"].default_value = 1.2
    nt.links.new(tc.outputs["Color"], bsdf.inputs["Base Color"])
    nt.links.new(tn.outputs["Color"], nm.inputs["Color"])
    nt.links.new(nm.outputs["Normal"], bsdf.inputs["Normal"])
    bsdf.inputs["Roughness"].default_value = 0.9
    mat.use_backface_culling = True               # bark is closed-ish: single-sided in glTF
    return mat


class TreeBuilder:
    """Accumulates faces with explicit per-loop UVs and optional per-loop
    custom normals; duck-types `tube` + `rng` for dh_env._grow."""

    def __init__(self, name):
        self.name = name
        self.rng = random.Random(zlib.crc32(name.encode()) & 0xFFFFFF)
        self.verts, self.faces, self.fmat, self.fuv, self.fnrm = [], [], [], [], []
        self.mats = []
        self.mat_bark = "bark"

    def _m(self, key):
        if key not in self.mats:
            self.mats.append(key)
        return self.mats.index(key)

    def face(self, vs, uvs, mat, nrms=None):
        base = len(self.verts)
        self.verts.extend(tuple(v) for v in vs)
        self.faces.append(tuple(range(base, base + len(vs))))
        self.fmat.append(self._m(mat))
        self.fuv.append(uvs)
        self.fnrm.append(nrms)

    # --- bark ---------------------------------------------------------------
    def tube(self, pts, radii, mat, n=8, cap=True, flat=1.0):
        pts = [Vector(p) for p in pts]
        n = max(5, min(n, 10))
        t0 = (pts[1] - pts[0]).normalized()
        ref = Vector((0, 0, 1)) if abs(t0.z) < 0.9 else Vector((1, 0, 0))
        u = t0.cross(ref).normalized()
        rings, vcoord, acc = [], [], 0.0
        for k, p in enumerate(pts):
            t = (pts[min(k + 1, len(pts) - 1)] - pts[max(k - 1, 0)]).normalized()
            u = (u - t * u.dot(t)).normalized()
            v = t.cross(u)
            if k:
                acc += (pts[k] - pts[k - 1]).length
            vcoord.append(acc / BARK_TILE_H)
            rings.append([p + (u * math.cos(2 * math.pi * i / n) + v * math.sin(2 * math.pi * i / n)) * radii[k]
                          for i in range(n)])
        reps = max(1, round(2 * math.pi * max(radii) / BARK_TILE_W))
        mat = {"bark": self.mat_bark}.get(mat, mat)
        for k in range(len(rings) - 1):
            for i in range(n):
                j = (i + 1) % n
                u0, u1 = reps * i / n, reps * (i + 1) / n
                self.face([rings[k][i], rings[k][j], rings[k + 1][j], rings[k + 1][i]],
                          [(u0, vcoord[k]), (u1, vcoord[k]), (u1, vcoord[k + 1]), (u0, vcoord[k + 1])], mat)
        if cap:
            tip = pts[-1] + (pts[-1] - pts[-2]).normalized() * radii[-1]
            for i in range(n):
                j = (i + 1) % n
                self.face([rings[-1][i], rings[-1][j], tip], [(i / n, vcoord[-1]), ((i + 1) / n, vcoord[-1]),
                                                               ((i + 0.5) / n, vcoord[-1] + 0.1)], mat)

    # --- leaf cards -----------------------------------------------------------
    def card(self, base, vdir, wdir, width, length, rect, mat, normal_fn, cup=0.18, droop=0.0):
        """V-folded card: 3 columns x 2 rows (4 tris). base = sprig base
        (bottom centre), vdir = growth direction, wdir = outward face normal.
        rect = (u0, v0, u1, v1) atlas cell; sprig base at v0."""
        vdir, wdir = Vector(vdir).normalized(), Vector(wdir).normalized()
        sdir = vdir.cross(wdir).normalized()
        u0, v0, u1, v1 = rect
        grid = []
        for r, f in enumerate((0.0, 1.0)):
            row = []
            for c, g in enumerate((-0.5, 0.0, 0.5)):
                p = Vector(base) + vdir * (length * f) + sdir * (width * g) + wdir * (
                    (cup * width if c == 1 else 0.0) * (0.4 + 0.6 * f) - droop * length * f * f)
                row.append((p, (u0 + (u1 - u0) * (g + 0.5), v0 + (v1 - v0) * f)))
            grid.append(row)
        for c in range(2):
            q = [grid[0][c], grid[0][c + 1], grid[1][c + 1], grid[1][c]]
            vs = [p for p, _ in q]
            fn = (vs[1] - vs[0]).cross(vs[3] - vs[0])
            if fn.dot(wdir) < 0:                         # front face outward
                q = list(reversed(q))
                vs = [p for p, _ in q]
            nrms = [(normal_fn(p) * 0.85 + wdir * 0.15).normalized() for p in vs]
            self.face(vs, [uv for _, uv in q], mat, nrms)

    def build(self, materials):
        me = bpy.data.meshes.new(self.name)
        me.from_pydata(self.verts, [], self.faces)
        uv = me.uv_layers.new(name="UVMap")
        li = 0
        loops_n = []
        for fi, poly in enumerate(me.polygons):
            poly.material_index = self.fmat[fi]
            poly.use_smooth = True
            for k in range(poly.loop_total):
                uv.data[poly.loop_start + k].uv = self.fuv[fi][k]
                nr = self.fnrm[fi]
                loops_n.append(tuple(nr[k]) if nr else (0.0, 0.0, 0.0))
            li += poly.loop_total
        for key in self.mats:
            me.materials.append(materials[key])
        me.update()
        me.normals_split_custom_set(loops_n)
        ob = bpy.data.objects.new(self.name, me)
        bpy.context.scene.collection.objects.link(ob)
        return ob


def _cell(i):
    col, row = i % 2, i // 2
    return (col * 0.5 + 0.004, 1 - (row + 1) * 0.5 + 0.004, (col + 1) * 0.5 - 0.004, 1 - row * 0.5 - 0.004)


def _ellipsoid_normal(c, r):
    c = Vector(c)

    def fn(p):
        d = Vector(p) - c
        n = Vector((d.x / r[0] ** 2, d.y / r[1] ** 2, d.z / r[2] ** 2))
        return n.normalized() if n.length > 1e-6 else Vector((0, 0, 1))
    return fn


def _crown_box(clusters):
    xs = [c.x for c, r in clusters]
    ys = [c.y for c, r in clusters]
    zs = [c.z for c, r in clusters]
    rm = max(r for _, r in clusters)
    c = Vector(((min(xs) + max(xs)) / 2, (min(ys) + max(ys)) / 2, (min(zs) + max(zs)) / 2))
    rad = ((max(xs) - min(xs)) / 2 + rm, (max(ys) - min(ys)) / 2 + rm, (max(zs) - min(zs)) / 2 + rm * 0.8)
    return c, rad


def _canopy(b, clusters, mat, card_len, density=26.0, squash=0.8, out_bias=0.2, up_w=0.0, cup=0.3,
            crown_w=0.7, droop=0.06, inner_f=0.7):
    """Fill each (centre, radius) cluster with cards. density = cards per m^2
    of cluster cross-section."""
    rng = b.rng
    cc, cr = _crown_box(clusters)
    crown_n = _ellipsoid_normal(cc, cr)
    for (c, r) in clusters:
        rr = (r, r, r * squash)
        clus_n = _ellipsoid_normal(c, rr)
        nfn = (lambda p, cn=clus_n: (crown_n(p) * crown_w + cn(p) * (1 - crown_w)).normalized())
        count = max(6, int(density * math.pi * r * r))
        for _ in range(count):
            while True:
                d = Vector((rng.gauss(0, 1), rng.gauss(0, 1), rng.gauss(0, 1)))
                if d.length > 1e-3:
                    d.normalize()
                    break
            f = 0.45 + 0.55 * rng.random() ** 0.6
            p = Vector(c) + Vector((d.x * rr[0], d.y * rr[1], d.z * rr[2])) * f
            n_out = clus_n(p)
            if up_w:
                n_out = (n_out + Vector((0, 0, up_w))).normalized()
            a = Vector((rng.gauss(0, 1), rng.gauss(0, 1), rng.gauss(0, 1)))
            t = (a - n_out * a.dot(n_out)).normalized()
            v = (t + n_out * out_bias).normalized()
            w = (n_out - v * n_out.dot(v)).normalized()
            L = card_len * rng.uniform(0.8, 1.15)
            base = p - v * L * 0.45
            b.card(base, v, w, L, L, _cell(rng.randrange(4)), mat + ("_inner" if f < inner_f else ""), nfn, cup=cup,
                   droop=droop)


def _canopy_crown(b, c, r, mat, card_len, count, lump=0.16, out_bias=0.2, up_w=0.3, inner_f=0.72, cup=0.34,
                  droop=0.06):
    """One whole-crown ellipsoid (centre c, radii r) with a lumpy, noise-
    modulated surface; cards biased to the outer shell, normals bent to the
    crown ellipsoid. Gives a round crown with a ragged leafy edge."""
    from mathutils import noise
    rng = b.rng
    c = Vector(c)
    crown_n = _ellipsoid_normal(c, r)
    off = Vector((rng.uniform(0, 40), rng.uniform(0, 40), rng.uniform(0, 40)))
    for _ in range(count):
        while True:
            d = Vector((rng.gauss(0, 1), rng.gauss(0, 1), rng.gauss(0, 1)))
            if d.length > 1e-3:
                d.normalize()
                break
        if d.z < -0.55:                       # flatter underside
            d.z = -0.55
            d.normalize()
        k = 1.0 + lump * noise.noise(d * 2.2 + off)
        f = (0.5 + 0.5 * rng.random() ** 0.5) * k
        p = c + Vector((d.x * r[0], d.y * r[1], d.z * r[2])) * f
        n_out = (crown_n(p) + Vector((0, 0, up_w))).normalized()
        a = Vector((rng.gauss(0, 1), rng.gauss(0, 1), rng.gauss(0, 1)))
        t = (a - n_out * a.dot(n_out)).normalized()
        v = (t + n_out * out_bias).normalized()
        w = (n_out - v * n_out.dot(v)).normalized()
        L = card_len * rng.uniform(0.8, 1.15)
        b.card(p - v * L * 0.45, v, w, L, L, _cell(rng.randrange(4)), mat + ("_inner" if f < inner_f else ""),
               crown_n, cup=cup, droop=droop)


def _materials():
    tx = textures()
    m = {}
    for k in ("maple", "street", "column", "jmaple", "cherry", "spruce"):
        m[k] = leaf_material("DH_Tree_%s_leaves" % k, tx[k])
        m[k + "_inner"] = leaf_material("DH_Tree_%s_leaves_inner" % k, tx[k], INNER_SHADE)
    for bk in ("bark", "bark_cherry", "bark_spruce"):
        m[bk] = bark_material("DH_Tree_%s" % bk, tx[bk])
    m["bark_dark"] = m["bark_spruce"]
    return m


# ---------------------------------------------------------------------------
# species
# ---------------------------------------------------------------------------
def _trunk(b, h, r0, r1, n=10, flare=1.45, wobble=0.03, lean=(0, 0, 1)):
    pts = E._bend_path((0, 0, 0), lean, h, 5, b.rng, wobble=wobble)
    radii = [r0 * flare, r0 * 1.05] + [r0 + (r1 - r0) * k / 4 for k in range(1, 5)]
    b.tube(pts, radii, b.mat_bark, n=n, cap=False)
    return pts


def tree_maple(size="large", name=None):
    s = 1.0 if size == "large" else 0.62
    b = TreeBuilder(name or "tree_maple_%s" % size)
    trunk = _trunk(b, 3.0 * s, 0.32 * s, 0.21 * s)
    tips = []
    for k in range(4):
        a = 2 * math.pi * k / 4 + 0.4
        E._grow(b, trunk[-1], (0.55 * math.cos(a), 0.55 * math.sin(a), 1.0), 2.0 * s, 0.18 * s, 2, tips, "bark",
                spread=(0.35, 0.7), kids=(2, 2), up=0.35, child_len=0.62, n=8)
    top = trunk[-1].z + 2.4 * s
    _canopy_crown(b, (0, 0, top), (3.2 * s, 3.2 * s, 2.5 * s), "maple", 1.3 * s ** 0.8, int(760 if s == 1 else 440), lump=0.3)
    return b.build(_materials())


def tree_street(name=None):
    b = TreeBuilder(name or "tree_street")
    trunk = _trunk(b, 3.2, 0.22, 0.15)
    tips = []
    for k in range(3):
        a = 2 * math.pi * k / 3 + 0.9
        E._grow(b, trunk[-1], (0.5 * math.cos(a), 0.5 * math.sin(a), 1.0), 1.4, 0.12, 2, tips, "bark",
                spread=(0.35, 0.65), kids=(2, 2), up=0.45, child_len=0.55, n=7)
    _canopy_crown(b, (0, 0, trunk[-1].z + 1.7), (2.3, 2.3, 1.6), "street", 1.15, 440, lump=0.32)
    return b.build(_materials())


def tree_columnar(size="large", name=None):
    s = 1.0 if size == "large" else 0.6
    b = TreeBuilder(name or "tree_columnar_%s" % size)
    H = 10.0 * s
    trunk = _trunk(b, H * 0.8, 0.22 * s, 0.06 * s, wobble=0.015)
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
    _canopy(b, cs, "column", 0.95 * s ** 0.7, density=12.0, squash=1.15, crown_w=0.75)
    return b.build(_materials())


def tree_cherry(size="large", name=None):
    s = 1.0 if size == "large" else 0.62
    b = TreeBuilder(name or "tree_cherry_%s" % size)
    b.mat_bark = "bark_cherry"
    trunk = _trunk(b, 1.3 * s, 0.25 * s, 0.19 * s, lean=(0.1, 0, 1), wobble=0.05)
    tips = []
    for k in range(5):
        a = 2 * math.pi * k / 5 + 0.3
        E._grow(b, trunk[-1], (math.cos(a), math.sin(a), 0.85), 1.9 * s, 0.13 * s, 2, tips, "bark",
                spread=(0.5, 0.9), kids=(2, 2), up=0.15, child_len=0.62, n=8)
    cs = [(p + Vector((0, 0, 0.3 * s)), 0.95 * s * b.rng.uniform(0.85, 1.1)) for p, d in tips]
    _canopy(b, cs, "cherry", 0.95 * s ** 0.8, density=10.0, squash=0.58, out_bias=0.5, up_w=0.35, crown_w=0.6)
    return b.build(_materials())


def tree_japanese_maple(name=None):
    b = TreeBuilder(name or "tree_japanese_maple")
    b.mat_bark = "bark_dark"
    tips = []
    for a, lean in ((0.3, 0.55), (2.4, 0.6), (4.3, 0.5)):
        E._grow(b, (0.05 * math.cos(a), 0.05 * math.sin(a), 0), (lean * math.cos(a), lean * math.sin(a), 1.0), 1.1,
                0.08, 2, tips, "bark", spread=(0.55, 0.95), kids=(2, 2), up=0.05, child_len=0.75, n=7)
    cs = [(p + Vector((0, 0, 0.1)), 0.7 * b.rng.uniform(0.85, 1.15)) for p, d in tips]
    _canopy(b, cs, "jmaple", 0.85, density=17.0, squash=0.38, out_bias=0.35, up_w=1.2, crown_w=0.55, cup=0.1)
    return b.build(_materials())


def tree_spruce(size="large", name=None):
    """Whorls of drooping branches, each a pair of crossed needle-spray
    cards (tilted +-28 deg about the branch) following the branch's droop;
    a lathed trunk; crossed vertical cards for the leader."""
    s = 1.0 if size == "large" else 0.55
    b = TreeBuilder(name or "tree_spruce_%s" % size)
    b.mat_bark = "bark_spruce"
    rng = b.rng
    H = 10.0 * s
    trunk = [Vector((0, 0, z * H)) for z in (0, 0.05, 0.3, 0.6, 0.85, 0.97)]
    b.tube(trunk, [0.3 * s, 0.24 * s, 0.19 * s, 0.12 * s, 0.06 * s, 0.03 * s], "bark", n=9, cap=True)

    def nfn(p):
        q = Vector((p.x, p.y, 0))
        rad = q.normalized() if q.length > 1e-4 else Vector((1, 0, 0))
        return (rad + Vector((0, 0, 0.55))).normalized()

    levels = 17 if s == 1 else 13
    for i in range(levels):
        f = i / levels
        z = 0.9 * s + f * (H - 1.9 * s)
        L = 2.5 * s * (1 - f) ** 0.85 + 0.4 * s
        nb = 8 if f < 0.6 else 6
        ph = rng.uniform(0, 2 * math.pi)
        for k in range(nb):
            a = ph + 2 * math.pi * k / nb + rng.uniform(-0.15, 0.15)
            d = Vector((math.cos(a), math.sin(a), 0))
            path = [Vector((0, 0, z)) + d * (L * t) + Vector((0, 0, 0.15 * L * t - 0.42 * L * t * t))
                    for t in (0.0, 0.34, 0.67, 1.0)]
            width = (0.5 * s + 0.3 * L)
            row = rng.randrange(4)
            v0, v1 = 1 - (row + 1) / 4 + 0.004, 1 - row / 4 - 0.004
            for tilt in (-0.5, 0.5):
                verts, uvs = [], []
                for j, p in enumerate(path):
                    tang = (path[min(j + 1, 3)] - path[max(j - 1, 0)]).normalized()
                    side = tang.cross(Vector((0, 0, 1))).normalized()
                    up = side.cross(tang).normalized()
                    across = (side * math.cos(tilt) + up * math.sin(tilt)).normalized()
                    hw = width / 2 * (1.0 if j else 0.6)
                    verts.append((p - across * hw, p + across * hw))
                    uvs.append(j / 3)
                for j in range(3):
                    q = [verts[j][0], verts[j + 1][0], verts[j + 1][1], verts[j][1]]
                    quv = [(uvs[j] * 0.985, v0), (uvs[j + 1] * 0.985, v0), (uvs[j + 1] * 0.985, v1), (uvs[j] * 0.985, v1)]
                    fnr = (q[1] - q[0]).cross(q[3] - q[0])
                    if fnr.z < 0:
                        q, quv = list(reversed(q)), list(reversed(quv))
                    b.face(q, quv, "spruce", [(nfn(p) * 0.8 + Vector((0, 0, 1)) * 0.2).normalized() for p in q])
    # leader: crossed vertical fronds
    for a in (0.0, math.pi / 2, math.pi / 4):
        side = Vector((math.cos(a), math.sin(a), 0))
        z0, z1 = H - 1.8 * s, H + 0.2 * s
        hw = 0.35 * s
        q = [Vector((0, 0, z0)) - side * hw, Vector((0, 0, z0)) + side * hw, Vector((0, 0, z1)) + side * hw * 0.3,
             Vector((0, 0, z1)) - side * hw * 0.3]
        quv = [(0.0, 0.004), (0.0, 0.246), (0.985, 0.246), (0.985, 0.004)]
        b.face(q, quv, "spruce", [nfn(p) for p in q])
    return b.build(_materials())


PIECES = {
    "tree-maple-large": (lambda: tree_maple("large"), "Maple (large)"),
    "tree-maple-small": (lambda: tree_maple("small"), "Maple (young)"),
    "tree-cherry-large": (lambda: tree_cherry("large"), "Cherry (large)"),
    "tree-cherry-small": (lambda: tree_cherry("small"), "Cherry (young)"),
    "tree-spruce-large": (lambda: tree_spruce("large"), "Spruce (large)"),
    "tree-spruce-small": (lambda: tree_spruce("small"), "Spruce (young)"),
    "tree-columnar-large": (lambda: tree_columnar("large"), "Columnar (large)"),
    "tree-columnar-small": (lambda: tree_columnar("small"), "Columnar (young)"),
    "tree-street": (tree_street, "Street tree"),
    "tree-japanese-maple": (tree_japanese_maple, "Japanese maple (yard)"),
}


def install_into_env():
    """Swap dh_env's tree builders for the textured ones (street scene)."""
    E.tree_maple = lambda size="large", name=None: tree_maple(size, name)
    E.tree_cherry = lambda size="large", name=None: tree_cherry(size, name)
    E.tree_spruce = lambda size="large", name=None: tree_spruce(size, name)
    E.tree_columnar = lambda size="large", name=None: tree_columnar(size, name)
    E.tree_street = lambda name=None: tree_street(name)
    E.tree_japanese_maple = lambda name=None: tree_japanese_maple(name)
