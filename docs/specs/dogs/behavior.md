# Dog Behavior

*Epic: [#3](https://github.com/derekwinters/lucas-doggiehood/issues/3)*

## Movement

Dogs wander/patrol the streets and are visibly roaming the neighborhood; the player can approach and interact with them. ([#8](https://github.com/derekwinters/lucas-doggiehood/issues/8)) Wander spans the **whole unlocked map**, not just the starting intersection: it is a node-to-node random walk over the sidewalk/crosswalk [walk network](../world/sidewalks.md), which derives its roads from the live multi-tile `Map`, so once a zone is unlocked dogs explore its sidewalks too — already-spawned dogs pick up the new tiles without re-spawning ([#398](https://github.com/derekwinters/lucas-doggiehood/issues/398)). **Wander now depends on the dog's own house ([#430](https://github.com/derekwinters/lucas-doggiehood/issues/430)):** `WanderBehavior` carries the dog's `HouseId`, and a house's front walkway is a wander candidate *only* for that house's own resident dog — so a dog may step onto its own walkway, while every other dog keeps excluding all front walkways and never detours onto a neighbor's lot. (Dogs actually entering the house or reaching the backyard remains out of scope, a possible later follow-up.)

Some dogs are placed inside houses rather than out on the street, visible looking out through a window, rather than roaming. ([#9](https://github.com/derekwinters/lucas-doggiehood/issues/9))

### Movement conveys personality

Rather than giving every personality its own animation set, mood is conveyed through **how a dog walks** ([#89](https://github.com/derekwinters/lucas-doggiehood/issues/89)):

- **Excited**: fast walking speed, long straight stretches down a street before turning — covers real distance rather than pacing back and forth.
- **Grumpy/sad**: slow walking speed, turns almost every step — shuffles around a small area. *(Deferred past v1.0 — see below.)*

Other personalities (Brave, Shy, Adventurous, Athletic) can get their own speed/turn-pattern combinations later using the same system.

**v1.0 scope**: only the general speed/turn-pattern system needs to exist. **Excited** is the one pattern to actually implement for the first complete release. Grumpy's distinct movement pattern is deferred — Grumpy remains a valid personality for dialogue/flavor (Pepper is Grumpy, see [Dog Roster & Names](roster-names.md)), it just won't have distinct movement behavior yet.

### Quest-related movement

When a dog's "buy me X" request is accepted, the dog walks home at a medium speed — routing over the [walk network](../world/sidewalks.md) if it isn't already there ([#106](https://github.com/derekwinters/lucas-doggiehood/issues/106)) — then sits and waits (see [animation](#animations)) until the delivery truck arrives. See [Quest Content](../quests/quest-content.md).

During this scripted walk home the dog **turns to face the direction it is walking before each step** — the same turn-then-move feel as ordinary wander — rather than sliding home backwards. While the delivery is in flight (walking home *and* sitting waiting for the truck) the dog does **not** free-roam wander: the scripted walk owns its movement, so wander and the walk-home leg never fight over the dog's position ([#470](https://github.com/derekwinters/lucas-doggiehood/issues/470)).

### Yielding at road crossings

A vehicle and a dog never occupy the same point on a crosswalk — collisions are resolved by yielding, never by driving through ([#546](https://github.com/derekwinters/lucas-doggiehood/issues/546)). Right-of-way is **first-come**: the first occupant to arrive at a given crosswalk claims it exclusively, and whichever arrives **second waits** at its own boundary until the first has fully crossed and released it.

- A **dog** arrives when its wander step's next hop is that `Crosswalk` edge, stepping off the curb. If a vehicle already holds the crosswalk, the dog **holds at its curb node** and does not advance, re-checking each frame until the claim releases, then crosses.
- A **vehicle** arrives when its drive position reaches the near edge of that crosswalk's road span. If a dog already holds it, the vehicle **pauses at the near edge** rather than driving through, resuming once the dog clears the far edge.

This is a **generic** rule, not a truck-only hack: it is keyed per crosswalk segment and shared by every current and future vehicle, so it also generalizes for free to a vehicle that eventually turns across an intersection (each crosswalk span it drives over is one independent claim). The mechanism lives in Core (`RoadCrossingGate` — a first-come claim/release gate on each crosswalk `WalkEdge` — with `RoadCrossingTraversal` driving the vehicle side in along-road coordinates); the thin Unity views (`DogView`, `DeliveryTruckView`) only convert positions and move. The minimal pairwise vehicle↔dog check per crosswalk is the whole scope; dog-vs-dog queuing over a shared crosswalk is explicitly out of scope (dogs already don't block each other), and no vehicle turns today. See the delivery-truck note in [Quest Content](../quests/quest-content.md) and the [walk network](../world/sidewalks.md).

## Population

Across the 4 starting houses: most houses have a parent dog + puppy, some houses have just one dog, and some have 2-3 dogs. This variety sets up quests like "lost my puppy" naturally. ([#34](https://github.com/derekwinters/lucas-doggiehood/issues/34)) See [Dog Roster & Names](roster-names.md) for the actual starting cast.

New houses (v0.4 expansion) start empty and dogs move in gradually over time — see [Neighborhood Expansion](../expansion.md).

## Breeds & personality

Breed is a **data** attribute (the `Breed` enum), not a distinct mesh. Every dog renders with the same **standard shared model** — the Kenney "Cube Pets" model (`Assets/Art/Dogs/CubePets/Resources/animal-dog.fbx`, wired in `DogView`, [#123](https://github.com/derekwinters/lucas-doggiehood/issues/123)) — and breeds are distinguished visually by **coat color/tint** (see [Art & UI Style](../world/art-style.md)), not by a per-breed mesh. Breed still gives each dog a clear identity through its name, coat, and personality. ([#35](https://github.com/derekwinters/lucas-doggiehood/issues/35), [#166](https://github.com/derekwinters/lucas-doggiehood/issues/166)) *(Decision 2026-07-16, Derek: Cube Pets is the single standard dog model, superseding the earlier "distinct model per breed" direction.)*

Every dog has a defined personality trait that colors its dialogue tone and the kinds of quests it gives. ([#36](https://github.com/derekwinters/lucas-doggiehood/issues/36))

**Personality types**: Brave, Adventurous, Shy, Excited, Grumpy, Athletic.

## Scope

Dogs are the only interactable characters for v1.0 — no cats, mail carriers, squirrels, or other animals/people. Scope is limited to dogs and their houses; other characters can be revisited in future expansions. ([#37](https://github.com/derekwinters/lucas-doggiehood/issues/37))

## Animations

Full animation/pose list — see [#66](https://github.com/derekwinters/lucas-doggiehood/issues/66) for the source issue:

| Pose | When it's used |
|---|---|
| Idle/wander | Base walking state; speed/turn-pattern varies by personality (see above) |
| Rest | Lying down — used when a dog uses a comfort decoration ([Decorations](../decorations.md)) |
| Sit | Waiting — used when a dog has walked home after accepting a quest and is waiting for the delivery truck |
| Window-watching | Dogs placed inside houses, looking out a window |

No separate animation is needed for: conversation start (the speech bubble appearing is the only signal — see [Conversation System](../quests/conversation-system.md)), happiness reactions beyond rest/idle, or the delivery truck's arrival (the truck is the actor, not the dog).

## Build checklist

- [ ] Dogs wander streets at a base walking speed and can be tapped/interacted with
- [ ] Some dogs are placed inside houses in a static window-watching pose instead of wandering
- [ ] General speed/turn-pattern movement system exists, with the **Excited** pattern implemented (fast + long straight stretches)
- [ ] Grumpy personality exists for dialogue/flavor purposes but has no distinct movement pattern yet
- [ ] Each dog has a breed (a `Breed`-enum data value, distinguished visually by coat/tint on the shared standard Cube Pets model — not a distinct per-breed mesh) and a personality trait
- [ ] Only dogs are interactable in the world — no other animal/person NPCs
- [ ] Rest, sit, idle/wander, and window-watching poses are all implemented
- [ ] Dog walks home and sits after accepting a "buy me X" quest, until the delivery truck arrives
- [ ] A vehicle and a dog never occupy the same point on a crosswalk — first-come right-of-way: the second to arrive yields (a dog waits at its curb, a vehicle pauses at the crosswalk's near edge) until the first crosses and releases it (#546)
