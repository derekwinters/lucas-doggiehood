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

        [Test]
        public void Gear_SitsInTheTopRightCorner()
        {
            // #219 / wireframe decision ①: the Settings gear takes the very
            // top-right corner, inset by the wireframe margin (88px @ 32px in).
            var gear = HudOverlay.ComputeGearRect(1920f, 1200f);

            Assert.That(gear.width, Is.EqualTo(88f));
            Assert.That(gear.height, Is.EqualTo(88f));
            Assert.That(gear.xMax, Is.EqualTo(1920f - 32f), "gear inset from the right edge by GearMarginPx");
            Assert.That(gear.yMin, Is.EqualTo(32f), "gear inset from the top edge by GearMarginPx");
        }

        [Test]
        public void CurrencyChip_MovesInboardToTheGearsLeft()
        {
            // #219 / wireframe decision ①: the coins chip is nudged inboard so
            // the gear owns the corner — the chip ends left of the gear.
            var gear = HudOverlay.ComputeGearRect(1920f, 1200f);
            var chip = HudOverlay.ComputeChipRect(1920f, 1200f);

            Assert.That(chip.xMax, Is.LessThanOrEqualTo(gear.xMin),
                "the currency chip sits entirely to the left of the gear");
        }

        [Test]
        public void TapGear_RaisesGearTapped_SoTheBootstrapCanOpenSettings()
        {
            var overlay = host.AddComponent<HudOverlay>();
            overlay.Init(GameState.CreateNew());

            var opened = 0;
            overlay.GearTapped += () => opened++;

            overlay.TapGear();

            Assert.That(opened, Is.EqualTo(1));
        }
    }
}
