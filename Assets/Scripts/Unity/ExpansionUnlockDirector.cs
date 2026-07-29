using Doggiehood.Core.Expansion;
using Doggiehood.Core.World;
using UnityEngine;

namespace Doggiehood.Unity
{
    /// <summary>
    /// Scene-side glue for the map-expansion unlock trigger (#343, Derek's
    /// Option A): wires the tappable lock indicator (<see cref="ExpansionIndicatorView"/>)
    /// to the reusable <see cref="ConfirmationDialog"/>, whose Yes calls
    /// GameState.TryUnlockNextZone. The mirror of how <see cref="ExpansionDirector"/>
    /// wires EmptyLotView → TryBuildHouse (#57): every decision stays in Core
    /// (the cost, the affordability gate on the tap, the unlock itself and its
    /// rejections) — this layer only opens the dialog and, on a confirmed
    /// success, makes the new zone's empty lots appear, refreshes the indicator,
    /// and saves. The currency HUD reflects the spend automatically since it
    /// reads Wallet.Coins live.
    /// </summary>
    public sealed class ExpansionUnlockDirector : MonoBehaviour
    {
        // Dialog copy for the zone-unlock confirmation (approved wireframe
        // docs/specs/ui/confirmation-dialog.md; the cost on Yes is supplied
        // separately by Core's ZoneUnlockOffer).
        private const string UnlockTitle = "Unlock this area?";
        private const string UnlockBody = "Open up the next neighborhood zone so you can build houses here.";

        private GameState state;
        private Transform worldRoot;
        private ConfirmationDialog dialog;
        private ExpansionDirector buildDirector;
        private ExpansionIndicatorView indicator;

        public void Init(GameState state, Transform worldRoot, ConfirmationDialog dialog,
            ExpansionDirector buildDirector)
        {
            this.state = state;
            this.worldRoot = worldRoot;
            this.dialog = dialog;
            this.buildDirector = buildDirector;

            indicator = Object.FindFirstObjectByType<ExpansionIndicatorView>();
            if (indicator != null)
            {
                indicator.UnlockRequested += OnUnlockRequested;
            }
        }

        /// <summary>The affordable lock was tapped (the view already gated on
        /// affordability). Resolves the live unlock offer for its cost and
        /// raises the confirmation dialog; Yes confirms the spend.</summary>
        private void OnUnlockRequested()
        {
            var offer = ZoneUnlockOffer.Resolve(state);
            if (offer == null)
            {
                return;
            }

            dialog.Open(UnlockTitle, UnlockBody, ConfirmUnlock, cost: offer.Value.Cost);
        }

        /// <summary>Yes: attempts the unlock through the single Core entry point.
        /// On success the new zone's empty lots appear (wired to the #57 build
        /// path), the indicator re-reads its live state (hiding once the last
        /// zone is unlocked), and the world is saved. On rejection (the balance
        /// dropped below the cost after the dialog opened, or nothing is left to
        /// unlock) nothing changes — Core is the sole authority on the spend.</summary>
        private void ConfirmUnlock()
        {
            if (!state.TryUnlockNextZone())
            {
                return;
            }

            var unlockedZone = state.UnlockedZones[state.UnlockedZones.Count - 1];
            foreach (var lot in unlockedZone.Lots)
            {
                if (state.IsLotBuildable(lot.HouseId))
                {
                    WorldBuilder.BuildEmptyLot(worldRoot, lot);
                }
            }

            if (buildDirector != null)
            {
                buildDirector.WireLots();
            }

            if (indicator != null)
            {
                indicator.Refresh();
            }

            SaveStore.Save(state);
        }
    }
}
