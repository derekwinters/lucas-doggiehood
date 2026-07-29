namespace Doggiehood.Core.Onboarding
{
    /// <summary>
    /// #316: the single tunable for the onboarding reward-chain. Every step of
    /// the 4-step guided sequence pays this flat amount, sized so the chain
    /// funds its own next step (100 bonus covers the 100 upgrade; each reward
    /// covers the next cost; the 50 build leaves a small cushion). A "starting
    /// value, not final balance" placeholder, kept as a named constant per
    /// #161 so tuning happens in one place.
    /// </summary>
    public static class OnboardingRewardChainNumbers
    {
        /// <summary>Flat coin reward granted at each of the four scripted
        /// onboarding steps (first-quest, upgrade, expand, build).</summary>
        public const int RewardPerStep = 100;
    }
}
