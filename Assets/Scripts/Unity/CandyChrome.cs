using UnityEngine;

namespace Doggiehood.Unity
{
    /// <summary>
    /// Shared "Candy Cottage" procedural chrome (#65), drawn on IMGUI with no
    /// external raster art. Established by the HUD currency chip (#296) and
    /// extracted here (#297) so every IMGUI overlay draws identical chrome
    /// rather than duplicating the routine.
    ///
    /// One runtime-generated white anti-aliased circle texture is tinted per
    /// layer and either used as a full disc (<see cref="DrawCircle"/>) or
    /// cap-and-stretched into a stadium / pill (<see cref="DrawStadium"/>). The
    /// fixed palette is the shared baseline from
    /// docs/specs/ui/shared-components.md.
    /// </summary>
    public static class CandyChrome
    {
        // --- Fixed Candy Cottage palette (shared-components.md) ---
        public static readonly Color InkColor = new Color32(0x2E, 0x2A, 0x26, 0xFF);
        public static readonly Color CreamColor = new Color32(0xFF, 0xF3, 0xD9, 0xFF);
        public static readonly Color GoldColor = new Color32(0xFF, 0xC2, 0x3C, 0xFF);
        public static readonly Color LeafColor = new Color32(0x58, 0xC0, 0x6A, 0xFF);

        // Procedural chrome: one white AA circle, tinted per layer. Used as a
        // full disc and, cap-and-stretched, as a stadium (pill).
        private const int CircleTextureSize = 128;
        private static Texture2D circleTexture;

        /// <summary>Draws a stadium (pill): a white AA circle capped on each
        /// end and stretched across the middle, tinted <paramref name="color"/>.
        /// A zero/negative-size rect draws nothing.</summary>
        public static void DrawStadium(Rect rect, Color color)
        {
            if (rect.width <= 0f || rect.height <= 0f)
            {
                return;
            }

            var previous = GUI.color;
            GUI.color = color;

            var circle = CircleTexture();
            var diameter = rect.height;
            var halfH = rect.height / 2f;
            GUI.DrawTexture(new Rect(rect.x, rect.y, diameter, diameter), circle);
            GUI.DrawTexture(new Rect(rect.xMax - diameter, rect.y, diameter, diameter), circle);

            var midWidth = rect.width - diameter;
            if (midWidth > 0f)
            {
                GUI.DrawTexture(new Rect(rect.x + halfH, rect.y, midWidth, rect.height), Texture2D.whiteTexture);
            }

            GUI.color = previous;
        }

        /// <summary>Draws a full disc filling <paramref name="rect"/>, tinted
        /// <paramref name="color"/>.</summary>
        public static void DrawCircle(Rect rect, Color color)
        {
            if (rect.width <= 0f || rect.height <= 0f)
            {
                return;
            }

            var previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, CircleTexture());
            GUI.color = previous;
        }

        /// <summary>Builds (once) the white anti-aliased circle used to compose
        /// all chrome — a full disc for tokens/dots, cap-and-stretched into a
        /// stadium for pills. Procedural; no external art asset.</summary>
        public static Texture2D CircleTexture()
        {
            if (circleTexture != null)
            {
                return circleTexture;
            }

            var size = CircleTextureSize;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
            };

            var pixels = new Color32[size * size];
            var center = (size - 1) * 0.5f;
            var radius = size * 0.5f - 0.5f;
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var dx = x - center;
                    var dy = y - center;
                    var distance = Mathf.Sqrt(dx * dx + dy * dy);
                    var coverage = Mathf.Clamp01(radius - distance + 0.5f);
                    var alpha = (byte)Mathf.RoundToInt(coverage * 255f);
                    pixels[y * size + x] = new Color32(255, 255, 255, alpha);
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply();
            circleTexture = texture;
            return circleTexture;
        }
    }
}
