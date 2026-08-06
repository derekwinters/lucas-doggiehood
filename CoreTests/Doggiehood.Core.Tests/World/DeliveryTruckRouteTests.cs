using System.Collections.Generic;
using System.Linq;
using Doggiehood.Core.World;
using NUnit.Framework;

namespace Doggiehood.Core.Tests.World
{
    /// <summary>
    /// #599: the delivery truck enters off-map at the open road edge nearest the
    /// destination door and routes IN over the live multi-tile road network to
    /// the road point nearest the door, drops the package, then routes OUT by a
    /// different opening when one is reachable — retracing the way it came only
    /// on a spur / cul-de-sac. It never leaves the roadway: every waypoint lies
    /// on a live road centerline (#538 invariant preserved).
    /// </summary>
    public class DeliveryTruckRouteTests
    {
        private const float Half = WorldDimensions.TileSize / 2f;

        // A door on the NE lot's street frontage — off the road, out past the
        // verge/sidewalk, where a waiting dog sits.
        private static readonly GridPoint NeDoor =
            new GridPoint(NeighborhoodLayout.LotDistanceFromCenter, NeighborhoodLayout.LotDistanceFromCenter);

        private static TileMap FourWay()
        {
            return new TileMap(new TileCoordinate(0, 0), TileType.FourWay);
        }

        [Test]
        public void Route_EntersFromTheOffMapOpeningNearestTheDoor()
        {
            var map = FourWay();
            var door = new GridPoint(40f, 6f); // clearly east

            var route = DeliveryTruckRoute.ToDoor(map, door);

            var expected = RoadOpenings.Nearest(RoadOpenings.Detect(map), door);
            Assert.That(route.Entry.X, Is.EqualTo(expected.Point.X).Within(0.001f));
            Assert.That(route.Entry.Z, Is.EqualTo(expected.Point.Z).Within(0.001f));
        }

        [Test]
        public void Route_StopIsTheNearestRoadPointToTheDoor()
        {
            var map = FourWay();
            var route = DeliveryTruckRoute.ToDoor(map, NeDoor);

            var nearest = float.MaxValue;
            foreach (var road in MapWalkNetwork.RoadsFrom(map))
            {
                for (var along = -road.HalfLength; along <= road.HalfLength; along += 0.1f)
                {
                    nearest = System.Math.Min(nearest, Distance(road.PointAt(along, 0f), NeDoor));
                }
            }

            Assert.That(Distance(route.Stop, NeDoor), Is.EqualTo(nearest).Within(0.15f));
        }

        [Test]
        public void Route_WholeDrivenPath_StaysOnTheRoadway()
        {
            var map = FourWay();
            var route = DeliveryTruckRoute.ToDoor(map, NeDoor);
            var roads = MapWalkNetwork.RoadsFrom(map);

            AssertPathOnRoads(route.Inbound, roads);
            AssertPathOnRoads(route.Outbound, roads);
        }

        [Test]
        public void Route_StopsShortOfTheWaitingDog_NoOverlap()
        {
            var route = DeliveryTruckRoute.ToDoor(FourWay(), NeDoor);
            var clearance = WorldDimensions.RoadWidth / 2f
                            + WorldDimensions.GrassVergeWidth
                            + WorldDimensions.SidewalkWidth;

            Assert.That(Distance(route.Stop, NeDoor), Is.GreaterThan(clearance));
        }

        [Test]
        public void Route_AcrossTwoTiles_SpansBothTilesOnTheCenterline()
        {
            var map = new TileMap(new TileCoordinate(0, 0), TileType.StraightNS);
            map.Place(new TileCoordinate(0, 1), TileType.StraightNS);

            // A door near the northern tile: entry is the north opening, exit the
            // south — so the full route crosses the shared boundary between tiles.
            var route = DeliveryTruckRoute.ToDoor(map, new GridPoint(5f, 45f));

            var all = route.Inbound.Concat(route.Outbound).ToList();
            Assert.That(all.All(p => System.Math.Abs(p.X) < 0.001f), Is.True, "stays on x=0 centerline");
            Assert.That(all.Any(p => System.Math.Abs(p.Z - Half) < 0.001f), Is.True,
                "passes through the shared tile boundary node (0, +Half)");
            Assert.That(all.Any(p => p.Z > WorldDimensions.TileSize), Is.True, "reaches the north tile");
            Assert.That(all.Any(p => p.Z < 0f), Is.True, "reaches the south tile");
        }

        [Test]
        public void Route_ExitsByADifferentOpening_WhenOneIsReachable()
        {
            // Single StraightNS: two openings (N and S). Enter by the nearer,
            // leave by the other — no retrace.
            var map = new TileMap(new TileCoordinate(0, 0), TileType.StraightNS);
            var door = new GridPoint(10f, 20f); // nearer the north opening

            var route = DeliveryTruckRoute.ToDoor(map, door);

            Assert.That(route.IsTurnaround, Is.False);
            Assert.That(route.Entry.Z, Is.EqualTo(Half).Within(0.001f), "entered from the north opening");
            Assert.That(route.Exit.Z, Is.EqualTo(-Half).Within(0.001f), "exited by the different south opening");
        }

        [Test]
        public void Route_Retraces_WhenTheEntryIsTheOnlyReachableOpening()
        {
            // A cul-de-sac tile has exactly one off-map opening — the truck must
            // turn around and retrace its inbound path back out.
            var map = new TileMap(new TileCoordinate(0, 0), TileType.CulDeSacNorth);

            var route = DeliveryTruckRoute.ToDoor(map, new GridPoint(8f, 0f));

            Assert.That(route.IsTurnaround, Is.True);
            Assert.That(route.Exit.X, Is.EqualTo(route.Entry.X).Within(0.001f));
            Assert.That(route.Exit.Z, Is.EqualTo(route.Entry.Z).Within(0.001f));

            // Outbound is the inbound reversed (in-place reorient, v1).
            var reversedInbound = route.Inbound.Reverse().ToList();
            Assert.That(route.Outbound.Count, Is.EqualTo(reversedInbound.Count));
            for (var i = 0; i < reversedInbound.Count; i++)
            {
                Assert.That(route.Outbound[i].X, Is.EqualTo(reversedInbound[i].X).Within(0.001f));
                Assert.That(route.Outbound[i].Z, Is.EqualTo(reversedInbound[i].Z).Within(0.001f));
            }
        }

        private static void AssertPathOnRoads(IReadOnlyList<GridPoint> path, IReadOnlyList<Road> roads)
        {
            const int samples = 60;
            for (var seg = 0; seg < path.Count - 1; seg++)
            {
                var from = path[seg];
                var to = path[seg + 1];
                for (var i = 0; i <= samples; i++)
                {
                    var t = i / (float)samples;
                    var p = new GridPoint(from.X + (to.X - from.X) * t, from.Z + (to.Z - from.Z) * t);
                    Assert.That(roads.Any(r => r.Contains(p)), Is.True,
                        $"route sample {p} left the roadway");
                }
            }
        }

        private static float Distance(GridPoint a, GridPoint b)
        {
            var dx = a.X - b.X;
            var dz = a.Z - b.Z;
            return (float)System.Math.Sqrt((dx * dx) + (dz * dz));
        }
    }
}
