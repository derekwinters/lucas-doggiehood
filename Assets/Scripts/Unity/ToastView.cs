using Doggiehood.Core.Ui;
using UnityEngine;

namespace Doggiehood.Unity
{
    /// <summary>
    /// #541: the non-modal completion toast (docs/specs/ui/toast.md /
    /// mockups/toast.html, approved wireframe #562). A small cream pill on the
    /// shared Candy Cottage baseline (#173) — a leading gold coin token + one
    /// message line — that slides into the reserved top-left HUD lane, holds for
    /// <see cref="ToastAutoDismissSeconds"/>, then slides out (or exits early the
    /// instant it is tapped). It renders the single current slot of the shared
    /// <see cref="ToastQueue{T}"/>; when it clears, the queue promotes the next
    /// waiting toast, which slides in in turn (one at a time — never stacked).
    ///
    /// <para><b>Not modal.</b> Unlike the centered panels, the toast never
    /// registers with <see cref="Doggiehood.Core.Cameras.ModalInputGate"/> and
    /// carries no scrim — it is a HUD element like the currency chip, just
    /// tappable. World taps outside the lane are unaffected. Kept on IMGUI
    /// alongside the HUD chip (<see cref="HudOverlay"/>), drawn procedurally from
    /// the shared <see cref="CandyChrome"/> primitives — no external raster art.
    /// Every geometry/tuning value is a named constant (#161).</para>
    /// </summary>
    public sealed class ToastView : MonoBehaviour
    {
        // --- Toast layout constants (docs/specs/ui/toast.md @ 1920×1200) ---
        public const float ToastLaneTopMarginPx = 32f;   // shares the chip/gear row
        public const float ToastLaneLeftMarginPx = 36f;  // safe-area left edge
        public const float ToastHeightPx = 88f;          // matches CurrencyChip.HeightPx (#173)
        public const float ToastMaxWidthPx = 640f;       // reserved lane width cap
        public const float ToastCoinDiameterPx = 60f;    // matches CurrencyChip.CoinDiameterPx (#173)
        public const float ToastPaddingLeftPx = 14f;     // coin inset (matches CurrencyChip)
        public const float ToastPaddingRightPx = 28f;    // message inset
        public const float ToastIconGapPx = 16f;         // coin -> message
        public const int ToastFontSizePx = 34;           // message (matches MessageFontSizePx #374)
        public const float ToastAutoDismissSeconds = 3.5f;
        public const float ToastSlideInMs = 220f;        // slide + fade in from the left edge
        public const float ToastSlideOutMs = 180f;       // slide + fade out (timeout or tap alike)

        // --- Shared Candy Cottage baseline chrome (#173, shared-components.md) ---
        public const float OutlineThicknessPx = 6f;      // Ink outline on the pill
        public const float ShadowOffsetPx = 8f;          // hard drop-shadow, straight down
        public const float CoinOutlineThicknessPx = 4f;  // coin token's ink ring

        // The payout tail ("+N coins") uses the shared Leaf role tint (mockup
        // .toast .amt); the rest of the line is Ink. Named, no inline literal.
        public static readonly Color InkColor = HudOverlay.InkColor;
        public static readonly Color CreamColor = HudOverlay.CreamColor;
        public static readonly Color GoldColor = HudOverlay.GoldColor;

        /// <summary>#291: the bundled UI font (never an editor-only builtin, which
        /// renders invisible in the player). Same asset the HUD chip uses.</summary>
        public const string LabelFontResource = "DejaVuSans";

        private const float MsPerSecond = 1000f;

        private static GUIStyle labelStyle;

        private ToastQueue<ToastRequest> queue;

        // The toast's animation state machine. Idle when the lane is empty;
        // otherwise it slides in, holds, then slides out — and on slide-out
        // completion it dismisses the queue's current slot (promoting the next).
        private enum Phase
        {
            Idle,
            SlideIn,
            Hold,
            SlideOut,
        }

        private Phase phase = Phase.Idle;
        private float phaseElapsedSec;

        /// <summary>Whether a toast currently occupies the lane (any non-idle
        /// phase). Exposed for wiring/tests.</summary>
        public bool IsShowing => phase != Phase.Idle;

        /// <summary>The message on the toast currently in the lane, or empty when
        /// none is showing. Reads live from the shared queue's current slot.</summary>
        public string CurrentMessage =>
            queue != null && queue.HasCurrent ? queue.Current.Message : string.Empty;

        /// <summary>Wires the view to the shared toast queue. If a toast was
        /// already enqueued before wiring, it begins showing immediately.</summary>
        public void Init(ToastQueue<ToastRequest> queue)
        {
            this.queue = queue;
            queue.CurrentChanged += OnQueueCurrentChanged;
            if (queue.HasCurrent && phase == Phase.Idle)
            {
                BeginShow();
            }
        }

        private void OnDestroy()
        {
            if (queue != null)
            {
                queue.CurrentChanged -= OnQueueCurrentChanged;
            }
        }

        /// <summary>The queue promoted a toast into an empty slot while the lane
        /// was idle — start showing it. (A dismiss-driven promotion is handled
        /// inline by <see cref="Finish"/>, which is already mid-transition, so we
        /// only react here when idle.)</summary>
        private void OnQueueCurrentChanged()
        {
            if (phase == Phase.Idle && queue.HasCurrent)
            {
                BeginShow();
            }
        }

        private void BeginShow()
        {
            phase = Phase.SlideIn;
            phaseElapsedSec = 0f;
        }

        private void Update()
        {
            Tick(Time.unscaledDeltaTime);
        }

        /// <summary>Advances the toast animation by <paramref name="deltaSeconds"/>.
        /// Auto-dismiss fires when the hold has run <see cref="ToastAutoDismissSeconds"/>;
        /// the slide-out then plays and, on completion, clears the current slot so
        /// the queue promotes the next waiting toast. Split out from
        /// <see cref="Update"/> so it is drivable in EditMode tests without a frame
        /// loop.</summary>
        public void Tick(float deltaSeconds)
        {
            if (phase == Phase.Idle)
            {
                return;
            }

            phaseElapsedSec += deltaSeconds;
            switch (phase)
            {
                case Phase.SlideIn:
                    if (phaseElapsedSec >= SlideInSeconds)
                    {
                        phase = Phase.Hold;
                        phaseElapsedSec = 0f;
                    }

                    break;
                case Phase.Hold:
                    if (phaseElapsedSec >= ToastAutoDismissSeconds)
                    {
                        EnterSlideOut();
                    }

                    break;
                case Phase.SlideOut:
                    if (phaseElapsedSec >= SlideOutSeconds)
                    {
                        Finish();
                    }

                    break;
            }
        }

        /// <summary>A tap anywhere on the toast dismisses it early — it begins the
        /// same slide-out exit immediately, skipping the remaining hold. A no-op
        /// while already sliding out or idle.</summary>
        public void Tap()
        {
            if (phase == Phase.SlideIn || phase == Phase.Hold)
            {
                EnterSlideOut();
            }
        }

        private void EnterSlideOut()
        {
            phase = Phase.SlideOut;
            phaseElapsedSec = 0f;
        }

        /// <summary>Slide-out finished: clear the current slot. If another toast
        /// is waiting the queue promotes it and it slides in next; otherwise the
        /// lane goes idle.</summary>
        private void Finish()
        {
            queue.DismissCurrent();
            if (queue.HasCurrent)
            {
                BeginShow();
            }
            else
            {
                phase = Phase.Idle;
                phaseElapsedSec = 0f;
            }
        }

        private static float SlideInSeconds => ToastSlideInMs / MsPerSecond;

        private static float SlideOutSeconds => ToastSlideOutMs / MsPerSecond;

        /// <summary>The lane's fully-shown rect: the reserved top-left corner rect
        /// of the given width (capped at <see cref="ToastMaxWidthPx"/>) at the lane
        /// margins. IMGUI top-left origin.</summary>
        public static Rect ComputeToastRect(float toastWidth)
        {
            var width = Mathf.Min(toastWidth, ToastMaxWidthPx);
            return new Rect(ToastLaneLeftMarginPx, ToastLaneTopMarginPx, width, ToastHeightPx);
        }

        /// <summary>The toast's content width for a message: outline both sides +
        /// coin inset + coin + gap + measured text + message inset, capped at the
        /// reserved lane width. Never a magic number (#161).</summary>
        public static float ComputeToastWidth(float textWidth)
        {
            var content = 2f * OutlineThicknessPx + ToastPaddingLeftPx + ToastCoinDiameterPx
                + ToastIconGapPx + textWidth + ToastPaddingRightPx;
            return Mathf.Min(content, ToastMaxWidthPx);
        }

        /// <summary>Horizontal slide offset (px) for the current phase: the toast
        /// starts one full lane-width to the left and slides to rest during
        /// slide-in, and reverses during slide-out. Zero while holding.</summary>
        public float SlideOffsetPx(float toastWidth)
        {
            var travel = toastWidth + ToastLaneLeftMarginPx;
            switch (phase)
            {
                case Phase.SlideIn:
                    return -travel * (1f - Mathf.Clamp01(phaseElapsedSec / SlideInSeconds));
                case Phase.SlideOut:
                    return -travel * Mathf.Clamp01(phaseElapsedSec / SlideOutSeconds);
                default:
                    return 0f;
            }
        }

        /// <summary>Fade alpha (0..1) for the current phase — fades in on slide-in,
        /// out on slide-out, full while holding.</summary>
        public float Alpha()
        {
            switch (phase)
            {
                case Phase.SlideIn:
                    return Mathf.Clamp01(phaseElapsedSec / SlideInSeconds);
                case Phase.SlideOut:
                    return 1f - Mathf.Clamp01(phaseElapsedSec / SlideOutSeconds);
                default:
                    return 1f;
            }
        }

        private void OnGUI()
        {
            if (phase == Phase.Idle || queue == null || !queue.HasCurrent)
            {
                return;
            }

            var message = queue.Current.Message;
            var style = LabelStyle();
            var textWidth = style.CalcSize(new GUIContent(message)).x;
            var width = ComputeToastWidth(textWidth);
            var rect = ComputeToastRect(width);
            var offset = SlideOffsetPx(width);
            var drawRect = new Rect(rect.x + offset, rect.y, rect.width, rect.height);

            var previous = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, Alpha());
            DrawToast(drawRect, message, style);
            GUI.color = previous;

            // The whole pill is the tap target — a tap anywhere on it dismisses
            // early (no scrim, no separate button). A transparent hit region so no
            // default IMGUI skin/glyph draws over the procedural chrome.
            if (GUI.Button(drawRect, GUIContent.none, GUIStyle.none))
            {
                Tap();
            }
        }

        /// <summary>Draws the Candy Cottage toast, back to front: hard straight-down
        /// shadow, Ink outline stadium, cream fill inset by the outline, the gold
        /// coin token (with its ink ring), then the message line.</summary>
        private void DrawToast(Rect rect, string message, GUIStyle style)
        {
            var shadow = new Rect(rect.x, rect.y + ShadowOffsetPx, rect.width, rect.height);
            CandyChrome.DrawStadium(shadow, InkColor);
            CandyChrome.DrawStadium(rect, InkColor);

            var fill = new Rect(
                rect.x + OutlineThicknessPx,
                rect.y + OutlineThicknessPx,
                rect.width - 2f * OutlineThicknessPx,
                rect.height - 2f * OutlineThicknessPx);
            CandyChrome.DrawStadium(fill, CreamColor);

            var coinX = rect.x + OutlineThicknessPx + ToastPaddingLeftPx;
            var coinY = rect.center.y - ToastCoinDiameterPx / 2f;
            CandyChrome.DrawCircle(new Rect(coinX, coinY, ToastCoinDiameterPx, ToastCoinDiameterPx), InkColor);
            var inner = ToastCoinDiameterPx - 2f * CoinOutlineThicknessPx;
            CandyChrome.DrawCircle(
                new Rect(coinX + CoinOutlineThicknessPx, coinY + CoinOutlineThicknessPx, inner, inner),
                GoldColor);

            var textX = coinX + ToastCoinDiameterPx + ToastIconGapPx;
            var textRight = rect.xMax - OutlineThicknessPx - ToastPaddingRightPx;
            var textRect = new Rect(textX, rect.y, Mathf.Max(0f, textRight - textX), rect.height);
            GUI.Label(textRect, message, style);
        }

        private static GUIStyle LabelStyle()
        {
            if (labelStyle == null)
            {
                labelStyle = new GUIStyle
                {
                    font = Resources.Load<Font>(LabelFontResource),
                    fontSize = ToastFontSizePx,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleLeft,
                };
                labelStyle.normal.textColor = InkColor;
            }

            return labelStyle;
        }
    }
}
