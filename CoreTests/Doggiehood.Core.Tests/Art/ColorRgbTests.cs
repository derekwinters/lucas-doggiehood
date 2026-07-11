using Doggiehood.Core.Art;
using NUnit.Framework;

namespace Doggiehood.Core.Tests.Art
{
    public class ColorRgbTests
    {
        [Test]
        public void Parse_ReadsAHexColor()
        {
            var color = ColorRgb.Parse("#FF6F61");

            Assert.That(color.R, Is.EqualTo(1f).Within(0.001f));
            Assert.That(color.G, Is.EqualTo(0x6F / 255f).Within(0.001f));
            Assert.That(color.B, Is.EqualTo(0x61 / 255f).Within(0.001f));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("FF6F61")]
        [TestCase("#FF6F6")]
        [TestCase("#GGGGGG")]
        public void Parse_RejectsInvalidInput(string input)
        {
            Assert.That(() => ColorRgb.Parse(input), Throws.ArgumentException);
        }

        [Test]
        public void SaturationAndValue_MatchHsvMath()
        {
            var red = ColorRgb.Parse("#FF0000");
            Assert.That(red.Saturation, Is.EqualTo(1f).Within(0.001f));
            Assert.That(red.Value, Is.EqualTo(1f).Within(0.001f));

            var gray = ColorRgb.Parse("#808080");
            Assert.That(gray.Saturation, Is.EqualTo(0f).Within(0.001f));
            Assert.That(gray.Value, Is.EqualTo(0x80 / 255f).Within(0.001f));
        }
    }
}
