using System;

namespace Doggiehood.Core.Interaction
{
    /// <summary>
    /// #670: anything that wants to receive input. Registering with the
    /// <see cref="InputAuthority"/> is the <em>only</em> way to be offered a
    /// gesture — a consumer that never registers receives nothing, which is
    /// what makes new input work blocked by default instead of live by default.
    /// </summary>
    public interface IInputConsumer
    {
        /// <summary>Which priority tier this consumer sits in (R3).</summary>
        InputTier Tier { get; }

        /// <summary>"Is this gesture mine?" — asked once, at press-down, in
        /// tier order. Typically a hit-test. The first consumer to say yes owns
        /// the gesture outright.</summary>
        bool ClaimsGesture(InputGesture gesture);

        /// <summary>Delivery of a gesture this consumer owns. Never called for
        /// a gesture claimed by anyone else.</summary>
        void OnGesture(InputGesture gesture, InputGesturePhase phase);
    }

    /// <summary>
    /// #670: an <see cref="IInputConsumer"/> assembled from delegates, so the
    /// Unity wiring layer (and tests) can register a consumer without each
    /// site declaring a type. Keeps the Unity layer to "what to do", with the
    /// "who gets it" decision entirely in <see cref="InputAuthority"/>.
    /// </summary>
    public sealed class DelegateInputConsumer : IInputConsumer
    {
        private readonly Func<InputGesture, bool> claims;
        private readonly Action<InputGesture, InputGesturePhase> onGesture;

        /// <param name="claims">Hit-test asked at press-down; null claims every
        /// gesture (what the camera fallback wants).</param>
        public DelegateInputConsumer(
            InputTier tier,
            Action<InputGesture, InputGesturePhase> onGesture,
            Func<InputGesture, bool> claims = null)
        {
            Tier = tier;
            this.onGesture = onGesture ?? throw new ArgumentNullException(nameof(onGesture));
            this.claims = claims;
        }

        public InputTier Tier { get; }

        public bool ClaimsGesture(InputGesture gesture) => claims == null || claims(gesture);

        public void OnGesture(InputGesture gesture, InputGesturePhase phase) => onGesture(gesture, phase);
    }
}
