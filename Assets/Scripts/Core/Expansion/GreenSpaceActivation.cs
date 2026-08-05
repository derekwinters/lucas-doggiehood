using System.Collections.Generic;
using Doggiehood.Core.World;

namespace Doggiehood.Core.Expansion
{
    /// <summary>
    /// #539: the green-space auto-activation eligibility test. Distinct from the
    /// paid, road-connected player-choice frontier (<see cref="TileFrontier"/>):
    /// a target <see cref="TileType.GreenSpace"/> coordinate becomes eligible the
    /// moment 2+ of its 4 edges border a tile already in <paramref name="placed"/>
    /// — a placed road tile OR an already-activated green space, so activation
    /// cascades. There is no cost, no lock icon, and no road-connection
    /// requirement (a green space carries no road, so it could never satisfy
    /// <see cref="TileMap.HasRoadConnectionAt"/> and can never appear on the
    /// frontier — see <see cref="TileFrontier"/>).
    ///
    /// A pure function of its two inputs — nothing cached — mirroring
    /// <see cref="TileFrontier.Compute"/>'s shape. Callers re-read it as the
    /// placed map grows; <see cref="GameState"/> loops it to a fixpoint so one
    /// activated green space can make an adjacent one newly eligible.
    /// </summary>
    public static class GreenSpaceActivation
    {
        /// <summary>The minimum number of a green space's four edges that must
        /// border an already-activated tile for it to auto-activate (Derek's
        /// "when 2 edges … have an activated tile touching them").</summary>
        public const int RequiredBorderingEdges = 2;

        private static readonly TileEdge[] AllEdges =
        {
            TileEdge.North, TileEdge.South, TileEdge.East, TileEdge.West,
        };

        public static IReadOnlyCollection<TileCoordinate> Compute(TileMap placed, TileMap target)
        {
            var eligible = new HashSet<TileCoordinate>();
            foreach (var entry in target.Tiles)
            {
                if (entry.Value != TileType.GreenSpace)
                {
                    continue;
                }

                var coordinate = entry.Key;
                if (placed.HasTileAt(coordinate))
                {
                    continue;
                }

                if (CountBorderingPlacedEdges(placed, coordinate) >= RequiredBorderingEdges)
                {
                    eligible.Add(coordinate);
                }
            }

            return eligible;
        }

        private static int CountBorderingPlacedEdges(TileMap placed, TileCoordinate coordinate)
        {
            var count = 0;
            foreach (var edge in AllEdges)
            {
                if (placed.HasTileAt(coordinate.Neighbor(edge)))
                {
                    count++;
                }
            }

            return count;
        }
    }
}
