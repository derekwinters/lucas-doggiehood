using System.Collections.Generic;
using System.Linq;
using Doggiehood.Core.World;
using NUnit.Framework;

namespace Doggiehood.Core.Tests.World
{
    /// <summary>
    /// #672: a driven route is a sequence of LEGS, each running along one road,
    /// and the lane a vehicle keeps to is a property of the leg — not of the
    /// route's waypoints. An intersection waypoint belongs to two roads with two
    /// different right-hand sides, so shifting the graph itself would make that
    /// waypoint ambiguous; resolving the road and travel direction per leg (which
    /// the driving code already did privately) and offsetting there keeps the road
    /// network on its centerline and localises the lane to the leg being driven.
    ///
    /// <see cref="RoadLeg"/> is that resolution moved into Core, so the lane rule,
    /// the travel sign and the along-road entry/exit are all testable without the
    /// engine — and so the intersection-manoeuvre work (#673) has one place to
    /// reason about a leg rather than reaching into a MonoBehaviour.
    /// </summary>
    public class RoadLegTests
    {
        private const float Tolerance = 0.001f;

        private static IReadOnlyList<Road> OriginRoads()
        {
            return MapWalkNetwork.RoadsFrom(new TileMap(new TileCoordinate(0, 0), TileType.FourWay));
        }

        private static Road NorthSouth(IReadOnlyList<Road> roads)
        {
            return roads.First(r => r.Orientation == StreetOrientation.NorthSouth);
        }

        [Test]
        public void Resolve_FindsTheRoadALegRunsAlong_AndItsTravelSign()
        {
            var roads = OriginRoads();
            var road = NorthSouth(roads);

            // Driving north (toward increasing Z) up the north-south road.
            var from = road.PointAt(-10f, 0f);
            var to = road.PointAt(10f, 0f);

            Assert.That(RoadLeg.TryResolve(roads, from, to, out var leg), Is.True);
            Assert.That(leg.Road.Orientation, Is.EqualTo(StreetOrientation.NorthSouth));
            Assert.That(leg.TravelSign, Is.EqualTo(1f));
            Assert.That(leg.EntryAlong, Is.EqualTo(-10f).Within(Tolerance));
            Assert.That(leg.ExitAlong, Is.EqualTo(10f).Within(Tolerance));
        }

        [Test]
        public void Resolve_ReadsTheReverseLegAsTheOppositeTravelSign()
        {
            var roads = OriginRoads();
            var road = NorthSouth(roads);

            Assert.That(RoadLeg.TryResolve(roads, road.PointAt(10f, 0f), road.PointAt(-10f, 0f), out var leg),
                Is.True);
            Assert.That(leg.TravelSign, Is.EqualTo(-1f));
        }

        [Test]
        public void Resolve_RejectsALegThatIsNotAlongAnyRoad()
        {
            var roads = OriginRoads();

            // A diagonal across a yard: on no road's axis.
            Assert.That(RoadLeg.TryResolve(roads, new GridPoint(20f, 20f), new GridPoint(25f, 26f), out _),
                Is.False);
        }

        [Test]
        public void ALegsDrivenPoint_IsTheRightHandLaneNotTheCenterline()
        {
            var roads = OriginRoads();
            var road = NorthSouth(roads);

            Assert.That(RoadLeg.TryResolve(roads, road.PointAt(-10f, 0f), road.PointAt(10f, 0f), out var leg),
                Is.True);

            var driven = leg.PointAt(0f);
            var centerline = road.PointAt(0f, 0f);

            Assert.That(driven.X, Is.Not.EqualTo(centerline.X).Within(Tolerance),
                "the driven line must leave the centerline");
            Assert.That(driven.X, Is.EqualTo(centerline.X + RoadLane.Offset).Within(Tolerance),
                "heading north (+Z), the right-hand lane is on the +X side");
            Assert.That(driven.Z, Is.EqualTo(centerline.Z).Within(Tolerance));
        }

        [Test]
        public void TwoLegsInOppositeDirectionsOnOneRoad_SitOnOppositeSidesOfTheCenterline()
        {
            // Two trucks passing: each keeps to its own half of the road, so they
            // never occupy the same lateral band.
            var roads = OriginRoads();
            var road = NorthSouth(roads);

            Assert.That(RoadLeg.TryResolve(roads, road.PointAt(-10f, 0f), road.PointAt(10f, 0f), out var north),
                Is.True);
            Assert.That(RoadLeg.TryResolve(roads, road.PointAt(10f, 0f), road.PointAt(-10f, 0f), out var south),
                Is.True);

            var lateralNorth = north.PointAt(0f).X - road.Center.X;
            var lateralSouth = south.PointAt(0f).X - road.Center.X;

            Assert.That(lateralNorth * lateralSouth, Is.LessThan(0f),
                "opposing traffic must be on opposite sides of the centerline");
        }

        [Test]
        public void EveryDrivenPointOnALeg_StaysOnTheRoadwayAndOffTheCenterline()
        {
            // The #538 invariant is preserved and #672 tightens it: on a road leg
            // the vehicle is on the paved surface AND never crosses the centerline.
            var roads = OriginRoads();

            foreach (var road in roads)
            {
                foreach (var travelSign in new[] { 1f, -1f })
                {
                    var from = road.PointAt(-road.HalfLength * travelSign, 0f);
                    var to = road.PointAt(road.HalfLength * travelSign, 0f);
                    Assert.That(RoadLeg.TryResolve(roads, from, to, out var leg), Is.True);

                    var expectedSign = leg.LaneOffset < 0f ? -1f : 1f;
                    for (var along = -road.HalfLength; along <= road.HalfLength; along += 0.5f)
                    {
                        var driven = leg.PointAt(along);
                        Assert.That(road.Contains(driven), Is.True,
                            $"driven point at along={along} left the roadway");

                        var lateral = road.Orientation == StreetOrientation.NorthSouth
                            ? driven.X - road.Center.X
                            : driven.Z - road.Center.Z;
                        Assert.That(lateral * expectedSign, Is.GreaterThan(0f),
                            $"driven point at along={along} crossed the centerline");
                    }
                }
            }
        }
    }
}
