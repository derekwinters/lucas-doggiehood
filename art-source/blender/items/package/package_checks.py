"""Plain-Python checks for the delivery package (#410): no Blender needed.

  python3 package_checks.py --cargo L W H [--clearance C]     check the package against a cargo box

Sizes are (length, depth, height) in metres, Blender axes (X, Y, Z).

Truck fit. The package has to fit inside the rear of the new delivery truck (#924). That truck is
not built yet, and neither #924 nor the specs state its cargo-bay size, so the only stated truck
dimension is today's FOOTPRINT (DeliveryTruckFootprint in Core: 3.25 m x 1.5 m at import scale,
ModelScale 1.5, #660 / #672), which #924 keeps. export_package.py checks the package against that
footprint with no height, which is a necessary but not a sufficient check. When #924 states its
cargo bay, run this script with --cargo, or pass that box to fits_in_cargo().
"""
import argparse
import os
import sys

# DeliveryTruckFootprint.ModelLengthAtImportScale / ModelWidthAtImportScale * ModelScale
TRUCK_FOOTPRINT = (3.25 * 1.5, 1.5 * 1.5)

# Provisional per-mesh backstop (#799); the per-scene limits come later from phone measurements.
ART_BUDGET_TRIS = 6000

# Today's graybox: DeliveryTruckView.DropPackage scales a unit cube by 0.6.
GRAYBOX_SIZE = 0.6
# "Roughly today's cube size": the package's longest side within this fraction of the graybox.
GRAYBOX_TOLERANCE = 0.10


def fits_in_cargo(size, cargo, clearance=0.0):
    """True when the box `size` fits inside `cargo`, upright, turned either way in plan, with
    `clearance` left on every side (and above). A cargo height of None checks the plan only."""
    l, d, h = size
    cl, cd, ch = cargo
    c2 = 2 * clearance
    plan = any(a + c2 <= cl and b + c2 <= cd for a, b in ((l, d), (d, l)))
    return plan and (ch is None or h + clearance <= ch)


def within_budget(tris, budget=ART_BUDGET_TRIS):
    return tris <= budget


def near_graybox(size, graybox=GRAYBOX_SIZE, tolerance=GRAYBOX_TOLERANCE):
    return abs(max(size) - graybox) <= graybox * tolerance


def main(argv=None):
    sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
    ap = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    ap.add_argument("--cargo", type=float, nargs=3, required=True, metavar=("L", "W", "H"))
    ap.add_argument("--clearance", type=float, default=0.0)
    a = ap.parse_args(argv)
    import json
    with open(os.path.join(os.path.dirname(os.path.abspath(__file__)), "exports", "package_stats.json")) as fh:
        size = json.load(fh)["size_xyz_m"]
    ok = fits_in_cargo(size, a.cargo, a.clearance)
    print("package %s in cargo %s (clearance %.3f m): %s" % (size, a.cargo, a.clearance,
                                                             "FITS" if ok else "DOES NOT FIT"))
    return 0 if ok else 1


if __name__ == "__main__":
    sys.exit(main())
