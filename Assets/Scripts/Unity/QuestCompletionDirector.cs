using Doggiehood.Core.Quests;
using Doggiehood.Core.Ui;
using Doggiehood.Core.World;
using UnityEngine;

namespace Doggiehood.Unity
{
    /// <summary>
    /// #541: scene-side glue that surfaces every quest completion as a non-modal
    /// completion toast (docs/specs/ui/toast.md). It subscribes to the single Core
    /// completion signal (<see cref="QuestManager.QuestCompleted"/>, raised once
    /// from the <c>Complete</c> funnel for every completion path — delivery,
    /// lost-item find, spray) and enqueues one toast onto the shared
    /// <see cref="ToastQueue{T}"/> with the Unity-side approved copy
    /// (<see cref="ToastCopy.QuestComplete"/>, "Quest complete! +N coins") and the
    /// flat payout Core just deposited.
    ///
    /// <para>Thin wiring only — it holds no game rules and moves no coins: the
    /// quest manager owns the deposit, and the currency chip updates on its own
    /// off <c>Wallet.Coins</c>.</para>
    /// </summary>
    public sealed class QuestCompletionDirector : MonoBehaviour
    {
        private ToastQueue<ToastRequest> toastQueue;

        public void Init(GameState state, ToastQueue<ToastRequest> toastQueue)
        {
            this.toastQueue = toastQueue;
            state.Quests.QuestCompleted += OnQuestCompleted;
        }

        /// <summary>A quest just completed and paid out: enqueue its completion
        /// toast. Pure presentation over the deposit Core already made — the
        /// amount is exactly the flat payout the manager granted.</summary>
        private void OnQuestCompleted(Quest quest, int amount)
        {
            toastQueue.Enqueue(new ToastRequest(ToastCopy.QuestComplete(amount)));
        }
    }
}
