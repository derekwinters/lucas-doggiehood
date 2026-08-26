# Product Scope & Constraints

*Epic: [#17](https://github.com/derekwinters/lucas-doggiehood/issues/17)*

These are standing product decisions, not tied to any single build milestone.

## Monetization

No ads, no in-app purchases, no store monetization plans. This is a free personal project. ([#41](https://github.com/derekwinters/lucas-doggiehood/issues/41))

## Connectivity

Fully offline. No account system, no backend. Progress is saved locally on the device only. ([#42](https://github.com/derekwinters/lucas-doggiehood/issues/42))

**Invariant — the game never makes a network call.** Nothing in this project opens a socket, contacts a server, or uploads anything, in any build. There is exactly one way data leaves the device, and the player starts it by hand: the Settings → Debug → Reports **Share bug report** row writes a [bug report](../engineering/bug-reports.md) to local storage and hands *that file* to the Android share sheet. What happens next is the operating system's and the chosen app's: the player picks the destination, and that app does whatever sending it does. The game has no recipient, no address and no upload of its own, and nothing is ever offered in the background or without a tap. ([#693](https://github.com/derekwinters/lucas-doggiehood/issues/693) / [#695](https://github.com/derekwinters/lucas-doggiehood/issues/695))

> **How the spec is changing (#695).** This page used to say only *"fully offline"* and leave it there, which read as "nothing ever leaves the device by any route" → it now keeps that rule exactly as strict for the **game** — still no network call, anywhere — and states the one narrow, player-initiated exception explicitly: a bug report can be handed to the **OS share sheet** as a file, with the player choosing the destination app → because a report Lucas cannot send without a USB cable is a report Derek never sees ([#693](https://github.com/derekwinters/lucas-doggiehood/issues/693) chose this over an in-app uploader for exactly that reason). The carve-out is deliberately narrow: handing a file to the OS is not the app going online, and it does not open the door to uploads, telemetry, accounts or crash reporting — those stay out of scope.

## Saved state

*Added by [#704](https://github.com/derekwinters/lucas-doggiehood/issues/704) so "is this persisted?" has one authoritative answer.*

"Progress is saved locally" means the whole neighborhood, not a subset of it. The save is a single local text file (`SaveStore`, serialized by `Doggiehood.Core.World.SaveCodec`) — no accounts, no backend, nothing else on the device.

**Invariant — the neighborhood is durable.** Anything the player earned, spent, or was given — every dog living in the neighborhood, every offered or accepted quest, every coin — survives closing the app. Only *presentation* is session-only: positions, animations, and in-flight view objects.

**Durable** (in the save):

- Coins, onboarding completion, and the one-time onboarding reward-chain step (plus its upgrade-target house)
- The map: every player-unlocked tile with its type, in unlock order (green-space tiles are re-derived, not stored)
- Every built house — level, occupancy, rolled art variant — and each unbuilt lot's pre-assigned variant
- Every dog that has moved in: name, breed, personality, house, puppy flag, coat. (The 8 starting dogs are recreated on load, exactly like the 4 starting houses.)
- Every uncompleted quest, in full: type, status, delivery phase, its dog, its subject, the dialogue that was rolled for it, the hidden item's position, the cost already fronted, the afflicted house, and a decoration request's options
- Pacing state: the refresh clock, the last rotation stamp, the fractional trickle accumulator
- The move-in pity counter and the unconsumed easter-egg-name / reserved-breed reserves
- Placed items and yard decorations

**Deliberately session-only** (rebuilt each launch, and *correct* not to persist):

- Where a dog is standing, which way it faces, its pose and its wander target — presentation, re-derived from the walk network on spawn
- In-flight view objects: the delivery truck on its route, and the delivered package on the doorstep, which is scene-only by design ([#703](https://github.com/derekwinters/lucas-doggiehood/issues/703)) and asserted so in Core tests
- The authored target map and everything derived from it (the unlock frontier, the walk network, camera reach) — fixed design data, re-supplied on every launch
- Dog happiness, which is flavor only and gates nothing ([#47](https://github.com/derekwinters/lucas-doggiehood/issues/47))

**Format compatibility.** The save is a line-per-record key/value format and the loader acts only on keys it knows, so adding a record type is backward- *and* forward-compatible: an older save simply lacks the newer lines and each takes a defined absent-value default, and a save written by a newer build still opens in an older one. New fields are therefore added without a schema version bump; anything that genuinely cannot be defaulted needs a documented migration instead (the one so far: a pre-#704 save's house recorded as occupied with no residents is re-vacated on load, so it re-enters the move-in pool rather than staying permanently unfillable).

## Audience

All-ages/family. The game aims to be cozy and simple enough for a family to play together, without being designed around any one specific age bracket. ([#43](https://github.com/derekwinters/lucas-doggiehood/issues/43))

## Build checklist

- [ ] No ad SDK, IAP SDK, or monetization code paths exist anywhere in the project
- [ ] No network calls exist anywhere in the project — the app functions with no connectivity at all
- [ ] Save data is local-only (device storage), with no account/login flow
- [x] Everything the player earned, spent, or was given survives a relaunch; only presentation is session-only — see [Saved state](#saved-state) ([#704](https://github.com/derekwinters/lucas-doggiehood/issues/704))
- [x] The app saves on backgrounding and on quit, and on an interval while it runs, so a session is never rolled back ([#704](https://github.com/derekwinters/lucas-doggiehood/issues/704))
