using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Doggiehood.Core.Economy;
using Doggiehood.Core.Quests;
using Doggiehood.Core.World;
using NUnit.Framework;

namespace Doggiehood.Core.Tests.Quests
{
    public class QuestManagerTests
    {
        private static GameState NewState()
        {
            return GameState.CreateNew();
        }

        [Test]
        public void ExposesActiveQuestsAcrossAllDogs()
        {
            // #23: QuestManager is the one view over active quests.
            var state = NewState();

            Assert.That(state.Quests.ActiveQuests, Is.Empty);

            state.Quests.StartNewDay(new System.Random(1));

            Assert.That(state.Quests.ActiveQuests.Count(), Is.InRange(2, 4));
        }

        [Test]
        public void NewDay_AssignsQuestsToTwoToFourDogs()
        {
            // #26: daily rotation of a few active quests.
            for (var seed = 0; seed < 10; seed++)
            {
                var state = NewState();
                state.Quests.StartNewDay(new System.Random(seed));

                var dogsWithQuests = state.Dogs.Count(d => d.HasActiveQuest);
                Assert.That(dogsWithQuests, Is.InRange(2, 4), $"seed {seed}");
            }
        }

        [Test]
        public void NewDay_NeverOverwritesAnUncompletedQuest()
        {
            // #26 precedence rule: a dog holding an uncompleted quest keeps
            // it; the rotation only assigns to quest-free dogs.
            var state = NewState();
            state.Quests.StartNewDay(new System.Random(1));

            var held = state.Quests.ActiveQuests.First();
            var holder = held.DogName;

            for (var day = 0; day < 5; day++)
            {
                state.Quests.StartNewDay(new System.Random(100 + day));
            }

            var stillHeld = state.Quests.ActiveQuests.Single(q => q.DogName == holder);
            Assert.That(stillHeld.Id, Is.EqualTo(held.Id), "quest was overwritten by rotation");
        }

        [Test]
        public void Rotation_IsDeterministicForASeed()
        {
            var a = NewState();
            var b = NewState();
            a.Quests.StartNewDay(new System.Random(42));
            b.Quests.StartNewDay(new System.Random(42));

            Assert.That(
                a.Quests.ActiveQuests.Select(q => (q.DogName, q.Type, q.ItemName)),
                Is.EqualTo(b.Quests.ActiveQuests.Select(q => (q.DogName, q.Type, q.ItemName))));
        }

        [Test]
        public void CurrencyOnlyMovesOnQuestCompletion()
        {
            // #24: no idle income — days passing never change the balance.
            var state = NewState();

            for (var day = 0; day < 30; day++)
            {
                state.Quests.StartNewDay(new System.Random(day));
            }

            Assert.That(state.Wallet.Coins, Is.EqualTo(0), "coins appeared without any quest completing");
        }

        [Test]
        public void CompletingAnyQuestType_PaysTheFlatPayout()
        {
            // #23/#24/#62 + integration: full loop for each quest type.
            var state = NewState();
            state.Wallet.Deposit(100); // funds for the BuyGift acceptance cost

            // Lost item: accept, then tap the hidden position (#12, #31).
            var lost = state.Quests.GiveQuestTo(state.Dogs[0], QuestType.LostItem, new System.Random(1));
            var before = state.Wallet.Coins;
            state.Quests.Accept(lost);
            Assert.That(state.Quests.TapWorldPosition(lost.HiddenItemPosition.Value), Is.True);
            Assert.That(lost.Status, Is.EqualTo(QuestStatus.Completed));
            Assert.That(state.Wallet.Coins, Is.EqualTo(before + Doggiehood.Core.Economy.EconomyNumbers.QuestPayout));

            // Buy gift: accept deducts cost; payout only after delivery (#13, #30).
            var buy = state.Quests.GiveQuestTo(state.Dogs[1], QuestType.BuyGift, new System.Random(2));
            before = state.Wallet.Coins;
            state.Quests.Accept(buy);
            Assert.That(state.Wallet.Coins, Is.EqualTo(before - buy.Cost.Value));
            state.Quests.NotifyDogArrivedHome(buy);
            state.Quests.DeliverPackage(buy);
            Assert.That(buy.Status, Is.EqualTo(QuestStatus.Completed));
            Assert.That(state.Wallet.Coins,
                Is.EqualTo(before - buy.Cost.Value + Doggiehood.Core.Economy.EconomyNumbers.QuestPayout));

            // Pest control: spray the right house (#53).
            var pest = state.Quests.GiveQuestTo(state.Dogs[4], QuestType.PestControl, new System.Random(3));
            before = state.Wallet.Coins;
            state.Quests.Accept(pest);
            Assert.That(state.Quests.SprayHouse(pest.TargetHouseId.Value), Is.True);
            Assert.That(pest.Status, Is.EqualTo(QuestStatus.Completed));
            Assert.That(state.Wallet.Coins, Is.EqualTo(before + Doggiehood.Core.Economy.EconomyNumbers.QuestPayout));
        }

        [Test]
        public void CompletingAQuest_RollsTheMoveInPityCounter_ButNeverFillsAnOccupiedHouse()
        {
            // #58: every quest completion is wired to GameState's move-in
            // hook (#54). With all 4 starting houses already occupied
            // there is nothing to fill, so completing a quest must never
            // grow the dog roster or touch house vacancy, no matter how
            // the roll would have landed.
            var state = NewState();
            var dogCountBefore = state.Dogs.Count;
            var pest = state.Quests.GiveQuestTo(state.Dogs[4], QuestType.PestControl, new System.Random(3));
            state.Quests.Accept(pest);

            Assert.That(state.Quests.SprayHouse(pest.TargetHouseId.Value), Is.True);

            Assert.That(state.Dogs.Count, Is.EqualTo(dogCountBefore));
            Assert.That(state.Houses, Has.All.Property("IsVacant").False);
        }

        [Test]
        public void QuestsNeverExpire_AcrossAnyNumberOfRotations()
        {
            // #28: an active quest stays active until explicitly completed.
            var state = NewState();
            var quest = state.Quests.GiveQuestTo(state.Dogs[0], QuestType.LostItem, new System.Random(1));

            for (var day = 0; day < 50; day++)
            {
                state.Quests.StartNewDay(new System.Random(day));
            }

            Assert.That(quest.Status, Is.Not.EqualTo(QuestStatus.Completed));
            Assert.That(state.Quests.ActiveQuests.Select(q => q.Id), Does.Contain(quest.Id));
        }

        [Test]
        public void HousesAwaitingSpray_ListsOnlyAcceptedUncompletedPestHouses()
        {
            // #53/#157: the visible bug state on a house is driven by Core —
            // a house shows a bug swarm exactly while it holds an accepted,
            // not-yet-sprayed pest-control quest.
            var state = NewState();
            var buggedDog = state.Dogs[4]; // Pepper, house 3
            var pest = state.Quests.GiveQuestTo(buggedDog, QuestType.PestControl, new System.Random(5));

            // Given but not yet accepted -> not actionable, no swarm yet.
            Assert.That(state.Quests.HousesAwaitingSpray(), Does.Not.Contain(buggedDog.HouseId));

            state.Quests.Accept(pest);
            Assert.That(state.Quests.HousesAwaitingSpray(), Is.EqualTo(new[] { buggedDog.HouseId }));

            // Other quest types never register a bug house.
            state.Wallet.Deposit(100);
            var buy = state.Quests.GiveQuestTo(state.Dogs[1], QuestType.BuyGift, new System.Random(3));
            state.Quests.Accept(buy);
            Assert.That(state.Quests.HousesAwaitingSpray(), Is.EqualTo(new[] { buggedDog.HouseId }));

            // Once sprayed (completed) the house drops off the list.
            Assert.That(state.Quests.SprayHouse(buggedDog.HouseId), Is.True);
            Assert.That(state.Quests.HousesAwaitingSpray(), Is.Empty);
        }

        [Test]
        public void LostItemPool_ExactlyMatchesTheCatalogsLostEligibleItems()
        {
            // #190: pools are queries over the single tagged catalog, not a
            // hand-maintained parallel list.
            var state = NewState();
            var expected = ItemCatalog.NamesEligibleFor(ItemEligibility.Lost);
            var observed = new HashSet<string>();

            for (var seed = 0; seed < 200; seed++)
            {
                var quest = state.Quests.GiveQuestTo(state.Dogs[0], QuestType.LostItem, new Random(seed));
                observed.Add(quest.ItemName);
            }

            Assert.That(observed, Is.EquivalentTo(expected));
        }

        [Test]
        public void GiftPool_ExactlyMatchesTheCatalogsGiftEligibleItems()
        {
            // #190.
            var state = NewState();
            var expected = ItemCatalog.NamesEligibleFor(ItemEligibility.Gift);
            var observed = new HashSet<string>();

            for (var seed = 0; seed < 200; seed++)
            {
                var quest = state.Quests.GiveQuestTo(state.Dogs[1], QuestType.BuyGift, new Random(seed));
                observed.Add(quest.ItemName);
            }

            Assert.That(observed, Is.EquivalentTo(expected));
        }

        [Test]
        public void DecorationRequestOptions_ExactlyMatchTheCatalogsDecorationEligibleItems()
        {
            // #190: the generic decoration request offers the
            // Decoration-eligible catalog slice, no second parallel list.
            var state = NewState();
            var expected = ItemCatalog.NamesEligibleFor(ItemEligibility.Decoration);

            var quest = state.Quests.GiveQuestTo(state.Dogs[0], QuestType.DecorationRequest, new Random(1));

            Assert.That(quest.Options, Is.EquivalentTo(expected));
        }

        [Test]
        public void FindOnlyItems_AreNeverChosenForABuyGiftQuest()
        {
            // Find-only items (e.g. "puppy") carry no cost and must never be
            // selectable for a purchase-driven quest type.
            var state = NewState();

            for (var seed = 0; seed < 200; seed++)
            {
                var quest = state.Quests.GiveQuestTo(state.Dogs[1], QuestType.BuyGift, new Random(seed));
                Assert.That(quest.ItemName, Is.Not.EqualTo("puppy"));
            }
        }

        [Test]
        public void NoParallelItemArrays_RemainOnQuestManager()
        {
            // #190 guard: LostItems/GiftItems/DecorationItems are deleted —
            // pools must be queries over ItemCatalog, not hand-kept lists.
            // (Invariant.)
            var forbidden = new[] { "LostItems", "GiftItems", "DecorationItems" };

            var offenders = typeof(QuestManager)
                .GetFields(BindingFlags.NonPublic | BindingFlags.Public
                    | BindingFlags.Static | BindingFlags.Instance)
                .Select(f => f.Name)
                .Where(name => forbidden.Contains(name))
                .ToList();

            Assert.That(offenders, Is.Empty);
        }

        [Test]
        public void QuestSchema_HasNoExpiryOrFailFields()
        {
            // #28: structurally no timers/fail states. (Invariant guard.)
            var forbidden = new[] { "expir", "fail", "timer", "deadline", "timeout" };

            var offenders = typeof(Quest).GetProperties()
                .Where(p => forbidden.Any(f => p.Name.ToLowerInvariant().Contains(f)))
                .Select(p => p.Name)
                .ToList();

            Assert.That(offenders, Is.Empty);
        }
    }
}
