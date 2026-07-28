using System;
using System.Collections.Generic;
using System.Linq;
using Doggiehood.Core.World;
using NUnit.Framework;

namespace Doggiehood.Core.Tests.World
{
    /// <summary>
    /// #342 (adopting #147's settled geometry): the backyard fence traces
    /// the lot boundary rather than the house-footprint width. The fence
    /// line is inset from EVERY edge of the lot's
    /// <see cref="LotBounds.QuadrantBounds"/> by one
    /// <see cref="WorldDimensions.SidewalkWidth"/> (uniform on every edge —
    /// no corner-lot special-casing). The shape is five runs: two side runs
    /// plus one rear run trace the offset boundary rectangle, and two short
    /// connectors turn perpendicular-inward from each side run's front end
    /// to the house side-wall midpoints. The front stays open (the #128
    /// walkway never meets a fence). Fences stay defined for every lot but
    /// HIDDEN by default: HouseLot.HasFence defaults false, the geometry
    /// stays queryable via LotFence.GeometryFor for a disabled lot, and a
    /// future quest (#147/#318) purchases them.
    /// </summary>
    public class LotFenceTests
    {
        private const float Epsilon = 0.001f;

        [Test]
        public void HouseLots_AllHaveFencesHiddenByDefault()
        {
            // The built world renders NO fences by default — a future quest
            // (#147/#318) purchases them per lot.
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
        public void Fence_SitsOneSidewalkWidthInsideEveryQuadrantBoundsEdge_RealLots()
        {
            // #342/#147, full pipeline: for every starting lot, the rear run
            // and both side runs trace the OFFSET boundary rectangle —
            // QuadrantBounds inset by one SidewalkWidth on every edge.
            foreach (var lot in NeighborhoodLayout.HouseLots)
            {
                var quadrant = LotBounds.QuadrantBounds(lot);
                var facing = HousePlacement.FrontFacing(lot);
                var runs = LotFence.GeometryFor(lot);
                AssertOffsetBoundary(runs, quadrant, facing, $"lot {lot.HouseId}");
            }
        }

        [Test]
        public void Fence_SitsOneSidewalkWidthInsideEveryQuadrantBoundsEdge_EveryCardinalFacing()
        {
            // The uniform offset holds in EVERY orientation. The starting
            // lots all face along X, so the two Z-facing cardinals are
            // exercised through the pure builder with a synthetic square
            // quadrant and a real model — no per-neighbour special-casing.
            var model = HouseModelCatalog.ForHouse(1);
            var quadrant = new LotRect(0f, 30f, 0f, 30f);

            var cardinals = new[]
            {
                new GridPoint(1f, 0f), new GridPoint(-1f, 0f),
                new GridPoint(0f, 1f), new GridPoint(0f, -1f),
            };

            foreach (var facing in cardinals)
            {
                var perp = new GridPoint(-facing.Z, facing.X);
                // Place the house cross-centred in the quadrant, 10m behind
                // the front (max-along) edge.
                var alongFront = FrontEdgeAlong(quadrant, facing);
                var alongHouse = alongFront - 10f;
                var crossCentre = CrossOf(quadrant.Center, perp);
                var house = new GridPoint(
                    facing.X * alongHouse + perp.X * crossCentre,
                    facing.Z * alongHouse + perp.Z * crossCentre);

                var runs = LotFence.BackyardRuns(quadrant, model, house, facing, HousePlacement.KitScale);
                AssertOffsetBoundary(runs, quadrant, facing, $"facing ({facing.X},{facing.Z})");
            }
        }

        private static void AssertOffsetBoundary(
            IReadOnlyList<FenceRun> runs, LotRect quadrant, GridPoint facing, string who)
        {
            var offset = WorldDimensions.SidewalkWidth;
            var perp = new GridPoint(-facing.Z, facing.X);

            // Every fence point stays within the offset (inset) rectangle —
            // no point sits closer than one SidewalkWidth to any quadrant edge.
            foreach (var run in runs)
            {
                foreach (var point in new[] { run.A, run.B })
                {
                    Assert.That(point.X, Is.GreaterThanOrEqualTo(quadrant.MinX + offset - Epsilon),
                        $"{who}: fence point {point} is under one SidewalkWidth from the MinX edge");
                    Assert.That(point.X, Is.LessThanOrEqualTo(quadrant.MaxX - offset + Epsilon),
                        $"{who}: fence point {point} is under one SidewalkWidth from the MaxX edge");
                    Assert.That(point.Z, Is.GreaterThanOrEqualTo(quadrant.MinZ + offset - Epsilon),
                        $"{who}: fence point {point} is under one SidewalkWidth from the MinZ edge");
                    Assert.That(point.Z, Is.LessThanOrEqualTo(quadrant.MaxZ - offset + Epsilon),
                        $"{who}: fence point {point} is under one SidewalkWidth from the MaxZ edge");
                }
            }

            // The rear run sits exactly one SidewalkWidth inside the rear
            // quadrant edge (the edge opposite the facing).
            var rear = RearRunOf(runs);
            var rearAlong = AlongOf(rear.A, facing);
            Assert.That(AlongOf(rear.B, facing), Is.EqualTo(rearAlong).Within(Epsilon),
                $"{who}: the rear run is a single boundary-parallel line");
            Assert.That(rearAlong, Is.EqualTo(RearEdgeAlong(quadrant, facing) + offset).Within(Epsilon),
                $"{who}: rear run must sit one SidewalkWidth inside the rear quadrant edge");

            // The two side runs sit exactly one SidewalkWidth inside the two
            // side quadrant edges (the cross-axis extremes).
            var sideCrosses = SideRunsOf(runs).Select(r => CrossOf(r.A, perp)).OrderBy(c => c).ToList();
            var cornerCrossA = CrossOf(new GridPoint(quadrant.MinX, quadrant.MinZ), perp);
            var cornerCrossB = CrossOf(new GridPoint(quadrant.MaxX, quadrant.MaxZ), perp);
            var quadrantCrossLo = Math.Min(cornerCrossA, cornerCrossB);
            var quadrantCrossHi = Math.Max(cornerCrossA, cornerCrossB);
            Assert.That(sideCrosses[0], Is.EqualTo(quadrantCrossLo + offset).Within(Epsilon),
                $"{who}: the low-side run must sit one SidewalkWidth inside its quadrant edge");
            Assert.That(sideCrosses[1], Is.EqualTo(quadrantCrossHi - offset).Within(Epsilon),
                $"{who}: the high-side run must sit one SidewalkWidth inside its quadrant edge");
        }

        [Test]
        public void Fence_IsFiveRuns_TwoConnectorsToTheSideWallMidpoints_FrontOpen()
        {
            foreach (var lot in NeighborhoodLayout.HouseLots)
            {
                var runs = LotFence.RunsFor(FencedCloneOf(lot));
                Assert.That(runs.Count, Is.EqualTo(5),
                    $"lot {lot.HouseId}: two side runs + one rear run + two connectors");

                var facing = HousePlacement.FrontFacing(lot);
                var house = HousePlacement.Position(lot, HousePlacement.KitScale);
                var model = HouseModelCatalog.ForHouse(lot.HouseId);
                var halfWidth = HousePlacement.KitScale * model.FootprintX / 2f;

                // The two open ends of the polyline are the house side-wall
                // midpoints (the connectors reach perpendicular-inward to
                // them), sitting at the house center depth-wise.
                var perp = new GridPoint(-facing.Z, facing.X);
                var expectedAnchors = new[]
                {
                    new GridPoint(house.X + perp.X * halfWidth, house.Z + perp.Z * halfWidth),
                    new GridPoint(house.X - perp.X * halfWidth, house.Z - perp.Z * halfWidth),
                };

                var openEnds = OpenEndsOf(runs);
                Assert.That(openEnds.Count, Is.EqualTo(2),
                    $"lot {lot.HouseId}: the fence must be one open polyline with two ends");
                foreach (var anchor in expectedAnchors)
                {
                    Assert.That(openEnds.Any(e => PointsNearlyEqual(e, anchor)), Is.True,
                        $"lot {lot.HouseId}: a connector must reach the side wall midpoint {anchor}");
                }

                // The two connectors are the runs touching an open end. Each
                // turns perpendicular to the facing (it runs along the cross
                // axis) inward to a side-wall midpoint.
                var connectors = runs.Where(r =>
                    openEnds.Any(e => PointsNearlyEqual(e, r.A) || PointsNearlyEqual(e, r.B))).ToList();
                Assert.That(connectors.Count, Is.EqualTo(2),
                    $"lot {lot.HouseId}: exactly two connectors");
                foreach (var connector in connectors)
                {
                    var alongComponent = (connector.A.X - connector.B.X) * facing.X
                        + (connector.A.Z - connector.B.Z) * facing.Z;
                    Assert.That(Math.Abs(alongComponent), Is.LessThan(Epsilon),
                        $"lot {lot.HouseId}: a connector must run perpendicular to the facing");
                }

                // Front stays open: every run endpoint sits at or behind the
                // house center along the facing axis, so nothing crosses in
                // front of the house facade (runs are straight).
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
                // runs — no gap anywhere.
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
        public void EnclosedWidth_IsMeaningfullyWiderThanTheHouseFootprint_EvenForTheNarrowModel()
        {
            // #342: the point of the widening. The enclosed span (the rear
            // run's length, tracing the offset boundary) must be much wider
            // than the house's scaled FootprintX — asserted most sharply on
            // the narrowest starting model, building-type-k (house 3, a
            // 6.45m footprint at ×7).
            var narrowLot = NeighborhoodLayout.HouseLots.Single(l => l.HouseId == 3);
            Assert.That(HouseModelCatalog.ForHouse(narrowLot.HouseId).ModelName,
                Is.EqualTo("building-type-k"), "house 3 is expected to be the narrow building-type-k");

            foreach (var lot in NeighborhoodLayout.HouseLots)
            {
                var runs = LotFence.RunsFor(FencedCloneOf(lot));
                var model = HouseModelCatalog.ForHouse(lot.HouseId);
                var scaledFootprintX = HousePlacement.KitScale * model.FootprintX;

                // The rear run spans the offset boundary width: the quadrant
                // width (30m) minus one SidewalkWidth per side (2 * 2m).
                var expectedEnclosed = LotBounds.QuadrantBounds(lot).Width
                    - 2f * WorldDimensions.SidewalkWidth;
                var rear = RearRunOf(runs);
                Assert.That(rear.Length, Is.EqualTo(expectedEnclosed).Within(Epsilon),
                    $"lot {lot.HouseId}: rear run must span the offset boundary width");

                // Meaningfully wider than the house footprint (at least twice
                // as wide — for building-type-k, 26m vs 6.45m).
                Assert.That(rear.Length, Is.GreaterThan(2f * scaledFootprintX),
                    $"lot {lot.HouseId}: the enclosed width ({rear.Length:F2}m) must be much wider than "
                    + $"the house footprint ({scaledFootprintX:F2}m)");
            }

            // Spelled out on the narrow model specifically.
            var narrowRear = RearRunOf(LotFence.RunsFor(FencedCloneOf(narrowLot)));
            var narrowFootprintX = HousePlacement.KitScale
                * HouseModelCatalog.ForHouse(narrowLot.HouseId).FootprintX;
            Assert.That(narrowFootprintX, Is.EqualTo(6.447f).Within(0.01f),
                "building-type-k footprint width at ×7");
            Assert.That(narrowRear.Length, Is.GreaterThan(narrowFootprintX + 15f),
                "building-type-k's enclosed back yard must be far wider than its own footprint");
        }

        [Test]
        public void BackyardRuns_RejectsNonPositiveScale()
        {
            var model = HouseModelCatalog.ForHouse(1);
            Assert.That(() => LotFence.BackyardRuns(model, new GridPoint(0f, 0f),
                new GridPoint(0f, -1f), 0f),
                Throws.ArgumentException);
        }

        [Test]
        public void BackyardRuns_LotFreeGalleryForm_UsesTheSameOffsetAgainstTheStandardQuadrant()
        {
            // #126 gallery / lot-free path: no lot means no real quadrant, so
            // the offset is applied against the STANDARD quadrant
            // (TileSize/2 per side, identical for every lot). The gallery
            // therefore shows the real offset outline: five runs, the rear
            // run tracing the offset boundary, and the two open ends at the
            // scaled side-wall midpoints.
            var model = HouseModelCatalog.ForHouse(1);
            var facing = new GridPoint(0f, -1f);
            var position = new GridPoint(0f, 0f);
            var halfWidth = HousePlacement.KitScale * model.FootprintX / 2f;

            var runs = LotFence.BackyardRuns(model, position, facing, HousePlacement.KitScale);
            Assert.That(runs.Count, Is.EqualTo(5), "the gallery outline is the same five-run shape");

            // The rear run spans the standard offset boundary width.
            var expectedEnclosed = WorldDimensions.TileSize / 2f - 2f * WorldDimensions.SidewalkWidth;
            Assert.That(RearRunOf(runs).Length, Is.EqualTo(expectedEnclosed).Within(Epsilon),
                "the gallery rear run spans the standard offset boundary");

            // The two open ends sit at the scaled side-wall midpoints.
            var openEnds = OpenEndsOf(runs);
            Assert.That(openEnds.Count, Is.EqualTo(2), "one open polyline");
            foreach (var end in openEnds)
            {
                Assert.That(Math.Abs(end.X) - halfWidth, Is.EqualTo(0f).Within(Epsilon),
                    "an open end sits at a scaled side-wall midpoint (X)");
                Assert.That(end.Z, Is.EqualTo(position.Z).Within(Epsilon),
                    "an open end sits at the house depth midpoint (Z)");
            }
        }

        private static HouseLot FencedCloneOf(HouseLot lot)
        {
            return new HouseLot(lot.HouseId, lot.Quadrant, lot.Position, hasFence: true);
        }

        private static float AlongOf(GridPoint p, GridPoint facing)
        {
            return p.X * facing.X + p.Z * facing.Z;
        }

        private static float CrossOf(GridPoint p, GridPoint perp)
        {
            return p.X * perp.X + p.Z * perp.Z;
        }

        /// <summary>The along-axis coordinate of the quadrant's rear edge —
        /// the boundary opposite the facing direction (its most negative
        /// projection onto the facing).</summary>
        private static float RearEdgeAlong(LotRect quadrant, GridPoint facing)
        {
            var corners = new[]
            {
                new GridPoint(quadrant.MinX, quadrant.MinZ),
                new GridPoint(quadrant.MinX, quadrant.MaxZ),
                new GridPoint(quadrant.MaxX, quadrant.MinZ),
                new GridPoint(quadrant.MaxX, quadrant.MaxZ),
            };
            return corners.Min(c => AlongOf(c, facing));
        }

        /// <summary>The along-axis coordinate of the quadrant's front edge —
        /// the boundary in the facing direction (its most positive
        /// projection onto the facing).</summary>
        private static float FrontEdgeAlong(LotRect quadrant, GridPoint facing)
        {
            var corners = new[]
            {
                new GridPoint(quadrant.MinX, quadrant.MinZ),
                new GridPoint(quadrant.MinX, quadrant.MaxZ),
                new GridPoint(quadrant.MaxX, quadrant.MinZ),
                new GridPoint(quadrant.MaxX, quadrant.MaxZ),
            };
            return corners.Max(c => AlongOf(c, facing));
        }

        /// <summary>The rear run: the one run touching no open end.</summary>
        private static FenceRun RearRunOf(IReadOnlyList<FenceRun> runs)
        {
            var openEnds = OpenEndsOf(runs);
            return runs.Single(r =>
                !openEnds.Any(e => PointsNearlyEqual(e, r.A) || PointsNearlyEqual(e, r.B))
                && !SharesACornerWithAConnector(r, runs, openEnds));
        }

        /// <summary>The two side runs: touch no open end, but each shares a
        /// corner with a connector (a run that does touch an open end).</summary>
        private static IReadOnlyList<FenceRun> SideRunsOf(IReadOnlyList<FenceRun> runs)
        {
            var openEnds = OpenEndsOf(runs);
            return runs.Where(r =>
                !openEnds.Any(e => PointsNearlyEqual(e, r.A) || PointsNearlyEqual(e, r.B))
                && SharesACornerWithAConnector(r, runs, openEnds)).ToList();
        }

        private static bool SharesACornerWithAConnector(
            FenceRun run, IReadOnlyList<FenceRun> runs, List<GridPoint> openEnds)
        {
            bool IsConnector(FenceRun r) =>
                openEnds.Any(e => PointsNearlyEqual(e, r.A) || PointsNearlyEqual(e, r.B));

            foreach (var other in runs.Where(IsConnector))
            {
                foreach (var p in new[] { run.A, run.B })
                {
                    if (PointsNearlyEqual(p, other.A) || PointsNearlyEqual(p, other.B))
                    {
                        return true;
                    }
                }
            }

            return false;
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
