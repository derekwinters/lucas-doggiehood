using Doggiehood.Core.Tests.World;
using System.Linq;
using Doggiehood.Core.Cameras;
using Doggiehood.Core.World;
using NUnit.Framework;

namespace Doggiehood.Core.Tests.Cameras
{
    public class CameraControllerTests
    {
        private static CameraController NewController()
        {
            return CameraController.ForStartingNeighborhood();
        }

        [Test]
        public void StartsCenteredOnTheIntersection()
        {
            var camera = NewController();

            Assert.That(camera.Position, Is.EqualTo(NeighborhoodLayout.Intersection));
        }

        [Test]
        public void Pan_MovesThePositionByTheDelta()
        {
            var camera = NewController();

            camera.Pan(3f, -2f);

            Assert.That(camera.Position.X, Is.EqualTo(3f));
            Assert.That(camera.Position.Z, Is.EqualTo(-2f));
        }

        [Test]
        public void Pan_IsClampedToTheWorldBounds()
        {
            var camera = NewController();

            camera.Pan(10000f, 10000f);
            Assert.That(camera.Position.X, Is.EqualTo(camera.Bounds.MaxX));
            Assert.That(camera.Position.Z, Is.EqualTo(camera.Bounds.MaxZ));

            camera.Pan(-20000f, -20000f);
            Assert.That(camera.Position.X, Is.EqualTo(camera.Bounds.MinX));
            Assert.That(camera.Position.Z, Is.EqualTo(camera.Bounds.MinZ));
        }

        [Test]
        public void FocusOn_MovesThePositionToTheTargetPoint()
        {
            // #165: the dog profile's Home button flies the camera to the
            // dog's house — an absolute focus, not a relative pan.
            var camera = NewController();

            camera.FocusOn(new GridPoint(5f, -7f));

            Assert.That(camera.Position.X, Is.EqualTo(5f));
            Assert.That(camera.Position.Z, Is.EqualTo(-7f));
        }

        [Test]
        public void FocusOn_IsClampedToTheWorldBounds()
        {
            var camera = NewController();

            camera.FocusOn(new GridPoint(10000f, 10000f));

            Assert.That(camera.Position.X, Is.EqualTo(camera.Bounds.MaxX));
            Assert.That(camera.Position.Z, Is.EqualTo(camera.Bounds.MaxZ));
        }

        [Test]
        public void WorldBounds_EncloseEveryHouseLot()
        {
            var camera = NewController();

            foreach (var lot in NeighborhoodLayout.HouseLots)
            {
                Assert.That(lot.Position.X, Is.InRange(camera.Bounds.MinX, camera.Bounds.MaxX));
                Assert.That(lot.Position.Z, Is.InRange(camera.Bounds.MinZ, camera.Bounds.MaxZ));
            }
        }

        [Test]
        public void RecomputeBoundsFromMap_GrowsBoundsToReachAnUnlockedNorthernZone()
        {
            // #373 (Gap 2): unlocking a zone that extends the map north (the
            // single CulDeSacSouth at (0,1), #360) must grow the pan bounds so
            // the player can pan/focus onto the new zone's lots.
            var camera = NewController();

            // The northernmost lot of the first (north cul-de-sac) zone —
            // outside the starting bounds until the map grows.
            var northLot = FrontierTestWorld.FirstTileLots()
                .OrderByDescending(lot => lot.Position.Z)
                .First()
                .Position;
            Assert.That(northLot.Z, Is.GreaterThan(camera.Bounds.MaxZ),
                "the starting bounds exclude the northern zone");

            var map = new TileMap(new TileCoordinate(0, 0), TileType.FourWay);
            map.Place(FrontierTestWorld.FirstTile, FrontierTestWorld.FirstTileType);
            camera.RecomputeBoundsFromMap(map);

            Assert.That(northLot.Z, Is.InRange(camera.Bounds.MinZ, camera.Bounds.MaxZ),
                "the grown bounds now include the northern lot");
        }

        [Test]
        public void AfterBoundsGrow_PanAndFocusOn_CanReachTheNewZone()
        {
            var camera = NewController();
            var northLot = FrontierTestWorld.FirstTileLots()
                .OrderByDescending(lot => lot.Position.Z)
                .First()
                .Position;

            var map = new TileMap(new TileCoordinate(0, 0), TileType.FourWay);
            map.Place(FrontierTestWorld.FirstTile, FrontierTestWorld.FirstTileType);
            camera.RecomputeBoundsFromMap(map);

            camera.FocusOn(northLot);
            Assert.That(camera.Position.Z, Is.EqualTo(northLot.Z),
                "FocusOn reaches the new zone's lot rather than clamping short of it");

            camera.FocusOn(NeighborhoodLayout.Intersection);
            camera.Pan(0f, 10000f);
            Assert.That(camera.Position.Z, Is.GreaterThanOrEqualTo(northLot.Z),
                "panning north now travels past the new zone's lot");
        }

        [Test]
        public void RecomputeBoundsFromMap_ReclampsThePositionIntoTheNewBounds()
        {
            // Growing the map only ever widens the bounds here, but the
            // controller must re-settle its position against whatever the new
            // bounds are so it never sits outside them.
            var camera = NewController();
            camera.Pan(10000f, 10000f); // pinned to the old max corner

            var map = new TileMap(new TileCoordinate(0, 0), TileType.FourWay);
            map.Place(FrontierTestWorld.FirstTile, FrontierTestWorld.FirstTileType);
            camera.RecomputeBoundsFromMap(map);

            Assert.That(camera.Position.X, Is.InRange(camera.Bounds.MinX, camera.Bounds.MaxX));
            Assert.That(camera.Position.Z, Is.InRange(camera.Bounds.MinZ, camera.Bounds.MaxZ));
        }

        [Test]
        public void MaxZoom_ForStartingNeighborhood_FramesTheSeededIntersection()
        {
            // #510: the max zoom-out is now a per-instance value derived from
            // the map extent (no longer a fixed static const). Even the tiny
            // starting map must let the player frame the whole seeded tile.
            var camera = NewController();

            Assert.That(camera.MaxZoom, Is.GreaterThan(CameraController.DefaultZoom));
            Assert.That(camera.MaxZoom, Is.GreaterThanOrEqualTo(WorldDimensions.TileSize / 2f));
        }

        [Test]
        public void RecomputeBoundsFromMap_GrowsTheMaxZoomOutAsTheMapGrows()
        {
            // #510: mirroring how the pan bounds already grow, the max zoom-out
            // scales up with the live map extent so the player can zoom out far
            // enough to see the whole larger neighborhood at once.
            var camera = NewController();
            var startingMaxZoom = camera.MaxZoom;

            var map = new TileMap(new TileCoordinate(0, 0), TileType.FourWay);
            map.Place(FrontierTestWorld.FirstTile, FrontierTestWorld.FirstTileType);
            camera.RecomputeBoundsFromMap(map);

            Assert.That(camera.MaxZoom, Is.GreaterThan(startingMaxZoom),
                "growing the map raises the zoom-out cap");
        }

        [Test]
        public void AfterBoundsGrow_ZoomBy_CanReachTheNewLargerMaxZoom()
        {
            // #510: previously ZoomBy was clamped at a fixed 30f. After the map
            // grows, the player can zoom out past the old cap to the new one.
            var camera = NewController();
            var map = new TileMap(new TileCoordinate(0, 0), TileType.FourWay);
            map.Place(FrontierTestWorld.FirstTile, FrontierTestWorld.FirstTileType);
            camera.RecomputeBoundsFromMap(map);

            camera.ZoomBy(100000f);

            Assert.That(camera.Zoom, Is.EqualTo(camera.MaxZoom));
            Assert.That(camera.Zoom, Is.GreaterThan(30f),
                "the grown map lets the camera zoom out past the old fixed cap");
        }

        [Test]
        public void RecomputeBoundsFromMap_ReclampsTheZoomIntoTheNewRange()
        {
            // #510: whenever the range recomputes, the current Zoom is re-settled
            // into [MinZoom, MaxZoom] — mirroring how Position is re-clamped — so
            // recomputing to a smaller map never leaves the camera past the cap.
            var camera = NewController();

            var big = new TileMap(new TileCoordinate(0, 0), TileType.FourWay);
            big.Place(FrontierTestWorld.FirstTile, FrontierTestWorld.FirstTileType);
            camera.RecomputeBoundsFromMap(big);
            camera.ZoomBy(100000f); // pinned at the big map's max zoom-out
            var zoomedOut = camera.Zoom;

            var small = new TileMap(new TileCoordinate(0, 0), TileType.FourWay);
            camera.RecomputeBoundsFromMap(small);

            Assert.That(camera.MaxZoom, Is.LessThan(zoomedOut),
                "the smaller map has a smaller zoom-out cap");
            Assert.That(camera.Zoom, Is.EqualTo(camera.MaxZoom),
                "the current zoom is pulled back down onto the new cap");
            Assert.That(camera.Zoom, Is.InRange(CameraController.MinZoom, camera.MaxZoom));
        }

        [Test]
        public void GroundExtentForMap_PadsTheTileCoveringByTheBoundsMargin()
        {
            // #558: the ground mesh tracks the map's own tile footprint plus a
            // modest, constant BoundsMargin on every side — the same margin the
            // pan Bounds use — decoupled from the camera's MaxZoom (which #536
            // padded by, and which balloons as the map grows). So it is exactly
            // MapExtent.Covering padded by BoundsMargin per side, no more.
            var map = new TileMap(new TileCoordinate(0, 0), TileType.FourWay);
            map.Place(FrontierTestWorld.FirstTile, FrontierTestWorld.FirstTileType);

            var covering = MapExtent.Covering(map);
            var ground = CameraController.GroundExtentForMap(map);

            Assert.That(ground.MinX, Is.EqualTo(covering.MinX - CameraController.BoundsMargin).Within(0.001f),
                "west edge is the tile coverage minus one BoundsMargin");
            Assert.That(ground.MaxX, Is.EqualTo(covering.MaxX + CameraController.BoundsMargin).Within(0.001f),
                "east edge is the tile coverage plus one BoundsMargin");
            Assert.That(ground.MinZ, Is.EqualTo(covering.MinZ - CameraController.BoundsMargin).Within(0.001f),
                "south edge is the tile coverage minus one BoundsMargin");
            Assert.That(ground.MaxZ, Is.EqualTo(covering.MaxZ + CameraController.BoundsMargin).Within(0.001f),
                "north edge is the tile coverage plus one BoundsMargin");
        }

        [Test]
        public void GroundExtentForMap_MarginStaysConstant_WhileMaxZoomGrowsWithTheMap()
        {
            // #558: ground extent no longer scales with MaxZoom. The pad beyond
            // the tile coverage stays a constant 2 x BoundsMargin per axis
            // regardless of map size, even though MaxZoom keeps growing with the
            // map — that decoupling is the whole point of the fix.
            var small = new TileMap(new TileCoordinate(0, 0), TileType.FourWay);
            var big = new TileMap(new TileCoordinate(0, 0), TileType.FourWay);
            big.Place(FrontierTestWorld.FirstTile, FrontierTestWorld.FirstTileType);

            var smallPad = CameraController.GroundExtentForMap(small).Width - MapExtent.Covering(small).Width;
            var bigPad = CameraController.GroundExtentForMap(big).Width - MapExtent.Covering(big).Width;

            Assert.That(smallPad, Is.EqualTo(2f * CameraController.BoundsMargin).Within(0.001f));
            Assert.That(bigPad, Is.EqualTo(2f * CameraController.BoundsMargin).Within(0.001f),
                "the ground pad stays a constant margin regardless of map size");

            // Meanwhile MaxZoom still grows with the map (unchanged by #558).
            var smallCam = NewController();
            smallCam.RecomputeBoundsFromMap(small);
            var bigCam = NewController();
            bigCam.RecomputeBoundsFromMap(big);
            Assert.That(bigCam.MaxZoom, Is.GreaterThan(smallCam.MaxZoom),
                "MaxZoom still grows with the map even though the ground pad does not");
        }

        [Test]
        public void GroundExtentForMap_StaysCentredOnTheMapAndGrowsWithIt()
        {
            var small = new TileMap(new TileCoordinate(0, 0), TileType.FourWay);
            var big = new TileMap(new TileCoordinate(0, 0), TileType.FourWay);
            big.Place(FrontierTestWorld.FirstTile, FrontierTestWorld.FirstTileType);

            var smallGround = CameraController.GroundExtentForMap(small);
            var bigGround = CameraController.GroundExtentForMap(big);

            Assert.That(smallGround.CenterX, Is.EqualTo(MapExtent.Covering(small).CenterX).Within(0.001f));
            Assert.That(smallGround.CenterZ, Is.EqualTo(MapExtent.Covering(small).CenterZ).Within(0.001f));
            // The extra north tile grows the map's footprint along Z, so — now
            // that ground tracks the footprint rather than a uniform MaxZoom
            // radius — the plane grows along that same axis with it.
            Assert.That(bigGround.Depth, Is.GreaterThan(smallGround.Depth),
                "a bigger map footprint grows the ground plane with it");
        }

        [Test]
        public void ZoomBy_ChangesTheZoomLevel()
        {
            var camera = NewController();
            var before = camera.Zoom;

            camera.ZoomBy(-2f);

            Assert.That(camera.Zoom, Is.EqualTo(before - 2f));
        }

        [Test]
        public void ZoomBy_IsClampedBetweenMinAndMax()
        {
            var camera = NewController();

            camera.ZoomBy(-10000f);
            Assert.That(camera.Zoom, Is.EqualTo(CameraController.MinZoom));

            camera.ZoomBy(10000f);
            Assert.That(camera.Zoom, Is.EqualTo(camera.MaxZoom));
        }

        [Test]
        public void InitialZoom_IsWithinTheClampRange()
        {
            var camera = NewController();

            Assert.That(camera.Zoom, Is.InRange(CameraController.MinZoom, camera.MaxZoom));
        }

        [Test]
        public void Yaw_StartsAtTheDefaultIsometricYaw()
        {
            // #203: yaw is now mutable state, starting at the old fixed 45deg.
            var camera = NewController();

            Assert.That(camera.Yaw, Is.EqualTo(CameraController.DefaultYaw));
            Assert.That(CameraController.DefaultYaw, Is.EqualTo(45f));
        }

        [Test]
        public void Rotate_ChangesYawByTheDelta()
        {
            var camera = NewController();

            camera.Rotate(30f);

            Assert.That(camera.Yaw, Is.EqualTo(45f + 30f));
        }

        [Test]
        public void Rotate_IsFree_NoClampingOrSnapping()
        {
            // #203: free continuous rotation - yaw is never clamped to a range
            // nor snapped to fixed angles, in either direction, past a full turn.
            var camera = NewController();

            camera.Rotate(1000f);
            Assert.That(camera.Yaw, Is.EqualTo(45f + 1000f));

            camera.Rotate(-2000f);
            Assert.That(camera.Yaw, Is.EqualTo(45f + 1000f - 2000f));
        }
    }
}
