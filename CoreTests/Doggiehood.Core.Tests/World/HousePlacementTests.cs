using System;
using Doggiehood.Core.World;
using NUnit.Framework;

namespace Doggiehood.Core.Tests.World
{
    /// <summary>
    /// Front-setback house placement (#127): each house is moved from its
    /// lot center toward the street it faces, so its front facade sits
    /// exactly <see cref="HousePlacement.FrontSetback"/> from the
    /// sidewalk's OUTER edge. The lot center itself stays where it is —
    /// it anchors the deferred expansion geometry — only the house visual
    /// moves, and only along the facing axis. Since #128 the facing rule
    /// is keyed to the lot's front walkway (which replaced the driveway
    /// stub): squarely toward the road the walkway attaches to.
    /// </summary>
    public class HousePlacementTests
    {
        /// <summary>Same target the game uses (WorldBuilder.HouseTargetFootprint
        /// aliases this Core constant since #128).</summary>
        private const float TargetFootprint = HousePlacement.HouseTargetFootprint;

        private static WalkEdge FrontWalkwayFor(HouseLot lot)
        {
            Assert.That(NeighborhoodLayout.WalkNetwork.TryGetFrontWalkway(lot.HouseId, out var walkway),
                Is.True, $"house {lot.HouseId} has no front walkway");
            return walkway;
        }

        private static float SidewalkOuterEdgeOffset()
        {
            // Road centerline -> sidewalk outer edge: 3 + 0.75 + 2 = 5.75m.
            return WorldDimensions.RoadWidth / 2f
                + WorldDimensions.GrassVergeWidth
                + WorldDimensions.SidewalkWidth;
        }

        [Test]
        public void HouseTargetFootprint_IsTheGamesEightMeterTarget()
        {
            // Moved into Core from WorldBuilder (#128): WalkNetwork's
            // walkway construction needs the door position, which depends
            // on the game's uniform house scale — so the canonical target
            // lives engine-free and WorldBuilder aliases it.
            Assert.That(HousePlacement.HouseTargetFootprint, Is.EqualTo(8f));
        }

        [Test]
        public void FrontFacing_IsAUnitCardinalDirection_TowardTheWalkwayAttachPoint()
        {
            // The facing rule (#127, retargeted by #128): squarely toward
            // the road the lot's front walkway attaches to.
            foreach (var lot in NeighborhoodLayout.HouseLots)
            {
                var facing = HousePlacement.FrontFacing(lot);

                Assert.That(facing.X == 0f || facing.Z == 0f, Is.True,
                    $"house {lot.HouseId} facing {facing} is not cardinal");
                Assert.That(Math.Abs(facing.X) + Math.Abs(facing.Z), Is.EqualTo(1f).Within(0.0001f),
                    $"house {lot.HouseId} facing {facing} is not a unit direction");

                var attach = FrontWalkwayFor(lot).B;
                var towardAttach = facing.X != 0f
                    ? (attach.X - lot.Position.X) * facing.X
                    : (attach.Z - lot.Position.Z) * facing.Z;
                Assert.That(towardAttach, Is.GreaterThan(0f),
                    $"house {lot.HouseId} must face its walkway attach point {attach}");
            }
        }

        [Test]
        public void ModelYawDegrees_PlacesTheDoorAtItsAuthoredDepthAndLateralOffset()
        {
            // #128: the yaw the game applies to a kit model (look toward
            // the facing direction, plus the art-side 180° correction for
            // the kits' -Z-facing fronts) lives in Core, because the
            // walkway needs the door's world position engine-free. Fed to
            // FrontDoorWorldPosition, it must map the model-local -Z axis
            // onto the facing direction: the door's displacement from the
            // house position, measured ALONG facing, is the scaled authored
            // door depth (-FrontDoorLocalZ — recessed doors since gallery
            // pass 1, no longer the facade half-depth), and ACROSS facing
            // it is the scaled authored lateral offset.
            foreach (var lot in NeighborhoodLayout.HouseLots)
            {
                var model = HouseModelCatalog.ForHouse(lot.HouseId);
                var scale = TargetFootprint / model.MaxFootprint;
                var facing = HousePlacement.FrontFacing(lot);
                var position = HousePlacement.Position(lot, TargetFootprint);

                var door = model.FrontDoorWorldPosition(
                    position, HousePlacement.ModelYawDegrees(facing), scale);

                var dx = door.X - position.X;
                var dz = door.Z - position.Z;
                var alongFacing = dx * facing.X + dz * facing.Z;
                var acrossFacing = dx * facing.Z - dz * facing.X;

                Assert.That(alongFacing,
                    Is.EqualTo(scale * -model.FrontDoorLocalZ).Within(0.001f),
                    $"house {lot.HouseId} door depth along its facing direction");
                Assert.That(Math.Abs(acrossFacing),
                    Is.EqualTo(scale * Math.Abs(model.FrontDoorLocalX)).Within(0.001f),
                    $"house {lot.HouseId} door lateral offset across its facing direction");
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
        public void Position_LeavesTheLotCenterUntouched_AndTheWalkwayEndsAtTheDoor()
        {
            // The lot center still anchors the deferred expansion geometry
            // — computing the setback position must not move it. The walk
            // network anchor, though, moved with #128: the lot-side node
            // of the lot's connection IS the front door now (decision on
            // #128 — the old stub's lot-center node is gone).
            foreach (var lot in NeighborhoodLayout.HouseLots)
            {
                HousePlacement.Position(lot, TargetFootprint);

                Assert.That(Math.Abs(lot.Position.X),
                    Is.EqualTo(NeighborhoodLayout.LotDistanceFromCenter).Within(0.0001f));
                Assert.That(Math.Abs(lot.Position.Z),
                    Is.EqualTo(NeighborhoodLayout.LotDistanceFromCenter).Within(0.0001f));

                var walkway = FrontWalkwayFor(lot);
                var model = HouseModelCatalog.ForHouse(lot.HouseId);
                var scale = TargetFootprint / model.MaxFootprint;
                var door = model.FrontDoorWorldPosition(
                    HousePlacement.Position(lot, TargetFootprint),
                    HousePlacement.ModelYawDegrees(HousePlacement.FrontFacing(lot)),
                    scale);

                Assert.That(walkway.A.X, Is.EqualTo(door.X).Within(0.001f),
                    $"house {lot.HouseId}'s walkway lot-side node must be its front door (X)");
                Assert.That(walkway.A.Z, Is.EqualTo(door.Z).Within(0.001f),
                    $"house {lot.HouseId}'s walkway lot-side node must be its front door (Z)");
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
