namespace Doggiehood.Core.Cameras
{
    /// <summary>
    /// #311: hit-tests a tap against the active lost item's on-screen bounds
    /// padded outward by a fixed margin, mirroring <see cref="BubbleTapZone"/>
    /// (#169). The lost item's SphereCollider (radius 0.3) projects to a
    /// very small on-screen target under the fixed 45-degree rig — around
    /// 18px on a 1080p-reference view — and the full-map ground Plane
    /// collider underlies the whole spawn area, so a Physics.Raycast-only
    /// check has effectively zero forgiveness: a tap that visually reads as
    /// "on the ball" but lands a few pixels off misses the tiny collider
    /// entirely and instead lands on the ground, silently doing nothing.
    /// This is pure screen-space rectangle geometry, independent of
    /// Physics.Raycast/collider layers.
    /// </summary>
    public static class LostItemTapZone
    {
        /// <summary>Outward padding, in screen pixels, applied to every edge
        /// of the lost item's projected screen bounds before hit-testing a
        /// tap. Larger than <see cref="BubbleTapZone.PaddingPixels"/> (#169)
        /// because the ball's on-screen footprint is much smaller and
        /// near-fully surrounded by the always-present ground collider, so
        /// touch imprecision needs more headroom to still register (#311).
        /// Tunable placeholder — no spec pins a number.</summary>
        public const float PaddingPixels = 32f;

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
