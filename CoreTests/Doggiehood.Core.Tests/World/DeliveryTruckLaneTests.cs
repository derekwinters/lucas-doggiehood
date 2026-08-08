using System.Collections.Generic;
using System.Linq;
using Doggiehood.Core.World;
using NUnit.Framework;

namespace Doggiehood.Core.Tests.World
{
    /// <summary>
    /// #672: the delivery truck used to drive straddling the centre line for its
    /// whole route, because every road position the route model could express WAS
    /// the centerline. These tests take a real route over a live map, walk it leg
    /// by leg through <see cref="RoadLeg"/>, and assert the line the truck
    /// actually drives keeps right — all the way to the delivery stop and back
    /// out, including through a turnaround.
    ///
    /// The route's own waypoints deliberately stay on the centerline (#538/#599):
    /// the lane is derived per leg, so an intersection waypoint shared by two
    /// roads keeps one unambiguous position.
    /// </summary>
    public class DeliveryTruckLaneTests
    {
        private const float Tolerance = 0.001f;

        private static TileMap FourWay()
        {
            return new TileMap(new TileCoordinate(0, 0), TileType.FourWay);
        }

        // The legs a waypoint path is driven as, in order, skipping any hop that
        // lies along no road (a turnaround pivot, or a zero-length duplicate).
        private static List<RoadLeg> LegsOf(IReadOnlyList<GridPoint> path, IReadOnlyList<Road> roads)
        {
            var legs = new List<RoadLeg>();
            for (var i = 1; i < path.Count; i++)
            {
                if (RoadLeg.TryResolve(roads, path[i - 1], path[i], out var leg))
                {
                    legs.Add(leg);
                }
            }

            return legs;
        }

        // How far the point sits off its road's centerline, signed in the road's
        // own perpendicular axis.
        private static float LateralOf(Road road, GridPoint point)
        {
            return road.Orientation == StreetOrientation.NorthSouth
                ? point.X - road.Center.X
                : point.Z - road.Center.Z;
        }

        [Test]
        public void EveryDrivenPointOfAWholeDelivery_KeepsToTheRightHandLane()
        {
            // The invariant, end to end: in from the off-map opening, to the stop,
            // and back out — on every leg the truck is a lane offset to the right
            // of the centerline, never on it and never across it.
            var map = FourWay();
            var roads = MapWalkNetwork.RoadsFrom(map);
            var route = DeliveryTruckRoute.ToDoor(map, new GridPoint(
                NeighborhoodLayout.LotDistanceFromCenter, NeighborhoodLayout.LotDistanceFromCenter));

            var legs = LegsOf(route.Inbound, roads).Concat(LegsOf(route.Outbound, roads)).ToList();
            Assert.That(legs, Is.Not.Empty, "the route must resolve to at least one road leg");

            foreach (var leg in legs)
            {
                var expectedSign = leg.LaneOffset < 0f ? -1f : 1f;
                var low = System.Math.Min(leg.EntryAlong, leg.ExitAlong);
                var high = System.Math.Max(leg.EntryAlong, leg.ExitAlong);

                for (var along = low; along <= high; along += 0.5f)
                {
                    var driven = leg.PointAt(along);
                    Assert.That(leg.Road.Contains(driven), Is.True,
                        $"driven point at along={along} left the roadway");

                    var lateral = LateralOf(leg.Road, driven);
                    Assert.That(System.Math.Abs(lateral), Is.EqualTo(RoadLane.Offset).Within(Tolerance),
                        "the truck drives its lane centre, not the road's");
                    Assert.That(lateral * expectedSign, Is.GreaterThan(0f),
                        $"driven point at along={along} crossed the centerline");
                }
            }
        }

        [Test]
        public void TheDeliveryStop_LandsInTheRightHandLane_NotOnTheCenterline()
        {
            var map = FourWay();
            var roads = MapWalkNetwork.RoadsFrom(map);
            var route = DeliveryTruckRoute.ToDoor(map, new GridPoint(
                NeighborhoodLayout.LotDistanceFromCenter, NeighborhoodLayout.LotDistanceFromCenter));

            var finalLeg = LegsOf(route.Inbound, roads).Last();
            var stop = finalLeg.PointAt(finalLeg.ExitAlong);

            Assert.That(System.Math.Abs(LateralOf(finalLeg.Road, stop)),
                Is.EqualTo(RoadLane.Offset).Within(Tolerance),
                "the truck parks in its own lane, not straddling the middle of the street");
        }

        [Test]
        public void ADoorOnTheFarSideOfTheStreet_StillStopsTheTruckInItsOwnLane()
        {
            // The tempting wrong answer is to pull the truck across to whichever
            // side the door is on. It must not: the package is carried to the
            // door, the truck stays in its lane. Both frontages of the same street
            // are checked, so at least one is necessarily the far side.
            var map = FourWay();
            var roads = MapWalkNetwork.RoadsFrom(map);
            var sawFarSideDoor = false;

            foreach (var doorX in new[] { NeighborhoodLayout.LotDistanceFromCenter,
                                          -NeighborhoodLayout.LotDistanceFromCenter })
            {
                var route = DeliveryTruckRoute.ToDoor(map, new GridPoint(doorX, 20f));
                var finalLeg = LegsOf(route.Inbound, roads).Last();
                var stop = finalLeg.PointAt(finalLeg.ExitAlong);

                var stopLateral = LateralOf(finalLeg.Road, stop);
                Assert.That(System.Math.Abs(stopLateral), Is.EqualTo(RoadLane.Offset).Within(Tolerance),
                    $"door at x={doorX}: the truck must stop in its own lane");
                Assert.That(stopLateral * finalLeg.LaneOffset, Is.GreaterThan(0f),
                    $"door at x={doorX}: the stop must be on the right for the leg's travel direction");

                var doorLateral = LateralOf(finalLeg.Road, new GridPoint(doorX, 20f));
                if (stopLateral * doorLateral < 0f)
                {
                    sawFarSideDoor = true;
                }
            }

            Assert.That(sawFarSideDoor, Is.True,
                "one of the two frontages must be across the centerline from the truck's lane — "
                + "otherwise this case never exercises a far-side door");
        }

        [Test]
        public void ATurnaround_EndsInTheOppositeLaneForItsReversedHeading()
        {
            // A cul-de-sac retrace reverses the truck's heading on the same road,
            // so it must come back down the OTHER half of the street — a U-turn
            // that stayed in the same lane would be driving into oncoming traffic.
            var map = new TileMap(new TileCoordinate(0, 0), TileType.CulDeSacNorth);
            var roads = MapWalkNetwork.RoadsFrom(map);
            var route = DeliveryTruckRoute.ToDoor(map, new GridPoint(8f, 0f));
            Assert.That(route.IsTurnaround, Is.True, "the fixture must actually retrace");

            var inboundLeg = LegsOf(route.Inbound, roads).Last();
            var outboundLeg = LegsOf(route.Outbound, roads).First();

            Assert.That(outboundLeg.TravelSign, Is.EqualTo(-inboundLeg.TravelSign),
                "the retrace drives back the way it came");
            Assert.That(inboundLeg.LaneOffset * outboundLeg.LaneOffset, Is.LessThan(0f),
                "after turning around the truck must be on the other side of the centerline");
        }
    }
}
