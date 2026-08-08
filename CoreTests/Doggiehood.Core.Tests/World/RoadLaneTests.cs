using Doggiehood.Core.World;
using NUnit.Framework;

namespace Doggiehood.Core.Tests.World
{
    /// <summary>
    /// #672: a road has two lanes, and a vehicle keeps to the RIGHT-hand one for
    /// its direction of travel. Before this the road model had no lane concept at
    /// all — every drivable road position was the centerline — so the delivery
    /// truck straddled the middle of the street and left no room for oncoming
    /// traffic.
    ///
    /// The sign convention is the whole risk here. <see cref="Road.PointAt"/>'s
    /// perpendicular offset means +X on a north-south road but +Z on an east-west
    /// one, while "right" is the right-hand normal of the HEADING — so the sign
    /// flips between the two orientations. An implementation that gets it right on
    /// one street and backwards on the other looks perfectly correct until you
    /// drive the other way, which is why every orientation x travel-sign case is
    /// pinned here rather than spot-checked. The expected side is derived in the
    /// test from the right-hand normal of the heading, independently of how
    /// <see cref="RoadLane"/> computes it.
    /// </summary>
    public class RoadLaneTests
    {
        private const float Tolerance = 0.0001f;

        private const float HalfLength = 30f;

        // Right-hand normal of a heading (dx, dz) in Unity's XZ plane: heading +Z
        // (north) has its right hand pointing +X (east). Written out here so the
        // expectation is an independent derivation, not a restatement of the
        // production formula.
        private static (float X, float Z) RightHandNormal(float headingX, float headingZ)
        {
            return (headingZ, -headingX);
        }

        private static Road RoadAt(StreetOrientation orientation)
        {
            return new Road(orientation, new GridPoint(0f, 0f), HalfLength);
        }

        // The heading a truck drives on a road of this orientation at this travel
        // sign: along the road's own axis (Z for north-south, X for east-west).
        private static (float X, float Z) Heading(StreetOrientation orientation, float travelSign)
        {
            return orientation == StreetOrientation.NorthSouth
                ? (0f, travelSign)
                : (travelSign, 0f);
        }

        [Test]
        public void LaneOffset_IsAQuarterOfTheRoadWidth_DerivedNotHardcoded()
        {
            // The right lane's centre is halfway between the centerline and the
            // road edge: (RoadWidth / 2) / 2. Named on the road model (#161) so a
            // road-width change carries the lane with it — and so any future road
            // user inherits the rule instead of re-deriving it.
            Assert.That(RoadLane.Offset,
                Is.EqualTo(WorldDimensions.RoadWidth / 4f).Within(Tolerance));
        }

        [TestCase(StreetOrientation.NorthSouth, 1f)]
        [TestCase(StreetOrientation.NorthSouth, -1f)]
        [TestCase(StreetOrientation.EastWest, 1f)]
        [TestCase(StreetOrientation.EastWest, -1f)]
        public void LanePoint_SitsOneLaneOffsetToTheRightOfTheCenterline(
            StreetOrientation orientation, float travelSign)
        {
            var road = RoadAt(orientation);
            var heading = Heading(orientation, travelSign);
            var right = RightHandNormal(heading.X, heading.Z);

            const float along = 7f;
            var centerline = road.PointAt(along, 0f);
            var lane = road.LanePointAt(along, travelSign);

            Assert.That(lane.X,
                Is.EqualTo(centerline.X + right.X * RoadLane.Offset).Within(Tolerance),
                "lane centre must sit to the right of the centerline for the travel direction");
            Assert.That(lane.Z,
                Is.EqualTo(centerline.Z + right.Z * RoadLane.Offset).Within(Tolerance),
                "lane centre must sit to the right of the centerline for the travel direction");
        }

        [TestCase(StreetOrientation.NorthSouth)]
        [TestCase(StreetOrientation.EastWest)]
        public void ReversingTravelDirection_PutsTheTruckOnTheOtherSideOfTheCenterline(
            StreetOrientation orientation)
        {
            // "Right" is relative to travel, not to the road's axis: a truck that
            // turns around on the same road swaps lanes.
            var road = RoadAt(orientation);

            var forward = RoadLane.PerpendicularOffsetFor(orientation, 1f);
            var backward = RoadLane.PerpendicularOffsetFor(orientation, -1f);

            Assert.That(forward, Is.EqualTo(-backward).Within(Tolerance),
                "reversing must mirror the lateral offset about the centerline");
            Assert.That(forward * backward, Is.LessThan(0f),
                "the two directions must sit on opposite sides, not the same one");
        }

        [TestCase(StreetOrientation.NorthSouth, 1f)]
        [TestCase(StreetOrientation.NorthSouth, -1f)]
        [TestCase(StreetOrientation.EastWest, 1f)]
        [TestCase(StreetOrientation.EastWest, -1f)]
        public void ALaneNeverCrossesTheCenterline_AnywhereAlongAStraightLeg(
            StreetOrientation orientation, float travelSign)
        {
            // The invariant (#672): on a road leg the vehicle stays entirely in
            // the right-hand half of the roadway — so the SIGN of its lateral
            // offset never changes and is never zero, at any point along the leg.
            var road = RoadAt(orientation);
            var expectedSign = RoadLane.PerpendicularOffsetFor(orientation, travelSign) < 0f ? -1f : 1f;

            for (var along = -HalfLength; along <= HalfLength; along += 1f)
            {
                var lane = road.LanePointAt(along, travelSign);
                var lateral = orientation == StreetOrientation.NorthSouth
                    ? lane.X - road.Center.X
                    : lane.Z - road.Center.Z;

                Assert.That(lateral * expectedSign, Is.GreaterThan(0f),
                    $"at along={along} the lane crossed to the wrong side of the centerline");
                Assert.That(System.Math.Abs(lateral),
                    Is.EqualTo(RoadLane.Offset).Within(Tolerance),
                    $"at along={along} the lane drifted off its own lane centre");
            }
        }

        [TestCase(StreetOrientation.NorthSouth, 1f)]
        [TestCase(StreetOrientation.NorthSouth, -1f)]
        [TestCase(StreetOrientation.EastWest, 1f)]
        [TestCase(StreetOrientation.EastWest, -1f)]
        public void ALanePointIsStillOnTheRoadway(StreetOrientation orientation, float travelSign)
        {
            // The #538 invariant is preserved, not traded away: shifting into the
            // lane must keep every driven point on the paved surface.
            var road = RoadAt(orientation);

            for (var along = -HalfLength; along <= HalfLength; along += 1f)
            {
                Assert.That(road.Contains(road.LanePointAt(along, travelSign)), Is.True,
                    $"lane point at along={along} left the roadway");
            }
        }
    }
}
