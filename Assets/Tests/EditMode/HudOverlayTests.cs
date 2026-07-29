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
        public void Label_OnANewGame_ReadsBareZero()
        {
            // #296: the coin token carries the "coins" meaning, so the chip
            // shows a bare tabular number — a fresh save reads "0".
            var overlay = host.AddComponent<HudOverlay>();
            overlay.Init(GameState.CreateNew());

            Assert.That(overlay.Label, Is.EqualTo("0"));
        }

        [Test]
        public void Label_TracksTheWalletLive_WithNoCaching()
        {
            // #159: a Deposit after Init must show up immediately — the
            // overlay reads the wallet each time, it never snapshots it.
            var overlay = host.AddComponent<HudOverlay>();
            var state = GameState.CreateNew();
            overlay.Init(state);
            Assert.That(overlay.Label, Is.EqualTo("0"));

            state.Wallet.Deposit(EconomyNumbers.QuestPayout);

            Assert.That(overlay.Label, Is.EqualTo("10"));
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
        public void CurrencyChip_SitsEntirelyLeftOfTheGear()
        {
            // #219 / wireframe decision ①: the coins chip is nudged inboard so
            // the gear owns the corner — the chip ends left of the gear.
            var full = new Rect(0f, 0f, 1920f, 1200f);
            var width = HudOverlay.ComputeChipWidth("128");
            var gear = HudOverlay.ComputeGearRect(1920f, 1200f);
            var chip = HudOverlay.ComputeChipRect(1920f, 1200f, full, width);

            Assert.That(chip.xMax, Is.LessThanOrEqualTo(gear.xMin),
                "the currency chip sits entirely to the left of the gear");
            Assert.That(chip.height, Is.EqualTo(HudOverlay.HeightPx));
        }

        [Test]
        public void CurrencyChip_InsetsFromTheSafeAreaTopByHudEdgeMargin()
        {
            // hud.md (#174): the chip's top inset is measured from the SAFE-AREA
            // edge, not the raw screen edge. With a 54px top safe inset the chip
            // top lands at 54 + HudEdgeMarginPx(36) = 90 (the mockup's value).
            // Screen.safeArea is bottom-left origin: a 54/48 top/bottom inset on
            // a 1200-tall screen gives y=48, height=1098 (yMax=1146).
            var safe = new Rect(72f, 48f, 1920f - 144f, 1200f - 54f - 48f);
            var width = HudOverlay.ComputeChipWidth("128");
            var chip = HudOverlay.ComputeChipRect(1920f, 1200f, safe, width);

            Assert.That(chip.y, Is.EqualTo(90f), "chip top = safe-area top inset (54) + HudEdgeMarginPx (36)");
            Assert.That(HudOverlay.HudEdgeMarginPx, Is.EqualTo(36f));
        }

        [Test]
        public void ChipConstants_MatchTheSharedComponentSpec()
        {
            // shared-components.md CurrencyChip constants.
            Assert.That(HudOverlay.HeightPx, Is.EqualTo(64f));
            Assert.That(HudOverlay.CoinDiameterPx, Is.EqualTo(44f));
            Assert.That(HudOverlay.PaddingLeftPx, Is.EqualTo(10f));
            Assert.That(HudOverlay.PaddingRightPx, Is.EqualTo(26f));
            Assert.That(HudOverlay.FontSizePx, Is.EqualTo(34));
        }

        [Test]
        public void ChromeConstants_MatchTheCandyCottageBaseline()
        {
            // shared-components.md shared baseline (#65).
            Assert.That(HudOverlay.OutlineThicknessPx, Is.EqualTo(6f));
            Assert.That(HudOverlay.ShadowOffsetPx, Is.EqualTo(8f));
            Assert.That(HudOverlay.PillRadiusPx, Is.EqualTo(999f));
            Assert.That(HudOverlay.CoinOutlineThicknessPx, Is.EqualTo(4f),
                "coin token's ink ring (mockup .coin border:4px)");
        }

        [Test]
        public void ChipColors_MatchTheFixedCandyCottagePalette()
        {
            // shared-components.md palette: Cream #FFF3D9, Ink #2E2A26, Gold #FFC23C.
            AssertHex(HudOverlay.CreamColor, 0xFF, 0xF3, 0xD9, "Cream fill");
            AssertHex(HudOverlay.InkColor, 0x2E, 0x2A, 0x26, "Ink outline/shadow");
            AssertHex(HudOverlay.GoldColor, 0xFF, 0xC2, 0x3C, "Gold coin token");
        }

        [Test]
        public void ChipWidth_DerivesFromItsRegions_NotAMagicNumber()
        {
            // width = 2*outline + paddingLeft + coin + iconGap + number + paddingRight.
            // For "128" (3 tabular glyphs): 12 + 10 + 44 + 12 + 3*22 + 26 = 170.
            var expected = 2f * HudOverlay.OutlineThicknessPx
                + HudOverlay.PaddingLeftPx + HudOverlay.CoinDiameterPx
                + HudOverlay.IconGapPx + 3f * HudOverlay.DigitAdvancePx
                + HudOverlay.PaddingRightPx;

            Assert.That(HudOverlay.ComputeChipWidth("128"), Is.EqualTo(expected));
            Assert.That(expected, Is.EqualTo(170f));
        }

        [Test]
        public void LabelFont_IsTheBundledDejaVuSans_NotAnEditorOnlyBuiltin()
        {
            // #291: runtime-drawn text must use the bundled font, never an
            // editor-only built-in (which renders invisible in the build).
            Assert.That(HudOverlay.LabelFontResource, Is.EqualTo("DejaVuSans"));
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

        private static void AssertHex(Color color, byte r, byte g, byte b, string what)
        {
            var c32 = (Color32)color;
            Assert.That(c32.r, Is.EqualTo(r), what + " red channel");
            Assert.That(c32.g, Is.EqualTo(g), what + " green channel");
            Assert.That(c32.b, Is.EqualTo(b), what + " blue channel");
        }
    }
}
