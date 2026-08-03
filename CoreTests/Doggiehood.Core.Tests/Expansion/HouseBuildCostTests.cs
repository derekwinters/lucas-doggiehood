using Doggiehood.Core.Expansion;
using NUnit.Framework;

namespace Doggiehood.Core.Tests.Expansion
{
    /// <summary>
    /// #540: the house-build cost is a single named, tunable balance function —
    /// a 50-coin base that rises by <see cref="HouseBuildNumbers.PerBatchStep"/>
    /// (5) for every <see cref="HouseBuildNumbers.HousesPerStep"/> (4) houses the
    /// player has already built: <c>BaseCost + PerBatchStep * floor(built /
    /// HousesPerStep)</c>. Builds 1-4 are at the base, 5-8 at base+5, and so on.
    /// The count is player-built houses only (the 4 starting houses are excluded
    /// by the caller), so the first build is at the base. No upper cap.
    /// </summary>
    public class HouseBuildCostTests
    {
        [Test]
        public void BaseCost_IsFiftyCoins()
        {
            Assert.That(HouseBuildNumbers.BaseCost, Is.EqualTo(50));
        }

        [Test]
        public void PerBatchStep_IsFiveCoins_EveryFourHouses()
        {
            Assert.That(HouseBuildNumbers.PerBatchStep, Is.EqualTo(5));
            Assert.That(HouseBuildNumbers.HousesPerStep, Is.EqualTo(4));
        }

        [Test]
        public void FirstBuild_WithNoPlayerHousesYet_CostsTheBase()
        {
            Assert.That(HouseBuildNumbers.Cost(housesBuilt: 0), Is.EqualTo(HouseBuildNumbers.BaseCost));
        }

        [Test]
        public void Cost_RisesByFiveEveryFourHouses()
        {
            // Builds 1-4 (0..3 already built) stay at the base.
            Assert.That(HouseBuildNumbers.Cost(housesBuilt: 0), Is.EqualTo(50));
            Assert.That(HouseBuildNumbers.Cost(housesBuilt: 3), Is.EqualTo(50));
            // Build 5 (4 already built) is the first +5 step.
            Assert.That(HouseBuildNumbers.Cost(housesBuilt: 4), Is.EqualTo(55));
            Assert.That(HouseBuildNumbers.Cost(housesBuilt: 7), Is.EqualTo(55));
            // Build 9 (8 already built) is the second step.
            Assert.That(HouseBuildNumbers.Cost(housesBuilt: 8), Is.EqualTo(60));
        }

        [Test]
        public void Cost_HasNoUpperCap()
        {
            Assert.That(HouseBuildNumbers.Cost(housesBuilt: 100),
                Is.EqualTo(HouseBuildNumbers.BaseCost + HouseBuildNumbers.PerBatchStep * (100 / HouseBuildNumbers.HousesPerStep)),
                "the step curve keeps rising with no ceiling");
        }
    }
}
