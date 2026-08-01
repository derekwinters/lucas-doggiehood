using System;
using Doggiehood.Core.Art;
using Doggiehood.Core.Expansion;
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
        /// <summary>Same fixed kit scale the game uses (WorldBuilder.HouseKitScale
        /// aliases this Core constant since #145).</summary>
        private const float KitScale = HousePlacement.KitScale;

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
        public void KitScale_IsTheFixedTimesSevenScale_AppliedToEveryCityKitHouseModel()
        {
            // Decision (Derek, 2026-07-14, #145): ONE fixed uniform scale
            // for ALL City Kit house models — ×7 — replacing the old 8m
            // max-footprint normalization, which gave each model a
            // different scale factor so houses (and their doors) read at
            // different sizes. ×8 was rejected: building-type-b would be
            // 14.6m wide against the 15m fence square, failing #129's
            // 0.5m margin guard; at ×7 it is 12.8m with 1.1m margin.
            Assert.That(HousePlacement.KitScale, Is.EqualTo(7f));
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
                var scale = KitScale;
                var facing = HousePlacement.FrontFacing(lot);
                var position = HousePlacement.Position(lot, KitScale);

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
                var facadeHalfDepth = KitScale * model.FootprintZ / 2f;

                var facing = HousePlacement.FrontFacing(lot);
                var position = HousePlacement.Position(lot, KitScale);

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
                var position = HousePlacement.Position(lot, KitScale);

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
                var position = HousePlacement.Position(lot, KitScale);

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
                HousePlacement.Position(lot, KitScale);

                Assert.That(Math.Abs(lot.Position.X),
                    Is.EqualTo(NeighborhoodLayout.LotDistanceFromCenter).Within(0.0001f));
                Assert.That(Math.Abs(lot.Position.Z),
                    Is.EqualTo(NeighborhoodLayout.LotDistanceFromCenter).Within(0.0001f));

                var walkway = FrontWalkwayFor(lot);
                var model = HouseModelCatalog.ForHouse(lot.HouseId);
                var scale = KitScale;
                var door = model.FrontDoorWorldPosition(
                    HousePlacement.Position(lot, KitScale),
                    HousePlacement.ModelYawDegrees(HousePlacement.FrontFacing(lot)),
                    scale);

                Assert.That(walkway.A.X, Is.EqualTo(door.X).Within(0.001f),
                    $"house {lot.HouseId}'s walkway lot-side node must be its front door (X)");
                Assert.That(walkway.A.Z, Is.EqualTo(door.Z).Within(0.001f),
                    $"house {lot.HouseId}'s walkway lot-side node must be its front door (Z)");
            }
        }

        [Test]
        public void ZoneLot_FootprintAndWalkwayPlacement_DoNotThrow()
        {
            // #414: once WalkNetwork spans zone tiles (#398 rebuilds it from
            // GameState.Map), a zone lot (id >= 5) gets a front-walkway edge
            // and the placement/footprint call sites reach
            // HouseModelCatalog.ForHouse(zoneId) — which used to throw through
            // HouseStyleTable. Simulate that future state with a hand-built
            // WalkNetwork containing a zone lot (does NOT require #398 merged):
            // WalkNetwork.BuildFrom itself resolves each lot's model via
            // ForHouse while attaching its front walkway, so building the
            // network over a zone lot is the chokepoint under test.
            var zoneLot = new HouseLot(
                HouseVariantAssignment.FirstZoneHouseId, Quadrant.NorthEast,
                new GridPoint(NeighborhoodLayout.LotDistanceFromCenter,
                    NeighborhoodLayout.LotDistanceFromCenter));

            WalkNetwork network = null;
            Assert.That(
                () => network = WalkNetwork.BuildFrom(NeighborhoodLayout.Roads, new[] { zoneLot }),
                Throws.Nothing,
                "building a walk network over a zone lot must not throw through ForHouse");
            Assert.That(network.TryGetFrontWalkway(zoneLot.HouseId, out var walkway), Is.True,
                "the hand-built network must attach a front walkway to the zone lot");

            // The walkway-dependent placement call site (PositionFor, reached
            // only when a front-walkway attach point exists) must resolve the
            // zone house model instead of throwing.
            Assert.That(() => HousePlacement.PositionFor(zoneLot, KitScale, walkway.B), Throws.Nothing);

            // The footprint call site (shared by yard trees #170 and quest
            // hidden-item placement #290) must resolve it too.
            Assert.That(() => HousePlacement.HouseFootprint(zoneLot), Throws.Nothing);
        }

        [Test]
        public void FrontFacing_WithAZoneSpanningNetwork_UsesTheWalkwayFacing_NotTheZSignFallback()
        {
            // #405: FrontFacing keys the house's orientation off the lot's
            // front walkway, but for a zone lot (id >= 5) the starting-tile
            // NeighborhoodLayout.WalkNetwork has no such edge, so the single-arg
            // overload falls back to the crude Z-sign guess. Given a network
            // that DOES span the zone (the #398 map-derived graph shape,
            // hand-built here so the test needs no live GameState), the
            // network-aware overload must return the real street-ward facing
            // the walkway attaches to.
            //
            // The lot sits far out along +X but well within the north-south
            // road's span, so its nearest sidewalk is that NS road's east
            // sidewalk: the walkway-derived facing is along X (-1, 0), which
            // can never coincide with the Z-sign fallback (0, -1) — making the
            // "walkway facing, not the fallback" distinction unambiguous.
            var zoneLot = new HouseLot(
                HouseVariantAssignment.FirstZoneHouseId, Quadrant.NorthEast,
                new GridPoint(NeighborhoodLayout.LotDistanceFromCenter, 20f));

            var network = WalkNetwork.BuildFrom(NeighborhoodLayout.Roads, new[] { zoneLot });
            Assert.That(network.TryGetFrontWalkway(zoneLot.HouseId, out var walkway), Is.True,
                "the zone-spanning network must attach a front walkway to the zone lot");

            var walkwayFacing = HousePlacement.FacingToward(walkway.A, walkway.B);
            var zSignFallback = new GridPoint(0f, -Math.Sign(zoneLot.Position.Z));
            Assert.That(walkwayFacing, Is.Not.EqualTo(zSignFallback),
                "this lot is chosen so the walkway facing differs from the fallback");

            Assert.That(HousePlacement.FrontFacing(zoneLot, network), Is.EqualTo(walkwayFacing),
                "the network-aware FrontFacing must use the walkway-derived facing");

            // And the single-arg overload (starting-tile network only) still
            // gives the fallback for a zone lot — the overload is what changes.
            Assert.That(HousePlacement.FrontFacing(zoneLot), Is.EqualTo(zSignFallback),
                "without a zone-spanning network the zone lot falls back to the Z-sign guess");
        }

        [Test]
        public void HouseFootprint_NetworkOverload_ResolvesFacingAndPositionFromTheNetwork_NotTheSingleton()
        {
            // #461: fence/tree geometry needs the house's REAL orientation for a
            // zone lot, so HouseFootprint gains a network-aware overload. For a
            // zone lot placed within the NS road's span, the walkway-derived
            // facing is along X and the setback position sits off the lot centre —
            // both taken from the passed network — whereas the single-arg overload
            // falls back to the Z-sign facing and the un-set-back lot centre.
            var zoneLot = new HouseLot(
                HouseVariantAssignment.FirstZoneHouseId, Quadrant.NorthEast,
                new GridPoint(NeighborhoodLayout.LotDistanceFromCenter, 20f));

            var network = WalkNetwork.BuildFrom(NeighborhoodLayout.Roads, new[] { zoneLot });
            var networkFacing = HousePlacement.FrontFacing(zoneLot, network);
            Assert.That(networkFacing.X, Is.Not.EqualTo(0f),
                "sanity: this zone lot's real facing is along X, differing from the Z-sign guess");

            var networkFootprint = HousePlacement.HouseFootprint(zoneLot, network);
            var networkPosition = HousePlacement.Position(zoneLot, KitScale, network);
            Assert.That(networkFootprint.Center.X, Is.EqualTo(networkPosition.X).Within(0.001f),
                "the network footprint is centred on the network-resolved setback position (X)");
            Assert.That(networkFootprint.Center.Z, Is.EqualTo(networkPosition.Z).Within(0.001f),
                "the network footprint is centred on the network-resolved setback position (Z)");
            Assert.That(networkFootprint.Center.X, Is.Not.EqualTo(zoneLot.Position.X).Within(0.001f),
                "the network footprint is NOT centred on the lot centre — it resolves position from the network");

            // The single-arg overload keeps the old singleton behaviour: the
            // Z-sign facing and the un-set-back lot centre.
            var singletonFootprint = HousePlacement.HouseFootprint(zoneLot);
            Assert.That(singletonFootprint.Center.X, Is.EqualTo(zoneLot.Position.X).Within(0.001f),
                "the single-arg footprint stays centred on the lot centre for a zone lot");
        }

        [Test]
        public void PredeterminedFrontFacing_AtUnlock_MatchesTheActualBuiltFacing_AndDiffersFromTheZSignGuess()
        {
            // #461: a zone lot's trees are pre-baked at UNLOCK time, before its
            // house (and so its front-walkway edge) exists. The predetermined
            // facing — resolved by projecting the lot centre onto its nearest
            // sidewalk in the map-spanning network — must equal the facing the
            // house actually gets once built (FrontFacing(lot, network) with the
            // walkway present), or #434's "never regenerate trees on build"
            // assumption breaks. It must also differ from the crude Z-sign guess,
            // proving the fix is meaningful for this lot.
            var state = GameState.CreateNew();
            state.Wallet.Deposit(1_000_000);
            Assert.That(state.TryUnlockNextZone(), Is.True, "the first zone unlocks");

            var lot = ZoneCatalog.FirstZone.Lots[0];
            var zSignGuess = new GridPoint(0f, -Math.Sign(lot.Position.Z));

            // At unlock the lot has no built house / walkway edge yet.
            Assert.That(state.WalkNetwork.TryGetFrontWalkway(lot.HouseId, out _), Is.False,
                "precondition: no front walkway exists for the unbuilt zone lot");
            var predetermined = HousePlacement.PredeterminedFrontFacing(lot, state.WalkNetwork);
            Assert.That(predetermined, Is.Not.EqualTo(zSignGuess),
                "the predetermined facing must be the real street-ward facing, not the Z-sign guess");

            // Build the house; the walkway now exists and FrontFacing resolves it.
            Assert.That(state.TryBuildHouse(lot.HouseId), Is.True, "the zone house builds");
            Assert.That(state.WalkNetwork.TryGetFrontWalkway(lot.HouseId, out _), Is.True,
                "the built house grows its front walkway");
            var actual = HousePlacement.FrontFacing(lot, state.WalkNetwork);

            Assert.That(predetermined, Is.EqualTo(actual),
                "predetermined-at-unlock facing must equal the actual built walkway facing");
        }

        [Test]
        public void Position_AtLevelTwo_KeepsTheCurrentLevelsFacade_ExactlyFrontSetbackFromTheSidewalk()
        {
            // #454: the front-setback pivot must be calibrated to the house's
            // CURRENT level's mesh, not always level 1. At level 2 the current
            // level's facade (its own -FootprintZ/2 plane) must still sit exactly
            // FrontSetback beyond the sidewalk outer edge — the bug was that the
            // level-2 mesh sat at the level-1-calibrated pivot, so it drifted.
            const int level = 2;
            foreach (var lot in NeighborhoodLayout.HouseLots)
            {
                var model = HouseModelCatalog.ForHouse(lot.HouseId, level);
                var facadeHalfDepth = KitScale * model.FootprintZ / 2f;

                var facing = HousePlacement.FrontFacing(lot);
                var position = HousePlacement.Position(lot, KitScale, level);

                var facadeCoordinate = facing.X != 0f
                    ? position.X + facing.X * facadeHalfDepth
                    : position.Z + facing.Z * facadeHalfDepth;

                Assert.That(Math.Abs(facadeCoordinate),
                    Is.EqualTo(SidewalkOuterEdgeOffset() + HousePlacement.FrontSetback).Within(0.0001f),
                    $"house {lot.HouseId} level-2 facade must sit FrontSetback beyond the sidewalk outer edge");

                // Where the level-2 footprint depth differs from level 1, the
                // pivot must actually move (it no longer sits at the L1 pivot).
                var levelOneModel = HouseModelCatalog.ForHouse(lot.HouseId, HouseLevelModelTable.MinLevel);
                if (Math.Abs(model.FootprintZ - levelOneModel.FootprintZ) > 0.0001f)
                {
                    var levelOne = HousePlacement.Position(lot, KitScale, HouseLevelModelTable.MinLevel);
                    Assert.That(position, Is.Not.EqualTo(levelOne),
                        $"house {lot.HouseId} level-2 pivot must move when the footprint depth changes");
                }
            }
        }

        [Test]
        public void HouseFootprint_AtLevelTwo_IsSizedToTheCurrentLevelModel_NotLevelOne()
        {
            // #454: the shared footprint rect (yard trees #170, quest hidden
            // items #290, empty-lot marker #434) must be sized to the house's
            // CURRENT level's mesh, not always level 1.
            const int level = 2;
            foreach (var lot in NeighborhoodLayout.HouseLots)
            {
                var model = HouseModelCatalog.ForHouse(lot.HouseId, level);
                var facing = HousePlacement.FrontFacing(lot);
                var footprint = HousePlacement.HouseFootprint(lot, level);

                var expectedWidth = KitScale * (facing.X != 0f ? model.FootprintZ : model.FootprintX);
                var expectedDepth = KitScale * (facing.X != 0f ? model.FootprintX : model.FootprintZ);

                Assert.That(footprint.Width, Is.EqualTo(expectedWidth).Within(0.001f),
                    $"house {lot.HouseId} level-2 footprint width must match the current-level model");
                Assert.That(footprint.Depth, Is.EqualTo(expectedDepth).Within(0.001f),
                    $"house {lot.HouseId} level-2 footprint depth must match the current-level model");
            }

            // House 1's level-2 mesh (building-type-c) has a larger footprint than
            // its level-1 mesh (building-type-r), so the rect must genuinely grow.
            var lot1 = NeighborhoodLayout.GetHouseLot(1);
            var l1 = HousePlacement.HouseFootprint(lot1, HouseLevelModelTable.MinLevel);
            var l2 = HousePlacement.HouseFootprint(lot1, level);
            Assert.That(l2.Width, Is.Not.EqualTo(l1.Width).Within(0.0001f),
                "house 1's footprint must change between level 1 and level 2");
        }

        [Test]
        public void LevelOne_PositionAndFootprint_AreByteIdenticalToTheLevelBlindPath()
        {
            // #454 critical regression guard: threading the level through
            // placement is a STRICT SUPERSET — a never-upgraded (level 1) house
            // must compute the exact same position and footprint the level-blind
            // API returns today, bit-for-bit.
            foreach (var lot in NeighborhoodLayout.HouseLots)
            {
                Assert.That(HousePlacement.Position(lot, KitScale, HouseLevelModelTable.MinLevel),
                    Is.EqualTo(HousePlacement.Position(lot, KitScale)),
                    $"house {lot.HouseId} level-1 position must be byte-identical to today");

                var levelOne = HousePlacement.HouseFootprint(lot, HouseLevelModelTable.MinLevel);
                var today = HousePlacement.HouseFootprint(lot);
                Assert.That(levelOne.MinX, Is.EqualTo(today.MinX), $"house {lot.HouseId} footprint MinX");
                Assert.That(levelOne.MaxX, Is.EqualTo(today.MaxX), $"house {lot.HouseId} footprint MaxX");
                Assert.That(levelOne.MinZ, Is.EqualTo(today.MinZ), $"house {lot.HouseId} footprint MinZ");
                Assert.That(levelOne.MaxZ, Is.EqualTo(today.MaxZ), $"house {lot.HouseId} footprint MaxZ");
            }
        }

        [Test]
        public void MaxHouseFootprint_IsCenteredOnTheSamePivotAsHouseFootprint_SizedFromTheMaxFootprint()
        {
            // #459: the tree-obstacle rect reserves the house's LARGEST
            // possible footprint across its upgrade ladder. It is built
            // exactly like HouseFootprint — same center (Position(lot,
            // KitScale)), same width/depth axis-swap-on-facing — but sized
            // from HouseModelCatalog.MaxFootprint instead of the level-1 model,
            // so it always contains the level-1 footprint.
            foreach (var lot in NeighborhoodLayout.HouseLots)
            {
                var facing = HousePlacement.FrontFacing(lot);
                var max = HouseModelCatalog.MaxFootprint(lot.HouseId);
                var expectedWidth = KitScale * (facing.X != 0f ? max.FootprintZ : max.FootprintX);
                var expectedDepth = KitScale * (facing.X != 0f ? max.FootprintX : max.FootprintZ);

                var rect = HousePlacement.MaxHouseFootprint(lot);
                var footprint = HousePlacement.HouseFootprint(lot);

                Assert.That(rect.Center.X, Is.EqualTo(footprint.Center.X).Within(0.0001f),
                    $"house {lot.HouseId} max-footprint rect must share HouseFootprint's center (X)");
                Assert.That(rect.Center.Z, Is.EqualTo(footprint.Center.Z).Within(0.0001f),
                    $"house {lot.HouseId} max-footprint rect must share HouseFootprint's center (Z)");

                Assert.That(rect.Width, Is.EqualTo(expectedWidth).Within(0.001f),
                    $"house {lot.HouseId} max-footprint rect width must come from MaxFootprint");
                Assert.That(rect.Depth, Is.EqualTo(expectedDepth).Within(0.001f),
                    $"house {lot.HouseId} max-footprint rect depth must come from MaxFootprint");

                // Sharing the level-1 center and being sized to the
                // componentwise max (>= every level's dimensions), it must fully
                // contain the level-1 footprint — the conservative envelope the
                // approved fix reserves against. (Note: #454 pins the facade to
                // the setback and shifts the house back as it deepens, so a
                // per-level footprint is NOT concentric with this rect; this rect
                // is the level-1-centered max-dimension envelope the issue's
                // checklist prescribes, not a union of the repositioned levels.)
                Assert.That(rect.Contains(footprint), Is.True,
                    $"house {lot.HouseId} max-footprint rect must contain its level-1 footprint");
                Assert.That(rect.Width, Is.GreaterThanOrEqualTo(footprint.Width),
                    $"house {lot.HouseId} max-footprint rect is never narrower than the level-1 footprint");
                Assert.That(rect.Depth, Is.GreaterThanOrEqualTo(footprint.Depth),
                    $"house {lot.HouseId} max-footprint rect is never shallower than the level-1 footprint");
            }
        }

        [Test]
        public void HouseFootprint_EveryStarterHouseLadderLevel_StaysInsideItsLotQuadrantBounds()
        {
            // #462 acceptance bar: "centered within property" / "leveling never
            // resizes or moves the lot" (docs/specs/expansion.md#house-leveling)
            // expressed concretely — for every starter house's full L1..L4
            // ladder, the level-aware footprint (#454) must stay fully inside the
            // lot's own QuadrantBounds. Because #454 pins each level's facade to
            // the front setback and lets the deeper mesh grow into the BACKYARD
            // (away from the street), the rear edge advances toward the far
            // quadrant line as the house upgrades; this guard proves it never
            // crosses it (nor does the lateral span spill sideways) at any rung.
            foreach (var lot in NeighborhoodLayout.HouseLots)
            {
                var bounds = LotBounds.QuadrantBounds(lot);
                for (var level = HouseLevelModelTable.MinLevel;
                    level <= HouseUpgradeNumbers.MaxLevel;
                    level++)
                {
                    var footprint = HousePlacement.HouseFootprint(lot, level);
                    Assert.That(bounds.Contains(footprint), Is.True,
                        $"house {lot.HouseId} level {level} footprint " +
                        $"[{footprint.MinX}..{footprint.MaxX}]x[{footprint.MinZ}..{footprint.MaxZ}] " +
                        $"must stay inside its lot quadrant bounds " +
                        $"[{bounds.MinX}..{bounds.MaxX}]x[{bounds.MinZ}..{bounds.MaxZ}]");
                }
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
