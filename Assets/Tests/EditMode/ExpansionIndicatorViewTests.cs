using Doggiehood.Core.Cameras;
using Doggiehood.Core.Expansion;
using Doggiehood.Core.World;
using Doggiehood.Unity;
using NUnit.Framework;
using UnityEngine;

namespace Doggiehood.Unity.EditModeTests
{
    /// <summary>
    /// #453: one lock indicator view bound to a single frontier coordinate. Its
    /// rendered position/tint/visibility track that coordinate's live Core state
    /// (frontier membership + <see cref="TileUnlock"/> affordability) — no
    /// caching, same "read live every time" contract HudOverlay uses for the
    /// wallet label — and tapping an affordable lock raises
    /// <see cref="ExpansionIndicatorView.UnlockRequested"/> with ITS OWN
    /// coordinate.
    /// </summary>
    public class ExpansionIndicatorViewTests
    {
        private GameObject host;
        private ExpansionIndicatorView view;
        private Sprite affordableSprite;
        private Sprite lockedSprite;
        private GameState state;

        [SetUp]
        public void CreateHost()
        {
            host = new GameObject("expansion-indicator-under-test");
            host.AddComponent<SpriteRenderer>();
            view = host.AddComponent<ExpansionIndicatorView>();

            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            texture.Apply();
            affordableSprite = Sprite.Create(texture, new Rect(0, 0, 2, 2), Vector2.one * 0.5f);
            lockedSprite = Sprite.Create(texture, new Rect(0, 0, 2, 2), Vector2.one * 0.5f);

            // A live target map so the scripted first tile (0,1) is on the
            // unlockable frontier (onboarding-gated).
            state = FrontierEditModeWorld.WithTargetMap();
        }

        [TearDown]
        public void Cleanup()
        {
            Object.DestroyImmediate(host);
        }

        private void InitOnFirstTile()
        {
            view.Init(state, FrontierEditModeWorld.FirstTile, affordableSprite, lockedSprite);
        }

        [Test]
        public void Init_OnAFreshGame_PositionsPastItsFrontierEdge_TintedLocked()
        {
            InitOnFirstTile();

            var expected = ExpansionIndicatorPlacement.Resolve(state.Map, FrontierEditModeWorld.FirstTile);
            var renderer = host.GetComponent<SpriteRenderer>();
            Assert.That(renderer.enabled, Is.True);
            Assert.That(host.transform.position.x, Is.EqualTo(expected.X).Within(0.001f));
            Assert.That(host.transform.position.y, Is.EqualTo(ExpansionIndicatorView.HoverHeight).Within(0.001f));
            Assert.That(host.transform.position.z, Is.EqualTo(expected.Z).Within(0.001f));
            Assert.That(renderer.sprite, Is.SameAs(lockedSprite), "an empty wallet cannot afford the tile");
        }

        [Test]
        public void Refresh_SwitchesToTheAffordableSprite_OnceTheWalletCoversTheCost()
        {
            InitOnFirstTile();

            state.Wallet.Deposit(TileUnlock.Cost(state.Map.Tiles.Count));
            view.Refresh();

            Assert.That(host.GetComponent<SpriteRenderer>().sprite, Is.SameAs(affordableSprite));
        }

        [Test]
        public void Refresh_FacesTheLiveCameraYaw_AsTheCameraRotates()
        {
            // #266: the lock icon billboards to the live CameraController.Yaw so
            // it reads head-on at every rotation.
            var rigObject = new GameObject("yaw-rig", typeof(Camera), typeof(CameraRig));
            var rig = rigObject.GetComponent<CameraRig>();
            try
            {
                rig.Controller.Rotate(75f); // 45 default + 75 => 120

                InitOnFirstTile();
                view.Refresh();

                var live = CameraFacing.Resolve(rig.Controller.Yaw);
                var expected = Quaternion.Euler(live.PitchDegrees, live.YawDegrees, live.RollDegrees);
                Assert.That(Quaternion.Angle(host.transform.rotation, expected), Is.LessThan(0.1f),
                    "lock icon must face the live camera yaw, not the fixed default");

                var fixedFacing = CameraFacing.Resolve(CameraController.DefaultYaw);
                var fixedRotation = Quaternion.Euler(
                    fixedFacing.PitchDegrees, fixedFacing.YawDegrees, fixedFacing.RollDegrees);
                Assert.That(Quaternion.Angle(host.transform.rotation, fixedRotation), Is.GreaterThan(1f),
                    "a rotated camera must move the lock icon off the old fixed 45° facing");
            }
            finally
            {
                Object.DestroyImmediate(rigObject);
            }
        }

        [Test]
        public void Refresh_AtTheDefaultYaw_FacesTheOriginalFixedOrientation()
        {
            // #266 regression guard: with no CameraRig the facing falls back
            // to the fixed default yaw, so on-launch appearance is unchanged.
            InitOnFirstTile();

            var facing = CameraFacing.Resolve(CameraController.DefaultYaw);
            var expected = Quaternion.Euler(facing.PitchDegrees, facing.YawDegrees, facing.RollDegrees);
            Assert.That(Quaternion.Angle(host.transform.rotation, expected), Is.LessThan(0.1f),
                "lock icon must face the fixed default yaw when no camera rig exists");
        }

        [Test]
        public void Refresh_DisablesTheRenderer_WhenItsCoordinateLeavesTheFrontier()
        {
            InitOnFirstTile();
            Assert.That(host.GetComponent<SpriteRenderer>().enabled, Is.True, "precondition: visible while unlockable");

            // Unlock (0,1): it is now placed, so this view's coordinate leaves
            // the frontier and the lock must hide itself.
            state.Wallet.Deposit(TileUnlock.Cost(state.Map.Tiles.Count));
            Assert.That(state.TryUnlockTile(FrontierEditModeWorld.FirstTile), Is.True);
            view.Refresh();

            Assert.That(host.GetComponent<SpriteRenderer>().enabled, Is.False);
        }

        // --- #453: the lock itself is the unlock affordance (Option A) ---

        [Test]
        public void OnTapped_WhenAffordable_RaisesUnlockRequestedWithItsOwnCoordinate_Once()
        {
            state.Wallet.Deposit(TileUnlock.Cost(state.Map.Tiles.Count)); // affordable (gold)
            InitOnFirstTile();

            var requested = new System.Collections.Generic.List<TileCoordinate>();
            view.UnlockRequested += coordinate => requested.Add(coordinate);
            view.OnTapped();

            Assert.That(requested, Is.EqualTo(new[] { FrontierEditModeWorld.FirstTile }),
                "tapping the affordable/gold lock requests an unlock for its own coordinate");
        }

        [Test]
        public void OnTapped_WhenNotAffordable_IsANoOp()
        {
            InitOnFirstTile(); // fresh wallet: the lock is grey

            var requests = 0;
            view.UnlockRequested += _ => requests++;
            view.OnTapped();

            Assert.That(requests, Is.EqualTo(0),
                "a grey/unaffordable lock's tap does nothing (docs/specs/expansion.md)");
        }

        [Test]
        public void OnTapped_WhenItsCoordinateIsNoLongerUnlockable_IsANoOp()
        {
            state.Wallet.Deposit(TileUnlock.Cost(state.Map.Tiles.Count) * 10);
            InitOnFirstTile();
            Assert.That(state.TryUnlockTile(FrontierEditModeWorld.FirstTile), Is.True); // now placed

            var requests = 0;
            view.UnlockRequested += _ => requests++;
            view.OnTapped();

            Assert.That(requests, Is.EqualTo(0),
                "a lock whose coordinate is already placed has nothing to request");
        }

        [Test]
        public void Init_AddsATapCollider_WhoseEnabledTracksTheRenderer()
        {
            InitOnFirstTile();

            var collider = host.GetComponent<BoxCollider>();
            Assert.That(collider, Is.Not.Null, "the lock needs a collider so TapRouter can raycast it");
            Assert.That(collider.enabled, Is.True, "the collider is active while a lock is shown");
        }

        [Test]
        public void Refresh_DisablesTheColliderWithTheRenderer_WhenTheCoordinateLeavesTheFrontier()
        {
            state.Wallet.Deposit(TileUnlock.Cost(state.Map.Tiles.Count));
            InitOnFirstTile();
            Assert.That(state.TryUnlockTile(FrontierEditModeWorld.FirstTile), Is.True);
            view.Refresh();

            var collider = host.GetComponent<BoxCollider>();
            Assert.That(collider.enabled, Is.False,
                "a hidden lock is not tappable — the collider follows the renderer");
        }
    }
}
