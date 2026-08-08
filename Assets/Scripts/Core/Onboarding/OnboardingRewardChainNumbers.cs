using Doggiehood.Core.Tuning;

namespace Doggiehood.Core.Onboarding
{
    /// <summary>
    /// #316: the single tunable for the onboarding reward-chain. Every step of
    /// the 4-step guided sequence pays this flat amount, sized so the chain
    /// funds the whole guided sequence out of its own payouts — the
    /// <b>self-funding ladder</b>. The binding rung is the expand step, which the
    /// player reaches holding two rewards minus the upgrade; #674 raised this
    /// 100 -> 200 when the tile unlock rose to 200, because the old reward left
    /// the player 100 short there. See
    /// <see cref="OnboardingLadder"/>, which derives the minimum viable reward
    /// from the live costs so the relationship is tested, not assumed. As of
    /// #620 the value reads from the runtime-overridable
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
