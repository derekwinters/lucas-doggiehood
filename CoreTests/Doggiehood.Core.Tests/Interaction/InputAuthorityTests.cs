using System.Collections.Generic;
using Doggiehood.Core.Cameras;
using Doggiehood.Core.Interaction;
using NUnit.Framework;

namespace Doggiehood.Core.Tests.Interaction
{
    /// <summary>
    /// #670: the single input authority. Before this, "blocking" was a property
    /// individual consumers opted into — only <c>TapRouter.RouteTap</c> ever
    /// asked <see cref="ModalInputGate"/>, so camera pan/pinch/twist/scroll
    /// reached the camera straight from raw polling with no gate check at all
    /// (dragging a tuning-menu slider panned the map underneath it). These
    /// tests pin the three rules that replace that: one entry point, ownership
    /// latched at press-down, and strict-priority exclusive delivery.
    /// </summary>
    public class InputAuthorityTests
    {
        private ModalInputGate gate;
        private InputAuthority authority;

        [SetUp]
        public void CreateAuthority()
        {
            gate = new ModalInputGate();
            authority = new InputAuthority(gate);
        }

        /// <summary>A consumer that records everything delivered to it, so a
        /// test can assert both "got exactly this" and "got nothing".</summary>
        private sealed class RecordingConsumer : IInputConsumer
        {
            private readonly bool claims;

            public RecordingConsumer(InputTier tier, bool claims = true)
            {
                Tier = tier;
                this.claims = claims;
            }

            public InputTier Tier { get; }

            public List<(InputGesture Gesture, InputGesturePhase Phase)> Received { get; }
                = new List<(InputGesture, InputGesturePhase)>();

            public bool ClaimsGesture(InputGesture gesture) => claims;

            public void OnGesture(InputGesture gesture, InputGesturePhase phase)
                => Received.Add((gesture, phase));
        }

        private static InputGesture Press(int pointerId = 0, float x = 100f, float y = 100f)
            => InputGesture.Pan(pointerId, x, y);

        private static InputGesture Drag(int pointerId, float x, float y, float dx, float dy)
            => InputGesture.Pan(pointerId, x, y, dx, dy);

        [Test]
        public void OwnershipLatchesAtPressDown_AndSurvivesTheWholeGesture()
        {
            // R2: the owner of a press is resolved ONCE, at press-down, and
            // every move and the release go to it alone — even after the
            // pointer has travelled far outside whatever it started on. This is
            // the rule that fixes the tuning-menu slider: the press begins on
            // the slider, so the entire drag belongs to the slider and the
            // camera never sees it, with no drag-distance heuristic involved.
            var ui = new RecordingConsumer(InputTier.NonModalUi);
            var camera = new RecordingConsumer(InputTier.Camera);
            authority.Register(ui);
            authority.Register(camera);

            Assert.That(authority.Begin(Press()), Is.SameAs(ui));
            authority.Continue(Drag(0, 900f, 900f, 800f, 800f));
            authority.End(Drag(0, 900f, 900f, 0f, 0f));

            Assert.That(ui.Received.Count, Is.EqualTo(3),
                "the press, the move and the release all go to the press-down owner");
            Assert.That(ui.Received[0].Phase, Is.EqualTo(InputGesturePhase.Began));
            Assert.That(ui.Received[1].Phase, Is.EqualTo(InputGesturePhase.Changed));
            Assert.That(ui.Received[2].Phase, Is.EqualTo(InputGesturePhase.Ended));
            Assert.That(camera.Received, Is.Empty,
                "the camera never sees a gesture another consumer claimed at press-down");
        }

        [Test]
        public void PriorityResolvesModalOverNonModalOverWorldOverCamera_AndDeliveryIsExclusive()
        {
            // R3: at press-down the topmost interested tier wins and CONSUMES.
            // Exactly one consumer per gesture — never a fan-out.
            var nonModal = new RecordingConsumer(InputTier.NonModalUi);
            var world = new RecordingConsumer(InputTier.World);
            var camera = new RecordingConsumer(InputTier.Camera);

            // Registered deliberately out of priority order: the tier decides,
            // not the registration order.
            authority.Register(camera);
            authority.Register(world);
            authority.Register(nonModal);

            Assert.That(authority.Begin(Press()), Is.SameAs(nonModal));
            authority.End(Press());
            Assert.That(world.Received, Is.Empty);
            Assert.That(camera.Received, Is.Empty);

            authority.Unregister(nonModal);
            Assert.That(authority.Begin(Press()), Is.SameAs(world));
            authority.End(Press());
            Assert.That(camera.Received, Is.Empty);

            authority.Unregister(world);
            Assert.That(authority.Begin(Press()), Is.SameAs(camera));
        }

        [Test]
        public void ModalOpen_RefusesCameraPan()
        {
            // The reported symptom: drag with the tuning menu open used to pan
            // the map. One test per gesture kind so no single path can be
            // re-added unguarded.
            var camera = new RecordingConsumer(InputTier.Camera);
            authority.Register(camera);
            gate.Register(new object());

            Assert.That(authority.Begin(Press()), Is.Null);
            authority.Continue(Drag(0, 500f, 500f, 400f, 400f));
            authority.End(Drag(0, 500f, 500f, 0f, 0f));

            Assert.That(camera.Received, Is.Empty, "no pan reaches the camera while a modal is open");
        }

        [Test]
        public void ModalOpen_RefusesCameraPinch()
        {
            var camera = new RecordingConsumer(InputTier.Camera);
            authority.Register(camera);
            gate.Register(new object());

            Assert.That(authority.Begin(InputGesture.Pinch(0f)), Is.Null);
            authority.Continue(InputGesture.Pinch(40f));

            Assert.That(camera.Received, Is.Empty, "no pinch-zoom reaches the camera while a modal is open");
        }

        [Test]
        public void ModalOpen_RefusesCameraTwist()
        {
            var camera = new RecordingConsumer(InputTier.Camera);
            authority.Register(camera);
            gate.Register(new object());

            Assert.That(authority.Begin(InputGesture.Twist(0f)), Is.Null);
            authority.Continue(InputGesture.Twist(30f));

            Assert.That(camera.Received, Is.Empty, "no twist-rotate reaches the camera while a modal is open");
        }

        [Test]
        public void ModalOpen_RefusesCameraScroll()
        {
            var camera = new RecordingConsumer(InputTier.Camera);
            authority.Register(camera);
            gate.Register(new object());

            Assert.That(authority.Deliver(InputGesture.Scroll(3f)), Is.Null);

            Assert.That(camera.Received, Is.Empty, "no scroll-zoom reaches the camera while a modal is open");
        }

        [Test]
        public void ScrollReachesTheCamera_WhenNoModalIsOpen()
        {
            // The regression guard for the four "refused" tests above: with
            // nothing blocking, a discrete gesture still reaches the camera, so
            // those tests can't pass by breaking scroll outright.
            var camera = new RecordingConsumer(InputTier.Camera);
            authority.Register(camera);

            Assert.That(authority.Deliver(InputGesture.Scroll(3f)), Is.SameAs(camera));
            Assert.That(camera.Received.Count, Is.EqualTo(1));
        }

        [Test]
        public void AGestureInFlightWhenAModalOpens_KeepsItsOwner()
        {
            // Q1, resolved in triage: a modal opening does not retroactively
            // un-own a press that was legitimately claimed before it existed.
            // The modal blocks every gesture that STARTS after it opens.
            var camera = new RecordingConsumer(InputTier.Camera);
            authority.Register(camera);

            Assert.That(authority.Begin(Press()), Is.SameAs(camera));
            gate.Register(new object());

            Assert.That(authority.Continue(Drag(0, 200f, 200f, 100f, 100f)), Is.SameAs(camera));
            Assert.That(authority.End(Drag(0, 200f, 200f, 0f, 0f)), Is.SameAs(camera));
            Assert.That(camera.Received.Count, Is.EqualTo(3));

            // ...but the next gesture, which starts after the modal opened, is
            // refused.
            Assert.That(authority.Begin(Press()), Is.Null);
        }

        [Test]
        public void AGestureThatBeganBlocked_StaysBlockedEvenAfterTheModalCloses()
        {
            // The mirror of the rule above: ownership (including "nobody owns
            // this") is latched at press-down for the gesture's whole life, so
            // a modal closing mid-drag can't hand the rest of that drag to the
            // camera.
            var camera = new RecordingConsumer(InputTier.Camera);
            authority.Register(camera);
            var token = new object();
            gate.Register(token);

            Assert.That(authority.Begin(Press()), Is.Null);
            gate.Unregister(token);
            gate.EndFrame();

            Assert.That(authority.Continue(Drag(0, 300f, 300f, 200f, 200f)), Is.Null);
            Assert.That(authority.End(Drag(0, 300f, 300f, 0f, 0f)), Is.Null);
            Assert.That(camera.Received, Is.Empty);
        }

        [Test]
        public void TheSameFrameCloseLatchStillBlocks()
        {
            // #568 survives the rework: the tap that dismissed a modal this
            // frame must not also reach the world/camera, regardless of whether
            // the modal's Unregister ran before or after this gesture within the
            // frame. EndFrame() releases it for the next frame.
            var camera = new RecordingConsumer(InputTier.Camera);
            authority.Register(camera);
            var token = new object();
            gate.Register(token);
            gate.Unregister(token);

            Assert.That(gate.IsBlocking, Is.False, "the modal really is closed");
            Assert.That(authority.Begin(Press()), Is.Null,
                "but the closing frame's own gesture is still consumed (#568)");
            authority.End(Press());

            gate.EndFrame();
            Assert.That(authority.Begin(Press()), Is.SameAs(camera),
                "the next frame's genuinely new gesture routes normally");
        }

        [Test]
        public void AConsumerThatNeverRegisters_ReceivesNothing()
        {
            // R1/R4: blocked by default. Reaching input requires registering
            // with the authority — a new consumer is not silently live.
            var unregistered = new RecordingConsumer(InputTier.World);

            Assert.That(authority.Begin(Press()), Is.Null);
            authority.Continue(Drag(0, 400f, 400f, 300f, 300f));
            authority.End(Drag(0, 400f, 400f, 0f, 0f));

            Assert.That(unregistered.Received, Is.Empty);
            Assert.That(authority.Consumers, Is.Empty);
        }

        [Test]
        public void AConsumerThatDeclinesTheGesture_FallsThroughToTheTierBelow()
        {
            // Registering is necessary but not sufficient: a consumer only owns
            // gestures it actually claims (a hit-test), so a world object that
            // isn't under the pointer doesn't swallow the camera's pan.
            var world = new RecordingConsumer(InputTier.World, claims: false);
            var camera = new RecordingConsumer(InputTier.Camera);
            authority.Register(world);
            authority.Register(camera);

            Assert.That(authority.Begin(Press()), Is.SameAs(camera));
            Assert.That(world.Received, Is.Empty);
        }

        [Test]
        public void ContinueAndEndWithoutABegin_AreRefused()
        {
            // R2 again, from the other side: ownership is resolved at press-down
            // and only there. A move that arrives without a press has no owner
            // to inherit, so it reaches nobody rather than being re-resolved
            // against whatever now sits under the pointer.
            var camera = new RecordingConsumer(InputTier.Camera);
            authority.Register(camera);

            Assert.That(authority.Continue(Drag(0, 100f, 100f, 50f, 50f)), Is.Null);
            Assert.That(authority.End(Drag(0, 100f, 100f, 0f, 0f)), Is.Null);
            Assert.That(camera.Received, Is.Empty);
        }

        [Test]
        public void TwoPointersAreOwnedIndependently()
        {
            // Ownership latches per pointer, so a second finger landing on the
            // camera doesn't steal the first finger's UI gesture (or vice versa).
            var ui = new RecordingConsumer(InputTier.NonModalUi);
            authority.Register(ui);

            Assert.That(authority.Begin(Press(pointerId: 0)), Is.SameAs(ui));
            authority.Unregister(ui);
            Assert.That(authority.Begin(Press(pointerId: 1)), Is.Null,
                "the second pointer resolves against the registry as it is now");
            Assert.That(authority.Continue(Drag(0, 150f, 150f, 50f, 50f)), Is.SameAs(ui),
                "the first pointer keeps the owner it latched at press-down");
        }

        [Test]
        public void CancellingAGesture_EndsItWithoutLookingLikeARelease()
        {
            // A single-finger press that a second finger turns into a pinch is
            // over, but it was never released — delivering it as an Ended would
            // fire the tap the player never made. Cancel clears the latch and
            // says so explicitly.
            var camera = new RecordingConsumer(InputTier.Camera);
            authority.Register(camera);

            authority.Begin(Press());
            Assert.That(authority.Cancel(0), Is.SameAs(camera));

            Assert.That(camera.Received[1].Phase, Is.EqualTo(InputGesturePhase.Cancelled));
            Assert.That(authority.IsInFlight(0), Is.False, "a cancelled gesture is no longer in flight");
            Assert.That(authority.Continue(Drag(0, 200f, 200f, 50f, 50f)), Is.Null);
        }

        [Test]
        public void CancellingAGestureThatIsNotInFlight_IsANoOp()
        {
            var camera = new RecordingConsumer(InputTier.Camera);
            authority.Register(camera);

            Assert.That(authority.Cancel(7), Is.Null);
            Assert.That(camera.Received, Is.Empty);
        }

        [Test]
        public void TheReservedPointerIdsCannotCollideWithARealTouch()
        {
            // Platform touch fingerIds are non-negative, so the synthetic
            // pointers (mouse, two-finger, wheel) must all be negative and
            // distinct — otherwise a second finger could inherit the mouse's
            // ownership latch, or a pinch could be delivered to a pan's owner.
            var reserved = new[]
            {
                InputGesture.MousePointerId,
                InputGesture.TwoFingerPointerId,
                InputGesture.ScrollPointerId,
            };

            Assert.That(reserved, Is.Unique);
            Assert.That(reserved, Is.All.Negative);
        }

        [Test]
        public void RegisteringTheSameConsumerTwice_StillDeliversOnce()
        {
            // Exclusive delivery has to survive an idempotent re-registration
            // (scene rebuilds re-run wiring), or one gesture would fire the same
            // handler twice.
            var camera = new RecordingConsumer(InputTier.Camera);
            authority.Register(camera);
            authority.Register(camera);

            Assert.That(authority.Consumers.Count, Is.EqualTo(1));
            authority.Begin(Press());
            Assert.That(camera.Received.Count, Is.EqualTo(1));
        }
    }
}
