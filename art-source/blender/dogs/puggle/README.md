# Puggle (approved, #909)

The fifth per-breed dog model, a **Puggle** (Pug × Beagle), built with the shared dog kit. Derek
approved round 1, and it is kept here as **source assets only** (#909), the same way as the Golden
Retriever, the Labrador, the German Shepherd and the Beagle. Nothing here is imported by Unity,
because the folder is outside `Assets/`; the game still uses the Cube Pets dog. Importing it
depends on the glTF importer (#776).

It is meant to read as a Puggle at the game camera — not a Pug and not a Beagle — and as a **small
dog**, about the Beagle's size, a little smaller and clearly stockier. It has a compact, chunky
body with a broad chest on short legs, a rounder head than the Beagle's with a **short muzzle**
(longer than a Pug's, shorter than a Beagle's), soft brow wrinkles, a **black mask**, medium soft
hanging ears that are darker than the coat, and a tail that **curls up and over the back** in a
loose loop. It keeps the family look approved for the others: the big head and eyes, the open
mouth with lips that wrap the corners, the red collar and tag, the one continuous skin, the
32-joint rig and a sit that really rests on the ground.

It is built by the shared dog kit (`../kit/`, see `../README.md`) from this folder's `breed.py`,
which holds only data, and it passes every kit check (`model` and `motion` both print `ALL OK`).
The curled tail needed three small, **optional** kit additions (see Kit additions). The Golden, the
Labrador, the German Shepherd and the Beagle rebuild byte for byte unchanged with them. It follows
`docs/engineering/blender-pipeline.md`.

## Coats (decided, #909)

There are exactly three, keyed by the game's coat slots (Derek, 2026-11-26): **Default** = fawn
with a black mask, **Black** = all black (a very dark charcoal like the Lab's and the Shepherd's
black; the mask blends in), and **Light** = a lighter apricot / cream fawn, still with the black
mask and darker ears. The files are `dog_puggle_default.gltf`, `dog_puggle_black.gltf` and
`dog_puggle_light.gltf`, with materials `DH_Dog_Fur_Default`, `DH_Dog_Fur_Black` and
`DH_Dog_Fur_Light`. **The Puggle has no Dark coat.** Wiring the slots in Unity belongs to the
later Unity import work (#776).

Values are sRGB hex, in the kit's three-colour recipe: **main**, **underside** (the kit's
feathering channel: the slightly paler chest, belly and leg backs) and **dark** (the kit's ear
channel, used by `FUR_PATCHES` for the mask, the ears and the wrinkles).

| Coat | Main | Underside | Dark (mask, ears, wrinkles) |
|---|---|---|---|
| Default (fawn, black mask) | `#CFA068` fawn | `#E9D3AA` pale fawn | `#2B221D` warm black |
| Black | `#2C2927` charcoal | `#34302D` | `#232020` |
| Light (apricot, black mask) | `#EBCF9F` light apricot | `#F6EAD2` cream | `#2B221D` warm black |

- **The pattern** is one mask shared by every coat (`FUR_PATCHES`):
  - the **black mask**: the muzzle in front of the stop, fading in over about 7 cm so its back edge
    is soft below the eyes;
  - **darker ears**: the kit's dark ear, lightened a little (about 68% of the dark colour over
    the main), so they read dark brown on fawn rather than black;
  - **brow wrinkles**: two soft, darker folds across the front of the skull above the eyes
    (texture only, 20–24% of the dark colour);
  - a clean fawn skull (the kit's darker crown is cleared).
- **A new coat is just one more `COATS` entry**: three colours and a `COAT_LABELS` label.
- **The coat is short and smooth.** It uses the Lab's / Beagle's `FUR_STRAND` (short strands, a
  flat normal map at 0.16) and the Beagle's fur shading (roughness 0.64, specular 0.38).
- **Collar, nose, lips, tongue and iris:** the Golden's trim palette.

The three coats are three `.gltf` files with identical `.bin`s: the same mesh, rig and UVs, with
only the `DH_Dog_Fur_<Coat>` material different. The textures are the kit's set:
`dog_fur_{default,black,light}_color.png`, `dog_fur_normal.png` and `dog_fur_mask.png` at 1024;
the `dog_fur_lod1_*` set at 512; `dog_trim_color.png`.

## What makes it a Puggle

`breed.py` started from the Beagle's (the closest build: a small dog with short legs and hanging
ears). The body and legs are mapped from the Beagle's with explicit factors, listed in its header:
about y 0.10 and the paw-centre height, **lengths ×0.92 and heights ×0.90**; the torso is
**×1.12 as wide** and ×0.96 as deep (so relatively deeper); the legs are ×1.06 as wide and set
×1.10 further apart.

| | Beagle | Puggle |
|---|---|---|
| Overall | 1.16 m tall, 1.17 m nose to rump | **about the Beagle's size, smaller and stockier**: 1.13 m tall, 1.05 m nose to rump, 0.64 m at the back |
| Body | compact, deep chest; 0.49 m across | **chunkier**: 0.55 m across at the ribs, a broad, deep chest that comes down to the elbows, a level topline, a short strong neck |
| Legs | short; shoulder and hip joints at 0.52 m | **shorter and sturdier**: shoulder and hip joints at 0.47 m, set wider apart |
| Head | broad, domed skull; square, medium muzzle | **rounder skull** (×1.04 wider, ×1.08 taller at the crown, rounder section) and a **short, broad muzzle**: ×0.70 the Beagle's length in front of the stop, with softer corners. Longer than a Pug's flat face, clearly shorter than the Beagle's. The head is carried 3 cm higher |
| Mask | white muzzle and blaze | **black mask** on the muzzle; soft brow wrinkles above the eyes |
| Ears | 0.37 m, long, low-set | **medium**: 0.23 m (×0.62), set a little higher, hanging close to the cheek to about the mouth corner; darker than the coat |
| Tail | upright "flag", 0.44 m | **curled over the back**: it leaves the top of the croup rising steeply, arcs over at about 0.96 m (world) and comes forward and down, the tip about 8 cm above the back. A loose curl (a Pug's is tight and flat on the hip). 13 cm thick at the root, tapering to 5 cm |
| Eyes, mouth, collar, rig | — | **the Beagle's** (moved with the head; the collar's skin rays start nearer, see Kit additions) |

`review/puggle-vs-beagle-vs-lab.png` shows the three side by side (front, side, 3/4) at the same
orthographic scale, and overlays the Beagle's and the Lab's side outlines on the Puggle.

## Sizes

These are measured by the kit (`exports/dog_stats.json`), with the same method for every breed.

| Measure | Puggle | Beagle | Labrador | German Shepherd | Golden |
|---|---|---|---|---|---|
| Nose to rump | **1.045 m** | 1.17 m | 1.46 m | 1.52 m | 1.49 m |
| Nose to tail tip | **1.09 m** | 1.29 m | 1.85 m | 1.84 m | 1.94 m |
| Top of head | **1.13 m** | 1.16 m | 1.58 m | 1.84 m (ear tips) | 1.585 m |
| Top of back | **0.64 m** | 0.70 m | 1.035 m | 1.05 m | 1.034 m |
| Width incl. ears | **0.64 m** | 0.62 m | 0.74 m | 0.70 m | 0.74 m |
| Puppy | adult × 0.55 | adult × 0.55 | adult × 0.55 | adult × 0.55 | adult × 0.55 |

Against the Beagle, the Puggle is **0.90× as long** (nose to rump), **0.92× at the back** and
**1.04× as wide**, with a 1.12× wider body. Its head top is almost the Beagle's (0.98×) because the
round head is carried higher. It sits well inside today's in-game stand-in box (1.26 × 1.50 ×
1.58 m). The origin is under the middle of the nose-to-rump span (`OY` 0.07). The nose-to-tail-tip
span is only 4.5 cm more than nose to rump, because the tail curls forward over the back. The back
is measured at world y 0.06–0.22, behind the skull and ahead of the curl's tip
(`MEASURE["back_y"]`).

## Pieces and triangles

| Piece | Tris | LOD1 tris |
|---|---:|---:|
| body (one skin: torso, neck, head + jaw, legs, tail, mouth) | 2,880 | 964 |
| ear_L / ear_R | 96 each | 60 each |
| eye_L / eye_R | 136 each | 64 each |
| nose | 80 | 36 |
| collar (+ tag) | 176 | 110 |
| tongue | 96 | 48 |
| **Total** | **3,696** (budget 4,000) | **1,406** (budget 1,500) |

The skin has 1,460 vertices, at most 4 weights each summing to 1, and the same 32 bones and names
as the others. The default glTF's `.bin` is 161 KB, and the viewer `.glb` is 1.83 MB.

## Checks (all pass)

**`model` → `ALL OK`.** All 5 files (3 coats, the LOD, the viewer `.glb`) are read back:
textures and materials; one skin with 32 joints, at most 4 weights per vertex summing to 1, and
weight sanity; the default pose on the ground and matching Blender's within 2 mm; the triangle
budgets; and the sit-contact check.

Sit contact (m; + = above the ground; `sit_contact_m` in `dog_stats.json`):

| Part | Full | LOD1 |
|---|---:|---:|
| rump / haunches | −0.0002 | −0.0016 |
| hocks | −0.0005 | −0.0057 |
| hind feet | −0.0013 | −0.0009 |
| front paws | +0.0019 | +0.0036 |
| tail (carried: must be ≥ +0.01) | +0.2380 | +0.2907 |
| lowest vertex, any piece | −0.0013 | −0.0057 |

The tail is **carried in the sit**, not laid on the ground (`SIT_POSE["tail_on_ground"]: False`):
it turns with the hips (`SIT["tail"]` 0), so the curl stays over the haunches and points up and
back, 24 cm clear of the ground. `solve-sit` fits the other four contacts and leaves the
Beagle-derived `SIT` unchanged (every contact already within 2 mm); the hip tilt is the Beagle's
−43°.

**`motion` → `ALL OK`.** Every frame of every clip is checked on the skinned mesh, and **no IK
target is out of reach** (no clamps). TailWag is the kit's calm wag, and its two invariants hold.

| Clip | Frames | Ground speed | Lowest point | Worst planted-paw slide / height |
|---|---|---|---|---|
| Idle | 120, loop | 0 | 0.0 mm | 0.0 / 0.0 mm |
| Walk | **24**, loop | **0.60 m/s** (stride 0.48 m) | −0.1 mm | 1.2 / 0.4 mm |
| Run | **13**, loop | **2.56 m/s** (stride 1.11 m) | −0.1 mm | 0.9 / 0.5 mm |
| SitDown | 54, once, ends on `SIT` | 0 | −1.8 mm | — |
| SitIdle | 90, loop | 0 | −1.3 mm | 0.0 / 3.9 mm |
| TailWag | 30, loop | 0 | 0.0 mm | 0.0 / 0.0 mm |
| PlayBow | 84, once, starts and ends standing | 0 | −0.1 mm | 0.4 / 0.1 mm |

The glTF read-back also passes: one skin, 32 joints, every clip sampled on every frame, and the
file's skinned mesh matching Blender's within 2 mm. The full report is
`motion/review/motion-contact.txt`.

**The curled tail stays clear of the back and the ground in every frame of every clip**
(`check_tail.py`, a Puggle-only extra; report in `motion/review/tail-clearance.txt`). It measures,
on the skinned mesh, the signed distance of every curl vertex (tail_2–4) from the back's surface,
and the lowest tail vertex:

| Clip | Curl above the back (closest) | Tail above the ground (lowest) |
|---|---:|---:|
| Idle | 9.8 cm | 57.7 cm |
| Walk | 9.7 cm | 55.8 cm |
| Run | 9.6 cm | 52.0 cm |
| SitDown | 9.8 cm | 14.8 cm |
| SitIdle | 9.5 cm | 14.8 cm |
| TailWag | 9.4 cm | 56.6 cm |
| PlayBow | 9.5 cm | 57.8 cm |

With the kit's clips as written for a tail carried out behind (no `TAIL_SWING`), the same check
**fails**: the curl goes 1.7 cm into the back in Run, 3.5 cm in TailWag and comes within 1 cm in
PlayBow. That is why the tail rotations are scaled (see Kit additions).

**Gait changes, all in breed data.** The legs are about 0.90× the Beagle's, so strides are 0.90×
with a touch quicker cycles:

| | Beagle | Puggle |
|---|---|---|
| Walk | 26 frames, 0.61 m/s (stride 0.53 m) | 24 frames, 0.60 m/s (stride 0.48 m) |
| Run | 14 frames, 2.64 m/s (stride 1.23 m) | 13 frames, 2.56 m/s (stride 1.11 m) |

Other values scaled with the legs (×0.90): the swing lifts, stance centre offsets and the gallop's
suspension lift; `SIT_DOWN` (`rock_radius` 0.42 m, the hip-to-shoulder span; dip and step lifts);
`PLAY_BOW` elbow and wrist heights (0.088 / 0.073 m). `SIT_DOWN["tail_lift"]` and `["tail_lag"]`
are 0: the curl turns with the hips on the way down (the default 18° lift tipped it to within 4 cm
of the back). The footfall order, duty factors, roll, curl and fold are unchanged.

**Determinism.** Building `model` + `motion` from this `breed.py` again, in fresh processes, gives
byte-identical textures, exports and reports (the kit's invariant, see `../README.md`).

## Kit additions

There are three optional additions (see `../README.md`). A breed that does not use them builds
exactly as before: the Golden, the Labrador, the German Shepherd and the Beagle rebuild
byte-identical with them.

- **`TAIL_SWING`** (a new optional top-level field; `kit/dh_dog_rig.py`, `kit/dh_dog_clips.py`):
  multipliers per world axis on the tail rotations the clips ask for in Idle, Walk, Run, TailWag
  and PlayBow, and in the "wag" test pose. The clips lift the tail (about X) for a tail carried
  out behind; on a curl that lift drives the tip down into the back. The Puggle sets
  `(0.2, 1.0, 1.0)`: a fifth of the lift, the full side-to-side wag. The sit's tail (`SIT`,
  `SIT_POSE`, SitDown, SitIdle) is not scaled.
- **`SIT_POSE["tail_on_ground"]`** (`kit/dh_dog_rig.py`, `export_dog.py`, `solve_sit.py`,
  `dh_dog_clips.py`): `False` carries the tail in the sit. The tail leaves the sit's contact
  groups — the model's sit check, `solve-sit`'s fits and the SitDown / SitIdle touches — and the
  sit check instead requires it to stay **at least 1 cm clear of the ground**. `SIT["tail"]` is
  then the breed's choice.
- **`COLLAR["ray"]`** (`kit/dh_dog.py`): how far out the collar's rays start when they find the
  skin around the collar loop (default 0.6 m). From 0.6 m, the rays toward the back met the curl
  first and the strap ran out to the tail. The Puggle sets 0.3 m (outside the neck, inside the
  curl).
- Tests: `kit/tests/test_curled_tail.py` (8 tests: it drives the clips' pose functions on a bare
  armature, records every bone rotation, and compares a breed with and without the keys) and
  `kit/tests/test_collar_ray.py` (2 tests: a stand-in neck with a ball in the old rays' way).
- Documentation: `kit/dh_breed.py` (optional-fields comment), `kit/export_dog.py` and
  `kit/solve_sit.py` docstrings, and `../README.md` (a bullet for the keys, the checks paragraph,
  the test descriptions).

## Files

| Path | What it is |
|---|---|
| `breed.py` | The breed definition, data only; the Puggle's choices are commented `Puggle:` |
| `check_tail.py` | Puggle-only extra: the curled tail's clearance from the back and the ground in every frame of every clip (exit 1 on failure) |
| `render_compare.py` | Puggle-only extra: renders `puggle-vs-beagle-vs-lab.png` (builds the Puggle, imports the Beagle's and the Lab's default glTFs) |
| `.gitignore` | Ignores the web-viewer files (`dog-puggle-viewer.glb`, `viewer/`, `motion/viewer/`) and scratch renders (`out/`), as the other breeds |
| `textures/` | See Coats |
| `exports/dog_puggle_{default,black,light}.gltf` (+ `.bin`) | The adult in each coat: armature + skinned body and ears + rigid pieces |
| `exports/dog_puggle_lod1.gltf` (+ `.bin`) | The LOD, default coat, with its own 512 fur atlas |
| `exports/dog_puggle_skeleton.json` | Bones in Blender and glTF axes, accessory attachments, the test poses |
| `exports/dog_stats.json` | Triangles per piece, sizes, bones, weight statistics, file sizes, sit contact, check results |
| `motion/exports/dog_puggle_anim.gltf` (+ `.bin`) | The default-coat model with all 7 clips as named glTF animations; textures from `../../textures/` |
| `motion/exports/dog_puggle_anim_clips.json` | Per clip: frames, duration, loop, ground speed, stride, footfalls, duty factor, notes and check results |
| `motion/review/<clip>.mp4` | 1280×720 3/4 view of each clip |
| `motion/review/walk-contact-sheet.png`, `run-contact-sheet.png` | Side-view contact sheets |
| `motion/review/motion-contact.txt` | The per-frame contact report `export_anim.py` writes |
| `motion/review/tail-clearance.txt` | The curled tail's clearance report `check_tail.py` writes |
| `review/turnaround.png`, `gamecam.png`, `face-closeup.png`, `coats-and-puppy.png`, `sit.png`, `puggle-vs-beagle-vs-lab.png` | The main review sheets |
| `review/mouth.png`, `hips.png`, `joins.png`, `chest.png`, `rig.png`, `pivot-check.png`, `lod1.png` | The detail sheets, as for the Golden (no "before" rows: this is round 1) |

There is no `vs-splash.png`, because there is no Puggle splash art yet.

## Regenerating

From the repository root, with headless Blender as a Python module (`pip install bpy==5.3.0`) plus
numpy and Pillow:

```
python3 art-source/blender/dogs/kit/build_breed.py puggle                   # model + motion: both print ALL OK
python3 art-source/blender/dogs/puggle/check_tail.py                       # the curl clears the back: ALL OK
python3 art-source/blender/dogs/kit/build_breed.py puggle render <shot> art-source/blender/dogs/puggle/review
python3 art-source/blender/dogs/kit/build_breed.py puggle video <Clip>      # into motion/review/
python3 art-source/blender/dogs/kit/build_breed.py puggle sheet <Walk|Run>  # into motion/review/
python3 art-source/blender/dogs/puggle/render_compare.py art-source/blender/dogs art-source/blender/dogs/puggle/review
python3 art-source/blender/dogs/kit/build_breed.py puggle solve-sit         # after any shape change
```

`model` and `motion` rewrite `textures/`, `exports/`, `motion/exports/` and
`motion/review/motion-contact.txt`, and also write the git-ignored web-viewer files
(`dog-puggle-viewer.glb`, `motion/viewer/`); `check_tail.py` rewrites
`motion/review/tail-clearance.txt`. Renders are not byte-reproducible; the committed review sheets
and videos were rendered from this folder's committed build. Render one shot or clip per command
(EEVEE on lavapipe can crash after about 100 frames in one process). `gamecam` needs the base-1
house and street props from `art-source/blender` (found relative to the kit, or `DH_ART`).
`render_compare.py` needs the Beagle's and the Lab's folders (with their exports) beside this one.

**PNG metadata.** No PNG, JPG or MP4 committed here may carry C2PA metadata (a `caBX` PNG chunk or
an MP4 `uuid` box). Some shared folders add it to every file written to them; regenerate here
rather than copying files in.

## Known limits

Derek accepted these rough spots as is (#909).


- **The brow wrinkles are texture stripes.** They are two soft darker bands, straight across
  (a `FUR_PATCHES` band follows a height, not a curve across the face), with fairly crisp ends.
  At the game camera they read as a wrinkled brow; close up they look painted on. Real folds would
  need geometry or a normal-map feature in the kit.
- **The mask is a texture blend** with a soft back edge, and the chin and lower jaw stay fawn
  (the mask follows the muzzle's front). On Black the mask disappears, as intended.
- **The curl is planar and symmetric** (in the dog's mid-plane, over the spine). Many Puggles
  carry the curl a little to one side; a sideways curl would need the kit's tail loft to leave the
  mid-plane.
- **The tail's clip motion is reduced, not re-authored.** `TAIL_SWING` keeps the side-to-side wag
  and drops most of the lift; the curl wags as a whole over the back. It reads as a happy curled
  tail but has not been tuned per clip. SitIdle's clip note still says "a lazy tail sweep along
  the ground" (kit text); the Puggle's sweep happens in the air.
- **A kit-level tail-clearance check does not exist.** `check_tail.py` is a Puggle-only extra;
  making it a kit check for every breed would change the other breeds' reports, so it belongs in
  its own issue.
- **Light vs Default** differ only in the body colour (a pale cream apricot vs a deeper fawn); side
  by side they are clearly different, on their own at the game camera less so. The mask and ears
  are the same on both.
- **The ears are flat blades with rounded tips** (the kit's ear shape), like the Beagle's.
- **The head is big for the body** (family style), so the Puggle stands nearly as tall at the
  head as the Beagle, though it is shorter at the back and in length.
- The shared kit's limits are unchanged: automatic retopology, region-based skinning, atlas use
  about 49%.
