using System.Globalization;

namespace Doggiehood.Core.Economy
{
    /// <summary>Which way a coin change went, so the HUD can paint the floating
    /// delta label the right color — Gain is Leaf green, Spend is Coral red
    /// (shared-components.md palette). #542.</summary>
    public enum CoinDeltaRole
    {
        Gain,
        Spend,
    }

    /// <summary>
    /// #542: the pure, Unity-independent animation state for one HUD currency
    /// chip balance change. It owns two independent tweens, both driven purely
    /// by elapsed seconds so the thin <c>HudOverlay</c> wiring only reads the
    /// values each frame and paints them (no decision logic in the MonoBehaviour):
    ///
    /// <list type="bullet">
    /// <item>the <b>count-up</b> — the displayed balance eases from its prior
    /// value to the new one over <see cref="CountUpDurationSec"/> instead of
    /// snapping; and</item>
    /// <item>the <b>floating delta label</b> — a signed "+N"/"−N" that rises
    /// <see cref="DeltaRiseDistancePx"/> while fading to transparent over
    /// <see cref="DeltaRiseDurationSec"/>.</item>
    /// </list>
    ///
    /// The label rise (0.9s) outlasts the count-up (0.5s), so the whole
    /// animation is <see cref="IsFinished"/> — discardable — once the rise has
    /// played out. Every timing/curve value is a named constant (#161); the
    /// pixel gap and font size of the label are pure render layout and live in
    /// the drawing layer instead.
    /// </summary>
    public sealed class CoinChipAnimation
    {
        /// <summary>Balance-number count-up tween duration.</summary>
        public const float CountUpDurationSec = 0.5f;

        /// <summary>Floating delta label rise + fade duration.</summary>
        public const float DeltaRiseDurationSec = 0.9f;

        /// <summary>Total distance (px) the delta label rises before it is
        /// discarded.</summary>
        public const float DeltaRiseDistancePx = 48f;

        private readonly int fromBalance;
        private readonly int toBalance;
        private readonly int delta;

        public CoinChipAnimation(int fromBalance, int toBalance, int delta)
        {
            this.fromBalance = fromBalance;
            this.toBalance = toBalance;
            this.delta = delta;
        }

        /// <summary>Gain when the balance went up, Spend when it went down.</summary>
        public CoinDeltaRole Role => delta >= 0 ? CoinDeltaRole.Gain : CoinDeltaRole.Spend;

        /// <summary>The signed label text: "+100" for a gain, "−50" for a spend
        /// (U+2212 MINUS SIGN, matching the spec's "−50").</summary>
        public string DeltaText
        {
            get
            {
                var magnitude = (delta < 0 ? -delta : delta).ToString(CultureInfo.InvariantCulture);
                return (delta < 0 ? "−" : "+") + magnitude;
            }
        }

        /// <summary>How far (px) the delta label has risen at
        /// <paramref name="elapsedSec"/>: 0 at the start, clamped to
        /// <see cref="DeltaRiseDistancePx"/> once the rise has finished. Linear.</summary>
        public float RiseOffsetPx(float elapsedSec)
        {
            return DeltaRiseDistancePx * RiseProgress(elapsedSec);
        }

        /// <summary>The delta label's opacity at <paramref name="elapsedSec"/>:
        /// 1 at the start, fading linearly to 0 at
        /// <see cref="DeltaRiseDurationSec"/> (and clamped to 0 past it).</summary>
        public float Alpha(float elapsedSec)
        {
            return 1f - RiseProgress(elapsedSec);
        }

        /// <summary>True once the label's rise+fade has fully played out — the
        /// longer of the two tweens — so the animation can be discarded.</summary>
        public bool IsFinished(float elapsedSec)
        {
            return elapsedSec >= DeltaRiseDurationSec;
        }

        /// <summary>The balance to display at <paramref name="elapsedSec"/>: eases
        /// from the old value to the new one over
        /// <see cref="CountUpDurationSec"/>, clamped to the new value past it.</summary>
        public int DisplayedBalance(float elapsedSec)
        {
            var progress = Progress(elapsedSec, CountUpDurationSec);
            var value = fromBalance + (toBalance - fromBalance) * progress;
            return Round(value);
        }

        private static float RiseProgress(float elapsedSec)
        {
            return Progress(elapsedSec, DeltaRiseDurationSec);
        }

        private static float Progress(float elapsedSec, float durationSec)
        {
            if (elapsedSec <= 0f)
            {
                return 0f;
            }

            if (elapsedSec >= durationSec)
            {
                return 1f;
            }

            return elapsedSec / durationSec;
        }

        private static int Round(float value)
        {
            return (int)(value + (value >= 0f ? 0.5f : -0.5f));
        }
    }
}
