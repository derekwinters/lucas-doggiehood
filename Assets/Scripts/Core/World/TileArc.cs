using System;

namespace Doggiehood.Core.World
{
    /// <summary>
    /// One continuous road path within a tile, joining two corner-adjacent
    /// edges (#109). Used to describe the <c>OpposingTurnsNS</c>/
    /// <c>OpposingTurnsEW</c> tiles' two independent, unconnected arcs —
    /// resolved on #109 ("each arc joins only its two adjacent sides",
    /// overriding the earlier #105 loop/island framing). Every other tile
    /// type's edges belong to one single connected junction or dead end, so
    /// they declare no arcs at all (see <see cref="TileTypeDefinition"/>).
    /// </summary>
    public readonly struct TileArc : IEquatable<TileArc>
    {
        public TileEdge First { get; }
        public TileEdge Second { get; }

        public TileArc(TileEdge first, TileEdge second)
        {
            if (!first.IsAdjacentTo(second))
            {
                throw new ArgumentException(
                    $"A tile arc joins two adjacent edges; {first} and {second} are not adjacent.");
            }

            First = first;
            Second = second;
        }

        /// <summary>The other edge this arc reaches from <paramref name="edge"/>,
        /// or null if this arc doesn't touch <paramref name="edge"/> at all.</summary>
        public TileEdge? OtherEnd(TileEdge edge)
        {
            if (edge == First)
            {
                return Second;
            }

            if (edge == Second)
            {
                return First;
            }

            return null;
        }

        public bool Equals(TileArc other)
        {
            return (First == other.First && Second == other.Second)
                || (First == other.Second && Second == other.First);
        }

        public override bool Equals(object obj)
        {
            return obj is TileArc other && Equals(other);
        }

        public override int GetHashCode()
        {
            // Symmetric so (A, B) and (B, A) hash identically, matching Equals.
            return (int)First ^ (int)Second;
        }

        public override string ToString()
        {
            return $"{First}<->{Second}";
        }
    }
}
