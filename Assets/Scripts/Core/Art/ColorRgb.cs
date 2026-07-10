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

        public float Value
        {
            get { return Math.Max(R, Math.Max(G, B)); }
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
