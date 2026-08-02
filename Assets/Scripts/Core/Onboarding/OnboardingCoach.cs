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

        // #451 per-phase title copy for the coach bar's phase-title tab, distilled
        // into the onboarding-overlay spec's "Phase-title region" table (do not
        // invent new copy). One title per onboarding PHASE, not per step. Declared
        // as named constants per the no-inline-literals rule (#161).
        public const string LearnTheRopesTitle = "Learn the ropes";
        public const string FixUpAHomeTitle = "Fix up a home";
        public const string GrowTheNeighborhoodTitle = "Grow the neighborhood";
        public const string BuildHouseTitle = "Build a house";

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

        /// <summary>#451: the phase-title tab's label for the current onboarding
        /// <b>phase</b> (not step), keyed the same way <see cref="ShouldShow"/>
        /// combines the two state machines. While the first-quest sequence runs
        /// (<paramref name="sequenceStep"/> not <see cref="OnboardingStep.Done"/>)
        /// every tutorial step shows the one Tutorial-phase title; once the
        /// sequence is Done the reward-chain step names its own phase. Empty once
        /// everything is Done (the bar is dismissed then). Copy lives here in
        /// Core; the thin <c>OnboardingOverlay</c> only renders it.</summary>
        public static string PhaseTitle(OnboardingStep sequenceStep, OnboardingRewardStep rewardStep)
        {
            if (sequenceStep != OnboardingStep.Done)
            {
                return LearnTheRopesTitle;
            }

            switch (rewardStep)
            {
                case OnboardingRewardStep.UpgradeHouse:
                    return FixUpAHomeTitle;
                case OnboardingRewardStep.ExpandMap:
                    return GrowTheNeighborhoodTitle;
                case OnboardingRewardStep.BuildHouse:
                    return BuildHouseTitle;
                default:
                    return string.Empty;
            }
        }

        /// <summary>Whether the coach bar should still show: true while the
        /// first-quest sequence is running (<paramref name="sequenceStep"/> not
        /// <see cref="OnboardingStep.Done"/>) OR the reward chain has not yet
        /// completed. Dismissal is gated on the reward chain finishing at the
        /// build step (#371) — not on the first-quest sequence alone.
        ///
        /// <para>#506: a centered modal panel (e.g. the house profile the Upgrade
        /// step tells the player to open) covers the bottom-anchored coach bar and
        /// the very button it points at, so while such a panel is open the bar is
        /// suppressed outright — <paramref name="centeredPanelOpen"/> wins over the
        /// step state. Suppression is not a step advance: closing the panel restores
        /// whatever the bar would otherwise show, and a chain that has already
        /// completed stays dismissed regardless.</para></summary>
        public static bool ShouldShow(
            OnboardingStep sequenceStep,
            OnboardingRewardStep rewardStep,
            bool centeredPanelOpen = false)
        {
            return !centeredPanelOpen
                && (sequenceStep != OnboardingStep.Done
                    || rewardStep != OnboardingRewardStep.Done);
        }
    }
}
