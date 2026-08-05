using System.Linq;
using Doggiehood.Core.World;
using NUnit.Framework;

namespace Doggiehood.Core.Tests.World
{
    /// <summary>
    /// #546: a vehicle driving along one road claims each crosswalk it reaches
    /// (so a dog that hasn't claimed it yet must wait) and pauses at the near
    /// edge of any crosswalk a dog already holds, resuming once the dog clears
    /// it. This is the vehicle side of the first-come <see cref="RoadCrossingGate"/>
    /// rule, expressed purely in along-road coordinates so the thin Unity view
    /// only converts positions and drives.
    /// </summary>
    public class RoadCrossingTraversalTests
    {
        private const float HalfCrosswalk = 3f / 2f; // WorldDimensions.CrosswalkWidth / 2

        private static Road NorthSouthRoad()
        {
            return NeighborhoodLayout.Roads.First(r => r.Orientation == StreetOrientation.NorthSouth);
        }

        // The starting intersection's north-south road carries two crosswalks,
        // north (+) and south (-) of the origin, at along = +/- the crossing
        // road's sidewalk offset (4.75m).
        private static WalkEdge CrosswalkAt(Road road, float along)
        {
            return NeighborhoodLayout.WalkNetwork.Edges.Single(e =>
                e.Kind == WalkEdgeKind.Crosswalk
                && System.Math.Abs(road.AlongAxis(Midpoint(e)) - along) < 0.01f
                && System.Math.Abs(Perp(road, Midpoint(e))) < 0.01f);
        }

        private static GridPoint Midpoint(WalkEdge e)
        {
            return new GridPoint((e.A.X + e.B.X) / 2f, (e.A.Z + e.B.Z) / 2f);
        }

        private static float Perp(Road road, GridPoint p)
        {
            return road.Orientation == StreetOrientation.NorthSouth
                ? p.X - road.Center.X
                : p.Z - road.Center.Z;
        }

        [Test]
        public void PausesAtTheNearEdge_WhenTheNextCrosswalkIsHeldByAnotherOccupant()
        {
            var gate = new RoadCrossingGate();
            var road = NorthSouthRoad();
            var truck = new object();
            // Truck drives from the north end (+30) to the south end (-30).
            var traversal = new RoadCrossingTraversal(gate, truck, road, NeighborhoodLayout.WalkNetwork, 30f, -30f);

            // A dog claims the north crosswalk first.
            var north = CrosswalkAt(road, 4.75f);
            Assert.That(gate.TryEnter(north, new object()), Is.True);

            // The truck, already at the near edge, must not advance into the band.
            var nearEdge = 4.75f + HalfCrosswalk; // approached travelling in -Z, near edge is on the +Z side
            var allowed = traversal.Advance(nearEdge, -30f);

            Assert.That(allowed, Is.EqualTo(nearEdge).Within(0.001f),
                "the vehicle must hold at the crosswalk's near edge while a dog holds the claim");
        }

        [Test]
        public void ClaimsAFreeCrosswalkAndDrivesThrough_BlockingADogUntilPast()
        {
            var gate = new RoadCrossingGate();
            var road = NorthSouthRoad();
            var truck = new object();
            var traversal = new RoadCrossingTraversal(gate, truck, road, NeighborhoodLayout.WalkNetwork, 30f, -30f);

            var north = CrosswalkAt(road, 4.75f);
            var nearEdge = 4.75f + HalfCrosswalk;

            // Reaching a FREE crosswalk boundary claims it and lets the truck
            // continue past — down to the next crosswalk's near edge.
            var allowed = traversal.Advance(nearEdge, -30f);
            var southNear = -4.75f + HalfCrosswalk; // -3.25
            Assert.That(allowed, Is.EqualTo(southNear).Within(0.001f),
                "a free crosswalk is claimed and driven through, stopping at the next crosswalk's near edge");

            // Having claimed it, a dog arriving now is denied.
            Assert.That(gate.TryEnter(north, new object()), Is.False,
                "while the truck holds the crosswalk, a dog that arrives second must wait");
        }

        [Test]
        public void ReleasesEachCrosswalkOnceFullyPast_SoALaterDogMayCross()
        {
            var gate = new RoadCrossingGate();
            var road = NorthSouthRoad();
            var truck = new object();
            var traversal = new RoadCrossingTraversal(gate, truck, road, NeighborhoodLayout.WalkNetwork, 30f, -30f);

            var north = CrosswalkAt(road, 4.75f);
            var nearEdge = 4.75f + HalfCrosswalk;

            // Claim the north crosswalk at its boundary...
            traversal.Advance(nearEdge, -30f);
            Assert.That(gate.TryEnter(north, new object()), Is.False, "truck holds it mid-crossing");

            // ...then drive well past its far edge; the claim releases.
            var pastFarEdge = 4.75f - HalfCrosswalk - 0.5f; // beyond the -Z (far) side
            traversal.Advance(pastFarEdge, -30f);

            var dog = new object();
            Assert.That(gate.TryEnter(north, dog), Is.True,
                "once the vehicle is fully past a crosswalk it releases the claim, so a dog may cross");
        }

        [Test]
        public void WithNoCrosswalkAhead_TheFullTargetIsAllowed()
        {
            var gate = new RoadCrossingGate();
            var road = NorthSouthRoad();
            var truck = new object();
            var traversal = new RoadCrossingTraversal(gate, truck, road, NeighborhoodLayout.WalkNetwork, 30f, -30f);

            // Driving from +30 down to a stop at +14: both crosswalks (+/-4.75)
            // are beyond the stop, so nothing clamps the approach.
            var allowed = traversal.Advance(30f, 14f);
            Assert.That(allowed, Is.EqualTo(14f).Within(0.001f),
                "an inbound leg that stops short of every crosswalk reaches its stop unobstructed");
        }

        [Test]
        public void ReleaseAll_DropsEveryClaimTheVehicleStillHolds()
        {
            var gate = new RoadCrossingGate();
            var road = NorthSouthRoad();
            var truck = new object();
            var traversal = new RoadCrossingTraversal(gate, truck, road, NeighborhoodLayout.WalkNetwork, 30f, -30f);

            var north = CrosswalkAt(road, 4.75f);
            traversal.Advance(4.75f + HalfCrosswalk, -30f); // claim north
            Assert.That(gate.TryEnter(north, new object()), Is.False);

            traversal.ReleaseAll();

            Assert.That(gate.TryEnter(north, new object()), Is.True,
                "ReleaseAll frees claims a vehicle abandoned (e.g. when its view is destroyed mid-route)");
        }
    }
}
