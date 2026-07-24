using System.Linq;
using Doggiehood.Core.Quests;
using Doggiehood.Core.World;
using Doggiehood.Unity;
using NUnit.Framework;
using UnityEngine;

namespace Doggiehood.Unity.EditModeTests
{
    public class ConversationPresenterTests
    {
        private GameObject host;
        private GameState state;
        private ConversationPresenter presenter;

        [SetUp]
        public void CreatePresenter()
        {
            state = GameState.CreateNew();
            host = new GameObject("conversation-presenter-under-test");
            presenter = host.AddComponent<ConversationPresenter>();
            presenter.State = state;
        }

        [TearDown]
        public void Cleanup()
        {
            Object.DestroyImmediate(host);
        }

        [Test]
        public void DeclineCurrent_ClosesThePanel_WithoutAcceptingTheQuest()
        {
            // #185: "Not now" must be a silent, non-punishing exit distinct
            // from Accept — the quest stays Available, not Accepted, and no
            // QuestAccepted notification fires.
            var dog = state.Dogs.First();
            var quest = state.Quests.GiveQuestTo(dog, QuestType.LostItem, new System.Random(1));
            Assert.That(presenter.TryOpen(dog), Is.True);

            var accepted = false;
            presenter.QuestAccepted += _ => accepted = true;

            presenter.DeclineCurrent();

            Assert.That(presenter.IsOpen, Is.False, "Not now closes the panel");
            Assert.That(quest.Status, Is.EqualTo(QuestStatus.Available), "declining must not accept the quest");
            Assert.That(accepted, Is.False, "declining must not raise QuestAccepted");
        }

        [Test]
        public void DeclineCurrent_LeavesTheQuestFullyReopenable()
        {
            // #185: the dog keeps its speech bubble and the exact same
            // request can be re-presented — decline is not a one-shot
            // dismissal, no cooldown, no re-offer delay.
            var dog = state.Dogs.First();
            var quest = state.Quests.GiveQuestTo(dog, QuestType.LostItem, new System.Random(2));
            presenter.TryOpen(dog);

            presenter.DeclineCurrent();

            Assert.That(presenter.TryOpen(dog), Is.True, "the conversation must be re-openable after declining");
            Assert.That(presenter.Current.Lines, Is.EqualTo(quest.DialogueLines),
                "re-opening presents the same request");
        }
    }
}
