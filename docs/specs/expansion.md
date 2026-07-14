# Neighborhood Expansion

*Epic: [#55](https://github.com/derekwinters/lucas-doggiehood/issues/55)*

!!! note "Post-MVP"
    This entire system is fully designed but **explicitly excluded from the MVP vertical slice** ([#88](https://github.com/derekwinters/lucas-doggiehood/issues/88)). Milestone `08 - Vertical Slice Release Candidate` ships with just the 4 starting houses (see [World & Neighborhood](world/world.md)). Nothing on this page gets built before its milestone (`06 - Neighborhood Expansion`) starts.

## The loop

1. **Unlock a zone**: the player pays a currency cost to unlock a new zone/area of the map. Newly unlocked zones start with no houses at all — just empty land. ([#56](https://github.com/derekwinters/lucas-doggiehood/issues/56))
2. **Build houses**: the player spends currency to construct individual houses within an unlocked zone. Houses don't appear automatically — building each one is its own purchase. ([#57](https://github.com/derekwinters/lucas-doggiehood/issues/57))
3. **Vacancy**: a freshly built house is empty and displays a "for sale" sign until a dog eventually moves in. ([#58](https://github.com/derekwinters/lucas-doggiehood/issues/58))
4. **Occupancy over time**: new dogs arrive and move into empty houses gradually over time, rather than all at once. ([#54](https://github.com/derekwinters/lucas-doggiehood/issues/54)) See [Move-in system](#move-in-system) below for the full mechanism, and [Dog Roster & Names](dogs/roster-names.md) for how names and breeds are drawn.
5. **House leveling**: every house starts at level 1 and can be upgraded up to level 4. Upgrading makes the house visually bigger/nicer and increases how many decorations its yard can hold. ([#59](https://github.com/derekwinters/lucas-doggiehood/issues/59))

## Map shape

> **Decision (2026-07-14, Derek, on [#86](https://github.com/derekwinters/lucas-doggiehood/issues/86)):** the map is **hand-picked expansion with no fixed end** — zones are authored and added over time, and procedural generation stays out of scope ([#109](https://github.com/derekwinters/lucas-doggiehood/issues/109)). A **zone is a hand-authored group of tiles** from the [tile catalog](world/tile-catalog.md) (60m grid), pre-authored by Derek and Lucas; the player unlocks zones in sequence but never places tiles. Initial zones use only **cul-de-sac, straight, tee, and turn** tiles — more variety (including the OpposingTurns/park-island tiles, whose arch-loop question stays deferred on #109) comes later.

**First zone:** a cul-de-sac street to the northwest of the starting intersection. The proposed concrete layout (derived, to be confirmed on [#56](https://github.com/derekwinters/lucas-doggiehood/issues/56)): from the starting `FourWay` at grid (0,0), `TurnSW` at (0,1), `CulDeSacEast` at (−1,1) — the road runs north from the intersection, turns west, and ends in the bulb.

The tile-grid placement/adjacency system this all sits on is [#109](https://github.com/derekwinters/lucas-doggiehood/issues/109), the milestone's geometric prerequisite.

## Pricing

*Decisions 2026-07-14 (Derek, in conversation). All values are named, tunable constants in Core — expect adjustment during playtesting; see also [Quest & Economy](quests/economy.md#numbers-placeholder-expect-tuning).*

| Purchase | Cost |
|---|---|
| Zone unlock | 100 coins for the first zone, +100 per subsequent zone (100, 200, 300, …) |
| Build a house | 50 coins, flat |
| House upgrade to L2 / L3 / L4 | 100 / 200 / 400 coins (doubles per level) |

## Move-in system

*Decision 2026-07-14 (Derek, in conversation), detailed on [#54](https://github.com/derekwinters/lucas-doggiehood/issues/54). All values named, tunable constants; RNG injectable for deterministic tests.*

- **Trigger — quest-completion pity counter, shared:** every completed quest rolls a single shared move-in chance: base **5%**, **+5%** per completed quest without a move-in, reset to base on success. One counter for the whole neighborhood; on success one vacant house is chosen at random (uniform). The counter only advances while at least one vacant house exists.
- **Households:** roster-style mix from day one — **70% single / 25% parent+puppy / 5% three-dog**. Parent+puppy pairs share a breed; every dog gets its own personality, randomly drawn from the [behavior](dogs/behavior.md) set.
- **Breeds:** the reserved French Bulldog and Puggle are the first two move-ins; afterwards breeds are chosen by **count-weighted randomness** — each breed weighted inversely by how many currently live in the neighborhood, so variety self-adapts to the existing distribution.
- **Easter eggs:** each move-in has a **5%** chance the household head is an [easter-egg dog](dogs/roster-names.md#easter-egg-dogs) (fixed breed/coat, breed roll skipped); once used, an easter-egg name is permanently removed from the reserve — each appears exactly once.
- **Names** come from the [general name pool](dogs/roster-names.md#general-name-pool-for-dogs-that-move-in-later), no duplicates among active dogs.
- New dogs **join the daily quest rotation immediately**.

## House leveling

Levels 1–4. Decoration slots equal the house level (**1/2/3/4**); upgrade costs are in [Pricing](#pricing). The MVP decoration flow auto-places with no cap, so the cap is introduced with [#59](https://github.com/derekwinters/lucas-doggiehood/issues/59) (already-placed decorations are never removed). Per-level *visuals* are deliberately not yet designed — Core carries only the level number, and the art mapping is flagged as an open item on #59.

## The "for sale" sign

Vacancy is Core state; the sign is purely its visual. No sign 3D asset exists yet — sourcing one is [#154](https://github.com/derekwinters/lucas-doggiehood/issues/154) (Direct Involvement Needed). Until it lands, a graybox placeholder is used via `WorldBuilder`'s existing missing-kit-piece fallback, so the asset does not block [#58](https://github.com/derekwinters/lucas-doggiehood/issues/58).

## Build checklist (for when milestone `06` starts)

- [ ] Tile grid placement + adjacency validation exists ([#109](https://github.com/derekwinters/lucas-doggiehood/issues/109) — build first)
- [ ] Currency-gated zone unlock (100 + 100 per zone) reveals an authored, empty zone; first zone is the northwest cul-de-sac street
- [ ] Currency-gated house building (50 flat) places a level-1, vacant house on an empty lot in an unlocked zone
- [ ] Newly built houses show a "for sale" sign (graybox until [#154](https://github.com/derekwinters/lucas-doggiehood/issues/154)) that clears on move-in
- [ ] The shared pity-counter move-in system (5% base, +5% per quest, reset on success) fills vacant houses per the household/breed/easter-egg rules above
- [ ] Houses support 4 discrete levels (upgrades 100/200/400) with decoration slots equal to level
- [ ] Every tuning value above is a named constant adjustable for playtesting
