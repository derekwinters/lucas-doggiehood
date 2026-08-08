namespace Doggiehood.Core.Interaction
{
    /// <summary>#670: the kinds of gesture the <see cref="InputAuthority"/>
    /// routes. One value per raw-input path that used to reach the camera
    /// ungated, so each can be blocked — and tested — on its own.</summary>
    public enum InputGestureKind
    {
        /// <summary>A single pointer's whole press → travel → release
        /// lifecycle. It pans the camera while it travels and taps on release;
        /// both are the same gesture, which is why ownership can be latched
        /// once at press-down rather than re-decided from drag distance.</summary>
        Pan,

        /// <summary>Two-finger spread/squeeze, in pixels of span change.</summary>
        Pinch,

        /// <summary>Two-finger twist, in degrees of finger rotation.</summary>
        Twist,

        /// <summary>Mouse wheel, in scroll ticks. Discrete: it has no press or
        /// release, so it is resolved and delivered in one shot.</summary>
        Scroll,
    }

    /// <summary>#670: where a gesture is in its lifecycle. Ownership is
    /// resolved at <see cref="Began"/> and held to <see cref="Ended"/>.</summary>
    public enum InputGesturePhase
    {
        Began,
        Changed,
        Ended,

        /// <summary>The gesture stopped without a release — a second finger
        /// turned a pan into a pinch, or the scene tore down mid-press. Distinct
        /// from <see cref="Ended"/> so a cancelled press can't fire the tap the
        /// player never made.</summary>
        Cancelled,
    }

    /// <summary>
    /// #670: one raw input event, engine-free, as handed to the
    /// <see cref="InputAuthority"/>. The Unity layer's sole job is to build
    /// these from <c>UnityEngine.Input</c> and hand them over; every decision
    /// about who receives them is made in Core.
    /// </summary>
    public readonly struct InputGesture
    {
        /// <summary>The pointer id for the desktop mouse. Platform touch
        /// fingerIds are non-negative, so the synthetic pointers are negative
        /// and can never collide with a real finger.</summary>
        public const int MousePointerId = -1;

        /// <summary>The pointer id shared by both halves of a two-finger
        /// gesture, so a pinch and the twist it emits alongside belong to one
        /// gesture with one owner. Negative so it can never collide with a
        /// platform touch fingerId.</summary>
        public const int TwoFingerPointerId = -2;

        /// <summary>The pointer id for the (pointer-less) mouse wheel.</summary>
        public const int ScrollPointerId = -3;

        public InputGesture(
            InputGestureKind kind,
            int pointerId,
            float screenX,
            float screenY,
            float deltaX,
            float deltaY,
            float scalar)
        {
            Kind = kind;
            PointerId = pointerId;
            ScreenX = screenX;
            ScreenY = screenY;
            DeltaX = deltaX;
            DeltaY = deltaY;
            Scalar = scalar;
        }

        public InputGestureKind Kind { get; }

        /// <summary>Identifies the in-flight gesture this event belongs to —
        /// a touch fingerId, <see cref="TwoFingerPointerId"/>, or
        /// <see cref="ScrollPointerId"/>. Ownership latches per pointer id.</summary>
        public int PointerId { get; }

        public float ScreenX { get; }

        public float ScreenY { get; }

        /// <summary>Pointer travel since the previous sample, in pixels
        /// (<see cref="InputGestureKind.Pan"/> only).</summary>
        public float DeltaX { get; }

        public float DeltaY { get; }

        /// <summary>The single-axis magnitude of a non-pan gesture: pinch span
        /// change in pixels, twist in degrees, scroll in ticks.</summary>
        public float Scalar { get; }

        public static InputGesture Pan(int pointerId, float screenX, float screenY, float deltaX = 0f, float deltaY = 0f)
            => new InputGesture(InputGestureKind.Pan, pointerId, screenX, screenY, deltaX, deltaY, 0f);

        public static InputGesture Pinch(float pinchDeltaPixels)
            => new InputGesture(InputGestureKind.Pinch, TwoFingerPointerId, 0f, 0f, 0f, 0f, pinchDeltaPixels);

        public static InputGesture Twist(float twistDeltaDegrees)
            => new InputGesture(InputGestureKind.Twist, TwoFingerPointerId, 0f, 0f, 0f, 0f, twistDeltaDegrees);

        public static InputGesture Scroll(float scrollTicks)
            => new InputGesture(InputGestureKind.Scroll, ScrollPointerId, 0f, 0f, 0f, 0f, scrollTicks);
    }
}
