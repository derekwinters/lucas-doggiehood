using Doggiehood.Core.Cameras;
using Doggiehood.Core.Interaction;
using Doggiehood.Core.Tuning;
using Doggiehood.Unity;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Doggiehood.Unity.EditModeTests
{
    /// <summary>
    /// #670: the reported symptom and its siblings, at the Unity seam. Opening
    /// the debug tuning menu and dragging a slider moved the slider <em>and</em>
    /// panned the map underneath it, because "blocking" had only ever been built
    /// for taps — <see cref="CameraRig"/> polled input itself and drove pan,
    /// pinch, twist and scroll without ever consulting
    /// <see cref="ModalInputGate"/>.
    ///
    /// These tests drive the <see cref="InputAuthority"/> the way
    /// <see cref="InputRouter"/> does (the router's own translation of
    /// <c>UnityEngine.Input</c> can't be exercised headlessly) and assert on the
    /// live <see cref="CameraController"/>. There is one test per gesture kind
    /// on purpose: each was a separate ungated path, so each needs its own
    /// tripwire.
    /// </summary>
    public class InputAuthorityRoutingTests
    {
        private const string BundledFontPath = "Assets/UI/Fonts/Resources/DejaVuSans.ttf";

        private GameObject rigObject;
        private CameraRig rig;
        private object modalToken;

        [SetUp]
        public void CreateRig()
        {
            // Both registries are process-global; clear them so a rig or modal
            // leaked by an earlier test can't claim this fixture's gestures.
            ModalInputGate.Shared.Clear();
            InputAuthority.Shared.Clear();

            rigObject = new GameObject("rig-under-test", typeof(Camera));
            rig = rigObject.AddComponent<CameraRig>();
            rig.ApplyConfiguration();
            modalToken = new object();
        }

        [TearDown]
        public void DestroyRig()
        {
            Object.DestroyImmediate(rigObject);
            InputAuthority.Shared.Clear();
            ModalInputGate.Shared.Clear();
        }

        private static void Drag(float fromX, float fromY, float dx, float dy)
        {
            InputAuthority.Shared.Begin(InputGesture.Pan(0, fromX, fromY));
            InputAuthority.Shared.Continue(InputGesture.Pan(0, fromX + dx, fromY + dy, dx, dy));
            InputAuthority.Shared.End(InputGesture.Pan(0, fromX + dx, fromY + dy));
        }

        [Test]
        public void TuningMenuOpen_ADragDoesNotPanTheCamera()
        {
            // The reported case, with the real overlay: the tuning menu registers
            // itself modal on Open, so the drag that moves a slider must not also
            // reach the camera. (The slider's own UGUI drag can't be dispatched
            // headlessly; what this pins is the half that was broken — the map
            // moving underneath it.)
            AssetDatabase.ImportAsset(BundledFontPath, ImportAssetOptions.ForceSynchronousImport);
            var configAtStart = TuningConfig.Active;
            TuningConfig.Active = new TuningConfig();

            var canvasHost = new GameObject("ui-canvas", typeof(Canvas));
            canvasHost.AddComponent<UiCanvas>().Configure();
            var overlay = TuningMenuOverlay.Create(canvasHost.transform);
            try
            {
                overlay.Init();
                overlay.Open();
                Assert.That(ModalInputGate.Shared.IsBlocking, Is.True,
                    "the tuning menu registers itself as modal on open");

                var before = rig.Controller.Position;
                Drag(600f, 400f, 220f, 0f);

                Assert.That(rig.Controller.Position.X, Is.EqualTo(before.X).Within(0.0001f));
                Assert.That(rig.Controller.Position.Z, Is.EqualTo(before.Z).Within(0.0001f));
            }
            finally
            {
                Object.DestroyImmediate(canvasHost);
                TuningConfig.Active = configAtStart;
            }
        }

        [Test]
        public void ModalOpen_ADragOnTheScrimDoesNotPanTheCamera()
        {
            ModalInputGate.Shared.Register(modalToken);
            var before = rig.Controller.Position;

            Drag(100f, 100f, 500f, 500f);

            Assert.That(rig.Controller.Position.X, Is.EqualTo(before.X).Within(0.0001f));
            Assert.That(rig.Controller.Position.Z, Is.EqualTo(before.Z).Within(0.0001f));
        }

        [Test]
        public void ModalOpen_APinchDoesNotZoom()
        {
            ModalInputGate.Shared.Register(modalToken);
            var before = rig.Controller.Zoom;

            InputAuthority.Shared.Begin(InputGesture.Pinch(0f));
            InputAuthority.Shared.Continue(InputGesture.Pinch(200f));

            Assert.That(rig.Controller.Zoom, Is.EqualTo(before).Within(0.0001f));
        }

        [Test]
        public void ModalOpen_ATwistDoesNotRotate()
        {
            ModalInputGate.Shared.Register(modalToken);
            var before = rig.Controller.Yaw;

            InputAuthority.Shared.Begin(InputGesture.Twist(0f));
            InputAuthority.Shared.Continue(InputGesture.Twist(45f));

            Assert.That(rig.Controller.Yaw, Is.EqualTo(before).Within(0.0001f));
        }

        [Test]
        public void ModalOpen_AScrollDoesNotZoom()
        {
            ModalInputGate.Shared.Register(modalToken);
            var before = rig.Controller.Zoom;

            InputAuthority.Shared.Deliver(InputGesture.Scroll(150f));

            Assert.That(rig.Controller.Zoom, Is.EqualTo(before).Within(0.0001f));
        }

        [Test]
        public void NoModalOpen_PanPinchTwistAndScrollAllStillReachTheCamera()
        {
            // The regression guard that keeps the four tests above honest — and
            // the promise that this rework does not change how the camera feels
            // in normal play. Every gesture that used to work still works.
            var startPosition = rig.Controller.Position;
            Drag(100f, 100f, 400f, 0f);
            Assert.That(rig.Controller.Position.X, Is.Not.EqualTo(startPosition.X).Within(0.0001f),
                "a drag with nothing blocking still pans");

            var zoomBeforePinch = rig.Controller.Zoom;
            InputAuthority.Shared.Begin(InputGesture.Pinch(0f));
            InputAuthority.Shared.Continue(InputGesture.Pinch(200f));
            InputAuthority.Shared.End(InputGesture.Pinch(0f));
            Assert.That(rig.Controller.Zoom, Is.LessThan(zoomBeforePinch), "pinch apart still zooms in");

            var yawBeforeTwist = rig.Controller.Yaw;
            InputAuthority.Shared.Begin(InputGesture.Twist(0f));
            InputAuthority.Shared.Continue(InputGesture.Twist(45f));
            InputAuthority.Shared.End(InputGesture.Twist(0f));
            Assert.That(rig.Controller.Yaw, Is.Not.EqualTo(yawBeforeTwist).Within(0.0001f),
                "a twist still rotates");

            var zoomBeforeScroll = rig.Controller.Zoom;
            InputAuthority.Shared.Deliver(InputGesture.Scroll(150f));
            Assert.That(rig.Controller.Zoom, Is.Not.EqualTo(zoomBeforeScroll).Within(0.0001f),
                "the scroll wheel still zooms");
        }

        [Test]
        public void AGestureInFlightWhenAModalOpens_KeepsPanning()
        {
            // Q1 as resolved in triage, at the Unity seam: a modal opening does
            // not retroactively un-own a press claimed before it existed. The
            // modal blocks gestures that START after it opens.
            InputAuthority.Shared.Begin(InputGesture.Pan(0, 100f, 100f));
            ModalInputGate.Shared.Register(modalToken);

            var before = rig.Controller.Position;
            InputAuthority.Shared.Continue(InputGesture.Pan(0, 300f, 100f, 200f, 0f));

            Assert.That(rig.Controller.Position.X, Is.Not.EqualTo(before.X).Within(0.0001f),
                "the in-flight pan keeps its owner until it is released");
            InputAuthority.Shared.End(InputGesture.Pan(0, 300f, 100f));
        }

        [Test]
        public void TheRigBringsTheSingleInputRouterWithIt()
        {
            // R1: raw input has exactly one entry point, and it is present
            // wherever a camera rig is — without hand-editing scene
            // serialization to add a second component (CLAUDE.md rule #6).
            Assert.That(rigObject.GetComponent<InputRouter>(), Is.Not.Null);
        }

        [Test]
        public void TheRigIsAnOrdinaryRegisteredConsumer_AtTheLowestTier()
        {
            // R1/R4: the camera is offered a gesture only after every tier above
            // it declines, and it is in the enumerable registry like anything
            // else — not a privileged path around it.
            Assert.That(InputAuthority.Shared.Consumers, Is.Not.Empty);
            foreach (var consumer in InputAuthority.Shared.Consumers)
            {
                Assert.That(consumer.Tier, Is.EqualTo(InputTier.Camera));
            }
        }
    }
}
