using Doggiehood.Core.World;
using Doggiehood.Unity;
using NUnit.Framework;
using UnityEngine;

namespace Doggiehood.Unity.EditModeTests
{
    /// <summary>
    /// #600: the thin view side of car-following. Two delivery trucks derive
    /// separate <see cref="Road"/> instances for the same physical segment, so the
    /// view exposes a value-based segment key that matches across them; and the
    /// leader-aware <see cref="DeliveryTruckView.Tick(float, float?)"/> holds a
    /// follower one car length behind the truck ahead (the Core
    /// <see cref="CarFollowing"/> decision, converted to/from along-road here),
    /// while an open road (null leader) imposes no such clamp.
    /// </summary>
    public class TruckCarFollowingTests
    {
        private static TileMap OriginMap()
        {
            return new TileMap(new TileCoordinate(0, 0), TileType.FourWay);
        }

        private static WalkNetwork OriginNetwork()
        {
            return NeighborhoodLayout.WalkNetwork;
        }

        private static readonly Vector3 Door = new Vector3(
            NeighborhoodLayout.LotDistanceFromCenter, 0f, NeighborhoodLayout.LotDistanceFromCenter);

        [SetUp]
        public void Reset()
        {
            RoadCrossingGate.Shared.Clear();
            DeliveryTruckView.ForcePrimitiveFallback = false;
        }

        [Test]
        public void TwoTrucksOnTheSameRoad_ShareAValueEqualSegmentKey()
        {
            var root = new GameObject("truck-follow-root");
            try
            {
                var leader = DeliveryTruckView.Spawn(root.transform);
                var follower = DeliveryTruckView.Spawn(root.transform);
                leader.DeliverTo(Door, OriginMap(), OriginNetwork(), () => { });
                follower.DeliverTo(Door, OriginMap(), OriginNetwork(), () => { });

                Assert.That(leader.IsDriving, Is.True, "the leader is on a road leg after DeliverTo");
                Assert.That(follower.IsDriving, Is.True, "the follower is on a road leg after DeliverTo");
                Assert.That(follower.CurrentSegmentKey, Is.EqualTo(leader.CurrentSegmentKey),
                    "trucks on the same physical road must key equal despite separate Road instances, "
                    + "so the owner can group them for single-file following");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void AFollowerExactlyOneCarLengthBehind_HoldsPosition_WhenTickedWithThatLeader()
        {
            var root = new GameObject("truck-follow-root");
            try
            {
                var follower = DeliveryTruckView.Spawn(root.transform);
                follower.DeliverTo(Door, OriginMap(), OriginNetwork(), () => { });

                var startAlong = follower.CurrentAlong;
                // A leader sitting exactly one car length ahead in the travel
                // direction — the follower must not advance into that gap.
                var leaderAlong = startAlong + follower.TravelSign * CarFollowing.GapMeters;

                follower.Tick(0.1f, leaderAlong);

                Assert.That(follower.CurrentAlong, Is.EqualTo(startAlong).Within(0.001f),
                    "the follower holds when the leader is exactly one car length ahead");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void WithNoLeaderAhead_TheFollowerDrivesForwardNormally()
        {
            var root = new GameObject("truck-follow-root");
            try
            {
                var truck = DeliveryTruckView.Spawn(root.transform);
                truck.DeliverTo(Door, OriginMap(), OriginNetwork(), () => { });

                var startAlong = truck.CurrentAlong;

                truck.Tick(0.1f, null);

                var advanced = (truck.CurrentAlong - startAlong) * truck.TravelSign;
                Assert.That(advanced, Is.GreaterThan(0.001f),
                    "an open road ahead (null leader) imposes no following clamp — the truck drives on");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }
    }
}
