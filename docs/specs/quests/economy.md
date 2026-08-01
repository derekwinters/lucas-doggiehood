# Quest & Economy

*Epic: [#15](https://github.com/derekwinters/lucas-doggiehood/issues/15)*

## Core loop

The overall goal is helping dogs around the neighborhood by completing their requests, combined with a daily rotation of active quests. ([#23](https://github.com/derekwinters/lucas-doggiehood/issues/23))

- **Pacing**: a rotation refreshes the neighborhood's active requests, mixing quest types, in gentle batches (2-4 dogs at a time). Keeps sessions short and gives a reason to return. ([#26](https://github.com/derekwinters/lucas-doggiehood/issues/26))
    - **Cadence — every 8 hours (UTC)**: the recurring refresh is owned by [#310](https://github.com/derekwinters/lucas-doggiehood/issues/310). A refresh runs when at least `EconomyNumbers.RefreshInterval` (**8 hours**) has elapsed since the last one, measured against a persisted **UTC timestamp** (`GameState.LastRotationUtc`). UTC — never the device-local clock — so changing the device timezone can neither double-fire nor stall the boundary. It is a boundary *check*, not a countdown/expiry: nothing is ever removed and no quest can fail (see **No pressure** below). **Missed time is a single top-up**: away 8 hours or 4 days, returning triggers exactly one refresh to the cap — the elapsed span only decides *whether* to refresh, never *how many* to add, so there is no catch-up flood and no not-playing-to-hoard exploit.
    - **Concurrent-quest cap — population-scaled**: a refresh is a **top-up toward** a target, never a blind add — it adds `min(2-4 batch, target − activeCount, freeDogs)`, floored at 0, so once the neighborhood already holds the target number of uncompleted quests a refresh adds nothing until the player clears some. The target is `clamp(round(dogCount / 3), 3, 12)` — divisor, floor (3), and ceiling (12) are all named `EconomyNumbers` constants. Sample ramp: 8 dogs → 3, 12 → 4, 18 → 6, 24 → 8, 36 → 12, 100 → 12. The **ceiling (12) is the playtest flood-control dial** — drop it first if a mid-game map feels busy.
    - **Always one free quest — soft-lock protection**: to prevent a dead-end of 0 coins with every active quest requiring coins to accept, every refresh guarantees the post-refresh active set holds **≥1 free-type quest** (the no-cost types, LostItem / PestControl; the paid types are BuyGift / DecorationRequest). If a top-up would otherwise leave an all-paid set, one added quest is forced to a free type. This is enforced **only at the refresh boundary, never on quest completion** — instant replenish would be an unlimited-coin faucet. A temporary "no free quest" window of up to one interval is fine (completing anything frees a slot and the next refresh restores a free option); only *locked progress with no exit* is prevented.
    - **The pacing seam** (`QuestPacingPolicy`): the pacing decisions — `ShouldRefresh(nowUtc, state)` (cadence), `TargetActiveCount(state)` (cap), and `EligibleSubjectPool(tag, state)` (the population-gated purchasable subject pool — see [Cost tiers](#cost-tiers-population-gated-317)) — live in one Unity-independent Core class that `QuestManager` asks, rather than reading raw constants inline. All take `GameState`, so scaling rules driven by dog population change here only, never in the quest engine or the Unity layer.
    - **First launch is the exception**: the batch rotation is suppressed during [onboarding](../onboarding.md#first-launch-quest-seeding-312) — the world seeds exactly one easy lost-item quest so the tutorial has a single gentle target. Suppression now extends across the whole [onboarding reward-chain](../onboarding.md#onboarding-reward-chain-316): the first normal rotation is held back until the player finishes the four guided steps (quest → upgrade → expand → build), then released the moment the chain completes at the build step. The recurring 8h refresh ([#310](https://github.com/derekwinters/lucas-doggiehood/issues/310)) takes over from there. The single Core decision `QuestManager.EnsureQuestsForLaunch` picks the pre-chain seed / mid-chain suppression / post-chain refresh at each launch. ([#312](https://github.com/derekwinters/lucas-doggiehood/issues/312), [#316](https://github.com/derekwinters/lucas-doggiehood/issues/316))
- **Permanence**: items/decorations delivered or found through completed quests stay in the world permanently — nothing resets with the daily rotation. ([#27](https://github.com/derekwinters/lucas-doggiehood/issues/27))
- **No pressure**: quests never expire and there is no timer or fail condition anywhere. ([#28](https://github.com/derekwinters/lucas-doggiehood/issues/28))

## Currency

- **Source**: currency is earned only by completing dog requests/quests. No idle income, ads, or mini-games for v1.0. ([#24](https://github.com/derekwinters/lucas-doggiehood/issues/24))
- **Sinks**: quest gifts, decorations (both delivered via the normal quest flow — see [Decorations](../decorations.md)), and (in v0.4) new streets/houses ([Neighborhood Expansion](../expansion.md)). ([#25](https://github.com/derekwinters/lucas-doggiehood/issues/25))

### Numbers (placeholder, expect tuning)

*Source: [#62](https://github.com/derekwinters/lucas-doggiehood/issues/62)*

- A completed quest pays a **flat 10 coins**, regardless of quest type.
- A typical gift/decoration item costs **roughly 30-50 coins** (3-5 quests' worth of saving) — this is the **starter cost tier**, the only tier eligible early on (see [Cost tiers](#cost-tiers-population-gated-317) below).

Population-gated cost tiers *(decision 2026-07-28, Derek — see [#317](https://github.com/derekwinters/lucas-doggiehood/issues/317))*:

- Purchasable-quest difficulty scales with **total dog population** (`state.Dogs.Count`) — as the neighborhood grows, higher-cost tiers become eligible. All boundaries are draft placeholders, named `QuestCostTiers` constants (one-line tunes):

    | Tier | Cost band | Eligible once `Dogs.Count` ≥ |
    |------|-----------|------------------------------|
    | **Starter** | 30-50 coins (today's baseline) | 1 (always) |
    | **Mid** | 60-90 coins | 5 |
    | **Premium** | 100+ coins (no ceiling) | 10 |

v0.4 expansion sinks *(decisions 2026-07-14, Derek — see [Neighborhood Expansion](../expansion.md#pricing))*:

- Zone unlock: **100 coins** for the first zone, **+100 per subsequent zone** (100, 200, 300, …).
- Building a house: **50 coins**, flat.
- House upgrades: **100 / 200 / 400 coins** for levels 2 / 3 / 4.

Onboarding reward-chain *(decision 2026-07-28, Derek — see [Onboarding](../onboarding.md#onboarding-reward-chain-316))*:

- Each of the four guided steps pays a flat **100 coins** (`OnboardingRewardChainNumbers.RewardPerStep`), reusing the quest reward-payout path (a wallet deposit), not the random rotation. The ladder is **self-funding**: the 100 bonus covers the 100 upgrade, the upgrade reward covers the 100 expand, the expand reward covers the 50 build, and the player ends the chain with a small cushion. See [Neighborhood Expansion → pricing](../expansion.md#pricing) for the matching sink costs.

These are starting values, not final balance — expect to tune once the daily-rotation pacing can actually be felt in a playable build. Every one of them is a named constant in Core so playtesting adjustments are one-line changes.

## Item catalog

*Source: [#190](https://github.com/derekwinters/lucas-doggiehood/issues/190), decided in interview with Derek 2026-07-16.*

The priced catalog and every quest type's subject pool are **one source of truth**: a single tagged item catalog (`ItemCatalog`), not per-type hand-maintained lists. Each entry carries:

- **Name**
- **Cost** — optional. Purchasable items (gift/decoration) carry a cost in the 30-50 range above; find-only items (e.g. a lost puppy) carry none, since they're found rather than bought.
- **Eligibility tags** for which quest type(s) it can appear in — Lost, Gift, Decoration. An item can carry more than one tag (a toy or ball is both lost- and gift-eligible).

Each quest type's subject pool is a query over the catalog's tags (e.g. "every Gift-eligible item"), and the decoration-request options are the Decoration-eligible slice of the same catalog — there is no second, independently maintained item list anywhere. Adding a new item is a single catalog entry with its tags and cost; it then appears automatically in every rotation pool it's tagged for. The 30-50 coin rule is a tested invariant on every Gift- or Decoration-eligible catalog entry priced in the **starter tier**; a purchasable entry may instead sit in a higher [cost tier](#cost-tiers-population-gated-317) (mid 60-90, premium 100+), in which case the population gate holds it out of the pool until the neighborhood grows.

The **fence** ([#318](https://github.com/derekwinters/lucas-doggiehood/issues/318)) is the first such higher-tier entry: a `Gift`-tagged catalog item costing **100 coins** (`ItemCatalog.FenceItemName`/`FenceCost`), reusing the existing Gift eligibility tag — **no new quest type and no fourth eligibility tag**. Its 100-coin price places it in the **premium tier**, so `QuestPacingPolicy.EligibleSubjectPool` only offers it once the neighborhood reaches the premium population gate (~10 dogs) — that population gate *is* its "later game" gate, with no bespoke threshold. Unlike every other Gift subject (a package the delivery truck drops at the door), the fence has **no delivery flow**: accepting it deducts the 100 coins and completes the quest immediately — no delivery truck spawns, the dog does not walk home, and the fence becomes visible right away (no animation). Completion records a permanent `PlacedItem(houseId, "fence")` (the same persistence mechanism as any delivered gift, round-tripped by `SaveCodec`); the world's [backyard fence](../world/world.md#backyard-fences) for that lot then renders from that placed item.

### Cost tiers — population-gated (#317)

*Source: [#317](https://github.com/derekwinters/lucas-doggiehood/issues/317), decision 2026-07-28 (Derek).*

Purchasable quest difficulty ramps with the neighborhood's size so a new player is never offered an unaffordable request before banking coins. This is **new cost tiers within the existing 3 quest types**, *not* new quest mechanics — the [frozen-types rule](quest-content.md#v10-quest-types) holds. There is **no hidden level and no new persisted state**: total dog population (`state.Dogs.Count`) is already known, and drives everything.

- A **cost tier** is a cost band over the existing item catalog — nothing but a classification of entries already priced. The bands and the population gate that unlocks each are in the [Numbers § table](#numbers-placeholder-expect-tuning) above.
- Eligibility is **cumulative and monotonic** — reaching a higher population only ever *adds* tiers to the eligible pool, never removes the cheaper ones, so an established player still gets affordable requests mixed in. The pure `QuestCostTiers.EligibleCostTiers(dogCount)` function computes this.
- The gate filters only the **purchasable subject pool** — BuyGift subjects and decoration-request options. Catalog entries priced above the population-eligible ceiling are excluded from the candidate set, and the injectable RNG picks only from the eligible slice (deterministic per seed). **Find-only LostItem subjects (no cost) and PestControl (no item cost) are unaffected.**
- At the starting population the eligible pool is **identical to today's** (starter tier only), so onboarding and early game are unchanged.
- The filter is consumed through the [pacing seam](#core-loop)'s `QuestPacingPolicy.EligibleSubjectPool(tag, state)`, which feeds the live `ItemCatalog` and `state.Dogs.Count` into `QuestCostTiers` — one population source of truth shared with [#310](https://github.com/derekwinters/lucas-doggiehood/issues/310)'s availability pacing, not a second parallel signal.

## Quest authoring

*Source: [#61](https://github.com/derekwinters/lucas-doggiehood/issues/61), implementation tracked in [#69](https://github.com/derekwinters/lucas-doggiehood/issues/69). Line-variety model ("Model 2") decided in interview with Derek 2026-07-16, tracked in [#189](https://github.com/derekwinters/lucas-doggiehood/issues/189).*

Quests are authored as reusable **templates**, not hand-written per dog. Each quest type (see [Quest Content](quest-content.md)) has a dialogue template with variable slots:

- Dog name
- Item/subject (toy name, decoration type, pest type, etc.)
- Personality ([Dog Behavior](../dogs/behavior.md)) — seasons which opener/closer line is drawn, per the pooled model below

New quest types (mechanics) beyond the existing set stay deferred to a later version — this section is only about line variety within the existing types, not new mechanics.

### Line variety: pooled openers/closers, uniform random ("Model 2")

For both the opener and the closer, each quest type holds two pools:

- A **default pool** of personality-agnostic lines (authoring target ~10-15 lines).
- A small **per-personality pool** of personality-specific lines, for whichever personalities we choose to flavor (authoring target ~3-4 lines per personality; 0 is valid — flavoring a personality is optional per type).

When a quest fires, the candidate set is `default pool ∪ this dog's personality pool`, and one line is picked **uniformly at random per string** (not per bucket) — so with ~15 defaults and ~3 Grumpy-specific lines, a Grumpy dog says a Grumpy-specific line only ~1-in-6 of the time. Personality is seasoning, not the default voice; wanting more personality presence means writing more personality-specific lines, never special-casing the weighting. Uniform-per-string is a hard requirement — no single line variant may be allowed to dominate.

Selection is **pure random each fire**: no "avoid immediate repeat" memory, no cycle-through, no per-dog or per-session persisted state. The RNG is injectable (constructor/method-seeded `System.Random`), matching the pattern used by the [move-in system](../expansion.md#move-in-system), so line selection stays deterministic under test.

This generalizes what earlier drafts did with a single default line and a single flavored line per personality — same slots, same personality-driven flavoring, just pools instead of one-liners so a given dog doesn't say the identical sentence every time.

**Active-quest reminder pool ([#472](https://github.com/derekwinters/lucas-doggiehood/issues/472)).** Each quest type carries a third pooled line set — a **reminder** — under the identical Model 2 rules (default pool ∪ this dog's personality pool, uniform-random per string, `{dog}`/`{item}` slots, injectable RNG, no persisted state). It renders as a single line, shown when a dog with an already-`Accepted` quest is re-tapped (see [Conversation System](conversation-system.md) and the [conversation panel](../ui/conversation-panel.md)); the opener/closer offer is unchanged.

## Build checklist

- [ ] Currency balance persists across sessions
- [ ] Completing any quest grants a flat 10 coins
- [ ] Spending currency on a gift/decoration deducts its cost (30-50 coin range) and fails gracefully if the player can't afford it
- [x] A rotation system tops up 2-4 dogs at a time toward a population-scaled cap (`clamp(round(dogCount/3), 3, 12)`), refreshing on an 8-hour UTC cadence, guaranteeing at least one free-type quest so the player is never soft-locked — the cadence and cap both live behind the `QuestPacingPolicy` seam ([#310](https://github.com/derekwinters/lucas-doggiehood/issues/310))
- [ ] Completed quest state (delivered items, decorations) persists permanently — no reset logic tied to the daily rotation
- [ ] No quest has a timer, expiration, or fail state anywhere in the system
- [ ] A quest template data structure exists with slots for dog name, personality-flavored line variant, and item/subject
- [ ] At least the 3 v1.0 quest types (see [Quest Content](quest-content.md)) are expressed as templates, not hard-coded per-dog text
- [x] Opener and closer lines are drawn from a default pool ∪ per-personality pool, uniform-random per string, via an injectable RNG — no anti-repeat memory or per-dog persisted state ([#189](https://github.com/derekwinters/lucas-doggiehood/issues/189))
- [x] All quest subject pools (and decoration-request options) are queries over one tagged item catalog — no per-type parallel item lists; every Gift/Decoration-eligible entry costs 30-50 coins, find-only entries carry no cost ([#190](https://github.com/derekwinters/lucas-doggiehood/issues/190))
- [x] Purchasable-quest cost tiers are gated by total dog population — `QuestCostTiers.EligibleCostTiers(dogCount)` is monotonic (starter-only at 1, +mid at 5, +premium at 10), the pacing seam's `EligibleSubjectPool` excludes catalog entries above the population-eligible ceiling, and LostItem/PestControl are unaffected; no new persisted state ([#317](https://github.com/derekwinters/lucas-doggiehood/issues/317))
- [x] The fence is a `Gift`-tagged, 100-coin (premium-tier) catalog entry — offered only at ≥10 dogs via the population gate; accepting it deducts the cost and completes immediately with **no delivery truck and no walk-home**, recording a permanent `PlacedItem(houseId, "fence")` that drives the lot's [backyard fence](../world/world.md#backyard-fences) visibility and round-trips through `SaveCodec` ([#318](https://github.com/derekwinters/lucas-doggiehood/issues/318))
