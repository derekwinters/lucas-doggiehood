# French Bulldog (approved, #915)

The fifth per-breed dog model, a **French Bulldog**, built with the shared dog kit. Derek approved
round 1, and it is kept here as **source assets only** (#915), the same way as the Golden, the
Labrador, the German Shepherd and the Beagle. Nothing here is imported by Unity, because the folder
is outside `Assets/`; the game still uses the Cube Pets dog. Importing it depends on the glTF
importer (#776).

It is meant to read as a Frenchie at the game camera, not a small Lab: large erect **bat ears**
set high and wide on a broad, flat skull; a **flat face** with a deep stop, a very short, broad,
square muzzle, a wide black nose and a black mask; big round eyes set low and wide; a **small,
compact, stocky body** with a broad, deep chest on short, wide-set front legs, a short back and a
rump that sits a little high; and a **stub tail** carried low. It keeps the family look approved for
the others: the big head and eyes, the open mouth with lips that wrap the corners, the red collar
and tag, the one continuous skin, the 32-joint rig and a sit that really rests on the ground.

It is built by the shared dog kit (`../kit/`, see `../README.md`) from this folder's `breed.py`,
which holds only data, and it passes every kit check (`model` and `motion` both print `ALL OK`).
**It needed no kit change**: the bat ears use the Shepherd's optional `EARS["side"]` /
`EARS["swing"]`, the mask uses `FUR_PATCHES`, the coat uses the Lab's `FUR_STRAND`, and the stub
tail's SitDown uses `SIT_DOWN["tail_lift"]` / `["tail_lag"]`. It follows
`docs/engineering/blender-pipeline.md`.

## Coat (decided, #915)

Exactly one, keyed by the game's coat slot (Derek, 2026-11-26): **Default** = fawn with a black
mask and dark ears. The file is `dog_frenchie_default.gltf` (material `DH_Dog_Fur_Default`). **The
French Bulldog has no Light, Dark or Black coat.** Wiring the slot in Unity belongs to the later
Unity import work (#776).

Values are sRGB hex. "Underside" is the kit's feathering channel (chest, belly, insides of the legs);
"dark" is the kit's ear channel, which `FUR_PATCHES` also uses for the mask.

| Coat | Main (fawn) | Underside | Dark (mask, ears) |
|---|---|---|---|
| Default (fawn, black mask) | `#C99A62` | `#DDBB8C` | `#2A2321` |

- **The pattern:** a black mask over the whole short muzzle, up the face around the eyes (the
  forehead and the outer cheeks stay fawn), and back along the lips (flews) to the mouth corners;
  dark ears, with paler inner faces; no darker crown. The rest is fawn, a little paler underneath.
- **A coat would be one more `COATS` entry** (three colours) and a `COAT_LABELS` label; the mask
  lives in `FUR_PATCHES`, so it carries over. A brindle would need its stripes as a texture
  pattern, which the kit cannot paint today (see Out of scope).
- **The coat is short, fine and smooth:** the Lab's `FUR_STRAND` (short strands, a flat normal map
  at 0.16) and a fur material with a soft sheen (roughness 0.62, specular 0.40, near the Lab's).
- **Collar, nose, lips and tongue:** the Golden's trim palette. The iris is a darker brown
  (`#3A2416`, Golden `#4A2C1A`).

The textures are the kit's set with the Frenchie's coat name: `dog_fur_default_color.png`,
`dog_fur_normal.png` and `dog_fur_mask.png` at 1024, the `dog_fur_lod1_*` set at 512, and
`dog_trim_color.png`.

## What makes it a Frenchie

`breed.py` started from the Beagle's round 1 (the smaller dog on the Lab's build). The body and legs
are mapped down from the Beagle's with explicit factors (listed in its header); the head, ears,
mouth and tail are new.

| | Labrador (approved) | Beagle (approved) | French Bulldog |
|---|---|---|---|
| Overall | 1.58 m tall, 1.46 m nose to rump | 1.16 m tall, 1.17 m nose to rump | **the smallest**: 1.14 m to the ear tips (0.96 m to the skull), 0.89 m nose to rump |
| Body | long, level | compact, deep chest | **short and stocky**: lengths 0.80× the Beagle's, heights 0.80×, widths kept, so it is as broad as the Beagle (0.50 m across the chest) on a much shorter body. A deep chest that hangs to 0.18 m off the ground, between the front legs; a tucked belly; the back rises a little from the withers (0.57 m) to the croup (0.59 m) |
| Legs | elbows and hips at 0.80 m | joints at 0.52 m | **short and sturdy**: shoulder 0.42 m, elbow 0.27 m; hind legs a touch longer (hip 0.436 m), so the rump sits a little high. Front legs wide-set (0.155 m off the midline, the outside of the leg at the edge of the chest) and 1.05× the Beagle's thickness; muscular thighs |
| Head | broad, flat skull; defined stop; short square muzzle | domed skull, square medium muzzle | **big, broad and square**: skull 0.51 m across, flat on top; the forehead drops 11 cm at a **deep stop** onto a **very short muzzle** (10 cm in front of the stop), broad (0.38 m) and square, with a slight rise just ahead of the stop (a hint of the nose roll); a wide black nose |
| Eyes | Golden size | big, soft | **big and round, set low** at the stop and wide apart, looking forward |
| Ears | hanging, 0.26 m | long, hanging | **bat ears**: erect, 0.24 m root to tip, 0.18 m across at the base and 0.22 m at their widest, holding their width to a broad rounded tip; set high and wide on the flat skull, leaning out about 25° and opening forward. Dark, with paler inner faces |
| Tail | otter tail, level | upright flag | **a stub**: 15 cm from its root inside the rump, much of it lying against the rump, thick (8.6 cm), set low on the rump and hanging down |
| Coat | short, smooth | short, tricolour | **short, fine, smooth**; fawn with a black mask |
| Mouth, collar, rig | approved | approved look | **unchanged** (the mouth corners sit under the eyes, inside the mask; the collar strap and tag are 0.8× the Golden's) |

`review/frenchie-vs-lab-vs-shepherd.png` shows the Frenchie beside the approved Lab and Shepherd
(front, side, 3/4) at the same orthographic scale, with the Shepherd's and the Lab's side outlines
over the Frenchie.

## Sizes

Measured by the kit (`exports/dog_stats.json`), the same way for every breed.

| Measure | French Bulldog | Beagle | Labrador | German Shepherd |
|---|---|---|---|---|
| Nose to rump | **0.89 m** | 1.17 m | 1.46 m | 1.52 m |
| Nose to tail tip | **0.92 m** | 1.29 m | 1.85 m | 1.84 m |
| Top of head | **1.14 m** (ear tips; skull 0.96 m) | 1.16 m | 1.58 m | 1.84 m (ear tips) |
| Top of back | **0.58 m** | 0.70 m | 1.035 m | 1.05 m |
| Width incl. ears | **0.60 m** | 0.62 m | 0.74 m | 0.70 m |
| Puppy | adult × 0.55 | adult × 0.55 | adult × 0.55 | adult × 0.55 |

Against the Beagle it is 0.76× as long (nose to rump) and 0.83× as high at the back; the skull is
0.96 m high against the Beagle's 1.16 m, and only the ears bring it up to about the Beagle's height.
Against the Lab it is 0.61× as long and 0.56× as high at the back. The head is big for the body
(family style). The origin is under the middle of the nose-to-rump span (`OY` 0.024). The back is
measured between y 0.0 and 0.30 (`MEASURE["back_y"]`).

## Pieces and triangles

| Piece | Tris | LOD1 tris |
|---|---:|---:|
| body (one skin: torso, neck, head + jaw, legs, tail, mouth) | 2,872 | 992 |
| ear_L / ear_R | 96 each | 60 each |
| eye_L / eye_R | 136 each | 64 each |
| nose | 80 | 36 |
| collar (+ tag) | 176 | 110 |
| tongue | 96 | 48 |
| **Total** | **3,688** (budget 4,000) | **1,434** (budget 1,500) |

The skin has 1,452 vertices, at most 4 weights each summing to 1, and the same 32 bones and names
as the others. The default glTF's `.bin` is 154 KB; the viewer `.glb` is 1.65 MB.

## Checks (all pass)

**`model` → `ALL OK`.** The default glTF, the LOD and the viewer `.glb` are read back: textures and
materials; one skin with 32 joints, at most 4 weights per vertex summing to 1, and weight sanity;
the default pose on the ground and matching Blender's within 2 mm; the triangle budgets and the
sit-contact check.

Sit contact (m; + = above the ground; `sit_contact_m` in `dog_stats.json`):

| Part | Full | LOD1 |
|---|---:|---:|
| rump / haunches | −0.0017 | −0.0074 |
| hocks | +0.0035 | −0.0061 |
| hind feet | +0.0046 | +0.0079 |
| front paws | −0.0003 | +0.0013 |
| tail | +0.0002 | +0.0007 |
| lowest vertex, any piece | −0.0017 | −0.0074 |

`SIT` is as `solve-sit` printed except `hock_z`, set 5 mm higher by hand (as the Lab's was, the
other way): the solved value left the LOD's decimated hock 11.5 mm below the ground.

**`motion` → `ALL OK`.** Every frame of every clip is checked on the skinned mesh, and no IK target
is out of reach. TailWag is the kit's calm wag (#896).

| Clip | Frames | Ground speed | Lowest point | Worst planted-paw slide / height |
|---|---|---|---|---|
| Idle | 120, loop | 0 | 0.0 mm | 0.0 / 0.0 mm |
| Walk | **24**, loop | **0.53 m/s** (stride 0.42 m) | −0.2 mm | 1.7 / 0.7 mm |
| Run | **13**, loop | **2.20 m/s** (stride 0.95 m) | −0.1 mm | 1.5 / 0.8 mm |
| SitDown | 54, once, ends on `SIT` | 0 | −8.1 mm (tail tip, mid-sit) | — |
| SitIdle | 90, loop | 0 | −1.7 mm (haunch) | 0.0 / 5.8 mm |
| TailWag | 30, loop | 0 | 0.0 mm | 0.0 / 0.0 mm |
| PlayBow | 84, once, starts and ends standing | 0 | −0.3 mm | 0.6 / 0.3 mm |

The glTF read-back also passes. The full report is `motion/review/motion-contact.txt`.

**Breed data the short legs and the stub needed (no kit change):**

- **Gaits:** strides about 0.80× the Beagle's (the legs above the paw are 0.80× / 0.84×) on
  slightly shorter cycles: Walk 24 frames at 0.53 m/s (Beagle 26 / 0.61), Run 13 frames at
  2.20 m/s (Beagle 14 / 2.64). Swing lifts, stance centre offsets and the gallop's suspension
  scaled with the legs; footfalls, duty factors, roll, curl and fold unchanged.
- **`SIT_DOWN`:** `rock_radius` 0.387 m (the hip-to-shoulder span), dip and step lifts scaled;
  **`tail_lift` −45°, `tail_lag` 0**. The stub hangs down, so the kit's default (+18°, trailing
  6 frames) swung it into the ground mid-sit (−14 mm); a negative lift swings it forward under the
  rump instead (−8.1 mm, within the 1 cm tolerance).
- **`PLAY_BOW`:** elbow and wrist heights 0.078 / 0.065 m. With the first, straighter hind leg,
  PlayBow's raised rump took the hind paws 15 mm off the ground (the leg could not reach); the
  stifle was moved 1.5 cm forward so the leg keeps more bend.
- **The tail is set low enough to reach the ground in the sit.** A stub set at the usual height sat
  6 cm above the ground (the sit-contact check wants the tail within 1 cm), so the stub leaves the
  lower rump and hangs down; seated, it rests on the ground under the rump.

**Determinism.** `model` + `motion` were rebuilt from this `breed.py` in a separate folder in fresh
processes, and every texture, export and report matched byte for byte.

## Kit change

None. The French Bulldog is breed data only, so the other breeds cannot change.

## Files

| Path | What it is |
|---|---|
| `breed.py` | The breed definition, data only; the Frenchie's choices are commented `Frenchie:` |
| `render_compare.py` | Renders `frenchie-vs-lab-vs-shepherd.png` (builds the Frenchie, imports the Lab's and the Shepherd's committed default glTFs) |
| `textures/` | See Coats |
| `exports/dog_frenchie_default.gltf` (+ `.bin`) | The adult: armature + skinned body and ears + rigid pieces |
| `exports/dog_frenchie_lod1.gltf` (+ `.bin`) | The LOD, with its own 512 fur atlas |
| `exports/dog_frenchie_skeleton.json`, `exports/dog_stats.json` | Bones and attachments; triangles, sizes, weights, sit contact, check results |
| `motion/exports/dog_frenchie_anim.gltf` (+ `.bin`), `dog_frenchie_anim_clips.json` | The model with all 7 clips, and per-clip metadata and checks |
| `motion/review/<clip>.mp4`, `walk-contact-sheet.png`, `run-contact-sheet.png`, `motion-contact.txt` | Motion review |
| `review/turnaround.png`, `gamecam.png`, `face-closeup.png`, `coats-and-puppy.png`, `sit.png`, `frenchie-vs-lab-vs-shepherd.png` | The main review sheets |
| `review/mouth.png`, `hips.png`, `joins.png`, `chest.png`, `rig.png`, `pivot-check.png`, `lod1.png` | The detail sheets |

There is no `vs-splash.png`: there is no French Bulldog splash art yet.

## Regenerating

From the repository root:

```
python3 art-source/blender/dogs/kit/build_breed.py french-bulldog                   # model + motion: both print ALL OK
python3 art-source/blender/dogs/kit/build_breed.py french-bulldog render <shot> art-source/blender/dogs/french-bulldog/review
python3 art-source/blender/dogs/kit/build_breed.py french-bulldog video <Clip>      # into motion/review/
python3 art-source/blender/dogs/kit/build_breed.py french-bulldog sheet <Walk|Run>  # into motion/review/
python3 art-source/blender/dogs/french-bulldog/render_compare.py art-source/blender/dogs art-source/blender/dogs/french-bulldog/review
python3 art-source/blender/dogs/kit/build_breed.py french-bulldog solve-sit         # after any shape change
```

No PNG, JPG or MP4 committed may carry C2PA metadata (a `caBX` PNG chunk or an MP4 `uuid` box).

## Out of scope (noted at approval)

Derek approved round 1 as is; these are known limits, left for later issues.


- **The stub barely shows.** To pass the sit-contact rule the stub leaves the lower rump and hangs
  down, so standing it reads as a small bump under the tail set (`review/hips.png`), and the
  TailWag swing is small (the wag lifts it out behind first, so it shows from behind and the
  side, but it is a short flick, not a sweep; `motion/review/tailwag.mp4`). A higher-set, screw-tail look would need the sit rule to allow
  a stub that does not reach the ground. A higher-set screw tail would need the sit-contact rule relaxed for stub tails.
- **No real nose roll or wrinkles.** The muzzle has a slight rise just ahead of the stop, but the
  shape library lofts one ring at a time, so a crease across the face is not possible without a
  new shape (or a normal-map detail).
- **The mask edges are texture blends**, soft rather than crisp; from the side the mask ends in a
  near-vertical edge on the cheek (`review/chest.png`, side tile).
- **The bat ears are flat blades with rounded tips**, not cupped; from the side they are thin.
  They read well from the front and at the game camera.
- **QuadriFlow is touchy on this shape**, as on the others: 1,440 quads gave 4,004 triangles (over
  budget), 1,400 gives 3,688. A shape change can flip it again.
- **`hock_z` is hand-set** (see Checks) to keep the LOD's hock within 1 cm; the full model's hocks
  sit 3.5 mm up.
- **Brindle would need a kit addition**: `FUR_PATCHES` paints regions, not stripes.
- **The head is big for the body** (family style), and the body keeps the Beagle's rounded
  barrel rather than a pear shape (broad front, narrow waist), so the "bulldog" taper is mild.
- The shared kit's limits are unchanged: automatic retopology, region-based skinning, atlas use
  about 50%.
