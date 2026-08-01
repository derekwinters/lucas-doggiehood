using System;
using Doggiehood.Core.World;

namespace Doggiehood.Core.Expansion
{
    /// <summary>
    /// Where the map-expansion lock indicator hovers for a given frontier
    /// coordinate (#178/#453): the boundary between the currently placed
    /// <see cref="TileMap"/> and that coordinate, pushed
    /// <see cref="ExpansionIndicatorNumbers.HoverOffset"/> further past that edge
    /// — "just past the end of the road", per docs/specs/expansion.md "Expansion
    /// indicator". Derived entirely from the #109 tile layout, never a separately
    /// hand-picked position. Keyed on a coordinate (the #453 multi-lock frontier
    /// model), not the retired <c>Zone</c>.
    /// </summary>
    public static class ExpansionIndicatorPlacement
    {
        private static readonly TileEdge[] AllEdges =
        {
            TileEdge.North, TileEdge.South, TileEdge.East, TileEdge.West,
        };

        /// <summary>
        /// Resolves the indicator position for the frontier
        /// <paramref name="frontierCoordinate"/> against <paramref name="placed"/>:
        /// finds which edge of the coordinate already borders a placed tile, then
        /// hovers past that shared edge's midpoint, away from the map. Throws if
        /// the coordinate doesn't border the map at all — a caller error, since a
        /// frontier coordinate is by definition adjacent to a placed tile
        /// (<see cref="TileFrontier"/>).
        /// </summary>
        public static GridPoint Resolve(TileMap placed, TileCoordinate frontierCoordinate)
        {
            foreach (var edgeTowardMap in AllEdges)
            {
                var neighborCoordinate = frontierCoordinate.Neighbor(edgeTowardMap);
                if (!placed.HasTileAt(neighborCoordinate))
                {
                    continue;
                }

                var boundary = TileGeometry.EdgeMidpoint(frontierCoordinate, edgeTowardMap);
                return Push(boundary, edgeTowardMap.Opposite(), ExpansionIndicatorNumbers.HoverOffset);
            }

            throw new InvalidOperationException(
                $"Frontier coordinate {frontierCoordinate} does not border the given map — no boundary to hover past.");
        }

        /// <summary>Moves <paramref name="point"/> <paramref name="distance"/>
        /// meters in the compass direction <paramref name="direction"/> represents.</summary>
        private static GridPoint Push(GridPoint point, TileEdge direction, float distance)
        {
            switch (direction)
            {
                case TileEdge.North: return new GridPoint(point.X, point.Z + distance);
                case TileEdge.South: return new GridPoint(point.X, point.Z - distance);
                case TileEdge.East: return new GridPoint(point.X + distance, point.Z);
                case TileEdge.West: return new GridPoint(point.X - distance, point.Z);
                default: throw new ArgumentOutOfRangeException(nameof(direction), direction, null);
            }
        }
    }
}
