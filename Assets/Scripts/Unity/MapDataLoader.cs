using System.Linq;
using Doggiehood.Core.World;
using UnityEngine;

namespace Doggiehood.Unity
{
    /// <summary>
    /// #453 (Decision A): supplies Core the authored target neighborhood at
    /// runtime. The Map Builder's design data (<c>docs/tools/map-data.json</c>,
    /// #383) is staged as a checked-in <see cref="TextAsset"/> under
    /// <c>Assets/Resources/Data/map-data.json</c> (the same "stage the source
    /// asset under Resources/" precedent the lock icon uses); this thin loader
    /// <see cref="Resources.Load{T}"/>s it, runs it through the existing
    /// engine-free <see cref="MapDefinition.Parse"/> + <see cref="MapLoader.Load"/>,
    /// and hands the validated <see cref="TileMap"/> to
    /// <see cref="GameState.SetTargetMap"/> so <see cref="GameState.UnlockableFrontier"/>
    /// has something to derive locks from. Without it the frontier is always
    /// empty in the shipped game. Any tile the authored file could not place
    /// (<see cref="MapLoadResult.RejectedCoordinates"/>) is LOGGED, not silently
    /// dropped, so an authoring error is visible.
    /// </summary>
    public static class MapDataLoader
    {
        /// <summary>Resources key for the target-map TextAsset, staged at
        /// <c>Assets/Resources/Data/map-data.json</c> (bare, extensionless path
        /// as Resources.Load expects).</summary>
        public const string ResourceKey = "Data/map-data";

        /// <summary>Loads and validates the authored target map, or null if the
        /// staged asset can't be found. Logs any rejected coordinates.</summary>
        public static MapLoadResult Load()
        {
            var textAsset = Resources.Load<TextAsset>(ResourceKey);
            if (textAsset == null)
            {
                Debug.LogWarning(
                    $"MapDataLoader: no target-map TextAsset at Resources/{ResourceKey}; " +
                    "the expansion frontier will be empty.");
                return null;
            }

            var definition = MapDefinition.Parse(textAsset.text);
            var result = MapLoader.Load(definition);
            LogRejected(result);
            return result;
        }

        /// <summary>Loads the authored target map and supplies it to
        /// <paramref name="state"/> via <see cref="GameState.SetTargetMap"/>. Call
        /// once at bootstrap, BEFORE anything reads
        /// <see cref="GameState.UnlockableFrontier"/>. A no-op (frontier stays
        /// empty) if the asset can't be loaded.</summary>
        public static void Apply(GameState state)
        {
            var result = Load();
            if (result != null)
            {
                state.SetTargetMap(result.Map);
            }
        }

        private static void LogRejected(MapLoadResult result)
        {
            if (result.RejectedCoordinates.Count == 0)
            {
                return;
            }

            var coordinates = string.Join(", ", result.RejectedCoordinates.Select(c => c.ToString()));
            Debug.LogWarning(
                $"MapDataLoader: {result.RejectedCoordinates.Count} authored tile(s) could not be placed " +
                $"(#109 adjacency or unreachable from the origin): {coordinates}");
        }
    }
}
