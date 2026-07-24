using Doggiehood.Core.Dogs;
using Doggiehood.Core.Economy;
using Doggiehood.Core.Quests;
using Doggiehood.Core.World;
using NUnit.Framework;

namespace Doggiehood.Core.Tests.Quests
{
    /// <summary>
    /// #186: the conversation panel had no affordance showing a buy-quest's
    /// cost or affordability. This pure presentation logic (label text +
    /// affordability) stays Unity-independent so it's covered here rather
    /// than only in an EditMode test.
    /// </summary>
    public class QuestPurchasePresentationTests
    {
        [Test]
        public void AcceptLabel_IsPlainAcceptForANoCostQuest()
        {
            var state = GameState.CreateNew();
            var quest = state.Quests.GiveQuestTo(state.Dogs[0], QuestType.LostItem, new System.Random(9));

            Assert.That(QuestPurchasePresentation.AcceptLabel(quest, ConversationEnding.Accept), Is.EqualTo("Accept"));
        }

        [Test]
        public void AcceptLabel_IsCompleteWhenNoQuestAndEndingIsComplete()
        {
            Assert.That(QuestPurchasePresentation.AcceptLabel(null, ConversationEnding.Complete), Is.EqualTo("Complete"));
        }

        [Test]
        public void AcceptLabel_ShowsTheCost_ForABuyQuest_MatchingTheApprovedWireframeFormat()
        {
            // docs/specs/ui/conversation-panel.md (#175): "Buy · 40".
            var state = GameState.CreateNew();
            var quest = state.Quests.GiveQuestTo(state.Dogs[1], QuestType.BuyGift, new System.Random(3));

            Assert.That(QuestPurchasePresentation.AcceptLabel(quest, ConversationEnding.Accept),
                Is.EqualTo($"Buy · {quest.Cost.Value}"));
        }

        [Test]
        public void IsAcceptAffordable_TrueForANoCostQuest_RegardlessOfWallet()
        {
            var state = GameState.CreateNew();
            var quest = state.Quests.GiveQuestTo(state.Dogs[0], QuestType.LostItem, new System.Random(9));

            Assert.That(QuestPurchasePresentation.IsAcceptAffordable(quest, state.Wallet), Is.True);
        }

        [Test]
        public void IsAcceptAffordable_ReflectsTheWalletBalance_ForABuyQuest()
        {
            var state = GameState.CreateNew();
            var quest = state.Quests.GiveQuestTo(state.Dogs[1], QuestType.BuyGift, new System.Random(3));

            Assert.That(QuestPurchasePresentation.IsAcceptAffordable(quest, state.Wallet), Is.False,
                "a fresh wallet starts at 0 coins");

            state.Wallet.Deposit(quest.Cost.Value);
            Assert.That(QuestPurchasePresentation.IsAcceptAffordable(quest, state.Wallet), Is.True);
        }

        [Test]
        public void OptionLabel_ShowsTheCatalogCost_ForAPurchasableOption()
        {
            var cushion = ItemCatalog.Get("cushion");

            Assert.That(QuestPurchasePresentation.OptionLabel("cushion"), Is.EqualTo($"cushion · {cushion.Cost.Value}"));
        }

        [Test]
        public void OptionLabel_IsPlainName_ForAFindOnlyOption()
        {
            Assert.That(QuestPurchasePresentation.OptionLabel("puppy"), Is.EqualTo("puppy"));
        }

        [Test]
        public void IsOptionAffordable_ReflectsTheWalletBalance()
        {
            var state = GameState.CreateNew();
            var cost = ItemCatalog.Get("cushion").Cost.Value;

            Assert.That(QuestPurchasePresentation.IsOptionAffordable("cushion", state.Wallet), Is.False);

            state.Wallet.Deposit(cost);
            Assert.That(QuestPurchasePresentation.IsOptionAffordable("cushion", state.Wallet), Is.True);
        }

        [Test]
        public void AffordabilityChecks_DefaultToTrue_WhenNoWalletIsSupplied()
        {
            // Defensive default for callers that haven't wired a GameState yet.
            var state = GameState.CreateNew();
            var quest = state.Quests.GiveQuestTo(state.Dogs[1], QuestType.BuyGift, new System.Random(3));

            Assert.That(QuestPurchasePresentation.IsAcceptAffordable(quest, null), Is.True);
            Assert.That(QuestPurchasePresentation.IsOptionAffordable("cushion", null), Is.True);
        }
    }
}
