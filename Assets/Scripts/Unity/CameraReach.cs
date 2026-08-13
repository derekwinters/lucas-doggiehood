using Doggiehood.Core.World;

namespace Doggiehood.Unity
{
    /// <summary>
    /// The one place the camera's reach — its pan bounds and its maximum
    /// zoom-out — is brought back into step with the live tile map (#691).
    ///
    /// <para>Both are derived from the live tile extent and nothing about them
    /// is persisted: <see cref="CameraRig"/> re-runs
    /// <c>CameraController.ForStartingNeighborhood()</c> on every scene load.
    /// So they have to be re-derived whenever that extent changes <em>or is
    /// restored</em> — at launch as well as on a zone unlock
    /// (docs/specs/world/camera-controls.md). Before #691 the live-unlock path
    /// was the only production caller, which is why a loaded save was pinned to
    /// the starting intersection's limits until the player unlocked something.
    /// Keeping the one call here rather than duplicated per trigger is what
    /// stops the next trigger from quietly missing it.</para>
    ///
    /// <para>The decision itself is Core's
    /// (<see cref="Doggiehood.Core.Cameras.CameraController.RecomputeBoundsFromMap"/>,
    /// which also re-clamps the current position and zoom into the new range);
    /// this only feeds it the live <see cref="GameState.Map"/> and re-applies
    /// the result to the rig. Tolerates no rig, mirroring how the rest of the
    /// scene wiring degrades gracefully when one isn't present.</para>
    /// </summary>
    public static class CameraReach
    {
        public static void SyncToLiveMap(GameState state)
        {
            var rig = UnityEngine.Object.FindFirstObjectByType<CameraRig>();
            if (rig == null)
            {
                return;
            }

            rig.Controller.RecomputeBoundsFromMap(state.Map);
            rig.ApplyConfiguration();
        }
    }
}
