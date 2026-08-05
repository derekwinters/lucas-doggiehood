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
    /// segment (not per whole route), so it generalizes for free to a vehicle
    /// that eventually turns across an intersection — each crosswalk span it
    /// drives over is one independent claim.
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
        private readonly struct CrosswalkKey : IEquatable<CrosswalkKey>
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
