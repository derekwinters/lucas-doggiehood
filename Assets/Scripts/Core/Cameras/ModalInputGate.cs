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
    ///
    /// #670: this gate was always correct — the problem was who asked it. Only
    /// the world-tap router ever did, so camera pan, pinch, twist and scroll
    /// reached the camera without consulting it and a drag on an open dialog
    /// panned the map underneath. It is now the modal tier of
    /// <c>Doggiehood.Core.Interaction.InputAuthority</c>, consulted once at
    /// press-down for <em>every</em> gesture kind, so registering here blocks
    /// all input rather than only taps.
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

        /// <summary>
        /// #568: latched true when a still-open modal <see cref="Unregister"/>ed
        /// during the current frame, and cleared by <see cref="EndFrame"/> at end
        /// of frame. Closes a same-frame tap-through race: a modal's
        /// <c>Unregister</c> runs synchronously while the EventSystem dispatches
        /// the very tap that dismissed it, and <c>EventSystem.Update()</c> has no
        /// defined order relative to the camera rig's <c>Update()</c> that routes
        /// world taps. Without this latch, if the EventSystem ran first
        /// <see cref="IsBlocking"/> would already read <c>false</c> for that same
        /// tap, so it would fall through and fire whatever world object sat under
        /// the panel — the reported "one tap closes the panel AND opens the thing
        /// underneath" bug. The world-tap router blocks on
        /// <c>IsBlocking || ClosedThisFrame</c>, so the closing tap stays consumed
        /// for the rest of the frame regardless of Update() ordering, and the
        /// latch is always clear before the next frame's unrelated tap.
        /// </summary>
        public bool ClosedThisFrame { get; private set; }

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

            // #568: only latch when we actually removed a still-registered modal
            // — a no-op unregister of an unknown/already-removed token must not
            // block world taps for a frame in which no modal was open.
            if (openModals.Remove(token))
            {
                ClosedThisFrame = true;
            }
        }

        /// <summary>#568: clears the <see cref="ClosedThisFrame"/> latch at end
        /// of frame. Called from <c>InputRouter.LateUpdate</c>, which Unity runs
        /// only after every <c>Update()</c> (including the EventSystem's) has
        /// completed — so the latch can never be cleared before this frame's tap
        /// has been routed, but is always clear before the next frame's unrelated
        /// tap is checked. Leaves <see cref="IsBlocking"/> untouched: it still
        /// purely reflects the live open-token count.</summary>
        public void EndFrame()
        {
            ClosedThisFrame = false;
        }

        /// <summary>Releases every registration in one call, so the gate stops
        /// blocking. Use on a hard reset boundary — scene unload, or test
        /// isolation between EditMode tests that share the process-global
        /// <see cref="Shared"/> singleton — so a modal registration can never
        /// leak past that boundary and leave world taps dead for the next
        /// scene/test.</summary>
        public void Clear()
        {
            openModals.Clear();

            // #568: a hard reset also drops the this-frame close latch, so a
            // modal that closed just before the boundary can't leave world taps
            // dead for the next scene/test.
            ClosedThisFrame = false;
        }
    }
}
