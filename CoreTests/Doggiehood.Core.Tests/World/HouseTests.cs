using Doggiehood.Core.World;
using NUnit.Framework;

namespace Doggiehood.Core.Tests.World
{
    public class HouseTests
    {
        [Test]
        public void NewlyBuiltHouse_ReportsVacant()
        {
            // #58: a freshly built house has no dog living in it yet, and
            // displays that state until #54's move-in system fills it.
            var house = new House(99, Quadrant.NorthEast);

            Assert.That(house.IsVacant, Is.True);
        }

        [Test]
        public void House_CanBeConstructedAlreadyOccupied()
        {
            // GameState's 4 starting houses already have dogs living in
            // them at CreateNew() (#63) — they must not report vacant.
            var house = new House(99, Quadrant.NorthEast, isVacant: false);

            Assert.That(house.IsVacant, Is.False);
        }

        [Test]
        public void MarkOccupied_FlipsVacancyToOccupied()
        {
            var house = new House(99, Quadrant.NorthEast);

            house.MarkOccupied();

            Assert.That(house.IsVacant, Is.False);
        }

        [Test]
        public void NewlyBuiltHouse_DefaultsToLevelOne()
        {
            // #57/#59: a house built via GameState.TryBuildHouse starts at
            // level 1 — the full upgrade path (levels 2-4) is #59's job,
            // this just pins the starting value.
            var house = new House(99, Quadrant.NorthEast);

            Assert.That(house.Level, Is.EqualTo(1));
            Assert.That(house.Level, Is.EqualTo(House.InitialLevel));
        }

        [Test]
        public void House_CanBeConstructedAtAnExplicitLevel()
        {
            var house = new House(99, Quadrant.NorthEast, isVacant: false, level: 3);

            Assert.That(house.Level, Is.EqualTo(3));
        }

        [Test]
        public void RaiseLevel_IncrementsTheLevelByOne()
        {
            // #59: the mutating upgrade path, mirroring MarkOccupied — a
            // one-way increment with no ceremony of its own. The max-level
            // and cost guards live in GameState.TryUpgradeHouse (mirroring
            // how MarkOccupied leaves the vacancy decision to the move-in
            // system), so a house's level only ever climbs through that
            // validated entry point.
            var house = new House(99, Quadrant.NorthEast);

            house.RaiseLevel();

            Assert.That(house.Level, Is.EqualTo(House.InitialLevel + 1));
        }
    }
}
