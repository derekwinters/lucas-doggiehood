using System;

namespace Doggiehood.Core.World
{
    /// <summary>An axis-aligned rectangle on the ground plane, in meters
    /// (#222). Facing directions in this codebase are always cardinal
    /// (<see cref="HousePlacement.FacingToward"/> snaps to the dominant
    /// axis), so every shape <see cref="LotBounds"/> works with stays
    /// axis-aligned — no rotation needed.</summary>
    public readonly struct LotRect
    {
        public float MinX { get; }
        public float MaxX { get; }
        public float MinZ { get; }
        public float MaxZ { get; }

        public LotRect(float minX, float maxX, float minZ, float maxZ)
        {
            if (maxX < minX)
            {
                throw new ArgumentException("maxX must be >= minX.", nameof(maxX));
            }

            if (maxZ < minZ)
            {
                throw new ArgumentException("maxZ must be >= minZ.", nameof(maxZ));
            }

            MinX = minX;
            MaxX = maxX;
            MinZ = minZ;
            MaxZ = maxZ;
        }

        public float Width
        {
            get { return MaxX - MinX; }
        }

        public float Depth
        {
            get { return MaxZ - MinZ; }
        }

        public GridPoint Center
        {
            get { return new GridPoint((MinX + MaxX) / 2f, (MinZ + MaxZ) / 2f); }
        }

        /// <summary>Whether <paramref name="point"/> sits inside this rect
        /// (inclusive of the edges).</summary>
        public bool Contains(GridPoint point)
        {
            return point.X >= MinX && point.X <= MaxX && point.Z >= MinZ && point.Z <= MaxZ;
        }

        /// <summary>Whether this rect fully contains <paramref name="other"/>.</summary>
        public bool Contains(LotRect other)
        {
            return other.MinX >= MinX && other.MaxX <= MaxX && other.MinZ >= MinZ && other.MaxZ <= MaxZ;
        }

        /// <summary>Whether this rect shares any positive-area overlap with
        /// <paramref name="other"/> — rects that only touch at an edge
        /// (e.g. two lot quadrants meeting at the road centerline) do NOT
        /// overlap.</summary>
        public bool Overlaps(LotRect other)
        {
            return MinX < other.MaxX && MaxX > other.MinX && MinZ < other.MaxZ && MaxZ > other.MinZ;
        }
    }

    /// <summary>
    /// Lot bounds (#222). Decision (conversation with Derek, 2026-07-20): a
    /// property (lot) is one QUADRANT of a tile — on the starting FourWay
    /// intersection each of the 4 quadrants is a property, and that same
    /// quadrant size (<see cref="WorldDimensions.TileSize"/> / 2 per side)
    /// is the standard for every layout.
    ///
    /// <see cref="QuadrantBounds"/> derives purely from the lot's own
    /// <see cref="HouseLot.Quadrant"/> and <see cref="WorldDimensions.TileSize"/>
    /// — NOT from <see cref="HouseLot.Position"/> (a separate, hand-picked
    /// house-placement choice, <see cref="NeighborhoodLayout.LotDistanceFromCenter"/>).
    /// Sizing bounds to a full tile-quadrant (half = TileSize/4 = 15m) and
    /// centering them on the hand-picked position (14m) would overlap the
    /// opposite lot's bounds across the road by 1m; centering each
    /// quadrant on the tile's own quadrant split (0m/±30m per lot) instead
    /// makes the 4 lots' bounds exactly tile the 60m tile with no gap or
    /// overlap, satisfying the "no spill into the neighbouring quadrant"
    /// requirement by construction.
    ///
    /// <see cref="FrontYard"/>/<see cref="BackYard"/> split those bounds
    /// relative to <see cref="HousePlacement.FrontFacing"/>, excluding the
    /// house footprint — the regions #170 scatters trees into.
    /// </summary>
    public static class LotBounds
    {
        /// <summary>The lot's rectangular bounds: one tile-quadrant
        /// (<see cref="WorldDimensions.TileSize"/> / 2 per side), positioned
        /// on the lot's own <see cref="HouseLot.Quadrant"/>.</summary>
        public static LotRect QuadrantBounds(HouseLot lot)
        {
            var half = WorldDimensions.TileSize / 4f;
            var (signX, signZ) = SignsFor(lot.Quadrant);
            var centerX = signX * half;
            var centerZ = signZ * half;
            return new LotRect(centerX - half, centerX + half, centerZ - half, centerZ + half);
        }

        /// <summary>The portion of <see cref="QuadrantBounds"/> on the
        /// street side of the house (the direction
        /// <see cref="HousePlacement.FrontFacing"/> points), excluding the
        /// house footprint.</summary>
        public static LotRect FrontYard(HouseLot lot)
        {
            return YardSplit(lot).Front;
        }

        /// <summary>The portion of <see cref="QuadrantBounds"/> behind the
        /// house (away from the faced street), excluding the house
        /// footprint.</summary>
        public static LotRect BackYard(HouseLot lot)
        {
            return YardSplit(lot).Back;
        }

        private static (LotRect Front, LotRect Back) YardSplit(HouseLot lot)
        {
            var bounds = QuadrantBounds(lot);
            var facing = HousePlacement.FrontFacing(lot);
            var house = HousePlacement.Position(lot, HousePlacement.KitScale);
            var halfDepth = HalfDepthOf(lot);

            if (facing.X > 0f)
            {
                var facadeX = house.X + halfDepth;
                var rearX = house.X - halfDepth;
                return (
                    new LotRect(facadeX, bounds.MaxX, bounds.MinZ, bounds.MaxZ),
                    new LotRect(bounds.MinX, rearX, bounds.MinZ, bounds.MaxZ));
            }

            if (facing.X < 0f)
            {
                var facadeX = house.X - halfDepth;
                var rearX = house.X + halfDepth;
                return (
                    new LotRect(bounds.MinX, facadeX, bounds.MinZ, bounds.MaxZ),
                    new LotRect(rearX, bounds.MaxX, bounds.MinZ, bounds.MaxZ));
            }

            if (facing.Z > 0f)
            {
                var facadeZ = house.Z + halfDepth;
                var rearZ = house.Z - halfDepth;
                return (
                    new LotRect(bounds.MinX, bounds.MaxX, facadeZ, bounds.MaxZ),
                    new LotRect(bounds.MinX, bounds.MaxX, bounds.MinZ, rearZ));
            }

            if (facing.Z < 0f)
            {
                var facadeZ = house.Z - halfDepth;
                var rearZ = house.Z + halfDepth;
                return (
                    new LotRect(bounds.MinX, bounds.MaxX, bounds.MinZ, facadeZ),
                    new LotRect(bounds.MinX, bounds.MaxX, rearZ, bounds.MaxZ));
            }

            throw new ArgumentException("Lot facing must be a nonzero cardinal direction.", nameof(lot));
        }

        private static float HalfDepthOf(HouseLot lot)
        {
            var model = HouseModelCatalog.ForHouse(lot.HouseId);
            return HousePlacement.KitScale * model.FootprintZ / 2f;
        }

        private static (float SignX, float SignZ) SignsFor(Quadrant quadrant)
        {
            switch (quadrant)
            {
                case Quadrant.NorthEast: return (1f, 1f);
                case Quadrant.NorthWest: return (-1f, 1f);
                case Quadrant.SouthEast: return (1f, -1f);
                case Quadrant.SouthWest: return (-1f, -1f);
                default: throw new ArgumentOutOfRangeException(nameof(quadrant), quadrant, null);
            }
        }
    }
}
