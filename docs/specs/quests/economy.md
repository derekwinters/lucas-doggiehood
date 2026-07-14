# Quest & Economy

*Epic: [#15](https://github.com/derekwinters/lucas-doggiehood/issues/15)*

## Core loop

The overall goal is helping dogs around the neighborhood by completing their requests, combined with a daily rotation of active quests. ([#23](https://github.com/derekwinters/lucas-doggiehood/issues/23))

- **Pacing**: each day, a small number of dogs (2-4) have new requests available, mixing quest types. Keeps sessions short and gives a reason to return daily. ([#26](https://github.com/derekwinters/lucas-doggiehood/issues/26))
- **Permanence**: items/decorations delivered or found through completed quests stay in the world permanently — nothing resets with the daily rotation. ([#27](https://github.com/derekwinters/lucas-doggiehood/issues/27))
- **No pressure**: quests never expire and there is no timer or fail condition anywhere. ([#28](https://github.com/derekwinters/lucas-doggiehood/issues/28))

## Currency

- **Source**: currency is earned only by completing dog requests/quests. No idle income, ads, or mini-games for MVP. ([#24](https://github.com/derekwinters/lucas-doggiehood/issues/24))
- **Sinks**: quest gifts, decorations (both delivered via the normal quest flow — see [Decorations](../decorations.md)), and (post-MVP) new streets/houses ([Neighborhood Expansion](../expansion.md)). ([#25](https://github.com/derekwinters/lucas-doggiehood/issues/25))

### Numbers (placeholder, expect tuning)

*Source: [#62](https://github.com/derekwinters/lucas-doggiehood/issues/62)*

- A completed quest pays a **flat 10 coins**, regardless of quest type.
- A typical gift/decoration item costs **roughly 30-50 coins** (3-5 quests' worth of saving).

Post-MVP expansion sinks *(decisions 2026-07-14, Derek — see [Neighborhood Expansion](../expansion.md#pricing))*:

- Zone unlock: **100 coins** for the first zone, **+100 per subsequent zone** (100, 200, 300, …).
- Building a house: **50 coins**, flat.
- House upgrades: **100 / 200 / 400 coins** for levels 2 / 3 / 4.

These are starting values, not final balance — expect to tune once the daily-rotation pacing can actually be felt in a playable build. Every one of them is a named constant in Core so playtesting adjustments are one-line changes.

## Quest authoring

*Source: [#61](https://github.com/derekwinters/lucas-doggiehood/issues/61), implementation tracked in [#69](https://github.com/derekwinters/lucas-doggiehood/issues/69)*

Quests are authored as reusable **templates**, not hand-written per dog. Each quest type (see [Quest Content](quest-content.md)) has a dialogue template with variable slots:

- Dog name
- Personality ([Dog Behavior](../dogs/behavior.md)) — flavors which line variant is picked (a Grumpy dog's template line reads differently than an Excited dog's)
- Item/subject (toy name, decoration type, pest type, etc.)

This is what lets the daily rotation and a growing neighborhood stay populated with quests without hand-authoring hundreds of unique conversations. **Not yet implemented** — needs an actual template data structure once the project exists.

## Build checklist

- [ ] Currency balance persists across sessions
- [ ] Completing any quest grants a flat 10 coins
- [ ] Spending currency on a gift/decoration deducts its cost (30-50 coin range) and fails gracefully if the player can't afford it
- [ ] A daily rotation system selects 2-4 dogs to have new active quests, refreshing once per day
- [ ] Completed quest state (delivered items, decorations) persists permanently — no reset logic tied to the daily rotation
- [ ] No quest has a timer, expiration, or fail state anywhere in the system
- [ ] A quest template data structure exists with slots for dog name, personality-flavored line variant, and item/subject
- [ ] At least the 3 MVP quest types (see [Quest Content](quest-content.md)) are expressed as templates, not hard-coded per-dog text
