using System;
using System.Collections.Generic;
using System.Linq;

namespace Doggiehood.Core.World
{
    /// <summary>
    /// World-space positions for a tile placed at a
    /// <see cref="TileCoordinate"/> (#109): derived from the coordinate and
    /// the #105 standard <see cref="WorldDimensions"/> only, never a
    /// separately hand-picked value.
    /// </summary>
    public static class TileGeometry
    {
        /// <summary>The tile's center in world-space meters.</summary>
        public static GridPoint CenterOf(TileCoordinate coordinate)
        {
            return new GridPoint(
                coordinate.Col * WorldDimensions.TileSize,
                coordinate.Row * WorldDimensions.TileSize);
        }

        /// <summary>The midpoint of the tile's <paramref name="edge"/> in world-space meters.</summary>
        public static GridPoint EdgeMidpoint(TileCoordinate coordinate, TileEdge edge)
        {
            var center = CenterOf(coordinate);
            float half = WorldDimensions.TileSize / 2f;

            switch (edge)
            {
                case TileEdge.North: return new GridPoint(center.X, center.Z + half);
                case TileEdge.South: return new GridPoint(center.X, center.Z - half);
                case TileEdge.East: return new GridPoint(center.X + half, center.Z);
                case TileEdge.West: return new GridPoint(center.X - half, center.Z);
                default: throw new ArgumentOutOfRangeException(nameof(edge), edge, null);
            }
        }

        /// <summary>
        /// This tile's property-lot slots (<see cref="TileLotCatalog"/>) in
        /// world-space meters: each type's local offsets shifted by the
        /// tile's own <see cref="CenterOf"/>. A FourWay yields all four
        /// quadrant slots (#607); the origin FourWay's seeded lots are guarded
        /// in <see cref="GameState.LotsForUnlockedTile"/>, not here.
        /// </summary>
        public static IReadOnlyList<GridPoint> LotWorldPositionsFor(TileType type, TileCoordinate coordinate)
        {
            var center = CenterOf(coordinate);
            return TileLotCatalog.LotOffsetsFor(type)
                .Select(offset => new GridPoint(center.X + offset.X, center.Z + offset.Z))
                .ToList();
        }

        /// <summary>
        /// The world-space positions of a tile's open-space-with-trees
        /// quadrants (<see cref="TileLotCatalog.TreeQuadrantsFor"/>) - each
        /// type's local tree offsets shifted by the tile's own
        /// <see cref="CenterOf"/>. Every quadrant with no kept house lot gets a
        /// tree (#614): cul-de-sacs' two bulb-side quadrants (#385), a bend's
        /// cupped corner AND its diagonal opposite, and all four of a twin
        /// bend's quadrants; full-lot types (FourWay/Straight*/Tee*) and the
        /// out-of-scope GreenSpace park return an empty list. Each candidate is
        /// cleared against the tile's own roads (<see cref="LotBounds.RoadsFor"/>/
        /// <see cref="LotBounds.ClearRoadCorridors"/>) so a tree never lands in
        /// the bend's road arc; a quadrant with no clean grass left is skipped
        /// rather than force-placed (<see cref="OpenSpaceTreeHasClearGrass"/>).
        /// </summary>
        public static IReadOnlyList<GridPoint> TreeWorldPositionsFor(TileType type, TileCoordinate coordinate)
        {
            var center = CenterOf(coordinate);
            var roads = LotBounds.RoadsFor(coordinate, type);
            var positions = new List<GridPoint>();
            foreach (var entry in TileLotCatalog.TreeQuadrantsFor(type))
            {
                var position = new GridPoint(center.X + entry.Value.X, center.Z + entry.Value.Z);
                if (OpenSpaceTreeHasClearGrass(QuadrantWorldBounds(coordinate, entry.Key), position, roads))
                {
                    positions.Add(position);
                }
            }

            return positions;
        }

        /// <summary>
        /// Whether an open-space tree at <paramref name="position"/> still has
        /// clean grass inside <paramref name="quadrantBounds"/> once the tile's
        /// <paramref name="roads"/> corridors are cleared (#614). False when the
        /// quadrant is all pavement — the cupped corner of a bend the road arc
        /// leaves no room in — so the caller skips the tree rather than forcing
        /// it into the road; the diagonal-opposite quadrant (which borders no
        /// roaded edge) always keeps its clean grass and its tree.
        /// </summary>
        public static bool OpenSpaceTreeHasClearGrass(
            LotRect quadrantBounds, GridPoint position, IReadOnlyList<Road> roads)
        {
            var clear = LotBounds.ClearRoadCorridors(quadrantBounds, roads);
            return clear.Width > 0f && clear.Depth > 0f && clear.Contains(position);
        }

        /// <summary>The world-space bounds of one <paramref name="quadrant"/> of
        /// the tile at <paramref name="coordinate"/>: a
        /// <see cref="WorldDimensions.TileSize"/>/2-per-side rect on that
        /// quadrant, centred on the tile — the same tiling
        /// <see cref="LotBounds.QuadrantBounds"/> produces, keyed by coordinate
        /// rather than a house lot.</summary>
        private static LotRect QuadrantWorldBounds(TileCoordinate coordinate, Quadrant quadrant)
        {
            var center = CenterOf(coordinate);
            var half = WorldDimensions.TileSize / 4f;
            var (signX, signZ) = SignsFor(quadrant);
            var centerX = center.X + signX * half;
            var centerZ = center.Z + signZ * half;
            return new LotRect(centerX - half, centerX + half, centerZ - half, centerZ + half);
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
