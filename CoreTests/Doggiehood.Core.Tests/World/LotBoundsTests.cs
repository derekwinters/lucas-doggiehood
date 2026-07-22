using System;
using System.Linq;
using Doggiehood.Core.World;
using NUnit.Framework;

namespace Doggiehood.Core.Tests.World
{
    /// <summary>
    /// #222: a property (lot) is one quadrant of a tile. Bounds are sized
    /// to a tile-quadrant (WorldDimensions.TileSize / 2 per side) and
    /// positioned on the lot's own Quadrant, so the 4 starting lots'
    /// bounds exactly tile the FourWay's 60m tile with no gap or overlap.
    /// Front-yard / back-yard regions split those bounds relative to
    /// HousePlacement.FrontFacing, excluding the house footprint.
    /// </summary>
    public class LotBoundsTests
    {
        private const float Epsilon = 0.001f;

        [Test]
        public void QuadrantBounds_IsSizedToOneTileQuadrant()
        {
            var half = WorldDimensions.TileSize / 4f;
            foreach (var lot in NeighborhoodLayout.HouseLots)
            {
                var bounds = LotBounds.QuadrantBounds(lot);
                Assert.That(bounds.Width, Is.EqualTo(half * 2f).Within(Epsilon),
                    $"lot {lot.HouseId}: width must be half the tile size (one tile-quadrant)");
                Assert.That(bounds.Depth, Is.EqualTo(half * 2f).Within(Epsilon),
                    $"lot {lot.HouseId}: depth must be half the tile size (one tile-quadrant)");
            }
        }

        [Test]
        public void QuadrantBounds_IsPositionedOnTheLotsOwnQuadrant()
        {
            var half = WorldDimensions.TileSize / 4f;
            var expected = new[]
            {
                (Quadrant.NorthEast, new LotRect(0f, half * 2f, 0f, half * 2f)),
                (Quadrant.NorthWest, new LotRect(-half * 2f, 0f, 0f, half * 2f)),
                (Quadrant.SouthEast, new LotRect(0f, half * 2f, -half * 2f, 0f)),
                (Quadrant.SouthWest, new LotRect(-half * 2f, 0f, -half * 2f, 0f)),
            };

            foreach (var (quadrant, rect) in expected)
            {
                var lot = NeighborhoodLayout.HouseLots.Single(l => l.Quadrant == quadrant);
                var bounds = LotBounds.QuadrantBounds(lot);
                AssertRectsEqual(bounds, rect, $"lot {lot.HouseId} ({quadrant})");
            }
        }

        [Test]
        public void QuadrantBounds_StayWithinTheTile_AndDoNotOverlapAnyOtherLot()
        {
            var half = WorldDimensions.TileSize / 2f;
            var allBounds = NeighborhoodLayout.HouseLots
                .Select(lot => (lot, bounds: LotBounds.QuadrantBounds(lot)))
                .ToList();

            foreach (var (lot, bounds) in allBounds)
            {
                Assert.That(bounds.MinX, Is.GreaterThanOrEqualTo(-half),
                    $"lot {lot.HouseId}: bounds must not spill past the west tile edge");
                Assert.That(bounds.MaxX, Is.LessThanOrEqualTo(half),
                    $"lot {lot.HouseId}: bounds must not spill past the east tile edge");
                Assert.That(bounds.MinZ, Is.GreaterThanOrEqualTo(-half),
                    $"lot {lot.HouseId}: bounds must not spill past the south tile edge");
                Assert.That(bounds.MaxZ, Is.LessThanOrEqualTo(half),
                    $"lot {lot.HouseId}: bounds must not spill past the north tile edge");
            }

            foreach (var (lotA, boundsA) in allBounds)
            {
                foreach (var (lotB, boundsB) in allBounds)
                {
                    if (lotA.HouseId == lotB.HouseId)
                    {
                        continue;
                    }

                    Assert.That(boundsA.Overlaps(boundsB), Is.False,
                        $"lot {lotA.HouseId} bounds must not spill into lot {lotB.HouseId}'s quadrant"
                        + " (across the road)");
                }
            }
        }

        [Test]
        public void FrontYard_And_BackYard_SplitOnTheFrontFacingAxis_AndStayInsideBounds()
        {
            foreach (var lot in NeighborhoodLayout.HouseLots)
            {
                var bounds = LotBounds.QuadrantBounds(lot);
                var front = LotBounds.FrontYard(lot);
                var back = LotBounds.BackYard(lot);

                Assert.That(bounds.Contains(front), Is.True,
                    $"lot {lot.HouseId}: front yard must stay within the lot's quadrant bounds");
                Assert.That(bounds.Contains(back), Is.True,
                    $"lot {lot.HouseId}: back yard must stay within the lot's quadrant bounds");

                Assert.That(front.Overlaps(back), Is.False,
                    $"lot {lot.HouseId}: front and back yard must not overlap each other");

                // The front yard sits on the street side of the house (the
                // direction HousePlacement.FrontFacing points).
                var facing = HousePlacement.FrontFacing(lot);
                var house = HousePlacement.Position(lot, HousePlacement.KitScale);
                var frontCenter = front.Center;
                var backCenter = back.Center;
                var frontAlongFacing =
                    (frontCenter.X - house.X) * facing.X + (frontCenter.Z - house.Z) * facing.Z;
                var backAlongFacing =
                    (backCenter.X - house.X) * facing.X + (backCenter.Z - house.Z) * facing.Z;
                Assert.That(frontAlongFacing, Is.GreaterThan(backAlongFacing),
                    $"lot {lot.HouseId}: front yard must sit further along FrontFacing than back yard");
            }
        }

        [Test]
        public void FrontYard_And_BackYard_ExcludeTheHouseFootprint()
        {
            foreach (var lot in NeighborhoodLayout.HouseLots)
            {
                var front = LotBounds.FrontYard(lot);
                var back = LotBounds.BackYard(lot);

                var facing = HousePlacement.FrontFacing(lot);
                var house = HousePlacement.Position(lot, HousePlacement.KitScale);
                var model = HouseModelCatalog.ForHouse(lot.HouseId);
                var halfWidth = HousePlacement.KitScale * model.FootprintX / 2f;
                var halfDepth = HousePlacement.KitScale * model.FootprintZ / 2f;

                // House footprint as an axis-aligned rect (facing is always
                // cardinal, so the footprint is axis-aligned too).
                var footprint = facing.X != 0f
                    ? new LotRect(house.X - halfDepth, house.X + halfDepth,
                        house.Z - halfWidth, house.Z + halfWidth)
                    : new LotRect(house.X - halfWidth, house.X + halfWidth,
                        house.Z - halfDepth, house.Z + halfDepth);

                Assert.That(front.Overlaps(footprint), Is.False,
                    $"lot {lot.HouseId}: front yard must exclude the house footprint");
                Assert.That(back.Overlaps(footprint), Is.False,
                    $"lot {lot.HouseId}: back yard must exclude the house footprint");
            }
        }

        [Test]
        public void QuadrantBounds_DerivesFromTheLotsQuadrantField_NotItsHandPlacedPosition()
        {
            // #222: property = one tile-quadrant is a fact about the lot's
            // Quadrant, not about NeighborhoodLayout.LotDistanceFromCenter
            // (a separate, hand-picked house-placement choice) — so bounds
            // for two lots in the same quadrant must be identical even if
            // their (hypothetical) house-placement positions differ.
            var half = WorldDimensions.TileSize / 4f;
            var atStandardDistance = new HouseLot(101, Quadrant.NorthEast, new GridPoint(14f, 14f));
            var atADifferentDistance = new HouseLot(102, Quadrant.NorthEast, new GridPoint(20f, 9f));

            var boundsA = LotBounds.QuadrantBounds(atStandardDistance);
            var boundsB = LotBounds.QuadrantBounds(atADifferentDistance);

            AssertRectsEqual(boundsA, new LotRect(0f, half * 2f, 0f, half * 2f), "standard-distance lot");
            AssertRectsEqual(boundsB, boundsA, "different-distance lot in the same quadrant");
        }

        private static void AssertRectsEqual(LotRect actual, LotRect expected, string label)
        {
            Assert.That(actual.MinX, Is.EqualTo(expected.MinX).Within(Epsilon), label + " MinX");
            Assert.That(actual.MaxX, Is.EqualTo(expected.MaxX).Within(Epsilon), label + " MaxX");
            Assert.That(actual.MinZ, Is.EqualTo(expected.MinZ).Within(Epsilon), label + " MinZ");
            Assert.That(actual.MaxZ, Is.EqualTo(expected.MaxZ).Within(Epsilon), label + " MaxZ");
        }
    }
}
