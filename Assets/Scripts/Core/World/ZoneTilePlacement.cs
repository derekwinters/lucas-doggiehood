using System;

namespace Doggiehood.Core.World
{
    /// <summary>
    /// One authored tile in a <see cref="Zone"/> (#56): the grid coordinate
    /// it goes at and which <see cref="TileType"/> to place there.
    /// </summary>
    public readonly struct ZoneTilePlacement : IEquatable<ZoneTilePlacement>
    {
        public TileCoordinate Coordinate { get; }
        public TileType Type { get; }

        public ZoneTilePlacement(TileCoordinate coordinate, TileType type)
        {
            Coordinate = coordinate;
            Type = type;
        }

        public bool Equals(ZoneTilePlacement other)
        {
            return Coordinate.Equals(other.Coordinate) && Type == other.Type;
        }

        public override bool Equals(object obj)
        {
            return obj is ZoneTilePlacement other && Equals(other);
        }

        public override int GetHashCode()
        {
            return (Coordinate.GetHashCode() * 397) ^ (int)Type;
        }

        public override string ToString()
        {
            return $"{Type}@{Coordinate}";
        }
    }
}
