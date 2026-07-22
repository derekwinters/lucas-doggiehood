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
    /// by a tree footprint radius derived from the kit mesh bounds
    /// (tree-large.fbx/tree-small.fbx/planter.fbx). Selection is seeded
    /// deterministically per lot.
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

        private static List<YardTreeCandidate> FarApartCandidates(int count)
        {
            var list = new List<YardTreeCandidate>();
            for (var i = 0; i < count; i++)
            {
                list.Add(new YardTreeCandidate(new GridPoint(i * (YardLandscaping.MinSpacing * 2f), 0f)));
            }

            return list;
        }

        private static List<(GridPoint Position, YardTreeKind Kind)> Placements(IReadOnlyList<YardTreePlacement> placements)
        {
            return placements.Select(p => (p.Position, p.Kind)).ToList();
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
