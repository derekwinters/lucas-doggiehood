using System;

namespace Doggiehood.Core.World
{
    /// <summary>
    /// An integer grid position in the multi-tile map (#109): each unit is
    /// one <see cref="WorldDimensions.TileSize"/>-meter tile - Col is the
    /// east/west axis, Row is the north/south axis, matching
    /// <see cref="TileGeometry"/>'s conversion to world-space meters.
    /// </summary>
    public readonly struct TileCoordinate : IEquatable<TileCoordinate>
    {
        public int Col { get; }
        public int Row { get; }

        public TileCoordinate(int col, int row)
        {
            Col = col;
            Row = row;
        }

        /// <summary>The coordinate of the tile sharing this one's <paramref name="edge"/>.</summary>
        public TileCoordinate Neighbor(TileEdge edge)
        {
            switch (edge)
            {
                case TileEdge.North: return new TileCoordinate(Col, Row + 1);
                case TileEdge.South: return new TileCoordinate(Col, Row - 1);
                case TileEdge.East: return new TileCoordinate(Col + 1, Row);
                case TileEdge.West: return new TileCoordinate(Col - 1, Row);
                default: throw new ArgumentOutOfRangeException(nameof(edge), edge, null);
            }
        }

        public bool Equals(TileCoordinate other)
        {
            return Col == other.Col && Row == other.Row;
        }

        public override bool Equals(object obj)
        {
            return obj is TileCoordinate other && Equals(other);
        }

        public override int GetHashCode()
        {
            return (Col * 397) ^ Row;
        }

        public override string ToString()
        {
            return $"({Col}, {Row})";
        }
    }
}
