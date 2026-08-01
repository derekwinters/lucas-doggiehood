using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using Doggiehood.Core.World;
using NUnit.Framework;

namespace Doggiehood.Core.Tests.World
{
    /// <summary>
    /// #383 (Option A): the Core loader that parses the authored
    /// <c>docs/tools/map-data.json</c> target map
    /// (<c>{name, tiles:[{x,y,type}]}</c>, <c>x-&gt;Col</c>, <c>y-&gt;Row</c>,
    /// origin <see cref="TileType.FourWay"/> at <c>(0,0)</c>) into a validated
    /// <see cref="TileMap"/>. Placement is self-ordered (file order is
    /// authoring order, not adjacency order) and tiles that fail #109
    /// adjacency are rejected, not thrown.
    /// </summary>
    public class MapLoaderTests
    {
        private const int AuthoredMapTileCount = 84;

        [Test]
        public void Parse_OneTileEntry_MapsXToColAndYToRowAndKeepsType()
        {
            var definition = MapDefinition.Parse(
                "{\"name\":\"sample\",\"tiles\":[{\"x\":3,\"y\":-4,\"type\":\"TurnNE\"}]}");

            Assert.That(definition.Name, Is.EqualTo("sample"));
            Assert.That(definition.Tiles.Count, Is.EqualTo(1));

            var tile = definition.Tiles[0];
            Assert.That(tile.Coordinate, Is.EqualTo(new TileCoordinate(3, -4)));
            Assert.That(tile.Type, Is.EqualTo(TileType.TurnNE));
        }

        [Test]
        public void Load_EntriesNotAdjacencyOrdered_PlacesEveryTile()
        {
            // (2,0) appears before (1,0): a naive foreach would fail to place
            // (2,0) since its only would-be neighbor (1,0) isn't down yet.
            var definition = MapDefinition.Parse(
                "{\"name\":\"line\",\"tiles\":["
                + "{\"x\":0,\"y\":0,\"type\":\"FourWay\"},"
                + "{\"x\":2,\"y\":0,\"type\":\"StraightEW\"},"
                + "{\"x\":1,\"y\":0,\"type\":\"StraightEW\"}"
                + "]}");

            var result = MapLoader.Load(definition);

            Assert.That(result.RejectedCoordinates, Is.Empty);
            Assert.That(result.Map.Tiles.Count, Is.EqualTo(3));
            Assert.That(result.Map.HasTileAt(new TileCoordinate(1, 0)), Is.True);
            Assert.That(result.Map.HasTileAt(new TileCoordinate(2, 0)), Is.True);
        }

        [Test]
        public void Load_TileFailingAdjacency_IsRejectedNotPlacedAndReported()
        {
            // CulDeSacEast at (1,0) has no road on its West edge, but the
            // origin FourWay to its west has a road on its East edge: a #109
            // mismatch. The valid StraightNS at (0,1) still places.
            var definition = MapDefinition.Parse(
                "{\"name\":\"mismatch\",\"tiles\":["
                + "{\"x\":0,\"y\":0,\"type\":\"FourWay\"},"
                + "{\"x\":1,\"y\":0,\"type\":\"CulDeSacEast\"},"
                + "{\"x\":0,\"y\":1,\"type\":\"StraightNS\"}"
                + "]}");

            MapLoadResult result = null;
            Assert.DoesNotThrow(() => result = MapLoader.Load(definition));

            Assert.That(result.Map.HasTileAt(new TileCoordinate(1, 0)), Is.False);
            Assert.That(result.Map.HasTileAt(new TileCoordinate(0, 1)), Is.True);
            Assert.That(result.RejectedCoordinates, Is.EquivalentTo(new[] { new TileCoordinate(1, 0) }));
        }

        [Test]
        public void Load_RealAuthoredMap_PlacesAllTilesWithZeroRejections()
        {
            var definition = MapDefinition.Parse(File.ReadAllText(AuthoredMapPath()));

            var result = MapLoader.Load(definition);

            Assert.That(definition.Tiles.Count, Is.EqualTo(AuthoredMapTileCount));
            Assert.That(result.RejectedCoordinates, Is.Empty,
                "Authored map must place cleanly (0 adjacency mismatches).");
            Assert.That(result.Map.Tiles.Count, Is.EqualTo(AuthoredMapTileCount));
        }

        [Test]
        public void Load_ExposesCurvedCornerPerTurnTile()
        {
            var definition = MapDefinition.Parse(
                "{\"name\":\"bend\",\"tiles\":["
                + "{\"x\":0,\"y\":0,\"type\":\"FourWay\"},"
                + "{\"x\":1,\"y\":0,\"type\":\"TurnNW\"}"
                + "]}");

            var result = MapLoader.Load(definition);

            // TurnNW cups its own NW corner; the origin FourWay has no curved
            // corner and must not appear.
            Assert.That(result.CurvedCorners.ContainsKey(new TileCoordinate(0, 0)), Is.False);
            Assert.That(result.CurvedCorners[new TileCoordinate(1, 0)], Is.EqualTo(Quadrant.NorthWest));
        }

        private static string AuthoredMapPath([CallerFilePath] string thisFilePath = null)
        {
            // thisFilePath: .../CoreTests/Doggiehood.Core.Tests/World/MapLoaderTests.cs
            // Three levels up from its directory is the repo root.
            var testFileDirectory = Path.GetDirectoryName(thisFilePath);
            var repoRoot = Path.GetFullPath(Path.Combine(testFileDirectory, "..", "..", ".."));
            return Path.Combine(repoRoot, "docs", "tools", "map-data.json");
        }
    }
}
