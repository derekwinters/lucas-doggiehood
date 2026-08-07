using Doggiehood.Core.Tuning;

namespace Doggiehood.Core.Onboarding
{
    /// <summary>
    /// #316: the single tunable for the onboarding reward-chain. Every step of
    /// the 4-step guided sequence pays this flat amount, sized so the chain
    /// funds its own next step (100 bonus covers the 100 upgrade; each reward
    /// covers the next cost; the 50 build leaves a small cushion). As of #620
    /// the value reads from the runtime-overridable
    /// <see cref="TuningConfig.Active"/>; the shipping default lives on
    /// <see cref="TuningConfig"/>.
    /// </summary>
    public static class OnboardingRewardChainNumbers
    {
        /// <summary>Flat coin reward granted at each of the four scripted
        /// onboarding steps (first-quest, upgrade, expand, build).</summary>
        public static int RewardPerStep => TuningConfig.Active.OnboardingRewardPerStep;
    }
}
