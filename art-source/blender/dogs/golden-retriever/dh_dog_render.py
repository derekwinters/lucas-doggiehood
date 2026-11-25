"""Shared render helpers for the dog review renders (EEVEE, headless).

EEVEE needs a GPU API. In a container without one, point Vulkan at Mesa's
lavapipe before running (render_dog.py does this itself when the variable is
unset): VK_ICD_FILENAMES=/opt/lavapipe/lvp_icd.json
"""
import math
import os

import bpy
from mathutils import Vector

BG = "#F1ECE3"
FLOOR = "#B9B3A8"


def hex_lin(h, a=1.0):
    h = h.lstrip("#")
    out = []
    for i in (0, 2, 4):
        c = int(h[i:i + 2], 16) / 255.0
        out.append(c / 12.92 if c <= 0.04045 else ((c + 0.055) / 1.055) ** 2.4)
    return (*out, a)


def reset():
    bpy.ops.wm.read_factory_settings(use_empty=True)


def setup(path, res=(1600, 1000), samples=64, bg=BG, sun_yaw=-35, sun_pitch=48, energy=3.4):
    sc = bpy.context.scene
    sc.render.engine = "BLENDER_EEVEE"
    sc.render.resolution_x, sc.render.resolution_y = res
    sc.render.resolution_percentage = 100
    sc.render.filepath = path
    sc.render.image_settings.file_format = "PNG"
    sc.render.film_transparent = False
    sc.view_settings.view_transform = "Standard"
    sc.view_settings.look = "None"
    try:
        sc.eevee.taa_render_samples = samples
        sc.eevee.use_shadows = True
        sc.eevee.shadow_ray_count = 2
        sc.eevee.shadow_step_count = 8
        sc.eevee.use_gtao = True
        sc.eevee.gtao_distance = 0.4
    except AttributeError:
        pass
    world = bpy.data.worlds.new("World")
    sc.world = world
    world.use_nodes = True
    bgn = world.node_tree.nodes["Background"]
    bgn.inputs["Color"].default_value = hex_lin(bg)
    bgn.inputs["Strength"].default_value = 1.0
    # warm key sun
    sd = bpy.data.lights.new("Sun", "SUN")
    sd.energy = energy
    sd.color = (1.0, 0.95, 0.87)
    sd.angle = math.radians(8)
    sun = bpy.data.objects.new("Sun", sd)
    sun.rotation_euler = (math.radians(90 - sun_pitch), 0, math.radians(sun_yaw + 180))
    sc.collection.objects.link(sun)
    # cool soft fill from the other side, no shadow
    fd = bpy.data.lights.new("Fill", "SUN")
    fd.energy = 0.9
    fd.color = (0.88, 0.92, 1.0)
    fd.angle = math.radians(30)
    try:
        fd.use_shadow = False
    except AttributeError:
        pass
    fill = bpy.data.objects.new("Fill", fd)
    fill.rotation_euler = (math.radians(70), 0, math.radians(sun_yaw + 40))
    sc.collection.objects.link(fill)
    return sc


def floor(size=30, hex_color=FLOOR, z=0.0):
    me = bpy.data.meshes.new("floor")
    s = size / 2
    me.from_pydata([(-s, -s, z), (s, -s, z), (s, s, z), (-s, s, z)], [], [(0, 1, 2, 3)])
    m = bpy.data.materials.new("floor")
    m.use_nodes = True
    b = m.node_tree.nodes["Principled BSDF"]
    b.inputs["Base Color"].default_value = hex_lin(hex_color)
    b.inputs["Roughness"].default_value = 0.95
    me.materials.append(m)
    ob = bpy.data.objects.new("floor", me)
    bpy.context.scene.collection.objects.link(ob)
    return ob


def world_points(objs):
    """World positions of the objects' vertices as deformed (armature applied)."""
    pts = []
    dg = bpy.context.evaluated_depsgraph_get()
    for o in objs:
        if o.type != "MESH":
            continue
        oe = o.evaluated_get(dg)
        me = oe.to_mesh()
        mw = oe.matrix_world
        pts.extend(mw @ v.co for v in me.vertices)
        oe.to_mesh_clear()
    return pts


def box_object(lo, hi):
    """An unrendered mesh with the corners of a box, for framing a camera on a region."""
    vs = [(x, y, z) for x in (lo[0], hi[0]) for y in (lo[1], hi[1]) for z in (lo[2], hi[2])]
    me = bpy.data.meshes.new("frame_box")
    me.from_pydata(vs, [], [])
    ob = bpy.data.objects.new("frame_box", me)
    ob.hide_render = True
    bpy.context.scene.collection.objects.link(ob)
    return ob


def camera(objs, yaw, pitch, res, margin=1.15, ortho=True, lens=85, center=None):
    """Camera looking at the objects from (yaw, pitch). yaw 0 = from the front (-Y)."""
    sc = bpy.context.scene
    cd = bpy.data.cameras.new("Cam")
    cd.type = "ORTHO" if ortho else "PERSP"
    cd.lens = lens
    cam = bpy.data.objects.new("Cam", cd)
    sc.collection.objects.link(cam)
    cam.rotation_euler = (math.radians(90 - pitch), 0, math.radians(yaw))
    sc.camera = cam
    bpy.context.view_layer.update()
    rot = cam.rotation_euler.to_matrix()
    inv = rot.inverted()
    pts = [inv @ p for p in world_points(objs)]
    xs, ys, zs = [p.x for p in pts], [p.y for p in pts], [p.z for p in pts]
    cx, cy = (min(xs) + max(xs)) / 2, (min(ys) + max(ys)) / 2
    w, h = max(xs) - min(xs), max(ys) - min(ys)
    aspect = res[0] / res[1]
    span = max(w, h * aspect) * margin
    cd.clip_end = 400
    if ortho:
        cd.ortho_scale = span
        cam.location = rot @ Vector((cx, cy, max(zs) + 40))
    else:
        fov = 2 * math.atan(cd.sensor_width / (2 * lens))
        dist = span / 2 / math.tan(fov / 2) + (max(zs) - min(zs)) / 2
        cam.location = rot @ Vector((cx, cy, (max(zs) + min(zs)) / 2 + dist))
    return cam


def render():
    bpy.ops.render.render(write_still=True)
