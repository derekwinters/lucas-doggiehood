using Doggiehood.Core.Dogs;
using Doggiehood.Core.Onboarding;
using Doggiehood.Core.World;
using UnityEngine;

namespace Doggiehood.Unity
{
    /// <summary>
    /// First-launch guidance (#44), layered over live gameplay as a slim
    /// bottom-center coach bar — never a modal scene. It is the ONE standard
    /// guidance surface for the whole onboarding journey (#374): it watches the
    /// real camera controller for pan/zoom, the presenter for the bubble tap,
    /// and the live quest for the first completion, then — instead of
    /// dismissing — re-shows for the follow-on reward chain
    /// (upgrade -> expand -> build, #316/#371), advancing as each real action
    /// completes on <see cref="GameState.RewardChain"/> and dismissing for good
    /// only when the chain finishes. All decision logic lives in Core
    /// (<see cref="OnboardingSequence"/> / <see cref="OnboardingCoach"/>); layout
    /// follows the approved wireframe (docs/specs/ui/onboarding-overlay.md,
    /// #176/#374).
    /// </summary>
    public sealed class OnboardingOverlay : MonoBehaviour
    {
        // Layout constants from the #176/#374 wireframe, authored at the
        // 1920x1200 reference (docs/specs/ui/onboarding-overlay.md). No inline
        // geometry literals (#161).
        private const float ReferenceHeightPx = 1200f;
        // Locked #374 constants: CoachWidthPx is now a MINIMUM (the bar grows to
        // fit the measured message), CoachMaxWidthPx is the cap beyond which the
        // message wraps and the bar grows in height instead.
        public const float CoachWidthPx = 900f;
        public const float CoachMaxWidthPx = 1500f;
        private const float CoachHeightPx = 88f;
        private const float CoachBottomMarginPx = 56f;
        public const int StepDotCount = 4;
        private const int MsgFontPx = 30;

        // --- Shared Candy Cottage baseline (shared-components.md #65/#173) ---
        // #297 restyle: the graybox GUI.Box becomes the Candy Cottage coach bar
        // (mockups/onboarding-overlay.html), drawn procedurally via CandyChrome.
        public const float OutlineThicknessPx = 6f;   // ink outline on the bar
        public const float ShadowOffsetPx = 8f;       // hard drop-shadow, straight down
        public const float PillRadiusPx = 999f;       // full pill (stadium) ends

        // --- Coach bar layout (mockup .coach) ---
        public const float CoachPadXPx = 34f;   // content x-padding inside the bar
        public const float CoachGapPx = 22f;    // gap between the three inline regions

        // --- Leaf paw badge (mockup .paw) ---
        public const float PawDiameterPx = 52f;         // round leaf badge
        public const float PawOutlineThicknessPx = 5f;  // badge's ink ring
        // Procedural ink paw-print inside the badge (no emoji glyph / raster art).
        private const float PawPadDiameterPx = 15f;       // main foot pad
        private const float PawToeDiameterPx = 9f;        // each toe pad
        private const float PawToeSpreadPx = 12f;         // outer toe x-offset from center
        private const float PawToeRisePx = 11f;           // toe y-offset above center
        private const float PawCenterToeExtraRisePx = 5f; // middle toe sits a touch higher
        private const float PawPadDropPx = 6f;            // main pad below center

        // --- Trailing step dots (mockup .dots) ---
        public const float DotDiameterPx = 16f;         // each progress dot
        public const float DotOutlineThicknessPx = 4f;  // hollow dot's ink ring
        public const float DotGapPx = 12f;              // gap between dots

        // --- Fixed Candy Cottage palette (shared-components.md), via CandyChrome ---
        public static readonly Color InkColor = CandyChrome.InkColor;
        public static readonly Color CreamColor = CandyChrome.CreamColor;
        public static readonly Color LeafColor = CandyChrome.LeafColor;

        // --- Gesture-arrow coach (#330, docs/specs/ui/onboarding-overlay.md) ---
        // A looping directional-arrow coach drawn over the Pan and Zoom steps
        // only, so the gesture is shown, not just told. Values are the approved
        // wireframe constants, authored at the 1200px reference; no inline
        // geometry literals (#161). Timing (BeatDurationSec/BeatPauseSec) is the
        // engine-free GestureCoach's; these are the on-screen geometry.
        public const float GestureCenterYPx = 480f;       // vertical anchor of the group
        public const float ArrowLengthPx = 200f;          // shaft + head, along its axis
        public const float ArrowThicknessPx = 22f;        // shaft width
        public const float ArrowHeadSizePx = 56f;         // chevron arrowhead span
        public const float ArrowOutlineThicknessPx = 6f;  // ink outline (shared baseline)
        public const float PanTravelPx = 260f;            // pan arrow center sweep per beat
        public const float ZoomNearOffsetPx = 70f;        // zoom arrow distance, closest
        public const float ZoomFarOffsetPx = 220f;        // zoom arrow distance, farthest
        public const float ArrowFillOpacity = 0.92f;      // keeps the map faintly readable

        // Gold fill (Decision 1): distinct from the coach bar's own chrome so the
        // gesture reads as "do this", not decoration.
        public static readonly Color GestureFillColor = CandyChrome.GoldColor;

        // Chevron arrowhead arm angle off the shaft axis — an internal rendering
        // constant (not a wireframe layout value), named per #161.
        private const float ChevronHalfAngleDeg = 40f;

        // Direction the canonical (points +x / right) arrow is rotated to, in
        // GUI degrees (clockwise, y-down): right, down, left, up.
        private const float ArrowAngleRightDeg = 0f;
        private const float ArrowAngleDownDeg = 90f;
        private const float ArrowAngleLeftDeg = 180f;
        private const float ArrowAngleUpDeg = 270f;

        /// <summary>Elapsed seconds the current gesture animation has been
        /// playing; accumulated in <see cref="Update"/> while the arrows show and
        /// reset otherwise, then mapped to a beat by the Core
        /// <see cref="GestureCoach"/>.</summary>
        private float gestureElapsed;

        /// <summary>#291: the bundled UI font, loaded from Resources so it ships
        /// in the Android build — never an editor-only built-in font, which
        /// renders invisible in the player. Same asset the HUD chip uses.</summary>
        public const string LabelFontResource = "DejaVuSans";

        private static GUIStyle messageStyle;

        private OnboardingSequence sequence;
        private GameState state;
        private CameraRig rig;
        private GridPoint startPosition;
        private float startZoom;

        /// <summary>The live sequence step; used by wiring tests to observe
        /// advancement. Done once onboarding is complete.</summary>
        public OnboardingStep CurrentStep
        {
            get { return sequence == null ? OnboardingStep.Done : sequence.CurrentStep; }
        }

        /// <summary>Whether the coach bar should render this frame — false
        /// before Init, and (per #371) only once BOTH the first-quest sequence
        /// is Done AND the reward chain has completed. Dismissal is gated on the
        /// reward chain finishing at the build step, not on the first quest
        /// alone (#207 auto-dismiss, extended to the whole guided journey by
        /// #374). The decision itself is engine-free Core
        /// (<see cref="OnboardingCoach.ShouldShow"/>).</summary>
        public bool ShouldDraw
        {
            get
            {
                return sequence != null
                    && OnboardingCoach.ShouldShow(sequence.CurrentStep, state.RewardChain.CurrentStep);
            }
        }

        /// <summary>#330: whether the animated gesture-arrow coach should draw
        /// this frame — only during the <see cref="OnboardingStep.Pan"/> and
        /// <see cref="OnboardingStep.Zoom"/> movement steps, and only while the
        /// coach itself is showing. Because <see cref="AdvanceCameraSteps"/>
        /// advances the sequence past Pan/Zoom the instant the real pan/zoom is
        /// registered, this flips false at that same moment — the arrows are a
        /// non-blocking visual coach with no separate hide gate to get wrong.</summary>
        public bool ShouldDrawGesture
        {
            get
            {
                return ShouldDraw
                    && (CurrentStep == OnboardingStep.Pan || CurrentStep == OnboardingStep.Zoom);
            }
        }

        public void Init(GameState state, CameraRig rig, ConversationPresenter presenter)
        {
            this.state = state;
            this.rig = rig;
            sequence = new OnboardingSequence(state);

            if (rig != null)
            {
                startPosition = rig.Controller.Position;
                startZoom = rig.Controller.Zoom;
            }

            presenter.Opened += dog => sequence.NotifyConversationOpened(dog);
        }

        /// <summary>One poll of the onboarding wiring: advance the camera
        /// steps from the live rig, then check quest completion. Public so
        /// EditMode tests can drive it without a running player loop.</summary>
        public void Poll()
        {
            if (sequence == null || sequence.CurrentStep == OnboardingStep.Done)
            {
                return;
            }

            AdvanceCameraSteps();

            // #329: self-heal the TapBubble step if the bubble interaction
            // already happened (opened/accepted early during Pan/Zoom) so the
            // flow never strands with "nothing to tap".
            sequence.Reconcile();

            if (sequence.CurrentStep == OnboardingStep.CompleteQuest)
            {
                CheckQuestCompletion();
            }

            if (sequence.CurrentStep == OnboardingStep.Done)
            {
                SaveStore.Save(state);
            }
        }

        /// <summary>#329: whether the target dog's speech bubble should stay
        /// hidden this frame because onboarding hasn't reached the TapBubble
        /// step yet. <see cref="DogView"/> consults this so the bubble is
        /// first tappable exactly at step 3. False when onboarding isn't
        /// running (no sequence) — the bubble follows its normal quest
        /// binding.</summary>
        public bool SuppressesBubbleFor(Dog dog)
        {
            return sequence != null && sequence.ShouldSuppressBubble(dog);
        }

        private void Update()
        {
            Poll();

            // #330: advance the gesture animation clock while the arrows show,
            // and reset it when they don't so each movement step restarts from
            // the first beat.
            if (ShouldDrawGesture)
            {
                gestureElapsed += Time.deltaTime;
            }
            else
            {
                gestureElapsed = 0f;
            }
        }

        private void AdvanceCameraSteps()
        {
            if (sequence.CurrentStep != OnboardingStep.Pan
                && sequence.CurrentStep != OnboardingStep.Zoom)
            {
                return;
            }

            if (rig == null)
            {
                // No CameraRig was wired (WorldBootstrap can find none): there
                // is nothing to pan or zoom, so don't deadlock the sequence on
                // steps the player physically can't perform — satisfy them so
                // onboarding can still reach the tap/complete steps and dismiss.
                sequence.NotifyPanned();
                sequence.NotifyZoomed();
                return;
            }

            if (!rig.Controller.Position.Equals(startPosition))
            {
                sequence.NotifyPanned();
            }

            if (!Mathf.Approximately(rig.Controller.Zoom, startZoom))
            {
                sequence.NotifyZoomed();
            }
        }

        private void CheckQuestCompletion()
        {
            if (sequence.TargetDog == null || sequence.TargetDog.HasActiveQuest)
            {
                return;
            }

            sequence.NotifyTargetDogQuestResolved();
            // Persistence on reaching Done is handled centrally in Poll (#329),
            // which also covers the self-heal cascade path to Done.
        }

        /// <summary>The two zoom arrows' screen centers, symmetric about the
        /// gesture anchor (#330).</summary>
        public readonly struct ZoomArrowCenters
        {
            public ZoomArrowCenters(Vector2 left, Vector2 right)
            {
                Left = left;
                Right = right;
            }

            public Vector2 Left { get; }
            public Vector2 Right { get; }
        }

        /// <summary>#330: the pan arrow's screen center for a beat + 0-1 progress,
        /// scaled from the 1200px reference. Horizontal beats sweep x by
        /// <see cref="PanTravelPx"/> about the anchor (screen-center-x at
        /// <see cref="GestureCenterYPx"/>); vertical beats sweep y. Public static
        /// so EditMode tests can assert it without a running player loop, mirroring
        /// <see cref="ComputeCoachRect"/>.</summary>
        public static Vector2 ComputePanArrowCenter(float screenWidth, float screenHeight, GestureBeat beat, float progress)
        {
            var scale = screenHeight / ReferenceHeightPx;
            var anchorX = screenWidth / 2f;
            var anchorY = GestureCenterYPx * scale;
            var travel = PanTravelPx * scale;
            var offset = Mathf.Lerp(-travel / 2f, travel / 2f, progress);

            switch (beat)
            {
                case GestureBeat.LeftToRight:
                    return new Vector2(anchorX + offset, anchorY);
                case GestureBeat.RightToLeft:
                    return new Vector2(anchorX - offset, anchorY);
                case GestureBeat.UpToDown:
                    return new Vector2(anchorX, anchorY + offset);
                case GestureBeat.DownToUp:
                    return new Vector2(anchorX, anchorY - offset);
                default:
                    return new Vector2(anchorX, anchorY);
            }
        }

        /// <summary>#330: each zoom arrow's scaled distance from the anchor for a
        /// beat + 0-1 progress — zoom-in spreads from <see cref="ZoomNearOffsetPx"/>
        /// to <see cref="ZoomFarOffsetPx"/>, zoom-out closes the other way. Both
        /// arrows sit this far either side of the anchor.</summary>
        public static float ComputeZoomArrowOffsetPx(float screenHeight, GestureBeat beat, float progress)
        {
            var scale = screenHeight / ReferenceHeightPx;
            var near = ZoomNearOffsetPx * scale;
            var far = ZoomFarOffsetPx * scale;

            switch (beat)
            {
                case GestureBeat.ZoomIn:
                    return Mathf.Lerp(near, far, progress);
                case GestureBeat.ZoomOut:
                    return Mathf.Lerp(far, near, progress);
                default:
                    return near;
            }
        }

        /// <summary>#330: the two zoom arrows' screen centers, placed
        /// symmetrically either side of the gesture anchor by
        /// <see cref="ComputeZoomArrowOffsetPx"/>.</summary>
        public static ZoomArrowCenters ComputeZoomArrowCenters(float screenWidth, float screenHeight, GestureBeat beat, float progress)
        {
            var scale = screenHeight / ReferenceHeightPx;
            var anchorX = screenWidth / 2f;
            var anchorY = GestureCenterYPx * scale;
            var offset = ComputeZoomArrowOffsetPx(screenHeight, beat, progress);
            return new ZoomArrowCenters(
                new Vector2(anchorX - offset, anchorY),
                new Vector2(anchorX + offset, anchorY));
        }

        /// <summary>Computes the coach bar rect for the given screen size at
        /// the minimum (<see cref="CoachWidthPx"/>) width: bottom-center, scaled
        /// from the 1920x1200 wireframe reference so each px constant keeps a
        /// fixed meaning across tablet sizes.</summary>
        public static Rect ComputeCoachRect(float screenWidth, float screenHeight)
        {
            return ComputeCoachRect(screenWidth, screenHeight, CoachWidthPx);
        }

        /// <summary>Grow-to-fit overload (#369/#374): sizes the bar to
        /// <paramref name="desiredContentWidthPx"/> (the measured message plus
        /// badge, dots, and paddings, in reference px), clamped to
        /// [<see cref="CoachWidthPx"/>, <see cref="CoachMaxWidthPx"/>]. Past the
        /// cap the message wraps and the bar grows in height instead, so no step
        /// string overflows the pill.</summary>
        public static Rect ComputeCoachRect(float screenWidth, float screenHeight, float desiredContentWidthPx)
        {
            var scale = screenHeight / ReferenceHeightPx;
            var width = ComputeCoachWidthPx(desiredContentWidthPx) * scale;
            var height = ComputeCoachHeightPx(NeedsWrap(desiredContentWidthPx)) * scale;
            var x = (screenWidth - width) / 2f;
            var y = screenHeight - height - CoachBottomMarginPx * scale;
            return new Rect(x, y, width, height);
        }

        /// <summary>The bar's reference-px width for a message whose full
        /// single-line content spans <paramref name="desiredContentWidthPx"/>:
        /// the minimum <see cref="CoachWidthPx"/>, grown to fit, clamped to
        /// <see cref="CoachMaxWidthPx"/> (#369/#374).</summary>
        public static float ComputeCoachWidthPx(float desiredContentWidthPx)
        {
            return Mathf.Clamp(desiredContentWidthPx, CoachWidthPx, CoachMaxWidthPx);
        }

        /// <summary>Whether the message must wrap to a second line — true once
        /// the desired single-line content would exceed <see cref="CoachMaxWidthPx"/>.</summary>
        public static bool NeedsWrap(float desiredContentWidthPx)
        {
            return desiredContentWidthPx > CoachMaxWidthPx;
        }

        /// <summary>The bar's reference-px height: the base
        /// <see cref="CoachHeightPx"/>, plus one <see cref="MsgFontPx"/> line
        /// when the message wraps (#369/#374).</summary>
        public static float ComputeCoachHeightPx(bool wrapped)
        {
            return wrapped ? CoachHeightPx + MsgFontPx : CoachHeightPx;
        }

        /// <summary>The bar's full single-line content width in reference px for
        /// a message measured at <paramref name="messageWidthPx"/> reference px:
        /// left/right padding + paw badge + gap + message + gap + the frozen
        /// four-dot row. Drives the grow-to-fit width in <see cref="OnGUI"/>.</summary>
        private static float DesiredContentWidthPx(float messageWidthPx)
        {
            var dotsRow = StepDotCount * DotDiameterPx + (StepDotCount - 1) * DotGapPx;
            return 2f * CoachPadXPx + PawDiameterPx + CoachGapPx
                + messageWidthPx + CoachGapPx + dotsRow;
        }

        /// <summary>The coach bar's current message. While the first-quest
        /// sequence runs it is the live <see cref="OnboardingSequence"/> step
        /// text (target dog's name substituted on the tap-bubble step); once the
        /// sequence is Done it becomes the current reward-chain step's prompt
        /// (#371, <see cref="OnboardingCoach.PromptForRewardStep"/>) — upgrade,
        /// then expand, then build — and empty only once the chain completes.
        /// Public so wiring tests can assert the text without rendering.</summary>
        public string MessageText
        {
            get
            {
                if (sequence == null)
                {
                    return string.Empty;
                }

                return sequence.CurrentStep == OnboardingStep.Done
                    ? OnboardingCoach.PromptForRewardStep(state.RewardChain.CurrentStep)
                    : PromptFor(sequence.CurrentStep);
            }
        }

        /// <summary>How many of the <c>StepDotCount</c> trailing dots are filled
        /// for the given step — one per guided step, up to and including the
        /// current one. The terminal Done state keeps them all filled.</summary>
        public static int FilledDotCount(OnboardingStep step)
        {
            return StepIndex(step) + 1;
        }

        private void OnGUI()
        {
            if (!ShouldDraw)
            {
                return;
            }

            var scale = Screen.height / ReferenceHeightPx;

            // Grow-to-fit (#369/#374): measure the message, size the bar to fit
            // it (clamped to [CoachWidthPx, CoachMaxWidthPx]); past the cap the
            // message wraps and the bar grows in height instead of overflowing.
            var style = MessageStyle(scale);
            var messageWidthRefPx = style.CalcSize(new GUIContent(MessageText)).x / scale;
            var desiredContentPx = DesiredContentWidthPx(messageWidthRefPx);
            style.wordWrap = NeedsWrap(desiredContentPx);

            DrawCoachBar(ComputeCoachRect(Screen.width, Screen.height, desiredContentPx), scale);

            // #330: the animated gesture-arrow coach layers over the map during
            // the Pan/Zoom steps only.
            if (ShouldDrawGesture)
            {
                DrawGesture(scale);
            }
        }

        /// <summary>#330: draws the looping directional-arrow coach for the
        /// current movement step — a single sweeping arrow for Pan, a symmetric
        /// pair for Zoom — using the beat + progress from the engine-free
        /// <see cref="GestureCoach"/>. Purely presentational; the beat logic and
        /// the arrow offsets are covered by Core and EditMode tests respectively,
        /// while this rotation/rendering is verified on-device.</summary>
        private void DrawGesture(float scale)
        {
            var beatState = GestureCoach.BeatAt(CurrentStep, gestureElapsed);
            if (beatState.Beat == GestureBeat.Hidden)
            {
                return;
            }

            if (beatState.Beat == GestureBeat.ZoomIn || beatState.Beat == GestureBeat.ZoomOut)
            {
                var centers = ComputeZoomArrowCenters(Screen.width, Screen.height, beatState.Beat, beatState.Progress);
                // Zoom-in points outward (apart); zoom-out points inward (together).
                var leftAngle = beatState.Beat == GestureBeat.ZoomIn ? ArrowAngleLeftDeg : ArrowAngleRightDeg;
                var rightAngle = beatState.Beat == GestureBeat.ZoomIn ? ArrowAngleRightDeg : ArrowAngleLeftDeg;
                DrawArrow(centers.Left, leftAngle, scale);
                DrawArrow(centers.Right, rightAngle, scale);
            }
            else
            {
                var center = ComputePanArrowCenter(Screen.width, Screen.height, beatState.Beat, beatState.Progress);
                DrawArrow(center, PanBeatAngleDeg(beatState.Beat), scale);
            }
        }

        private static float PanBeatAngleDeg(GestureBeat beat)
        {
            switch (beat)
            {
                case GestureBeat.RightToLeft:
                    return ArrowAngleLeftDeg;
                case GestureBeat.UpToDown:
                    return ArrowAngleDownDeg;
                case GestureBeat.DownToUp:
                    return ArrowAngleUpDeg;
                default:
                    return ArrowAngleRightDeg; // LeftToRight
            }
        }

        /// <summary>Draws one Candy Cottage arrow (Gold fill, ink outline)
        /// centered at <paramref name="center"/> and rotated to
        /// <paramref name="directionDeg"/> — a stadium shaft plus a two-armed
        /// chevron head, all built from <see cref="CandyChrome"/> stadiums so
        /// there is no external raster art.</summary>
        private static void DrawArrow(Vector2 center, float directionDeg, float scale)
        {
            var previous = GUI.matrix;
            GUIUtility.RotateAroundPivot(directionDeg, center);

            var length = ArrowLengthPx * scale;
            var thickness = ArrowThicknessPx * scale;
            var head = ArrowHeadSizePx * scale;
            var outline = ArrowOutlineThicknessPx * scale;

            var left = center.x - length / 2f;
            var tipX = center.x + length / 2f;
            var shaftRight = tipX - head;

            var shaft = new Rect(left, center.y - thickness / 2f, Mathf.Max(0f, shaftRight - left), thickness);
            DrawArrowStroke(shaft, outline);

            // Chevron head: two arms running back from the tip at +/- the half-angle.
            var arm = new Rect(tipX - head, center.y - thickness / 2f, head, thickness);
            var tip = new Vector2(tipX, center.y);
            DrawRotatedArrowStroke(arm, ChevronHalfAngleDeg, tip, outline);
            DrawRotatedArrowStroke(arm, -ChevronHalfAngleDeg, tip, outline);

            GUI.matrix = previous;
        }

        private static void DrawRotatedArrowStroke(Rect rect, float angleDeg, Vector2 pivot, float outline)
        {
            var saved = GUI.matrix;
            GUIUtility.RotateAroundPivot(angleDeg, pivot);
            DrawArrowStroke(rect, outline);
            GUI.matrix = saved;
        }

        private static void DrawArrowStroke(Rect rect, float outline)
        {
            if (rect.width <= 0f || rect.height <= 0f)
            {
                return;
            }

            var ink = new Rect(rect.x - outline, rect.y - outline, rect.width + 2f * outline, rect.height + 2f * outline);
            CandyChrome.DrawStadium(ink, InkColor);

            var fill = GestureFillColor;
            fill.a = ArrowFillOpacity;
            CandyChrome.DrawStadium(rect, fill);
        }

        /// <summary>Draws the Candy Cottage coach bar (#297), back to front:
        /// hard straight-down shadow, ink outline, cream pill fill, the leading
        /// leaf paw badge, the trailing step dots, then the message text
        /// centered between them. All chrome is procedural (CandyChrome) — no
        /// external raster art.</summary>
        private void DrawCoachBar(Rect coach, float scale)
        {
            var outline = OutlineThicknessPx * scale;

            var shadow = new Rect(coach.x, coach.y + ShadowOffsetPx * scale, coach.width, coach.height);
            CandyChrome.DrawStadium(shadow, InkColor);
            CandyChrome.DrawStadium(coach, InkColor);

            var fill = new Rect(
                coach.x + outline,
                coach.y + outline,
                coach.width - 2f * outline,
                coach.height - 2f * outline);
            CandyChrome.DrawStadium(fill, CreamColor);

            var padX = CoachPadXPx * scale;
            var contentLeft = coach.x + outline + padX;
            var contentRight = coach.xMax - outline - padX;

            var pawDiameter = PawDiameterPx * scale;
            var pawRect = new Rect(contentLeft, coach.center.y - pawDiameter / 2f, pawDiameter, pawDiameter);
            DrawPawBadge(pawRect, scale);

            var dotsLeft = DrawStepDots(contentRight, coach.center.y, scale);

            var gap = CoachGapPx * scale;
            var msgLeft = pawRect.xMax + gap;
            var msgRight = dotsLeft - gap;
            var msgRect = new Rect(msgLeft, coach.y, Mathf.Max(0f, msgRight - msgLeft), coach.height);
            GUI.Label(msgRect, MessageText, MessageStyle(scale));
        }

        /// <summary>The leaf paw badge: an ink-ringed leaf disc with a
        /// procedural ink paw-print (one pad + three toes) — no emoji glyph or
        /// raster art, so it renders identically on device.</summary>
        private static void DrawPawBadge(Rect badge, float scale)
        {
            CandyChrome.DrawCircle(badge, InkColor);
            var ring = PawOutlineThicknessPx * scale;
            var leaf = new Rect(badge.x + ring, badge.y + ring, badge.width - 2f * ring, badge.height - 2f * ring);
            CandyChrome.DrawCircle(leaf, LeafColor);

            var cx = badge.center.x;
            var cy = badge.center.y;

            var padDiameter = PawPadDiameterPx * scale;
            var pad = new Rect(cx - padDiameter / 2f, cy - padDiameter / 2f + PawPadDropPx * scale, padDiameter, padDiameter);
            CandyChrome.DrawCircle(pad, InkColor);

            var toeDiameter = PawToeDiameterPx * scale;
            var spread = PawToeSpreadPx * scale;
            var rise = PawToeRisePx * scale;
            DrawToe(cx - spread, cy - rise, toeDiameter);
            DrawToe(cx, cy - rise - PawCenterToeExtraRisePx * scale, toeDiameter);
            DrawToe(cx + spread, cy - rise, toeDiameter);
        }

        private static void DrawToe(float centerX, float centerY, float diameter)
        {
            CandyChrome.DrawCircle(new Rect(centerX - diameter / 2f, centerY - diameter / 2f, diameter, diameter), InkColor);
        }

        /// <summary>Draws the trailing row of <c>StepDotCount</c> progress dots,
        /// right-aligned to <paramref name="rightEdge"/>: filled dots are solid
        /// ink discs, remaining dots are hollow (cream inner over an ink ring).
        /// Returns the row's left edge so the message can stop short of it.</summary>
        private float DrawStepDots(float rightEdge, float centerY, float scale)
        {
            var dotDiameter = DotDiameterPx * scale;
            var dotGap = DotGapPx * scale;
            var rowWidth = StepDotCount * dotDiameter + (StepDotCount - 1) * dotGap;
            var left = rightEdge - rowWidth;

            var filled = FilledDotCount(sequence.CurrentStep);
            var inset = DotOutlineThicknessPx * scale;
            for (var i = 0; i < StepDotCount; i++)
            {
                var x = left + i * (dotDiameter + dotGap);
                var dot = new Rect(x, centerY - dotDiameter / 2f, dotDiameter, dotDiameter);
                CandyChrome.DrawCircle(dot, InkColor);
                if (i >= filled)
                {
                    var inner = new Rect(dot.x + inset, dot.y + inset, dot.width - 2f * inset, dot.height - 2f * inset);
                    CandyChrome.DrawCircle(inner, CreamColor);
                }
            }

            return left;
        }

        private static GUIStyle MessageStyle(float scale)
        {
            if (messageStyle == null)
            {
                messageStyle = new GUIStyle
                {
                    font = Resources.Load<Font>(LabelFontResource),
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleLeft,
                    wordWrap = false,
                };
                messageStyle.normal.textColor = InkColor;
            }

            messageStyle.fontSize = Mathf.RoundToInt(MsgFontPx * scale);
            return messageStyle;
        }

        private string PromptFor(OnboardingStep step)
        {
            switch (step)
            {
                case OnboardingStep.Pan:
                    return "Welcome to Doggiehood! Drag to look around the neighborhood.";
                case OnboardingStep.Zoom:
                    return "Nice! Pinch (or scroll) to zoom in and out.";
                case OnboardingStep.TapBubble:
                    var name = sequence.TargetDog != null ? sequence.TargetDog.Name : "a dog";
                    return $"{name} has something to say — tap the speech bubble!";
                default:
                    return "Help them out to finish your first quest!";
            }
        }

        private static int StepIndex(OnboardingStep step)
        {
            switch (step)
            {
                case OnboardingStep.Pan:
                    return 0;
                case OnboardingStep.Zoom:
                    return 1;
                case OnboardingStep.TapBubble:
                    return 2;
                default:
                    return 3;
            }
        }
    }
}
