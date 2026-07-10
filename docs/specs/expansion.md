# Neighborhood Expansion

*Epic: [#55](https://github.com/derekwinters/lucas-doggiehood/issues/55)*

!!! note "Post-MVP"
    This entire system is fully designed but **explicitly excluded from the MVP vertical slice** ([#88](https://github.com/derekwinters/lucas-doggiehood/issues/88)). Milestone `08 - Vertical Slice Release Candidate` ships with just the 4 starting houses (see [World & Neighborhood](world/world.md)). Nothing on this page needs to be built for MVP — it's documented now so the design isn't lost before its milestone (`06 - Neighborhood Expansion`) starts.

## The loop

1. **Unlock a zone**: the player pays a currency cost to unlock a new zone/area of the map. Newly unlocked zones start with no houses at all — just empty land. ([#56](https://github.com/derekwinters/lucas-doggiehood/issues/56))
2. **Build houses**: the player spends currency to construct individual houses within an unlocked zone. Houses don't appear automatically — building each one is its own purchase. ([#57](https://github.com/derekwinters/lucas-doggiehood/issues/57))
3. **Vacancy**: a freshly built house is empty and displays a "for sale" sign until a dog eventually moves in. ([#58](https://github.com/derekwinters/lucas-doggiehood/issues/58))
4. **Occupancy over time**: new dogs arrive and move into empty houses gradually over time, rather than all at once. ([#54](https://github.com/derekwinters/lucas-doggiehood/issues/54)) New dogs are drawn from the [name pool](dogs/roster-names.md#general-name-pool-for-dogs-that-move-in-later) and assigned breeds from the remaining pool (French Bulldog, Puggle reserved — see [Dog Roster & Names](dogs/roster-names.md)).
5. **House leveling**: every house starts at level 1 and can be upgraded up to level 4. Upgrading makes the house visually bigger/nicer and increases how many decorations its yard can hold. ([#59](https://github.com/derekwinters/lucas-doggiehood/issues/59))

## Open question: overall map shape

*[#86](https://github.com/derekwinters/lucas-doggiehood/issues/86) — genuinely unresolved, does not block MVP*

Still undecided: whether the overall map is a fixed finite layout or open-ended, what shape expansion takes (grid, branching streets, a loop), roughly how many zones are planned, and whether there's any landmark variety (a park, a cul-de-sac) versus uniform intersections. This needs an answer before work on milestone `06` begins, but not before MVP work starts.

## Build checklist (for when milestone `06` starts — not now)

- [ ] Currency-gated zone unlock reveals an empty zone
- [ ] Currency-gated house building places a house in an unlocked zone
- [ ] Newly built houses show a "for sale" sign
- [ ] A gradual, time-based system moves dogs into empty houses (drawing from the name pool + remaining breed pool)
- [ ] Houses support 4 discrete levels, each with a bigger footprint and more decoration slots
- [ ] Overall map shape/pattern is decided (resolve [#86](https://github.com/derekwinters/lucas-doggiehood/issues/86) first)
