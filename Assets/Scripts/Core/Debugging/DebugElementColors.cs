using Doggiehood.Core.Art;

namespace Doggiehood.Core.Debugging
{
    /// <summary>
    /// #611: the diagnostic debug-element palette. Zooming out shows a green
    /// "border" at the bottom of the screen, and prior fixes (#536/#558/#570)
    /// chased it as a coverage problem without ending it. Before committing to a
    /// visual fix, this exposes a way to see WHICH element the border actually is:
    /// when the Debug-tab "Show debug element colors" toggle is on, the ground
    /// plane and the camera void backstop are painted in two loudly-different,
    /// obviously-fake colours (NOT shades of green) instead of the current matched
    /// <see cref="Palette.GrassHex"/>, so whichever one is visible at any zoom/pan
    /// is unambiguous. Off (default) restores today's exact visuals — both fall
    /// back to <see cref="Palette.GrassHex"/>, byte-identical.
    ///
    /// This is the colour decision only — pure, engine-free Core data. The Unity
    /// layer holds the runtime on/off flag (WorldBuilder.ShowDebugElementColors),
    /// paints the ground (WorldBuilder) and clears the camera (CameraRig) from
    /// these hexes via CoreColors.FromHex.
    /// </summary>
    public static class DebugElementColors
    {
        /// <summary>Debug colour for the lit ground plane — bright magenta,
        /// deliberately nothing like grass green so it can't be mistaken for the
        /// backstop or the real grass.</summary>
        public const string GroundDebugHex = "#FF00FF";

        /// <summary>Debug colour for the camera void backstop (clear colour) —
        /// bright cyan, loudly distinct from both the magenta ground and
        /// grass green.</summary>
        public const string BackstopDebugHex = "#00E5FF";

        /// <summary>The hex the ground plane should be painted: the debug magenta
        /// when the diagnostic toggle is on, otherwise the matched grass green.</summary>
        public static string GroundHex(bool showDebugColors)
            => showDebugColors ? GroundDebugHex : Palette.GrassHex;

        /// <summary>The hex the camera should clear to (the void backstop): the
        /// debug cyan when the diagnostic toggle is on, otherwise the matched
        /// grass green.</summary>
        public static string BackstopHex(bool showDebugColors)
            => showDebugColors ? BackstopDebugHex : Palette.GrassHex;
    }
}
