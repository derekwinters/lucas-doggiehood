using System;
using Doggiehood.Core.World;

namespace Doggiehood.Core.Dogs
{
    /// <summary>
    /// Resolves whether a tap lands on a dog at its current position (#8).
    /// The Unity layer raycasts to colliders; this is the engine-free
    /// decision rule both sides share.
    /// </summary>
    public static class TapResolver
    {
        public static bool IsHit(GridPoint dogPosition, GridPoint tap, float radius)
        {
            var dx = dogPosition.X - tap.X;
            var dz = dogPosition.Z - tap.Z;
            return Math.Sqrt(dx * dx + dz * dz) <= radius;
        }
    }
}
