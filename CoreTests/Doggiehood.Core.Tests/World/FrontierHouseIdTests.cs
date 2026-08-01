using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using Doggiehood.Core.World;
using NUnit.Framework;

namespace Doggiehood.Core.Tests.World
{
    /// <summary>
    /// #453: <see cref="FrontierHouseId.For"/> derives a stable, collision-free
    /// house id purely from a frontier tile's <see cref="TileCoordinate"/> and a
    /// <see cref="Quadrant"/> — so a player-choice frontier lot has a
    /// deterministic id without a counter or extra persistence (superseding the
    /// retired sequential zone-lot ids).
    /// </summary>
    public class FrontierHouseIdTests
    {
        [Test]
        public void For_IsPure_SameInputsAlwaysProduceTheSameId()
        {
            var coordinate = new TileCoordinate(3, -2);

            var first = FrontierHouseId.For(coordinate, Quadrant.SouthEast);
            var second = FrontierHouseId.For(coordinate, Quadrant.SouthEast);

            Assert.That(second, Is.EqualTo(first));
        }

        [Test]
        public void For_IsCollisionFree_AcrossTheFullAuthoredTargetMap_AndNeverHitsAStartingId()
        {
            var target = LoadAuthoredTargetMap();
            var startingIds = new HashSet<int> { 1, 2, 3, 4 };

            var seen = new HashSet<int>();
            foreach (var entry in target.Tiles)
            {
                foreach (Quadrant quadrant in System.Enum.GetValues(typeof(Quadrant)))
                {
                    var id = FrontierHouseId.For(entry.Key, quadrant);
                    Assert.That(startingIds, Does.Not.Contain(id),
                        $"frontier id {id} collides with a starting-layout house id");
                    Assert.That(seen.Add(id), Is.True,
                        $"frontier id {id} collides with another (coordinate, quadrant) pair");
                }
            }
        }

        [Test]
        public void For_DistinctQuadrantsOfOneTile_ProduceDistinctIds()
        {
            var coordinate = new TileCoordinate(0, 1);
            var ids = new HashSet<int>
            {
                FrontierHouseId.For(coordinate, Quadrant.NorthEast),
                FrontierHouseId.For(coordinate, Quadrant.NorthWest),
                FrontierHouseId.For(coordinate, Quadrant.SouthEast),
                FrontierHouseId.For(coordinate, Quadrant.SouthWest),
            };

            Assert.That(ids.Count, Is.EqualTo(4));
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
