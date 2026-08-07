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

            Assert.That(overlay.Label, Is.EqualTo("20"));
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
        public void CurrencyChip_SharesTheGearsVerticalCentreline()
        {
            // #440 (supersedes the old safe-area top-inset rule): now that the
            // chip and gear are the same height, they centre on the same
            // horizontal line so they read as one clean row — regardless of the
            // two elements' different edge references (the chip's y is computed
            // off the gear's actual on-screen middle and its own height).
            var safe = new Rect(72f, 48f, 1920f - 144f, 1200f - 54f - 48f);
            var width = HudOverlay.ComputeChipWidth("128");
            var chip = HudOverlay.ComputeChipRect(1920f, 1200f, safe, width);
            var gear = HudOverlay.ComputeGearRect(1920f, 1200f);

            Assert.That(chip.center.y, Is.EqualTo(gear.center.y),
                "the chip and gear share a vertical centreline");
        }

        [Test]
        public void ChipHeight_EqualsTheGearHeight_SoTheyReadAsOneRow()
        {
            // #440: the coins pill was 24px shorter than the Settings gear beside
            // it. It now matches the gear — tied to the gear's ACTUAL height
            // (not a second hardcoded 88f) so the two can never silently drift.
            var gear = HudOverlay.ComputeGearRect(1920f, 1200f);

            Assert.That(HudOverlay.HeightPx, Is.EqualTo(gear.height));
        }

        [Test]
        public void ChipConstants_MatchTheSharedComponentSpec()
        {
            // shared-components.md CurrencyChip constants — the interior scaled
            // x1.375 (=88/64) to fill the enlarged pill without dead padding (#440).
            Assert.That(HudOverlay.CoinDiameterPx, Is.EqualTo(60f));
            Assert.That(HudOverlay.PaddingLeftPx, Is.EqualTo(14f));
            Assert.That(HudOverlay.PaddingRightPx, Is.EqualTo(36f));
            Assert.That(HudOverlay.IconGapPx, Is.EqualTo(17f));
            Assert.That(HudOverlay.FontSizePx, Is.EqualTo(46));
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
            // For "128" (3 tabular glyphs): 12 + 14 + 60 + 17 + 3*22 + 36 = 205 (#440).
            var expected = 2f * HudOverlay.OutlineThicknessPx
                + HudOverlay.PaddingLeftPx + HudOverlay.CoinDiameterPx
                + HudOverlay.IconGapPx + 3f * HudOverlay.DigitAdvancePx
                + HudOverlay.PaddingRightPx;

            Assert.That(HudOverlay.ComputeChipWidth("128"), Is.EqualTo(expected));
            Assert.That(expected, Is.EqualTo(205f));
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

        [Test]
        public void Gear_HasNoFontGlyph_SoItNeverFallsBackToTheGrayBox()
        {
            // #370: the ⚙ glyph (U+2699) has no coverage in the bundled
            // DejaVuSans font, so on device the gear rendered as an empty
            // default-skin gray box. The gear is now drawn procedurally — the
            // GearGlyph field must be gone entirely, no font glyph in its path.
            var glyphField = typeof(HudOverlay).GetField(
                "GearGlyph",
                System.Reflection.BindingFlags.NonPublic
                    | System.Reflection.BindingFlags.Public
                    | System.Reflection.BindingFlags.Static
                    | System.Reflection.BindingFlags.Instance);

            Assert.That(glyphField, Is.Null,
                "the ⚙ font glyph must be removed; the gear is drawn procedurally");
        }

        [Test]
        public void GearIcon_GeometryConstants_MatchTheProceduralGearSpec()
        {
            // #370 / #161: the procedural toothed-disc gear icon geometry is
            // expressed as named constants (no inline literals), asserted here
            // the way the coach bar asserts its procedural paw-badge constants.
            Assert.That(HudOverlay.GearToothCount, Is.EqualTo(8), "gear tooth count");
            Assert.That(HudOverlay.GearBodyDiameterPx, Is.EqualTo(46f), "gear body disc diameter");
            Assert.That(HudOverlay.GearToothDiameterPx, Is.EqualTo(14f), "each tooth disc diameter");
            Assert.That(HudOverlay.GearToothOrbitRadiusPx, Is.EqualTo(26f), "tooth center orbit radius");
            Assert.That(HudOverlay.GearHubDiameterPx, Is.EqualTo(18f), "cream hub-hole diameter");
        }

        [Test]
        public void GearIcon_FitsInsideTheCreamFill_AndTeethPokePastTheBody()
        {
            // The toothed disc must read as a gear: teeth poke out past the body
            // disc, and the whole icon stays inside the cream fill (button minus
            // the ink outline on both sides). All from named constants (#161).
            var gear = HudOverlay.ComputeGearRect(1920f, 1200f);
            var creamDiameter = gear.width - 2f * HudOverlay.OutlineThicknessPx;
            var toothTipDiameter = 2f * HudOverlay.GearToothOrbitRadiusPx + HudOverlay.GearToothDiameterPx;

            Assert.That(toothTipDiameter, Is.LessThanOrEqualTo(creamDiameter),
                "the tooth tips stay within the cream fill");
            Assert.That(HudOverlay.GearToothOrbitRadiusPx + HudOverlay.GearToothDiameterPx / 2f,
                Is.GreaterThan(HudOverlay.GearBodyDiameterPx / 2f),
                "the teeth poke out past the gear body so it reads as toothed");
            Assert.That(HudOverlay.GearHubDiameterPx, Is.LessThan(HudOverlay.GearBodyDiameterPx),
                "the hub hole is smaller than the gear body");
        }

        [Test]
        public void CoinsChanged_OnAGain_SpawnsALeafDeltaLabel()
        {
            // #542: a deposit spawns a floating "+N" delta, role Gain, painted
            // Leaf green (the palette's positive/confirm role).
            var overlay = host.AddComponent<HudOverlay>();
            var state = GameState.CreateNew();
            overlay.Init(state);

            state.Wallet.Deposit(100);

            Assert.That(overlay.CurrentDelta, Is.Not.Null, "a gain spawns a delta label");
            Assert.That(overlay.CurrentDelta.Role, Is.EqualTo(CoinDeltaRole.Gain));
            Assert.That(overlay.CurrentDelta.DeltaText, Is.EqualTo("+100"));
            AssertHex(HudOverlay.DeltaColor(CoinDeltaRole.Gain), 0x58, 0xC0, 0x6A, "Leaf gain");
        }

        [Test]
        public void CoinsChanged_OnASpend_SpawnsACoralDeltaLabel()
        {
            // #542: a spend spawns a floating "−N" delta, role Spend, painted
            // Coral red (the palette's primary/spend role).
            var overlay = host.AddComponent<HudOverlay>();
            var state = GameState.CreateNew();
            overlay.Init(state);
            state.Wallet.Deposit(200);

            Assert.That(state.Wallet.TrySpend(50), Is.True);

            Assert.That(overlay.CurrentDelta, Is.Not.Null);
            Assert.That(overlay.CurrentDelta.Role, Is.EqualTo(CoinDeltaRole.Spend));
            Assert.That(overlay.CurrentDelta.DeltaText, Is.EqualTo("−50"));
            AssertHex(HudOverlay.DeltaColor(CoinDeltaRole.Spend), 0xFF, 0x7A, 0x5C, "Coral spend");
        }

        [Test]
        public void DeltaLabel_SitsDeltaOffsetYBelowTheChip_AndCentredUnderIt()
        {
            // #542: at the start of the rise (offset 0) the delta label begins
            // DeltaOffsetYPx below the chip's bottom edge, horizontally centred
            // under the chip.
            var full = new Rect(0f, 0f, 1920f, 1200f);
            var width = HudOverlay.ComputeChipWidth("128");
            var chip = HudOverlay.ComputeChipRect(1920f, 1200f, full, width);

            var label = HudOverlay.ComputeDeltaLabelRect(chip, 0f);

            Assert.That(label.yMin, Is.EqualTo(chip.yMax + HudOverlay.DeltaOffsetYPx),
                "delta label starts DeltaOffsetYPx below the chip");
            Assert.That(label.center.x, Is.EqualTo(chip.center.x),
                "delta label is centred under the chip");
        }

        [Test]
        public void DeltaLabel_RisesUpwardAsItsOffsetGrows()
        {
            // #542: the label rises (moves up — decreasing y in IMGUI's top-left
            // origin) as its rise offset grows toward DeltaRiseDistancePx.
            var full = new Rect(0f, 0f, 1920f, 1200f);
            var width = HudOverlay.ComputeChipWidth("128");
            var chip = HudOverlay.ComputeChipRect(1920f, 1200f, full, width);

            var start = HudOverlay.ComputeDeltaLabelRect(chip, 0f);
            var risen = HudOverlay.ComputeDeltaLabelRect(chip, CoinChipAnimation.DeltaRiseDistancePx);

            Assert.That(risen.yMin, Is.LessThan(start.yMin), "the label moves up as it rises");
            Assert.That(start.yMin - risen.yMin, Is.EqualTo(CoinChipAnimation.DeltaRiseDistancePx),
                "it rises exactly the full rise distance");
        }

        [Test]
        public void CountUp_InFlight_ShowsTheTweenedValue_NotTheRawLiveCoins()
        {
            // #542: while the count-up tween runs, the chip shows the tweened
            // value — not the raw live Wallet.Coins, which has already jumped to
            // the new total.
            var overlay = host.AddComponent<HudOverlay>();
            var state = GameState.CreateNew();
            overlay.Init(state);

            state.Wallet.Deposit(100);

            Assert.That(overlay.Label, Is.EqualTo("100"), "the raw live wallet already reads the new total");
            Assert.That(overlay.DisplayedLabel(0f), Is.EqualTo("0"), "the tween starts at the old value");
            Assert.That(overlay.DisplayedLabel(CoinChipAnimation.CountUpDurationSec / 2f), Is.EqualTo("50"),
                "half-way through, half-way counted up");
            Assert.That(overlay.DisplayedLabel(CoinChipAnimation.CountUpDurationSec), Is.EqualTo("100"),
                "the tween reaches the new total at CountUpDurationSec");
        }

        [Test]
        public void DeltaAnimationConstants_MatchTheApprovedWireframeValues()
        {
            // #542 approved proposal (shared-components.md CurrencyChip table).
            Assert.That(HudOverlay.DeltaFontSizePx, Is.EqualTo(32), "delta label font size");
            Assert.That(HudOverlay.DeltaOffsetYPx, Is.EqualTo(12f), "gap: chip bottom -> delta label");
            Assert.That(CoinChipAnimation.DeltaRiseDistancePx, Is.EqualTo(48f), "rise distance");
            Assert.That(CoinChipAnimation.DeltaRiseDurationSec, Is.EqualTo(0.9f), "rise + fade duration");
            Assert.That(CoinChipAnimation.CountUpDurationSec, Is.EqualTo(0.5f), "count-up duration");
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
