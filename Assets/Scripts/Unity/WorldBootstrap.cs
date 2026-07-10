using Doggiehood.Core.World;
using UnityEngine;

namespace Doggiehood.Unity
{
    /// <summary>Builds the neighborhood and its dogs when the scene starts.</summary>
    public sealed class WorldBootstrap : MonoBehaviour
    {
        private void Awake()
        {
            var state = GameState.CreateNew();
            var root = WorldBuilder.Build(state);
            DogSpawner.SpawnDogs(state, root.transform);

            if (FindFirstObjectByType<ConversationPresenter>() == null)
            {
                gameObject.AddComponent<ConversationPresenter>();
            }
        }
    }
}
