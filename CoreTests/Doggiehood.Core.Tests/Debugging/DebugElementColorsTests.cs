using Doggiehood.Core.Art;
using Doggiehood.Core.Debugging;
using NUnit.Framework;

namespace Doggiehood.Core.Tests.Debugging
{
    /// <summary>
    /// #611: the diagnostic debug-element palette. When the Debug-tab "Show
    /// debug element colors" toggle is on, the ground plane and the camera void
    /// backstop are painted in two loudly-different, obviously-fake colours (NOT
    /// shades of green) so that a playtester can tell, unambiguously, which
    /// element the bottom-of-screen border actually is. Off (default), both fall
    /// back to <see cref="Palette.GrassHex"/> — today's exact matched look.
    /// Pure data + the on/off colour decision; no Unity dependency.
    /// </summary>
    public class DebugElementColorsTests
    {
        [Test]
        public void GroundAndBackstopDebugColors_AreDistinctFromEachOther()
        {
            // The whole point of the diagnostic is telling the two apart, so the
            // debug ground colour and debug backstop colour must differ.
            Assert.That(DebugElementColors.GroundDebugHex,
                Is.Not.EqualTo(DebugElementColors.BackstopDebugHex));
        }

        [Test]
        public void DebugColors_AreObviouslyFake_NotShadesOfGrass()
        {
            // "not shades of green" — each debug colour must differ from the
            // matched GrassHex so the seam pops instead of blending in.
            Assert.That(DebugElementColors.GroundDebugHex, Is.Not.EqualTo(Palette.GrassHex));
            Assert.That(DebugElementColors.BackstopDebugHex, Is.Not.EqualTo(Palette.GrassHex));

            // They must at least be well-formed, parseable colours.
            Assert.That(() => ColorRgb.Parse(DebugElementColors.GroundDebugHex), Throws.Nothing);
            Assert.That(() => ColorRgb.Parse(DebugElementColors.BackstopDebugHex), Throws.Nothing);
        }

        [Test]
        public void GroundHex_IsGrass_WhenDebugOff_AndTheDebugColor_WhenOn()
        {
            Assert.That(DebugElementColors.GroundHex(false), Is.EqualTo(Palette.GrassHex));
            Assert.That(DebugElementColors.GroundHex(true), Is.EqualTo(DebugElementColors.GroundDebugHex));
        }

        [Test]
        public void BackstopHex_IsGrass_WhenDebugOff_AndTheDebugColor_WhenOn()
        {
            Assert.That(DebugElementColors.BackstopHex(false), Is.EqualTo(Palette.GrassHex));
            Assert.That(DebugElementColors.BackstopHex(true), Is.EqualTo(DebugElementColors.BackstopDebugHex));
        }
    }
}
