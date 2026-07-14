using System;

namespace Doggiehood.Core.World
{
    /// <summary>
    /// Front-setback house placement (#127): where a house VISUAL actually
    /// stands on its lot. Lot centers (<see cref="HouseLot.Position"/>,
    /// ±14) stay untouched — they anchor the deferred expansion geometry —
    /// but the rendered house is pulled from the lot center toward the
    /// street it faces, so its front facade sits exactly
    /// <see cref="FrontSetback"/> from the sidewalk's OUTER edge
    /// (RoadWidth/2 + GrassVergeWidth + SidewalkWidth = 5.75m from the
    /// road centerline). The facade position comes from the #125 catalog
    /// (the model-local z = -FootprintZ/2 plane at the game's uniform
    /// scale), so the setback is exact per model.
    ///
    /// The facing rule moved here from WorldBuilder.HouseFrontFacing
    /// (which now delegates): squarely toward the road the lot's front
    /// walkway (#128 — it replaced the driveway stub) attaches to. The
    /// walkway runs door → sidewalk, exactly perpendicular to the street
    /// on this axis-aligned map (snapping to the dominant axis is just
    /// defensive). A lot with no walkway falls back to squarely facing
    /// the east-west road.
    ///
    /// The pure helpers (<see cref="FacingToward"/>, <see cref="PositionFor"/>,
    /// <see cref="ModelYawDegrees"/>) take explicit inputs and never read
    /// <see cref="NeighborhoodLayout.WalkNetwork"/> — WalkNetwork.BuildFrom
    /// calls them MID-BUILD to place each front door, so any network
    /// lookup here would recurse into the build.
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
        /// The ONE fixed uniform scale applied to every City Kit house
        /// model. Decision (Derek, 2026-07-14, #145): ×7, the kit-wide
        /// default unless a specific model gets a documented exception
        /// (none today). It replaced the 8m max-footprint normalization
        /// (#122/#128), which gave each model a different scale factor —
        /// houses weren't at the same scale, so doors read different
        /// sizes. ×8 was rejected: building-type-b would be 14.6m wide
        /// against the 15m fence square, failing #129's 0.5m margin
        /// guard; at ×7 it is 12.8m with 1.1m margin. Lives in Core
        /// because the walk network's front walkways start at each
        /// house's door, and the door's world position depends on this
        /// scale — WorldBuilder.HouseKitScale aliases it.
        /// </summary>
        public const float KitScale = 7f;

        /// <summary>
        /// Yaw correction applied after pointing a house model at its
        /// street-front facing: the City Kit Suburban models face model
        /// local -Z (Derek's #122 Editor screenshot evidence), so the
        /// look-at yaw needs a 180° flip. Moved into Core from WorldBuilder
        /// (#128) together with the uniform house scale (now
        /// <see cref="KitScale"/>) — the
        /// door position math needs it engine-free — and still a single
        /// constant (WorldBuilder aliases it) so one flip fixes all
        /// houses if it's ever still wrong.
        /// </summary>
        public const float ModelYawOffsetDegrees = 180f;

        /// <summary>
        /// The direction the house's front should face, as a unit cardinal
        /// direction on the ground plane: toward the sidewalk attach point
        /// of the lot's front walkway (see class docs).
        /// </summary>
        public static GridPoint FrontFacing(HouseLot lot)
        {
            if (NeighborhoodLayout.WalkNetwork.TryGetFrontWalkway(lot.HouseId, out var walkway))
            {
                // Door → sidewalk attach is exactly the street-ward
                // direction of the walkway.
                return FacingToward(walkway.A, walkway.B);
            }

            return new GridPoint(0f, -Math.Sign(lot.Position.Z));
        }

        /// <summary>The unit cardinal direction from <paramref name="from"/>
        /// toward <paramref name="toward"/>, snapped to the dominant axis.
        /// Pure — safe for WalkNetwork.BuildFrom to call mid-build.</summary>
        public static GridPoint FacingToward(GridPoint from, GridPoint toward)
        {
            var dx = toward.X - from.X;
            var dz = toward.Z - from.Z;
            return Math.Abs(dx) >= Math.Abs(dz)
                ? new GridPoint(Math.Sign(dx), 0f)
                : new GridPoint(0f, Math.Sign(dz));
        }

        /// <summary>
        /// The world yaw (Unity convention: degrees, clockwise from above)
        /// a kit house model gets for a given facing direction: look toward
        /// <paramref name="facing"/>, plus the art-side
        /// <see cref="ModelYawOffsetDegrees"/> correction. Feed it to
        /// <see cref="HouseModel.FrontDoorWorldPosition"/> to find the
        /// door. Pure geometry, no engine.
        /// </summary>
        public static float ModelYawDegrees(GridPoint facing)
        {
            return (float)(Math.Atan2(facing.X, facing.Z) * 180.0 / Math.PI) + ModelYawOffsetDegrees;
        }

        /// <summary>
        /// The house's world position at the given uniform scale
        /// (<see cref="KitScale"/> in the game — the same fixed scale
        /// WorldBuilder.BuildHouseModel and the #126 gallery apply, #145):
        /// the lot position shifted along the facing axis only, so the
        /// scaled front facade lands <see cref="FrontSetback"/> beyond
        /// the sidewalk's outer edge. A lot with no front walkway has no
        /// street to set back from and keeps its lot-center position.
        /// </summary>
        public static GridPoint Position(HouseLot lot, float uniformScale)
        {
            if (uniformScale <= 0f)
            {
                throw new ArgumentException("Uniform scale must be positive.", nameof(uniformScale));
            }

            if (!NeighborhoodLayout.WalkNetwork.TryGetFrontWalkway(lot.HouseId, out var walkway))
            {
                return lot.Position;
            }

            return PositionFor(lot, uniformScale, walkway.B);
        }

        /// <summary>
        /// Pure form of <see cref="Position"/> for a known sidewalk attach
        /// point (a point on the sidewalk CENTERLINE the lot connects to)
        /// — no network lookup, so WalkNetwork.BuildFrom can use it
        /// mid-build to place each house before its walkway edge exists.
        /// </summary>
        public static GridPoint PositionFor(HouseLot lot, float uniformScale, GridPoint sidewalkAttach)
        {
            if (uniformScale <= 0f)
            {
                throw new ArgumentException("Uniform scale must be positive.", nameof(uniformScale));
            }

            var model = HouseModelCatalog.ForHouse(lot.HouseId);
            var facadeHalfDepth = uniformScale * model.FootprintZ / 2f;

            // The attach point sits on the sidewalk CENTERLINE, so the
            // outer edge is half a sidewalk width back toward the lot, and
            // the house center sits FrontSetback + facade half-depth
            // further back still.
            var pullback = WorldDimensions.SidewalkWidth / 2f + FrontSetback + facadeHalfDepth;

            var facing = FacingToward(lot.Position, sidewalkAttach);
            return facing.X != 0f
                ? new GridPoint(sidewalkAttach.X - facing.X * pullback, lot.Position.Z)
                : new GridPoint(lot.Position.X, sidewalkAttach.Z - facing.Z * pullback);
        }
    }
}
