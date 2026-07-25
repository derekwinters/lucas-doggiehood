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
            // The Level-1 (as-built) mesh of each house's proposed #59
            // upgrade path — Derek's call (2026-07-25) resolving the #122
            // placeholder picks. Homes now start small and visibly grow as
            // they level up; these are the chosen starter meshes, no longer
            // placeholders. Each house keeps its original tint variant (the
            // tint is independent of the model), so the 4 starters stay 4
            // distinct models with 4 distinct tints.
            new HouseStyle(1, "building-type-r", HouseTintVariant.Colormap),
            new HouseStyle(2, "building-type-h", HouseTintVariant.VariationA),
            new HouseStyle(3, "building-type-k", HouseTintVariant.VariationB),
            new HouseStyle(4, "building-type-q", HouseTintVariant.VariationC),
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

        /// <summary>
        /// Non-throwing existence check for <see cref="ForHouse"/> (#57):
        /// a house built beyond the starting 4 (an unlocked zone's lot) has
        /// no authored model/tint assignment yet — per-zone-house styling
        /// is undesigned, so callers use this to fall back to a plain
        /// render instead of catching ForHouse's ArgumentException.
        /// </summary>
        public static bool HasStyle(int houseId)
        {
            foreach (var style in Styles)
            {
                if (style.StyleId == houseId)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
