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
- A **vehicle** arrives when its **front** reaches the near edge of that crosswalk's road span. If a dog already holds it, the vehicle **stops short of the near edge** rather than driving through — far enough back that its **whole body**, not just its centre, stays clear of the crosswalk band — resuming once the dog clears the far edge. Symmetrically, it **releases the band only once its whole body is off it**: the claim is held until the vehicle's **tail**, not its centre, has passed the far edge.

    At an **intersection** a vehicle does not arrive at one band at a time — it arrives at the whole crossing at once (see *Crossing a whole intersection* below).

    A vehicle is measured at its **leading edge, not its pivot** ([#639](https://github.com/derekwinters/lucas-doggiehood/issues/639)). A vehicle's position is the **centre** of its body, so stopping *that* at the near edge left the front half of a delivery truck overhanging the stripes and clipping the dogs crossing them. The stop boundary is therefore set back by the vehicle's own **pivot-to-front-bumper distance (half its body length) plus a small stop gap**, so it comes to rest with visible daylight between its bumper and the stripes. The setback is the **vehicle's** property, not the crossing's: `RoadCrossingTraversal` takes it as a parameter defaulting to **0**, so the rule stays generic over occupants — a point occupant (a dog) still stops exactly at the near edge, while `DeliveryTruckView` supplies a value derived from the truck's **measured body length** (its kit model's bounds at `ModelScale`, or the graybox footprint). This only changes where a vehicle stops when it is **blocked**; a vehicle facing a clear crosswalk still claims it and drives all the way through.

    A vehicle is likewise measured at its **trailing edge, not its pivot, on the way OUT** ([#658](https://github.com/derekwinters/lucas-doggiehood/issues/658)). The same pivot-is-the-centre problem mirrored on the release side: handing the claim back the moment the *centre* passed the far edge let a waiting dog step onto a band the vehicle's whole **back half** was still sitting on, and it got clipped from behind. The claim is therefore held until the vehicle's **pivot-to-tail distance (half its body length)** has cleared the far edge. Like the front setback this is the **vehicle's** property — `RoadCrossingTraversal` takes a `rearSetback` next to the `frontSetback`, also defaulting to **0**, so a point occupant (a dog) still releases at exactly the far edge and the rule stays generic over occupants. There is **no stop gap on this side**: the visible daylight is wanted where a vehicle *comes to rest*, not where it drives away, so `rs = bodyLength/2` while `fs = bodyLength/2 + stopGap`.

    **A vehicle must fit between an intersection's two crosswalk bands** ([#660](https://github.com/derekwinters/lucas-doggiehood/issues/660)). Because the setbacks above are measured from the vehicle's ends, they consume real road, and both have to fit in the clear roadway between the two bands an intersection straddles itself with:

    ```
    frontSetback + rearSetback  <  crosswalkSpacing − CrosswalkWidth  =  9.5 − 3  =  6.5 m
    ```

    Substituting `fs = bodyLength/2 + stopGap` and `rs = bodyLength/2` reduces this to a plain bound on the vehicle itself: **`bodyLength < 6.0 m`**. A vehicle longer than that necessarily holds **both** of an intersection's crosswalks at once, and once two claims can be held simultaneously, two **oncoming** vehicles acquire them in opposite order and wedge permanently — a lock-ordering cycle that also freezes any dog waiting to cross, unrecoverable without a restart. This was proven empirically on [#658](https://github.com/derekwinters/lucas-doggiehood/issues/658): at the delivery truck's original `ModelScale = 3` the body was 9.75 m and the setbacks summed to 10.25 m against the 6.5 m gap, so adding the rear setback deadlocked the game and had to be reverted.

    The truck's scale is therefore **bounded by this constraint, not chosen freely for looks**: `ModelScale` was lowered to **1.5** (body 4.875 m, setbacks 2.9375 + 2.4375 = 5.375 m, **1.125 m of margin**), which was preferred over changing the [#546](https://github.com/derekwinters/lucas-doggiehood/issues/546) right-of-way model because it needs no new rule — if the vehicle fits, the front and rear setbacks simply compose. The rule lives in Core as `DeliveryTruckFootprint` (the body length, both setback derivations, and the budget, all derived from the locked `WorldDimensions` road constants) and is **pinned by Core tests**, so a later scale or road-layout change fails CI loudly instead of wedging the game at runtime — it is deliberately not left as a comment, which is how it was missed the first time.

    With that bound satisfied, the two setbacks **do** compose, and the rear setback shipped on [#658](https://github.com/derekwinters/lucas-doggiehood/issues/658). The deadlock condition is checked directly by a Core simulation of the case that failed before — **two oncoming vehicles** on one road, acquiring the intersection's two bands in opposite order. At the shipped geometry both complete their routes; sweeping the rear setback upward, the pair first wedges at **3.5626 → 3.5627 m**, against the algebraic threshold `clearGap − fs = 6.5 − 2.9375 = 3.5625 m` (the extra ten-thousandth is the traversal's float epsilon). The truck uses **2.4375 m**, so the shipped margin is **1.125 m** — the figure the `ModelScale` choice was sized for. Note that a *single* vehicle can never deadlock against itself (a blocked vehicle at its stop boundary has already released the band behind it), so the two-vehicle case is the one worth pinning; a one-truck test passes either way.

#### Crossing a whole intersection ([#673](https://github.com/derekwinters/lucas-doggiehood/issues/673))

A four-way puts **two** crosswalk bands across the road a vehicle is driving — one either side of the box — and a turning vehicle crosses one band on the way in and a *different road's* band on the way out. Treating each band as its own claim is what produced the "don't block the box" bug: the truck drove to the centre of the intersection, turned, and only then looked at the crosswalk ahead of it. A dog held it, so the truck stopped dead in the middle of the crossing — and, because reaching the turn point had released the leg it just finished, it sat there holding nothing at all.

Right-of-way is therefore scoped to the **manoeuvre**: every band between the vehicle and the far side of the intersection.

**Invariant — a vehicle does not enter an intersection until the entire manoeuvre through it is clear.** It takes every band of the crossing or none of them, before its bumper reaches the first. **A vehicle never comes to rest inside the intersection box** — it is either fully behind the first band or fully through. **Claims for one manoeuvre are all-or-nothing**: a denied vehicle holds nothing (so a dog may still take the band it was refused), and it holds what it has until its **tail** clears the manoeuvre's **final** band, then releases the set together.

*Derek's call (2026-08-07):* **"if the vehicle can't cross the intersection cleanly, it stops before entering"** — one rule for turns **and** straight runs. A straight pass through a four-way is the same two-band manoeuvre a turn is, so no code path anywhere asks "is this a turn?". The cost is accepted: vehicles wait behind intersections somewhat more often than they used to, including on straight runs that worked fine before.

Two consequences worth stating. **No deadlock:** all-or-nothing acquisition with release-on-failure removes hold-and-wait, so two vehicles can never each own half a crossing and wait on the other. **No livelock:** bands are attempted in one **global order** keyed on band identity, so two vehicles whose manoeuvres overlap always contend in the same sequence and one wins outright, rather than both rolling back and retrying in step. This is also what turns the [#660](https://github.com/derekwinters/lucas-doggiehood/issues/660) vehicle-length bound from the *only* thing preventing a permanent wedge into a belt-and-braces geometry rule.

This is a **generic** rule, not a truck-only hack: the claim is keyed per crosswalk segment and shared by every current and future vehicle. Since [#599](https://github.com/derekwinters/lucas-doggiehood/issues/599) the delivery truck routes over the **live multi-tile road network** — entering off-map, turning at intersections, and leaving by another road edge — so a vehicle that turns across an intersection is the real case, not an anticipated one. Because a route's waypoints are road junctions, crossing an intersection is **always two legs meeting at its centre**, which is precisely why a per-leg claim could never see a whole crossing. The mechanism lives in Core (`RoadCrossingGate` — a first-come claim/release gate on each crosswalk `WalkEdge`; `RoadManoeuvre` — one intersection crossing as a single all-or-nothing claim; `RouteManoeuvres` — the manoeuvres a route contains, resolved once and **shared by both legs** of each crossing; `RoadCrossingTraversal` — the vehicle side in along-road coordinates, per leg); the thin Unity views (`DogView`, `DeliveryTruckView`) only convert positions and move. The minimal pairwise vehicle↔dog check per crosswalk is the whole scope; dog-vs-dog queuing over a shared crosswalk is explicitly out of scope (dogs already don't block each other). See the delivery-truck note in [Quest Content](../quests/quest-content.md) and the [walk network](../world/sidewalks.md).

> **How the spec is changing ([#673](https://github.com/derekwinters/lucas-doggiehood/issues/673)).** It used to say each leg of a route carries its **own** `RoadCrossingTraversal` and each crosswalk a vehicle drives over is **one independent claim** → it now says every band of one intersection crossing is a single all-or-nothing **manoeuvre**, acquired before the vehicle enters and released only when its tail is out the far side → because a turn is two legs, so per-leg right-of-way never asked "can I get all the way through?" and let the truck strand itself in the middle of the box. A one-band crossing (a Tee's lone arm) is unchanged: it was already its own whole manoeuvre.

**Vehicle↔vehicle: car-following on shared roads** ([#600](https://github.com/derekwinters/lucas-doggiehood/issues/600)). Because off-map entry ([#599](https://github.com/derekwinters/lucas-doggiehood/issues/599)) lengthens each drive, two "buy me something" deliveries can be in flight at once, so the driving system owns **multiple** active trucks that must not collide with **each other** as well as with dogs. At a crosswalk this is already covered — the same first-come `RoadCrossingGate` is generic over occupants, so a second truck reaching a crosswalk a first truck holds simply **waits**, exactly as a dog would. The gate only arbitrates the *crosswalk claim*, though; it does nothing to stop a following truck driving up into a stopped leader's body on the **approach**. So along the open road a separate **1-D car-following** rule keeps trucks apart:

- **Gap** — a follower never advances closer than **one car length** behind the truck ahead of it on the same road span (Derek, 2026-08-05).
- **Start-up delay** — when a stopped leader begins to move, the follower waits **one second** before it starts moving too, modelling a driver's reaction time (Derek, 2026-08-05).
- **Single file** — one lane per road span in each direction; no passing or lane-changing (this is not a traffic sim). A truck is constrained only by the nearest truck ahead of it on its own segment driving the same way; trucks on other segments, or oncoming, don't constrain it. Since [#672](https://github.com/derekwinters/lucas-doggiehood/issues/672) those two lanes are **physically** separated — each direction drives its own half of the road (see [Sidewalks & Walk Network § Lanes](../world/sidewalks.md#lanes-672)) — so an oncoming truck is not just excluded by rule, it is beside the follower rather than in front of it.

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
- [x] A vehicle **releases a crosswalk at its trailing edge, not its centre** — the claim is held until its pivot-to-tail distance (half its measured body length) has passed the far edge, so a waiting dog is never let onto a band the vehicle's back half is still covering; caller-supplied and defaulting to `0`, so a point occupant is unchanged, with the no-deadlock property pinned by a **two-oncoming-vehicles** Core simulation (#658)
- [x] A vehicle's **front + rear setbacks fit inside the clear gap between an intersection's two crosswalk bands** (`< crosswalkSpacing − CrosswalkWidth`, i.e. body length `< 6.0m`), so it never holds both bands at once and two oncoming vehicles cannot deadlock; the delivery truck's `ModelScale` is bounded by this, and the constraint is pinned by Core tests derived from `WorldDimensions` rather than left as a comment (#660)
- [x] A vehicle **acquires every crosswalk band of an intersection crossing before it enters the first one, all-or-nothing**, and never comes to rest inside the box — one rule for turns and straight runs alike; a denied vehicle holds nothing, and the set is released only once its tail clears the manoeuvre's final band. Bands are attempted in one global order so two contending vehicles cannot livelock (#673)
