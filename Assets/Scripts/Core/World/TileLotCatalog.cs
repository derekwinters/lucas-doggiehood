using System;
using System.Collections.Generic;
using System.Linq;

namespace Doggiehood.Core.World
{
    /// <summary>
    /// Per-type property-lot slots for the 17 lotted tile types (#109) — every
    /// type except the house-free <see cref="TileType.GreenSpace"/>, including
    /// the full-intersection <see cref="TileType.FourWay"/> (#607) — following
    /// the "Property lots per tile" rules settled in
    /// <c>docs/specs/world/tile-catalog.md</c> (#383, refined by
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
        // #539/#607: GreenSpace is the only type with no per-quadrant catalog
        // lot slots — a house-free park tile that never holds a lot — so it is
        // the only type excluded from Types (LotsFor still handles it by
        // returning an empty set). FourWay is a full intersection carrying all
        // four quadrant lots wherever it appears (#607); the origin FourWay's
        // seeded lots are guarded in GameState, not by excluding FourWay here.
        private static readonly IReadOnlyList<TileType> LottedTypes = ((TileType[])Enum.GetValues(typeof(TileType)))
            .Where(type => type != TileType.GreenSpace)
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
            get { return LottedTypes; }
        }

        /// <summary>The quadrant lot slots for <paramref name="type"/>, as
        /// offsets in meters from the tile's center. Twin bends return an
        /// empty set; bends drop their cupped corner and its diagonal opposite
        /// (2 slots); cul-de-sacs keep the two quadrants adjacent to the roaded
        /// edge (2 slots); every other type — including the full-intersection
        /// <see cref="TileType.FourWay"/> (#607) — returns all 4. The origin
        /// FourWay's seeded lots (<see cref="NeighborhoodLayout"/>) are guarded
        /// in <see cref="GameState.LotsForUnlockedTile"/>, not here.</summary>
        public static IReadOnlyDictionary<Quadrant, GridPoint> LotsFor(TileType type)
        {
            // #539: a green-space tile never holds a house — no lot slots.
            if (type == TileType.GreenSpace || TwinBends.Contains(type))
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

        /// <summary>The quadrants a tile renders as open space with trees, as
        /// offsets in meters from the tile's center: every quadrant that holds
        /// no kept house lot (#614). Derived as "all four quadrants minus
        /// <see cref="LotsFor"/>" so trees and lots share one source of truth
        /// and can never disagree — cul-de-sacs keep their two bulb-side
        /// quadrants (#385), bends drop the cupped corner AND its diagonal
        /// opposite, twin bends drop all four, and full-lot types
        /// (<c>FourWay</c>/<c>Straight*</c>/<c>Tee*</c>) drop none. The
        /// whole-tile <see cref="TileType.GreenSpace"/> park (#539) is the one
        /// exception: it holds no lots, so a naive "no lot ⇒ trees" rule would
        /// plant on all four of its quadrants, but it is a separate park tile
        /// (out of scope for #614) and stays bare. World-space placement clears
        /// each tree of the tile's roads and skips any quadrant with no clean
        /// grass — see <see cref="TileGeometry.TreeWorldPositionsFor"/>.</summary>
        public static IReadOnlyDictionary<Quadrant, GridPoint> TreeQuadrantsFor(TileType type)
        {
            if (type == TileType.GreenSpace)
            {
                return new Dictionary<Quadrant, GridPoint>();
            }

            var lots = LotsFor(type);
            var trees = new Dictionary<Quadrant, GridPoint>();
            foreach (var entry in AllFourQuadrantLots())
            {
                if (!lots.ContainsKey(entry.Key))
                {
                    trees[entry.Key] = entry.Value;
                }
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
