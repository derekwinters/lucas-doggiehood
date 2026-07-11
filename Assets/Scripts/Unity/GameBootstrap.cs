using UnityEngine;

namespace Doggiehood.Unity
{
    /// <summary>
    /// Entry-point MonoBehaviour for the thin Unity wiring layer. Game rules
    /// never live here — decision logic belongs in Doggiehood.Core, and this
    /// layer only connects it to the running scene.
    /// </summary>
    public sealed class GameBootstrap : MonoBehaviour
    {
        private void Awake()
        {
            DontDestroyOnLoad(gameObject);
        }
    }
}
