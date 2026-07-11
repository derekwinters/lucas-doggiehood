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
    }
}
