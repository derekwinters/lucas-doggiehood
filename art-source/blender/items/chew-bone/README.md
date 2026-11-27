# Chew bone (#923): approved model

The chew bone is one of the three toys a dog can ask you to buy (with the toy and the ball). It is
a `Gift` item in `ItemCatalog` (35 coins). This folder holds its Blender source: a generator
script, the export, the review renders and the tests. It is **source art only**. Nothing here is
imported by Unity (the folder is outside `Assets/`). Showing it in the game is #929.

**Status: approved.** Derek approved the 300-triangle renders on 2026-11-27 (#923).

## Size and triangles

| Measure | Size |
|---|---|
| Length, knob tip to knob tip | 0.291 m |
| Width across a knobbed end | 0.097 m |
| Height lying flat (one knob) | 0.055 m |
| Shaft at its waist / where it meets the knobs | 0.033 m / 0.045 m |
| Triangles | **300**. Its own cap is 400 (Derek on #923: a small object never seen up close); the per-mesh backstop from #799 is 6,000 |
| Materials | 1 (`DH_Item_ChewBone`), one 512² colour + normal pair |

**How the size was chosen.** The game's dogs are bigger than real dogs: the adult Golden's top of
back is 1.034 m in the game against about 0.60 m on a real Golden, so the dog scale is 1.72. The
bone is a real large chew bone for a big dog (17 cm long, 5.8 cm across the ends) times 1.72, so
it is about a fifth of the adult Golden's 1.49 m nose-to-rump length, as a real one would be. A
test keeps the bone between 15% and 25% of the Golden's in-game nose-to-rump.

## Files

```
chew-bone/
  dh_chew_bone.py      DIMS, the field, build(), make_textures()
  export_chew_bone.py  textures + exports/chew_bone.gltf/.bin + chew_bone_stats.json, read-back checks
  render_chew_bone.py  the review renders, one shot per command, to out/ (git-ignored)
  tests/               test_chew_bone.py: shape, size beside the dog, budget, export, rebuilds
  textures/            chew_bone_color.png, chew_bone_normal.png
  exports/             chew_bone.gltf, chew_bone.bin, chew_bone_stats.json
  review/              chew-bone-gamecam.png, chew-bone-closeup.png, chew-bone-turnaround.png
```

It imports the house kit (`../..`), the dog kit's mesher (`../../dogs/kit/dh_dog_mesh.py`), the
tree noise (`../../environment/trees`) and, for the renders, the dog render helpers and the lawn
(`../../environment/ground`), all by relative path, and copies nothing.

## How it is built

One signed distance field: a waisted shaft and four slightly stretched round knobs, two side by
side at each end, merged with a smooth minimum (a 12 mm fillet at every join, never an
intersection line). The field is meshed with surface nets on a 2 mm grid, Taubin-smoothed,
decimated to 300 triangles, and every vertex is projected back onto the exact field.
The bone lies flat: the knobs touch the ground and the shaft rides just above it.

- **Pivot:** on the ground at the footprint centre. The long axis is X in Blender (the glTF file
  is Y-up, as for every set).
- **UVs:** box projection at 0.16 m per texture repeat, with the first UV axis along the bone, so
  the fibres follow its length.
- **Texture:** pressed nylon/rawhide in a warm bone white (`#E8DCC4`) with long darker fibres,
  faint mottling and small pits, plus a matching normal map. Roughness 0.62.

## Regenerating

```
pip install bpy==5.3.0 Pillow numpy
python3 art-source/blender/items/chew-bone/export_chew_bone.py
python3 art-source/blender/items/chew-bone/render_chew_bone.py gamecam      # also: closeup, turnaround
python3 art-source/blender/items/chew-bone/tests/test_chew_bone.py          # under a minute
```

The export rewrites the committed textures and exports byte for byte (checked with two runs on
`bpy` 5.3.0, and by `test_every_rebuild_matches_the_committed_files`, which rebuilds twice in
fresh processes into temporary folders). The renders need EEVEE; without a GPU the render script
uses Mesa's lavapipe when it is installed at `/opt/lavapipe`. Renders are not byte-reproducible.

## Review renders

| File | What |
|---|---|
| `review/chew-bone-gamecam.png` | The game camera (orthographic, 45° pitch, yaw −40) on the approved lawn: the bone beside the approved adult Golden, base 1 behind, then the same camera zoomed in |
| `review/chew-bone-closeup.png` | A perspective close-up on the lawn |
| `review/chew-bone-turnaround.png` | Side, end and top, orthographic, with a 10 cm scale bar |

## Things I was unsure about / concerns

- **Size.** Scaling a real bone by the dog's in-game scale (1.72) is my choice; nothing in the
  specs sizes items. At the full game-camera zoom the bone is only a few pixels long. If it should
  read better from there, a bigger, more toy-like bone is a change to `REAL` in
  `dh_chew_bone.py`, and the size test's range would move with it.
- **Facets at 300 triangles.** The knobs are visibly faceted in the close-up and the end view;
  at the game camera the outline still reads as a bone with two knobs per end.
- **Texture seams.** The box projection changes direction across each knob, which shows as a
  change in the fibre direction in the end view. It is not visible at game distance.
- **Colour.** Bone white is the classic look. A coloured nylon bone (for example red or blue) would
  stand out more on the lawn; it is only a change of `BASE` and `FIBRE_DARK`.
