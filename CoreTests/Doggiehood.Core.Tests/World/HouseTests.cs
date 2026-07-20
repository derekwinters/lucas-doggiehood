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
    }
}
