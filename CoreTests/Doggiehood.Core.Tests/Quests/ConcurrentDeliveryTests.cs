using System.Linq;
using Doggiehood.Core.Economy;
using Doggiehood.Core.Quests;
using Doggiehood.Core.World;
using NUnit.Framework;

namespace Doggiehood.Core.Tests.Quests
{
    /// <summary>
    /// #600 first step: before building car-following, confirm the quest system
    /// can actually have two "buy me X" deliveries in flight at once — otherwise
    /// there is nothing for the following rule to arbitrate. Guards against a
    /// single-active-delivery cap silently reappearing.
    /// </summary>
    public class ConcurrentDeliveryTests
    {
        [Test]
        public void TwoBuyGiftDeliveries_CanBeInFlightAtOnce_NoSingleActiveCap()
        {
            var state = GameState.CreateNew();
            state.Wallet.Deposit(1000);
            var rng = new System.Random(1);

            var freeDogs = state.Dogs.Where(d => !d.HasActiveQuest).Take(2).ToList();
            Assert.That(freeDogs.Count, Is.EqualTo(2), "need two free dogs to run two deliveries");

            var q1 = state.Quests.GiveQuestTo(freeDogs[0], QuestType.BuyGift, rng);
            var q2 = state.Quests.GiveQuestTo(freeDogs[1], QuestType.BuyGift, rng);

            // The fresh-state cost ceiling gates the fence out of the gift pool,
            // so both are truck-delivered gifts (not the no-truck fence purchase).
            Assert.That(q1.ItemName, Is.Not.EqualTo(ItemCatalog.FenceItemName));
            Assert.That(q2.ItemName, Is.Not.EqualTo(ItemCatalog.FenceItemName));

            Assert.That(state.Quests.Accept(q1), Is.True);
            Assert.That(state.Quests.Accept(q2), Is.True);

            // Both dogs reach home and sit waiting for their own truck.
            state.Quests.NotifyDogArrivedHome(q1);
            state.Quests.NotifyDogArrivedHome(q2);

            var waiting = state.Quests.ActiveQuests
                .Where(q => q.Type == QuestType.BuyGift
                    && q.DeliveryPhase == DeliveryPhase.WaitingForDelivery)
                .ToList();

            Assert.That(waiting.Count, Is.EqualTo(2),
                "two buy-gift deliveries are in flight simultaneously — no single-active-delivery cap");
        }
    }
}
