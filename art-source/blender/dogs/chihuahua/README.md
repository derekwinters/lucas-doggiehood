# Chihuahua (approved, #914)

A per-breed dog model, a smooth-coat **Chihuahua** (the apple-head type), built with the shared
dog kit. Derek approved round 1 as is, and it is kept here as **source assets only** (#914), the
same way as the Golden Retriever, the Labrador and the German Shepherd. Nothing here is imported by
Unity, because the folder is outside `Assets/`; the game still uses the Cube Pets dog. Importing it
depends on the glTF importer (#776).

It is meant to read as a Chihuahua at the game camera, and as clearly **the smallest dog so far**,
well below the Beagle. It has a big round apple-dome skull with a pronounced stop and a short, fine,
slightly pointed muzzle, very large round dark eyes set wide, large erect ears that flare out at
about 45°, fine straight legs on small feet, a compact body a little longer than tall, and a thin
tail carried up in a sickle over the back. It keeps the family look approved for the others: the
big head and eyes (here even bigger for the body, which is the breed), the open mouth with lips
that wrap the corners, the red collar and tag (scaled down), the one continuous skin, the 32-joint
rig and a sit that really rests on the ground.

It is built by the shared dog kit (`../kit/`, see `../README.md`) from this folder's `breed.py`,
which holds only data, and it passes every kit check (`model` and `motion` both print `ALL OK`). **It needed no kit change**: it
uses only fields and optional keys that are already on main (`FUR_STRAND`, `FUR_PATCHES`,
`EARS["side"]` / `EARS["swing"]`). It follows `docs/engineering/blender-pipeline.md`.

## Coats (decided, #914)

There are exactly two, keyed by the game's coat slots (Derek, 2026-11-26): **Default** = fawn and
**Black** = black and tan. The files are `dog_chihuahua_default.gltf` and
`dog_chihuahua_black.gltf`, with materials `DH_Dog_Fur_Default` and `DH_Dog_Fur_Black`. There is
no Light or Dark coat. The game gives Chihuahuas only the Default slot today; wiring the slots in
Unity belongs to the later Unity import work (#776).

Values are sRGB hex. The kit's three-colour recipe is used as **main** (the tan points), **underside**
(the kit's feathering channel: chest, belly, inner legs) and **dark** (the kit's ear channel, laid
out by `FUR_PATCHES` over the top of the dog).

| Coat | Main (points) | Underside | Dark channel (back, head, ears, muzzle top, tail) |
|---|---|---|---|
| Default (fawn) | `#D8AA6F` sandy fawn | `#EEDCBC` pale cream | `#C99760` a slightly deeper fawn |
| Black (black and tan) | `#B8743C` tan | `#C98B52` lighter tan | `#1D1917` black |

- **The pattern** is one mask for both coats (`FUR_PATCHES`):
  - **Dark:** the back and neck, the upper flanks and the outside of the thighs, the upper legs,
    the whole head and the ears, the top of the muzzle, and the tail.
  - **Points (main colour):** an eyebrow pip above each eye, the cheeks and the sides of the
    muzzle below the eyes, the chest and the front of the neck, the lower legs and feet, and the
    underside of the tail at the root. The inner face of each ear is paler.
  - On **Black** this is the classic black and tan. On **Default** the dark channel is only a
    shade deeper than the main fawn, so the same mask reads as a fawn coat that is a little darker
    along the top (back, ear backs, top of the muzzle) and paler on the chest, belly and inner
    legs. The eyebrow pips are kept faint on the fawn on purpose.
- **A new coat is just one more `COATS` entry**: three colours and a `COAT_LABELS` label (for
  example chocolate and tan: a chocolate dark channel with the same tan).
- **The coat is short and smooth.** It uses the Lab's `FUR_STRAND` (short strands, a flat normal
  map at 0.16) and a fur material with a soft sheen (roughness 0.62, specular 0.40, close to the
  Lab's 0.60 / 0.42).
- **Collar, nose, lips and tongue:** the Golden's trim palette. The iris is a darker brown
  (`#3E2416`) for the dark Chihuahua eye.

The two coats are two `.gltf` files with identical `.bin`s: the same mesh, rig and UVs, with only
the `DH_Dog_Fur_<Coat>` material different. The textures are the kit's set with the Chihuahua's coat
names:
- `dog_fur_{default,black}_color.png`, `dog_fur_normal.png` and `dog_fur_mask.png` at 1024;
- the `dog_fur_lod1_*` set at 512;
- `dog_trim_color.png`.

## What makes it a Chihuahua

`breed.py` started from the Beagle (`../beagle/`), the closest build: a small dog with a short
coat.
The body and legs are mapped down from the Beagle's with explicit factors (listed in its header);
the head, ears, tail and coat are new. The erect-ear keys come from the approved German Shepherd.

| | Beagle | Chihuahua |
|---|---|---|
| Overall | 1.16 m tall, 1.17 m nose to rump | **the smallest dog**: 0.94 m tall (ear tips), 0.74 m nose to rump |
| Body | compact and sturdy, deep chest, level topline at 0.70 m | **compact, a little longer than tall**: level topline at 0.47 m, lengths 0.62× the Beagle's, depths 0.62×, widths 0.66×; a little more tuck-up |
| Neck | short and strong | **longer and carried higher, slightly arched**, so the head sits up |
| Legs | short for the body, sturdy | **fine-boned and straight**: 0.69× the Beagle's height and about half its width, so they are long for the body compared with the Beagle's. The hind leg is a little more angulated (see Known limits) |
| Feet | — | **small and dainty**: 0.62× as long and half as wide |
| Head | broad, domed skull; moderate stop; medium square muzzle | **apple dome**: a round skull 0.39 m across (the body is 0.32 m across), a **pronounced stop**, and a **short (9 cm), fine, slightly pointed muzzle** |
| Eyes | big and soft | **very large, round and dark**, set wide and a little prominent |
| Ears | long, low-set, hanging | **large, erect, broad at the base**: 0.19 m root to tip, 0.18 m across at the base, set high on the dome and **flaring out at about 45°**, opening forward and out (`EARS["side"]`, `EARS["swing"]` as the Shepherd's) |
| Tail | medium, upright flag, white tip | **thin and tapering, carried up in a sickle curve over the back**, not touching it: 9 cm at the root, 3 cm at the tip, about 0.36 m long, the tip about 0.2 m above the topline |
| Coat | short, smooth; tricolour / lemon and white | **short, smooth** (the Lab's strand look); fawn / black and tan (`FUR_PATCHES`) |
| Mouth, collar, rig | the family's | **unchanged** (the mouth is scaled onto the short muzzle; the collar strap and tag are 0.7×) |

`review/chihuahua-vs-beagle-vs-lab.png` shows all three side by side (front, side, 3/4) at the same
orthographic scale. It also overlays the Beagle's and the Lab's side outlines on the Chihuahua.

## Sizes

These are measured by the kit (`exports/dog_stats.json`), with the same method for every dog.

| Measure | Chihuahua | Beagle | Labrador | German Shepherd | Golden |
|---|---|---|---|---|---|
| Nose to rump | **0.74 m** | 1.17 m | 1.46 m | 1.52 m | 1.49 m |
| Nose to tail tip | **0.79 m** | 1.29 m | 1.85 m | 1.84 m | 1.94 m |
| Top of head | **0.94 m** (ear tips) | 1.16 m | 1.58 m | 1.84 m (ear tips) | 1.585 m |
| Top of back | **0.47 m** | 0.70 m | 1.035 m | 1.05 m | 1.034 m |
| Width incl. ears | **0.475 m** | 0.62 m | 0.74 m | 0.70 m | 0.74 m |
| Puppy | adult × 0.55 | adult × 0.55 | adult × 0.55 | adult × 0.55 | adult × 0.55 |

Against the Beagle, the Chihuahua is **0.81× as tall** to the ear tips (the top of the skull is
0.81 m, 0.70× the Beagle's head), **0.67× at the back** and **0.63× as long**. Against the Lab it
is 0.60× as tall and 0.46× at the back. In life a Chihuahua is about a third of a Lab's height; the
stylised family keeps big heads (and the Chihuahua's ears are tall), so the gap is smaller than in
life, but side by side it reads clearly as the smallest dog. The nose-to-tail-tip length is barely
more than nose to rump because the tail curls up over the back. The origin is under the middle of
the nose-to-rump span (`OY` 0.07). The back is measured between y 0.0 and 0.2, behind the withers
and ahead of the tail, whose tip comes forward over the back (`MEASURE["back_y"]`).

## Pieces and triangles

| Piece | Tris | LOD1 tris |
|---|---:|---:|
| body (one skin: torso, neck, head + jaw, legs, tail, mouth) | 3,014 | 940 |
| ear_L / ear_R | 96 each | 60 each |
| eye_L / eye_R | 136 each | 64 each |
| nose | 80 | 36 |
| collar (+ tag) | 176 | 110 |
| tongue | 96 | 48 |
| **Total** | **3,830** (budget 4,000) | **1,382** (budget 1,500) |

The skin has 1,521 vertices, at most 4 weights each summing to 1, and the same 32 bones and names
as the others. The default glTF's `.bin` is 162 KB, and the viewer `.glb` is 1.50 MB.

## Checks (all pass)

**`model` → `ALL OK`.** All 3 files and the viewer `.glb` are read back:
- textures and materials;
- one skin with 32 joints, at most 4 weights per vertex summing to 1, and weight sanity;
- the default pose on the ground and matching Blender's within 2 mm;
- the triangle budgets and the sit-contact check.

Sit contact (m; + = above the ground; `sit_contact_m` in `dog_stats.json`):

| Part | Full | LOD1 |
|---|---:|---:|
| rump / haunches | −0.0000 | +0.0003 |
| hocks | −0.0000 | −0.0058 |
| hind feet | −0.0016 | −0.0017 |
| front paws | −0.0011 | −0.0028 |
| tail | +0.0010 | +0.0011 |
| lowest vertex, any piece | −0.0016 | −0.0058 |

Every `SIT` value is exactly as `solve-sit` printed, with no hand edits. The solver settles on a
hip tilt of −54° (the Beagle −43°, the others −60°).

**`motion` → `ALL OK`.** Every frame of every clip is checked on the skinned mesh, and **no IK
target is out of reach** (no clamps). TailWag is the kit's calm wag, and its two invariants hold.
All 11 clips are in `motion/exports/dog_chihuahua_anim.gltf`: Idle, Walk, Run, SitDown, SitIdle,
TailWag, PlayBow, Swim and Shake (#934), and the lying clips LieDown and LieIdle (added in #933).

| Clip | Frames | Ground speed | Lowest point | Worst planted-paw slide / height |
|---|---|---|---|---|
| Idle | 120, loop | 0 | 0.0 mm | 0.0 / 0.0 mm |
| Walk | **22**, loop | **0.50 m/s** (stride 0.37 m) | −0.4 mm | 2.4 / 1.3 mm |
| Run | **12**, loop | **2.10 m/s** (stride 0.84 m) | −0.2 mm | 3.3 / 1.9 mm |
| SitDown | 54, once, ends on `SIT` | 0 | −8.5 mm (hind foot) | — |
| SitIdle | 90, loop | 0 | −1.6 mm (hind foot) | 0.0 / 3.0 mm |
| TailWag | 30, loop | 0 | 0.0 mm | 0.1 / 0.2 mm |
| PlayBow | 84, once, starts and ends standing | 0 | −0.1 mm | 0.9 / 0.4 mm |

The glTF read-back also passes: one skin, 32 joints, every clip sampled on every frame, and the
file's skinned mesh matching Blender's within 2 mm. The full report is
`motion/review/motion-contact.txt`.

**Gait changes, all in breed data (no kit change).** A small dog takes quick, short steps. The
strides are scaled to the legs (0.69× the Beagle's) on shorter cycles:

| | Beagle | Chihuahua |
|---|---|---|
| Walk | 26 frames, 0.61 m/s (stride 0.53 m) | 22 frames, 0.50 m/s (stride 0.37 m) |
| Run | 14 frames, 2.64 m/s (stride 1.23 m) | 12 frames, 2.10 m/s (stride 0.84 m) |

Other values scaled with the legs:
- swing lifts: Walk F 0.055 → 0.038 m, H 0.045 → 0.031 m; Run F 0.11 → 0.076 m, H 0.085 → 0.059 m;
- stance centre offsets and the gallop's suspension lift (0.025 → 0.017 m);
- `SIT_DOWN`: dip and step lifts; `rock_radius` is set to `-SIT["drop"] / sin(-SIT["hips"])`
  (0.29 m), so the hips arrive at the sit without first sinking below it (the first value tried,
  the hip-to-shoulder span, sank the haunches 3.4 cm into the ground near the end of SitDown);
- `PLAY_BOW` elbow and wrist heights (0.098 / 0.081 → 0.068 / 0.056 m).

The footfall order, duty factors, roll, curl and fold are unchanged.

**Determinism.** Building `model` + `motion` from this `breed.py` again in a separate folder, in
fresh processes, gives byte-identical textures, exports and reports (the kit's invariant).

## Kit change

None. Everything is breed data on the existing kit: no file under `kit/` changed, and the Golden,
the Labrador and the German Shepherd rebuild byte for byte unchanged.

## Files

| Path | What it is |
|---|---|
| `breed.py` | The breed definition, data only; the Chihuahua's choices are commented `Chihuahua:` |
| `render_compare.py` | Chihuahua-only extra: renders `chihuahua-vs-beagle-vs-lab.png` (builds the Chihuahua, imports the Beagle's and the Lab's committed default glTFs) |
| `textures/` | See Coats |
| `exports/dog_chihuahua_default.gltf` / `_black` (+ `.bin`) | The adult in each coat: armature + skinned body and ears + rigid pieces |
| `exports/dog_chihuahua_lod1.gltf` (+ `.bin`) | The LOD, default coat, with its own 512 fur atlas |
| `exports/dog_chihuahua_skeleton.json`, `exports/dog_stats.json` | Bones and attachments; triangles, sizes, weights, sit contact, check results |
| `motion/exports/dog_chihuahua_anim.gltf` (+ `.bin`), `dog_chihuahua_anim_clips.json` | The default-coat model with all 11 clips (Swim and Shake added in #934; LieDown and LieIdle, the lying clips, added in #933), and per-clip metadata and checks |
| `motion/review/<clip>.mp4`, `walk-contact-sheet.png`, `run-contact-sheet.png`, `motion-contact.txt` | Motion review |
| `review/turnaround.png`, `gamecam.png`, `face-closeup.png`, `coats-and-puppy.png`, `sit.png`, `chihuahua-vs-beagle-vs-lab.png` | The main review sheets |
| `review/mouth.png`, `hips.png`, `joins.png`, `chest.png`, `rig.png`, `pivot-check.png`, `lod1.png` | The detail sheets |

There is no `vs-splash.png`, because there is no Chihuahua splash art yet.

## Regenerating

From the repository root, with headless Blender as a Python module (`pip install bpy==5.3.0`) plus
numpy and Pillow:

```
python3 art-source/blender/dogs/kit/build_breed.py chihuahua                   # model + motion: both print ALL OK
python3 art-source/blender/dogs/kit/build_breed.py chihuahua render <shot> art-source/blender/dogs/chihuahua/review
python3 art-source/blender/dogs/kit/build_breed.py chihuahua video <Clip>      # into motion/review/
python3 art-source/blender/dogs/kit/build_breed.py chihuahua sheet <Walk|Run>  # into motion/review/
python3 art-source/blender/dogs/chihuahua/render_compare.py art-source/blender/dogs art-source/blender/dogs/chihuahua/review
python3 art-source/blender/dogs/kit/build_breed.py chihuahua solve-sit         # after any shape change
```

`model` and `motion` rewrite `textures/`, `exports/`, `motion/exports/` and
`motion/review/motion-contact.txt`, and also write the git-ignored web-viewer files
(`dog-chihuahua-viewer.glb`, `motion/viewer/`). Renders are not byte-reproducible; the committed
review sheets and videos were rendered from this folder's committed build.

A video can stop partway (EEVEE on lavapipe crashes after about 100 frames in one process); run the
same command again and it resumes from the frames already rendered.

**PNG metadata.** No PNG, JPG or MP4 committed here may carry C2PA metadata (a `caBX` PNG chunk or
an MP4 `uuid` box). Some shared folders add it to every file written to them; regenerate here
rather than copying files in.

## Known limits

Derek accepted these round-1 rough spots as is (#914).


- **The sit hides the tail.** As with the Beagle, the kit's sit-contact rule makes the tail lie on
  the ground, and the solver gets there by swinging the sickle tail down about 180° so it ends
  tucked under the haunches. The check passes, but the sitting dog shows no tail
  (`review/sit.png`, the SitDown and SitIdle clips), and the tucked tail leaves a few small
  crumpled bits at the bottom of the rump in the sit (`review/hips.png`, `review/joins.png`). Accepted
  for now; a tail that curls out on the ground beside the sitting dog would be a later change.
- **The tail is thicker than a real Chihuahua's** (9 cm at the root, 3 cm at the tip). QuadriFlow
  dropped thinner tails (7 cm root) at most targets, and is touchy on this shape in general: a
  `SKIN_FACES` target only a little different drops the tail or goes over budget (1450 was
  chosen from a scan where every target between 1400 and 1650 kept the tail). Any shape change
  can flip this again, as for the Lab, the Shepherd and the Beagle.
- **The ears flare at 45°, so at the game camera the near ear is foreshortened** and the pair
  reads smaller than in the front view (`review/gamecam.png`). In the Run the ears lay back hard
  (the Shepherd's `EARS["swing"]`); fine for a sprint, but it has not been tuned per breed.
- **The fawn's eyebrow pips show as two faint pale patches with straight sides**, and there is a
  faint vertical seam down the middle of the forehead on both coats (`review/face-closeup.png`,
  `review/lod1.png`). They come from the shared black-and-tan mask (`FUR_PATCHES` has only
  straight ramps, so a pip is a soft-edged box) and the texture seam of the kit's unwrap. On the
  black and tan the pips read well.
- **The hind legs are thin and a little more angulated than the Beagle's mapping gave.** With the
  straight mapped hind leg, PlayBow's raised rump (a fixed 3 cm lift in the kit) left the hind
  paws 2 cm short of the ground, so the hind leg's fore/aft offsets were bent 1.4× more about the
  hip. It passes, but the hocks now sit further back than a Chihuahua's upright hind leg.
- **The body is a little long and the rump square** in side view (`review/turnaround.png`): the
  torso is the Beagle's, shortened. A rounder, shorter rump would be more Chihuahua.
- **The open mouth is the family's**: on the short muzzle the lip line runs almost from ear to
  nose tip and the tongue hangs below the chin (`review/mouth.png`). It reads as cheerful at the
  game camera, but it is a wide grin for a muzzle this small.
- **The head is big for the body even for the family** (that is the breed), so the Chihuahua is
  0.81× the Beagle's height (ear tips) rather than the real ~0.6×; the back is 0.67× and the
  length 0.63×. The shared kit's limits are unchanged: automatic retopology, region-based
  skinning, atlas use about 50%.
