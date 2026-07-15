using System.Globalization;
using System.Threading;
using Doggiehood.Core.Economy;
using NUnit.Framework;

namespace Doggiehood.Core.Tests.Economy
{
    public class CurrencyChipTests
    {
        [Test]
        public void Label_ForANewGameBalance_ReadsCoinsZero()
        {
            // #159: the HUD chip on a fresh save shows "Coins: 0".
            Assert.That(CurrencyChip.Label(0), Is.EqualTo("Coins: 0"));
        }

        [Test]
        public void Label_ShowsTheExactBalance()
        {
            Assert.That(CurrencyChip.Label(10), Is.EqualTo("Coins: 10"));
            Assert.That(CurrencyChip.Label(340), Is.EqualTo("Coins: 340"));
        }

        [Test]
        public void Label_GroupsThousandsWithCommas()
        {
            Assert.That(CurrencyChip.Label(1234), Is.EqualTo("Coins: 1,234"));
            Assert.That(CurrencyChip.Label(1234567), Is.EqualTo("Coins: 1,234,567"));
        }

        [Test]
        public void Label_IgnoresTheDeviceLocale()
        {
            // #159: invariant-culture grouping so the chip doesn't drift by
            // device locale (e.g. de-DE would otherwise render "1.234").
            var original = Thread.CurrentThread.CurrentCulture;
            try
            {
                Thread.CurrentThread.CurrentCulture = new CultureInfo("de-DE");
                Assert.That(CurrencyChip.Label(1234), Is.EqualTo("Coins: 1,234"));
            }
            finally
            {
                Thread.CurrentThread.CurrentCulture = original;
            }
        }
    }
}
