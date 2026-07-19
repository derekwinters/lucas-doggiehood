using System.Linq;
using Doggiehood.Core.Dogs;
using Doggiehood.Core.Quests;
using Doggiehood.Core.World;
using NUnit.Framework;

namespace Doggiehood.Core.Tests.Quests
{
    public class LostItemQuestTests
    {
        [Test]
        public void ProducesAnItemName_AndAHiddenLocationInsideTheWorld()
        {
            // #12/#31: hidden position defined, within scene bounds.
            var state = GameState.CreateNew();
            var quest = state.Quests.GiveQuestTo(state.Dogs[0], QuestType.LostItem, new System.Random(9));

            Assert.That(quest.ItemName, Is.Not.Empty);
            Assert.That(quest.HiddenItemPosition, Is.Not.Null);
            Assert.That(System.Math.Abs(quest.HiddenItemPosition.Value.X), Is.LessThanOrEqualTo(30f));
            Assert.That(System.Math.Abs(quest.HiddenItemPosition.Value.Z), Is.LessThanOrEqualTo(30f));
        }

        [Test]
        public void TappingElsewhere_HasNoEffect()
        {
            // #31: only the hidden spot completes the quest.
            var state = GameState.CreateNew();
            var quest = state.Quests.GiveQuestTo(state.Dogs[0], QuestType.LostItem, new System.Random(9));
            state.Quests.Accept(quest);

            var far = new GridPoint(quest.HiddenItemPosition.Value.X + 10f, quest.HiddenItemPosition.Value.Z);

            Assert.That(state.Quests.TapWorldPosition(far), Is.False);
            Assert.That(quest.Status, Is.EqualTo(QuestStatus.Accepted));
        }

        [Test]
        public void NoHintOrRadarApi_ExistsInCore()
        {
            // #31 guard: hidden-object search only — no hints. (Invariant.)
            var forbidden = new[] { "hint", "radar" };

            var offenders = typeof(Quest).Assembly.GetTypes()
                .Where(t => t.Namespace != null
                    && t.Namespace.StartsWith("Doggiehood.Core")
                    && !t.Namespace.Contains("Tests"))
                .Where(t => forbidden.Any(f => t.Name.ToLowerInvariant().Contains(f)))
                .Select(t => t.FullName)
                .ToList();

            Assert.That(offenders, Is.Empty);
        }

        [Test]
        public void GeneratedViaTheSharedTemplateSystem()
        {
            // #12: dialogue comes from the template, not per-dog text.
            // #189: the pooled opener/closer are drawn via an injectable
            // RNG shared with the rest of quest generation, so the exact
            // line depends on how much that RNG was already advanced —
            // assert pool membership rather than an exact re-render.
            var state = GameState.CreateNew();
            var quest = state.Quests.GiveQuestTo(state.Dogs[0], QuestType.LostItem, new System.Random(9));
            var dog = state.Dogs[0];
            var template = QuestTemplates.For(QuestType.LostItem);

            var openerCandidates = FilledPool(template.DefaultOpeners, template.FlavoredOpeners, dog, quest.ItemName);
            var closerCandidates = FilledPool(template.DefaultClosers, template.FlavoredClosers, dog, quest.ItemName);

            Assert.That(quest.DialogueLines.Count, Is.EqualTo(2));
            Assert.That(openerCandidates, Does.Contain(quest.DialogueLines[0]));
            Assert.That(closerCandidates, Does.Contain(quest.DialogueLines[1]));
        }

        private static System.Collections.Generic.HashSet<string> FilledPool(
            System.Collections.Generic.IReadOnlyList<string> defaults,
            System.Collections.Generic.IReadOnlyDictionary<Personality, System.Collections.Generic.IReadOnlyList<string>> flavored,
            Dog dog,
            string item)
        {
            var raw = new System.Collections.Generic.List<string>(defaults);
            if (flavored.TryGetValue(dog.Personality, out var personalityLines))
            {
                raw.AddRange(personalityLines);
            }

            return new System.Collections.Generic.HashSet<string>(
                raw.ConvertAll(t => t.Replace("{dog}", dog.Name).Replace("{item}", item)));
        }
    }

    public class BuyGiftQuestTests
    {
        [Test]
        public void ProducesAnItemWithACatalogCost()
        {
            var state = GameState.CreateNew();
            var quest = state.Quests.GiveQuestTo(state.Dogs[1], QuestType.BuyGift, new System.Random(3));

            Assert.That(quest.ItemName, Is.Not.Empty);
            Assert.That(quest.Cost, Is.Not.Null);
            Assert.That(quest.Cost.Value, Is.InRange(30, 50));
        }

        [Test]
        public void Acceptance_IsRejectedWhenUnaffordable()
        {
            // #25: the spend simply doesn't occur; no negative balance.
            var state = GameState.CreateNew();
            var quest = state.Quests.GiveQuestTo(state.Dogs[1], QuestType.BuyGift, new System.Random(3));

            Assert.That(state.Quests.Accept(quest), Is.False);
            Assert.That(quest.Status, Is.EqualTo(QuestStatus.Available));
            Assert.That(state.Wallet.Coins, Is.EqualTo(0));
        }

        [Test]
        public void AcceptedDog_HeadsHomeThenSitsThenGetsDelivery()
        {
            // #30: heading home -> Sit on arrival -> delivery -> item at house.
            var state = GameState.CreateNew();
            state.Wallet.Deposit(100);
            var dog = state.Dogs[1];
            var quest = state.Quests.GiveQuestTo(dog, QuestType.BuyGift, new System.Random(3));

            state.Quests.Accept(quest);
            Assert.That(quest.DeliveryPhase, Is.EqualTo(DeliveryPhase.HeadingHome));

            state.Quests.NotifyDogArrivedHome(quest);
            Assert.That(quest.DeliveryPhase, Is.EqualTo(DeliveryPhase.WaitingForDelivery));
            Assert.That(dog.State, Is.EqualTo(DogState.Sit));

            Assert.That(quest.Status, Is.Not.EqualTo(QuestStatus.Completed),
                "no payout before delivery (#30)");

            state.Quests.DeliverPackage(quest);
            Assert.That(quest.DeliveryPhase, Is.EqualTo(DeliveryPhase.Delivered));
            Assert.That(quest.Status, Is.EqualTo(QuestStatus.Completed));
            Assert.That(state.PlacedItems.Any(p => p.HouseId == dog.HouseId && p.ItemName == quest.ItemName),
                Is.True, "delivered item appears at the house");
        }
    }

    public class PestControlQuestTests
    {
        [Test]
        public void QuestIsTiedToTheDogsHouse()
        {
            var state = GameState.CreateNew();
            var dog = state.Dogs[4];
            var quest = state.Quests.GiveQuestTo(dog, QuestType.PestControl, new System.Random(5));

            Assert.That(quest.TargetHouseId, Is.EqualTo(dog.HouseId));
        }

        [Test]
        public void SprayingAHouseWithNoActiveQuest_HasNoEffect()
        {
            var state = GameState.CreateNew();

            Assert.That(state.Quests.SprayHouse(1), Is.False);
            Assert.That(state.Wallet.Coins, Is.EqualTo(0));
        }

        [Test]
        public void SprayingTheWrongHouse_DoesNotComplete()
        {
            var state = GameState.CreateNew();
            var dog = state.Dogs[4]; // Pepper, house 3
            var quest = state.Quests.GiveQuestTo(dog, QuestType.PestControl, new System.Random(5));
            state.Quests.Accept(quest);

            Assert.That(state.Quests.SprayHouse(1), Is.False);
            Assert.That(quest.Status, Is.EqualTo(QuestStatus.Accepted));
        }
    }

    public class PermanenceTests
    {
        [Test]
        public void DeliveredItems_SurviveASaveLoadCycle()
        {
            // #27: permanent world changes persist.
            var state = GameState.CreateNew();
            state.Wallet.Deposit(100);
            var quest = state.Quests.GiveQuestTo(state.Dogs[1], QuestType.BuyGift, new System.Random(3));
            state.Quests.Accept(quest);
            state.Quests.NotifyDogArrivedHome(quest);
            state.Quests.DeliverPackage(quest);

            var loaded = SaveCodec.Load(SaveCodec.Save(state));

            Assert.That(loaded.PlacedItems.Count, Is.EqualTo(1));
            Assert.That(loaded.PlacedItems[0].ItemName, Is.EqualTo(quest.ItemName));
            Assert.That(loaded.PlacedItems[0].HouseId, Is.EqualTo(state.Dogs[1].HouseId));
            Assert.That(loaded.Wallet.Coins, Is.EqualTo(state.Wallet.Coins));
        }

        [Test]
        public void DailyRotation_NeverRemovesADeliveredItem()
        {
            // #27: nothing resets with the rotation.
            var state = GameState.CreateNew();
            state.Wallet.Deposit(100);
            var quest = state.Quests.GiveQuestTo(state.Dogs[1], QuestType.BuyGift, new System.Random(3));
            state.Quests.Accept(quest);
            state.Quests.NotifyDogArrivedHome(quest);
            state.Quests.DeliverPackage(quest);

            for (var day = 0; day < 20; day++)
            {
                state.Quests.StartNewDay(new System.Random(day));
            }

            Assert.That(state.PlacedItems.Count, Is.EqualTo(1));
        }
    }
}
