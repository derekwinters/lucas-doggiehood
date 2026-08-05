using Doggiehood.Core.Onboarding;
using Doggiehood.Core.Ui;
using Doggiehood.Core.World;
using UnityEngine;

namespace Doggiehood.Unity
{
    /// <summary>
    /// #372/#541: scene-side glue that surfaces each onboarding reward-chain step
    /// payout as a non-modal completion toast (docs/specs/ui/toast.md). It
    /// subscribes to the single Core reward event
    /// (<see cref="OnboardingRewardChain.RewardGranted"/>, fired from the #316
    /// payout path) and, for each completed step, enqueues one toast onto the
    /// shared <see cref="ToastQueue{T}"/> with the Unity-side approved copy
    /// (<see cref="ToastCopy.OnboardingStep"/>) and the amount Core just
    /// deposited.
    ///
    /// <para>#541 reverses the #374 modal exception: the step feedback used to
    /// raise the centered <see cref="OnboardingRewardPanel"/> — it now enqueues a
    /// toast instead, so onboarding no longer blocks play on a reward step.</para>
    ///
    /// <para>Thin wiring only — it holds no game rules and moves no coins: the
    /// reward chain owns the deposit, and the currency chip updates on its own off
    /// <c>Wallet.Coins</c>. The event stays silent for a returning player (a
    /// completed chain never re-pays), so this director simply never enqueues for
    /// them.</para>
    /// </summary>
    public sealed class OnboardingRewardDirector : MonoBehaviour
    {
        private ToastQueue<ToastRequest> toastQueue;

        public void Init(GameState state, ToastQueue<ToastRequest> toastQueue)
        {
            this.toastQueue = toastQueue;
            state.RewardChain.RewardGranted += OnRewardGranted;
        }

        /// <summary>A reward-chain step just paid out: enqueue its celebration
        /// toast. The line is pure presentation over the deposit Core already
        /// made — the amount is exactly what the chain granted.</summary>
        private void OnRewardGranted(OnboardingRewardStep step, int amount)
        {
            toastQueue.Enqueue(new ToastRequest(ToastCopy.OnboardingStep(step, amount)));
        }
    }
}
