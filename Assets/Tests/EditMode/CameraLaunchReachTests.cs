using System.Collections.Generic;
using System.Reflection;
using Doggiehood.Core.Cameras;
using Doggiehood.Core.Interaction;
using Doggiehood.Core.Onboarding;
using Doggiehood.Core.World;
using Doggiehood.Unity;
using NUnit.Framework;
using UnityEngine;

namespace Doggiehood.Unity.EditModeTests
{
    /// <summary>
    /// #691: the camera's reach — its pan bounds and its maximum zoom-out — is
    /// derived from the live tile extent, and nothing about it is persisted.
    /// <see cref="CameraRig"/> re-runs
    /// <c>CameraController.ForStartingNeighborhood()</c> on every scene load, so
    /// each launch starts back at the origin intersection's limits
    /// (±<see cref="StartingPanLimitMeters"/> m, <c>MaxZoom</c> 54). Until now the
    /// only production caller that grew them was the live-unlock path, so a
    /// player who loaded a large saved neighborhood and simply looked around
    /// could not pan to its southern tiles and could not zoom out far enough to
    /// see it all — until they unlocked something, which fixed it for that
    /// session only.
    ///
    /// These tests exercise the shipped launch sequence itself
    /// (<c>WorldBootstrap.RestoreMapAndCameraReach</c>) rather than a mirror of
    /// it, so the ordering it encodes — apply the authored/target map first
    /// (#453, which also runs #539's green-space activation onto
    /// <see cref="GameState.Map"/>), sync the camera's reach second — is the
    /// thing under test.
    /// </summary>
    public class CameraLaunchReachTests
    {
        /// <summary>The pan limit a freshly constructed rig starts at: half a
        /// tile beyond the origin intersection's centre plus
        /// <see cref="CameraController.BoundsMargin"/> — 42 m, the constant every
        /// launch was stuck at before #691. Named rather than inlined (#161) and
        /// derived, so it tracks the real geometry.</summary>
        private static float StartingPanLimitMeters
            => (WorldDimensions.TileSize / 2f) + CameraController.BoundsMargin;

        /// <summary>Metre tolerance for float comparisons of world positions.</summary>
        private const float ToleranceMeters = 0.01f;

        /// <summary>Enough coins to unlock the handful of tiles these fixtures
        /// expand by, whatever the live cost curve charges.</summary>
        private const int TestWalletCoins = 100_000;

        /// <summary>The four road tiles around the origin intersection in the
        /// authored map — unlocked so the saved neighborhood extends past the
        /// origin-tile bounds on every axis, south included (the direction the
        /// on-device repro could not pan toward).</summary>
        private static readonly TileCoordinate[] TilesAroundTheOrigin =
        {
            new TileCoordinate(0, 1),
            new TileCoordinate(0, -1),
            new TileCoordinate(1, 0),
            new TileCoordinate(-1, 0),
        };

        private GameObject rigObject;
        private CameraRig rig;

        [SetUp]
        public void CreateRig()
        {
            // #670/#544: the input authority and the modal gate are
            // process-global; clear both so a rig leaked by an earlier test
            // cannot be the one FindFirstObjectByType hands the sync.
            InputAuthority.Shared.Clear();
            ModalInputGate.Shared.Clear();

            BuildFreshRig();
        }

        [TearDown]
        public void DestroyRig()
        {
            Object.DestroyImmediate(rigObject);
            InputAuthority.Shared.Clear();
            ModalInputGate.Shared.Clear();
        }

        [Test]
        public void Launch_GrowsThePanBounds_ToCoverTheWholeSavedMap()
        {
            var state = SavedNeighborhoodSpanningSeveralTiles();
            var map = MapExtent.Covering(state.Map);
            AssertTheFixtureReachesPastTheOriginTile(map);

            // The bug's signature: a fresh rig — i.e. every launch — is pinned to
            // the origin intersection, well short of the saved map's south edge.
            Assert.That(rig.Controller.Bounds.MinZ,
                Is.EqualTo(-StartingPanLimitMeters).Within(ToleranceMeters),
                "a fresh rig starts at the origin-tile bounds; that is what launch must grow");

            Launch(state);

            AssertThePanBoundsCover(map);
        }

        [Test]
        public void Launch_GrowsTheMaxZoomOut_ToFrameTheWholeSavedMap()
        {
            var state = SavedNeighborhoodSpanningSeveralTiles();
            var map = MapExtent.Covering(state.Map);
            var maxZoomAtLaunchStart = rig.Controller.MaxZoom;

            Launch(state);

            Assert.That(rig.Controller.MaxZoom, Is.GreaterThan(maxZoomAtLaunchStart),
                "the zoom-out cap must grow past the starting intersection's");
            AssertTheMaxZoomFrames(map);
        }

        [Test]
        public void Relaunch_AfterExpanding_StillCoversTheExpandedMap()
        {
            // Derek's on-device confirmation: expanding fixed pan and zoom for
            // the rest of that session, then closing and reloading put both
            // limits back at the exact same point. Nothing about the camera's
            // reach is persisted, so the reload — not the first launch — is the
            // case that tells "recomputed at launch" from "recomputed once ever".
            var state = SavedNeighborhoodSpanningSeveralTiles();
            Launch(state);

            ExpandDuringTheSession(state);
            var expanded = MapExtent.Covering(state.Map);
            AssertThePanBoundsCover(expanded);
            AssertTheMaxZoomFrames(expanded);

            // Relaunch: the save round-trips through the codec (the camera's
            // reach is not part of it) and the scene builds a brand new rig.
            var reloaded = SaveCodec.Load(SaveCodec.Save(state));
            BuildFreshRig();
            Assert.That(rig.Controller.Bounds.MaxZ,
                Is.EqualTo(StartingPanLimitMeters).Within(ToleranceMeters),
                "the reloaded scene's rig starts back at the origin-tile bounds");

            Launch(reloaded);

            AssertThePanBoundsCover(expanded);
            AssertTheMaxZoomFrames(expanded);
        }

        [Test]
        public void Launch_DerivesTheReachFromTheLiveMap_NotTheAuthoredTargetMap()
        {
            // The reach tracks what the player has actually unlocked. Deriving it
            // from the authored TargetMap would also "fix" the repro, while
            // letting the player pan and zoom out over land they have never
            // unlocked — so pin the live map as the basis.
            var state = FrontierEditModeWorld.WithFirstTileUnlocked();

            Launch(state);

            Assert.That(state.TargetMap, Is.Not.Null,
                "the launch sequence applies the authored target map before it syncs the camera");
            var live = MapExtent.Covering(state.Map);
            var authored = MapExtent.Covering(state.TargetMap);
            Assert.That(authored.MaxZ, Is.GreaterThan(live.MaxZ),
                "the authored map must reach further than the unlocked one, or this proves nothing");

            AssertThePanBoundsCover(live);
            Assert.That(rig.Controller.Bounds.MaxZ, Is.LessThan(authored.MaxZ),
                "panning must not reach over tiles that are still locked");
        }

        [Test]
        public void Launch_LetsAFocusOnAFrontierHouse_LandExactly()
        {
            // #165's Home fly-to and #518/#604's "Say hi!" both FocusOn through
            // these same bounds, so on a fresh launch they were silently clamped
            // back toward the origin instead of landing on the house.
            var state = FrontierEditModeWorld.WithFirstTileUnlocked();
            var lot = FrontierEditModeWorld.FirstTileLots(state)[0].Position;
            Assert.That(lot.Z, Is.GreaterThan(StartingPanLimitMeters),
                "the fixture house must sit outside the origin-tile bounds, or this proves nothing");

            rig.Controller.FocusOn(lot);
            Assert.That(rig.Controller.Position.Z, Is.LessThan(lot.Z),
                "before the launch recompute the focus is clamped short of the house");

            Launch(state);
            rig.Controller.FocusOn(lot);

            Assert.That(rig.Controller.Position.X, Is.EqualTo(lot.X).Within(ToleranceMeters));
            Assert.That(rig.Controller.Position.Z, Is.EqualTo(lot.Z).Within(ToleranceMeters));
        }

        /// <summary>Runs the shipped launch sequence
        /// (<c>WorldBootstrap.RestoreMapAndCameraReach</c>) against the live
        /// scene rig — the same one call <c>Awake</c> makes, invoked directly
        /// rather than mirrored so these assertions run on production code.</summary>
        private static void Launch(GameState state)
        {
            var restore = typeof(WorldBootstrap).GetMethod(
                "RestoreMapAndCameraReach", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(restore, Is.Not.Null,
                "WorldBootstrap.RestoreMapAndCameraReach(state) is the launch sequence under test");
            restore.Invoke(null, new object[] { state });
        }

        /// <summary>A saved game whose neighborhood spans the origin
        /// intersection plus the four road tiles around it, so its extent runs
        /// past the origin-tile pan limit in every direction. The onboarding
        /// reward chain is restored to Done — a player long past onboarding —
        /// because until the "expand the map" step completes the frontier offers
        /// only the single scripted tile.</summary>
        private static GameState SavedNeighborhoodSpanningSeveralTiles()
        {
            var state = FrontierEditModeWorld.WithTargetMap();
            state.RestoreRewardChainStep(OnboardingRewardStep.Done);
            state.Wallet.Deposit(TestWalletCoins);
            foreach (var coordinate in TilesAroundTheOrigin)
            {
                Assert.That(state.TryUnlockTile(coordinate), Is.True,
                    $"the fixture expects {coordinate} to be unlockable from the origin");
            }

            return state;
        }

        /// <summary>Expands the neighborhood the way a live unlock does (#373):
        /// Core takes each spend and places the tile, then the same camera-reach
        /// sync <see cref="ExpansionUnlockDirector"/> runs. Unlocks the whole
        /// current frontier so the map is guaranteed to outgrow its previous
        /// extent rather than fill an interior notch.</summary>
        private static void ExpandDuringTheSession(GameState state)
        {
            var before = MapExtent.Covering(state.Map);
            foreach (var coordinate in new List<TileCoordinate>(state.UnlockableFrontier()))
            {
                Assert.That(state.TryUnlockTile(coordinate), Is.True,
                    $"the fixture expects frontier tile {coordinate} to be unlockable");
            }

            var after = MapExtent.Covering(state.Map);
            Assert.That(after.Width + after.Depth, Is.GreaterThan(before.Width + before.Depth),
                "the expansion must grow the map's extent, or the relaunch assertions prove nothing");

            CameraReach.SyncToLiveMap(state);
        }

        private void BuildFreshRig()
        {
            if (rigObject != null)
            {
                Object.DestroyImmediate(rigObject);
            }

            rigObject = new GameObject("camera-rig-under-test", typeof(Camera));
            rig = rigObject.AddComponent<CameraRig>();
            rig.ApplyConfiguration();
        }

        private void AssertThePanBoundsCover(MapExtent map)
        {
            Assert.That(rig.Controller.Bounds.MinX, Is.LessThanOrEqualTo(map.MinX),
                "the west edge of the map is out of pan reach");
            Assert.That(rig.Controller.Bounds.MaxX, Is.GreaterThanOrEqualTo(map.MaxX),
                "the east edge of the map is out of pan reach");
            Assert.That(rig.Controller.Bounds.MinZ, Is.LessThanOrEqualTo(map.MinZ),
                "the south edge of the map is out of pan reach — the #691 repro");
            Assert.That(rig.Controller.Bounds.MaxZ, Is.GreaterThanOrEqualTo(map.MaxZ),
                "the north edge of the map is out of pan reach");
        }

        private void AssertTheMaxZoomFrames(MapExtent map)
        {
            // Zoom is an orthographic half-height, and the viewport is wider
            // than it is tall, so framing the larger of the two spans at half
            // its length is the conservative "the whole map fits" check.
            var largerSpan = Mathf.Max(map.Width, map.Depth);
            Assert.That(rig.Controller.MaxZoom, Is.GreaterThanOrEqualTo(largerSpan / 2f),
                "at full zoom-out the whole saved map must fit on screen");
        }

        private static void AssertTheFixtureReachesPastTheOriginTile(MapExtent map)
        {
            Assert.That(map.MinZ, Is.LessThan(-StartingPanLimitMeters),
                "the fixture map must extend south of the origin-tile bounds, or this proves nothing");
            Assert.That(map.MaxZ, Is.GreaterThan(StartingPanLimitMeters),
                "the fixture map must extend north of the origin-tile bounds, or this proves nothing");
        }
    }
}
