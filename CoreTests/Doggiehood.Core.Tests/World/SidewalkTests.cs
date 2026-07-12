using Doggiehood.Core.World;
using NUnit.Framework;

namespace Doggiehood.Core.Tests.World
{
    /// <summary>
    /// #106: Sidewalk cross-section geometry, exercised on an east-west
    /// road to prove the offset math isn't hardcoded to one axis.
    /// </summary>
    public class SidewalkTests
    {
        [Test]
        public void EastWestRoad_SidewalksOffsetOnZ_NotX()
        {
            var road = new Road(StreetOrientation.EastWest, new GridPoint(0f, 0f), 26f);
            var positive = road.Sidewalks[0].Side == RoadSide.Positive ? road.Sidewalks[0] : road.Sidewalks[1];

            var point = road.PointAt(alongAxis: 10f, perpendicularOffset: positive.CenterOffset);

            Assert.That(point.Z, Is.EqualTo(positive.CenterOffset).Within(0.0001f));
            Assert.That(point.X, Is.EqualTo(10f).Within(0.0001f));
        }

        [Test]
        public void Sidewalk_KnowsWhichRoadItBelongsTo()
        {
            var road = new Road(StreetOrientation.EastWest, new GridPoint(0f, 0f), 26f);

            foreach (var sidewalk in road.Sidewalks)
            {
                Assert.That(sidewalk.Road, Is.SameAs(road));
            }
        }
    }
}
