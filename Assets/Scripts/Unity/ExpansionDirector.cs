using System.Collections.Generic;
using System.Linq;
using Doggiehood.Core.Expansion;
using Doggiehood.Core.World;
using UnityEngine;

namespace Doggiehood.Unity
{
    /// <summary>
    /// Scene-side glue for building houses on empty lots (#57/#406): wires every
    /// EmptyLotView's tap to the reusable <see cref="ConfirmationDialog"/>, whose
    /// Yes calls GameState.TryBuildHouse and, on success, swaps the tapped lot's
    /// marker for the real house visual. The mirror of how
    /// <see cref="ExpansionUnlockDirector"/> wraps the zone-unlock spend (#343):
    /// every decision stays in Core (the cost via <see cref="HouseBuildOffer"/>,
    /// the occupied/locked/insufficient-balance rejections, the new house's
    /// level/vacancy) — this layer only opens the dialog and, on a confirmed
    /// build, asks WorldBuilder to render the result. A stray tap no longer
    /// spends coins: it must be confirmed first.
    /// </summary>
    public sealed class ExpansionDirector : MonoBehaviour
    {
        // Dialog copy for the house-build confirmation (approved shared component
        // docs/specs/ui/confirmation-dialog.md; the flat cost on Yes is supplied
        // separately by Core's HouseBuildOffer). Literal Yes/No labels and the
        // leaf confirm tint stay default — this is a friendly spend, not a danger
        // prompt.
        private const string BuildTitle = "Build a house here?";
        private const string BuildBody = "Spend coins to build a house on this lot.";

        public GameState State { get; private set; }

        private Transform worldRoot;
        private ConfirmationDialog dialog;
        private readonly HashSet<EmptyLotView> wiredLots = new HashSet<EmptyLotView>();

        public void Init(GameState state, Transform worldRoot, ConfirmationDialog dialog)
        {
            State = state;
            this.worldRoot = worldRoot;
            this.dialog = dialog;
            WireLots();
        }

        /// <summary>Subscribes every EmptyLotView in the scene to the build
        /// path, skipping any already wired — idempotent, so it can be called
        /// again after a mid-game zone unlock (#343) builds new lot markers,
        /// without double-firing existing ones.</summary>
        public void WireLots()
        {
            foreach (var lotView in Object.FindObjectsByType<EmptyLotView>(FindObjectsSortMode.None))
            {
                if (!wiredLots.Add(lotView))
                {
                    continue;
                }

                var houseId = lotView.HouseId;
                lotView.Tapped += () => OnLotTapped(houseId);
            }
        }

        /// <summary>#406: a lot tap is a build *request*, not an immediate
        /// spend. It resolves the live build offer for the lot's cost and raises
        /// the confirmation dialog; a null offer (the lot isn't buildable) is a
        /// no-op that never opens the dialog — mirroring how
        /// <see cref="ExpansionUnlockDirector.OnUnlockRequested"/> early-returns
        /// on a null unlock offer. Only Yes performs the build.</summary>
        private void OnLotTapped(int houseId)
        {
            var offer = HouseBuildOffer.Resolve(State, houseId);
            if (offer == null)
            {
                return;
            }

            dialog.Open(BuildTitle, BuildBody, () => ConfirmBuild(houseId), cost: offer.Value.Cost);
        }

        /// <summary>Yes: builds the house through the single Core entry point. On
        /// success the marker is replaced by the real house visual and the world
        /// is saved; on rejection (occupied, locked zone, insufficient balance)
        /// nothing changes — Core is the sole authority on the spend, and the
        /// currency HUD reads the wallet live, so an untouched balance is itself
        /// the rejection feedback.</summary>
        private void ConfirmBuild(int houseId)
        {
            if (!State.TryBuildHouse(houseId))
            {
                return;
            }

            var marker = Object.FindObjectsByType<EmptyLotView>(FindObjectsSortMode.None)
                .SingleOrDefault(view => view.HouseId == houseId);
            if (marker != null)
            {
                DestroyMarker(marker.gameObject);
            }

            var house = State.Houses.Single(h => h.Id == houseId);
            var lot = State.GetHouseLot(houseId);
            WorldBuilder.BuildHouse(worldRoot, house, lot);

            // #405: a starting house gets its front walkway, yard trees, and
            // fence at world-build time; a mid-game zone-lot build only rendered
            // the mesh. Render the same three treatments for the newly built lot
            // so it matches a starting-neighborhood house.
            WorldBuilder.BuildWalkway(worldRoot, lot);
            WorldBuilder.BuildYardLandscaping(worldRoot, lot);
            WorldBuilder.BuildFence(worldRoot, lot);

            SaveStore.Save(State);
        }

        private static void DestroyMarker(GameObject marker)
        {
            if (Application.isPlaying)
            {
                Destroy(marker);
            }
            else
            {
                DestroyImmediate(marker);
            }
        }
    }
}
