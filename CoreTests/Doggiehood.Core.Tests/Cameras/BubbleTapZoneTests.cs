using Doggiehood.Core.Cameras;
using NUnit.Framework;

namespace Doggiehood.Core.Tests.Cameras
{
    public class BubbleTapZoneTests
    {
        // A stand-in projected bubble rectangle: 100..140 horizontally,
        // 200..240 vertically (a 40x40px screen footprint, matching the
        // readable-tap-target floor asserted by DogLayerTests).
        private const float MinX = 100f;
        private const float MinY = 200f;
        private const float MaxX = 140f;
        private const float MaxY = 240f;

        [Test]
        public void TapInsideTheRawBounds_IsAHit()
        {
            Assert.That(BubbleTapZone.Contains(MinX, MinY, MaxX, MaxY, 120f, 220f), Is.True);
        }

        [Test]
        public void TapJustOutsideTheRawBounds_StillHits_WithinThePaddingMargin()
        {
            // #169: a mouse cursor is pixel-precise, but a finger touch is
            // not — a tap that visually reads as "on the bubble" commonly
            // lands a little outside its exact rendered silhouette. This is
            // the padding margin that makes those still register.
            var justOutsideX = MaxX + BubbleTapZone.PaddingPixels - 1f;
            Assert.That(BubbleTapZone.Contains(MinX, MinY, MaxX, MaxY, justOutsideX, 220f), Is.True);

            var justOutsideY = MinY - BubbleTapZone.PaddingPixels + 1f;
            Assert.That(BubbleTapZone.Contains(MinX, MinY, MaxX, MaxY, 120f, justOutsideY), Is.True);
        }

        [Test]
        public void TapBeyondThePaddingMargin_IsAMiss()
        {
            var wellOutsideX = MaxX + BubbleTapZone.PaddingPixels + 1f;
            Assert.That(BubbleTapZone.Contains(MinX, MinY, MaxX, MaxY, wellOutsideX, 220f), Is.False);

            var wellOutsideY = MinY - BubbleTapZone.PaddingPixels - 1f;
            Assert.That(BubbleTapZone.Contains(MinX, MinY, MaxX, MaxY, 120f, wellOutsideY), Is.False);
        }
    }
}
