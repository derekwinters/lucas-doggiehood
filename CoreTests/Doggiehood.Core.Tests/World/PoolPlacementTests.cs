using System;
using System.Collections.Generic;
using Doggiehood.Core.Economy;
using Doggiehood.Core.World;
using NUnit.Framework;

namespace Doggiehood.Core.Tests.World
{
    /// <summary>
    /// #740: the graybox backyard pool a delivered "pool" gift leaves in a
    /// dog's yard — its dimensions (derived from the RENDERED ADULT DOG, not
    /// hand-picked meters) and its placement (deterministic per lot, inside
    /// the back yard, clear of the house, the fence line and every other yard
    /// object).
    /// </summary>
    public class PoolPlacementTests
    {
        private const float Tolerance = 0.0001f;

        [Test]
        public void PoolDimensions_DeriveFromTheRenderedAdultDog()
        {
            // #740, Derek's direction: "a cylinder about the height of a dog,
            // about the width of two dogs". Nothing here may be a hand-picked
            // meter figure — every dimension is the Cube Pets model's own
            // geometry at DogView's ADULT scale, so retuning the dog scale
            // retunes the pool with it.
            Assert.That(PoolPlacement.AdultDogScale, Is.EqualTo(1f).Within(Tolerance),
                "DogView renders an adult dog at scale 1 (puppies at 0.55)");

            Assert.That(PoolPlacement.AdultDogHeight,
                Is.EqualTo(PoolPlacement.DogModelHeight * PoolPlacement.AdultDogScale).Within(Tolerance));
            Assert.That(PoolPlacement.AdultDogWidth,
                Is.EqualTo(PoolPlacement.DogModelWidth * PoolPlacement.AdultDogScale).Within(Tolerance));

            Assert.That(PoolPlacement.PoolHeight, Is.EqualTo(PoolPlacement.AdultDogHeight).Within(Tolerance),
                "the pool stands about one adult dog tall");
            Assert.That(PoolPlacement.PoolOuterDiameter,
                Is.EqualTo(PoolPlacement.AdultDogWidth * PoolPlacement.PoolWidthInDogs).Within(Tolerance),
                "the pool is about two adult dogs wide");
            Assert.That(PoolPlacement.PoolFootprintRadius,
                Is.EqualTo(PoolPlacement.PoolOuterDiameter / 2f).Within(Tolerance));
        }

        [Test]
        public void PoolInterior_IsInsetWithinTheShell_AndSitsBelowTheRim()
        {
            // #740, Derek: "gray outer surface, and blue interior that is
            // slightly lower than the rest of the cylinder" — so the water
            // reads as an open container from the fixed camera angle, never a
            // solid drum.
            Assert.That(PoolPlacement.PoolInnerDiameter, Is.LessThan(PoolPlacement.PoolOuterDiameter),
                "the water surface is inset within the shell wall");
            Assert.That(PoolPlacement.PoolInnerDiameter,
                Is.EqualTo(PoolPlacement.PoolOuterDiameter - 2f * PoolPlacement.PoolWallThickness).Within(Tolerance));

            Assert.That(PoolPlacement.PoolWaterSurfaceHeight, Is.LessThan(PoolPlacement.PoolHeight),
                "the water sits below the shell rim");
            Assert.That(PoolPlacement.PoolWaterSurfaceHeight,
                Is.EqualTo(PoolPlacement.PoolHeight - PoolPlacement.PoolWaterDropBelowRim).Within(Tolerance));
            Assert.That(PoolPlacement.PoolWaterDropBelowRim, Is.LessThan(PoolPlacement.PoolHeight / 2f),
                "'slightly' lower — the drop is a lip, not half the pool");
        }

        // -------------------------------------------------------------
        // Pure placement.
        // -------------------------------------------------------------

        private static LotRect SpaciousBackYard => new LotRect(-20f, 20f, -20f, 20f);

        private static LotRect NoHouse => new LotRect(1000f, 1001f, 1000f, 1001f);

        [Test]
        public void TryFindPosition_InAnUnobstructedBackYard_KeepsItsWholeFootprintInside()
        {
            var found = PoolPlacement.TryFindPosition(
                SpaciousBackYard, NoHouse, Array.Empty<FenceRun>(), Array.Empty<YardObstacle>(),
                seed: 1, out var position);

            Assert.That(found, Is.True, "a spacious empty back yard always has room for one pool");
            Assert.That(SpaciousBackYard.Contains(position), Is.True,
                "the pool never sits outside its own back yard");

            var clearance = PoolPlacement.PoolFootprintRadius + PoolPlacement.ComfortMargin;
            Assert.That(position.X - SpaciousBackYard.MinX, Is.GreaterThanOrEqualTo(clearance - Tolerance));
            Assert.That(SpaciousBackYard.MaxX - position.X, Is.GreaterThanOrEqualTo(clearance - Tolerance));
            Assert.That(position.Z - SpaciousBackYard.MinZ, Is.GreaterThanOrEqualTo(clearance - Tolerance));
            Assert.That(SpaciousBackYard.MaxZ - position.Z, Is.GreaterThanOrEqualTo(clearance - Tolerance));
        }

        [Test]
        public void TryFindPosition_ClearsTheHouse_TheFenceLine_AndEveryOtherYardObject()
        {
            var house = new LotRect(-6f, 6f, 4f, 16f);
            var fenceRuns = new[]
            {
                new FenceRun(new GridPoint(-18f, -18f), new GridPoint(18f, -18f)),
                new FenceRun(new GridPoint(-18f, -18f), new GridPoint(-18f, 18f)),
            };
            var obstacles = new[]
            {
                new YardObstacle(new GridPoint(8f, -8f), YardLandscaping.TreeFootprintRadius),
                new YardObstacle(new GridPoint(-4f, 0f), PoolPlacement.DecorationFootprintRadius),
            };

            var found = PoolPlacement.TryFindPosition(
                SpaciousBackYard, house, fenceRuns, obstacles, seed: 7, out var position);

            Assert.That(found, Is.True);
            Assert.That(house.DistanceTo(position),
                Is.GreaterThanOrEqualTo(PoolPlacement.PoolFootprintRadius + PoolPlacement.ComfortMargin - Tolerance),
                "the pool clears the house footprint by its own radius plus the comfort margin");

            foreach (var run in fenceRuns)
            {
                Assert.That(DistanceToSegment(position, run.A, run.B),
                    Is.GreaterThanOrEqualTo(PoolPlacement.PoolFootprintRadius + PoolPlacement.ComfortMargin - Tolerance),
                    "the pool clears every fence run");
            }

            foreach (var obstacle in obstacles)
            {
                Assert.That(Distance(position, obstacle.Position),
                    Is.GreaterThanOrEqualTo(
                        PoolPlacement.PoolFootprintRadius + obstacle.Radius + PoolPlacement.ComfortMargin - Tolerance),
                    "the pool clears every other placed yard object");
            }
        }

        [Test]
        public void TryFindPosition_RelaxesTheComfortMargin_RatherThanOverlapping()
        {
            // #740: the fallback relaxes the COMFORT MARGIN only — the hard
            // no-overlap distance is never negotiable. This yard has room for
            // the pool's footprint and a sliver more, but nowhere near the
            // full comfort margin.
            var tight = PoolPlacement.PoolFootprintRadius + 0.05f;
            var snugYard = new LotRect(-tight, tight, -tight, tight);

            var found = PoolPlacement.TryFindPosition(
                snugYard, NoHouse, Array.Empty<FenceRun>(), Array.Empty<YardObstacle>(),
                seed: 3, out var position);

            Assert.That(found, Is.True, "the margin relaxes so a snug yard still gets its pool");
            Assert.That(snugYard.Contains(position), Is.True,
                "relaxing the margin never pushes the pool outside the back yard");
            Assert.That(position.X - snugYard.MinX,
                Is.GreaterThanOrEqualTo(PoolPlacement.PoolFootprintRadius - Tolerance),
                "the hard footprint clearance still holds");
        }

        [Test]
        public void TryFindPosition_WhenNoLegalSpotFits_PlacesNothing()
        {
            // Smaller than the pool itself: rendering nothing beats rendering
            // a pool that overlaps the house or spills out of the yard.
            var tooSmall = new LotRect(-0.1f, 0.1f, -0.1f, 0.1f);

            var found = PoolPlacement.TryFindPosition(
                tooSmall, NoHouse, Array.Empty<FenceRun>(), Array.Empty<YardObstacle>(),
                seed: 3, out var position);

            Assert.That(found, Is.False, "no legal spot means no pool at all");
            Assert.That(position, Is.EqualTo(default(GridPoint)));
        }

        [Test]
        public void TryFindPosition_IsPure_SameRegionAndSeedGivesTheSamePoint()
        {
            PoolPlacement.TryFindPosition(
                SpaciousBackYard, NoHouse, Array.Empty<FenceRun>(), Array.Empty<YardObstacle>(),
                seed: 11, out var first);
            PoolPlacement.TryFindPosition(
                SpaciousBackYard, NoHouse, Array.Empty<FenceRun>(), Array.Empty<YardObstacle>(),
                seed: 11, out var second);

            Assert.That(second, Is.EqualTo(first));
        }

        // -------------------------------------------------------------
        // Resolved per lot, and the yard-item invariant.
        // -------------------------------------------------------------

        [Test]
        public void PositionFor_IsDeterministicPerLot_AcrossRepeatedCalls()
        {
            // #719 is the failure mode this avoids: a yard object seeded off
            // anything but the lot itself moves between sessions.
            var state = GameState.CreateNew();

            foreach (var lot in NeighborhoodLayout.HouseLots)
            {
                Assert.That(PoolPlacement.TryPositionFor(lot, state, out var first), Is.True,
                    $"lot {lot.HouseId}: a starting back yard has room for a pool");
                Assert.That(PoolPlacement.TryPositionFor(lot, state, out var second), Is.True);
                Assert.That(PoolPlacement.TryPositionFor(lot, state, out var third), Is.True);

                Assert.That(second, Is.EqualTo(first),
                    $"lot {lot.HouseId}: the same lot always lands on the same pool position");
                Assert.That(third, Is.EqualTo(first));
            }
        }

        [Test]
        public void PositionFor_NeverOverlapsTheHouse_AnotherYardObject_OrTheFenceLine_AndStaysInTheBackYard()
        {
            // The #740 invariant, enforced: "A placed yard item never overlaps
            // the house, another yard object, or the fence line, and never sits
            // outside its own back yard."
            var state = GameState.CreateNew();

            foreach (var lot in NeighborhoodLayout.HouseLots)
            {
                Assert.That(PoolPlacement.TryPositionFor(lot, state, out var position), Is.True);

                var backYard = LotBounds.BackYard(lot);
                Assert.That(backYard.Contains(position), Is.True,
                    $"lot {lot.HouseId}: the pool sits inside its own back yard");
                Assert.That(position.X - backYard.MinX,
                    Is.GreaterThanOrEqualTo(PoolPlacement.PoolFootprintRadius - Tolerance),
                    $"lot {lot.HouseId}: the pool's whole footprint stays in the back yard");
                Assert.That(backYard.MaxX - position.X,
                    Is.GreaterThanOrEqualTo(PoolPlacement.PoolFootprintRadius - Tolerance));
                Assert.That(position.Z - backYard.MinZ,
                    Is.GreaterThanOrEqualTo(PoolPlacement.PoolFootprintRadius - Tolerance));
                Assert.That(backYard.MaxZ - position.Z,
                    Is.GreaterThanOrEqualTo(PoolPlacement.PoolFootprintRadius - Tolerance));

                // #459: the house obstacle is the MAX-across-upgrade-ladder
                // footprint, so a later upgrade can never grow into the pool.
                Assert.That(HousePlacement.MaxHouseFootprint(lot).DistanceTo(position),
                    Is.GreaterThanOrEqualTo(PoolPlacement.PoolFootprintRadius - Tolerance),
                    $"lot {lot.HouseId}: the pool never overlaps the house at any level");

                foreach (var run in LotFence.GeometryFor(lot))
                {
                    Assert.That(DistanceToSegment(position, run.A, run.B),
                        Is.GreaterThanOrEqualTo(PoolPlacement.PoolFootprintRadius - Tolerance),
                        $"lot {lot.HouseId}: the pool never overlaps the fence line");
                }

                foreach (var tree in YardLandscaping.BackTreesFor(lot))
                {
                    Assert.That(Distance(position, tree.Position),
                        Is.GreaterThanOrEqualTo(
                            PoolPlacement.PoolFootprintRadius + YardLandscaping.TreeFootprintRadius - Tolerance),
                        $"lot {lot.HouseId}: the pool never overlaps a yard tree");
                }
            }
        }

        [Test]
        public void PositionFor_KeepsClearOfThatHousesOwnYardDecorations()
        {
            // A decoration is a placed yard object too, so the pool has to
            // check it even though decorations don't check back.
            var state = GameState.CreateNew();
            var lot = NeighborhoodLayout.HouseLots[0];

            Assert.That(PoolPlacement.TryPositionFor(lot, state, out var bare), Is.True);

            state.AddDecoration(new Doggiehood.Core.Decorations.Decoration("bed", lot.HouseId, bare));

            Assert.That(PoolPlacement.TryPositionFor(lot, state, out var withDecoration), Is.True);
            Assert.That(Distance(withDecoration, bare),
                Is.GreaterThanOrEqualTo(
                    PoolPlacement.PoolFootprintRadius + PoolPlacement.DecorationFootprintRadius - Tolerance),
                "a decoration standing on the old spot pushes the pool off it");
        }

        // -------------------------------------------------------------
        // Visibility.
        // -------------------------------------------------------------

        [Test]
        public void HasPool_OnlyOnceAPoolPlacedItemIsPersistedForThatHouse()
        {
            // #740: visibility derives from the persisted PlacedItem — the
            // same mechanism the purchased fence uses. No new save state.
            var state = GameState.CreateNew();
            var houseId = NeighborhoodLayout.HouseLots[0].HouseId;
            var neighbourId = NeighborhoodLayout.HouseLots[1].HouseId;

            Assert.That(PoolPlacement.HasPool(houseId, state), Is.False,
                "a yard that never received a pool shows nothing");

            state.AddPlacedItem(houseId, ItemCatalog.PoolItemName);

            Assert.That(PoolPlacement.HasPool(houseId, state), Is.True);
            Assert.That(PoolPlacement.HasPool(neighbourId, state), Is.False,
                "only the house that bought one gets a pool");
        }

        [Test]
        public void HasPool_IgnoresOtherPlacedItems()
        {
            var state = GameState.CreateNew();
            var houseId = NeighborhoodLayout.HouseLots[0].HouseId;

            state.AddPlacedItem(houseId, ItemCatalog.FenceItemName);

            Assert.That(PoolPlacement.HasPool(houseId, state), Is.False,
                "a purchased fence is not a pool");
        }

        private static float Distance(GridPoint a, GridPoint b)
        {
            var dx = a.X - b.X;
            var dz = a.Z - b.Z;
            return (float)Math.Sqrt(dx * dx + dz * dz);
        }

        private static float DistanceToSegment(GridPoint point, GridPoint a, GridPoint b)
        {
            var abx = b.X - a.X;
            var abz = b.Z - a.Z;
            var lengthSquared = abx * abx + abz * abz;
            if (lengthSquared <= 0f)
            {
                return Distance(point, a);
            }

            var t = ((point.X - a.X) * abx + (point.Z - a.Z) * abz) / lengthSquared;
            t = Math.Max(0f, Math.Min(1f, t));
            return Distance(point, new GridPoint(a.X + t * abx, a.Z + t * abz));
        }
    }
}
