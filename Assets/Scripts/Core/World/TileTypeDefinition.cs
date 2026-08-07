using System.Collections.Generic;

namespace Doggiehood.Core.World
{
    /// <summary>
    /// One catalog entry (#105/#109): a <see cref="TileType"/> and the edges
    /// it carries a road on. Every type's roaded edges belong to one single
    /// connected junction or dead end — the two <c>OpposingTurns</c> "twin
    /// bend" types were the sole exception (two independent internal arcs),
    /// and #583 removed them, so the per-type arc modeling went with them.
    /// </summary>
    public sealed class TileTypeDefinition
    {
        public TileType Type { get; }

        /// <summary>Which of the tile's four edges carry a road — the
        /// basis for placement/adjacency validation against a
        /// neighboring tile (#109).</summary>
        public IReadOnlyCollection<TileEdge> RoadEdges { get; }

        private readonly HashSet<TileEdge> roadEdgeSet;

        public TileTypeDefinition(TileType type, IEnumerable<TileEdge> roadEdges)
        {
            Type = type;
            roadEdgeSet = new HashSet<TileEdge>(roadEdges);
            RoadEdges = roadEdgeSet;
        }

        /// <summary>Whether this tile carries a road on <paramref name="edge"/>.</summary>
        public bool HasRoadOn(TileEdge edge)
        {
            return roadEdgeSet.Contains(edge);
        }
    }
}
