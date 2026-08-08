using Doggiehood.Core.World;
using NUnit.Framework;

namespace Doggiehood.Core.Tests.World
{
    /// <summary>
    /// #600: the along-road membership rule that picks each follower's immediate
    /// leader from the set of active trucks. Single file per road segment and per
    /// travel direction — a truck is only constrained by the nearest truck ahead
    /// of it on its own segment; trucks on other segments (or driving the other
    /// way) never constrain it. This is the "owns the set" seam the Unity tick
    /// loop feeds each tick; all decision logic stays here in Core.
    /// </summary>
    public class RoadTrafficTests
    {
        [Test]
        public void ImmediateLeader_IsTheNearestVehicleAheadOnTheSameSegmentAndDirection()
        {
            var segment = new object();
            var others = new[]
            {
                ((object)segment, 1f, 20f), // further ahead
                ((object)segment, 1f, 12f), // the immediate leader
            };

            var leader = RoadTraffic.ImmediateLeaderAlong(segment, 1f, 6f, others);

            Assert.That(leader, Is.EqualTo(12f).Within(0.001f),
                "the immediate leader is the nearest truck ahead, not the furthest");
        }

        [Test]
        public void ThreeTrucksSingleFile_EachFollowsItsImmediateLeader_TheHeadFollowsNoOne()
        {
            var seg = new object();
            const float a = 20f; // head
            const float b = 12f; // middle
            const float c = 6f;  // tail
            var all = new[]
            {
                ((object)seg, 1f, a),
                ((object)seg, 1f, b),
                ((object)seg, 1f, c),
            };

            Assert.That(RoadTraffic.ImmediateLeaderAlong(seg, 1f, c, all), Is.EqualTo(b).Within(0.001f),
                "the tail truck follows the middle truck");
            Assert.That(RoadTraffic.ImmediateLeaderAlong(seg, 1f, b, all), Is.EqualTo(a).Within(0.001f),
                "the middle truck follows the head truck");
            Assert.That(RoadTraffic.ImmediateLeaderAlong(seg, 1f, a, all), Is.Null,
                "the head truck has no one ahead of it");
        }

        [Test]
        public void ATruckOnADifferentSegment_IsNotALeader()
        {
            var seg = new object();
            var otherSeg = new object();
            var all = new[] { ((object)otherSeg, 1f, 20f) };

            Assert.That(RoadTraffic.ImmediateLeaderAlong(seg, 1f, 6f, all), Is.Null,
                "a truck on another road segment never constrains this one");
        }

        [Test]
        public void ATruckDrivingTheOtherWay_IsNotALeader_SingleFilePerDirection()
        {
            var seg = new object();
            var all = new[] { ((object)seg, -1f, 20f) };

            Assert.That(RoadTraffic.ImmediateLeaderAlong(seg, 1f, 6f, all), Is.Null,
                "an oncoming truck is not a leader — following is single-file per direction");
        }

        [Test]
        public void OpposingTrucksAreInDifferentLanes_AndStillDoNotFollowEachOther()
        {
            // #672 regression guard for #600. Now that a road has two lanes, the
            // "single-file per direction" rule has a physical reading: opposing
            // trucks are on opposite sides of the centerline, so one is never
            // behind the other in any meaningful sense. Both halves are pinned
            // together here so a later change can't quietly make an oncoming truck
            // a leader — which would have a truck braking for traffic in the lane
            // beside it — while same-direction queuing keeps working.
            var seg = new object();
            var oncoming = new[] { ((object)seg, -1f, 20f) };
            var ahead = new[] { ((object)seg, 1f, 20f) };

            Assert.That(RoadLane.PerpendicularOffsetFor(StreetOrientation.NorthSouth, 1f)
                        * RoadLane.PerpendicularOffsetFor(StreetOrientation.NorthSouth, -1f),
                Is.LessThan(0f), "opposing traffic occupies opposite lanes");

            Assert.That(RoadTraffic.ImmediateLeaderAlong(seg, 1f, 6f, oncoming), Is.Null,
                "a truck in the oncoming lane never constrains this one");
            Assert.That(RoadTraffic.ImmediateLeaderAlong(seg, 1f, 6f, ahead), Is.EqualTo(20f).Within(0.001f),
                "a truck ahead in the SAME lane still does");
        }
    }
}
