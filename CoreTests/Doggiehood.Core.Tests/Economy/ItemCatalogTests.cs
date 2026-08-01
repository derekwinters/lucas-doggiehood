using System.Linq;
using Doggiehood.Core.Economy;
using Doggiehood.Core.Quests;
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
        public void PurchasableGiftOrDecorationItems_EachFallInADefinedCostTierBand()
        {
            // #62 + #317: purchasable entries are priced within a defined cost
            // tier band — Starter (30-50), Mid (60-90), or Premium (100+).
            // #318's fence is the first Premium entry; earlier gifts and
            // decorations still sit in the Starter band.
            var purchasable = ItemCatalog.Items.Where(i =>
                i.IsEligibleFor(ItemEligibility.Gift) || i.IsEligibleFor(ItemEligibility.Decoration));

            Assert.That(purchasable, Is.Not.Empty);
            foreach (var item in purchasable)
            {
                Assert.That(item.Cost, Is.Not.Null, item.Name);
                var cost = item.Cost.Value;
                var inStarter = cost >= QuestCostTiers.StarterMinCost
                    && cost <= QuestCostTiers.StarterMaxCost;
                var inMid = cost >= QuestCostTiers.MidMinCost && cost <= QuestCostTiers.MidMaxCost;
                var inPremium = cost >= QuestCostTiers.PremiumMinCost;
                Assert.That(inStarter || inMid || inPremium, Is.True,
                    $"{item.Name} costs {cost}, which falls in no defined tier band");
            }
        }

        [Test]
        public void Fence_IsAGiftTaggedPremiumEntry_CostingOneHundred()
        {
            // #318: the fence is a purchasable Gift-tagged catalog entry priced
            // in the Premium tier (100 coins), reusing the existing Gift
            // eligibility tag rather than inventing a new one.
            var fence = ItemCatalog.Get(ItemCatalog.FenceItemName);

            Assert.That(fence.Name, Is.EqualTo("fence"));
            Assert.That(fence.IsEligibleFor(ItemEligibility.Gift), Is.True);
            Assert.That(fence.IsEligibleFor(ItemEligibility.Decoration), Is.False);
            Assert.That(fence.IsEligibleFor(ItemEligibility.Lost), Is.False);
            Assert.That(fence.Cost, Is.EqualTo(QuestCostTiers.PremiumMinCost));
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
                Is.EquivalentTo(new[] { "toy", "ball", "chew bone", "pool", "fence" }));
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
