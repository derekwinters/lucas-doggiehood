namespace Doggiehood.Core.World
{
    /// <summary>
    /// The single fixed "pleasant mid-day" lighting setup (#39). There is
    /// deliberately exactly one preset — no day/night cycle or weather for
    /// MVP (a future system is tracked as #87). Colors are hex strings so
    /// Core stays engine-free; the Unity layer parses them at the boundary.
    /// </summary>
    public static class LightingPreset
    {
        public const float SunPitchDegrees = 50f;
        public const float SunYawDegrees = 30f;
        public const float SunIntensity = 1.1f;
        public const string SunColorHex = "#FFF4E5";
        public const string AmbientColorHex = "#C7DDF5";

        /// <summary>
        /// Height (in metres, world Y) at which the Sun's transform is
        /// placed (#560). A directional light's illumination is
        /// position-independent, but leaving the transform at the parent's
        /// default origin sits it at ground level in the map centre, where a
        /// halo/flare/editor gizmo can render a visible bright hotspot on the
        /// grass. Lifting it well above the ground (and the houses) removes
        /// any possibility of a light artifact anchoring visibly at origin.
        /// </summary>
        public const float SunHeight = 100f;
    }
}
