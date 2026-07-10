using Doggiehood.Core.Art;
using UnityEngine;

namespace Doggiehood.Unity
{
    /// <summary>Boundary converter: Core hex colors to UnityEngine.Color.</summary>
    public static class CoreColors
    {
        public static Color FromHex(string hex)
        {
            var parsed = ColorRgb.Parse(hex);
            return new Color(parsed.R, parsed.G, parsed.B, 1f);
        }
    }
}
