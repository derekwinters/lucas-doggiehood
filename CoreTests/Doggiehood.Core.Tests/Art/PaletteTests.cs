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
        public void HouseTintPalette_Is20EvenlySpacedHues_SharingSaturationAndValue()
        {
            // #299: the zone-house tint palette is GENERATED, not authored as
            // 20 literals — 20 hues evenly around the wheel (18 deg apart),
            // fixed S = 0.70, V = 0.90, converted HSV->RGB. Assert the rule,
            // not fixed hex strings, so a palette retune only touches S/V/hue.
            for (var i = 0; i < HouseVariantAssignment.TintCount; i++)
            {
                var hex = Palette.HouseTintHex(i);
                Assert.That(() => ColorRgb.Parse(hex), Throws.Nothing, $"tint {i} is a valid hex");

                var color = ColorRgb.Parse(hex);
                Assert.That(color.Hue, Is.EqualTo(i * Palette.HouseTintHueStepDegrees).Within(2f),
                    $"tint {i} hue is {i} steps of {Palette.HouseTintHueStepDegrees} deg around the wheel");
                Assert.That(color.Saturation, Is.EqualTo(Palette.HouseTintSaturation).Within(0.02f),
                    $"tint {i} shares the fixed saturation");
                Assert.That(color.Value, Is.EqualTo(Palette.HouseTintValue).Within(0.02f),
                    $"tint {i} shares the fixed value");
            }
        }

        [Test]
        public void HouseTintPalette_HasEvenHueStep_ForItsSize()
        {
            // The step is derived from the count (360 / 20), not hard-coded to
            // 18 in two places — retuning the count retunes the spacing.
            Assert.That(Palette.HouseTintHueStepDegrees,
                Is.EqualTo(360f / HouseVariantAssignment.TintCount).Within(0.0001f));
        }

        [Test]
        public void HouseTintHex_ThrowsForAnIndexOutOfRange()
        {
            Assert.That(() => Palette.HouseTintHex(-1), Throws.InstanceOf<System.ArgumentOutOfRangeException>());
            Assert.That(() => Palette.HouseTintHex(HouseVariantAssignment.TintCount),
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
