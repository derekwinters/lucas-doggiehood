using Doggiehood.Core.Economy;
using Doggiehood.Core.Quests;
using NUnit.Framework;

namespace Doggiehood.Core.Tests.Economy
{
    public class WalletTests
    {
        [Test]
        public void StartsEmpty()
        {
            Assert.That(new Wallet().Coins, Is.EqualTo(0));
        }

        [Test]
        public void Deposit_AddsCoins()
        {
            var wallet = new Wallet();
            wallet.Deposit(10);
            Assert.That(wallet.Coins, Is.EqualTo(10));
        }

        [Test]
        public void TrySpend_DeductsWhenAffordable()
        {
            var wallet = new Wallet();
            wallet.Deposit(50);

            Assert.That(wallet.TrySpend(30), Is.True);
            Assert.That(wallet.Coins, Is.EqualTo(20));
        }

        [Test]
        public void TrySpend_RejectsOverdraft_BalanceUntouched()
        {
            // #25: currency can never go negative.
            var wallet = new Wallet();
            wallet.Deposit(20);

            Assert.That(wallet.TrySpend(30), Is.False);
            Assert.That(wallet.Coins, Is.EqualTo(20));
        }

        [Test]
        public void NegativeAmounts_AreRejected()
        {
            var wallet = new Wallet();
            Assert.That(() => wallet.Deposit(-5), Throws.ArgumentException);
            Assert.That(() => wallet.TrySpend(-5), Throws.ArgumentException);
        }

        [Test]
        public void Deposit_RaisesCoinsChanged_WithThePositiveDelta()
        {
            // #542: the HUD chip animation needs to know a change happened and
            // by how much, to spawn the floating "+N" delta and drive the tween.
            var wallet = new Wallet();
            int? seen = null;
            wallet.CoinsChanged += delta => seen = delta;

            wallet.Deposit(100);

            Assert.That(seen, Is.EqualTo(100));
        }

        [Test]
        public void TrySpend_RaisesCoinsChanged_WithTheNegativeDelta()
        {
            // #542: a spend animates a red "−N" — the event carries the signed
            // (negative) change.
            var wallet = new Wallet();
            wallet.Deposit(100);
            int? seen = null;
            wallet.CoinsChanged += delta => seen = delta;

            Assert.That(wallet.TrySpend(50), Is.True);
            Assert.That(seen, Is.EqualTo(-50));
        }

        [Test]
        public void TrySpend_Rejected_RaisesNothing()
        {
            // #542: a rejected spend leaves the balance untouched, so there is
            // nothing to animate — no event.
            var wallet = new Wallet();
            wallet.Deposit(20);
            var raised = 0;
            wallet.CoinsChanged += _ => raised++;

            Assert.That(wallet.TrySpend(30), Is.False);
            Assert.That(raised, Is.EqualTo(0));
        }

        [Test]
        public void ZeroValuedChange_RaisesNothing()
        {
            // A degenerate zero deposit/spend is not a visible change — no delta
            // label should be spawned, so no event fires.
            var wallet = new Wallet();
            var raised = 0;
            wallet.CoinsChanged += _ => raised++;

            wallet.Deposit(0);
            wallet.TrySpend(0);

            Assert.That(raised, Is.EqualTo(0));
        }

        [Test]
        public void CanAfford_TrueOnlyWhenBalanceCoversTheAmount()
        {
            // #186: lets the UI query affordability (e.g. to grey out a buy
            // pill) without reaching into Coins and re-implementing the
            // comparison itself.
            var wallet = new Wallet();
            wallet.Deposit(20);

            Assert.That(wallet.CanAfford(20), Is.True, "exact balance is affordable");
            Assert.That(wallet.CanAfford(19), Is.True);
            Assert.That(wallet.CanAfford(21), Is.False);
        }
    }

    public class EconomyNumbersTests
    {
        [Test]
        public void QuestPayout_IsTwentyCoins_ForFreeTypes()
        {
            // #62: flat payout for the free quest types (LostItem / PestControl),
            // defined once centrally. #623: raised 10 -> 20 to roughly double the
            // early-game earn rate. #626: paid types are earners instead — they
            // pay cost × markup (see PaidQuestPayout), not this flat value.
            Assert.That(EconomyNumbers.QuestPayout, Is.EqualTo(20));
            Assert.That(EconomyNumbers.PaidQuestMarkup, Is.EqualTo(1.5));
        }

        [Test]
        public void EveryPurchasableCatalogItemCost_FallsInADefinedTierBand()
        {
            // #62/#190 + #317: purchasable gifts/decorations are priced within a
            // defined cost tier band — Starter (30-50), Mid (60-90), or Premium
            // (100+, #318's fence). Find-only items (no Gift/Decoration
            // eligibility) carry no cost.
            Assert.That(ItemCatalog.Items, Is.Not.Empty);

            foreach (var item in ItemCatalog.Items)
            {
                var purchasable = item.IsEligibleFor(ItemEligibility.Gift)
                    || item.IsEligibleFor(ItemEligibility.Decoration);

                if (purchasable)
                {
                    Assert.That(item.Cost, Is.Not.Null, item.Name);
                    var cost = item.Cost.Value;
                    var inStarter = cost >= QuestCostTiers.StarterMinCost
                        && cost <= QuestCostTiers.StarterMaxCost;
                    var inMid = cost >= QuestCostTiers.MidMinCost && cost <= QuestCostTiers.MidMaxCost;
                    var inPremium = cost >= QuestCostTiers.PremiumMinCost;
                    Assert.That(inStarter || inMid || inPremium, Is.True,
                        $"{item.Name} costs {cost}, which falls in no defined tier band");
                }
                else
                {
                    Assert.That(item.Cost, Is.Null, item.Name + " is find-only and should carry no cost");
                }
            }
        }

        [Test]
        public void ItemCosts_AreLookedUpCentrally()
        {
            var pool = ItemCatalog.Get("pool");

            Assert.That(pool.Cost, Is.EqualTo(ItemCatalog.Get("pool").Cost));
            Assert.That(() => ItemCatalog.Get("nonexistent-item"), Throws.ArgumentException);
        }
    }
}
