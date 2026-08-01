using Doggiehood.Core.World;
using Doggiehood.Unity;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Doggiehood.Unity.EditModeTests
{
    /// <summary>
    /// #453 (Decision A): the Unity-side loader that supplies Core the authored
    /// target map at runtime. The staged Resources TextAsset
    /// (<c>Assets/Resources/Data/map-data.json</c>) round-trips through
    /// <see cref="MapDefinition.Parse"/> + <see cref="MapLoader.Load"/> with zero
    /// rejected coordinates, and once handed to <see cref="GameState.SetTargetMap"/>
    /// the expansion frontier is non-empty (so locks can appear).
    /// </summary>
    public class MapDataLoaderTests
    {
        [SetUp]
        public void ImportTheMapAsset()
        {
            // A cold CI Library imports Resources lazily; force it so
            // Resources.Load resolves the TextAsset deterministically.
            AssetDatabase.ImportAsset(
                FrontierEditModeWorld.MapDataPath, ImportAssetOptions.ForceSynchronousImport);
        }

        [Test]
        public void Load_ResolvesTheStagedResourcesAsset()
        {
            var asset = Resources.Load<TextAsset>(MapDataLoader.ResourceKey);
            Assert.That(asset, Is.Not.Null,
                "the target map must be staged as a Resources TextAsset at " + MapDataLoader.ResourceKey);
        }

        [Test]
        public void Load_RoundTripsTheAuthoredMap_WithZeroRejectedCoordinates()
        {
            var result = MapDataLoader.Load();

            Assert.That(result, Is.Not.Null, "the staged asset loads and parses");
            Assert.That(result.RejectedCoordinates, Is.Empty,
                "every authored tile places validly (#109 adjacency) — no authoring errors");
            Assert.That(result.Map.Tiles.Count, Is.GreaterThan(1),
                "the target map holds the whole authored neighborhood, not just the origin");
        }

        [Test]
        public void Apply_SuppliesTheTargetMap_SoTheFrontierIsNonEmpty()
        {
            var state = GameState.CreateNew();
            Assert.That(state.UnlockableFrontier(), Is.Empty, "precondition: no target map, no frontier");

            MapDataLoader.Apply(state);

            Assert.That(state.TargetMap, Is.Not.Null, "Apply supplies the authored target map");
            Assert.That(state.UnlockableFrontier(), Is.Not.Empty,
                "with the target map supplied there is at least one unlockable frontier coordinate");
        }
    }
}
