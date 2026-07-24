using Doggiehood.Core.World;

namespace Doggiehood.Core.Expansion
{
    /// <summary>
    /// Tuning constants for the map-expansion lock indicator (#178,
    /// docs/specs/expansion.md "Expansion indicator"). Expect adjustment
    /// during playtesting — tune here (and only here).
    /// </summary>
    public static class ExpansionIndicatorNumbers
    {
        /// <summary>
        /// How far past the boundary between the placed map and the next
        /// locked zone's entrance tile the indicator hovers (Derek,
        /// 2026-07-18, on #178: "The icon could be hovering just passed
        /// the end of the road."). Tied to <see cref="WorldDimensions.RoadWidth"/>
        /// — one road-width past the edge midpoint reads as "just past
        /// where the pavement stops" rather than an arbitrary distance.
        /// </summary>
        public const float HoverOffset = WorldDimensions.RoadWidth;
    }
}
