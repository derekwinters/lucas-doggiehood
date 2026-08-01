using System.Collections.Generic;

namespace Doggiehood.Core.World
{
    /// <summary>
    /// Loads an authored <see cref="MapDefinition"/> (#383, Option A) into a
    /// validated <see cref="TileMap"/>, exposed as the neighborhood's
    /// target/full layout. The live playable world stays incremental and is
    /// unaffected by this loader - it produces the authored target map as
    /// Core data only.
    ///
    /// Placement is <b>self-ordered</b>: the authored file lists tiles in
    /// authoring order, not adjacency order, so a naive
    /// <c>foreach + Place</c> would fail against <see cref="TileMap.CanPlace"/>
    /// (#109 adjacency) partway through. Starting from the seeded origin, the
    /// loader repeatedly places any pending tile that already has a placed
    /// neighbor, until no further progress is possible. Any tile still
    /// pending - a #109 adjacency mismatch, or one never reachable from the
    /// origin - is reported as rejected, never thrown.
    /// </summary>
    public static class MapLoader
    {
        private static readonly TileCoordinate Origin = new TileCoordinate(0, 0);
        private const TileType OriginType = TileType.FourWay;

        public static MapLoadResult Load(MapDefinition definition)
        {
            var map = new TileMap(Origin, OriginType);

            var pending = new List<MapDefinitionTile>();
            foreach (var tile in definition.Tiles)
            {
                // The origin is already seeded; skip its authored entry so it
                // isn't mistaken for a rejected tile (it can never re-place
                // onto an occupied coordinate).
                if (tile.Coordinate.Equals(Origin))
                {
                    continue;
                }

                pending.Add(tile);
            }

            bool placedAny;
            do
            {
                placedAny = false;
                for (int i = pending.Count - 1; i >= 0; i--)
                {
                    var tile = pending[i];
                    if (map.CanPlace(tile.Coordinate, tile.Type))
                    {
                        map.Place(tile.Coordinate, tile.Type);
                        pending.RemoveAt(i);
                        placedAny = true;
                    }
                }
            }
            while (placedAny);

            var rejected = new List<TileCoordinate>();
            foreach (var tile in pending)
            {
                rejected.Add(tile.Coordinate);
            }

            var curvedCorners = new Dictionary<TileCoordinate, Quadrant>();
            foreach (var entry in map.Tiles)
            {
                if (TileLotCatalog.TryGetCuppedCorner(entry.Value, out var curved))
                {
                    curvedCorners[entry.Key] = curved;
                }
            }

            return new MapLoadResult(map, rejected, curvedCorners);
        }
    }
}
