using System;
using System.Collections.Generic;
using System.Linq;
using Doggiehood.Core.Economy;
using Doggiehood.Core.World;
using NUnit.Framework;

namespace Doggiehood.Core.Tests.World
{
    /// <summary>
    /// #342 (adopting #147's settled geometry) + #147 offset fix: the
    /// backyard fence traces the lot boundary rather than the house-footprint
    /// width. The fence line leaves one <see cref="WorldDimensions.SidewalkWidth"/>
    /// strip of grass beyond the pavement on every edge: a road-bordering
    /// quadrant edge (one whose centerline lies ON the edge) is offset from
    /// the sidewalk's OUTER edge (<see cref="LotBounds.StreetCorridorInset"/>)
    /// by one <see cref="LotFence.BoundaryOffset"/>, and a neighbour-yard /
    /// map edge is offset from the quadrant boundary by that same
    /// <see cref="LotFence.BoundaryOffset"/>. The shape is five runs: two side runs
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
        public void RunsFor_TreatsLotAsFenced_WhenPlacedItemsHoldAFenceForThatHouse()
        {
            // #318: a completed fence-purchase quest records a
            // PlacedItem(houseId, "fence"). Fence visibility derives from that
            // persisted state — a lot with the static HasFence flag off still
            // renders its fence once its house owns a placed "fence".
            var state = GameState.CreateNew();
            state.SetTargetMap(FrontierTestWorld.LoadAuthoredTargetMap());
            var lot = NeighborhoodLayout.HouseLots[0];
            Assert.That(lot.HasFence, Is.False, "precondition: the lot's static flag is off");

            Assert.That(LotFence.RunsFor(lot, state), Is.Empty,
                "no placed fence yet — nothing to build");

            state.AddPlacedItem(lot.HouseId, ItemCatalog.FenceItemName);

            Assert.That(LotFence.RunsFor(lot, state), Is.EqualTo(LotFence.GeometryFor(lot)),
                "a placed fence makes the lot render exactly its queryable geometry");
        }

        [Test]
        public void RunsFor_WithState_StillHonoursTheStaticHasFenceFlag()
        {
            // #318: the persisted-fence source is ADDITIVE to the existing
            // static HouseLot.HasFence flag, not a replacement.
            var state = GameState.CreateNew();
            state.SetTargetMap(FrontierTestWorld.LoadAuthoredTargetMap());
            var lot = FencedCloneOf(NeighborhoodLayout.HouseLots[0]);

            Assert.That(LotFence.RunsFor(lot, state), Is.EqualTo(LotFence.GeometryFor(lot)));
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
        public void Fence_TracesTheRoadAwareOffsetBoundary_RealLots()
        {
            // #342/#147, full pipeline: for every starting lot, the rear run
            // and both side runs trace the OFFSET boundary rectangle — a
            // road-bordering quadrant edge offset from the sidewalk's outer
            // edge (StreetCorridorInset) by one BoundaryOffset, a neighbour /
            // map edge offset from the quadrant boundary by one BoundaryOffset.
            foreach (var lot in NeighborhoodLayout.HouseLots)
            {
                var quadrant = LotBounds.QuadrantBounds(lot);
                var facing = HousePlacement.FrontFacing(lot);
                var runs = LotFence.GeometryFor(lot);
                AssertOffsetBoundary(runs, quadrant, facing, NeighborhoodLayout.Roads, $"lot {lot.HouseId}");
            }
        }

        [Test]
        public void Fence_LeavesOneSidewalkWidthGrassStrip_BeyondTheSidewalk_OnRoadBorderingEdges()
        {
            // #147 offset fix (Derek playtest): on a road-bordering edge the
            // fence must sit one grass strip BEYOND the sidewalk's outer edge
            // — StreetCorridorInset + BoundaryOffset from the road centerline
            // — NOT BoundaryOffset from the raw quadrant edge (which put the
            // fence line inside the road). The strip of grass between the
            // sidewalk's outer edge and the fence line equals one SidewalkWidth.
            foreach (var lot in NeighborhoodLayout.HouseLots)
            {
                var facing = HousePlacement.FrontFacing(lot);
                var runs = LotFence.GeometryFor(lot);

                // The road bordering a TRACED side edge runs parallel to the
                // facing axis (the faced road, perpendicular to the facing,
                // borders the open front). Its centerline sits at the
                // perpendicular coordinate 0 on the FourWay.
                var sideRoad = NeighborhoodLayout.Roads.Single(r => RunsParallelTo(r, facing));
                var fenceDistance = runs
                    .SelectMany(run => new[] { run.A, run.B })
                    .Min(p => PerpDistanceToCenterline(p, sideRoad));

                Assert.That(fenceDistance,
                    Is.EqualTo(LotBounds.StreetCorridorInset + LotFence.BoundaryOffset).Within(Epsilon),
                    $"lot {lot.HouseId}: the road-bordering fence run must sit "
                    + "StreetCorridorInset + BoundaryOffset from the road centerline");

                var grassStrip = fenceDistance - LotBounds.StreetCorridorInset;
                Assert.That(grassStrip, Is.EqualTo(WorldDimensions.SidewalkWidth).Within(Epsilon),
                    $"lot {lot.HouseId}: one sidewalk-width strip of grass must sit between the "
                    + "sidewalk's outer edge and the fence line");
            }
        }

        [Test]
        public void Fence_SitsOneBoundaryOffsetInsideEveryQuadrantBoundsEdge_NoRoads_EveryCardinalFacing()
        {
            // With NO adjacent roads (the neighbour-yard / map-edge case, and
            // the lot-free gallery standard quadrant), the offset is a plain
            // BoundaryOffset uniform on every edge, in EVERY orientation. The
            // two Z-facing cardinals are exercised through the pure builder
            // with a synthetic square quadrant and a real model.
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

                var runs = LotFence.BackyardRuns(
                    quadrant, model, house, facing, HousePlacement.KitScale, Array.Empty<Road>());
                AssertOffsetBoundary(runs, quadrant, facing, Array.Empty<Road>(),
                    $"facing ({facing.X},{facing.Z})");
            }
        }

        private static void AssertOffsetBoundary(
            IReadOnlyList<FenceRun> runs, LotRect quadrant, GridPoint facing,
            IReadOnlyList<Road> roads, string who)
        {
            // The fence traces the road-aware inset rectangle: road-bordering
            // edges pulled off the sidewalk's outer edge, map edges pulled off
            // the quadrant boundary.
            var expected = ExpectedInsetRect(quadrant, roads);
            var perp = new GridPoint(-facing.Z, facing.X);

            // Every fence point stays within the road-aware inset rectangle.
            foreach (var run in runs)
            {
                foreach (var point in new[] { run.A, run.B })
                {
                    Assert.That(point.X, Is.GreaterThanOrEqualTo(expected.MinX - Epsilon),
                        $"{who}: fence point {point} is outside the road-aware inset (MinX)");
                    Assert.That(point.X, Is.LessThanOrEqualTo(expected.MaxX + Epsilon),
                        $"{who}: fence point {point} is outside the road-aware inset (MaxX)");
                    Assert.That(point.Z, Is.GreaterThanOrEqualTo(expected.MinZ - Epsilon),
                        $"{who}: fence point {point} is outside the road-aware inset (MinZ)");
                    Assert.That(point.Z, Is.LessThanOrEqualTo(expected.MaxZ + Epsilon),
                        $"{who}: fence point {point} is outside the road-aware inset (MaxZ)");
                }
            }

            // The rear run sits exactly on the rear edge of the road-aware
            // inset rectangle (the edge opposite the facing).
            var rear = RearRunOf(runs);
            var rearAlong = AlongOf(rear.A, facing);
            Assert.That(AlongOf(rear.B, facing), Is.EqualTo(rearAlong).Within(Epsilon),
                $"{who}: the rear run is a single boundary-parallel line");
            Assert.That(rearAlong, Is.EqualTo(RearEdgeAlong(expected, facing)).Within(Epsilon),
                $"{who}: rear run must sit on the road-aware inset rear edge");

            // The two side runs sit on the two side edges of the road-aware
            // inset rectangle (the cross-axis extremes).
            var sideCrosses = SideRunsOf(runs).Select(r => CrossOf(r.A, perp)).OrderBy(c => c).ToList();
            var cornerCrossA = CrossOf(new GridPoint(expected.MinX, expected.MinZ), perp);
            var cornerCrossB = CrossOf(new GridPoint(expected.MaxX, expected.MaxZ), perp);
            var insetCrossLo = Math.Min(cornerCrossA, cornerCrossB);
            var insetCrossHi = Math.Max(cornerCrossA, cornerCrossB);
            Assert.That(sideCrosses[0], Is.EqualTo(insetCrossLo).Within(Epsilon),
                $"{who}: the low-side run must sit on the road-aware inset side edge");
            Assert.That(sideCrosses[1], Is.EqualTo(insetCrossHi).Within(Epsilon),
                $"{who}: the high-side run must sit on the road-aware inset side edge");
        }

        /// <summary>The road-aware inset rectangle a correct fence traces:
        /// every quadrant edge that lies on a road centerline (and overlaps
        /// that road's finite extent) is pulled in by
        /// <see cref="LotBounds.StreetCorridorInset"/>, then EVERY edge by one
        /// <see cref="LotFence.BoundaryOffset"/>. Written independently of the
        /// SUT so it is a real check.</summary>
        private static LotRect ExpectedInsetRect(LotRect quadrant, IReadOnlyList<Road> roads)
        {
            var minX = quadrant.MinX;
            var maxX = quadrant.MaxX;
            var minZ = quadrant.MinZ;
            var maxZ = quadrant.MaxZ;

            foreach (var road in roads)
            {
                if (road.Orientation == StreetOrientation.NorthSouth)
                {
                    var extentMin = road.Center.Z - road.HalfLength;
                    var extentMax = road.Center.Z + road.HalfLength;
                    if (!(minZ < extentMax && maxZ > extentMin))
                    {
                        continue;
                    }

                    if (Math.Abs(minX - road.Center.X) <= Epsilon)
                    {
                        minX += LotBounds.StreetCorridorInset;
                    }

                    if (Math.Abs(maxX - road.Center.X) <= Epsilon)
                    {
                        maxX -= LotBounds.StreetCorridorInset;
                    }
                }
                else
                {
                    var extentMin = road.Center.X - road.HalfLength;
                    var extentMax = road.Center.X + road.HalfLength;
                    if (!(minX < extentMax && maxX > extentMin))
                    {
                        continue;
                    }

                    if (Math.Abs(minZ - road.Center.Z) <= Epsilon)
                    {
                        minZ += LotBounds.StreetCorridorInset;
                    }

                    if (Math.Abs(maxZ - road.Center.Z) <= Epsilon)
                    {
                        maxZ -= LotBounds.StreetCorridorInset;
                    }
                }
            }

            return new LotRect(
                minX + LotFence.BoundaryOffset, maxX - LotFence.BoundaryOffset,
                minZ + LotFence.BoundaryOffset, maxZ - LotFence.BoundaryOffset);
        }

        /// <summary>Whether <paramref name="road"/> runs parallel to
        /// <paramref name="facing"/> (a north-south road runs along Z, so it
        /// is parallel to a Z-facing).</summary>
        private static bool RunsParallelTo(Road road, GridPoint facing)
        {
            return road.Orientation == StreetOrientation.NorthSouth
                ? Math.Abs(facing.Z) > Math.Abs(facing.X)
                : Math.Abs(facing.X) > Math.Abs(facing.Z);
        }

        /// <summary>Perpendicular distance from <paramref name="point"/> to a
        /// road's centerline (constant-X for north-south, constant-Z for
        /// east-west).</summary>
        private static float PerpDistanceToCenterline(GridPoint point, Road road)
        {
            return road.Orientation == StreetOrientation.NorthSouth
                ? Math.Abs(point.X - road.Center.X)
                : Math.Abs(point.Z - road.Center.Z);
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

                // The rear run spans the cross extent of the road-aware inset
                // rectangle: for a corner lot one side is a road edge (pulled
                // off the sidewalk's outer edge) and one a map edge (pulled off
                // the quadrant boundary), so the span is the quadrant width
                // (30m) minus one StreetCorridorInset + BoundaryOffset (road
                // side) and one BoundaryOffset (map side) — 20.25m.
                var facing = HousePlacement.FrontFacing(lot);
                var expected = ExpectedInsetRect(LotBounds.QuadrantBounds(lot), NeighborhoodLayout.Roads);
                var expectedEnclosed = PerpExtent(expected, facing);
                var rear = RearRunOf(runs);
                Assert.That(rear.Length, Is.EqualTo(expectedEnclosed).Within(Epsilon),
                    $"lot {lot.HouseId}: rear run must span the road-aware inset width");

                // Meaningfully wider than the house footprint (at least twice
                // as wide — for building-type-k, 20.25m vs 6.45m).
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
            Assert.That(narrowRear.Length, Is.GreaterThan(narrowFootprintX + 10f),
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

        [Test]
        public void ZoneLot_FenceGeometryResolution_DoesNotThrow()
        {
            // #414: LotFence.GeometryFor resolves the house model via
            // HouseModelCatalog.ForHouse(lot.HouseId). For a zone lot (id >= 5)
            // that used to throw through HouseStyleTable (no starter style);
            // the chokepoint fix routes it through the #299 rolled ladder, so
            // both the raw geometry and the HasFence-gated RunsFor resolve.
            var zoneLot = new HouseLot(
                Doggiehood.Core.Art.HouseVariantAssignment.FirstZoneHouseId, Quadrant.NorthEast,
                new GridPoint(NeighborhoodLayout.LotDistanceFromCenter,
                    NeighborhoodLayout.LotDistanceFromCenter));

            Assert.That(() => LotFence.GeometryFor(zoneLot), Throws.Nothing,
                "zone lot backyard fence geometry must not throw through ForHouse");
            Assert.That(() => LotFence.RunsFor(FencedCloneOf(zoneLot)), Throws.Nothing,
                "an enabled zone lot's fence runs must resolve too");
        }

        [Test]
        public void GeometryFor_ForEveryRealUnlockedZoneLot_DoesNotThrow()
        {
            // #424: fence geometry must be DEFINED for every house, including
            // houses built on an unlocked zone's OWN tile — not just the
            // starting four. A hand-placed lot on the starting tile does NOT
            // reproduce the zone-tile offset (that gap caused #429's CI-only
            // `maxZ must be >= minZ` crash), so exercise the REAL ZoneCatalog
            // lots, whose positions sit on their own zone tile. Resolves the
            // rolled model (#414/#299), the tile-centered QuadrantBounds
            // (#429/#405), and the fallback facing.
            foreach (var lot in FrontierTestWorld.FirstTileLots())
            {
                Assert.That(() => LotFence.GeometryFor(lot), Throws.Nothing,
                    $"zone lot {lot.HouseId} at {lot.Position}: fence geometry must not throw");
                Assert.That(() => LotFence.RunsFor(FencedCloneOf(lot)), Throws.Nothing,
                    $"zone lot {lot.HouseId}: an enabled fence's runs must resolve too");
            }
        }

        [Test]
        public void Fence_ForEveryRealUnlockedZoneLot_IsTheSameFiveRunFrontOpenShape_OrientedToItsFacing()
        {
            // #424: a zone house's fence is the SAME five-run, front-open
            // backyard shape a starting house gets, oriented to the lot's
            // facing (the Z-sign fallback for zone lots today; perfect
            // live-network street facing is deferred to #430). The two open
            // ends reach the house side-wall midpoints and nothing crosses the
            // open front.
            foreach (var lot in FrontierTestWorld.FirstTileLots())
            {
                var runs = LotFence.RunsFor(FencedCloneOf(lot));
                Assert.That(runs.Count, Is.EqualTo(5),
                    $"zone lot {lot.HouseId}: two side runs + one rear run + two connectors");

                var facing = HousePlacement.FrontFacing(lot);
                var house = HousePlacement.Position(lot, HousePlacement.KitScale);
                var model = HouseModelCatalog.ForHouse(lot.HouseId);
                var halfWidth = HousePlacement.KitScale * model.FootprintX / 2f;

                // The two open ends are the house side-wall midpoints.
                var perp = new GridPoint(-facing.Z, facing.X);
                var expectedAnchors = new[]
                {
                    new GridPoint(house.X + perp.X * halfWidth, house.Z + perp.Z * halfWidth),
                    new GridPoint(house.X - perp.X * halfWidth, house.Z - perp.Z * halfWidth),
                };

                var openEnds = OpenEndsOf(runs);
                Assert.That(openEnds.Count, Is.EqualTo(2),
                    $"zone lot {lot.HouseId}: one open polyline with two ends");
                foreach (var anchor in expectedAnchors)
                {
                    Assert.That(openEnds.Any(e => PointsNearlyEqual(e, anchor)), Is.True,
                        $"zone lot {lot.HouseId}: a connector must reach the side wall midpoint {anchor}");
                }

                // Front stays open: every run endpoint sits at or behind the
                // house center along the facing axis.
                foreach (var run in runs)
                {
                    foreach (var point in new[] { run.A, run.B })
                    {
                        var alongFacing = (point.X - house.X) * facing.X + (point.Z - house.Z) * facing.Z;
                        Assert.That(alongFacing, Is.LessThanOrEqualTo(Epsilon),
                            $"zone lot {lot.HouseId}: fence point {point} intrudes into the front yard");
                    }
                }
            }
        }

        [Test]
        public void HouseLot_HasNoFenceOverride_ByDefault()
        {
            // #223: the manual per-lot override is opt-in. A lot built without
            // one reports no override, and every starting lot is override-free
            // so the shipping behavior is the auto-derived geometry.
            var lot = new HouseLot(1, Quadrant.NorthEast, new GridPoint(14f, 14f));
            Assert.That(lot.HasFenceOverride, Is.False, "no override by default");
            Assert.That(lot.FenceOverride, Is.Null, "the override is absent by default");

            foreach (var starting in NeighborhoodLayout.HouseLots)
            {
                Assert.That(starting.HasFenceOverride, Is.False,
                    $"lot {starting.HouseId} must ship with no fence override");
            }
        }

        [Test]
        public void GeometryFor_WithNoOverride_ReturnsTodaysAutoDerivedGeometry()
        {
            // #223 regression guard: the no-override path must stay
            // byte-for-byte today's model-derived geometry.
            foreach (var lot in NeighborhoodLayout.HouseLots)
            {
                var withoutOverride = LotFence.GeometryFor(lot);
                Assert.That(withoutOverride, Is.Not.Empty,
                    $"lot {lot.HouseId}: auto-derived geometry stays queryable");

                // Building an identical lot (still no override) yields the same
                // runs — the auto-derivation is unchanged by #223.
                var twin = new HouseLot(lot.HouseId, lot.Quadrant, lot.Position);
                var again = LotFence.GeometryFor(twin);
                Assert.That(again.Count, Is.EqualTo(withoutOverride.Count),
                    $"lot {lot.HouseId}: auto-derived run count unchanged");
                for (var i = 0; i < withoutOverride.Count; i++)
                {
                    AssertPointsEqual(again[i].A, withoutOverride[i].A, $"lot {lot.HouseId} run {i} A");
                    AssertPointsEqual(again[i].B, withoutOverride[i].B, $"lot {lot.HouseId} run {i} B");
                }
            }
        }

        [Test]
        public void GeometryFor_WithOverride_ReturnsTheOverriddenRunsVerbatim()
        {
            // #223: when a lot carries a manual override, GeometryFor returns
            // exactly those anchors/runs, NOT the model-derived geometry.
            var baseLot = NeighborhoodLayout.HouseLots[0];
            var autoDerived = LotFence.GeometryFor(baseLot);

            // A deliberately different, still-continuous three-run chain.
            var overrideRuns = new[]
            {
                new FenceRun(new GridPoint(2f, 2f), new GridPoint(2f, 10f)),
                new FenceRun(new GridPoint(2f, 10f), new GridPoint(12f, 10f)),
                new FenceRun(new GridPoint(12f, 10f), new GridPoint(12f, 2f)),
            };

            var lot = new HouseLot(
                baseLot.HouseId, baseLot.Quadrant, baseLot.Position, fenceOverride: overrideRuns);

            var runs = LotFence.GeometryFor(lot);
            Assert.That(runs.Count, Is.EqualTo(overrideRuns.Length),
                "the override's run count is used verbatim");
            for (var i = 0; i < overrideRuns.Length; i++)
            {
                AssertPointsEqual(runs[i].A, overrideRuns[i].A, $"override run {i} A");
                AssertPointsEqual(runs[i].B, overrideRuns[i].B, $"override run {i} B");
            }

            // ...and it is NOT the auto-derived geometry it replaced (the
            // shipping shape is five runs; this override is a three-run chain).
            Assert.That(runs.Count, Is.Not.EqualTo(autoDerived.Count),
                "sanity: the override replaces, not augments, the auto-derived geometry");
        }

        [Test]
        public void RunsFor_StillHonoursHasFence_WhenAnOverrideIsSet()
        {
            // #223: an override changes fence SHAPE, never visibility. With the
            // flag off the lot contributes nothing; with it on it contributes
            // exactly the overridden runs.
            var baseLot = NeighborhoodLayout.HouseLots[0];
            var overrideRuns = new[]
            {
                new FenceRun(new GridPoint(2f, 2f), new GridPoint(2f, 10f)),
                new FenceRun(new GridPoint(2f, 10f), new GridPoint(12f, 10f)),
                new FenceRun(new GridPoint(12f, 10f), new GridPoint(12f, 2f)),
            };

            var hidden = new HouseLot(
                baseLot.HouseId, baseLot.Quadrant, baseLot.Position,
                hasFence: false, fenceOverride: overrideRuns);
            Assert.That(LotFence.RunsFor(hidden), Is.Empty,
                "override + HasFence off must contribute no runs");

            var shown = new HouseLot(
                baseLot.HouseId, baseLot.Quadrant, baseLot.Position,
                hasFence: true, fenceOverride: overrideRuns);
            var runs = LotFence.RunsFor(shown);
            Assert.That(runs.Count, Is.EqualTo(overrideRuns.Length),
                "override + HasFence on must contribute the overridden runs");
            for (var i = 0; i < overrideRuns.Length; i++)
            {
                AssertPointsEqual(runs[i].A, overrideRuns[i].A, $"shown override run {i} A");
                AssertPointsEqual(runs[i].B, overrideRuns[i].B, $"shown override run {i} B");
            }
        }

        [Test]
        public void FenceOverride_WithAGap_IsRejected()
        {
            // #223: a malformed override — a chain with a gap between
            // consecutive runs — is rejected at construction, mirroring
            // BackyardRuns' argument guards. The override must be a continuous
            // open polyline, preserving the continuous-runs invariant the
            // auto-derived geometry guarantees.
            var baseLot = NeighborhoodLayout.HouseLots[0];
            var gapped = new[]
            {
                new FenceRun(new GridPoint(2f, 2f), new GridPoint(2f, 10f)),
                // Does NOT connect to the previous run — a gap.
                new FenceRun(new GridPoint(50f, 50f), new GridPoint(60f, 50f)),
                new FenceRun(new GridPoint(60f, 50f), new GridPoint(60f, 40f)),
            };

            Assert.That(
                () => new HouseLot(baseLot.HouseId, baseLot.Quadrant, baseLot.Position,
                    fenceOverride: gapped),
                Throws.ArgumentException,
                "an override with a gap between runs must be rejected");

            Assert.That(
                () => new HouseLot(baseLot.HouseId, baseLot.Quadrant, baseLot.Position,
                    fenceOverride: Array.Empty<FenceRun>()),
                Throws.ArgumentException,
                "an empty override is malformed too");
        }

        // ---- #460: fence connectors track the house's CURRENT level ----

        [Test]
        public void GeometryFor_WithState_AtLevelOne_IsByteIdenticalToTheLevelBlindGeometry()
        {
            // #460 regression guard (the #147/#342 as-built contract): a fresh
            // GameState has every starting house at level 1, so the level-aware
            // state overload must produce byte-for-byte the same runs as the
            // level-blind geometry — the fix must not disturb today's shape.
            var state = GameState.CreateNew();
            state.SetTargetMap(FrontierTestWorld.LoadAuthoredTargetMap());
            foreach (var lot in NeighborhoodLayout.HouseLots)
            {
                var blind = LotFence.GeometryFor(lot);
                var aware = LotFence.GeometryFor(lot, state);
                Assert.That(aware.Count, Is.EqualTo(blind.Count),
                    $"lot {lot.HouseId}: same run count at level 1");
                for (var i = 0; i < blind.Count; i++)
                {
                    Assert.That(aware[i].A.X, Is.EqualTo(blind[i].A.X),
                        $"lot {lot.HouseId} run {i} A.X byte-identical at level 1");
                    Assert.That(aware[i].A.Z, Is.EqualTo(blind[i].A.Z),
                        $"lot {lot.HouseId} run {i} A.Z byte-identical at level 1");
                    Assert.That(aware[i].B.X, Is.EqualTo(blind[i].B.X),
                        $"lot {lot.HouseId} run {i} B.X byte-identical at level 1");
                    Assert.That(aware[i].B.Z, Is.EqualTo(blind[i].B.Z),
                        $"lot {lot.HouseId} run {i} B.Z byte-identical at level 1");
                }
            }
        }

        [Test]
        public void ConnectorTerminationPoints_DeriveFromTheHousesCurrentLevelFootprint_NotAlwaysLevelOne()
        {
            // #460: once a house upgrades its rendered mesh grows, so the two
            // connectors must reach the CURRENT level's side-wall midpoints, not
            // stay pinned to the level-1 half-width (which would land inside the
            // wider upgraded house).
            var state = GameState.CreateNew();
            state.SetTargetMap(FrontierTestWorld.LoadAuthoredTargetMap());
            var lot = NeighborhoodLayout.HouseLots.Single(l => l.HouseId == 1);

            UpgradeTo(state, 1, 4); // r -> c -> s -> b, footprint grows every rung
            Assert.That(state.Houses.Single(h => h.Id == 1).Level, Is.EqualTo(4));

            var runs = LotFence.RunsFor(FencedCloneOf(lot), state);
            var facing = HousePlacement.FrontFacing(lot);
            var house = HousePlacement.Position(lot, HousePlacement.KitScale);
            var perp = new GridPoint(-facing.Z, facing.X);

            var level4HalfWidth = HousePlacement.KitScale
                * HouseModelCatalog.ForHouse(1, 4).FootprintX / 2f;
            var level1HalfWidth = HousePlacement.KitScale
                * HouseModelCatalog.ForHouse(1).FootprintX / 2f;
            Assert.That(level4HalfWidth, Is.GreaterThan(level1HalfWidth),
                "sanity: house 1's ladder widens from level 1 to level 4");

            var openEnds = OpenEndsOf(runs);
            var level4Anchors = new[]
            {
                new GridPoint(house.X + perp.X * level4HalfWidth, house.Z + perp.Z * level4HalfWidth),
                new GridPoint(house.X - perp.X * level4HalfWidth, house.Z - perp.Z * level4HalfWidth),
            };
            foreach (var anchor in level4Anchors)
            {
                Assert.That(openEnds.Any(e => PointsNearlyEqual(e, anchor)), Is.True,
                    $"a connector must reach the LEVEL-4 side-wall midpoint {anchor}");
            }

            var level1Anchors = new[]
            {
                new GridPoint(house.X + perp.X * level1HalfWidth, house.Z + perp.Z * level1HalfWidth),
                new GridPoint(house.X - perp.X * level1HalfWidth, house.Z - perp.Z * level1HalfWidth),
            };
            foreach (var stale in level1Anchors)
            {
                Assert.That(openEnds.Any(e => PointsNearlyEqual(e, stale)), Is.False,
                    $"the connector must NOT stay pinned to the stale level-1 midpoint {stale}");
            }
        }

        [Test]
        public void UpgradingAHouse_MovesTheConnectorEndpoints_WheneverTheLaddersFootprintChanges()
        {
            // #460 no-op guard: upgrading a starter house through TryUpgradeHouse
            // and re-querying RunsFor(lot, state) must move the connector open
            // ends whenever the ladder's footprint changes, so the fix can't
            // silently no-op.
            var state = GameState.CreateNew();
            state.SetTargetMap(FrontierTestWorld.LoadAuthoredTargetMap());
            var fencedLot = FencedCloneOf(NeighborhoodLayout.HouseLots.Single(l => l.HouseId == 1));

            Assert.That(HouseModelCatalog.ForHouse(1, 2).FootprintX,
                Is.Not.EqualTo(HouseModelCatalog.ForHouse(1, 1).FootprintX),
                "sanity: house 1's level 1 -> 2 footprint changes (r -> c)");

            var before = OpenEndsOf(LotFence.RunsFor(fencedLot, state));
            UpgradeTo(state, 1, 2);
            var after = OpenEndsOf(LotFence.RunsFor(fencedLot, state));

            Assert.That(after.Any(a => !before.Any(b => PointsNearlyEqual(a, b))), Is.True,
                "the connector open ends must move after an upgrade that changes the footprint");
        }

        [Test]
        public void ZoneHouse_ConnectorTerminationPoints_TrackItsRolledLadderLevel()
        {
            // #460: the level-aware resolution covers a ZONE house (id >= 5)
            // upgrading through its rolled ladder, not just a starter house.
            var state = GameState.CreateNew();
            state.SetTargetMap(FrontierTestWorld.LoadAuthoredTargetMap());
            state.Wallet.Deposit(100000);
            Assert.That(state.TryUnlockTile(FrontierTestWorld.FirstTile), Is.True);
            var lot = FrontierTestWorld.FirstTileLots()[0];
            Assert.That(state.TryBuildHouse(lot.HouseId), Is.True, "the zone house builds at level 1");
            var fencedLot = FencedCloneOf(lot);

            Assert.That(HouseModelCatalog.ForHouse(lot.HouseId, 2).FootprintX,
                Is.Not.EqualTo(HouseModelCatalog.ForHouse(lot.HouseId, 1).FootprintX),
                "sanity: the zone house's rolled ladder footprint changes level 1 -> 2");

            var before = OpenEndsOf(LotFence.RunsFor(fencedLot, state));
            state.Wallet.Deposit(Doggiehood.Core.Expansion.HouseUpgradeNumbers.CostToReach(2));
            Assert.That(state.TryUpgradeHouse(lot.HouseId), Is.True, "the zone house upgrades to level 2");
            var after = OpenEndsOf(LotFence.RunsFor(fencedLot, state));

            Assert.That(after.Any(a => !before.Any(b => PointsNearlyEqual(a, b))), Is.True,
                "a zone house's connector endpoints must track its rolled ladder's per-level footprint");

            // #461: the zone fence now orients to the lot's REAL walk-network
            // facing/position (state.WalkNetwork), not the pre-rotation single-arg
            // Z-sign fallback — so the expected side-wall midpoints are computed
            // from that same live resolution.
            var facing = HousePlacement.FrontFacing(lot, state.WalkNetwork);
            var house = HousePlacement.Position(lot, HousePlacement.KitScale, state.WalkNetwork);
            var perp = new GridPoint(-facing.Z, facing.X);
            var halfWidth = HousePlacement.KitScale
                * HouseModelCatalog.ForHouse(lot.HouseId, 2).FootprintX / 2f;
            var expected = new[]
            {
                new GridPoint(house.X + perp.X * halfWidth, house.Z + perp.Z * halfWidth),
                new GridPoint(house.X - perp.X * halfWidth, house.Z - perp.Z * halfWidth),
            };
            foreach (var anchor in expected)
            {
                Assert.That(after.Any(e => PointsNearlyEqual(e, anchor)), Is.True,
                    $"a zone connector must reach its LEVEL-2 side-wall midpoint {anchor}");
            }
        }

        // ---- #461: zone-lot fence orients to the REAL walk-network facing ----

        [Test]
        public void GeometryFor_WithState_ForABuiltZoneLot_TracesTheRealFacing_NotTheZSignFallback()
        {
            // #461: the state-aware GeometryFor must trace fence runs from the
            // lot's REAL walk-network front-facing edge (state.WalkNetwork), not
            // NeighborhoodLayout.WalkNetwork's Z-sign fallback. The first zone's
            // cul-de-sac lots face along X once built, so the connectors reach the
            // side-wall midpoints offset along Z (perp of an X-facing) — the exact
            // opposite of the Z-sign guess (0, -1), whose perp is along X.
            var state = GameState.CreateNew();
            state.SetTargetMap(FrontierTestWorld.LoadAuthoredTargetMap());
            state.Wallet.Deposit(1_000_000);
            Assert.That(state.TryUnlockTile(FrontierTestWorld.FirstTile), Is.True, "the first zone unlocks");
            var lot = FrontierTestWorld.FirstTileLots()[0];
            Assert.That(state.TryBuildHouse(lot.HouseId), Is.True, "the zone house builds");

            var realFacing = HousePlacement.FrontFacing(lot, state.WalkNetwork);
            var zSignFacing = HousePlacement.FrontFacing(lot); // singleton -> Z-sign fallback
            Assert.That(realFacing, Is.Not.EqualTo(zSignFacing),
                "sanity: this zone lot's real facing differs from the Z-sign fallback");

            var runs = LotFence.GeometryFor(FencedCloneOf(lot), state);
            Assert.That(runs.Count, Is.EqualTo(5), "the five-run backyard shape");

            var house = HousePlacement.Position(lot, HousePlacement.KitScale, state.WalkNetwork);
            var model = HouseModelCatalog.ForHouse(lot.HouseId);
            var halfWidth = HousePlacement.KitScale * model.FootprintX / 2f;
            var realPerp = new GridPoint(-realFacing.Z, realFacing.X);
            var realAnchors = new[]
            {
                new GridPoint(house.X + realPerp.X * halfWidth, house.Z + realPerp.Z * halfWidth),
                new GridPoint(house.X - realPerp.X * halfWidth, house.Z - realPerp.Z * halfWidth),
            };

            var openEnds = OpenEndsOf(runs);
            Assert.That(openEnds.Count, Is.EqualTo(2), "one open polyline with two ends");
            foreach (var anchor in realAnchors)
            {
                Assert.That(openEnds.Any(e => PointsNearlyEqual(e, anchor)), Is.True,
                    $"a connector must reach the REAL-facing side-wall midpoint {anchor}");
            }

            // The Z-sign fallback's side-wall midpoints (perp along X) must NOT be
            // where the connectors land — proving the fence rotated with the house.
            var zPerp = new GridPoint(-zSignFacing.Z, zSignFacing.X);
            var zAnchors = new[]
            {
                new GridPoint(lot.Position.X + zPerp.X * halfWidth, lot.Position.Z + zPerp.Z * halfWidth),
                new GridPoint(lot.Position.X - zPerp.X * halfWidth, lot.Position.Z - zPerp.Z * halfWidth),
            };
            foreach (var stale in zAnchors)
            {
                Assert.That(openEnds.Any(e => PointsNearlyEqual(e, stale)), Is.False,
                    $"the connectors must NOT sit at the pre-rotation Z-sign midpoint {stale}");
            }
        }

        // ---- #509: expansion-tile fence stays out of the tile's own road ----

        [Test]
        public void GeometryFor_WithState_OnAnExpansionTile_CorridorClearsThatTilesOwnRoad_NotJustTheOriginRoads()
        {
            // #509: GeometryFor(lot, state) resolved the road-corridor clear from
            // the static NeighborhoodLayout.Roads (the origin FourWay's arms only),
            // so on an EXPANSION tile a lot whose SIDE edge borders that tile's own
            // road — a road absent from NeighborhoodLayout.Roads — was never
            // recognised as road-bordering. Its fence side run was inset by a plain
            // BoundaryOffset off an edge sitting on the road centerline, landing the
            // fence in the paved road. The fix feeds LotBounds.RoadsFor(lot, tileType)
            // (origin roads PLUS the lot's own tile's roads) so every tile's
            // road-bordering edges are corridor-cleared.
            //
            // TeeEast at (-1, 0) — directly west of the origin, a legal frontier
            // unlock — has a N-S crossbar (the faced road, front-open) plus an EAST
            // stem arm. Its NorthEast lot borders that stem on a SIDE edge; the stem
            // lives only in the per-tile road geometry, never in
            // NeighborhoodLayout.Roads.
            var state = FrontierTestWorld.AfterOnboarding();
            state.Wallet.Deposit(1_000_000);

            var tile = new TileCoordinate(-1, 0);
            Assert.That(state.TryUnlockTile(tile), Is.True, "precondition: the TeeEast tile unlocks");
            var tileType = state.Map.GetTileAt(tile);
            Assert.That(tileType, Is.EqualTo(TileType.TeeEast), "precondition: authored map places TeeEast here");

            var houseId = FrontierHouseId.For(tile, Quadrant.NorthEast);
            Assert.That(state.TryBuildHouse(houseId), Is.True, "precondition: the lot's house builds");
            var lot = state.GetHouseLot(houseId);

            var roads = LotBounds.RoadsFor(lot, tileType);

            // Reconstruct the pre-fix geometry: the same five-run shape the buggy
            // GeometryFrom produced, given ONLY the origin roads. It proves the
            // setup genuinely triggers the bug (non-vacuous) — a side run sits in
            // the tile's own paved road.
            var facing = HousePlacement.PredeterminedFrontFacing(lot, state.WalkNetwork);
            var position = HousePlacement.PredeterminedPosition(
                lot, HousePlacement.KitScale, state.WalkNetwork,
                Doggiehood.Core.Art.HouseLevelModelTable.MinLevel);
            var model = HouseModelCatalog.ForHouse(lot.HouseId, state.GetHouseLevel(lot.HouseId));
            var quadrant = LotBounds.QuadrantBounds(lot);
            var preFixRuns = LotFence.BackyardRuns(
                quadrant, model, position, facing, HousePlacement.KitScale, NeighborhoodLayout.Roads);
            Assert.That(
                preFixRuns.SelectMany(RunEndpoints).Any(p => roads.Any(r => InsidePavedRoad(p, r))),
                Is.True,
                "sanity: with only the origin roads the fence lands in the expansion tile's own road");

            // The fix: GeometryFor(lot, state) corridor-clears against the tile's
            // own roads, so NO fence point sits in any paved road.
            var runs = LotFence.GeometryFor(FencedCloneOf(lot), state);
            foreach (var point in runs.SelectMany(RunEndpoints))
            {
                foreach (var road in roads)
                {
                    Assert.That(InsidePavedRoad(point, road), Is.False,
                        $"fence point {point} must not sit inside the paved road at {road.Center}");
                }
            }
        }

        private static IEnumerable<GridPoint> RunEndpoints(FenceRun run)
        {
            yield return run.A;
            yield return run.B;
        }

        /// <summary>Whether <paramref name="p"/> lies inside <paramref name="road"/>'s
        /// paved corridor: within half a <see cref="WorldDimensions.RoadWidth"/> of the
        /// centerline (perpendicular) AND within the road's finite extent (along).</summary>
        private static bool InsidePavedRoad(GridPoint p, Road road)
        {
            var halfWidth = WorldDimensions.RoadWidth / 2f;
            if (road.Orientation == StreetOrientation.NorthSouth)
            {
                var withinExtent = p.Z > road.Center.Z - road.HalfLength - Epsilon
                    && p.Z < road.Center.Z + road.HalfLength + Epsilon;
                return withinExtent && Math.Abs(p.X - road.Center.X) < halfWidth - Epsilon;
            }

            var withinAlong = p.X > road.Center.X - road.HalfLength - Epsilon
                && p.X < road.Center.X + road.HalfLength + Epsilon;
            return withinAlong && Math.Abs(p.Z - road.Center.Z) < halfWidth - Epsilon;
        }

        /// <summary>Deposits exactly what each rung costs and upgrades
        /// <paramref name="houseId"/> one level at a time to
        /// <paramref name="targetLevel"/> through the real
        /// <see cref="GameState.TryUpgradeHouse"/> path.</summary>
        private static void UpgradeTo(GameState state, int houseId, int targetLevel)
        {
            while (state.Houses.Single(h => h.Id == houseId).Level < targetLevel)
            {
                var next = state.Houses.Single(h => h.Id == houseId).Level + 1;
                state.Wallet.Deposit(Doggiehood.Core.Expansion.HouseUpgradeNumbers.CostToReach(next));
                Assert.That(state.TryUpgradeHouse(houseId), Is.True,
                    $"house {houseId} upgrades toward level {targetLevel}");
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

        /// <summary>The extent of <paramref name="rect"/> perpendicular to
        /// <paramref name="facing"/> — its Depth for an X-facing, its Width for
        /// a Z-facing.</summary>
        private static float PerpExtent(LotRect rect, GridPoint facing)
        {
            return Math.Abs(facing.X) > Math.Abs(facing.Z) ? rect.Depth : rect.Width;
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
