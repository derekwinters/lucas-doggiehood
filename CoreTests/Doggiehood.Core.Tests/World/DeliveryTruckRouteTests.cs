using System.Collections.Generic;
using Doggiehood.Core.World;
using NUnit.Framework;

namespace Doggiehood.Core.Tests.World
{
    /// <summary>
    /// #538: the delivery truck must derive its approach path from the real
    /// road geometry and stay on the roadway for its ENTIRE route — entry,
    /// the stop where it drops the package, and exit are all on a road. It
    /// stops at the road point nearest the dog's front door (never driving
    /// onto the sidewalk, yard, or lot) and stops short of the waiting dog,
    /// so it never overlaps it. The package is still placed AT the door by
    /// the Unity layer; the truck itself does not go there.
    /// </summary>
    public class DeliveryTruckRouteTests
    {
        // A door on the NE lot's street frontage: off the road, out past the
        // verge/sidewalk/front walkway, exactly where a waiting dog sits.
        private static readonly GridPoint NeDoor =
            new GridPoint(NeighborhoodLayout.LotDistanceFromCenter, NeighborhoodLayout.LotDistanceFromCenter);

        [Test]
        public void EntryStopAndExit_AreAllOnARoad()
        {
            var route = DeliveryTruckRoute.ToDoor(NeighborhoodLayout.Roads, NeDoor);

            Assert.That(AnyRoadContains(route.Entry), Is.True, "entry point must be on a road");
            Assert.That(AnyRoadContains(route.Stop), Is.True, "stop point must be on a road");
            Assert.That(AnyRoadContains(route.Exit), Is.True, "exit point must be on a road");
        }

        [Test]
        public void EveryPointAlongTheWholeRoute_StaysOnTheRoadway()
        {
            // The invariant: a delivery truck never leaves the roadway. Sample
            // the full driven path (entry -> stop -> exit) densely and assert
            // every sample lands on a road surface.
            var route = DeliveryTruckRoute.ToDoor(NeighborhoodLayout.Roads, NeDoor);

            AssertLegOnRoad(route.Entry, route.Stop);
            AssertLegOnRoad(route.Stop, route.Exit);
        }

        [Test]
        public void Stop_IsTheNearestRoadPointToTheDoor()
        {
            var route = DeliveryTruckRoute.ToDoor(NeighborhoodLayout.Roads, NeDoor);

            // No road point is closer to the door than the chosen stop.
            var nearest = float.MaxValue;
            foreach (var road in NeighborhoodLayout.Roads)
            {
                for (var along = -road.HalfLength; along <= road.HalfLength; along += 0.1f)
                {
                    var p = road.PointAt(along, 0f);
                    nearest = Mathf.MinF(nearest, Distance(p, NeDoor));
                }
            }

            Assert.That(Distance(route.Stop, NeDoor), Is.EqualTo(nearest).Within(0.15f),
                "the truck must stop at the road point nearest the door");
        }

        [Test]
        public void Stop_StopsShortOfTheDog_NoOverlap()
        {
            // The dog waits at the door; the truck stops on the road well
            // before it. Clearance must exceed the road half-width plus the
            // verge + sidewalk band the door sits beyond.
            var route = DeliveryTruckRoute.ToDoor(NeighborhoodLayout.Roads, NeDoor);

            var clearance = WorldDimensions.RoadWidth / 2f
                            + WorldDimensions.GrassVergeWidth
                            + WorldDimensions.SidewalkWidth;

            Assert.That(Distance(route.Stop, NeDoor), Is.GreaterThan(clearance),
                "the truck must stop short of the waiting dog at the door, not overlap it");
        }

        private static void AssertLegOnRoad(GridPoint from, GridPoint to)
        {
            const int samples = 200;
            for (var i = 0; i <= samples; i++)
            {
                var t = i / (float)samples;
                var p = new GridPoint(from.X + (to.X - from.X) * t, from.Z + (to.Z - from.Z) * t);
                Assert.That(AnyRoadContains(p), Is.True,
                    $"route sample at t={t} left the roadway: {p}");
            }
        }

        private static bool AnyRoadContains(GridPoint point)
        {
            foreach (var road in NeighborhoodLayout.Roads)
            {
                if (road.Contains(point))
                {
                    return true;
                }
            }

            return false;
        }

        private static float Distance(GridPoint a, GridPoint b)
        {
            var dx = a.X - b.X;
            var dz = a.Z - b.Z;
            return (float)System.Math.Sqrt(dx * dx + dz * dz);
        }

        private static class Mathf
        {
            public static float MinF(float a, float b)
            {
                return a < b ? a : b;
            }
        }
    }
}
