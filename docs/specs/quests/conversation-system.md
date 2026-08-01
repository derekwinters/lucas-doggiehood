# Conversation System

*Epic: [#4](https://github.com/derekwinters/lucas-doggiehood/issues/4)*

## Discovery

Dogs that have something to say show a speech bubble icon above them. ([#10](https://github.com/derekwinters/lucas-doggiehood/issues/10)) This is the **sole** way quests are surfaced for v1.0 — there is no separate quest log or journal screen. ([#32](https://github.com/derekwinters/lucas-doggiehood/issues/32))

## Starting a conversation

Clicking/tapping a dog's speech bubble opens the conversation UI with that dog. ([#11](https://github.com/derekwinters/lucas-doggiehood/issues/11))

## Dialogue structure

Conversations are a simple linear back-and-forth: the dog explains its request, and the player either **accepts** it or **declines**. The panel presents an accept/complete action alongside a non-punishing "Not now" decline that dismisses the panel and can be reopened later (tapping outside the panel counts as declining); see the [conversation panel wireframe](../ui/conversation-panel.md) ([#175](https://github.com/derekwinters/lucas-doggiehood/issues/175), [#185](https://github.com/derekwinters/lucas-doggiehood/issues/185)). A decline is an exit, not a dialogue branch — **there are still no branching dialogue choices for v1.0.** ([#33](https://github.com/derekwinters/lucas-doggiehood/issues/33))

Dialogue lines are generated from templates rather than hand-written per dog — see [Quest & Economy](economy.md#quest-authoring) for how template content is structured and personality-flavored. The opener and closer are each drawn at random from a pool (default lines plus the dog's personality-specific lines) when the quest fires ([#189](https://github.com/derekwinters/lucas-doggiehood/issues/189)) — still a single linear exchange, no branching, just non-repetitive wording.

**Re-tapping an active quest ([#472](https://github.com/derekwinters/lucas-doggiehood/issues/472)).** The speech bubble stays up while an accepted quest is in progress, so a dog can be re-tapped before its quest is done. That re-tap shows a **contextual reminder** of the current quest (e.g. *"Any sign of my {item} yet?"*) — a single line drawn from the same pooled, personality-flavored template model as the opener/closer, just a third pool (the **reminder** pool) rather than a fresh offer. The reminder is **dismiss-only**: its lone action is a **"Still looking"** pill that is mechanically identical to the non-punishing "Not now" decline — it closes the panel, leaves the quest exactly as it was (still `Accepted`), and is fully re-openable, with no give-up/cancel path (quest cancellation is deferred — [#479](https://github.com/derekwinters/lucas-doggiehood/issues/479)). Before this, a re-tapped active quest fell through to a leftover placeholder line; the reminder replaces it.

**Buying something ([#186](https://github.com/derekwinters/lucas-doggiehood/issues/186)).** Accepting a buy-something or decoration quest is the same accept action, not a separate confirm-purchase step — the panel just surfaces what that accept spends: the accept/option pill shows the item's cost and greys out when it isn't affordable (see the [conversation panel wireframe](../ui/conversation-panel.md)). The actual spend is re-checked at accept time against [Quest & Economy](economy.md)'s "fails gracefully" rule; if it's rejected, the panel stays open with an insufficient-funds message instead of closing as if nothing happened.

## Build checklist

- [ ] Dogs with an active quest display a speech bubble icon above them at all times until resolved
- [ ] Tapping the speech bubble opens a conversation UI scoped to that dog
- [ ] Conversation UI presents the dog's request as linear text with an accept/complete action or a non-punishing decline; still no branching choice tree
- [ ] Re-tapping a dog whose quest is already accepted shows a contextual reminder line (from the pooled reminder template set) with a dismiss-only "Still looking" action that keeps the quest active and re-openable — never a leftover placeholder, and no give-up/cancel path ([#472](https://github.com/derekwinters/lucas-doggiehood/issues/472))
- [ ] The conversation panel is always dismissable without accepting (a "Not now" / tap-outside decline), and Accept resolves exactly one quest at a time — it never runs through the whole message queue ([#221](https://github.com/derekwinters/lucas-doggiehood/issues/221))
- [ ] No quest log/journal screen exists; the speech bubble is the only quest-discovery mechanism
