# Onboarding

*Epic: [#18](https://github.com/derekwinters/lucas-doggiehood/issues/18)*

The first time the game is opened, the first dog with a speech bubble walks the player through panning/zooming, tapping the speech bubble, and completing one simple quest — teaching the core loop naturally, without a separate tutorial screen. ([#44](https://github.com/derekwinters/lucas-doggiehood/issues/44))

The guidance is presented as a slim **bottom-center coach prompt** (not a top banner) that is layered over live gameplay, never blocks input, advances itself through the four steps as the player performs each real action, and auto-dismisses once the first quest is complete. Its layout — regions, anchors, and named size/margin constants — is the approved wireframe in [UI Wireframes → Onboarding overlay](ui/onboarding-overlay.md) ([#176](https://github.com/derekwinters/lucas-doggiehood/issues/176)). The old graybox top-banner rendering in `OnboardingOverlay` has been replaced by that bottom-center coach prompt, and the prompt now correctly advances on the real pan/zoom/tap interactions and dismisses after the first quest completes ([#207](https://github.com/derekwinters/lucas-doggiehood/issues/207)).

## First-launch quest seeding ([#312](https://github.com/derekwinters/lucas-doggiehood/issues/312))

On the very first launch the world seeds **exactly one** dog with a single easy **lost-item** quest, and the normal 2-4 daily [rotation](quests/economy.md#core-loop) is **suppressed until onboarding completes**. This guarantees the tutorial has one and only one gentle tap-to-find target — the `OnboardingSequence` resolves that seeded dog as its `TargetDog`, so step 4 ("complete a quest") is always the low-friction lost-item tap rather than, say, a purchase or pest-control errand competing for attention.

The branch lives in Core (`QuestManager.BeginInitialQuests(rng)`): with onboarding incomplete it seeds the one lost-item quest and returns; once onboarding is complete it is just the normal `StartNewDay` rotation. The thin Unity layer (`WorldBootstrap`) calls the seam behind its existing "no active quests" guard — no game logic in the `MonoBehaviour`.

When onboarding finishes, its `Done` transition now grants **step 1 of the [onboarding reward-chain](#onboarding-reward-chain-316)** (a 100-coin completion bonus) rather than starting the daily rotation. The normal rotation stays suppressed across the rest of the guided chain and is released only when the chain completes at its final (build) step — at which point recurring rotation ([#310](https://github.com/derekwinters/lucas-doggiehood/issues/310)) takes over. ([#316](https://github.com/derekwinters/lucas-doggiehood/issues/316))

## Onboarding reward-chain ([#316](https://github.com/derekwinters/lucas-doggiehood/issues/316))

*Design settled with Derek, review session 2026-07-28.*

Immediately after the first-quest tutorial, a **one-time, first-run scripted reward-chain** walks a new player through every core early mechanic and seeds enough coins that they never stall. It is a fixed four-step sequence — **not** the random rotation — that fires each step **exactly once, in guaranteed order**, paying a flat **100 coins** per step (`OnboardingRewardChainNumbers.RewardPerStep`) by reusing the quest reward-payout path (a wallet deposit):

1. **Complete the first quest** (samples the quest loop) → 100 bonus. Core entry: `GameState.GrantOnboardingCompletionReward`, fired by `OnboardingSequence` when the guided quest completes.
2. **Upgrade a house** (L1 → L2, cost 100) → 100. Core entry: `GameState.TryUpgradeHouse`.
3. **Expand the map** (first zone, cost 100) → 100. Core entry: `GameState.TryUnlockNextZone`; the player-facing lock-icon trigger lands with [#343](https://github.com/derekwinters/lucas-doggiehood/issues/343)/[#344](https://github.com/derekwinters/lucas-doggiehood/issues/344).
4. **Build a house** on the newly unlocked lot (cost 50) → 100. Core entry: `GameState.TryBuildHouse`.

**Self-funding ladder.** The 100 bonus covers the 100 upgrade; the upgrade reward covers the 100 expand; the expand reward covers the 50 build; and because the build costs only 50, the player ends the chain with a small cushion. The costs are the shipped v0.4 sink prices — see [Neighborhood Expansion → pricing](expansion.md#pricing) and [Economy → Numbers](quests/economy.md#numbers-placeholder-expect-tuning).

**Ordering and one-time guarantee.** The chain (`OnboardingRewardChain`, engine-free Core) tracks the step it is waiting on. A tracked action taken out of turn — or repeated after it already paid — neither pays nor advances; the chain simply keeps waiting on its current step. Progress round-trips through the save (`SaveCodec`), so the chain resumes where it left off and is never restarted or re-paid on reload; a pre-#316 save that had already finished onboarding is treated as a completed chain.

**Rotation handoff (#312 → #310).** The normal quest rotation stays suppressed while the chain is in progress and is released **exactly when step 4 (build) completes**, at which point the recurring #310 pacing takes over — no rotation is seeded mid-chain. The single Core decision `QuestManager.EnsureQuestsForLaunch` applies this at every launch (pre-chain seed → mid-chain suppression → post-chain refresh), so the thin Unity bootstrap carries no pacing logic.

## Step-gated speech bubble & self-heal ([#329](https://github.com/derekwinters/lucas-doggiehood/issues/329))

The four steps run in order — `Pan → Zoom → TapBubble → CompleteQuest → Done` — and the "tap the speech bubble" action is gated to its own step so each step is the obvious next thing to do:

- **The target dog's speech bubble is hidden until the `TapBubble` step.** While onboarding is running, the seeded dog's bubble does not appear (and cannot be tapped) during `Pan`/`Zoom`; it becomes visible and tappable for the first time exactly when the flow reaches `TapBubble`. The gating decision lives in Core (`OnboardingSequence.ShouldSuppressBubble`); the thin Unity layer (`DogView` via `OnboardingOverlay.SuppressesBubbleFor`) just consults it. Only the target dog is gated — other dogs are unaffected (and during the single-seeded onboarding, #312, there are none). Once onboarding is complete, bubbles follow their normal `HasActiveQuest` binding.
- **The `TapBubble` step self-heals if the interaction already happened.** Should the conversation somehow have been opened early (or the quest already accepted/resolved before the step), the sequence does not strand. `OnboardingSequence` remembers that the target dog's conversation was opened even if it fired before `TapBubble`, and on entering `TapBubble` — or on any overlay poll (`OnboardingSequence.Reconcile`) — it auto-advances when the conversation was already opened or the target dog's quest is no longer `Available`, cascading on through `CompleteQuest`/`Done` when the quest is already fully resolved. This closes the soft-lock where an early bubble tap left the coach bar pinned on `TapBubble` forever.
- **The conversation dialog is always dismissible.** The "Not now" decline ([#185](https://github.com/derekwinters/lucas-doggiehood/issues/185)) stays reachable in every onboarding state, so no path leaves the dialog stuck open.

## Build checklist

- [ ] First-launch state is tracked (so onboarding only runs once)
- [ ] Guided prompts cover: panning, zooming, tapping a speech bubble, and completing a quest through to reward
- [ ] Onboarding uses the real game systems (an actual dog, actual quest, actual reward) rather than a separate scripted tutorial scene
- [ ] No blocking modal tutorial screen — guidance is layered over live gameplay
