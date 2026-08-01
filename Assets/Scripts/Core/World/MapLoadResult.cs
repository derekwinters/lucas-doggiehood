using System.Collections.Generic;

namespace Doggiehood.Core.World
{
    /// <summary>
    /// The outcome of loading an authored <see cref="MapDefinition"/> into a
    /// validated <see cref="TileMap"/> (#383): the built <see cref="Map"/>,
    /// the <see cref="RejectedCoordinates"/> that could not be placed
    /// (a #109 adjacency mismatch or a tile never reachable from the seeded
    /// origin), and <see cref="CurvedCorners"/> - for each placed bend
    /// (<c>Turn*</c>) tile, the <see cref="Quadrant"/> corner that renders
    /// curved (data only for this issue; no rendering).
    /// </summary>
    public sealed class MapLoadResult
    {
        public MapLoadResult(
            TileMap map,
            IReadOnlyList<TileCoordinate> rejectedCoordinates,
            IReadOnlyDictionary<TileCoordinate, Quadrant> curvedCorners)
        {
            Map = map;
            RejectedCoordinates = rejectedCoordinates;
            CurvedCorners = curvedCorners;
        }

        public TileMap Map { get; }

        public IReadOnlyList<TileCoordinate> RejectedCoordinates { get; }

        public IReadOnlyDictionary<TileCoordinate, Quadrant> CurvedCorners { get; }
    }
}
