# German Shepherd (approved, #903)

The third per-breed dog model, a **German Shepherd**, built with the shared dog kit. Derek
approved round 1, and it is kept here as **source assets only** (#903), the same way as the Golden
Retriever and the Labrador. Nothing here is imported by Unity, because the folder is outside
`Assets/`; the game still uses the Cube Pets dog. Importing it depends on the glTF importer (#776).

It is meant to read as a Shepherd at the game camera, not a Golden in new colours: tall, erect,
pointed ears; a long, wedge-shaped muzzle with little stop and a dark mask; a black saddle over tan
legs, chest and cheeks; a back that slopes gently from the withers to the croup; more angulated
hind legs; and a long, bushy tail carried low. It keeps the family look approved for the Golden and
the Lab: the big head and eyes, the open mouth with lips that wrap the corners, the red collar and
tag, the one continuous skin, the 32-joint rig and a sit that really rests on the ground.

It is built by the shared dog kit (`../kit/`, see `../README.md`) from this folder's `breed.py`,
which holds only data, and it passes every kit check (`model` and `motion` both print `ALL OK`).
It needed four small, **optional** kit additions (see Kit additions); the Golden and the Labrador
rebuild byte for byte unchanged with them. It follows `docs/engineering/blender-pipeline.md`.

## Coats (decided, #903)

Exactly two, keyed by the game's coat slots (Derek, 2026-11-26): **Default** = black-and-tan,
**Black** = solid black. The files are `dog_shepherd_default.gltf` and `dog_shepherd_black.gltf`
(materials `DH_Dog_Fur_Default` / `DH_Dog_Fur_Black`). **The Shepherd has no Light or Dark coat.**
Wiring the slots in Unity belongs to the later Unity import work (#776).

Values are sRGB hex. "Underside" is the kit's feathering channel (chest, belly, leg backs, tail
underside); "dark" is the kit's ear channel, which `FUR_PATCHES` also uses for the saddle and mask.

| Coat | Main (tan) | Underside | Dark (saddle, mask, ears) |
|---|---|---|---|
| Default (black-and-tan) | `#AA6A38` | `#CC9C66` | `#221D1A` |
| Black | `#2A2725` | `#302C29` | `#252220` |

- **The pattern** (Default): a black saddle over the back and upper flanks that reaches up the top
  of the neck and down the croup and the top of the thighs, with a slightly broken edge; a dark
  muzzle mask that runs up between the eyes to a dark crown and the back of the skull; dark ears
  with paler (tan) inner faces; a dark tail with a little tan underneath near the root. The legs,
  chest, throat, cheeks and "eyebrow" spots are tan, with a paler underside.
- **Black is a very dark charcoal, not pure black,** like the Lab's black: with the slight sheen
  of the fur material (roughness 0.70, specular 0.34, between the Golden's 0.82/0.25 and the Lab's
  0.60/0.42) the head, shoulders and legs still show their form (`review/coats-and-puppy.png`).
- **Collar, nose, eyes, lips, tongue:** the Golden's trim palette, unchanged.

The two coats are two `.gltf` files with identical `.bin`s (same mesh, rig and UVs; only the
`DH_Dog_Fur_<Coat>` material differs). The textures are the Golden's set with the Shepherd's coat
names: `dog_fur_{default,black}_color.png`, `dog_fur_normal.png` and `dog_fur_mask.png` at 1024,
the `dog_fur_lod1_*` set at 512, and `dog_trim_color.png`.

## What makes it a Shepherd (what changed from the Golden)

| | Golden (approved) | German Shepherd |
|---|---|---|
| Ears | hanging, 0.385 m long | **erect and pointed**, 0.36 m root to tip, 0.24 m wide at the base, set high on the skull, leaning a little out and opening forward. Dark backs, tan inner faces |
| Skull | 0.60 m across, rounded | **0.575 m across**, flatter on top |
| Stop and muzzle | gradual stop, muzzle to y −0.868 | **little stop**; the forehead runs straight into a muzzle **3.2 cm longer**, a little shallower, that narrows to a wedge (half width 0.17 → 0.08 m over the last 0.2 m). Lip line 3 cm longer; tongue and jaw follow |
| Topline | level (about 1.03 m from withers to croup) | **slopes gently**: withers about 1.06 m, mid-back 1.03 m, croup 1.01 m, falling to 0.98 m at the tail set (profile tops) |
| Neck | — | **a light ruff**: 1.1–1.7 cm fuller each side at the neck base and the collar |
| Hind legs | stifle (0.350, 0.48), hock (0.500, 0.255), foot (0.472, 0.085) | **more angulated**: stifle (0.335, 0.485), hock (0.530, 0.255), foot (0.502, 0.085): the stifle 1.5 cm forward, the hock and rear foot 3 cm back, the lower thigh 3.2 cm longer; hip, hock and paw heights unchanged. A slightly broader thigh |
| Tail | feathered plume carried out and down | **long, bushy, carried low**: hangs from a lower set in a gentle curve to about 0.42 m off the ground, with a slight lift at the tip; 0.19 m across and 0.21 m deep at its fullest |
| Coat | long (Golden strand look) | **medium**: strands between the Lab's and the Golden's (`FUR_STRAND`), normal map at 0.28 strength (Golden 0.40, Lab 0.16) |
| Front legs, chest, eyes, mouth, collar, rig, gaits | approved | **unchanged** (the mouth is moved onto the longer muzzle) |

`review/shepherd-vs-lab-vs-golden.png` shows all three side by side (front, side, 3/4) at the same
orthographic scale, and overlays the Golden's and the Lab's side outlines on the Shepherd.

## Sizes

Measured by the kit (`exports/dog_stats.json`), same method for all three.

| Measure | German Shepherd | Labrador | Golden |
|---|---|---|---|
| Nose to rump | **1.52 m** | 1.46 m | 1.49 m |
| Nose to tail tip | **1.84 m** | 1.85 m | 1.94 m |
| Top of head | **1.84 m (ear tips)**; the skull is about as high as the others | 1.58 m | 1.585 m |
| Top of back (y 0–0.6) | **1.05 m** | 1.035 m | 1.034 m |
| Width incl. ears | **0.70 m** | 0.74 m | 0.74 m |
| Puppy | adult × 0.55 | adult × 0.55 | adult × 0.55 |

The Shepherd is 3 cm longer nose-to-rump than the Golden because of its longer muzzle. Its skull
is the same height as the others', but the erect ears add about 26 cm, so the "top of head" is
1.84 m: **taller than today's in-game stand-in box (1.58 m)** by the ears alone. The low tail makes
it 10 cm shorter nose-to-tail-tip than the Golden. The origin is under the middle of the
nose-to-rump span (`OY` 0.115).

## Pieces and triangles

| Piece | Tris | LOD1 tris |
|---|---:|---:|
| body (one skin: torso, neck, head + jaw, legs, tail, mouth) | 3,044 | 966 |
| ear_L / ear_R | 96 each | 60 each |
| eye_L / eye_R | 136 each | 64 each |
| nose | 80 | 36 |
| collar (+ tag) | 176 | 110 |
| tongue | 96 | 48 |
| **Total** | **3,860** (budget 4,000) | **1,408** (budget 1,500) |

The skin has 1,537 vertices, at most 4 weights each summing to 1, and the same 32 bones as the
Golden and the Lab (the joint names and count are unchanged; the ears use the same `ear_<s>_1` /
`ear_<s>_2` chain, pointing up). The default glTF's `.bin` is 164 KB; the viewer `.glb` is 1.74 MB.

## Checks (all pass)

**`model` → `ALL OK`.** All 4 files and the viewer `.glb` are read back: textures and materials, one
skin with 32 joints, at most 4 weights per vertex summing to 1, weight sanity, the default pose on
the ground and matching Blender's within 2 mm, the triangle budgets and the sit-contact check.

Sit contact (m; + = above the ground; `sit_contact_m` in `dog_stats.json`):

| Part | Full | LOD1 |
|---|---:|---:|
| rump / haunches | +0.0008 | −0.0011 |
| hocks | +0.0002 | +0.0051 |
| hind feet | −0.0005 | −0.0006 |
| front paws | −0.0000 | −0.0013 |
| tail | −0.0010 | −0.0038 |
| lowest vertex, any piece | −0.0010 | −0.0038 |

Every `SIT` value is exactly as `solve-sit` printed (no hand edits).

**`motion` → `ALL OK`.** Every frame of every clip is checked on the skinned mesh. The gaits are the
Golden's: the hind leg is longer (the lower thigh), the hip and paw heights are the same, and the
IK-reach check passes with no clamps. TailWag is the kit's calm wag (#896).

| Clip | Frames | Ground speed | Lowest point | Worst planted-paw slide / height |
|---|---|---|---|---|
| Idle | 120, loop | 0 | 0.0 mm | 0.0 / 0.0 mm |
| Walk | 30, loop | 0.80 m/s (stride 0.80 m) | 0.0 mm | 0.3 / 0.1 mm |
| Run | 16, loop | 3.50 m/s (stride 1.87 m) | 0.0 mm | 0.3 / 0.1 mm |
| SitDown | 54, once, ends on `SIT` | 0 | −6.1 mm (tail, frame 44) | — |
| SitIdle | 90, loop | 0 | −1.0 mm (tail) | 0.0 / 1.4 mm |
| TailWag | 30, loop | 0 | 0.0 mm | 0.0 / 0.0 mm |
| PlayBow | 84, once, starts and ends standing | 0 | 0.0 mm | 0.1 / 0.0 mm |

The glTF read-back also passes (one skin, 32 joints, every clip sampled on every frame, the file's
skinned mesh matching Blender's within 2 mm). The full report is `motion/review/motion-contact.txt`.

**Determinism.** Building `model` + `motion` from this `breed.py` again, in fresh processes, gives
byte-identical textures, exports and reports (the kit's invariant, see `../README.md`).

## Kit additions

All four are optional (see `../README.md`): a breed that does not set them builds exactly as before.

1. **`FUR_PATCHES`** (`kit/dh_dog_tex.py`): a list of patches laid over the coat's dark channel (a
   saddle, a mask) or lightening it (an ear's inner face). Each patch covers some body regions where
   the surface is above a `(y, z)` line and/or within `y` / `z` ramps, `|x| < x_max` and a normal
   ramp, with a soft edge broken up by the coat's own low-frequency noise. The colour recipe is
   unchanged (still three colours per coat), so a Unity shader could do the same.
   Test: `kit/tests/test_fur_patches.py`.
2. **Erect ears** (`kit/dh_dog.py`, `kit/dh_dog_rig.py`, `kit/dh_dog_anim.py`): `EARS["side"]` turns
   the ear loft's width to run across the head (the kit's ear is a hanging ear whose width runs
   front-to-back); the right ear's side is mirrored so each face of it gets the same UVs as the
   left's. `EARS["swing"]` scales the clips' and test poses' ear rotations per world axis: the clips
   are written for hanging ears, so the same "+X" that swings a hanging ear back would fold an
   erect ear forward. The Shepherd uses `(-0.7, -1, 1)`, so its ears lay back in Run and flick out
   in Idle. **No new bones**: same 32 joints, same names. Test: `kit/tests/test_erect_ears.py`.
3. **`SIT_DOWN["tail_lift"]` and `SIT_DOWN["tail_lag"]`** (`kit/dh_dog_clips.py`): during SitDown the
   kit lifts the tail 18° clear of the ground and lets it trail the hips by 6 frames, values tuned
   for the Golden's high tail. The Shepherd's low tail dug 23 mm into the ground halfway through
   the sit, so it sets 60° and 0 frames. Test: `kit/tests/test_sit_down_tail.py`.
4. Documentation of the new optional fields in `kit/dh_breed.py` and `../README.md`.

## Files

| Path | What it is |
|---|---|
| `breed.py` | The breed definition, data only; the Shepherd's values are commented `GSD:` |
| `render_compare.py` | Shepherd-only extra: renders `shepherd-vs-lab-vs-golden.png` (builds the Shepherd, imports the Golden's and the Lab's committed default glTFs) |
| `textures/` | See Coats |
| `exports/dog_shepherd_default.gltf` / `_black` (+ `.bin`) | The adult in each coat: armature + skinned body and ears + rigid pieces |
| `exports/dog_shepherd_lod1.gltf` (+ `.bin`) | The LOD, default coat, with its own 512 fur atlas |
| `exports/dog_shepherd_skeleton.json` | Bones in Blender and glTF axes, accessory attachments, the test poses |
| `exports/dog_stats.json` | Triangles per piece, sizes, bones, weight statistics, file sizes, sit contact, check results |
| `motion/exports/dog_shepherd_anim.gltf` (+ `.bin`) | The default-coat model with all 7 clips as named glTF animations; textures from `../../textures/` |
| `motion/exports/dog_shepherd_anim_clips.json` | Per clip: frames, duration, loop, ground speed, stride, footfalls, duty factor, notes and check results |
| `motion/review/<clip>.mp4` | 1280×720 3/4 view of each clip |
| `motion/review/walk-contact-sheet.png`, `run-contact-sheet.png` | Side-view contact sheets |
| `motion/review/motion-contact.txt` | The per-frame contact report `export_anim.py` writes |
| `review/turnaround.png`, `gamecam.png`, `face-closeup.png`, `coats-and-puppy.png`, `sit.png`, `shepherd-vs-lab-vs-golden.png` | The main review sheets |
| `review/mouth.png`, `hips.png`, `joins.png`, `chest.png`, `rig.png`, `pivot-check.png`, `lod1.png` | The detail sheets, as for the Golden (no "before" rows: this is round 1) |

There is no `vs-splash.png`: there is no German Shepherd splash art yet.

## Regenerating

From the repository root, with headless Blender as a Python module (`pip install bpy==5.3.0`) plus
numpy and Pillow:

```
python3 art-source/blender/dogs/kit/build_breed.py german-shepherd                   # model + motion: both print ALL OK
python3 art-source/blender/dogs/kit/build_breed.py german-shepherd render <shot> art-source/blender/dogs/german-shepherd/review
python3 art-source/blender/dogs/kit/build_breed.py german-shepherd video <Clip>      # into motion/review/
python3 art-source/blender/dogs/kit/build_breed.py german-shepherd sheet <Walk|Run>  # into motion/review/
python3 art-source/blender/dogs/german-shepherd/render_compare.py art-source/blender/dogs art-source/blender/dogs/german-shepherd/review
python3 art-source/blender/dogs/kit/build_breed.py german-shepherd solve-sit         # after any shape change
```

`model` and `motion` rewrite `textures/`, `exports/`, `motion/exports/` and
`motion/review/motion-contact.txt`, and also write the git-ignored web-viewer files
(`dog-shepherd-viewer.glb`, `motion/viewer/`). Renders are not byte-reproducible; the committed
review sheets and videos were rendered from this folder's committed build. Render one shot or clip
per command (EEVEE on lavapipe can crash after about 100 frames in one process).

**PNG metadata.** No PNG, JPG or MP4 committed here may carry C2PA metadata (a `caBX` PNG chunk or
an MP4 `uuid` box). Some shared folders add it to every file written to them; regenerate here
rather than copying files in.

## Known limits

- **QuadriFlow is touchy on this shape.** The body's retopology overshoots its target by a varying
  amount: 1,500 quads gave 4,004 triangles (over budget) on one tail shape and 3,448 on another, and
  one target left the LOD's hock 1.7 cm off the ground in the sit. `SKIN_FACES = 1420` gives 3,044
  body triangles and passes everything, but any shape change can flip it again (the Lab had the
  same problem with its tail). A kit guard for the overshoot belongs in its own issue.
- **The ears are thin, flat blades with a paler front, not cupped.** At the game camera they read
  clearly as erect Shepherd ears; in a pure side view an ear is a narrow spike. A real cupped ear
  would need a new ear shape in the kit. They are separate pieces (as on the Golden and Lab), rooted
  inside the skull.
- **The ears make the dog 1.84 m tall**, above today's in-game box (1.58 m). Shorter ears would
  read less like a Shepherd.
- **SitDown's lowest point is the tail at −6.1 mm** (limit −10 mm) near the end of the sit, even
  with the new tail options; the rest of the sit is within 1 mm.
- **The tail swings back quite far (60°) on the way down in SitDown** to stay clear of the ground
  (`motion/review/sitdown.mp4`).
- **The saddle edge is a texture blend**, soft rather than crisp, and the back of the skull shows a
  dark band rather than a full dark cap.
- **The tan is one warm tan** with a paler underside; real Shepherds shade from red-tan to cream.
- The shared kit's limits are unchanged: automatic retopology, region-based skinning, atlas use
  about 46%.
