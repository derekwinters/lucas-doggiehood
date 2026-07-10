using System;
using System.Collections.Generic;

namespace Doggiehood.Core.Art
{
    /// <summary>
    /// The four cottage silhouette variants and their assignment to the
    /// starting houses (#64): suburban cottage-style, varied per house, in
    /// the bright &amp; saturated palette (docs/specs/world/art-style.md).
    /// </summary>
    public static class HouseStyleTable
    {
        public static IReadOnlyList<HouseStyle> Styles { get; } = new[]
        {
            new HouseStyle(1, RoofShape.Gable, true, "#FF6F61", "#D64550"),
            new HouseStyle(2, RoofShape.Hip, false, "#FFD23F", "#F28F3B"),
            new HouseStyle(3, RoofShape.Gambrel, true, "#3FA7D6", "#2D89AD"),
            new HouseStyle(4, RoofShape.Shed, false, "#59CD90", "#6C4FC4"),
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
