using Doggiehood.Core.Art;
using NUnit.Framework;

namespace Doggiehood.Core.Tests.Art
{
    public class ArtBudgetTests
    {
        [Test]
        public void MaxTrianglesPerMesh_IsTheAgreedLowPolyBudget()
        {
            // #6: the low-poly budget every imported mesh must stay under.
            // 2000 triangles is generous for low-poly houses/dogs/props but
            // firmly rules out high-poly/realistic assets.
            Assert.That(ArtBudget.MaxTrianglesPerMesh, Is.EqualTo(2000));
        }

        [TestCase(0, true)]
        [TestCase(1999, true)]
        [TestCase(2000, true)]
        [TestCase(2001, false)]
        [TestCase(50000, false)]
        public void IsWithinBudget_ComparesTriangleCountAgainstTheBudget(int triangles, bool expected)
        {
            Assert.That(ArtBudget.IsWithinBudget(triangles), Is.EqualTo(expected));
        }
    }
}
