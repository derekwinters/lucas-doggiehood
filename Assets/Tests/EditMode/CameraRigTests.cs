using Doggiehood.Core.Art;
using Doggiehood.Core.Cameras;
using Doggiehood.Core.Debugging;
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
        private bool showDebugAtStart;

        [SetUp]
        public void CreateRig()
        {
            // #611: the diagnostic debug-colour flag is a static; pin it off so
            // the backstop-colour assertions are deterministic regardless of test
            // order, then restore whatever it was in teardown.
            showDebugAtStart = WorldBuilder.ShowDebugElementColors;
            WorldBuilder.ShowDebugElementColors = false;

            // #670: the input authority and the modal gate are process-global.
            // Clear both so a rig or modal leaked by an earlier test can't claim
            // this fixture's gestures ahead of the rig under test.
            ModalInputGate.Shared.Clear();
            Doggiehood.Core.Interaction.InputAuthority.Shared.Clear();

            rigObject = new GameObject("rig-under-test", typeof(Camera));
            cam = rigObject.GetComponent<Camera>();
            rig = rigObject.AddComponent<CameraRig>();
            rig.ApplyConfiguration();
        }

        [TearDown]
        public void DestroyRig()
        {
            WorldBuilder.ShowDebugElementColors = showDebugAtStart;
            Object.DestroyImmediate(rigObject);
            Doggiehood.Core.Interaction.InputAuthority.Shared.Clear();
            ModalInputGate.Shared.Clear();
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
        public void ClearsToGrass_AsTheVoidBackstop()
        {
            // #558: the main game camera clears to a solid grass-green colour
            // (the same #7ED957 the ground plane is painted), so any area beyond
            // the mesh edge at extreme pan+zoom-out reads as continuous grass
            // rather than the blue void seam — a pixel-level backstop that can't
            // under-cover. Mirrors PortraitCamera's SolidColor pattern.
            Assert.That(cam.clearFlags, Is.EqualTo(CameraClearFlags.SolidColor));
            Assert.That(cam.backgroundColor, Is.EqualTo(CoreColors.FromHex(Palette.GrassHex)));
        }

        [Test]
        public void ClearsToTheDebugBackstopColor_WhenDebugElementColorsOn()
        {
            // #611: when the diagnostic toggle is on, the camera clears to the
            // loud, obviously-fake debug backstop colour instead of grass — so the
            // area beyond the mesh edge (the bottom "border") is visually distinct
            // from the ground plane, revealing which element it actually is. Still
            // a SolidColor clear; only the colour changes.
            WorldBuilder.ShowDebugElementColors = true;
            rig.ApplyConfiguration();

            Assert.That(cam.clearFlags, Is.EqualTo(CameraClearFlags.SolidColor));
            Assert.That(cam.backgroundColor,
                Is.EqualTo(CoreColors.FromHex(DebugElementColors.BackstopHex(true))));
            Assert.That(cam.backgroundColor,
                Is.Not.EqualTo(CoreColors.FromHex(Palette.GrassHex)),
                "the backstop is no longer the matched grass green");
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
        public void TwoFingerTwist_PerFrameAngleDelta_ReachesTheCameraThroughTheAuthority()
        {
            // #203: two-finger twist detection mirrors the lastPinchDistance
            // pattern - the first sample only records the baseline angle, and
            // the next sample forwards the per-frame angle delta. Fingers here
            // rotate counter-clockwise (angle 0deg -> 45deg); the scene follows
            // the fingers, so the camera turns the opposite way (yaw increases)
            // - see GestureMapper.TwistToRotation.
            //
            // #670: the sampling moved to InputRouter (the single raw-input
            // entry point) and reaches the rig only via InputAuthority, so this
            // now drives the router and asserts the rig still turns.
            var router = rigObject.GetComponent<InputRouter>();
            Assert.That(router, Is.Not.Null, "the rig brings the single input router with it");

            var start = new Vector2(0f, 0f);
            router.SampleTwoFingerGesture(start, new Vector2(100f, 0f), true);
            var yawAfterBaseline = rig.Controller.Yaw;

            router.SampleTwoFingerGesture(start, new Vector2(100f, 100f), true);

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
        public void CameraPlacementAndBothClipPlanes_AreDerivedFromTheLiveZoom()
        {
            // #679: the rig's depth placement is no longer pinned to a constant,
            // and the clip planes are no longer whatever the Main scene happens
            // to have serialized (0.3 / 300) — the rig writes both itself, from
            // the live zoom, so the "the whole viewport renders world" guarantee
            // is owned by code and travels with every scene.
            foreach (var targetZoom in new[] { CameraController.MinZoom, CameraController.DefaultZoom, rig.Controller.MaxZoom })
            {
                rig.Controller.ZoomBy(targetZoom - rig.Controller.Zoom);
                rig.ApplyConfiguration();

                var focus = new Vector3(rig.Controller.Position.X, 0f, rig.Controller.Position.Z);
                var expected = focus
                    - rigObject.transform.forward * CameraRigConfig.RigDistanceFor(rig.Controller.Zoom);

                Assert.That(rigObject.transform.position.x, Is.EqualTo(expected.x).Within(0.01f));
                Assert.That(rigObject.transform.position.y, Is.EqualTo(expected.y).Within(0.01f));
                Assert.That(rigObject.transform.position.z, Is.EqualTo(expected.z).Within(0.01f));
                Assert.That(cam.nearClipPlane,
                    Is.EqualTo(CameraRigConfig.NearClipPlane).Within(0.0001f),
                    $"the near clip plane is not code-owned at zoom {rig.Controller.Zoom}");
                Assert.That(cam.farClipPlane,
                    Is.EqualTo(CameraRigConfig.FarClipFor(rig.Controller.Zoom)).Within(0.01f),
                    $"the far clip plane is not code-owned at zoom {rig.Controller.Zoom}");
            }
        }

        [Test]
        public void AtMaxZoomOutOnAGrownMap_TheGroundAtEveryFrameEdge_LiesBetweenTheClipPlanes()
        {
            // #679, the actual artifact: at max zoom-out on an expanded map the
            // near half of the ground fell *behind* the camera and was clipped
            // away, leaving a blank band along the bottom of the screen that
            // grew with the zoom. Asserted here on real Unity clipping state —
            // WorldToViewportPoint against the camera's own clip planes — rather
            // than on the Core formula, which is pinned separately.
            var map = new Doggiehood.Core.World.TileMap(
                new Doggiehood.Core.World.TileCoordinate(0, 0), Doggiehood.Core.World.TileType.FourWay);
            for (int north = 1; north <= 4; north++)
            {
                map.Place(new Doggiehood.Core.World.TileCoordinate(0, north),
                    Doggiehood.Core.World.TileType.FourWay);
            }

            rig.Controller.RecomputeBoundsFromMap(map);
            rig.Controller.ZoomBy(rig.Controller.MaxZoom - rig.Controller.Zoom);
            rig.ApplyConfiguration();

            var texture = new RenderTexture(1920, 1080, 0);
            cam.targetTexture = texture;
            try
            {
                float zoom = rig.Controller.Zoom;
                Assert.That(zoom, Is.GreaterThan(CameraRigConfig.RigDistance),
                    "a five-tile map must zoom out past the old fixed set-back, or this proves nothing");

                // The ground points that land on the bottom and top edges of the
                // frame: zoom / sin(pitch) metres either side of the focus point,
                // measured across the ground along the camera's heading.
                var focus = new Vector3(rig.Controller.Position.X, 0f, rig.Controller.Position.Z);
                var groundHeading = Vector3.ProjectOnPlane(cam.transform.forward, Vector3.up).normalized;
                float groundReach = zoom / Mathf.Sin(CameraRigConfig.PitchDegrees * Mathf.Deg2Rad);

                var nearestVisibleGround = cam.WorldToViewportPoint(focus - groundHeading * groundReach);
                var farthestVisibleGround = cam.WorldToViewportPoint(focus + groundHeading * groundReach);

                Assert.That(nearestVisibleGround.y, Is.EqualTo(0f).Within(0.01f),
                    "this point should sit on the bottom edge of the frame");
                Assert.That(nearestVisibleGround.z, Is.GreaterThan(cam.nearClipPlane),
                    "the ground along the bottom of the screen is behind the near clip plane — the #679 band");
                Assert.That(nearestVisibleGround.z, Is.EqualTo(CameraRigConfig.RigDistance).Within(0.01f),
                    "the nearest visible ground should sit the constant clearance in front of the lens");

                Assert.That(farthestVisibleGround.y, Is.EqualTo(1f).Within(0.01f),
                    "this point should sit on the top edge of the frame");
                Assert.That(farthestVisibleGround.z, Is.LessThan(cam.farClipPlane),
                    "the ground along the top of the screen is past the far clip plane");
            }
            finally
            {
                cam.targetTexture = null;
                Object.DestroyImmediate(texture);
            }
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

                foreach (var targetZoom in new[] { CameraController.MinZoom, CameraController.DefaultZoom, rig.Controller.MaxZoom })
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
