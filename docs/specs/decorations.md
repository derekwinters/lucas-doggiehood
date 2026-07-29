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

## Build checklist

- [ ] Decoration requests are scoped to a single dog's own yard; no shared-space decorating exists
- [ ] Delivering a decoration raises the dog's happiness value; happiness only affects animation/dialogue tone, never gameplay gating
- [ ] Decorations auto-place in the yard on delivery — no placement UI
- [ ] Decoration requests come from the same quest rotation as other requests, not a separate shop screen
- [ ] A decoration request presents a small set of item choices (not one named item) for the player to pick from
- [ ] At least the 3 comfort items (bed, cushion, blanket) exist as deliverable decorations
- [ ] A dog with a comfort decoration periodically and automatically walks over to it via the walk network and uses the "Rest" pose on it on arrival (no teleport into the pose)
