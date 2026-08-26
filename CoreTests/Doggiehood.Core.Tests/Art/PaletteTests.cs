using Doggiehood.Core.Art;
using NUnit.Framework;

namespace Doggiehood.Core.Tests.Art
{
    public class PaletteTests
    {
        [Test]
        public void GrassIsBrightAndSaturated()
        {
            var grass = ColorRgb.Parse(Palette.GrassHex);

            Assert.That(grass.Saturation, Is.GreaterThanOrEqualTo(0.5f));
            Assert.That(grass.Value, Is.GreaterThanOrEqualTo(0.6f));
        }

        [Test]
        public void StreetAndSidewalkParseAsValidColors()
        {
            // Streets/sidewalks are deliberately neutral so the bright
            // houses pop; they only need to be valid colors.
            Assert.That(() => ColorRgb.Parse(Palette.StreetHex), Throws.Nothing);
            Assert.That(() => ColorRgb.Parse(Palette.SidewalkHex), Throws.Nothing);
        }

        [Test]
        public void GrassVergeAndCrosswalk_AreValidColors_DistinctFromEveryOtherSurface()
        {
            // #106: WorldBuilder renders road/verge/sidewalk/crosswalk as
            // visually distinct placeholder surfaces — the palette must
            // actually give it four distinct hex values to draw from.
            Assert.That(() => ColorRgb.Parse(Palette.GrassVergeHex), Throws.Nothing);
            Assert.That(() => ColorRgb.Parse(Palette.CrosswalkHex), Throws.Nothing);

            var surfaces = new[] { Palette.StreetHex, Palette.GrassVergeHex, Palette.SidewalkHex, Palette.CrosswalkHex };
            Assert.That(surfaces, Is.Unique);
        }

        [Test]
        public void PoolGraybox_IsAGrayShellAroundBlueWater()
        {
            // #740, Derek: "a gray outer surface, and blue interior". The
            // graybox pool's two colours are named palette entries (#161), the
            // same way DecorationView picks its graybox colours.
            var shell = ColorRgb.Parse(Palette.PoolShellHex);
            var water = ColorRgb.Parse(Palette.PoolWaterHex);

            Assert.That(shell.Saturation, Is.LessThanOrEqualTo(0.1f),
                "the outer shell reads as gray, not as a tinted surface");

            Assert.That(water.Hue, Is.InRange(180f, 260f), "the interior reads as blue");
            Assert.That(water.Saturation, Is.GreaterThanOrEqualTo(0.4f),
                "the water is saturated enough to read as water against the gray shell");

            Assert.That(Palette.PoolShellHex, Is.Not.EqualTo(Palette.PoolWaterHex));
        }

        // #519 (Derek & Lucas, 2026-08-02): the zone-house tint palette is a
        // CURATED explicit 20-entry list, not the old generated even-18-deg-hue
        // rule — 10 slots kept, the 10 flagged (electric) ones softened, cool
        // blues/violets nudged lighter. Index-stable (TintCount stays 20) so
        // every persisted house tint INDEX recolors for free, no save migration.
        private static readonly string[] ApprovedHouseTints =
        {
            "#E64545", "#E67545", "#E6A545", "#E6D545", "#C5E645",
            "#95E645", "#88D15E", "#6ACC9E", "#45E685", "#45E6B5",
            "#45E6E6", "#45B5E6", "#6AB5EB", "#809DED", "#9E8EED",
            "#C18AE6", "#D87EE6", "#E879D9", "#ED72AF", "#ED6B85",
        };

        [Test]
        public void HouseTintPalette_IsTheCurated20EntryList()
        {
            // Pin every index to its approved curated colour (#519).
            Assert.That(ApprovedHouseTints.Length, Is.EqualTo(HouseVariantAssignment.TintCount),
                "the curated palette has exactly TintCount entries");

            for (var i = 0; i < HouseVariantAssignment.TintCount; i++)
            {
                Assert.That(Palette.HouseTintHex(i), Is.EqualTo(ApprovedHouseTints[i]),
                    $"tint {i} is the approved curated colour");
            }
        }

        [Test]
        public void HouseTintPalette_EntriesAreAllValidAndDistinct()
        {
            var tints = new string[HouseVariantAssignment.TintCount];
            for (var i = 0; i < HouseVariantAssignment.TintCount; i++)
            {
                tints[i] = Palette.HouseTintHex(i);
                Assert.That(() => ColorRgb.Parse(tints[i]), Throws.Nothing, $"tint {i} is a valid hex");
            }

            Assert.That(tints, Is.Unique, "every curated tint is a distinct colour");
        }

        [Test]
        public void HouseTintHex_ThrowsForAnIndexOutOfRange()
        {
            Assert.That(() => Palette.HouseTintHex(-1), Throws.InstanceOf<System.ArgumentOutOfRangeException>());
            Assert.That(() => Palette.HouseTintHex(HouseVariantAssignment.TintCount),
                Throws.InstanceOf<System.ArgumentOutOfRangeException>());
        }

        [Test]
        public void LostItemGlow_IsABrightSaturatedRed_ThatPopsOffEverySurface()
        {
            // #521: the lost-item finder glow is RED (Derek's decision), a
            // named Palette constant. It must be a genuinely red, bright,
            // saturated colour so it reads on sidewalk (#EFE8D8), grass
            // (#7ED957) and road (#8A8FA3) alike, and be distinct from those
            // surfaces.
            Assert.That(() => ColorRgb.Parse(Palette.LostItemGlowHex), Throws.Nothing);

            var glow = ColorRgb.Parse(Palette.LostItemGlowHex);
            Assert.That(glow.Hue, Is.LessThanOrEqualTo(20f).Or.GreaterThanOrEqualTo(340f),
                "the finder glow is red (hue near 0 deg)");
            Assert.That(glow.Saturation, Is.GreaterThanOrEqualTo(0.6f), "vividly saturated");
            Assert.That(glow.Value, Is.GreaterThanOrEqualTo(0.7f), "bright");

            foreach (var surface in new[] { Palette.SidewalkHex, Palette.GrassHex, Palette.StreetHex })
            {
                Assert.That(Palette.LostItemGlowHex, Is.Not.EqualTo(surface));
            }
        }

        // #601 (Derek, 2026-08-05): delivery trucks carry a small CURATED
        // standard car-color spread — real-world car colors, NOT the broad
        // decorative 20-tint house table — applied per-spawn as a material
        // color-multiply. Pin every index to its approved colour.
        private static readonly string[] ApprovedCarColors =
        {
            "#EDEDED", // white
            "#2B2B2B", // black
            "#C4C8CC", // silver
            "#83878C", // gray
            "#B32424", // red
            "#23366B", // dark blue
            "#235939", // dark green
        };

        [Test]
        public void CarColorPalette_IsTheCuratedStandardCarSpread()
        {
            Assert.That(ApprovedCarColors.Length, Is.EqualTo(CarColorAssignment.CarColorCount),
                "the curated car palette has exactly CarColorCount entries");

            for (var i = 0; i < CarColorAssignment.CarColorCount; i++)
            {
                Assert.That(Palette.CarColorHex(i), Is.EqualTo(ApprovedCarColors[i]),
                    $"car color {i} is the approved standard car colour");
            }
        }

        [Test]
        public void CarColorPalette_EntriesAreAllValidAndDistinct()
        {
            var colors = new string[CarColorAssignment.CarColorCount];
            for (var i = 0; i < CarColorAssignment.CarColorCount; i++)
            {
                colors[i] = Palette.CarColorHex(i);
                Assert.That(() => ColorRgb.Parse(colors[i]), Throws.Nothing, $"car color {i} is a valid hex");
            }

            Assert.That(colors, Is.Unique, "every curated car color is a distinct colour");
        }

        [Test]
        public void CarColorHex_ThrowsForAnIndexOutOfRange()
        {
            Assert.That(() => Palette.CarColorHex(-1), Throws.InstanceOf<System.ArgumentOutOfRangeException>());
            Assert.That(() => Palette.CarColorHex(CarColorAssignment.CarColorCount),
                Throws.InstanceOf<System.ArgumentOutOfRangeException>());
        }

        [Test]
        public void YardLandscapingFallback_IsAValidColor_DistinctFromTheGrassGround()
        {
            // #170: the graybox-fallback yard tree marker must read as its
            // own object against the lawn, not blend into it.
            Assert.That(() => ColorRgb.Parse(Palette.YardLandscapingFallbackHex), Throws.Nothing);
            Assert.That(Palette.YardLandscapingFallbackHex, Is.Not.EqualTo(Palette.GrassHex));
        }
    }
}
