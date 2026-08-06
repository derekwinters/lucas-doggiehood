using System;

namespace Doggiehood.Core.World
{
    /// <summary>
    /// An off-map road opening (#599): an outer edge of a placed tile that
    /// carries a road but has no placed neighbor across it — the point where a
    /// road runs off the edge of the currently built map. This is exactly the
    /// "road end at the map boundary" the expansion frontier already reasons
    /// about (<see cref="TileMap.HasRoadConnectionAt"/> /
    /// <see cref="TileTypeDefinition.HasRoadOn"/>); a delivery truck drives in
    /// from off-map through one of these and leaves through another.
    ///
    /// <see cref="Point"/> is the edge midpoint in world meters — the same point
    /// <see cref="MapWalkNetwork.RoadsFrom"/> uses as the outermost end of the
    /// road that reaches this edge, so an opening is always a node of the live
    /// road graph.
    /// </summary>
    public readonly struct RoadOpening : IEquatable<RoadOpening>
    {
        public TileCoordinate Tile { get; }
        public TileEdge Edge { get; }
        public GridPoint Point { get; }

        public RoadOpening(TileCoordinate tile, TileEdge edge, GridPoint point)
        {
            Tile = tile;
            Edge = edge;
            Point = point;
        }

        public bool Equals(RoadOpening other)
        {
            return Tile.Equals(other.Tile) && Edge == other.Edge;
        }

        public override bool Equals(object obj)
        {
            return obj is RoadOpening other && Equals(other);
        }

        public override int GetHashCode()
        {
            return (Tile.GetHashCode() * 397) ^ (int)Edge;
        }

        public override string ToString()
        {
            return $"{Edge} opening of {Tile} at {Point}";
        }
    }
}
