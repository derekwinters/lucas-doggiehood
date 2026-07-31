using System;
using Doggiehood.Core.Onboarding;

namespace Doggiehood.Unity
{
    /// <summary>
    /// #372: the Unity-layer step-to-message table for the onboarding reward
    /// celebration panel (docs/specs/ui/onboarding-reward.md). Copy stays OUT of
    /// engine-free Core (rule #2): Core's reward event names only the completed
    /// <see cref="OnboardingRewardStep"/> + amount, and this table turns that
    /// step into the exact approved accomplishment line the
    /// <see cref="OnboardingRewardPanel"/> shows.
    ///
    /// <para>Only the four paying steps have copy; the terminal
    /// <see cref="OnboardingRewardStep.Done"/> is never a completed step, so
    /// asking for its message is a programming error and fails loudly rather than
    /// rendering blank text.</para>
    /// </summary>
    public static class OnboardingRewardCopy
    {
        // Per-step accomplishment lines — verbatim from the approved wireframe
        // (#374, docs/specs/ui/onboarding-reward.md → "Per-step copy").
        private const string FirstQuestMessage = "You finished your first quest!";
        private const string UpgradeHouseMessage = "You made a house even nicer!";
        private const string ExpandMapMessage = "You opened up a brand-new street!";
        private const string BuildHouseMessage = "You built a brand-new house!";

        /// <summary>The approved accomplishment line for a just-completed
        /// <paramref name="step"/>. Throws for any step without copy (notably
        /// <see cref="OnboardingRewardStep.Done"/>) so a missing mapping surfaces
        /// immediately instead of as an empty message.</summary>
        public static string MessageFor(OnboardingRewardStep step)
        {
            switch (step)
            {
                case OnboardingRewardStep.FirstQuest:
                    return FirstQuestMessage;
                case OnboardingRewardStep.UpgradeHouse:
                    return UpgradeHouseMessage;
                case OnboardingRewardStep.ExpandMap:
                    return ExpandMapMessage;
                case OnboardingRewardStep.BuildHouse:
                    return BuildHouseMessage;
                default:
                    throw new ArgumentOutOfRangeException(nameof(step), step,
                        "No onboarding reward copy for this step (it is not a paying reward step).");
            }
        }
    }
}
