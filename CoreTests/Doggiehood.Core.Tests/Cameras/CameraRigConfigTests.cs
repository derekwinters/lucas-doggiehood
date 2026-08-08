using System;
using System.Linq;
using System.Reflection;
using Doggiehood.Core.Cameras;
using Doggiehood.Core.World;
using NUnit.Framework;

namespace Doggiehood.Core.Tests.Cameras
{
    public class CameraRigConfigTests
    {
        [Test]
        public void DocumentedFixedCameraConstants()
        {
            // #21/#203: pitch and projection stay fixed. Yaw is no longer a
            // config constant here - it is mutable state on CameraController.
            Assert.That(CameraRigConfig.PitchDegrees, Is.EqualTo(45f));
            Assert.That(CameraRigConfig.Orthographic, Is.True);
            Assert.That(CameraRigConfig.RigDistance, Is.GreaterThan(0f));
        }

        [Test]
        public void PitchProjectionAndDistance_StayFixedImmutableConsts()
        {
            // Guard (#203): pitch, orthographic projection and rig distance
            // remain compile-time constants with no writable code path. Only
            // yaw became mutable, and it moved off this type entirely - there
            // must be no YawDegrees constant left to accidentally rely on.
            var type = typeof(CameraRigConfig);

            Assert.That(type.IsAbstract && type.IsSealed, Is.True, "CameraRigConfig must be a static class");

            var constFields = type.GetFields(BindingFlags.Public | BindingFlags.Static)
                .Where(f => f.IsLiteral)
                .Select(f => f.Name)
                .ToList();
            Assert.That(constFields, Does.Contain(nameof(CameraRigConfig.PitchDegrees)));
            Assert.That(constFields, Does.Contain(nameof(CameraRigConfig.Orthographic)));
            Assert.That(constFields, Does.Contain(nameof(CameraRigConfig.RigDistance)));
            Assert.That(constFields, Does.Not.Contain("YawDegrees"),
                "Yaw is no longer a fixed config constant (#203)");

            var writableFields = type.GetFields(BindingFlags.Public | BindingFlags.Static)
                .Where(f => !f.IsLiteral && !f.IsInitOnly)
                .Select(f => f.Name)
                .ToList();
            Assert.That(writableFields, Is.Empty, "No mutable fields allowed");

            var settableProperties = type.GetProperties(BindingFlags.Public | BindingFlags.Static)
                .Where(p => p.CanWrite)
                .Select(p => p.Name)
                .ToList();
            Assert.That(settableProperties, Is.Empty, "No settable properties allowed");
        }

        [Test]
        public void Rotation_IsNowPossible_ViaCameraController()
        {
            // Companion to the guard above (#203): the deliberate immovability
            // of #21 is reopened for yaw - the camera CAN now rotate freely.
            var controller = CameraController.ForStartingNeighborhood();
            var before = controller.Yaw;

            controller.Rotate(37f);

            Assert.That(controller.Yaw, Is.EqualTo(before + 37f));
        }

        // #679: the rig's depth placement is derived from the live zoom, never
        // pinned. The tests below pin that derivation and the two clipping
        // invariants it exists to guarantee.

        [TestCase(6f)]
        [TestCase(18f)]
        [TestCase(84f)]
        [TestCase(300f)]
        public void GroundDepthReach_IsTheVisibleGroundSlabHalfThickness(float zoom)
        {
            // The ground is tilted away from the view axis, so the viewport does
            // not cover a constant-depth slice of it: a point at the top/bottom
            // edge of the frame lies Zoom / sin(pitch) metres away across the
            // ground, of which cos(pitch) projects onto the view axis. The
            // visible ground is therefore a slab of view depth
            // Zoom / tan(pitch) either side of the focus point.
            double pitchRadians = CameraRigConfig.PitchDegrees * Math.PI / 180.0;
            float expected = (float)(zoom / Math.Tan(pitchRadians));

            Assert.That(CameraRigConfig.GroundDepthReach(zoom), Is.EqualTo(expected).Within(0.0001f));
        }

        [TestCase(6f)]
        [TestCase(18f)]
        [TestCase(84f)]
        [TestCase(300f)]
        public void RigDistanceFor_HoldsTheNearestVisibleGround_AConstantClearanceInFrontOfTheCamera(float zoom)
        {
            // The set-back grows with the slab, so the near edge of the visible
            // ground sits the same RigDistance in front of the lens at every
            // zoom - which is exactly what the fixed 60m set-back could not do.
            float nearEdgeDepth = CameraRigConfig.RigDistanceFor(zoom) - CameraRigConfig.GroundDepthReach(zoom);

            Assert.That(nearEdgeDepth, Is.EqualTo(CameraRigConfig.RigDistance).Within(0.0001f));
        }

        [Test]
        public void NearPlaneInvariant_HoldsAtEveryZoom_IncludingTheOnesTheOldFixedSetBackClipped()
        {
            // The bug (#679): with a fixed 60m set-back the near edge of the
            // visible ground crossed the 0.3 near plane once Zoom > 59.7, so the
            // foreground half of the frame was clipped away and the clear colour
            // showed through as a blank band along the bottom of the screen.
            for (float zoom = CameraController.MinZoom; zoom <= 2000f; zoom += 7f)
            {
                float nearEdgeDepth = CameraRigConfig.RigDistanceFor(zoom) - CameraRigConfig.GroundDepthReach(zoom);

                Assert.That(nearEdgeDepth, Is.GreaterThan(CameraRigConfig.NearClipPlane),
                    $"the nearest visible ground fell behind the near clip plane at zoom {zoom}");
            }
        }

        [Test]
        public void FarPlaneInvariant_HoldsAtEveryZoom()
        {
            // The mirror of the near-plane failure: with the Main scene's fixed
            // far clip of 300, the distant edge of the slab passed it once
            // Zoom > 240 and the top of the screen clipped instead.
            for (float zoom = CameraController.MinZoom; zoom <= 2000f; zoom += 7f)
            {
                float farEdgeDepth = CameraRigConfig.RigDistanceFor(zoom) + CameraRigConfig.GroundDepthReach(zoom);

                Assert.That(farEdgeDepth, Is.LessThan(CameraRigConfig.FarClipFor(zoom)),
                    $"the far edge of the visible ground passed the far clip plane at zoom {zoom}");
            }
        }

        [Test]
        public void BothInvariants_HoldAtTheMaxZoomsAGrowingMapActuallyProduces()
        {
            // The regression guard #510/#524 needed and lacked: MaxZoom grows
            // with the map extent, and a TileSize of 60m means a single tile of
            // expansion already reaches MaxZoom = 84 - which under the old fixed
            // set-back put the nearest visible ground 24m *behind* the camera.
            // Exercised against the real RecomputeBoundsFromMap path so a future
            // change to the cap cannot silently reintroduce the clip.
            var camera = CameraController.ForStartingNeighborhood();
            var map = new TileMap(new TileCoordinate(0, 0), TileType.FourWay);

            for (int tile = 0; tile <= 12; tile++)
            {
                if (tile > 0)
                {
                    map.Place(new TileCoordinate(0, tile), TileType.FourWay);
                }

                camera.RecomputeBoundsFromMap(map);
                float maxZoom = camera.MaxZoom;

                float nearEdgeDepth = CameraRigConfig.RigDistanceFor(maxZoom) - CameraRigConfig.GroundDepthReach(maxZoom);
                float farEdgeDepth = CameraRigConfig.RigDistanceFor(maxZoom) + CameraRigConfig.GroundDepthReach(maxZoom);

                Assert.That(nearEdgeDepth, Is.GreaterThan(CameraRigConfig.NearClipPlane),
                    $"the near edge clipped at the max zoom-out of a {tile + 1}-tile map ({maxZoom})");
                Assert.That(farEdgeDepth, Is.LessThan(CameraRigConfig.FarClipFor(maxZoom)),
                    $"the far edge clipped at the max zoom-out of a {tile + 1}-tile map ({maxZoom})");
            }
        }

        [Test]
        public void ClearanceMargins_AreNamedCompileTimeConstants()
        {
            // #161/#679: the clearances the invariants turn on are named
            // constants, not literals buried in the rig - and the near plane in
            // particular is owned here rather than left to whatever the Main
            // scene happens to serialize.
            var constFields = typeof(CameraRigConfig)
                .GetFields(BindingFlags.Public | BindingFlags.Static)
                .Where(f => f.IsLiteral)
                .Select(f => f.Name)
                .ToList();

            Assert.That(constFields, Does.Contain(nameof(CameraRigConfig.NearClipPlane)));
            Assert.That(CameraRigConfig.NearClipPlane, Is.GreaterThan(0f));
        }
    }
}
