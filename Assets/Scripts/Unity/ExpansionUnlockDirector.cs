using System.Collections.Generic;
using Doggiehood.Core.Expansion;
using Doggiehood.Core.World;
using UnityEngine;

namespace Doggiehood.Unity
{
    /// <summary>
    /// Scene-side glue for the multi-lock map-expansion trigger (#453, Derek's
    /// Option A). Builds and maintains ONE <see cref="ExpansionIndicatorView"/>
    /// per currently-unlockable frontier coordinate (one lock per open connection
    /// point) — spawning a lock for a newly-unlockable coordinate and destroying
    /// the one whose coordinate got placed — via
    /// <see cref="WorldBuilder.SyncExpansionIndicators"/>, refreshed on the same
    /// per-frame cadence the retired single view used. Tapping an affordable lock
    /// raises the reusable <see cref="ConfirmationDialog"/> with that coordinate's
    /// cost; confirming (Yes) calls <see cref="GameState.TryUnlockTile"/> for
    /// THAT coordinate, renders the just-unlocked tile as real neighborhood
    /// (#373), wires its empty lots to the #57 build path, grows the camera pan
    /// bounds, reconciles the lock set, and saves. Every decision stays in Core
    /// (cost, affordability gate, the unlock and its rejections); this layer only
    /// opens the dialog and applies the result. The mirror of how
    /// <see cref="ExpansionDirector"/> wires EmptyLotView → TryBuildHouse.
    /// </summary>
    public sealed class ExpansionUnlockDirector : MonoBehaviour
    {
        // Dialog copy for the tile-unlock confirmation (approved wireframe
        // docs/specs/ui/confirmation-dialog.md; the cost on Yes is supplied
        // separately from the #295 TileUnlock pricing path).
        private const string UnlockTitle = "Unlock this area?";
        private const string UnlockBody = "Open up this part of the neighborhood so you can build houses here.";

        private GameState state;
        private Transform worldRoot;
        private ConfirmationDialog dialog;
        private ExpansionDirector buildDirector;

        private readonly Dictionary<TileCoordinate, ExpansionIndicatorView> views =
            new Dictionary<TileCoordinate, ExpansionIndicatorView>();
        private Sprite affordableSprite;
        private Sprite lockedSprite;
        private bool spritesLoaded;

        public void Init(GameState state, Transform worldRoot, ConfirmationDialog dialog,
            ExpansionDirector buildDirector)
        {
            this.state = state;
            this.worldRoot = worldRoot;
            this.dialog = dialog;
            this.buildDirector = buildDirector;

            spritesLoaded = WorldBuilder.TryLoadExpansionIndicatorSprites(out affordableSprite, out lockedSprite);
            Sync();
        }

        /// <summary>Test seam: supply the lock sprites directly so an EditMode
        /// test needn't depend on the staged icon Resource importing. Rebuilds the
        /// current lock set with the supplied sprites.</summary>
        public void UseSprites(Sprite affordable, Sprite locked)
        {
            affordableSprite = affordable;
            lockedSprite = locked;
            spritesLoaded = true;
            Sync();
        }

        private void Update()
        {
            Sync();
        }

        /// <summary>Reconciles the live lock set against Core's frontier and wires
        /// each freshly-spawned lock's tap to the unlock flow. A no-op until the
        /// lock sprites have loaded (no designed graybox stand-in for the icon).</summary>
        private void Sync()
        {
            if (state == null || !spritesLoaded)
            {
                return;
            }

            WorldBuilder.SyncExpansionIndicators(
                worldRoot, state, views, affordableSprite, lockedSprite,
                view => view.UnlockRequested += OnUnlockRequested);
        }

        /// <summary>An affordable lock was tapped (the view already gated on
        /// affordability + frontier membership). Raises the confirmation dialog
        /// showing that coordinate's flat unlock cost; Yes confirms the
        /// spend.</summary>
        private void OnUnlockRequested(TileCoordinate coordinate)
        {
            var cost = TileUnlock.Cost(state.Map.Tiles.Count);
            dialog.Open(UnlockTitle, UnlockBody, () => ConfirmUnlock(coordinate), cost: cost);
        }

        /// <summary>Yes: attempts the unlock for THIS coordinate through the single
        /// Core entry point. On success the tile renders as real neighborhood —
        /// grass under it and roads along it (#373) — its empty lots are wired to
        /// the #57 build path, the camera pan bounds grow so the player can reach
        /// it, the lock set is reconciled (this coordinate's lock disappears; any
        /// newly-opened frontier locks appear), and the world is saved. On
        /// rejection (the balance dropped below the cost after the dialog opened,
        /// or the coordinate left the frontier) nothing changes — Core is the sole
        /// authority on the spend.</summary>
        private void ConfirmUnlock(TileCoordinate coordinate)
        {
            if (!state.TryUnlockTile(coordinate))
            {
                return;
            }

            WorldBuilder.RenderUnlockedTile(worldRoot, state, coordinate);

            if (buildDirector != null)
            {
                buildDirector.WireLots();
            }

            GrowCameraBoundsToMap();
            Sync();
            SaveStore.Save(state);
        }

        /// <summary>Recomputes the live camera rig's pan bounds from the newly
        /// extended map (#373) so <c>Pan</c>/<c>FocusOn</c> can reach the just
        /// unlocked tile. The decision lives in Core
        /// (<see cref="Doggiehood.Core.Cameras.CameraController.RecomputeBoundsFromMap"/>);
        /// this only feeds it the live <see cref="GameState.Map"/> and re-applies
        /// the result to the rig. Tolerates no rig (mirrors how the rest of the
        /// scene wiring degrades gracefully when one isn't present).</summary>
        private void GrowCameraBoundsToMap()
        {
            var rig = Object.FindFirstObjectByType<CameraRig>();
            if (rig == null)
            {
                return;
            }

            rig.Controller.RecomputeBoundsFromMap(state.Map);
            rig.ApplyConfiguration();
        }
    }
}
