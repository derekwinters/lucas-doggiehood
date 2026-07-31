namespace Doggiehood.Core.Cameras
{
    /// <summary>
    /// #422: hit-tests a tap against the HUD Settings gear's on-screen bounds,
    /// so the world-tap router can absorb taps that land on the gear instead of
    /// raycasting the world behind it. The gear is still drawn in IMGUI
    /// (<c>HudOverlay</c>, migration tracked by #370), so it lives outside the
    /// EventSystem/GraphicRaycaster and <c>IsPointerOverGameObject()</c> — which
    /// covers the UGUI overlays — cannot see it. This is pure screen-space
    /// rectangle geometry, independent of Physics.Raycast/collider layers,
    /// mirroring <see cref="BubbleTapZone"/> (#169) and
    /// <see cref="LostItemTapZone"/> (#311). Interim scaffolding: it goes away
    /// once #370 moves the gear onto the UGUI canvas, where the pointer-over-UI
    /// guard already covers it.
    /// </summary>
    public static class GearTapZone
    {
        /// <summary>True when (tapX, tapY) falls within (inclusive of the
        /// edges) the rectangle [minX, maxX] x [minY, maxY]. Unlike the
        /// bubble/lost-item zones there is no outward padding: the gear is a
        /// large, fixed corner affordance, so a precise rect suffices — a
        /// padded margin would only steal taps from the world just inboard of
        /// it.</summary>
        public static bool Contains(
            float minX, float minY, float maxX, float maxY,
            float tapX, float tapY)
        {
            return tapX >= minX
                && tapX <= maxX
                && tapY >= minY
                && tapY <= maxY;
        }
    }
}
