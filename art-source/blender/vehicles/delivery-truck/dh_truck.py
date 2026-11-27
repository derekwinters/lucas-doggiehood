"""Blender delivery truck (#924): a box delivery truck with a car-colour body,
"Delivery" lettered on both sides, four separate wheels and a swinging rear door. No driver or people.

All numbers are in truck_dims.py. build() returns the root object
(`delivery_truck`, the body mesh, pivot on the ground at the footprint centre)
with five children, each with its pivot where it moves:

  wheel_front_left, wheel_front_right, wheel_rear_left, wheel_rear_right
      origin on the axle centre; a wheel spins about its local X axis
  door_rear
      origin on the hinge (bottom of the left door post's inner edge, at the
      rear face); the door swings about its local Z axis, open = DOOR_OPEN_DEG
  decal_delivery_left, decal_delivery_right
      the word "Delivery" on each long side of the cargo box (static), in its
      own untinted lettering materials

The body is drawn through one material, DH_Truck_body (white, for Unity's
colour multiply with Palette.CarColorHex); glass, tyres, trim, hubs and lamps
are their own untinted materials. Uses the house kit's MeshBuilder
(art-source/blender/dh_house_kit.py) by relative path.
"""
import math
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
sys.path.insert(0, os.path.normpath(os.path.join(HERE, "..", "..")))
import bpy  # noqa: E402
import bmesh  # noqa: E402
import dh_house_kit as K  # noqa: E402
import truck_dims as D  # noqa: E402

NAME = "delivery_truck"


def palette():
    p = {k: v[0] for k, v in D.MATERIALS.items()}
    p["_id"] = "Truck"
    return p


def _finish_materials():
    """Roughness per slot (the kit's material() uses one roughness for all)."""
    for slot, (_, rough) in D.MATERIALS.items():
        m = bpy.data.materials.get(D.MATERIAL_PREFIX + slot)
        if m is not None:
            m.node_tree.nodes["Principled BSDF"].inputs["Roughness"].default_value = rough


def _arch(cy, r, n=D.ARCH_SEGMENTS, z=D.BODY_BOTTOM):
    """Arch points (y, z) over an axle at y=cy, from the rear foot to the front foot."""
    return [(cy + r * math.cos(math.pi * k / n), z + r * math.sin(math.pi * k / n)) for k in range(n + 1)]


def profile_prism(m, prof, x0, x1, mat, edge_mats=None):
    """Extrude a (y, z) side profile across X from x0 to x1. edge_mats[i] is the
    material of the side face from prof[i] to prof[i+1] (default `mat`)."""
    a = [(x0, y, z) for y, z in prof]
    b = [(x1, y, z) for y, z in prof]
    n = len(prof)
    m._faces(a, [tuple(reversed(range(n)))], mat)
    m._faces(b, [tuple(range(n))], mat)
    for i in range(n):
        j = (i + 1) % n
        em = edge_mats[i] if edge_mats else mat
        m._faces([a[i], a[j], b[j], b[i]], [(0, 1, 2, 3)], em)


def _with_arch(prof_top, y_rear, y_front, axle_y):
    """Close a profile: prof_top runs front (low y) to rear (high y) along the
    top; then down the rear face, along the bottom with an arch over axle_y,
    and up the front face. Returns (points, edge materials)."""
    pts = list(prof_top) + [(y_rear, D.BODY_BOTTOM)]
    arch = _arch(axle_y, D.ARCH_RADIUS)
    pts += arch
    pts += [(y_front, D.BODY_BOTTOM)]
    mats = ["body"] * len(pts)
    first = len(prof_top) + 1          # index of the arch's first point
    for i in range(first, first + len(arch) - 1):
        mats[i] = "trim"
    return pts, mats


def _glass_quad(m, quad_yz, x, outward):
    """A thin glass slab on a side face at X = x, standing GLASS_T proud outward (+1/-1)."""
    x1 = x + outward * D.GLASS_T
    lo, hi = (x, x1) if outward > 0 else (x1, x)
    profile_prism(m, quad_yz, lo, hi, "glass")


def build_body():
    m = K.MeshBuilder(NAME, palette())
    hw = D.HALF_W                       # mirrors reach the footprint's edge
    bw = D.BOX_HALF_W                   # the cargo box stops short of it by the lettering's depth
    # cab: hood, windscreen, roof; arch over the front axle
    cab_top = [(D.CAB_FRONT_Y, D.BODY_BOTTOM)] + D.CAB_PROFILE
    pts, mats = _with_arch(cab_top[1:], D.CAB_REAR_Y, D.CAB_FRONT_Y, D.AXLE_FRONT_Y)
    pts = [cab_top[0]] + pts[:-1]
    mats = ["body"] + mats[:-1]
    profile_prism(m, pts, -D.CAB_HALF_W, D.CAB_HALF_W, "body", mats)

    # cargo box: solid in front of the hold, arch over the rear axle, chamfered top front edge
    hold_y = D.BOX_REAR_Y - D.HOLD_DEPTH
    box_top = [(D.BOX_FRONT_Y, D.HEIGHT - D.BOX_CHAMFER), (D.BOX_FRONT_Y + D.BOX_CHAMFER, D.HEIGHT),
               (hold_y, D.HEIGHT)]
    pts, mats = _with_arch(box_top, hold_y, D.BOX_FRONT_Y, D.AXLE_REAR_Y)
    pts = [(D.BOX_FRONT_Y, D.BODY_BOTTOM)] + pts[:-1]
    mats = ["body"] + mats[:-1]
    # the rear face of the solid part is the back wall of the cargo hold
    mats[len(box_top)] = "trim"
    profile_prism(m, pts, -bw, bw, "body", mats)

    # the hold's shell: two door posts, roof, floor sill
    m.box((D.POST_X, hold_y, D.BODY_BOTTOM), (bw, D.BOX_REAR_Y, D.HEIGHT), "body")
    m.box((-bw, hold_y, D.BODY_BOTTOM), (-D.POST_X, D.BOX_REAR_Y, D.HEIGHT), "body")
    m.box((-D.POST_X, hold_y, D.DOOR_TOP), (D.POST_X, D.BOX_REAR_Y, D.HEIGHT), "body")
    m.box((-D.POST_X, hold_y, D.BODY_BOTTOM), (D.POST_X, D.BOX_REAR_Y, D.DOOR_BOTTOM), "trim")

    # chassis and dark wheel-well liners (so the arches don't show daylight)
    c = D.CHASSIS
    m.box((-c["half_w"], c["y0"], c["z0"]), (c["half_w"], c["y1"], D.BODY_BOTTOM), "trim")
    for ay in (D.AXLE_FRONT_Y, D.AXLE_REAR_Y):
        m.box((-D.WELL_HALF_W, ay - D.ARCH_RADIUS, D.BODY_BOTTOM),
              (D.WELL_HALF_W, ay + D.ARCH_RADIUS, D.BODY_BOTTOM + D.ARCH_RADIUS), "trim")

    # bumpers
    bf, br = D.BUMPER_FRONT, D.BUMPER_REAR
    m.box((-bf["half_w"], -D.HALF_L, bf["z0"]), (bf["half_w"], bf["y1"], bf["z1"]), "trim")
    m.box((-br["half_w"], br["y0"], br["z0"]), (br["half_w"], D.HALF_L, br["z1"]), "trim")

    # cab front: grille and headlights
    fy0, fy1 = D.CAB_FRONT_Y - D.FRONT_PROUD, D.CAB_FRONT_Y + 0.01
    g = D.GRILLE
    m.box((-g["half_w"], fy0, g["z0"]), (g["half_w"], fy1, g["z1"]), "trim")
    h = D.HEADLIGHT
    for s in (1, -1):
        xa, xb = sorted((s * h["x0"], s * h["x1"]))
        m.box((xa, fy0, h["z0"]), (xb, fy1, h["z1"]), "headlight")

    # windscreen glass, parallel to the windscreen and inset from its edges
    (ya, za), (yb, zb) = D.WINDSCREEN
    L = math.hypot(yb - ya, zb - za)
    dy, dz = (yb - ya) / L, (zb - za) / L
    ny, nz = -dz, dy                     # outward normal (forward and up)
    i = D.WINDSCREEN_INSET
    p0 = (ya + dy * i, za + dz * i)
    p1 = (yb - dy * i, zb - dz * i)
    gx = D.CAB_HALF_W - i
    quad = [(p0[0] - ny * 0.002, p0[1] - nz * 0.002), (p1[0] - ny * 0.002, p1[1] - nz * 0.002),
            (p1[0] + ny * D.GLASS_T, p1[1] + nz * D.GLASS_T), (p0[0] + ny * D.GLASS_T, p0[1] + nz * D.GLASS_T)]
    profile_prism(m, quad, -gx, gx, "glass")

    # side windows: front edge follows the windscreen, inset
    sw = D.SIDE_WINDOW
    slope = (yb - ya) / (zb - za)

    def ws_y(z):
        return ya + (z - za) * slope + sw["front_inset"]
    win = [(ws_y(sw["z0"]), sw["z0"]), (sw["rear_y"], sw["z0"]), (sw["rear_y"], sw["z1"]), (ws_y(sw["z1"]), sw["z1"])]
    for s in (1, -1):
        _glass_quad(m, win, s * D.CAB_HALF_W, s)

    # mirrors on short arms, reaching exactly the box's width
    mr = D.MIRROR
    for s in (1, -1):
        xa, xb = sorted((s * D.CAB_HALF_W, s * mr["head_x0"]))
        m.box((xa, mr["y0"] + 0.02, mr["arm_z"]), (xb, mr["y1"] - 0.02, mr["arm_z"] + mr["arm_h"]), "trim")
        xa, xb = sorted((s * mr["head_x0"], s * hw))
        m.box((xa, mr["y0"], mr["z0"]), (xb, mr["y1"], mr["z1"]), "trim")

    # cab door handles
    ch = D.CAB_HANDLE
    for s in (1, -1):
        xa, xb = sorted((s * D.CAB_HALF_W, s * (D.CAB_HALF_W + ch["proud"])))
        m.box((xa, ch["y0"], ch["z0"]), (xb, ch["y1"], ch["z1"]), "trim")

    # tail lights on the rear door posts
    t = D.TAILLIGHT
    for s in (1, -1):
        xa, xb = sorted((s * t["x0"], s * t["x1"]))
        m.box((xa, D.BOX_REAR_Y - 0.01, t["z0"]), (xb, D.BOX_REAR_Y + t["proud"], t["z1"]), "taillight")
    return m.build()


def _disc(m, x0, x1, r, n, mat, phase=-math.pi / 2):
    """A cylinder about the X axis from x0 to x1; a vertex sits at the bottom."""
    pts = [(r * math.cos(phase + 2 * math.pi * k / n), r * math.sin(phase + 2 * math.pi * k / n))
           for k in range(n)]
    profile_prism(m, pts, x0, x1, mat)


def build_wheel(name, side):
    """One wheel in its own space: axle on local X, origin at the axle centre.
    side = +1 for a left wheel (hub faces +X), -1 for a right wheel."""
    m = K.MeshBuilder(name, palette())
    hw = D.WHEEL_WIDTH / 2
    _disc(m, -hw, hw, D.WHEEL_RADIUS, D.WHEEL_SIDES, "tyre")

    def span(a, b):
        return tuple(sorted((side * a, side * b)))
    _disc(m, *span(hw - 0.01, hw + D.HUB_PROUD), D.HUB_RADIUS, D.HUB_SIDES, "hub")
    _disc(m, *span(hw, hw + D.CAP_PROUD), D.CAP_RADIUS, D.CAP_SIDES, "trim")
    xa, xb = span(hw, hw + D.LUG_PROUD)
    for k in range(D.LUG_COUNT):
        a = math.pi / 2 + 2 * math.pi * k / D.LUG_COUNT
        cy, cz = D.LUG_RING * math.cos(a), D.LUG_RING * math.sin(a)
        s = D.LUG_SIZE / 2
        m.box((xa, cy - s, cz - s), (xb, cy + s, cz + s), "trim")
    return m.build()


def build_door():
    """The rear door in hinge space: hinge line on local Z at X = 0, door
    running to -X, outer face at local Y = 0, bottom at local Z = 0."""
    m = K.MeshBuilder("door_rear", palette())
    w, hgt, t = D.DOOR_WIDTH, D.DOOR_TOP - D.DOOR_BOTTOM, D.DOOR_T
    m.box((-w, -t, 0.0), (0.0, 0.0, hgt), "body")
    r = D.DOOR_RIB
    for z in r["z"]:
        zz = z * hgt
        m.box((-w + r["inset"], 0.0, zz), (-r["inset"], r["proud"], zz + r["h"]), "body")
    dh = D.DOOR_HANDLE
    m.box((dh["x0"], 0.0, dh["z0"]), (dh["x1"], dh["proud"], dh["z1"]), "trim")
    k = D.DOOR_HINGE_KNUCKLE
    for z in k["z"]:
        m.box((-k["size"], 0.0, z), (0.0, k["size"] / 2, z + k["size"] * 2), "trim")
    return m.build()


def _text_faces(offset):
    """The lettering's filled glyphs as triangles in font space (x along the word,
    y up, centred, facing +Z), at the fitted size. offset > 0 fattens the glyphs
    (the outline)."""
    cu = bpy.data.curves.new("_lettering", "FONT")
    cu.body = D.LETTERING_TEXT
    cu.align_x, cu.align_y = "CENTER", "CENTER"
    cu.resolution_u = D.LETTERING_RESOLUTION
    cu.offset = offset
    ob = bpy.data.objects.new("_lettering", cu)
    bpy.context.scene.collection.objects.link(ob)
    me = bpy.data.meshes.new_from_object(ob.evaluated_get(bpy.context.evaluated_depsgraph_get()))
    faces = [[tuple(me.vertices[i].co) for i in p.vertices] for p in me.polygons]
    bpy.data.objects.remove(ob)
    bpy.data.curves.remove(cu)
    bpy.data.meshes.remove(me)
    return faces


def build_decal(name, side):
    """'Delivery' for one side of the cargo box, origin on the box side at
    LETTERING_CENTRE. side = +1 (left, +X) or -1 (right, -X). The word runs toward
    the reader's right from each side (front to back on the left, back to front on
    the right) and every face points outward, so it is never mirrored."""
    fill = _text_faces(0.0)
    outline = _text_faces(D.LETTERING_OUTLINE)
    xs = [v[0] for f in fill for v in f]
    k = D.LETTERING_WIDTH / (max(xs) - min(xs))
    bm = bmesh.new()

    def put(faces, depth, mi):
        for f in faces:
            # font (u, v) -> local (x, y, z): u along +Y on the left side, -Y on the right
            vs = [bm.verts.new((side * depth, side * u * k, v * k)) for u, v, _ in f]
            face = bm.faces.new(vs)
            face.material_index = mi
    put(outline, D.OUTLINE_PROUD, 0)
    put(fill, D.DECAL_PROUD, 1)
    bmesh.ops.triangulate(bm, faces=bm.faces)
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    for slot in ("lettering_outline", "lettering"):
        hexc, _ = D.MATERIALS[slot]
        me.materials.append(K.material(D.MATERIAL_PREFIX + slot, hexc))
    ob = bpy.data.objects.new(name, me)
    bpy.context.scene.collection.objects.link(ob)
    return ob


def build():
    """The whole truck at rest (door closed). Returns the root object."""
    body = build_body()
    for name, p in D.WHEEL_PIVOTS.items():
        wh = build_wheel(name, 1 if p[0] > 0 else -1)
        wh.parent = body
        wh.location = p
    for name, side in D.DECALS.items():
        dec = build_decal(name, side)
        dec.parent = body
        dec.location = (side * D.BOX_HALF_W, D.LETTERING_CENTRE[0], D.LETTERING_CENTRE[1])
    door = build_door()
    door.parent = body
    door.location = D.DOOR_HINGE
    _finish_materials()
    bpy.context.view_layer.update()
    return body


def parts(root):
    return {c.name: c for c in root.children}


def set_pose(root, door_deg=0.0, wheel_deg=0.0):
    """Pose for renders: door swung door_deg about its hinge, wheels turned
    wheel_deg about their axles (positive = rolling forward, toward -Y)."""
    p = parts(root)
    p["door_rear"].rotation_euler = (0.0, 0.0, math.radians(door_deg))
    for n in D.WHEEL_PIVOTS:
        p[n].rotation_euler = (math.radians(wheel_deg), 0.0, 0.0)
    bpy.context.view_layer.update()


def tint(hex_color):
    """Show the body in a car colour (what Unity's multiply over white gives)."""
    m = bpy.data.materials[D.MATERIAL_PREFIX + "body"]
    col = K.hex_rgba(hex_color)
    m.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = col
    m.diffuse_color = col
