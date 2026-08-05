using Doggiehood.Core.World;

namespace Doggiehood.Core.Onboarding
{
    /// <summary>
    /// #571: the Unity-independent decision for the onboarding "fix up a home"
    /// (upgrade a house) highlight — the existing red ground-ring cue (#535)
    /// reused on the specific house the player must tap and upgrade, so it's
    /// obvious which one to click. Mirrors <see cref="LostItemGlow.ShouldShow"/>'s
    /// shape: a small engine-free predicate the thin Unity view applies, keeping
    /// the lifecycle decision in Core.
    ///
    /// <para>No new Core state (#469): the highlight is on for exactly the window
    /// during which <see cref="GameState.IsHouseUpgradeEligible"/> already gates
    /// the real upgrade — reading only
    /// <see cref="OnboardingRewardChain.CurrentStep"/> and the already-stored
    /// <see cref="GameState.OnboardingUpgradeTargetHouseId"/>. It clears the moment
    /// the chain advances past <see cref="OnboardingRewardStep.UpgradeHouse"/>,
    /// and stays cleared for a returning player whose chain is already
    /// past it.</para>
    /// </summary>
    public static class OnboardingHouseHighlight
    {
        /// <summary>True when the target house should carry the highlight: the
        /// reward chain is waiting on the <see cref="OnboardingRewardStep.UpgradeHouse"/>
        /// step AND a target house id is recorded. Any other step, or no recorded
        /// target (e.g. a pre-#469 legacy save), gets no highlight.</summary>
        public static bool ShouldShow(OnboardingRewardStep currentStep, int? targetHouseId)
        {
            return currentStep == OnboardingRewardStep.UpgradeHouse && targetHouseId.HasValue;
        }

        /// <summary>The house id to highlight for <paramref name="state"/>, or
        /// null when no highlight should show. Always exactly the stored
        /// <see cref="GameState.OnboardingUpgradeTargetHouseId"/> (never any other
        /// house), and only while <see cref="ShouldShow"/> holds — so the target
        /// id persisting past the step (it does, #469) never re-shows the
        /// highlight. Null-safe.</summary>
        public static int? TargetHouseId(GameState state)
        {
            if (state == null)
            {
                return null;
            }

            var target = state.OnboardingUpgradeTargetHouseId;
            return ShouldShow(state.RewardChain.CurrentStep, target) ? target : null;
        }
    }
}
