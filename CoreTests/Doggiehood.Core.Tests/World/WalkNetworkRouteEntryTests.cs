using System;
using Doggiehood.Core.World;
using NUnit.Framework;

namespace Doggiehood.Core.Tests.World
{
    /// <summary>
    /// #677: the two primitives a walk-home route needs so it can neither enter
    /// the network by cutting across open ground nor silently arrive somewhere
    /// other than where it was aimed.
    ///
    /// <see cref="WalkNetwork.FindPath"/> snaps BOTH endpoints to the nearest
    /// node, which is right for a destination that is deliberately off-graph (a
    /// yard decoration) and wrong for one that must be an exact node (a front
    /// door): a lookup miss became a route to the nearest sidewalk instead of a
    /// failure. <see cref="WalkNetwork.FindPathBetween"/> is the strict form, and
    /// <see cref="WalkNetwork.TryProjectOntoNearestEdge"/> is the entry-side
    /// counterpart — nearest point on a walk EDGE rather than nearest node.
    /// </summary>
    public class WalkNetworkRouteEntryTests
    {
        private const float Tolerance = 0.01f;

        [Test]
        public void TryProjectOntoNearestEdge_ReturnsThePointOnTheEdgeUnderfoot_NotADistantNode()
        {
            var network = NeighborhoodLayout.WalkNetwork;

            // Mid-way along the west sidewalk arm north of the intersection: on an
            // edge, but far from either of that edge's nodes.
            var standing = new GridPoint(-4.75f, 22f);

            Assert.That(network.TryProjectOntoNearestEdge(standing, out var foot, out var edge), Is.True);
            Assert.That(Distance(standing, foot), Is.LessThan(Tolerance),
                "a dog standing on an edge projects onto its own position");
            Assert.That(edge.Kind, Is.EqualTo(WalkEdgeKind.Sidewalk));
            Assert.That(Distance(standing, network.NearestNode(standing)), Is.GreaterThan(5f),
                "test sanity: the nearest NODE is far off, so edge projection is materially different");
        }

        [Test]
        public void TryProjectOntoNearestEdge_ProjectsAnOffNetworkPoint_OntoTheClosestPavedEdge()
        {
            var network = NeighborhoodLayout.WalkNetwork;

            // Out in a yard, 5m east of the west sidewalk arm.
            var standing = new GridPoint(-9.75f, 22f);

            Assert.That(network.TryProjectOntoNearestEdge(standing, out var foot, out _), Is.True);
            Assert.That(foot.X, Is.EqualTo(-4.75f).Within(Tolerance));
            Assert.That(foot.Z, Is.EqualTo(22f).Within(Tolerance),
                "the projection is perpendicular — the shortest possible step back onto the pavement");
        }

        [Test]
        public void TryProjectOntoNearestEdge_IsFalse_OnAnEdgelessNetwork()
        {
            var empty = WalkNetwork.BuildFrom(Array.Empty<Road>(), Array.Empty<HouseLot>());

            Assert.That(empty.TryProjectOntoNearestEdge(new GridPoint(0f, 0f), out _, out _), Is.False);
        }

        [Test]
        public void SegmentStaysOnPavement_IsTrueAlongAnEdge_AndFalseAcrossOpenGround()
        {
            var network = NeighborhoodLayout.WalkNetwork;

            // Both ends on the west sidewalk arm running from house 2's walkway
            // attach (-4.75, 14) up to the street's north tip (-4.75, 30).
            Assert.That(network.SegmentStaysOnPavement(new GridPoint(-4.75f, 16f), new GridPoint(-4.75f, 28f)),
                Is.True, "a hop along one sidewalk edge stays on the pavement");

            // The diagonal a beelining dog cut: from the north-west sidewalk
            // across the lawns and the roadway to the east sidewalk.
            Assert.That(network.SegmentStaysOnPavement(new GridPoint(-4.75f, 28f), new GridPoint(4.75f, 16f)),
                Is.False, "a diagonal across the yards and roadway is not on the pavement");
        }

        [Test]
        public void FindPathBetween_RefusesAPointThatIsNotAGraphNode_RatherThanSnappingIt()
        {
            var network = NeighborhoodLayout.WalkNetwork;
            Assert.That(network.TryGetFrontWalkway(3, out var walkway), Is.True);

            Assert.That(
                () => network.FindPathBetween(new GridPoint(999f, 999f), walkway.A),
                Throws.InstanceOf<ArgumentException>(),
                "the strict path refuses a non-node endpoint instead of quietly routing somewhere else");
        }

        [Test]
        public void FindPathBetween_BetweenTwoRealNodes_ReturnsThePathFindPathWouldHaveFound()
        {
            var network = NeighborhoodLayout.WalkNetwork;
            Assert.That(network.TryGetFrontWalkway(3, out var walkway), Is.True);
            var start = new GridPoint(-4.75f, 30f);

            var strict = network.FindPathBetween(start, walkway.A);
            var snapped = network.FindPath(start, walkway.A);

            Assert.That(strict, Is.EqualTo(snapped),
                "for endpoints that ARE nodes the strict form changes nothing");
        }

        private static float Distance(GridPoint a, GridPoint b)
        {
            var dx = a.X - b.X;
            var dz = a.Z - b.Z;
            return (float)Math.Sqrt(dx * dx + dz * dz);
        }
    }
}
