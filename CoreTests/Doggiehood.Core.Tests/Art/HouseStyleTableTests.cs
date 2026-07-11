using System.Linq;
using Doggiehood.Core.Art;
using Doggiehood.Core.World;
using NUnit.Framework;

namespace Doggiehood.Core.Tests.Art
{
    public class HouseStyleTableTests
    {
        [Test]
        public void DefinesFourDistinctSilhouetteVariants()
        {
            // #64: each starting house gets its own cottage silhouette.
            Assert.That(HouseStyleTable.Styles.Count, Is.EqualTo(4));
            Assert.That(HouseStyleTable.Styles.Select(s => s.RoofShape), Is.Unique);
            Assert.That(HouseStyleTable.Styles.Select(s => s.WallColorHex), Is.Unique);
        }

        [Test]
        public void EveryStartingHouse_GetsAStyle_Consistently()
        {
            foreach (var lot in NeighborhoodLayout.HouseLots)
            {
                var first = HouseStyleTable.ForHouse(lot.HouseId);
                var second = HouseStyleTable.ForHouse(lot.HouseId);

                Assert.That(first, Is.Not.Null);
                Assert.That(second.StyleId, Is.EqualTo(first.StyleId));
            }
        }

        [Test]
        public void StartingHouses_AllGetDifferentStyles()
        {
            var styles = NeighborhoodLayout.HouseLots
                .Select(lot => HouseStyleTable.ForHouse(lot.HouseId).StyleId)
                .ToList();

            Assert.That(styles, Is.Unique);
        }

        [Test]
        public void ForHouse_UnknownIdThrows()
        {
            Assert.That(() => HouseStyleTable.ForHouse(999), Throws.ArgumentException);
        }

        [Test]
        public void AllHouseColors_AreBrightAndSaturated()
        {
            // The documented palette direction (#64,
            // docs/specs/world/art-style.md): bold, punchy colors — not
            // muted/earthy, not pastel. Enforced as HSV thresholds.
            foreach (var style in HouseStyleTable.Styles)
            {
                foreach (var hex in new[] { style.WallColorHex, style.RoofColorHex })
                {
                    var color = ColorRgb.Parse(hex);
                    Assert.That(color.Saturation, Is.GreaterThanOrEqualTo(0.5f),
                        $"{hex} on style {style.StyleId} is under-saturated for the bright palette");
                    Assert.That(color.Value, Is.GreaterThanOrEqualTo(0.6f),
                        $"{hex} on style {style.StyleId} is too dark for the bright palette");
                }
            }
        }
    }
}
