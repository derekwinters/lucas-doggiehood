using System.Collections.Generic;
using Doggiehood.Core.Cameras;

namespace Doggiehood.Core.Interaction
{
    /// <summary>
    /// #670: the one place input is arbitrated. Before this, "an open modal
    /// blocks input" was a property each consumer opted into, and only one ever
    /// did — <c>TapRouter.RouteTap</c>, reached solely from a sub-12px tap
    /// release. Camera pan, pinch, twist and scroll each ran straight from raw
    /// polling to the camera with no gate check at all, which is why dragging a
    /// slider in the debug tuning menu panned the map underneath it. Fixing the
    /// slider alone would have left the shape of the defect intact, so the rule
    /// moved here instead:
    ///
    /// <list type="number">
    /// <item><description><b>R1 — single entry point.</b> All raw input enters
    /// through one router (the Unity <c>InputRouter</c>) and is handed to this
    /// authority. No component polls input or acts on a gesture on its own; the
    /// camera is an ordinary registered consumer, not a privileged path.</description></item>
    /// <item><description><b>R2 — ownership latches at press-down.</b> The owner
    /// of a gesture is resolved once, at <see cref="Begin"/>, and every
    /// subsequent <see cref="Continue"/> and the <see cref="End"/> go to that
    /// owner alone — even if the pointer leaves its bounds, even if what it
    /// started on has since despawned or animated away.</description></item>
    /// <item><description><b>R3 — strict priority, exclusive delivery.</b> At
    /// press-down the topmost interested <see cref="InputTier"/> wins and
    /// consumes: modal UI &gt; non-modal UI &gt; world &gt; camera, exactly one
    /// consumer per gesture, never a fan-out.</description></item>
    /// <item><description><b>R4 — the registry is enumerable</b>
    /// (<see cref="Consumers"/>) and a guard test enforces that nothing reaches
    /// input around it. See <c>docs/engineering/input-authority.md</c>.</description></item>
    /// </list>
    ///
    /// The modal tier is not a registration: it is the shared
    /// <see cref="ModalInputGate"/> every overlay already registers with on
    /// open, consulted here at press-down. That keeps #544's deterministic,
    /// frame-timing-independent flag and #568's same-frame close latch working
    /// unchanged — but now for every gesture kind, not just taps.
    /// </summary>
    public sealed class InputAuthority
    {
        /// <summary>The one authority the Unity router and every production
        /// consumer converge on, so priority is global rather than per-scene.</summary>
        public static InputAuthority Shared { get; } = new InputAuthority(ModalInputGate.Shared);

        private readonly ModalInputGate modalGate;
        private readonly List<IInputConsumer> consumers = new List<IInputConsumer>();

        // Ownership latch (R2), keyed by pointer id. A tracked entry with a
        // null value means "this gesture was claimed by the modal tier, or by
        // nobody" — either way it is settled for the gesture's whole life, so
        // Continue/End can never re-resolve it against a registry that has
        // since changed.
        private readonly Dictionary<int, IInputConsumer> owners = new Dictionary<int, IInputConsumer>();

        public InputAuthority(ModalInputGate modalGate)
        {
            this.modalGate = modalGate;
        }

        /// <summary>R4: every consumer currently able to receive input. Public
        /// so the registry is inspectable rather than implicit.</summary>
        public IReadOnlyList<IInputConsumer> Consumers => consumers;

        /// <summary>Adds <paramref name="consumer"/> to the registry.
        /// Idempotent — re-running scene wiring must not double-deliver.</summary>
        public void Register(IInputConsumer consumer)
        {
            if (consumer == null || consumers.Contains(consumer))
            {
                return;
            }

            consumers.Add(consumer);
        }

        /// <summary>Removes <paramref name="consumer"/>. Gestures it already
        /// owns keep their owner until they end (R2).</summary>
        public void Unregister(IInputConsumer consumer)
        {
            if (consumer == null)
            {
                return;
            }

            consumers.Remove(consumer);
        }

        /// <summary>Drops every registration and every in-flight gesture. Use on
        /// a hard reset boundary — scene unload, or isolation between tests that
        /// share <see cref="Shared"/>.</summary>
        public void Clear()
        {
            consumers.Clear();
            owners.Clear();
        }

        /// <summary>True while <paramref name="pointerId"/> has a gesture in
        /// flight (owned or blocked).</summary>
        public bool IsInFlight(int pointerId) => owners.ContainsKey(pointerId);

        /// <summary>Press-down: resolves the gesture's owner for its entire
        /// lifetime (R2/R3) and delivers <see cref="InputGesturePhase.Began"/>.
        /// Returns the owner, or null when the modal tier consumed the gesture
        /// or nobody claimed it.</summary>
        public IInputConsumer Begin(InputGesture gesture)
        {
            var owner = Resolve(gesture);
            owners[gesture.PointerId] = owner;

            if (owner != null)
            {
                owner.OnGesture(gesture, InputGesturePhase.Began);
            }

            return owner;
        }

        /// <summary>A move/change within a gesture already in flight. Delivered
        /// to the press-down owner alone; refused outright when no owner was
        /// latched, so a stray move can never be re-resolved against whatever
        /// now happens to sit under the pointer.</summary>
        public IInputConsumer Continue(InputGesture gesture)
            => DeliverToOwner(gesture, InputGesturePhase.Changed, endsGesture: false);

        /// <summary>Release: delivers <see cref="InputGesturePhase.Ended"/> to
        /// the press-down owner and clears the latch.</summary>
        public IInputConsumer End(InputGesture gesture)
            => DeliverToOwner(gesture, InputGesturePhase.Ended, endsGesture: true);

        /// <summary>Ends an in-flight gesture that was never released — a
        /// second finger turning a pan into a pinch, or a teardown mid-press.
        /// Clears the latch and tells the owner explicitly, so it doesn't fire
        /// the release behaviour (a tap) for a gesture the player abandoned.
        /// A no-op for a pointer with nothing in flight.</summary>
        public IInputConsumer Cancel(int pointerId)
        {
            if (!owners.TryGetValue(pointerId, out var owner))
            {
                return null;
            }

            owners.Remove(pointerId);
            owner?.OnGesture(
                InputGesture.Pan(pointerId, 0f, 0f), InputGesturePhase.Cancelled);
            return owner;
        }

        /// <summary>A discrete gesture with no press/release lifecycle (the
        /// mouse wheel): resolved and delivered in one shot, latching nothing.
        /// It still passes the same tier resolution, so scroll-to-zoom is
        /// refused while a modal is open just like every other gesture.</summary>
        public IInputConsumer Deliver(InputGesture gesture)
        {
            var owner = Resolve(gesture);
            if (owner != null)
            {
                owner.OnGesture(gesture, InputGesturePhase.Changed);
            }

            return owner;
        }

        private IInputConsumer DeliverToOwner(InputGesture gesture, InputGesturePhase phase, bool endsGesture)
        {
            if (!owners.TryGetValue(gesture.PointerId, out var owner))
            {
                return null;
            }

            if (endsGesture)
            {
                owners.Remove(gesture.PointerId);
            }

            owner?.OnGesture(gesture, phase);
            return owner;
        }

        private IInputConsumer Resolve(InputGesture gesture)
        {
            // Tier 1. The modal tier consumes unconditionally — an open dialog
            // (or one that closed earlier in this same frame, #568) blocks every
            // tier below it, for every gesture kind.
            if (modalGate != null && (modalGate.IsBlocking || modalGate.ClosedThisFrame))
            {
                return null;
            }

            // Tiers 2-4, in strict priority order. Registration order only
            // breaks ties within a tier, so wiring order can't quietly change
            // which tier wins.
            for (var tier = InputTier.NonModalUi; tier <= InputTier.Camera; tier++)
            {
                foreach (var consumer in consumers)
                {
                    if (consumer.Tier == tier && consumer.ClaimsGesture(gesture))
                    {
                        return consumer;
                    }
                }
            }

            return null;
        }
    }
}
