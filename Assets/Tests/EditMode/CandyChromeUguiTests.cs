using Doggiehood.Core.Ui;
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
        public void ApplyRounded_SetsSlicedSpriteFill_PlusInkContourBandAndHardShadow()
        {
            var parent = new GameObject("parent", typeof(RectTransform));
            var go = new GameObject("chrome", typeof(RectTransform));
            go.transform.SetParent(parent.transform, false);
            var rt = (RectTransform)go.transform;
            rt.sizeDelta = new Vector2(200f, 120f);
            var image = go.AddComponent<Image>();

            CandyChromeUgui.ApplyRounded(image, CandyChromeUgui.Panel, CandyChromeUgui.PanelRadiusPx, withShadow: true);

            Assert.That(image.type, Is.EqualTo(Image.Type.Sliced),
                "rounded corners come from a 9-sliced procedural sprite, not a stretched quad");
            Assert.That(image.sprite, Is.Not.Null);
            AssertHex(image.color, 0xFF, 0xFD, 0xF7, "panel fill");

            // #616: no offset-copy Outline mesh effect any more — the outline is an
            // Ink constant-width contour band drawn behind the fill.
            Assert.That(go.GetComponent<Outline>(), Is.Null,
                "the offset-copy Outline mesh effect is gone (#616)");

            var ink = CandyChromeUgui.OutlineInk(go);
            Assert.That(ink, Is.Not.Null, "chrome carries an Ink contour-band underlay");
            AssertHex(ink.color, 0x2E, 0x2A, 0x26, "outline");
            Assert.That(ink.type, Is.EqualTo(Image.Type.Sliced));
            Assert.That(ink.raycastTarget, Is.False, "the outline band must not intercept taps");
            Assert.That(ink.material, Is.EqualTo(ink.defaultMaterial),
                "the outline band renders through the default UI material (device-safe)");

            var inkRt = ink.rectTransform;
            Assert.That(inkRt.GetSiblingIndex(), Is.LessThan(rt.GetSiblingIndex()),
                "the band renders behind the fill");
            var w = CandyChromeUgui.OutlineThicknessPx;
            Assert.That(rt.offsetMin.x - inkRt.offsetMin.x, Is.EqualTo(w).Within(0.01f));
            Assert.That(rt.offsetMin.y - inkRt.offsetMin.y, Is.EqualTo(w).Within(0.01f));
            Assert.That(inkRt.offsetMax.x - rt.offsetMax.x, Is.EqualTo(w).Within(0.01f));
            Assert.That(inkRt.offsetMax.y - rt.offsetMax.y, Is.EqualTo(w).Within(0.01f),
                "the band is a uniform OutlineThicknessPx on every side");
            Assert.That(ink.sprite.border.x, Is.EqualTo(CandyChromeUgui.PanelRadiusPx + w),
                "the band's corner radius is the fill radius + W, so its inner edge sits on the fill contour");

            var shadow = PureShadow(go);
            Assert.That(shadow, Is.Not.Null,
                "a hard drop-shadow (the plain Shadow, not the Outline subclass) is present");
            AssertHex(shadow.effectColor, 0x2E, 0x2A, 0x26, "shadow");
            Assert.That(shadow.effectDistance, Is.EqualTo(new Vector2(0f, -CandyChromeUgui.ShadowOffsetPx)),
                "the shadow is a single hard offset straight down — no blur");

            Object.DestroyImmediate(parent);
        }

        [Test]
        public void ApplyRounded_WithoutShadow_AddsContourBandButNoDropShadow()
        {
            var parent = new GameObject("parent", typeof(RectTransform));
            var go = new GameObject("outline-only", typeof(RectTransform));
            go.transform.SetParent(parent.transform, false);
            ((RectTransform)go.transform).sizeDelta = new Vector2(120f, 56f);
            var image = go.AddComponent<Image>();

            CandyChromeUgui.ApplyRounded(image, CandyChromeUgui.Leaf, 28f, withShadow: false);

            Assert.That(go.GetComponent<Outline>(), Is.Null);
            Assert.That(CandyChromeUgui.OutlineInk(go), Is.Not.Null);
            Assert.That(PureShadow(go), Is.Null,
                "a switch track carries the outline only, matching the wireframe toggle (no drop-shadow)");

            Object.DestroyImmediate(parent);
        }

        [Test]
        public void AddOutline_WithACustomThickness_ResizesTheSameBandIdempotently()
        {
            // The Welcome portrait / Onboarding medal path: ApplyPill lays the shared
            // 6px band, then AddOutline re-sizes it to a thicker ring. It must reuse
            // the one band (not stack a second), and the band must widen to match.
            var parent = new GameObject("parent", typeof(RectTransform));
            var go = new GameObject("medal", typeof(RectTransform));
            go.transform.SetParent(parent.transform, false);
            ((RectTransform)go.transform).sizeDelta = new Vector2(160f, 160f);
            var image = go.AddComponent<Image>();

            CandyChromeUgui.ApplyPill(image, CandyChromeUgui.Gold, 160f, withShadow: true);
            var first = CandyChromeUgui.OutlineInk(go);
            CandyChromeUgui.AddOutline(go, 80f, 8f);
            var second = CandyChromeUgui.OutlineInk(go);

            Assert.That(second, Is.SameAs(first), "the band is reused, not stacked");
            var rt = (RectTransform)go.transform;
            var inkRt = second.rectTransform;
            Assert.That(rt.offsetMin.x - inkRt.offsetMin.x, Is.EqualTo(8f).Within(0.01f),
                "the band widened to the custom 8px thickness");
            Assert.That(second.sprite.border.x, Is.EqualTo(80f + 8f),
                "the band's corner radius tracks the custom thickness");

            Object.DestroyImmediate(parent);
        }

        [Test]
        public void RenderedChromeAlpha_HasAConstantWidthInkBand_AroundEveryCornerArc()
        {
            // Item 5 (#616): run the shared Core ray-march checker against the
            // ACTUAL baked chrome alpha (RoundedRectCoverage output) for a pip and a
            // panel corner. The band must be a flat W around the corner arcs; the
            // axis-aligned straight strips reach the sprite border and are excluded.
            AssertBakedBandIsFlatAroundCorners(PipRadiusPx);
            AssertBakedBandIsFlatAroundCorners(CandyChromeUgui.PanelRadiusPx);
        }

        private const float PipRadiusPx = 12f;

        private static void AssertBakedBandIsFlatAroundCorners(float radiusPx)
        {
            var w = CandyChromeUgui.OutlineThicknessPx;
            var fillTex = (Texture2D)CandyChromeUgui.RoundedSprite(radiusPx).texture;
            var inkTex = (Texture2D)CandyChromeUgui.RoundedSprite(radiusPx + w).texture;

            System.Func<double, double, double> fillAlpha = (x, y) =>
                fillTex.GetPixelBilinear((float)(x / fillTex.width + 0.5), (float)(y / fillTex.height + 0.5)).a;
            System.Func<double, double, double> inkAlpha = (x, y) =>
                inkTex.GetPixelBilinear((float)(x / inkTex.width + 0.5), (float)(y / inkTex.height + 0.5)).a;

            const int angleCount = 720;
            var maxMarch = radiusPx * 2.0 + w + 8.0;
            var widths = RoundedRectContour.MeasureBandWidths(fillAlpha, inkAlpha, 0.0, 0.0, maxMarch, angleCount);

            for (var k = 0; k < angleCount; k++)
            {
                var deg = 360.0 * k / angleCount;
                var mod = deg % 90.0;
                var offAxis = System.Math.Min(mod, 90.0 - mod);
                if (offAxis < 15.0)
                {
                    continue; // straight strip runs to the sprite border — not a contour ray
                }

                Assert.That(widths[k], Is.EqualTo((double)w).Within(1.0),
                    "baked Ink band is outside +/-1px at " + deg + " deg (radius " + radiusPx + ")");
            }
        }

        [Test]
        public void ApplyPill_UsesHalfHeightCornerRadius_ForAFullyRoundEnd()
        {
            var parent = new GameObject("parent", typeof(RectTransform));
            var go = new GameObject("pill", typeof(RectTransform));
            go.transform.SetParent(parent.transform, false);
            var image = go.AddComponent<Image>();

            CandyChromeUgui.ApplyPill(image, CandyChromeUgui.Gold, 72f, withShadow: true);

            Assert.That(image.sprite.border, Is.EqualTo(new Vector4(36f, 36f, 36f, 36f)),
                "a full pill's corner radius is half its height, so the ends are semicircles");

            Object.DestroyImmediate(parent);
        }

        [Test]
        public void ChromeImages_UseTheDefaultUiMaterial_NoCustomShaderToStrip()
        {
            // #291 device-safety by construction: the helper never assigns a
            // custom material, so every chromed Image renders through the
            // always-included UI/Default material (guarded by UiBuildResourcesTests).
            var parent = new GameObject("parent", typeof(RectTransform));
            var go = new GameObject("chrome", typeof(RectTransform));
            go.transform.SetParent(parent.transform, false);
            var image = go.AddComponent<Image>();

            CandyChromeUgui.ApplyRounded(image, CandyChromeUgui.Panel, CandyChromeUgui.PanelRadiusPx, withShadow: true);

            Assert.That(image.material, Is.EqualTo(image.defaultMaterial));

            Object.DestroyImmediate(parent);
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
