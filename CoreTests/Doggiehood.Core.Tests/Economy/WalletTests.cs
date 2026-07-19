using Doggiehood.Core.Economy;
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
    }

    public class EconomyNumbersTests
    {
        [Test]
        public void QuestPayout_IsTenCoins_ForAllQuestTypes()
        {
            // #62: flat payout regardless of type, defined once centrally.
            Assert.That(EconomyNumbers.QuestPayout, Is.EqualTo(10));
        }

        [Test]
        public void EveryPurchasableCatalogItemCost_IsInTheThirtyToFiftyRange()
        {
            // #62/#190: gifts/decorations cost 3-5 quests' worth of saving.
            // Find-only items (no Gift/Decoration eligibility) carry no cost.
            Assert.That(ItemCatalog.Items, Is.Not.Empty);

            foreach (var item in ItemCatalog.Items)
            {
                var purchasable = item.IsEligibleFor(ItemEligibility.Gift)
                    || item.IsEligibleFor(ItemEligibility.Decoration);

                if (purchasable)
                {
                    Assert.That(item.Cost, Is.Not.Null, item.Name);
                    Assert.That(item.Cost.Value, Is.InRange(30, 50), item.Name);
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
