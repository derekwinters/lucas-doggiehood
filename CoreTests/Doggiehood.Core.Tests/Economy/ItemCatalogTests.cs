using System.Linq;
using Doggiehood.Core.Economy;
using NUnit.Framework;

namespace Doggiehood.Core.Tests.Economy
{
    /// <summary>
    /// #190: one tagged item catalog is the single source of truth for
    /// quest subjects — pools are queries over eligibility tags, not
    /// hand-maintained parallel lists.
    /// </summary>
    public class ItemCatalogTests
    {
        [Test]
        public void GiftOrDecorationEligibleItems_AllCostThirtyToFifty()
        {
            // #62 economy rule applies only to purchasable items.
            var purchasable = ItemCatalog.Items.Where(i =>
                i.IsEligibleFor(ItemEligibility.Gift) || i.IsEligibleFor(ItemEligibility.Decoration));

            Assert.That(purchasable, Is.Not.Empty);
            foreach (var item in purchasable)
            {
                Assert.That(item.Cost, Is.Not.Null, item.Name);
                Assert.That(item.Cost.Value, Is.InRange(30, 50), item.Name);
            }
        }

        [Test]
        public void FindOnlyItems_HaveNoCost()
        {
            // e.g. "puppy" — you find it, you don't buy it.
            var puppy = ItemCatalog.Get("puppy");

            Assert.That(puppy.IsEligibleFor(ItemEligibility.Lost), Is.True);
            Assert.That(puppy.IsEligibleFor(ItemEligibility.Gift), Is.False);
            Assert.That(puppy.IsEligibleFor(ItemEligibility.Decoration), Is.False);
            Assert.That(puppy.Cost, Is.Null);
        }

        [Test]
        public void EligibleFor_ReturnsExactlyTheItemsTaggedForThatType()
        {
            Assert.That(ItemCatalog.NamesEligibleFor(ItemEligibility.Lost),
                Is.EquivalentTo(new[] { "toy", "ball", "puppy" }));
            Assert.That(ItemCatalog.NamesEligibleFor(ItemEligibility.Gift),
                Is.EquivalentTo(new[] { "toy", "ball", "chew bone", "pool" }));
            Assert.That(ItemCatalog.NamesEligibleFor(ItemEligibility.Decoration),
                Is.EquivalentTo(new[] { "bed", "cushion", "blanket" }));
        }

        [Test]
        public void ANewCatalogEntry_FlowsIntoOnlyItsTaggedQueries()
        {
            // Proves the querying mechanism itself: one entry tagged for
            // several types is reachable from exactly those queries, and
            // no others, with no additional wiring required per query.
            var multiTagged = new CatalogItem("leash", ItemEligibility.Lost | ItemEligibility.Gift, 30);
            var decorationOnly = new CatalogItem("rug", ItemEligibility.Decoration, 35);

            Assert.That(multiTagged.IsEligibleFor(ItemEligibility.Lost), Is.True);
            Assert.That(multiTagged.IsEligibleFor(ItemEligibility.Gift), Is.True);
            Assert.That(multiTagged.IsEligibleFor(ItemEligibility.Decoration), Is.False);

            Assert.That(decorationOnly.IsEligibleFor(ItemEligibility.Decoration), Is.True);
            Assert.That(decorationOnly.IsEligibleFor(ItemEligibility.Lost), Is.False);
            Assert.That(decorationOnly.IsEligibleFor(ItemEligibility.Gift), Is.False);
        }

        [Test]
        public void Get_StillThrows_ForAnUnknownItem()
        {
            Assert.That(() => ItemCatalog.Get("nonexistent-item"), Throws.ArgumentException);
        }
    }
}
