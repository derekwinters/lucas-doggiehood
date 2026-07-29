using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace Doggiehood.Unity.EditModeTests
{
    /// <summary>
    /// #298: the shared UGUI "Candy Cottage" chrome helper. Unlike the IMGUI
    /// <see cref="Doggiehood.Unity.CandyChrome"/> (HUD/onboarding overlays draw
    /// on OnGUI), this realizes the same shared baseline
    /// (docs/specs/ui/shared-components.md) for retained UGUI panels built under
    /// the #256 CanvasScaler — a procedurally generated rounded sprite plus the
    /// built-in <c>Outline</c>/<c>Shadow</c> mesh effects, so it stays
    /// device-safe (only the always-included <c>UI/Default</c> shader, no raster
    /// art asset — see #291 / docs/engineering/unity-serialization.md §5).
    /// </summary>
    public class CandyChromeUguiTests
    {
        [Test]
        public void BaselineConstants_MatchTheSharedComponentsBaseline()
        {
            Assert.That(CandyChromeUgui.OutlineThicknessPx, Is.EqualTo(6f));
            Assert.That(CandyChromeUgui.ShadowOffsetPx, Is.EqualTo(8f));
            Assert.That(CandyChromeUgui.PillRadiusPx, Is.EqualTo(999f));
            Assert.That(CandyChromeUgui.PanelRadiusPx, Is.EqualTo(40f));
        }

        [Test]
        public void Palette_MatchesTheCandyCottageHexValues()
        {
            AssertHex(CandyChromeUgui.Ink, 0x2E, 0x2A, 0x26, "Ink");
            AssertHex(CandyChromeUgui.Cream, 0xFF, 0xF3, 0xD9, "Cream");
            AssertHex(CandyChromeUgui.Panel, 0xFF, 0xFD, 0xF7, "Panel");
            AssertHex(CandyChromeUgui.Coral, 0xFF, 0x7A, 0x5C, "Coral");
            AssertHex(CandyChromeUgui.Leaf, 0x58, 0xC0, 0x6A, "Leaf");
            AssertHex(CandyChromeUgui.Gold, 0xFF, 0xC2, 0x3C, "Gold");
            AssertHex(CandyChromeUgui.Disabled, 0xD8, 0xD2, 0xC6, "Disabled");
        }

        [Test]
        public void RoundedSprite_IsBorderedForNineSlice_AndCachedPerRadius()
        {
            var sprite = CandyChromeUgui.RoundedSprite(CandyChromeUgui.PanelRadiusPx);

            Assert.That(sprite, Is.Not.Null);
            Assert.That(sprite.border, Is.EqualTo(new Vector4(40f, 40f, 40f, 40f)),
                "the 9-slice border equals the corner radius so a stretched panel keeps a fixed radius");

            var again = CandyChromeUgui.RoundedSprite(CandyChromeUgui.PanelRadiusPx);
            Assert.That(again, Is.SameAs(sprite),
                "sprites are cached per radius — no per-frame texture churn");
        }

        [Test]
        public void ApplyRounded_SetsSlicedSpriteFill_PlusInkOutlineAndHardShadow()
        {
            var go = new GameObject("chrome", typeof(RectTransform));
            var image = go.AddComponent<Image>();

            CandyChromeUgui.ApplyRounded(image, CandyChromeUgui.Panel, CandyChromeUgui.PanelRadiusPx, withShadow: true);

            Assert.That(image.type, Is.EqualTo(Image.Type.Sliced),
                "rounded corners come from a 9-sliced procedural sprite, not a stretched quad");
            Assert.That(image.sprite, Is.Not.Null);
            AssertHex(image.color, 0xFF, 0xFD, 0xF7, "panel fill");

            var outline = go.GetComponent<Outline>();
            Assert.That(outline, Is.Not.Null, "chrome carries a thick dark outline");
            AssertHex(outline.effectColor, 0x2E, 0x2A, 0x26, "outline");
            Assert.That(outline.effectDistance,
                Is.EqualTo(new Vector2(CandyChromeUgui.OutlineThicknessPx, CandyChromeUgui.OutlineThicknessPx)));

            var shadow = PureShadow(go);
            Assert.That(shadow, Is.Not.Null,
                "a hard drop-shadow (the plain Shadow, not the Outline subclass) is present");
            AssertHex(shadow.effectColor, 0x2E, 0x2A, 0x26, "shadow");
            Assert.That(shadow.effectDistance, Is.EqualTo(new Vector2(0f, -CandyChromeUgui.ShadowOffsetPx)),
                "the shadow is a single hard offset straight down — no blur");

            Object.DestroyImmediate(go);
        }

        [Test]
        public void ApplyRounded_WithoutShadow_AddsOutlineButNoDropShadow()
        {
            var go = new GameObject("outline-only", typeof(RectTransform));
            var image = go.AddComponent<Image>();

            CandyChromeUgui.ApplyRounded(image, CandyChromeUgui.Leaf, 28f, withShadow: false);

            Assert.That(go.GetComponent<Outline>(), Is.Not.Null);
            Assert.That(PureShadow(go), Is.Null,
                "a switch track carries the outline only, matching the wireframe toggle (no drop-shadow)");

            Object.DestroyImmediate(go);
        }

        [Test]
        public void ApplyPill_UsesHalfHeightCornerRadius_ForAFullyRoundEnd()
        {
            var go = new GameObject("pill", typeof(RectTransform));
            var image = go.AddComponent<Image>();

            CandyChromeUgui.ApplyPill(image, CandyChromeUgui.Gold, 72f, withShadow: true);

            Assert.That(image.sprite.border, Is.EqualTo(new Vector4(36f, 36f, 36f, 36f)),
                "a full pill's corner radius is half its height, so the ends are semicircles");

            Object.DestroyImmediate(go);
        }

        [Test]
        public void ChromeImages_UseTheDefaultUiMaterial_NoCustomShaderToStrip()
        {
            // #291 device-safety by construction: the helper never assigns a
            // custom material, so every chromed Image renders through the
            // always-included UI/Default material (guarded by UiBuildResourcesTests).
            var go = new GameObject("chrome", typeof(RectTransform));
            var image = go.AddComponent<Image>();

            CandyChromeUgui.ApplyRounded(image, CandyChromeUgui.Panel, CandyChromeUgui.PanelRadiusPx, withShadow: true);

            Assert.That(image.material, Is.EqualTo(image.defaultMaterial));

            Object.DestroyImmediate(go);
        }

        private static Shadow PureShadow(GameObject go)
        {
            foreach (var shadow in go.GetComponents<Shadow>())
            {
                if (shadow.GetType() == typeof(Shadow))
                {
                    return shadow;
                }
            }

            return null;
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
