using System.Collections.Generic;

namespace Doggiehood.Core.World
{
    /// <summary>A buildable house position in the neighborhood (#7, #38).</summary>
    public sealed class HouseLot
    {
        public int HouseId { get; }
        public Quadrant Quadrant { get; }
        public GridPoint Position { get; }

        /// <summary>Whether this lot renders its backyard fence (#129,
        /// reshaped by #146). Defaults OFF since #146 — every lot's fence
        /// is defined but hidden until a future quest purchases it (#147).
        /// A lot with the flag off contributes no built fence, but its
        /// geometry stays queryable (see LotFence.GeometryFor).</summary>
        public bool HasFence { get; }

        /// <summary>#223: an optional per-lot MANUAL override of the
        /// auto-derived backyard fence geometry. Null (the default) leaves
        /// <see cref="LotFence.GeometryFor"/> computing the model-derived runs
        /// exactly as today — the no-override path is byte-for-byte the
        /// shipping behavior. When set, <see cref="LotFence.GeometryFor"/>
        /// returns these runs verbatim instead of the auto-derived geometry.
        /// The override changes fence SHAPE only, never visibility:
        /// <see cref="HasFence"/> (and the state-aware
        /// <see cref="LotFence.IsFenced"/>) still gate whether the runs build.
        /// The override is validated as a continuous open polyline (no gap)
        /// at construction, mirroring
        /// <see cref="LotFence.BackyardRuns(LotRect, HouseModel, GridPoint, GridPoint, float, IReadOnlyList{Road})"/>'s
        /// argument guards, so an override preserves the same continuous-runs
        /// invariant the auto-derived geometry guarantees. Coordinates with
        /// #147's per-lot property alignment: the override is authored data
        /// layered on top of the same geometry seam #147 tunes, so the two do
        /// not fight.</summary>
        public IReadOnlyList<FenceRun> FenceOverride { get; }

        /// <summary>#223: whether this lot carries a manual
        /// <see cref="FenceOverride"/>. False for every lot built without one
        /// — the shipping default.</summary>
        public bool HasFenceOverride => FenceOverride != null;

        public HouseLot(int houseId, Quadrant quadrant, GridPoint position, bool hasFence = false,
            IReadOnlyList<FenceRun> fenceOverride = null)
        {
            HouseId = houseId;
            Quadrant = quadrant;
            Position = position;
            HasFence = hasFence;

            if (fenceOverride != null)
            {
                LotFence.ValidateFenceOverride(fenceOverride);
                // Defensive copy so a validated override can't be mutated
                // out from under the invariant after construction.
                var copy = new FenceRun[fenceOverride.Count];
                for (var i = 0; i < fenceOverride.Count; i++)
                {
                    copy[i] = fenceOverride[i];
                }

                FenceOverride = copy;
            }
        }
    }
}
