using System;
using System.Collections.Generic;
using Doggiehood.Core.Dogs;
using Doggiehood.Core.Expansion;
using Doggiehood.Core.World;
using UnityEngine;

namespace Doggiehood.Unity
{
    /// <summary>
    /// #518: scene-side glue that raises the "Welcome to the neighborhood!"
    /// pop-up (<see cref="WelcomePopup"/>, approved wireframe
    /// docs/specs/ui/welcome-popup.md) each time a household moves into a vacant
    /// house. It subscribes to the same Core move-in event the
    /// <see cref="QuestDirector"/> uses to spawn the new dogs
    /// (<see cref="QuestManager.MoveInOccurred"/>) — so the spawn and the
    /// announcement fire off one signal — and, after a
    /// <see cref="WelcomePopup.WelcomePopupDelaySeconds"/> beat, shows the panel
    /// with the Core-composed copy (<see cref="WelcomeMessage"/>).
    ///
    /// <para>The delay keeps the welcome from stacking on top of the
    /// quest-resolution feedback that triggered the move-in (welcome-popup.md
    /// "Timing"). Thin wiring only — it holds no game rules: Core has already
    /// added the household and flipped the house occupied; this only presents it.
    /// The non-presentational behaviors both hang off <b>Say hi!</b>: it flies the
    /// camera to the new house via the Core
    /// <see cref="Doggiehood.Core.Cameras.CameraController.FocusOn"/> AND opens
    /// that house's profile (#604, via the injected
    /// <c>openHouseProfile</c> callback) so the resident dog(s) are one tap away;
    /// the scrim tap dismisses without panning or opening anything.</para>
    /// </summary>
    public sealed class WelcomePopupDirector : MonoBehaviour
    {
        private GameState state;
        private WelcomePopup popup;
        private Transform worldRoot;
        private Action<int> openHouseProfile;

        private WelcomeMessage pendingMessage;
        private int pendingHouseId;
        private bool hasPending;
        private float delayRemaining;

        /// <summary>True while a move-in has fired but its delayed welcome has
        /// not yet popped.</summary>
        public bool HasPendingWelcome => hasPending;

        /// <summary>Wires the director. <paramref name="openHouseProfile"/> (#604)
        /// opens the house profile modal for a given house id — the same resolve
        /// a house tap uses (<c>WorldBootstrap.OpenHouseProfile</c>) — so
        /// <b>Say hi!</b> takes the player to meet the new neighbour, not just
        /// pan to their roof.</summary>
        public void Init(GameState state, WelcomePopup popup, Transform worldRoot, Action<int> openHouseProfile)
        {
            this.state = state;
            this.popup = popup;
            this.worldRoot = worldRoot;
            this.openHouseProfile = openHouseProfile;

            state.Quests.MoveInOccurred += OnMoveInOccurred;
        }

        private void OnDestroy()
        {
            if (state != null)
            {
                state.Quests.MoveInOccurred -= OnMoveInOccurred;
            }
        }

        /// <summary>A household just moved in: compose its welcome copy now and
        /// arm the delay so the panel pops a beat after the quest-resolution
        /// feedback that triggered it (welcome-popup.md "Timing").</summary>
        private void OnMoveInOccurred(IReadOnlyList<Dog> household)
        {
            if (household == null || household.Count == 0)
            {
                return;
            }

            pendingMessage = WelcomeMessage.ForHousehold(household);
            pendingHouseId = household[0].HouseId;
            delayRemaining = WelcomePopup.WelcomePopupDelaySeconds;
            hasPending = true;
        }

        private void Update()
        {
            Tick(Time.deltaTime);
        }

        /// <summary>Counts down the arm delay and raises the pop-up when it
        /// elapses. Called by Update at runtime and directly by EditMode tests
        /// (mirrors <see cref="QuestDirector.Tick"/>).</summary>
        public void Tick(float deltaTime)
        {
            if (!hasPending)
            {
                return;
            }

            delayRemaining -= deltaTime;
            if (delayRemaining > 0f)
            {
                return;
            }

            hasPending = false;
            var houseId = pendingHouseId;
            // #604: "Say hi!" pans to the new house AND opens its profile (so the
            // resident dog(s) are one tap away). The welcome unregisters from the
            // modal gate on Dismiss and the profile registers on Open within this
            // same synchronous callback, so exactly one modal stays registered
            // and no world tap leaks in between. The scrim path (Dismiss) is
            // untouched — it opens nothing.
            popup.Show(pendingMessage, () =>
            {
                FlyCameraToHouse(houseId);
                openHouseProfile?.Invoke(houseId);
            });
        }

        /// <summary>Recentres the camera on the moved-in house: resolves its
        /// world position from its <see cref="HouseView"/>, asks the Core
        /// controller to focus there (clamped to bounds), and re-applies the
        /// state to the live rig. Mirrors the #165 Home-button fly-to-house. A
        /// no-op if the house or rig can't be found.</summary>
        private void FlyCameraToHouse(int houseId)
        {
            HouseView house = null;
            foreach (var view in worldRoot.GetComponentsInChildren<HouseView>())
            {
                if (view.HouseId == houseId)
                {
                    house = view;
                    break;
                }
            }

            var rig = UnityEngine.Object.FindFirstObjectByType<CameraRig>();
            if (house == null || rig == null)
            {
                return;
            }

            var position = house.transform.position;
            rig.Controller.FocusOn(new GridPoint(position.x, position.z));
            rig.ApplyConfiguration();
        }
    }
}
