"""The dog bed's checks (#411), as plain functions of measured numbers and the exported glTF.

export_bed.py applies them to the real export and exits non-zero if any fails;
tests/test_bed_checks.py pins them. No Blender needed. Each function returns a list of error
strings, empty when the check passes.
"""
import os

# Today's graybox slab (DecorationView: localScale 1.4 x 0.35 x 1.0, long side along X).
SLAB_W = 1.4
SLAB_D = 1.0
# "About today's slab footprint": the bed must also fill at least this share of each side.
SLAB_FILL = 0.96
# The provisional per-mesh triangle backstop (#799).
BUDGET_TRIS = 6000
# Pivot: on the ground at the footprint centre, within this many metres.
PIVOT_TOL = 0.002
# The rest area: every sample within this many metres of the rest height.
FLAT_TOL = 0.012
MATERIAL_PREFIX = "DH_Item_Bed_"
REST_NODE = "ATTACH_REST"


def footprint_errors(size_xyz):
    w, d = size_xyz[0], size_xyz[1]
    errs = []
    if w > SLAB_W + 1e-6 or d > SLAB_D + 1e-6:
        errs.append("footprint %.3f x %.3f m is outside the %.1f x %.1f m slab" % (w, d, SLAB_W, SLAB_D))
    if w < SLAB_W * SLAB_FILL or d < SLAB_D * SLAB_FILL:
        errs.append("footprint %.3f x %.3f m fills less than %d%% of the slab" % (w, d, round(SLAB_FILL * 100)))
    return errs


def pivot_errors(lo, hi):
    errs = []
    cx, cy = (lo[0] + hi[0]) / 2, (lo[1] + hi[1]) / 2
    if abs(cx) > PIVOT_TOL or abs(cy) > PIVOT_TOL:
        errs.append("footprint centre is at (%.3f, %.3f), not the pivot" % (cx, cy))
    if abs(lo[2]) > PIVOT_TOL:
        errs.append("lowest point is at z = %.3f, not on the ground" % lo[2])
    return errs


def budget_errors(tris):
    if tris > BUDGET_TRIS:
        return ["%d triangles is over the %d backstop (#799)" % (tris, BUDGET_TRIS)]
    return []


def flat_top_errors(zs, rest_z):
    if not zs:
        return ["no samples of the rest area"]
    errs = []
    missed = sum(1 for z in zs if z is None)
    if missed:
        errs.append("%d of %d rest-area samples found no cushion" % (missed, len(zs)))
    off = [z for z in zs if z is not None and abs(z - rest_z) > FLAT_TOL]
    if off:
        errs.append("%d rest-area samples are more than %.3f m from the rest height %.3f (worst %.3f)"
                    % (len(off), FLAT_TOL, rest_z, max(off, key=lambda z: abs(z - rest_z))))
    return errs


def gltf_errors(j, gltf_path, tex_dir):
    errs = []
    tex = os.path.realpath(tex_dir)
    for im in j.get("images", []):
        uri = im.get("uri")
        if uri is None:
            errs.append("an image is embedded, not referenced from textures/")
            continue
        if os.path.isabs(uri) or ":" in uri:
            errs.append("image URI %s is not relative" % uri)
            continue
        f = os.path.realpath(os.path.join(os.path.dirname(gltf_path), uri))
        if os.path.dirname(f) != tex or not os.path.isfile(f):
            errs.append("image URI %s does not resolve inside textures/" % uri)
    for m in j.get("materials", []):
        if not m.get("name", "").startswith(MATERIAL_PREFIX):
            errs.append("material %r lacks the %s prefix" % (m.get("name"), MATERIAL_PREFIX))
    if not any(n.get("name") == REST_NODE for n in j.get("nodes", [])):
        errs.append("no %s node" % REST_NODE)
    return errs
