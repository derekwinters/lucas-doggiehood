using System;
using System.Collections.Generic;
using System.Linq;

namespace Doggiehood.Core.World
{
    /// <summary>
    /// Per-type property-lot slots for the 16 non-<see cref="TileType.FourWay"/>
    /// tile types (#109). Follows the starting FourWay tile's own pattern
    /// (<see cref="NeighborhoodLayout"/>): one lot per <see cref="Quadrant"/>,
    /// offset from the tile's center by
    /// <see cref="NeighborhoodLayout.LotDistanceFromCenter"/> along both
    /// axes - applied uniformly to every type regardless of which edges
    /// carry a road, since that corner offset always clears a 60m tile's
    /// road pavement/cul-de-sac bulb no matter the tile's road layout, and
    /// no design precedent narrows lot count/position by road shape (see
    /// the #109 PR notes for the reasoning and its limits).
    /// </summary>
    public static class TileLotCatalog
    {
        private static readonly IReadOnlyList<TileType> NonFourWayTypes = ((TileType[])Enum.GetValues(typeof(TileType)))
            .Where(type => type != TileType.FourWay)
            .ToList();

        public static IReadOnlyCollection<TileType> Types
        {
            get { return NonFourWayTypes; }
        }

        /// <summary>The 4 quadrant lot slots for <paramref name="type"/>, as
        /// offsets in meters from the tile's center. Throws for
        /// <see cref="TileType.FourWay"/> - its lots are already defined by
        /// <see cref="NeighborhoodLayout"/>, not this catalog.</summary>
        public static IReadOnlyDictionary<Quadrant, GridPoint> LotsFor(TileType type)
        {
            if (type == TileType.FourWay)
            {
                throw new ArgumentException(
                    "FourWay's lots are defined by NeighborhoodLayout, not TileLotCatalog.", nameof(type));
            }

            float d = NeighborhoodLayout.LotDistanceFromCenter;
            return new Dictionary<Quadrant, GridPoint>
            {
                { Quadrant.NorthEast, new GridPoint(d, d) },
                { Quadrant.NorthWest, new GridPoint(-d, d) },
                { Quadrant.SouthEast, new GridPoint(d, -d) },
                { Quadrant.SouthWest, new GridPoint(-d, -d) },
            };
        }

        /// <summary>The same 4 lot slots as <see cref="LotsFor"/>, as a flat
        /// list of local offsets (no quadrant association).</summary>
        public static IReadOnlyList<GridPoint> LotOffsetsFor(TileType type)
        {
            return LotsFor(type).Values.ToList();
        }
    }
}
