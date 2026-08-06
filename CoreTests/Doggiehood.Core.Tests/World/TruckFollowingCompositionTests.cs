using System.Linq;
using Doggiehood.Core.World;
using NUnit.Framework;

namespace Doggiehood.Core.Tests.World
{
    /// <summary>
    /// #600: the along-road following rule (<see cref="CarFollowing"/>) composes
    /// on top of the existing crosswalk gate (<see cref="RoadCrossingGate"/> /
    /// <see cref="RoadCrossingTraversal"/>) for two trucks queued on one road.
    /// The gate arbitrates the crosswalk claim first-come; the following rule
    /// keeps the follower physically one car length behind the leader on the
    /// approach. When the leader releases the crosswalk and pulls away, the
    /// follower closes the gap and then claims the crosswalk itself.
    /// </summary>
    public class TruckFollowingCompositionTests
    {
        [Test]
        public void TruckFollowsTruck_FirstComeCrosswalk_ThenFollowerClosesGapAndClaimsItself()
        {
            var gate = new RoadCrossingGate();
            var road = NeighborhoodLayout.Roads.First(r => r.Orientation == StreetOrientation.NorthSouth);
            var network = NeighborhoodLayout.WalkNetwork;
            var leader = new object();
            var follower = new object();

            // Both trucks drive north(+30) -> south(-30): the along-coordinate
            // decreases, so the travel sign is -1.
            var leaderTraversal = new RoadCrossingTraversal(gate, leader, road, network, 30f, -30f);
            var north = CrosswalkAt(road, network, 4.75f);
            var halfCrosswalk = WorldDimensions.CrosswalkWidth / 2f;
            var nearEdge = 4.75f + halfCrosswalk; // the +Z (near) side, approached driving -Z

            // The leader reaches the crosswalk boundary and claims it.
            leaderTraversal.Advance(nearEdge, -30f);
            Assert.That(gate.TryEnter(north, follower), Is.False,
                "truck<->truck at a crosswalk is first-come: the second truck is denied while the leader holds it");

            // The following rule keeps the follower one car length behind the
            // stopped leader on the approach — the gate never governs the body gap.
            var followModel = new CarFollowing(travelSign: -1f);
            var leaderAlong = nearEdge; // the leader sits at the near edge holding the crosswalk
            var followerStart = leaderAlong + CarFollowing.GapMeters + 3f; // further back
            var held = followModel.Advance(followerStart, -30f, leaderAlong, 0.1f);
            Assert.That(held, Is.EqualTo(leaderAlong + CarFollowing.GapMeters).Within(0.001f),
                "the follower drives up to exactly one car length behind the stopped leader, no closer");

            // A second stopped-leader tick establishes it as stopped (arming the
            // start-up delay for when it moves).
            followModel.Advance(held, -30f, leaderAlong, 0.1f);

            // The leader clears the crosswalk's far edge and releases the claim.
            leaderTraversal.Advance(4.75f - halfCrosswalk - 1f, -30f);
            Assert.That(gate.TryEnter(north, follower), Is.True,
                "once the leader releases, the follower may claim the crosswalk itself");

            // The leader pulls away; after the one-second start-up delay the
            // follower closes the gap it was holding.
            var movingLeaderAlong = leaderAlong;
            var pos = held;
            for (var t = 0f; t < CarFollowing.StartUpDelaySeconds + 0.2f; t += 0.1f)
            {
                movingLeaderAlong -= 2f; // leader drives on, gap opens up
                pos = followModel.Advance(pos, -30f, movingLeaderAlong, 0.1f);
            }

            Assert.That(pos, Is.LessThan(held - 0.001f),
                "after the start-up delay the follower resumes and closes the gap the leader opened");
        }

        private static WalkEdge CrosswalkAt(Road road, WalkNetwork network, float along)
        {
            return network.Edges.Single(e =>
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
    }
}
