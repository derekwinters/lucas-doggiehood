using System;
using System.Linq;
using Doggiehood.Core.Dogs;
using Doggiehood.Core.Economy;
using Doggiehood.Core.Quests;
using Doggiehood.Core.Tuning;
using Doggiehood.Core.World;
using NUnit.Framework;

namespace Doggiehood.Core.Tests.Quests
{
    /// <summary>
    /// #626: paid quests are earners, not sinks — the "getting hired" model.
    /// Completing a paid quest (BuyGift / DecorationRequest / fence) pays back
    /// the item cost with a margin: <c>round(cost × markup)</c>, default markup
    /// 1.5×. Free types (LostItem / PestControl) keep the flat free-type payout
    /// since there is no cost to reimburse. The markup is a
    /// <see cref="TuningConfig"/> field.
    /// </summary>
    public class PaidQuestPayoutTests
    {
        [TearDown]
        public void ResetTuning() => TuningConfig.ResetToDefaults();

        private static GameState NewState() => GameState.CreateNew();

        [Test]
        public void BuyGiftCompletion_PaysCostTimesMarkup()
        {
            var state = NewState();
            state.Wallet.Deposit(1000);
            var buy = state.Quests.GiveQuestTo(state.Dogs[1], QuestType.BuyGift, new Random(3));
            var cost = buy.Cost.Value;
            var expectedPayout = EconomyNumbers.PaidQuestPayout(cost);

            var before = state.Wallet.Coins;
            Assert.That(state.Quests.Accept(buy), Is.True);
            state.Quests.NotifyDogArrivedHome(buy);
            state.Quests.DeliverPackage(buy);

            Assert.That(buy.Status, Is.EqualTo(QuestStatus.Completed));
            Assert.That(state.Wallet.Coins, Is.EqualTo(before - cost + expectedPayout));
            Assert.That(expectedPayout, Is.GreaterThan(cost), "paid quests are net positive");
        }

        [Test]
        public void PaidQuestPayout_IsRoundedCostTimesMarkup()
        {
            // The spec's worked examples (docs/specs/quests/economy.md):
            // toy 30 -> 45, pool 50 -> 75, fence 100 -> 150. #742: the middle one
            // read "pool 40 -> 60" here too — the same stale figure the spec
            // carried, against a catalog that has always charged 50.
            Assert.That(EconomyNumbers.PaidQuestPayout(30), Is.EqualTo(45));
            Assert.That(EconomyNumbers.PaidQuestPayout(50), Is.EqualTo(75));
            Assert.That(EconomyNumbers.PaidQuestPayout(100), Is.EqualTo(150));
        }

        [Test]
        public void DecorationRequestCompletion_PaysCostTimesMarkup()
        {
            var state = NewState();
            state.Wallet.Deposit(1000);
            var deco = state.Quests.GiveQuestTo(state.Dogs[2], QuestType.DecorationRequest, new Random(7));
            var chosen = deco.Options.First();
            var cost = ItemCatalog.Get(chosen).Cost.Value;
            var expectedPayout = EconomyNumbers.PaidQuestPayout(cost);

            var before = state.Wallet.Coins;
            Assert.That(state.Quests.AcceptWithChoice(deco, chosen), Is.True);
            state.Quests.NotifyDogArrivedHome(deco);
            state.Quests.DeliverPackage(deco);

            Assert.That(deco.Status, Is.EqualTo(QuestStatus.Completed));
            Assert.That(state.Wallet.Coins, Is.EqualTo(before - cost + expectedPayout));
            Assert.That(expectedPayout, Is.GreaterThan(cost));
        }

        [Test]
        public void FenceCompletion_PaysCostTimesMarkup_100To150()
        {
            var (state, quest, _) = ReadyFenceQuest();
            var before = state.Wallet.Coins;
            var fenceCost = ItemCatalog.Get(ItemCatalog.FenceItemName).Cost.Value;

            Assert.That(state.Quests.Accept(quest), Is.True);

            Assert.That(quest.Status, Is.EqualTo(QuestStatus.Completed));
            Assert.That(state.Wallet.Coins,
                Is.EqualTo(before - fenceCost + EconomyNumbers.PaidQuestPayout(fenceCost)));
            Assert.That(EconomyNumbers.PaidQuestPayout(fenceCost), Is.EqualTo(150));
        }

        [Test]
        public void FreeTypes_StillPayFlatFreePayout()
        {
            // LostItem and PestControl carry no cost — regression: the flat
            // free-type payout (#623: 20) is unchanged.
            var state = NewState();

            var lost = state.Quests.GiveQuestTo(state.Dogs[0], QuestType.LostItem, new Random(1));
            var beforeLost = state.Wallet.Coins;
            Assert.That(state.Quests.Accept(lost), Is.True);
            Assert.That(state.Quests.TapWorldPosition(lost.HiddenItemPosition.Value), Is.True);
            Assert.That(state.Wallet.Coins, Is.EqualTo(beforeLost + EconomyNumbers.QuestPayout));

            var pest = state.Quests.GiveQuestTo(state.Dogs[4], QuestType.PestControl, new Random(3));
            var beforePest = state.Wallet.Coins;
            Assert.That(state.Quests.Accept(pest), Is.True);
            Assert.That(state.Quests.SprayHouse(pest.TargetHouseId.Value), Is.True);
            Assert.That(state.Wallet.Coins, Is.EqualTo(beforePest + EconomyNumbers.QuestPayout));
        }

        [Test]
        public void EveryPaidCatalogEntry_PaysMoreThanItsCost()
        {
            // Invariant: no paid quest is ever a net loss.
            foreach (var item in ItemCatalog.Items.Where(i => i.Cost.HasValue))
            {
                Assert.That(EconomyNumbers.PaidQuestPayout(item.Cost.Value),
                    Is.GreaterThan(item.Cost.Value), $"{item.Name} must pay back more than its cost");
            }
        }

        [Test]
        public void MarkupIsDrawnFromTuningConfig()
        {
            Assert.That(EconomyNumbers.PaidQuestMarkup, Is.EqualTo(TuningConfig.Active.PaidQuestMarkup));

            TuningConfig.Active.PaidQuestMarkup = 2.0;
            Assert.That(EconomyNumbers.PaidQuestPayout(40), Is.EqualTo(80),
                "payout tracks the live TuningConfig markup");
        }

        [Test]
        public void DefaultMarkupIsOnePointFive()
        {
            Assert.That(new TuningConfig().PaidQuestMarkup, Is.EqualTo(1.5));
        }

        /// <summary>#318/#626: a real fence BuyGift quest through the production
        /// path — premium population so the fence enters the Gift pool, funded
        /// wallet, drawing BuyGift quests until the RNG yields the fence.</summary>
        private static (GameState State, Quest Quest, Dog Dog) ReadyFenceQuest()
        {
            for (var seed = 0; seed < 500; seed++)
            {
                var state = NewState();
                for (var i = state.Dogs.Count; i < QuestCostTiers.PremiumPopulationGate; i++)
                {
                    state.AddDog(new Dog($"extra-{i}", Breed.GermanShepherd, Personality.Brave, 1, false));
                }

                state.Wallet.Deposit(1000);
                var dog = state.Dogs[0];
                var quest = state.Quests.GiveQuestTo(dog, QuestType.BuyGift, new Random(seed));
                if (quest.ItemName == ItemCatalog.FenceItemName)
                {
                    return (state, quest, dog);
                }
            }

            throw new InvalidOperationException("No fence BuyGift quest produced across seeds.");
        }
    }
}
