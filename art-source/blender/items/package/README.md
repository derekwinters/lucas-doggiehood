# Delivery package (#410)

The taped cardboard parcel the delivery truck drops at a dog's front door. Today the game drops a
plain white 0.6 m graybox cube (`DeliveryTruckView.DropPackage`); #930 swaps this model in and
animates it hopping from the truck to the doorstep. Nothing in this folder is imported by Unity
(it is outside `Assets/`). It follows `docs/engineering/blender-pipeline.md`.

**Status: built, waiting for Derek to approve the renders on #410.** Approved renders are copied
into `review/` (none yet).

## The model

| | |
|---|---|
| Size (L x D x H) | **0.60 x 0.50 x 0.45 m**, tape included: the graybox cube's 0.6 m on its longest side |
| Triangles | **88** (art budget backstop: 6,000 per mesh, provisional, #799) |
| Pivot | on the ground at the footprint centre, like a street prop |
| Axes | metres, front -Y, up +Z in Blender; the `.glb` is Y-up (front +Z) |
| Object | one mesh, `package` |
| Materials | `DH_Package_cardboard` (colour + normal map), `DH_Package_tape` (flat colour), `DH_Package_label` (colour map); all single-sided |
| File | `exports/package.glb`, 158 KB, textures embedded |

A box with chamfered edges, one strip of brown packing tape along the length over the top seam and
down both ends, and a plain shipping label on the top beside the tape, where the 45° game camera
sees it. The label has grey address bars and a barcode, and no words, logo or brand.

The tape is wider than a real 48 mm roll (85 mm) so it still reads at the game camera.

## Files

```
package/
  dh_package.py          the model: DIMS and the other named sizes, materials, build()
  dh_package_tex.py      the textures (numpy + Pillow, fixed seeds)
  package_checks.py      plain-Python checks: truck fit, triangle budget, size near the graybox
  export_package.py      writes textures/, exports/package.glb and exports/package_stats.json, reads the .glb back
  render_package.py      review renders (door, closeup, turnaround) into out/
  tests/test_package.py  the checks below, as unit tests
  exports/package.glb             the model, self-contained (Unity, #776, and a three.js viewer)
  exports/package_stats.json      size, triangles, materials, every check's result
  textures/package_cardboard_color.png    256 x 256, tileable, 0.5 m per repeat
  textures/package_cardboard_normal.png   256 x 256
  textures/package_label_color.png        256 x 160
```

Houses export a self-contained `.glb` with embedded textures, and so does this, rather than the
`.gltf` + shared `textures/` layout of the trees and street props: it is one small model with its
own textures, and the same file loads in a web viewer. The PNGs in `textures/` are the generator's
output, kept so a rebuild can be compared byte for byte.

## Rebuilding

From the repository root, with `pip install bpy==5.3.0` plus numpy and Pillow:

```
python3 art-source/blender/items/package/export_package.py                 # about 2 s, prints ALL OK
python3 art-source/blender/items/package/render_package.py all             # about 2 min on lavapipe
python3 -m unittest discover -s art-source/blender/items/package/tests      # about 3 s
```

`export_package.py` takes an optional output root (default: this folder). It exits non-zero unless
the read-back passes: one mesh named `package`, `DH_Package_*` materials, nothing double-sided,
every image embedded, no animations or skins, the file's size equal to `DIMS` and on the ground,
within the art budget, near the graybox size, and inside the truck's footprint.

> **Invariant — the export rebuilds byte for byte from the script.** Exporting into a clean folder
> rewrites `exports/package.glb`, `exports/package_stats.json` and the three textures exactly.
> `tests/test_package.py` (`RebuildsByteIdentical`) exports twice in fresh processes and compares
> against the committed files; 20 fresh-process exports were compared while building this.
> The chamfered body is built from explicit faces in a fixed order: Blender's bevel operator gave
> a different face order (and so a different index buffer) from one process to the next.

## Fits in the truck

The package has to fit inside the rear of the new delivery truck (#924). That truck isn't built
yet, and neither #924 nor the specs give its cargo-bay size. The only truck size that is written
down is today's **footprint, 4.875 x 2.25 m** (`DeliveryTruckFootprint` in Core: 3.25 x 1.5 m at
import, `ModelScale` 1.5, #660 / #672), which #924 keeps. The export checks the package against
that footprint with no height limit. It passes, but that only proves the package isn't bigger than
the truck.

Once #924 states its cargo bay, check against it (clearance is optional; the package may be turned
a quarter in plan):

```
python3 art-source/blender/items/package/package_checks.py --cargo L W H [--clearance C]
```

## Review renders

`render_package.py` draws the committed `.glb`, not a fresh build:

- `door`: the game camera (orthographic, pitch 45°, yaw -40°) at the approved base-1 house's
  front door. The package sits on the front walk at the foot of the steps, beside the approved
  adult Golden Retriever (`dogs/golden-retriever/exports/dog_golden_default.gltf`).
- `closeup`: the package close up from the game camera's direction (pitch 35°), beside a grey
  ghost of today's 0.6 m graybox cube.
- `turnaround`: front, right, back, left and top, on one sheet with the size and triangles.

Renders go to `out/` (git-ignored) and are not byte-reproducible.

## For #930

- The pivot is on the ground, not at the centre: place the model at ground height, not at
  `PackageHeight` (0.3 m, the graybox cube's centre).
- The collider can be a box of the size above. The graybox's 0.6 m cube collider is up to
  0.15 m bigger on each axis.
- The label is on the top at +X, -Y. If the package lands turned, it may face away from the camera.
