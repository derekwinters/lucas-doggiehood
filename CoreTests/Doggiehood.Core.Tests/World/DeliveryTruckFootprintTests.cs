using Doggiehood.Core.World;
using NUnit.Framework;

namespace Doggiehood.Core.Tests.World
{
    /// <summary>
    /// #660: the delivery truck's footprint must FIT between an intersection's
    /// two crosswalk bands. This is a safety property, not a cosmetic one — the
    /// analysis on #658 proved that a truck longer than the clear gap between
    /// the bands necessarily holds BOTH of an intersection's crosswalks at once,
    /// which lets two oncoming trucks acquire them in opposite order and wedge
    /// permanently, taking any crossing dog down with them.
    ///
    /// The property previously lived only in prose, so a later ModelScale tweak
    /// could silently reintroduce the deadlock. These tests pin it to the locked
    /// <see cref="WorldDimensions"/> constants so a road-layout OR a truck-scale
    /// change fails CI loudly instead of freezing the game at runtime.
    /// </summary>
    public class DeliveryTruckFootprintTests
    {
        private const float Tolerance = 0.0001f;

        [Test]
        public void ClearGapBetweenCrosswalkBands_IsDerivedFromTheLockedWorldDimensions()
        {
            // An intersection's two crosswalks straddle it at +/- the crossing
            // road's sidewalk-centre offset, so they sit CrosswalkSpacing apart;
            // subtracting one band's own width leaves the clear roadway between
            // them. Derived, never a literal (#161).
            var offset = WorldDimensions.RoadWidth / 2f
                         + WorldDimensions.GrassVergeWidth
                         + WorldDimensions.SidewalkWidth / 2f;

            Assert.That(DeliveryTruckFootprint.CrosswalkSpacing,
                Is.EqualTo(2f * offset).Within(Tolerance),
                "the two bands sit one crossing-road sidewalk offset either side of the intersection");
            Assert.That(DeliveryTruckFootprint.ClearGapBetweenCrosswalkBands,
                Is.EqualTo(2f * offset - WorldDimensions.CrosswalkWidth).Within(Tolerance),
                "the clear gap is the spacing less one band's width");
        }

        [Test]
        public void FrontAndRearSetbacksTogether_FitInsideTheClearGapBetweenTheBands()
        {
            // THE constraint. #639 gives the truck a front setback so its bumper
            // stops behind a held band; #658 adds the matching rear setback so
            // its tail is clear before a waiting dog is released. Both are drawn
            // from the same body-length budget, and both must fit in the gap:
            //
            //     frontSetback + rearSetback  <  crosswalkSpacing - CrosswalkWidth
            //
            // If they don't, the truck is holding both bands at once and two
            // oncoming trucks deadlock. Strictly less than — equality means the
            // truck's ends touch both bands simultaneously.
            var body = DeliveryTruckFootprint.NominalBodyLength;
            var sum = DeliveryTruckFootprint.FrontSetbackFor(body)
                      + DeliveryTruckFootprint.RearSetbackFor(body);

            Assert.That(sum, Is.LessThan(DeliveryTruckFootprint.ClearGapBetweenCrosswalkBands),
                "the truck's full crosswalk footprint must fit between an intersection's two bands — "
                + "a longer truck holds both at once and two oncoming trucks wedge permanently (#658)");
        }

        [Test]
        public void MaxBodyLength_IsTheClearGapLessTheStopGap_AndTheTruckIsUnderIt()
        {
            // Substituting fs = L/2 + gap and rs = L/2 reduces the constraint
            // above to a plain bound on the body length itself, which is the
            // form a scale change is checked against.
            Assert.That(DeliveryTruckFootprint.MaxBodyLength,
                Is.EqualTo(DeliveryTruckFootprint.ClearGapBetweenCrosswalkBands
                           - DeliveryTruckFootprint.CrosswalkStopGap).Within(Tolerance));
            Assert.That(DeliveryTruckFootprint.NominalBodyLength,
                Is.LessThan(DeliveryTruckFootprint.MaxBodyLength),
                "the truck body at its configured ModelScale must fit the budget");
            Assert.That(DeliveryTruckFootprint.FitsBetweenCrosswalkBands(
                    DeliveryTruckFootprint.NominalBodyLength), Is.True);
        }

        [Test]
        public void FitsBetweenCrosswalkBands_RejectsABodyAtOrOverTheBudget()
        {
            // The predicate is what #658 and any future scale change consult, so
            // pin both sides of its boundary rather than only the passing one.
            Assert.That(DeliveryTruckFootprint.FitsBetweenCrosswalkBands(
                    DeliveryTruckFootprint.MaxBodyLength), Is.False,
                "a body exactly at the budget touches both bands at once — not allowed");
            Assert.That(DeliveryTruckFootprint.FitsBetweenCrosswalkBands(
                    DeliveryTruckFootprint.MaxBodyLength * 2f), Is.False);
        }

        [Test]
        public void NominalBodyLength_IsTheImportedModelLengthTimesTheConfiguredScale()
        {
            Assert.That(DeliveryTruckFootprint.NominalBodyLength,
                Is.EqualTo(DeliveryTruckFootprint.ModelLengthAtImportScale
                           * DeliveryTruckFootprint.ModelScale).Within(Tolerance));
        }

        [Test]
        public void SetbacksAreDrawnFromTheSameBody_FrontLeadingByTheStopGap()
        {
            // Both setbacks are measured from the truck's pivot (the centre of
            // its body): half a body to either end, with the front carrying the
            // extra daylight it stops with (#639).
            const float body = 4f;
            Assert.That(DeliveryTruckFootprint.RearSetbackFor(body),
                Is.EqualTo(body / 2f).Within(Tolerance));
            Assert.That(DeliveryTruckFootprint.FrontSetbackFor(body),
                Is.EqualTo(body / 2f + DeliveryTruckFootprint.CrosswalkStopGap).Within(Tolerance));
        }
    }
}
