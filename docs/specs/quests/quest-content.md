# Quest Content

*Epic: [#5](https://github.com/derekwinters/lucas-doggiehood/issues/5)*

## v1.0 quest types

v1.0 ships with exactly 3 quest types. Expanding variety is explicitly deferred — see [#60](https://github.com/derekwinters/lucas-doggiehood/issues/60).

!!! note "Cost tiers are not new quest types"
    The population-gated [cost tiers](economy.md#cost-tiers-population-gated-317) ([#317](https://github.com/derekwinters/lucas-doggiehood/issues/317)) add **higher-cost bands within these same 3 quest types** as the neighborhood grows — bigger-ticket BuyGift and decoration-request subjects, nothing more. They are **not** new quest mechanics, so this frozen-types rule is unaffected: no new resolution mechanic, screen, or interaction is introduced, only a wider price range on the existing purchasable subjects.

### 1. Lost something

*[#12](https://github.com/derekwinters/lucas-doggiehood/issues/12)*

A dog's conversation reveals it has lost something — a toy, or its own puppy — and needs the player's help finding it. Resolved via the hidden-object search mechanic: the item is placed somewhere visible in the main neighborhood scene (behind a bush, on a roof, etc.); the player pans/zooms around and taps it when spotted. No separate hidden-object scene, hint system, or radar. ([#31](https://github.com/derekwinters/lucas-doggiehood/issues/31))

!!! note "Eligibility — a puppy can't lose its own puppy ([#463](https://github.com/derekwinters/lucas-doggiehood/issues/463))"
    The lost-**puppy** subject is excluded when the receiving dog is itself a puppy (`Dog.IsPuppy`), so a puppy is never handed a quest about losing its own puppy. Only the puppy subject is filtered out for puppy receivers — the rest of the Lost pool (toy, ball) remains, so the pool never empties. Non-puppy dogs are unaffected and can still lose a puppy. The excluded name keys off the shared `ItemCatalog.PuppyItemName` catalog constant, not a duplicated literal (#161).

The hidden item's position is generated in Core within the **quest dog's own home tile** — sampled inside that lot's quadrant bounds (`LotBounds.QuadrantBounds`, keyed off `dog.HouseId` → its lot) rather than a fixed origin-centered square — so as the map grows the item is always findable near that dog's house rather than drifting off somewhere in the neighborhood ([#520](https://github.com/derekwinters/lucas-doggiehood/issues/520)). Within that tile the item is kept clear of the dog's house footprint: candidates within a named house-clearance buffer (`QuestManager.HouseClearanceBuffer`) of that lot's house are rejected and re-rolled, so the item always sits in open, tappable ground rather than behind the house where the geometry occludes the tap. (Only that lot's own footprint needs checking — quadrant bounds tile the map with no overlap, so no neighbouring house can intrude on the sampled region.) The buffer clears the full lost-item tap radius with margin, and placement stays deterministic per quest/seed. This mirrors the collision-aware footprint check yard landscaping already uses (see [World](../world/world.md) § Yard landscaping). ([#290](https://github.com/derekwinters/lucas-doggiehood/issues/290))

Tapping is forgiving on two levels, mirroring how the dog speech bubble already handles imprecise touch input: the game-logic tolerance (`QuestManager.LostItemTapRadius`) accepts any tap within 1.5m of the hidden item, and the item also offers a padded screen-space tap zone so that tolerance genuinely applies to real taps — a mouse cursor is pixel-precise, a finger touch is not, and the item's small on-screen footprint under the fixed camera pitch would otherwise leave a raw hit-test with no forgiveness at all. ([#311](https://github.com/derekwinters/lucas-doggiehood/issues/311))

**How the item renders.** A lost **puppy** reuses the shared Kenney Cube Pets dog model — the same asset every roster dog renders (see [Art & UI Style](../world/art-style.md)) — rather than a bespoke model or the graybox sphere: it is tinted a puppy coat, scaled slightly smaller than a puppy dog (`LostItemView.PuppyModelScale`, below the 0.55 puppy-dog scale), given a fitted tap collider (the imported FBX ships none), and left in place with a slow in-place look-around yaw so it reads as a real puppy at its small on-screen size. Every other subject (e.g. toy, ball — no reusable model yet) still renders the graybox sphere placeholder. ([#335](https://github.com/derekwinters/lucas-doggiehood/issues/335))

**Finder glow (making the item pop).** The hidden item can visually vanish into its surface — a white ball on the white sidewalk (`SidewalkHex #EFE8D8`), or a tennis-ball-style item in the grass (`GrassHex #7ED957`) — so, regardless of which model renders, the item carries a world-space **finder glow** so it's easy to spot once you're near (Derek, 2026-08-02: *"Option 1 — a world-space glow on the item itself, colored RED"*). The glow is a soft **pulsing red halo** on the item, a flat **ground contact ring**, and a subtle orbiting **sparkle** — it's the glow the eye catches, not the item's own colour, and the red reads on the sidewalk, grass and road alike. The colour is the named `Palette.LostItemGlowHex` (a bright, saturated red) and every size/pulse/ring/sparkle value is a named constant on `LostItemGlow` (`Doggiehood.Core.Quests`) (#161). The glow is **pure decoration**: it is active only while a lost-item quest's hidden item is placed (`LostItemGlow.ShouldShow`), it is a child of the item view so it is torn down with the item on collect/dismiss, and it carries no collider and is non-interactable — so it never intercepts a tap and the forgiving tap-to-collect zone ([#311](https://github.com/derekwinters/lucas-doggiehood/issues/311)) stays sized to the item itself. Any onboarding quest that reuses the same lost-item view inherits the glow for free. This is a graybox first pass built from Unity primitives; the final low-poly glow VFX is a later polish. ([#521](https://github.com/derekwinters/lucas-doggiehood/issues/521))

### 2. Buy something

*[#13](https://github.com/derekwinters/lucas-doggiehood/issues/13)*

A dog asks the player to buy it something — a toy, or a pool for its house. Resolved via the delivery-truck mechanic: accepting the quest deducts currency, a delivery truck animates in and drops the package at the dog's front door, and the dog receives it automatically — no manual placement by the player. The dog itself walks home and sits waiting for the truck (see [Dog Behavior](../dogs/behavior.md)). ([#30](https://github.com/derekwinters/lucas-doggiehood/issues/30))

Once the package is delivered, the dog is handed back to free-roam wander and picks a **fresh** wander target from where it now stands (its own front door) — it does not resume the target it had cached before the walk home, which would send it beelining off the walk network back to its old position ([#470](https://github.com/derekwinters/lucas-doggiehood/issues/470)).

**Exception — the fence** ([#318](https://github.com/derekwinters/lucas-doggiehood/issues/318)): a fence is a lot property rather than a delivered package, so its "Buy something" quest has **no delivery leg**. Accepting deducts the cost (100 coins) and completes immediately — no delivery truck, no walk-home — and the [backyard fence](../world/world.md#backyard-fences) becomes visible right away with no animation. It is still the same quest type (a `Gift`-tagged catalog purchase), just installed in place instead of delivered; see [Quest & Economy § Item catalog](economy.md#item-catalog).

### 3. Bug problem

*[#53](https://github.com/derekwinters/lucas-doggiehood/issues/53)*

A dog's house has a bug problem; the player helps by spraying to clear it out. While the quest is active, a bug swarm hovers over the affected house so the player can tell which one needs attention; tapping that house sprays it, which clears the swarm and completes the quest. There's no separate spray tool or aiming — the house itself is the tap target. ([#157](https://github.com/derekwinters/lucas-doggiehood/issues/157))

The indicator must be **clearly visible, not merely present**: a first-time player must be able to tell which house needs spraying without guessing. It is a world-space marker (no HUD overlay), positioned above the house's **actual** roofline — measured from the house mesh, not a blind fixed height — and made deliberately readable under the fixed 45° orthographic camera: a bright, tall beacon topped by a chunky pest swarm, rising far enough to poke into open sky so a tall roof can't occlude it. It stays purely feedback (no collider — the house underneath remains the tap target) and clears the instant the house is sprayed. The current form is graybox; the final low-poly pest art swaps in via [#334](https://github.com/derekwinters/lucas-doggiehood/issues/334). ([#331](https://github.com/derekwinters/lucas-doggiehood/issues/331))

## Not a quest type: decoration requests

Decoration requests (a dog wants something comfy for its yard) are handled by the [Decorations](../decorations.md) system — mechanically similar to "buy something" but with a generic prompt and a small set of player-chosen options rather than one named item.

## Authoring

All 3 types are implemented as templates, not one-off hand-written text — see [Quest & Economy § Quest authoring](economy.md#quest-authoring).

Item/subject names for all 3 types (and for decoration requests) are drawn from a single tagged item catalog, not per-type hand-written lists — see [Quest & Economy § Item catalog](economy.md#item-catalog). Find-only subjects (e.g. a lost puppy) carry no price, since they're found rather than bought.

**Active-quest reminder copy ([#472](https://github.com/derekwinters/lucas-doggiehood/issues/472)).** Alongside the opener and closer, each quest type's template carries a third pooled line set — the **reminder** — shown when a dog with an *already-accepted* quest is re-tapped (e.g. *"Any sign of my {item} yet?"* for a lost item, *"Any luck getting me that {item}?"* for a buy-gift, a bugs-still-here nudge for pest control, a *"still thinking about that {item}?"* for a decoration request). It uses the exact same authoring model as the opener/closer — a personality-agnostic default pool plus optional per-personality flavor, `{dog}`/`{item}` slots filled from the same catalog, uniform-random per string via the injectable RNG — just a single reminder line rather than a two-line offer. The current lines are first-draft placeholders; the real writing pass is [#100](https://github.com/derekwinters/lucas-doggiehood/issues/100).

## Build checklist

- [ ] "Lost something" quest: item placed in-scene, resolved by pan/zoom + tap, no separate search screen
- [ ] "Lost something" quest: the placed item carries a world-space red **finder glow** (pulsing halo + ground contact ring + sparkle, `Palette.LostItemGlowHex`) so it pops on any surface — active only while the item is placed, non-interactive, torn down with the item ([#521](https://github.com/derekwinters/lucas-doggiehood/issues/521))
- [ ] "Buy something" quest: accept → currency deducted → delivery truck spawns → dog walks home and sits → truck delivers to the door → dog "receives" it (decoration/item appears)
- [ ] "Bug problem" quest: spray interaction clears the bug state and completes the quest
- [ ] "Bug problem" quest: the affected house carries a **clearly visible** world-space indicator (readable under the 45° camera, clears the roofline, feedback-only) that clears the moment the house is sprayed ([#331](https://github.com/derekwinters/lucas-doggiehood/issues/331))
- [ ] All 3 types are driven by the shared quest template system, not hard-coded per instance
- [ ] Each quest type correctly triggers the flat 10-coin payout on completion (see [Quest & Economy](economy.md))
