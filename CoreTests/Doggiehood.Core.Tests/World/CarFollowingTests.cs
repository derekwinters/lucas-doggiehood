using Doggiehood.Core.World;
using NUnit.Framework;

namespace Doggiehood.Core.Tests.World
{
    /// <summary>
    /// #600: 1-D car-following on a shared road segment. When more than one
    /// delivery truck drives the same segment single-file, a follower must keep
    /// one car length behind the truck ahead (the crosswalk gate only arbitrates
    /// the crosswalk claim, never the body gap on the approach), and — matching a
    /// real driver's reaction time — must wait one second after a stopped leader
    /// begins moving before it resumes. All expressed in along-road coordinates
    /// so the thin Unity view only converts positions and drives.
    /// </summary>
    public class CarFollowingTests
    {
        private const float Gap = CarFollowing.GapMeters;

        [Test]
        public void MaintainsGap_BehindAMovingLeader_NeverAdvancingPastLeaderMinusGap()
        {
            var following = new CarFollowing(travelSign: 1f);

            // The follower wants to reach the leader's position but is clamped to
            // exactly one car length behind it.
            var allowed = following.Advance(
                currentAlong: 5f, targetAlong: 20f, leaderAlong: 20f, deltaTime: 0.1f);

            Assert.That(allowed, Is.EqualTo(20f - Gap).Within(0.001f),
                "the follower holds exactly one car length behind the leader");
        }

        [Test]
        public void AContinuouslyMovingLeader_NeverArmsAStartUpDelay_GapHeldEveryTick()
        {
            var following = new CarFollowing(travelSign: 1f);
            var leader = 20f;
            var follower = 5f;

            for (var i = 0; i < 5; i++)
            {
                follower = following.Advance(follower, targetAlong: leader, leaderAlong: leader, deltaTime: 0.1f);
                Assert.That(follower, Is.EqualTo(leader - Gap).Within(0.001f),
                    "a leader that was already moving triggers no start-up wait — the gap is tracked every tick");
                leader += 1f;
            }
        }

        [Test]
        public void HoldsBehindAStoppedLeader_NeverEncroachingOnItsBuffer()
        {
            var following = new CarFollowing(travelSign: 1f);

            var allowed = following.Advance(
                currentAlong: 2f, targetAlong: 10f, leaderAlong: 10f, deltaTime: 0.1f);
            Assert.That(allowed, Is.EqualTo(10f - Gap).Within(0.001f),
                "the follower stops one car length short of the stopped leader");

            allowed = following.Advance(
                currentAlong: allowed, targetAlong: 10f, leaderAlong: 10f, deltaTime: 0.1f);
            Assert.That(allowed, Is.EqualTo(10f - Gap).Within(0.001f),
                "and stays pinned there while the leader remains stopped");
        }

        [Test]
        public void WhenAStoppedLeaderBeginsMoving_TheFollowerHoldsForOneSecondThenResumes()
        {
            var following = new CarFollowing(travelSign: 1f);
            const float dt = 0.25f;
            var pinned = 10f - Gap;

            // Two ticks establish the leader as stopped and pin the follower.
            following.Advance(pinned, 10f, 10f, dt);
            following.Advance(pinned, 10f, 10f, dt);

            // The leader pulls away; for a full second the follower must not move,
            // even though the gap has opened up.
            var leader = 10f;
            var elapsed = 0f;
            while (elapsed < CarFollowing.StartUpDelaySeconds - 0.0001f)
            {
                leader += 1f;
                var held = following.Advance(pinned, leader, leader, dt);
                Assert.That(held, Is.EqualTo(pinned).Within(0.001f),
                    "during the one-second start-up delay the follower holds position");
                elapsed += dt;
            }

            // After the second has elapsed, the follower resumes and closes the gap.
            leader += 1f;
            var resumed = following.Advance(pinned, leader, leader, dt);
            Assert.That(resumed, Is.GreaterThan(pinned + 0.001f),
                "once the one-second delay elapses the follower resumes and closes the gap");
            Assert.That(resumed, Is.LessThanOrEqualTo(leader - Gap + 0.001f),
                "but still never closer than one car length behind the leader");
        }

        [Test]
        public void WithNoLeaderAhead_TheFullTargetIsAllowed()
        {
            var following = new CarFollowing(travelSign: 1f);

            var allowed = following.Advance(
                currentAlong: 0f, targetAlong: 25f, leaderAlong: null, deltaTime: 0.1f);

            Assert.That(allowed, Is.EqualTo(25f).Within(0.001f),
                "an open road ahead imposes no following clamp");
        }

        [Test]
        public void TravelSignIsHonoured_FollowingWorksDrivingInTheNegativeDirection()
        {
            var following = new CarFollowing(travelSign: -1f);

            // Driving toward -along: "ahead" is a smaller along, so the follower
            // (larger along) is clamped to leaderAlong + one car length.
            var allowed = following.Advance(
                currentAlong: 20f, targetAlong: -30f, leaderAlong: 5f, deltaTime: 0.1f);

            Assert.That(allowed, Is.EqualTo(5f + Gap).Within(0.001f),
                "the follower holds one car length behind the leader in the travel direction");
        }
    }
}
