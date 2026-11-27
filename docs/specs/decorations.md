# Decorations

*Epic: [#45](https://github.com/derekwinters/lucas-doggiehood/issues/45)*

## Scope

Decorations apply to a specific dog's own yard/house only. No decorating shared public spaces (streets, the intersection) for v1.0. ([#46](https://github.com/derekwinters/lucas-doggiehood/issues/46))

## Effect

Decorations are cosmetic but also raise a dog's happiness. Happiness affects animations (more wagging), idle behavior, and dialogue tone/mood **only** — it never gates or unlocks quests, dialogue, or rewards. This keeps the no-fail, low-pressure design intact. ([#47](https://github.com/derekwinters/lucas-doggiehood/issues/47))

## Placement

Decorations are placed automatically in a sensible spot in the yard — no drag-and-drop or arranging UI for v1.0. ([#48](https://github.com/derekwinters/lucas-doggiehood/issues/48))

## Capacity (= house level)

A yard holds at most as many decorations as its [house level](expansion.md#house-leveling) — **1 at level 1, up to 4 at level 4** ([#59](https://github.com/derekwinters/lucas-doggiehood/issues/59)). A placement past the current capacity is rejected. Upgrading the house raises the cap.

**Grandfathering (Derek, 2026-07-14):** the MVP decoration flow auto-placed with no cap, so a yard can already hold more decorations than its level allows when the cap ships. Nothing already placed is ever removed — the cap blocks *new* placements only. In Core this is `GameState.TryAddDecoration` (the capacity-respecting path); the raw `AddDecoration` used by save-load stays uncapped, which is what preserves the grandfathered items.

## Acquisition

There is **no standalone decoration shop**. Decorations are requested by dogs the same way other gifts are, as part of the normal quest rotation, fulfilled via the delivery truck (see [Quest Content](quests/quest-content.md)). ([#49](https://github.com/derekwinters/lucas-doggiehood/issues/49))

A decoration request is **generic** (e.g. "my yard could use something comfy") rather than naming one specific item — the player picks which item to send from a small set of options. This differs from the toy/pool quest, which asks for one specific named thing. ([#50](https://github.com/derekwinters/lucas-doggiehood/issues/50))

## v1.0 category: comfort items

Starting scope is **comfort items** — dog beds, cushions, blankets. ([#51](https://github.com/derekwinters/lucas-doggiehood/issues/51)) Other categories considered for later: play items (pool, sprinkler), yard dressing (flowers, fence), food/bowls.

Once delivered, the dog periodically decides — on its own, no player tap/trigger, consistent with dogs already behaving autonomously — to walk over to the comfort item and settle onto it. The dog actually paths there over the [walk network](world/sidewalks.md) (sidewalks, crosswalks, its house's front walkway) from wherever it is, then enters the "Rest" pose **on arrival** — it never teleports into the pose. ([#52](https://github.com/derekwinters/lucas-doggiehood/issues/52), [#112](https://github.com/derekwinters/lucas-doggiehood/issues/112)) See the "Rest" pose in [Dog Behavior](dogs/behavior.md#animations).

**The blanket model** ([#413](https://github.com/derekwinters/lucas-doggiehood/issues/413)). The blanket is a soft wool plaid throw built as a Blender source asset in `art-source/blender/items/blanket/`, outside `Assets/`. It lies gently rumpled on the grass, with a short fringe on its short ends and a cream fleece underside. The cloth is **2.0 × 1.4 m**, big enough for an adult Golden Retriever lying flat. With the fringe it covers a **2.12 × 1.40 m** footprint and stands at most 4 cm tall. It has 2,080 triangles, and its **pivot is on the ground** at the centre of its footprint. That footprint is larger than today's 1.4 × 1.0 m graybox slab. So when [#932](https://github.com/derekwinters/lucas-doggiehood/issues/932) imports the model, it has to reserve the larger footprint, including the pool's clearance around a decoration (`PoolPlacement.DecorationFootprintRadius`, today half the slab's diagonal). Until then, the game still shows the blue graybox slab.

**Blanket colourways** ([#413](https://github.com/derekwinters/lucas-doggiehood/issues/413)). The blanket comes in four colourways: navy & blue, red & green tartan, green & cream, and grey & purple. They share one mesh, and only the plaid differs. **A gifted blanket picks one colourway at random when it is delivered** (Derek, 2026-11-27). The pick is game code ([#932](https://github.com/derekwinters/lucas-doggiehood/issues/932)), not part of the asset. How the pick is kept, so the blanket stays the same colour after a relaunch, is for #932 to settle.

> **How the spec is changing (#413).** It used to say the blanket stays inside today's 1.4 × 1.0 m graybox slab, so placement would not change → it now says the cloth is about 2.0 × 1.4 m (a 2.12 × 1.40 m footprint with the fringe), and that the game must reserve that larger footprint → because Derek asked for a bigger blanket that an adult dog can lie flat on.

**Invariant — the blanket never leaves its placement footprint** ([#413](https://github.com/derekwinters/lucas-doggiehood/issues/413)). Every vertex of every colourway's export lies inside the 2.12 × 1.40 m footprint, centred on the item's position and on or above the ground. That rectangle is the footprint the game places the blanket by. The export's read-back check and `tests/test_blanket.py` enforce this, and the size lives in one constant (`blanket_spec.py`).

**The bed model** ([#411](https://github.com/derekwinters/lucas-doggiehood/issues/411)). The dog bed is a padded bolster bed built as a Blender source asset in `art-source/blender/items/bed/`, outside `Assets/`. It has a low padded base, a soft bolster round the rim that dips at the front where a dog steps in, and a flat cream fleece cushion. Its footprint is **1.60 × 1.15 m**, with the long side along X like today's slab. It stands 0.36 m tall at the back and has 1,792 triangles. Its **pivot is on the ground** at the centre of its footprint. A dog lies on the cushion top, **0.18 m** up, at the `ATTACH_REST` point the export carries, over a flat area of at least 0.95 × 0.50 m. The footprint is larger than today's 1.4 × 1.0 m graybox slab. So when [#932](https://github.com/derekwinters/lucas-doggiehood/issues/932) imports the model, it has to reserve the larger footprint, including the pool's clearance around a decoration. Two beds in neighbouring slots (2 m apart) would sit 0.85 m apart instead of 1.0 m. Until then, the game still shows the purple graybox slab.

**Bed colours** ([#411](https://github.com/derekwinters/lucas-doggiehood/issues/411)). The bed comes in ten cover colours: Heather Plum, Denim Blue, Dove Grey, Charcoal, Rust, Cranberry, Mustard, Dusty Rose, Teal and Cocoa. They share one mesh and one set of textures. A colour is just the base colour of the cover and base materials, and the cream cushion never changes. `art-source/blender/items/bed/bed_colours.json` lists the colours and how to apply them. **A gifted bed picks one colour at random when it is delivered** (Derek, 2026-11-27). The pick is game code ([#932](https://github.com/derekwinters/lucas-doggiehood/issues/932)), not part of the asset, and #932 settles how the pick is kept across a relaunch, as for the blanket.

> **How the spec is changing (#411).** It used to say the bed stays inside today's 1.4 × 1.0 m graybox slab, so placement would not change → it now says the bed's footprint is 1.60 × 1.15 m, and that the game must reserve that larger footprint → because Derek asked for a slightly bigger bed so an adult dog fits it better.

**Invariant — the bed never leaves its placement footprint** ([#411](https://github.com/derekwinters/lucas-doggiehood/issues/411)). Every vertex of the bed's export lies inside the 1.60 × 1.15 m footprint, centred on the item's position and on the ground. That rectangle is the footprint the game places the bed by. The export's checks and `tests/test_bed_model.py` enforce this, and the size lives in one place (`check_bed.py`).

## Build checklist

- [ ] Decoration requests are scoped to a single dog's own yard; no shared-space decorating exists
- [ ] Delivering a decoration raises the dog's happiness value; happiness only affects animation/dialogue tone, never gameplay gating
- [ ] Decorations auto-place in the yard on delivery — no placement UI
- [ ] Decoration requests come from the same quest rotation as other requests, not a separate shop screen
- [ ] A decoration request presents a small set of item choices (not one named item) for the player to pick from
- [ ] At least the 3 comfort items (bed, cushion, blanket) exist as deliverable decorations
- [ ] A gifted blanket gets one of its colourways at random on delivery (#932)
- [ ] A gifted bed gets one of its ten colours at random on delivery (#932)
- [ ] A dog with a comfort decoration periodically and automatically walks over to it via the walk network and uses the "Rest" pose on it on arrival (no teleport into the pose)
