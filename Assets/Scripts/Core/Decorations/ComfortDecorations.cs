using System.Collections.Generic;
using Doggiehood.Core.Economy;

namespace Doggiehood.Core.Decorations
{
    /// <summary>
    /// The v1 decoration category (#51): comfort items only. Names and
    /// prices both come from the central ItemCatalog (#62, #190) — this is
    /// the Decoration-eligible slice of that one catalog, not a second
    /// parallel list. Other categories (play items, yard dressing, food)
    /// are future scope.
    /// </summary>
    public static class ComfortDecorations
    {
        public static IReadOnlyList<string> ItemNames =>
            ItemCatalog.NamesEligibleFor(ItemEligibility.Decoration);
    }
}
