using Doggiehood.Core.Economy;
using NUnit.Framework;

namespace Doggiehood.Core.Tests.Economy
{
    /// <summary>
    /// #542: the pure count-up + floating-delta animation state for the HUD
    /// currency chip. All the timing/curve math lives here (Unity-independent),
    /// so the thin HudOverlay wiring only reads these values each frame and
    /// paints them.
    /// </summary>
    public class CoinChipAnimationTests
    {
        private const float Tolerance = 0.0001f;

        [Test]
        public void Gain_ExposesSignedTextAndRole()
        {
            // A deposit of 100 from a displayed balance of 28: the label reads
            // "+100" and the role is Gain (painted Leaf green by the HUD).
            var anim = new CoinChipAnimation(fromBalance: 28, toBalance: 128, delta: 100);

            Assert.That(anim.Role, Is.EqualTo(CoinDeltaRole.Gain));
            Assert.That(anim.DeltaText, Is.EqualTo("+100"));
        }

        [Test]
        public void Spend_ExposesSignedTextAndRole()
        {
            // A spend of 50: the label reads "−50" (U+2212 minus, matching the
            // spec) and the role is Spend (painted Coral red).
            var anim = new CoinChipAnimation(fromBalance: 128, toBalance: 78, delta: -50);

            Assert.That(anim.Role, Is.EqualTo(CoinDeltaRole.Spend));
            Assert.That(anim.DeltaText, Is.EqualTo("−50"));
        }

        [Test]
        public void RiseOffset_StartsAtZero_AndReachesTheRiseDistanceAtTheEnd()
        {
            var anim = new CoinChipAnimation(0, 100, 100);

            Assert.That(anim.RiseOffsetPx(0f), Is.EqualTo(0f).Within(Tolerance));
            Assert.That(anim.RiseOffsetPx(CoinChipAnimation.DeltaRiseDurationSec),
                Is.EqualTo(CoinChipAnimation.DeltaRiseDistancePx).Within(Tolerance));
            // Half-way through, half the rise (linear).
            Assert.That(anim.RiseOffsetPx(CoinChipAnimation.DeltaRiseDurationSec / 2f),
                Is.EqualTo(CoinChipAnimation.DeltaRiseDistancePx / 2f).Within(Tolerance));
        }

        [Test]
        public void RiseOffset_ClampsToTheRiseDistancePastTheEnd()
        {
            var anim = new CoinChipAnimation(0, 100, 100);

            Assert.That(anim.RiseOffsetPx(CoinChipAnimation.DeltaRiseDurationSec * 2f),
                Is.EqualTo(CoinChipAnimation.DeltaRiseDistancePx).Within(Tolerance));
        }

        [Test]
        public void Alpha_FadesLinearlyFromOneToZeroAcrossTheRiseDuration()
        {
            var anim = new CoinChipAnimation(0, 100, 100);

            Assert.That(anim.Alpha(0f), Is.EqualTo(1f).Within(Tolerance));
            Assert.That(anim.Alpha(CoinChipAnimation.DeltaRiseDurationSec / 2f),
                Is.EqualTo(0.5f).Within(Tolerance));
            Assert.That(anim.Alpha(CoinChipAnimation.DeltaRiseDurationSec),
                Is.EqualTo(0f).Within(Tolerance));
        }

        [Test]
        public void Alpha_ClampsToZeroPastTheEnd()
        {
            var anim = new CoinChipAnimation(0, 100, 100);

            Assert.That(anim.Alpha(CoinChipAnimation.DeltaRiseDurationSec * 2f),
                Is.EqualTo(0f).Within(Tolerance));
        }

        [Test]
        public void IsFinished_OnlyOnceTheRiseHasFullyPlayedOut()
        {
            // The label is discardable once its rise+fade is done — the longer of
            // the two tweens (rise 0.9s outlasts the count-up 0.5s), so the whole
            // animation is finished at DeltaRiseDurationSec.
            var anim = new CoinChipAnimation(0, 100, 100);

            Assert.That(anim.IsFinished(0f), Is.False);
            Assert.That(anim.IsFinished(CoinChipAnimation.DeltaRiseDurationSec - 0.01f), Is.False);
            Assert.That(anim.IsFinished(CoinChipAnimation.DeltaRiseDurationSec), Is.True);
            Assert.That(anim.IsFinished(CoinChipAnimation.DeltaRiseDurationSec + 0.5f), Is.True);
        }

        [Test]
        public void DisplayedBalance_CountsUpFromOldToNewOverTheCountUpDuration()
        {
            // Count-up from 0 to 100: starts at the old value, reaches the new
            // value exactly at CountUpDurationSec, linear in between.
            var anim = new CoinChipAnimation(fromBalance: 0, toBalance: 100, delta: 100);

            Assert.That(anim.DisplayedBalance(0f), Is.EqualTo(0));
            Assert.That(anim.DisplayedBalance(CoinChipAnimation.CountUpDurationSec / 2f), Is.EqualTo(50));
            Assert.That(anim.DisplayedBalance(CoinChipAnimation.CountUpDurationSec), Is.EqualTo(100));
        }

        [Test]
        public void DisplayedBalance_ClampsToTheNewValuePastTheCountUpDuration()
        {
            var anim = new CoinChipAnimation(fromBalance: 0, toBalance: 100, delta: 100);

            Assert.That(anim.DisplayedBalance(CoinChipAnimation.CountUpDurationSec * 3f), Is.EqualTo(100));
        }

        [Test]
        public void DisplayedBalance_CountsDownOnASpend()
        {
            var anim = new CoinChipAnimation(fromBalance: 100, toBalance: 60, delta: -40);

            Assert.That(anim.DisplayedBalance(0f), Is.EqualTo(100));
            Assert.That(anim.DisplayedBalance(CoinChipAnimation.CountUpDurationSec / 2f), Is.EqualTo(80));
            Assert.That(anim.DisplayedBalance(CoinChipAnimation.CountUpDurationSec), Is.EqualTo(60));
        }

        [Test]
        public void Constants_MatchTheApprovedWireframeValues()
        {
            // #542 approved proposal (shared-components.md CurrencyChip table).
            Assert.That(CoinChipAnimation.CountUpDurationSec, Is.EqualTo(0.5f));
            Assert.That(CoinChipAnimation.DeltaRiseDurationSec, Is.EqualTo(0.9f));
            Assert.That(CoinChipAnimation.DeltaRiseDistancePx, Is.EqualTo(48f));
        }
    }
}
