using System.Linq;
using Doggiehood.Core.Art;
using Doggiehood.Core.World;
using NUnit.Framework;

namespace Doggiehood.Core.Tests.Art
{
    public class HouseStyleTableTests
    {
        [Test]
        public void DefinesFourDistinctModelAndTintVariants()
        {
            // #64: each starting house gets its own kit model + tint
            // variant. Colors now live in the kit's real textures, not as
            // Core-owned hex data, so the old HSV-brightness coverage
            // (AllHouseColors_AreBrightAndSaturated) is retired — nothing
            // in Core represents color any more.
            Assert.That(HouseStyleTable.Styles.Count, Is.EqualTo(4));
            Assert.That(HouseStyleTable.Styles.Select(s => s.TintVariant), Is.Unique);
            Assert.That(HouseStyleTable.Styles.Select(s => s.ModelName), Is.Unique);
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
        public void ForHouse_MatchesThePlaceholderModelPicks()
        {
            // Same 4 letter picks as the pre-#64 HouseModelCatalog
            // assignment (#122 placeholder, pending Derek/Lucas visual
            // review in #125/#122) — consolidated here, not re-picked.
            Assert.That(HouseStyleTable.ForHouse(1).ModelName, Is.EqualTo("building-type-b"));
            Assert.That(HouseStyleTable.ForHouse(2).ModelName, Is.EqualTo("building-type-g"));
            Assert.That(HouseStyleTable.ForHouse(3).ModelName, Is.EqualTo("building-type-k"));
            Assert.That(HouseStyleTable.ForHouse(4).ModelName, Is.EqualTo("building-type-m"));
        }

        [Test]
        public void DefaultTintVariant_IsColormap()
        {
            // At least one house keeps the kit's base colormap texture
            // rather than a tint-variant swap.
            Assert.That(HouseStyleTable.Styles.Select(s => s.TintVariant),
                Does.Contain(HouseTintVariant.Colormap));
        }
    }
}
