using Doggiehood.Unity;
using NUnit.Framework;
using UnityEngine;

namespace Doggiehood.Unity.EditModeTests
{
    /// <summary>
    /// #297: the shared Candy Cottage procedural-chrome helper extracted from
    /// the HUD chip (#296). Guards the fixed palette (shared-components.md) and
    /// that the white anti-aliased circle texture builds procedurally (no
    /// external raster art), so every IMGUI overlay composes identical chrome.
    /// </summary>
    public class CandyChromeTests
    {
        [Test]
        public void Palette_MatchesTheFixedCandyCottageColors()
        {
            // shared-components.md: Ink #2E2A26, Cream #FFF3D9, Gold #FFC23C, Leaf #58C06A.
            AssertHex(CandyChrome.InkColor, 0x2E, 0x2A, 0x26, "Ink");
            AssertHex(CandyChrome.CreamColor, 0xFF, 0xF3, 0xD9, "Cream");
            AssertHex(CandyChrome.GoldColor, 0xFF, 0xC2, 0x3C, "Gold");
            AssertHex(CandyChrome.LeafColor, 0x58, 0xC0, 0x6A, "Leaf");
        }

        [Test]
        public void CircleTexture_BuildsProcedurally_AndIsCachedAcrossCalls()
        {
            var first = CandyChrome.CircleTexture();
            Assert.That(first, Is.Not.Null, "the procedural circle texture must build without an art asset");
            Assert.That(first.width, Is.GreaterThan(0));
            Assert.That(first.height, Is.EqualTo(first.width), "the chrome texture is a square circle");

            var second = CandyChrome.CircleTexture();
            Assert.That(second, Is.SameAs(first), "the texture is generated once and reused");
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
