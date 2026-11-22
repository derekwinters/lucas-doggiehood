"""Build, export and render base house 1 + its three upgrade pieces.

    python3 art-source/blender/render_base_1.py [out_dir] [--only name,name]

Needs `pip install bpy==5.3.0` (headless Blender). Outputs go to out_dir
(default art-source/blender/out/).
"""

import json
import math
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import bpy  # noqa: E402
import dh_house_kit as K  # noqa: E402
import base_house_1 as B1  # noqa: E402
import piece_addition  # noqa: E402
import piece_deck  # noqa: E402
import piece_patio  # noqa: E402

args = [a for a in sys.argv[1:] if not a.startswith("--")]
OUT = args[0] if args else os.path.join(HERE, "out")
ONLY = None
for i, a in enumerate(sys.argv):
    if a == "--only":
        ONLY = set(sys.argv[i + 1].split(","))
os.makedirs(OUT, exist_ok=True)
STATS = {}

# Starting corner lot, in LOT terms (derived in art-source/blender/README.md from
# WorldDimensions / HousePlacement / LotFence). s = distance from the
# perpendicular (side) road's centreline, measured into the lot; d = distance
# from the faced road's centreline, measured into the lot.
LOT_SIZE = 30.0                 # one tile quadrant
FENCE_S_ROAD = 7.75             # side fence on the road edge: StreetCorridorInset 5.75 + 2
FENCE_S_FAR = LOT_SIZE - 2.0    # side fence on the neighbour edge: 2 m inside the lot
FENCE_D_REAR = LOT_SIZE - 2.0   # rear fence: 2 m inside the lot
SIDEWALK_D = 5.75               # sidewalk outer edge (front yard starts here)
FACADE_D = SIDEWALK_D + 2.75    # FrontSetback: facade 8.5 m off the faced road centreline

FACADE_Y = -B1.DIMS["depth"] / 2
ROAD_CL_Y = FACADE_Y - FACADE_D  # faced road centreline, house-local


def level4_placement(bounds, addition_toward_road=True):
    """THE PLACEMENT RULE: the lot positions the LEVEL-4 composite (base +
    addition + deck + patio), not the base. Its bounding box is centred
    across the backyard-fence envelope, and its facade keeps FrontSetback.
    The base pivot is therefore fixed from day one and nothing moves when a
    piece is added. Returns house-local lot geometry + clearances."""
    x0, x1, y0, y1 = bounds
    s_mid = (FENCE_S_ROAD + FENCE_S_FAR) / 2
    x_mid = (x0 + x1) / 2
    if addition_toward_road:      # side road on house-local +X (addition side)
        s_pivot = s_mid + x_mid
        side_road_x = s_pivot          # s = s_pivot - x
        quad_x = (side_road_x - LOT_SIZE, side_road_x)
        fence_x = (side_road_x - FENCE_S_FAR, side_road_x - FENCE_S_ROAD)
    else:                         # side road on house-local -X
        s_pivot = s_mid - x_mid
        side_road_x = -s_pivot         # s = x + s_pivot
        quad_x = (side_road_x, side_road_x + LOT_SIZE)
        fence_x = (side_road_x + FENCE_S_ROAD, side_road_x + FENCE_S_FAR)
    p = {
        "addition_toward_road": addition_toward_road,
        "pivot_s_from_side_road": round(s_pivot, 3),
        "side_road_x": side_road_x,
        "quad": (quad_x[0], quad_x[1], ROAD_CL_Y, ROAD_CL_Y + LOT_SIZE),
        "fence": (fence_x[0], fence_x[1], 0.0, ROAD_CL_Y + FENCE_D_REAR),
        "composite": [round(v, 3) for v in bounds],
    }
    road_edge = fence_x[1] if addition_toward_road else fence_x[0]
    far_edge = fence_x[0] if addition_toward_road else fence_x[1]
    p["clearance"] = {
        "road_side_to_fence": round(abs(road_edge - (x1 if addition_toward_road else x0)), 3),
        "far_side_to_fence": round(abs((x0 if addition_toward_road else x1) - far_edge), 3),
        "front_to_sidewalk": round(y0 - (ROAD_CL_Y + SIDEWALK_D), 3),
        "facade_to_sidewalk": round(FACADE_Y - (ROAD_CL_Y + SIDEWALK_D), 3),
        "rear_to_fence": round((ROAD_CL_Y + FENCE_D_REAR) - y1, 3),
    }
    p["fits"] = all(v >= 0 for v in p["clearance"].values())
    return p


DOG = (1.26, 1.50, 1.58)            # Cube Pets adult dog at in-game scale (w, l, h)


def want(name):
    return ONLY is None or name in ONLY


def place(ob, empty):
    # attach empties are parented to the base at the origin, so their local
    # transform is the base-space attach transform (matrix_world is stale
    # until a depsgraph update)
    ob.location = empty.location.copy()
    ob.rotation_euler = empty.rotation_euler.copy()


def dog_box(loc, yaw_deg=0):
    """Chunky stand-in at the Cube Pets adult dog's real in-game bounds."""
    pal = {"dog": "#D99A52", "dog_dark": "#8A5530", "trim": K.COMMON["trim"]}
    m = K.MeshBuilder("dog_scale_box", pal)
    w, l, h = DOG
    leg = 0.24
    for sx in (-1, 1):
        for sy in (-1, 1):
            cx, cy = sx * (w / 2 - leg / 2 - 0.08), sy * (l / 2 - leg / 2 - 0.1) + 0.12
            m.box((cx - leg / 2, cy - leg / 2, 0), (cx + leg / 2, cy + leg / 2, 0.42), "dog")
    m.box((-w * 0.38, -l / 2 + 0.45, 0.38), (w * 0.38, l / 2, 0.98), "dog")           # body
    m.box((-w / 2, -l / 2, 0.78), (w / 2, -l / 2 + 0.72, h - 0.12), "dog")            # head
    m.box((-w * 0.22, -l / 2 - 0.12, 0.8), (w * 0.22, -l / 2 + 0.05, 1.08), "trim")   # snout
    m.box((-0.08, -l / 2 - 0.16, 1.0), (0.08, -l / 2 - 0.1, 1.08), "dog_dark")        # nose
    for sx in (-1, 1):                                                                 # ears
        m.box((sx * (w / 2 - 0.12) - 0.13, -l / 2 + 0.2, h - 0.12),
              (sx * (w / 2 - 0.12) + 0.13, -l / 2 + 0.48, h), "dog_dark")
    m.beam((0, l / 2 - 0.05, 0.9), (0, l / 2 + 0.25, 1.2), 0.12, 0.12, (0, -1, 1), "dog")  # tail
    ob = m.build()
    ob.location = loc
    ob.rotation_euler = (0, 0, math.radians(yaw_deg))
    return ob


def lot_scene(P):
    SIDE_ROAD_CL_X = P["side_road_x"]
    QUAD, FENCE = P["quad"], P["fence"]
    toward = P["addition_toward_road"]
    pal = dict(K.COMMON)
    g = K.MeshBuilder("lot_ground", pal)
    g.box((-70, -70, -0.06), (70, 80, -0.01), "grass")
    # faced road + sidewalk
    # roads sit a hair above the sidewalks so crossings read as road
    for off in (3.75, -5.75):  # sidewalks both sides of each road
        g.box((-70, ROAD_CL_Y + off, -0.05), (70, ROAD_CL_Y + off + 2, 0.03), "sidewalk")
        g.box((SIDE_ROAD_CL_X + off, -70, -0.05), (SIDE_ROAD_CL_X + off + 2, 80, 0.03), "sidewalk")
    g.box((-70, ROAD_CL_Y - 3, -0.05), (70, ROAD_CL_Y + 3, 0.04), "road")
    g.box((SIDE_ROAD_CL_X - 3, -70, -0.05), (SIDE_ROAD_CL_X + 3, 80, 0.04), "road")
    # front walkway door -> sidewalk
    g.box((-0.6, ROAD_CL_Y + 5.75, -0.05), (0.6, FACADE_Y - 1.6, 0.03), "sidewalk")
    ground = g.build()
    ln = K.MeshBuilder("lot_lines", pal)
    t, z = 0.16, 0.07

    def rect_lines(x0, x1, y0, y1, key, sides="nsew", dash=None):
        segs = []
        if "s" in sides:
            segs.append(((x0, y0), (x1, y0)))
        if "n" in sides:
            segs.append(((x0, y1), (x1, y1)))
        if "w" in sides:
            segs.append(((x0, y0), (x0, y1)))
        if "e" in sides:
            segs.append(((x1, y0), (x1, y1)))
        for (ax, ay), (bx, by) in segs:
            L = math.hypot(bx - ax, by - ay)
            n = int(L / dash) if dash else 1
            for i in range(n):
                if dash and i % 2:
                    continue
                f0, f1 = i / n, (i + 1) / n
                px0, py0 = ax + (bx - ax) * f0, ay + (by - ay) * f0
                px1, py1 = ax + (bx - ax) * f1, ay + (by - ay) * f1
                ln.box((min(px0, px1) - t / 2, min(py0, py1) - t / 2, 0),
                       (max(px0, px1) + t / 2, max(py0, py1) + t / 2, z), key)

    rect_lines(*QUAD, "lot_line", dash=0.8)
    rect_lines(*FENCE, "fence_line", sides="new")
    # fence connectors: front ends of the side runs -> nearest house wall
    # (the addition's end wall on the addition side)
    W = B1.DIMS["width"]
    add_x = W / 2 + piece_addition.DIMS["width"]
    rect_lines(FENCE[0], -W / 2, 0, 0, "fence_line", sides="s")
    rect_lines(add_x, FENCE[1], 0, 0, "fence_line", sides="s")
    lines = ln.build()
    return ground, lines


def studio(path, objs, margin=1.12, yaw=K.GAME_YAW, ground=True):
    if ground:
        K.ground_plane(80, K.COMMON["ground_studio"])
    K.setup_render(path, bg=K.COMMON["ground_studio"])
    K.frame_camera(objs, margin=margin, yaw=yaw)
    K.render()


def stat(name, ob):
    STATS[name] = K.tri_count(ob)


def build_level4(palette=None, house_mode="finished"):
    pal = palette or B1.PALETTE
    house, att = B1.build(house_mode, palette=pal)
    a, a_att = piece_addition.build(pal)
    place(a, att[0])
    dk = piece_deck.build(pal)
    place(dk, att[1])
    pt = piece_patio.build(pal)
    # patio hangs off the addition's ATTACH_PATIO (base space = addition
    # attach + addition-local offset; no rotations in play)
    pt.location = att[0].location + a_att[0].location
    return [house, a, dk, pt]


def composite_bounds(parts):
    bpy.context.view_layer.update()
    xs, ys = [], []
    for o in parts:
        for v in o.data.vertices:
            w = o.matrix_world @ v.co
            xs.append(w.x)
            ys.append(w.y)
    return (min(xs), max(xs), min(ys), max(ys))


# --- exports + stats -----------------------------------------------------
for mode in ("finished", "framing"):
    K.reset_scene()
    ob, att = B1.build(mode)
    stat("base-1-%s" % mode, ob)
    K.export_glb(os.path.join(OUT, "base-1-%s.glb" % mode), [ob])
for nm, mod in (("addition", piece_addition), ("deck", piece_deck), ("patio", piece_patio)):
    K.reset_scene()
    ob = mod.build(B1.PALETTE, name="piece_%s" % nm)
    if isinstance(ob, tuple):
        ob = ob[0]
    stat("piece-%s" % nm, ob)
    K.export_glb(os.path.join(OUT, "piece-%s.glb" % nm), [ob])
K.reset_scene()
_fin, _ = B1.build("finished")
_fra, _ = B1.build("framing")


def _bounds(o):
    xs = [v.co.x for v in o.data.vertices]
    ys = [v.co.y for v in o.data.vertices]
    zs = [v.co.z for v in o.data.vertices]
    return [round(min(xs), 3), round(max(xs), 3), round(min(ys), 3), round(max(ys), 3),
            round(min(zs), 3), round(max(zs), 3)]


STATS["bounds_finished"] = _bounds(_fin)
STATS["bounds_framing"] = _bounds(_fra)
K.reset_scene()
_b = composite_bounds(build_level4())
PLACEMENT = level4_placement(_b, addition_toward_road=True)
STATS["placement_addition_road_side"] = PLACEMENT
STATS["placement_addition_far_side"] = level4_placement(_b, addition_toward_road=False)
print("TRIS", json.dumps(STATS))
with open(os.path.join(HERE, "base_1_stats.json"), "w") as f:
    json.dump(STATS, f, indent=1)

# --- renders -------------------------------------------------------------
for mode in ("finished", "framing"):
    if want(mode):
        # both images use the FINISHED house's bounds so they line up 1:1
        K.reset_scene()
        ref, ref_att = B1.build("finished", name="frame_ref")
        K.ground_plane(80, K.COMMON["ground_studio"])
        K.setup_render(os.path.join(OUT, "base-1-%s.png" % mode), bg=K.COMMON["ground_studio"])
        K.frame_camera([ref], margin=1.12)
        if mode == "framing":
            for o in [ref] + ref_att:
                bpy.data.objects.remove(o)
            B1.build("framing")
        K.render()

if want("level4"):
    K.reset_scene()
    parts = build_level4()
    ground, lines = lot_scene(PLACEMENT)
    dog = dog_box((2.2, FACADE_Y - 3.4, 0), yaw_deg=-25)
    K.setup_render(os.path.join(OUT, "base-1-level4.png"), bg=K.COMMON["ground_studio"])
    K.frame_camera(parts + [lines], margin=1.04)
    K.render()

if want("level4close"):
    # close-up on the real lot, framed to reach the side road on the
    # addition side so the road-side placement is visible
    K.reset_scene()
    parts = build_level4()
    dog = dog_box((2.2, FACADE_Y - 2.4, 0), yaw_deg=-25)
    ground, lines = lot_scene(PLACEMENT)
    hint = K.MeshBuilder("frame_hint", {"g": "#000000"})
    hx = PLACEMENT["side_road_x"]
    hint.box((hx - 1.0, -4.0, 0), (hx, -3.0, 0.1), "g")
    hint = hint.build()
    K.setup_render(os.path.join(OUT, "base-1-level4-close.png"), bg=K.COMMON["ground_studio"])
    K.frame_camera(parts + [dog, hint], margin=1.12)
    bpy.data.objects.remove(hint)
    K.render()

if want("level4back"):
    K.reset_scene()
    parts = build_level4()
    dog = dog_box((6.2, 5.4, 0.08), yaw_deg=150)
    studio(os.path.join(OUT, "base-1-level4-back.png"), parts + [dog], margin=1.06, yaw=140)
# --- colour schemes -------------------------------------------------------
TILE = (600, 450)
LABEL_H = 56
FONT = "/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf"

if want("schemes"):
    tiles = []
    for i, sch in enumerate(K.COLOR_SCHEMES):
        K.reset_scene()
        parts = build_level4(K.scheme_palette(sch))
        tile = os.path.join(HERE, "_scheme_%02d.png" % i)
        K.ground_plane(80, K.COMMON["ground_studio"])
        K.setup_render(tile, res=TILE, bg=K.COMMON["ground_studio"], samples=32)
        bpy.context.scene.world.node_tree.nodes["Background"].inputs["Color"].default_value = \
            K.hex_rgba("#FFFFFF")
        K.frame_camera(parts, margin=1.06, res=TILE)
        K.render()
        tiles.append((sch["name"], tile))
    from PIL import Image, ImageDraw, ImageFont
    cols, rows = 4, 3
    sheet = Image.new("RGB", (cols * TILE[0], rows * (TILE[1] + LABEL_H)), "#FFFFFF")
    draw = ImageDraw.Draw(sheet)
    font = ImageFont.truetype(FONT, 30)
    for i, (nm, tile) in enumerate(tiles):
        cx, cy = (i % cols) * TILE[0], (i // cols) * (TILE[1] + LABEL_H)
        sheet.paste(Image.open(tile).convert("RGB"), (cx, cy + LABEL_H))
        label = "%d. %s" % (i + 1, nm)
        w = draw.textlength(label, font=font)
        draw.text((cx + (TILE[0] - w) / 2, cy + 16), label, fill="#2B2B33", font=font)
        sw = 22  # siding / roof swatches next to the name
        for k, key in enumerate(("siding", "roof")):
            col = K.COLOR_SCHEMES[i][key]
            x = cx + (TILE[0] + w) / 2 + 12 + k * (sw + 6)
            draw.rounded_rectangle((x, cy + 20, x + sw, cy + 20 + sw), 5, fill=col, outline="#2B2B33",
                                   width=2)
        os.remove(tile)
    sheet.save(os.path.join(OUT, "color-schemes.png"))


def build_level(palette, level):
    """Base plus the first (level - 1) pieces of the fixed path."""
    pal = palette
    house, att = B1.build("finished", palette=pal)
    objs = [house] + att
    if level >= 2:
        a, a_att = piece_addition.build(pal)
        place(a, att[0])
        objs += [a] + a_att
    if level >= 3:
        dk = piece_deck.build(pal)
        place(dk, att[1])
        objs.append(dk)
    if level >= 4:
        pt = piece_patio.build(pal)
        pt.location = att[0].location + a_att[0].location
        objs.append(pt)
    return objs


if want("neighborhood"):
    K.reset_scene()
    picks = [("Coastal Blue", 1), ("Red Farmhouse", 2), ("Colonial White", 3), ("Sage Cottage", 4),
             ("Navy Saltbox", 4)]
    spacing = 18.0
    meshes = []
    for i, (nm, lvl) in enumerate(picks):
        dx = (i - (len(picks) - 1) / 2) * spacing
        for o in build_level(K.scheme_palette(nm), lvl):
            if o.parent is None:
                o.location.x += dx
            if o.type == "MESH":
                meshes.append(o)
    pal = dict(K.COMMON)
    g = K.MeshBuilder("street", pal)
    g.box((-120, -120, -0.06), (120, 120, -0.01), "grass")
    g.box((-120, ROAD_CL_Y - 3, -0.05), (120, ROAD_CL_Y + 3, 0.04), "road")
    for off in (3.75, -5.75):
        g.box((-120, ROAD_CL_Y + off, -0.05), (120, ROAD_CL_Y + off + 2, 0.03), "sidewalk")
    for i in range(len(picks)):
        dx = (i - (len(picks) - 1) / 2) * spacing
        g.box((dx - 0.6, ROAD_CL_Y + 5.75, -0.05), (dx + 0.6, FACADE_Y - 1.6, 0.03), "sidewalk")
    road = g.build()
    for j, (x, yaw) in enumerate(((-33, -20), (2.5, 35), (38.5, -150))):
        dog_box((x, FACADE_Y - 2.5, 0.0), yaw_deg=yaw)
    K.setup_render(os.path.join(OUT, "color-neighborhood.png"), res=(1600, 900),
                   bg=K.COMMON["ground_studio"], samples=32)
    hint = K.MeshBuilder("frame_hint", {"g": "#000000"})
    hint.box((-2, ROAD_CL_Y - 3.5, 0), (2, ROAD_CL_Y - 3, 0.1), "g")
    hint = hint.build()
    K.frame_camera(meshes + [hint], margin=1.03, yaw=-22, res=(1600, 900))
    bpy.data.objects.remove(hint)
    K.render()
print("DONE")
