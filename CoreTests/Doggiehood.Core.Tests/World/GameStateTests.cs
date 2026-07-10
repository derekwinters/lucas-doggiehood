using System.Linq;
using Doggiehood.Core.World;
using NUnit.Framework;

namespace Doggiehood.Core.Tests.World
{
    public class GameStateTests
    {
        [Test]
        public void CreateNew_ContainsExactlyFourHouses()
        {
            Assert.That(GameState.CreateNew().Houses.Count, Is.EqualTo(4));
        }

        [Test]
        public void Houses_HaveUniqueIds()
        {
            Assert.That(GameState.CreateNew().Houses.Select(h => h.Id), Is.Unique);
        }

        [Test]
        public void Houses_CoverAllFourQuadrants()
        {
            var quadrants = GameState.CreateNew().Houses.Select(h => h.Quadrant).ToList();

            Assert.That(quadrants, Is.Unique);
            Assert.That(quadrants.Count, Is.EqualTo(4));
        }

        [Test]
        public void Houses_MatchTheNeighborhoodLayoutLots()
        {
            foreach (var house in GameState.CreateNew().Houses)
            {
                var lot = NeighborhoodLayout.GetHouseLot(house.Id);
                Assert.That(house.Quadrant, Is.EqualTo(lot.Quadrant));
            }
        }
    }
}
