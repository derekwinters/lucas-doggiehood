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
        private System.Action<HouseView> onHouseBuilt;
        private readonly HashSet<EmptyLotView> wiredLots = new HashSet<EmptyLotView>();

        /// <summary>#456: a house built here is the only house that is both vacant
        /// and never rebuilt, so — unlike a starter house (wired at bootstrap) or
        /// an occupied house (re-wired on every move-in/upgrade rebuild) — nothing
        /// else subscribes to its <see cref="HouseView.Tapped"/>. WorldBootstrap
        /// passes <paramref name="onHouseBuilt"/> (the same wiring it hands the
        /// HouseUpgradeDirector rebuild callback) so the freshly built house's tap
        /// opens its profile. Optional: EditMode tests that don't exercise the tap
        /// leave it null.</summary>
        public void Init(GameState state, Transform worldRoot, ConfirmationDialog dialog,
            System.Action<HouseView> onHouseBuilt = null)
        {
            State = state;
            this.worldRoot = worldRoot;
            this.dialog = dialog;
            this.onHouseBuilt = onHouseBuilt;
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

            // #430: build against the LIVE map-spanning walk network, not the
            // starting-tile singleton — a zone lot's front-walkway edge (and so
            // its street-ward facing + front-setback position) lives only there.
            // With it, the zone house now faces the street and renders a real
            // walkway instead of the Z-sign fallback / no-op.
            var houseRoot = WorldBuilder.BuildHouse(worldRoot, house, lot, State.WalkNetwork);

            // #456: wire the freshly built (vacant) house's tap so it opens its
            // profile — the break the issue reported. Nothing else covers a house
            // that has never been rebuilt, so without this the tap did nothing.
            var houseView = houseRoot.GetComponent<HouseView>();
            if (houseView != null)
            {
                onHouseBuilt?.Invoke(houseView);
            }

            // #405: a starting house gets its front walkway and fence at
            // world-build time; a mid-game zone-lot build only rendered the mesh.
            // Render those treatments for the newly built lot so it matches a
            // starting-neighborhood house.
            // #434: the lot's yard trees were ALREADY placed when its zone
            // unlocked (WorldBuilder.BuildEmptyLots / RenderUnlockedZone), so we
            // deliberately do NOT re-render them here — only the foundation slab
            // (destroyed above) is swapped for the house mesh. Re-rendering would
            // duplicate the "Yard - N" container.
            WorldBuilder.BuildWalkway(worldRoot, lot, State.WalkNetwork);
            WorldBuilder.BuildFence(worldRoot, lot, State);

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
