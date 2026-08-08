using System.Collections.Generic;
using Doggiehood.Core.Dogs;
using Doggiehood.Core.Economy;
using Doggiehood.Core.Quests;
using Doggiehood.Core.Ui;
using Doggiehood.Core.World;
using UnityEngine;

namespace Doggiehood.Unity
{
    /// <summary>
    /// #675: scene-side glue that announces each move-in as a non-modal "new
    /// resident" toast (docs/specs/ui/toast.md) — the toast's third trigger,
    /// alongside quest completion and onboarding reward steps. It subscribes to
    /// the same single Core move-in signal the dog-spawn
    /// (<see cref="QuestDirector"/>) and welcome pop-up
    /// (<see cref="WelcomePopupDirector"/>) paths use
    /// (<see cref="QuestManager.MoveInOccurred"/>, raised once per household from
    /// the <c>Complete</c> funnel) and enqueues one toast with the Unity-side
    /// approved copy (<see cref="ToastCopy.MoveIn"/>).
    ///
    /// <para>One toast per <b>household</b>, never one per dog — the same
    /// per-household rule the payout itself follows (the event carries the whole
    /// household in one call).</para>
    ///
    /// <para>Thin wiring only — it holds no game rules and moves no coins:
    /// <see cref="Doggiehood.Core.World.GameState.HandleQuestCompleted"/> already
    /// deposited <see cref="EconomyNumbers.MoveInReward"/> on the move-in state
    /// change, and this reads that same live seam purely to render the amount.
    /// The welcome pop-up is unaffected: both surfaces hang off the one event
    /// independently, so neither can suppress the other.</para>
    /// </summary>
    public sealed class MoveInToastDirector : MonoBehaviour
    {
        private GameState state;
        private ToastQueue<ToastRequest> toastQueue;

        public void Init(GameState state, ToastQueue<ToastRequest> toastQueue)
        {
            this.state = state;
            this.toastQueue = toastQueue;
            state.Quests.MoveInOccurred += OnMoveInOccurred;
        }

        private void OnDestroy()
        {
            if (state != null)
            {
                state.Quests.MoveInOccurred -= OnMoveInOccurred;
            }
        }

        /// <summary>A household just moved in and was paid for: enqueue its
        /// arrival toast. Pure presentation over the deposit Core already made —
        /// the amount shown is exactly the reward it granted.</summary>
        private void OnMoveInOccurred(IReadOnlyList<Dog> household)
        {
            if (household == null || household.Count == 0)
            {
                return;
            }

            toastQueue.Enqueue(new ToastRequest(
                ToastCopy.MoveIn(household, EconomyNumbers.MoveInReward)));
        }
    }
}
