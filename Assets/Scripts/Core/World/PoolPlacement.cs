using System;
using System.Collections.Generic;

namespace Doggiehood.Core.World
{
    /// <summary>
    /// One thing already standing in a back yard that a pool has to stay off
    /// (#740): a position and the collision radius reserved around it. Lets
    /// the pure placement function take yard trees
    /// (<see cref="YardLandscaping.TreeFootprintRadius"/>) and yard
    /// decorations (<see cref="PoolPlacement.DecorationFootprintRadius"/>)
    /// through one obstacle list instead of one parameter per kind.
    /// </summary>
    public readonly struct YardObstacle
    {
        public GridPoint Position { get; }

        /// <summary>Radius reserved around <see cref="Position"/>. A pool
        /// centre must stay at least this plus the pool's own footprint
        /// radius away.</summary>
        public float Radius { get; }

        public YardObstacle(GridPoint position, float radius)
        {
            if (radius < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(radius), "An obstacle radius is never negative.");
            }

            Position = position;
            Radius = radius;
        }
    }

    /// <summary>
    /// The graybox backyard pool a delivered "pool" gift leaves in a dog's
    /// yard (#740): its dimensions and where it goes.
    ///
    /// Dimensions are derived from the RENDERED ADULT DOG — the shared Cube
    /// Pets model at <c>DogView</c>'s adult scale — rather than hand-picked
    /// meters, so retuning the dog scale retunes the pool with it (Derek's
    /// direction: "a cylinder about the height of a dog, about the width of
    /// two dogs, have a gray outer surface, and blue interior that is
    /// slightly lower than the rest of the cylinder").
    ///
    /// Placement follows the <see cref="YardLandscaping"/> pattern exactly,
    /// including its two-tier split: <see cref="TryFindPosition"/> is pure
    /// (explicit region + obstacles + seed, no NeighborhoodLayout lookups)
    /// and <see cref="TryPositionFor"/> resolves that geometry for a real lot
    /// and derives the seed from <see cref="HouseLot.HouseId"/>, so a lot's
    /// pool lands in the same spot every session (the #719 failure mode).
    /// </summary>
    public static class PoolPlacement
    {
        // ---------------------------------------------------------------
        // Sizing — derived from the rendered adult dog, never hand-picked.
        // ---------------------------------------------------------------

        /// <summary>Model-local Y offset of the Cube Pets dog's <c>body</c>
        /// node (its <c>Lcl Translation</c>, raw units / 100 — the same
        /// cm-to-m convention <see cref="YardLandscaping.TreeHalfExtentX"/>
        /// parses the tree kit FBX with). The body sits this far above the
        /// ground its legs stand on.</summary>
        public const float DogModelBodyOffsetY = 0.181250f;

        /// <summary>Model-local top of the Cube Pets dog's body mesh — the
        /// tip of its ears — parsed from animal-dog.fbx's vertex data the
        /// same way (raw 140.318668 / 100).</summary>
        public const float DogModelBodyTopY = 1.403187f;

        /// <summary>Standing height of the assembled Cube Pets dog model at
        /// unit scale: from the ground its legs stand on to the top of its
        /// ears.</summary>
        public const float DogModelHeight = DogModelBodyOffsetY + DogModelBodyTopY;

        /// <summary>Model-local half-extent (X axis) of the Cube Pets dog's
        /// body mesh — the larger of the two sides (raw 63.5 / 100), the same
        /// max-abs half-extent convention <see cref="YardLandscaping"/> uses
        /// for the tree meshes.</summary>
        public const float DogModelHalfWidthX = 0.635f;

        /// <summary>Side-to-side body width of the Cube Pets dog model at
        /// unit scale.</summary>
        public const float DogModelWidth = DogModelHalfWidthX * 2f;

        /// <summary>The scale <c>DogView</c> renders an ADULT dog at
        /// (<c>dog.IsPuppy ? 0.55f : 1f</c>). Named here so the pool's
        /// dimensions track the dog the player actually sees rather than the
        /// raw model.</summary>
        public const float AdultDogScale = 1f;

        /// <summary>Standing height of a rendered adult dog.</summary>
        public const float AdultDogHeight = DogModelHeight * AdultDogScale;

        /// <summary>Body width of a rendered adult dog.</summary>
        public const float AdultDogWidth = DogModelWidth * AdultDogScale;

        /// <summary>How many adult dogs wide the pool is (Derek: "about the
        /// width of two dogs"). Named rather than a bare 2 (#161).</summary>
        public const int PoolWidthInDogs = 2;

        /// <summary>Height of the outer shell: about one adult dog tall.</summary>
        public const float PoolHeight = AdultDogHeight;

        /// <summary>Outer diameter of the shell: about two adult dogs
        /// wide.</summary>
        public const float PoolOuterDiameter = AdultDogWidth * PoolWidthInDogs;

        /// <summary>The pool's collision radius — half its outer diameter.
        /// Nothing may overlap this, ever (see the yard-item invariant in
        /// docs/specs/world/world.md).</summary>
        public const float PoolFootprintRadius = PoolOuterDiameter / 2f;

        /// <summary>Shell wall thickness as a fraction of the outer diameter:
        /// thick enough to read as a rim from the fixed 45-degree camera,
        /// thin enough that the blue interior dominates. A first-pass graybox
        /// figure tuned visually, like the rest of this kit's constants
        /// (e.g. <see cref="FenceTiling.Scale"/>).</summary>
        public const float PoolWallThicknessFraction = 0.08f;

        /// <summary>Shell wall thickness in meters.</summary>
        public const float PoolWallThickness = PoolOuterDiameter * PoolWallThicknessFraction;

        /// <summary>Diameter of the blue interior — inset within the shell by
        /// one wall thickness on each side.</summary>
        public const float PoolInnerDiameter = PoolOuterDiameter - 2f * PoolWallThickness;

        /// <summary>How far below the shell rim the water surface sits, as a
        /// fraction of the pool's height ("slightly lower" — a lip, not a
        /// deep well). First-pass graybox figure, tuned visually.</summary>
        public const float PoolWaterDropFraction = 0.15f;

        /// <summary>How far below the shell rim the water surface sits, in
        /// meters.</summary>
        public const float PoolWaterDropBelowRim = PoolHeight * PoolWaterDropFraction;

        /// <summary>Height of the blue interior above the ground — the shell
        /// height less the rim drop.</summary>
        public const float PoolWaterSurfaceHeight = PoolHeight - PoolWaterDropBelowRim;

        // ---------------------------------------------------------------
        // Placement.
        // ---------------------------------------------------------------

        /// <summary>How many adult-dog body widths of breathing room the pool
        /// keeps around its own footprint: one, so a dog can walk between the
        /// pool and whatever stands next to it and the pool never reads as
        /// jammed against a tree or the fence. Derived from the dog like every
        /// other figure here, in the spirit of
        /// <see cref="YardLandscaping.MinSpacing"/>.</summary>
        public const float ComfortMarginInDogWidths = 1f;

        /// <summary>The comfort margin in meters. Unlike
        /// <see cref="PoolFootprintRadius"/> this is a SOFT constraint — the
        /// only thing the fallback below is allowed to give up.</summary>
        public const float ComfortMargin = AdultDogWidth * ComfortMarginInDogWidths;

        /// <summary>Collision radius reserved around an existing yard
        /// <see cref="Decorations.Decoration"/>: half the diagonal of the
        /// graybox decoration's 1.4 x 1.0 m ground footprint
        /// (<c>DecorationView</c>), so the pool clears a decoration whichever
        /// way it faces. Today's decorations are not collision-checked against
        /// anything themselves, but a placed yard item never overlaps another
        /// placed yard item, so the pool checks them.</summary>
        public const float DecorationFootprintRadius = 0.8602f;

        /// <summary>Rejection-sampling attempt budget per relaxation pass —
        /// the same generous-but-instant budget
        /// <see cref="YardLandscaping"/> samples yard trees with.</summary>
        private const int MaxAttemptsPerPass = 1000;

        /// <summary>The fallback when no legal spot fits (#740): the COMFORT
        /// MARGIN relaxes, in these fractions, and the search retries. The
        /// hard no-overlap distance (<see cref="PoolFootprintRadius"/>) is
        /// never relaxed, and the last pass is a full 0 — so if even the
        /// bare-footprint pass finds nothing, there is no legal spot and
        /// <see cref="TryFindPosition"/> reports no placement rather than
        /// returning an overlapping point.</summary>
        private static readonly float[] MarginRelaxationFractions = { 1f, 0.5f, 0f };

        /// <summary>Salt mixed into the per-lot seed, so a pool draws its own
        /// stream rather than replaying one of <see cref="YardLandscaping"/>'s
        /// (which salts 0-3). Same shape as that class's seed derivation, so
        /// the pool is as deterministic per lot as the trees are.</summary>
        private const int PositionSeedSalt = 5;

        /// <summary>
        /// One pool position inside <paramref name="backYard"/>, keeping the
        /// pool's whole footprint clear of <paramref name="houseFootprint"/>,
        /// every run in <paramref name="fenceRuns"/> (checked regardless of
        /// whether the fence is currently purchased/visible, exactly as yard
        /// trees do) and every obstacle in <paramref name="obstacles"/>. Pure
        /// — no NeighborhoodLayout lookups, so the same region + seed always
        /// gives the same point.
        ///
        /// Returns false — with <paramref name="position"/> left at its
        /// default — when no legal spot exists even after
        /// <see cref="MarginRelaxationFractions"/> has relaxed the comfort
        /// margin all the way to zero. Rendering nothing is the correct
        /// outcome there; overlapping something is not.
        /// </summary>
        public static bool TryFindPosition(
            LotRect backYard,
            LotRect houseFootprint,
            IReadOnlyList<FenceRun> fenceRuns,
            IReadOnlyList<YardObstacle> obstacles,
            int seed,
            out GridPoint position)
        {
            if (fenceRuns == null)
            {
                throw new ArgumentNullException(nameof(fenceRuns));
            }

            if (obstacles == null)
            {
                throw new ArgumentNullException(nameof(obstacles));
            }

            foreach (var fraction in MarginRelaxationFractions)
            {
                var margin = ComfortMargin * fraction;
                if (TrySample(backYard, houseFootprint, fenceRuns, obstacles, margin, seed, out position))
                {
                    return true;
                }
            }

            position = default;
            return false;
        }

        /// <summary>
        /// <see cref="TryFindPosition"/> for a real lot: resolves the back
        /// yard region, the house obstacle and the fence line the same way
        /// <see cref="YardLandscaping"/> does, collects the lot's own back
        /// trees and yard decorations as obstacles, and derives the seed from
        /// the lot's own <see cref="HouseLot.HouseId"/> — so a lot's pool
        /// lands in the same spot on every launch and never jumps between
        /// sessions (#719).
        ///
        /// The house obstacle is the MAX-across-upgrade-ladder footprint
        /// (<see cref="HousePlacement.MaxHouseFootprint"/>), not the as-built
        /// level-1 one: a later house upgrade grows the mesh from the same
        /// fixed centre, and reserving only today's footprint would let an
        /// upgraded house grow into the pool (#459).
        /// </summary>
        public static bool TryPositionFor(HouseLot lot, GameState state, out GridPoint position)
        {
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            return TryFindPosition(
                LotBounds.BackYard(lot),
                HousePlacement.MaxHouseFootprint(lot),
                LotFence.GeometryFor(lot),
                ObstaclesFor(lot, state),
                SeedFor(lot),
                out position);
        }

        /// <summary>
        /// #740: whether a house's yard shows a pool — true exactly when a
        /// completed pool gift recorded a
        /// <see cref="Economy.ItemCatalog.PoolItemName"/>
        /// <see cref="PlacedItem"/> for it. The same persisted-item mechanism
        /// the purchased fence uses (<see cref="LotFence.IsFenced"/>), so a
        /// delivered pool survives save/load and reappears on the next world
        /// build with no new save state.
        /// </summary>
        public static bool HasPool(int houseId, GameState state)
        {
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            foreach (var item in state.PlacedItems)
            {
                if (item.HouseId == houseId && item.ItemName == Economy.ItemCatalog.PoolItemName)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>Everything already standing in this lot's back yard that
        /// the pool has to stay off: its selected back-yard trees (#170) and
        /// its own house's yard decorations (#48).</summary>
        private static IReadOnlyList<YardObstacle> ObstaclesFor(HouseLot lot, GameState state)
        {
            var obstacles = new List<YardObstacle>();

            foreach (var tree in YardLandscaping.BackTreesFor(lot))
            {
                obstacles.Add(new YardObstacle(tree.Position, YardLandscaping.TreeFootprintRadius));
            }

            foreach (var decoration in state.Decorations)
            {
                if (decoration.HouseId == lot.HouseId)
                {
                    obstacles.Add(new YardObstacle(decoration.YardPosition, DecorationFootprintRadius));
                }
            }

            return obstacles;
        }

        private static bool TrySample(
            LotRect backYard,
            LotRect houseFootprint,
            IReadOnlyList<FenceRun> fenceRuns,
            IReadOnlyList<YardObstacle> obstacles,
            float margin,
            int seed,
            out GridPoint position)
        {
            var inset = PoolFootprintRadius + margin;
            var minX = backYard.MinX + inset;
            var maxX = backYard.MaxX - inset;
            var minZ = backYard.MinZ + inset;
            var maxZ = backYard.MaxZ - inset;
            if (minX > maxX || minZ > maxZ)
            {
                // The region can't hold the pool's whole footprint at this
                // margin — never a point that spills outside the back yard.
                position = default;
                return false;
            }

            var rng = new Random(seed);
            for (var attempt = 0; attempt < MaxAttemptsPerPass; attempt++)
            {
                var point = new GridPoint(
                    Lerp(minX, maxX, (float)rng.NextDouble()),
                    Lerp(minZ, maxZ, (float)rng.NextDouble()));

                if (IsBlocked(point, houseFootprint, fenceRuns, obstacles, margin))
                {
                    continue;
                }

                position = point;
                return true;
            }

            position = default;
            return false;
        }

        private static bool IsBlocked(
            GridPoint point,
            LotRect houseFootprint,
            IReadOnlyList<FenceRun> fenceRuns,
            IReadOnlyList<YardObstacle> obstacles,
            float margin)
        {
            if (houseFootprint.DistanceTo(point) < PoolFootprintRadius + margin)
            {
                return true;
            }

            foreach (var run in fenceRuns)
            {
                if (DistanceToSegment(point, run.A, run.B) < PoolFootprintRadius + margin)
                {
                    return true;
                }
            }

            foreach (var obstacle in obstacles)
            {
                if (Distance(point, obstacle.Position) < PoolFootprintRadius + obstacle.Radius + margin)
                {
                    return true;
                }
            }

            return false;
        }

        private static int SeedFor(HouseLot lot)
        {
            return unchecked(lot.HouseId * 397 + PositionSeedSalt);
        }

        private static float Lerp(float min, float max, float t)
        {
            return min + t * (max - min);
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
