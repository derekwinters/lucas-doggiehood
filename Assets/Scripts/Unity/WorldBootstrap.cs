using Doggiehood.Core.World;
using UnityEngine;

namespace Doggiehood.Unity
{
    /// <summary>Builds the neighborhood when the scene starts.</summary>
    public sealed class WorldBootstrap : MonoBehaviour
    {
        private void Awake()
        {
            WorldBuilder.Build(GameState.CreateNew());
        }
    }
}
