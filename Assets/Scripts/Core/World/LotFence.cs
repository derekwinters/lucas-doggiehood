using System;
using System.Collections.Generic;

namespace Doggiehood.Core.World
{
    /// <summary>One straight fence line (#129, reshaped by #146): a
    /// segment of a lot's backyard fence, on the ground plane. Purely
    /// geometric — the tiling into kit pieces is
    /// <see cref="FenceTiling"/>'s job.</summary>
    public readonly struct FenceRun
    {
        public GridPoint A { get; }
        public GridPoint B { get; }

        public FenceRun(GridPoint a, GridPoint b)
        {
            A = a;
            B = b;
        }

        /// <summary>Straight-line length of the run.</summary>
        public float Length
        {
            get
            {
                var dx = A.X - B.X;
                var dz = A.Z - B.Z;
                return (float)Math.Sqrt(dx * dx + dz * dz);
            }
        }
    }

    /// <summary>
    /// Per-lot backyard fence geometry (#146, replacing #129's boundary
    /// square with a gate gap): three continuous runs anchored at the
    /// midpoint of each side wall of the HOUSE, wrapping around the back
    /// yard only. The front yard stays open — no fence line ever crosses
    /// the front, so the walkway (#128, door → sidewalk) needs no gate.
    /// The fence rotates with the house facing: side anchors sit
    /// ±(scaled FootprintX / 2) perpendicular to
    /// <see cref="HousePlacement.FrontFacing"/> at the house's depth
    /// midpoint, and the rear line sits
    /// <see cref="RearBoundaryFromLotCenter"/> behind the lot center, away
    /// from the faced street.
    ///
    /// Fences are defined for every lot but HIDDEN by default
    /// (<see cref="HouseLot.HasFence"/> defaults false since #146; a
    /// future quest, #147, purchases them). The flag-respecting
    /// <see cref="RunsFor"/> is what WorldBuilder consumes — empty while
    /// hidden — while <see cref="GeometryFor"/> keeps the geometry
    /// queryable for a disabled lot.
    /// </summary>
    public static class LotFence
    {
        /// <summary>
        /// Distance from the lot center to the rear fence line, in meters
        /// away from the street the house faces. Decision (#146,
        /// 2026-07-14): reuses #129's 7.5m boundary half-extent for the
        /// REAR ONLY — it stays inside the starting tile quadrant
        /// (14 + 7.5 &lt; 30) and clears every model's setback-shifted rear
        /// wall at the fixed ×7 kit scale (#145) by at least 3m (the
        /// deepest model, building-type-m at 10.00m, keeps 3.0m;
        /// test-enforced at ≥ 0.5m). Exact alignment with property layouts
        /// is deferred to #147.
        /// </summary>
        public const float RearBoundaryFromLotCenter = 7.5f;

        private const float Epsilon = 0.001f;

        /// <summary>
        /// The rear fence line's distance behind the scaled FRONT FACADE
        /// plane — the house-relative form of
        /// <see cref="RearBoundaryFromLotCenter"/>, and the one the #126
        /// gallery (which has no lots or streets) feeds to
        /// <see cref="BackyardRuns"/>. Derived, identical for every lot
        /// and model: the #127 setback puts every facade
        /// LotDistanceFromCenter − sidewalk outer edge − FrontSetback =
        /// 5.5m street-ward of its lot center, so the rear line lands
        /// 5.5 + 7.5 = 13m behind the facade.
        /// </summary>
        public static float RearLineBehindFacade
        {
            get
            {
                var sidewalkOuterEdge = WorldDimensions.RoadWidth / 2f
                    + WorldDimensions.GrassVergeWidth + WorldDimensions.SidewalkWidth;
                return NeighborhoodLayout.LotDistanceFromCenter - sidewalkOuterEdge
                    - HousePlacement.FrontSetback + RearBoundaryFromLotCenter;
            }
        }

        /// <summary>
        /// The fence lines <paramref name="lot"/> contributes to the built
        /// world: empty while the lot's fence is hidden
        /// (<see cref="HouseLot.HasFence"/> off — every starting lot's
        /// default), otherwise <see cref="GeometryFor"/>.
        /// </summary>
        public static IReadOnlyList<FenceRun> RunsFor(HouseLot lot)
        {
            return lot.HasFence ? GeometryFor(lot) : Array.Empty<FenceRun>();
        }

        /// <summary>
        /// The lot's backyard fence geometry regardless of the
        /// <see cref="HouseLot.HasFence"/> flag — queryable for a disabled
        /// lot (the #147 purchase flow needs to describe what it sells).
        /// Reads <see cref="NeighborhoodLayout.WalkNetwork"/> via
        /// HousePlacement, like #129's version did; it is only ever called
        /// after the network is built.
        /// </summary>
        public static IReadOnlyList<FenceRun> GeometryFor(HouseLot lot)
        {
            var model = HouseModelCatalog.ForHouse(lot.HouseId);
            var facing = HousePlacement.FrontFacing(lot);
            var position = HousePlacement.Position(lot, HousePlacement.KitScale);
            return BackyardRuns(model, position, facing, HousePlacement.KitScale, RearLineBehindFacade);
        }

        /// <summary>
        /// Pure form (no network lookup — the #126 gallery reuses it with
        /// its own placement): the three backyard fence runs for a house
        /// model at <paramref name="housePosition"/> facing the unit
        /// cardinal <paramref name="facing"/> at
        /// <paramref name="uniformScale"/>, with the rear line
        /// <paramref name="rearLineBehindFacade"/> meters behind the scaled
        /// front facade plane. Runs chain side anchor → rear corner → rear
        /// corner → side anchor, continuous with no gap.
        /// </summary>
        public static IReadOnlyList<FenceRun> BackyardRuns(HouseModel model, GridPoint housePosition,
            GridPoint facing, float uniformScale, float rearLineBehindFacade)
        {
            if (uniformScale <= 0f)
            {
                throw new ArgumentException("Uniform scale must be positive.", nameof(uniformScale));
            }

            var halfWidth = uniformScale * model.FootprintX / 2f;
            var halfDepth = uniformScale * model.FootprintZ / 2f;

            // The facade sits halfDepth in FRONT of the house center, so
            // the rear line sits (rearLineBehindFacade - halfDepth) behind
            // the center along the facing axis.
            var rearBehindCenter = rearLineBehindFacade - halfDepth;
            if (rearBehindCenter <= Epsilon)
            {
                throw new ArgumentException(
                    "Rear line must sit behind the house center.", nameof(rearLineBehindFacade));
            }

            // Perpendicular to the facing on the ground plane; the side
            // walls run along the facing (depth) axis at ±halfWidth.
            var perp = new GridPoint(-facing.Z, facing.X);

            var sideA = new GridPoint(
                housePosition.X + perp.X * halfWidth, housePosition.Z + perp.Z * halfWidth);
            var sideB = new GridPoint(
                housePosition.X - perp.X * halfWidth, housePosition.Z - perp.Z * halfWidth);
            var rearA = new GridPoint(
                sideA.X - facing.X * rearBehindCenter, sideA.Z - facing.Z * rearBehindCenter);
            var rearB = new GridPoint(
                sideB.X - facing.X * rearBehindCenter, sideB.Z - facing.Z * rearBehindCenter);

            return new[]
            {
                new FenceRun(sideA, rearA),
                new FenceRun(rearA, rearB),
                new FenceRun(rearB, sideB),
            };
        }
    }
}
