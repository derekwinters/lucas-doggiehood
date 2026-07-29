using System.Globalization;
using System.Threading;
using Doggiehood.Core.Economy;
using NUnit.Framework;

namespace Doggiehood.Core.Tests.Economy
{
    public class CurrencyChipTests
    {
        [Test]
        public void Label_ForANewGameBalance_ReadsBareZero()
        {
            // #296: the coin token region carries the "coins" meaning
            // (shared-components.md), so the chip shows a bare tabular number —
            // no "Coins: " prefix. A fresh save reads "0".
            Assert.That(CurrencyChip.Label(0), Is.EqualTo("0"));
        }

        [Test]
        public void Label_ShowsTheExactBalance()
        {
            Assert.That(CurrencyChip.Label(10), Is.EqualTo("10"));
            Assert.That(CurrencyChip.Label(340), Is.EqualTo("340"));
        }

        [Test]
        public void Label_GroupsThousandsWithCommas()
        {
            Assert.That(CurrencyChip.Label(1234), Is.EqualTo("1,234"));
            Assert.That(CurrencyChip.Label(1234567), Is.EqualTo("1,234,567"));
        }

        [Test]
        public void Label_IgnoresTheDeviceLocale()
        {
            // #159/#296: invariant-culture grouping so the chip doesn't drift by
            // device locale (e.g. de-DE would otherwise render "1.234").
            var original = Thread.CurrentThread.CurrentCulture;
            try
            {
                Thread.CurrentThread.CurrentCulture = new CultureInfo("de-DE");
                Assert.That(CurrencyChip.Label(1234), Is.EqualTo("1,234"));
            }
            finally
            {
                Thread.CurrentThread.CurrentCulture = original;
            }
        }
    }
}
