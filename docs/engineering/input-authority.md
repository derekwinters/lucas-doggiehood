# Input Authority

*Issue: [#670](https://github.com/derekwinters/lucas-doggiehood/issues/670)*

Every touch, drag, pinch, twist and scroll in Doggiehood enters the game through **one** router and is handed to **one** authority, which decides who receives it. Nothing else reads raw input, and nothing receives input without registering. This page is the rule; a guard test enforces it.

## Why this exists

Opening the debug tuning menu and dragging a slider used to move the slider **and** pan the map underneath it. The modal registry (`ModalInputGate`) was not broken — all seven overlays registered with it correctly. The camera simply never asked it.

"Blocking" had been built for **taps only**. `TapRouter.RouteTap` consulted the gate, and it was reached from exactly one place: a pointer release with under 12 px of travel. Every other path — touch drag, mouse drag, pinch, twist, scroll wheel — ran straight from raw polling to the camera with no gate check at all. Each one silently opted out of blocking when it was added, and nothing failed when it did.

That is the shape of the defect worth fixing, not the slider. Blocking was a **property individual consumers opted into**, so:

- a new input consumer was unblocked by default, and adding one broke no test;
- arbitration was per-site and inconsistent — `TapRouter` grew bespoke pre-passes for the HUD gear, speech bubbles ([#169](https://github.com/derekwinters/lucas-doggiehood/issues/169)) and lost items ([#311](https://github.com/derekwinters/lucas-doggiehood/issues/311)), while `HouseView` had no arbitration at all and fired every subscriber;
- ownership was decided at **release**, so a gesture that began on a control could end somewhere else and be delivered to whatever happened to be under the finger by then.

## The four rules

**R1 — Single entry point.** All raw `UnityEngine.Input` is read in `InputRouter` and nowhere else. It translates raw events into engine-free `InputGesture`s and hands every one to `InputAuthority`. It makes no decision of its own: it never moves the camera, never hit-tests the world, never asks whether a dialog is open. `CameraRig` stopped being a privileged path and became an ordinary consumer.

**R2 — Ownership is claimed at gesture start and held to gesture end.** The owner of a press is resolved **once, at press-down**, and every subsequent move and the release of that gesture go to that owner **alone** — even after the pointer leaves its bounds, even if what it started on has despawned or animated away. This is what fixes the slider directly: the press begins on the slider, so the whole drag belongs to the slider and the camera never sees it. No drag-distance heuristic is involved.

**R3 — Strict priority, exclusive delivery.** At press-down the topmost interested tier wins and **consumes**:

| Tier | Who | Notes |
|---|---|---|
| 1 | **Modal UI** | An open dialog/overlay and its scrim. Blocks every tier below it, including camera pan/pinch/twist/scroll. Membership is the shared `ModalInputGate`, not a registration. |
| 2 | **Non-modal UI** | HUD, the IMGUI gear, tuning-menu chrome. |
| 3 | **World objects** | Houses, dogs, lost items, bug swarms, expansion locks. |
| 4 | **Camera** | Pan/zoom/rotate — the fallback, offered a gesture only when nothing above claimed it. |

Exactly one consumer per gesture, never a fan-out.

**R4 — The registry is enumerable, and a guard test enforces it.** `InputAuthority.Consumers` lists everything that can receive input, and `InputAuthorityGuardTests` (Core suite) fails the build if any type outside the router reads raw input or drives a camera gesture directly. Without it R1–R3 are a convention, and conventions are exactly what failed here repeatedly. **Deleting that test reopens this bug.**

## Invariants

**Invariant — all input passes through the one authority.** Every gesture the game acts on is read by `InputRouter`, resolved by `InputAuthority`, and delivered to a registered consumer. A component may not poll input, and may not act on a gesture it was not handed. This is a hard rule about *how* input is wired, not just a target outcome: a second polling path that happens to check the modal gate today is still not acceptable, because the next one won't.

**Invariant — while any modal is open, no input of any kind reaches the camera or the world.** Not taps, not drags, not pinches, not twists, not scroll.

**Invariant — a gesture is delivered to exactly one consumer, chosen at press-down, for its entire lifetime.** A gesture already in flight when a modal opens **keeps its owner** until it is released; a modal blocks every gesture that *starts* after it opens. A gesture that began blocked stays blocked even if the modal closes mid-gesture.

**Invariant — a new input consumer is blocked by default.** Reaching input requires registering with the authority; a consumer that never registers receives nothing.

## The pieces

Core (`Doggiehood.Core.Interaction`, engine-free and unit-tested):

- `InputGesture` — one raw event: kind (`Pan`, `Pinch`, `Twist`, `Scroll`), pointer id, screen position, deltas. Synthetic pointer ids (mouse, two-finger, wheel) are negative so they can never collide with a platform touch `fingerId`.
- `InputGesturePhase` — `Began` / `Changed` / `Ended` / `Cancelled`. `Cancelled` exists so a press superseded by a second finger cannot resolve as the tap the player never made.
- `IInputConsumer` / `DelegateInputConsumer` — tier, a `ClaimsGesture` hit-test asked once at press-down, and delivery.
- `InputAuthority` — the registry, the priority resolution, and the ownership latch. Its modal tier is the shared `ModalInputGate`, so [#544](https://github.com/derekwinters/lucas-doggiehood/issues/544)'s deterministic open-modal flag and [#568](https://github.com/derekwinters/lucas-doggiehood/issues/568)'s same-frame close latch keep working — but now for every gesture kind, not just taps.
- `HouseTapArbiter` — a house tap resolves to **one** outcome: spray if the house has bugs, otherwise open its profile.

Unity (thin wiring only):

- `InputRouter` — the single raw-input reader. Also clears the `ModalInputGate` same-frame latch in `LateUpdate`, since that is an input-frame boundary.
- `CameraRig` — registers as the tier-4 consumer and applies gestures it is handed.

## What has not been ported yet

The bespoke pre-passes inside `TapRouter` — the HUD gear rect, speech bubbles (#169), lost items (#311) — still work as they did, behind the new authority. The authority makes them **redundant**, not broken, and retiring them is a follow-up: bundling three behaviour-preserving refactors into the change that reworked all five camera paths would have made any regression hard to attribute. Tiers 2 and 3 therefore have no registered production consumer yet; world taps still reach objects through `TapRouter`, invoked by the camera consumer at release.

When that follow-up lands, world objects become tier-3 consumers and the pre-passes dissolve.

## Working on input

If you are adding anything that responds to touch, drag, pinch, twist or scroll:

1. Do **not** read `UnityEngine.Input`. Register an `IInputConsumer` with `InputAuthority` at the right tier.
2. Do the hit-test in `ClaimsGesture`, not in the delivery handler — ownership is decided at press-down.
3. If the guard test fails, do not add your file to its allow-list. Register instead.
