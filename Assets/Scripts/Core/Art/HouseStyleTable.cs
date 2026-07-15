using System;
using System.Collections.Generic;

namespace Doggiehood.Core.Art
{
    /// <summary>
    /// The four starting-house variants: which City Kit Suburban model
    /// each house renders as, and which kit texture variant tints it
    /// (#64). Single source of truth for "what does this house look
    /// like" — model + tint together, rather than the model assignment
    /// living separately on HouseModelCatalog (pre-#64) while the palette
    /// lived here disconnected from it. HouseModelCatalog still owns the
    /// per-model footprint/door geometry (#125), keyed by ModelName.
    /// </summary>
    public static class HouseStyleTable
    {
        public static IReadOnlyList<HouseStyle> Styles { get; } = new[]
        {
            // Same 4 letter picks as the pre-#64 HouseModelCatalog
            // assignment (#122 placeholder pending Derek/Lucas visual
            // review) — consolidated here, not re-picked.
            new HouseStyle(1, "building-type-b", HouseTintVariant.Colormap),
            new HouseStyle(2, "building-type-g", HouseTintVariant.VariationA),
            new HouseStyle(3, "building-type-k", HouseTintVariant.VariationB),
            new HouseStyle(4, "building-type-m", HouseTintVariant.VariationC),
        };

        /// <summary>Starting assignment: house id N gets style N.</summary>
        public static HouseStyle ForHouse(int houseId)
        {
            foreach (var style in Styles)
            {
                if (style.StyleId == houseId)
                {
                    return style;
                }
            }

            throw new ArgumentException($"No house style assigned for house id {houseId}.", nameof(houseId));
        }
    }
}
