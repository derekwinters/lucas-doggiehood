using System.Linq;
using Doggiehood.Core.World;
using NUnit.Framework;

namespace Doggiehood.Core.Tests.World
{
    public class NeighborhoodLayoutTests
    {
        [Test]
        public void Streets_ContainsExactlyTwoStreets()
        {
            Assert.That(NeighborhoodLayout.Streets.Count, Is.EqualTo(2));
        }

        [Test]
        public void Streets_FormASingleIntersection()
        {
            // Two axis-aligned streets with different orientations cross
            // exactly once (#7, #38).
            var orientations = NeighborhoodLayout.Streets.Select(s => s.Orientation).ToList();
            Assert.That(orientations, Is.Unique);
            Assert.That(orientations, Does.Contain(StreetOrientation.NorthSouth));
            Assert.That(orientations, Does.Contain(StreetOrientation.EastWest));
        }

        [Test]
        public void HouseLots_HasExactlyFour_OnePerQuadrant()
        {
            Assert.That(NeighborhoodLayout.HouseLots.Count, Is.EqualTo(4));
            Assert.That(NeighborhoodLayout.HouseLots.Select(lot => lot.Quadrant), Is.Unique);
        }

        [Test]
        public void HouseLots_PositionsLieInsideTheirQuadrant()
        {
            foreach (var lot in NeighborhoodLayout.HouseLots)
            {
                var expectEastOfCenter = lot.Quadrant == Quadrant.NorthEast || lot.Quadrant == Quadrant.SouthEast;
                var expectNorthOfCenter = lot.Quadrant == Quadrant.NorthEast || lot.Quadrant == Quadrant.NorthWest;

                Assert.That(lot.Position.X > 0, Is.EqualTo(expectEastOfCenter),
                    $"lot {lot.HouseId} X={lot.Position.X} inconsistent with {lot.Quadrant}");
                Assert.That(lot.Position.Z > 0, Is.EqualTo(expectNorthOfCenter),
                    $"lot {lot.HouseId} Z={lot.Position.Z} inconsistent with {lot.Quadrant}");
            }
        }

        [Test]
        public void GetHouseLot_ReturnsConsistentResultsAcrossRepeatedCalls()
        {
            foreach (var lot in NeighborhoodLayout.HouseLots)
            {
                var first = NeighborhoodLayout.GetHouseLot(lot.HouseId);
                var second = NeighborhoodLayout.GetHouseLot(lot.HouseId);

                Assert.That(first.HouseId, Is.EqualTo(lot.HouseId));
                Assert.That(second.Quadrant, Is.EqualTo(first.Quadrant));
                Assert.That(second.Position, Is.EqualTo(first.Position));
            }
        }

        [Test]
        public void GetHouseLot_UnknownIdThrows()
        {
            Assert.That(() => NeighborhoodLayout.GetHouseLot(999), Throws.ArgumentException);
        }

        [Test]
        public void Roads_ContainsOneRoadPerStreet_CenteredOnTheIntersection()
        {
            // #106: NeighborhoodLayout exposes real Road geometry built
            // from its own Streets, so downstream code (WalkNetwork,
            // WorldBuilder) never has to re-derive it.
            Assert.That(NeighborhoodLayout.Roads.Count, Is.EqualTo(NeighborhoodLayout.Streets.Count));

            foreach (var road in NeighborhoodLayout.Roads)
            {
                Assert.That(road.Center, Is.EqualTo(NeighborhoodLayout.Intersection));
                Assert.That(road.HalfLength, Is.EqualTo(NeighborhoodLayout.StreetHalfLength));
            }

            Assert.That(NeighborhoodLayout.Roads.Select(r => r.Orientation), Is.Unique);
        }
    }
}
