using System;
using Doggiehood.Core.Cameras;
using Doggiehood.Core.World;
using NUnit.Framework;

namespace Doggiehood.Core.Tests.Cameras
{
    /// <summary>
    /// The zoom-out framing regression the bottom "border" chase kept missing
    /// (#536 → #558 → #570 → #611, all treated as a *coverage* problem).
    ///
    /// The rig is an orthographic camera pitched <see cref="CameraRigConfig.PitchDegrees"/>
    /// down, set back along its view direction from the focus point on the
    /// ground. Because the ground is tilted away from the view axis, the strip
    /// of ground the viewport covers is not flat in *view depth*: a point at the
    /// top/bottom edge of the frame sits <c>Zoom / tan(pitch)</c> metres further
    /// from / closer to the camera than the focus point (Zoom is the orthographic
    /// half-height, so the edge is <c>Zoom / sin(pitch)</c> metres away across
    /// the ground, of which <c>cos(pitch)</c> projects onto the view axis).
    ///
    /// So the visible ground spans <c>[setback - reach, setback + reach]</c> in
    /// view depth, and that whole span has to fit between the camera's near and
    /// far clip planes. With a *fixed* setback the near half falls behind the
    /// camera as soon as Zoom exceeds it — the foreground is clipped away and
    /// the clear colour shows through as a growing band along the bottom of the
    /// screen. Since #510/#524 made MaxZoom grow with the map, that is exactly
    /// what the playtest builds show.
    /// </summary>
    public class CameraRigDepthTests
    {
        private const float Tolerance = 0.001f;

        /// <summary>Independent restatement of the geometry the production code
        /// must implement: how far, along the view axis, the near and far edges
        /// of the visible ground sit from the focus point.</summary>
        private static float ExpectedDepthReach(float zoom)
        {
            var pitchRadians = CameraRigConfig.PitchDegrees * Math.PI / 180.0;
            return (float)(zoom / Math.Tan(pitchRadians));
        }

        [Test]
        public void GroundDepthReach_IsTheOrthographicHalfHeightProjectedOntoTheViewAxis()
        {
            foreach (var zoom in new[] { CameraController.MinZoom, CameraController.DefaultZoom, 120f, 400f })
            {
                Assert.That(CameraRigConfig.GroundDepthReach(zoom),
                    Is.EqualTo(ExpectedDepthReach(zoom)).Within(Tolerance),
                    $"depth reach at zoom {zoom}");
            }
        }

        [Test]
        public void RigSetback_KeepsTheNearestVisibleGroundInFrontOfTheNearClipPlane()
        {
            // The regression itself. A fixed 60m setback puts the bottom edge of
            // the frame *behind* the camera for any zoom past ~60 — negative view
            // depth, so the near clip plane discards it. The setback has to grow
            // with the zoom instead.
            foreach (var zoom in new[] { CameraController.MinZoom, CameraController.DefaultZoom, 60f, 120f, 400f, 1000f })
            {
                var nearestVisibleDepth =
                    CameraRigConfig.RigDistanceFor(zoom) - CameraRigConfig.GroundDepthReach(zoom);

                Assert.That(nearestVisibleDepth, Is.GreaterThan(CameraRigConfig.NearClipPlane),
                    $"the foreground edge of the frame is clipped away at zoom {zoom}");
            }
        }

        [Test]
        public void FarClip_ReachesPastTheFarthestVisibleGround()
        {
            foreach (var zoom in new[] { CameraController.MinZoom, CameraController.DefaultZoom, 120f, 400f, 1000f })
            {
                var farthestVisibleDepth =
                    CameraRigConfig.RigDistanceFor(zoom) + CameraRigConfig.GroundDepthReach(zoom);

                Assert.That(CameraRigConfig.FarClipFor(zoom), Is.GreaterThan(farthestVisibleDepth),
                    $"the distant edge of the frame is clipped away at zoom {zoom}");
            }
        }

        [Test]
        public void ClipRange_StaysValid_NearAlwaysCloserThanFar()
        {
            foreach (var zoom in new[] { CameraController.MinZoom, CameraController.DefaultZoom, 400f })
            {
                Assert.That(CameraRigConfig.NearClipPlane, Is.GreaterThan(0f),
                    "Unity rejects a non-positive near clip plane");
                Assert.That(CameraRigConfig.FarClipFor(zoom), Is.GreaterThan(CameraRigConfig.NearClipPlane));
            }
        }

        [Test]
        public void RigSetback_GrowsWithZoom_RatherThanStayingTheFixedDistance()
        {
            // Names the defect: the setback used to be the constant RigDistance
            // at every zoom, which is what let the frame outrun the camera.
            var atDefault = CameraRigConfig.RigDistanceFor(CameraController.DefaultZoom);
            var farOut = CameraRigConfig.RigDistanceFor(400f);

            Assert.That(farOut, Is.GreaterThan(atDefault),
                "zooming out has to pull the camera back, not just widen the frustum");
            Assert.That(CameraRigConfig.RigDistanceFor(0f), Is.EqualTo(CameraRigConfig.RigDistance).Within(Tolerance),
                "RigDistance stays the clearance kept in front of the nearest visible ground");
        }

        [Test]
        public void AtMaxZoomOut_TheWholeFrameStaysInsideTheClipRange_ForEveryMapSize()
        {
            // End-to-end with the live growth path: MaxZoom is re-derived from
            // the map on every unlock (#510/#524), so the invariant has to hold
            // at the cap for maps of every size — that coupling is what turned a
            // latent clipping bug into a screen-filling one as the map grew.
            var map = new TileMap(new TileCoordinate(0, 0), TileType.FourWay);
            var camera = new CameraController(
                new WorldBounds(-1f, 1f, -1f, 1f), new GridPoint(0f, 0f), CameraController.DefaultZoom);

            for (var ring = 1; ring <= 12; ring++)
            {
                map.Place(new TileCoordinate(ring, 0), TileType.StraightEW);
                map.Place(new TileCoordinate(0, ring), TileType.StraightNS);
                camera.RecomputeBoundsFromMap(map);
                camera.ZoomBy(100000f); // pin at the cap

                var reach = CameraRigConfig.GroundDepthReach(camera.Zoom);
                var setback = CameraRigConfig.RigDistanceFor(camera.Zoom);

                Assert.That(setback - reach, Is.GreaterThan(CameraRigConfig.NearClipPlane),
                    $"foreground clipped at max zoom-out with a {ring}-tile arm (zoom {camera.Zoom})");
                Assert.That(setback + reach, Is.LessThan(CameraRigConfig.FarClipFor(camera.Zoom)),
                    $"background clipped at max zoom-out with a {ring}-tile arm (zoom {camera.Zoom})");
            }
        }
    }
}
