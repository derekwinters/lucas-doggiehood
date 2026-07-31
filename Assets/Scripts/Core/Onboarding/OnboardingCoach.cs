namespace Doggiehood.Core.Onboarding
{
    /// <summary>
    /// #371: the engine-free guidance decision behind the single onboarding
    /// coach bar. Per the approved standard-coverage wireframe (#374,
    /// docs/specs/ui/onboarding-overlay.md), one coach surface guides the whole
    /// journey — the first-quest <see cref="OnboardingSequence"/> (pan -> zoom
    /// -> tap bubble -> complete quest) AND the follow-on
    /// <see cref="OnboardingRewardChain"/> (upgrade -> expand -> build). This
    /// type decides, from the two Core states, whether the bar should still show
    /// and which reward-step prompt to display; the thin
    /// <c>OnboardingOverlay</c> only renders the result.
    /// </summary>
    public static class OnboardingCoach
    {
        // Accepted #374 step copy, distilled into the onboarding-overlay spec's
        // step table (do not invent new copy). Declared as named constants per
        // the no-inline-literals rule (#161).
        public const string UpgradeHousePrompt = "Tap a house, then Upgrade to make it even nicer!";
        public const string ExpandMapPrompt = "Tap the glowing lock to open up a new street!";
        public const string BuildHousePrompt = "Tap the empty lot to build a new house!";

        /// <summary>The coach prompt for a reward-chain step. Empty for
        /// <see cref="OnboardingRewardStep.FirstQuest"/> (owned by the
        /// first-quest sequence's own four prompts) and
        /// <see cref="OnboardingRewardStep.Done"/> (nothing left to guide).</summary>
        public static string PromptForRewardStep(OnboardingRewardStep step)
        {
            switch (step)
            {
                case OnboardingRewardStep.UpgradeHouse:
                    return UpgradeHousePrompt;
                case OnboardingRewardStep.ExpandMap:
                    return ExpandMapPrompt;
                case OnboardingRewardStep.BuildHouse:
                    return BuildHousePrompt;
                default:
                    return string.Empty;
            }
        }

        /// <summary>Whether the coach bar should still show: true while the
        /// first-quest sequence is running (<paramref name="sequenceStep"/> not
        /// <see cref="OnboardingStep.Done"/>) OR the reward chain has not yet
        /// completed. Dismissal is gated on the reward chain finishing at the
        /// build step (#371) — not on the first-quest sequence alone.</summary>
        public static bool ShouldShow(OnboardingStep sequenceStep, OnboardingRewardStep rewardStep)
        {
            return sequenceStep != OnboardingStep.Done
                || rewardStep != OnboardingRewardStep.Done;
        }
    }
}
