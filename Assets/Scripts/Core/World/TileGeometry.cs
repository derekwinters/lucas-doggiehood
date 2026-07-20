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
        /// tile's own <see cref="CenterOf"/>. Not valid for
        /// <see cref="TileType.FourWay"/> - see <see cref="TileLotCatalog.LotsFor"/>.
        /// </summary>
        public static IReadOnlyList<GridPoint> LotWorldPositionsFor(TileType type, TileCoordinate coordinate)
        {
            var center = CenterOf(coordinate);
            return TileLotCatalog.LotOffsetsFor(type)
                .Select(offset => new GridPoint(center.X + offset.X, center.Z + offset.Z))
                .ToList();
        }
    }
}
