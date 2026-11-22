"""Doggiehood house kit: shared helpers for scripted Blender houses.

Every base house and upgrade piece is generated from dimensions by these
helpers, so a base's FRAMING and FINISHED versions are built from the very
same numbers (footprint, pivot, floor height, wall heights, roof pitch) and
line up exactly.

Conventions (concept stage, see art-source/blender/README.md):
  * 1 Blender unit = 1 m at in-game size (no KitScale factor).
  * Pivot = centre of the wall footprint, on the ground (z = 0).
  * Front door faces -Y (the street). +X is the addition side, +Y the back.
  * Walls are described by their OUTER face line, listed counter-clockwise
    seen from above, so the outward normal of a wall a->b is (dy, -dx).
"""

import math
import os

import bmesh
import bpy
from mathutils import Vector

# ---------------------------------------------------------------------------
# Shared dimensions (metres, in-game size)
# ---------------------------------------------------------------------------

SLAB_TOP = 0.35          # top of the concrete slab / foundation
SLAB_OVERHANG = 0.15     # slab sticks out past the wall outer face
FLOOR_Z = 0.65           # finished floor = top of subfloor
WALL_T = 0.22            # finished wall thickness
SUBFLOOR_T = 0.05

# Framing lumber (chunky on purpose so it reads from the game camera)
STUD_W = 0.12            # along the wall
STUD_D = 0.16            # through the wall
STUD_SPACING = 0.70
PLATE_H = 0.10
HEADER_H = 0.24
JOIST_W = 0.12
JOIST_SPACING = 0.70
RAFTER_W = 0.12
RAFTER_H = 0.22
RAFTER_SPACING = 0.70
RAFTER_TAIL = 0.30       # framing rafters stick out this far past the wall

# Finished roof
ROOF_T = 0.16
EAVE_OVERHANG = 0.40     # horizontal, at the eaves
RAKE_OVERHANG = 0.35     # at gable ends
RIDGE_CAP_W = 0.42
RIDGE_CAP_H = 0.20
SHINGLE_COURSES = 4      # raised course lines per roof plane (breaks up big flat roofs)
COURSE_W = 0.12
COURSE_H = 0.05
SEAM_SPACING = 0.50      # standing-seam metal roofs
SEAM_W = 0.05
SEAM_H = 0.06

# Trim
TRIM_W = 0.12
TRIM_OUT = 0.06          # how far trim stands proud of the wall
SKIRT_H = 0.30           # water-table band at the bottom of the siding

# ---------------------------------------------------------------------------
# Palette: bright & saturated (docs/specs/world/art-style.md)
# ---------------------------------------------------------------------------

COMMON = {
    "concrete": "#C9C3B8",
    "trim": "#FFF7EA",
    "glass": "#8FDcff",
    "metal": "#F2C14E",
    "framing": "#EFC27A",      # light raw wood
    "framing_dark": "#D29A55",  # plates / rim joists / subfloor sheets
    "subfloor": "#E2B271",
    "brick": "#C0533A",
    "flower_a": "#FF4F9A",
    "flower_b": "#FFD23F",
    "leaf": "#3FB44F",
    "deck": "#C27B45",
    "deck_dark": "#9E5F33",
    "stone_a": "#F4BE84",
    "stone_b": "#E8925A",
    "stone_c": "#EADCC6",
    "grass": "#7ED957",        # Palette.GrassHex
    "road": "#8A8FA3",         # Palette.StreetHex
    "sidewalk": "#EFE8D8",     # Palette.SidewalkHex
    "lot_line": "#FF4FD8",
    "fence_line": "#FFFFFF",
    "dog": "#B8743A",
    "ground_studio": "#E6ECE8",
}


# ---------------------------------------------------------------------------
# House colour schemes: the ONE place they are defined. Every base and every
# upgrade piece paints itself only through these slots, so a piece always
# matches the house it is attached to. The game assigns one scheme per house
# at random for neighbourhood variety.
#
# Slots (hex):
#   siding      main wall colour            roof       roof slabs
#   trim        window/door frames, fascia, corner boards, deck railing
#   shutter     window shutters             door       front + back doors
#   chimney     chimney masonry             flowerbox  window-box planter
#   flower_a/b  the two flower colours in the window boxes
#   deck        deck boards and stairs      patio_a/b  the two paver tones
# Derived (not stored): roof_cap/course lines, door_panel and deck_dark are
# the slot colour darkened by DERIVED_DARKEN.
# ---------------------------------------------------------------------------

DERIVED_DARKEN = 0.80

# Round 1 (candy-bright). SUPERSEDED: rejected in favour of round 2
# (COLOR_SCHEMES below, approved 2026-11-21, #814). Kept only for comparison /
# revert and not written to colour-schemes.json; roof_style 'courses'.
COLOR_SCHEMES_BRIGHT = [
    {"name": "Sunny Cottage", "siding": "#FFC93C", "roof": "#E5483A", "trim": "#FFF7EA",
     "shutter": "#1FB5A8", "door": "#14A89E", "chimney": "#C0533A", "flowerbox": "#FFF7EA",
     "flower_a": "#FF4F9A", "flower_b": "#FF8A3D", "deck": "#C27B45",
     "patio_a": "#F4BE84", "patio_b": "#E8925A"},
    {"name": "Bluebird", "siding": "#5CC3F2", "roof": "#2F4FA8", "trim": "#FFFFFF",
     "shutter": "#FFC93C", "door": "#FF5A4A", "chimney": "#C76B4A", "flowerbox": "#FFFFFF",
     "flower_a": "#FFD23F", "flower_b": "#FF4F9A", "deck": "#B9824F",
     "patio_a": "#E9E3D6", "patio_b": "#B9C3CC"},
    {"name": "Mint Julep", "siding": "#7FE3B8", "roof": "#D9466F", "trim": "#FFFFFF",
     "shutter": "#2E9E78", "door": "#FF8FB8", "chimney": "#B9563E", "flowerbox": "#2E9E78",
     "flower_a": "#FF4F9A", "flower_b": "#FFE45C", "deck": "#C9915E",
     "patio_a": "#F2D7C9", "patio_b": "#E3A3A8"},
    {"name": "Coral Reef", "siding": "#FF7F66", "roof": "#1E8E8A", "trim": "#FFF7EA",
     "shutter": "#FFF7EA", "door": "#FFD23F", "chimney": "#9C4A36", "flowerbox": "#1E8E8A",
     "flower_a": "#FFD23F", "flower_b": "#FFFFFF", "deck": "#B87445",
     "patio_a": "#EADBC4", "patio_b": "#9ED6CF"},
    {"name": "Lavender Fields", "siding": "#B89DF5", "roof": "#4B3A8C", "trim": "#FFFFFF",
     "shutter": "#4B3A8C", "door": "#FFC93C", "chimney": "#A85A48", "flowerbox": "#FFFFFF",
     "flower_a": "#FF6FB5", "flower_b": "#FFE45C", "deck": "#A8836A",
     "patio_a": "#E6DCF2", "patio_b": "#C3B2E0"},
    {"name": "Peach Cobbler", "siding": "#FFB27A", "roof": "#7A4128", "trim": "#FFF7EA",
     "shutter": "#3FA7D6", "door": "#3FA7D6", "chimney": "#B5523B", "flowerbox": "#FFF7EA",
     "flower_a": "#FF4F9A", "flower_b": "#FFFFFF", "deck": "#C27B45",
     "patio_a": "#F6D2A8", "patio_b": "#D98F63"},
    {"name": "Picket White", "siding": "#F6F3EA", "roof": "#2F8A57", "trim": "#FFFFFF",
     "shutter": "#2F4FA8", "door": "#E5483A", "chimney": "#B8483A", "flowerbox": "#2F4FA8",
     "flower_a": "#FF4F9A", "flower_b": "#FFD23F", "deck": "#B98454",
     "patio_a": "#E0DCD2", "patio_b": "#C9A27E"},
    {"name": "Sage & Clay", "siding": "#A9D88B", "roof": "#C9562C", "trim": "#FFF7EA",
     "shutter": "#FFF7EA", "door": "#F2A93B", "chimney": "#8F4A33", "flowerbox": "#C9562C",
     "flower_a": "#FFE45C", "flower_b": "#FF7A59", "deck": "#B57A4A",
     "patio_a": "#EFC9A4", "patio_b": "#D7835A"},
    {"name": "Butter & Blue", "siding": "#FFE680", "roof": "#3F7FD9", "trim": "#FFFFFF",
     "shutter": "#3F7FD9", "door": "#E5483A", "chimney": "#B9563E", "flowerbox": "#FFFFFF",
     "flower_a": "#FF4F9A", "flower_b": "#3F7FD9", "deck": "#C99055",
     "patio_a": "#F3E3B5", "patio_b": "#A9C5EA"},
    {"name": "Teal Harbor", "siding": "#2DB6B0", "roof": "#F29B30", "trim": "#FFFFFF",
     "shutter": "#FFFFFF", "door": "#FF5E57", "chimney": "#A9503A", "flowerbox": "#FFFFFF",
     "flower_a": "#FFE45C", "flower_b": "#FF5E57", "deck": "#B07A52",
     "patio_a": "#F2E2C4", "patio_b": "#F2B66B"},
    {"name": "Bubblegum", "siding": "#FF9CCB", "roof": "#A3245E", "trim": "#FFFFFF",
     "shutter": "#FFFFFF", "door": "#FFD23F", "chimney": "#B4574B", "flowerbox": "#A3245E",
     "flower_a": "#FFE45C", "flower_b": "#FFFFFF", "deck": "#C48A63",
     "patio_a": "#F7E1EA", "patio_b": "#D4C9F2"},
    {"name": "Slate & Mustard", "siding": "#5F86C4", "roof": "#353C4A", "trim": "#FFFFFF",
     "shutter": "#F2B935", "door": "#F2B935", "chimney": "#B04A35", "flowerbox": "#FFFFFF",
     "flower_a": "#F2B935", "flower_b": "#FF6B6B", "deck": "#9C7A5C",
     "patio_a": "#DCDDE0", "patio_b": "#A7B1C2"},
]


# Round 2 (current): realistic American-suburban colours. roof_style:
#   "shingle"  asphalt shingles: tinted tileable shingle texture
#   "metal"    standing-seam metal: plain colour + raised seam strips
#   "tile"     clay tile: tinted tileable barrel-tile texture
#   "courses"  round-1 look: plain colour + 4 raised course lines
COLOR_SCHEMES = [
    {"name": "Colonial White", "siding": "#F2F0EA", "roof": "#4A4D53", "roof_style": "shingle",
     "trim": "#FFFFFF", "shutter": "#24272C", "door": "#9E2A30", "chimney": "#9C4A3A",
     "flowerbox": "#FFFFFF", "flower_a": "#D9433F", "flower_b": "#F4F2EC", "deck": "#8E5D3E",
     "patio_a": "#C9C5BC", "patio_b": "#B4AFA5"},
    {"name": "Cream & Forest", "siding": "#EDE3C8", "roof": "#7D807F", "roof_style": "shingle",
     "trim": "#FFFFFF", "shutter": "#2F4A37", "door": "#2F4A37", "chimney": "#8F8B84",
     "flowerbox": "#2F4A37", "flower_a": "#E26A8A", "flower_b": "#F4F2EC", "deck": "#A87449",
     "patio_a": "#D2CBBE", "patio_b": "#BDB5A6"},
    {"name": "Adobe Tile", "siding": "#DCC4A0", "roof": "#B5552F", "roof_style": "tile",
     "trim": "#F7F1E4", "shutter": "#5E7A6E", "door": "#6E4A2E", "chimney": "#C9B79A",
     "flowerbox": "#6E4A2E", "flower_a": "#D9433F", "flower_b": "#F2C14E", "deck": "#8C5636",
     "patio_a": "#C9774F", "patio_b": "#B2623F"},
    {"name": "Greige Modern", "siding": "#BEB6A9", "roof": "#2E3034", "roof_style": "metal",
     "trim": "#F5F3EE", "shutter": "#2E3034", "door": "#B9824C", "chimney": "#8A8782",
     "flowerbox": "#2E3034", "flower_a": "#F4F2EC", "flower_b": "#9B7FC0", "deck": "#6F4D36",
     "patio_a": "#A9A6A0", "patio_b": "#93908A"},
    {"name": "Dove Gray", "siding": "#AEB4B9", "roof": "#2D2E31", "roof_style": "shingle",
     "trim": "#FFFFFF", "shutter": "#FFFFFF", "door": "#2C5C8C", "chimney": "#9E4F3C",
     "flowerbox": "#FFFFFF", "flower_a": "#D9433F", "flower_b": "#F4F2EC", "deck": "#8F857A",
     "patio_a": "#8E99A3", "patio_b": "#A8B0B7"},
    {"name": "Charcoal Craftsman", "siding": "#4F535A", "roof": "#6E5846", "roof_style": "shingle",
     "trim": "#F2EEE6", "shutter": "#F2EEE6", "door": "#B57B45", "chimney": "#9C978E",
     "flowerbox": "#B57B45", "flower_a": "#F2C14E", "flower_b": "#E26A8A", "deck": "#A06B43",
     "patio_a": "#BDB7AC", "patio_b": "#A59F94"},
    {"name": "Coastal Blue", "siding": "#A8BDCB", "roof": "#58636F", "roof_style": "shingle",
     "trim": "#FFFFFF", "shutter": "#FFFFFF", "door": "#26385A", "chimney": "#A3A09A",
     "flowerbox": "#FFFFFF", "flower_a": "#7B5BA6", "flower_b": "#F4F2EC", "deck": "#9A9188",
     "patio_a": "#D7D2C6", "patio_b": "#BFC6CC"},
    {"name": "Navy Saltbox", "siding": "#2F3F5E", "roof": "#3E4146", "roof_style": "shingle",
     "trim": "#F6F4EE", "shutter": "#F6F4EE", "door": "#A3262F", "chimney": "#A0503C",
     "flowerbox": "#F6F4EE", "flower_a": "#D9433F", "flower_b": "#F2C14E", "deck": "#8E5D3E",
     "patio_a": "#C9C5BC", "patio_b": "#A9A399"},
    {"name": "Sage Cottage", "siding": "#9DA88C", "roof": "#4F4137", "roof_style": "shingle",
     "trim": "#F4EFE1", "shutter": "#414C38", "door": "#8B2E2E", "chimney": "#A45A42",
     "flowerbox": "#F4EFE1", "flower_a": "#E26A8A", "flower_b": "#F2C14E", "deck": "#9A6240",
     "patio_a": "#D9C7A5", "patio_b": "#C2AE8B"},
    {"name": "Red Farmhouse", "siding": "#8E2F2A", "roof": "#878E95", "roof_style": "metal",
     "trim": "#FFFFFF", "shutter": "#FFFFFF", "door": "#2B2B2B", "chimney": "#97928A",
     "flowerbox": "#FFFFFF", "flower_a": "#F2C14E", "flower_b": "#F4F2EC", "deck": "#8C5A3C",
     "patio_a": "#B9B4AA", "patio_b": "#A29C91"},
    {"name": "Buttercream", "siding": "#EAD9A0", "roof": "#3D5B45", "roof_style": "metal",
     "trim": "#FFFFFF", "shutter": "#3D5B45", "door": "#9A6A3E", "chimney": "#A4553F",
     "flowerbox": "#FFFFFF", "flower_a": "#D9433F", "flower_b": "#7B5BA6", "deck": "#B07A47",
     "patio_a": "#A65A42", "patio_b": "#8E4B37"},
    {"name": "Cedar Lodge", "siding": "#8C6244", "roof": "#36383C", "roof_style": "shingle",
     "trim": "#F1EADB", "shutter": "#3B4A36", "door": "#3B4A36", "chimney": "#8F8B84",
     "flowerbox": "#3B4A36", "flower_a": "#F2C14E", "flower_b": "#E26A8A", "deck": "#6E4A33",
     "patio_a": "#9C978E", "patio_b": "#B8B2A6"},
]


def darken(h, f=DERIVED_DARKEN):
    h = h.lstrip("#")
    r, g, b = (int(h[i:i + 2], 16) for i in (0, 2, 4))
    return "#%02X%02X%02X" % (min(255, int(r * f)), min(255, int(g * f)), min(255, int(b * f)))


def scheme_palette(scheme):
    """Palette dict (slot key -> hex) for a scheme dict or scheme name."""
    if isinstance(scheme, str):
        scheme = next(c for c in COLOR_SCHEMES + COLOR_SCHEMES_BRIGHT if c["name"] == scheme)
    p = dict(COMMON)
    p.update({
        "_id": "".join(ch for ch in scheme["name"] if ch.isalnum()),
        "_roof_style": scheme.get("roof_style", "courses"),
        # realistic schemes get calmer leaves and glass than the candy round
        "leaf": COMMON["leaf"] if "roof_style" not in scheme else "#4F8A3C",
        "glass": COMMON["glass"] if "roof_style" not in scheme else "#A8D2E6",
        "roof_seam": darken(scheme["roof"], 1.22),
        "siding": scheme["siding"], "roof": scheme["roof"], "roof_cap": darken(scheme["roof"]),
        "trim": scheme["trim"], "shutter": scheme["shutter"],
        "door": scheme["door"], "door_panel": darken(scheme["door"]),
        "brick": scheme["chimney"], "flowerbox": scheme["flowerbox"],
        "flower_a": scheme["flower_a"], "flower_b": scheme["flower_b"],
        "deck": scheme["deck"], "deck_dark": darken(scheme["deck"], 0.78),
        "stone_a": scheme["patio_a"], "stone_b": scheme["patio_b"],
    })
    return p


# ---------------------------------------------------------------------------
# Roof textures: small tileable GREYSCALE masks, multiplied by the scheme's
# roof colour. In Unity this is one grey texture per roof style on a Lit
# material with Base Color = the scheme's roof hex (base map x colour). For
# the concept GLBs we bake the tinted copy so viewers show it exactly.
# UVs are planar in metres along the roof plane (u across, v up-slope), so a
# texel is the same size on every house and piece.
# ---------------------------------------------------------------------------

TEX_DIR = os.path.join(os.path.dirname(os.path.abspath(__file__)), "textures")
TEX_PX = 240
ROOF_TEX_METRES = {"shingle": (1.0, 1.0), "tile": (0.96, 0.96)}
SHINGLE_COURSES_PER_TILE = 4      # 0.25 m courses (chunky but believable)
SHINGLE_TABS_PER_COURSE = 3       # 0.33 m tabs, staggered half a tab
TILE_BARRELS_PER_TILE = 3         # 0.32 m clay barrels
TILE_COURSES_PER_TILE = 4         # 0.24 m clay courses


def roof_mask(style):
    """Write (once) and return the greyscale mask PNG path for a roof style."""
    from PIL import Image
    import random
    os.makedirs(TEX_DIR, exist_ok=True)
    path = os.path.join(TEX_DIR, "roof_%s_mask.png" % style)
    if os.path.exists(path):
        return path
    n = TEX_PX
    img = Image.new("L", (n, n), 255)
    px = img.load()
    rng = random.Random(11)
    if style == "shingle":
        ch = n // SHINGLE_COURSES_PER_TILE
        tw = n // SHINGLE_TABS_PER_COURSE
        shade = {(r, t): rng.uniform(0.76, 1.0) for r in range(SHINGLE_COURSES_PER_TILE)
                 for t in range(SHINGLE_TABS_PER_COURSE)}
        for y in range(n):
            r = y // ch
            yy = y % ch                       # 0 = top of the course image row
            off = (tw // 2) * (r % 2)
            for x in range(n):
                xx = (x + off) % n
                t = xx // tw
                v = shade[(r, t)]
                if yy >= ch - 5:              # shadow line at the course's lower edge
                    v *= 0.55
                elif yy < 2:
                    v *= 1.04
                if xx % tw < 4 and ch * 0.35 < yy < ch - 5:  # 3-tab slot, cut up from the edge
                    v *= 0.68
                px[x, y] = max(0, min(255, int(235 * v)))
    elif style == "tile":
        import math
        ch = n // TILE_COURSES_PER_TILE
        bw = n // TILE_BARRELS_PER_TILE
        shade = {(r, b): rng.uniform(0.88, 1.02) for r in range(TILE_COURSES_PER_TILE)
                 for b in range(TILE_BARRELS_PER_TILE)}
        for y in range(n):
            r = y // ch
            yy = y % ch
            for x in range(n):
                b = x // bw
                f = (x % bw) / bw
                v = (0.62 + 0.42 * math.sin(math.pi * f)) * shade[(r, b)]
                if yy >= ch - 5:
                    v *= 0.6
                px[x, y] = max(0, min(255, int(240 * v)))
    img.save(path)
    return path


def tinted_texture(style, hex_color):
    from PIL import Image, ImageChops
    path = os.path.join(TEX_DIR, "roof_%s_%s.png" % (style, hex_color.lstrip("#").upper()))
    if not os.path.exists(path):
        mask = Image.open(roof_mask(style)).convert("RGB")
        col = Image.new("RGB", mask.size, hex_color)
        ImageChops.multiply(mask, col).save(path)
    return path


def hex_rgba(h):
    h = h.lstrip("#")
    srgb = [int(h[i:i + 2], 16) / 255.0 for i in (0, 2, 4)]
    lin = [c / 12.92 if c <= 0.04045 else ((c + 0.055) / 1.055) ** 2.4 for c in srgb]
    return (lin[0], lin[1], lin[2], 1.0)


def material(name, hex_color, roughness=0.85, texture=None):
    mat = bpy.data.materials.get(name)
    if mat is None:
        mat = bpy.data.materials.new(name)
        mat.use_nodes = True
    bsdf = mat.node_tree.nodes.get("Principled BSDF")
    col = hex_rgba(hex_color)
    bsdf.inputs["Base Color"].default_value = col
    if texture:
        tn = mat.node_tree.nodes.new("ShaderNodeTexImage")
        tn.image = bpy.data.images.load(texture, check_existing=True)
        tn.interpolation = "Linear"
        mat.node_tree.links.new(tn.outputs["Color"], bsdf.inputs["Base Color"])
    bsdf.inputs["Roughness"].default_value = roughness
    if "Specular IOR Level" in bsdf.inputs:
        bsdf.inputs["Specular IOR Level"].default_value = 0.2
    mat.diffuse_color = col
    return mat


# ---------------------------------------------------------------------------
# Mesh builder: accumulate closed primitives into one object
# ---------------------------------------------------------------------------

class MeshBuilder:
    def __init__(self, name, palette):
        self.name = name
        self.palette = palette
        self.bm = bmesh.new()
        self.mat_names = []

    def _mi(self, key):
        if key not in self.mat_names:
            self.mat_names.append(key)
        return self.mat_names.index(key)

    def _faces(self, verts, faces, mat):
        vs = [self.bm.verts.new(Vector(v)) for v in verts]
        mi = self._mi(mat)
        for f in faces:
            try:
                face = self.bm.faces.new([vs[i] for i in f])
                face.material_index = mi
            except ValueError:
                pass

    def hexahedron(self, c, mat, skip=()):
        """c = 8 corners: bottom quad (0-3) then top quad (4-7), same order.
        skip: face indices to leave out (hidden faces), 4 = the c[2]/c[3] side."""
        faces = [(0, 1, 2, 3), (4, 7, 6, 5), (0, 4, 5, 1), (1, 5, 6, 2), (2, 6, 7, 3), (3, 7, 4, 0)]
        self._faces(c, [f for i, f in enumerate(faces) if i not in skip], mat)

    def box(self, mn, mx, mat):
        x0, y0, z0 = mn
        x1, y1, z1 = mx
        if x1 - x0 <= 1e-5 or y1 - y0 <= 1e-5 or z1 - z0 <= 1e-5:
            return
        self.hexahedron([(x0, y0, z0), (x1, y0, z0), (x1, y1, z0), (x0, y1, z0),
                         (x0, y0, z1), (x1, y0, z1), (x1, y1, z1), (x0, y1, z1)], mat)

    def beam(self, p0, p1, w, h, up, mat):
        """Box beam from p0 to p1; `h` measured along `up`, `w` across."""
        p0, p1, up = Vector(p0), Vector(p1), Vector(up)
        d = p1 - p0
        if d.length < 1e-5:
            return
        d.normalize()
        n = (up - d * up.dot(d)).normalized()
        s = d.cross(n).normalized()
        c = []
        for p in (p0, p1):
            c.append([p - s * w / 2 - n * h / 2, p + s * w / 2 - n * h / 2,
                      p + s * w / 2 + n * h / 2, p - s * w / 2 + n * h / 2])
        a, b = c
        self.hexahedron([a[0], a[1], a[2], a[3], b[0], b[1], b[2], b[3]], mat)

    def prism(self, pts, offset, mat, side_mat=None):
        pts = [Vector(p) for p in pts]
        off = Vector(offset)
        n = len(pts)
        verts = pts + [p + off for p in pts]
        faces = [tuple(reversed(range(n))), tuple(range(n, 2 * n))]
        self._faces(verts, faces, mat)
        side = side_mat or mat
        vs_side = []
        for i in range(n):
            j = (i + 1) % n
            vs_side.append((pts[i], pts[j], pts[j] + off, pts[i] + off))
        for q in vs_side:
            self._faces(q, [(0, 1, 2, 3)], side)

    def build(self, collection=None):
        bm = self.bm
        bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-5)
        bmesh.ops.triangulate(bm, faces=bm.faces)
        bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
        style = self.palette.get("_roof_style", "courses")
        textured = style in ROOF_TEX_METRES and "roof" in self.mat_names
        if textured:
            _roof_uvs(bm, self.mat_names.index("roof"), ROOF_TEX_METRES[style])
        me = bpy.data.meshes.new(self.name)
        bm.to_mesh(me)
        bm.free()
        pid = self.palette.get("_id", "")
        for key in self.mat_names:
            tex = tinted_texture(style, self.palette[key]) if (textured and key == "roof") else None
            me.materials.append(material("DH_%s%s" % (pid + "_" if pid else "", key),
                                         self.palette[key], texture=tex))
        for p in me.polygons:
            p.use_smooth = False
        ob = bpy.data.objects.new(self.name, me)
        (collection or bpy.context.scene.collection).objects.link(ob)
        return ob


def _roof_uvs(bm, roof_mi, metres):
    """Planar UVs in metres on every roof-material face: u runs across the
    slope (horizontal), v runs up the slope, so shingle courses stay level."""
    uv = bm.loops.layers.uv.verify()
    up = Vector((0, 0, 1))
    for f in bm.faces:
        if f.material_index != roof_mi:
            continue
        n = f.normal
        if abs(n.z) > 0.999 or n.length < 1e-6:
            h, s = Vector((1, 0, 0)), Vector((0, 1, 0))
        else:
            h = up.cross(n).normalized()
            s = n.cross(h).normalized()
            if s.z < 0:
                s = -s
        for loop in f.loops:
            co = loop.vert.co
            loop[uv].uv = (co.dot(h) / metres[0], co.dot(s) / metres[1])


def tri_count(ob):
    return sum(len(p.vertices) - 2 for p in ob.data.polygons)


# ---------------------------------------------------------------------------
# Walls
# ---------------------------------------------------------------------------

class Opening:
    """An opening in a wall. `t` = centre distance from the wall start,
    `sill` = height of the bottom above the finished floor."""

    def __init__(self, kind, t, width, height, sill=0.0, shutters=False, flowerbox=False,
                 mullions=True, arched=False):
        self.kind, self.t, self.w, self.h, self.sill = kind, t, width, height, sill
        self.shutters, self.flowerbox, self.mullions = shutters, flowerbox, mullions
        # arched: round head of radius w/2 inside the opening's height (doors, windows)
        self.arched = arched

    def span(self):
        return self.t - self.w / 2, self.t + self.w / 2


class Wall:
    """Outer-face line a->b, top height z_top, openings, end trim (how much
    to stop short of b so the next wall owns the corner)."""

    def __init__(self, a, b, z_top, openings=(), end_trim=WALL_T, start_trim=0.0, z_base=None):
        self.a, self.b = Vector((a[0], a[1], 0)), Vector((b[0], b[1], 0))
        self.z_top = z_top
        self.z_base = FLOOR_Z if z_base is None else z_base  # framing: bottom plate sits here
        self.openings = list(openings)
        self.end_trim, self.start_trim = end_trim, start_trim
        d = self.b - self.a
        self.L = d.length
        self.dir = d.normalized()
        self.n = Vector((self.dir.y, -self.dir.x, 0))  # outward

    def p(self, t, k, z):
        """Point at distance t along, k INWARD from the outer face, height z."""
        return self.a + self.dir * t - self.n * k + Vector((0, 0, z))

    def wbox(self, m, t0, t1, k0, k1, z0, z1, mat, cull_back=False):
        """Box in wall space. cull_back drops the inner (k1) face - for trim
        set into the wall, where that face can never be seen."""
        if t1 - t0 <= 1e-5 or z1 - z0 <= 1e-5:
            return
        c = [self.p(t0, k0, z0), self.p(t1, k0, z0), self.p(t1, k1, z0), self.p(t0, k1, z0),
             self.p(t0, k0, z1), self.p(t1, k0, z1), self.p(t1, k1, z1), self.p(t0, k1, z1)]
        m.hexahedron(c, mat, skip=(4,) if cull_back else ())

    @property
    def t_start(self):
        return self.start_trim

    @property
    def t_end(self):
        return self.L - self.end_trim


STORY_H = 2.8            # framing: one storey of studs + plates
PLATFORM_T = 0.30        # framing: second-floor joists + subfloor
TWO_STORY_WALL = 2 * STORY_H + PLATFORM_T   # finished wall height of a two-storey house


def split_stories(w, z_split=None, platform=PLATFORM_T):
    """Framing helper: split a tall wall into a ground-floor wall and an
    upper wall standing on a second-floor platform. Openings go to the
    storey their sill is in. The finished wall stays one tall piece."""
    z_split = FLOOR_Z + STORY_H if z_split is None else z_split
    lo = [o for o in w.openings if FLOOR_Z + o.sill < z_split]
    hi = [o for o in w.openings if FLOOR_Z + o.sill >= z_split]
    a, b = (w.a.x, w.a.y), (w.b.x, w.b.y)
    w1 = Wall(a, b, z_split, lo, w.end_trim, w.start_trim)
    w2 = Wall(a, b, w.z_top, hi, w.end_trim, w.start_trim, z_base=z_split + platform)
    return w1, w2


def wall_finished(m, w, siding):
    """Solid wall with trimmed doors/windows, shutters and flower boxes.
    A wall may carry z_bottom (dormer faces); default is the slab top."""
    z0, z1 = getattr(w, "z_bottom", SLAB_TOP), w.z_top
    ops = sorted(w.openings, key=lambda o: o.t)
    cursor = w.t_start
    # openings stacked over each other (two-storey walls) share a column:
    # the column spans the union of their widths and is filled storey by storey
    for col in _opening_columns(ops):
        ct0 = min(o.span()[0] for o in col)
        ct1 = max(o.span()[1] for o in col)
        w.wbox(m, cursor, ct0, 0, WALL_T, z0, z1, siding)
        zc = z0
        for o in sorted(col, key=lambda o: o.sill):
            t0, t1 = o.span()
            zb, zt = FLOOR_Z + o.sill, FLOOR_Z + o.sill + o.h
            w.wbox(m, ct0, ct1, 0, WALL_T, zc, zb, siding)
            if t0 > ct0 + 1e-4:
                w.wbox(m, ct0, t0, 0, WALL_T, zb, zt, siding)
            if t1 < ct1 - 1e-4:
                w.wbox(m, t1, ct1, 0, WALL_T, zb, zt, siding)
            _opening_trim(m, w, o, t0, t1, zb, zt)
            zc = zt
        w.wbox(m, ct0, ct1, 0, WALL_T, zc, z1, siding)
        cursor = ct1
    w.wbox(m, cursor, w.t_end, 0, WALL_T, z0, z1, siding)
    # water-table skirt band along the bottom, interrupted by doors
    cursor = w.t_start
    for o in ops:
        if o.kind in ("door", "garage"):
            t0, t1 = o.span()
            w.wbox(m, cursor, t0 - TRIM_W, -TRIM_OUT * 0.6, 0.02, z0, z0 + SKIRT_H, "trim")
            cursor = t1 + TRIM_W
    w.wbox(m, cursor, w.t_end, -TRIM_OUT * 0.6, 0.02, z0, z0 + SKIRT_H, "trim")
    # corner board at the wall start (outer corner)
    if w.start_trim == 0.0:
        w.wbox(m, -TRIM_OUT * 0.6, TRIM_W * 1.3, -TRIM_OUT * 0.6, 0.02, z0, z1, "trim")


def _opening_columns(ops):
    """Group openings (sorted by t) whose spans overlap into columns."""
    cols = []
    for o in ops:
        t0, t1 = o.span()
        if cols and t0 < max(p.span()[1] for p in cols[-1]) - 1e-4:
            cols[-1].append(o)
        else:
            cols.append([o])
    return cols


def _tb(w, m, *args):
    """Trim box set into the wall: its inner face is never visible."""
    w.wbox(m, *args, cull_back=True)


ARCH_SEGMENTS = 4     # segments per half-circle arch (low-poly: a half octagon)


def arch_points(tc, z_spring, r, n=ARCH_SEGMENTS):
    """Half-circle from the left spring point over the top to the right one,
    as (t, z) pairs."""
    return [(tc - r * math.cos(math.pi * i / n), z_spring + r * math.sin(math.pi * i / n)) for i in range(n + 1)]


def _arch_head(m, w, o, t0, t1, zt):
    """Round head for an arched opening: wall-coloured fills in the two top
    corners of the rectangular opening, and a trim ring around the arch."""
    r = o.w / 2
    tc, zs = (t0 + t1) / 2, zt - r
    arc = arch_points(tc, zs, r)
    k_fill = WALL_T * 0.34            # just in front of the door leaf / glass
    inward = -w.n * k_fill
    half = len(arc) // 2
    left = [(t0, zt)] + arc[:half + 1]
    right = arc[half:] + [(t1, zt)]
    for poly in (left, right):
        m.prism([w.p(t, -0.002, z) for t, z in poly], inward, "siding")
    tw = TRIM_W
    for (ta, za), (tb_, zb_) in zip(arc, arc[1:]):
        # annular segment: inner arc -> outer arc, standing proud of the wall
        def ring(t, z, extra):
            f = (r + extra) / r
            return tc + (t - tc) * f, zs + (z - zs) * f
        quad = [(ta, za), (tb_, zb_), ring(tb_, zb_, tw * 1.2), ring(ta, za, tw * 1.2)]
        m.prism([w.p(t, 0.02, z) for t, z in quad], w.n * (TRIM_OUT * 1.4 + 0.02), "trim")


def arcade(m, w, t0, t1, z_bottom, z_spring, z_top, n, pier, depth, mat="siding", cap=None):
    """Arcaded wall face (loggia front) along wall `w` between t0 and t1:
    n round arches springing at z_spring, piers of width `pier`, solid
    spandrel band up to z_top; `depth` = thickness behind the face."""
    ow = (t1 - t0 - (n + 1) * pier) / n
    r = ow / 2
    inward = -w.n * depth
    t = t0
    for i in range(n + 1):
        w.wbox(m, t, t + pier, 0, depth, z_bottom, z_spring, mat)
        t += pier + ow
    t = t0 + pier
    for i in range(n):
        tc = t + r
        arc = arch_points(tc, z_spring, r)
        poly = arc + [(t + ow, z_top), (t, z_top)]
        m.prism([w.p(a, 0, z) for a, z in poly], inward, mat)
        t += ow + pier
    # solid band over the piers between the arches
    t = t0
    for i in range(n + 1):
        w.wbox(m, t, t + pier, 0, depth, z_spring, z_top, mat)
        t += pier + ow
    if cap:
        w.wbox(m, t0 - 0.06, t1 + 0.06, -0.06, depth + 0.06, z_top, z_top + 0.12, cap)
    return ow


def _opening_trim(m, w, o, t0, t1, zb, zt):
    out, tw = -TRIM_OUT, TRIM_W
    zj = zt - o.w / 2 if o.arched else zt + tw     # jambs stop at the spring line on arched heads
    # jambs and head
    _tb(w, m, t0 - tw, t0, out, WALL_T * 0.5, zb - (0 if o.kind == "door" else tw), zj, "trim")
    _tb(w, m, t1, t1 + tw, out, WALL_T * 0.5, zb - (0 if o.kind == "door" else tw), zj, "trim")
    if o.arched:
        _arch_head(m, w, o, t0, t1, zt)
    else:
        _tb(w, m, t0 - tw * 1.6, t1 + tw * 1.6, out * 1.4, WALL_T * 0.5, zt, zt + tw * 1.3, "trim")
    if o.kind == "window":
        _tb(w, m, t0 - tw * 1.4, t1 + tw * 1.4, out * 2.2, WALL_T * 0.5, zb - tw, zb, "trim")
        _tb(w, m, t0, t1, WALL_T * 0.42, WALL_T * 0.58, zb, zt, "glass")
        if o.mullions:
            mw = tw * 0.55
            _tb(w, m, o.t - mw / 2, o.t + mw / 2, WALL_T * 0.3, WALL_T * 0.42, zb, zt, "trim")
            zm = (zb + zt) / 2
            _tb(w, m, t0, t1, WALL_T * 0.3, WALL_T * 0.42, zm - mw / 2, zm + mw / 2, "trim")
        if o.shutters:
            sw = o.w * 0.42
            _tb(w, m, t0 - tw - sw, t0 - tw, out * 1.3, 0.0, zb - tw * 0.5, zt + tw * 0.5, "shutter")
            _tb(w, m, t1 + tw, t1 + tw + sw, out * 1.3, 0.0, zb - tw * 0.5, zt + tw * 0.5, "shutter")
        if o.flowerbox:
            fb0, fb1 = t0 - tw, t1 + tw
            _tb(w, m, fb0, fb1, -0.38, out * 2.2, zb - tw - 0.34, zb - tw - 0.04, "flowerbox")
            # chunky flower clumps on top
            n = max(3, int(round((fb1 - fb0) / 0.32)))
            step = (fb1 - fb0) / n
            for i in range(n):
                tc = fb0 + step * (i + 0.5)
                key = "flower_a" if i % 2 == 0 else "flower_b"
                hh = 0.18 if i % 2 == 0 else 0.13
                _tb(w, m, tc - step * 0.42, tc + step * 0.42, -0.34, -0.12,
                       zb - tw - 0.04, zb - tw - 0.04 + hh, "leaf" if i % 3 == 2 else key)
    elif o.kind == "garage":
        # sectional garage door: trim-coloured panels, a row of small lites
        n_sec = 4
        sh = o.h / n_sec
        for i in range(n_sec):
            za, zz = zb + i * sh + 0.03, zb + (i + 1) * sh - 0.03
            _tb(w, m, t0 + 0.04, t1 - 0.04, WALL_T * 0.3, WALL_T * 0.6, za, zz, "trim")
        _tb(w, m, t0, t1, WALL_T * 0.45, WALL_T * 0.62, zb, zt, "door_panel")
        n_lite = max(3, int(o.w / 0.7))
        lw = (o.w - 0.3) / n_lite
        for i in range(n_lite):
            la = t0 + 0.15 + i * lw + 0.06
            _tb(w, m, la, la + lw - 0.12, WALL_T * 0.26, WALL_T * 0.3, zt - sh + 0.12, zt - 0.12, "glass")
    else:  # door
        _tb(w, m, t0, t1, WALL_T * 0.35, WALL_T * 0.6, zb, zt, "door")
        # small window in the top of the door + kick panel
        _tb(w, m, t0 + o.w * 0.22, t1 - o.w * 0.22, WALL_T * 0.3, WALL_T * 0.35,
               zt - o.h * 0.36, zt - o.h * 0.1, "glass")
        _tb(w, m, t0 + o.w * 0.18, t1 - o.w * 0.18, WALL_T * 0.28, WALL_T * 0.35,
               zb + o.h * 0.1, zb + o.h * 0.45, "door_panel")
        # knob
        kt = t1 - o.w * 0.18
        kz = zb + o.h * 0.45
        _tb(w, m, kt - 0.06, kt + 0.06, WALL_T * 0.18, WALL_T * 0.35, kz - 0.06, kz + 0.06, "metal")


def wall_framing(m, w):
    """Stud wall on the subfloor: plates, studs, king studs, headers, sills."""
    k0 = (WALL_T - STUD_D) / 2
    k1 = k0 + STUD_D
    zb_plate = w.z_base
    z_stud0 = w.z_base + PLATE_H
    z_stud1 = w.z_top - 2 * PLATE_H
    ts, te = w.t_start, w.t_end
    doors = [o.span() for o in w.openings if o.kind in ("door", "garage")]
    # bottom plate, cut at doors
    cursor = ts
    for d0, d1 in sorted(doors):
        w.wbox(m, cursor, d0 - STUD_W, k0, k1, zb_plate, z_stud0, "framing_dark")
        cursor = d1 + STUD_W
    w.wbox(m, cursor, te, k0, k1, zb_plate, z_stud0, "framing_dark")
    # double top plate (upper one runs to the corner to tie the walls)
    w.wbox(m, ts, te, k0, k1, z_stud1, z_stud1 + PLATE_H, "framing_dark")
    w.wbox(m, ts - (0 if w.start_trim else 0), te + w.end_trim - k0, k0, k1,
           z_stud1 + PLATE_H, w.z_top, "framing_dark")

    def stud(tc, za, zb):
        w.wbox(m, tc - STUD_W / 2, tc + STUD_W / 2, k0, k1, za, zb, "framing")

    positions = [ts + STUD_W / 2]
    t = ts + STUD_SPACING
    while t < te - STUD_W:
        positions.append(t)
        t += STUD_SPACING
    positions.append(te - STUD_W / 2)

    spans = [(o, *o.span()) for o in w.openings]
    for tc in positions:
        blocked = None
        for o, o0, o1 in spans:
            if o0 - STUD_W * 1.6 < tc < o1 + STUD_W * 1.6:
                blocked = o
        if blocked is None:
            stud(tc, z_stud0, z_stud1)
        else:
            o0, o1 = blocked.span()
            if o0 < tc < o1:
                zt = FLOOR_Z + blocked.sill + blocked.h + HEADER_H
                if z_stud1 - zt > 0.08:
                    stud(tc, zt, z_stud1)           # cripple above header
                if blocked.kind == "window":
                    zs = FLOOR_Z + blocked.sill - PLATE_H
                    if zs - z_stud0 > 0.08:
                        stud(tc, z_stud0, zs)       # cripple below sill
    for o, o0, o1 in spans:
        zb = FLOOR_Z + o.sill
        zt = zb + o.h
        stud(o0 - STUD_W / 2, z_stud0, z_stud1)         # king/jack studs
        stud(o1 + STUD_W / 2, z_stud0, z_stud1)
        w.wbox(m, o0 - STUD_W, o1 + STUD_W, k0, k1, zt, zt + HEADER_H, "framing_dark")  # header
        if o.kind == "window":
            w.wbox(m, o0, o1, k0, k1, zb - PLATE_H, zb, "framing_dark")              # sill


# ---------------------------------------------------------------------------
# Gable-end infill (above a wall's top plate, under the roof)
# ---------------------------------------------------------------------------

def gable_finished(m, w, roof_bottom_z, siding, samples=None):
    """Solid gable infill: polygon from the wall top up to the underside of
    the roof, following `roof_bottom_z(t)` (piecewise linear: give samples
    of t where the slope changes)."""
    ts = [w.t_start] + sorted(samples or []) + [w.L]
    outer = [w.p(t, 0, w.z_top) for t in (ts[0], ts[-1])]
    top = [w.p(t, 0, roof_bottom_z(t)) for t in reversed(ts)]
    pts = outer + top
    m.prism(pts, -w.n * WALL_T, siding)


def gable_framing(m, w, roof_bottom_z):
    k0 = (WALL_T - STUD_D) / 2
    k1 = k0 + STUD_D
    t = w.t_start + STUD_SPACING
    while t < w.t_end - STUD_W:
        z1 = roof_bottom_z(t) - 0.02
        if z1 - w.z_top > 0.15:
            w.wbox(m, t - STUD_W / 2, t + STUD_W / 2, k0, k1, w.z_top, z1, "framing")
        t += STUD_SPACING


def gable_window(m, w, t, z, size, mat="glass"):
    """Small square attic window on a gable face."""
    h = size / 2
    w.wbox(m, t - h - TRIM_W, t + h + TRIM_W, -TRIM_OUT, 0.02, z - h - TRIM_W, z + h + TRIM_W, "trim")
    w.wbox(m, t - h, t + h, -TRIM_OUT - 0.02, 0.0, z - h, z + h, mat)
    mw = TRIM_W * 0.5
    w.wbox(m, t - mw / 2, t + mw / 2, -TRIM_OUT - 0.04, 0, z - h, z + h, "trim")
    w.wbox(m, t - h, t + h, -TRIM_OUT - 0.04, 0, z - mw / 2, z + mw / 2, "trim")


def gable_vent(m, w, t, z, width, height, backing="roof", slats=4):
    """Louvred attic vent: trim frame, dark backing, horizontal slats."""
    hw, hh = width / 2, height / 2
    w.wbox(m, t - hw - TRIM_W, t + hw + TRIM_W, -TRIM_OUT, 0.02, z - hh - TRIM_W, z + hh + TRIM_W, "trim")
    w.wbox(m, t - hw, t + hw, -TRIM_OUT - 0.02, 0.0, z - hh, z + hh, backing)
    step = height / (slats + 1)
    for i in range(1, slats + 1):
        zc = z - hh + step * i
        w.wbox(m, t - hw, t + hw, -TRIM_OUT - 0.07, 0.0, zc - step * 0.22, zc + step * 0.22, "trim")


# ---------------------------------------------------------------------------
# Roof planes
# ---------------------------------------------------------------------------

class RoofPlane:
    """A sloped plane through the TOP of the rafters.

    `eave` is a point on the plane above the wall's outer line, `u` the
    horizontal direction along the eave, `inward` the horizontal direction
    up-slope, `pitch` in degrees. Plane coords (u, v): v measured along the
    slope from the eave line. Finished roof slabs sit ON this plane; framing
    rafters hang below it, so both versions share one roof surface."""

    def __init__(self, eave, u, inward, pitch_deg):
        self.o = Vector(eave)
        self.u = Vector(u).normalized()
        p = math.radians(pitch_deg)
        self.pitch = p
        self.s = (Vector(inward).normalized() * math.cos(p) + Vector((0, 0, math.sin(p))))
        n = self.u.cross(self.s)
        self.n = n if n.z > 0 else -n

    def pt(self, u, v):
        return self.o + self.u * u + self.s * v

    def v_for_run(self, horizontal):
        return horizontal / math.cos(self.pitch)

    def slab(self, m, poly_uv, mat, edge_mat):
        pts = [self.pt(u, v) for u, v in poly_uv]
        m.prism(pts, self.n * ROOF_T, mat, edge_mat)

    def rafters(self, m, poly_uv, spacing=RAFTER_SPACING, mat="framing"):
        us = [p[0] for p in poly_uv]
        u0, u1 = min(us) + RAFTER_W / 2, max(us) - RAFTER_W / 2
        n = max(1, int(math.ceil((u1 - u0) / spacing)))
        for i in range(n + 1):
            u = u0 + (u1 - u0) * i / n
            vr = _poly_v_range(poly_uv, u)
            if vr is None or vr[1] - vr[0] < 0.1:
                continue
            a = self.pt(u, vr[0]) - self.n * RAFTER_H / 2
            b = self.pt(u, vr[1]) - self.n * RAFTER_H / 2
            m.beam(a, b, RAFTER_W, RAFTER_H, self.n, mat)


def _poly_v_range(poly, u):
    vs = []
    k = len(poly)
    for i in range(k):
        (u0, v0), (u1, v1) = poly[i], poly[(i + 1) % k]
        if abs(u1 - u0) < 1e-9:
            if abs(u - u0) < 1e-6:
                vs += [v0, v1]
            continue
        if min(u0, u1) - 1e-6 <= u <= max(u0, u1) + 1e-6:
            f = (u - u0) / (u1 - u0)
            vs.append(v0 + f * (v1 - v0))
    if len(vs) < 2:
        return None
    return min(vs), max(vs)


def gable_roof(m, x0, x1, y0, y1, z_wall_top, pitch_deg, mode, ridge_axis="x",
               eave=EAVE_OVERHANG, rake=RAKE_OVERHANG, rake_start=None, rake_end=None,
               courses=SHINGLE_COURSES):
    """Simple gable over the rectangle x0..x1, y0..y1 (outer wall lines).

    ridge_axis "x": ridge runs along X, eaves on the y0/y1 walls.
    Returns roof_bottom_z(along, across) helpers via the dict it returns:
      info["top_z"](c)    - plane (rafter top) height at cross position c
      info["bottom_z"](c) - rafter underside height at cross position c
      info["ridge_z"]
    """
    rh_v = RAFTER_H / math.cos(math.radians(pitch_deg))
    tanp = math.tan(math.radians(pitch_deg))
    if ridge_axis == "x":
        a0, a1, c0, c1 = x0, x1, y0, y1
    else:
        a0, a1, c0, c1 = y0, y1, x0, x1
    half = (c1 - c0) / 2
    cm = (c0 + c1) / 2
    z_e = z_wall_top + rh_v
    rs = rake if rake_start is None else rake_start
    re_ = rake if rake_end is None else rake_end

    def top_z(c):
        return z_e + (half - abs(c - cm)) * tanp

    def bottom_z(c):
        return top_z(c) - rh_v

    def V(a, c, z):
        return (a, c, z) if ridge_axis == "x" else (c, a, z)

    def D(a, c):
        return (a, c, 0) if ridge_axis == "x" else (c, a, 0)

    ua = D(1, 0)
    planes = [RoofPlane(V(0, c0, z_e), ua, D(0, 1), pitch_deg),
              RoofPlane(V(0, c1, z_e), ua, D(0, -1), pitch_deg)]
    v_ridge = planes[0].v_for_run(half)
    if mode == "finished":
        v_e = -planes[0].v_for_run(eave)
        poly = [(a0 - rs, v_e), (a1 + re_, v_e), (a1 + re_, v_ridge), (a0 - rs, v_ridge)]
        style = m.palette.get("_roof_style", "courses")
        if style != "courses":
            courses = 0
        for pl in planes:
            pl.slab(m, poly, "roof", "trim")
            if style == "metal":
                # standing seams running up the slope
                u = a0 - rs + SEAM_SPACING / 2
                while u < a1 + re_ - 0.1:
                    a = pl.pt(u, v_e + 0.03) + pl.n * (ROOF_T + SEAM_H / 2)
                    b = pl.pt(u, v_ridge) + pl.n * (ROOF_T + SEAM_H / 2)
                    m.beam(a, b, SEAM_W, SEAM_H, pl.n, "roof_seam")
                    u += SEAM_SPACING
            for k in range(1, courses + 1):
                v = v_e + (v_ridge - v_e) * k / (courses + 1)
                a = pl.pt(a0 - rs + 0.06, v) + pl.n * (ROOF_T + COURSE_H / 2)
                b = pl.pt(a1 + re_ - 0.06, v) + pl.n * (ROOF_T + COURSE_H / 2)
                m.beam(a, b, COURSE_W, COURSE_H, pl.n, "roof_cap")
        # ridge cap
        zr = top_z(cm) + ROOF_T * 0.9
        m.beam(V(a0 - rs - 0.04, cm, zr), V(a1 + re_ + 0.04, cm, zr), RIDGE_CAP_W, RIDGE_CAP_H,
               (0, 0, 1), "roof_cap")
    else:
        v_t = -planes[0].v_for_run(RAFTER_TAIL)
        v_r = v_ridge - planes[0].v_for_run(0.03)
        poly = [(a0, v_t), (a1, v_t), (a1, v_r), (a0, v_r)]
        for pl in planes:
            pl.rafters(m, poly)
        # ridge board
        zr = top_z(cm) - rh_v * 0.55
        m.beam(V(a0, cm, zr), V(a1, cm, zr), 0.08, RAFTER_H * 1.4, (0, 0, 1), "framing_dark")
    return {"top_z": top_z, "bottom_z": bottom_z, "ridge_z": top_z(cm), "z_eave": z_e,
            "planes": planes}


def hip_roof(m, x0, x1, y0, y1, z_wall_top, pitch_deg, mode, eave=EAVE_OVERHANG):
    """Hip roof over x0..x1, y0..y1 (outer wall lines), ridge along X
    (needs width > depth). Same plane/rafter convention as gable_roof."""
    W, D = x1 - x0, y1 - y0
    assert W > D + 0.01, "hip_roof expects the ridge along X"
    rh_v = RAFTER_H / math.cos(math.radians(pitch_deg))
    tanp = math.tan(math.radians(pitch_deg))
    half = D / 2
    ym = (y0 + y1) / 2
    z_e = z_wall_top + rh_v
    planes = [RoofPlane((0, y0, z_e), (1, 0, 0), (0, 1, 0), pitch_deg),
              RoofPlane((0, y1, z_e), (1, 0, 0), (0, -1, 0), pitch_deg),
              RoofPlane((x1, 0, z_e), (0, 1, 0), (-1, 0, 0), pitch_deg),
              RoofPlane((x0, 0, z_e), (0, 1, 0), (1, 0, 0), pitch_deg)]
    vr = planes[0].v_for_run(half)
    ridge_top = z_e + half * tanp
    if mode == "finished":
        ov = eave
        ve = -planes[0].v_for_run(ov)
        trap = [(x0 - ov, ve), (x1 + ov, ve), (x1 - half, vr), (x0 + half, vr)]
        tri = [(y0 - ov, ve), (y1 + ov, ve), (ym, vr)]
        for i, pl in enumerate(planes):
            pl.slab(m, trap if i < 2 else tri, "roof", "trim")
        zc = ROOF_T * 0.95
        for (cx, cy), (rx, ry) in (((x0, y0), (x0 + half, ym)), ((x1, y0), (x1 - half, ym)),
                                   ((x0, y1), (x0 + half, ym)), ((x1, y1), (x1 - half, ym))):
            dx, dy = math.copysign(ov, cx - rx), math.copysign(ov, cy - ry)
            a = (cx + dx, cy + dy, z_e - ov * tanp + zc)
            b = (rx, ry, ridge_top + zc)
            m.beam(a, b, RIDGE_CAP_W * 0.75, RIDGE_CAP_H * 0.8, (0, 0, 1), "roof_cap")
        m.beam((x0 + half - 0.1, ym, ridge_top + zc), (x1 - half + 0.1, ym, ridge_top + zc),
               RIDGE_CAP_W, RIDGE_CAP_H, (0, 0, 1), "roof_cap")
    else:
        tl = RAFTER_TAIL
        vt = -planes[0].v_for_run(tl)
        vr2 = vr - planes[0].v_for_run(0.04)
        trap = [(x0 - tl, vt), (x1 + tl, vt), (x1 - half, vr2), (x0 + half, vr2)]
        tri = [(y0 - tl, vt), (y1 + tl, vt), (ym, vr2)]
        for i, pl in enumerate(planes):
            pl.rafters(m, trap if i < 2 else tri)
        zr = ridge_top - rh_v * 0.55
        m.beam((x0 + half, ym, zr), (x1 - half, ym, zr), 0.08, RAFTER_H * 1.4, (0, 0, 1), "framing_dark")
        for (cx, cy), (rx, ry) in (((x0, y0), (x0 + half, ym)), ((x1, y0), (x1 - half, ym)),
                                   ((x0, y1), (x0 + half, ym)), ((x1, y1), (x1 - half, ym))):
            m.beam((cx, cy, z_e - rh_v * 0.55), (rx, ry, zr), 0.14, RAFTER_H * 1.2, (0, 0, 1),
                   "framing_dark")
    return {"ridge_z": ridge_top, "z_eave": z_e, "tan": tanp}


def shed_roof(m, x0, x1, y_low, y_high, z_low, z_high, mode, over=(0.2, 0.25), tail=0.25):
    """Single-slope roof (porches): eave along X at y_low/z_low rising to a
    wall at y_high/z_high (heights are the TOP of the rafters)."""
    run = abs(y_high - y_low)
    pitch = math.degrees(math.atan2(z_high - z_low, run))
    inward = (0, 1 if y_high > y_low else -1, 0)
    pl = RoofPlane((0, y_low, z_low), (1, 0, 0), inward, pitch)
    v_top = pl.v_for_run(run)
    if mode == "finished":
        side, front = over
        ve = -pl.v_for_run(front)
        pl.slab(m, [(x0 - side, ve), (x1 + side, ve), (x1 + side, v_top), (x0 - side, v_top)], "roof", "trim")
    else:
        vt = -pl.v_for_run(tail)
        pl.rafters(m, [(x0, vt), (x1, vt), (x1, v_top - 0.05), (x0, v_top - 0.05)])
    return pl


def chimney_exterior(m, x_wall, cy, top_z, side=-1, base_z=SLAB_TOP, mat="brick"):
    """Exterior chimney against a side wall (side=-1: -X wall)."""
    def X(a, b):
        return (x_wall + side * a, x_wall + side * b) if side < 0 else (x_wall + a, x_wall + b)
    xa, xb = sorted(X(-0.02, 0.9))
    m.box((xa, cy - 0.7, base_z), (xb, cy + 0.7, 3.2), mat)
    xa, xb = sorted(X(-0.02, 0.75))
    m.box((xa, cy - 0.55, 3.2), (xb, cy + 0.55, 3.55), mat)
    xa, xb = sorted(X(0.1, 0.7))
    m.box((xa, cy - 0.45, 3.55), (xb, cy + 0.45, top_z), mat)
    xa, xb = sorted(X(0.0, 0.8))
    m.box((xa, cy - 0.55, top_z), (xb, cy + 0.55, top_z + 0.17), "concrete")


def chimney_through_roof(m, cx, cy, z_from, top_z, w=0.9, d=0.7, mat="brick"):
    m.box((cx - w / 2, cy - d / 2, z_from), (cx + w / 2, cy + d / 2, top_z), mat)
    m.box((cx - w / 2 - 0.08, cy - d / 2 - 0.08, top_z), (cx + w / 2 + 0.08, cy + d / 2 + 0.08, top_z + 0.15),
          "concrete")


def door_canopy(m, x_center, y_wall, z_door_top, hw=1.05, proj=0.9, drop=0.28):
    """Shed canopy over a front door on a -Y facing wall."""
    z_wall = z_door_top + 0.42
    pts = [(x_center - hw, y_wall + 0.02, z_wall), (x_center + hw, y_wall + 0.02, z_wall),
           (x_center + hw, y_wall - proj, z_wall - drop), (x_center - hw, y_wall - proj, z_wall - drop)]
    m.prism(pts, (0, 0, ROOF_T), "roof", "trim")
    for x in (x_center - hw + 0.12, x_center + hw - 0.12):
        m.beam((x, y_wall - 0.02, z_wall - 0.6), (x, y_wall - proj + 0.15, z_wall - drop + 0.02), 0.1, 0.1,
               (1, 0, 0), "trim")


def brace(m, w, t0, t1):
    """Temporary diagonal brace on a framed wall ("being built" cue)."""
    a = w.p(t0, -0.03, w.z_base + PLATE_H + 0.05)
    b = w.p(t1, -0.03, w.z_top - 2 * PLATE_H - 0.05)
    m.beam(a, b, 0.12, 0.05, -w.n, "framing_dark")


def steps(m, x0, x1, y_front, z_top, n=2, run=0.32, mat="concrete"):
    """Front steps descending toward -Y from y_front (the landing edge)."""
    rise = z_top / (n + 1)
    for i in range(1, n + 1):
        m.box((x0, y_front - run * i, 0), (x1, y_front, z_top - rise * i), mat)


# ---------------------------------------------------------------------------
# Foundation and floor
# ---------------------------------------------------------------------------

def slab(m, x0, x1, y0, y1, over=SLAB_OVERHANG, mat="concrete"):
    m.box((x0 - over, y0 - over, 0), (x1 + over, y1 + over, SLAB_TOP), mat)


def floor_framing(m, x0, x1, y0, y1, skip_sheet=None, z_bottom=SLAB_TOP, z_top=FLOOR_Z):
    """Rim joists, joists spanning Y, and a partly-laid subfloor."""
    zj0, zj1 = z_bottom, z_top - SUBFLOOR_T
    FLOOR_TOP = z_top
    i = WALL_T - STUD_D  # tuck the rim just inside the outer wall line
    m.box((x0 + i, y0 + i, zj0), (x1 - i, y0 + i + JOIST_W, zj1), "framing_dark")
    m.box((x0 + i, y1 - i - JOIST_W, zj0), (x1 - i, y1 - i, zj1), "framing_dark")
    m.box((x0 + i, y0 + i, zj0), (x0 + i + JOIST_W, y1 - i, zj1), "framing_dark")
    m.box((x1 - i - JOIST_W, y0 + i, zj0), (x1 - i, y1 - i, zj1), "framing_dark")
    x = x0 + i + JOIST_SPACING
    while x < x1 - i - JOIST_W:
        m.box((x - JOIST_W / 2, y0 + i + JOIST_W, zj0), (x + JOIST_W / 2, y1 - i - JOIST_W, zj1),
              "framing")
        x += JOIST_SPACING
    # subfloor sheets (2.4 x 1.2 m), a few left out so joists show
    sw, sd, gap = 2.4, 1.2, 0.03
    yy = y0 + i
    row = 0
    while yy < y1 - i - 0.05:
        xx = x0 + i + (0 if row % 2 == 0 else -sw / 2)
        col = 0
        while xx < x1 - i - 0.05:
            if not (skip_sheet and skip_sheet(row, col)):
                a = max(xx, x0 + i)
                b = min(xx + sw, x1 - i)
                m.box((a + gap, yy + gap, zj1), (b - gap, min(yy + sd, y1 - i) - gap, FLOOR_TOP),
                      "subfloor")
            xx += sw
            col += 1
        yy += sd
        row += 1


# ---------------------------------------------------------------------------
# Scene, attachment points, export, render
# ---------------------------------------------------------------------------

def reset_scene():
    bpy.ops.wm.read_factory_settings(use_empty=True)


def attach_point(name, loc, parent=None):
    e = bpy.data.objects.new(name, None)
    e.empty_display_type = "ARROWS"
    e.empty_display_size = 0.6
    e.location = loc
    bpy.context.scene.collection.objects.link(e)
    if parent is not None:
        e.parent = parent
    return e


def export_glb(path, objects):
    bpy.ops.object.select_all(action="DESELECT")
    for o in objects:
        o.select_set(True)
        for c in o.children:
            c.select_set(True)
    bpy.ops.export_scene.gltf(filepath=path, export_format="GLB", use_selection=True,
                              export_apply=True, export_yup=True)


def ground_plane(size, hex_color, z=-0.005, name="Ground"):
    m = MeshBuilder(name, {"g": hex_color})
    m.box((-size / 2, -size / 2, z - 0.05), (size / 2, size / 2, z), "g")
    return m.build()


def setup_render(path, res=(1200, 900), bg="#EEF1EC", samples=48):
    sc = bpy.context.scene
    sc.render.engine = "BLENDER_EEVEE"
    sc.render.resolution_x, sc.render.resolution_y = res
    sc.render.resolution_percentage = 100
    sc.render.filepath = path
    sc.render.image_settings.file_format = "PNG"
    sc.view_settings.view_transform = "Standard"
    sc.view_settings.look = "None"
    try:
        sc.eevee.taa_render_samples = samples
        sc.eevee.use_shadows = True
        sc.eevee.shadow_ray_count = 2
        sc.eevee.shadow_step_count = 8
        sc.eevee.use_gtao = True
        sc.eevee.gtao_distance = 1.5
    except AttributeError:
        pass
    world = bpy.data.worlds.new("World")
    sc.world = world
    world.use_nodes = True
    bgn = world.node_tree.nodes["Background"]
    bgn.inputs["Color"].default_value = hex_rgba(bg)
    bgn.inputs["Strength"].default_value = 0.9
    sun_data = bpy.data.lights.new("Sun", "SUN")
    sun_data.energy = 3.2
    sun_data.angle = math.radians(6)
    sun = bpy.data.objects.new("Sun", sun_data)
    sun.rotation_euler = (math.radians(42), math.radians(-18), math.radians(-35))
    sc.collection.objects.link(sun)


GAME_PITCH = 45.0
GAME_YAW = -40.0   # camera sits front-left, looking back-right


def frame_camera(objects, margin=1.12, yaw=GAME_YAW, pitch=GAME_PITCH, res=(1200, 900)):
    """Orthographic game-angle camera fitted to the objects' bounds."""
    sc = bpy.context.scene
    cam_data = bpy.data.cameras.new("Cam")
    cam_data.type = "ORTHO"
    cam = bpy.data.objects.new("Cam", cam_data)
    sc.collection.objects.link(cam)
    cam.rotation_euler = (math.radians(90 - pitch), 0, math.radians(yaw))
    sc.camera = cam
    bpy.context.view_layer.update()
    rot = cam.rotation_euler.to_matrix()
    inv = rot.inverted()
    pts = []
    for o in objects:
        if o.type != "MESH":
            continue
        for c in o.bound_box:
            pts.append(inv @ (o.matrix_world @ Vector(c)))
    xs, ys, zs = [p.x for p in pts], [p.y for p in pts], [p.z for p in pts]
    cx, cy = (min(xs) + max(xs)) / 2, (min(ys) + max(ys)) / 2
    w, h = max(xs) - min(xs), max(ys) - min(ys)
    aspect = res[0] / res[1]
    cam_data.ortho_scale = max(w, h * aspect) * margin
    center = rot @ Vector((cx, cy, max(zs) + 60))
    cam.location = center
    cam_data.clip_end = 500
    return cam


def render():
    bpy.ops.render.render(write_still=True)


# ---------------------------------------------------------------------------
# Upgrade-path layout shared by every base
# ---------------------------------------------------------------------------

STAIR_GAP = 0.9          # deck edge -> patio edge, bridged by the deck stairs
DECK_MAX_W = 6.0
ADDITION_BACK_INSET = 3.75   # addition centre = back wall y - this (keeps the patio beside the deck)


def deck_layout(half_width):
    """Deck centre x and width for a base of the given half width: the deck's
    +X edge stops STAIR_GAP short of the patio (which starts at the addition
    slab), so the side stairs land on the patio."""
    xr = half_width + SLAB_OVERHANG - STAIR_GAP
    return xr - DECK_MAX_W / 2, DECK_MAX_W


def wedge_apron(m, x0, x1, y_wall, depth, z_top, mat="concrete"):
    """Sloped concrete apron from a -Y facing wall down to the ground."""
    y_out = y_wall - depth
    m.hexahedron([(x0, y_out, 0), (x1, y_out, 0), (x1, y_wall, 0), (x0, y_wall, 0),
                  (x0, y_out, 0.04), (x1, y_out, 0.04), (x1, y_wall, z_top), (x0, y_wall, z_top)], mat)
