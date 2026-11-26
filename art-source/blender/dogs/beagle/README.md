# Beagle (approved, #905)

The fourth per-breed dog model, a **Beagle**, built with the shared dog kit. Derek approved round
1, and it is kept here as **source assets only** (#905), the same way as the Golden Retriever, the
Labrador and the German Shepherd. Nothing here is imported by Unity, because the folder is outside
`Assets/`; the game still uses the Cube Pets dog. Importing it depends on the glTF importer (#776).

It is meant to read as a Beagle at the game camera, and as a clearly **smaller dog** than the other
three. It has short legs under a compact, deep body, a broad domed head with a square muzzle, long
low-set hanging ears, big soft eyes and an upright "flag" tail with a white tip. The coat is the
classic black, tan and white tricolour. It keeps the family look approved for the others: the big
head and eyes, the open mouth with lips that wrap the corners, the red collar and tag, the one
continuous skin, the 32-joint rig and a sit that really rests on the ground.

It is built by the shared dog kit (`../kit/`, see `../README.md`) from this folder's `breed.py`,
which holds only data, and it passes every kit check (`model` and `motion` both print `ALL OK`).
It needed one small, **optional** kit addition: a `"pale"` op for `FUR_PATCHES` (see Kit addition).
The Golden, the Labrador and the German Shepherd rebuild byte for byte unchanged with it. It
follows `docs/engineering/blender-pipeline.md`.

## Coats (decided, #905)

There are exactly two, keyed by the game's coat slots (Derek, 2026-11-26): **Default** = black, tan
and white tricolour, and **Light** = lemon and white. The files are `dog_beagle_default.gltf` and
`dog_beagle_light.gltf`, with materials `DH_Dog_Fur_Default` and `DH_Dog_Fur_Light`. **The Beagle
has no Dark or Black coat.** Wiring the slots in Unity belongs to the later Unity import work (#776).

Values are sRGB hex. The kit's three-colour recipe is used as **main** (tan), **underside** (the
kit's feathering channel, used here for all the white) and **dark** (the kit's ear channel, used by
`FUR_PATCHES` for the saddle and the top of the tail).

| Coat | Main | White (underside channel) | Dark channel (saddle, tail top) |
|---|---|---|---|
| Default (tricolour) | `#B5733A` tan | `#F2EDE3` warm white | `#1F1A17` black |
| Light (lemon and white) | `#E3C68E` pale lemon | `#F4F0E7` white | `#D9B677` slightly deeper lemon |

- **The pattern** is the same mask for both coats.
  - **Black saddle** (Default) over the back, from the withers to the tail, down the flanks to about
    mid-ribs, with a slightly broken edge. On Light it is a soft lemon patch, so there is no black.
  - **Tan** head, cheeks, ears, shoulders and thighs.
  - **White:** the muzzle (from just in front of the eyes), a narrow blaze up the forehead, the
    throat, chest and belly, the legs (front legs below the elbow, hind legs below the hock) and
    feet, and the **tail tip** (the "flag").
  - **The tail** is dark from the root to the white tip.
- **A new coat is just one more `COATS` entry**: three colours and a `COAT_LABELS` label.
  For example, red-and-white would be a red main and dark channel with the same white. The
  pattern does not change, because it lives in `FUR_PATCHES`.
- **The coat is short, dense and smooth.** It uses the Lab's `FUR_STRAND` (short strands, a flat
  normal map at 0.16) and a fur material with a soft sheen (roughness 0.64, specular 0.38,
  between the Lab's and the Shepherd's).
- **Collar, nose, lips and tongue:** the Golden's trim palette. The iris is a warmer brown
  (`#5A341E`, Golden `#4A2C1A`), for the soft hound eye.

The two coats are two `.gltf` files with identical `.bin`s: the same mesh, rig and UVs, with only
the `DH_Dog_Fur_<Coat>` material different. The textures are the kit's set with the Beagle's coat
names:
- `dog_fur_{default,light}_color.png`, `dog_fur_normal.png` and `dog_fur_mask.png` at 1024;
- the `dog_fur_lod1_*` set at 512;
- `dog_trim_color.png`.

## What makes it a Beagle

`breed.py` started from the approved Labrador, the closest build: a short coat, a square muzzle and
hanging ears. The body and legs are mapped down from the Lab's with explicit factors, which are
listed in its header.

| | Labrador (approved) | Beagle |
|---|---|---|
| Overall | 1.58 m tall, 1.46 m nose to rump | **a smaller dog**: 1.16 m tall, 1.17 m nose to rump |
| Legs | elbows and hips at 0.80 m; underline at the ribs 0.50 m | **short for the body**. Joints at 0.52 m and the legs above the paw are 0.62× as long, while the body is 0.75× as long and 0.80× as deep. The deep chest comes about down to the elbows, and the underline at the ribs is about 0.27 m, **0.38 of the back height** (Lab 0.48). Widths are 0.84×, so the legs are sturdy |
| Body | — | **compact and sturdy**, with a level topline at 0.70 m, a deep chest and a little tuck-up. The neck is short and strong |
| Head | broad, flat skull; defined stop; short square muzzle | **broad, domed skull**, 0.53 m across with a taller, rounder crown. The stop is moderate and the muzzle is medium, square and deep, with a little flew. The head group is scaled 0.84× (the body 0.75–0.80×), so the head is big for the body, in the same stylised family |
| Eyes | Golden size | **big and soft**: 1.14× the Lab's relative to the head, set a touch wider, with a warmer brown iris |
| Ears | 0.26 m, close to the cheek | **long, low-set, hanging**: rooted at about eye level, 0.37 m long and 0.27 m wide, with a round tip. They hang close to the cheeks to below the jaw line. Relative to the head, they are about 1.7× the Lab's length |
| Tail | otter tail, carried level | **medium, carried up**: 0.44 m long. It leaves the top of the croup at about 50° and curves up to about 84°, with a white tip. It is thick at the root (13 cm), tapering to a brush |
| Coat | short, smooth; yellow / black / chocolate | **short, smooth** (the Lab's strand look); tricolour and lemon-and-white (`FUR_PATCHES`) |
| Mouth, collar, rig | approved | **unchanged** (the mouth is head-scaled onto the muzzle; the collar strap and tag are 0.85×) |

`review/beagle-vs-lab-vs-shepherd.png` shows all three side by side (front, side, 3/4) at the same
orthographic scale. It also overlays the Shepherd's and the Lab's side outlines on the Beagle.

## Sizes

These are measured by the kit (`exports/dog_stats.json`), with the same method for all four.

| Measure | Beagle | Labrador | German Shepherd | Golden |
|---|---|---|---|---|
| Nose to rump | **1.17 m** | 1.46 m | 1.52 m | 1.49 m |
| Nose to tail tip | **1.29 m** | 1.85 m | 1.84 m | 1.94 m |
| Top of head | **1.16 m** | 1.58 m | 1.84 m (ear tips) | 1.585 m |
| Top of back | **0.70 m** | 1.035 m | 1.05 m | 1.034 m |
| Width incl. ears | **0.62 m** | 0.74 m | 0.70 m | 0.74 m |
| Puppy | adult × 0.55 | adult × 0.55 | adult × 0.55 | adult × 0.55 |

Against the Lab, the Beagle is **0.73× as tall** (head), **0.68× at the back** and **0.80× as long**.
A real Beagle is about 0.62× a Lab's height. The stylised family keeps big heads, so the gap is a
little smaller than in life, but side by side it reads clearly as the small dog of the four.
It is the first dog **well inside today's in-game stand-in box** (1.26 × 1.50 × 1.58 m). The origin
is under the middle of the nose-to-rump span (`OY` 0.09). The back is measured between y 0.05 and
0.40, which is behind the skull and ahead of the upright tail (`MEASURE["back_y"]`).

## Pieces and triangles

| Piece | Tris | LOD1 tris |
|---|---:|---:|
| body (one skin: torso, neck, head + jaw, legs, tail, mouth) | 3,070 | 1,000 |
| ear_L / ear_R | 96 each | 60 each |
| eye_L / eye_R | 136 each | 64 each |
| nose | 80 | 36 |
| collar (+ tag) | 176 | 110 |
| tongue | 96 | 48 |
| **Total** | **3,886** (budget 4,000) | **1,442** (budget 1,500) |

The skin has 1,556 vertices, at most 4 weights each summing to 1, and the same 32 bones and names
as the other three. The default glTF's `.bin` is 167 KB, and the viewer `.glb` is 1.88 MB.

## Checks (all pass)

**`model` → `ALL OK`.** All 4 files and the viewer `.glb` are read back:
- textures and materials;
- one skin with 32 joints, at most 4 weights per vertex summing to 1, and weight sanity;
- the default pose on the ground and matching Blender's within 2 mm;
- the triangle budgets and the sit-contact check.

Sit contact (m; + = above the ground; `sit_contact_m` in `dog_stats.json`):

| Part | Full | LOD1 |
|---|---:|---:|
| rump / haunches | −0.0000 | +0.0017 |
| hocks | −0.0017 | −0.0066 |
| hind feet | +0.0009 | +0.0015 |
| front paws | −0.0002 | +0.0008 |
| tail | +0.0000 | +0.0011 |
| lowest vertex, any piece | −0.0017 | −0.0066 |

Every `SIT` value is exactly as `solve-sit` printed, with no hand edits. The solver settles on a
hip tilt of −43° (the others −60°): with the shorter legs, that is the tilt that keeps the front
paws planted.

**`motion` → `ALL OK`.** Every frame of every clip is checked on the skinned mesh, and **no IK
target is out of reach** (no clamps). TailWag is the kit's calm wag (#896), and its two invariants
hold.

| Clip | Frames | Ground speed | Lowest point | Worst planted-paw slide / height |
|---|---|---|---|---|
| Idle | 120, loop | 0 | 0.0 mm | 0.0 / 0.0 mm |
| Walk | **26**, loop | **0.61 m/s** (stride 0.53 m) | −0.1 mm | 1.1 / 0.3 mm |
| Run | **14**, loop | **2.64 m/s** (stride 1.23 m) | 0.0 mm | 0.8 / 0.5 mm |
| SitDown | 54, once, ends on `SIT` | 0 | −1.9 mm (hock) | — |
| SitIdle | 90, loop | 0 | −1.7 mm (hock) | 0.0 / 3.0 mm |
| TailWag | 30, loop | 0 | 0.0 mm | 0.0 / 0.0 mm |
| PlayBow | 84, once, starts and ends standing | 0 | −0.2 mm | 0.3 / 0.2 mm |

The glTF read-back also passes: one skin, 32 joints, every clip sampled on every frame, and the
file's skinned mesh matching Blender's within 2 mm. The full report is
`motion/review/motion-contact.txt`.

**Gait changes, all in breed data (no kit change).** Shorter legs take shorter, quicker strides,
so the strides are about 0.66× the Lab's (the legs above the paw are 0.62×) on slightly shorter
cycles:

| | Lab / Golden | Beagle |
|---|---|---|
| Walk | 30 frames, 0.80 m/s | 26 frames, 0.61 m/s |
| Run | 16 frames, 3.50 m/s | 14 frames, 2.64 m/s |

Other values scaled with the legs:
- swing lifts: Walk F 0.075 → 0.055 m, H 0.06 → 0.045 m; Run F 0.16 → 0.11 m, H 0.12 → 0.085 m;
- stance centre offsets and the gallop's suspension lift (0.035 → 0.025 m);
- `SIT_DOWN`: `rock_radius` 0.62 → 0.465 m (the hip-to-shoulder span), dip and step lifts;
- `PLAY_BOW` elbow and wrist heights (0.115 / 0.095 → 0.098 / 0.081 m).

The footfall order, duty factors, roll, curl and fold are unchanged.

**Determinism.** Building `model` + `motion` from this `breed.py` again, in fresh processes, gives
byte-identical textures, exports and reports (the kit's invariant, see `../README.md`).

## Kit addition

There is one optional addition (see `../README.md`). A breed that does not use it builds exactly as
before: the Golden, the Labrador and the German Shepherd rebuild byte-identical with it.

- **`FUR_PATCHES` `"op": "pale"`** (`kit/dh_dog_tex.py`). Until now, patches only drew on the dark
  channel. The Beagle's white markings (blaze, muzzle, legs, tail tip) need the underside channel,
  and they must win over the saddle. A `"pale"` patch raises the underside channel to its strength
  and clears the dark channel by the same amount. `_patches` now takes and returns both channels,
  and a breed without `"pale"` patches gets its feathering channel back unchanged. The colour
  recipe is still three colours per coat. Tests are in `kit/tests/test_fur_patches.py`:
  - a pale patch whitens its area fully and clears the dark channel there;
  - it changes nothing outside its range;
  - it respects its strength.
- Documentation: `kit/dh_breed.py` (optional-fields comment) and `../README.md` (the `FUR_PATCHES`
  bullet and the test description).

## Files

| Path | What it is |
|---|---|
| `breed.py` | The breed definition, data only; the Beagle's choices are commented `Beagle:` |
| `render_compare.py` | Beagle-only extra: renders `beagle-vs-lab-vs-shepherd.png` (builds the Beagle, imports the Lab's and the Shepherd's committed default glTFs) |
| `textures/` | See Coats |
| `exports/dog_beagle_default.gltf` / `_light` (+ `.bin`) | The adult in each coat: armature + skinned body and ears + rigid pieces |
| `exports/dog_beagle_lod1.gltf` (+ `.bin`) | The LOD, default coat, with its own 512 fur atlas |
| `exports/dog_beagle_skeleton.json` | Bones in Blender and glTF axes, accessory attachments, the test poses |
| `exports/dog_stats.json` | Triangles per piece, sizes, bones, weight statistics, file sizes, sit contact, check results |
| `motion/exports/dog_beagle_anim.gltf` (+ `.bin`) | The default-coat model with all 7 clips as named glTF animations; textures from `../../textures/` |
| `motion/exports/dog_beagle_anim_clips.json` | Per clip: frames, duration, loop, ground speed, stride, footfalls, duty factor, notes and check results |
| `motion/review/<clip>.mp4` | 1280×720 3/4 view of each clip |
| `motion/review/walk-contact-sheet.png`, `run-contact-sheet.png` | Side-view contact sheets |
| `motion/review/motion-contact.txt` | The per-frame contact report `export_anim.py` writes |
| `review/turnaround.png`, `gamecam.png`, `face-closeup.png`, `coats-and-puppy.png`, `sit.png`, `beagle-vs-lab-vs-shepherd.png` | The main review sheets |
| `review/mouth.png`, `hips.png`, `joins.png`, `chest.png`, `rig.png`, `pivot-check.png`, `lod1.png` | The detail sheets, as for the Golden (no "before" rows: this is round 1) |

There is no `vs-splash.png`, because there is no Beagle splash art yet.

## Regenerating

From the repository root, with headless Blender as a Python module (`pip install bpy==5.3.0`) plus
numpy and Pillow:

```
python3 art-source/blender/dogs/kit/build_breed.py beagle                   # model + motion: both print ALL OK
python3 art-source/blender/dogs/kit/build_breed.py beagle render <shot> art-source/blender/dogs/beagle/review
python3 art-source/blender/dogs/kit/build_breed.py beagle video <Clip>      # into motion/review/
python3 art-source/blender/dogs/kit/build_breed.py beagle sheet <Walk|Run>  # into motion/review/
python3 art-source/blender/dogs/beagle/render_compare.py art-source/blender/dogs art-source/blender/dogs/beagle/review
python3 art-source/blender/dogs/kit/build_breed.py beagle solve-sit         # after any shape change
```

`model` and `motion` rewrite `textures/`, `exports/`, `motion/exports/` and
`motion/review/motion-contact.txt`, and also write the git-ignored web-viewer files
(`dog-beagle-viewer.glb`, `motion/viewer/`). Renders are not byte-reproducible; the committed
review sheets and videos were rendered from this folder's committed build. Render one shot or clip
per command (EEVEE on lavapipe can crash after about 100 frames in one process). `gamecam` needs
the base-1 house and street props from `art-source/blender` (found relative to the kit, or
`DH_ART`).

**PNG metadata.** No PNG, JPG or MP4 committed here may carry C2PA metadata (a `caBX` PNG chunk or
an MP4 `uuid` box). Some shared folders add it to every file written to them; regenerate here
rather than copying files in.

## Known limits

- **The sit hides the tail.** To lie on the ground (the kit's sit-contact rule), the upright tail
  swings down about 180° and ends tucked under the haunches. The check passes, but in the sit the
  tail cannot be seen (`review/sit.png`, `review/joins.png`). Derek accepted the tucked tail as is
  (#905); a tail that curls out beside the dog would need different `SIT_POSE` tail angles or a
  per-breed tail rule for the sit.
- **QuadriFlow is touchy on this shape.**
  - At the first tail thickness (10 cm at the root), it dropped the thin upright tail at every
    target, so the tail is now 13 cm at the root.
  - Its triangle count jumps around with the target: 1,460 quads gave 4,294 triangles (over
    budget), while 1,490 gives 3,886.
  - Any shape change can flip either again, like the Lab and the Shepherd. A kit guard for this
    belongs in its own issue.
- **The ears are flat blades with rounded tips**, not soft folded leathers. At the root, the front
  edge stands a little off the skull above the eye (`review/face-closeup.png`). They hang well at the
  game camera, but a real fold would need a new ear shape in the kit.
- **The upright tail reuses clips written for low tails.** TailWag, Walk and PlayBow lift `tail_1`,
  which on the Beagle tips the tail further forward, and in PlayBow it goes a little past vertical
  over the back. It reads as a happy flag, but it has not been tuned per breed.
- **The head is big for the body** (family style), so the Beagle is 0.73× the Lab's height rather
  than the real ~0.62×.
- **Markings are texture blends:** the saddle and blaze edges are soft rather than crisp. On Light,
  the lemon saddle is deliberately subtle (no black).
- The shared kit's limits are unchanged: automatic retopology, region-based skinning, atlas use
  about 49%.
