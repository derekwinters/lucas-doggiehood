using System;
using System.Collections.Generic;

namespace Doggiehood.Core.Economy
{
    public sealed class CatalogItem
    {
        public string Name { get; }
        public int Cost { get; }

        public CatalogItem(string name, int cost)
        {
            Name = name;
            Cost = cost;
        }
    }

    /// <summary>
    /// Central price list (#62): every gift/decoration costs 30-50 coins
    /// (3-5 quests of saving). Comfort decorations (#51) share this table.
    /// </summary>
    public static class ItemCatalog
    {
        public static IReadOnlyList<CatalogItem> Items { get; } = new[]
        {
            new CatalogItem("toy", 30),
            new CatalogItem("ball", 30),
            new CatalogItem("chew bone", 35),
            new CatalogItem("pool", 50),
            new CatalogItem("bed", 40),
            new CatalogItem("cushion", 30),
            new CatalogItem("blanket", 30),
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
    }
}
