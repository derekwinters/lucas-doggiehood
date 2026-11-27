"""The motion clips (first pass). Each make_<clip>(rig) bakes one Blender action on the breed's
armature and returns its metadata. Gait timing, speeds and size-dependent heights come from the
breed (GAITS, SIT_DOWN, PLAY_BOW); the choreography is shared. All clips are 30 fps, deterministic (pure functions of the
frame number), and in place: the root never travels in X/Y.

  Idle       loop   idle breathing, head look, ear twitch, tail sway
  Walk       loop   four-beat lateral-sequence walk (LH, LF, RH, RF)
  Run        loop   transverse gallop with a gathered suspension
  SitDown    once   standing -> the approved SIT pose (last frame == dh_dog_rig SIT exactly)
  SitIdle    loop   in the SIT pose: breathing, head look, ear twitch, tail sweep on the ground
  TailWag    loop   standing happy wag: the tail whips; body, breathing and face as calm as Idle
  PlayBow    once   bow down (elbows near the ground, rump up, tail wagging, bounce), back up
  Swim       loop   dog-paddle in place: front legs paddle alternately, hind legs kick, head up, tail
                    as a rudder; checked against the breed's water line (SWIM), not the ground
  Shake      once   shake dry: a wave of roll from the head through the shoulders, ribs and hips to
                    the tail, ears flapping; standing, all four paws planted throughout
  LieDown    once   SIT -> lying (sphinx) rest   [optional]
  RestIdle   loop   lying rest loop               [optional]
"""
import math

import numpy as np
from mathutils import Matrix, Quaternion, Vector

import dh_dog as D
import dh_dog_anim as A
import dh_dog_gait as G
import dh_dog_rig as RG

TAU = 2 * math.pi
JAW = D.JAW_REST_OPEN_DEG
B = D.B


def s1(ph, k=1, off=0.0):
    return math.sin(TAU * (k * ph - off))


def c1(ph, k=1, off=0.0):
    return math.cos(TAU * (k * ph - off))


def bump(ph, at, width):
    """Periodic smooth pulse (0..1) centred on `at`, `width` = full width (fraction of the loop)."""
    d = ((ph - at + 0.5) % 1.0) - 0.5
    x = d / (width / 2)
    return 0.5 * (1 + math.cos(math.pi * x)) if abs(x) < 1 else 0.0


def plant_all(rig, tag, k=0.85):
    """Keep all four paws at their rest positions (flat)."""
    for leg, r in rig.rest.items():
        rig.solve_leg(leg, r["paw0"], 0.0, k=k, tag=tag)


def tail(rig, bone, rx=0.0, ry=0.0, rz=0.0):
    """A clip's tail rotation, scaled by the breed's optional TAIL_SWING (a curled tail)."""
    rig.set(bone, *RG.tail_swing(bone, rx, ry, rz))


# =========================================================================== Idle
IDLE_N = 120


def idle_body(rig, ph, look=1.0):
    br = s1(ph, 3)                                   # breathing: 3 breaths per 4 s
    rig.set("spine_2", -0.5 * br)
    rig.set("spine_3", 0.6 * br)
    rig.set("neck", -0.3 * br)
    # head look: left, back to centre, a little right, centre
    yaw = look * (14 * bump(ph, 0.25, 0.36) - 9 * bump(ph, 0.72, 0.30))
    tilt = look * (4 * bump(ph, 0.25, 0.36) + 3 * bump(ph, 0.72, 0.30))
    rig.set("neck", -0.3 * br + 1.5 * s1(ph, 1, 0.1), 0, 0.45 * yaw)
    rig.set("head", -tilt * 0.5, 0.6 * tilt * (1 if yaw >= 0 else -1), 0.55 * yaw)
    rig.set("jaw", JAW + 1.0 * br)
    # ear twitch: left ear flicks twice quickly around 55 %, right ear once at 15 %
    tw_l = bump(ph, 0.53, 0.05) + 0.7 * bump(ph, 0.585, 0.05)
    tw_r = bump(ph, 0.14, 0.06)
    rig.set("ear_L_1", 4 * tw_l, -16 * tw_l, 0)
    rig.set("ear_L_2", 0, -8 * tw_l, 0)
    rig.set("ear_R_1", 4 * tw_r, 16 * tw_r, 0)
    rig.set("ear_R_2", 0, 8 * tw_r, 0)
    # tail: gentle sway, each segment lagging
    for i, b in enumerate(("tail_1", "tail_2", "tail_3", "tail_4")):
        tail(rig, b, 0.8 * br if i == 0 else 0, 0, (6 - i) * s1(ph, 2, 0.06 * i))


def make_idle(rig):
    def pose(f):
        ph = f / IDLE_N
        idle_body(rig, ph)
        rig.update()
        plant_all(rig, "Idle f%d" % f)
    rig.bake("Idle", IDLE_N, pose)
    return dict(name="Idle", frames=IDLE_N, loop=True, ground_speed=0.0,
                plants={leg: [(0, IDLE_N)] for leg in A.LEGS},
                notes="breathing (3 per loop), head looks left then right, left ear double twitch, "
                      "right ear twitch, tail sway (2 per loop); all four paws planted")


# =========================================================================== Walk
WALK = B.GAITS["Walk"]
WALK_N = WALK["frames"]
WALK_SPEED = WALK["speed"]


def walk_body(rig, ph):
    # pelvis yaws/rolls toward the hind leg in stance; the shoulders counter-rotate
    rig.set("hips", 0.0, 2.0 * s1(ph, 1, 0.05), 3.0 * s1(ph, 1, 0.20))
    rig.set("spine_1", 0, 0, -1.5 * s1(ph, 1, 0.20))
    rig.set("spine_2", 0, 0, -1.5 * s1(ph, 1, 0.20))
    rig.set("spine_3", 0.6 * c1(ph, 2, 0.05), -1.5 * s1(ph, 1, 0.45), 0)
    # head: bobs twice per cycle (a nod against the shoulders' dip), slight counter-yaw
    rig.set("neck", 2.0 * c1(ph, 2, 0.15), 0, 1.0 * s1(ph, 1, 0.25))
    rig.set("head", -1.5 * c1(ph, 2, 0.15), 0, 0)
    rig.set("jaw", JAW)
    # ears swing a little, lagging the head
    for s, sg in (("L", -1), ("R", 1)):
        rig.set("ear_%s_1" % s, 3 * c1(ph, 2, 0.30), sg * 3 * c1(ph, 2, 0.30), 0)
        rig.set("ear_%s_2" % s, 3 * c1(ph, 2, 0.38), 0, 0)
    # tail: carried a bit higher, sways once per cycle with a travelling lag
    for i, b in enumerate(("tail_1", "tail_2", "tail_3", "tail_4")):
        tail(rig, b, (10, 3, 0, 0)[i], 0, (9, 8, 7, 6)[i] * s1(ph, 1, 0.30 + 0.07 * i))


def gait(rig, name, g, body):
    """A dh_dog_gait.Gait from a breed GAITS entry."""
    opt = {k: g[k] for k in ("reach", "win", "sigma", "k", "land") if k in g}
    if "suspension" in g:
        lift, at, width = g["suspension"]
        opt["extra_z"] = lambda ph: lift * bump(ph, at, width)
    return G.Gait(rig, name, g["frames"], g["speed"], phase=dict(g["phase"]), duty=dict(g["duty"]),
                  lift=dict(g["lift"]), roll=dict(g["roll"]), curl=dict(g["curl"]), fold=dict(g["fold"]),
                  center=dict(g["center"]), body=body, **opt)


def walk_gait(rig):
    return gait(rig, "Walk", WALK, lambda ph: walk_body(rig, ph))


def make_walk(rig):
    g = walk_gait(rig)
    g.solve_heights()
    rig.bake("Walk", WALK_N, g.pose)
    return dict(name="Walk", frames=WALK_N, loop=True, ground_speed=WALK_SPEED, stride_m=g.stride,
                footfalls={k: v for k, v in g.phase.items()}, duty=g.duty, plants=g.plants(),
                notes=WALK["notes"])


# =========================================================================== Run
RUN = B.GAITS["Run"]
RUN_N = RUN["frames"]
RUN_SPEED = RUN["speed"]


def run_body(rig, ph):
    f = c1(ph, 1, 0.95)                      # spine: +1 gathered (flexed) .. -1 stretched (extended)
    pitch = 5.0 * c1(ph, 1, 0.22)            # nose up while the hind legs drive, down on the fore legs
    rig.set("hips", pitch - 7.0 * f, 0, 1.5 * s1(ph, 1, 0.1))
    for b in ("spine_1", "spine_2", "spine_3"):
        rig.set(b, 4.5 * f, 0, 0)
    front = pitch + 6.5 * f
    rig.set("neck", -0.55 * front - 4.0, 0, 0)
    rig.set("head", -0.45 * front + 2.0 * s1(ph, 1, 0.5), 0, 0)
    rig.set("jaw", JAW + 12 + 4 * s1(ph, 1, 0.3))
    # ears stream back and flap with the stride
    for s, sg in (("L", -1), ("R", 1)):
        rig.set("ear_%s_1" % s, 42 + 14 * s1(ph, 1, 0.55), sg * 10, 0)
        rig.set("ear_%s_2" % s, 18 + 10 * s1(ph, 1, 0.65), 0, 0)
    # tail streams out behind, waving with a travelling lag
    for i, b in enumerate(("tail_1", "tail_2", "tail_3", "tail_4")):
        tail(rig, b, (26, 4, -6, -8)[i] + (5, 6, 7, 8)[i] * s1(ph, 1, 0.75 + 0.08 * i), 0,
                (3, 3, 4, 4)[i] * s1(ph, 1, 0.2 + 0.08 * i))


def run_gait(rig):
    return gait(rig, "Run", RUN, lambda ph: run_body(rig, ph))


def make_run(rig):
    g = run_gait(rig)
    g.solve_heights()
    rig.bake("Run", RUN_N, g.pose)
    return dict(name="Run", frames=RUN_N, loop=True, ground_speed=RUN_SPEED, stride_m=g.stride,
                footfalls=dict(g.phase), duty=g.duty, plants=g.plants(), zoff=[round(float(z), 3) for z in g.zoff],
                notes=RUN["notes"])


# =========================================================================== registry
ORDER = ["Idle", "Walk", "Run", "SitDown", "SitIdle", "TailWag", "PlayBow", "Swim", "Shake", "LieDown", "RestIdle"]


def make(rig, name):
    fn = {"Idle": make_idle, "Walk": make_walk, "Run": make_run}
    for k in ORDER:
        f = globals().get("make_" + k[0].lower() + "".join("_" + c.lower() if c.isupper() else c for c in k[1:]))
        if f:
            fn[k] = f
    return fn[name](rig)


# =========================================================================== Sit
SIT_N = 54
SIT_HIPS = None


def sit_targets(rig):
    """World paw targets of the approved SIT pose: {leg: (paw joint, paw pitch, lower pitch)}."""
    rig.reset()
    rig.apply_spec(rig.sit_spec)
    rig.update()
    out = {leg: rig.leg_state(leg) for leg in A.LEGS}
    rig.reset()
    rig.update()
    return out


def sit_body(rig, s, s_head=None, s_tail=None):
    """Body bones at sit progress s (0 standing .. 1 SIT). The root drop follows the hip tilt
    so the shoulders keep their height (the dog rocks back onto its haunches)."""
    sp = rig.sit_spec
    th = sp["hips"]["rot"][0] * s
    s_head = s if s_head is None else s_head
    s_tail = s if s_tail is None else s_tail
    drop = -B.SIT_DOWN["rock_radius"] * math.sin(math.radians(-th))
    if s >= 1.0:
        drop = sp["root"]["loc"][2]
    rig.root(0, 0, drop - B.SIT_DOWN["dip"] * math.sin(math.pi * s))
    rig.set("hips", th)
    rig.set("spine_3", sp["spine_3"]["rot"][0] * s)
    rig.set("neck", sp["neck"]["rot"][0] * s_head)
    rig.set("head", sp["head"]["rot"][0] * s_head)
    rig.set("jaw", JAW)
    # the tail keeps its world angle (counter to the hip tilt), lifted clear of the ground on
    # the way down (by SIT_DOWN's optional "tail_lift", degrees: a low-carried tail needs more),
    # and lands lying on the ground behind
    t1 = sp["tail_1"]["rot"][0]
    lift = B.SIT_DOWN.get("tail_lift", 18)
    rig.set("tail_1", -th + (t1 + sp["hips"]["rot"][0]) * s_tail + lift * math.sin(math.pi * s))
    for b in ("tail_2", "tail_3"):
        rig.set(b, sp[b]["rot"][0] * s_tail)


def step_path(p0, p1, u, lift):
    p = p0.lerp(p1, A.smoother(u))
    p.z += lift * math.sin(math.pi * min(1.0, max(0.0, u)))
    return p


def make_sit_down(rig):
    tg = sit_targets(rig)
    rest = rig.rest
    N = SIT_N
    t0, t1 = 4, 46                     # body progress runs over these frames, then settles
    hl, fl = B.SIT_DOWN["hind_step_lift"], B.SIT_DOWN["front_step_lift"]
    steps = {"HL": (0.22, 0.50, hl), "HR": (0.34, 0.62, hl),
             "FL": (0.42, 0.68, fl), "FR": (0.58, 0.84, fl)}

    def pose(f):
        if f >= N:
            rig.apply_spec(rig.sit_spec)
            return
        s = A.smoother((f - t0) / (t1 - t0))
        sh = A.smoother((f - t0 - 4) / (t1 - t0 + 2))         # head follows a little behind
        # settle: the head finishes with a tiny nod into place
        lag = B.SIT_DOWN.get("tail_lag", 6)                     # frames the tail trails the hips
        sit_body(rig, s, s_head=min(1.0, sh), s_tail=A.smooth((f - t0 - lag) / (t1 - t0 - lag + 2)))
        if f > t1:
            rig.set("head", rig.sit_spec["head"]["rot"][0] * sh + 2.5 * math.sin(math.pi * (f - t1) / (N - t1)) ** 2)
        rig.update()
        for leg, (a, b, lift) in steps.items():
            u = (s - a) / (b - a)
            r = rest[leg]
            pj1, pd1, ld1 = tg[leg]
            pj = step_path(r["paw0"], pj1, u, lift)
            k = A.smoother(u)
            if leg[0] == "F":
                rig.solve_leg(leg, pj, pd1 * k, lower_d=ld1 * k, tag="SitDown f%d" % f)
            else:
                # the rear foot lowers onto the hock as the haunches go down (follows the body)
                kh = A.smoother((s - 0.15) / 0.85)
                rig.solve_leg(leg, pj, pd1 * k, lower_d=ld1 * kh, tag="SitDown f%d" % f)

    rig.bake("SitDown", N, pose, loop=False)
    return dict(name="SitDown", frames=N, loop=False, ground_speed=0.0,
                touch={N: list(RG.SIT_TOUCH)},
                notes="standing -> SIT: the hips tilt and the root drops together (shoulders keep "
                      "their height), hind paws step forward under the body, front paws step back; "
                      "last frame is dh_dog_rig SIT exactly")


SIT_IDLE_N = 90


def make_sit_idle(rig):
    tg = sit_targets(rig)
    sp = rig.sit_spec
    N = SIT_IDLE_N

    def pose(f):
        ph = f / N
        rig.apply_spec(sp)
        br = s1(ph, 2)                                  # two breaths per loop
        rig.set("spine_2", -0.6 * br)
        rig.set("spine_3", sp["spine_3"]["rot"][0] + 0.8 * br)
        yaw = 12 * bump(ph, 0.30, 0.34) - 8 * bump(ph, 0.75, 0.26)
        rig.set("neck", sp["neck"]["rot"][0] - 0.4 * br, 0, 0.45 * yaw)
        rig.set("head", sp["head"]["rot"][0], 0, 0.55 * yaw)
        rig.set("jaw", JAW + 1.2 * br)
        tw = bump(ph, 0.55, 0.05)
        rig.set("ear_R_1", 4 * tw, 16 * tw, 0)
        rig.set("ear_R_2", 0, 8 * tw, 0)
        tl = bump(ph, 0.08, 0.05) + 0.6 * bump(ph, 0.13, 0.04)
        rig.set("ear_L_1", 3 * tl, -12 * tl, 0)
        rig.update()
        # tail: a lazy sweep along the ground (about the vertical through its root)
        rig.turn_world("tail_1", (0, 0, 1), 10 * s1(ph, 1))
        rig.update()
        rig.turn_world("tail_2", (0, 0, 1), 6 * s1(ph, 1, 0.08))
        rig.update()
        for leg in ("FL", "FR"):
            pj, pd, ld = tg[leg]
            rig.solve_leg(leg, pj, pd, lower_d=ld, tag="SitIdle f%d" % f)

    rig.bake("SitIdle", N, pose)
    return dict(name="SitIdle", frames=N, loop=True, ground_speed=0.0,
                plants={leg: [(0, N)] for leg in A.LEGS},
                touch={0: list(RG.SIT_TOUCH), N // 4: list(RG.SIT_TOUCH), 3 * N // 4: list(RG.SIT_TOUCH)},
                notes="the SIT pose with 2 breaths, a head look left then right, ear twitches and a "
                      "lazy tail sweep along the ground; paws stay planted")


# =========================================================================== Tail wag
WAG_N = 30          # 1 s: three full wags


def make_tail_wag(rig):
    N = WAG_N

    def pose(f):
        ph = f / N
        w = s1(ph, 3)                                   # the wag
        w2 = s1(ph, 3, 0.10)                            # the rear follows a little later
        br = s1(ph, 1)                                  # one calm breath per loop, as in Idle
        # the wag lives in the tail (#895): the torso only follows it a touch, the body does
        # not bounce (the root holds a steady, slightly lowered stance)
        rig.set("hips", 0, 0.2 * w2, -0.6 * w2)
        rig.set("spine_1", 0, 0, 0.4 * w2)
        rig.set("spine_2", -0.5 * br, 0, 0.2 * w2)
        rig.set("spine_3", 0.6 * br, 0, 0.1 * w2)
        rig.root(0, 0, -0.016)
        # calm happy face, as subtle as Idle's: the mouth as open as Idle's and breathing with it,
        # head up and tilted a touch, ears held back; no panting
        rig.set("neck", -4 - 0.3 * br, 0, -0.2 * w2)
        rig.set("head", 3, 4.0, 0)
        rig.set("jaw", JAW + 1.0 * br)
        for sd, sg in (("L", -1), ("R", 1)):
            rig.set("ear_%s_1" % sd, 14 + 0.5 * br, sg * 8, 0)
            rig.set("ear_%s_2" % sd, 6, 0, 0)
        # tail: up and wagging hard, each segment lagging (whip)
        for i, b in enumerate(("tail_1", "tail_2", "tail_3", "tail_4")):
            tail(rig, b, (34, 8, 2, 0)[i], 0, (26, 18, 15, 12)[i] * s1(ph, 3, 0.04 * i))
        rig.update()
        plant_all(rig, "TailWag f%d" % f)

    rig.bake("TailWag", N, pose)
    return dict(name="TailWag", frames=N, loop=True, ground_speed=0.0,
                plants={leg: [(0, N)] for leg in A.LEGS},
                notes="standing happy wag: 3 wags per second, the tail whips side to side while the "
                      "body stands still (the hips and spine follow by about a degree, no bounce), "
                      "calm breathing and mouth as in Idle, head up, ears back; paws planted")


# =========================================================================== Play bow
BOW_N = 84          # 2.8 s: down 0.6 s, hold + bounce 1.4 s, up 0.6 s, settle


def bow_body(rig, b, ph, bounce):
    """b = bow amount 0..1; bounce = small chest bounce (degrees)."""
    rig.set("hips", 27 * b + bounce, 0, 0)
    rig.set("spine_1", 2 * b)
    rig.set("spine_2", 5 * b + 0.5 * bounce)
    rig.set("spine_3", 4 * b)
    rig.root(0, 0, 0.03 * b)
    # head up and forward, looking at the playmate; mouth open
    rig.set("neck", -30 * b - bounce, 0, 0)
    rig.set("head", -12 * b - 0.5 * bounce, 7 * b, 0)
    rig.set("jaw", JAW + 14 * b)
    for sd, sg in (("L", -1), ("R", 1)):
        rig.set("ear_%s_1" % sd, -6 * b + 3 * bounce, sg * 6 * b, 0)
        rig.set("ear_%s_2" % sd, 2 * bounce, 0, 0)


def make_play_bow(rig):
    N = BOW_N
    down0, down1, up0, up1 = 2, 20, 62, 80

    def amount(f):
        return A.smoother((f - down0) / (down1 - down0)) * (1 - A.smoother((f - up0) / (up1 - up0)))

    def pose(f):
        ph = f / N
        b = amount(f)
        hold = A.smooth((f - down1 + 2) / 6) * (1 - A.smooth((f - up0 + 2) / 6))
        bounce = -3.0 * hold * math.sin(TAU * (f - down1) / 14.0) ** 2
        bow_body(rig, b, ph, bounce)
        # tail high and wagging (faster while bowed)
        amp = (0.25 + 0.75 * b) * A.smooth(f / 8.0) * (1 - A.smooth((f - (N - 9)) / 8.0))
        for i, t in enumerate(("tail_1", "tail_2", "tail_3", "tail_4")):
            tail(rig, t, (20, 10, 6, 2)[i] * b, 0, (22, 16, 12, 10)[i] * math.sin(TAU * (f / 10.0 - 0.04 * i)) * amp)
        rig.update()
        # hind paws planted
        for leg in ("HL", "HR"):
            rig.solve_leg(leg, rig.rest[leg]["paw0"], 0.0, k=0.6, tag="PlayBow f%d" % f)
        # front: the forearms lie forward along the ground (elbow just above it), paws flat in
        # front; the paws slap forward on the way down and hop back on the way up
        for leg in ("FL", "FR"):
            r = rig.rest[leg]
            J0 = rig.joint(A.LEGS[leg][0])
            L = r["L"]
            ez = B.PLAY_BOW["elbow_z"]           # elbow joint height when fully down
            dz = max(0.0, J0.z - ez)
            ey = J0.y + math.sqrt(max(L[0] ** 2 - dz ** 2, 0.0))
            wz = B.PLAY_BOW["wrist_z"]
            wy = ey - math.sqrt(max(L[1] ** 2 - (ez - wz) ** 2, 0.0))
            low_d = -70.0                        # pastern turned forward, near horizontal
            a2 = r["ph"][2] + low_d
            pj_bow = Vector((r["x"], wy + L[2] * math.cos(math.radians(a2)), wz + L[2] * math.sin(math.radians(a2))))
            pj_bow.z = max(pj_bow.z, r["paw0"].z)
            pj_bow.y = min(pj_bow.y, r["paw0"].y)
            off = 0.0 if leg == "FL" else 0.04
            u_dn = A.smoother((f - down0 - 2 - off * 50) / (down1 - down0 - 6))
            u_up = A.smoother((f - up0 - off * 50) / (up1 - up0 - 4))
            u = u_dn * (1 - u_up)
            pj = r["paw0"].lerp(pj_bow, u)
            hop = math.sin(math.pi * u_dn) if u_up <= 0 else math.sin(math.pi * u_up)
            pj.z += 0.05 * hop
            rig.solve_leg(leg, pj, 0.0, lower_d=low_d * u if u > 0 else None, k=0.85, tag="PlayBow f%d" % f)

    rig.bake("PlayBow", N, pose, loop=False)
    return dict(name="PlayBow", frames=N, loop=False, ground_speed=0.0,
                plants={"HL": [(0, N)], "HR": [(0, N)]},
                notes="front end drops (forearms along the ground, elbows just above it), rump up, "
                      "tail high and wagging, a few chest bounces, then back up to standing")


# =========================================================================== Swim
SWIM_N = 24                 # 0.8 s: one stroke of each front leg
WATER_BONE = "neck"         # default water line: the rest height of this bone's head (the base of the neck)
PADDLE_OF_LEG = 0.2         # default half stroke: this fraction of the front leg's height (shoulder to paw)
SWIM_PHASE = {"FL": 0.0, "FR": 0.5, "HR": 0.0, "HL": 0.5}     # left and right alternate; diagonals together
# each leg's paddle loop, in units of the half stroke P: the loop's centre forward (-Y) / up of the
# leg's rest paw, its half height, its fore-and-aft scale, and the paw's curl on the recovery (deg)
SWIM_LOOP = {"F": dict(fwd=0.45, up=1.3, depth=0.6, scale=1.0, curl=55.0, curl0=10.0, k=0.85),
             "H": dict(fwd=-0.35, up=0.9, depth=0.45, scale=0.8, curl=25.0, curl0=5.0, k=0.5)}
SWIM_PITCH = -8.0           # hips: the front of the body rides up (degrees, + = front down)
SWIM_BOB = 0.08             # root bob, in units of the half stroke (two per loop: each front stroke)


def swim_params(rig):
    """(water line z, half stroke) from the breed's optional SWIM, else from its rig."""
    sw = getattr(B, "SWIM", None) or {}
    water = sw.get("water_z")
    if water is None:
        water = rig.arm.data.bones[WATER_BONE].head_local.z
    paddle = sw.get("paddle")
    if paddle is None:
        r = rig.rest["FL"]
        paddle = PADDLE_OF_LEG * (float(r["J"][0][1]) - r["paw"].z)
    return water, paddle


def swim_body(rig, ph, P):
    br = s1(ph, 2, 0.1)                                  # each front stroke heaves the chest a little
    rig.root(0, 0, SWIM_BOB * P * br)
    rig.set("hips", SWIM_PITCH + 1.0 * br)
    rig.set("spine_1", 0, 0, 1.0 * s1(ph, 1, 0.20))
    rig.set("spine_2", -0.6 * br, 0, 1.0 * s1(ph, 1, 0.20))
    rig.set("spine_3", -2.0 + 0.6 * br, 0, 0)
    # head held up, the nose level and clear of the water; a small nod with each stroke
    rig.set("neck", -16.0 + 1.5 * s1(ph, 2, 0.25), 0, 1.0 * s1(ph, 1, 0.3))
    rig.set("head", 10.0 - 1.0 * s1(ph, 2, 0.25), 0, 0)
    rig.set("jaw", JAW)
    # ears float back and out a little, bobbing with the strokes
    for sd, sg in (("L", -1), ("R", 1)):
        rig.set("ear_%s_1" % sd, 12 + 3 * s1(ph, 2, 0.35), sg * 10, 0)
        rig.set("ear_%s_2" % sd, 6 + 3 * s1(ph, 2, 0.45), 0, 0)
    # tail: a rudder, streaming out behind near the surface, swaying slowly
    for i, b in enumerate(("tail_1", "tail_2", "tail_3", "tail_4")):
        tail(rig, b, (16, 4, 0, -2)[i], 0, (5, 6, 7, 8)[i] * s1(ph, 1, 0.30 + 0.07 * i))


def swim_leg_target(rig, leg, ph, P):
    """(paw joint target, paw pitch) for a leg's paddle at phase ph: a loop under the body, fixed to
    the leg's posed top joint. The power stroke pushes back (+Y) low, the recovery comes forward
    high with the paw curled."""
    r = rig.rest[leg]
    c = SWIM_LOOP[leg[0]]
    th = TAU * (ph - SWIM_PHASE[leg])
    top = rig.joint(A.LEGS[leg][0])
    rest_top = Vector((r["x"], float(r["J"][0][0]), float(r["J"][0][1])))
    a = c["scale"] * P
    centre = top + (r["paw"] - rest_top) + Vector((0, -c["fwd"] * P, c["up"] * P))
    pj = centre + Vector((0, -a * math.cos(th), -c["depth"] * P * math.sin(th)))
    paw_d = c["curl0"] + c["curl"] * 0.5 * (1 - math.sin(th))
    return pj, paw_d


def make_swim(rig):
    N = SWIM_N
    water, P = swim_params(rig)

    def pose(f):
        ph = f / N
        swim_body(rig, ph, P)
        rig.update()
        for leg in A.LEGS:
            pj, paw_d = swim_leg_target(rig, leg, ph, P)
            rig.solve_leg(leg, pj, paw_d, k=SWIM_LOOP[leg[0]]["k"], tag="Swim f%d" % f)

    rig.bake("Swim", N, pose)
    return dict(name="Swim", frames=N, loop=True, ground_speed=0.0, water_z=water, paddle=P,
                notes="dog-paddle in place: the front legs paddle alternately under the chest, the hind "
                      "legs kick (diagonals together), the head is held up with the chin clear of the "
                      "water line, the tail streams out behind as a rudder; no paw is planted: checked "
                      "against the water line z = %.3f m, not the ground" % water)


# =========================================================================== Shake
SHAKE_N = 30                # 1 s
SHAKE_HZ = 5.0              # the shake's frequency
SHAKE_LAG = 1.2             # frames each link of the wave trails the one before it
# the wave, head to tail: (bone, roll about the body's long axis, side-to-side yaw), degrees
SHAKE_WAVE = (("head", 16.0, 6.0), ("neck", 8.0, 3.0), ("spine_3", 12.0, 0.0), ("spine_2", 10.0, 0.0),
              ("spine_1", 8.0, 0.0), ("hips", 9.0, 0.0))
SHAKE_TAIL = ((8.0, 22.0), (4.0, 18.0), (2.0, 15.0), (0.0, 12.0))   # tail_1..4: (lift, swing), degrees
SHAKE_EAR = dict(lift=10.0, out=25.0, flap=30.0, tip=20.0, lag=1.0)  # ear lift, outward bias, flap, tip flop
SHAKE_JAW = 5.0             # the lips loosen
SHAKE_CROUCH_OF_LEG = 0.09  # the stance drops by this fraction of the front leg's height (bracing)
SHAKE_DELAY = len(SHAKE_WAVE) + 1.5   # links in the wave, the tail's last segment included
SHAKE_DUR = SHAKE_N - 2 - SHAKE_DELAY * SHAKE_LAG


def shake_link(f, i):
    """(envelope 0..1, oscillation -1..1) of wave link i (0 = the head) at frame f: each link
    shakes for SHAKE_DUR frames, starting SHAKE_LAG frames after the one before it."""
    t = f - 1 - i * SHAKE_LAG
    u = t / SHAKE_DUR
    if u <= 0 or u >= 1:
        return 0.0, 0.0
    return math.sin(math.pi * u) ** 2, math.sin(TAU * SHAKE_HZ * t / A.FPS)


def make_shake(rig):
    N = SHAKE_N
    r = rig.rest["FL"]
    crouch = SHAKE_CROUCH_OF_LEG * (float(r["J"][0][1]) - r["paw"].z)

    def pose(f):
        u = f / N
        rig.root(0, 0, -crouch * math.sin(math.pi * u) ** 2 if 0 < u < 1 else 0.0)
        for i, (bone, roll, yaw) in enumerate(SHAKE_WAVE):
            e, o = shake_link(f, i)
            rig.set(bone, 0, roll * e * o, yaw * e * o)
        e, o = shake_link(f, 0)
        rig.set("jaw", JAW + SHAKE_JAW * e)
        # the ears fly out with the head's roll and flop a frame behind it
        el, ol = shake_link(f - SHAKE_EAR["lag"], 0)
        et, ot = shake_link(f - 2 * SHAKE_EAR["lag"], 0)
        for sd, sg in (("L", -1), ("R", 1)):
            rig.set("ear_%s_1" % sd, SHAKE_EAR["lift"] * el, sg * SHAKE_EAR["out"] * el + SHAKE_EAR["flap"] * el * ol, 0)
            rig.set("ear_%s_2" % sd, 0, SHAKE_EAR["tip"] * et * ot, 0)
        # the tail: the end of the wave, swinging side to side
        for k, (lift, swing) in enumerate(SHAKE_TAIL):
            e, o = shake_link(f, len(SHAKE_WAVE) + 0.5 * k)
            tail(rig, "tail_%d" % (k + 1), lift * e, 0, swing * e * o)
        rig.update()
        plant_all(rig, "Shake f%d" % f)

    rig.bake("Shake", N, pose, loop=False)
    return dict(name="Shake", frames=N, loop=False, ground_speed=0.0,
                plants={leg: [(0, N)] for leg in A.LEGS},
                notes="shake dry, about 1 s: a wave of roll at %.0f Hz runs from the head through the "
                      "shoulders, ribs and hips to the tail, the ears flap and the stance braces a "
                      "little; starts and ends standing, all four paws planted" % SHAKE_HZ)
