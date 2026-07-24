using System;
using Doggiehood.Core.World;

namespace Doggiehood.Core.Expansion
{
    /// <summary>
    /// Where the map-expansion lock indicator hovers for a given locked
    /// <see cref="Zone"/> (#178): the boundary between the currently
    /// placed <see cref="TileMap"/> and the zone's entrance tile (its
    /// first authored placement), pushed <see cref="ExpansionIndicatorNumbers.HoverOffset"/>
    /// further past that edge — "just past the end of the road", per
    /// docs/specs/expansion.md "Expansion indicator". Derived entirely
    /// from the #109 tile layout, never a separately hand-picked position.
    /// </summary>
    public static class ExpansionIndicatorPlacement
    {
        private static readonly TileEdge[] AllEdges =
        {
            TileEdge.North, TileEdge.South, TileEdge.East, TileEdge.West,
        };

        /// <summary>
        /// Resolves <paramref name="zone"/>'s indicator position against
        /// <paramref name="map"/>: finds which edge of the zone's first
        /// tile placement already borders a tile on the map, then hovers
        /// past that shared edge's midpoint, away from the map. Throws if
        /// the zone's entrance tile doesn't border the map at all — a
        /// caller error, since every authored zone's first placement is
        /// required (by #109's adjacency rule) to touch the map it will
        /// eventually be placed onto.
        /// </summary>
        public static GridPoint Resolve(TileMap map, Zone zone)
        {
            var entry = zone.TilePlacements[0];

            foreach (var edgeTowardMap in AllEdges)
            {
                var neighborCoordinate = entry.Coordinate.Neighbor(edgeTowardMap);
                if (!map.HasTileAt(neighborCoordinate))
                {
                    continue;
                }

                var boundary = TileGeometry.EdgeMidpoint(entry.Coordinate, edgeTowardMap);
                return Push(boundary, edgeTowardMap.Opposite(), ExpansionIndicatorNumbers.HoverOffset);
            }

            throw new InvalidOperationException(
                $"Zone entrance at {entry.Coordinate} does not border the given map — no boundary to hover past.");
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
