using System.Collections.Generic;
using Doggiehood.Core.World;

namespace Doggiehood.Core.Expansion
{
    /// <summary>
    /// The player-choice expansion frontier (#295): given the currently-placed
    /// <see cref="TileMap"/> and the full authored target map, every coordinate
    /// that (a) borders an already-placed tile, (b) exists in the target map,
    /// and (c) isn't already placed is a candidate. A candidate is only an
    /// unlockable frontier tile when it would place validly
    /// (<see cref="TileMap.CanPlace"/> — its road/no-road edges agree with every
    /// placed neighbor) <b>and</b> it forms a real road connection into the
    /// network (<see cref="TileMap.HasRoadConnectionAt"/> — at least one shared
    /// edge carries a road on both sides). CanPlace alone admits a
    /// no-road/no-road boundary, so keying the frontier to the connection
    /// predicate is what makes it precisely Derek's "a road with a connection
    /// point that has another tile defined" (#507). A pure function of its two
    /// inputs — nothing cached — so callers re-read it as the placed map grows.
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

                if (placed.CanPlace(coordinate, entry.Value)
                    && placed.HasRoadConnectionAt(coordinate, entry.Value))
                {
                    frontier.Add(coordinate);
                }
            }

            return frontier;
        }
    }
}
