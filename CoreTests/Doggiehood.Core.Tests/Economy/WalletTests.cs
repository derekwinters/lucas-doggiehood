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
        public void EveryCatalogItemCost_IsInTheThirtyToFiftyRange()
        {
            // #62: gifts/decorations cost 3-5 quests' worth of saving.
            Assert.That(ItemCatalog.Items, Is.Not.Empty);

            foreach (var item in ItemCatalog.Items)
            {
                Assert.That(item.Cost, Is.InRange(30, 50), item.Name);
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
