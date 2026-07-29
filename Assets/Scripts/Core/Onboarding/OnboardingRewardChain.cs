using Doggiehood.Core.Economy;

namespace Doggiehood.Core.Onboarding
{
    /// <summary>The four scripted onboarding steps (#316), in the fixed order
    /// they must fire: complete the first quest, upgrade a house, expand the
    /// map, build a house on the newly unlocked lot. <see cref="Done"/> is the
    /// terminal state once all four have paid out.</summary>
    public enum OnboardingRewardStep
    {
        FirstQuest,
        UpgradeHouse,
        ExpandMap,
        BuildHouse,
        Done,
    }

    /// <summary>
    /// #316: a one-time, first-run scripted reward chain that walks a new
    /// player through all four core early mechanics — quest -> upgrade ->
    /// expand -> build — paying a flat <see cref="OnboardingRewardChainNumbers.RewardPerStep"/>
    /// at each step by reusing the quest reward-payout path (a
    /// <see cref="Wallet"/> deposit), never the random rotation.
    ///
    /// <para>Each step fires exactly once and only in order: an action taken
    /// out of turn neither pays nor advances, so the chain keeps waiting on the
    /// step it is on. Engine-free Core — the live entry points on
    /// <see cref="World.GameState"/> notify it; the thin Unity layer never
    /// touches it directly.</para>
    /// </summary>
    public sealed class OnboardingRewardChain
    {
        public OnboardingRewardStep CurrentStep { get; private set; } = OnboardingRewardStep.FirstQuest;

        /// <summary>Whether every step has paid out and the guided sequence is
        /// finished — the point at which normal quest rotation is released.</summary>
        public bool IsComplete
        {
            get { return CurrentStep == OnboardingRewardStep.Done; }
        }

        /// <summary>
        /// Records that <paramref name="action"/> just happened. When it is the
        /// step the chain is currently waiting on, pays the flat reward into
        /// <paramref name="wallet"/> and advances to the next step; returns true
        /// in that case. Any out-of-order action (or one after the chain is
        /// complete) is ignored — no payment, no advance — and returns false.
        /// </summary>
        public bool TryAdvance(OnboardingRewardStep action, Wallet wallet)
        {
            if (IsComplete || action != CurrentStep)
            {
                return false;
            }

            wallet.Deposit(OnboardingRewardChainNumbers.RewardPerStep);
            CurrentStep = CurrentStep + 1;
            return true;
        }

        /// <summary>Restores a persisted step on load (#316) without paying any
        /// reward — round-tripping progress so a one-time chain is never
        /// restarted or re-paid on reload.</summary>
        public void RestoreStep(OnboardingRewardStep step)
        {
            CurrentStep = step;
        }
    }
}
