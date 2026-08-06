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
        /// <see cref="CenterOf"/>. Only cul-de-sacs have any (their two
        /// bulb-side quadrants, #385); every other type returns an empty list.
        /// </summary>
        public static IReadOnlyList<GridPoint> TreeWorldPositionsFor(TileType type, TileCoordinate coordinate)
        {
            var center = CenterOf(coordinate);
            return TileLotCatalog.TreeQuadrantsFor(type).Values
                .Select(offset => new GridPoint(center.X + offset.X, center.Z + offset.Z))
                .ToList();
        }
    }
}
