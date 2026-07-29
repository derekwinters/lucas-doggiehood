using Doggiehood.Core.World;
using NUnit.Framework;

namespace Doggiehood.Core.Tests.World
{
    /// <summary>
    /// #208 / docs/specs/ui/house-profile.md: the house profile view reads a
    /// house's level and vacancy from its Core data. <see cref="HouseProfile"/>
    /// is the engine-free presentation model behind the header level badge
    /// (`Lv N` + N-of-4 pips) and the footer Upgrade entry point (#59) — the
    /// Unity overlay is thin wiring on top of it. The upgrade flow's own
    /// confirmation UI is out of scope (#294); this model only surfaces the
    /// entry-point affordance's state (next cost, or a disabled Max-level).
    /// </summary>
    public class HouseProfileTests
    {
        private static House HouseAt(int level, bool vacant)
        {
            return new House(1, Quadrant.NorthWest, isVacant: vacant, level: level);
        }

        [Test]
        public void For_ReadsLevelAndVacancy_FromTheHouse()
        {
            var profile = HouseProfile.For(HouseAt(level: 2, vacant: false));

            Assert.That(profile.Level, Is.EqualTo(2));
            Assert.That(profile.IsVacant, Is.False);
        }

        [Test]
        public void LevelText_IsTheLvNBadgeLabel()
        {
            Assert.That(HouseProfile.For(HouseAt(3, false)).LevelText, Is.EqualTo("Lv 3"));
        }

        [Test]
        public void Pips_AreNOfFour_WithTheCurrentLevelFilled()
        {
            var profile = HouseProfile.For(HouseAt(2, false));

            Assert.That(profile.PipCount, Is.EqualTo(4),
                "the level cap (HouseUpgradeNumbers.MaxLevel) is the pip count");
            Assert.That(profile.FilledPipCount, Is.EqualTo(2),
                "filled pips = current level, so the headroom to upgrade reads at a glance");
        }

        [Test]
        public void OccupiedHouseBelowMax_OffersUpgrade_ShowingTheNextCost()
        {
            var profile = HouseProfile.For(HouseAt(2, false));

            Assert.That(profile.ShowsUpgradeAction, Is.True);
            Assert.That(profile.IsMaxLevel, Is.False);
            Assert.That(profile.UpgradeCost, Is.EqualTo(200),
                "the level 2 -> 3 step costs 200 (HouseUpgradeNumbers.CostToLevel3)");
            Assert.That(profile.UpgradeButtonText, Is.EqualTo("Upgrade · 200"));
        }

        [TestCase(1, 100)]
        [TestCase(2, 200)]
        [TestCase(3, 400)]
        public void UpgradeCost_IsTheDoublingLadder(int level, int expectedCost)
        {
            Assert.That(HouseProfile.For(HouseAt(level, false)).UpgradeCost, Is.EqualTo(expectedCost));
        }

        [Test]
        public void MaxLevelHouse_DisablesUpgradeIntoAMaxLevelState()
        {
            var profile = HouseProfile.For(HouseAt(4, false));

            Assert.That(profile.ShowsUpgradeAction, Is.True,
                "the footer still shows the action, disabled, at the cap");
            Assert.That(profile.IsMaxLevel, Is.True);
            Assert.That(profile.UpgradeButtonText, Is.EqualTo("Max level"));
        }

        [Test]
        public void VacantHouse_OffersNoUpgradeAction_AndCarriesTheEmptyStateLine()
        {
            var profile = HouseProfile.For(HouseAt(1, true));

            Assert.That(profile.IsVacant, Is.True);
            Assert.That(profile.ShowsUpgradeAction, Is.False,
                "no Upgrade action is offered for a vacant house (house-profile.md)");
            Assert.That(profile.EmptyStateText, Is.EqualTo("No dogs live here yet."));
        }
    }
}
