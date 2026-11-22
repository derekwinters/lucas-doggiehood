"""sampler-street-props.png: every street prop, labelled with its triangle
count and size, with the dog stand-in for scale. Four orthographic panels
(lights + pole, signs, street furniture, road decals) composed onto one sheet.

  python3 render_props_sampler.py panel <lights|signs|furniture|decals> <out.png>
  python3 render_props_sampler.py compose <out.png> <lights.png> <signs.png> <furniture.png> <decals.png>
"""
import json
import math
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
# The committed round-3 tree modules (../trees) and house kit (art-source/blender).
sys.path.insert(0, os.path.normpath(os.path.join(HERE, "..", "trees")))
sys.path.insert(0, os.path.normpath(os.path.join(HERE, "..", "..")))
from PIL import Image, ImageDraw, ImageFont  # noqa: E402

F1 = "/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf"
F2 = "/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf"
PANELS = {
    # name: (yaw, pitch, res, items [(key, x, y, yaw)], dog position or None)
    "lights": (-24, 14, (2000, 1800), [("lamp_post_lantern", -1.2, 0, 0), ("lamp_post_cobra", 2.6, 0, -90),
                                       ("utility_pole", 6.6, 0, 0)], (-5.0, 0.2)),
    "signs": (-14, 8, (3300, 1300), [(k, -9.6 + 2.6 * i, 0, 0) for i, k in enumerate(
        ["stop_sign", "yield_sign", "speed_limit_25", "slow_dogs_at_play", "pedestrian_crossing_ahead",
         "crosswalk_sign", "dead_end", "no_parking", "bus_stop_sign"])], (13.8, 0.4)),
    "furniture": (-24, 20, (3300, 1300), None, None),
    "decals": (-24, 42, (1700, 1300), [("manhole_cover", -2.4, 1.0, 0), ("storm_drain", 2.0, 3.0, 0),
                                       ("crosswalk", 0.2, -2.0, 90), ("stop_line", 4.7, -3.4, 0)], None),
}
FURN = [("bench_park", 2.6), ("bench_bus", 2.9), ("fire_hydrant", 1.2), ("mailbox", 1.2), ("trash_bin", 1.4),
        ("recycling_bin", 1.4), ("dog_waste_station", 1.7), ("bike_rack", 2.5), ("newspaper_box", 1.3)]


def panel(name, out):
    import bpy
    from mathutils import Vector
    from bpy_extras.object_utils import world_to_camera_view
    import dh_house_kit as K
    import dh_scene as S
    import dh_props as P
    yaw, pitch, res, items, dog = PANELS[name]
    if name == "furniture":
        items, x = [], -10.0
        for k, w in FURN:
            items.append((k, x + w / 2, 0, 0))
            x += w
        dog = (x + 1.0, 0.3)
    K.reset_scene()
    ms, labels = [], []
    for key, x, y, yw in items:
        fn, lab = P.PIECES[key]
        ob = fn()
        ob.location = (x, y, 0.0 if name != "decals" else 0.0)
        ob.rotation_euler = (0, 0, math.radians(yw))
        ms.append(ob)
        bpy.context.view_layer.update()
        pts = [ob.matrix_world @ Vector(c) for c in ob.bound_box]
        sz = [max(p[i] for p in pts) - min(p[i] for p in pts) for i in range(3)]
        ymin = min(p.y for p in pts)
        dims = ("%.2f x %.2f m" % (sz[0], sz[1])) if name == "decals" else ("%.2f m tall" % sz[2])
        labels.append((lab, "%d tris  |  %s" % (K.tri_count(ob), dims), Vector((x, ymin - 0.35, 0))))
    if dog:
        d = S.dog_box((dog[0], dog[1], 0), yaw_deg=-60)
        ms.append(d)
        labels.append(("Dog stand-in", "1.58 m tall", Vector((dog[0], dog[1] - 1.0, 0))))
    pal = {"ground": "#A9ADA6", "road": "#4B4E52", "walk": "#B8B4AA"}
    g = K.MeshBuilder("ground", pal)
    g.box((-60, -60, -0.12), (60, 60, -0.06), "ground")
    if name == "decals":
        g.box((-4.2, -4.6, -0.06), (6.0, 3.0, 0.0), "road")
        g.box((-4.2, 3.0, -0.06), (6.0, 5.0, 0.12), "walk")      # curb + sidewalk behind the drain
    g.build()
    K.setup_render(out, res=res, bg="#EEF1EC", samples=48)
    sc = bpy.context.scene
    hint = []
    if name == "decals":
        hint = [K.MeshBuilder("hint", {"a": "#000000"})]
        hint[0].box((-4.2, -5.6, 0), (6.0, 4.4, 0.01), "a")
        hint = [hint[0].build()]
    cam = K.frame_camera(ms + hint, margin=1.3 if name == "lights" else 1.12, yaw=yaw, pitch=pitch, res=res)
    for h in hint:
        bpy.data.objects.remove(h)
    # leave room under the props for the labels
    cam.location -= cam.matrix_world.to_3x3() @ Vector((0, 0.06 * cam.data.ortho_scale * res[1] / res[0], 0))
    K.render()
    img = Image.open(out).convert("RGB")
    dr = ImageDraw.Draw(img)
    fs = {"lights": 1.3}.get(name, 1.0)
    f1, f2 = ImageFont.truetype(F1, int(30 * fs)), ImageFont.truetype(F2, int(25 * fs))
    for i, (a, b, p) in enumerate(labels):
        q = world_to_camera_view(sc, cam, p)
        stag = 84 * fs if (i % 2 and name in ("signs", "furniture", "lights")) else 0
        x, y = q.x * res[0], (1 - q.y) * res[1] + 6 + stag
        tw = max(dr.textlength(a, font=f1), dr.textlength(b, font=f2))
        dr.rounded_rectangle((x - tw / 2 - 9, y - 4, x + tw / 2 + 9, y + 66 * fs), 7, fill="#FFFFFF", outline="#9A9A9A")
        dr.text((x - dr.textlength(a, font=f1) / 2, y), a, fill="#222222", font=f1)
        dr.text((x - dr.textlength(b, font=f2) / 2, y + 34 * fs), b, fill="#555555", font=f2)
    img.save(out)


def compose(out, paths):
    titles = ["Lights and utility pole", "Signs (no street names)", "Street furniture", "Road decals"]
    ims = [Image.open(p).convert("RGB") for p in paths]
    L, Sg, Fu, D = ims
    head, pad = 64, 16
    s_top = Sg.width / (Sg.width + L.width) if False else 1.0
    # layout: row 1 = lights | signs ; row 2 = furniture | decals, scaled to equal row heights
    def row(a, b, h):
        a2 = a.resize((int(a.width * h / a.height), h), Image.LANCZOS)
        b2 = b.resize((int(b.width * h / b.height), h), Image.LANCZOS)
        return a2, b2
    W = 3600
    # pick row heights so each row's total width = W
    h1 = int((W - pad * 3) / (L.width / L.height + Sg.width / Sg.height))
    h2 = int((W - pad * 3) / (Fu.width / Fu.height + D.width / D.height))
    r1 = row(L, Sg, h1)
    r2 = row(Fu, D, h2)
    sheet = Image.new("RGB", (W, 110 + head * 2 + h1 + h2 + pad * 3), "#FFFFFF")
    d = ImageDraw.Draw(sheet)
    d.text((pad, 22), "Street props  -  round 3 quality (textured, normal-mapped)", fill="#222222",
           font=ImageFont.truetype(F1, 50))
    y = 110
    k = 0
    for (a, b), h in ((r1, h1), (r2, h2)):
        x = pad
        for im in (a, b):
            d.text((x + 6, y + 12), titles[k], fill="#333333", font=ImageFont.truetype(F1, 38))
            sheet.paste(im, (x, y + head))
            x += im.width + pad
            k += 1
        y += head + h + pad
    sheet.save(out)


if __name__ == "__main__":
    if sys.argv[1] == "panel":
        panel(sys.argv[2], sys.argv[3])
    else:
        compose(sys.argv[2], sys.argv[3:7])
