"""One continuous dog surface from the lofted shape library (round 3).

Each lofted part (torso + neck, head, tail, leg segments) is turned into a
signed distance field on a regular grid, the fields are merged with a smooth
minimum (so every junction gets a rounded fillet instead of a straight
intersection line), the merged field is meshed with surface nets, and the
result is retopologised with QuadriFlow to the triangle budget. All steps are
deterministic (fixed grid, fixed QuadriFlow seed, and QuadriFlow's fresh heap memory pinned to a
fixed fill; see quadriflow()).

Invariant - parts that meet on the dog's surface are joined by the field's
smooth minimum, never by intersecting separate meshes.
"""
import contextlib
import ctypes
import ctypes.util
import math

import bmesh
import bpy
import numpy as np
from mathutils import Vector
from mathutils.bvhtree import BVHTree

FAR = 1.0


def part_sdf(bm, origin, h, shape, margin):
    """Signed distance (m) of a closed bmesh on the grid, evaluated inside the
    part's bounding box + margin; FAR elsewhere."""
    bm.normal_update()
    bvh = BVHTree.FromBMesh(bm)
    co = np.array([v.co[:] for v in bm.verts])
    lo = np.floor((co.min(0) - margin - origin) / h).astype(int).clip(0, np.array(shape) - 1)
    hi = np.ceil((co.max(0) + margin - origin) / h).astype(int).clip(0, np.array(shape) - 1)
    out = np.full(shape, FAR, dtype=np.float32)
    xs = origin[0] + h * np.arange(lo[0], hi[0] + 1)
    ys = origin[1] + h * np.arange(lo[1], hi[1] + 1)
    zs = origin[2] + h * np.arange(lo[2], hi[2] + 1)
    sub = np.empty((len(xs), len(ys), len(zs)), dtype=np.float32)
    fn = bvh.find_nearest
    for i, x in enumerate(xs):
        for j, y in enumerate(ys):
            for k, z in enumerate(zs):
                p = Vector((x, y, z))
                loc, nrm, _, d = fn(p)
                sub[i, j, k] = d if (p - loc).dot(nrm) >= 0 else -d
    out[lo[0]:hi[0] + 1, lo[1]:hi[1] + 1, lo[2]:hi[2] + 1] = sub
    return out


def smin(a, b, k):
    """Polynomial smooth minimum: a fillet of about k metres where the shapes meet."""
    hh = np.clip(0.5 + 0.5 * (b - a) / k, 0.0, 1.0)
    return (b * (1 - hh) + a * hh - k * hh * (1 - hh)).astype(np.float32)


def surface_nets(f, origin, h):
    """Quad mesh of the zero level set of f (naive surface nets). Returns (verts, quads)."""
    inside = f < 0
    nx, ny, nz = f.shape
    # cell corner offsets and the 12 cell edges
    corners = [(i, j, k) for k in (0, 1) for j in (0, 1) for i in (0, 1)]
    edges = [(0, 1), (2, 3), (4, 5), (6, 7), (0, 2), (1, 3), (4, 6), (5, 7), (0, 4), (1, 5), (2, 6), (3, 7)]
    cs = (nx - 1, ny - 1, nz - 1)
    cval = [f[i:i + cs[0], j:j + cs[1], k:k + cs[2]] for i, j, k in corners]
    acc = np.zeros(cs + (3,), dtype=np.float64)
    cnt = np.zeros(cs, dtype=np.int32)
    for a, b in edges:
        fa, fb = cval[a], cval[b]
        m = (fa < 0) != (fb < 0)
        t = np.where(m, fa / np.where(m, fa - fb, 1.0), 0.0)
        pa, pb = np.array(corners[a], float), np.array(corners[b], float)
        for ax in range(3):
            acc[..., ax] += np.where(m, pa[ax] + t * (pb[ax] - pa[ax]), 0.0)
        cnt += m
    has = cnt > 0
    idx = -np.ones(cs, dtype=np.int64)
    idx[has] = np.arange(has.sum())
    cell = np.argwhere(has)
    verts = origin + h * (cell + acc[has] / cnt[has][:, None])
    quads, dirs = [], []
    # every grid edge whose ends straddle the surface gets the quad of the 4 cells around it
    for ax in range(3):
        a1, a2 = [(1, 2), (0, 2), (0, 1)][ax]
        sl0 = [slice(None)] * 3
        sl1 = [slice(None)] * 3
        sl0[ax] = slice(0, f.shape[ax] - 1)
        sl1[ax] = slice(1, f.shape[ax])
        ch = inside[tuple(sl0)] != inside[tuple(sl1)]
        pts = np.argwhere(ch)
        out_dir = np.where(inside[tuple(sl0)][ch], 1.0, -1.0)      # outward = toward the outside end
        ok = (pts[:, a1] >= 1) & (pts[:, a2] >= 1) & (pts[:, a1] < cs[a1]) & (pts[:, a2] < cs[a2])
        pts, out_dir = pts[ok], out_dir[ok]
        cyc = []
        for d1, d2 in ((0, 0), (1, 0), (1, 1), (0, 1)):
            q = pts.copy()
            q[:, a1] -= d1
            q[:, a2] -= d2
            cyc.append(idx[q[:, 0], q[:, 1], q[:, 2]])
        cyc = np.stack(cyc, 1)
        p = verts[cyc]
        nrm = np.cross(p[:, 2] - p[:, 0], p[:, 3] - p[:, 1])
        flip = nrm[:, ax] * out_dir < 0
        cyc[flip] = cyc[flip][:, ::-1]
        quads.append(cyc)
    quads = np.concatenate(quads).tolist()
    return verts, quads


def mesh_from(name, verts, faces, collection):
    me = bpy.data.meshes.new(name)
    me.from_pydata([tuple(v) for v in verts], [], [tuple(int(i) for i in f) for f in faces])
    me.validate()
    ob = bpy.data.objects.new(name, me)
    collection.objects.link(ob)
    return ob


# glibc mallopt(M_PERTURB, byte): every malloc'd block is filled with (byte ^ 0xFF) and every
# freed block with byte; 0 turns it off. 0x5A gives a fill of 0xA5 bytes, which read as a float or
# a double are a tiny negative number (about -3e-16 / -2e-130), i.e. effectively zero.
M_PERTURB = -6
QF_HEAP_FILL = 0x5A


def _libc_mallopt():
    try:
        libc = ctypes.CDLL(ctypes.util.find_library("c") or None)
        return libc.mallopt
    except (OSError, AttributeError):
        return None


@contextlib.contextmanager
def pinned_heap_fill():
    """Run the block with every fresh heap block filled with a fixed byte (glibc only).

    Why: Blender's bundled QuadriFlow (5.3.0) reads heap memory it never initialised. A fresh
    block from the operating system is zero, but a recycled one holds whatever was there before,
    so now and then the remesh came out slightly different: the same topology with vertices up
    to about 1.7 cm apart (#894). The proof: with no fill, about 1 run in 5 differs when it is
    repeated in one process (and about 1 in 36 in fresh processes); with this fill, every run
    gives the result the zero memory gives, so the committed outputs did not change; with the
    opposite fill (0xA5 -> 0x5A bytes, huge floats) the result changes and varies. The bug is
    inside Blender, so the kit pins the memory QuadriFlow sees instead. Elsewhere than glibc
    (no mallopt) this does nothing and the build may not be byte-identical.
    """
    mallopt = _libc_mallopt()
    if mallopt is None:
        yield
        return
    mallopt(M_PERTURB, QF_HEAP_FILL)
    try:
        yield
    finally:
        mallopt(M_PERTURB, 0)


def quadriflow(ob, faces, seed=1):
    with pinned_heap_fill(), bpy.context.temp_override(active_object=ob, object=ob, selected_objects=[ob],
                                   selected_editable_objects=[ob]):
        r = bpy.ops.object.quadriflow_remesh(target_faces=faces, seed=seed, use_mesh_symmetry=False,
                                             use_preserve_sharp=False, use_preserve_boundary=False,
                                             smooth_normals=False, mode="FACES")
    if "FINISHED" not in r:
        raise RuntimeError("QuadriFlow failed")
    return ob


def largest_component(verts, faces):
    """Drop stray bubbles: keep the connected component with the most faces."""
    n = len(verts)
    parent = np.arange(n)

    def find(a):
        while parent[a] != a:
            parent[a] = parent[parent[a]]
            a = parent[a]
        return a
    for f in faces:
        r0 = find(f[0])
        for i in f[1:]:
            r = find(i)
            if r != r0:
                parent[r] = r0
    roots = np.array([find(f[0]) for f in faces])
    vals, counts = np.unique(roots, return_counts=True)
    keep = vals[np.argmax(counts)]
    faces = [f for f, r in zip(faces, roots) if r == keep]
    used = np.unique(np.array(faces).ravel())
    remap = -np.ones(n, dtype=np.int64)
    remap[used] = np.arange(len(used))
    return verts[used], [[int(remap[i]) for i in f] for f in faces]


def taubin(ob, iters=12, lam=0.5, mu=-0.53):
    """Taubin (shrink-free) smoothing of an object's mesh, in place."""
    me = ob.data
    n = len(me.vertices)
    co = np.empty(n * 3)
    me.vertices.foreach_get("co", co)
    co = co.reshape(-1, 3)
    e = np.empty(len(me.edges) * 2, dtype=np.int64)
    me.edges.foreach_get("vertices", e)
    e = e.reshape(-1, 2)
    deg = np.bincount(e.ravel(), minlength=n).astype(float)[:, None]
    for it in range(iters * 2):
        s = np.zeros_like(co)
        np.add.at(s, e[:, 0], co[e[:, 1]])
        np.add.at(s, e[:, 1], co[e[:, 0]])
        lap = s / np.maximum(deg, 1) - co
        co = co + (lam if it % 2 == 0 else mu) * lap
    me.vertices.foreach_set("co", co.ravel())
    me.update()


def voxel_clean(ob, size):
    """Voxel remesh (OpenVDB) to guarantee a closed 2-manifold before QuadriFlow."""
    md = ob.modifiers.new("vox", "REMESH")
    md.mode = "VOXEL"
    md.voxel_size = size
    md.adaptivity = 0.0
    md.use_smooth_shade = True
    with bpy.context.temp_override(object=ob, active_object=ob):
        bpy.ops.object.modifier_apply(modifier=md.name)
    return ob
