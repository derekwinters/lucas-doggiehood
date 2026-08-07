using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Doggiehood.Unity
{
    /// <summary>
    /// Shared "Candy Cottage" chrome for retained <b>UGUI</b> panels (#298),
    /// the UGUI counterpart to the IMGUI <see cref="CandyChrome"/>. Both realize
    /// the one shared baseline in docs/specs/ui/shared-components.md so every
    /// screen draws identical chrome rather than hand-picking values (#161).
    ///
    /// Chrome is fully procedural and <b>device-safe</b> (#291): a rounded-rect
    /// sprite generated at runtime as an in-memory <see cref="Texture2D"/> (no
    /// raster art asset, no build-stripping) rendered through the default
    /// <c>UI/Default</c> material — which lives in Always Included Shaders and is
    /// guarded by <c>UiBuildResourcesTests</c>. Nothing here assigns a custom
    /// material, so there is no shader to strip and no magenta box.
    ///
    /// <para><b>Outline (#616):</b> the dark outline is a <b>constant-width contour
    /// band</b>, not Unity's <see cref="Outline"/> mesh effect. That effect stamps
    /// four diagonally-offset copies of the graphic, whose union is not a
    /// constant-width band around a curve — it bulges on-axis and dips/flattens
    /// off-axis, the uneven look Derek reported. Instead <see cref="AddOutline"/>
    /// places a second Ink <see cref="Image"/> — the same procedural rounded sprite
    /// inflated by the band width — behind the fill, so a uniform band of Ink shows
    /// around the <i>entire</i> rounded contour, corners included. The fill's own
    /// contour is the band's inner edge by construction, so the fill radius and the
    /// outline's inner radius can never drift. The geometry invariant is proved in
    /// <c>Doggiehood.Core.Ui.RoundedRectContour</c>.</para>
    /// </summary>
    public static class CandyChromeUgui
    {
        // --- Shared baseline constants (docs/specs/ui/shared-components.md) ---
        public const float OutlineThicknessPx = 6f;
        public const float ShadowOffsetPx = 8f;
        public const float PillRadiusPx = 999f;
        public const float PanelRadiusPx = 40f;

        // --- Shared Candy Cottage palette (exact hex, theme-independent) ---
        public static readonly Color Ink = new Color32(0x2E, 0x2A, 0x26, 0xFF);
        public static readonly Color Cream = new Color32(0xFF, 0xF3, 0xD9, 0xFF);
        public static readonly Color Panel = new Color32(0xFF, 0xFD, 0xF7, 0xFF);
        public static readonly Color Coral = new Color32(0xFF, 0x7A, 0x5C, 0xFF);
        public static readonly Color Leaf = new Color32(0x58, 0xC0, 0x6A, 0xFF);
        public static readonly Color Gold = new Color32(0xFF, 0xC2, 0x3C, 0xFF);
        public static readonly Color Disabled = new Color32(0xD8, 0xD2, 0xC6, 0xFF);

        // Bare RawImage frames (render-texture snapshots) have no rounded fill of
        // their own; their Ink frame uses gently-rounded (square-ish) corners.
        public const float FrameCornerRadiusPx = 0f;

        // The Ink underlay is a sibling named after its fill so re-applying the
        // chrome reuses it (idempotent) instead of stacking frames.
        private const string OutlineSuffix = " Outline";

        // A small solid center strip keeps the 9-slice grid non-degenerate.
        private const int CenterStripPx = 4;

        // The generated sprite maps 1 texture pixel to 1 UI reference pixel: the
        // UiCanvas Canvas keeps the default referencePixelsPerUnit (100), so a
        // sprite authored at 100 PPU keeps the corner radius fixed under scaling.
        private const float SpritePixelsPerUnit = 100f;

        private static readonly Dictionary<int, Sprite> RoundedCache = new Dictionary<int, Sprite>();

        /// <summary>Applies rounded Candy Cottage chrome to an existing
        /// <see cref="Image"/>: a 9-sliced rounded sprite at
        /// <paramref name="cornerRadiusPx"/>, the <paramref name="fill"/> tint, a
        /// thick Ink constant-width contour outline at the same corner radius, and
        /// (when <paramref name="withShadow"/>) a flat hard drop-shadow straight
        /// down. The shadow is added first so it sits behind the fill.</summary>
        public static void ApplyRounded(Image image, Color fill, float cornerRadiusPx, bool withShadow)
        {
            image.sprite = RoundedSprite(cornerRadiusPx);
            image.type = Image.Type.Sliced;
            image.color = fill;

            if (withShadow)
            {
                AddShadow(image.gameObject);
            }

            AddOutline(image.gameObject, cornerRadiusPx, OutlineThicknessPx);
        }

        /// <summary>Applies a full pill (fully-round ends): the corner radius is
        /// half <paramref name="heightPx"/>, so the caps are semicircles
        /// (<see cref="PillRadiusPx"/> semantics).</summary>
        public static void ApplyPill(Image image, Color fill, float heightPx, bool withShadow)
        {
            ApplyRounded(image, fill, heightPx / 2f, withShadow);
        }

        /// <summary>Frames <paramref name="go"/> with the shared Ink contour outline
        /// (<see cref="OutlineThicknessPx"/>) using square-ish
        /// (<see cref="FrameCornerRadiusPx"/>) corners — for bare RawImage frames
        /// (render-texture snapshots) that carry no rounded fill of their own.</summary>
        public static Image AddOutline(GameObject go)
        {
            return AddOutline(go, FrameCornerRadiusPx, OutlineThicknessPx);
        }

        /// <summary>Adds (or re-sizes, idempotently) the Ink <b>constant-width
        /// contour band</b> behind <paramref name="go"/> (#616). The band is the
        /// fill's rounded rect inflated by <paramref name="thicknessPx"/> on every
        /// side — extents grow by W and the corner radius grows to
        /// <c>cornerRadiusPx + W</c> — realized as a second Ink <see cref="Image"/>
        /// sibling placed immediately behind the fill in the sibling order. So a
        /// uniform W of Ink shows around the whole rounded contour (corners
        /// included) and the fill's own contour is the band's inner edge by
        /// construction. Returns the Ink underlay <see cref="Image"/>.</summary>
        public static Image AddOutline(GameObject go, float cornerRadiusPx, float thicknessPx)
        {
            var fillRt = go.GetComponent<RectTransform>();
            var inkImage = FindOrCreateInk(go, fillRt);

            var inkRt = inkImage.rectTransform;
            inkRt.anchorMin = fillRt.anchorMin;
            inkRt.anchorMax = fillRt.anchorMax;
            inkRt.pivot = fillRt.pivot;
            var inflate = new Vector2(thicknessPx, thicknessPx);
            inkRt.offsetMin = fillRt.offsetMin - inflate;
            inkRt.offsetMax = fillRt.offsetMax + inflate;

            inkImage.sprite = RoundedSprite(cornerRadiusPx + thicknessPx);
            inkImage.type = Image.Type.Sliced;
            inkImage.color = Ink;
            inkImage.raycastTarget = false;

            return inkImage;
        }

        /// <summary>The Ink contour-band underlay behind <paramref name="go"/>, or
        /// <c>null</c> if it has no chrome outline. Exposed so serialization-level
        /// EditMode tests can assert the band's geometry (#616).</summary>
        public static Image OutlineInk(GameObject go)
        {
            var fillRt = go.GetComponent<RectTransform>();
            if (fillRt == null)
            {
                return null;
            }

            var parent = fillRt.parent;
            var index = fillRt.GetSiblingIndex();
            if (parent == null || index <= 0)
            {
                return null;
            }

            var prev = parent.GetChild(index - 1);
            return prev.name == go.name + OutlineSuffix ? prev.GetComponent<Image>() : null;
        }

        // Finds this fill's existing Ink underlay (the sibling immediately behind
        // it, so duplicate-named fills like level pips never cross-match) or creates
        // one and slots it directly behind the fill in the sibling order.
        private static Image FindOrCreateInk(GameObject go, RectTransform fillRt)
        {
            var existing = OutlineInk(go);
            if (existing != null)
            {
                return existing;
            }

            var inkGo = new GameObject(go.name + OutlineSuffix, typeof(RectTransform));
            var inkImage = inkGo.AddComponent<Image>();
            var parent = fillRt.parent;
            inkImage.rectTransform.SetParent(parent, false);
            if (parent != null)
            {
                // UGUI renders siblings back-to-front by index; placing the ink at
                // the fill's index pushes the fill one slot ahead so the band draws
                // behind it.
                inkImage.rectTransform.SetSiblingIndex(fillRt.GetSiblingIndex());
            }

            return inkImage;
        }

        /// <summary>Adds (or re-tints) the flat hard drop-shadow — the plain
        /// <see cref="Shadow"/>, distinct from the <see cref="Outline"/> subclass
        /// which also derives from Shadow. A single straight-down offset, no
        /// blur.</summary>
        public static Shadow AddShadow(GameObject go)
        {
            Shadow shadow = null;
            foreach (var candidate in go.GetComponents<Shadow>())
            {
                if (candidate.GetType() == typeof(Shadow))
                {
                    shadow = candidate;
                    break;
                }
            }

            if (shadow == null)
            {
                shadow = go.AddComponent<Shadow>();
            }

            shadow.effectColor = Ink;
            shadow.effectDistance = new Vector2(0f, -ShadowOffsetPx);
            shadow.useGraphicAlpha = true;
            return shadow;
        }

        /// <summary>Builds (once, cached per radius) a white anti-aliased
        /// rounded-rect sprite whose 9-slice border equals the corner radius, so
        /// stretching it to any panel/pill size keeps that fixed corner radius.
        /// Procedural — no external art asset.</summary>
        public static Sprite RoundedSprite(float cornerRadiusPx)
        {
            var radius = Mathf.Max(1, Mathf.RoundToInt(cornerRadiusPx));
            if (RoundedCache.TryGetValue(radius, out var cached) && cached != null)
            {
                return cached;
            }

            var side = radius * 2 + CenterStripPx;
            var texture = new Texture2D(side, side, TextureFormat.RGBA32, false)
            {
                name = "CandyRoundedTex-" + radius,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
            };

            var pixels = new Color32[side * side];
            for (var y = 0; y < side; y++)
            {
                for (var x = 0; x < side; x++)
                {
                    var coverage = RoundedRectCoverage(x, y, side, radius);
                    var alpha = (byte)Mathf.RoundToInt(coverage * 255f);
                    pixels[y * side + x] = new Color32(255, 255, 255, alpha);
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply();

            var sprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, side, side),
                new Vector2(0.5f, 0.5f),
                SpritePixelsPerUnit,
                0,
                SpriteMeshType.FullRect,
                new Vector4(radius, radius, radius, radius));
            sprite.name = "CandyRounded-" + radius;

            RoundedCache[radius] = sprite;
            return sprite;
        }

        /// <summary>Anti-aliased coverage of a rounded rect that fills the whole
        /// texture: the four corners are quarter-circles of <paramref name="radius"/>,
        /// the straight edges reach the texture border. Distance is measured from
        /// the pixel to its nearest point on the inner (corner-center) rectangle.</summary>
        private static float RoundedRectCoverage(int x, int y, int side, int radius)
        {
            var px = x + 0.5f;
            var py = y + 0.5f;
            var nearestX = Mathf.Clamp(px, radius, side - radius);
            var nearestY = Mathf.Clamp(py, radius, side - radius);
            var dx = px - nearestX;
            var dy = py - nearestY;
            var distance = Mathf.Sqrt(dx * dx + dy * dy);
            return Mathf.Clamp01(radius - distance + 0.5f);
        }
    }
}
