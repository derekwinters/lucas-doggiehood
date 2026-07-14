using System;
using System.Collections.Generic;
using System.Linq;
using Doggiehood.Core.World;
using NUnit.Framework;

namespace Doggiehood.Core.Tests.World
{
    /// <summary>
    /// #146 (reshaping #129): each fence anchors at the midpoint of each
    /// side wall of the HOUSE and wraps around the back yard only — the
    /// front yard stays open, so the #129 gate gap is gone (the walkway
    /// runs door → sidewalk through the open front and never meets a
    /// fence). Fences are defined for every lot but HIDDEN by default:
    /// HouseLot.HasFence defaults false, the geometry stays queryable via
    /// LotFence.GeometryFor for a disabled lot, and a future quest (#147)
    /// purchases them.
    /// </summary>
    public class LotFenceTests
    {
        private const float Epsilon = 0.001f;

        [Test]
        public void HouseLots_AllHaveFencesHiddenByDefault()
        {
            // #146: the built world renders NO fences by default — a future
            // quest (#147) purchases them per lot.
            foreach (var lot in NeighborhoodLayout.HouseLots)
            {
                Assert.That(lot.HasFence, Is.False,
                    $"lot {lot.HouseId} must have its fence hidden by default");
            }

            var fresh = new HouseLot(1, Quadrant.NorthEast, new GridPoint(14f, 14f));
            Assert.That(fresh.HasFence, Is.False, "HasFence must default false");
        }

        [Test]
        public void HasFence_CanBeEnabledPerLot()
        {
            var lot = new HouseLot(1, Quadrant.NorthEast, new GridPoint(14f, 14f), hasFence: true);
            Assert.That(lot.HasFence, Is.True);
        }

        [Test]
        public void RunsFor_DisabledLot_ContributesNoRuns_ButGeometryStaysQueryable()
        {
            foreach (var lot in NeighborhoodLayout.HouseLots)
            {
                // The flag-respecting API WorldBuilder consumes: nothing to
                // build for a hidden fence...
                Assert.That(LotFence.RunsFor(lot), Is.Empty,
                    $"lot {lot.HouseId} is unfenced by default and must contribute no runs");

                // ...but the geometry stays fully describable for a disabled
                // lot (the #147 purchase flow and the #126 gallery need it),
                // and it is exactly what an enabled lot would build.
                var geometry = LotFence.GeometryFor(lot);
                Assert.That(geometry, Is.Not.Empty,
                    $"lot {lot.HouseId}: fence geometry must stay queryable while hidden");

                var enabledRuns = LotFence.RunsFor(FencedCloneOf(lot));
                Assert.That(enabledRuns.Count, Is.EqualTo(geometry.Count),
                    $"lot {lot.HouseId}: enabling the fence must build exactly the queryable geometry");
                for (var i = 0; i < geometry.Count; i++)
                {
                    AssertPointsEqual(enabledRuns[i].A, geometry[i].A, $"lot {lot.HouseId} run {i} A");
                    AssertPointsEqual(enabledRuns[i].B, geometry[i].B, $"lot {lot.HouseId} run {i} B");
                }
            }
        }

        [Test]
        public void Runs_StartAtEachSideWallMidpoint_AndEncloseOnlyTheBackYard()
        {
            foreach (var lot in NeighborhoodLayout.HouseLots)
            {
                var runs = LotFence.RunsFor(FencedCloneOf(lot));
                Assert.That(runs.Count, Is.EqualTo(3),
                    $"lot {lot.HouseId}: two side runs plus one rear run");

                var facing = HousePlacement.FrontFacing(lot);
                var house = HousePlacement.Position(lot, HousePlacement.KitScale);
                var model = HouseModelCatalog.ForHouse(lot.HouseId);
                var halfWidth = HousePlacement.KitScale * model.FootprintX / 2f;

                // The side walls run along the facing (depth) axis at
                // ±halfWidth perpendicular to it; their midpoints sit at the
                // house center depth-wise.
                var perp = new GridPoint(-facing.Z, facing.X);
                var expectedAnchors = new[]
                {
                    new GridPoint(house.X + perp.X * halfWidth, house.Z + perp.Z * halfWidth),
                    new GridPoint(house.X - perp.X * halfWidth, house.Z - perp.Z * halfWidth),
                };

                // The open ends of the fence polyline (endpoints appearing
                // exactly once) are exactly the two side-wall midpoints.
                var openEnds = OpenEndsOf(runs);
                Assert.That(openEnds.Count, Is.EqualTo(2),
                    $"lot {lot.HouseId}: the fence must be one open polyline with two ends");
                foreach (var anchor in expectedAnchors)
                {
                    Assert.That(openEnds.Any(e => PointsNearlyEqual(e, anchor)), Is.True,
                        $"lot {lot.HouseId}: a fence run must start at the side wall midpoint {anchor}");
                }

                // Nothing in front of the side midpoints — the front yard
                // stays open. Every run endpoint sits at or behind the house
                // center along the facing axis (runs are straight, so
                // endpoint checks cover the whole line).
                foreach (var run in runs)
                {
                    foreach (var point in new[] { run.A, run.B })
                    {
                        var alongFacing = (point.X - house.X) * facing.X + (point.Z - house.Z) * facing.Z;
                        Assert.That(alongFacing, Is.LessThanOrEqualTo(Epsilon),
                            $"lot {lot.HouseId}: fence point {point} intrudes into the front yard");
                    }
                }
            }
        }

        [Test]
        public void Runs_AreContinuous_WithNoGateGap_AndNeverCrossTheWalkway()
        {
            foreach (var lot in NeighborhoodLayout.HouseLots)
            {
                var runs = LotFence.RunsFor(FencedCloneOf(lot));

                // Continuous: every non-end corner is shared by exactly two
                // runs — no gap anywhere (the #129 gate gap is gone).
                var endpoints = runs.SelectMany(r => new[] { r.A, r.B }).ToList();
                foreach (var point in endpoints)
                {
                    var occurrences = endpoints.Count(p => PointsNearlyEqual(p, point));
                    Assert.That(occurrences, Is.EqualTo(1).Or.EqualTo(2),
                        $"lot {lot.HouseId}: corner {point} must join at most two runs");
                }

                Assert.That(OpenEndsOf(runs).Count, Is.EqualTo(2),
                    $"lot {lot.HouseId}: the runs must chain into one continuous polyline");

                // The walkway (door → sidewalk through the open front) never
                // meets a fence run.
                Assert.That(NeighborhoodLayout.WalkNetwork.TryGetFrontWalkway(lot.HouseId, out var walkway),
                    Is.True, $"lot {lot.HouseId} has no front walkway");
                foreach (var run in runs)
                {
                    Assert.That(SegmentsIntersect(walkway.A, walkway.B, run.A, run.B), Is.False,
                        $"lot {lot.HouseId}: the walkway crosses a fence run");
                    Assert.That(SegmentDistance(walkway.A, walkway.B, run.A, run.B),
                        Is.GreaterThan(0.25f),
                        $"lot {lot.HouseId}: a fence run comes too close to the walkway");
                }
            }
        }

        [Test]
        public void RearLine_SitsAtTheLotRearBoundary_AndClearsEveryModelsRearWall()
        {
            // Decision (#146): the rear fence line reuses the #129 boundary
            // distance — RearBoundaryFromLotCenter (7.5m) away from the
            // street the house faces, measured from the lot center. Exact
            // alignment with property layouts is deferred to #147.
            foreach (var lot in NeighborhoodLayout.HouseLots)
            {
                var runs = LotFence.RunsFor(FencedCloneOf(lot));
                var facing = HousePlacement.FrontFacing(lot);
                var house = HousePlacement.Position(lot, HousePlacement.KitScale);
                var model = HouseModelCatalog.ForHouse(lot.HouseId);

                // The rear run is the one not touching an open end.
                var openEnds = OpenEndsOf(runs);
                var rearRuns = runs.Where(r =>
                    !openEnds.Any(e => PointsNearlyEqual(e, r.A) || PointsNearlyEqual(e, r.B))).ToList();
                Assert.That(rearRuns.Count, Is.EqualTo(1),
                    $"lot {lot.HouseId}: exactly one rear run");
                var rear = rearRuns[0];

                var expectedRearAlongFacing =
                    (lot.Position.X * facing.X + lot.Position.Z * facing.Z)
                    - LotFence.RearBoundaryFromLotCenter;
                foreach (var point in new[] { rear.A, rear.B })
                {
                    Assert.That(point.X * facing.X + point.Z * facing.Z,
                        Is.EqualTo(expectedRearAlongFacing).Within(Epsilon),
                        $"lot {lot.HouseId}: rear line must sit {LotFence.RearBoundaryFromLotCenter}m"
                        + " behind the lot center, away from the street");
                }

                // The rear line clears the scaled rear wall by >= 0.5m —
                // the guard that sized the rear boundary against the
                // setback-shifted deepest model (building-type-m, 10.00m).
                var rearWallAlongFacing = (house.X * facing.X + house.Z * facing.Z)
                    - HousePlacement.KitScale * model.FootprintZ / 2f;
                Assert.That(rearWallAlongFacing - expectedRearAlongFacing,
                    Is.GreaterThanOrEqualTo(0.5f),
                    $"lot {lot.HouseId}: the rear fence line must clear the rear wall by at least 0.5m");
            }
        }

        [Test]
        public void Runs_WidthTracksTheScaledHouseWidthPerModel()
        {
            foreach (var lot in NeighborhoodLayout.HouseLots)
            {
                var runs = LotFence.RunsFor(FencedCloneOf(lot));
                var model = HouseModelCatalog.ForHouse(lot.HouseId);
                var scaledWidth = HousePlacement.KitScale * model.FootprintX;

                // The rear run spans exactly the scaled house width...
                var openEnds = OpenEndsOf(runs);
                var rear = runs.Single(r =>
                    !openEnds.Any(e => PointsNearlyEqual(e, r.A) || PointsNearlyEqual(e, r.B)));
                Assert.That(rear.Length, Is.EqualTo(scaledWidth).Within(Epsilon),
                    $"lot {lot.HouseId}: rear run must span the scaled width of {model.ModelName}");

                // ...and so does the distance between the two side anchors.
                var dx = openEnds[0].X - openEnds[1].X;
                var dz = openEnds[0].Z - openEnds[1].Z;
                Assert.That(Math.Sqrt(dx * dx + dz * dz), Is.EqualTo(scaledWidth).Within(Epsilon),
                    $"lot {lot.HouseId}: side anchors must sit a scaled house width apart");
            }
        }

        [Test]
        public void Runs_StayClearOfTheSidewalks_AndInsideTheStartingTile()
        {
            // A corner lot has streets on two sides: no fence point may
            // reach either sidewalk's outer edge, and the whole fence stays
            // inside the starting 60m tile's quadrant.
            var sidewalkOuterEdge = WorldDimensions.RoadWidth / 2f
                + WorldDimensions.GrassVergeWidth + WorldDimensions.SidewalkWidth;

            foreach (var lot in NeighborhoodLayout.HouseLots)
            {
                foreach (var run in LotFence.RunsFor(FencedCloneOf(lot)))
                {
                    foreach (var point in new[] { run.A, run.B })
                    {
                        Assert.That(Math.Abs(point.X), Is.GreaterThan(sidewalkOuterEdge),
                            $"lot {lot.HouseId}: fence point {point} reaches the north-south sidewalk");
                        Assert.That(Math.Abs(point.Z), Is.GreaterThan(sidewalkOuterEdge),
                            $"lot {lot.HouseId}: fence point {point} reaches the east-west sidewalk");
                        Assert.That(Math.Abs(point.X), Is.LessThanOrEqualTo(WorldDimensions.TileSize / 2f),
                            $"lot {lot.HouseId}: fence point {point} leaves the starting tile (X)");
                        Assert.That(Math.Abs(point.Z), Is.LessThanOrEqualTo(WorldDimensions.TileSize / 2f),
                            $"lot {lot.HouseId}: fence point {point} leaves the starting tile (Z)");
                    }
                }
            }
        }

        [Test]
        public void RearLineBehindFacade_DerivesFromTheLotRearBoundary()
        {
            // The house-relative form the #126 gallery uses: the facade sits
            // LotDistanceFromCenter - sidewalk outer edge - FrontSetback
            // street-ward of the lot center (5.5m), so the rear line lands
            // 5.5 + 7.5 = 13m behind the scaled front facade — the same
            // world line as RearBoundaryFromLotCenter for every lot.
            var sidewalkOuterEdge = WorldDimensions.RoadWidth / 2f
                + WorldDimensions.GrassVergeWidth + WorldDimensions.SidewalkWidth;
            var expected = NeighborhoodLayout.LotDistanceFromCenter - sidewalkOuterEdge
                - HousePlacement.FrontSetback + LotFence.RearBoundaryFromLotCenter;

            Assert.That(LotFence.RearLineBehindFacade, Is.EqualTo(expected).Within(Epsilon));
            Assert.That(LotFence.RearLineBehindFacade, Is.EqualTo(13f).Within(Epsilon));
        }

        [Test]
        public void BackyardRuns_RejectsNonPositiveScale()
        {
            var model = HouseModelCatalog.ForHouse(1);
            Assert.That(() => LotFence.BackyardRuns(model, new GridPoint(0f, 0f),
                new GridPoint(0f, -1f), 0f, LotFence.RearLineBehindFacade),
                Throws.ArgumentException);
        }

        private static HouseLot FencedCloneOf(HouseLot lot)
        {
            return new HouseLot(lot.HouseId, lot.Quadrant, lot.Position, hasFence: true);
        }

        /// <summary>Endpoints that appear exactly once across all runs —
        /// the open ends of the fence polyline.</summary>
        private static List<GridPoint> OpenEndsOf(IReadOnlyList<FenceRun> runs)
        {
            var endpoints = runs.SelectMany(r => new[] { r.A, r.B }).ToList();
            return endpoints
                .Where(p => endpoints.Count(other => PointsNearlyEqual(p, other)) == 1)
                .ToList();
        }

        private static bool PointsNearlyEqual(GridPoint a, GridPoint b)
        {
            return Math.Abs(a.X - b.X) < Epsilon && Math.Abs(a.Z - b.Z) < Epsilon;
        }

        private static void AssertPointsEqual(GridPoint actual, GridPoint expected, string label)
        {
            Assert.That(actual.X, Is.EqualTo(expected.X).Within(Epsilon), label + " X");
            Assert.That(actual.Z, Is.EqualTo(expected.Z).Within(Epsilon), label + " Z");
        }

        private static bool SegmentsIntersect(GridPoint a, GridPoint b, GridPoint c, GridPoint d)
        {
            var d1 = Cross(c, d, a);
            var d2 = Cross(c, d, b);
            var d3 = Cross(a, b, c);
            var d4 = Cross(a, b, d);

            if (((d1 > 0f && d2 < 0f) || (d1 < 0f && d2 > 0f))
                && ((d3 > 0f && d4 < 0f) || (d3 < 0f && d4 > 0f)))
            {
                return true;
            }

            return (Math.Abs(d1) < Epsilon && OnSegment(c, d, a))
                || (Math.Abs(d2) < Epsilon && OnSegment(c, d, b))
                || (Math.Abs(d3) < Epsilon && OnSegment(a, b, c))
                || (Math.Abs(d4) < Epsilon && OnSegment(a, b, d));
        }

        private static float Cross(GridPoint a, GridPoint b, GridPoint p)
        {
            return (b.X - a.X) * (p.Z - a.Z) - (b.Z - a.Z) * (p.X - a.X);
        }

        private static bool OnSegment(GridPoint a, GridPoint b, GridPoint p)
        {
            return p.X >= Math.Min(a.X, b.X) - Epsilon && p.X <= Math.Max(a.X, b.X) + Epsilon
                && p.Z >= Math.Min(a.Z, b.Z) - Epsilon && p.Z <= Math.Max(a.Z, b.Z) + Epsilon;
        }

        /// <summary>Minimum distance between two non-intersecting segments:
        /// the closest pair always involves an endpoint.</summary>
        private static float SegmentDistance(GridPoint a, GridPoint b, GridPoint c, GridPoint d)
        {
            return Math.Min(
                Math.Min(PointToSegment(a, c, d), PointToSegment(b, c, d)),
                Math.Min(PointToSegment(c, a, b), PointToSegment(d, a, b)));
        }

        private static float PointToSegment(GridPoint point, GridPoint a, GridPoint b)
        {
            var abx = b.X - a.X;
            var abz = b.Z - a.Z;
            var lengthSquared = abx * abx + abz * abz;
            var t = lengthSquared < 0.000001f
                ? 0f
                : Math.Max(0f, Math.Min(1f,
                    ((point.X - a.X) * abx + (point.Z - a.Z) * abz) / lengthSquared));

            var dx = a.X + t * abx - point.X;
            var dz = a.Z + t * abz - point.Z;
            return (float)Math.Sqrt(dx * dx + dz * dz);
        }
    }
}
