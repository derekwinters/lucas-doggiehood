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

## Item catalog

*Source: [#190](https://github.com/derekwinters/lucas-doggiehood/issues/190), decided in interview with Derek 2026-07-16.*

The priced catalog and every quest type's subject pool are **one source of truth**: a single tagged item catalog (`ItemCatalog`), not per-type hand-maintained lists. Each entry carries:

- **Name**
- **Cost** — optional. Purchasable items (gift/decoration) carry a cost in the 30-50 range above; find-only items (e.g. a lost puppy) carry none, since they're found rather than bought.
- **Eligibility tags** for which quest type(s) it can appear in — Lost, Gift, Decoration. An item can carry more than one tag (a toy or ball is both lost- and gift-eligible).

Each quest type's subject pool is a query over the catalog's tags (e.g. "every Gift-eligible item"), and the decoration-request options are the Decoration-eligible slice of the same catalog — there is no second, independently maintained item list anywhere. Adding a new item is a single catalog entry with its tags and cost; it then appears automatically in every rotation pool it's tagged for. The 30-50 coin rule above is a tested invariant on every Gift- or Decoration-eligible catalog entry.

## Quest authoring

*Source: [#61](https://github.com/derekwinters/lucas-doggiehood/issues/61), implementation tracked in [#69](https://github.com/derekwinters/lucas-doggiehood/issues/69). Line-variety model ("Model 2") decided in interview with Derek 2026-07-16, tracked in [#189](https://github.com/derekwinters/lucas-doggiehood/issues/189).*

Quests are authored as reusable **templates**, not hand-written per dog. Each quest type (see [Quest Content](quest-content.md)) has a dialogue template with variable slots:

- Dog name
- Item/subject (toy name, decoration type, pest type, etc.)
- Personality ([Dog Behavior](../dogs/behavior.md)) — seasons which opener/closer line is drawn, per the pooled model below

New quest types (mechanics) beyond the existing set stay deferred/post-MVP — this section is only about line variety within the existing types, not new mechanics.

### Line variety: pooled openers/closers, uniform random ("Model 2")

For both the opener and the closer, each quest type holds two pools:

- A **default pool** of personality-agnostic lines (authoring target ~10-15 lines).
- A small **per-personality pool** of personality-specific lines, for whichever personalities we choose to flavor (authoring target ~3-4 lines per personality; 0 is valid — flavoring a personality is optional per type).

When a quest fires, the candidate set is `default pool ∪ this dog's personality pool`, and one line is picked **uniformly at random per string** (not per bucket) — so with ~15 defaults and ~3 Grumpy-specific lines, a Grumpy dog says a Grumpy-specific line only ~1-in-6 of the time. Personality is seasoning, not the default voice; wanting more personality presence means writing more personality-specific lines, never special-casing the weighting. Uniform-per-string is a hard requirement — no single line variant may be allowed to dominate.

Selection is **pure random each fire**: no "avoid immediate repeat" memory, no cycle-through, no per-dog or per-session persisted state. The RNG is injectable (constructor/method-seeded `System.Random`), matching the pattern used by the [move-in system](../expansion.md#move-in-system), so line selection stays deterministic under test.

This generalizes what earlier drafts did with a single default line and a single flavored line per personality — same slots, same personality-driven flavoring, just pools instead of one-liners so a given dog doesn't say the identical sentence every time.

## Build checklist

- [ ] Currency balance persists across sessions
- [ ] Completing any quest grants a flat 10 coins
- [ ] Spending currency on a gift/decoration deducts its cost (30-50 coin range) and fails gracefully if the player can't afford it
- [ ] A daily rotation system selects 2-4 dogs to have new active quests, refreshing once per day
- [ ] Completed quest state (delivered items, decorations) persists permanently — no reset logic tied to the daily rotation
- [ ] No quest has a timer, expiration, or fail state anywhere in the system
- [ ] A quest template data structure exists with slots for dog name, personality-flavored line variant, and item/subject
- [ ] At least the 3 MVP quest types (see [Quest Content](quest-content.md)) are expressed as templates, not hard-coded per-dog text
- [x] Opener and closer lines are drawn from a default pool ∪ per-personality pool, uniform-random per string, via an injectable RNG — no anti-repeat memory or per-dog persisted state ([#189](https://github.com/derekwinters/lucas-doggiehood/issues/189))
- [x] All quest subject pools (and decoration-request options) are queries over one tagged item catalog — no per-type parallel item lists; every Gift/Decoration-eligible entry costs 30-50 coins, find-only entries carry no cost ([#190](https://github.com/derekwinters/lucas-doggiehood/issues/190))
