using System.Collections.Generic;

namespace Doggiehood.Core.Art
{
    /// <summary>
    /// #601: picks a delivery truck's color from the curated standard car
    /// spread (<see cref="Palette.CarColorHex"/>). Unlike a house's tint —
    /// which <see cref="HouseVariantAssignment"/> derives from the house id and
    /// SaveCodec persists — a truck is transient and never saved, so the pick
    /// is keyed on a spawn seed the Unity layer supplies (an incrementing spawn
    /// counter), NOT on any stable entity id. The mapping is still a pure,
    /// deterministic function of that seed so it is unit-testable; an optional
    /// overload takes the colors currently on the road and picks outside them
    /// so two concurrent trucks don't share a color.
    /// </summary>
    public static class CarColorAssignment
    {
        /// <summary>How many standard car colors the pick chooses from — the
        /// size of the curated real-world car spread. Single source of truth
        /// for that count: <see cref="Palette.CarColorHex"/> exposes exactly
        /// this many colours.</summary>
        public const int CarColorCount = 7;

        /// <summary>The deterministic car-color index (0..<see cref="CarColorCount"/>-1)
        /// for a spawn <paramref name="seed"/>. A fixed integer avalanche mix
        /// (constant in, constant out) so consecutive spawn seeds spread across
        /// the whole spread instead of clustering, then reduced into range —
        /// no external state, stable across sessions and machines.</summary>
        public static int IndexFor(int seed)
        {
            unchecked
            {
                var h = (uint)seed;
                h ^= 2166136261u;
                h *= 16777619u;
                h ^= h >> 13;
                h *= 0x5bd1e995u;
                h ^= h >> 15;
                return (int)(h % CarColorCount);
            }
        }

        /// <summary>The car-color index for a spawn <paramref name="seed"/> that
        /// avoids the colors already on the road (<paramref name="activeIndices"/>):
        /// starts from the plain seeded pick and steps forward through the spread
        /// to the first free color. When every color is in use it falls back to
        /// the seeded pick (all trucks visible means a repeat is unavoidable),
        /// and with no active colors it is exactly <see cref="IndexFor(int)"/>.</summary>
        public static int IndexFor(int seed, ICollection<int> activeIndices)
        {
            var start = IndexFor(seed);
            if (activeIndices == null || activeIndices.Count == 0)
            {
                return start;
            }

            for (var step = 0; step < CarColorCount; step++)
            {
                var candidate = (start + step) % CarColorCount;
                if (!activeIndices.Contains(candidate))
                {
                    return candidate;
                }
            }

            return start;
        }
    }
}
