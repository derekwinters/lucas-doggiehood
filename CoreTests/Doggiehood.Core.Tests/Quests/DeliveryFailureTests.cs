using System.Linq;
using Doggiehood.Core.Dogs;
using Doggiehood.Core.Economy;
using Doggiehood.Core.Quests;
using Doggiehood.Core.World;
using NUnit.Framework;

namespace Doggiehood.Core.Tests.Quests
{
    /// <summary>
    /// #677: a delivery leg that cannot be carried out fails SAFELY. The reported
    /// bug left a dog sitting in the waiting pose forever with no truck ever
    /// coming, because the only exit from <c>WaitingForDelivery</c> was a truck
    /// that had already thrown. The player has been charged by then, so the safe
    /// outcome is the one they paid for: the item lands, the dog is handed back to
    /// wander, and the quest completes — nobody waits forever.
    /// </summary>
    public class DeliveryFailureTests
    {
        [Test]
        public void FailDelivery_AfterTheDogSatWaiting_StillDeliversTheItem_AndHandsTheDogBackToWander()
        {
            var state = GameState.CreateNew();
            state.Wallet.Deposit(1000);
            var dog = state.Dogs.First(d => d.HouseId == 3);
            var quest = state.Quests.GiveQuestTo(dog, QuestType.BuyGift, new System.Random(3));
            Assert.That(state.Quests.Accept(quest), Is.True);
            state.Quests.NotifyDogArrivedHome(quest);
            Assert.That(dog.State, Is.EqualTo(DogState.Sit), "precondition: the dog waits for the truck");

            state.Quests.FailDelivery(quest);

            Assert.That(quest.DeliveryPhase, Is.EqualTo(DeliveryPhase.Delivered),
                "the delivery resolves rather than hanging in WaitingForDelivery");
            Assert.That(quest.Status, Is.EqualTo(QuestStatus.Completed));
            Assert.That(
                state.PlacedItems.Any(item => item.HouseId == dog.HouseId && item.ItemName == quest.ItemName),
                Is.True, "the player paid for the item, so the item still arrives");
            Assert.That(dog.State, Is.EqualTo(DogState.IdleWander),
                "the dog must not be left stranded in the waiting pose");
            Assert.That(dog.WantsToWander, Is.True);
        }

        [Test]
        public void FailDelivery_WhileTheDogIsStillWalkingHome_NeverSitsItWhereItStands()
        {
            // A walk home that cannot be planned must not be resolved by pretending
            // the dog arrived: it is NOT at its front door, so it never sits.
            var state = GameState.CreateNew();
            state.Wallet.Deposit(1000);
            var dog = state.Dogs.First(d => d.HouseId == 3);
            var quest = state.Quests.GiveQuestTo(dog, QuestType.BuyGift, new System.Random(3));
            Assert.That(state.Quests.Accept(quest), Is.True);
            Assert.That(quest.DeliveryPhase, Is.EqualTo(DeliveryPhase.HeadingHome));

            state.Quests.FailDelivery(quest);

            Assert.That(dog.State, Is.Not.EqualTo(DogState.Sit),
                "a dog that never reached its door must never enter the waiting pose");
            Assert.That(quest.Status, Is.EqualTo(QuestStatus.Completed));
            Assert.That(dog.WantsToWander, Is.True, "the dog goes back to wandering rather than freezing");
        }

        [Test]
        public void FailDelivery_OnAQuestWithNoDeliveryLeg_IsANoOp()
        {
            var state = GameState.CreateNew();
            var dog = state.Dogs.First(d => d.HouseId == 3);
            var quest = state.Quests.GiveQuestTo(dog, QuestType.PestControl, new System.Random(5));
            Assert.That(state.Quests.Accept(quest), Is.True);
            Assert.That(quest.DeliveryPhase, Is.EqualTo(DeliveryPhase.None));

            state.Quests.FailDelivery(quest);

            Assert.That(quest.Status, Is.EqualTo(QuestStatus.Accepted),
                "a quest with no delivery leg is untouched by a delivery failure");
        }
    }
}
