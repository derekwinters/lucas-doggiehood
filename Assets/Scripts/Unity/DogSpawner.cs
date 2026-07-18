using System.Linq;
using Doggiehood.Core.World;
using UnityEngine;

namespace Doggiehood.Unity
{
    /// <summary>
    /// Spawns a DogView for every dog in game state (#8, #106): street dogs
    /// start on the sidewalk nearest their house — the same attach point
    /// their front walkway (#128, replacing the driveway stub) connects to
    /// on the walk network — staggered along that sidewalk so housemates
    /// don't overlap; window dogs render at their house's window anchor.
    /// </summary>
    public static class DogSpawner
    {
        public const string DogNamePrefix = "Dog - ";
        private const float StaggerDistance = 2.5f;

        public static void SpawnDogs(GameState state, Transform parent)
        {
            var houses = Object.FindObjectsByType<HouseView>(FindObjectsSortMode.None)
                .ToDictionary(h => h.HouseId);

            var perHouseIndex = state.Houses.ToDictionary(h => h.Id, h => 0);

            foreach (var dog in state.Dogs)
            {
                var go = new GameObject(DogNamePrefix + dog.Name);
                go.transform.SetParent(parent);

                var index = perHouseIndex[dog.HouseId]++;
                go.transform.position = SidewalkSpawnPoint(dog.HouseId, index);

                houses.TryGetValue(dog.HouseId, out var house);
                go.AddComponent<DogView>().Init(dog, house != null ? house.WindowAnchor : null);
            }
        }

        /// <summary>The house's walkway attach point on the walk network
        /// (#128 — the sidewalk end of its front walkway), staggered along
        /// whichever sidewalk arm it sits on so multiple housemates don't
        /// spawn on top of each other.</summary>
        private static Vector3 SidewalkSpawnPoint(int houseId, int indexAtHouse)
        {
            var lot = NeighborhoodLayout.GetHouseLot(houseId);
            var network = NeighborhoodLayout.WalkNetwork;

            if (!network.TryGetFrontWalkway(houseId, out var walkway))
            {
                // A lot with no walkway (no sidewalk to attach to) keeps
                // the old lot-center spawn, off the walk network entirely,
                // so it stays at road/ground level.
                return new Vector3(lot.Position.X, WorldDimensions.RoadSurfaceHeight, lot.Position.Z);
            }

            var attach = walkway.B;

            var direction = new Vector3(1f, 0f, 0f);
            // #151: the attach point is always on a sidewalk, so its
            // ground height is the raised sidewalk surface, not the road's.
            var groundY = WorldDimensions.SidewalkSurfaceHeight;
            var sidewalkEdge = network.EdgesFrom(attach)
                .Where(e => e.Kind == WalkEdgeKind.Sidewalk)
                .Select(e => (WalkEdge?)e)
                .FirstOrDefault();

            if (sidewalkEdge.HasValue)
            {
                var other = sidewalkEdge.Value.Other(attach);
                var toOther = new Vector3(other.X - attach.X, 0f, other.Z - attach.Z);
                if (toOther.sqrMagnitude > 0.0001f)
                {
                    direction = toOther.normalized;
                }

                groundY = network.GroundHeight(attach, other);
            }

            var stagger = indexAtHouse * StaggerDistance;
            return new Vector3(attach.X, groundY, attach.Z) + direction * stagger;
        }
    }
}
