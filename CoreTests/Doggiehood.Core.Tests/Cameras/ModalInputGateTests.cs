using Doggiehood.Core.Cameras;
using NUnit.Framework;

namespace Doggiehood.Core.Tests.Cameras
{
    /// <summary>
    /// #544: a deterministic, engine-free "is a center-anchored modal open?"
    /// registry. Center-anchored overlays (dog/house profile, confirmation
    /// dialog, welcome pop-up, onboarding reward) register on open and
    /// unregister on close; the world-tap router consults <see cref="ModalInputGate.IsBlocking"/>
    /// to swallow taps that land on a modal (panel or scrim) instead of leaking
    /// them to the world objects behind it. Unlike the #422 EventSystem guard,
    /// this is a synchronous boolean flip with no dependency on input-module
    /// frame timing, so it is fully unit-testable and reliable at touch release.
    /// </summary>
    public class ModalInputGateTests
    {
        [Test]
        public void NewGate_IsNotBlocking()
        {
            var gate = new ModalInputGate();

            Assert.That(gate.IsBlocking, Is.False,
                "a gate with nothing registered must not block world taps");
        }

        [Test]
        public void Register_MakesTheGateBlock()
        {
            var gate = new ModalInputGate();

            gate.Register(new object());

            Assert.That(gate.IsBlocking, Is.True,
                "a registered modal must block world taps");
        }

        [Test]
        public void Unregister_ReleasesTheBlock()
        {
            var gate = new ModalInputGate();
            var token = new object();
            gate.Register(token);

            gate.Unregister(token);

            Assert.That(gate.IsBlocking, Is.False,
                "once the only registered modal is unregistered, the gate must stop blocking");
        }

        [Test]
        public void IsBlocking_StaysTrue_UntilEveryTokenIsUnregistered()
        {
            // Two modals can't stack today, but the registry must not falsely
            // unblock if that ever changes.
            var gate = new ModalInputGate();
            var first = new object();
            var second = new object();
            gate.Register(first);
            gate.Register(second);

            gate.Unregister(first);
            Assert.That(gate.IsBlocking, Is.True,
                "with one modal still registered the gate must keep blocking");

            gate.Unregister(second);
            Assert.That(gate.IsBlocking, Is.False,
                "only after every token is unregistered does the gate release");
        }

        [Test]
        public void Register_IsIdempotent_PerToken()
        {
            // The same modal registering twice (e.g. a re-Open without a Close)
            // is balanced by a single Unregister — it must not leave the gate
            // stuck blocking.
            var gate = new ModalInputGate();
            var token = new object();

            gate.Register(token);
            gate.Register(token);
            gate.Unregister(token);

            Assert.That(gate.IsBlocking, Is.False,
                "re-registering the same token must not require a matching extra unregister");
        }

        [Test]
        public void Unregister_UnknownToken_IsANoOp()
        {
            var gate = new ModalInputGate();

            Assert.DoesNotThrow(() => gate.Unregister(new object()),
                "unregistering a token that was never registered must be a no-op, not an error");
            Assert.That(gate.IsBlocking, Is.False);
        }

        [Test]
        public void Unregister_NullToken_IsANoOp()
        {
            var gate = new ModalInputGate();

            Assert.DoesNotThrow(() => gate.Unregister(null));
            Assert.That(gate.IsBlocking, Is.False);
        }

        [Test]
        public void Register_NullToken_IsANoOp()
        {
            var gate = new ModalInputGate();

            gate.Register(null);

            Assert.That(gate.IsBlocking, Is.False,
                "a null token is not a real modal and must not block");
        }

        [Test]
        public void Clear_ReleasesEveryRegistration_InOneCall()
        {
            // #544 follow-up: a deterministic reset for the process-global
            // Shared gate. On scene unload (and in test isolation) any modal
            // that was open must be released in one call so the gate can never
            // be left stuck blocking world taps for the next scene/test.
            var gate = new ModalInputGate();
            gate.Register(new object());
            gate.Register(new object());

            gate.Clear();

            Assert.That(gate.IsBlocking, Is.False,
                "Clear releases every registration so the gate stops blocking");
        }

        [Test]
        public void Clear_OnAnEmptyGate_IsANoOp()
        {
            var gate = new ModalInputGate();

            Assert.DoesNotThrow(() => gate.Clear());
            Assert.That(gate.IsBlocking, Is.False);
        }

        [Test]
        public void NewGate_ClosedThisFrame_IsFalse()
        {
            var gate = new ModalInputGate();

            Assert.That(gate.ClosedThisFrame, Is.False,
                "a fresh gate has closed nothing this frame");
        }

        [Test]
        public void Unregister_OfARegisteredToken_LatchesClosedThisFrame()
        {
            // #568: the same tap that dismisses a modal must not leak to the
            // world. Unregistering a still-open modal latches ClosedThisFrame so
            // the world-tap guard keeps blocking for the rest of this frame,
            // regardless of Update() ordering between the EventSystem and the
            // camera rig.
            var gate = new ModalInputGate();
            var token = new object();
            gate.Register(token);

            gate.Unregister(token);

            Assert.That(gate.ClosedThisFrame, Is.True,
                "unregistering a still-registered modal latches that a modal closed this frame");
        }

        [Test]
        public void Unregister_OfAnUnknownToken_DoesNotLatchClosedThisFrame()
        {
            var gate = new ModalInputGate();

            gate.Unregister(new object());

            Assert.That(gate.ClosedThisFrame, Is.False,
                "a no-op unregister of a token that was never registered must not latch the close");
        }

        [Test]
        public void Unregister_OfAnAlreadyRemovedToken_DoesNotLatchClosedThisFrame()
        {
            var gate = new ModalInputGate();
            var token = new object();
            gate.Register(token);
            gate.Unregister(token);
            gate.EndFrame(); // clear the latch from the real close above

            gate.Unregister(token); // second unregister removes nothing

            Assert.That(gate.ClosedThisFrame, Is.False,
                "re-unregistering an already-removed token removes nothing, so it must not re-latch the close");
        }

        [Test]
        public void Unregister_OfANullToken_DoesNotLatchClosedThisFrame()
        {
            var gate = new ModalInputGate();

            gate.Unregister(null);

            Assert.That(gate.ClosedThisFrame, Is.False,
                "a null token is not a real modal, so unregistering it must not latch a close");
        }

        [Test]
        public void EndFrame_ClearsClosedThisFrame_AndLeavesIsBlockingUntouched()
        {
            var gate = new ModalInputGate();
            var closing = new object();
            var stillOpen = new object();
            gate.Register(closing);
            gate.Register(stillOpen);
            gate.Unregister(closing);

            Assert.That(gate.ClosedThisFrame, Is.True);

            gate.EndFrame();

            Assert.That(gate.ClosedThisFrame, Is.False,
                "EndFrame clears the this-frame close latch");
            Assert.That(gate.IsBlocking, Is.True,
                "EndFrame must not touch IsBlocking — a modal still registered open keeps blocking");
        }

        [Test]
        public void EndFrame_DoesNotAffectIsBlocking_OnAnEmptyGate()
        {
            var gate = new ModalInputGate();

            gate.EndFrame();

            Assert.That(gate.IsBlocking, Is.False,
                "IsBlocking still purely reflects the open-token count after EndFrame");
        }

        [Test]
        public void CloseDuringAFrame_KeepsBlockingTheTap_UntilEndFrame()
        {
            // #568 regression, reproduced deterministically: a modal unregistered
            // earlier in the same frame (during the EventSystem's UI dispatch)
            // must still block this frame's world tap. The combined signal the
            // tap router reads (IsBlocking || ClosedThisFrame) stays true until
            // EndFrame() runs at end of frame (CameraRig.LateUpdate).
            var gate = new ModalInputGate();
            var token = new object();
            gate.Register(token);

            gate.Unregister(token);

            Assert.That(gate.IsBlocking, Is.False,
                "the live open-token count is zero once the modal unregistered");
            Assert.That(gate.IsBlocking || gate.ClosedThisFrame, Is.True,
                "but the combined tap-blocking signal stays true this frame, so the closing tap can't leak to the world");

            gate.EndFrame();

            Assert.That(gate.IsBlocking || gate.ClosedThisFrame, Is.False,
                "after end of frame the latch clears, so a genuinely new tap next frame routes to the world");
        }

        [Test]
        public void Shared_IsAStableSingleton()
        {
            Assert.That(ModalInputGate.Shared, Is.Not.Null);
            Assert.That(ModalInputGate.Shared, Is.SameAs(ModalInputGate.Shared),
                "the shared gate must be one stable instance so panels and the tap router converge on it");
        }
    }
}
