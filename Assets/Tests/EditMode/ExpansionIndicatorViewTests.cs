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
        public void Refresh_DisablesTheRenderer_WhenNoLockedZoneRemains()
        {
            var state = GameState.CreateNew();
            state.Wallet.Deposit(ZoneUnlockNumbers.BaseCost);
            state.TryUnlockNextZone(); // unlocks the only authored zone so far

            view.Init(state, affordableSprite, lockedSprite);

            Assert.That(host.GetComponent<SpriteRenderer>().enabled, Is.False);
        }
    }
}
