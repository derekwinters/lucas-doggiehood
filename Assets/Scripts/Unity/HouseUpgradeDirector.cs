using System.Linq;
using Doggiehood.Core.World;
using UnityEngine;

namespace Doggiehood.Unity
{
    /// <summary>
    /// Scene-side glue for house upgrades (#407). Core raises a house's level
    /// (<see cref="GameState.TryUpgradeHouse"/>), but the mesh is chosen by
    /// level <b>once</b>, at <see cref="WorldBuilder.BuildHouse(Transform, House, HouseLot)"/>
    /// time — so the already-built <see cref="HouseView"/> keeps its old mesh
    /// and the home never visibly grows. This director re-renders the world
    /// house after a successful upgrade, the mirror of how
    /// <see cref="ExpansionDirector"/> swaps an empty-lot marker for the real
    /// house on build: it finds the stale <see cref="HouseView"/> for the
    /// upgraded id, destroys it, and rebuilds through <c>BuildHouse</c> at the
    /// house's current level, then hands the fresh view to a re-wire callback
    /// so tap-to-open-profile (#208) and quest spray routing (#53) reach the
    /// rebuilt object (a fresh <see cref="HouseView"/> neither bootstrap loop
    /// has seen).
    ///
    /// Every decision stays in Core: the level and the mesh-by-level table
    /// (<see cref="Doggiehood.Core.Art.HouseLevelModelTable"/>) are Core's;
    /// this layer only re-renders and re-wires. Because <c>BuildHouse</c>
    /// re-derives everything from the same Core <see cref="House"/> +
    /// <see cref="HouseLot"/>, a zone house's rolled ladder/tint (#299) and a
    /// vacant house's greyscale (#58) survive the rebuild unchanged.
    /// </summary>
    public sealed class HouseUpgradeDirector : MonoBehaviour
    {
        public GameState State { get; private set; }

        private Transform worldRoot;
        private System.Action<HouseView> onHouseRebuilt;

        public void Init(GameState state, Transform worldRoot, System.Action<HouseView> onHouseRebuilt)
        {
            State = state;
            this.worldRoot = worldRoot;
            this.onHouseRebuilt = onHouseRebuilt;
        }

        /// <summary>Re-renders the world house for an already-upgraded id:
        /// destroys the stale <see cref="HouseView"/> and rebuilds it at the
        /// house's current level via <c>BuildHouse</c>, then re-wires the fresh
        /// view's tap handlers through the injected callback. Called only after
        /// a successful <see cref="GameState.TryUpgradeHouse"/>, so the house
        /// and its lot always resolve. Returns the rebuilt view.</summary>
        public HouseView RefreshHouse(int houseId)
        {
            var stale = FindHouseView(houseId);
            if (stale != null)
            {
                DestroyView(stale.gameObject);
            }

            var house = State.Houses.Single(h => h.Id == houseId);
            var lot = State.GetHouseLot(houseId);
            var rebuilt = WorldBuilder.BuildHouse(worldRoot, house, lot).GetComponent<HouseView>();

            onHouseRebuilt?.Invoke(rebuilt);
            return rebuilt;
        }

        private static HouseView FindHouseView(int houseId)
        {
            foreach (var view in Object.FindObjectsByType<HouseView>(FindObjectsSortMode.None))
            {
                if (view.HouseId == houseId)
                {
                    return view;
                }
            }

            return null;
        }

        private static void DestroyView(GameObject view)
        {
            if (Application.isPlaying)
            {
                Destroy(view);
            }
            else
            {
                DestroyImmediate(view);
            }
        }
    }
}
