# Neighborhood Expansion

*Epic: [#55](https://github.com/derekwinters/lucas-doggiehood/issues/55)*

!!! note "v0.4"
    This entire system is fully designed and is now slated for **v0.4** ([#88](https://github.com/derekwinters/lucas-doggiehood/issues/88)) — no longer deferred. The neighborhood still starts with just the 4 houses (see [World & Neighborhood](world/world.md)); nothing on this page gets built before `v0.4` starts.

## The loop

1. **Unlock a zone**: the player pays a currency cost to unlock a new zone/area of the map. Newly unlocked zones start with no houses at all — just empty land. ([#56](https://github.com/derekwinters/lucas-doggiehood/issues/56))
2. **Build houses**: the player spends currency to construct individual houses within an unlocked zone. Houses don't appear automatically — building each one is its own purchase. ([#57](https://github.com/derekwinters/lucas-doggiehood/issues/57))
3. **Vacancy**: a freshly built house is empty and renders greyscaled until a dog eventually moves in. ([#58](https://github.com/derekwinters/lucas-doggiehood/issues/58))
4. **Occupancy over time**: new dogs arrive and move into empty houses gradually over time, rather than all at once. ([#54](https://github.com/derekwinters/lucas-doggiehood/issues/54)) See [Move-in system](#move-in-system) below for the full mechanism, and [Dog Roster & Names](dogs/roster-names.md) for how names and breeds are drawn.
5. **House leveling**: every house starts at level 1 and can be upgraded up to level 4. Upgrading makes the house visually bigger/nicer and increases how many decorations its yard can hold. ([#59](https://github.com/derekwinters/lucas-doggiehood/issues/59))

## Map shape

> **Decision (2026-07-14, Derek, on [#86](https://github.com/derekwinters/lucas-doggiehood/issues/86)):** the map is **hand-picked expansion with no fixed end** — zones are authored and added over time, and procedural generation stays out of scope ([#109](https://github.com/derekwinters/lucas-doggiehood/issues/109)). A **zone is a hand-authored group of tiles** from the [tile catalog](world/tile-catalog.md) (60m grid), pre-authored by Derek and Lucas; the player unlocks zones in sequence but never places tiles. Initial zones use only **cul-de-sac, straight, tee, and turn** tiles — more variety (including the OpposingTurns/park-island tiles, whose arch-loop question stays deferred on #109) comes later.

**First zone:** a cul-de-sac street to the northwest of the starting intersection. The confirmed layout (2026-07-14, Derek, on [#56](https://github.com/derekwinters/lucas-doggiehood/issues/56)): from the starting `FourWay` at grid (0,0), `TurnSW` at (0,1), `CulDeSacEast` at (−1,1) — the road runs north from the intersection, turns west, and ends in the bulb.

The tile-grid placement/adjacency system this all sits on is [#109](https://github.com/derekwinters/lucas-doggiehood/issues/109), the milestone's geometric prerequisite.

## Pricing

*Decisions 2026-07-14 (Derek, in conversation). All values are named, tunable constants in Core — expect adjustment during playtesting; see also [Quest & Economy](quests/economy.md#numbers-placeholder-expect-tuning).*

| Purchase | Cost |
|---|---|
| Zone unlock | 100 coins for the first zone, +100 per subsequent zone (100, 200, 300, …) |
| Build a house | 50 coins, flat |
| House upgrade to L2 / L3 / L4 | 100 / 200 / 400 coins (doubles per level) |

**Implementation note ([#56](https://github.com/derekwinters/lucas-doggiehood/issues/56)):** built as pure Core logic on top of [#109](https://github.com/derekwinters/lucas-doggiehood/issues/109)'s tile grid — `Doggiehood.Core.World.Zone` (authored tile placements + the quadrant lots each tile carries, via `TileLotCatalog`/`TileGeometry`), `Doggiehood.Core.World.ZoneCatalog` (the authored zone list — only the first zone exists yet), and `Doggiehood.Core.Expansion.ZoneUnlock`/`ZoneUnlockNumbers` for the cost formula. `GameState` now owns a `TileMap` (`Map`, seeded with just the starting FourWay intersection) and `UnlockedZones`; `GameState.TryUnlockNextZone()` is the one entry point — it charges `Wallet` for the next zone in sequence, adds that zone's tiles to `Map` through #109's placement/adjacency validation, and returns false with no state change (no deduction, no tiles placed) if the balance can't cover it or every authored zone is already unlocked. Unlocking never creates `House` objects — `GameState.IsLotBuildable(houseId)` reports a zone's lots as buildable exactly because no `House` exists for that id yet, which is what house building (#57) will check. **Still outstanding:** persisting `Map`/`UnlockedZones` through `SaveCodec` — like the [move-in system's pity counter](#move-in-system) below, this resets every app session until that lands. No Unity-side unlock trigger exists yet either — the UI/placement question is open on [#178](https://github.com/derekwinters/lucas-doggiehood/issues/178).

## Move-in system

*Decision 2026-07-14 (Derek, in conversation), detailed on [#54](https://github.com/derekwinters/lucas-doggiehood/issues/54). All values named, tunable constants; RNG injectable for deterministic tests.*

- **Trigger — quest-completion pity counter, shared:** every completed quest rolls a single shared move-in chance: base **5%**, **+5%** per completed quest without a move-in, reset to base on success. One counter for the whole neighborhood; on success one vacant house is chosen at random (uniform). The counter only advances while at least one vacant house exists.
- **Households:** roster-style mix from day one — **70% single / 25% parent+puppy / 5% three-dog**. Parent+puppy pairs share a breed; every dog gets its own personality, randomly drawn from the [behavior](dogs/behavior.md) set.
- **Breeds:** the reserved French Bulldog and Puggle are the first two move-ins; afterwards breeds are chosen by **count-weighted randomness** — each breed weighted inversely by how many currently live in the neighborhood, so variety self-adapts to the existing distribution.
- **Easter eggs:** each move-in has a **5%** chance the household head is an [easter-egg dog](dogs/roster-names.md#easter-egg-dogs) (fixed breed/coat, breed roll skipped); once used, an easter-egg name is permanently removed from the reserve — each appears exactly once.
- **Names** come from the [general name pool](dogs/roster-names.md#general-name-pool-for-dogs-that-move-in-later), no duplicates among active dogs.
- New dogs **join the daily quest rotation immediately**.

**Implementation note ([#54](https://github.com/derekwinters/lucas-doggiehood/issues/54)/[#58](https://github.com/derekwinters/lucas-doggiehood/issues/58)):** the mechanism above is built as pure Core logic — `Doggiehood.Core.Expansion.MoveInSystem`, `VacantHouses`, and `BreedWeighting` — operating on an abstract set of vacant house ids plus the current dog roster, with no dependency on the #109/#56 tile-grid/zone geometry. `Doggiehood.Core.World.House` carries the vacancy flag itself (`IsVacant`, flipped by `MarkOccupied`); `Doggiehood.Core.Expansion.HouseOccupancy` bridges the two, deriving the current vacant set from live houses, running one `MoveInSystem` roll, and — only on success — occupying the filled house. `GameState.HandleQuestCompleted` calls this on every completed quest (`QuestManager.Complete`), so the trigger is fully wired. Since zone unlock (#56) and house building (#57) don't exist yet, every house in a real game is one of the 4 starting houses (already occupied at `GameState.CreateNew()`) — so this wiring has no vacant house to actually fill until #56/#57 land, but is exercised directly in the Core test suite. **Still outstanding:** persisting `MoveInSystem`'s pity-counter and easter-egg-reserve state through `SaveCodec` — until that lands, both reset every app session.

## House leveling

Levels 1–4. Decoration slots equal the house level (**1/2/3/4**); upgrade costs are in [Pricing](#pricing). The v1.0 decoration flow auto-places with no cap, so the cap is introduced with [#59](https://github.com/derekwinters/lucas-doggiehood/issues/59) (already-placed decorations are never removed). Per-level *visuals* are deliberately not yet designed — Core carries only the level number, and the art mapping is flagged as an open item on #59.

## Expansion indicator (discoverability)

The neighborhood needs a visible signal that it *can* grow — a marker on the map for an unexplored/expandable zone. The broader discoverability gap (how the player learns coins unlock zones, how affordability is shown, where the unlock action lives) is tracked on [#178](https://github.com/derekwinters/lucas-doggiehood/issues/178) and still open.

> **Decision (2026-07-16, Derek, on [#178](https://github.com/derekwinters/lucas-doggiehood/issues/178)):** the indicator is a **lock icon** marking the locked/expandable zone. The art is Kenney's `locked` Game Icon (CC0), staged at `Assets/Art/UI/ExpansionIndicator/locked.png`. This resolves the first open question on #178 (how the map shows expandable area exists); the remaining #178 questions (onboarding the coins→zones concept, affordability display, action placement) stay open.

The asset is imported/staged only — nothing is wired to it yet. Like every other art binary in the repo it carries no committed per-file `.meta`; when `v0.4` wires the icon into the map UI, that PR pins its GUID and sprite internal ID and adds the serialization guard test, per [Hand-Authoring Unity Serialized Assets](../engineering/unity-serialization.md).

## Vacant house rendering

> **Decision update (2026-07-16, Derek, on [#58](https://github.com/derekwinters/lucas-doggiehood/issues/58)):** superseded the earlier "for sale sign in the yard" plan. Instead, **the whole house mesh renders greyscaled (desaturated) while vacant** and returns to its normal tinted color the moment a dog moves in. This needs no new art asset — it's a flat desaturated material color multiplied over the existing house mesh (the same technique already used for dog coat tinting), not a sign object placed in the yard. It removed the dependency on sourcing a "for sale" sign 3D asset, so [#154](https://github.com/derekwinters/lucas-doggiehood/issues/154) is closed as no longer needed.

Vacancy is Core state (`House.IsVacant`); the greyscale is purely its visual, with no logic of its own — `WorldBuilder` reads the flag at build time and picks the flat vacancy tint instead of the house's normal `HouseStyleTable` coloring, in both the City Kit model path and the graybox fallback.

## Build checklist (for when `v0.4` starts)

- [x] Tile grid placement + adjacency validation exists ([#109](https://github.com/derekwinters/lucas-doggiehood/issues/109) — build first)
- [x] Currency-gated zone unlock (100 + 100 per zone) reveals an authored, empty zone; first zone is the northwest cul-de-sac street
- [ ] Currency-gated house building (50 flat) places a level-1, vacant house on an empty lot in an unlocked zone
- [ ] Newly built houses render greyscaled and return to their normal tinted color on move-in ([#58](https://github.com/derekwinters/lucas-doggiehood/issues/58) — Core state, wiring, and rendering already built; unreachable in a real game until house building (#57) exists)
- [ ] The shared pity-counter move-in system (5% base, +5% per quest, reset on success) fills vacant houses per the household/breed/easter-egg rules above
- [ ] Houses support 4 discrete levels (upgrades 100/200/400) with decoration slots equal to level
- [ ] Every tuning value above is a named constant adjustable for playtesting
