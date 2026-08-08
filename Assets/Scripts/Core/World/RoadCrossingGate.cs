using System;
using System.Collections.Generic;

namespace Doggiehood.Core.World
{
    /// <summary>
    /// First-come right-of-way gate for road crossings (#546). Each crosswalk
    /// <see cref="WalkEdge"/> is a single exclusive claim: the first occupant to
    /// arrive at it (via <see cref="TryEnter"/>) holds it until it
    /// <see cref="Exit"/>s, and any different occupant that arrives meanwhile is
    /// denied and must wait. This is the whole "a vehicle and a dog never occupy
    /// the same point on a crosswalk" rule, resolved by yielding rather than
    /// overlapping — and it is deliberately generic: it knows nothing about
    /// trucks or dogs, only opaque occupant identities, so every current and
    /// future vehicle shares one mechanism. The claim is keyed per crosswalk
    /// segment (not per whole route).
    ///
    /// That per-segment keying is the right STORAGE but the wrong unit of
    /// decision (#673). This type's doc used to argue the per-segment claim
    /// "generalizes for free to a vehicle that eventually turns across an
    /// intersection — each crosswalk span it drives over is one independent
    /// claim." It does not: an intersection's incoming and outgoing bands are
    /// not independent, because a vehicle that takes the first without being
    /// sure of the second strands itself between them. A vehicle therefore
    /// claims a whole <see cref="RoadManoeuvre"/> at a time, layered on the
    /// per-band <see cref="TryEnter"/>/<see cref="Exit"/> here — whose
    /// semantics are unchanged.
    ///
    /// Not thread-safe: the simulation ticks on one thread.
    /// </summary>
    public sealed class RoadCrossingGate
    {
        /// <summary>
        /// The one process-wide gate every vehicle and dog coordinates through
        /// (mirrors <see cref="Cameras.ModalInputGate.Shared"/>). A single
        /// neighborhood has a single set of crosswalks, so one shared gate is the
        /// whole coordination surface. Tests reset it with <see cref="Clear"/>.
        /// </summary>
        public static RoadCrossingGate Shared { get; } = new RoadCrossingGate();

        private readonly Dictionary<CrosswalkKey, object> claims =
            new Dictionary<CrosswalkKey, object>();

        /// <summary>
        /// Claims <paramref name="crosswalk"/> for <paramref name="occupant"/>.
        /// Returns true when the occupant now holds it — either because it was
        /// unclaimed (first-come) or because this same occupant already holds it
        /// (idempotent re-checks mid-crossing never self-lock). Returns false
        /// only when a DIFFERENT occupant currently holds the claim, in which
        /// case the caller must wait at its own boundary and retry.
        /// </summary>
        public bool TryEnter(WalkEdge crosswalk, object occupant)
        {
            if (occupant == null)
            {
                throw new ArgumentNullException(nameof(occupant));
            }

            var key = CrosswalkKey.For(crosswalk);
            if (claims.TryGetValue(key, out var holder))
            {
                return Equals(holder, occupant);
            }

            claims[key] = occupant;
            return true;
        }

        /// <summary>
        /// Releases <paramref name="occupant"/>'s claim on
        /// <paramref name="crosswalk"/> once it has fully cleared the far edge.
        /// A no-op when the occupant does not currently hold that crosswalk, so
        /// a stray Exit can never release another occupant's claim.
        /// </summary>
        public void Exit(WalkEdge crosswalk, object occupant)
        {
            var key = CrosswalkKey.For(crosswalk);
            if (claims.TryGetValue(key, out var holder) && Equals(holder, occupant))
            {
                claims.Remove(key);
            }
        }

        /// <summary>
        /// True when <paramref name="occupant"/> currently holds
        /// <paramref name="crosswalk"/>. A read-only probe with no claiming side
        /// effect, so an all-or-nothing multi-band acquire
        /// (<see cref="RoadManoeuvre.TryAcquire"/>) can tell a band it claimed
        /// in THIS attempt — and must unwind if the attempt fails — from one the
        /// occupant already legitimately held.
        /// </summary>
        public bool IsHeldBy(WalkEdge crosswalk, object occupant)
        {
            if (occupant == null)
            {
                return false;
            }

            return claims.TryGetValue(CrosswalkKey.For(crosswalk), out var holder)
                   && Equals(holder, occupant);
        }

        /// <summary>
        /// A total order over crosswalk identity — the same for every occupant,
        /// whichever direction it is driving. #673 needs it so two vehicles
        /// whose manoeuvres overlap always ATTEMPT the shared bands in the same
        /// sequence: one of them then wins outright, instead of the pair
        /// grabbing different halves, both rolling back, and retrying in
        /// lockstep forever. Ordered by the canonical (low, high) endpoint pair,
        /// so a vehicle reading an edge A-&gt;B and one reading it B-&gt;A sort
        /// it identically.
        /// </summary>
        public static int CompareClaimOrder(WalkEdge a, WalkEdge b)
        {
            return CrosswalkKey.For(a).CompareTo(CrosswalkKey.For(b));
        }

        /// <summary>Drops every claim. Used to reset <see cref="Shared"/> to a
        /// clean state between tests, exactly as the modal-input gate is.</summary>
        public void Clear()
        {
            claims.Clear();
        }

        /// <summary>
        /// Identity of a crosswalk as a gate key: its two endpoints, ordered
        /// canonically so the same physical crosswalk keys identically whether a
        /// vehicle reads its edge A-&gt;B or a dog reads it B-&gt;A. Kind and
        /// width are intentionally excluded — the two endpoints fully identify
        /// the span.
        /// </summary>
        private readonly struct CrosswalkKey : IEquatable<CrosswalkKey>, IComparable<CrosswalkKey>
        {
            private readonly GridPoint low;
            private readonly GridPoint high;

            private CrosswalkKey(GridPoint low, GridPoint high)
            {
                this.low = low;
                this.high = high;
            }

            public static CrosswalkKey For(WalkEdge edge)
            {
                return Precedes(edge.A, edge.B)
                    ? new CrosswalkKey(edge.A, edge.B)
                    : new CrosswalkKey(edge.B, edge.A);
            }

            private static bool Precedes(GridPoint a, GridPoint b)
            {
                if (a.X != b.X)
                {
                    return a.X < b.X;
                }

                return a.Z < b.Z;
            }

            public bool Equals(CrosswalkKey other)
            {
                return low.Equals(other.low) && high.Equals(other.high);
            }

            /// <summary>Lexicographic over the canonical endpoint pair — an
            /// arbitrary but STABLE order, which is all a lock ordering needs
            /// (#673).</summary>
            public int CompareTo(CrosswalkKey other)
            {
                var byLow = Compare(low, other.low);
                return byLow != 0 ? byLow : Compare(high, other.high);
            }

            private static int Compare(GridPoint a, GridPoint b)
            {
                var byX = a.X.CompareTo(b.X);
                return byX != 0 ? byX : a.Z.CompareTo(b.Z);
            }

            public override bool Equals(object obj)
            {
                return obj is CrosswalkKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                return (low.GetHashCode() * 397) ^ high.GetHashCode();
            }
        }
    }
}
