using System;
using System.Linq;
using Doggiehood.Core.Dogs;
using Doggiehood.Core.Quests;
using Doggiehood.Core.World;

namespace Doggiehood.Core.Onboarding
{
    public enum OnboardingStep
    {
        Pan,
        Zoom,
        TapBubble,
        CompleteQuest,
        Done,
    }

    /// <summary>
    /// The guided first-quest tutorial (#44): teaches pan, zoom, tapping a
    /// speech bubble, and completing one quest — using a real dog with a
    /// real quest from the live rotation, rewarded through the standard
    /// payout path. Runs once ever; the flag persists in the save.
    /// Prompts layer over live gameplay — there is no tutorial scene.
    /// </summary>
    public sealed class OnboardingSequence
    {
        private readonly GameState state;

        /// <summary>#329: remembers that the target dog's conversation was
        /// opened at least once, even if the open happened before the
        /// <see cref="OnboardingStep.TapBubble"/> step. Without this an early
        /// open (during Pan/Zoom) is silently dropped and the step later
        /// strands with "nothing to tap".</summary>
        private bool conversationOpenedForTargetDog;

        public Dog TargetDog { get; }
        public OnboardingStep CurrentStep { get; private set; }

        public OnboardingSequence(GameState state)
            : this(state, new Random())
        {
        }

        /// <summary>The RNG overload is retained for call-site compatibility
        /// (deterministic tests, matching <see cref="QuestManager"/>'s pattern).
        /// Since #316 the completion handoff grants the reward-chain bonus
        /// rather than seeding a rotation, so the sequence no longer needs the
        /// RNG itself — the parameter is accepted but unused.</summary>
        public OnboardingSequence(GameState state, Random rng)
        {
            this.state = state;
            TargetDog = state.Dogs.FirstOrDefault(d => d.HasActiveQuest);
            CurrentStep = OnboardingStep.Pan;
        }

        public static bool ShouldRun(GameState state)
        {
            return !state.OnboardingComplete;
        }

        public void NotifyPanned()
        {
            if (CurrentStep == OnboardingStep.Pan)
            {
                CurrentStep = OnboardingStep.Zoom;
            }
        }

        public void NotifyZoomed()
        {
            if (CurrentStep == OnboardingStep.Zoom)
            {
                CurrentStep = OnboardingStep.TapBubble;

                // #329: self-heal on arrival — if the bubble interaction
                // already happened during Pan/Zoom, don't strand on a step
                // with nothing left to do.
                Reconcile();
            }
        }

        public void NotifyConversationOpened(Dog dog)
        {
            if (dog != TargetDog)
            {
                return;
            }

            // #329: remember the open even if it happens before the TapBubble
            // step, so an early tap during Pan/Zoom isn't discarded and can
            // self-heal the step on arrival.
            conversationOpenedForTargetDog = true;

            if (CurrentStep == OnboardingStep.TapBubble)
            {
                CurrentStep = OnboardingStep.CompleteQuest;
            }
        }

        /// <summary>#329: whether the target dog's speech bubble should stay
        /// hidden (and untappable) right now. During onboarding the bubble is
        /// gated to its <see cref="OnboardingStep.TapBubble"/> step, so the
        /// player can't open the conversation during Pan/Zoom and strand the
        /// flow — the tap becomes the third guided action. Only the target dog
        /// is gated; any other dog (there are none during the single-seeded
        /// onboarding, #312) is never suppressed, and nothing is suppressed
        /// once the flow reaches TapBubble or completes.</summary>
        public bool ShouldSuppressBubble(Dog dog)
        {
            return dog == TargetDog
                && (CurrentStep == OnboardingStep.Pan || CurrentStep == OnboardingStep.Zoom);
        }

        /// <summary>#329 self-heal: if the flow is sitting on
        /// <see cref="OnboardingStep.TapBubble"/> but the bubble interaction
        /// has effectively already happened — the target dog's conversation
        /// was opened once, or its quest has left <c>Available</c> (accepted
        /// early) so there is nothing left to re-present — advance past
        /// TapBubble instead of stranding. Cascades on through
        /// CompleteQuest/Done when the quest is already fully resolved.
        /// Idempotent and safe to call on every poll.</summary>
        public void Reconcile()
        {
            if (CurrentStep == OnboardingStep.TapBubble
                && TargetDog != null
                && (conversationOpenedForTargetDog || !TargetDogHasAvailableQuest()))
            {
                CurrentStep = OnboardingStep.CompleteQuest;
            }

            if (CurrentStep == OnboardingStep.CompleteQuest)
            {
                NotifyTargetDogQuestResolved();
            }
        }

        private bool TargetDogHasAvailableQuest()
        {
            return TargetDog != null
                && state.Quests.ActiveQuests.Any(q =>
                    q.DogName == TargetDog.Name && q.Status == QuestStatus.Available);
        }

        /// <summary>Observer-friendly completion signal: the target dog's
        /// quest cleared through the standard path.</summary>
        public void NotifyTargetDogQuestResolved()
        {
            if (CurrentStep == OnboardingStep.CompleteQuest
                && TargetDog != null
                && !TargetDog.HasActiveQuest)
            {
                Complete();
            }
        }

        public void NotifyQuestCompleted(Quest quest)
        {
            if (CurrentStep == OnboardingStep.CompleteQuest
                && quest.DogName == TargetDog.Name
                && quest.Status == QuestStatus.Completed)
            {
                Complete();
            }
        }

        /// <summary>The one Done transition: marks onboarding complete and,
        /// per #316, grants step 1 of the onboarding reward chain (the
        /// completion bonus) instead of starting the daily rotation. The
        /// rotation now stays suppressed while the guided chain runs
        /// (upgrade -> expand -> build) and is released only when the chain
        /// completes at the build step — the #312 -> #310 handoff. Guarded by
        /// the callers' <see cref="OnboardingStep.CompleteQuest"/> check so it
        /// runs exactly once.</summary>
        private void Complete()
        {
            CurrentStep = OnboardingStep.Done;
            state.MarkOnboardingComplete();
            // #469: hand the first-quest dog's house to the reward chain so the
            // following "upgrade a house" step is scoped to it. TargetDog is
            // non-null here — the flow only reaches Done through CompleteQuest,
            // which required a real TargetDog.
            state.GrantOnboardingCompletionReward(TargetDog.HouseId);
        }
    }
}
