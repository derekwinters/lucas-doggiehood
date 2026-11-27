"""Render the 3D splash street from the solved splash camera, and from other angles.

    python3 art-source/splash-3d/render_splash.py OUT_DIR [--scale 0.5] [--focal 2400]
        [--only splash,turnaround]

Writes into OUT_DIR:
  splash-render.png         the render, transparent where there is sky
  splash-render-sky.png     the render over a sky plate made from the original's open sky
  turnaround-<house>-<n>.png  a few houses from other angles (orbit views)
EEVEE needs Mesa's software EGL in a headless container (libegl1, libgl1-mesa-dri).
"""
import argparse
import math
import os
import sys

import bpy
import numpy as np
from mathutils import Vector
from PIL import Image, ImageFilter

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import build_scene as B  # noqa: E402
import splash_data as SD  # noqa: E402

REPO = os.path.abspath(os.path.join(HERE, "..", ".."))
ORIGINAL = os.path.join(REPO, "Assets", "Art", "Splash", "cover-art.png")
SKY_SAT = 0.18           # a pixel of the original counts as open sky when it is this blue (b - r, 0..1)
SKY_FILL_DOWN = 8          # the sky fill runs at 1/8 size
SKY_FILL_ITERS = 3000
SKY_BLUR = 12              # px blur on the filled sky
SKY_B_MIN = 0.85         # open sky is this blue at least (the blue roofs are not)
SKY_R_MAX = 0.72         # ... and not the white clouds
SKY_CLOUDS_TO = 380      # no clouds below this row: the pale haze near the horizon is sky
SKY_ROWS_TO = 600        # rows below this are the far treeline: the sky above carries on
TITLE_BOX = (590, 140, 2240, 530)          # the title on the original (never sampled as sky)
SUN = (1700, 90, 110, 330)                 # sun centre x, y, disc radius, glow radius (px)
SUN_DISC = (252 / 255, 249 / 255, 206 / 255)
SUN_GLOW = (225 / 255, 244 / 255, 224 / 255)
TURN_HOUSES = ("red", "blue", "far_yellow")
TURN_YAWS = (-50, 35, 140)     # degrees around the house, 0 = the splash direction
TURN_PITCH = 18
TURN_RES = (1200, 800)


def setup_render(scale, samples=16):
    sc = bpy.context.scene
    sc.render.engine = "BLENDER_EEVEE"
    sc.render.resolution_percentage = int(round(scale * 100))
    sc.render.film_transparent = True
    sc.render.image_settings.file_format = "PNG"
    sc.render.image_settings.color_mode = "RGBA"
    ee = sc.eevee
    ee.taa_render_samples = samples
    for attr, val in (("use_shadows", True), ("shadow_ray_count", 2), ("shadow_step_count", 8)):
        if hasattr(ee, attr):
            setattr(ee, attr, val)


def render(path):
    bpy.context.scene.render.filepath = path
    bpy.ops.render.render(write_still=True)


def sky_plate(size):
    """A sky made from the original: per row, the median of the pixels that are open sky
    (outside the title), filled down to the horizon, plus the sun as a disc with a glow."""
    im = np.asarray(Image.open(ORIGINAL).convert("RGB")).astype(np.float32) / 255.0
    h, w, _ = im.shape
    yy, xx = np.mgrid[0:h, 0:w]
    mask = ((im[..., 2] - im[..., 0]) > SKY_SAT) & (im[..., 2] > SKY_B_MIN) & ((im[..., 0] < SKY_R_MAX) | (yy > SKY_CLOUDS_TO)) & (yy < SKY_ROWS_TO)
    tx0, ty0, tx1, ty1 = TITLE_BOX
    mask &= ~((xx >= tx0) & (xx <= tx1) & (yy >= ty0) & (yy <= ty1))
    sx, sy, sr, sg = SUN
    mask &= ((xx - sx) ** 2 + (yy - sy) ** 2) > sg ** 2
    # fill everything that is not open sky by diffusion from the open sky around it (at a
    # small size, then scaled up): the original's sky gets lighter and greener toward the
    # middle and the horizon, so one colour per row is not enough
    k = SKY_FILL_DOWN
    sh, sw = h // k, w // k
    small = np.asarray(Image.fromarray((im * 255).astype(np.uint8)).resize((sw, sh), Image.BOX)).astype(np.float32) / 255
    known = np.asarray(Image.fromarray(mask.astype(np.uint8) * 255).resize((sw, sh), Image.BOX)) > 250
    fill = np.where(known[..., None], small, small[known].mean(axis=0))
    for _ in range(SKY_FILL_ITERS):
        p = np.pad(fill, ((1, 1), (1, 1), (0, 0)), mode="edge")
        avg = (p[:-2, 1:-1] + p[2:, 1:-1] + p[1:-1, :-2] + p[1:-1, 2:]) / 4
        fill = np.where(known[..., None], small, avg)
    plate = np.asarray(Image.fromarray((fill * 255).astype(np.uint8)).resize((w, h), Image.BICUBIC)
                       .filter(ImageFilter.GaussianBlur(SKY_BLUR))).astype(np.float32) / 255
    r = np.sqrt((xx - sx) ** 2 + (yy - sy) ** 2)
    glow = np.clip(1 - (r - sr) / (sg - sr), 0, 1) ** 1.6
    plate = plate * (1 - glow[..., None]) + np.array(SUN_GLOW) * glow[..., None]
    disc = np.clip(sr + 1.5 - r, 0, 1)[..., None]
    plate = plate * (1 - disc) + np.array(SUN_DISC) * disc
    out = Image.fromarray((np.clip(plate, 0, 1) * 255).astype(np.uint8))
    return out.resize(size, Image.LANCZOS)


def turnaround(out_dir, houses, cam):
    sc = bpy.context.scene
    sc.render.resolution_x, sc.render.resolution_y = TURN_RES
    sc.render.resolution_percentage = 100
    c = bpy.data.cameras.new("orbit")
    c.lens = 35
    o = bpy.data.objects.new("orbit", c)
    sc.collection.objects.link(o)
    sc.camera = o
    paths = []
    for key in TURN_HOUSES:
        root = houses[key]
        objs = [ch for ch in root.children]
        pts = [ob.matrix_world @ Vector(v) for ob in objs for v in ob.bound_box]
        lo = Vector((min(p.x for p in pts), min(p.y for p in pts), min(p.z for p in pts)))
        hi = Vector((max(p.x for p in pts), max(p.y for p in pts), max(p.z for p in pts)))
        centre = (lo + hi) / 2
        radius = (hi - lo).length * 0.5
        dist = radius / math.tan(math.radians(20)) * 1.05
        # hide everything else but the ground
        keep = set(objs) | {root}
        hidden = []
        for ob in sc.objects:
            if ob.type == "MESH" and ob not in keep and not ob.name.startswith("ground"):
                ob.hide_render = True
                hidden.append(ob)
        for i, yaw in enumerate(TURN_YAWS):
            a = math.radians(yaw) + root.rotation_euler.z   # yaw 0 looks at the house's front
            p = math.radians(TURN_PITCH)
            d = Vector((math.sin(a) * math.cos(p), -math.cos(a) * math.cos(p), math.sin(p)))
            o.location = centre + d * dist
            o.rotation_euler = (centre - o.location).to_track_quat("-Z", "Y").to_euler()
            path = os.path.join(out_dir, f"turnaround-{key}-{i}.png")
            render(path)
            paths.append(path)
        for ob in hidden:
            ob.hide_render = False
    return paths


def write_screen_boxes(out_dir, houses, cam):
    """Where each house lands in the splash frame (px), and what the layout pass changed."""
    import json
    boxes = {}
    for k, root in houses.items():
        xs, ys = [], []
        for ob in root.children:
            if ob.type != "MESH":
                continue
            mw = ob.matrix_world
            for v in ob.data.vertices:
                p = mw @ v.co
                if p.y > 0.5:
                    x, y = SD.project(cam, p.x, p.y, p.z)
                    xs.append(x)
                    ys.append(y)
        boxes[k] = [min(xs), min(ys), max(xs), max(ys)]
    with open(os.path.join(out_dir, "house_screen_boxes.json"), "w") as f:
        json.dump({"boxes": boxes, "notes": B.NOTES}, f, indent=1)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("out_dir")
    ap.add_argument("--scale", type=float, default=0.5)
    ap.add_argument("--focal", type=float, default=None)
    ap.add_argument("--samples", type=int, default=16)
    ap.add_argument("--only", default="splash,turnaround")
    a = ap.parse_args()
    os.makedirs(a.out_dir, exist_ok=True)
    only = set(a.only.split(","))
    cam, houses = B.build("render", a.focal)
    setup_render(a.scale, a.samples)
    write_screen_boxes(a.out_dir, houses, cam)
    if "splash" in only:
        p = os.path.join(a.out_dir, "splash-render.png")
        render(p)
        r = Image.open(p).convert("RGBA")
        sky = sky_plate(r.size).convert("RGBA")
        sky.alpha_composite(r)
        sky.convert("RGB").save(os.path.join(a.out_dir, "splash-render-sky.png"))
    if "turnaround" in only:
        turnaround(a.out_dir, houses, cam)


if __name__ == "__main__":
    main()
