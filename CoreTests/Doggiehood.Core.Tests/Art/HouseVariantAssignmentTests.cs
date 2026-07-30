using System.Collections.Generic;
using Doggiehood.Core.Art;
using NUnit.Framework;

namespace Doggiehood.Core.Tests.Art
{
    /// <summary>
    /// #299: every zone-built house (id >= 5) rolls one of the five
    /// <see cref="HouseLevelModelTable"/> ladders and one of the 20 generated
    /// palette tints, via a deterministic seed derived from the house id —
    /// assigned once at build and stable across sessions and iteration order.
    /// The four starting houses (ids 1-4) keep their fixed
    /// <see cref="HouseStyleTable"/> ladders and are exempt from the roll.
    /// </summary>
    public class HouseVariantAssignmentTests
    {
        [Test]
        public void ForHouse_IsDeterministic_SameIdSameVariantEveryCall()
        {
            for (var id = HouseVariantAssignment.FirstZoneHouseId; id < HouseVariantAssignment.FirstZoneHouseId + 50; id++)
            {
                var first = HouseVariantAssignment.ForHouse(id);
                var second = HouseVariantAssignment.ForHouse(id);

                Assert.That(second.LadderId, Is.EqualTo(first.LadderId), $"house {id} ladder is stable");
                Assert.That(second.TintIndex, Is.EqualTo(first.TintIndex), $"house {id} tint is stable");
            }
        }

        [Test]
        public void ForHouse_StaysInRange_ForLadderAndTint()
        {
            for (var id = HouseVariantAssignment.FirstZoneHouseId; id < HouseVariantAssignment.FirstZoneHouseId + 200; id++)
            {
                var variant = HouseVariantAssignment.ForHouse(id);

                Assert.That(variant.LadderId, Is.InRange(1, HouseVariantAssignment.LadderCount),
                    $"house {id} ladder id in 1..{HouseVariantAssignment.LadderCount}");
                Assert.That(variant.TintIndex, Is.InRange(0, HouseVariantAssignment.TintCount - 1),
                    $"house {id} tint index in 0..{HouseVariantAssignment.TintCount - 1}");
            }
        }

        [Test]
        public void ForHouse_SpreadsAcrossAllFiveLadders_AndAllTwentyTints()
        {
            var ladders = new HashSet<int>();
            var tints = new HashSet<int>();
            for (var id = HouseVariantAssignment.FirstZoneHouseId; id < HouseVariantAssignment.FirstZoneHouseId + 200; id++)
            {
                var variant = HouseVariantAssignment.ForHouse(id);
                ladders.Add(variant.LadderId);
                tints.Add(variant.TintIndex);
            }

            Assert.That(ladders.Count, Is.EqualTo(HouseVariantAssignment.LadderCount),
                "distinct zone houses use every one of the five ladders");
            Assert.That(tints.Count, Is.EqualTo(HouseVariantAssignment.TintCount),
                "distinct zone houses use every one of the twenty tints");
        }

        [Test]
        public void IsZoneHouse_IsTrueOnlyFromTheFirstZoneHouseId()
        {
            Assert.That(HouseVariantAssignment.IsZoneHouse(1), Is.False);
            Assert.That(HouseVariantAssignment.IsZoneHouse(4), Is.False);
            Assert.That(HouseVariantAssignment.IsZoneHouse(HouseVariantAssignment.FirstZoneHouseId), Is.True);
            Assert.That(HouseVariantAssignment.IsZoneHouse(HouseVariantAssignment.FirstZoneHouseId + 1), Is.True);
        }

        [Test]
        public void ForHouse_ThrowsForAStarterHouse_TheyKeepTheirFixedLadder()
        {
            Assert.That(() => HouseVariantAssignment.ForHouse(1), Throws.ArgumentException);
            Assert.That(() => HouseVariantAssignment.ForHouse(4), Throws.ArgumentException);
        }
    }
}
