# Onboarding

*Epic: [#18](https://github.com/derekwinters/lucas-doggiehood/issues/18)*

The first time the game is opened, the first dog with a speech bubble walks the player through panning/zooming, tapping the speech bubble, and completing one simple quest — teaching the core loop naturally, without a separate tutorial screen. ([#44](https://github.com/derekwinters/lucas-doggiehood/issues/44))

The guidance is presented as a slim **bottom-center coach prompt** (not a top banner) that is layered over live gameplay, never blocks input, advances itself through the four steps as the player performs each real action, and auto-dismisses once the first quest is complete. Its layout — regions, anchors, and named size/margin constants — is the approved wireframe in [UI Wireframes → Onboarding overlay](ui/onboarding-overlay.md) ([#176](https://github.com/derekwinters/lucas-doggiehood/issues/176)). The old graybox top-banner rendering in `OnboardingOverlay` has been replaced by that bottom-center coach prompt, and the prompt now correctly advances on the real pan/zoom/tap interactions and dismisses after the first quest completes ([#207](https://github.com/derekwinters/lucas-doggiehood/issues/207)).

## First-launch quest seeding ([#312](https://github.com/derekwinters/lucas-doggiehood/issues/312))

On the very first launch the world seeds **exactly one** dog with a single easy **lost-item** quest, and the normal 2-4 daily [rotation](quests/economy.md#core-loop) is **suppressed until onboarding completes**. This guarantees the tutorial has one and only one gentle tap-to-find target — the `OnboardingSequence` resolves that seeded dog as its `TargetDog`, so step 4 ("complete a quest") is always the low-friction lost-item tap rather than, say, a purchase or pest-control errand competing for attention.

The branch lives in Core (`QuestManager.BeginInitialQuests(rng)`): with onboarding incomplete it seeds the one lost-item quest and returns; once onboarding is complete it is just the normal `StartNewDay` rotation. The thin Unity layer (`WorldBootstrap`) calls the seam behind its existing "no active quests" guard — no game logic in the `MonoBehaviour`.

When onboarding finishes, its `Done` transition kicks off the **first** normal daily rotation exactly once (2-4 other dogs get quests) — the handoff point to recurring rotation ([#310](https://github.com/derekwinters/lucas-doggiehood/issues/310)), which owns once-per-day recurrence beyond that first rotation.

## Build checklist

- [ ] First-launch state is tracked (so onboarding only runs once)
- [ ] Guided prompts cover: panning, zooming, tapping a speech bubble, and completing a quest through to reward
- [ ] Onboarding uses the real game systems (an actual dog, actual quest, actual reward) rather than a separate scripted tutorial scene
- [ ] No blocking modal tutorial screen — guidance is layered over live gameplay
