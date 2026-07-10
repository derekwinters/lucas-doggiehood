using System.Linq;
using Doggiehood.Core.World;
using UnityEngine;

namespace Doggiehood.Unity
{
    /// <summary>
    /// Spawns a DogView for every dog in game state (#8): street dogs start
    /// on the street stretch nearest their house (staggered so housemates
    /// don't overlap); window dogs render at their house's window anchor.
    /// </summary>
    public static class DogSpawner
    {
        public const string DogNamePrefix = "Dog - ";

        public static void SpawnDogs(GameState state, Transform parent)
        {
            var houses = Object.FindObjectsByType<HouseView>(FindObjectsSortMode.None)
                .ToDictionary(h => h.HouseId);

            var perHouseIndex = state.Houses.ToDictionary(h => h.Id, h => 0);

            foreach (var dog in state.Dogs)
            {
                var go = new GameObject(DogNamePrefix + dog.Name);
                go.transform.SetParent(parent);

                var lot = NeighborhoodLayout.GetHouseLot(dog.HouseId);
                var stagger = perHouseIndex[dog.HouseId]++ * 2.5f;
                // Nearest north-south street point to the house, staggered
                // along the street for multi-dog households.
                go.transform.position = new Vector3(0f, 0f, lot.Position.Z + stagger - 2.5f);

                houses.TryGetValue(dog.HouseId, out var house);
                go.AddComponent<DogView>().Init(dog, house != null ? house.WindowAnchor : null);
            }
        }
    }
}
