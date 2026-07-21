using Doggiehood.Core.Expansion;
using NUnit.Framework;

namespace Doggiehood.Core.Tests.Expansion
{
    /// <summary>
    /// #56: the nth zone's unlock cost, base + (n-1) * step, with both
    /// tuning values as named ZoneUnlockNumbers constants
    /// (docs/specs/expansion.md#pricing: "100 coins for the first zone,
    /// +100 per subsequent zone").
    /// </summary>
    public class ZoneUnlockTests
    {
        [Test]
        public void CostForZoneNumber_FirstZone_IsBaseCost()
        {
            Assert.That(ZoneUnlock.CostForZoneNumber(1), Is.EqualTo(ZoneUnlockNumbers.BaseCost));
        }

        [TestCase(1, 100)]
        [TestCase(2, 200)]
        [TestCase(3, 300)]
        [TestCase(4, 400)]
        public void CostForZoneNumber_FollowsBasePlusStepTimesZoneIndex(int zoneNumber, int expectedCost)
        {
            Assert.That(ZoneUnlock.CostForZoneNumber(zoneNumber), Is.EqualTo(expectedCost));
        }

        [Test]
        public void CostForZoneNumber_RejectsAZeroOrNegativeZoneNumber()
        {
            Assert.Throws<System.ArgumentOutOfRangeException>(() => ZoneUnlock.CostForZoneNumber(0));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => ZoneUnlock.CostForZoneNumber(-1));
        }
    }
}
