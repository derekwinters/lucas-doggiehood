using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Doggiehood.Core.World
{
    /// <summary>
    /// One authored tile entry from the target-map file (#383): its grid
    /// <see cref="TileCoordinate"/> (authored <c>x</c> to
    /// <see cref="TileCoordinate.Col"/>, <c>y</c> to
    /// <see cref="TileCoordinate.Row"/>) and its <see cref="TileType"/>.
    /// </summary>
    public readonly struct MapDefinitionTile
    {
        public TileCoordinate Coordinate { get; }
        public TileType Type { get; }

        public MapDefinitionTile(TileCoordinate coordinate, TileType type)
        {
            Coordinate = coordinate;
            Type = type;
        }
    }

    /// <summary>
    /// The authored target neighborhood parsed from
    /// <c>docs/tools/map-data.json</c> (#383): the design data the Map
    /// Builder emits, shaped <c>{name, tiles:[{x,y,type}]}</c> with origin
    /// <see cref="TileType.FourWay"/> at <c>(0,0)</c>. This is a plain,
    /// engine-free reader (Core/Unity split, CLAUDE.md rule #2): it uses a
    /// small hand-rolled scan rather than a JSON library so it compiles in
    /// both the <c>dotnet test</c> harness and Unity with no extra
    /// dependency. Turning this definition into a validated
    /// <see cref="TileMap"/> is <see cref="MapLoader"/>'s job.
    /// </summary>
    public sealed class MapDefinition
    {
        // JSON field readers. Tile objects hold no nested braces, so a
        // brace-delimited block that carries x/y/type is one tile entry.
        private const string NamePattern = "\"name\"\\s*:\\s*\"((?:\\\\.|[^\"\\\\])*)\"";
        private const string TileBlockPattern = "\\{[^{}]*\\}";
        private const string IntXPattern = "\"x\"\\s*:\\s*(-?\\d+)";
        private const string IntYPattern = "\"y\"\\s*:\\s*(-?\\d+)";
        private const string TypePattern = "\"type\"\\s*:\\s*\"(\\w+)\"";

        private static readonly Regex NameRegex = new Regex(NamePattern, RegexOptions.Compiled);
        private static readonly Regex TileBlockRegex = new Regex(TileBlockPattern, RegexOptions.Compiled);
        private static readonly Regex XRegex = new Regex(IntXPattern, RegexOptions.Compiled);
        private static readonly Regex YRegex = new Regex(IntYPattern, RegexOptions.Compiled);
        private static readonly Regex TypeRegex = new Regex(TypePattern, RegexOptions.Compiled);

        private readonly List<MapDefinitionTile> tiles;

        private MapDefinition(string name, List<MapDefinitionTile> tiles)
        {
            Name = name;
            this.tiles = tiles;
        }

        public string Name { get; }

        public IReadOnlyList<MapDefinitionTile> Tiles
        {
            get { return tiles; }
        }

        /// <summary>Parses the authored map JSON text into a
        /// <see cref="MapDefinition"/>. Throws <see cref="FormatException"/>
        /// if a tile entry names a <see cref="TileType"/> that does not
        /// exist.</summary>
        public static MapDefinition Parse(string json)
        {
            if (json == null)
            {
                throw new ArgumentNullException(nameof(json));
            }

            var nameMatch = NameRegex.Match(json);
            var name = nameMatch.Success ? nameMatch.Groups[1].Value : string.Empty;

            var tiles = new List<MapDefinitionTile>();
            foreach (Match block in TileBlockRegex.Matches(json))
            {
                var text = block.Value;
                var xMatch = XRegex.Match(text);
                var yMatch = YRegex.Match(text);
                var typeMatch = TypeRegex.Match(text);
                if (!xMatch.Success || !yMatch.Success || !typeMatch.Success)
                {
                    continue;
                }

                var col = int.Parse(xMatch.Groups[1].Value, CultureInfo.InvariantCulture);
                var row = int.Parse(yMatch.Groups[1].Value, CultureInfo.InvariantCulture);
                var typeName = typeMatch.Groups[1].Value;
                if (!Enum.TryParse<TileType>(typeName, out var type))
                {
                    throw new FormatException($"Unknown tile type '{typeName}' in map definition.");
                }

                tiles.Add(new MapDefinitionTile(new TileCoordinate(col, row), type));
            }

            return new MapDefinition(name, tiles);
        }
    }
}
