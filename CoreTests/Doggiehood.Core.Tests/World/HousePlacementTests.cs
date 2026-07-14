using System;
using System.Linq;
using Doggiehood.Core.World;
using NUnit.Framework;

namespace Doggiehood.Core.Tests.World
{
    /// <summary>
    /// Front-setback house placement (#127): each house is moved from its
    /// lot center toward the street it faces, so its front facade sits
    /// exactly <see cref="HousePlacement.FrontSetback"/> from the
    /// sidewalk's OUTER edge. The lot center itself stays where it is —
    /// it anchors the walk network's driveway stub and the deferred
    /// expansion geometry — only the house visual moves, and only along
    /// the facing axis.
    /// </summary>
    public class HousePlacementTests
    {
        /// <summary>Same target the game uses (WorldBuilder.HouseTargetFootprint).</summary>
        private const float TargetFootprint = 8f;

        private static WalkEdge DrivewayStubFor(HouseLot lot)
        {
            return NeighborhoodLayout.WalkNetwork.Edges.Single(e =>
                e.Kind == WalkEdgeKind.DrivewayStub
                && (e.A.Equals(lot.Position) || e.B.Equals(lot.Position)));
        }

        private static float SidewalkOuterEdgeOffset()
        {
            // Road centerline -> sidewalk outer edge: 3 + 0.75 + 2 = 5.75m.
            return WorldDimensions.RoadWidth / 2f
                + WorldDimensions.GrassVergeWidth
                + WorldDimensions.SidewalkWidth;
        }

        [Test]
        public void FrontFacing_IsAUnitCardinalDirection_TowardTheDrivewayAttachPoint()
        {
            // The facing rule moves into Core from WorldBuilder (#127
            // needs it for the setback math): squarely toward the road the
            // lot's driveway stub connects to.
            foreach (var lot in NeighborhoodLayout.HouseLots)
            {
                var facing = HousePlacement.FrontFacing(lot);

                Assert.That(facing.X == 0f || facing.Z == 0f, Is.True,
                    $"house {lot.HouseId} facing {facing} is not cardinal");
                Assert.That(Math.Abs(facing.X) + Math.Abs(facing.Z), Is.EqualTo(1f).Within(0.0001f),
                    $"house {lot.HouseId} facing {facing} is not a unit direction");

                var attach = DrivewayStubFor(lot).Other(lot.Position);
                var towardAttach = facing.X != 0f
                    ? (attach.X - lot.Position.X) * facing.X
                    : (attach.Z - lot.Position.Z) * facing.Z;
                Assert.That(towardAttach, Is.GreaterThan(0f),
                    $"house {lot.HouseId} must face its driveway attach point {attach}");
            }
        }

        [Test]
        public void Position_PutsTheScaledFrontFacade_ExactlyFrontSetbackFromTheSidewalkOuterEdge()
        {
            // The front facade is the model-local z = -FootprintZ/2 plane
            // (HouseModel), so at the game's uniform scale it sits
            // scale * FootprintZ / 2 in front of the house position. Both
            // roads' centerlines pass through the intersection at the
            // origin, so the facade's distance from its facing road is
            // just its coordinate magnitude along the facing axis.
            foreach (var lot in NeighborhoodLayout.HouseLots)
            {
                var model = HouseModelCatalog.ForHouse(lot.HouseId);
                var scale = TargetFootprint / model.MaxFootprint;
                var facadeHalfDepth = scale * model.FootprintZ / 2f;

                var facing = HousePlacement.FrontFacing(lot);
                var position = HousePlacement.Position(lot, TargetFootprint);

                var facadeCoordinate = facing.X != 0f
                    ? position.X + facing.X * facadeHalfDepth
                    : position.Z + facing.Z * facadeHalfDepth;

                Assert.That(Math.Abs(facadeCoordinate),
                    Is.EqualTo(SidewalkOuterEdgeOffset() + HousePlacement.FrontSetback).Within(0.0001f),
                    $"house {lot.HouseId} facade must sit FrontSetback beyond the sidewalk outer edge");
            }
        }

        [Test]
        public void Position_ShiftsOnlyAlongTheFacingAxis_LateralCoordinateUnchanged()
        {
            foreach (var lot in NeighborhoodLayout.HouseLots)
            {
                var facing = HousePlacement.FrontFacing(lot);
                var position = HousePlacement.Position(lot, TargetFootprint);

                if (facing.X != 0f)
                {
                    Assert.That(position.Z, Is.EqualTo(lot.Position.Z).Within(0.0001f),
                        $"house {lot.HouseId} must not move laterally (Z)");
                }
                else
                {
                    Assert.That(position.X, Is.EqualTo(lot.Position.X).Within(0.0001f),
                        $"house {lot.HouseId} must not move laterally (X)");
                }
            }
        }

        [Test]
        public void Position_MovesTheHouseTowardItsStreet_StayingOnItsOwnSide()
        {
            foreach (var lot in NeighborhoodLayout.HouseLots)
            {
                var facing = HousePlacement.FrontFacing(lot);
                var position = HousePlacement.Position(lot, TargetFootprint);

                var lotCoordinate = facing.X != 0f ? lot.Position.X : lot.Position.Z;
                var houseCoordinate = facing.X != 0f ? position.X : position.Z;

                Assert.That(Math.Sign(houseCoordinate), Is.EqualTo(Math.Sign(lotCoordinate)),
                    $"house {lot.HouseId} must stay on its own side of the street");
                Assert.That(Math.Abs(houseCoordinate), Is.LessThan(Math.Abs(lotCoordinate)),
                    $"house {lot.HouseId} must move toward the street, not away");
            }
        }

        [Test]
        public void Position_LeavesTheLotCenterAndItsWalkNetworkAnchorsUntouched()
        {
            // The lot center is the anchor for the driveway stub (and the
            // deferred expansion geometry) — computing the setback position
            // must not move it.
            foreach (var lot in NeighborhoodLayout.HouseLots)
            {
                HousePlacement.Position(lot, TargetFootprint);

                Assert.That(Math.Abs(lot.Position.X),
                    Is.EqualTo(NeighborhoodLayout.LotDistanceFromCenter).Within(0.0001f));
                Assert.That(Math.Abs(lot.Position.Z),
                    Is.EqualTo(NeighborhoodLayout.LotDistanceFromCenter).Within(0.0001f));

                // The stub still attaches at the lot center itself.
                Assert.That(() => DrivewayStubFor(lot), Throws.Nothing,
                    $"house {lot.HouseId} lost its lot-center driveway stub anchor");
            }
        }

        [Test]
        public void FrontSetback_SitsInDereksAgreedTuningRange()
        {
            // #127 left the exact number to be tuned visually; the agreed
            // starting range is 2.5-3.5m from the sidewalk's outer edge
            // (Derek, 2026-07-13). This pins the constant to that range so
            // a retune outside it is a conscious decision, not a typo.
            Assert.That(HousePlacement.FrontSetback, Is.InRange(2.5f, 3.5f));
        }
    }
}
