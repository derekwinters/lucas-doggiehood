using System.Linq;
using Doggiehood.Core.Quests;
using Doggiehood.Core.World;
using Doggiehood.Unity;
using NUnit.Framework;
using UnityEngine;

namespace Doggiehood.Unity.EditModeTests
{
    /// <summary>
    /// #185: "Not now" must be a silent, non-punishing decline distinct from
    /// Accept. #186: the conversation panel had no affordance for a
    /// buy-something quest's cost, and a failed purchase used to close the
    /// panel with zero player-visible feedback. These guard both fixes: the
    /// decline action, cost/affordability surfaced via
    /// QuestPurchasePresentation, and a failed Accept/AcceptWithChoice
    /// leaving the panel open with a message instead of silently closing.
    /// </summary>
    public class ConversationPresenterTests
    {
        private GameState state;
        private GameObject host;
        private ConversationPresenter presenter;

        [SetUp]
        public void CreatePresenter()
        {
            state = GameState.CreateNew();
            host = new GameObject("conversation-presenter-host");
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

        [Test]
        public void AcceptLabel_ShowsTheCost_ForABuyQuest()
        {
            var dog = state.Dogs[1];
            var quest = state.Quests.GiveQuestTo(dog, QuestType.BuyGift, new System.Random(3));
            presenter.TryOpen(dog);

            Assert.That(presenter.AcceptLabel, Is.EqualTo($"Buy · {quest.Cost.Value}"));
        }

        [Test]
        public void AcceptIsAffordable_ReflectsTheWalletBalance_ForABuyQuest()
        {
            var dog = state.Dogs[1];
            var quest = state.Quests.GiveQuestTo(dog, QuestType.BuyGift, new System.Random(3));
            presenter.TryOpen(dog);

            Assert.That(presenter.AcceptIsAffordable, Is.False, "a fresh wallet starts at 0 coins");

            state.Wallet.Deposit(quest.Cost.Value);
            Assert.That(presenter.AcceptIsAffordable, Is.True);
        }

        [Test]
        public void OptionLabel_And_OptionIsAffordable_ReflectTheCatalogCostAndWallet()
        {
            var dog = state.Dogs[2];
            var quest = state.Quests.GiveQuestTo(dog, QuestType.DecorationRequest, new System.Random(7));
            presenter.TryOpen(dog);
            var option = quest.Options[0];
            var cost = Doggiehood.Core.Economy.ItemCatalog.Get(option).Cost.Value;

            Assert.That(presenter.OptionLabel(option), Is.EqualTo($"{option} · {cost}"));
            Assert.That(presenter.OptionIsAffordable(option), Is.False);

            state.Wallet.Deposit(cost);
            Assert.That(presenter.OptionIsAffordable(option), Is.True);
        }

        [Test]
        public void AcceptCurrent_OnAnUnaffordableBuyQuest_LeavesThePanelOpen_WithAnInsufficientFundsMessage()
        {
            var dog = state.Dogs[1];
            var quest = state.Quests.GiveQuestTo(dog, QuestType.BuyGift, new System.Random(3));
            presenter.TryOpen(dog);

            presenter.AcceptCurrent();

            Assert.That(presenter.IsOpen, Is.True, "a failed purchase must not silently close the panel");
            Assert.That(presenter.StatusMessage, Is.Not.Null.And.Not.Empty);
            Assert.That(quest.Status, Is.EqualTo(QuestStatus.Available), "no accept side effect on a rejected spend");
            Assert.That(state.Wallet.Coins, Is.EqualTo(0), "an unaffordable attempt spends nothing");
        }

        [Test]
        public void AcceptCurrent_OnAnAffordableBuyQuest_ClosesThePanel_WithNoStatusMessage()
        {
            var dog = state.Dogs[1];
            var quest = state.Quests.GiveQuestTo(dog, QuestType.BuyGift, new System.Random(3));
            state.Wallet.Deposit(quest.Cost.Value);
            presenter.TryOpen(dog);

            presenter.AcceptCurrent();

            Assert.That(presenter.IsOpen, Is.False);
            Assert.That(presenter.StatusMessage, Is.Null);
            Assert.That(quest.Status, Is.EqualTo(QuestStatus.Accepted));
        }

        [Test]
        public void AcceptChoice_OnAnUnaffordableDecorationOption_LeavesThePanelOpen_WithAnInsufficientFundsMessage()
        {
            var dog = state.Dogs[2];
            var quest = state.Quests.GiveQuestTo(dog, QuestType.DecorationRequest, new System.Random(7));
            presenter.TryOpen(dog);
            var option = quest.Options[0];

            presenter.AcceptChoice(option);

            Assert.That(presenter.IsOpen, Is.True, "a failed purchase must not silently close the panel");
            Assert.That(presenter.StatusMessage, Is.Not.Null.And.Not.Empty);
            Assert.That(quest.Status, Is.EqualTo(QuestStatus.Available), "no accept side effect on a rejected spend");
            Assert.That(state.Wallet.Coins, Is.EqualTo(0), "an unaffordable attempt spends nothing");
        }

        [Test]
        public void ReopeningThePanel_ClearsAnyStaleStatusMessage()
        {
            var dog = state.Dogs[1];
            var quest = state.Quests.GiveQuestTo(dog, QuestType.BuyGift, new System.Random(3));
            presenter.TryOpen(dog);
            presenter.AcceptCurrent();
            Assert.That(presenter.StatusMessage, Is.Not.Null, "sanity check: the failed attempt set a message");

            presenter.Close();
            presenter.TryOpen(dog);

            Assert.That(presenter.StatusMessage, Is.Null, "a fresh open should not show a stale message");
        }
    }
}
