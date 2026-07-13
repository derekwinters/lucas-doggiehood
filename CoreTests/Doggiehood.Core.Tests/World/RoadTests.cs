using Doggiehood.Core.World;
using NUnit.Framework;

namespace Doggiehood.Core.Tests.World
{
    /// <summary>
    /// #106: a Road is a finite straight road segment. Every road declares
    /// a sidewalk on both sides, built purely from the locked #105
    /// WorldDimensions constants. (The grass verge between road and
    /// sidewalk is 0m since Derek's 2026-07-13 decision — sidewalks abut
    /// the road directly, aligning with the City Kit tiles, #121/#122.)
    /// </summary>
    public class RoadTests
    {
        [Test]
        public void Road_ExposesItsOrientationCenterAndHalfLength()
        {
            var road = new Road(StreetOrientation.NorthSouth, new GridPoint(0f, 0f), 26f);

            Assert.That(road.Orientation, Is.EqualTo(StreetOrientation.NorthSouth));
            Assert.That(road.Center, Is.EqualTo(new GridPoint(0f, 0f)));
            Assert.That(road.HalfLength, Is.EqualTo(26f));
        }

        [Test]
        public void Road_WidthComesFromWorldDimensions()
        {
            var road = new Road(StreetOrientation.NorthSouth, new GridPoint(0f, 0f), 26f);

            Assert.That(road.Width, Is.EqualTo(WorldDimensions.RoadWidth));
        }

        [Test]
        public void Road_DeclaresExactlyTwoSidewalks_OnePerSide()
        {
            var road = new Road(StreetOrientation.NorthSouth, new GridPoint(0f, 0f), 26f);

            Assert.That(road.Sidewalks.Count, Is.EqualTo(2));
            Assert.That(road.Sidewalks[0].Side, Is.Not.EqualTo(road.Sidewalks[1].Side));
        }

        [Test]
        public void Sidewalks_AreSymmetric_UsingOnlyTheLockedWorldDimensions()
        {
            // #105/#106: sidewalk offset from the road centerline is
            // derived purely from RoadWidth, GrassVergeWidth, and
            // SidewalkWidth — no new literals.
            var road = new Road(StreetOrientation.NorthSouth, new GridPoint(0f, 0f), 26f);
            var expectedOffset = WorldDimensions.RoadWidth / 2f
                + WorldDimensions.GrassVergeWidth
                + WorldDimensions.SidewalkWidth / 2f;

            var positive = road.Sidewalks[0].Side == RoadSide.Positive ? road.Sidewalks[0] : road.Sidewalks[1];
            var negative = road.Sidewalks[0].Side == RoadSide.Negative ? road.Sidewalks[0] : road.Sidewalks[1];

            Assert.That(positive.CenterOffset, Is.EqualTo(expectedOffset).Within(0.0001f));
            Assert.That(negative.CenterOffset, Is.EqualTo(-expectedOffset).Within(0.0001f));
            Assert.That(positive.Width, Is.EqualTo(WorldDimensions.SidewalkWidth));
            Assert.That(positive.VergeWidth, Is.EqualTo(WorldDimensions.GrassVergeWidth));
        }

        [Test]
        public void Sidewalk_PointsSitOutsideTheRoad_OnTheCorrectAxis()
        {
            // North-south road: sidewalk offset is on X; a point at the
            // sidewalk's along-axis coordinate 0 sits at (offset, 0).
            var road = new Road(StreetOrientation.NorthSouth, new GridPoint(0f, 0f), 26f);
            var positive = road.Sidewalks[0].Side == RoadSide.Positive ? road.Sidewalks[0] : road.Sidewalks[1];

            var point = road.PointAt(alongAxis: 10f, perpendicularOffset: positive.CenterOffset);

            Assert.That(point.X, Is.EqualTo(positive.CenterOffset).Within(0.0001f));
            Assert.That(point.Z, Is.EqualTo(10f).Within(0.0001f));
            Assert.That(point.X, Is.GreaterThan(road.Width / 2f + WorldDimensions.GrassVergeWidth),
                "sidewalk centerline must sit beyond the road edge (verge is 0m since 2026-07-13)");
        }
    }
}
