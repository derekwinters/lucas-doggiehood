using Doggiehood.Core.Interaction;
using UnityEngine;

namespace Doggiehood.Unity
{
    /// <summary>
    /// Scene-side handle for a house (#38).
    ///
    /// #670 (absorbing #667): a house tap resolves to exactly ONE outcome. This
    /// used to raise a single <c>Tapped</c> event that two independent
    /// subscribers both handled — QuestDirector's spray and WorldBootstrap's
    /// open-profile — so tapping a house with bugs on it sprayed the house
    /// <em>and</em> opened its profile panel over the result, with nothing
    /// arbitrating between them. The choice is now made once, in Core
    /// (<see cref="HouseTapArbiter"/>), and only the winning event is raised:
    /// while a house has bugs, tapping anywhere on it sprays; otherwise the tap
    /// opens its profile. Never both.
    /// </summary>
    public sealed class HouseView : MonoBehaviour, IInteractable
    {
        public int HouseId { get; private set; }
        public int TapCount { get; private set; }

        /// <summary>Where a window-watching dog renders (#9).</summary>
        public Transform WindowAnchor { get; set; }

        /// <summary>#670: "does this house have bugs on it right now?" — the
        /// live Core predicate (<c>QuestManager.IsAwaitingSpray</c>) the tap is
        /// arbitrated with, supplied by the quest wiring. Unset means no quest
        /// system is wired, which reads as "no bugs".</summary>
        public System.Func<bool> HasPendingSpray { get; set; }

        /// <summary>Raised when the tap is a spray (#53) — and then the profile
        /// is not opened.</summary>
        public event System.Action SprayRequested;

        /// <summary>Raised when the tap opens the house profile (#208) — and
        /// then nothing is sprayed.</summary>
        public event System.Action ProfileRequested;

        public void Init(int houseId)
        {
            HouseId = houseId;
        }

        public void OnTapped()
        {
            TapCount++;

            if (HouseTapArbiter.Resolve(HasPendingSpray != null && HasPendingSpray())
                == HouseTapOutcome.Spray)
            {
                SprayRequested?.Invoke();
                return;
            }

            ProfileRequested?.Invoke();
        }
    }
}
