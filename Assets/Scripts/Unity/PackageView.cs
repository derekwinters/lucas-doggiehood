using UnityEngine;

namespace Doggiehood.Unity
{
    /// <summary>
    /// Scene-side tap target for a delivered gift package (#471). Same
    /// pass-through stub shape as <see cref="HouseView"/>/<see cref="EmptyLotView"/>:
    /// tap handling is a stub until a package interaction is designed. Before
    /// this existed the delivered package carried no <see cref="IInteractable"/>,
    /// so <see cref="TapRouter"/>'s <c>GetComponentInParent&lt;IInteractable&gt;()</c>
    /// lookup returned null and the tap was silently swallowed; TapCount/Tapped
    /// let tests observe that the package now routes.
    /// </summary>
    public sealed class PackageView : MonoBehaviour, IInteractable
    {
        public int TapCount { get; private set; }

        /// <summary>Raised on tap so future package wiring can react.</summary>
        public event System.Action Tapped;

        public void OnTapped()
        {
            TapCount++;
            Tapped?.Invoke();
        }
    }
}
