# Conversation System

*Epic: [#4](https://github.com/derekwinters/lucas-doggiehood/issues/4)*

## Discovery

Dogs that have something to say show a speech bubble icon above them. ([#10](https://github.com/derekwinters/lucas-doggiehood/issues/10)) This is the **sole** way quests are surfaced for MVP — there is no separate quest log or journal screen. ([#32](https://github.com/derekwinters/lucas-doggiehood/issues/32))

## Starting a conversation

Clicking/tapping a dog's speech bubble opens the conversation UI with that dog. ([#11](https://github.com/derekwinters/lucas-doggiehood/issues/11))

## Dialogue structure

Conversations are a simple linear back-and-forth: the dog explains its request, the player taps to accept, done. **No branching dialogue choices for MVP.** ([#33](https://github.com/derekwinters/lucas-doggiehood/issues/33))

Dialogue lines are generated from templates rather than hand-written per dog — see [Quest & Economy](economy.md#quest-authoring) for how template content is structured and personality-flavored.

## Build checklist

- [ ] Dogs with an active quest display a speech bubble icon above them at all times until resolved
- [ ] Tapping the speech bubble opens a conversation UI scoped to that dog
- [ ] Conversation UI presents the dog's request as linear text with a single accept/complete action — no branching choice tree
- [ ] No quest log/journal screen exists; the speech bubble is the only quest-discovery mechanism
