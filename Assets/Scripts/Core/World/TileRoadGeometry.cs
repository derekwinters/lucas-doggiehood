using System.Collections.Generic;

namespace Doggiehood.Core.World
{
    /// <summary>
    /// One straight road segment of a tile (#373): from the tile centre out to
    /// one road-carrying edge, half a tile long and <see cref="Width"/> wide,
    /// running along the <see cref="Orientation"/> axis. The Unity layer renders
    /// a road surface for each segment so an unlocked tile's roads derive from
    /// the tile catalog geometry the same way the starting intersection's arms
    /// do.
    /// </summary>
    public readonly struct TileRoadSegment
    {
        public GridPoint Center { get; }
        public StreetOrientation Orientation { get; }
        public float Length { get; }
        public float Width { get; }

        public TileRoadSegment(GridPoint center, StreetOrientation orientation, float length, float width)
        {
            Center = center;
            Orientation = orientation;
            Length = length;
            Width = width;
        }
    }

    /// <summary>
    /// Road surfaces for a tile derived from its <see cref="TileCatalog"/> road
    /// edges (#373, the #109 tile geometry): one <see cref="TileRoadSegment"/>
    /// reaching from the tile centre to each edge that carries a road. A
    /// <see cref="TileType.FourWay"/> yields four arms (the starting
    /// intersection's shape); a cul-de-sac yields one arm meeting its neighbour.
    /// Purely a function of the coordinate and the catalog — never a
    /// hand-placed value — so any authored zone tile renders road the same way.
    /// </summary>
    public static class TileRoadGeometry
    {
        private static readonly TileEdge[] AllEdges =
        {
            TileEdge.North, TileEdge.South, TileEdge.East, TileEdge.West,
        };

        public static IReadOnlyList<TileRoadSegment> SegmentsFor(TileCoordinate coordinate, TileType type)
        {
            var definition = TileCatalog.Get(type);
            var center = TileGeometry.CenterOf(coordinate);
            var segments = new List<TileRoadSegment>();

            foreach (var edge in AllEdges)
            {
                if (!definition.HasRoadOn(edge))
                {
                    continue;
                }

                var edgeMidpoint = TileGeometry.EdgeMidpoint(coordinate, edge);
                var segmentCenter = new GridPoint(
                    (center.X + edgeMidpoint.X) / 2f,
                    (center.Z + edgeMidpoint.Z) / 2f);
                var orientation = (edge == TileEdge.North || edge == TileEdge.South)
                    ? StreetOrientation.NorthSouth
                    : StreetOrientation.EastWest;

                segments.Add(new TileRoadSegment(
                    segmentCenter, orientation, WorldDimensions.TileSize / 2f, WorldDimensions.RoadWidth));
            }

            return segments;
        }
    }
}
