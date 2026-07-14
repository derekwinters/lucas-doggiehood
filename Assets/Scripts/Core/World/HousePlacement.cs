using System;

namespace Doggiehood.Core.World
{
    /// <summary>
    /// Front-setback house placement (#127): where a house VISUAL actually
    /// stands on its lot. Lot centers (<see cref="HouseLot.Position"/>,
    /// ±14) stay untouched — they anchor the walk network's driveway stubs
    /// and the deferred expansion geometry — but the rendered house is
    /// pulled from the lot center toward the street it faces, so its front
    /// facade sits exactly <see cref="FrontSetback"/> from the sidewalk's
    /// OUTER edge (RoadWidth/2 + GrassVergeWidth + SidewalkWidth = 5.75m
    /// from the road centerline). The facade position comes from the #125
    /// catalog (the model-local z = -FootprintZ/2 plane at the game's
    /// uniform scale), so the setback is exact per model.
    ///
    /// The facing rule moved here from WorldBuilder.HouseFrontFacing
    /// (which now delegates): squarely toward the road the lot's driveway
    /// stub connects to — the stub's far endpoint is the sidewalk attach
    /// point, and this map's axis-aligned roads make the stub exactly
    /// cardinal (snapping to the dominant axis is just defensive). A lot
    /// with no stub falls back to squarely facing the east-west road.
    /// </summary>
    public static class HousePlacement
    {
        /// <summary>
        /// Distance from the sidewalk's outer edge to the house's front
        /// facade, in meters. Decision (#127, 2026-07-13): Derek asked for
        /// houses "moved toward their street" without pinning the number;
        /// the agreed sensible range is 2.5-3.5m (at the old lot-centered
        /// placement the facades sat ~4.25m out). 2.75m is the first-pass
        /// pick — toward the near end of the range so the move reads
        /// clearly, while leaving a front yard for the #128 walkways.
        /// Derek tunes it visually in the Editor check afterwards.
        /// </summary>
        public const float FrontSetback = 2.75f;

        /// <summary>
        /// The direction the house's front should face, as a unit cardinal
        /// direction on the ground plane: toward the sidewalk attach point
        /// of the lot's driveway stub (see class docs).
        /// </summary>
        public static GridPoint FrontFacing(HouseLot lot)
        {
            if (TryGetDrivewayAttachPoint(lot, out var attach))
            {
                var dx = attach.X - lot.Position.X;
                var dz = attach.Z - lot.Position.Z;
                return Math.Abs(dx) >= Math.Abs(dz)
                    ? new GridPoint(Math.Sign(dx), 0f)
                    : new GridPoint(0f, Math.Sign(dz));
            }

            return new GridPoint(0f, -Math.Sign(lot.Position.Z));
        }

        /// <summary>
        /// The house's world position for the game's uniform scaling rule
        /// (scale = <paramref name="targetFootprint"/> / MaxFootprint —
        /// the same rule WorldBuilder.BuildHouseModel and the #126 gallery
        /// apply): the lot position shifted along the facing axis only, so
        /// the scaled front facade lands <see cref="FrontSetback"/> beyond
        /// the sidewalk's outer edge. A lot with no driveway stub has no
        /// street to set back from and keeps its lot-center position.
        /// </summary>
        public static GridPoint Position(HouseLot lot, float targetFootprint)
        {
            if (targetFootprint <= 0f)
            {
                throw new ArgumentException("Target footprint must be positive.", nameof(targetFootprint));
            }

            if (!TryGetDrivewayAttachPoint(lot, out var attach))
            {
                return lot.Position;
            }

            var model = HouseModelCatalog.ForHouse(lot.HouseId);
            var scale = targetFootprint / model.MaxFootprint;
            var facadeHalfDepth = scale * model.FootprintZ / 2f;

            // The attach point sits on the sidewalk CENTERLINE, so the
            // outer edge is half a sidewalk width back toward the lot, and
            // the house center sits FrontSetback + facade half-depth
            // further back still.
            var pullback = WorldDimensions.SidewalkWidth / 2f + FrontSetback + facadeHalfDepth;

            var facing = FrontFacing(lot);
            return facing.X != 0f
                ? new GridPoint(attach.X - facing.X * pullback, lot.Position.Z)
                : new GridPoint(lot.Position.X, attach.Z - facing.Z * pullback);
        }

        private static bool TryGetDrivewayAttachPoint(HouseLot lot, out GridPoint attach)
        {
            foreach (var edge in NeighborhoodLayout.WalkNetwork.Edges)
            {
                if (edge.Kind != WalkEdgeKind.DrivewayStub
                    || (!edge.A.Equals(lot.Position) && !edge.B.Equals(lot.Position)))
                {
                    continue;
                }

                attach = edge.Other(lot.Position);
                return true;
            }

            attach = lot.Position;
            return false;
        }
    }
}
