"""Build the 3D splash street in Blender: the 11 houses, the ground and the key props.

Run from the repository root with headless Blender as a Python module (bpy 5.3.0, see
docs/engineering/blender-pipeline.md):

    python3 art-source/splash-3d/build_scene.py [out.blend]

`build(mode)` is what the render and export scripts call. mode="render" gives the toon
materials used for the splash renders; mode="export" gives plain Principled materials
(base colour only) for glTF.

World axes follow the house kit: X right, Y along the street away from the splash camera,
Z up; 1 unit = 1 m. Every house front faces -Y (toward the splash camera).
"""
import math
import os
import random
import sys
import zlib

import bpy

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import splash_data as SD  # noqa: E402

# --------------------------------------------------------------------------
# Tuning (m unless noted)
# --------------------------------------------------------------------------
FRAME_OUT = 0.07         # window / door frame stands this far proud of the wall
GLASS_IN = 0.02          # glass sits this far proud of the wall (inside the frame)
MULL_OUT = 0.05
SILL_OUT = 0.14          # a plain sill's depth
BOX_OUT = 0.3            # a flower box's depth
TRIM_OUT = 0.1           # trim band's depth
FOUND_OUT = 0.04
TREAD = 0.34             # step tread depth
STEP_PAD = 0.18          # steps run this far past the door frame on each side
PATH_W = 1.1             # front path width
WING_ROOF_T = 0.22       # wing roof slab thickness
GAP_MIN = 0.45           # keep a house's back this far in front of the next house's front
MAILBOX_TOP_K = 0.9      # the measured top includes the flag: the box's top is a little lower
MAILBOX_BOX_K = 0.27     # box height as a share of the whole mailbox height
MAILBOX_LEN_K = 1.35     # box length / box height
SIDEWALK_RAISE = 0.12    # sidewalks and lawns sit this far above the road
STREET_FAR = 70.0        # the street's far end (m along the street)
HAZE_NEAR, HAZE_FAR, HAZE_MAX = 25.0, 110.0, 0.55   # ground haze ramp (distance m, max mix)
HAZE_COLOUR = "#b9e6ee"
TREELINE_N, TREELINE_HALF, TREELINE_H = 18, 30.0, 3.4   # far treeline blobs: count, half-width, height (m)
TOON_SHADE = 0.66        # the toon ramp's value for faces turned away from the light
TOON_TOP = 1.1           # the toon ramp's cap (faces turned to the light more than a front)
LIGHT_DIR = (0.22, -0.62, 0.75)   # toward the light: behind the camera, above, a little right
RAW_UNIT = 0.254         # ShaderToRGB of a white diffuse face square to a sun of strength 1 (measured)
SUN_ENERGY = 3.0

_LN = math.sqrt(sum(c * c for c in LIGHT_DIR))
LIGHT = tuple(c / _LN for c in LIGHT_DIR)
FRONT_COS = -LIGHT[1]    # cos of the light on a face square to the camera (front walls)


# --------------------------------------------------------------------------
# Materials
# --------------------------------------------------------------------------
def srgb_to_lin(c):
    return c / 12.92 if c <= 0.04045 else ((c + 0.055) / 1.055) ** 2.4


def hex_lin(h):
    h = h.lstrip("#")
    return tuple(srgb_to_lin(int(h[i:i + 2], 16) / 255.0) for i in (0, 2, 4))


_MATS = {}
MODE = {"m": "render"}


def mat(name, hexc, haze=False, flat=False):
    """A material named DH_Splash_<name>. In render mode: toon (base x ramp(light)), with
    optional ground haze; flat=True is unlit (the far treeline). In export mode: Principled."""
    key = (name, hexc, haze, flat)
    if key in _MATS:
        return _MATS[key]
    m = bpy.data.materials.new(f"DH_Splash_{name}")
    m.diffuse_color = (*hex_lin(hexc), 1.0)
    nt = m.node_tree
    nt.nodes.clear()
    out = nt.nodes.new("ShaderNodeOutputMaterial")
    base = hex_lin(hexc)
    if MODE["m"] == "export":
        p = nt.nodes.new("ShaderNodeBsdfPrincipled")
        p.inputs["Base Color"].default_value = (*base, 1.0)
        p.inputs["Roughness"].default_value = 0.9
        p.inputs["Metallic"].default_value = 0.0
        nt.links.new(p.outputs[0], out.inputs[0])
        _MATS[key] = m
        return m
    rgb = nt.nodes.new("ShaderNodeRGB")
    rgb.outputs[0].default_value = (*base, 1.0)
    col = rgb.outputs[0]
    if not flat:
        dif = nt.nodes.new("ShaderNodeBsdfDiffuse")
        dif.inputs[0].default_value = (1, 1, 1, 1)
        s2r = nt.nodes.new("ShaderNodeShaderToRGB")
        nt.links.new(dif.outputs[0], s2r.inputs[0])
        bw = nt.nodes.new("ShaderNodeRGBToBW")
        nt.links.new(s2r.outputs[0], bw.inputs[0])
        # s = lit fraction relative to a front wall: 0 in shade, 1 on a front wall
        mr = nt.nodes.new("ShaderNodeMapRange")
        mr.clamp = True
        mr.inputs["From Min"].default_value = 0.0
        mr.inputs["From Max"].default_value = RAW_UNIT * SUN_ENERGY * FRONT_COS * (TOON_TOP - TOON_SHADE) / (1 - TOON_SHADE)
        mr.inputs["To Min"].default_value = TOON_SHADE
        mr.inputs["To Max"].default_value = TOON_TOP
        nt.links.new(bw.outputs[0], mr.inputs["Value"])
        mix = nt.nodes.new("ShaderNodeMix")
        mix.data_type = "RGBA"
        mix.blend_type = "MULTIPLY"
        mix.inputs["Factor"].default_value = 1.0
        nt.links.new(col, mix.inputs[6])
        comb = nt.nodes.new("ShaderNodeCombineColor")
        for i in range(3):
            nt.links.new(mr.outputs[0], comb.inputs[i])
        nt.links.new(comb.outputs[0], mix.inputs[7])
        col = mix.outputs[2]
    if haze:
        cam = nt.nodes.new("ShaderNodeCameraData")
        hr = nt.nodes.new("ShaderNodeMapRange")
        hr.clamp = True
        hr.inputs["From Min"].default_value = HAZE_NEAR
        hr.inputs["From Max"].default_value = HAZE_FAR
        hr.inputs["To Min"].default_value = 0.0
        hr.inputs["To Max"].default_value = HAZE_MAX
        nt.links.new(cam.outputs["View Distance"], hr.inputs["Value"])
        hz = nt.nodes.new("ShaderNodeMix")
        hz.data_type = "RGBA"
        nt.links.new(hr.outputs[0], hz.inputs["Factor"])
        nt.links.new(col, hz.inputs[6])
        hz.inputs[7].default_value = (*hex_lin(HAZE_COLOUR), 1.0)
        col = hz.outputs[2]
    em = nt.nodes.new("ShaderNodeEmission")
    nt.links.new(col, em.inputs[0])
    nt.links.new(em.outputs[0], out.inputs[0])
    _MATS[key] = m
    return m


# --------------------------------------------------------------------------
# Mesh building
# --------------------------------------------------------------------------
class MB:
    """Accumulates polygons (with a material each) for one object."""

    def __init__(self):
        self.v, self.f, self.fm, self.mats = [], [], [], []

    def _mi(self, m):
        if m not in self.mats:
            self.mats.append(m)
        return self.mats.index(m)

    def poly(self, pts, m):
        i0 = len(self.v)
        self.v.extend(pts)
        self.f.append(list(range(i0, i0 + len(pts))))
        self.fm.append(self._mi(m))

    def box(self, x0, x1, y0, y1, z0, z1, m, front=None, top=None, skip=()):
        """Axis-aligned box. front: material of the -Y face; top: of the +Z face."""
        x0, x1 = min(x0, x1), max(x0, x1)
        y0, y1 = min(y0, y1), max(y0, y1)
        z0, z1 = min(z0, z1), max(z0, z1)
        faces = {
            "front": ([(x0, y0, z0), (x1, y0, z0), (x1, y0, z1), (x0, y0, z1)], front or m),
            "back": ([(x1, y1, z0), (x0, y1, z0), (x0, y1, z1), (x1, y1, z1)], m),
            "left": ([(x0, y1, z0), (x0, y0, z0), (x0, y0, z1), (x0, y1, z1)], m),
            "right": ([(x1, y0, z0), (x1, y1, z0), (x1, y1, z1), (x1, y0, z1)], m),
            "top": ([(x0, y0, z1), (x1, y0, z1), (x1, y1, z1), (x0, y1, z1)], top or m),
            "bottom": ([(x0, y1, z0), (x1, y1, z0), (x1, y0, z0), (x0, y0, z0)], m),
        }
        for k, (pts, mm) in faces.items():
            if k not in skip:
                self.poly(pts, mm)

    def prism_y(self, xz, y0, y1, m, front=None, back=None):
        """Extrude a polygon in the XZ plane (counter-clockwise seen from -Y) from y0 to y1."""
        n = len(xz)
        self.poly([(x, y0, z) for x, z in xz], front or m)
        self.poly([(x, y1, z) for x, z in reversed(xz)], back or m)
        for i in range(n):
            (ax, az), (bx, bz) = xz[i], xz[(i + 1) % n]
            self.poly([(bx, y0, bz), (ax, y0, az), (ax, y1, az), (bx, y1, bz)], m)

    def hexa(self, c, mats):
        """A six-faced solid from 8 corners: c[0..3] the y0 face (ccw from -Y), c[4..7] the
        same corners at y1. mats: front, back, and the four sides in order of edges."""
        f, b, sides = mats[0], mats[1], mats[2:]
        self.poly([c[0], c[1], c[2], c[3]], f)
        self.poly([c[7], c[6], c[5], c[4]], b)
        for i in range(4):
            j = (i + 1) % 4
            self.poly([c[j], c[i], c[i + 4], c[j + 4]], sides[i])

    def obj(self, name, coll, parent=None, smooth=False):
        me = bpy.data.meshes.new(name)
        me.from_pydata(self.v, [], self.f)
        me.update()
        for m in self.mats:
            me.materials.append(m)
        for p, mi in zip(me.polygons, self.fm):
            p.material_index = mi
            p.use_smooth = smooth
        # merge coincident corners so normals and exports are clean
        import bmesh
        bm = bmesh.new()
        bm.from_mesh(me)
        bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-5)
        bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
        bm.to_mesh(me)
        bm.free()
        o = bpy.data.objects.new(name, me)
        coll.objects.link(o)
        if parent is not None:
            o.parent = parent
        return o


def ico(mb, cx, cy, cz, rx, ry, rz, m, seed, jitter=0.18, subdiv=1, m_lit=None):
    """A faceted low-poly crown: a jittered icosphere."""
    rnd = random.Random(seed)
    t = (1 + 5 ** 0.5) / 2
    verts = [(-1, t, 0), (1, t, 0), (-1, -t, 0), (1, -t, 0), (0, -1, t), (0, 1, t), (0, -1, -t), (0, 1, -t),
             (t, 0, -1), (t, 0, 1), (-t, 0, -1), (-t, 0, 1)]
    faces = [(0, 11, 5), (0, 5, 1), (0, 1, 7), (0, 7, 10), (0, 10, 11), (1, 5, 9), (5, 11, 4), (11, 10, 2),
             (10, 7, 6), (7, 1, 8), (3, 9, 4), (3, 4, 2), (3, 2, 6), (3, 6, 8), (3, 8, 9), (4, 9, 5), (2, 4, 11),
             (6, 2, 10), (8, 6, 7), (9, 8, 1)]
    verts = [tuple(c / math.sqrt(sum(a * a for a in v)) for c in v) for v in verts]
    for _ in range(subdiv):
        cache, nf = {}, []

        def mid(a, b):
            k = (min(a, b), max(a, b))
            if k not in cache:
                p = [(verts[a][i] + verts[b][i]) / 2 for i in range(3)]
                ln = math.sqrt(sum(c * c for c in p))
                verts.append(tuple(c / ln for c in p))
                cache[k] = len(verts) - 1
            return cache[k]
        for a, b, c in faces:
            ab, bc, ca = mid(a, b), mid(b, c), mid(c, a)
            nf += [(a, ab, ca), (b, bc, ab), (c, ca, bc), (ab, bc, ca)]
        faces = nf
    pts = []
    for v in verts:
        k = 1 + rnd.uniform(-jitter, jitter)
        pts.append((cx + v[0] * rx * k, cy + v[1] * ry * k, cz + v[2] * rz * k))
    for a, b, c in faces:
        mm = m
        if m_lit is not None:
            # facets facing up and toward the camera take the lit leaf colour
            nz = sum(verts[i][2] for i in (a, b, c)) / 3
            ny = sum(verts[i][1] for i in (a, b, c)) / 3
            mm = m_lit if (nz > 0.35 and ny < 0.3) else m
        mb.poly([pts[a], pts[b], pts[c]], mm)


def cyl(mb, cx, cy, z0, z1, r0, r1, m, n=6):
    ring0 = [(cx + r0 * math.cos(2 * math.pi * i / n), cy + r0 * math.sin(2 * math.pi * i / n), z0) for i in range(n)]
    ring1 = [(cx + r1 * math.cos(2 * math.pi * i / n), cy + r1 * math.sin(2 * math.pi * i / n), z1) for i in range(n)]
    for i in range(n):
        j = (i + 1) % n
        mb.poly([ring0[i], ring0[j], ring1[j], ring1[i]], m)
    mb.poly(list(reversed(ring0)), m)
    mb.poly(ring1, m)


# --------------------------------------------------------------------------
# The houses
# --------------------------------------------------------------------------
def house_depths(cam):
    """Per house: its front's distance and how deep its blocks may run before the next
    house along its side of the street."""
    out = {}
    for side in "LR":
        hs = sorted((h for h in SD.HOUSES if h["side"] == side), key=lambda h: SD.depth_of(cam, h["ground"]))
        for i, h in enumerate(hs):
            d = SD.depth_of(cam, h["ground"])
            gap = (SD.depth_of(cam, hs[i + 1]["ground"]) - d) if i + 1 < len(hs) else 99.0
            dd = dict(SD.DEPTH)
            dd.update(h.get("depth", {}))
            room = gap - GAP_MIN
            dd["gable"] = max(2.5, min(dd["gable"], room - dd["overhang_back"]))
            dd["wing"] = max(2.5, min(dd["wing"], room - dd["wing_setback"] - dd["overhang_back"]))
            out[h["key"]] = (d, dd)
    return out


def build_house(h, cam, coll, depth):
    d, dd = depth
    C = h["colours"]
    U = lambda x, y, dist=d: SD.unproject(cam, x, y, dist)  # noqa: E731
    px = lambda n, dist=d: n * dist / cam["f"]                # noqa: E731
    sgn = -1 if h["side"] == "L" else 1                       # the wing's side (away from the street)
    g = h["gable"]
    M = {k: mat(f"{h['key']}_{k}", v) for k, v in C.items()}
    M.setdefault("gable", M["wall"])
    M.setdefault("fascia", M["barge"])

    X0, _ = U(g["x0"], h["ground"])
    X1, _ = U(g["x1"], h["ground"])
    _, Zel = U(g["x0"], g["eave_y"][0])
    _, Zer = U(g["x1"], g["eave_y"][1])
    Xa, Za = U(*g["apex"])
    _, Ztop = U(g["apex"][0], g["roof_top"])
    tv = max(0.12, Ztop - Za)
    ov = px(g["overhang"])
    Dg = dd["gable"]
    of, ob = dd["overhang_front"], dd["overhang_back"]

    root = bpy.data.objects.new(f"house_{h['key']}", None)
    coll.objects.link(root)
    root.location = ((X0 + X1) / 2, d, 0.0)
    root.empty_display_size = 1.0
    ox, oy = root.location.x, root.location.y

    def L(p):  # world -> root-local
        return (p[0] - ox, p[1] - oy, p[2])

    walls, roof, det = MB(), MB(), MB()

    # ---- front-gable block
    walls.box(X0, X1, d, d + Dg, 0, min(Zel, Zer), M["wall"])
    tri = [(X0, min(Zel, Zer)), (X1, min(Zel, Zer)), (X1, Zer), (Xa, Za), (X0, Zel)]
    walls.prism_y(tri, d, d + Dg, M["wall"], front=M["gable"])

    def slope_slab(xe, ze, y0, y1, mats_front, rough_sign):
        """One roof plane from the apex to an eave corner (xe, ze), carried past the wall by ov."""
        dx, dz = xe - Xa, ze - Za
        k = (abs(dx) + ov) / abs(dx)
        bx, bz = Xa + dx * k, Za + dz * k
        c = [(Xa, y0, Za), (bx, y0, bz), (bx, y0, bz + tv), (Xa, y0, Za + tv)]
        if rough_sign > 0:
            c = [c[1], c[0], c[3], c[2]]
        c += [(p[0], y1, p[2]) for p in c]
        return c

    y0r, y1r = d - of, d + Dg + ob
    for xe, ze, s in ((X0, Zel, -1), (X1, Zer, 1)):
        c = slope_slab(xe, ze, y0r, y1r, None, s)
        # faces: front (barge), back, then edges: bottom (soffit), eave end (fascia), top (roof), apex
        if s < 0:
            mats = [M["barge"], M["barge"], M["soffit"], M["fascia"], M["roof"], M["roof"]]
        else:
            mats = [M["barge"], M["barge"], M["soffit"], M["roof"], M["roof"], M["fascia"]]
        roof.hexa(c, mats)

    # ---- side wing (set back, side-gabled, runs away from the street)
    w = h.get("wing")
    Xw_out = None
    if w:
        dw = d + dd["wing_setback"]
        Dw = dd["wing"]
        Xo, Zw = U(w["x_out"], w["top"], dw)
        Xi = {"x0": X0 if sgn < 0 else X1, "apex": Xa, "x1": X1 if sgn < 0 else X0}[dd["wing_inner"]]
        if abs(Xo - Xi) > dd["wing_width"] + abs(X1 - X0) and abs(w["x_out"] - SD.W / 2) > SD.W / 2:
            Xo = Xi + sgn * dd["wing_width"]
        Xw_out = Xo
        walls.box(Xo, Xi, dw, dw + Dw, 0, Zw, M["wing"])
        yr = dw + Dw / 2
        _, Zr = U(w["x_out"], w["ridge"], yr)
        wov = 0.3
        # front and back slopes (slabs of thickness WING_ROOF_T, vertical)
        run = Dw / 2 + wov
        slope = (Zr - Zw) / (Dw / 2)
        xa, xb = (Xo + sgn * wov, Xi) if sgn < 0 else (Xi, Xo + sgn * wov)
        ze = Zw - wov * slope
        for (ya, za), (yb, zb) in (((dw - wov, ze), (yr, Zr)), ((yr, Zr), (dw + Dw + wov, ze))):
            t = WING_ROOF_T
            # corners at x = xa (y0 face ccw seen from -X... built explicitly)
            pts = [(ya, za - t), (yb, zb - t), (yb, zb), (ya, za)]
            c = [(xa, y, z) for y, z in pts] + [(xb, y, z) for y, z in pts]
            # hexa wants c[0..3] at one end; faces: end, end, bottom(soffit), far, top(roof), near
            for i in range(4):
                pass
            roof.poly([c[0], c[1], c[2], c[3]][::-1], M["barge"])
            roof.poly([c[4], c[5], c[6], c[7]], M["barge"])
            roof.poly([c[0], c[4], c[5], c[1]][::-1], M["soffit"])
            roof.poly([c[1], c[5], c[6], c[2]][::-1], M["fascia"])
            roof.poly([c[2], c[6], c[7], c[3]][::-1], M["roof"])
            roof.poly([c[3], c[7], c[4], c[0]][::-1], M["fascia"])
        # the wing's outer gable end
        walls.poly([(Xo, dw, Zw), (Xo, dw + Dw, Zw), (Xo, yr, Zr - WING_ROOF_T)][:: (1 if sgn > 0 else -1)], M["wing"])
        # chimney (on the wing's front slope)
        ch = h.get("chimney")
        if ch:
            dc = dw + Dw * 0.3
            fx0, fz1 = U(ch["front"][0], ch["front"][1], dc)
            fx1, _ = U(ch["front"][2], ch["front"][3], dc)
            cx0, cz1 = U(ch["cap"][0], ch["cap"][1], dc)
            cx1, cz0 = U(ch["cap"][2], ch["cap"][3], dc)
            depth_c = abs(fx1 - fx0) * 0.9
            det.box(fx0, fx1, dc, dc + depth_c, Zw, cz0, M["chimney"])
            det.box(cx0, cx1, dc - 0.06, dc + depth_c + 0.06, cz0, cz1, M["cap"])
        sk = h.get("skylight")
        if sk:
            # a flat box lying on the wing's front slope, found by casting the camera ray
            def on_roof(x, y):
                # ray (0, 0, h) + t * ((x-cx)/f, 1, (vy-y)/f) meets z = Zw + (Y - dw) * slope
                rx, rz = (x - cam["cx"]) / cam["f"], (cam["vy"] - y) / cam["f"]
                t = (Zw - dw * slope - cam["h"]) / (rz - slope)
                return (rx * t, t, cam["h"] + rz * t)
            a, b = on_roof(sk[0], sk[3]), on_roof(sk[2], sk[1])
            n = (0, -slope, 1)
            nl = math.sqrt(1 + slope * slope)
            up = 0.06
            p = [(a[0], a[1], a[2]), (b[0], a[1], a[2]), (b[0], b[1], b[2]), (a[0], b[1], b[2])]
            top = [(q[0], q[1] + n[1] / nl * up, q[2] + n[2] / nl * up) for q in p]
            det.poly(top, M["skylight"])
            det.poly([p[0], p[1], top[1], top[0]], M["roof"])

    # ---- the far houses' and yellow's lower bands
    if h.get("foundation"):
        _, Zf = U(g["x0"], h["foundation"])
        det.box(X0 - 0.02, X1 + 0.02, d - FOUND_OUT, d + 0.2, 0, Zf, M["foundation"])
        if w:
            _, Zfw = U(w["x_out"], h["foundation"], dw)
            det.box(Xw_out, Xi, dw - FOUND_OUT, dw + 0.2, 0, Zfw, M["foundation"])
    if h.get("porch"):
        _, Zp = U(g["x0"], h["porch"])
        det.box(X0, X1, d - 0.03, d + 0.2, 0, Zp, M["porch"])

    # ---- trim band at the gable's foot
    if h.get("trim"):
        _, zt0 = U(g["x0"], h["trim"][1])
        _, zt1 = U(g["x0"], h["trim"][0])
        det.box(X0 - 0.06, X1 + 0.06, d - TRIM_OUT, d + 0.05, zt0, zt1, M["trim"])

    # ---- windows
    for win in h.get("windows", []):
        on_wing = win.get("on") == "wing" and w
        p = dw if on_wing else d
        fr, gl = win["frame"], win["glass"]
        fx0, fz1 = U(fr[0], fr[1], p)
        fx1, fz0 = U(fr[2], fr[3], p)
        gx0, gz1 = U(gl[0], gl[1], p)
        gx1, gz0 = U(gl[2], gl[3], p)
        mf = mat(f"{h['key']}_wframe_{win.get('colour', C['frame'])}", win.get("colour", C["frame"]))
        mg = mat(f"{h['key']}_glass_{win.get('glass_colour', C['glass'])}", win.get("glass_colour", C["glass"]))
        y0 = p - FRAME_OUT
        det.box(fx0, fx1, y0, p + 0.02, gz1, fz1, mf)    # head
        det.box(fx0, fx1, y0, p + 0.02, fz0, gz0, mf)    # foot
        det.box(fx0, gx0, y0, p + 0.02, gz0, gz1, mf)    # left jamb
        det.box(gx1, fx1, y0, p + 0.02, gz0, gz1, mf)    # right jamb
        det.box(gx0, gx1, p - GLASS_IN, p + 0.02, gz0, gz1, mg)
        if win.get("mull"):
            _, mz1 = U(gl[0], win["mull"][0], p)
            _, mz0 = U(gl[0], win["mull"][1], p)
            det.box(gx0, gx1, p - MULL_OUT, p - GLASS_IN, mz0, mz1, mf)
        if win.get("sill"):
            _, sz1 = U(fr[0], win["sill_y"][0], p)
            _, sz0 = U(fr[0], win["sill_y"][1], p)
            ovh = (fx1 - fx0) * 0.08
            out = BOX_OUT if win["sill"] == "box" else SILL_OUT
            det.box(fx0 - ovh, fx1 + ovh, p - out, p + 0.02, sz0, max(sz1, fz0 + 0.001), M["sill"])

    # ---- door
    dr = h["door"]
    dx0, dz1 = U(dr["frame"][0], dr["frame"][1])
    dx1, dz0 = U(dr["frame"][2], dr["frame"][3])
    px0, pz1 = U(dr["panel"][0], dr["panel"][1])
    px1, _ = U(dr["panel"][2], dr["panel"][3])
    det.box(dx0, dx1, d - FRAME_OUT, d + 0.02, pz1, dz1, M["frame"])
    det.box(dx0, px0, d - FRAME_OUT, d + 0.02, dz0, pz1, M["frame"])
    det.box(px1, dx1, d - FRAME_OUT, d + 0.02, dz0, pz1, M["frame"])
    det.box(px0, px1, d - 0.015, d + 0.02, dz0, pz1, M["door"])
    if dr.get("knob"):
        kx, kz = U(*dr["knob"])
        r = px(9)
        ico(det, kx, d - 0.03 - r, kz, r, r, r, M["knob"], seed=1, jitter=0.0, subdiv=1)

    # ---- front steps down from the door, and the path to the sidewalk
    st = h.get("steps") or {}
    n = st.get("n", 0)
    sx0, sx1 = dx0 - STEP_PAD, dx1 + STEP_PAD
    if n and dz0 > 0.05:
        rise = dz0 / n
        for k in range(n):
            det.box(sx0, sx1, d - (k + 1) * TREAD, d, 0, dz0 - k * rise, M["steps"], front=M["foundation"])
    foot = d - n * TREAD
    lawn = cam["lawn_l"] if h["side"] == "L" else cam["lawn_r"]
    door_mid = (dx0 + dx1) / 2
    pw = min(PATH_W, (sx1 - sx0))
    pm = mat("path", SD.GROUND_COLOURS["path"])
    pe = mat("path_edge", SD.GROUND_COLOURS["path_edge"])
    pa, pb = (door_mid - pw / 2, lawn) if h["side"] == "R" else (lawn, door_mid + pw / 2)
    det.box(pa, pb, foot - pw, foot, 0, 0.025, pm, front=pe)

    objs = []
    for nm, mb in (("walls", walls), ("roof", roof), ("details", det)):
        if not mb.f:
            continue
        mb.v = [L(p) for p in mb.v]
        objs.append(mb.obj(f"house_{h['key']}_{nm}", coll, parent=root))
    return root


# --------------------------------------------------------------------------
# Ground and props
# --------------------------------------------------------------------------
def build_ground(cam, coll):
    G = SD.GROUND_COLOURS
    mb = MB()
    raise_ = SIDEWALK_RAISE
    far = STREET_FAR
    near = 1.0
    # road (below the sidewalks), kerb faces, sidewalks with joints, lawns
    mb.box(cam["road_l"], cam["road_r"], near, far + 6, -0.3, -raise_, mat("asphalt", G["asphalt"], haze=True))
    for side in ("l", "r"):
        sg = -1 if side == "l" else 1
        road, lawn = cam[f"road_{side}"], cam[f"lawn_{side}"]
        mpav = mat("paving", G["paving"], haze=True)
        mkerb = mat("kerb", G["kerb"], haze=True)
        mb.box(road, road - sg * 0.001, near, far, -raise_, 0.0, mkerb)  # kerb face
        mb.poly([(road, near, -raise_), (road, far, -raise_), (road, far, 0), (road, near, 0)][:: (1 if side == "l" else -1)], mkerb)
        mb.box(lawn, road, near, far, -0.05, 0.0, mpav)
        # joints: one slab = the measured joint spacing
        y_near, y_far = SD.JOINTS[side]
        j0 = SD.depth_of(cam, y_near)
        slab = SD.depth_of(cam, y_far) - j0
        mj = mat("joint", G["joint"], haze=True)
        y = j0 - slab * math.floor((j0 - near) / slab)
        while y < far:
            mb.box(lawn, road, y - 0.03, y + 0.03, 0.0, 0.004, mj)
            y += slab
        # lawn
        mb.box(lawn, lawn + sg * 60, near, far + 40, -0.05, -0.01, mat("grass", G["grass"], haze=True))
    # far cross street and the lawn beyond it
    mb.box(-60, 60, far, far + 6, -0.3, -raise_, mat("asphalt", G["asphalt"], haze=True))
    mb.box(-60, 60, far + 6, far + 60, -0.05, -0.01, mat("grass", G["grass"], haze=True))
    mb.obj("ground_street", coll)

    # horizon treeline: hazy blue-green blobs past the cross street (unlit, as painted)
    tl = MB()
    mt = mat("treeline", G["treeline"], flat=True)
    rnd = random.Random(7)
    for i in range(TREELINE_N):
        x = -TREELINE_HALF + i * (2 * TREELINE_HALF / (TREELINE_N - 1)) + rnd.uniform(-1, 1)
        hgt = TREELINE_H * rnd.uniform(0.6, 1.25)
        ico(tl, x, far + 32 + rnd.uniform(0, 6), hgt * 0.45, 2.6, 2.0, hgt * 0.55, mt, seed=100 + i)
    tl.obj("horizon_treeline", coll)


def ground_pt(cam, x, y):
    Y = SD.depth_of(cam, y)
    X, _ = SD.unproject(cam, x, y, Y)
    return X, Y


def build_props(cam, coll):
    pc = bpy.data.collections.new("props")
    coll.children.link(pc)
    for t in SD.TREES:
        X, Y = ground_pt(cam, *t["foot"])
        cx, cz = SD.unproject(cam, t["crown"][0], t["crown"][1], Y)
        rx, rz = t["r"][0] * Y / cam["f"], t["r"][1] * Y / cam["f"]
        mb = MB()
        ml, mlit = mat(f"leaf_{t['leaf']}", t["leaf"]), mat(f"leaf_{t['lit']}", t["lit"])
        cyl(mb, X, Y, 0, cz, t["trunk_w"] / 2 * Y / cam["f"] * 1.1, t["trunk_w"] / 2 * Y / cam["f"] * 0.7,
            mat(f"bark_{t['bark']}", t["bark"]))
        ico(mb, cx, Y, cz, rx, rx, rz, ml, seed=zlib.crc32(t["key"].encode()) % 1000, m_lit=mlit)
        mb.obj(t["key"], pc)
    for t in SD.POPLARS:
        X, Y = ground_pt(cam, *t["foot"])
        hh, ww = t["h"] * Y / cam["f"], t["w"] * Y / cam["f"]
        mb = MB()
        cyl(mb, X, Y, 0, hh * 0.35, ww * 0.12, ww * 0.1, mat(f"bark_{t['bark']}", t["bark"]))
        ico(mb, X, Y, hh * 0.6, ww / 2, ww / 2, hh * 0.42, mat(f"leaf_{t['leaf']}", t["leaf"]), seed=zlib.crc32(t["key"].encode()) % 997)
        mb.obj(t["key"], pc)
    for b in SD.BUSHES:
        X, Y = ground_pt(cam, *b["foot"])
        rx, rz = b["r"][0] * Y / cam["f"], b["r"][1] * Y / cam["f"]
        mb = MB()
        ico(mb, X, Y, rz * 0.8, rx, rx * 0.8, rz, mat(f"leaf_{b['leaf']}", b["leaf"]), seed=zlib.crc32(b["key"].encode()) % 991,
            m_lit=mat(f"leaf_{b['lit']}", b["lit"]))
        mb.obj(b["key"], pc)
    for fz in SD.FENCES:
        Xa, Y = ground_pt(cam, fz["x"][0], fz["ground"])
        Xb, _ = ground_pt(cam, fz["x"][1], fz["ground"])
        _, Zt = SD.unproject(cam, fz["x"][0], fz["top"], Y)
        mb = MB()
        m = mat(f"fence_{fz['colour']}", fz["colour"])
        n = max(3, int(abs(Xb - Xa) / 0.24))
        bw = abs(Xb - Xa) / n
        for i in range(n):
            x0 = min(Xa, Xb) + i * bw
            mb.box(x0 + 0.02, x0 + bw - 0.02, Y - 0.03, Y + 0.03, 0, Zt * (0.94 + 0.06 * (i % 2)), m)
        mb.box(min(Xa, Xb), max(Xa, Xb), Y + 0.03, Y + 0.09, Zt * 0.25, Zt * 0.33, m)
        mb.box(min(Xa, Xb), max(Xa, Xb), Y + 0.03, Y + 0.09, Zt * 0.7, Zt * 0.78, m)
        mb.obj(fz["key"], pc)
    G = SD.GROUND_COLOURS
    for mbx in SD.MAILBOXES:
        X, Y = ground_pt(cam, *mbx["foot"])
        _, Zt = SD.unproject(cam, mbx["foot"][0], mbx["top"], Y)
        Zt *= MAILBOX_TOP_K
        mb = MB()
        post = mat("post", G["post"])
        box = mat("mailbox", G["mailbox"])
        bh = Zt * MAILBOX_BOX_K
        bl = bh * MAILBOX_LEN_K
        bwid = bh * 0.85
        mb.box(X - 0.07, X + 0.07, Y - 0.07, Y + 0.07, 0, Zt - bh, post)
        # box with its door toward the street, a rounded top
        sg = 1 if X < 0 else -1
        xa, xb = X - bl / 2, X + bl / 2
        mb.box(xa, xb, Y - bwid / 2, Y + bwid / 2, Zt - bh, Zt - bwid / 2, box)
        n = 8
        for i in range(n):
            a0, a1 = math.pi * i / n, math.pi * (i + 1) / n
            y0, z0 = Y - bwid / 2 * math.cos(a0), Zt - bwid / 2 + bwid / 2 * math.sin(a0)
            y1, z1 = Y - bwid / 2 * math.cos(a1), Zt - bwid / 2 + bwid / 2 * math.sin(a1)
            mb.poly([(xa, y0, z0), (xb, y0, z0), (xb, y1, z1), (xa, y1, z1)], box)
            mb.poly([(xb, Y, Zt - bwid / 2), (xb, y1, z1), (xb, y0, z0)], box)
            mb.poly([(xa, Y, Zt - bwid / 2), (xa, y0, z0), (xa, y1, z1)], box)
        # flag on the side away from the camera
        fx = X - sg * bl * 0.15
        mb.box(fx - 0.02, fx + 0.02, Y + bwid / 2, Y + bwid / 2 + 0.04, Zt - bh * 0.6, Zt + bh * 0.45, mat("flag", G["flag"]))
        mb.obj(mbx["key"], pc)


# --------------------------------------------------------------------------
# Camera, light, scene
# --------------------------------------------------------------------------
def add_camera(cam, name="splash_camera"):
    c = bpy.data.cameras.new(name)
    c.sensor_fit = "HORIZONTAL"
    c.sensor_width = cam["sensor_mm"]
    c.lens = cam["lens_mm"]
    c.shift_x = cam["shift_x"]
    c.shift_y = cam["shift_y"]
    c.clip_start = 0.1
    c.clip_end = 1000
    o = bpy.data.objects.new(name, c)
    o.location = (0.0, 0.0, cam["h"])
    o.rotation_euler = (math.radians(90), 0.0, 0.0)
    bpy.context.scene.collection.objects.link(o)
    return o


def add_light():
    ld = bpy.data.lights.new("sun", "SUN")
    ld.energy = SUN_ENERGY
    ld.angle = math.radians(6)
    o = bpy.data.objects.new("sun", ld)
    # the sun object's -Z points along the light; aim it opposite LIGHT
    from mathutils import Vector
    o.rotation_euler = Vector(LIGHT).to_track_quat("Z", "Y").to_euler()
    bpy.context.scene.collection.objects.link(o)
    return o


def build(mode="render", focal_px=None, toe_in=0.0):
    """toe_in: 0 keeps every house square to the street; 1 turns each house about its front
    centre to face the splash camera (hides the street-side walls, bends the street's grid)."""
    bpy.ops.wm.read_factory_settings(use_empty=True)
    MODE["m"] = mode
    _MATS.clear()
    cam = SD.camera(focal_px)
    sc = bpy.context.scene
    root = sc.collection
    street = bpy.data.collections.new("street")
    root.children.link(street)
    build_ground(cam, street)
    build_props(cam, street)
    depths = house_depths(cam)
    houses = {}
    for h in SD.HOUSES:
        coll = bpy.data.collections.new(f"house_{h['key']}")
        root.children.link(coll)
        hroot = build_house(h, cam, coll, depths[h["key"]])
        if toe_in:
            hroot.rotation_euler.z = toe_in * math.atan2(-hroot.location.x, hroot.location.y)
        houses[h["key"]] = hroot
    camo = add_camera(cam)
    sc.camera = camo
    add_light()
    w = bpy.data.worlds.new("world")
    sc.world = w
    bg = w.node_tree.nodes.get("Background")
    if bg:
        bg.inputs[0].default_value = (0, 0, 0, 1)
    sc.render.resolution_x, sc.render.resolution_y = SD.W, SD.H
    sc.view_settings.view_transform = "Standard"
    sc.view_settings.look = "None"
    return cam, houses


if __name__ == "__main__":
    build("render")
    out = sys.argv[1] if len(sys.argv) > 1 else os.path.join(HERE, "out", "splash_street.blend")
    os.makedirs(os.path.dirname(out), exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=out)
    print("wrote", out)
