using Doggiehood.Core.Cameras;
using Doggiehood.Core.Expansion;
using Doggiehood.Core.World;
using Doggiehood.Unity;
using NUnit.Framework;
using UnityEngine;

namespace Doggiehood.Unity.EditModeTests
{
    /// <summary>
    /// #178: the lock indicator's rendered position/tint/visibility track
    /// Core's live ExpansionIndicator.Resolve(state) — no caching, same
    /// "read live every time" contract HudOverlay uses for the wallet
    /// label.
    /// </summary>
    public class ExpansionIndicatorViewTests
    {
        private GameObject host;
        private ExpansionIndicatorView view;
        private Sprite affordableSprite;
        private Sprite lockedSprite;

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
        }

        [TearDown]
        public void Cleanup()
        {
            Object.DestroyImmediate(host);
        }

        [Test]
        public void Init_OnAFreshGame_PositionsAtTheFirstZonesEntrance_TintedLocked()
        {
            var state = GameState.CreateNew();

            view.Init(state, affordableSprite, lockedSprite);

            var expected = ExpansionIndicator.Resolve(state).Value;
            var renderer = host.GetComponent<SpriteRenderer>();
            Assert.That(renderer.enabled, Is.True);
            Assert.That(host.transform.position.x, Is.EqualTo(expected.Position.X).Within(0.001f));
            Assert.That(host.transform.position.y, Is.EqualTo(ExpansionIndicatorView.HoverHeight).Within(0.001f));
            Assert.That(host.transform.position.z, Is.EqualTo(expected.Position.Z).Within(0.001f));
            Assert.That(renderer.sprite, Is.SameAs(lockedSprite));
        }

        [Test]
        public void Refresh_SwitchesToTheAffordableSprite_OnceTheWalletCoversTheCost()
        {
            var state = GameState.CreateNew();
            view.Init(state, affordableSprite, lockedSprite);

            state.Wallet.Deposit(ZoneUnlockNumbers.BaseCost);
            view.Refresh();

            Assert.That(host.GetComponent<SpriteRenderer>().sprite, Is.SameAs(affordableSprite));
        }

        [Test]
        public void Refresh_FacesTheLiveCameraYaw_AsTheCameraRotates()
        {
            // #266: the lock icon never rotated before — it was pinned to the
            // pre-#203 fixed yaw. It must now billboard to the live
            // CameraController.Yaw so it reads head-on at every rotation.
            var state = GameState.CreateNew();
            var rigObject = new GameObject("yaw-rig", typeof(Camera), typeof(CameraRig));
            var rig = rigObject.GetComponent<CameraRig>();
            try
            {
                rig.Controller.Rotate(75f); // 45 default + 75 => 120

                view.Init(state, affordableSprite, lockedSprite);
                view.Refresh();

                var live = CameraFacing.Resolve(rig.Controller.Yaw);
                var expected = Quaternion.Euler(
                    live.PitchDegrees, live.YawDegrees, live.RollDegrees);
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
            var state = GameState.CreateNew();

            view.Init(state, affordableSprite, lockedSprite);

            var facing = CameraFacing.Resolve(CameraController.DefaultYaw);
            var expected = Quaternion.Euler(
                facing.PitchDegrees, facing.YawDegrees, facing.RollDegrees);
            Assert.That(Quaternion.Angle(host.transform.rotation, expected), Is.LessThan(0.1f),
                "lock icon must face the fixed default yaw when no camera rig exists");
        }

        [Test]
        public void Refresh_DisablesTheRenderer_WhenNoLockedZoneRemains()
        {
            var state = GameState.CreateNew();
            state.Wallet.Deposit(ZoneUnlockNumbers.BaseCost);
            state.TryUnlockNextZone(); // unlocks the only authored zone so far

            view.Init(state, affordableSprite, lockedSprite);

            Assert.That(host.GetComponent<SpriteRenderer>().enabled, Is.False);
        }

        // --- #343: the lock itself is the unlock affordance (Option A) ---

        [Test]
        public void OnTapped_WhenAffordable_RaisesUnlockRequestedOnce()
        {
            var state = GameState.CreateNew();
            state.Wallet.Deposit(ZoneUnlockNumbers.BaseCost); // now affordable (gold)
            view.Init(state, affordableSprite, lockedSprite);

            var requests = 0;
            view.UnlockRequested += () => requests++;
            view.OnTapped();

            Assert.That(requests, Is.EqualTo(1),
                "tapping the affordable/gold lock requests an unlock (raises the confirm dialog)");
        }

        [Test]
        public void OnTapped_WhenNotAffordable_IsANoOp()
        {
            var state = GameState.CreateNew(); // fresh wallet: the lock is grey
            view.Init(state, affordableSprite, lockedSprite);

            var requests = 0;
            view.UnlockRequested += () => requests++;
            view.OnTapped();

            Assert.That(requests, Is.EqualTo(0),
                "a grey/unaffordable lock's tap does nothing (docs/specs/expansion.md)");
        }

        [Test]
        public void OnTapped_WhenNoLockedZoneRemains_IsANoOp()
        {
            var state = GameState.CreateNew();
            state.Wallet.Deposit(ZoneUnlockNumbers.BaseCost);
            state.TryUnlockNextZone(); // nothing left to unlock
            state.Wallet.Deposit(ZoneUnlockNumbers.BaseCost * 10); // plenty of coins, but no zone
            view.Init(state, affordableSprite, lockedSprite);

            var requests = 0;
            view.UnlockRequested += () => requests++;
            view.OnTapped();

            Assert.That(requests, Is.EqualTo(0),
                "with every zone unlocked there is nothing to request");
        }

        [Test]
        public void Init_AddsATapCollider_WhoseEnabledTracksTheRenderer()
        {
            var lockedState = GameState.CreateNew(); // a locked zone remains
            view.Init(lockedState, affordableSprite, lockedSprite);

            var collider = host.GetComponent<BoxCollider>();
            Assert.That(collider, Is.Not.Null, "the lock needs a collider so TapRouter can raycast it");
            Assert.That(collider.enabled, Is.True,
                "the collider is active while a lock is shown");
        }

        [Test]
        public void Refresh_DisablesTheColliderWithTheRenderer_WhenNothingRemains()
        {
            var state = GameState.CreateNew();
            state.Wallet.Deposit(ZoneUnlockNumbers.BaseCost);
            state.TryUnlockNextZone();
            view.Init(state, affordableSprite, lockedSprite);

            var collider = host.GetComponent<BoxCollider>();
            Assert.That(collider.enabled, Is.False,
                "a hidden lock is not tappable — the collider follows the renderer");
        }
    }
}
