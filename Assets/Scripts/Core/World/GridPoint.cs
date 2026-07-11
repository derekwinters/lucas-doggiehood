using System;

namespace Doggiehood.Core.World
{
    /// <summary>
    /// Engine-free 2D world position in meters on the ground plane
    /// (X = east, Z = north). The Unity layer converts to Vector3 at the
    /// boundary.
    /// </summary>
    public readonly struct GridPoint : IEquatable<GridPoint>
    {
        public float X { get; }
        public float Z { get; }

        public GridPoint(float x, float z)
        {
            X = x;
            Z = z;
        }

        public bool Equals(GridPoint other)
        {
            return X.Equals(other.X) && Z.Equals(other.Z);
        }

        public override bool Equals(object obj)
        {
            return obj is GridPoint other && Equals(other);
        }

        public override int GetHashCode()
        {
            return (X.GetHashCode() * 397) ^ Z.GetHashCode();
        }

        public override string ToString()
        {
            return $"({X}, {Z})";
        }
    }
}
