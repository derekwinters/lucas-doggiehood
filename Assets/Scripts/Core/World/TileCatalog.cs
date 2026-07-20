using System.Collections.Generic;

namespace Doggiehood.Core.World
{
    /// <summary>
    /// The tile-type catalog (#105 design, built by #109): all 17 types
    /// from docs/specs/world/tile-catalog.md and the road edges/arcs each
    /// one declares.
    /// </summary>
    public static class TileCatalog
    {
        private static readonly Dictionary<TileType, TileTypeDefinition> Definitions = BuildDefinitions();

        public static IReadOnlyCollection<TileType> Types
        {
            get { return Definitions.Keys; }
        }

        public static TileTypeDefinition Get(TileType type)
        {
            return Definitions[type];
        }

        private static Dictionary<TileType, TileTypeDefinition> BuildDefinitions()
        {
            // Resolved on #109 (Derek): "the two turns would not connect.
            // Each arc would connect two adjacent sides only." So
            // OpposingTurnsNS is a NE-corner arc plus an unrelated SW-corner
            // arc (together bowing north and south, per the catalog doc);
            // OpposingTurnsEW is that pairing's 90-degree rotation: a
            // NW-corner arc plus an unrelated SE-corner arc (bowing west
            // and east). Neither arc connects to the other.
            var opposingTurnsNsArcs = new[]
            {
                new TileArc(TileEdge.North, TileEdge.East),
                new TileArc(TileEdge.South, TileEdge.West),
            };
            var opposingTurnsEwArcs = new[]
            {
                new TileArc(TileEdge.North, TileEdge.West),
                new TileArc(TileEdge.South, TileEdge.East),
            };

            var all = new[]
            {
                new TileTypeDefinition(TileType.FourWay,
                    new[] { TileEdge.North, TileEdge.South, TileEdge.East, TileEdge.West }),
                new TileTypeDefinition(TileType.StraightNS,
                    new[] { TileEdge.North, TileEdge.South }),
                new TileTypeDefinition(TileType.StraightEW,
                    new[] { TileEdge.East, TileEdge.West }),
                new TileTypeDefinition(TileType.TurnNE,
                    new[] { TileEdge.North, TileEdge.East }),
                new TileTypeDefinition(TileType.TurnNW,
                    new[] { TileEdge.North, TileEdge.West }),
                new TileTypeDefinition(TileType.TurnSE,
                    new[] { TileEdge.South, TileEdge.East }),
                new TileTypeDefinition(TileType.TurnSW,
                    new[] { TileEdge.South, TileEdge.West }),
                new TileTypeDefinition(TileType.TeeNorth,
                    new[] { TileEdge.East, TileEdge.West, TileEdge.North }),
                new TileTypeDefinition(TileType.TeeSouth,
                    new[] { TileEdge.East, TileEdge.West, TileEdge.South }),
                new TileTypeDefinition(TileType.TeeEast,
                    new[] { TileEdge.North, TileEdge.South, TileEdge.East }),
                new TileTypeDefinition(TileType.TeeWest,
                    new[] { TileEdge.North, TileEdge.South, TileEdge.West }),
                new TileTypeDefinition(TileType.CulDeSacNorth,
                    new[] { TileEdge.North }),
                new TileTypeDefinition(TileType.CulDeSacSouth,
                    new[] { TileEdge.South }),
                new TileTypeDefinition(TileType.CulDeSacEast,
                    new[] { TileEdge.East }),
                new TileTypeDefinition(TileType.CulDeSacWest,
                    new[] { TileEdge.West }),
                new TileTypeDefinition(TileType.OpposingTurnsNS,
                    new[] { TileEdge.North, TileEdge.East, TileEdge.South, TileEdge.West },
                    opposingTurnsNsArcs),
                new TileTypeDefinition(TileType.OpposingTurnsEW,
                    new[] { TileEdge.North, TileEdge.East, TileEdge.South, TileEdge.West },
                    opposingTurnsEwArcs),
            };

            var definitions = new Dictionary<TileType, TileTypeDefinition>();
            foreach (var definition in all)
            {
                definitions[definition.Type] = definition;
            }

            return definitions;
        }
    }
}
