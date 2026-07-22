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
        public void YardLandscapingFallback_IsAValidColor_DistinctFromTheGrassGround()
        {
            // #170: the graybox-fallback yard tree/planter marker must read
            // as its own object against the lawn, not blend into it.
            Assert.That(() => ColorRgb.Parse(Palette.YardLandscapingFallbackHex), Throws.Nothing);
            Assert.That(Palette.YardLandscapingFallbackHex, Is.Not.EqualTo(Palette.GrassHex));
        }
    }
}
