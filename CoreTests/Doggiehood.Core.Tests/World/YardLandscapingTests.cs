using System;
using System.Collections.Generic;
using System.Linq;
using Doggiehood.Core.World;
using NUnit.Framework;

namespace Doggiehood.Core.Tests.World
{
    /// <summary>
    /// #170: procedural yard landscaping. Per lot, the front yard
    /// generates up to 4 candidate points (1 usually shown, 2
    /// occasionally) and the back yard generates up to 11 (3-5 shown),
    /// all collision-aware against the house footprint, the front
    /// walkway, the backyard fence line (#146), and each other — spaced
    /// by a tree footprint radius derived from the larger of the two tree
    /// kit meshes (tree-large.fbx/tree-small.fbx; the planter kind was
    /// removed in #243). Selection is seeded deterministically per lot.
    /// </summary>
    public class YardLandscapingTests
    {
        private static LotRect UnobstructedRegion => new LotRect(-50f, 50f, -50f, 50f);
        private static LotRect NoHouse => new LotRect(1000f, 1001f, 1000f, 1001f);

        [Test]
        public void GenerateFrontCandidates_InAnUnobstructedRegion_YieldsUpToFour()
        {
            var candidates = YardLandscaping.GenerateFrontCandidates(
                UnobstructedRegion, NoHouse, walkway: null, seed: 1);

            Assert.That(candidates.Count, Is.EqualTo(YardLandscaping.FrontCandidateCount),
                "an unobstructed region should be able to fit the full front candidate count");
        }

        [Test]
        public void GenerateBackCandidates_InAnUnobstructedRegion_YieldsUpToEleven()
        {
            var candidates = YardLandscaping.GenerateBackCandidates(
                UnobstructedRegion, NoHouse, fenceRuns: Array.Empty<FenceRun>(), seed: 1);

            Assert.That(candidates.Count, Is.EqualTo(YardLandscaping.BackCandidateCount),
                "an unobstructed region should be able to fit the full back candidate count");
        }

        [Test]
        public void FrontCandidates_StayWithinTheFrontYard_ClearOfHouseAndWalkway_AndMutuallySpaced()
        {
            foreach (var lot in NeighborhoodLayout.HouseLots)
            {
                var frontYard = LotBounds.FrontYard(lot);
                var candidates = YardLandscaping.FrontCandidatesFor(lot);

                Assert.That(candidates.Count, Is.LessThanOrEqualTo(YardLandscaping.FrontCandidateCount),
                    $"lot {lot.HouseId}: front candidates must never exceed the front candidate count");

                NeighborhoodLayout.WalkNetwork.TryGetFrontWalkway(lot.HouseId, out var walkway);
                var footprint = HouseFootprintOf(lot);

                foreach (var candidate in candidates)
                {
                    Assert.That(frontYard.Contains(candidate.Position), Is.True,
                        $"lot {lot.HouseId}: candidate {candidate.Position} must sit inside the front yard");

                    Assert.That(DistanceToRect(candidate.Position, footprint),
                        Is.GreaterThanOrEqualTo(YardLandscaping.TreeFootprintRadius),
                        $"lot {lot.HouseId}: candidate {candidate.Position} must clear the house footprint");

                    Assert.That(DistanceToSegment(candidate.Position, walkway.A, walkway.B),
                        Is.GreaterThanOrEqualTo(YardLandscaping.TreeFootprintRadius + walkway.Width / 2f),
                        $"lot {lot.HouseId}: candidate {candidate.Position} must clear the front walkway");
                }

                AssertMutuallySpaced(candidates.Select(c => c.Position).ToList(), lot.HouseId, "front");
            }
        }

        [Test]
        public void FrontCandidates_ClearTheStreetCorridor_NeverLandingInTheRoad()
        {
            // #244: front candidates escaped the lot into the road because
            // the front-yard region reached the tile centerline — where the
            // road the lot faces runs. Every front candidate's CENTER must
            // clear the road: at minimum the road half-width plus its own
            // footprint radius from the centerline of the road it faces.
            var minClearance = NeighborhoodLayout.StreetWidth / 2f + YardLandscaping.TreeFootprintRadius;

            foreach (var lot in NeighborhoodLayout.HouseLots)
            {
                var facing = HousePlacement.FrontFacing(lot);

                foreach (var candidate in YardLandscaping.FrontCandidatesFor(lot))
                {
                    // The road the front faces runs along the tile centerline
                    // perpendicular to the facing axis (through 0 on that
                    // axis), so distance to its centerline is the candidate's
                    // own coordinate on the facing axis.
                    var distanceToRoadCenterline = facing.X != 0f
                        ? Math.Abs(candidate.Position.X)
                        : Math.Abs(candidate.Position.Z);

                    Assert.That(distanceToRoadCenterline, Is.GreaterThanOrEqualTo(minClearance),
                        $"lot {lot.HouseId}: front candidate {candidate.Position} must clear the road "
                        + $"(at least {minClearance}m from the road centerline it faces), not sit in it");
                }
            }
        }

        [Test]
        public void Candidates_ClearEveryRoadBorderingTheLot_FacedAndPerpendicular_AcrossManySeeds()
        {
            // #272 (follow-up to #244): each lot is one tile QUADRANT, and on
            // the FourWay every quadrant borders TWO roads — one on each inner
            // edge (X=0 and Z=0). #244 only inset the FACED road's edge, so
            // both the front and back yard regions still spanned onto the
            // PERPENDICULAR road's centerline and trees landed in it. Every
            // placed tree's full (scaled) footprint — a conservative circle of
            // TreeFootprintRadius — must clear EVERY road bordering the lot,
            // asserted across all quadrants and many seeds.
            foreach (var lot in NeighborhoodLayout.HouseLots)
            {
                var frontYard = LotBounds.FrontYard(lot);
                var backYard = LotBounds.BackYard(lot);
                var footprint = HouseFootprintOf(lot);
                NeighborhoodLayout.WalkNetwork.TryGetFrontWalkway(lot.HouseId, out var walkway);
                var fenceRuns = LotFence.GeometryFor(lot);

                for (var seed = 0; seed < 50; seed++)
                {
                    var candidates = YardLandscaping
                        .GenerateFrontCandidates(frontYard, footprint, walkway, seed)
                        .Concat(YardLandscaping.GenerateBackCandidates(backYard, footprint, fenceRuns, seed));

                    foreach (var candidate in candidates)
                    {
                        foreach (var road in NeighborhoodLayout.Roads)
                        {
                            Assert.That(DistanceToRect(candidate.Position, RoadRect(road)),
                                Is.GreaterThanOrEqualTo(YardLandscaping.TreeFootprintRadius),
                                $"lot {lot.HouseId} (seed {seed}): candidate {candidate.Position} "
                                + $"must clear the {road.Orientation} road with its full scaled footprint");
                        }
                    }
                }
            }
        }

        private static LotRect RoadRect(Road road)
        {
            var halfWidth = road.Width / 2f;
            return road.Orientation == StreetOrientation.NorthSouth
                ? new LotRect(
                    road.Center.X - halfWidth, road.Center.X + halfWidth,
                    road.Center.Z - road.HalfLength, road.Center.Z + road.HalfLength)
                : new LotRect(
                    road.Center.X - road.HalfLength, road.Center.X + road.HalfLength,
                    road.Center.Z - halfWidth, road.Center.Z + halfWidth);
        }

        [Test]
        public void BackCandidates_StayWithinTheBackYard_ClearOfHouseAndFence_AndMutuallySpaced()
        {
            foreach (var lot in NeighborhoodLayout.HouseLots)
            {
                var backYard = LotBounds.BackYard(lot);
                var candidates = YardLandscaping.BackCandidatesFor(lot);

                Assert.That(candidates.Count, Is.LessThanOrEqualTo(YardLandscaping.BackCandidateCount),
                    $"lot {lot.HouseId}: back candidates must never exceed the back candidate count");

                var fenceRuns = LotFence.GeometryFor(lot);
                var footprint = HouseFootprintOf(lot);

                foreach (var candidate in candidates)
                {
                    Assert.That(backYard.Contains(candidate.Position), Is.True,
                        $"lot {lot.HouseId}: candidate {candidate.Position} must sit inside the back yard");

                    Assert.That(DistanceToRect(candidate.Position, footprint),
                        Is.GreaterThanOrEqualTo(YardLandscaping.TreeFootprintRadius),
                        $"lot {lot.HouseId}: candidate {candidate.Position} must clear the house footprint");

                    foreach (var run in fenceRuns)
                    {
                        Assert.That(DistanceToSegment(candidate.Position, run.A, run.B),
                            Is.GreaterThanOrEqualTo(YardLandscaping.TreeFootprintRadius),
                            $"lot {lot.HouseId}: candidate {candidate.Position} must clear the fence line");
                    }
                }

                AssertMutuallySpaced(candidates.Select(c => c.Position).ToList(), lot.HouseId, "back");
            }
        }

        [Test]
        public void BackTrees_StillFitAgainstTheWiderDeeperLotBoundaryFence()
        {
            // #342: the backyard fence now traces the offset lot boundary
            // (wider and deeper than #146's house-footprint-width fence), and
            // YardLandscaping rejection-samples back candidates against it via
            // LotFence.GeometryFor regardless of visibility. Confirm the back
            // yard still has room: every real lot fills its full 3-5 back tree
            // selection and each placed tree clears the wider/deeper fence.
            foreach (var lot in NeighborhoodLayout.HouseLots)
            {
                var fenceRuns = LotFence.GeometryFor(lot);
                Assert.That(fenceRuns.Count, Is.EqualTo(5),
                    $"lot {lot.HouseId}: the fence is the five-run lot-boundary shape");

                var backTrees = YardLandscaping.BackTreesFor(lot);
                Assert.That(backTrees.Count, Is.InRange(YardLandscaping.BackSelectMin, YardLandscaping.BackSelectMax),
                    $"lot {lot.HouseId}: the back yard must still fit its full 3-5 tree selection "
                    + "against the wider/deeper fence");

                var backYard = LotBounds.BackYard(lot);
                foreach (var tree in backTrees)
                {
                    Assert.That(backYard.Contains(tree.Position), Is.True,
                        $"lot {lot.HouseId}: back tree {tree.Position} must sit inside the back yard");
                    foreach (var run in fenceRuns)
                    {
                        Assert.That(DistanceToSegment(tree.Position, run.A, run.B),
                            Is.GreaterThanOrEqualTo(YardLandscaping.TreeFootprintRadius),
                            $"lot {lot.HouseId}: back tree {tree.Position} must clear the wider/deeper fence line");
                    }
                }
            }
        }

        [Test]
        public void SelectFront_AlwaysPicksOneOrTwo_AndBothOccurAcrossManySeeds()
        {
            var pool = FarApartCandidates(YardLandscaping.FrontCandidateCount);
            var counts = new HashSet<int>();

            for (var seed = 0; seed < 300; seed++)
            {
                var picks = YardLandscaping.SelectFront(pool, seed);
                Assert.That(picks.Count, Is.InRange(1, 2),
                    $"seed {seed}: front selection must pick 1 or 2 trees");
                counts.Add(picks.Count);
            }

            Assert.That(counts, Does.Contain(1), "1 front tree must occur (the common case)");
            Assert.That(counts, Does.Contain(2), "2 front trees must occur occasionally");
        }

        [Test]
        public void SelectBack_AlwaysPicksThreeToFive_AndAllValuesOccurAcrossManySeeds()
        {
            var pool = FarApartCandidates(YardLandscaping.BackCandidateCount);
            var counts = new HashSet<int>();

            for (var seed = 0; seed < 300; seed++)
            {
                var picks = YardLandscaping.SelectBack(pool, seed);
                Assert.That(picks.Count, Is.InRange(3, 5),
                    $"seed {seed}: back selection must pick 3-5 trees");
                counts.Add(picks.Count);
            }

            Assert.That(counts, Does.Contain(3));
            Assert.That(counts, Does.Contain(4));
            Assert.That(counts, Does.Contain(5));
        }

        [Test]
        public void TreeFootprintRadius_IsDerivedFromTheLargerTreeCanopyHalfExtent_AtMaxPossibleScale()
        {
            // #243: with the planter kind removed, the collision spacing is
            // re-derived from the largest REMAINING (tree) mesh — the larger of
            // the two shared tree canopy half-extents (Z, 0.1215 > X, 0.1052).
            // #458: per-tree size now varies up to MaxTreeScaleVariance (×1.25)
            // above the uniform scale, so the spacing must be widened to that
            // WORST-CASE size — UniformScale × MaxTreeScaleVariance — so two
            // trees can never visually overlap even when both land at max size.
            var largerTreeHalfExtent = Math.Max(YardLandscaping.TreeHalfExtentX, YardLandscaping.TreeHalfExtentZ);

            Assert.That(YardLandscaping.TreeFootprintRadius,
                Is.EqualTo(largerTreeHalfExtent * YardLandscaping.UniformScale * YardLandscaping.MaxTreeScaleVariance)
                    .Within(1e-6f),
                "footprint radius must come from the larger tree half-extent at the max possible scale");
            Assert.That(YardLandscaping.MinSpacing,
                Is.EqualTo(YardLandscaping.TreeFootprintRadius * 2f).Within(1e-6f),
                "min spacing is two footprint radii");
        }

        [Test]
        public void SelectedTreeScales_ForRealLots_AlwaysSitBetweenBaselineAndMaxVariance_NeverSmaller()
        {
            // #458: each pick carries a per-tree size multiplier drawn from
            // [BaselineScale, MaxTreeScaleVariance] — never below the current
            // baseline ("never smaller" in the ask), never above +25%.
            foreach (var lot in NeighborhoodLayout.HouseLots)
            {
                var picks = YardLandscaping.FrontTreesFor(lot)
                    .Concat(YardLandscaping.BackTreesFor(lot))
                    .ToList();
                Assert.That(picks, Is.Not.Empty, $"lot {lot.HouseId}: sanity — has yard picks");

                foreach (var pick in picks)
                {
                    Assert.That(pick.Scale,
                        Is.InRange(YardTreePlacement.BaselineScale, YardLandscaping.MaxTreeScaleVariance),
                        $"lot {lot.HouseId}: pick {pick.Position} scale must be baseline..+25%, never smaller");
                }
            }
        }

        [Test]
        public void SelectedTreeScales_SpanTheFullRange_AcrossManySeeds()
        {
            // #458: the draw actually varies — across many seeds some trees are
            // near baseline and some near the +25% cap, not a single fixed value.
            var pool = FarApartCandidates(YardLandscaping.BackCandidateCount);
            var minSeen = float.MaxValue;
            var maxSeen = float.MinValue;

            for (var seed = 0; seed < 300; seed++)
            {
                foreach (var pick in YardLandscaping.SelectBack(pool, seed))
                {
                    Assert.That(pick.Scale,
                        Is.InRange(YardTreePlacement.BaselineScale, YardLandscaping.MaxTreeScaleVariance),
                        $"seed {seed}: scale must stay within [baseline, +25%]");
                    minSeen = Math.Min(minSeen, pick.Scale);
                    maxSeen = Math.Max(maxSeen, pick.Scale);
                }
            }

            Assert.That(minSeen, Is.LessThan(1.05f), "some trees land near the baseline");
            Assert.That(maxSeen, Is.GreaterThan(1.20f), "some trees land near the +25% cap");
        }

        [Test]
        public void SelectedTreeScales_AreDeterministic_ForTheSameLotAcrossRepeatedCalls()
        {
            // #458: the scale draw uses the SAME already-seeded per-lot Random,
            // so repeated calls for the same lot yield byte-identical scales —
            // the #170 per-lot determinism guarantee extended to size.
            foreach (var lot in NeighborhoodLayout.HouseLots)
            {
                var frontA = YardLandscaping.FrontTreesFor(lot).Select(p => p.Scale).ToList();
                var frontB = YardLandscaping.FrontTreesFor(lot).Select(p => p.Scale).ToList();
                var backA = YardLandscaping.BackTreesFor(lot).Select(p => p.Scale).ToList();
                var backB = YardLandscaping.BackTreesFor(lot).Select(p => p.Scale).ToList();

                Assert.That(frontA, Is.EqualTo(frontB),
                    $"lot {lot.HouseId}: front scales stable across repeated calls");
                Assert.That(backA, Is.EqualTo(backB),
                    $"lot {lot.HouseId}: back scales stable across repeated calls");
            }
        }

        [Test]
        public void Selection_OnlyEverYieldsTreeModels_NeverAPlanter()
        {
            // #243: the planter kind is removed from the selection set, so
            // every placed prop can only ever render as one of the two tree
            // meshes — a planter can never be selected.
            var pool = FarApartCandidates(YardLandscaping.BackCandidateCount);
            var kindsSeen = new HashSet<YardTreeKind>();

            for (var seed = 0; seed < 300; seed++)
            {
                foreach (var pick in YardLandscaping.SelectBack(pool, seed))
                {
                    Assert.That(pick.Kind,
                        Is.EqualTo(YardTreeKind.TreeLarge).Or.EqualTo(YardTreeKind.TreeSmall),
                        $"seed {seed}: a selected pick must be a tree, never a planter");
                    kindsSeen.Add(pick.Kind);
                }
            }

            Assert.That(kindsSeen, Does.Contain(YardTreeKind.TreeLarge),
                "tree-large must still occur across many seeds");
            Assert.That(kindsSeen, Does.Contain(YardTreeKind.TreeSmall),
                "tree-small must still occur across many seeds");
        }

        [Test]
        public void Selection_IsDeterministic_ForTheSameLotAcrossRepeatedCalls()
        {
            foreach (var lot in NeighborhoodLayout.HouseLots)
            {
                var frontA = YardLandscaping.FrontTreesFor(lot);
                var frontB = YardLandscaping.FrontTreesFor(lot);
                var backA = YardLandscaping.BackTreesFor(lot);
                var backB = YardLandscaping.BackTreesFor(lot);

                Assert.That(Placements(frontA), Is.EqualTo(Placements(frontB)),
                    $"lot {lot.HouseId}: front selection must be stable across repeated calls (same seed)");
                Assert.That(Placements(backA), Is.EqualTo(Placements(backB)),
                    $"lot {lot.HouseId}: back selection must be stable across repeated calls (same seed)");
            }
        }

        [Test]
        public void SelectedTrees_ForRealLots_NeverOverlapEachOtherHouseWalkwayOrFence()
        {
            foreach (var lot in NeighborhoodLayout.HouseLots)
            {
                var front = YardLandscaping.FrontTreesFor(lot);
                var back = YardLandscaping.BackTreesFor(lot);

                Assert.That(front.Count, Is.InRange(1, 2), $"lot {lot.HouseId}: front tree count");
                Assert.That(back.Count, Is.InRange(3, 5), $"lot {lot.HouseId}: back tree count");

                var all = front.Select(p => p.Position).Concat(back.Select(p => p.Position)).ToList();
                AssertMutuallySpaced(all, lot.HouseId, "combined");

                var footprint = HouseFootprintOf(lot);
                NeighborhoodLayout.WalkNetwork.TryGetFrontWalkway(lot.HouseId, out var walkway);
                var fenceRuns = LotFence.GeometryFor(lot);

                foreach (var position in all)
                {
                    Assert.That(DistanceToRect(position, footprint),
                        Is.GreaterThanOrEqualTo(YardLandscaping.TreeFootprintRadius),
                        $"lot {lot.HouseId}: {position} must clear the house");

                    Assert.That(DistanceToSegment(position, walkway.A, walkway.B),
                        Is.GreaterThanOrEqualTo(YardLandscaping.TreeFootprintRadius + walkway.Width / 2f),
                        $"lot {lot.HouseId}: {position} must clear the front walkway");

                    foreach (var run in fenceRuns)
                    {
                        Assert.That(DistanceToSegment(position, run.A, run.B),
                            Is.GreaterThanOrEqualTo(YardLandscaping.TreeFootprintRadius),
                            $"lot {lot.HouseId}: {position} must clear the fence line");
                    }
                }
            }
        }

        [Test]
        public void Candidates_ForACulDeSacKeptQuadrantLot_NeverLandInThatTilesRoadStrip()
        {
            // #455: for a lot on a non-origin cul-de-sac tile, the yard road
            // clip used to see only NeighborhoodLayout.Roads (the origin
            // FourWay's streets), so candidates freely landed inside the
            // cul-de-sac's OWN paved road strip. The tile-aware resolvers thread
            // that tile's road in, so no candidate's footprint reaches it.
            const TileType type = TileType.CulDeSacSouth;
            var coordinate = new TileCoordinate(0, 1);
            var roadStrips = TileRoadGeometry.SegmentsFor(coordinate, type)
                .Select(RoadStrip).ToList();
            Assert.That(roadStrips, Is.Not.Empty, "a cul-de-sac tile has a road arm");

            foreach (var lot in ZoneCatalog.FirstZone.Lots)
            {
                var candidates = YardLandscaping.FrontCandidatesFor(lot, type)
                    .Concat(YardLandscaping.BackCandidatesFor(lot, type))
                    .ToList();
                Assert.That(candidates, Is.Not.Empty, $"zone lot {lot.HouseId}: yard fits some candidates");

                foreach (var candidate in candidates)
                {
                    foreach (var strip in roadStrips)
                    {
                        Assert.That(DistanceToRect(candidate.Position, strip),
                            Is.GreaterThanOrEqualTo(YardLandscaping.TreeFootprintRadius),
                            $"zone lot {lot.HouseId}: candidate {candidate.Position} must clear the "
                            + "cul-de-sac's paved road strip with its full footprint");
                    }
                }
            }
        }

        [Test]
        public void Trees_ForStartingFourWayLots_AreByteIdenticalThroughTheTileAwareOverload()
        {
            // #455 regression: a starting FourWay lot's selection must be
            // unchanged when routed through the tile-aware overload — same seed,
            // same yard regions (its tile arms are coincident with the origin
            // roads), so the exact same picks.
            foreach (var lot in NeighborhoodLayout.HouseLots)
            {
                Assert.That(Placements(YardLandscaping.FrontTreesFor(lot, TileType.FourWay)),
                    Is.EqualTo(Placements(YardLandscaping.FrontTreesFor(lot))),
                    $"lot {lot.HouseId}: front selection unchanged through the tile-aware overload");
                Assert.That(Placements(YardLandscaping.BackTreesFor(lot, TileType.FourWay)),
                    Is.EqualTo(Placements(YardLandscaping.BackTreesFor(lot))),
                    $"lot {lot.HouseId}: back selection unchanged through the tile-aware overload");
            }
        }

        [Test]
        public void AllYardCandidatesForRealLots_ClearTheMaxAcrossLadderFootprint_NotJustLevelOne()
        {
            // #459: the two candidate generators are wired to the house's
            // max-across-upgrade-ladder footprint (HousePlacement.MaxHouseFootprint)
            // rather than its level-1 footprint, so a tree seeded while the house
            // is small can't end up inside the larger upgraded mesh later. Every
            // front and back candidate of every real lot must clear that max rect
            // by a full tree footprint radius. (Before the wiring, candidates only
            // cleared the level-1 footprint, so some landed inside the max rect.)
            foreach (var lot in NeighborhoodLayout.HouseLots)
            {
                var maxFootprint = HousePlacement.MaxHouseFootprint(lot);

                var candidates = YardLandscaping.FrontCandidatesFor(lot)
                    .Concat(YardLandscaping.BackCandidatesFor(lot));

                foreach (var candidate in candidates)
                {
                    Assert.That(DistanceToRect(candidate.Position, maxFootprint),
                        Is.GreaterThanOrEqualTo(YardLandscaping.TreeFootprintRadius),
                        $"lot {lot.HouseId}: candidate {candidate.Position} must clear the "
                        + "max-across-ladder house footprint, not just the level-1 footprint");
                }
            }
        }

        [Test]
        public void GenerateCandidates_RejectAPointInsideTheMaxFootprint_ThatWasClearOfTheLevelOneFootprint()
        {
            // #459 (mechanism, deterministic): a candidate point that sits clear
            // of a small (level-1) footprint but inside the larger (max) footprint
            // is accepted when the generator is handed the level-1 rect and
            // rejected when handed the max rect. The whole yard region lies in the
            // gap band between the two rects, so the generator fills it against the
            // level-1 rect but returns nothing against the max rect.
            var levelOne = new LotRect(-10f, 0f, -5f, 5f);
            var max = new LotRect(-10f, 10f, -5f, 5f);
            var gapBand = new LotRect(1f, 9f, -5f, 5f); // clear of levelOne, inside max

            var againstLevelOne = YardLandscaping.GenerateFrontCandidates(gapBand, levelOne, walkway: null, seed: 7);
            var againstMax = YardLandscaping.GenerateFrontCandidates(gapBand, max, walkway: null, seed: 7);

            Assert.That(againstLevelOne, Is.Not.Empty,
                "a point in the gap band is clear of the level-1 footprint, so it is accepted");
            Assert.That(againstMax, Is.Empty,
                "the same point is inside the max footprint, so it is rejected once wired to it");
        }

        [Test]
        public void Candidates_DegradeToEmpty_WhenTheMaxFootprintSwallowsTheYardRegion_RatherThanThrowing()
        {
            // #459: if a house's max-across-ladder footprint is large enough to
            // cover the whole front/back yard region, generation must degrade to
            // an empty candidate list (every sample rejected) rather than throw —
            // the same graceful degradation as the existing "region too small"
            // path. SelectFront/SelectBack then simply yield nothing.
            var region = new LotRect(-2f, 2f, -2f, 2f);
            var swallowingFootprint = new LotRect(-10f, 10f, -10f, 10f);

            IReadOnlyList<YardTreeCandidate> front = null;
            IReadOnlyList<YardTreeCandidate> back = null;
            Assert.That(() => front = YardLandscaping.GenerateFrontCandidates(
                region, swallowingFootprint, walkway: null, seed: 3), Throws.Nothing);
            Assert.That(() => back = YardLandscaping.GenerateBackCandidates(
                region, swallowingFootprint, Array.Empty<FenceRun>(), seed: 3), Throws.Nothing);

            Assert.That(front, Is.Empty, "a swallowed front yard yields no candidates");
            Assert.That(back, Is.Empty, "a swallowed back yard yields no candidates");
            Assert.That(YardLandscaping.SelectFront(front, seed: 3), Is.Empty);
            Assert.That(YardLandscaping.SelectBack(back, seed: 3), Is.Empty);
        }

        // ---- #461: zone-lot yard trees orient to the REAL walk-network facing ----

        [Test]
        public void NetworkAwareCandidates_ForAZoneLot_AreOrientedToItsRealFacing_NotTheZSignFallback()
        {
            // #461: a zone lot's trees were pre-baked with the Z-sign fallback
            // facing, so they didn't rotate with the house. The network-aware
            // FrontCandidatesFor/BackCandidatesFor resolve the lot's REAL facing
            // (via the map-spanning network) so candidates land in the correctly
            // oriented yard regions.
            const TileType type = TileType.CulDeSacSouth;
            var state = UnlockedFirstZone();
            var network = state.WalkNetwork;
            var lot = ZoneCatalog.FirstZone.Lots[0];

            // The network-oriented yard regions differ from the Z-sign ones —
            // the house faces along X here, not toward -Z.
            var netFrontYard = LotBounds.FrontYard(lot, type, network);
            var netBackYard = LotBounds.BackYard(lot, type, network);
            var zFrontYard = LotBounds.FrontYard(lot, type);
            Assert.That(YardsDiffer(netFrontYard, zFrontYard), Is.True,
                "the network front yard must be oriented differently from the Z-sign front yard");

            var front = YardLandscaping.FrontCandidatesFor(lot, type, network);
            var back = YardLandscaping.BackCandidatesFor(lot, type, network);
            Assert.That(back, Is.Not.Empty, "the zone lot's back yard fits candidates in its real orientation");

            foreach (var candidate in front)
            {
                Assert.That(netFrontYard.Contains(candidate.Position), Is.True,
                    $"front candidate {candidate.Position} must sit in the network-oriented front yard");
            }

            foreach (var candidate in back)
            {
                Assert.That(netBackYard.Contains(candidate.Position), Is.True,
                    $"back candidate {candidate.Position} must sit in the network-oriented back yard");
            }
        }

        [Test]
        public void NetworkAwareYardRegions_ArePredeterminedAtUnlock_AndUnchangedOnceTheHouseIsBuilt()
        {
            // #461 (protects #434): trees are pre-baked at unlock and NEVER
            // regenerated on build, so the predetermined yard regions resolved at
            // unlock (no walkway yet, facing projected from the nearest sidewalk)
            // must be byte-identical to the regions once the house — and its real
            // walkway — exists. Otherwise the pre-baked trees would disagree with
            // the built house's orientation.
            const TileType type = TileType.CulDeSacSouth;
            var state = UnlockedFirstZone();
            var lot = ZoneCatalog.FirstZone.Lots[0];

            var frontAtUnlock = LotBounds.FrontYard(lot, type, state.WalkNetwork);
            var backAtUnlock = LotBounds.BackYard(lot, type, state.WalkNetwork);

            Assert.That(state.TryBuildHouse(lot.HouseId), Is.True, "the zone house builds");

            var frontBuilt = LotBounds.FrontYard(lot, type, state.WalkNetwork);
            var backBuilt = LotBounds.BackYard(lot, type, state.WalkNetwork);

            Assert.That(SameRect(frontAtUnlock, frontBuilt), Is.True,
                "the predetermined front yard must equal the built front yard");
            Assert.That(SameRect(backAtUnlock, backBuilt), Is.True,
                "the predetermined back yard must equal the built back yard");
        }

        private static GameState UnlockedFirstZone()
        {
            var state = GameState.CreateNew();
            state.Wallet.Deposit(1_000_000);
            Assert.That(state.TryUnlockNextZone(), Is.True, "the first zone unlocks");
            return state;
        }

        private static bool SameRect(LotRect a, LotRect b)
        {
            return a.MinX == b.MinX && a.MaxX == b.MaxX && a.MinZ == b.MinZ && a.MaxZ == b.MaxZ;
        }

        private static bool YardsDiffer(LotRect a, LotRect b)
        {
            return !SameRect(a, b);
        }

        private static LotRect RoadStrip(TileRoadSegment segment)
        {
            var halfWidth = segment.Width / 2f;
            var halfLength = segment.Length / 2f;
            return segment.Orientation == StreetOrientation.NorthSouth
                ? new LotRect(
                    segment.Center.X - halfWidth, segment.Center.X + halfWidth,
                    segment.Center.Z - halfLength, segment.Center.Z + halfLength)
                : new LotRect(
                    segment.Center.X - halfLength, segment.Center.X + halfLength,
                    segment.Center.Z - halfWidth, segment.Center.Z + halfWidth);
        }

        private static List<YardTreeCandidate> FarApartCandidates(int count)
        {
            var list = new List<YardTreeCandidate>();
            for (var i = 0; i < count; i++)
            {
                list.Add(new YardTreeCandidate(new GridPoint(i * (YardLandscaping.MinSpacing * 2f), 0f)));
            }

            return list;
        }

        private static List<(GridPoint Position, YardTreeKind Kind, float Scale)> Placements(IReadOnlyList<YardTreePlacement> placements)
        {
            return placements.Select(p => (p.Position, p.Kind, p.Scale)).ToList();
        }

        private static void AssertMutuallySpaced(IReadOnlyList<GridPoint> points, int houseId, string label)
        {
            for (var i = 0; i < points.Count; i++)
            {
                for (var j = i + 1; j < points.Count; j++)
                {
                    var distance = Distance(points[i], points[j]);
                    Assert.That(distance, Is.GreaterThanOrEqualTo(YardLandscaping.MinSpacing),
                        $"lot {houseId} ({label}): {points[i]} and {points[j]} must be spaced at least "
                        + $"{YardLandscaping.MinSpacing}m apart");
                }
            }
        }

        private static LotRect HouseFootprintOf(HouseLot lot)
        {
            var facing = HousePlacement.FrontFacing(lot);
            var house = HousePlacement.Position(lot, HousePlacement.KitScale);
            var model = HouseModelCatalog.ForHouse(lot.HouseId);
            var halfWidth = HousePlacement.KitScale * model.FootprintX / 2f;
            var halfDepth = HousePlacement.KitScale * model.FootprintZ / 2f;

            return facing.X != 0f
                ? new LotRect(house.X - halfDepth, house.X + halfDepth, house.Z - halfWidth, house.Z + halfWidth)
                : new LotRect(house.X - halfWidth, house.X + halfWidth, house.Z - halfDepth, house.Z + halfDepth);
        }

        private static float DistanceToRect(GridPoint p, LotRect rect)
        {
            var dx = Math.Max(rect.MinX - p.X, Math.Max(0f, p.X - rect.MaxX));
            var dz = Math.Max(rect.MinZ - p.Z, Math.Max(0f, p.Z - rect.MaxZ));
            return (float)Math.Sqrt(dx * dx + dz * dz);
        }

        private static float DistanceToSegment(GridPoint p, GridPoint a, GridPoint b)
        {
            var abx = b.X - a.X;
            var abz = b.Z - a.Z;
            var lengthSquared = abx * abx + abz * abz;
            if (lengthSquared < 0.0001f)
            {
                return Distance(p, a);
            }

            var t = ((p.X - a.X) * abx + (p.Z - a.Z) * abz) / lengthSquared;
            t = Math.Max(0f, Math.Min(1f, t));
            var closest = new GridPoint(a.X + t * abx, a.Z + t * abz);
            return Distance(p, closest);
        }

        private static float Distance(GridPoint a, GridPoint b)
        {
            var dx = a.X - b.X;
            var dz = a.Z - b.Z;
            return (float)Math.Sqrt(dx * dx + dz * dz);
        }
    }
}
