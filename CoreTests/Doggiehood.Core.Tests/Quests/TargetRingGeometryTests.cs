using System;
using Doggiehood.Core.Quests;
using Doggiehood.Core.World;
using NUnit.Framework;

namespace Doggiehood.Core.Tests.Quests
{
    /// <summary>
    /// #669: how big a red target ring has to be to actually frame the thing it
    /// marks. The onboarding target-house highlight (#571) sized its ring from
    /// the house's LONGEST SIDE (<c>1.15 · max(x, z)</c>), but a circle has to
    /// clear the footprint's DIAGONAL to contain it — for a square house the
    /// corners reach <c>1.414·w</c>, so the house covered the ring on all four
    /// diagonals by construction and it read as a few red slivers. The band is
    /// also hollow, so the edge that has to clear the house is the ring's INNER
    /// edge, not its outer one.
    ///
    /// <para>The sizing rule lives in Core because it is a rule, not a rendering
    /// detail — and so the build-step foundation highlight (#668) can reuse it
    /// rather than re-derive its own numbers.</para>
    /// </summary>
    public class TargetRingGeometryTests
    {
        /// <summary>The circumscribed radius of an <c>x × z</c> footprint — the
        /// corner-to-centre reach the ring has to clear.</summary>
        private static float CircumscribedRadius(float x, float z)
        {
            return 0.5f * (float)Math.Sqrt((x * x) + (z * z));
        }

        /// <summary>The radius of the ring's visible near edge (the hole), for a
        /// ring drawn at <paramref name="outerDiameter"/>.</summary>
        private static float InnerRadius(float outerDiameter)
        {
            return 0.5f * TargetRingGeometry.InnerDiameter(outerDiameter);
        }

        [TestCase(1f, 1f, TestName = "square footprint")]
        [TestCase(12.5f, 9.6f, TestName = "a real max-level house footprint")]
        [TestCase(20f, 4f, TestName = "strongly rectangular footprint")]
        [TestCase(4f, 20f, TestName = "strongly rectangular footprint, other axis")]
        public void OuterDiameter_LeavesTheWholeFootprint_CornersIncluded_InsideTheRingsHole(
            float x, float z)
        {
            // The invariant: the ring's INNER edge lies outside the target's
            // footprint. Corners included — so the bound is the circumscribed
            // radius, not half the longest side.
            var inner = InnerRadius(TargetRingGeometry.OuterDiameter(x, z));

            Assert.That(inner, Is.GreaterThan(CircumscribedRadius(x, z)),
                "the whole footprint, corners included, sits inside the ring's hole");
        }

        [TestCase(1f, 1f)]
        [TestCase(12.5f, 9.6f)]
        [TestCase(20f, 4f)]
        public void OuterDiameter_LeavesTheNamedProportionalGapOfOpenGround(float x, float z)
        {
            // Containment alone would allow a ring hugging the corners. The gap
            // is a single named constant (#161) and is proportional, so every
            // house variant/level gets the same visible band of ground between
            // the mesh and the ring.
            var circumscribed = CircumscribedRadius(x, z);
            var inner = InnerRadius(TargetRingGeometry.OuterDiameter(x, z));

            Assert.That(TargetRingGeometry.FootprintGapFraction, Is.GreaterThan(0f),
                "the gap is non-zero — the ring frames the house, it does not hug it");
            Assert.That(
                inner,
                Is.EqualTo(circumscribed * (1f + TargetRingGeometry.FootprintGapFraction)).Within(0.001f),
                "the clearance is exactly the named proportional gap");
        }

        [Test]
        public void OuterDiameter_ReadsTheSharedHoleRatio_RatherThanRestatingIt()
        {
            // #602's annulus hole ratio is what makes the inner edge the edge
            // that has to clear the house. Deriving the diameter from the shared
            // LostItemGlow ratio means a future change to the hole cannot
            // silently break containment with no test failing.
            var expected = (float)Math.Sqrt((6f * 6f) + (8f * 8f))
                * (1f + TargetRingGeometry.FootprintGapFraction)
                / LostItemGlow.GroundRingInnerFraction;

            Assert.That(TargetRingGeometry.OuterDiameter(6f, 8f), Is.EqualTo(expected).Within(0.0001f));
        }

        [Test]
        public void InnerDiameter_IsTheSharedHoleRatioOfTheOuterDiameter()
        {
            Assert.That(
                TargetRingGeometry.InnerDiameter(10f),
                Is.EqualTo(10f * LostItemGlow.GroundRingInnerFraction).Within(0.0001f));
        }

        [Test]
        public void GroundRingInnerFraction_IsTheSharedInnerOverOuterRingScale()
        {
            // One source of truth for the hole ratio: the mesh builder and this
            // sizing rule must read the same number, or the two drift apart.
            Assert.That(
                LostItemGlow.GroundRingInnerFraction,
                Is.EqualTo(LostItemGlow.GroundRingInnerScale / LostItemGlow.GroundRingScale).Within(0.0001f));
            Assert.That(LostItemGlow.GroundRingInnerFraction, Is.GreaterThan(0f).And.LessThan(1f),
                "a genuine hole strictly inside the outer edge");
        }

        [Test]
        public void OuterDiameter_ClearsTheOldLongestSideRule_WhichCouldNotContainASquareHouse()
        {
            // The #669 regression, pinned: the retired 1.15 · max(x, z) rule put
            // the ring's OUTER edge ~19% inside a square house's own corners, so
            // no gap was possible anywhere. Any replacement must be comfortably
            // larger than that.
            const float retiredLongestSideMultiplier = 1.15f;
            const float squareSide = 10f;

            var retired = retiredLongestSideMultiplier * squareSide;

            Assert.That(TargetRingGeometry.OuterDiameter(squareSide, squareSide), Is.GreaterThan(retired),
                "the retired longest-side rule was too small to contain the house at all");
        }

        [Test]
        public void OuterDiameter_SizesFromTheDiagonal_NotFromASquareOfTheLongestSide()
        {
            // The other half of the old bug: max(x, z) over-widened the short
            // axis of a rectangular house while still not clearing its diagonal.
            // Sizing from the diagonal keeps a long, shallow house's ring
            // strictly smaller than a square house of that same long side.
            var rectangular = TargetRingGeometry.OuterDiameter(20f, 4f);
            var squareOfLongSide = TargetRingGeometry.OuterDiameter(20f, 20f);

            Assert.That(rectangular, Is.LessThan(squareOfLongSide),
                "a shallow house is not given the ring of a square house of its long side");
        }

        [Test]
        public void OuterDiameter_GrowsLinearlyWithTheFootprint()
        {
            // Same shape, twice the size => twice the ring, so the ring reads
            // identically under every house variant and upgrade level.
            Assert.That(
                TargetRingGeometry.OuterDiameter(6f, 8f),
                Is.EqualTo(TargetRingGeometry.OuterDiameter(3f, 4f) * 2f).Within(0.0001f));
        }

        [TestCase(-1f, 4f)]
        [TestCase(4f, -1f)]
        public void OuterDiameter_RejectsANegativeFootprint(float x, float z)
        {
            Assert.Throws<ArgumentException>(() => TargetRingGeometry.OuterDiameter(x, z));
        }

        [Test]
        public void RingAroundAMaxLevelHouse_StaysInsideThatHousesOwnLot()
        {
            // Containing the footprint through a hollow band roughly doubles the
            // ring's diameter, which makes a new failure mode possible that the
            // old undersized ring hid: the ring spilling off the house's lot onto
            // a neighbour or the sidewalk. Checked against the largest mesh each
            // house can reach across its whole upgrade ladder.
            foreach (var lot in NeighborhoodLayout.HouseLots)
            {
                var footprint = HousePlacement.MaxHouseFootprint(lot);
                var outerRadius = 0.5f * TargetRingGeometry.OuterDiameter(footprint.Width, footprint.Depth);
                var center = footprint.Center;
                var ring = new LotRect(
                    center.X - outerRadius, center.X + outerRadius,
                    center.Z - outerRadius, center.Z + outerRadius);

                Assert.That(LotBounds.QuadrantBounds(lot).Contains(ring), Is.True,
                    "house " + lot.HouseId + "'s ring stays on its own lot at max upgrade level");
            }
        }
    }
}
