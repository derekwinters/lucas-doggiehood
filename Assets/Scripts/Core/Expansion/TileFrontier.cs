using System.Collections.Generic;
using Doggiehood.Core.World;

namespace Doggiehood.Core.Expansion
{
    /// <summary>
    /// The player-choice expansion frontier (#295): given the currently-placed
    /// <see cref="TileMap"/> and the full authored target map, every coordinate
    /// that (a) borders an already-placed tile, (b) exists in the target map,
    /// and (c) isn't already placed is an unlockable frontier tile. The check
    /// reuses #109 edge-adjacency exactly the way <see cref="TileMap.CanPlace"/>
    /// does — a target tile is on the frontier only if it would place validly
    /// (its road/no-road edges agree with every placed neighbor), which is
    /// precisely Derek's "a road with a connection point that has another tile
    /// defined". A pure function of its two inputs — nothing cached — so callers
    /// re-read it as the placed map grows.
    /// </summary>
    public static class TileFrontier
    {
        public static IReadOnlyCollection<TileCoordinate> Compute(TileMap placed, TileMap target)
        {
            var frontier = new HashSet<TileCoordinate>();
            foreach (var entry in target.Tiles)
            {
                var coordinate = entry.Key;
                if (placed.HasTileAt(coordinate))
                {
                    continue;
                }

                if (placed.CanPlace(coordinate, entry.Value))
                {
                    frontier.Add(coordinate);
                }
            }

            return frontier;
        }
    }
}
