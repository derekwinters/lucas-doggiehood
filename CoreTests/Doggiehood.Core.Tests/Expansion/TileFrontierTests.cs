using System.IO;
using System.Runtime.CompilerServices;
using Doggiehood.Core.Expansion;
using Doggiehood.Core.World;
using NUnit.Framework;

namespace Doggiehood.Core.Tests.Expansion
{
    /// <summary>
    /// #295: the player-choice frontier. Any placed tile's open road edge that
    /// borders a tile defined in the full authored target map (and not yet
    /// placed) is an unlockable frontier coordinate. The frontier is a pure
    /// function of (placed <see cref="TileMap"/>, target <see cref="TileMap"/>)
    /// reusing #109 edge-adjacency exactly the way <see cref="TileMap.CanPlace"/>
    /// does.
    /// </summary>
    public class TileFrontierTests
    {
        [Test]
        public void Compute_FromOriginOnly_ReturnsEveryConnectingNeighborInTarget()
        {
            var target = LoadAuthoredTargetMap();
            var placed = new TileMap(new TileCoordinate(0, 0), TileType.FourWay);

            var frontier = TileFrontier.Compute(placed, target);

            // The origin FourWay carries a road on all four edges; its four
            // authored neighbors each connect back, so all four are frontier.
            Assert.That(frontier, Is.EquivalentTo(new[]
            {
                new TileCoordinate(1, 0),
                new TileCoordinate(-1, 0),
                new TileCoordinate(0, 1),
                new TileCoordinate(0, -1),
            }));
        }

        [Test]
        public void Compute_ExcludesTargetTilesNotBorderingAPlacedTile()
        {
            var target = LoadAuthoredTargetMap();
            var placed = new TileMap(new TileCoordinate(0, 0), TileType.FourWay);

            var frontier = TileFrontier.Compute(placed, target);

            // (1,1) exists in the target but borders only (0,1)/(1,0)/(1,2)/(2,1)
            // — none of them placed — so it is not yet on the frontier.
            Assert.That(frontier, Does.Not.Contain(new TileCoordinate(1, 1)));
        }

        [Test]
        public void Compute_ExcludesTilesAlreadyPlaced()
        {
            var target = LoadAuthoredTargetMap();
            var placed = new TileMap(new TileCoordinate(0, 0), TileType.FourWay);
            placed.Place(new TileCoordinate(0, 1), TileType.CulDeSacSouth);

            var frontier = TileFrontier.Compute(placed, target);

            Assert.That(frontier, Does.Not.Contain(new TileCoordinate(0, 1)),
                "an already-placed coordinate is never on the frontier");
        }

        [Test]
        public void Compute_GrowsAsMoreTilesArePlaced()
        {
            var target = LoadAuthoredTargetMap();
            var placed = new TileMap(new TileCoordinate(0, 0), TileType.FourWay);
            placed.Place(new TileCoordinate(1, 0), TileType.TeeNorth);

            var frontier = TileFrontier.Compute(placed, target);

            // Placing (1,0) opens its own connecting neighbors as new frontier,
            // e.g. (1,1) which now borders a placed tile.
            Assert.That(frontier, Does.Contain(new TileCoordinate(1, 1)));
        }

        private static TileMap LoadAuthoredTargetMap()
        {
            var definition = MapDefinition.Parse(File.ReadAllText(AuthoredMapPath()));
            return MapLoader.Load(definition).Map;
        }

        private static string AuthoredMapPath([CallerFilePath] string thisFilePath = null)
        {
            var testFileDirectory = Path.GetDirectoryName(thisFilePath);
            var repoRoot = Path.GetFullPath(Path.Combine(testFileDirectory, "..", "..", ".."));
            return Path.Combine(repoRoot, "docs", "tools", "map-data.json");
        }
    }
}
