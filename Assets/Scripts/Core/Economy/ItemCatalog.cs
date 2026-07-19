using System;
using System.Collections.Generic;
using System.Linq;

namespace Doggiehood.Core.Economy
{
    /// <summary>
    /// Which quest types an item may appear in (#190). An item can be
    /// tagged for more than one — e.g. a toy is both find-and-lose and
    /// gift-worthy.
    /// </summary>
    [Flags]
    public enum ItemEligibility
    {
        None = 0,
        Lost = 1 << 0,
        Gift = 1 << 1,
        Decoration = 1 << 2,
    }

    public sealed class CatalogItem
    {
        public string Name { get; }

        /// <summary>Coins to purchase this item. Null for find-only items
        /// (e.g. "puppy") that are never bought, only found.</summary>
        public int? Cost { get; }

        public ItemEligibility Eligibility { get; }

        public CatalogItem(string name, ItemEligibility eligibility, int? cost = null)
        {
            Name = name;
            Eligibility = eligibility;
            Cost = cost;
        }

        public bool IsEligibleFor(ItemEligibility tag)
        {
            return (Eligibility & tag) != 0;
        }
    }

    /// <summary>
    /// Central item catalog (#62, #190): the single source of truth for
    /// every world item. Each entry carries its price (purchasable items
    /// only) and the quest types it's eligible for; quest pools are queries
    /// over this list rather than hand-maintained parallel arrays.
    /// </summary>
    public static class ItemCatalog
    {
        public static IReadOnlyList<CatalogItem> Items { get; } = new[]
        {
            new CatalogItem("toy", ItemEligibility.Lost | ItemEligibility.Gift, 30),
            new CatalogItem("ball", ItemEligibility.Lost | ItemEligibility.Gift, 30),
            new CatalogItem("chew bone", ItemEligibility.Gift, 35),
            new CatalogItem("pool", ItemEligibility.Gift, 50),
            new CatalogItem("bed", ItemEligibility.Decoration, 40),
            new CatalogItem("cushion", ItemEligibility.Decoration, 30),
            new CatalogItem("blanket", ItemEligibility.Decoration, 30),
            new CatalogItem("puppy", ItemEligibility.Lost),
        };

        public static CatalogItem Get(string name)
        {
            foreach (var item in Items)
            {
                if (item.Name == name)
                {
                    return item;
                }
            }

            throw new ArgumentException($"No catalog item named '{name}'.", nameof(name));
        }

        /// <summary>The subject pool for a quest type: every catalog item
        /// tagged with <paramref name="tag"/>, and nothing else. Adding a
        /// new catalog entry with this tag makes it show up here
        /// automatically — no separate list to update.</summary>
        public static IReadOnlyList<string> NamesEligibleFor(ItemEligibility tag)
        {
            return Items.Where(i => i.IsEligibleFor(tag)).Select(i => i.Name).ToList();
        }
    }
}
