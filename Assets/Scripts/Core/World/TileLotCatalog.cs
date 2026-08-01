using System;
using System.Collections.Generic;
using System.Linq;

namespace Doggiehood.Core.World
{
    /// <summary>
    /// Per-type property-lot slots for the 16 non-<see cref="TileType.FourWay"/>
    /// tile types (#109), following the "Property lots per tile" rules
    /// settled in <c>docs/specs/world/tile-catalog.md</c> (#383, refined by
    /// #385). House facing is settled as "remove" (no rotation): every kept
    /// lot borders a straight roaded edge square-on, and lots that can't are
    /// dropped:
    /// <list type="bullet">
    /// <item>Twin bends (<see cref="TileType.OpposingTurnsNS"/>/
    /// <see cref="TileType.OpposingTurnsEW"/>): no lots - their two arcs
    /// leave no clean buildable quadrant.</item>
    /// <item>Bends (<c>Turn*</c>): two lots - drop the small corner the curve
    /// cups (the bend's own corner) AND the corner diagonally opposite it,
    /// which borders neither roaded edge.</item>
    /// <item>Cul-de-sacs (<c>CulDeSac*</c>): two lots - keep the two quadrants
    /// adjacent to the single roaded edge; the two bulb-side quadrants become
    /// open space with trees (<see cref="TreeQuadrantsFor"/>).</item>
    /// <item>Every other type (<c>Straight*</c>/<c>Tee*</c>): all four
    /// quadrant lots.</item>
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
        // TryGetCuppedCorner (#383, curved-corner data). #385 additionally
        // drops the corner diagonally opposite the cup - see DiagonalOpposite.
        private static readonly IReadOnlyDictionary<TileType, Quadrant> CuppedCorners =
            new Dictionary<TileType, Quadrant>
            {
                { TileType.TurnNE, Quadrant.NorthEast },
                { TileType.TurnNW, Quadrant.NorthWest },
                { TileType.TurnSE, Quadrant.SouthEast },
                { TileType.TurnSW, Quadrant.SouthWest },
            };

        // #385: the two quadrants each cul-de-sac keeps - those adjacent to
        // its single roaded edge (they already face the road square-on). The
        // other two (bulb side) become open space with trees.
        private static readonly IReadOnlyDictionary<TileType, Quadrant[]> CulDeSacKeptQuadrants =
            new Dictionary<TileType, Quadrant[]>
            {
                { TileType.CulDeSacNorth, new[] { Quadrant.NorthEast, Quadrant.NorthWest } },
                { TileType.CulDeSacSouth, new[] { Quadrant.SouthEast, Quadrant.SouthWest } },
                { TileType.CulDeSacEast, new[] { Quadrant.NorthEast, Quadrant.SouthEast } },
                { TileType.CulDeSacWest, new[] { Quadrant.NorthWest, Quadrant.SouthWest } },
            };

        // The corner diagonally across the tile from a given quadrant.
        private static readonly IReadOnlyDictionary<Quadrant, Quadrant> DiagonalOpposite =
            new Dictionary<Quadrant, Quadrant>
            {
                { Quadrant.NorthEast, Quadrant.SouthWest },
                { Quadrant.SouthWest, Quadrant.NorthEast },
                { Quadrant.NorthWest, Quadrant.SouthEast },
                { Quadrant.SouthEast, Quadrant.NorthWest },
            };

        private static readonly IReadOnlyCollection<TileType> TwinBends =
            new[] { TileType.OpposingTurnsNS, TileType.OpposingTurnsEW };

        public static IReadOnlyCollection<TileType> Types
        {
            get { return NonFourWayTypes; }
        }

        /// <summary>The quadrant lot slots for <paramref name="type"/>, as
        /// offsets in meters from the tile's center. Twin bends return an
        /// empty set; bends drop their cupped corner and its diagonal opposite
        /// (2 slots); cul-de-sacs keep the two quadrants adjacent to the roaded
        /// edge (2 slots); every other type returns all 4. Throws for
        /// <see cref="TileType.FourWay"/> - its lots are already defined by
        /// <see cref="NeighborhoodLayout"/>, not this catalog.</summary>
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

            var lots = AllFourQuadrantLots();

            if (TryGetCuppedCorner(type, out var cupped))
            {
                lots.Remove(cupped);
                lots.Remove(DiagonalOpposite[cupped]);
            }
            else if (CulDeSacKeptQuadrants.TryGetValue(type, out var kept))
            {
                foreach (var quadrant in TreeQuadrantKeys(kept))
                {
                    lots.Remove(quadrant);
                }
            }

            return lots;
        }

        /// <summary>The dropped quadrants a tile renders as open space with
        /// trees, as offsets in meters from the tile's center. Only cul-de-sacs
        /// have any - their two bulb-side quadrants; every other type (bends'
        /// plain open-space drops included) returns an empty set.</summary>
        public static IReadOnlyDictionary<Quadrant, GridPoint> TreeQuadrantsFor(TileType type)
        {
            if (!CulDeSacKeptQuadrants.TryGetValue(type, out var kept))
            {
                return new Dictionary<Quadrant, GridPoint>();
            }

            var all = AllFourQuadrantLots();
            var trees = new Dictionary<Quadrant, GridPoint>();
            foreach (var quadrant in TreeQuadrantKeys(kept))
            {
                trees[quadrant] = all[quadrant];
            }

            return trees;
        }

        /// <summary>For a bend (<c>Turn*</c>) tile, the corner the curve cups
        /// - the lot it drops and the corner that renders curved (#383,
        /// data only). Returns false for every other type.</summary>
        public static bool TryGetCuppedCorner(TileType type, out Quadrant quadrant)
        {
            return CuppedCorners.TryGetValue(type, out quadrant);
        }

        /// <summary>The kept lot slots as a flat list of local offsets (no
        /// quadrant association).</summary>
        public static IReadOnlyList<GridPoint> LotOffsetsFor(TileType type)
        {
            return LotsFor(type).Values.ToList();
        }

        private static Dictionary<Quadrant, GridPoint> AllFourQuadrantLots()
        {
            float d = NeighborhoodLayout.LotDistanceFromCenter;
            return new Dictionary<Quadrant, GridPoint>
            {
                { Quadrant.NorthEast, new GridPoint(d, d) },
                { Quadrant.NorthWest, new GridPoint(-d, d) },
                { Quadrant.SouthEast, new GridPoint(d, -d) },
                { Quadrant.SouthWest, new GridPoint(-d, -d) },
            };
        }

        // The two quadrants NOT in <paramref name="kept"/> - a cul-de-sac's
        // bulb-side (tree) quadrants.
        private static IEnumerable<Quadrant> TreeQuadrantKeys(Quadrant[] kept)
        {
            return ((Quadrant[])Enum.GetValues(typeof(Quadrant))).Where(q => !kept.Contains(q));
        }
    }
}
