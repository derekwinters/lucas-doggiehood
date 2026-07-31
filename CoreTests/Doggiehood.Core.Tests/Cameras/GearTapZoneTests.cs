using Doggiehood.Core.Cameras;
using NUnit.Framework;

namespace Doggiehood.Core.Tests.Cameras
{
    /// <summary>
    /// #422: the still-IMGUI HUD Settings gear (HudOverlay.ComputeGearRect) is
    /// outside the EventSystem/GraphicRaycaster, so IsPointerOverGameObject
    /// can't tell a world-tap router that a tap landed on it. This pure
    /// screen-space rectangle check lets TapRouter absorb taps over the gear
    /// (interim scaffolding until #370 moves the gear to UGUI), mirroring
    /// BubbleTapZone/LostItemTapZone. Stand-in bounds here; the real gear rect
    /// wiring is asserted by the Unity-layer EditMode test.
    /// </summary>
    public class GearTapZoneTests
    {
        // A stand-in projected gear rectangle in the top-right of a
        // 1920x1200-reference screen (bottom-left origin, screen space).
        private const float MinX = 1800f;
        private const float MinY = 1080f;
        private const float MaxX = 1888f;
        private const float MaxY = 1168f;

        [Test]
        public void TapInsideTheGearBounds_IsAHit()
        {
            Assert.That(GearTapZone.Contains(MinX, MinY, MaxX, MaxY, 1844f, 1124f), Is.True);
        }

        [Test]
        public void TapOnTheGearEdges_IsAHit()
        {
            Assert.That(GearTapZone.Contains(MinX, MinY, MaxX, MaxY, MinX, MinY), Is.True);
            Assert.That(GearTapZone.Contains(MinX, MinY, MaxX, MaxY, MaxX, MaxY), Is.True);
        }

        [Test]
        public void TapOutsideTheGearBounds_IsAMiss()
        {
            Assert.That(GearTapZone.Contains(MinX, MinY, MaxX, MaxY, MinX - 1f, 1124f), Is.False);
            Assert.That(GearTapZone.Contains(MinX, MinY, MaxX, MaxY, MaxX + 1f, 1124f), Is.False);
            Assert.That(GearTapZone.Contains(MinX, MinY, MaxX, MaxY, 1844f, MinY - 1f), Is.False);
            Assert.That(GearTapZone.Contains(MinX, MinY, MaxX, MaxY, 1844f, MaxY + 1f), Is.False);
        }
    }
}
