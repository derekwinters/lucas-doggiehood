# Frenchton (approved, #918)

Another per-breed dog model, a **Frenchton** (French Bulldog × Boston Terrier), built with the
shared dog kit. Derek approved round 1, and it is kept here as **source assets only** (#918), the
same way as the Golden Retriever, the Labrador, the German Shepherd and the Beagle. Nothing here is
imported by Unity, because the folder is outside `Assets/`; the game still uses the Cube Pets dog.
Importing it depends on the glTF importer (#776).

It is meant to read as a Frenchton at the game camera, and **not** as a French Bulldog or a
Puggle. It is a **small, compact, muscular dog**, a little smaller than the Puggle and the Beagle,
lighter and a bit leggier than a Frenchie, with a level topline and a slight tuck-up. It has a
**square, Boston-style head** with a well-defined stop and a **short muzzle that is still a real
little snout**, big round wide-set eyes, **upright bat ears** set toward the corners of the skull,
and a **short, low stub tail**. The default coat is the Boston **black-and-white "tuxedo"**. It
keeps the family look approved for the others: the big head and eyes, the open mouth with lips
that wrap the corners, the red collar and tag, the one continuous skin, the 32-joint rig and a sit
that really rests on the ground.

It is built by the shared dog kit (`../kit/`, see `../README.md`) from this folder's `breed.py`,
which holds only data, and it passes every kit check (`model` and `motion` both print `ALL OK`).
It needed one small, **optional** kit addition: a `"plain"` op for `FUR_PATCHES` (see Kit
addition). Every other breed here rebuilds byte for byte unchanged with it. It follows `docs/engineering/blender-pipeline.md`.

## Coats (decided, #918)

There are exactly three, keyed by the game's coat slots (Derek, 2026-11-26), each with the same
white Boston markings: **Default** = black and white "tuxedo", **Dark** = brindle and white (soft
texture striping), **Light** = fawn and white with a slightly darker muzzle. The files are
`dog_frenchton_default.gltf`, `dog_frenchton_dark.gltf` and `dog_frenchton_light.gltf`, with
materials `DH_Dog_Fur_Default`, `DH_Dog_Fur_Dark` and `DH_Dog_Fur_Light`. **The Frenchton has no
Black coat**, so it never loses the Boston markings and stays distinct from the French Bulldog.
Wiring the slots in Unity belongs to the later Unity import work (#776).

Values are sRGB hex, in the kit's three-colour recipe: **main** (the body colour), **white** (the
kit's feathering channel, here used only for the white markings) and **dark** (the kit's ear
channel: the brindle stripes, the darker muzzle sides and lips, and the ears).

| Coat | Main | White | Dark (stripes, muzzle sides, ears) |
|---|---|---|---|
| Default (black and white) | `#211D1C` black | `#F3F0EA` warm white | `#1A1716` black |
| Dark (brindle and white) | `#6E4C33` brindle brown | `#F1EDE5` warm white | `#241A15` near-black brown |
| Light (fawn and white) | `#D8B282` cream fawn | `#F5F1E9` warm white | `#C59C6C` a slightly deeper fawn |

- **The pattern** is one mask shared by every coat (`FUR_PATCHES`):
  - **no kit underside**: a `"plain"` patch clears the kit's own paler chest, belly, leg backs and
    tail underside, so the coat is solid apart from its own markings (see Kit addition);
  - **white** (`"pale"` patches): a narrow **blaze** up the forehead between the eyes, a white
    **muzzle** above the lip line, the **chin and throat**, the **chest** and the **front feet**;
  - **darker muzzle sides and lips** (a dark patch in front of the stop, under the white);
  - **brindle stripes**: 27 soft, irregular stripes over the dark channel, slanting back up the
    body, neck and back of the skull (each is a dark patch in front of one slanted line and a light
    patch in front of a second line just ahead of it), plus a light dark wash on the legs.
- **Real brindle, not a fallback.** The stripes are true texture stripes, so the Dark coat is a
  striped brindle rather than a plain seal-brown (Derek's call, #918). They are soft (60% of the
  dark colour) and live in the shared mask, so they are on every coat. On Default the dark colour
  is the same black, so they cannot show. On Light the dark colour is only a little deeper than
  the fawn, so they are faint (6–10% darker), and the same channel gives Light its slightly darker
  muzzle sides.
- **A new coat is just one more `COATS` entry**: three colours and a `COAT_LABELS` label, and it
  keeps the same white markings.
- **The coat is short, fine and smooth.** It uses the Lab's / Beagle's `FUR_STRAND` (short strands,
  a flat normal map at 0.16) with a touch more sheen (roughness 0.60, specular 0.40; Beagle 0.64 /
  0.38).
- **Collar, lips, tongue and iris:** the Golden's trim palette; the nose is a slightly blacker
  `#1E1A19`.

The three coats are three `.gltf` files with identical `.bin`s: the same mesh, rig and UVs, with
only the `DH_Dog_Fur_<Coat>` material different. The textures are the kit's set with the
Frenchton's coat names:
- `dog_fur_{default,dark,light}_color.png`, `dog_fur_normal.png` and `dog_fur_mask.png` at 1024;
- the `dog_fur_lod1_*` set at 512;
- `dog_trim_color.png`.

## What makes it a Frenchton

`breed.py` started from the approved Beagle's (the closest small-dog build), with the German
Shepherd's optional erect-ear keys and the Puggle's gaits. The body and legs are mapped from the Beagle's
about y 0.10 and the paw-centre height with explicit factors, listed in its header: **lengths ×0.86,
heights ×0.90**; the torso is ×1.02 as wide, its depth above the spine ×0.88 and below it ×0.80
(keeping the topline), so the chest is shallower and more leg shows; the legs are ×1.03 as wide.

| | French Bulldog | Boston Terrier traits | Frenchton |
|---|---|---|---|
| Colour | fawn, dark mask | black-and-white tuxedo (also brindle / seal and white) | **black-and-white tuxedo** by default: white blaze, white muzzle, white chin, throat and chest, white front feet; brindle-and-white and fawn-and-white coats |
| Face | very flat | short muzzle, square, defined stop | **a little snout**: 0.14 m in front of the stop (×0.68 the Beagle's after the head scale ×0.95, about the Puggle's), square (exponent 2.7) and deep, with a well-defined stop |
| Skull | broad, domed | square, flat on top | **broad and square**: 0.53 m across, a squarer section (exponent 2.4, the Beagle's 2.15) and a flatter top (×0.96) |
| Ears | big, rounded bat ears | erect, pointed, at the skull corners | **upright bat ears**, 0.20 m tall, broad at the base and tapering to a rounded point, set toward the top corners of the skull and flaring a little outward (`EARS["side"]` / `["swing"]`, as the Shepherd) |
| Eyes | — | big, round, wide-set | **big and round**, rounder and a touch bigger than the Beagle's, set wide on the broad skull |
| Body | stocky, barrel-chested, short-legged | compact, square, lighter | **compact, square and muscular**, not barrel-chested: 0.50 m across at the ribs; the underline at the ribs is at 0.31 m, **0.49 of the back height** (Puggle 0.36, Beagle 0.38), so it stands taller on its legs than the Puggle |
| Tail | short, screw | short, low-set | **a short, thick stub** set low on the croup, below the topline, carried back and a little down: 0.20 m from the root, about 0.11 m showing behind the rump, 11 cm thick at the root |
| Mouth, collar, rig | — | — | **the Beagle's** (moved with the head, the parts in front of the stop with the shorter muzzle) |

Against the **Puggle** it differs in every identity cue: black-and-white (not fawn with a black
mask), upright bat ears (not hanging), a low stub (not a tail curled over the back), and a leggier,
less chunky body. `review/frenchton-vs-beagle-vs-puggle.png` shows the three side by side (front,
side, 3/4) at the same orthographic scale, and overlays the Beagle's and the Puggle's side outlines
on the Frenchton.

## Sizes

These are measured by the kit (`exports/dog_stats.json`), with the same method for every breed.

| Measure | Frenchton | Puggle | Beagle | Labrador | German Shepherd | Golden |
|---|---|---|---|---|---|---|
| Nose to rump | **1.00 m** | 1.045 m | 1.17 m | 1.46 m | 1.52 m | 1.49 m |
| Nose to tail tip | **1.11 m** | 1.09 m | 1.29 m | 1.85 m | 1.84 m | 1.94 m |
| Top of head | **1.21 m (ear tips)** | 1.13 m | 1.16 m | 1.58 m | 1.84 m (ear tips) | 1.585 m |
| Top of back | **0.64 m** | 0.64 m | 0.70 m | 1.035 m | 1.05 m | 1.034 m |
| Width incl. ears | **0.62 m** | 0.64 m | 0.62 m | 0.74 m | 0.70 m | 0.74 m |
| Puppy | adult × 0.55 | adult × 0.55 | adult × 0.55 | adult × 0.55 | adult × 0.55 | adult × 0.55 |

Against the Beagle, the Frenchton is **0.85× as long** (nose to rump) and **0.91× at the back**;
against the Puggle, 0.96× as long, the same at the back and a little narrower. The top of the
skull is at 1.11 m; the upright ears take it to 1.21 m, so it is the tallest of the three small dogs
at the ear tips only. It sits well inside today's in-game stand-in box (1.26 × 1.50 × 1.58 m). The
origin is under the middle of the nose-to-rump span (`OY` 0.03: nose −0.505 m, rump +0.509 m). The
back is measured at y 0.03–0.33 (`MEASURE["back_y"]`).

## Pieces and triangles

| Piece | Tris | LOD1 tris |
|---|---:|---:|
| body (one skin: torso, neck, head + jaw, legs, tail, mouth) | 2,662 | 1,008 |
| ear_L / ear_R | 96 each | 60 each |
| eye_L / eye_R | 136 each | 64 each |
| nose | 80 | 36 |
| collar (+ tag) | 176 | 110 |
| tongue | 96 | 48 |
| **Total** | **3,478** (budget 4,000) | **1,450** (budget 1,500) |

The skin has 1,347 vertices, at most 4 weights each summing to 1, and the same 32 bones and names
as the others. The default glTF's `.bin` is 149 KB, and the viewer `.glb` is 1.52 MB.

## Checks (all pass)

**`model` → `ALL OK`.** All 5 files (3 coats, the LOD, the viewer `.glb`) are read back: textures
and materials; one skin with 32 joints, at most 4 weights per vertex summing to 1, and weight
sanity; the default pose on the ground and matching Blender's within 2 mm; the triangle budgets;
and the sit-contact check.

Sit contact (m; + = above the ground; `sit_contact_m` in `dog_stats.json`):

| Part | Full | LOD1 |
|---|---:|---:|
| rump / haunches | −0.0018 | −0.0052 |
| hocks | −0.0011 | −0.0058 |
| hind feet | −0.0000 | −0.0001 |
| front paws | −0.0004 | +0.0068 |
| tail | +0.0005 | +0.0025 |
| lowest vertex, any piece | −0.0018 | −0.0058 |

Every `SIT` value is exactly as `solve-sit` printed, with no hand edits. The hip tilt settles at
−45.6° (the Beagle −43°). **The stub tail rests on the ground in the sit** (the kit's normal rule):
it points down and a little back behind the haunches (`SIT["tail"]` −9.7°, with `SIT_POSE` bending
`tail_2` / `tail_3` 12° / 8°). Getting there set the stub's length: at 0.13–0.17 m the tail could
not reach the ground from the sat croup (2.6–7 cm short at any angle), so it is 0.20 m from the root
(see Known limits).

**`motion` → `ALL OK`.** Every frame of every clip is checked on the skinned mesh, and **no IK
target is out of reach** (no clamps). TailWag is the kit's calm wag on the stub.

| Clip | Frames | Ground speed | Lowest point | Worst planted-paw slide / height |
|---|---|---|---|---|
| Idle | 120, loop | 0 | 0.0 mm | 0.0 / 0.0 mm |
| Walk | **24**, loop | **0.60 m/s** (stride 0.48 m) | −0.1 mm | 1.2 / 0.4 mm |
| Run | **13**, loop | **2.56 m/s** (stride 1.11 m) | −0.1 mm | 0.9 / 0.5 mm |
| SitDown | 54, once, ends on `SIT` | 0 | −2.0 mm | — |
| SitIdle | 90, loop | 0 | −1.8 mm | 0.0 / 1.2 mm |
| TailWag | 30, loop | 0 | 0.0 mm | 0.0 / 0.0 mm |
| PlayBow | 84, once, starts and ends standing | 0 | −0.2 mm | 0.4 / 0.2 mm |

The glTF read-back also passes: one skin, 32 joints, every clip sampled on every frame, and the
file's skinned mesh matching Blender's within 2 mm. The full report is
`motion/review/motion-contact.txt`.

**Gaits, all in breed data.** The legs are the Puggle's height (×0.90 the Beagle's), so the
Frenchton uses the Puggle's gaits unchanged (Walk 24 frames, 0.60 m/s; Run 13 frames, 2.56 m/s;
lifts, stance offsets and the gallop's suspension as the Puggle's). `SIT_DOWN["rock_radius"]` is
0.40 m (the hip-to-shoulder span, ×0.86 the Beagle's); its dips, steps and `PLAY_BOW` are the
Puggle's. **No `SIT_DOWN` tail keys were needed:** with the kit's default 18° lift and 6-frame lag
the stub stays above the ground all the way down (lowest point −2.0 mm, the hip, on frame 45).

**Determinism.** Building `model` + `motion` from this `breed.py` again in a separate folder, in
fresh processes, gives byte-identical textures, exports, motion exports and reports.

## Kit addition

There is one optional addition (see `../README.md`). A breed that does not use it builds exactly as
before: every other breed in this folder rebuilds byte-identical with it.

- **`FUR_PATCHES` `"op": "plain"`** (`kit/dh_dog_tex.py`, two lines in `_patches`). The kit
  always lays its paler underside (chest, belly, leg backs, thigh backs, tail underside, paws, the
  back of the rump) on the feathering channel. The Beagle's white markings live on that channel
  too, so on the Beagle the kit's underside is simply more white. On a black-and-white Boston it
  is wrong: the backs of the hind legs and thighs, the rump and the tail underside came out
  white-grey. No existing op could lower the feathering channel (`"light"` lowers the dark one),
  so a `"plain"` patch scales the feathering channel down by its strength and leaves the dark one
  alone. The Frenchton lays one full-strength `"plain"` patch over every region first, then its
  own `"pale"` markings (the Beagle's op). The colour recipe is still three colours per coat.
  Tests are in `kit/tests/test_fur_patches.py`:
  - a plain patch clears the feathering channel inside its range and leaves the dark channel and
    everything outside unchanged;
  - it respects its strength;
  - a later pale patch whitens over it.
- Documentation: `kit/dh_breed.py` (optional-fields comment) and `../README.md` (the `FUR_PATCHES`
  bullet and the test description). `FUR_PATCHES` stays optional (not in `REQUIRED`).

## Files

| Path | What it is |
|---|---|
| `breed.py` | The breed definition, data only; the Frenchton's choices are commented `Frenchton:` |
| `render_compare.py` | Frenchton-only extra: renders `frenchton-vs-beagle-vs-puggle.png` (builds the Frenchton, imports the Beagle's and the Puggle's default glTFs) |
| `textures/` | See Coats |
| `exports/dog_frenchton_{default,dark,light}.gltf` (+ `.bin`) | The adult in each coat: armature + skinned body and ears + rigid pieces |
| `exports/dog_frenchton_lod1.gltf` (+ `.bin`) | The LOD, default coat, with its own 512 fur atlas |
| `exports/dog_frenchton_skeleton.json` | Bones in Blender and glTF axes, accessory attachments, the test poses |
| `exports/dog_stats.json` | Triangles per piece, sizes, bones, weight statistics, file sizes, sit contact, check results |
| `motion/exports/dog_frenchton_anim.gltf` (+ `.bin`) | The default-coat model with all 11 clips (Swim and Shake added in #934; LieDown and LieIdle, the lying clips, added in #933) as named glTF animations; textures from `../../textures/` |
| `motion/exports/dog_frenchton_anim_clips.json` | Per clip: frames, duration, loop, ground speed, stride, footfalls, duty factor, notes and check results |
| `motion/review/<clip>.mp4` | 1280×720 3/4 view of each clip |
| `motion/review/walk-contact-sheet.png`, `run-contact-sheet.png` | Side-view contact sheets |
| `motion/review/motion-contact.txt` | The per-frame contact report `export_anim.py` writes |
| `review/turnaround.png`, `gamecam.png`, `face-closeup.png`, `coats-and-puppy.png`, `sit.png`, `frenchton-vs-beagle-vs-puggle.png` | The main review sheets |
| `review/mouth.png`, `hips.png`, `joins.png`, `chest.png`, `rig.png`, `pivot-check.png`, `lod1.png` | The detail sheets, as for the Golden (no "before" rows: this is round 1) |

There is no `vs-splash.png`, because there is no Frenchton splash art yet. No reference photos
were used or saved.

## Regenerating

From the repository root, with headless Blender as a Python module (`pip install bpy==5.3.0`) plus
numpy and Pillow:

```
python3 art-source/blender/dogs/kit/build_breed.py frenchton                   # model + motion: both print ALL OK
python3 art-source/blender/dogs/kit/build_breed.py frenchton render <shot> art-source/blender/dogs/frenchton/review
python3 art-source/blender/dogs/kit/build_breed.py frenchton video <Clip>      # into motion/review/
python3 art-source/blender/dogs/kit/build_breed.py frenchton sheet <Walk|Run>  # into motion/review/
python3 art-source/blender/dogs/frenchton/render_compare.py art-source/blender/dogs art-source/blender/dogs/frenchton/review
python3 art-source/blender/dogs/kit/build_breed.py frenchton solve-sit         # after any shape change
```

`model` and `motion` rewrite `textures/`, `exports/`, `motion/exports/` and
`motion/review/motion-contact.txt`, and also write the git-ignored web-viewer files
(`dog-frenchton-viewer.glb`, `motion/viewer/`). Renders are not byte-reproducible; the committed
review sheets and videos were rendered from this folder's committed build. Render one shot or clip
per command (EEVEE on lavapipe can crash after about 100 frames in one process). `gamecam` needs
the base-1 house and street props from `art-source/blender` (found relative to the kit, or
`DH_ART`).

`render_compare.py` needs the Beagle's and the Puggle's folders (with exports and textures) beside
this one.

**PNG metadata.** No PNG, JPG or MP4 committed here may carry C2PA metadata (a `caBX` PNG chunk or
an MP4 `uuid` box). Some shared folders add it to every file written to them; regenerate here
rather than copying files in.

## Known limits

These were reviewed with round 1 and are out of scope for this lock-in (#918).

- **The white markings have hard, boxy edges up close.** A `FUR_PATCHES` patch is a product of
  straight ramps, so the white muzzle is a soft-edged rectangle from the front and the blaze a
  straight stripe (`review/mouth.png`, front). At the game camera they read as a tuxedo face;
  close up they look stencilled. A rounder muzzle edge would need curved patch boundaries in the
  kit.
- **The brindle is subtle and only on the body.** The stripes slant back, soft and fairly regular;
  they show on the body, neck and back of the skull, and the legs get a flat darker wash instead
  (stripes across a leg would run along it). The face is plain brown with the dark muzzle sides.
  It reads as brindle at the game camera but not as richly as a real brindle coat.
- **Light shows faint stripes.** The stripes are in the shared mask, so the Light coat carries
  them at 6–10% darker than its fawn. Making Light's muzzle visibly darker would make its
  stripes visible too (the same channel). Per-coat patterns would need a kit change.
- **The stub is longer than a real Frenchton's.** It is 0.20 m from the root so it can reach the
  ground in the sit (the kit's rule); about 0.11 m shows behind the rump when standing, so it
  reads as a short pointed stub rather than a tiny screw tail. A shorter stub would need a carried
  tail in the sit (the Puggle's optional `SIT_POSE["tail_on_ground"]: False`), which this
  lock-in does not use.
- **The eyes are big and stand proud of the face** (family style, set wide on the broad skull);
  from the side they bulge a little past the cheek line.
- **The ears are flat blades** with a rounded point (the kit's ear shape), like the Shepherd's,
  with no inner-ear colour.
- The shared kit's limits are unchanged: automatic retopology, region-based skinning, atlas use
  about 47%.
