using Doggiehood.Core.Economy;
using Doggiehood.Core.World;
using Doggiehood.Unity;
using NUnit.Framework;
using UnityEngine;

namespace Doggiehood.Unity.EditModeTests
{
    public class HudOverlayTests
    {
        private GameObject host;

        [SetUp]
        public void CreateHost()
        {
            host = new GameObject("hud-under-test");
        }

        [TearDown]
        public void Cleanup()
        {
            Object.DestroyImmediate(host);
        }

        [Test]
        public void Label_OnANewGame_ReadsCoinsZero()
        {
            // #159: the chip shows the fresh save's empty wallet.
            var overlay = host.AddComponent<HudOverlay>();
            overlay.Init(GameState.CreateNew());

            Assert.That(overlay.Label, Is.EqualTo("Coins: 0"));
        }

        [Test]
        public void Label_TracksTheWalletLive_WithNoCaching()
        {
            // #159: a Deposit after Init must show up immediately — the
            // overlay reads the wallet each time, it never snapshots it.
            var overlay = host.AddComponent<HudOverlay>();
            var state = GameState.CreateNew();
            overlay.Init(state);
            Assert.That(overlay.Label, Is.EqualTo("Coins: 0"));

            state.Wallet.Deposit(EconomyNumbers.QuestPayout);

            Assert.That(overlay.Label, Is.EqualTo("Coins: 10"));
        }
    }
}
