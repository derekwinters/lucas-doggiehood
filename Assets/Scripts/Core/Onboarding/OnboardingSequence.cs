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
        private readonly Random rng;

        public Dog TargetDog { get; }
        public OnboardingStep CurrentStep { get; private set; }

        public OnboardingSequence(GameState state)
            : this(state, new Random())
        {
        }

        /// <summary>#312: the onboarding-completion handoff kicks off the first
        /// normal daily rotation (<see cref="QuestManager.StartNewDay"/>), so
        /// the RNG is injectable for deterministic tests — matching
        /// <see cref="QuestManager"/>'s pattern.</summary>
        public OnboardingSequence(GameState state, Random rng)
        {
            this.state = state;
            this.rng = rng;
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
            }
        }

        public void NotifyConversationOpened(Dog dog)
        {
            if (CurrentStep == OnboardingStep.TapBubble && dog == TargetDog)
            {
                CurrentStep = OnboardingStep.CompleteQuest;
            }
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
        /// per #312, hands off to the normal daily rotation by kicking off the
        /// first <see cref="QuestManager.StartNewDay"/> — the onboarding-time
        /// single-lost-item suppression no longer applies. Guarded by the
        /// callers' <see cref="OnboardingStep.CompleteQuest"/> check so it runs
        /// exactly once; #310 owns recurrence beyond this first rotation.</summary>
        private void Complete()
        {
            CurrentStep = OnboardingStep.Done;
            state.MarkOnboardingComplete();
            state.Quests.StartNewDay(rng);
        }
    }
}
