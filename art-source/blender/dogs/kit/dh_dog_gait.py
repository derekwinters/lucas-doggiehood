"""In-place locomotion generator (walk, run) on top of dh_dog_anim.Rig.

A gait = cycle length (frames), ground speed, and per leg: touchdown phase and duty factor.
The toe of a stance paw is planted and travels +Y at the ground speed; a swing paw lifts and
comes forward. The body (spine, head, tail, ears) is posed by a callback, the root height is
solved per frame so every stance leg can reach its paw, then smoothed, and the legs are
placed by the planar IK in dh_dog_anim.
"""
import math

import numpy as np
from mathutils import Vector

import dh_dog_anim as A

TOE_Z = None    # filled from the rig (rest toe heights)


class Gait:
    def __init__(self, rig, name, frames, speed, phase, duty, lift, roll, curl, fold,
                 center=None, body=None, reach=None, extra_z=None, win=1, sigma=1.5, k=0.85, land=0.92):
        self.rig, self.name, self.N, self.v = rig, name, frames, speed
        self.T = frames / A.FPS
        self.stride = speed * self.T
        self.phase, self.duty = phase, duty
        self.lift, self.roll, self.curl, self.fold = lift, roll, curl, fold
        self.center = center or {}
        self.body = body or (lambda ph: None)
        self.reach = reach or {"F": 0.975, "H": 0.95}
        self.extra_z = extra_z or (lambda ph: 0.0)
        self.k = k
        self.land = land
        self.win, self.sigma = win, sigma
        self.zoff = None

    # ------------------------------------------------------------ per leg
    def leg_phase(self, leg, f):
        return (f / self.N - self.phase[leg]) % 1.0

    def target(self, leg, f):
        """(paw joint world, paw pitch, fold deg, stance?, toe world)"""
        r = self.rig.rest[leg]
        fh = leg[0]
        p = self.leg_phase(leg, f)
        duty = self.duty[leg]
        sweep = self.v * duty * self.T
        cy = r["pivot"].y + self.center.get(leg, self.center.get(fh, 0.0))
        roll, roll_frac = self.roll[fh]
        if p < duty:
            u = p / duty
            ty = cy - sweep / 2 + sweep * u
            toe = Vector((r["x"], ty, 0.0))
            pd = roll * A.smooth((u - (1 - roll_frac)) / roll_frac)
            return self.rig.paw_from_pivot(leg, toe, pd), pd, 0.0, True, toe
        u = (p - duty) / (1 - duty)
        toe_lo = Vector((r["x"], cy + sweep / 2, 0.0))
        toe_td = Vector((r["x"], cy - sweep / 2, 0.0))
        pj_lo = self.rig.paw_from_pivot(leg, toe_lo, roll)
        pj_td = self.rig.paw_from_pivot(leg, toe_td, 0.0)
        s = A.smoother(u)
        bump = math.sin(math.pi * min(1.0, u / self.land)) ** 1.2 if u < self.land else 0.0
        early = math.sin(math.pi * min(1.0, u / 0.75)) if u < 0.75 else 0.0
        pj = pj_lo.lerp(pj_td, s)
        pj.z += self.lift[fh] * bump
        pd = A.lerp(roll, 0.0, A.smooth(u / 0.85)) + self.curl[fh] * early
        # a swinging paw never scrapes: keep its underside above the ground (clearance grows
        # with the lift, so the end points still touch exactly)
        low = self.rig.paw_low(leg, pj, pd)
        clear = 0.25 * self.lift[fh] * bump
        if low < clear:
            pj.z += clear - low
        return pj, pd, self.fold[fh] * early, False, None

    # ------------------------------------------------------------ frames
    def pose_body(self, f, z=0.0):
        rig = self.rig
        rig.reset()
        ph = (f / self.N) % 1.0
        self.body(ph)
        rig.root(0, 0, z + self.extra_z(ph))
        rig.update()

    def solve_heights(self):
        """Root z per frame so every stance leg can reach (then smoothed below that)."""
        rig = self.rig
        need = []
        for f in range(self.N):
            self.pose_body(f, 0.0)
            dz = 0.0
            for leg in A.LEGS:
                pj, pd, fold, st, _ = self.target(leg, f)
                if not st:
                    continue
                r = rig.rest[leg]
                J0 = rig.joint(A.LEGS[leg][0])
                R = self.reach[leg[0]] * sum(r["L"][:3])
                dy = J0.y - pj.y
                h = math.sqrt(max(R * R - dy * dy, 0.0))
                dz = min(dz, h - (J0.z - pj.z))
            need.append(dz)
        self.need = np.array(need)
        self.zoff = A.periodic_smooth_min(need, self.win, self.sigma)
        return self.zoff

    def pose(self, f):
        if self.zoff is None:
            self.solve_heights()
        self.pose_body(f, float(self.zoff[f % self.N]))
        for leg in A.LEGS:
            pj, pd, fold, st, _ = self.target(leg, f)
            self.rig.solve_leg(leg, pj, pd, fold=fold, k=self.k[leg[0]] if isinstance(self.k, dict) else self.k, tag="%s f%d" % (self.name, f))

    def plants(self, flat_only=True):
        """{leg: [(f0, f1), ...]} frames where the paw is planted (flat part of the stance)."""
        out = {}
        for leg in A.LEGS:
            fr = []
            for f in range(self.N + 1):
                p = self.leg_phase(leg, f)
                duty = self.duty[leg]
                if p < duty:
                    u = p / duty
                    if not flat_only or u <= 1 - self.roll[leg[0]][1]:
                        fr.append(f)
            # group consecutive frames (wrapping across the loop is split at the loop point)
            iv = []
            for f in fr:
                if iv and f == iv[-1][1] + 1:
                    iv[-1][1] = f
                else:
                    iv.append([f, f])
            out[leg] = [tuple(i) for i in iv]
        return out
