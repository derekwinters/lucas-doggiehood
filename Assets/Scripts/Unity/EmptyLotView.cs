using UnityEngine;

namespace Doggiehood.Unity
{
    /// <summary>
    /// Scene-side tap target for an empty, buildable lot in an unlocked
    /// zone (#57) — a graybox marker standing in for a house that hasn't
    /// been built yet. Tap handling is a thin pass-through, same pattern as
    /// HouseView: ExpansionDirector owns the actual build decision (Core's
    /// GameState.TryBuildHouse) and the visual swap, this view only raises
    /// the event and lets tests observe routing via TapCount.
    /// </summary>
    public sealed class EmptyLotView : MonoBehaviour, IInteractable
    {
        public int HouseId { get; private set; }
        public int TapCount { get; private set; }

        /// <summary>Raised on tap so ExpansionDirector can attempt the build.</summary>
        public event System.Action Tapped;

        public void Init(int houseId)
        {
            HouseId = houseId;
        }

        public void OnTapped()
        {
            TapCount++;
            Tapped?.Invoke();
        }
    }
}
