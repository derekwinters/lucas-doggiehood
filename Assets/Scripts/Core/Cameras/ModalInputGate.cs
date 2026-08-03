using System.Collections.Generic;

namespace Doggiehood.Core.Cameras
{
    /// <summary>
    /// #544: a deterministic, engine-free registry of open center-anchored
    /// modals, so the world-tap router can swallow taps that land on a modal
    /// (its panel or its dim scrim) instead of leaking them to the
    /// <c>HouseView</c>/<c>DogView</c>/expansion-lock colliders behind it.
    ///
    /// The #422 fix gated world routing solely on
    /// <c>EventSystem.IsPointerOverGameObject</c>, which reads the input
    /// module's last-processed pointer and can report <c>false</c> at touch
    /// release even when the finger is over the scrim — so a fast finger tap
    /// on a profile panel leaked through to the world while the scrim's own
    /// <c>Button.onClick</c> still closed the profile. This gate replaces the
    /// timing-dependent signal with an explicit "a modal is open" flag: each
    /// center-anchored overlay <see cref="Register"/>s on open and
    /// <see cref="Unregister"/>s on close, and the router short-circuits while
    /// <see cref="IsBlocking"/> is true. It is pure C# — no engine dependency,
    /// no frame timing — so it is fully unit-testable and reliable regardless
    /// of when the EventSystem last processed a pointer.
    ///
    /// It suppresses only the <em>world pass-through</em>. A scrim's own close
    /// button still fires through UGUI's GraphicRaycaster, so panels that
    /// intentionally dismiss on scrim tap keep doing so — the "dismiss this
    /// panel" and "pass the tap through to the world" behaviors stay distinct.
    /// </summary>
    public sealed class ModalInputGate
    {
        /// <summary>
        /// The one shared gate every center-anchored modal and the world-tap
        /// router converge on, so a modal opened anywhere blocks world taps
        /// everywhere.
        /// </summary>
        public static ModalInputGate Shared { get; } = new ModalInputGate();

        // A set (not a counter) so a modal re-registering without an
        // intervening close is idempotent — a single unregister still releases
        // it, and the gate can never get stuck blocking.
        private readonly HashSet<object> openModals = new HashSet<object>();

        /// <summary>True while at least one modal is registered open — the
        /// world-tap router must swallow taps rather than route them to the
        /// world.</summary>
        public bool IsBlocking => openModals.Count > 0;

        /// <summary>Marks <paramref name="token"/>'s modal as open. Idempotent:
        /// registering the same token twice still needs only one
        /// <see cref="Unregister"/>. A null token is ignored.</summary>
        public void Register(object token)
        {
            if (token == null)
            {
                return;
            }

            openModals.Add(token);
        }

        /// <summary>Marks <paramref name="token"/>'s modal as closed.
        /// Unregistering an unknown/already-removed/null token is a no-op, not
        /// an error.</summary>
        public void Unregister(object token)
        {
            if (token == null)
            {
                return;
            }

            openModals.Remove(token);
        }
    }
}
