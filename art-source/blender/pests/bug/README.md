# Bug (design build for #334, pending approval)

The bug that replaces the five magenta cubes over a house with a bug problem (`BugSwarmView`,
#331). It is a **chunky, friendly cartoon fly**: a round green-bottle body with a striped abdomen,
two big cartoon eyes, short antennae, six stubby legs and two pale wings. It has one looping clip,
**WingFlap**: the wings beat and the body bobs gently, in place. The game only moves the bug along
its flight path, and flying a swarm of them over the house and spraying them is #927.

Nothing here is imported by Unity (the folder is outside `Assets/`). It follows
`docs/engineering/blender-pipeline.md`. **Status: awaiting Derek's approval of the renders on
#334.** The species and colours are a taste call made on the renders (see *Alternatives*).

## Files

```
pests/bug/
  dh_bug.py              the model and the WingFlap clip, from the named numbers at its top
  export_bug.py          export + read-back checks; prints ALL OK
  render_bug.py          review renders: gamecam | closeup | video | sheet
  make_viewer.py         out/viewer.html: a self-contained three.js viewer (not committed)
  exports/pest_bug.gltf + pest_bug.bin   the bug and its clip (glTF 2.0, flat colours, no images)
  exports/bug_stats.json triangles, size, materials, the clip and its measured ranges
  review/gamecam.png     a swarm of 5 over the approved base-1 house beside the adult Golden, game camera
  review/closeup.png     turnaround: 3/4 front, side (10 cm bar), top, front wings up / down, back 3/4
  review/flap.mp4        the clip, 3/4 view, 4 loops (3.2 s)
  review/flap-contact-sheet.png   every frame of one loop, and frame 24 (== frame 0)
  tests/test_bug_export.py        the checks below, on a fresh build
```

## Size, pivot and axes

| | |
|---|---|
| Size at rest (frame 0) | 0.45 m wingspan x 0.70 m long x 0.47 m high, legs and antennae included |
| Scale | Game-readable, not life-size: about the size of today's 0.6 m graybox cubes, so it reads over a roof at the 45° camera. #927 can scale it |
| Pivot | The centre of the thorax (the bug flies; there is no ground contact) |
| Axes | Blender: front −Y, up +Z, metres. glTF: front +Z, up +Y (`export_yup`) |

## Nodes, triangles and materials

| Node | What | Triangles |
|---|---|---|
| `pest_bug` | Root empty. **Never animated**: the game moves it | 0 |
| `pest_bug_body` | Head, eyes, thorax, striped abdomen, legs, antennae (one mesh). Bobs | 1,356 |
| `pest_bug_wing_L`, `pest_bug_wing_R` | Wings, hinged on the thorax. Flap | 76 each |
| **Total** | | **1,508** |

The provisional per-mesh backstop is 6,000 triangles (#799), so the whole bug is about a quarter of
it. A swarm of 5 is 7,540 triangles. The spheres are 12 x 8 (pupils and antenna tips 8 x 5,
catch-lights 6 x 4); fewer would look faceted in the close-up but still read in the game.

Eight flat-colour materials, `DH_Pest_bug_<slot>`: `body`, `band_light`, `band_dark`,
`eye_white`, `pupil`, `shine`, `leg`, `wing`. No textures. Every material is single-sided (the
wings are thin closed solids, 12 mm thick, so they need no double-sided material) and uses no glTF
extension.

**No collider.** The export holds only the four nodes above and their three meshes: no collider,
camera, light, skin or extension. The house underneath stays the tap target.

## The WingFlap clip

| | |
|---|---|
| Length | 24 frames at 30 fps (0.8 s), sampled every frame (25 samples per channel) |
| Loop | Frame 24 equals frame 0 on every channel (the values come from the phase in the loop) |
| Wings | 4 beats per loop (6 frames each, 5 per second), rolling about the body's long axis from 65° up to 25° down (90° peak to peak); frame 0 is wings up |
| Body | One bob per loop, ±2.5 cm, with a ±4° nose-up / nose-down rock |
| Root | Not animated: no root motion |

The game should play it on a loop, and can start each bug of a swarm at a different time so the
wings do not beat together (the gamecam render does this).

## Checks (all pass)

`export_bug.py` reads the written file back and exits non-zero unless: the nodes, meshes and
scene root are exactly the bug's; there is one animation, `WingFlap`, with frames + 1 samples per
channel and every channel ending on its first sample; the root is not animated; the triangles are
under the backstop and match Blender's count; nothing is double-sided.

`tests/test_bug_export.py` builds the bug into a temporary folder (fresh processes) and checks
independently, with plain Python reading the `.gltf` + `.bin`:

> **Invariants**
> - **The wing-flap clip loops on its first frame:** every channel's last sample equals its first.
> - **The bug has no collider:** the file holds only the bug's own nodes and meshes.
> - **The export is reproducible:** a second build, in another fresh process, is byte-identical to
>   the first and to the committed `exports/`.

It also checks both wings swing at least 60° and beat at least 3 times a loop, the body bobs
between 1 and 10 cm, the root is not animated, and the triangles are within the backstop and match
`bug_stats.json`.

## Regenerating

From the repository root, with `pip install bpy==5.3.0` (plus Pillow for the renders):

```
python3 art-source/blender/pests/bug/export_bug.py                   # exports/, about 2 s, ALL OK
python3 art-source/blender/pests/bug/render_bug.py gamecam [outdir]  # also closeup | video | sheet
python3 art-source/blender/pests/bug/make_viewer.py [out.html]
python3 -m unittest discover -s art-source/blender/pests/bug/tests   # about 5 s
```

The export rewrites `exports/` byte for byte. The renders go to `out/` (git-ignored) and are not
byte-reproducible; the committed ones in `review/` were rendered from this folder's committed
build. The render scripts reuse the dogs' render helpers (`dogs/kit/dh_dog_render.py`) and read the
approved `exports/base-01-level1.glb` house and the Golden's `dog_golden_default.gltf`; nothing
shared was changed. The video resumes if EEVEE on lavapipe crashes partway.

**PNG metadata.** No PNG or MP4 committed here may carry C2PA metadata (a `caBX` PNG chunk or an MP4
`uuid` box). The PNGs are saved by Pillow (IHDR / IDAT / IEND only) and the MP4 has its metadata
stripped. Some shared folders add C2PA to every file written to them; regenerate here rather than
copying files in.

## Alternatives for the review

- **Species and colour.** This is a green-bottle fly, picked because green reads against the grey,
  brown and terracotta roofs. A classic grey **housefly** with big red compound eyes is the obvious
  alternative (more "pest", less friendly), or a small dark **gnat** (thinner, reads worse at this
  size).
- **Wings** are opaque pale blue-white. See-through wings would need a blended material, which
  sorts poorly in a swarm.

## Open

- Import into Unity, the flight path, the spray and how many bugs: #927.
- The real per-scene art budget: #799 and its follow-ups.
