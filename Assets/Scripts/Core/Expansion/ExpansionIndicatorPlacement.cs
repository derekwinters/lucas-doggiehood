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
        /// <paramref name="frontierCoordinate"/> (whose authored tile is
        /// <paramref name="frontierType"/>) against <paramref name="placed"/>:
        /// finds which edge of the coordinate carries a road into a placed tile —
        /// a road on <b>both</b> sides of the shared edge, the same
        /// <see cref="TileMap.HasRoadConnectionAt"/> predicate that gates the
        /// frontier (#537) — then hovers past that road edge's midpoint, away from
        /// the map. Throws if no shared edge carries a road — a caller error,
        /// since a frontier coordinate is by definition road-connected to the
        /// placed network (<see cref="TileFrontier"/>). A lock never anchors to a
        /// grass/non-road neighbour edge.
        /// </summary>
        public static GridPoint Resolve(TileMap placed, TileCoordinate frontierCoordinate, TileType frontierType)
        {
            var frontierDefinition = TileCatalog.Get(frontierType);

            foreach (var edgeTowardMap in AllEdges)
            {
                var neighborCoordinate = frontierCoordinate.Neighbor(edgeTowardMap);
                if (!placed.HasTileAt(neighborCoordinate))
                {
                    continue;
                }

                var neighborDefinition = TileCatalog.Get(placed.GetTileAt(neighborCoordinate));
                if (!frontierDefinition.HasRoadOn(edgeTowardMap)
                    || !neighborDefinition.HasRoadOn(edgeTowardMap.Opposite()))
                {
                    continue;
                }

                var boundary = TileGeometry.EdgeMidpoint(frontierCoordinate, edgeTowardMap);
                return Push(boundary, edgeTowardMap.Opposite(), ExpansionIndicatorNumbers.HoverOffset);
            }

            throw new InvalidOperationException(
                $"Frontier coordinate {frontierCoordinate} shares no road-carrying edge with the given map — no road end to hover past.");
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
