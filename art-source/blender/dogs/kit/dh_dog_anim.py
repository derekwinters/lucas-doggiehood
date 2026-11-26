"""Motion toolkit for the active breed's dog: pose helpers, a planar leg IK, gait foot tracks and
the action baker. The clips themselves are in dh_dog_clips.py.

Conventions (as dh_dog_rig.apply_pose): a rotation (rx, ry, rz) in degrees is about the
WORLD axes at the bone's head, relative to its parent (Q = Rz @ Ry @ Rx, expressed in the
bone's rest frame). Blender axes: front -Y, up +Z, dog's left +X. 1 unit = 1 m. 30 fps.

Every clip is in place: the root never travels in X/Y (it may bob in Z). For locomotion the
ground moves +Y under the dog at the clip's ground speed, so a planted paw moves +Y at that
speed and nothing else.
"""
import math
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
if HERE not in sys.path:
    sys.path.insert(0, HERE)
import dh_breed  # noqa: E402

dh_breed.get()
ROOT = dh_breed.DIR                     # the breed's folder
MOTION = os.path.join(ROOT, "motion")   # its motion/exports/ and motion/review/

import bpy  # noqa: E402
import numpy as np  # noqa: E402
from mathutils import Matrix, Quaternion, Vector  # noqa: E402

import dh_dog as D  # noqa: E402
import dh_dog_rig as RG  # noqa: E402

FPS = 30
LEGS = {
    "FL": ["front_shoulder_L", "front_elbow_L", "front_wrist_L", "front_paw_L"],
    "FR": ["front_shoulder_R", "front_elbow_R", "front_wrist_R", "front_paw_R"],
    "HL": ["back_hip_L", "back_stifle_L", "back_hock_L", "back_foot_L"],
    "HR": ["back_hip_R", "back_stifle_R", "back_hock_R", "back_foot_R"],
}
LEG_BONES = {b for v in LEGS.values() for b in v}


def _ang(y, z):
    return math.degrees(math.atan2(z, y))


def _qw(rx, ry=0.0, rz=0.0):
    return (Matrix.Rotation(math.radians(rz), 3, "Z") @ Matrix.Rotation(math.radians(ry), 3, "Y")
            @ Matrix.Rotation(math.radians(rx), 3, "X"))


def _wrap(a):
    return (a + 180.0) % 360.0 - 180.0


def _dir(a):
    r = math.radians(a)
    return np.array([math.cos(r), math.sin(r)])


def cross2(a, b):
    return float(a[0] * b[1] - a[1] * b[0])


def smooth(x):
    x = min(1.0, max(0.0, x))
    return x * x * (3 - 2 * x)


def smoother(x):
    x = min(1.0, max(0.0, x))
    return x * x * x * (x * (6 * x - 15) + 10)


def lerp(a, b, t):
    return a + (b - a) * t


class Rig:
    """The built dog plus pose helpers."""

    def __init__(self, coat=None, lod=False):
        bpy.ops.wm.read_factory_settings(use_empty=True)
        sc = bpy.context.scene
        sc.render.fps = FPS
        sc.render.fps_base = 1.0
        self.parts = D.build(coat, lod=lod)
        self.arm = D.build.armature
        self.W = D.build.weights
        self.arm.animation_data_create()
        for pb in self.arm.pose.bones:
            pb.rotation_mode = "QUATERNION"
        # rest joints (armature space = world: the armature sits at the origin)
        self.rest = {}
        for leg, names in LEGS.items():
            bones = [self.arm.data.bones[n] for n in names]
            J = [np.array([b.head_local.y, b.head_local.z]) for b in bones] + \
                [np.array([bones[-1].tail_local.y, bones[-1].tail_local.z])]
            L = [float(np.linalg.norm(J[i + 1] - J[i])) for i in range(4)]
            ph = [_ang(*(J[i + 1] - J[i])) for i in range(4)]
            # elbow / stifle side of the J0 -> J2 line
            c = cross2(J[1] - J[0], J[2] - J[0])
            x = bones[0].head_local.x
            self.rest[leg] = dict(J=J, L=L, ph=ph, bend=np.sign(c), x=x,
                                  paw=Vector((x, J[3][0], J[3][1])), toe=Vector((x, J[4][0], J[4][1])))
        self.clamped = []
        self.sit_spec = RG.poses()["sit"]
        # where each paw meets the ground at rest (skinned mesh): its lowest point (a paw may
        # hover a few mm; planted targets bring every paw down to z = 0)
        # and the front edge of its contact patch, the pivot the paw rolls over at toe-off
        self.update()
        import dh_dog_check as K
        body = K.frame_points(self)["body"]
        dom = np.array(RG.BONE_NAMES)[self.W.argmax(1)]
        for leg, names in LEGS.items():
            v = body[dom == names[3]]
            gz = float(v[:, 2].min())
            c = v[v[:, 2] < gz + 0.004]
            r = self.rest[leg]
            r["gz"] = gz
            r["paw0"] = r["paw"] - Vector((0, 0, gz))                    # planted paw joint
            r["pivot"] = Vector((r["x"], float(c[:, 1].min()), 0.0))     # planted roll pivot
            r["pawv"] = v[:, 1:3] - np.array([0.0, gz])                        # paw underside, planted

    # ----------------------------------------------------------------- pose
    def reset(self):
        for pb in self.arm.pose.bones:
            pb.rotation_quaternion = (1, 0, 0, 0)
            pb.location = (0, 0, 0)

    def q_world(self, bone, rx=0.0, ry=0.0, rz=0.0):
        """Quaternion for a world-axes rotation (degrees) in the bone's rest frame."""
        pb = self.arm.pose.bones[bone]
        R = pb.bone.matrix_local.to_3x3()
        Q = (Matrix.Rotation(math.radians(rz), 3, "Z") @ Matrix.Rotation(math.radians(ry), 3, "Y")
             @ Matrix.Rotation(math.radians(rx), 3, "X"))
        return (R.inverted() @ Q @ R).to_quaternion()

    def set(self, bone, rx=0.0, ry=0.0, rz=0.0):
        rx, ry, rz = RG.ear_swing(bone, rx, ry, rz)       # erect ears (EARS["swing"]); others unchanged
        self.arm.pose.bones[bone].rotation_quaternion = self.q_world(bone, rx, ry, rz)

    def set_q(self, bone, q):
        self.arm.pose.bones[bone].rotation_quaternion = q

    def root(self, x=0.0, y=0.0, z=0.0):
        pb = self.arm.pose.bones["root"]
        pb.location = pb.bone.matrix_local.to_3x3().inverted() @ Vector((x, y, z))

    def apply_spec(self, spec, skip=()):
        """A dh_dog_rig pose spec {bone: {rot, loc}}."""
        for b, d in spec.items():
            if b.startswith("_") or b in skip:
                continue
            if "rot" in d:
                self.set(b, *d["rot"])
            if "loc" in d:
                self.root(*d["loc"])

    def update(self):
        bpy.context.view_layer.update()

    def joint(self, bone, tail=False):
        pb = self.arm.pose.bones[bone]
        return self.arm.matrix_world @ (pb.tail if tail else pb.head)

    def delta(self, bone):
        """Posed world rotation delta of a bone (rest -> posed), 3x3. Needs update()."""
        pb = self.arm.pose.bones[bone]
        return (pb.matrix.to_3x3() @ pb.bone.matrix_local.to_3x3().inverted()).normalized()

    def turn_world(self, bone, axis, deg):
        """Rotate a bone (and its children) about a WORLD axis through its head, on top of its
        current pose. Needs the parents posed and update() called."""
        pb = self.arm.pose.bones[bone]
        Dp = self.delta(pb.parent.name) if pb.parent else Matrix.Identity(3)
        R = pb.bone.matrix_local.to_3x3()
        Q = R @ pb.rotation_quaternion.to_matrix() @ R.inverted()
        Rw = Matrix.Rotation(math.radians(deg), 3, Vector(axis))
        Q2 = Dp.inverted() @ Rw @ Dp @ Q
        pb.rotation_quaternion = (R.inverted() @ Q2 @ R).to_quaternion()

    def leg_state(self, leg):
        """Current world paw-joint position, paw pitch and lower-segment pitch deltas (degrees)."""
        names = LEGS[leg]
        pj = self.joint(names[3])
        out = [pj]
        for n in (names[3], names[2]):
            v = self.delta(n) @ Vector((0, 1, 0))
            out.append(_ang(v.y, v.z))
        return pj, out[1], out[2]

    # ------------------------------------------------------------------ IK
    def solve_leg(self, leg, paw, paw_d=0.0, lower_d=None, fold=0.0, k=0.85, tag=""):
        """Place one leg so the paw joint (front_paw / back_foot head) is at world point `paw`
        with the paw bone pitched by paw_d degrees from flat (world, + = heel up / curl).
        The lower segment (pastern / rear foot) follows the hip/shoulder-to-paw line by
        factor k (+ fold degrees), or takes world pitch lower_d if given. The upper two
        bones are a two-bone IK that keeps the rest bend side. Needs the parent posed
        (call update() after posing the body)."""
        r = self.rest[leg]
        names = LEGS[leg]
        pb0 = self.arm.pose.bones[names[0]]
        par = pb0.parent
        Mp = par.matrix @ par.bone.matrix_local.inverted()      # rest -> posed
        v = Mp.to_3x3() @ Vector((0, 1, 0))
        ppitch = _ang(v.y, v.z)
        Pl = Mp.inverted() @ Vector(paw)
        J, L, ph = r["J"], r["L"], r["ph"]
        # abduction: swing the leg plane about the Y axis through its top joint so the paw
        # reaches the target's x (the spine's yaw / roll moves the plane sideways)
        dx, dz = Pl.x - r["x"], Pl.z - J[0][1]
        h = math.hypot(dx, dz)
        beta = -math.degrees(math.atan2(dx, -dz))
        P = np.array([Pl.y, J[0][1] - h])
        a3 = ph[3] + paw_d - ppitch
        line_d = _wrap(_ang(*(P - J[0])) - _ang(*(J[3] - J[0])))
        reach = 0.995 * (L[0] + L[1])
        kk = k
        while True:
            a2 = (ph[2] + lower_d - ppitch) if lower_d is not None else (ph[2] + kk * line_d + fold)
            J2 = P - L[2] * _dir(a2)
            d = float(np.linalg.norm(J2 - J[0]))
            if d <= reach or lower_d is not None or kk >= 1.6:
                break
            kk += 0.05
        if d > reach:
            self.clamped.append((tag, leg, round(d - reach, 4)))
            J2 = J[0] + (J2 - J[0]) * (reach / d)
            d = reach
        dmin = abs(L[0] - L[1]) + 1e-4
        if d < dmin:
            J2 = J[0] + (J2 - J[0]) * (dmin / d)
            d = dmin
        ca = (L[0] ** 2 + d ** 2 - L[1] ** 2) / (2 * L[0] * d)
        a = math.degrees(math.acos(max(-1.0, min(1.0, ca))))
        base = _ang(*(J2 - J[0]))
        best = None
        for s in (1, -1):
            J1 = J[0] + L[0] * _dir(base + s * a)
            if np.sign(cross2(J1 - J[0], J2 - J[0])) == r["bend"]:
                best = J1
        J1 = best if best is not None else J[0] + L[0] * _dir(base + a)
        phis = [_ang(*(J1 - J[0])), _ang(*(J2 - J1)), a2, a3]
        acc = 0.0
        D3 = Mp.to_3x3().normalized()
        for i, n in enumerate(names[:3]):
            dd = _wrap((phis[i] - ph[i]) - acc)
            self.set(n, dd, beta if i == 0 else 0.0)
            acc += dd
            D3 = D3 @ _qw(dd, beta if i == 0 else 0.0)
        # the paw keeps its world heading (no yaw / roll from the spine): pitch paw_d only
        Q3 = D3.inverted() @ Matrix.Rotation(math.radians(paw_d), 3, "X")
        R = self.arm.pose.bones[names[3]].bone.matrix_local.to_3x3()
        self.set_q(names[3], (R.inverted() @ Q3 @ R).to_quaternion())
        return kk

    def paw_from_pivot(self, leg, pivot, paw_d):
        """Paw joint position for the paw's ground pivot (front of its contact patch) held at
        `pivot` while the paw pitches by paw_d (world degrees, + = heel up)."""
        r = self.rest[leg]
        dv = r["paw0"] - r["pivot"]
        R = Matrix.Rotation(math.radians(paw_d), 3, "X")
        out = Vector(pivot) + R @ dv
        # the paw is rounded: lift it so its rotated underside does not dip below the pivot's
        # height (it rolls forward onto the toes instead of sinking)
        a = math.radians(paw_d)
        rel = r["pawv"] - np.array([r["pivot"].y, r["pivot"].z])
        z = rel[:, 0] * math.sin(a) + rel[:, 1] * math.cos(a)
        out.z += max(0.0, -float(z.min()))
        return out

    def paw_low(self, leg, pj, paw_d):
        """Lowest point (world z) of the paw's underside (rigid with the paw bone) for the paw
        joint at pj pitched by paw_d."""
        r = self.rest[leg]
        a = math.radians(paw_d)
        rel = r["pawv"] - np.array([r["paw0"].y, r["paw0"].z])
        z = rel[:, 0] * math.sin(a) + rel[:, 1] * math.cos(a)
        return pj.z + float(z.min())

    # ------------------------------------------------------------ baking
    def bake(self, name, frames, pose_fn, loop=True):
        """Bake pose_fn(frame) for frame 0..frames into a new action (every bone, every frame,
        linear). For a loop, frame `frames` is posed from frame 0 so the ends match exactly."""
        act = bpy.data.actions.new(name)
        act.use_fake_user = True
        self.arm.animation_data.action = act
        prev = {}
        for f in range(frames + 1):
            self.reset()
            pose_fn(0 if (loop and f == frames) else f)
            for pb in self.arm.pose.bones:
                q = pb.rotation_quaternion.copy()
                if pb.name in prev and prev[pb.name].dot(q) < 0:
                    q.negate()
                pb.rotation_quaternion = q
                prev[pb.name] = q
                pb.keyframe_insert("rotation_quaternion", frame=f, group=pb.name)
                if pb.name == "root":
                    pb.keyframe_insert("location", frame=f, group=pb.name)
        for fc in _fcurves(act):
            for kp in fc.keyframe_points:
                kp.interpolation = "LINEAR"
        act.frame_range = (0, frames)
        try:
            act.use_frame_range = True
        except AttributeError:
            pass
        self.arm.animation_data.action = None
        self.reset()
        self.update()
        return act

    def play(self, act, frame):
        self.arm.animation_data.action = act
        bpy.context.scene.frame_set(frame)


def _fcurves(act):
    try:
        return list(act.fcurves)
    except AttributeError:
        pass
    out = []
    for layer in act.layers:
        for strip in layer.strips:
            for cb in strip.channelbags:
                out.extend(cb.fcurves)
    return out


# ---------------------------------------------------------------- gait
def foot_track(phase, duty, sweep, center_y, lift, x, height, roll=0.0, roll_frac=0.18,
               curl=0.0, lift_peak=0.4):
    """One paw over a gait cycle. phase 0 = touchdown. Stance: the toe is planted and moves
    +Y (with the ground) by `sweep` over the stance; the last roll_frac of the stance pivots the
    paw about the toe up to `roll` degrees. Swing: the paw lifts by `lift` and comes forward.
    Returns (toe_world or None, paw_joint_world or None, paw_pitch, stance?, swing_u)."""
    p = phase % 1.0
    if p < duty:
        u = p / duty
        y = center_y - sweep / 2 + sweep * u
        rr = roll * smooth((u - (1 - roll_frac)) / roll_frac) if roll else 0.0
        return y, rr, True, u
    u = (p - duty) / (1 - duty)
    # forward travel: eased; the paw rolls off at the start of the swing
    s = smoother(u)
    y = center_y + sweep / 2 - sweep * s
    return y, None, False, u


def periodic_smooth_min(vals, win, sigma):
    """Erode (running min, window win) then blur (periodic Gaussian): a smooth curve <= vals."""
    n = len(vals)
    v = np.asarray(vals, float)
    er = np.array([min(v[(i + k) % n] for k in range(-win, win + 1)) for i in range(n)])
    ks = np.arange(-3 * int(sigma + 1), 3 * int(sigma + 1) + 1)
    w = np.exp(-0.5 * (ks / max(sigma, 1e-6)) ** 2)
    w /= w.sum()
    return np.array([sum(w[j] * er[(i + ks[j]) % n] for j in range(len(ks))) for i in range(n)])
