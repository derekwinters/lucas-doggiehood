# Labrador Retriever (approved, #898)

The second per-breed dog model, a **Labrador Retriever**, built with the shared dog kit. Derek
approved round 1, and it is kept here as **source assets only** (#898), the same way
as the Golden Retriever. Nothing here is imported by Unity, because the folder is outside
`Assets/`; the game still uses the Cube Pets dog. Importing it depends on the glTF importer (#776).

It is meant to read as a Lab, not a Golden in a new colour: a broad skull with a clear stop, a
shorter and squarer muzzle, small ears that hang close to the head, a thick level "otter tail", a
slightly deeper and stockier body, and a short, smooth, glossy coat. Everything approved for the
Golden is kept: the mouth with its lips wrapping the corners, the one continuous skin, the hind-leg
build, the chest size and a sit that really rests on the ground.

It is built by the shared dog kit (`../kit/`, see `../README.md`) from this folder's `breed.py`,
which holds only what makes a Lab a Lab, and it passes every kit check (`model` and `motion` both
print `ALL OK`). It follows `docs/engineering/blender-pipeline.md`, like the Golden.

## Coat names (open question)

The game's coat slots are **Default / Light / Dark / Black**, and a Lab has three real colours.
The files keep the Lab's own names (`dog_labrador_yellow.gltf`, `_black`, `_chocolate`; materials
`DH_Dog_Fur_Yellow` / `_Black` / `_Chocolate`). Mapping them onto the game's slots is a separate,
open question for Derek; once it is chosen, renaming them is a change to the `COATS` keys only.
The round-1 suggestion was:

| Lab coat | Suggested game slot | Why |
|---|---|---|
| Yellow | **Default** | The most familiar Lab colour, and closest to the other breeds' default |
| Chocolate | **Dark** | The darker brown coat |
| Black | **Black** | It matches the name |
| (none) | **Light** | No fourth Lab colour: leave Light unused for Labs, add a pale cream yellow, or map Light to Yellow as well |

## What makes it a Lab (what changed from the Golden)

| | Golden (approved) | Labrador |
|---|---|---|
| Skull | 0.60 m across, rounded | **0.64 m across**, flatter on top (squarer cross-section) |
| Stop | gradual slope from forehead to muzzle | **defined**: the forehead drops steeply between y −0.53 and −0.63 onto a level muzzle |
| Muzzle | long, tapering | **3.6 cm shorter, about 2.5 cm wider each side**, boxy (superellipse exponent 2.8). The nose is a little broader |
| Ears | 0.385 m long, 0.27 m wide, swing away from the head | **0.26 m long, 0.21 m wide**, set a little higher, hanging close to the cheeks |
| Tail | feathered plume, 0.14 m deep, curving down | **otter tail**: round section, 16 cm thick at the root, tapering evenly to a point, carried level with a slight droop, no plume |
| Body | — | **1.2–1.5 cm wider each side, 1.3–1.9 cm deeper under the ribs, a fuller loin** (less tuck-up), thicker neck. The topline is the same height |
| Legs | — | **same length and joints**; forearm and gaskin about 6 mm thicker each side, thigh about 1 cm. Paws are 2 mm wider |
| Coat | long strands with clumps, darker ears, pale feathering | **short, dense, smooth**: strands about 3–4 mm, almost no clumping, a much flatter normal map (`FUR_STRAND`), glossier material (roughness 0.60 instead of 0.82; specular 0.42 instead of 0.25). The "feathering" channel is only a slightly paler underside, and the ears are barely darker |
| Mouth, eyes, collar, rig, sit method | approved | **unchanged** (the mouth is moved onto the shorter muzzle; the eyes sit 1 cm wider and 1.5 cm higher on the face of the stop) |

`review/lab-vs-golden.png` shows these side by side (front, side, 3/4) at the same orthographic
scale, and overlays the Golden's side outline on the Lab.

## Sizes

Measured by the kit (`exports/dog_stats.json`), in the same size class as the Golden and today's
in-game box (`dh_scene.DOG`, 1.26 w × 1.50 l × 1.58 h).

| Measure | Labrador | Golden |
|---|---|---|
| Nose to rump | **1.46 m** | 1.49 m |
| Nose to tail tip | **1.85 m** | 1.94 m |
| Top of head | **1.58 m** | 1.585 m |
| Top of back | **1.035 m** | 1.034 m |
| Width incl. ears | **0.74 m** | 0.74 m |
| Body width at the ribs / shoulder, mid-height (same method for both) | **0.57 / 0.57 m** | 0.54 / 0.55 m |
| Underline at the ribs (lowest midline point) | **0.495 m** | 0.508 m |
| Puppy | adult × 0.55 | adult × 0.55 |

Height and the topline match the Golden. The Lab is 3 cm shorter nose-to-rump because its muzzle
is shorter, which is the breed, not a shrink, and 9 cm shorter nose-to-tail because its tail is
shorter and carried level. The width including ears is the same: the ears hang closer, but the head
and hips are wider. The origin is on the ground under the middle of the nose-to-rump span (`OY`
0.085 instead of the Golden's 0.10).

## Pieces and triangles

| Piece | Tris | LOD1 tris |
|---|---:|---:|
| body (one skin: torso, neck, head + jaw, legs, tail, mouth) | 2,950 | 988 |
| ear_L / ear_R | 96 each | 60 each |
| eye_L / eye_R | 136 each | 64 each |
| nose | 80 | 36 |
| collar (+ tag) | 176 | 110 |
| tongue | 96 | 48 |
| **Total** | **3,766** (budget 4,000) | **1,430** (budget 1,500) |

The skin has 1,492 vertices, at most 4 weights each, and 32 bones (the same rig as the Golden's).

## Coats, materials and textures

The values are sRGB hex. "Underside" is the slightly paler belly, chest, leg insides and muzzle
(the kit's feathering channel); a Lab has no feathering. "Ears" are barely darker.

| Coat | Main | Underside | Ears |
|---|---|---|---|
| Yellow (the default) | `#D6A86A` | `#E4C48F` | `#C6975A` |
| Black (charcoal) | `#2E2C2A` | `#353230` | `#292726` |
| Chocolate | `#5E3B25` | `#6D4730` | `#53331F` |

- **Black is a dark charcoal, not pure black.** The glossier fur material gives it a soft sheen,
  so the head, shoulders and legs still show their form (see `review/coats-and-puppy.png`).
- **The collar is the Golden's:** a red strap with a gold tag, the same size and placement.
- **The nose, eyes and lips** are the Golden's dark trim palette on every coat (see Known limits
  about Chocolate).

The coats are three `.gltf` files with identical `.bin`s (same mesh, rig and UVs; only the
`DH_Dog_Fur_<Coat>` material differs). The materials and textures are the Golden's set (see
`../golden-retriever/README.md`) with the Lab's coat names: `dog_fur_{yellow,black,chocolate}_color.png`,
`dog_fur_normal.png` and `dog_fur_mask.png` at 1024, the `dog_fur_lod1_*` set at 512, and
`dog_trim_color.png`.

**The short coat (`FUR_STRAND`).** The kit's fur bake used to have one strand look built in: the
Golden's long coat. `breed.py` sets the kit's optional `FUR_STRAND` field (see `../README.md`):
shorter and finer strands, less clumping, a normal map at 0.16 strength instead of 0.40, and less
light/dark strand contrast. A breed without the field gets the old look, so the Golden rebuilds
byte for byte unchanged.

## Checks (all pass)

**`model` → `ALL OK`.** All 4 files and the viewer `.glb` (1.84 MB, under 3 MB) are read back:
textures and materials, one skin with 32 joints, at most 4 weights per vertex summing to 1, weight
sanity, the default pose on the ground and matching Blender's within 2 mm, the triangle budgets
and the sit-contact check.

Sit contact (m; + = above the ground; stored in `dog_stats.json` as `sit_contact_m`):

| Part | Full | LOD1 |
|---|---:|---:|
| rump / haunches | +0.0001 | +0.0045 |
| hocks | −0.0031 | +0.0076 |
| hind feet | −0.0043 | −0.0021 |
| front paws | −0.0002 | +0.0016 |
| tail | −0.0002 | −0.0021 |
| lowest vertex, any piece | −0.0043 | −0.0021 |

**`motion` → `ALL OK`.** Every frame of every clip is checked on the skinned mesh. The gaits are
the Golden's, because the leg lengths are the same and the IK-reach check passes. TailWag is the
kit's calmer wag (#896): the tail swings while the body stands still (the kit's TailWag invariants,
see `../README.md`).

| Clip | Frames | Ground speed | Lowest point | Worst planted-paw slide / height |
|---|---|---|---|---|
| Idle | 120, loop | 0 | 0.0 mm | 0.0 / 0.0 mm |
| Walk | 30, loop | 0.80 m/s (stride 0.80 m) | 0.0 mm | 0.3 / 0.2 mm |
| Run | 16, loop | 3.50 m/s (stride 1.87 m) | 0.0 mm | 0.4 / 0.2 mm |
| SitDown | 54, once, ends on `SIT` | 0 | −4.3 mm (rear foot) | — |
| SitIdle | 90, loop | 0 | −4.3 mm (rear foot) | 0.0 / −4.3 mm |
| TailWag | 30, loop | 0 | 0.0 mm | 0.0 / 0.0 mm |
| PlayBow | 84, once, starts and ends standing | 0 | 0.0 mm | 0.1 / 0.0 mm |

The glTF read-back also passes: one skin, 32 joints, every clip sampled on every frame, and the
file's skinned mesh matching Blender's within 2 mm. The full report is
`motion/review/motion-contact.txt`. Run is fully airborne on frames 14–15 (the gallop's gathered
suspension), as for the Golden.

**Determinism.** Building `model` + `motion` twice from this `breed.py`, in fresh processes, gives
byte-identical textures, exports and reports (the kit's invariant, see `../README.md`).

## Files

| Path | What it is |
|---|---|
| `breed.py` | The breed definition, data only; the Lab's values are commented `Lab:` |
| `render_compare.py` | Lab-only extra: renders `lab-vs-golden.png` (builds the Lab, imports the Golden's committed default glTF) |
| `textures/` | See Coats, materials and textures |
| `exports/dog_labrador_yellow.gltf` / `_black` / `_chocolate` (+ `.bin`) | The adult in each coat: armature + skinned body and ears + rigid pieces |
| `exports/dog_labrador_lod1.gltf` (+ `.bin`) | The LOD, yellow coat, with its own 512 fur atlas |
| `exports/dog_labrador_skeleton.json` | Bones in Blender and glTF axes, accessory attachments, the test poses |
| `exports/dog_stats.json` | Triangles per piece, sizes, bones, weight statistics, file sizes, sit contact, check results |
| `motion/exports/dog_labrador_anim.gltf` (+ `.bin`) | The yellow-coat model with all 7 clips as named glTF animations; textures from `../../textures/` |
| `motion/exports/dog_labrador_anim_clips.json` | Per clip: frames, duration, loop, ground speed, stride, footfalls, duty factor, notes and check results |
| `motion/review/<clip>.mp4` | 1280×720 3/4 view of each clip |
| `motion/review/walk-contact-sheet.png`, `run-contact-sheet.png` | Side-view contact sheets |
| `motion/review/motion-contact.txt` | The per-frame contact report `export_anim.py` writes |
| `review/turnaround.png`, `gamecam.png`, `face-closeup.png`, `coats-and-puppy.png`, `sit.png`, `lab-vs-golden.png` | The main review sheets |
| `review/mouth.png`, `hips.png`, `joins.png`, `chest.png`, `rig.png`, `pivot-check.png`, `lod1.png` | The detail sheets, as for the Golden (no "before" rows: this is round 1) |

There is no `vs-splash.png`: there is no Labrador splash art yet.

## Regenerating

From the repository root, with headless Blender as a Python module (`pip install bpy==5.3.0`) plus
numpy and Pillow:

```
python3 art-source/blender/dogs/kit/build_breed.py labrador                    # model + motion: both print ALL OK
python3 art-source/blender/dogs/kit/build_breed.py labrador render <shot> art-source/blender/dogs/labrador/review
python3 art-source/blender/dogs/kit/build_breed.py labrador video <Clip>       # into motion/review/
python3 art-source/blender/dogs/kit/build_breed.py labrador sheet <Walk|Run>   # into motion/review/
python3 art-source/blender/dogs/labrador/render_compare.py art-source/blender/dogs/golden-retriever art-source/blender/dogs/labrador/review
python3 art-source/blender/dogs/kit/build_breed.py labrador solve-sit          # after any shape change (see the hock_z note)
```

`model` and `motion` rewrite `textures/`, `exports/`, `motion/exports/` and
`motion/review/motion-contact.txt`, and also write the git-ignored web-viewer files
(`dog-labrador-viewer.glb`, `motion/viewer/`). Renders are not byte-reproducible; the committed
review sheets and videos were rendered from this folder's committed build. Render one shot or clip
per command (EEVEE on lavapipe can crash after about 100 frames in one process).

**PNG metadata.** No PNG, JPG or MP4 committed here may carry C2PA metadata (a `caBX` PNG chunk or
an MP4 `uuid` box). Some shared folders add it to every file written to them; regenerate here
rather than copying files in.

## Known limits

- **A small crease in the sit** where the front of the haunch meets the elbow and forearm (the
  notch on the chest line in `sit.png`). It is linear-blend skinning folding the fuller Lab flank,
  and it is not visible at the game camera.
- **QuadriFlow sometimes drops the otter tail.** At the Golden's body target of 1,500 quads, the
  retopology silently lost everything past the tail root; a probe of 14 targets from 1,420 to
  1,560 lost it at 5 of them, with no pattern. `SKIN_FACES = 1530` keeps the whole tail (body
  2,950 tris). Any future shape change can flip this again. The sit check catches it (a tailless
  dog's tail cannot reach the ground, so the build fails), but a kit-level guard would be better;
  that belongs in its own issue.
- **One sit value is hand-set.** `solve-sit` gave `hock_z` 0.0428. With that, the full model was
  perfect but the LOD's decimated hock sat 11.7 mm up and failed the 1 cm check, so `hock_z` is
  4 mm lower by hand. The full model's hocks and rear feet sink 3–4 mm, and the LOD's hocks hover
  7.6 mm; both are within 1 cm. Every other `SIT` value is exactly as solved. The same 4.3 mm sink
  shows in SitDown's last frames and in SitIdle.
- **The ears still stand a little off the cheeks** at their lower edge in the front view. They are
  much closer than the Golden's, but bringing them in further risks the ear passing through the
  wider skull, because the ears are separate pieces, not part of the skin.
- **Chocolate keeps the black nose and dark-brown eyes.** Real chocolate Labs have a liver/brown
  nose. The kit's trim palette is per breed, not per coat, so this would need a per-coat trim (a kit
  change); it is out of scope here.
- **The coat is a texture, not geometry.** At the game camera the short coat reads as a smooth,
  slightly glossy surface with fine grain. Up close (`face-closeup.png`) faint strand streaks still
  show on the muzzle and forehead.
- The shared kit's limits are unchanged: the topology is automatic retopology, not hand-made; the
  skinning is region-based; and the atlas use is about 46%.
