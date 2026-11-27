"""The dog bed (#411): a padded bolster bed with a flat fleece cushion, built from DIMS.

Conventions (docs/engineering/blender-pipeline.md): metres at in-game size, Z up, the front
(the low step-in edge of the bolster) faces -Y, the long side runs along X like today's
graybox slab (DecorationView, 1.4 x 0.35 x 1.0 m). The pivot is on the ground at the
footprint centre. One mesh object, `dog_bed`, plus one empty:

  ATTACH_REST   on the cushion top at its centre (0, 0, DIMS["rest_z"]): where a resting dog
                lies. Parented to the bed, so it exports as a glTF node.

Parts (all in the one mesh):
  base      a low padded base under everything, the full footprint, rounded top edge
  bolster   a puffy tube swept round the rim; its top dips at the front so a dog steps in
  cushion   the flat padded top a dog lies on, tucked under the bolster on every side

Materials: DH_Item_Bed_Cover (bolster) and DH_Item_Bed_Base (base) share the neutral canvas map,
each multiplied by its palette colour (glTF baseColorFactor); DH_Item_Bed_Cushion is the cream
fleece, never tinted. UVs are in metres / TILE, so the weave keeps one size everywhere.

  build() -> the dog_bed object (in the current scene)
"""
import math
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
# The committed house kit (art-source/blender): reset_scene, attach_point, hex_rgba, tri_count.
sys.path.insert(0, os.path.normpath(os.path.join(HERE, "..", "..")))
import bpy  # noqa: E402
import check_bed as C  # noqa: E402
import dh_house_kit as K  # noqa: E402

NAME = "dog_bed"
REST_NAME = "ATTACH_REST"

DIMS = {
    "w": 1.60,              # outer footprint along X (Derek on #411: ~15% over the slab's 1.4 m)
    "d": 1.15,              # outer footprint along Y (the slab's 1.0 m, ~15% bigger)
    "corner_r": 0.30,       # plan corner radius of the outer outline
    "base_h": 0.075,        # base pad thickness
    "base_edge_r": 0.025,   # base pad top edge rounding (its bottom edge sits on the ground, square)
    "bolster_w": 0.20,      # bolster cross-section width
    "bolster_sink": 0.015,  # bolster bottom sits this far into the base
    "back_top": 0.37,       # bolster top at the back and sides
    "front_top": 0.25,      # bolster top at the front dip
    "dip_start": 0.35,      # front dip begins where -sin(angle round the bed) passes this
    "bolster_round": 2.6,   # bolster cross-section superellipse exponent (2 = ellipse)
    "rest_z": 0.18,         # cushion top: where the dog lies
    "cushion_tuck": 0.035,  # cushion runs this far under the bolster on each side
    "cushion_edge_r": 0.05, # cushion top edge rounding (the puffy edge)
}

PLAN_SEGMENTS = 56          # points round the bolster's path
SLAB_PLAN_SEGMENTS = 48     # points round the base and cushion outlines
CORNER_WEIGHT = 2.0         # corners get this many times the points per metre of the straights
PROFILE_SEGMENTS = 10       # points round the bolster cross-section
EDGE_SEGMENTS = 2           # segments per rounded slab edge
TILE = 0.30                 # metres per texture repeat

# The cover colours (Derek on #411: "make the color dynamic", ~10 colours). The cover and base maps
# are neutral, so a bed's colour is its materials' base colour (glTF baseColorFactor): the cover
# takes the hex, the base takes it darkened by check_bed.BASE_DARKEN. The cushion is fixed cream.
# export_bed.py writes this list to bed_colours.json; the export itself is in DEFAULT_COLOUR.
PALETTE = (                 # (name, cover hex), in sheet order
    ("Heather Plum", "#6E5B82"),
    ("Denim Blue", "#4A7AAD"),
    ("Dove Grey", "#A9ADB2"),
    ("Charcoal", "#3F4248"),
    ("Rust", "#B5603A"),
    ("Cranberry", "#8E2F45"),
    ("Mustard", "#C9A03E"),
    ("Dusty Rose", "#C48A8F"),
    ("Teal", "#2F7A7E"),
    ("Cocoa", "#6B4A35"),
)
DEFAULT_COLOUR = "Heather Plum"
SLOTS = {                   # material: (texture, palette slot or None for untinted, roughness)
    "DH_Item_Bed_Cover": ("cover", "cover", 0.92),
    "DH_Item_Bed_Base": ("cover", "base", 0.95),
    "DH_Item_Bed_Cushion": ("fleece", None, 0.98),
}
NORMAL_STRENGTH = 1.0
TEX_DIR = os.path.join(HERE, "textures")


# ---------------------------------------------------------------------------
# outline
# ---------------------------------------------------------------------------
def rrect(w, d, r, n):
    """n points round a rounded rectangle centred on the origin, starting at the front centre
    (0, -d/2) and running counter-clockwise, evenly spaced along the outline with the corners
    weighted by CORNER_WEIGHT. Each point is (x, y, nx, ny, s): position, outward normal, arc
    length from the start. Returns (points, total arc length)."""
    hx, hy = w / 2 - r, d / 2 - r
    # pieces in order from the front centre: (kind, length, data)
    pieces = [("line", hx, ((0, -d / 2), (1, 0))),
              ("arc", math.pi / 2 * r, ((hx, -hy), -math.pi / 2)),
              ("line", 2 * hy, ((w / 2, -hy), (0, 1))),
              ("arc", math.pi / 2 * r, ((hx, hy), 0.0)),
              ("line", 2 * hx, ((hx, d / 2), (-1, 0))),
              ("arc", math.pi / 2 * r, ((-hx, hy), math.pi / 2)),
              ("line", 2 * hy, ((-w / 2, hy), (0, -1))),
              ("arc", math.pi / 2 * r, ((-hx, -hy), math.pi)),
              ("line", hx, ((-hx, -d / 2), (1, 0)))]
    total = sum(p[1] for p in pieces)
    wlen = [ln * (CORNER_WEIGHT if kind == "arc" else 1.0) for kind, ln, _ in pieces]
    wtotal = sum(wlen)
    pts = []
    for i in range(n):
        q = wtotal * i / n          # position in weighted length
        acc, s0 = 0.0, 0.0
        for k, ((kind, ln, data), wl) in enumerate(zip(pieces, wlen)):
            if q <= acc + wl or k == len(pieces) - 1:
                t = (q - acc) / wl * ln if wl > 0 else 0.0
                s = s0 + t
                if kind == "line":
                    (x0, y0), (dx, dy) = data
                    pts.append((x0 + dx * t, y0 + dy * t, dy, -dx, s))
                else:
                    (cx, cy), a0 = data
                    a = a0 + (t / r if r > 0 else 0.0)
                    pts.append((cx + r * math.cos(a), cy + r * math.sin(a), math.cos(a), math.sin(a), s))
                break
            acc += wl
            s0 += ln
    return pts, total


def _spow(v, e):
    return math.copysign(abs(v) ** e, v)


def _smooth(t):
    t = max(0.0, min(1.0, t))
    return t * t * (3 - 2 * t)


# ---------------------------------------------------------------------------
# mesh accumulator
# ---------------------------------------------------------------------------
class Builder:
    def __init__(self):
        self.verts, self.faces, self.mats, self.uvs = [], [], [], []
        self.mat_names = list(SLOTS)

    def v(self, co):
        self.verts.append(tuple(co))
        return len(self.verts) - 1

    def face(self, idx, mat, uvs):
        self.faces.append(tuple(idx))
        self.mats.append(self.mat_names.index(mat))
        self.uvs.append(tuple(uvs))

    def mesh(self, name):
        me = bpy.data.meshes.new(name)
        me.from_pydata(self.verts, [], self.faces)
        uv = me.uv_layers.new(name="UVMap")
        li = 0
        for p, mi, fuv in zip(me.polygons, self.mats, self.uvs):
            p.material_index = mi
            p.use_smooth = True
            for k in range(p.loop_total):
                uv.data[p.loop_start + k].uv = fuv[k]
            li += p.loop_total
        me.validate()
        return me


def rounded_slab(b, mat, w, d, corner_r, z0, z1, r_top, inset=0.0):
    """A slab over a rounded-rectangle plan from z0 to z1: a straight side, the top edge rounded
    by r_top, and a flat fan cap on top. It has no bottom: the base sits on the ground and the
    cushion inside the base, so a bottom would never be seen. inset shrinks the plan on every side."""
    plan, perim = rrect(w - 2 * inset, d - 2 * inset, max(corner_r - inset, 0.0), SLAB_PLAN_SEGMENTS)
    # cross-section from the bottom edge to the top cap edge: (inward offset, z)
    prof = [(0.0, z0)]
    for k in range(EDGE_SEGMENTS + 1):
        a = math.pi / 2 * k / EDGE_SEGMENTS
        prof.append((r_top * (1 - math.cos(a)), z1 - r_top + r_top * math.sin(a)))
    zarc = [0.0]
    for k in range(1, len(prof)):
        zarc.append(zarc[-1] + math.hypot(prof[k][0] - prof[k - 1][0], prof[k][1] - prof[k - 1][1]))
    rings = []
    for off, z in prof:
        rings.append([b.v((x - nx * off, y - ny * off, z)) for x, y, nx, ny, s in plan])
    n = len(plan)
    for k in range(len(prof) - 1):
        for i in range(n):
            j = (i + 1) % n
            s0, s1 = plan[i][4], (plan[j][4] if j else perim)
            b.face((rings[k][i], rings[k][j], rings[k + 1][j], rings[k + 1][i]), mat,
                   [(s0 / TILE, zarc[k] / TILE), (s1 / TILE, zarc[k] / TILE),
                    (s1 / TILE, zarc[k + 1] / TILE), (s0 / TILE, zarc[k + 1] / TILE)])

    def cap(ring, z, up):
        c = b.v((0.0, 0.0, z))
        for i in range(n):
            j = (i + 1) % n
            pi, pj = b.verts[ring[i]], b.verts[ring[j]]
            tri = (c, ring[i], ring[j]) if up else (c, ring[j], ring[i])
            co = {c: (0.0, 0.0), ring[i]: pi[:2], ring[j]: pj[:2]}
            b.face(tri, mat, [(co[t][0] / TILE, co[t][1] / TILE) for t in tri])
    cap(rings[-1], z1, True)


def bolster(b, mat):
    D = DIMS
    bw = D["bolster_w"]
    path, perim = rrect(D["w"] - bw, D["d"] - bw, D["corner_r"] - bw / 2, PLAN_SEGMENTS)
    z0 = D["base_h"] - D["bolster_sink"]
    e = 2.0 / D["bolster_round"]
    n, m = len(path), PROFILE_SEGMENTS
    rings, varc = [], None
    for x, y, nx, ny, s in path:
        ang = math.atan2(y / D["d"], x / D["w"])
        dip = _smooth((-math.sin(ang) - D["dip_start"]) / (1 - D["dip_start"]))
        top = D["back_top"] - (D["back_top"] - D["front_top"]) * dip
        zc, hz = (z0 + top) / 2, (top - z0) / 2
        ring, sec = [], []
        for k in range(m):
            a = 2 * math.pi * k / m
            off = bw / 2 * _spow(math.cos(a), e)
            z = zc + hz * _spow(math.sin(a), e)
            sec.append((off, z))
            ring.append(b.v((x + nx * off, y + ny * off, z)))
        if varc is None:   # V along the cross-section, measured once at the start (the front)
            varc = [0.0]
            for k in range(1, m + 1):
                p, q = sec[k - 1], sec[k % m]
                varc.append(varc[-1] + math.hypot(q[0] - p[0], q[1] - p[1]))
        rings.append(ring)
    for i in range(n):
        j = (i + 1) % n
        s0, s1 = path[i][4], (path[j][4] if j else perim)
        for k in range(m):
            k1 = (k + 1) % m
            b.face((rings[i][k], rings[j][k], rings[j][k1], rings[i][k1]), mat,
                   [(s0 / TILE, varc[k] / TILE), (s1 / TILE, varc[k] / TILE),
                    (s1 / TILE, varc[k + 1] / TILE), (s0 / TILE, varc[k + 1] / TILE)])


# ---------------------------------------------------------------------------
# materials
# ---------------------------------------------------------------------------
def palette():
    """[{name, cover, base}] in PALETTE order; base is the cover darkened by BASE_DARKEN."""
    return [{"name": n, "cover": h, "base": C.darken(h)} for n, h in PALETTE]


def colour(name):
    return [c for c in palette() if c["name"] == name][0]


def materials(tex_dir=TEX_DIR, colour_name=DEFAULT_COLOUR):
    """The three materials, tinted with a palette colour. The default colour keeps the plain
    names (the export); any other colour gets its own copies, suffixed with the colour, so beds
    of different colours can share a scene (the review renders)."""
    col = colour(colour_name)
    out = []
    for slot_name, (tex, slot, rough) in SLOTS.items():
        tint = col[slot] if slot else None
        own = slot and colour_name != DEFAULT_COLOUR
        name = "%s_%s" % (slot_name, colour_name.replace(" ", "")) if own else slot_name
        mat = bpy.data.materials.get(name)
        if mat is None:
            mat = bpy.data.materials.new(name)
            mat.use_nodes = True
            nt = mat.node_tree
            bsdf = nt.nodes["Principled BSDF"]
            tc = nt.nodes.new("ShaderNodeTexImage")
            tc.image = bpy.data.images.load(os.path.join(tex_dir, "bed_%s_color.png" % tex), check_existing=True)
            tn = nt.nodes.new("ShaderNodeTexImage")
            tn.image = bpy.data.images.load(os.path.join(tex_dir, "bed_%s_normal.png" % tex), check_existing=True)
            tn.image.colorspace_settings.name = "Non-Color"
            nm = nt.nodes.new("ShaderNodeNormalMap")
            nm.inputs["Strength"].default_value = NORMAL_STRENGTH
            nt.links.new(tn.outputs["Color"], nm.inputs["Color"])
            nt.links.new(nm.outputs["Normal"], bsdf.inputs["Normal"])
            if tint:
                mix = nt.nodes.new("ShaderNodeMix")
                mix.data_type = "RGBA"
                mix.blend_type = "MULTIPLY"
                mix.inputs["Factor"].default_value = 1.0
                mix.inputs[7].default_value = K.hex_rgba(tint)
                nt.links.new(tc.outputs["Color"], mix.inputs[6])
                nt.links.new(mix.outputs[2], bsdf.inputs["Base Color"])
            else:
                nt.links.new(tc.outputs["Color"], bsdf.inputs["Base Color"])
            bsdf.inputs["Roughness"].default_value = rough
            if "Specular IOR Level" in bsdf.inputs:
                bsdf.inputs["Specular IOR Level"].default_value = 0.2
            mat.use_backface_culling = True
        out.append(mat)
    return out


# ---------------------------------------------------------------------------
def build(tex_dir=TEX_DIR, colour_name=DEFAULT_COLOUR):
    if not os.path.isfile(os.path.join(tex_dir, "bed_cover_color.png")):
        import dh_bed_tex
        dh_bed_tex.build_all(tex_dir)
    D = DIMS
    b = Builder()
    rounded_slab(b, "DH_Item_Bed_Base", D["w"], D["d"], D["corner_r"], 0.0, D["base_h"], D["base_edge_r"])
    bolster(b, "DH_Item_Bed_Cover")
    inset = D["bolster_w"] - D["cushion_tuck"]
    rounded_slab(b, "DH_Item_Bed_Cushion", D["w"], D["d"], D["corner_r"], D["base_h"] - D["bolster_sink"],
                 D["rest_z"], D["cushion_edge_r"], inset=inset)
    me = b.mesh(NAME)
    for mat in materials(tex_dir, colour_name):
        me.materials.append(mat)
    ob = bpy.data.objects.new(NAME, me)
    bpy.context.scene.collection.objects.link(ob)
    K.attach_point(REST_NAME, (0.0, 0.0, D["rest_z"]), parent=ob)
    bpy.context.view_layer.update()
    return ob
