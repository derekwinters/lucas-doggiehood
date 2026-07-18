namespace Doggiehood.Core.Cameras
{
    /// <summary>
    /// #169: hit-tests a tap against a speech bubble's on-screen bounds
    /// padded outward by a fixed margin, so touch input reliably registers
    /// taps that land near — not just exactly on — the bubble's rendered
    /// silhouette. A mouse cursor is pixel-precise; a finger touch is not,
    /// so the collider-only approach from #148/#158 (accurately sized to
    /// the rendered mesh, per <c>DogView.BubbleScale</c>) leaves no room for
    /// that imprecision. This is pure screen-space rectangle geometry so it
    /// works from any projected bubble bounds without depending on
    /// Physics.Raycast collider generation, layers, or hit-priority.
    /// </summary>
    public static class BubbleTapZone
    {
        /// <summary>Outward padding, in screen pixels, applied to every edge
        /// of the bubble's projected screen bounds before hit-testing a tap.
        /// Generous enough to absorb typical touch imprecision beyond
        /// mouse-cursor precision (#169).</summary>
        public const float PaddingPixels = 24f;

        /// <summary>True when (tapX, tapY) falls within the padded bounds of
        /// the rectangle [minX, maxX] x [minY, maxY].</summary>
        public static bool Contains(
            float minX, float minY, float maxX, float maxY,
            float tapX, float tapY)
        {
            return tapX >= minX - PaddingPixels
                && tapX <= maxX + PaddingPixels
                && tapY >= minY - PaddingPixels
                && tapY <= maxY + PaddingPixels;
        }
    }
}
