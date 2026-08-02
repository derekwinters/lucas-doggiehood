using System;
using System.Collections.Generic;

namespace Doggiehood.Core.World
{
    /// <summary>
    /// The grid-coordinate tile map (#109): tiles keyed by
    /// <see cref="TileCoordinate"/>, one <see cref="WorldDimensions.TileSize"/>
    /// apart. A new tile may only be placed adjacent to the existing map,
    /// and placement is rejected if any shared edge with an existing
    /// neighbor mismatches (a road edge meeting a no-road edge). Procedural
    /// tile selection is out of scope (#109) - callers choose the type.
    /// </summary>
    public sealed class TileMap
    {
        private static readonly TileEdge[] AllEdges =
        {
            TileEdge.North, TileEdge.South, TileEdge.East, TileEdge.West,
        };

        private readonly Dictionary<TileCoordinate, TileType> tiles = new Dictionary<TileCoordinate, TileType>();

        public TileMap(TileCoordinate origin, TileType originType)
        {
            tiles[origin] = originType;
        }

        public IReadOnlyDictionary<TileCoordinate, TileType> Tiles
        {
            get { return tiles; }
        }

        public bool HasTileAt(TileCoordinate coordinate)
        {
            return tiles.ContainsKey(coordinate);
        }

        public TileType GetTileAt(TileCoordinate coordinate)
        {
            return tiles[coordinate];
        }

        /// <summary>
        /// Whether <paramref name="type"/> may be placed at
        /// <paramref name="coordinate"/>: the coordinate must be empty and
        /// adjacent to at least one existing tile, and every shared edge
        /// with an existing neighbor must agree on road/no-road.
        /// </summary>
        public bool CanPlace(TileCoordinate coordinate, TileType type)
        {
            if (tiles.ContainsKey(coordinate))
            {
                return false;
            }

            var definition = TileCatalog.Get(type);
            bool adjacentToMap = false;

            foreach (var edge in AllEdges)
            {
                var neighborCoordinate = coordinate.Neighbor(edge);
                if (!tiles.TryGetValue(neighborCoordinate, out var neighborType))
                {
                    continue;
                }

                adjacentToMap = true;
                var neighborDefinition = TileCatalog.Get(neighborType);
                bool thisHasRoad = definition.HasRoadOn(edge);
                bool neighborHasRoad = neighborDefinition.HasRoadOn(edge.Opposite());
                if (thisHasRoad != neighborHasRoad)
                {
                    return false;
                }
            }

            return adjacentToMap;
        }

        /// <summary>
        /// Whether placing <paramref name="type"/> at
        /// <paramref name="coordinate"/> would form a real road connection into
        /// the existing network: at least one edge shared with a placed
        /// neighbor must carry a road on <b>both</b> sides. This is strictly
        /// narrower than <see cref="CanPlace"/>, whose edge-agreement check is
        /// satisfied just as well by a no-road/no-road boundary — so the
        /// expansion frontier (#295/#507) keys the unlock indicator to this
        /// predicate, "a road with a connection point that has another tile
        /// defined", rather than to mere grid-adjacency.
        /// </summary>
        public bool HasRoadConnectionAt(TileCoordinate coordinate, TileType type)
        {
            var definition = TileCatalog.Get(type);

            foreach (var edge in AllEdges)
            {
                var neighborCoordinate = coordinate.Neighbor(edge);
                if (!tiles.TryGetValue(neighborCoordinate, out var neighborType))
                {
                    continue;
                }

                var neighborDefinition = TileCatalog.Get(neighborType);
                if (definition.HasRoadOn(edge) && neighborDefinition.HasRoadOn(edge.Opposite()))
                {
                    return true;
                }
            }

            return false;
        }

        public void Place(TileCoordinate coordinate, TileType type)
        {
            if (!CanPlace(coordinate, type))
            {
                throw new InvalidOperationException($"Cannot place {type} at {coordinate}.");
            }

            tiles[coordinate] = type;
        }
    }
}
