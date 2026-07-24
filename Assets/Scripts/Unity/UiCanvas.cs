using UnityEngine;
using UnityEngine.UI;

namespace Doggiehood.Unity
{
    /// <summary>
    /// Thin wiring for the game's UI canvas (#256). Every UI wireframe's
    /// layout constants are authored against a fixed <b>1920x1200 (16:10)</b>
    /// tablet reference resolution (see docs/specs/ui/index.md), so the
    /// canvas scales from that reference via a <see cref="CanvasScaler"/> in
    /// Scale-With-Screen-Size mode. That is what gives a 64px chip / 96px
    /// button (the #173/#174 constants) a fixed on-screen size across the
    /// supported range of tablet sizes. No decision logic lives here — this
    /// only configures Unity UI components; the graybox IMGUI overlays
    /// (HudOverlay/OnboardingOverlay) migrate onto this canvas later.
    /// </summary>
    [RequireComponent(typeof(Canvas))]
    public sealed class UiCanvas : MonoBehaviour
    {
        /// <summary>Reference width every UI constant is authored against (#256).</summary>
        public const float ReferenceWidth = 1920f;

        /// <summary>Reference height every UI constant is authored against (#256).</summary>
        public const float ReferenceHeight = 1200f;

        /// <summary>
        /// Balance between matching the reference width and the reference
        /// height when the device aspect ratio differs from 16:10. 0.5 keeps
        /// scaling even across the range of tablet aspect ratios rather than
        /// favouring one axis. CanvasScaler.ScreenMatchMode.MatchWidthOrHeight.
        /// </summary>
        public const float ScreenMatchWidthOrHeight = 0.5f;

        /// <summary>The authored tablet reference resolution as a vector.</summary>
        public static Vector2 ReferenceResolution => new Vector2(ReferenceWidth, ReferenceHeight);

        private void Awake()
        {
            Configure();
        }

        /// <summary>
        /// Ensures the Canvas + CanvasScaler + GraphicRaycaster exist and
        /// pins the scaler to Scale-With-Screen-Size at the 1920x1200
        /// reference. Public and return-typed so EditMode tests can apply and
        /// assert it directly without waiting on a Play-mode frame.
        /// </summary>
        public CanvasScaler Configure()
        {
            var canvas = GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = GetComponent<CanvasScaler>();
            if (scaler == null)
            {
                scaler = gameObject.AddComponent<CanvasScaler>();
            }

            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = ReferenceResolution;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = ScreenMatchWidthOrHeight;

            if (GetComponent<GraphicRaycaster>() == null)
            {
                gameObject.AddComponent<GraphicRaycaster>();
            }

            return scaler;
        }
    }
}
