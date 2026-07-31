using Doggiehood.Core.Onboarding;
using Doggiehood.Core.World;
using UnityEngine;

namespace Doggiehood.Unity
{
    /// <summary>
    /// #372: scene-side glue that raises the standard onboarding reward
    /// celebration panel (<see cref="OnboardingRewardPanel"/>) each time a
    /// reward-chain step actually pays out. It subscribes to the single Core
    /// reward event (<see cref="OnboardingRewardChain.RewardGranted"/>, fired
    /// from the #316 payout path) and, for each completed step, shows the panel
    /// with the Unity-side approved copy (<see cref="OnboardingRewardCopy"/>) and
    /// the amount Core just deposited.
    ///
    /// <para>Thin wiring only — it holds no game rules and moves no coins: the
    /// reward chain owns the deposit, and the currency chip updates on its own
    /// off <c>Wallet.Coins</c>. The event stays silent for a returning player
    /// (a completed chain never re-pays), so this director simply never fires
    /// for them.</para>
    /// </summary>
    public sealed class OnboardingRewardDirector : MonoBehaviour
    {
        private OnboardingRewardPanel panel;

        public void Init(GameState state, OnboardingRewardPanel panel)
        {
            this.panel = panel;
            state.RewardChain.RewardGranted += OnRewardGranted;
        }

        /// <summary>A reward-chain step just paid out: celebrate it. The panel is
        /// pure presentation over the deposit Core already made — the amount is
        /// exactly what the chain granted.</summary>
        private void OnRewardGranted(OnboardingRewardStep step, int amount)
        {
            panel.Show(OnboardingRewardCopy.MessageFor(step), amount);
        }
    }
}
