"""Road kit for every road tile type (#802; look A approved, #836): Blender meshes
built from dh_road_geom's shapes (exact Core geometry), on top of the
approved lawn (look C, #801/#834).

One ROAD mesh per tile, two materials (two draw calls):

  DH_Road_asphalt_<tone>   asphalt colour + normal on UV0 = world / 6.4 m,
                           the shared ground macro on UV1 = world / 211 m
                           (luminance only), x vertex colour COLOR_0, which is
                           white except on seeded patches (darker)
  DH_Road_markings_<fin>   the 512 x 128 markings atlas: lane paint, stop
                           lines, crosswalks, crack sealant AND the flat curb
                           band, all as thin strips cut into <= 1 m pieces

Sidewalks are built too, at their exact Core positions, but they belong to
#803: they use the lawn round's concrete (dh_ground.paving_materials) and are
a separate object that the road export leaves out.

Heights (metres above the lawn at z = 0), all flush within 1.5 cm:
asphalt 0.010 (dh_ground.ROAD_Z), patch 0.0115, crack 0.0125, sidewalk
0.012 (dh_ground.FLUSH_Z), curb 0.013, paint 0.0145. Paint, cracks and
patches are overlay geometry, not cut into the asphalt; see README.

Conventions as the rest of art-source/blender: metres, Z up, built in world
coordinates, Blender X = Core X (east), Blender Y = Core Z (north).
"""
import math
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
sys.path.insert(0, os.path.normpath(os.path.join(HERE, "..", "ground")))
import dh_ground as G  # noqa: E402  (puts ../fences, ../street-props, ../trees and the kit on the path)
import bpy  # noqa: E402
import dh_house_kit as K  # noqa: E402
import dh_ground_tex as GT  # noqa: E402
import dh_road_tex as RT  # noqa: E402
import dh_road_geom as R  # noqa: E402
import shapely  # noqa: E402

ROAD_Z = G.ROAD_Z               # 0.010
PATCH_Z = 0.0115
CRACK_Z = 0.0125
SIDEWALK_Z = G.FLUSH_Z          # 0.012
CURB_Z = 0.013                  # above a driveway apron (0.012), which it crosses
PAINT_Z = 0.0145
LAWN_LOOK, LAWN_APPROACH = "c", "rec"      # Derek's lawn pick
MACRO_STRENGTH = 0.6            # how much of the lawn macro's brightness swing the asphalt takes
STRIP_PAD = 4.0 / RT.MARK_SWATCH            # keep strip UVs this far inside a swatch (mip bleed)

LOOKS = {
    "a": {"label": "A. Fresh blacktop",
          "sub": "dark asphalt, clean paint, double yellow, continental bars",
          "asphalt": "fresh", "paint": "clean", "centre": "double", "crosswalk": "bars", "stop": True,
          "wear": None, "patch_tint": 1.0, "rough": 0.72},
    "b": {"label": "B. Weathered grey",
          "sub": "grey asphalt, worn paint, dashed yellow, two-line, cracks + patches",
          "asphalt": "worn", "paint": "worn", "centre": "dashed", "crosswalk": "two-line", "stop": True,
          "wear": {"patches": (1, 3), "cracks": (5, 10), "seed": 802}, "patch_tint": 0.70, "rough": 0.93},
    "c": {"label": "C. Quiet street",
          "sub": "charcoal asphalt, clean paint, no centre line, ladder, few patches",
          "asphalt": "mid", "paint": "clean", "centre": "none", "crosswalk": "ladder", "stop": True,
          "wear": {"patches": (0, 2), "cracks": (0, 3), "seed": 803}, "patch_tint": 0.84, "rough": 0.85},
}


# ---------------------------------------------------------------------------
# materials
# ---------------------------------------------------------------------------
def _img(nt, path, non_color=False):
    t = nt.nodes.new("ShaderNodeTexImage")
    t.image = bpy.data.images.load(path, check_existing=True)
    if non_color:
        t.image.colorspace_settings.name = "Non-Color"
    return t


def _mul(nt, a, b):
    m = nt.nodes.new("ShaderNodeMix")
    m.data_type = "RGBA"
    m.blend_type = "MULTIPLY"
    m.inputs["Factor"].default_value = 1.0
    nt.links.new(a, m.inputs[6])
    nt.links.new(b, m.inputs[7])
    return m.outputs[2]


def asphalt_material(look, macro=True):
    L = LOOKS[look]
    name = "DH_Road_asphalt_%s%s" % (L["asphalt"], "" if macro else "_export")
    mat = bpy.data.materials.get(name)
    if mat:
        return mat
    col, nrm = RT.need("asphalt_" + L["asphalt"])
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    nt = mat.node_tree
    bsdf = nt.nodes["Principled BSDF"]
    tc = _img(nt, col)
    tn = _img(nt, nrm, True)
    va = nt.nodes.new("ShaderNodeVertexColor")
    va.layer_name = "Col"
    out = _mul(nt, tc.outputs["Color"], va.outputs["Color"])
    if macro:
        uv1 = G._uvmap_node(nt, G.MACRO_UV)
        tm = _img(nt, GT.need("macro"), True)
        nt.links.new(uv1.outputs["UV"], tm.inputs["Vector"])
        bw = nt.nodes.new("ShaderNodeRGBToBW")
        nt.links.new(tm.outputs["Color"], bw.inputs[0])
        f = nt.nodes.new("ShaderNodeMath")            # 1 + s * (2 * lum - 1)
        f.operation = "MULTIPLY_ADD"
        f.inputs[1].default_value = 2.0 * MACRO_STRENGTH
        f.inputs[2].default_value = 1.0 - MACRO_STRENGTH
        nt.links.new(bw.outputs[0], f.inputs[0])
        cm = nt.nodes.new("ShaderNodeCombineColor")
        for i in range(3):
            nt.links.new(f.outputs[0], cm.inputs[i])
        out = _mul(nt, out, cm.outputs[0])
    nt.links.new(out, bsdf.inputs["Base Color"])
    nm = nt.nodes.new("ShaderNodeNormalMap")
    nt.links.new(tn.outputs["Color"], nm.inputs["Color"])
    nt.links.new(nm.outputs["Normal"], bsdf.inputs["Normal"])
    bsdf.inputs["Roughness"].default_value = L["rough"]
    if "Specular IOR Level" in bsdf.inputs:
        bsdf.inputs["Specular IOR Level"].default_value = 0.25
    mat.use_backface_culling = True
    return mat


def markings_material(look):
    fin = LOOKS[look]["paint"]
    name = "DH_Road_markings_%s" % fin
    mat = bpy.data.materials.get(name)
    if mat:
        return mat
    col, nrm = RT.need("markings_" + fin)
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    nt = mat.node_tree
    bsdf = nt.nodes["Principled BSDF"]
    tc = _img(nt, col)
    tn = _img(nt, nrm, True)
    nt.links.new(tc.outputs["Color"], bsdf.inputs["Base Color"])
    nm = nt.nodes.new("ShaderNodeNormalMap")
    nm.inputs["Strength"].default_value = 0.8
    nt.links.new(tn.outputs["Color"], nm.inputs["Color"])
    nt.links.new(nm.outputs["Normal"], bsdf.inputs["Normal"])
    bsdf.inputs["Roughness"].default_value = 0.6
    if "Specular IOR Level" in bsdf.inputs:
        bsdf.inputs["Specular IOR Level"].default_value = 0.3
    mat.use_backface_culling = True
    return mat


# ---------------------------------------------------------------------------
# mesh builder: shapely polygons and strips, world-space UVs
# ---------------------------------------------------------------------------
class RoadMesh:
    def __init__(self, name):
        self.name = name
        self.verts, self.faces, self.fmat, self.fuv, self.col = [], [], [], [], []
        self.mats = []

    def _m(self, key):
        if key not in self.mats:
            self.mats.append(key)
        return self.mats.index(key)

    def _face(self, pts, uvs, mat, col=(1.0, 1.0, 1.0, 1.0)):
        # orient up (+Z)
        a = sum(pts[i][0] * pts[(i + 1) % len(pts)][1] - pts[(i + 1) % len(pts)][0] * pts[i][1]
                for i in range(len(pts)))
        if abs(a) < 1e-12:
            return
        if a < 0:
            pts, uvs = pts[::-1], uvs[::-1]
        base = len(self.verts)
        self.verts.extend(pts)
        self.col.extend([col] * len(pts))
        self.faces.append(tuple(range(base, base + len(pts))))
        self.fmat.append(self._m(mat))
        self.fuv.append(uvs)

    def poly(self, geom, off, z, mat, uv_m, col=(1.0, 1.0, 1.0, 1.0)):
        """Triangulate a (Multi)Polygon (tile-local) and add it at height z,
        UV0 = world / uv_m."""
        if geom.is_empty:
            return
        for p in getattr(geom, "geoms", [geom]):
            if p.geom_type != "Polygon" or p.area < 1e-9:
                continue
            tris = shapely.constrained_delaunay_triangles(p)
            for t in tris.geoms:
                c = list(t.exterior.coords)[:3]
                pts = [(x + off[0], y + off[1], z) for x, y in c]
                self._face(pts, [(q[0] / uv_m, q[1] / uv_m) for q in pts], mat, col)

    def strip(self, line, width, off, z, mat, swatch, v0):
        """A strip of `width` centred on `line` (tile-local), cut at every
        whole metre of length so each piece samples one swatch's U range;
        mitred at interior vertices."""
        c = [tuple(p) for p in line.coords]
        c = [p for i, p in enumerate(c) if i == 0 or math.dist(p, c[i - 1]) > 1e-7]
        if len(c) < 2:
            return
        segs = []
        for i in range(len(c) - 1):
            dx, dy = c[i + 1][0] - c[i][0], c[i + 1][1] - c[i][1]
            L = math.hypot(dx, dy)
            segs.append((dx / L, dy / L, L))
        closed = math.dist(c[0], c[-1]) < 1e-7

        def normal_at(i):          # offset vector (already scaled for a mitre) at vertex i
            ins = [segs[i - 1]] if i > 0 else ([segs[-1]] if closed else [])
            outs = [segs[i]] if i < len(segs) else ([segs[0]] if closed else [])
            ns = [(-s[1], s[0]) for s in ins + outs]
            mx, my = sum(n[0] for n in ns), sum(n[1] for n in ns)
            m = math.hypot(mx, my)
            mx, my = mx / m, my / m
            k = 1.0 / max(0.25, mx * ns[0][0] + my * ns[0][1])
            return (mx * k, my * k)

        pts = []                   # (point, s, offset)
        s = 0.0
        for i in range(len(c) - 1):
            n_i = normal_at(i)
            pts.append((c[i], s, n_i))
            dx, dy, L = segs[i]
            seg_n = (-dy, dx)
            k = math.floor(s + 1e-9) + 1
            while k < s + L - 1e-6:
                t = k - s
                pts.append(((c[i][0] + dx * t, c[i][1] + dy * t), float(k), seg_n))
                k += 1
            s += L
        pts.append((c[-1], s, normal_at(len(c) - 1)))
        hw = width / 2
        vb = v0, v0 + width / RT.MARK_M
        for (pa, sa, na), (pb, sb, nb) in zip(pts, pts[1:]):
            if sb - sa < 1e-7:
                continue
            k = math.floor(sa + 1e-6)
            ua, ub = sa - k, sb - k
            ua, ub = (STRIP_PAD + u * (1 - 2 * STRIP_PAD) for u in (ua, ub))
            q = [(pa[0] - na[0] * hw, pa[1] - na[1] * hw), (pb[0] - nb[0] * hw, pb[1] - nb[1] * hw),
                 (pb[0] + nb[0] * hw, pb[1] + nb[1] * hw), (pa[0] + na[0] * hw, pa[1] + na[1] * hw)]
            uv = [RT.swatch_uv(swatch, ua, vb[0]), RT.swatch_uv(swatch, ub, vb[0]),
                  RT.swatch_uv(swatch, ub, vb[1]), RT.swatch_uv(swatch, ua, vb[1])]
            self._face([(x + off[0], y + off[1], z) for x, y in q], uv, mat)

    def build(self, materials, uv1=True):
        me = bpy.data.meshes.new(self.name)
        me.from_pydata(self.verts, [], self.faces)
        l0 = me.uv_layers.new(name="UVMap")
        l1 = me.uv_layers.new(name=G.MACRO_UV) if uv1 else None
        for fi, poly in enumerate(me.polygons):
            poly.material_index = self.fmat[fi]
            poly.use_smooth = False
            for k in range(poly.loop_total):
                li = poly.loop_start + k
                l0.data[li].uv = self.fuv[fi][k]
                if l1:
                    vx, vy, _ = self.verts[me.loops[li].vertex_index]
                    l1.data[li].uv = (vx / GT.MACRO_M, vy / GT.MACRO_M)
        ca = me.color_attributes.new("Col", "BYTE_COLOR", "POINT")
        for i, c in enumerate(self.col):
            ca.data[i].color = c
        me.color_attributes.active_color = ca
        me.color_attributes.render_color_index = 0
        for key in self.mats:
            me.materials.append(materials[key])
        me.update()
        # merge coincident vertices (same position, UVs stay per loop)
        import bmesh
        bm = bmesh.new()
        bm.from_mesh(me)
        bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-6)
        bm.to_mesh(me)
        bm.free()
        ob = bpy.data.objects.new(self.name, me)
        bpy.context.scene.collection.objects.link(ob)
        return ob


def _v0(rng_key, width):
    """A deterministic V offset for one strip inside its swatch."""
    import zlib
    h = (zlib.crc32(rng_key.encode()) & 0xFFFF) / 0xFFFF
    lo, hi = STRIP_PAD, 1.0 - STRIP_PAD - width / RT.MARK_M
    return lo + (hi - lo) * h


def tile_marks(lay, look):
    """[(LineString, width, swatch)] for every painted marking of a tile."""
    L = LOOKS[look]
    out = R.centre_lines(lay, L["centre"]) + R.crosswalk_lines(lay, L["crosswalk"])
    if L["stop"]:
        out += R.stop_lines(lay)
    return out


def build_tile(ttype, coord, look, centre=None, sidewalks=True, export=False):
    """The road (and, unless sidewalks=False, the #803 sidewalk context) for
    one tile at grid coordinate `coord`. Returns {"road": ob, "sidewalk": ob,
    "layout": Layout, "counts": {...}}."""
    lay = R.Layout(ttype)
    out = {"layout": lay, "road": None, "sidewalk": None, "counts": {}}
    if lay.kind == "green":
        return out
    off = centre if centre is not None else R.tile_centre(coord)
    L = LOOKS[look]
    m = RoadMesh("road_%s_%s_%d_%d" % (look, ttype, coord[0], coord[1]))
    m.poly(lay.asphalt, off, ROAD_Z, "asphalt", RT.ASPHALT_M)
    patches, cracks = R.features(lay, coord, L["wear"])
    tint = L["patch_tint"]
    for p in patches:
        m.poly(p, off, PATCH_Z, "asphalt", RT.ASPHALT_M, col=(tint, tint, tint, 1.0))
    for i, (ln, w) in enumerate(cracks):
        m.strip(ln, w, off, CRACK_Z, "markings", "sealant", _v0("crack%d" % i, w))
    for i, ln in enumerate(lay.curb_line):
        m.strip(ln, R.CURB_W, off, CURB_Z, "markings", "curb", _v0("curb%d" % i, R.CURB_W))
    marks = tile_marks(lay, look)
    for i, (ln, w, sw) in enumerate(marks):
        m.strip(ln, w, off, PAINT_Z, "markings", sw, _v0("%s%d" % (ttype, i), w))
    mats = {"asphalt": asphalt_material(look, macro=not export), "markings": markings_material(look)}
    out["road"] = m.build(mats)
    out["counts"] = {"patches": len(patches), "cracks": len(cracks), "marks": len(marks),
                     "tris": K.tri_count(out["road"])}
    out["marks"] = marks
    out["patches"], out["cracks"] = patches, cracks
    if sidewalks:
        s = RoadMesh("sidewalk_%s_%d_%d" % (ttype, coord[0], coord[1]))
        s.poly(lay.sidewalk, off, SIDEWALK_Z, "sidewalk", GT.CONCRETE_M)
        out["sidewalk"] = s.build({"sidewalk": G.paving_materials()["sidewalk"]}, uv1=False)
    return out


def build_area(tiles, look, lawn=True, origin=(0.0, 0.0)):
    """tiles: {(col, row): type}. Lawn under every tile, roads on top."""
    objs, info = [], {}
    for (c, r), t in tiles.items():
        cx, cy = origin[0] + c * R.TILE, origin[1] + r * R.TILE
        if lawn:
            objs.append(G.lawn_tile(LAWN_LOOK, LAWN_APPROACH, cx - R.HALF, cy - R.HALF))
        res = build_tile(t, (c, r), look, centre=(cx, cy))
        info[(c, r)] = res
        objs += [o for o in (res["road"], res["sidewalk"]) if o is not None]
    return objs, info


def starter_scene(look, fence_style="splitrail"):
    """The approved starter lot (ranch, base 2, level 4, fence style C, lawn
    look C) with the road kit replacing the lawn round's placeholder
    streets. The starter FourWay plus its neighbours (STARTER)."""
    sc = G.lot_scene(fence_style=fence_style)
    for o in sc["streets"]:
        bpy.data.objects.remove(o)
    cx, cy = sc["tile_centre"]
    objs, info = build_area(R.STARTER, look, origin=(cx, cy))
    sc["road_objs"], sc["road_info"] = objs, info
    return sc
