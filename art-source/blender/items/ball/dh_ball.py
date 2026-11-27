"""The dog ball (#333): a tennis-style ball, one mesh, one textured material.

Conventions (docs/engineering/blender-pipeline.md): metres, Z up, the origin on the ground at the
ball's footprint centre, so it rests on whatever surface it is placed at. The model is round, so
it has no front. Object `ball`; one material per colour variant, `DH_Item_Ball_<Variant>` (like the
dogs' `DH_Dog_Fur_<Coat>`), each with its own colour map and the shared normal map.

The mesh is a UV sphere of SEGMENTS x RINGS (triangle fans at the poles), with radial smooth
normals and equirectangular UVs that match dh_ball_tex: U = longitude / 2 pi, V = (latitude +
pi / 2) / pi. All the surface detail (the felt nap, the mottling and the curved seam with its
groove) is in the colour and normal maps.
"""
import math
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
sys.path.insert(0, os.path.normpath(os.path.join(HERE, "..", "..")))   # the house kit (art-source/blender)
import bpy  # noqa: E402
import dh_ball_tex as BT  # noqa: E402

NAME = "ball"
MATERIAL = "DH_Item_Ball_%s"     # % the variant, capitalised
SEGMENTS = 40                  # around the equator
RINGS = 20                     # pole to pole
ROUGHNESS = 0.92               # felt
METALLIC = 0.0
NORMAL_MAP_STRENGTH = 1.0


def material_name(key):
    return MATERIAL % key.capitalize()


def material(tex, name):
    """The felt material from tex = {"color": path, "normal": path}."""
    mat = bpy.data.materials.get(name)
    if mat:
        return mat
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    nt = mat.node_tree
    bsdf = nt.nodes["Principled BSDF"]
    tc = nt.nodes.new("ShaderNodeTexImage")
    tc.image = bpy.data.images.load(tex["color"], check_existing=True)
    tn = nt.nodes.new("ShaderNodeTexImage")
    tn.image = bpy.data.images.load(tex["normal"], check_existing=True)
    tn.image.colorspace_settings.name = "Non-Color"
    nm = nt.nodes.new("ShaderNodeNormalMap")
    nm.inputs["Strength"].default_value = NORMAL_MAP_STRENGTH
    nt.links.new(tc.outputs["Color"], bsdf.inputs["Base Color"])
    nt.links.new(tn.outputs["Color"], nm.inputs["Color"])
    nt.links.new(nm.outputs["Normal"], bsdf.inputs["Normal"])
    bsdf.inputs["Roughness"].default_value = ROUGHNESS
    bsdf.inputs["Metallic"].default_value = METALLIC
    mat.use_backface_culling = True               # closed: single-sided in glTF
    return mat


def _sphere():
    """(verts, faces, per-face loop UVs, per-vertex unit normals) of the unit UV sphere."""
    verts, normals = [], []
    lats = [-math.pi / 2 + math.pi * i / RINGS for i in range(RINGS + 1)]
    lons = [2 * math.pi * j / SEGMENTS for j in range(SEGMENTS)]
    index = {}
    for i, la in enumerate(lats):
        cols = [0] if i in (0, RINGS) else range(SEGMENTS)
        for j in cols:
            n = (math.cos(la) * math.cos(lons[j]), math.cos(la) * math.sin(lons[j]), math.sin(la))
            index[i, j] = len(verts)
            verts.append(n)
            normals.append(n)

    def vid(i, j):
        return index[i, 0] if i in (0, RINGS) else index[i, j % SEGMENTS]

    def uv(i, j):
        return (j / SEGMENTS, i / RINGS)

    faces, uvs = [], []
    for j in range(SEGMENTS):
        faces.append((vid(0, 0), vid(1, j + 1), vid(1, j)))          # south cap, outward winding
        uvs.append((((j + 0.5) / SEGMENTS, 0.0), uv(1, j + 1), uv(1, j)))
        for i in range(1, RINGS - 1):
            faces.append((vid(i, j), vid(i, j + 1), vid(i + 1, j + 1), vid(i + 1, j)))
            uvs.append((uv(i, j), uv(i, j + 1), uv(i + 1, j + 1), uv(i + 1, j)))
        faces.append((vid(RINGS - 1, j), vid(RINGS - 1, j + 1), vid(RINGS, 0)))   # north cap
        uvs.append((uv(RINGS - 1, j), uv(RINGS - 1, j + 1), ((j + 0.5) / SEGMENTS, 1.0)))
    return verts, faces, uvs, normals


def build(key, tex=None, collection=None):
    """Build the ball in colour variant key (a key of dh_ball_tex.VARIANTS) and link it into
    collection (default: the scene). tex is dh_ball_tex.paths() (default: this folder's
    textures/). Returns the object."""
    pair = BT.variant(tex or BT.paths(), key)
    r = BT.DIAMETER_M / 2
    verts, faces, uvs, normals = _sphere()
    me = bpy.data.meshes.new(NAME)
    me.from_pydata([(x * r, y * r, z * r + r) for x, y, z in verts], [], faces)
    uvl = me.uv_layers.new(name="UVMap")
    for poly, fuv in zip(me.polygons, uvs):
        for li, u in zip(poly.loop_indices, fuv):
            uvl.data[li].uv = u
    me.shade_smooth()
    me.normals_split_custom_set_from_vertices(normals)
    me.materials.append(material(pair, material_name(key)))
    ob = bpy.data.objects.new(NAME, me)
    (collection or bpy.context.scene.collection).objects.link(ob)
    return ob
