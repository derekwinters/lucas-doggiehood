using System;
using System.Collections.Generic;

namespace Doggiehood.Core.World
{
    /// <summary>
    /// Locates and ranks off-map road openings on the live <see cref="TileMap"/>
    /// (#599). Detection reuses the frontier predicate — an outer tile edge that
    /// <see cref="TileTypeDefinition.HasRoadOn"/> with no placed neighbor across
    /// it — rather than inventing a new boundary concept. Ranking is fully
    /// deterministic so Core tests are reproducible and multi-truck behavior
    /// (#600) is predictable: nearest opening to the target, ties broken by a
    /// fixed compass order (N→E→S→W) and then by tile coordinate.
    /// </summary>
    public static class RoadOpenings
    {
        private static readonly TileEdge[] AllEdges =
        {
            TileEdge.North, TileEdge.East, TileEdge.South, TileEdge.West,
        };

        /// <summary>
        /// Every off-map opening on <paramref name="map"/>: for each placed
        /// tile, each edge that carries a road but whose across-neighbor is not
        /// placed. Emitted in the deterministic compass-then-coordinate order so
        /// the list itself is stable regardless of tile iteration order.
        /// </summary>
        public static IReadOnlyList<RoadOpening> Detect(TileMap map)
        {
            if (map == null)
            {
                throw new ArgumentNullException(nameof(map));
            }

            var openings = new List<RoadOpening>();
            foreach (var entry in map.Tiles)
            {
                var coordinate = entry.Key;
                var definition = TileCatalog.Get(entry.Value);

                foreach (var edge in AllEdges)
                {
                    if (!definition.HasRoadOn(edge))
                    {
                        continue;
                    }

                    if (map.HasTileAt(coordinate.Neighbor(edge)))
                    {
                        continue;
                    }

                    var point = TileGeometry.EdgeMidpoint(coordinate, edge);
                    openings.Add(new RoadOpening(coordinate, edge, point));
                }
            }

            openings.Sort(Compare);
            return openings;
        }

        /// <summary>
        /// The opening nearest <paramref name="target"/>, ties broken by the
        /// fixed compass order (N→E→S→W) and then tile coordinate (Col ascending,
        /// then Row). Deterministic for a given input set.
        /// </summary>
        public static RoadOpening Nearest(IReadOnlyList<RoadOpening> openings, GridPoint target)
        {
            if (openings == null)
            {
                throw new ArgumentNullException(nameof(openings));
            }

            if (openings.Count == 0)
            {
                throw new ArgumentException("At least one opening is required.", nameof(openings));
            }

            var best = openings[0];
            var bestDistance = DistanceSquared(best.Point, target);
            for (var i = 1; i < openings.Count; i++)
            {
                var candidate = openings[i];
                var distance = DistanceSquared(candidate.Point, target);

                if (distance < bestDistance - Epsilon)
                {
                    best = candidate;
                    bestDistance = distance;
                }
                else if (distance <= bestDistance + Epsilon && Compare(candidate, best) < 0)
                {
                    best = candidate;
                    bestDistance = distance;
                }
            }

            return best;
        }

        private const float Epsilon = 0.0001f;

        /// <summary>Deterministic ordering: compass rank first, then tile
        /// coordinate (Col ascending, then Row).</summary>
        private static int Compare(RoadOpening a, RoadOpening b)
        {
            var byEdge = CompassRank(a.Edge).CompareTo(CompassRank(b.Edge));
            if (byEdge != 0)
            {
                return byEdge;
            }

            var byCol = a.Tile.Col.CompareTo(b.Tile.Col);
            if (byCol != 0)
            {
                return byCol;
            }

            return a.Tile.Row.CompareTo(b.Tile.Row);
        }

        private static int CompassRank(TileEdge edge)
        {
            switch (edge)
            {
                case TileEdge.North: return 0;
                case TileEdge.East: return 1;
                case TileEdge.South: return 2;
                case TileEdge.West: return 3;
                default: throw new ArgumentOutOfRangeException(nameof(edge), edge, null);
            }
        }

        private static float DistanceSquared(GridPoint a, GridPoint b)
        {
            var dx = a.X - b.X;
            var dz = a.Z - b.Z;
            return (dx * dx) + (dz * dz);
        }
    }
}
