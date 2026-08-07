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
- A **vehicle** arrives when its **front** reaches the near edge of that crosswalk's road span. If a dog already holds it, the vehicle **stops short of the near edge** rather than driving through — far enough back that its **whole body**, not just its centre, stays clear of the crosswalk band — resuming once the dog clears the far edge.

    A vehicle is measured at its **leading edge, not its pivot** ([#639](https://github.com/derekwinters/lucas-doggiehood/issues/639)). A vehicle's position is the **centre** of its body, so stopping *that* at the near edge left the front half of a delivery truck overhanging the stripes and clipping the dogs crossing them. The stop boundary is therefore set back by the vehicle's own **pivot-to-front-bumper distance (half its body length) plus a small stop gap**, so it comes to rest with visible daylight between its bumper and the stripes. The setback is the **vehicle's** property, not the crossing's: `RoadCrossingTraversal` takes it as a parameter defaulting to **0**, so the rule stays generic over occupants — a point occupant (a dog) still stops exactly at the near edge, while `DeliveryTruckView` supplies a value derived from the truck's **measured body length** (its kit model's bounds at `ModelScale`, or the graybox footprint). This only changes where a vehicle stops when it is **blocked**; a vehicle facing a clear crosswalk still claims it and drives all the way through.

    **A vehicle must fit between an intersection's two crosswalk bands** ([#660](https://github.com/derekwinters/lucas-doggiehood/issues/660)). Because the setbacks above are measured from the vehicle's ends, they consume real road, and both have to fit in the clear roadway between the two bands an intersection straddles itself with:

    ```
    frontSetback + rearSetback  <  crosswalkSpacing − CrosswalkWidth  =  9.5 − 3  =  6.5 m
    ```

    Substituting `fs = bodyLength/2 + stopGap` and `rs = bodyLength/2` reduces this to a plain bound on the vehicle itself: **`bodyLength < 6.0 m`**. A vehicle longer than that necessarily holds **both** of an intersection's crosswalks at once, and once two claims can be held simultaneously, two **oncoming** vehicles acquire them in opposite order and wedge permanently — a lock-ordering cycle that also freezes any dog waiting to cross, unrecoverable without a restart. This was proven empirically on [#658](https://github.com/derekwinters/lucas-doggiehood/issues/658): at the delivery truck's original `ModelScale = 3` the body was 9.75 m and the setbacks summed to 10.25 m against the 6.5 m gap, so adding the rear setback deadlocked the game and had to be reverted.

    The truck's scale is therefore **bounded by this constraint, not chosen freely for looks**: `ModelScale` was lowered to **1.5** (body 4.875 m, setbacks 2.9375 + 2.4375 = 5.375 m, **1.125 m of margin**), which was preferred over changing the [#546](https://github.com/derekwinters/lucas-doggiehood/issues/546) right-of-way model because it needs no new rule — if the vehicle fits, the front and rear setbacks simply compose. The rule lives in Core as `DeliveryTruckFootprint` (the body length, both setback derivations, and the budget, all derived from the locked `WorldDimensions` road constants) and is **pinned by Core tests**, so a later scale or road-layout change fails CI loudly instead of wedging the game at runtime — it is deliberately not left as a comment, which is how it was missed the first time.

This is a **generic** rule, not a truck-only hack: it is keyed per crosswalk segment and shared by every current and future vehicle. Since [#599](https://github.com/derekwinters/lucas-doggiehood/issues/599) the delivery truck routes over the **live multi-tile road network** — entering off-map, turning at intersections, and leaving by another road edge — so a vehicle that turns across an intersection is now the real case, not just an anticipated one: each leg of the route is one axis-aligned road span with its **own** `RoadCrossingTraversal`, and each crosswalk it drives over is one independent claim. The mechanism lives in Core (`RoadCrossingGate` — a first-come claim/release gate on each crosswalk `WalkEdge` — with `RoadCrossingTraversal` driving the vehicle side in along-road coordinates, per leg); the thin Unity views (`DogView`, `DeliveryTruckView`) only convert positions and move. The minimal pairwise vehicle↔dog check per crosswalk is the whole scope; dog-vs-dog queuing over a shared crosswalk is explicitly out of scope (dogs already don't block each other). See the delivery-truck note in [Quest Content](../quests/quest-content.md) and the [walk network](../world/sidewalks.md).

**Vehicle↔vehicle: car-following on shared roads** ([#600](https://github.com/derekwinters/lucas-doggiehood/issues/600)). Because off-map entry ([#599](https://github.com/derekwinters/lucas-doggiehood/issues/599)) lengthens each drive, two "buy me something" deliveries can be in flight at once, so the driving system owns **multiple** active trucks that must not collide with **each other** as well as with dogs. At a crosswalk this is already covered — the same first-come `RoadCrossingGate` is generic over occupants, so a second truck reaching a crosswalk a first truck holds simply **waits**, exactly as a dog would. The gate only arbitrates the *crosswalk claim*, though; it does nothing to stop a following truck driving up into a stopped leader's body on the **approach**. So along the open road a separate **1-D car-following** rule keeps trucks apart:

- **Gap** — a follower never advances closer than **one car length** behind the truck ahead of it on the same road span (Derek, 2026-08-05).
- **Start-up delay** — when a stopped leader begins to move, the follower waits **one second** before it starts moving too, modelling a driver's reaction time (Derek, 2026-08-05).
- **Single file** — one lane per road span in each direction; no passing or lane-changing (this is not a traffic sim). A truck is constrained only by the nearest truck ahead of it on its own segment driving the same way; trucks on other segments, or oncoming, don't constrain it.

The following rule **composes under** the crosswalk gate: it keeps the follower physically behind a stopped leader on the approach, and once the leader releases the crosswalk and pulls away, the follower closes the gap (after its one-second delay) and then claims the crosswalk itself. Like the crosswalk rule, the decision lives in Core (`CarFollowing` for the gap + start-up delay in along-road coordinates; `RoadTraffic` picks each follower's immediate leader), and the owning `QuestDirector` holds the **set** of active trucks and feeds each follower its leader's position every tick — the `DeliveryTruckView` only converts positions and drives.

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
- [ ] A vehicle and a dog never occupy the same point on a crosswalk — first-come right-of-way: the second to arrive yields (a dog waits at its curb, a vehicle stops short of the crosswalk's near edge) until the first crosses and releases it (#546). A vehicle yields at its **front bumper**, not its centre: its stop is set back by half its measured body length plus a stop gap, so its whole footprint stays off the band (#639)
- [x] A vehicle's **front + rear setbacks fit inside the clear gap between an intersection's two crosswalk bands** (`< crosswalkSpacing − CrosswalkWidth`, i.e. body length `< 6.0m`), so it never holds both bands at once and two oncoming vehicles cannot deadlock; the delivery truck's `ModelScale` is bounded by this, and the constraint is pinned by Core tests derived from `WorldDimensions` rather than left as a comment (#660)
