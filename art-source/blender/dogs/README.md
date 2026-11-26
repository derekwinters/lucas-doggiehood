# Dogs: one shared kit, one folder per breed

Every dog model is built by the same scripts in `kit/`. A breed is a folder here holding a
**`breed.py` of plain data** — its proportions, shapes, colours, sit, gaits and names — plus the
outputs the kit writes for it. Nothing here is imported by Unity (the folder is outside `Assets/`).
It follows `docs/engineering/blender-pipeline.md`.

| Folder | What it is |
|---|---|
| `kit/` | The shared pipeline: shape library, field meshing, skin, rig, textures, exports and checks, motion clips, review renders (see the table below) |
| `kit/tests/` | `test_breed_generality.py`: builds a deliberately different throwaway breed and checks it passes everything; `test_rebuild_determinism.py`: rebuilds the Golden in fresh processes and requires the committed bytes; `test_tail_wag.py`: the TailWag invariant below; `test_fur_strand.py`: the optional `FUR_STRAND` field; `test_fur_patches.py`: the optional `FUR_PATCHES` field; `test_erect_ears.py`: the optional `EARS["side"]` / `EARS["swing"]` keys; `test_sit_down_tail.py`: the optional `SIT_DOWN["tail_lift"]` / `SIT_DOWN["tail_lag"]` keys |
| `golden-retriever/` | The approved Golden Retriever (#855): `breed.py`, its outputs, and its round-comparison extras |
| `labrador/` | The approved Labrador Retriever (#898): `breed.py`, its outputs (yellow, black and chocolate coats), and `render_compare.py` (the Lab beside the Golden) |
| `german-shepherd/` | The approved German Shepherd (#903): `breed.py`, its outputs (black-and-tan default and solid black coats), and `render_compare.py` (beside the Lab and the Golden) |
| `beagle/` | The approved Beagle (#905): `breed.py`, its outputs (tricolour default and lemon-and-white light coats), and `render_compare.py` (beside the Lab and the Shepherd) |

> **Invariants**
> - **A breed definition holds data only:** numbers, strings, booleans, `None`, and tuples, lists
>   and dicts of them. No imports, functions or Blender calls. `kit/dh_breed.py` refuses anything
>   else when it loads the file.
> - **Every breed gets the same checks with no per-breed code:** the triangle budgets, the
>   sit-contact check, the per-frame motion contact and foot-slide check, and the glTF read-back.
> - **The kit holds no breed's numbers.** A value that differs between breeds goes in `breed.py`.

## Building a breed

From the repository root, with headless Blender as a Python module (`pip install bpy==5.3.0`) plus
numpy and Pillow:

```
python3 art-source/blender/dogs/kit/build_breed.py golden-retriever                  # model + motion, about 2 min
python3 art-source/blender/dogs/kit/build_breed.py golden-retriever model            # textures, exports, checks, stats
python3 art-source/blender/dogs/kit/build_breed.py golden-retriever motion           # clips, contact checks, read-back
python3 art-source/blender/dogs/kit/build_breed.py golden-retriever solve-sit        # print a new SIT for breed.py
python3 art-source/blender/dogs/kit/build_breed.py golden-retriever render <shot> [out_dir]
python3 art-source/blender/dogs/kit/build_breed.py golden-retriever video <Clip> [out_dir]
python3 art-source/blender/dogs/kit/build_breed.py golden-retriever sheet <Walk|Run> [out_dir]
```

The breed is a folder name here or a path to any folder holding a `breed.py`. Each step runs in its
own process and the command exits non-zero at the first failure. The `model` and `motion` steps each
print **`ALL OK`** when every check passes, and are deterministic: two runs give byte-identical
textures, exports and reports.

> **Invariant — building a breed twice from the same sources gives byte-identical exports,
> textures and reports, every run and in fresh processes** (#894).
>
> One setting holds this up. Blender's bundled QuadriFlow (the body's retopology) reads heap memory
> it never initialised, so now and then a build came out with the same mesh topology but vertices
> up to about 1.7 cm apart (1 in 36 fresh-process meshes; 1 of 12 motion rebuilds during #893). The
> bug is inside Blender, so `quadriflow()` in `kit/dh_dog_mesh.py` pins it: for that one call it
> sets glibc's `mallopt(M_PERTURB, 0x5A)`, which fills every fresh heap block with `0xA5` bytes
> (a float or double of effectively zero, the same result zero memory gives, so the committed files
> did not change), then turns it off. On a system without glibc's `mallopt` this does nothing and
> rebuilds may differ; `kit/tests/test_rebuild_determinism.py` shows whether they do. Render one sheet or clip per command — EEVEE on lavapipe can crash after
about 100 frames in one process. The scripts also run on their own, with the breed as the first
argument (`python3 kit/export_dog.py golden-retriever`).

## Adding a breed

1. **Copy** `golden-retriever/breed.py` into a new folder, `dogs/<breed>/breed.py`, and a
   `.gitignore` like the Golden's (it ignores `<name>-viewer.glb`, `viewer/` and `motion/viewer/`).
2. **Fill in the fields** (all of them are required, except the optional ones at the end of this
   list; the Golden's file documents each one):
   - **Names:** `NAME` (object and file stem, `dog_<breed>`), `DISPLAY_NAME`, `STATS_NAME`,
     `MOTION_STATUS`, `MATERIAL_NAMES` (keep the `DH_Dog_` prefix), `MATERIAL_SHADING`.
   - **Budgets:** `BUDGET` (triangles per file, full and LOD), `SKIN_FACES` (the body's
     QuadriFlow target), `LOD_SKIN_TRIS`.
   - **Body shape:** `TORSO`, `HEAD`, `TAIL` and the leg segments (`FRONT_UPPER`, `FRONT_FORE`,
     `FRONT_PASTERN`, `BACK_THIGH`, `BACK_GASKIN`, `BACK_META`), `FRONT_X` / `BACK_X` /
     `BACK_THIGH_DX`, the paws (`FRONT_PAW`, `BACK_PAW`, `PAW`), `LOFT_STYLE`, the smooth-union
     fillets (`FIELD`), and `FIELD_GRID` — its origin and extent must hold the whole standing dog.
     `HEAD_DY`, `OY` and `JAW_REST_OPEN_DEG` place the head group and the origin.
   - **Muzzle and mouth:** `HINGE`, `LIP_RUN`, `LIP_DROP`, `LIP_BAND`, `MOUTH_MAX_OPEN_DEG`,
     `MOUTH_ZONE`, `JAW_TIP`, `TONGUE`.
   - **Ears, eyes, nose, collar:** `EARS` (rings and the 2-bone chain), `EYES`, `NOSE`, `COLLAR`
     (which `TORSO` ring it hugs).
   - **Skeleton:** `SPINE`, `TAIL_BONES`, `JOINTS` (leg joints), `TORSO_CHAIN`.
   - **Sit:** `SIT_POSE` (the head and tail counter-rotations), then `SIT` from step 3.
   - **Checks and measurements:** `SANITY` (where head and tail weights may go), `MEASURE`.
   - **Coat:** `COATS` (any number; the first is the default, used by the LOD, the viewer and the
     motion export), `COAT_LABELS`, `FUR_FLOW`, `FUR_MARKINGS` (where feathering and ear colour
     start), `TRIM_COLOURS`.
   - **Motion:** `GAITS` (Walk and Run: frames, ground speed, footfalls, duty factors, lifts), and
     the size-dependent heights in `SIT_DOWN` and `PLAY_BOW`.
   - **Review renders:** `RENDER` (framing boxes, captions, and optional "before" rows from scripts
     kept in the breed's own folder, as the Golden's `render_round2.py` / `render_round3.py`).
   - **Optional — `FUR_STRAND`** (the fur strand look). Leave it out and the breed gets the
     Golden's long coat. Set any of its keys to change it: per noise layer (`fine`, `mid`, `clump`,
     `low`) the blur across / along the strands in cm, `mix` (the fine / mid / clump weights),
     `normal` (normal-map strength) and `contrast` (strand light/dark). The Labrador's short,
     smooth coat is the example. `STRAND` in `kit/dh_dog_tex.py` holds the defaults.
   - **Optional — `FUR_PATCHES`** (a coat pattern). A list of patches laid over the coat's dark
     ("ears") channel: each covers some body regions where the surface is above a `(y, z)` line
     and/or inside `y` / `z` ramps, `|x| < x_max` and a normal ramp `n`; `"op": "dark"` (default)
     raises the channel, `"light"` lowers it, and `"pale"` lays a white marking: it raises the
     feathering (underside) channel and clears the dark one, so the marking shows over a saddle.
     Leave it out and the masks are exactly as before. The German Shepherd's black-and-tan saddle
     and mask, and the Beagle's tricolour (saddle, blaze, white muzzle, legs and tail tip), are the
     examples (`kit/tests/test_fur_patches.py`).
   - **Optional keys for erect ears and low tails:** `EARS["side"]` turns the ear's width to run
     across the head (default: front-to-back, a hanging ear); `EARS["swing"]` scales the clips' ear
     rotations per world axis, so an erect ear lays back where a hanging ear swings back;
     `SIT_DOWN["tail_lift"]` (default 18°) and `SIT_DOWN["tail_lag"]` (default 6 frames) keep a
     low-carried tail clear of the ground during SitDown. Leave them out and the ears and SitDown
     are exactly as before. The German Shepherd is the example (`kit/tests/test_erect_ears.py`,
     `kit/tests/test_sit_down_tail.py`).
3. **Solve the sit:** run `build_breed.py <breed> solve-sit` and paste the printed `SIT` into
   `breed.py`. Re-run it whenever the shape or the rig changes.
4. **Build:** run `build_breed.py <breed>`. Both steps must print `ALL OK`. If a check fails,
   change the breed's data, not the kit.
5. **Render the review sheets** one shot per command into `review/` and `motion/review/`, for
   Derek's review. Strip any C2PA metadata from images copied in (see the Golden's README).

## The checks every breed must pass

`model` (`kit/export_dog.py`) reads every file back: relative texture URIs that resolve into the
breed's `textures/`; `DH_Dog_*` materials (at most 5, fur with colour + normal maps, nothing
double-sided); one skin with the 32 expected joints and parents; body and ears skinned with at most
4 weights summing to 1; leg, head and tail weights only where they belong (`SANITY`); accessories
parented to their bones; no animations; the file's own skinned default pose on the ground and
matching Blender's within 2 mm; **triangles within `BUDGET`**; the **sit-contact** invariant (rump,
hocks, hind feet, front paws and tail within 1 cm of the ground, nothing more than 1 cm below) on
the full model and the LOD; the viewer `.glb` under 3 MB.

`motion` (`kit/export_anim.py`) checks **every frame of every clip on the skinned mesh**: nothing
more than 1 cm below the ground, planted paws within 1 cm of it and sliding less than 1 cm against
it; loops ending on their first frame; SitDown ending on the breed's `SIT` and PlayBow starting
and ending standing; no IK target out of reach; and the **glTF read-back** (one skin, 32 joints,
every clip with every frame sampled, the file's skinned mesh matching Blender's within 2 mm).

## The kit

| Script | What it does |
|---|---|
| `build_breed.py` | The one command per breed (above) |
| `dh_breed.py` | Loads a breed's `breed.py`, checks it is plain data, and makes it the active breed |
| `dh_dog_shape.py` | The shape library: lofted volumes from the breed's profiles, and the blend recipe |
| `dh_dog_mesh.py` | Signed distance fields, smooth minimum, surface nets, voxel clean-up, QuadriFlow |
| `dh_dog_skin.py` | Field → retopology → mouth cut → unwrap; the LOD decimation |
| `dh_dog_rig.py` | Armature, skin weights, test poses, the sit pose and the ground-contact measure |
| `dh_dog.py` | The builder: skin, accessories, materials, rig binding, fur bake hook |
| `dh_dog_tex.py` | Textures: 3D fur bake (mask, normal, coat colours) and the trim palette |
| `export_dog.py` | Bakes the textures, writes the exports, reads every file back, writes the stats and skeleton |
| `export_viewer.py` | glTF reader used by the checks; on its own, writes web-viewer data (git-ignored) |
| `solve_sit.py` | Re-derives a breed's `SIT` from the skinned mesh's ground contact |
| `render_dog.py`, `dh_dog_render.py` | The review sheets (EEVEE) and their shared helpers |
| `dh_dog_anim.py` | Pose helpers, planar leg IK and the action baker |
| `dh_dog_gait.py` | The in-place walk / run generator |
| `dh_dog_clips.py` | The clips, one `make_<clip>` each (gait data from the breed) |
| `dh_dog_check.py` | The per-frame ground-contact check on the skinned mesh |
| `export_anim.py` | Bakes every clip, checks it, exports and reads the glTF back |
| `render_motion.py` | The motion review videos and contact sheets (EEVEE) |

`kit/tests/test_breed_generality.py` (about 5 minutes) proves the kit is general: it writes a
throwaway breed into a temporary folder — the Golden's data with legs 0.8× as long, ears 1.3× as
big, two different coats and strides in proportion to the shorter legs — solves its sit, builds it
and requires `ALL OK` from both steps. (With the Golden's own stride on the shorter legs the
`motion` step fails its IK-reach check: gaits are breed data and must suit the proportions.)
Nothing it writes stays in the repository.

```
python3 art-source/blender/dogs/kit/tests/test_breed_generality.py
```

`kit/tests/test_rebuild_determinism.py` (about 6 minutes) guards the determinism invariant. It runs
QuadriFlow 12 times on the same body mesh in one process and requires one result, then copies the
Golden's `breed.py` into a temporary folder, builds `model` + `motion` there `DH_REBUILDS` times
(default 2), each in fresh processes, and requires every export, texture and report to match the
Golden's committed bytes. `DH_REBUILDS=20` is the long proof.

```
python3 art-source/blender/dogs/kit/tests/test_rebuild_determinism.py
DH_REBUILDS=20 python3 -m unittest art-source/blender/dogs/kit/tests/test_rebuild_determinism.py -k Golden
```

`kit/tests/test_fur_strand.py` (a few seconds, no Blender) checks the optional `FUR_STRAND` field:
a breed without it gets the old strand constants (so the Golden's textures cannot change), a breed
with it gets its own values in the strand noise and the strand light/dark, and a partial
`FUR_STRAND` keeps the other defaults.

```
python3 art-source/blender/dogs/kit/tests/test_fur_strand.py
```

`kit/tests/test_fur_patches.py` (a few seconds, no Blender) checks the optional `FUR_PATCHES`
field the same way: a breed without it gets the old masks (passing the coat noise changes
nothing), a dark patch raises only the dark channel and only where it lies, a light patch
lowers it, and a pale patch raises the feathering channel to its strength and clears the dark
channel, only where it lies.

`kit/tests/test_erect_ears.py` and `kit/tests/test_sit_down_tail.py` (a few seconds each, need
`bpy` but build no dog: they loft the ears and pose a bare armature) check the optional erect-ear
and low-tail keys. Without `EARS["side"]` the ears are the old hanging loft; with it the ear's width
runs across the head and the right ear is the left's mirror image with each texel on the same face
(front or back) of both ears. `EARS["swing"]` scales the ear rotations of `Rig.set` and
`apply_pose` per axis and leaves every other bone alone. Without `SIT_DOWN["tail_lift"]` /
`["tail_lag"]` SitDown's tail lifts 18° and trails the hips by 6 frames as before; with them it
lifts and trails by the breed's values and still finishes sat on the same frame.

```
python3 art-source/blender/dogs/kit/tests/test_fur_patches.py
python3 art-source/blender/dogs/kit/tests/test_erect_ears.py
python3 art-source/blender/dogs/kit/tests/test_sit_down_tail.py
```

### Clip invariants

> **Invariant — the wag lives in the tail (#895).** During TailWag the torso stays essentially
> still: only the tail chain swings side to side. The hips and spine may follow with at most a
> small fraction of the tail's motion (each torso bone under 2° peak to peak, and under 5% of
> `tail_1`'s swing), and the body does not bounce (the root's height varies by under 1 mm).
>
> **Invariant — a wag is not a pant (#895).** During TailWag the face and breathing are as
> gentle as standing Idle's: the jaw opens no wider and swings no more than in Idle (within 0.5°),
> and no breathing or face bone (jaw, neck, head, chest, ears) moves faster per frame than in Idle
> (within 0.1° per frame).

`kit/tests/test_tail_wag.py` (about 40 s, needs `bpy`) bakes TailWag and Idle on the Golden's rig
and enforces both invariants. It also checks that the tail still swings at least as hard as
before (`tail_1` over 35° peak to peak, the tip sweeping over 0.5 m):

```
python3 art-source/blender/dogs/kit/tests/test_tail_wag.py
```
