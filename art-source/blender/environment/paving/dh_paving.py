"""Paving for #803 (P1 approved, #851): concrete sidewalks with expansion joints, front
walkways and driveways, on the approved road kit (look A, #836) and lawn
(look C, #801). Blender meshes built from dh_paving_geom's shapes.

Draw calls: ONE material per tile for every concrete surface (sidewalks,
poured walkways, concrete driveways and aprons all sample the option's
1024 x 512 slab atlas); a paver or flagstone walkway adds one material on the
lot, an asphalt driveway reuses the road's asphalt material (already bound).

Joints live in the texture, not in geometry: every slab is mapped onto one
whole atlas slot, whose border is the joint. The slot (variant 0..7) and a
0.95..1.0 grey tone in vertex colour COLOR_0 come from a hash of the slab's
world centre, so neighbouring slabs differ and nothing repeats.

Heights (metres above the lawn at z = 0): asphalt 0.010, sidewalk, walkway,
driveway and apron 0.012 (dh_ground.FLUSH_Z, as the road kit), curb band
0.013 (it crosses the driveway apron), paint 0.0145.

Conventions as the rest of art-source/blender: metres, Z up, world
coordinates, Blender X = Core X (east), Blender Y = Core Z (north).
"""
import math
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
sys.path.insert(0, os.path.normpath(os.path.join(HERE, "..", "roads")))
import dh_roads as D  # noqa: E402  (puts ../ground and the kit on the path)
import bpy  # noqa: E402
import dh_ground as G  # noqa: E402
import dh_road_geom as R  # noqa: E402
import dh_paving_geom as PG  # noqa: E402
import dh_paving_tex as PT  # noqa: E402

PAVE_Z = D.SIDEWALK_Z           # 0.012
ROAD_LOOK = "a"                 # Derek's road pick (#836)
ROUGH = 0.86
PAVER_COURSE = 0.25             # paver course depth (dh_paving_tex.pavers): walkways fit whole courses
PAVER_ACROSS = 0.50
WALK_TUCK = 0.05                # the walkway runs this far under the house front / bottom step
STEP_GROUND = 1e-6              # a box with z0 within this of 0 stands on the ground
STARTER_BASE = 2                # the approved starter lot's base (dh_ground.lot_scene default)

OPTIONS = {
    # P1 revised after Derek's review: walkway as wide as the narrower part of
    # the house's front steps, straight driveway (no flare)
    "p1": {"label": "P1. Classic broom (rev)",
           "sub": "light broom-finish concrete, tooled joints every 1.5 m,\nwalkway = front-step width, straight concrete driveway",
           "concrete": "classic", "spacing": 1.5, "walk": "poured", "walk_w": "steps", "drive": "straight",
           "drive_mat": "concrete", "align": True},   # align: lot joints continue the sidewalk's (Derek)
    "p2": {"label": "P2. Sawn grey",
           "sub": "grey smooth concrete, sawn joints every 2 m,\ncharcoal paver walkway, asphalt driveway",
           "concrete": "sawn", "spacing": 2.0, "walk": "pavers", "walk_w": "core", "drive": "straight",
           "drive_mat": "asphalt"},
    "p3": {"label": "P3. Warm cottage",
           "sub": "warm exposed-aggregate concrete, tooled joints every 2 m,\nflagstone walkway, ribbon driveway",
           "concrete": "warm", "spacing": 2.0, "walk": "flagstone", "walk_w": "core", "drive": "ribbon",
           "drive_mat": "concrete"},
}


# ---------------------------------------------------------------------------
# materials
# ---------------------------------------------------------------------------
def _tex_material(name, col, nrm, vcol=True):
    mat = bpy.data.materials.get(name)
    if mat:
        return mat
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    nt = mat.node_tree
    bsdf = nt.nodes["Principled BSDF"]
    tc = D._img(nt, col)
    tn = D._img(nt, nrm, True)
    out = tc.outputs["Color"]
    if vcol:
        va = nt.nodes.new("ShaderNodeVertexColor")
        va.layer_name = "Col"
        out = D._mul(nt, out, va.outputs["Color"])
    nt.links.new(out, bsdf.inputs["Base Color"])
    nm = nt.nodes.new("ShaderNodeNormalMap")
    nm.inputs["Strength"].default_value = 0.9
    nt.links.new(tn.outputs["Color"], nm.inputs["Color"])
    nt.links.new(nm.outputs["Normal"], bsdf.inputs["Normal"])
    bsdf.inputs["Roughness"].default_value = ROUGH
    if "Specular IOR Level" in bsdf.inputs:
        bsdf.inputs["Specular IOR Level"].default_value = 0.25
    mat.use_backface_culling = True
    return mat


def concrete_material(opt):
    key = OPTIONS[opt]["concrete"]
    col, nrm = PT.need("concrete_" + key)
    return _tex_material("DH_Paving_concrete_%s" % key, col, nrm)


def walk_material(kind):
    col, nrm = PT.need(kind)
    return _tex_material("DH_Paving_%s" % kind, col, nrm)


def materials(opt, export=False):
    return {"concrete": concrete_material(opt), "pavers": walk_material("pavers"),
            "flagstone": walk_material("flagstone"), "asphalt": D.asphalt_material(ROAD_LOOK, macro=not export)}


# ---------------------------------------------------------------------------
# meshes
# ---------------------------------------------------------------------------
class PavingMesh(D.RoadMesh):
    def slab(self, sl, off, z, mat="concrete"):
        """One concrete slab on one atlas slot."""
        import shapely
        c = sl.poly.centroid
        k, t = PG.slab_hash(c.x + off[0], c.y + off[1])
        col = (t, t, t, 1.0)
        for p in getattr(sl.poly, "geoms", [sl.poly]):
            if p.geom_type != "Polygon" or p.area < 1e-9:
                continue
            for tri in shapely.constrained_delaunay_triangles(p).geoms:
                q = list(tri.exterior.coords)[:3]
                uvs = [PT.slot_uv(k, *sl.uv(x, y)) for x, y in q]
                self._face([(x + off[0], y + off[1], z) for x, y in q], uvs, mat, col)

    def local(self, poly, off, z, mat, uvfn):
        """A polygon with UVs from uvfn(x, y) (tile/lot-local)."""
        import shapely
        for p in getattr(poly, "geoms", [poly]):
            if p.geom_type != "Polygon" or p.area < 1e-9:
                continue
            for tri in shapely.constrained_delaunay_triangles(p).geoms:
                q = list(tri.exterior.coords)[:3]
                self._face([(x + off[0], y + off[1], z) for x, y in q], [uvfn(x, y) for x, y in q], mat)


def build_sidewalks(ttype, coord, opt, centre=None, name=None, frontage=()):
    """One tile's sidewalk slabs. `frontage`: WORLD x/y points where this
    tile's lots put joints (lot_frontage), used only by aligned options."""
    lay = R.Layout(ttype)
    if lay.kind == "green":
        return None, []
    off = centre if centre is not None else R.tile_centre(coord)
    local = [(x - off[0], y - off[1]) for x, y in frontage] if OPTIONS[opt].get("align") else []
    slabs = PG.sidewalk_slabs(lay, OPTIONS[opt]["spacing"], frontage=local)
    m = PavingMesh(name or "paving_%s_%s_%d_%d" % (opt, ttype, coord[0], coord[1]))
    for sl in slabs:
        m.slab(sl, off, PAVE_Z)
    return m.build(materials(opt), uv1=False), slabs


def build_area(tiles, opt, lawn=True, origin=(0.0, 0.0), frontage=None):
    """Lawn + road kit (look A, no placeholder sidewalks) + this option's
    sidewalks for {(col, row): type}. `frontage`: {(col, row): [world
    points]}, each tile's own lots only."""
    objs, info = [], {}
    for (c, r), t in tiles.items():
        cx, cy = origin[0] + c * R.TILE, origin[1] + r * R.TILE
        if lawn:
            objs.append(G.lawn_tile(D.LAWN_LOOK, D.LAWN_APPROACH, cx - R.HALF, cy - R.HALF))
        res = D.build_tile(t, (c, r), ROAD_LOOK, centre=(cx, cy), sidewalks=False)
        ob, slabs = build_sidewalks(t, (c, r), opt, centre=(cx, cy), frontage=(frontage or {}).get((c, r), ()))
        info[(c, r)] = {"road": res, "slabs": slabs, "paving": ob}
        objs += [o for o in (res["road"], ob) if o is not None]
    return objs, info


def front_steps(n):
    """The front steps of base n, READ FROM ITS GEOMETRY: every box the base
    builds is recorded while it builds, and the bottom step is the
    frontmost (lowest y) box standing on the ground (z0 = 0) across the
    door's x. That box is the narrower part of the steps (each base's
    stoop or porch behind it is wider). Base-local = lot-scene world
    coordinates (dh_scene places the base at the origin).
    Returns {"x": (x0, x1), "width", "y_front", "door_x", "mat"}."""
    import importlib
    K = D.K
    B = importlib.import_module("base_house_%d" % n)
    door_x = getattr(B, "DOOR_X", 0.0)
    probe = "_steps_probe_%d" % n
    rec = []
    orig = K.MeshBuilder.box

    def spy(self, mn, mx, mat):
        if self.name == probe:
            rec.append((tuple(mn), tuple(mx), mat))
        return orig(self, mn, mx, mat)
    K.MeshBuilder.box = spy
    try:
        ob, att = B.build("finished", name=probe)
    finally:
        K.MeshBuilder.box = orig
    for o in [ob] + list(att):
        bpy.data.objects.remove(o)
    cand = [(mn, mx, mat) for mn, mx, mat in rec if abs(mn[2]) < STEP_GROUND and mn[0] < door_x < mx[0]]
    mn, mx, mat = min(cand, key=lambda b: b[0][1])
    return {"x": (mn[0], mx[0]), "width": mx[0] - mn[0], "y_front": mn[1], "door_x": door_x, "mat": mat}


def lot_frontage(steps, drive_x):
    """Lot-frame x of the joint lines a lot puts on its sidewalk."""
    k = PG.DRIVE_SLABS
    x0, x1 = drive_x
    return {"walk": list(steps["x"]), "drive": [x0 + (x1 - x0) * i / k for i in range(k + 1)]}


def lot_paving(opt, door, road_cl_y, drive_x, y_apron, name="lot_paving", export=False, steps=None):
    """Front walkway + driveway for a lot NORTH of a road along X (the lot
    scene's frame), in world coordinates. Returns (object, shapes).
    With walk_w "steps" the walkway is as wide as the bottom front step
    (`steps`, from front_steps) and centred on it, starting at its front
    edge; with "core" it is Core's walk width (2 m) centred on the door."""
    O = OPTIONS[opt]
    m = PavingMesh(name)
    if O.get("walk_w") == "steps":
        sx0, sx1 = steps["x"]
        start, width = ((sx0 + sx1) / 2, steps["y_front"]), sx1 - sx0
    else:
        start, width = door, PG.WALK_W
    attach = (start[0], road_cl_y + R.OFF)
    wpoly, axis = PG.walkway((start[0], start[1] + WALK_TUCK), attach, width)
    zero = (0.0, 0.0)
    along = O["spacing"] if O.get("align") else None
    if O["walk"] == "poured":
        for sl in PG.walkway_slabs(wpoly, axis, along or PG.WALK_PANEL_MAX, width):
            m.slab(sl, zero, PAVE_Z)
    else:
        L = axis.length
        a = axis.coords[0]
        dx, dy = (axis.coords[1][0] - a[0]) / L, (axis.coords[1][1] - a[1]) / L
        if O["walk"] == "pavers":   # whole courses along, two pavers across 1 m; long side across
            n = max(1, round(L / PAVER_COURSE))
            sv = n * PAVER_COURSE / L

            def uvfn(x, y):
                s = (x - a[0]) * dx + (y - a[1]) * dy
                lat = (x - a[0]) * -dy + (y - a[1]) * dx
                return ((lat + PG.WALK_W / 2) / PT.PAVER_M, s * sv / PT.PAVER_M)
        else:
            def uvfn(x, y):
                return (x / PT.FLAG_M, y / PT.FLAG_M)
        m.local(wpoly, zero, PAVE_Z, O["walk"], uvfn)
    dv = PG.driveway(O["drive"], drive_x[0], drive_x[1], road_cl_y + R.ROAD_HALF, y_apron)
    if O["drive_mat"] == "asphalt":
        for p in dv["drive"]:
            m.local(p, zero, PAVE_Z, "asphalt", lambda x, y: (x / D.RT.ASPHALT_M, y / D.RT.ASPHALT_M))
        for sl in PG.drive_slabs([dv["apron"]], split_wide=False):
            m.slab(sl, zero, PAVE_Z)
    else:
        for sl in PG.drive_slabs(dv["drive"], along or PG.DRIVE_PANEL) + \
                PG.drive_slabs([dv["apron"]], along or PG.DRIVE_PANEL, split_wide=bool(along)):
            m.slab(sl, zero, PAVE_Z)
    ob = m.build(materials(opt, export=export), uv1=True)
    return ob, {"walkway": wpoly, "axis": axis, "drive": dv, "walk_width": width}


def starter_scene(opt, fence_style="splitrail", export=False, base=STARTER_BASE):
    """The approved starter lot (ranch, base 2, level 4, fence style C) on
    the road kit, with this option's sidewalks, walkway and driveway.
    `base` swaps in another base on the same lot (for review close-ups)."""
    steps = front_steps(base)
    sc = G.lot_scene(base=base, fence_style=fence_style)
    for o in sc["streets"] + [sc["paving"]]:
        bpy.data.objects.remove(o)
    cx, cy = sc["tile_centre"]
    lot = sc["lot"]
    fr = lot_frontage(steps, lot["dinfo"]["x"])
    objs, info = build_area(R.STARTER, opt, origin=(cx, cy),
                            frontage={(0, 0): [(x, cy + R.OFF) for x in fr["walk"] + fr["drive"]]})
    door = (getattr(lot["B"], "DOOR_X", 0.0), lot["bounds"][2])
    ob, shapes = lot_paving(opt, door, cy, lot["dinfo"]["x"], lot["dinfo"]["y"][1], export=export, steps=steps)
    sc.update({"area_objs": objs, "area_info": info, "lot_paving": ob, "shapes": shapes, "door": door,
               "steps": steps})
    return sc


# ---------------------------------------------------------------------------
# a street of houses (review: alignment at scale)
# ---------------------------------------------------------------------------
ROW_TILES = {(-1, 0): "StraightEW", (0, 0): "StraightEW"}
ROW_LOT_X = (-75.0, -45.0, -15.0, 15.0)        # lot centres: two quadrants per tile, the border at x = -30
ROW_LOT_Y = R.OFF + R.SW / 2                   # the lots stand north of the road along X (centreline y = 0)


def street_row(opt, bases):
    """Level-4 houses of `bases` in a row north of a straight road across
    two tiles, each centred on its lot quadrant, with this option's
    walkways, driveways and per-tile aligned sidewalks. Placement here is a
    REVIEW layout only (no fences); in the game Core places the houses."""
    import dh_scene as S
    K = D.K
    K.reset_scene()
    lots, frontage = [], {}
    for n, lx in zip(bases, ROW_LOT_X):
        import importlib
        B = importlib.import_module("base_house_%d" % n)
        steps = front_steps(n)
        pal = K.scheme_palette(B.RENDER_SCHEME)
        objs = S.build_level(B, pal, 4)
        dw, dinfo = S.driveway(B, palette=pal)
        bpy.data.objects.remove(dw)
        x0, x1, _, _ = S.composite_bounds(objs)
        # base-local road centreline is at FRONT_Y - FRONT_D: move it to y = 0
        dx, dy = lx - (x0 + x1) / 2, -(B.FRONT_Y - S.FRONT_D)
        for o in objs:
            if o.parent is None:
                o.location.x += dx
                o.location.y += dy
        st = dict(steps, x=(steps["x"][0] + dx, steps["x"][1] + dx), y_front=steps["y_front"] + dy,
                  door_x=steps["door_x"] + dx)
        drive_x = (dinfo["x"][0] + dx, dinfo["x"][1] + dx)
        y_apron = dinfo["y"][1] + dy
        fr = lot_frontage(st, drive_x)
        tile = (int(math.floor((lx + R.HALF) / R.TILE)), 0)
        frontage.setdefault(tile, []).extend((x, R.OFF) for x in fr["walk"] + fr["drive"])
        lots.append((n, st, drive_x, y_apron))
    objs, info = build_area(ROW_TILES, opt, frontage=frontage)
    for i, (n, st, drive_x, y_apron) in enumerate(lots):
        lot_paving(opt, (st["door_x"], st["y_front"]), 0.0, drive_x, y_apron, name="row_paving_%d" % i, steps=st)
    return {"lots": lots, "frontage": frontage, "info": info}
