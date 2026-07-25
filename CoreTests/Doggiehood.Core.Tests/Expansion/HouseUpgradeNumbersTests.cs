using Doggiehood.Core.Expansion;
using NUnit.Framework;

namespace Doggiehood.Core.Tests.Expansion
{
    public class HouseUpgradeNumbersTests
    {
        [Test]
        public void UpgradeCosts_DoublePerLevel_100_200_400()
        {
            // #59 (Derek, 2026-07-14): the L1->L2/L3/L4 costs double each
            // step. Named, tunable constants — the single home for house
            // upgrade pricing, mirroring HouseBuildNumbers.
            Assert.That(HouseUpgradeNumbers.CostToLevel2, Is.EqualTo(100));
            Assert.That(HouseUpgradeNumbers.CostToLevel3, Is.EqualTo(200));
            Assert.That(HouseUpgradeNumbers.CostToLevel4, Is.EqualTo(400));
        }

        [Test]
        public void MaxLevel_IsFour()
        {
            Assert.That(HouseUpgradeNumbers.MaxLevel, Is.EqualTo(4));
        }

        [Test]
        public void CostToReach_ResolvesEachUpgradeStep()
        {
            Assert.That(HouseUpgradeNumbers.CostToReach(2), Is.EqualTo(HouseUpgradeNumbers.CostToLevel2));
            Assert.That(HouseUpgradeNumbers.CostToReach(3), Is.EqualTo(HouseUpgradeNumbers.CostToLevel3));
            Assert.That(HouseUpgradeNumbers.CostToReach(4), Is.EqualTo(HouseUpgradeNumbers.CostToLevel4));
        }

        [Test]
        public void CostToReach_ThrowsForALevelOutsideTheUpgradeRange()
        {
            // Level 1 is the as-built level (nothing to pay for) and level 5
            // is past the ceiling — neither is a reachable upgrade step.
            Assert.That(() => HouseUpgradeNumbers.CostToReach(1), Throws.InstanceOf<System.ArgumentOutOfRangeException>());
            Assert.That(() => HouseUpgradeNumbers.CostToReach(HouseUpgradeNumbers.MaxLevel + 1),
                Throws.InstanceOf<System.ArgumentOutOfRangeException>());
        }
    }
}
