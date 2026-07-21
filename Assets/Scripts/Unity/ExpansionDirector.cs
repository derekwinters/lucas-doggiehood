using System.Linq;
using Doggiehood.Core.World;
using UnityEngine;

namespace Doggiehood.Unity
{
    /// <summary>
    /// Scene-side glue for building houses on empty lots (#57): wires every
    /// EmptyLotView's tap to GameState.TryBuildHouse, swapping the tapped
    /// lot's marker for the real house visual on success. Every decision
    /// stays in Core (the cost, the occupied/locked/insufficient-balance
    /// rejections, the new house's level/vacancy) — this layer only reacts
    /// to the tap and asks WorldBuilder to render the result. Same pattern
    /// as QuestDirector's HouseView tap wiring.
    /// </summary>
    public sealed class ExpansionDirector : MonoBehaviour
    {
        public GameState State { get; private set; }

        private Transform worldRoot;

        public void Init(GameState state, Transform worldRoot)
        {
            State = state;
            this.worldRoot = worldRoot;

            foreach (var lotView in Object.FindObjectsByType<EmptyLotView>(FindObjectsSortMode.None))
            {
                var houseId = lotView.HouseId;
                lotView.Tapped += () => OnLotTapped(houseId);
            }
        }

        /// <summary>#57: a lot tap is a build attempt. On success the
        /// marker is replaced by the real house visual and the world is
        /// saved; on rejection (occupied, locked zone, insufficient
        /// balance) nothing changes — the currency HUD already reads the
        /// wallet live, so an untouched balance is itself the rejection
        /// feedback.</summary>
        private void OnLotTapped(int houseId)
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
