using System.Collections.Generic;
using Doggiehood.Core.Cameras;
using Doggiehood.Core.Onboarding;
using Doggiehood.Core.Quests;
using Doggiehood.Core.Ui;
using Doggiehood.Core.World;
using NUnit.Framework;
using UnityEngine;

namespace Doggiehood.Unity.EditModeTests
{
    /// <summary>
    /// #541: the two scene-side directors that turn Core completion signals into
    /// non-modal completion toasts (docs/specs/ui/toast.md). The onboarding
    /// director enqueues a toast (NOT the retired modal panel) on each reward-chain
    /// step payout with the four approved per-step lines; the quest director
    /// enqueues on every quest completion with the "Quest complete! +N coins"
    /// template. Copy is assembled in the Unity layer (<see cref="ToastCopy"/>);
    /// Core owns both payouts and neither toast is modal.
    ///
    /// <para>The toast's third trigger — a move-in (#675) — has its own director
    /// and copy branch, covered in <c>MoveInToastTests</c>.</para>
    /// </summary>
    public class CompletionToastDirectorTests
    {
        private GameObject host;

        [SetUp]
        public void SetUp()
        {
            // The modal-input gate is a process-global singleton; clear it so a
            // leak from an earlier test can't make the "toast is non-modal"
            // assertions read a stale block.
            ModalInputGate.Shared.Clear();
            host = new GameObject("completion-toast-directors");
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(host);
        }

        // --- Copy assembly (Unity layer; exact approved lines, toast.md) ---

        [Test]
        public void Copy_QuestComplete_UsesTheApprovedTemplateWithTheFlatPayout()
        {
            Assert.That(ToastCopy.QuestComplete(10), Is.EqualTo("Quest complete! +10 coins"));
        }

        [Test]
        public void Copy_OnboardingStep_ReusesTheFourApprovedAccomplishmentLines()
        {
            Assert.That(ToastCopy.OnboardingStep(OnboardingRewardStep.FirstQuest, 100),
                Is.EqualTo("You finished your first quest! +100 coins"));
            Assert.That(ToastCopy.OnboardingStep(OnboardingRewardStep.UpgradeHouse, 100),
                Is.EqualTo("You made a house even nicer! +100 coins"));
            Assert.That(ToastCopy.OnboardingStep(OnboardingRewardStep.ExpandMap, 100),
                Is.EqualTo("You opened up a brand-new street! +100 coins"));
            Assert.That(ToastCopy.OnboardingStep(OnboardingRewardStep.BuildHouse, 100),
                Is.EqualTo("You built a brand-new house! +100 coins"));
        }

        // --- Onboarding reward director: toast, not the modal panel ---

        [Test]
        public void OnboardingDirector_EnqueuesAToastPerStep_WithTheApprovedCopy_NotAModal()
        {
            var state = GameState.CreateNew();
            var queue = new ToastQueue<ToastRequest>();
            host.AddComponent<OnboardingRewardDirector>().Init(state, queue);

            // Drive the reward chain through all four paying steps in order; each
            // fires RewardGranted, which the director turns into one toast.
            state.RewardChain.TryAdvance(OnboardingRewardStep.FirstQuest, state.Wallet);
            state.RewardChain.TryAdvance(OnboardingRewardStep.UpgradeHouse, state.Wallet);
            state.RewardChain.TryAdvance(OnboardingRewardStep.ExpandMap, state.Wallet);
            state.RewardChain.TryAdvance(OnboardingRewardStep.BuildHouse, state.Wallet);

            // The payout in the line is the live per-step reward (#674: 200), so
            // the expectation is built from the seam rather than a stale literal.
            var reward = OnboardingRewardChainNumbers.RewardPerStep;
            Assert.That(Drain(queue), Is.EqualTo(new[]
            {
                $"You finished your first quest! +{reward} coins",
                $"You made a house even nicer! +{reward} coins",
                $"You opened up a brand-new street! +{reward} coins",
                $"You built a brand-new house! +{reward} coins",
            }), "one toast per step, queued FCFS, with the approved copy");

            Assert.That(ModalInputGate.Shared.IsBlocking, Is.False,
                "the reward-chain feedback is a toast, never a blocking modal (#541 reverses #374)");
        }

        // --- Quest-completion director ---

        [Test]
        public void QuestDirector_EnqueuesAToastOnCompletion_WithTheQuestCompleteTemplate()
        {
            var state = GameState.CreateNew();
            var queue = new ToastQueue<ToastRequest>();
            host.AddComponent<QuestCompletionDirector>().Init(state, queue);

            var quest = state.Quests.GiveQuestTo(state.Dogs[0], QuestType.LostItem, new System.Random(9));
            Assert.That(state.Quests.Accept(quest), Is.True);
            Assert.That(state.Quests.TapWorldPosition(quest.HiddenItemPosition.Value), Is.True);

            Assert.That(queue.HasCurrent, Is.True, "a completion enqueues a toast");
            Assert.That(queue.Current.Message, Is.EqualTo("Quest complete! +20 coins"),
                "using the approved template with the flat quest payout");
            Assert.That(ModalInputGate.Shared.IsBlocking, Is.False, "the quest toast is non-modal");
        }

        private static List<string> Drain(ToastQueue<ToastRequest> queue)
        {
            var messages = new List<string>();
            while (queue.HasCurrent)
            {
                messages.Add(queue.Current.Message);
                queue.DismissCurrent();
            }

            return messages;
        }
    }
}
