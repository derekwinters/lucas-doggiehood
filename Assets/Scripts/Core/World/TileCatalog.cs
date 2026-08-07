using System.Collections.Generic;

namespace Doggiehood.Core.World
{
    /// <summary>
    /// The tile-type catalog (#105 design, built by #109): all 16 types
    /// from docs/specs/world/tile-catalog.md and the road edges each one
    /// declares — the 15 road tiles plus the roadless #539 GreenSpace.
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
                // #539: the green-space tile carries no road on any edge. Its
                // empty road-edge set is what makes it place
                // through TileMap.CanPlace only against no-road neighbor edges,
                // and what keeps it out of the road-connection frontier forever.
                new TileTypeDefinition(TileType.GreenSpace,
                    System.Array.Empty<TileEdge>()),
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
