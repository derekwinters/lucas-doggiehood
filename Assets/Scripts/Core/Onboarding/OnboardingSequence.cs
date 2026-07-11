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

        public Dog TargetDog { get; }
        public OnboardingStep CurrentStep { get; private set; }

        public OnboardingSequence(GameState state)
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
                CurrentStep = OnboardingStep.Done;
                state.MarkOnboardingComplete();
            }
        }

        public void NotifyQuestCompleted(Quest quest)
        {
            if (CurrentStep == OnboardingStep.CompleteQuest
                && quest.DogName == TargetDog.Name
                && quest.Status == QuestStatus.Completed)
            {
                CurrentStep = OnboardingStep.Done;
                state.MarkOnboardingComplete();
            }
        }
    }
}
