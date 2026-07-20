using System.Collections.Generic;
using System.Linq;

namespace Doggiehood.Core.World
{
    /// <summary>
    /// One catalog entry (#105/#109): a <see cref="TileType"/>, the edges it
    /// carries a road on, and (for the OpposingTurns types only) the
    /// independent internal arcs those edges belong to.
    /// </summary>
    public sealed class TileTypeDefinition
    {
        public TileType Type { get; }

        /// <summary>Which of the tile's four edges carry a road — the
        /// basis for placement/adjacency validation against a
        /// neighboring tile (#109).</summary>
        public IReadOnlyCollection<TileEdge> RoadEdges { get; }

        /// <summary>The tile's independent internal road path(s). Empty
        /// for every type except <c>OpposingTurnsNS</c>/<c>OpposingTurnsEW</c>,
        /// whose two arcs are the only case where this catalog's edges
        /// don't all belong to one single connected junction/dead end.</summary>
        public IReadOnlyList<TileArc> Arcs { get; }

        private readonly HashSet<TileEdge> roadEdgeSet;

        public TileTypeDefinition(TileType type, IEnumerable<TileEdge> roadEdges, IEnumerable<TileArc> arcs = null)
        {
            Type = type;
            roadEdgeSet = new HashSet<TileEdge>(roadEdges);
            RoadEdges = roadEdgeSet;
            Arcs = (arcs ?? Enumerable.Empty<TileArc>()).ToList();
        }

        /// <summary>Whether this tile carries a road on <paramref name="edge"/>.</summary>
        public bool HasRoadOn(TileEdge edge)
        {
            return roadEdgeSet.Contains(edge);
        }

        /// <summary>
        /// Every edge reachable from <paramref name="edge"/> via one of
        /// this tile's <see cref="Arcs"/> — scoped to that single arc, so
        /// an OpposingTurns tile's two arcs never leak into each other
        /// (#109: "adjacency treats each arc's road ends independently").
        /// Empty for types with no arcs modeled.
        /// </summary>
        public IEnumerable<TileEdge> EdgesConnectedVia(TileEdge edge)
        {
            foreach (var arc in Arcs)
            {
                var other = arc.OtherEnd(edge);
                if (other.HasValue)
                {
                    yield return other.Value;
                }
            }
        }
    }
}
