using System;
using System.Globalization;

namespace Doggiehood.Core.Art
{
    /// <summary>
    /// Engine-free RGB color parsed from a #RRGGBB hex string, with HSV
    /// helpers so palette rules (bright &amp; saturated, #64) are testable
    /// without Unity. The Unity layer converts to UnityEngine.Color at the
    /// boundary.
    /// </summary>
    public readonly struct ColorRgb
    {
        public float R { get; }
        public float G { get; }
        public float B { get; }

        private ColorRgb(float r, float g, float b)
        {
            R = r;
            G = g;
            B = b;
        }

        public static ColorRgb Parse(string hex)
        {
            if (string.IsNullOrEmpty(hex) || hex.Length != 7 || hex[0] != '#')
            {
                throw new ArgumentException($"Expected a #RRGGBB hex color, got '{hex}'.", nameof(hex));
            }

            if (!int.TryParse(hex.Substring(1), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var rgb))
            {
                throw new ArgumentException($"Expected a #RRGGBB hex color, got '{hex}'.", nameof(hex));
            }

            return new ColorRgb(
                ((rgb >> 16) & 0xFF) / 255f,
                ((rgb >> 8) & 0xFF) / 255f,
                (rgb & 0xFF) / 255f);
        }

        /// <summary>
        /// Builds a color from HSV (#299 house palette generator): hue in
        /// degrees (wrapped into 0..360), saturation and value in 0..1. The
        /// engine-free counterpart of UnityEngine.Color.HSVToRGB, so palette
        /// generation stays testable without Unity.
        /// </summary>
        public static ColorRgb FromHsv(float hueDegrees, float saturation, float value)
        {
            var hue = hueDegrees % 360f;
            if (hue < 0f)
            {
                hue += 360f;
            }

            var c = value * saturation;
            var sector = hue / 60f;
            var x = c * (1f - Math.Abs((sector % 2f) - 1f));
            var m = value - c;

            float r, g, b;
            if (sector < 1f) { r = c; g = x; b = 0f; }
            else if (sector < 2f) { r = x; g = c; b = 0f; }
            else if (sector < 3f) { r = 0f; g = c; b = x; }
            else if (sector < 4f) { r = 0f; g = x; b = c; }
            else if (sector < 5f) { r = x; g = 0f; b = c; }
            else { r = c; g = 0f; b = x; }

            return new ColorRgb(r + m, g + m, b + m);
        }

        /// <summary>#RRGGBB hex string for this color, the inverse of
        /// <see cref="Parse"/> (each channel rounded to the nearest of 256
        /// levels).</summary>
        public string ToHex()
        {
            var r = (int)Math.Round(R * 255f);
            var g = (int)Math.Round(G * 255f);
            var b = (int)Math.Round(B * 255f);
            return "#" + r.ToString("X2", CultureInfo.InvariantCulture)
                + g.ToString("X2", CultureInfo.InvariantCulture)
                + b.ToString("X2", CultureInfo.InvariantCulture);
        }

        public float Value
        {
            get { return Math.Max(R, Math.Max(G, B)); }
        }

        /// <summary>Hue in degrees (0..360), the H of HSV — 0 for a fully
        /// desaturated (grey) color.</summary>
        public float Hue
        {
            get
            {
                var max = Value;
                var min = Math.Min(R, Math.Min(G, B));
                var delta = max - min;
                if (delta <= 0f)
                {
                    return 0f;
                }

                float hue;
                if (max == R)
                {
                    hue = 60f * (((G - B) / delta) % 6f);
                }
                else if (max == G)
                {
                    hue = 60f * (((B - R) / delta) + 2f);
                }
                else
                {
                    hue = 60f * (((R - G) / delta) + 4f);
                }

                if (hue < 0f)
                {
                    hue += 360f;
                }

                return hue;
            }
        }

        public float Saturation
        {
            get
            {
                var max = Value;
                if (max <= 0f)
                {
                    return 0f;
                }

                var min = Math.Min(R, Math.Min(G, B));
                return (max - min) / max;
            }
        }
    }
}
