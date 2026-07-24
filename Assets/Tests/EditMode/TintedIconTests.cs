using Doggiehood.Unity;
using NUnit.Framework;
using UnityEngine;

namespace Doggiehood.Unity.EditModeTests
{
    /// <summary>
    /// #178: the staged lock icon (#183,
    /// Assets/Art/UI/ExpansionIndicator/Resources/locked.png) is a literal
    /// black silhouette — RGB (0,0,0), shape carried entirely in the alpha
    /// channel. A standard multiply tint (SpriteRenderer.color, UI
    /// Image.color) can never recolor that: black times any tint is still
    /// black. TintedIcon.Recolor instead builds a new texture that keeps
    /// the source's alpha but replaces every pixel's RGB with the
    /// requested tint outright.
    /// </summary>
    public class TintedIconTests
    {
        [Test]
        public void Recolor_ReplacesRgb_ButPreservesSourceAlpha()
        {
            var source = new Texture2D(2, 1, TextureFormat.RGBA32, false);
            source.SetPixels32(new[]
            {
                new Color32(0, 0, 0, 255), // opaque silhouette pixel
                new Color32(0, 0, 0, 0), // transparent background pixel
            });
            source.Apply();

            var sprite = TintedIcon.Recolor(source, Color.yellow);

            var pixels = sprite.texture.GetPixels32();
            var expectedTint = (Color32)Color.yellow;
            Assert.That(pixels[0].r, Is.EqualTo(expectedTint.r));
            Assert.That(pixels[0].g, Is.EqualTo(expectedTint.g));
            Assert.That(pixels[0].b, Is.EqualTo(expectedTint.b));
            Assert.That(pixels[0].a, Is.EqualTo(255));

            Assert.That(pixels[1].a, Is.EqualTo(0));
        }

        [Test]
        public void Recolor_KeepsTheSourcesDimensions()
        {
            var source = new Texture2D(4, 3, TextureFormat.RGBA32, false);
            source.Apply();

            var sprite = TintedIcon.Recolor(source, Color.grey);

            Assert.That(sprite.texture.width, Is.EqualTo(4));
            Assert.That(sprite.texture.height, Is.EqualTo(3));
        }
    }
}
