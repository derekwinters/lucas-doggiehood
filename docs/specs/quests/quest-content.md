# Quest Content

*Epic: [#5](https://github.com/derekwinters/lucas-doggiehood/issues/5)*

## MVP quest types

MVP ships with exactly 3 quest types. Expanding variety is explicitly deferred — see [#60](https://github.com/derekwinters/lucas-doggiehood/issues/60).

### 1. Lost something

*[#12](https://github.com/derekwinters/lucas-doggiehood/issues/12)*

A dog's conversation reveals it has lost something — a toy, or its own puppy — and needs the player's help finding it. Resolved via the hidden-object search mechanic: the item is placed somewhere visible in the main neighborhood scene (behind a bush, on a roof, etc.); the player pans/zooms around and taps it when spotted. No separate hidden-object scene, hint system, or radar. ([#31](https://github.com/derekwinters/lucas-doggiehood/issues/31))

### 2. Buy something

*[#13](https://github.com/derekwinters/lucas-doggiehood/issues/13)*

A dog asks the player to buy it something — a toy, or a pool for its house. Resolved via the delivery-truck mechanic: accepting the quest deducts currency, a delivery truck animates in and drops the package at the dog's front door, and the dog receives it automatically — no manual placement by the player. The dog itself walks home and sits waiting for the truck (see [Dog Behavior](../dogs/behavior.md)). ([#30](https://github.com/derekwinters/lucas-doggiehood/issues/30))

### 3. Bug problem

*[#53](https://github.com/derekwinters/lucas-doggiehood/issues/53)*

A dog's house has a bug problem; the player helps by spraying to clear it out. While the quest is active, a bug swarm hovers over the affected house so the player can tell which one needs attention; tapping that house sprays it, which clears the swarm and completes the quest. There's no separate spray tool or aiming — the house itself is the tap target. ([#157](https://github.com/derekwinters/lucas-doggiehood/issues/157))

## Not a quest type: decoration requests

Decoration requests (a dog wants something comfy for its yard) are handled by the [Decorations](../decorations.md) system — mechanically similar to "buy something" but with a generic prompt and a small set of player-chosen options rather than one named item.

## Authoring

All 3 types are implemented as templates, not one-off hand-written text — see [Quest & Economy § Quest authoring](economy.md#quest-authoring).

## Build checklist

- [ ] "Lost something" quest: item placed in-scene, resolved by pan/zoom + tap, no separate search screen
- [ ] "Buy something" quest: accept → currency deducted → delivery truck spawns → dog walks home and sits → truck delivers to the door → dog "receives" it (decoration/item appears)
- [ ] "Bug problem" quest: spray interaction clears the bug state and completes the quest
- [ ] All 3 types are driven by the shared quest template system, not hard-coded per instance
- [ ] Each quest type correctly triggers the flat 10-coin payout on completion (see [Quest & Economy](economy.md))
