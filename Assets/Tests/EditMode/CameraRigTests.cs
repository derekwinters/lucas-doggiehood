using Doggiehood.Core.Cameras;
using Doggiehood.Unity;
using NUnit.Framework;
using UnityEngine;

namespace Doggiehood.Unity.EditModeTests
{
    public class CameraRigTests
    {
        private GameObject rigObject;
        private CameraRig rig;
        private Camera cam;

        [SetUp]
        public void CreateRig()
        {
            rigObject = new GameObject("rig-under-test", typeof(Camera));
            cam = rigObject.GetComponent<Camera>();
            rig = rigObject.AddComponent<CameraRig>();
            rig.ApplyConfiguration();
        }

        [TearDown]
        public void DestroyRig()
        {
            Object.DestroyImmediate(rigObject);
        }

        [Test]
        public void AppliesTheFixedPitchAndProjection_AndTheControllerYaw()
        {
            // #21/#203: pitch and projection stay the fixed documented
            // constants; yaw now reflects the mutable CameraController.Yaw.
            var euler = rigObject.transform.rotation.eulerAngles;

            Assert.That(euler.x, Is.EqualTo(CameraRigConfig.PitchDegrees).Within(0.01f));
            Assert.That(euler.y, Is.EqualTo(rig.Controller.Yaw).Within(0.01f));
            Assert.That(cam.orthographic, Is.EqualTo(CameraRigConfig.Orthographic));
            Assert.That(cam.orthographicSize, Is.EqualTo(rig.Controller.Zoom).Within(0.001f));
        }

        [Test]
        public void DragGesture_MapsToControllerPan()
        {
            // #20/#203: a drag pans via GestureMapper -> CameraController,
            // projected at the live camera yaw.
            var expected = GestureMapper.DragToPan(100f, 0f, rig.Controller.Yaw, rig.Controller.Zoom, 1000f);

            rig.HandleDrag(100f, 0f, 1000f);

            Assert.That(rig.Controller.Position.X, Is.EqualTo(expected.X).Within(0.0001f));
            Assert.That(rig.Controller.Position.Z, Is.EqualTo(expected.Z).Within(0.0001f));
        }

        [Test]
        public void TwistGesture_MapsToControllerRotation_AndTheCamera()
        {
            // #203: a twist rotates via GestureMapper -> CameraController and
            // the resulting yaw shows up on the transform's Y euler.
            var before = rig.Controller.Yaw;
            const float twistDelta = 20f;

            rig.HandleTwist(twistDelta);

            Assert.That(rig.Controller.Yaw,
                Is.EqualTo(before + GestureMapper.TwistToRotation(twistDelta)).Within(0.0001f));
            Assert.That(rigObject.transform.rotation.eulerAngles.y,
                Is.EqualTo(rig.Controller.Yaw).Within(0.01f));
        }

        [Test]
        public void TwoFingerTwist_PerFrameAngleDelta_ForwardsToHandleTwist()
        {
            // #203: two-finger twist detection mirrors the lastPinchDistance
            // pattern - the first sample only records the baseline angle, and
            // the next sample forwards the per-frame angle delta to HandleTwist.
            // Fingers here rotate counter-clockwise (angle 0deg -> 45deg); the
            // scene follows the fingers, so the camera turns the opposite way
            // (yaw increases) - see GestureMapper.TwistToRotation.
            var start = new Vector2(0f, 0f);
            rig.ProcessTwoFingerSample(start, new Vector2(100f, 0f), true, 1000f);
            var yawAfterBaseline = rig.Controller.Yaw;

            rig.ProcessTwoFingerSample(start, new Vector2(100f, 100f), true, 1000f);

            Assert.That(rig.Controller.Yaw, Is.GreaterThan(yawAfterBaseline),
                "a counter-clockwise finger twist turns the scene counter-clockwise (camera yaw increases)");
            Assert.That(rig.Controller.Yaw, Is.EqualTo(yawAfterBaseline + 45f).Within(0.01f),
                "a 45deg finger twist maps 1:1 to a 45deg rotation");
        }

        [Test]
        public void PinchGesture_MapsToControllerZoom_AndTheCamera()
        {
            var before = rig.Controller.Zoom;

            rig.HandlePinch(100f, 1000f);

            Assert.That(rig.Controller.Zoom, Is.LessThan(before), "pinch apart should zoom in");
            Assert.That(cam.orthographicSize, Is.EqualTo(rig.Controller.Zoom).Within(0.001f));
        }

        [Test]
        public void HugeDrag_StaysClampedInsideWorldBounds()
        {
            rig.HandleDrag(-1000000f, -1000000f, 1000f);

            Assert.That(rig.Controller.Position.X, Is.InRange(rig.Controller.Bounds.MinX, rig.Controller.Bounds.MaxX));
            Assert.That(rig.Controller.Position.Z, Is.InRange(rig.Controller.Bounds.MinZ, rig.Controller.Bounds.MaxZ));
        }

        [Test]
        public void TapOnAHouse_ReachesItsInteractionHandler_AtAnyZoom()
        {
            // #20: tap hit-testing works across zoom levels. A RenderTexture
            // gives the camera a real pixel rect under headless CI.
            var world = WorldBuilder.Build(Doggiehood.Core.World.GameState.CreateNew());
            var texture = new RenderTexture(1920, 1080, 0);
            cam.targetTexture = texture;
            try
            {
                var house = world.GetComponentsInChildren<HouseView>()[0];
                Physics.SyncTransforms();

                foreach (var targetZoom in new[] { CameraController.MinZoom, CameraController.DefaultZoom, CameraController.MaxZoom })
                {
                    rig.Controller.ZoomBy(targetZoom - rig.Controller.Zoom);
                    rig.ApplyConfiguration();

                    var screenPoint = cam.WorldToScreenPoint(house.transform.position + Vector3.up * 1f);
                    var before = house.TapCount;

                    rig.HandleTap(screenPoint);

                    Assert.That(house.TapCount, Is.EqualTo(before + 1),
                        $"tap missed the house at zoom {rig.Controller.Zoom}");
                }
            }
            finally
            {
                cam.targetTexture = null;
                Object.DestroyImmediate(texture);
                Object.DestroyImmediate(world);
            }
        }
    }
}
