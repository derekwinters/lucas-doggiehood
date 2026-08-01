using System;
using System.Collections.Generic;
using System.Linq;

namespace Doggiehood.Core.World
{
    /// <summary>
    /// Per-type property-lot slots for the 16 non-<see cref="TileType.FourWay"/>
    /// tile types (#109), following the "Property lots per tile" rules
    /// settled in <c>docs/specs/world/tile-catalog.md</c> and implemented
    /// for #383:
    /// <list type="bullet">
    /// <item>Twin bends (<see cref="TileType.OpposingTurnsNS"/>/
    /// <see cref="TileType.OpposingTurnsEW"/>): no lots - their two arcs
    /// leave no clean buildable quadrant.</item>
    /// <item>Bends (<c>Turn*</c>): three lots - drop the small corner the
    /// curve cups (the bend's own corner: <see cref="TileType.TurnNE"/> drops
    /// <see cref="Quadrant.NorthEast"/>, and so on).</item>
    /// <item>Every other type: all four quadrant lots.</item>
    /// </list>
    /// Kept lots sit one lot per <see cref="Quadrant"/>, offset from the
    /// tile's center by <see cref="NeighborhoodLayout.LotDistanceFromCenter"/>
    /// along both axes - the same corner distance the starting FourWay tile
    /// uses (<see cref="NeighborhoodLayout"/>).
    /// </summary>
    public static class TileLotCatalog
    {
        private static readonly IReadOnlyList<TileType> NonFourWayTypes = ((TileType[])Enum.GetValues(typeof(TileType)))
            .Where(type => type != TileType.FourWay)
            .ToList();

        // The "cupped" corner each bend drops (and renders curved): the
        // bend's own named corner. Also the single source for
        // TryGetCuppedCorner (#383, curved-corner data).
        private static readonly IReadOnlyDictionary<TileType, Quadrant> CuppedCorners =
            new Dictionary<TileType, Quadrant>
            {
                { TileType.TurnNE, Quadrant.NorthEast },
                { TileType.TurnNW, Quadrant.NorthWest },
                { TileType.TurnSE, Quadrant.SouthEast },
                { TileType.TurnSW, Quadrant.SouthWest },
            };

        private static readonly IReadOnlyCollection<TileType> TwinBends =
            new[] { TileType.OpposingTurnsNS, TileType.OpposingTurnsEW };

        public static IReadOnlyCollection<TileType> Types
        {
            get { return NonFourWayTypes; }
        }

        /// <summary>The quadrant lot slots for <paramref name="type"/>, as
        /// offsets in meters from the tile's center. Twin bends return an
        /// empty set; bends drop their own cupped corner (3 slots); every
        /// other type returns all 4. Throws for <see cref="TileType.FourWay"/>
        /// - its lots are already defined by <see cref="NeighborhoodLayout"/>,
        /// not this catalog.</summary>
        public static IReadOnlyDictionary<Quadrant, GridPoint> LotsFor(TileType type)
        {
            if (type == TileType.FourWay)
            {
                throw new ArgumentException(
                    "FourWay's lots are defined by NeighborhoodLayout, not TileLotCatalog.", nameof(type));
            }

            if (TwinBends.Contains(type))
            {
                return new Dictionary<Quadrant, GridPoint>();
            }

            float d = NeighborhoodLayout.LotDistanceFromCenter;
            var lots = new Dictionary<Quadrant, GridPoint>
            {
                { Quadrant.NorthEast, new GridPoint(d, d) },
                { Quadrant.NorthWest, new GridPoint(-d, d) },
                { Quadrant.SouthEast, new GridPoint(d, -d) },
                { Quadrant.SouthWest, new GridPoint(-d, -d) },
            };

            if (TryGetCuppedCorner(type, out var cupped))
            {
                lots.Remove(cupped);
            }

            return lots;
        }

        /// <summary>For a bend (<c>Turn*</c>) tile, the corner the curve cups
        /// - the lot it drops and the corner that renders curved (#383,
        /// data only). Returns false for every other type.</summary>
        public static bool TryGetCuppedCorner(TileType type, out Quadrant quadrant)
        {
            return CuppedCorners.TryGetValue(type, out quadrant);
        }

        /// <summary>The same 4 lot slots as <see cref="LotsFor"/>, as a flat
        /// list of local offsets (no quadrant association).</summary>
        public static IReadOnlyList<GridPoint> LotOffsetsFor(TileType type)
        {
            return LotsFor(type).Values.ToList();
        }
    }
}
