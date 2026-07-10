using System.Collections.Generic;

namespace Doggiehood.Core.Decorations
{
    /// <summary>
    /// The v1 decoration category (#51): comfort items only. Prices live in
    /// the central ItemCatalog (#62). Other categories (play items, yard
    /// dressing, food) are future scope.
    /// </summary>
    public static class ComfortDecorations
    {
        public static IReadOnlyList<string> ItemNames { get; } = new[]
        {
            "bed",
            "cushion",
            "blanket",
        };
    }
}
