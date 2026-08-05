using System.Collections.Generic;
using Doggiehood.Core.Economy;
using Doggiehood.Core.Quests;
using Doggiehood.Core.World;
using NUnit.Framework;

namespace Doggiehood.Core.Tests.Quests
{
    /// <summary>
    /// #541: <see cref="QuestManager.Complete"/> is the single funnel every
    /// completion path routes through (delivery, lost-item find, spray). It now
    /// raises <see cref="QuestManager.QuestCompleted"/> exactly once per
    /// completion — carrying the completed quest and the flat payout — so the
    /// Unity toast layer has one Core signal to celebrate on, mirroring the
    /// existing single-funnel <see cref="QuestManager.MoveInOccurred"/> pattern.
    /// </summary>
    public class QuestCompletedEventTests
    {
        [Test]
        public void LostItemFind_RaisesQuestCompletedOnce_WithTheQuestAndFlatPayout()
        {
            var state = GameState.CreateNew();
            var quest = state.Quests.GiveQuestTo(state.Dogs[0], QuestType.LostItem, new System.Random(9));
            Assert.That(state.Quests.Accept(quest), Is.True);

            var events = new List<(Quest Quest, int Amount)>();
            state.Quests.QuestCompleted += (q, amount) => events.Add((q, amount));

            Assert.That(state.Quests.TapWorldPosition(quest.HiddenItemPosition.Value), Is.True);

            Assert.That(events.Count, Is.EqualTo(1), "one completion raises exactly one event");
            Assert.That(events[0].Quest, Is.SameAs(quest), "the event carries the completed quest");
            Assert.That(events[0].Amount, Is.EqualTo(EconomyNumbers.QuestPayout),
                "and the flat quest payout Core just deposited");
        }

        [Test]
        public void Spray_RaisesQuestCompletedOnce_WithTheQuestAndFlatPayout()
        {
            var state = GameState.CreateNew();
            var quest = state.Quests.GiveQuestTo(state.Dogs[0], QuestType.PestControl, new System.Random(5));
            Assert.That(state.Quests.Accept(quest), Is.True);

            var events = new List<(Quest Quest, int Amount)>();
            state.Quests.QuestCompleted += (q, amount) => events.Add((q, amount));

            Assert.That(state.Quests.SprayHouse(quest.TargetHouseId.Value), Is.True);

            Assert.That(events.Count, Is.EqualTo(1));
            Assert.That(events[0].Quest, Is.SameAs(quest));
            Assert.That(events[0].Amount, Is.EqualTo(EconomyNumbers.QuestPayout));
        }

        [Test]
        public void Delivery_RaisesQuestCompletedOnce_WithTheQuestAndFlatPayout()
        {
            var state = GameState.CreateNew();
            // Fund the wallet so the buy-gift acceptance is not rejected (#25).
            state.Wallet.Deposit(100);
            var quest = state.Quests.GiveQuestTo(state.Dogs[1], QuestType.BuyGift, new System.Random(3));
            Assert.That(state.Quests.Accept(quest), Is.True);

            var events = new List<(Quest Quest, int Amount)>();
            state.Quests.QuestCompleted += (q, amount) => events.Add((q, amount));

            // Only the truck delivery (after the dog sits waiting) completes and pays.
            state.Quests.NotifyDogArrivedHome(quest);
            state.Quests.DeliverPackage(quest);

            Assert.That(events.Count, Is.EqualTo(1));
            Assert.That(events[0].Quest, Is.SameAs(quest));
            Assert.That(events[0].Amount, Is.EqualTo(EconomyNumbers.QuestPayout));
        }
    }
}
