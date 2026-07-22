using System;
using System.Collections.Generic;
using System.Linq;

namespace Doggiehood.Core.World
{
    /// <summary>One candidate point for a yard landscaping pick (#170):
    /// collision-clear of the house footprint, the front walkway or
    /// backyard fence line (whichever applies to its yard), and every
    /// other candidate in the same batch.</summary>
    public readonly struct YardTreeCandidate
    {
        public GridPoint Position { get; }

        public YardTreeCandidate(GridPoint position)
        {
            Position = position;
        }
    }

    /// <summary>One SELECTED yard landscaping pick (#170): a candidate
    /// position plus the kit model it renders as.</summary>
    public readonly struct YardTreePlacement
    {
        public GridPoint Position { get; }
        public YardTreeKind Kind { get; }

        public YardTreePlacement(GridPoint position, YardTreeKind kind)
        {
            Position = position;
            Kind = kind;
        }
    }

    /// <summary>
    /// Procedural yard landscaping (#170): per property, candidate points
    /// are generated inside the front/back yard regions (#222's
    /// <see cref="LotBounds.FrontYard"/>/<see cref="LotBounds.BackYard"/>),
    /// collision-aware against the house footprint, the front walkway
    /// (#128) or backyard fence line (#146, <see cref="LotFence"/>), and
    /// each other — then a subset is selected, seeded deterministically
    /// per lot so the same lot always lands on the same trees across
    /// sessions and builds. No per-lot manual authoring: everything here
    /// is a pure function of the lot's own geometry and
    /// <see cref="HouseLot.HouseId"/>.
    ///
    /// Two-tier design (matching <see cref="LotFence"/>): the
    /// <c>Generate*</c>/<c>Select*</c> overloads taking explicit geometry
    /// are pure and independently testable; the <c>*For(HouseLot)</c>
    /// overloads resolve that geometry from
    /// <see cref="NeighborhoodLayout"/> and derive the per-lot seed.
    /// </summary>
    public static class YardLandscaping
    {
        /// <summary>Candidate points generated in the front yard.
        /// Decision (#170, Derek, 2026-07-20): 4.</summary>
        public const int FrontCandidateCount = 4;

        /// <summary>Candidate points generated in the back yard.
        /// Decision (#170, Derek, 2026-07-20): 11.</summary>
        public const int BackCandidateCount = 11;

        /// <summary>Minimum front trees actually shown.</summary>
        public const int FrontSelectMin = 1;

        /// <summary>Maximum front trees actually shown ("occasionally 2").</summary>
        public const int FrontSelectMax = 2;

        /// <summary>Minimum back trees actually shown.</summary>
        public const int BackSelectMin = 3;

        /// <summary>Maximum back trees actually shown.</summary>
        public const int BackSelectMax = 5;

        /// <summary>Chance a front yard shows its occasional second tree
        /// rather than the common single tree. Decision (#170): a plain
        /// minority-of-the-time reading of "1 most of the time, 2
        /// occasionally" — tuned visually like the rest of this kit's
        /// first-pass constants (e.g. FenceTiling.Scale).</summary>
        public const float TwoFrontTreesProbability = 0.25f;

        /// <summary>Model-local half-extent (X axis) of tree-large.fbx /
        /// tree-small.fbx, parsed from the kit FBX geometry (raw units /
        /// 100 — the same cm-to-m convention HouseModelCatalog's
        /// footprints and FenceTiling/WalkwayTiling's piece sizes use;
        /// cross-checked against FenceTiling.PieceModelLength, which the
        /// same conversion reproduces exactly).</summary>
        public const float TreeHalfExtentX = 0.1052f;

        /// <summary>Model-local half-extent (Z axis) of tree-large.fbx /
        /// tree-small.fbx — both trees share the same canopy footprint,
        /// only their height differs.</summary>
        public const float TreeHalfExtentZ = 0.1215f;

        /// <summary>Model-local half-extent (X axis) of planter.fbx — the
        /// widest of the three kit pieces.</summary>
        public const float PlanterHalfExtentX = 0.2000f;

        /// <summary>Model-local half-extent (Z axis) of planter.fbx.</summary>
        public const float PlanterHalfExtentZ = 0.1506f;

        /// <summary>
        /// Uniform scale applied to every yard landscaping kit piece.
        /// Decision (#170): reuses <see cref="FenceTiling.Scale"/> (×5) —
        /// the fence pieces the trees stand alongside in the same
        /// backyard space are already tuned to that scale, so matching it
        /// keeps yard props visually consistent; Derek tunes it further
        /// in the Editor check, same as the fence scale itself.
        /// </summary>
        public const float UniformScale = FenceTiling.Scale;

        /// <summary>
        /// The single collision radius used for every candidate and pick,
        /// regardless of which of the three kit models ends up there:
        /// the largest model-local half-extent among tree-large.fbx/
        /// tree-small.fbx/planter.fbx (planter.fbx's X half-extent) at
        /// <see cref="UniformScale"/>. Conservative by construction — a
        /// spacing derived from the largest piece can never let a smaller
        /// piece overlap either.
        /// </summary>
        public const float TreeFootprintRadius = PlanterHalfExtentX * UniformScale;

        /// <summary>Minimum center-to-center distance between two placed
        /// yard props: two footprint radii.</summary>
        public const float MinSpacing = TreeFootprintRadius * 2f;

        /// <summary>Rejection-sampling attempt budget per candidate batch —
        /// generous enough to reliably fill <see cref="BackCandidateCount"/>
        /// candidates spaced by <see cref="MinSpacing"/> inside a typical
        /// back yard region, small enough to keep generation instant.</summary>
        private const int MaxGenerationAttempts = 1000;

        private const int FrontCandidateSeedSalt = 0;
        private const int BackCandidateSeedSalt = 1;
        private const int FrontSelectionSeedSalt = 2;
        private const int BackSelectionSeedSalt = 3;

        private const float Epsilon = 0.0001f;

        private static readonly YardTreeKind[] Kinds =
        {
            YardTreeKind.TreeLarge, YardTreeKind.TreeSmall, YardTreeKind.Planter,
        };

        /// <summary>
        /// Up to <see cref="FrontCandidateCount"/> candidate points inside
        /// <paramref name="frontYard"/>, clear of <paramref name="houseFootprint"/>,
        /// clear of <paramref name="walkway"/> (its full paved width, not
        /// just its centerline) when present, and mutually spaced by
        /// <see cref="MinSpacing"/>. Pure — no NeighborhoodLayout lookups.
        /// </summary>
        public static IReadOnlyList<YardTreeCandidate> GenerateFrontCandidates(
            LotRect frontYard, LotRect houseFootprint, WalkEdge? walkway, int seed)
        {
            bool IsBlocked(GridPoint point)
            {
                if (DistanceToRect(point, houseFootprint) < TreeFootprintRadius)
                {
                    return true;
                }

                if (walkway.HasValue)
                {
                    var edge = walkway.Value;
                    if (DistanceToSegment(point, edge.A, edge.B) < TreeFootprintRadius + edge.Width / 2f)
                    {
                        return true;
                    }
                }

                return false;
            }

            return GenerateCandidates(frontYard, FrontCandidateCount, IsBlocked, new Random(seed));
        }

        /// <summary>
        /// Up to <see cref="BackCandidateCount"/> candidate points inside
        /// <paramref name="backYard"/>, clear of <paramref name="houseFootprint"/>,
        /// clear of every run in <paramref name="fenceRuns"/> (#146's
        /// backyard fence line — checked regardless of whether the fence
        /// is currently purchased/visible, same as <see cref="LotFence.GeometryFor"/>),
        /// and mutually spaced by <see cref="MinSpacing"/>. Pure — no
        /// NeighborhoodLayout lookups.
        /// </summary>
        public static IReadOnlyList<YardTreeCandidate> GenerateBackCandidates(
            LotRect backYard, LotRect houseFootprint, IReadOnlyList<FenceRun> fenceRuns, int seed)
        {
            bool IsBlocked(GridPoint point)
            {
                if (DistanceToRect(point, houseFootprint) < TreeFootprintRadius)
                {
                    return true;
                }

                foreach (var run in fenceRuns)
                {
                    if (DistanceToSegment(point, run.A, run.B) < TreeFootprintRadius)
                    {
                        return true;
                    }
                }

                return false;
            }

            return GenerateCandidates(backYard, BackCandidateCount, IsBlocked, new Random(seed));
        }

        /// <summary>
        /// <see cref="GenerateFrontCandidates(LotRect, LotRect, WalkEdge?, int)"/>
        /// for a real lot: resolves the front yard region, house
        /// footprint, and front walkway from <see cref="NeighborhoodLayout"/>,
        /// and derives the seed from the lot's own
        /// <see cref="HouseLot.HouseId"/> — deterministic and stable
        /// across sessions and builds.
        /// </summary>
        public static IReadOnlyList<YardTreeCandidate> FrontCandidatesFor(HouseLot lot)
        {
            var frontYard = LotBounds.FrontYard(lot);
            var footprint = HouseFootprintOf(lot);
            var walkway = NeighborhoodLayout.WalkNetwork.TryGetFrontWalkway(lot.HouseId, out var edge)
                ? edge
                : (WalkEdge?)null;

            return GenerateFrontCandidates(frontYard, footprint, walkway, SeedFor(lot, FrontCandidateSeedSalt));
        }

        /// <summary>
        /// <see cref="GenerateBackCandidates(LotRect, LotRect, IReadOnlyList{FenceRun}, int)"/>
        /// for a real lot: resolves the back yard region, house
        /// footprint, and fence line from <see cref="NeighborhoodLayout"/>/
        /// <see cref="LotFence"/>, and derives the seed from the lot's own
        /// <see cref="HouseLot.HouseId"/>.
        /// </summary>
        public static IReadOnlyList<YardTreeCandidate> BackCandidatesFor(HouseLot lot)
        {
            var backYard = LotBounds.BackYard(lot);
            var footprint = HouseFootprintOf(lot);
            var fenceRuns = LotFence.GeometryFor(lot);

            return GenerateBackCandidates(backYard, footprint, fenceRuns, SeedFor(lot, BackCandidateSeedSalt));
        }

        /// <summary>
        /// Picks <see cref="FrontSelectMin"/> trees most of the time,
        /// <see cref="FrontSelectMax"/> occasionally
        /// (<see cref="TwoFrontTreesProbability"/>) from
        /// <paramref name="candidates"/>, deterministically for
        /// <paramref name="seed"/>. Never picks more than
        /// <paramref name="candidates"/> has available (a yard whose
        /// generator was too obstructed to fill its full candidate count
        /// still yields a valid, if smaller, selection).
        /// </summary>
        public static IReadOnlyList<YardTreePlacement> SelectFront(
            IReadOnlyList<YardTreeCandidate> candidates, int seed)
        {
            var rng = new Random(seed);
            var desired = rng.NextDouble() < TwoFrontTreesProbability ? FrontSelectMax : FrontSelectMin;
            return Select(candidates, Math.Min(desired, candidates.Count), rng);
        }

        /// <summary>
        /// Picks a uniformly random count between <see cref="BackSelectMin"/>
        /// and <see cref="BackSelectMax"/> (inclusive) from
        /// <paramref name="candidates"/>, deterministically for
        /// <paramref name="seed"/>, capped by availability like
        /// <see cref="SelectFront"/>.
        /// </summary>
        public static IReadOnlyList<YardTreePlacement> SelectBack(
            IReadOnlyList<YardTreeCandidate> candidates, int seed)
        {
            var rng = new Random(seed);
            var desired = BackSelectMin + rng.Next(BackSelectMax - BackSelectMin + 1);
            return Select(candidates, Math.Min(desired, candidates.Count), rng);
        }

        /// <summary>The lot's selected front trees: candidates plus
        /// selection, both seeded from the lot's own HouseId.</summary>
        public static IReadOnlyList<YardTreePlacement> FrontTreesFor(HouseLot lot)
        {
            return SelectFront(FrontCandidatesFor(lot), SeedFor(lot, FrontSelectionSeedSalt));
        }

        /// <summary>The lot's selected back trees: candidates plus
        /// selection, both seeded from the lot's own HouseId.</summary>
        public static IReadOnlyList<YardTreePlacement> BackTreesFor(HouseLot lot)
        {
            return SelectBack(BackCandidatesFor(lot), SeedFor(lot, BackSelectionSeedSalt));
        }

        private static List<YardTreeCandidate> GenerateCandidates(
            LotRect region, int maxCount, Func<GridPoint, bool> isBlocked, Random rng)
        {
            var candidates = new List<YardTreeCandidate>();

            var insetMinX = region.MinX + TreeFootprintRadius;
            var insetMaxX = region.MaxX - TreeFootprintRadius;
            var insetMinZ = region.MinZ + TreeFootprintRadius;
            var insetMaxZ = region.MaxZ - TreeFootprintRadius;
            if (insetMinX > insetMaxX || insetMinZ > insetMaxZ)
            {
                // The region is too small to fit even one tree's footprint
                // — no candidates rather than one that spills outside it.
                return candidates;
            }

            var attempts = 0;
            while (candidates.Count < maxCount && attempts < MaxGenerationAttempts)
            {
                attempts++;

                var point = new GridPoint(
                    Lerp(insetMinX, insetMaxX, (float)rng.NextDouble()),
                    Lerp(insetMinZ, insetMaxZ, (float)rng.NextDouble()));

                if (isBlocked(point))
                {
                    continue;
                }

                if (candidates.Any(c => Distance(c.Position, point) < MinSpacing))
                {
                    continue;
                }

                candidates.Add(new YardTreeCandidate(point));
            }

            return candidates;
        }

        private static List<YardTreePlacement> Select(
            IReadOnlyList<YardTreeCandidate> candidates, int count, Random rng)
        {
            var pool = candidates.ToList();
            var picks = new List<YardTreePlacement>(count);

            for (var i = 0; i < count && pool.Count > 0; i++)
            {
                var index = rng.Next(pool.Count);
                picks.Add(new YardTreePlacement(pool[index].Position, Kinds[rng.Next(Kinds.Length)]));
                pool.RemoveAt(index);
            }

            return picks;
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

        private static int SeedFor(HouseLot lot, int salt)
        {
            return unchecked(lot.HouseId * 397 + salt);
        }

        private static float Lerp(float min, float max, float t)
        {
            return min + t * (max - min);
        }

        private static float DistanceToRect(GridPoint point, LotRect rect)
        {
            var dx = Math.Max(rect.MinX - point.X, Math.Max(0f, point.X - rect.MaxX));
            var dz = Math.Max(rect.MinZ - point.Z, Math.Max(0f, point.Z - rect.MaxZ));
            return (float)Math.Sqrt(dx * dx + dz * dz);
        }

        private static float DistanceToSegment(GridPoint point, GridPoint a, GridPoint b)
        {
            var abx = b.X - a.X;
            var abz = b.Z - a.Z;
            var lengthSquared = abx * abx + abz * abz;
            if (lengthSquared < Epsilon)
            {
                return Distance(point, a);
            }

            var t = ((point.X - a.X) * abx + (point.Z - a.Z) * abz) / lengthSquared;
            t = Math.Max(0f, Math.Min(1f, t));
            var closest = new GridPoint(a.X + t * abx, a.Z + t * abz);
            return Distance(point, closest);
        }

        private static float Distance(GridPoint a, GridPoint b)
        {
            var dx = a.X - b.X;
            var dz = a.Z - b.Z;
            return (float)Math.Sqrt(dx * dx + dz * dz);
        }
    }
}
