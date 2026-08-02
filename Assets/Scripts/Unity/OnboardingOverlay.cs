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

        // --- Phase-title tab (#451, onboarding-overlay.md "Phase-title region") ---
        // An overlapping gold tab at the bar's top-left naming the current
        // onboarding PHASE, styled like the DialogueBox name tag but drawn
        // procedurally via CandyChrome (the coach bar is IMGUI, not the UGUI
        // shell). Replaces the former trailing step-dots. Values are the approved
        // constants from the spec table, authored at the 1920x1200 reference; no
        // inline geometry literals (#161).
        public const float PhaseTitleLeftInsetPx = 34f;   // tab inset from bar's left edge
        public const float PhaseTitleOffsetPx = 28f;      // overlap above the bar's top edge
        public const float PhaseTitlePaddingXPx = 30f;    // tab horizontal label inset
        public const float PhaseTitlePaddingYPx = 8f;     // tab vertical label inset
        public const int PhaseTitleFontPx = 26;           // tab label size
        // Content-row paddings so the badge/message row (and a wrapped two-line
        // message) clears the overlapping tab instead of colliding with it.
        public const float PhaseTitleContentTopPaddingPx = 48f;
        public const float PhaseTitleContentBottomPaddingPx = 16f;

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
        /// playing; accumulated in <see cref="StepGestureClock"/> while the arrows
        /// show and reset otherwise, then mapped to a beat by the Core
        /// <see cref="GestureCoach"/>.</summary>
        private float gestureElapsed;

        /// <summary>#468: the <see cref="CurrentStep"/> observed on the previous
        /// gesture-clock tick, so the clock can restart from the first beat on ANY
        /// step change — not only when <see cref="ShouldDrawGesture"/> flips false.
        /// <see cref="OnboardingStep.Pan"/> is the enum's zero value, matching the
        /// live first step after <see cref="Init"/>.</summary>
        private OnboardingStep lastGestureStep;

        /// <summary>#291: the bundled UI font, loaded from Resources so it ships
        /// in the Android build — never an editor-only built-in font, which
        /// renders invisible in the player. Same asset the HUD chip uses.</summary>
        public const string LabelFontResource = "DejaVuSans";

        private static GUIStyle messageStyle;
        private static GUIStyle phaseTitleStyle;

        private OnboardingSequence sequence;
        private GameState state;
        private CameraRig rig;
        private GridPoint startPosition;
        private float startZoom;

        /// <summary>#506: reports whether a centered modal panel (house/dog
        /// profile, and any future centered panel) is currently open. Wired from
        /// <c>WorldBootstrap</c>, which owns the references to those overlays; the
        /// bottom-anchored coach bar is suppressed while one is open so it can't
        /// cover the very button it points at (e.g. the house-profile Upgrade
        /// button during the Upgrade step). Null when unwired — treated as "no
        /// panel open", preserving the pre-#506 behavior.</summary>
        private System.Func<bool> centeredPanelOpen;

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
        /// #374). #506: also false while a centered modal panel is open (see
        /// <see cref="centeredPanelOpen"/>), so the bottom-anchored bar never
        /// covers a centered panel's controls. The decision itself is engine-free
        /// Core (<see cref="OnboardingCoach.ShouldShow"/>).</summary>
        public bool ShouldDraw
        {
            get
            {
                return sequence != null
                    && OnboardingCoach.ShouldShow(
                        sequence.CurrentStep,
                        state.RewardChain.CurrentStep,
                        IsCenteredPanelOpen);
            }
        }

        /// <summary>#506: whether a centered modal panel is currently open, per the
        /// wired <see cref="centeredPanelOpen"/> observer. False when no observer
        /// was wired (the pre-#506 default).</summary>
        private bool IsCenteredPanelOpen
        {
            get { return centeredPanelOpen != null && centeredPanelOpen(); }
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

        public void Init(GameState state, CameraRig rig, ConversationPresenter presenter,
            System.Func<bool> centeredPanelOpen = null)
        {
            this.state = state;
            this.rig = rig;
            this.centeredPanelOpen = centeredPanelOpen;
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
            StepGestureClock(Time.deltaTime);
        }

        /// <summary>#330/#468: advances the gesture-animation clock by
        /// <paramref name="deltaSeconds"/> and returns the resulting elapsed
        /// seconds. Resets to 0 on ANY <see cref="CurrentStep"/> change — so each
        /// movement step restarts from its first beat, including across the
        /// <see cref="OnboardingStep.Pan"/> -> <see cref="OnboardingStep.Zoom"/>
        /// handoff where <see cref="ShouldDrawGesture"/> stays true — and whenever
        /// the arrows aren't showing. Public so EditMode tests can drive the reset
        /// without a running player loop, mirroring <see cref="Poll"/>.</summary>
        public float StepGestureClock(float deltaSeconds)
        {
            if (CurrentStep != lastGestureStep)
            {
                gestureElapsed = 0f;
                lastGestureStep = CurrentStep;
            }

            if (ShouldDrawGesture)
            {
                gestureElapsed += deltaSeconds;
            }
            else
            {
                gestureElapsed = 0f;
            }

            return gestureElapsed;
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

        /// <summary>The bar's content-sized reference-px height (#451): the
        /// measured content row height — the base <see cref="CoachHeightPx"/>,
        /// grown by one <see cref="MsgFontPx"/> line when the message wraps —
        /// clamped to at least <see cref="CoachHeightPx"/>, plus the
        /// phase-title tab's content paddings so the row clears the overlapping
        /// tab at any message length: <c>max(CoachHeightPx, content) +
        /// PhaseTitleContentTopPaddingPx + PhaseTitleContentBottomPaddingPx</c>
        /// (88 + 48 + 16 = 152 for a single line; taller when wrapped). The bar
        /// grows upward, so <see cref="CoachBottomMarginPx"/> is unaffected.</summary>
        public static float ComputeCoachHeightPx(bool wrapped)
        {
            var content = wrapped ? CoachHeightPx + MsgFontPx : CoachHeightPx;
            return Mathf.Max(CoachHeightPx, content)
                + PhaseTitleContentTopPaddingPx + PhaseTitleContentBottomPaddingPx;
        }

        /// <summary>The bar's full single-line content width in reference px for
        /// a message measured at <paramref name="messageWidthPx"/> reference px:
        /// left/right padding + paw badge + gap + message. Reserves no trailing
        /// dots column (#451 removed the step dots). Drives the grow-to-fit width
        /// in <see cref="OnGUI"/>; public so EditMode tests can assert it.</summary>
        public static float DesiredContentWidthPx(float messageWidthPx)
        {
            return 2f * CoachPadXPx + PawDiameterPx + CoachGapPx + messageWidthPx;
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

        /// <summary>#451: the phase-title tab's label for the current onboarding
        /// phase, from the engine-free <see cref="OnboardingCoach.PhaseTitle"/>
        /// lookup — "Learn the ropes" through every tutorial step, then the
        /// reward-chain phase title (fix up a home / grow the neighborhood /
        /// build a house). Empty before Init or once onboarding is fully done.
        /// Public so wiring tests can assert the label without rendering.</summary>
        public string PhaseTitleText
        {
            get
            {
                return sequence == null
                    ? string.Empty
                    : OnboardingCoach.PhaseTitle(sequence.CurrentStep, state.RewardChain.CurrentStep);
            }
        }

        /// <summary>#451: the phase-title tab's rect for a coach bar at
        /// <paramref name="coach"/> and a label measured at
        /// <paramref name="labelWidthPx"/> screen px: top-left, inset
        /// <see cref="PhaseTitleLeftInsetPx"/> from the bar's left edge and
        /// overlapping <see cref="PhaseTitleOffsetPx"/> above its top edge, sized
        /// to the label plus <see cref="PhaseTitlePaddingXPx"/>/<see cref="PhaseTitlePaddingYPx"/>.
        /// Public static so EditMode tests can assert placement without rendering,
        /// mirroring <see cref="ComputeCoachRect"/>.</summary>
        public static Rect ComputePhaseTitleRect(Rect coach, float labelWidthPx, float scale)
        {
            var height = (PhaseTitleFontPx + 2f * PhaseTitlePaddingYPx) * scale;
            var width = labelWidthPx + 2f * PhaseTitlePaddingXPx * scale;
            var x = coach.x + PhaseTitleLeftInsetPx * scale;
            var y = coach.y - PhaseTitleOffsetPx * scale;
            return new Rect(x, y, width, height);
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

        /// <summary>#468: the screen position of an arrow's tip — the point half
        /// a shaft-length (<see cref="ArrowLengthPx"/>) from <paramref name="center"/>
        /// along the arrow's axis after the per-direction rotation. Public static
        /// so EditMode tests can assert the chevron head's anchor without a running
        /// player loop, mirroring <see cref="ComputePanArrowCenter"/>.</summary>
        public static Vector2 ComputeArrowTip(Vector2 center, float directionDeg, float scale)
        {
            var halfLength = ArrowLengthPx * scale / 2f;
            return center + Rotate(new Vector2(halfLength, 0f), directionDeg);
        }

        /// <summary>#468: the screen position of a chevron arm's far (back)
        /// endpoint. Each arm is one head-length (<see cref="ArrowHeadSizePx"/>)
        /// bar running back from the tip, rotated by a SINGLE consistent rotation
        /// of <c>directionDeg + <paramref name="armHalfAngleDeg"/></c> — never a
        /// second <see cref="GUIUtility.RotateAroundPivot"/> nested on the already
        /// per-direction-rotated shaft matrix (whose differing pivot mispositioned
        /// the head for every non-zero direction). <paramref name="armHalfAngleDeg"/>
        /// is the signed half-angle off the shaft axis (<c>+/-</c>
        /// <see cref="ChevronHalfAngleDeg"/>). Public static so the head geometry is
        /// EditMode-testable, mirroring <see cref="ComputePanArrowCenter"/>.</summary>
        public static Vector2 ComputeChevronArmEnd(Vector2 center, float directionDeg, float armHalfAngleDeg, float scale)
        {
            var tip = ComputeArrowTip(center, directionDeg, scale);
            var head = ArrowHeadSizePx * scale;
            return tip + Rotate(new Vector2(-head, 0f), directionDeg + armHalfAngleDeg);
        }

        /// <summary>Rotates <paramref name="v"/> by <paramref name="degrees"/>
        /// using the same clockwise (GUI y-down) convention as
        /// <see cref="GUIUtility.RotateAroundPivot"/>, so the computed geometry and
        /// the rendered strokes stay in lockstep.</summary>
        private static Vector2 Rotate(Vector2 v, float degrees)
        {
            var rad = degrees * Mathf.Deg2Rad;
            var cos = Mathf.Cos(rad);
            var sin = Mathf.Sin(rad);
            return new Vector2(v.x * cos - v.y * sin, v.x * sin + v.y * cos);
        }

        /// <summary>Draws one Candy Cottage arrow (Gold fill, ink outline)
        /// centered at <paramref name="center"/> and rotated to
        /// <paramref name="directionDeg"/> — a stadium shaft plus a two-armed
        /// chevron head, all built from <see cref="CandyChrome"/> stadiums so
        /// there is no external raster art.</summary>
        private static void DrawArrow(Vector2 center, float directionDeg, float scale)
        {
            var length = ArrowLengthPx * scale;
            var thickness = ArrowThicknessPx * scale;
            var head = ArrowHeadSizePx * scale;
            var outline = ArrowOutlineThicknessPx * scale;

            // Shaft: one rotation about the arrow center is correct on its own.
            var previous = GUI.matrix;
            GUIUtility.RotateAroundPivot(directionDeg, center);
            var left = center.x - length / 2f;
            var shaftRight = center.x + length / 2f - head;
            var shaft = new Rect(left, center.y - thickness / 2f, Mathf.Max(0f, shaftRight - left), thickness);
            DrawArrowStroke(shaft, outline);
            GUI.matrix = previous;

            // Chevron head (#468): each arm is drawn under ONE rotation about the
            // final tip — directionDeg combined with the arm's +/- half-angle — so
            // the head stays attached to the shaft for every direction. Previously
            // a second RotateAroundPivot(ChevronHalfAngleDeg, tip) was nested on the
            // already-rotated shaft matrix; the differing pivots don't commute, so
            // the head was only correct at directionDeg == 0.
            var tip = ComputeArrowTip(center, directionDeg, scale);
            DrawChevronArm(tip, directionDeg + ChevronHalfAngleDeg, head, thickness, outline);
            DrawChevronArm(tip, directionDeg - ChevronHalfAngleDeg, head, thickness, outline);
        }

        private static void DrawChevronArm(Vector2 tip, float armDeg, float head, float thickness, float outline)
        {
            var saved = GUI.matrix;
            GUIUtility.RotateAroundPivot(armDeg, tip);
            var arm = new Rect(tip.x - head, tip.y - thickness / 2f, head, thickness);
            DrawArrowStroke(arm, outline);
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

        /// <summary>Draws the Candy Cottage coach bar (#297/#451), back to front:
        /// hard straight-down shadow, ink outline, cream pill fill, the leading
        /// leaf paw badge and the message text in a content row inset below the
        /// tab, then the overlapping top-left gold phase-title tab. All chrome is
        /// procedural (CandyChrome) — no external raster art.</summary>
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

            // #451: the content row sits below the tab's overlap and above the
            // bottom padding, so a wrapped two-line message clears the tab.
            var contentTop = coach.y + PhaseTitleContentTopPaddingPx * scale;
            var contentBottom = coach.yMax - PhaseTitleContentBottomPaddingPx * scale;
            var contentCenterY = (contentTop + contentBottom) / 2f;

            var pawDiameter = PawDiameterPx * scale;
            var pawRect = new Rect(contentLeft, contentCenterY - pawDiameter / 2f, pawDiameter, pawDiameter);
            DrawPawBadge(pawRect, scale);

            var gap = CoachGapPx * scale;
            var msgLeft = pawRect.xMax + gap;
            var msgRect = new Rect(msgLeft, contentTop, Mathf.Max(0f, contentRight - msgLeft),
                Mathf.Max(0f, contentBottom - contentTop));
            GUI.Label(msgRect, MessageText, MessageStyle(scale));

            // The tab is drawn last so it overlaps on top of the bar's top edge.
            DrawPhaseTitleTab(coach, scale);
        }

        /// <summary>#451: draws the overlapping top-left gold phase-title tab —
        /// an ink outline + Gold fill stadium (styled like the DialogueBox name
        /// tag, drawn procedurally on IMGUI), labeled from the Core
        /// <see cref="OnboardingCoach.PhaseTitle"/> lookup. Placement is
        /// <see cref="ComputePhaseTitleRect"/> (EditMode-tested); this
        /// rendering is verified on-device.</summary>
        private void DrawPhaseTitleTab(Rect coach, float scale)
        {
            var title = PhaseTitleText;
            if (string.IsNullOrEmpty(title))
            {
                return;
            }

            var style = PhaseTitleStyle(scale);
            var labelWidth = style.CalcSize(new GUIContent(title)).x;
            var tab = ComputePhaseTitleRect(coach, labelWidth, scale);

            var outline = OutlineThicknessPx * scale;
            CandyChrome.DrawStadium(tab, InkColor);
            var fill = new Rect(
                tab.x + outline,
                tab.y + outline,
                tab.width - 2f * outline,
                tab.height - 2f * outline);
            CandyChrome.DrawStadium(fill, CandyChrome.GoldColor);

            GUI.Label(tab, title, style);
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

        /// <summary>#451: the phase-title tab label style — bundled DejaVuSans,
        /// bold, ink text centered in the gold tab, sized at
        /// <see cref="PhaseTitleFontPx"/> (matching the name tag).</summary>
        private static GUIStyle PhaseTitleStyle(float scale)
        {
            if (phaseTitleStyle == null)
            {
                phaseTitleStyle = new GUIStyle
                {
                    font = Resources.Load<Font>(LabelFontResource),
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter,
                    wordWrap = false,
                };
                phaseTitleStyle.normal.textColor = InkColor;
            }

            phaseTitleStyle.fontSize = Mathf.RoundToInt(PhaseTitleFontPx * scale);
            return phaseTitleStyle;
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
    }
}
