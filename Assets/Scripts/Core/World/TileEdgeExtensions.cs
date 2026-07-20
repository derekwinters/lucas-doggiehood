using System;

namespace Doggiehood.Core.World
{
    /// <summary>Helpers for reasoning about pairs of <see cref="TileEdge"/>
    /// values (#109): which edge faces back across a shared tile boundary,
    /// and which pairs are corner-adjacent rather than opposite.</summary>
    public static class TileEdgeExtensions
    {
        /// <summary>The edge directly across the tile (North &lt;-&gt; South,
        /// East &lt;-&gt; West) — the edge a neighboring tile shares this one
        /// through when adjacent in that direction.</summary>
        public static TileEdge Opposite(this TileEdge edge)
        {
            switch (edge)
            {
                case TileEdge.North: return TileEdge.South;
                case TileEdge.South: return TileEdge.North;
                case TileEdge.East: return TileEdge.West;
                case TileEdge.West: return TileEdge.East;
                default: throw new ArgumentOutOfRangeException(nameof(edge), edge, null);
            }
        }

        /// <summary>True when the two edges meet at a tile corner (e.g.
        /// North &amp; East) rather than being the same edge or opposite
        /// each other (e.g. North &amp; South). Turn-shaped tiles/arcs
        /// (#109) only ever join adjacent edges.</summary>
        public static bool IsAdjacentTo(this TileEdge edge, TileEdge other)
        {
            return edge != other && edge.Opposite() != other;
        }
    }
}
