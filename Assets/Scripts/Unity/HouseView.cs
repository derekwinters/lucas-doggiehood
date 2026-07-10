using UnityEngine;

namespace Doggiehood.Unity
{
    /// <summary>
    /// Scene-side handle for a house (#38). Tap handling is a stub until the
    /// conversation/quest systems (milestones 03/04) give houses real
    /// interactions; TapCount lets tests observe routing today.
    /// </summary>
    public sealed class HouseView : MonoBehaviour, IInteractable
    {
        public int HouseId { get; private set; }
        public int TapCount { get; private set; }

        public void Init(int houseId)
        {
            HouseId = houseId;
        }

        public void OnTapped()
        {
            TapCount++;
        }
    }
}
