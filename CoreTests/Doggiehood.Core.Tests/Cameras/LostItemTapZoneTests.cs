using Doggiehood.Core.Cameras;
using NUnit.Framework;

namespace Doggiehood.Core.Tests.Cameras
{
    public class LostItemTapZoneTests
    {
        // A stand-in projected lost-item rectangle: the ball's tiny
        // SphereCollider (radius 0.3, #311) projects to only a small
        // on-screen footprint under the 45-degree rig, e.g. 18x18px at
        // 1080p — much smaller than the speech-bubble footprint
        // BubbleTapZoneTests uses.
        private const float MinX = 100f;
        private const float MinY = 200f;
        private const float MaxX = 118f;
        private const float MaxY = 218f;

        [Test]
        public void TapInsideTheRawBounds_IsAHit()
        {
            Assert.That(LostItemTapZone.Contains(MinX, MinY, MaxX, MaxY, 110f, 210f), Is.True);
        }

        [Test]
        public void TapJustOutsideTheRawBounds_StillHits_WithinThePaddingMargin()
        {
            // #311: the ground Plane collider sits beneath/around the item
            // everywhere in the spawn area, so Physics.Raycast alone has no
            // forgiveness for a tap that visually reads as "on the ball" but
            // lands a little outside its tiny rendered silhouette. This is
            // the padding margin that makes those still register.
            var justOutsideX = MaxX + LostItemTapZone.PaddingPixels - 1f;
            Assert.That(LostItemTapZone.Contains(MinX, MinY, MaxX, MaxY, justOutsideX, 210f), Is.True);

            var justOutsideY = MinY - LostItemTapZone.PaddingPixels + 1f;
            Assert.That(LostItemTapZone.Contains(MinX, MinY, MaxX, MaxY, 110f, justOutsideY), Is.True);
        }

        [Test]
        public void TapBeyondThePaddingMargin_IsAMiss()
        {
            var wellOutsideX = MaxX + LostItemTapZone.PaddingPixels + 1f;
            Assert.That(LostItemTapZone.Contains(MinX, MinY, MaxX, MaxY, wellOutsideX, 210f), Is.False);

            var wellOutsideY = MinY - LostItemTapZone.PaddingPixels - 1f;
            Assert.That(LostItemTapZone.Contains(MinX, MinY, MaxX, MaxY, 110f, wellOutsideY), Is.False);
        }
    }
}
