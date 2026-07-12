using System.Collections.Generic;
using System.Linq;
using Doggiehood.Core.Quests;
using Doggiehood.Core.World;
using UnityEngine;

namespace Doggiehood.Unity
{
    /// <summary>
    /// Scene-side glue for the quest loop (#23, #30, #31, #53): watches
    /// accepted quests and animates their consequences — walks buy-quest
    /// dogs home at medium speed along the sidewalk/crosswalk network
    /// (#106), spawns the delivery truck, places lost items in the world,
    /// routes house taps to the spray action, and saves after every
    /// completion. Every decision stays in Core: the route itself comes
    /// from <see cref="Doggiehood.Core.World.WalkNetwork.FindPath"/> —
    /// this layer only walks the returned waypoints frame by frame.
    /// </summary>
    public sealed class QuestDirector : MonoBehaviour
    {
        private const float WalkHomeSpeed = 1.6f;
        private const float RestTickInterval = 2f;
        private const float WaypointArriveDistance = 0.05f;

        public GameState State { get; private set; }

        private Transform worldRoot;
        private readonly System.Random restRng = new System.Random();
        private float restTickTimer;

        // Per-quest home route (Core-computed waypoints) and how far along
        // it that dog has walked so far.
        private readonly Dictionary<int, List<Vector3>> homeRoutes = new Dictionary<int, List<Vector3>>();
        private readonly Dictionary<int, int> homeRouteProgress = new Dictionary<int, int>();

        public void Init(GameState state, Transform worldRoot)
        {
            State = state;
            this.worldRoot = worldRoot;

            foreach (var house in Object.FindObjectsByType<HouseView>(FindObjectsSortMode.None))
            {
                var houseId = house.HouseId;
                house.Tapped += () => State.Quests.SprayHouse(houseId);
            }

            RefreshDecorations();
        }

        /// <summary>Ensures every Core decoration has a scene view — spawns
        /// loaded-save decorations at Init and new deliveries as they land.</summary>
        public void RefreshDecorations()
        {
            var existing = Object.FindObjectsByType<DecorationView>(FindObjectsSortMode.None)
                .Select(v => v.Decoration)
                .ToHashSet();

            foreach (var decoration in State.Decorations)
            {
                if (!existing.Contains(decoration))
                {
                    DecorationView.Spawn(decoration, worldRoot);
                }
            }
        }

        /// <summary>Called by the presenter when a quest is accepted.</summary>
        public void OnQuestAccepted(Quest quest)
        {
            if (quest.Type == QuestType.LostItem)
            {
                LostItemView.Spawn(State, quest, worldRoot);
            }

            SaveStore.Save(State);
        }

        private void Update()
        {
            Tick(Time.deltaTime);

            // Autonomous comfort use (#52): Core decides who rests and when.
            restTickTimer += Time.deltaTime;
            if (restTickTimer >= RestTickInterval)
            {
                restTickTimer = 0f;
                foreach (var dog in State.Dogs)
                {
                    Doggiehood.Core.Decorations.RestBehavior.Tick(dog, State, restRng);
                }
            }
        }

        /// <summary>Advances every heading-home quest's walk; called by
        /// Update at runtime and directly by EditMode tests.</summary>
        public void Tick(float deltaTime)
        {
            foreach (var quest in State.Quests.ActiveQuests.ToList())
            {
                if (quest.DeliveryPhase == DeliveryPhase.HeadingHome)
                {
                    WalkDogHome(quest, deltaTime);
                }
            }
        }

        private void WalkDogHome(Quest quest, float deltaTime)
        {
            var view = Object.FindObjectsByType<DogView>(FindObjectsSortMode.None)
                .FirstOrDefault(v => v.Dog.Name == quest.DogName);
            if (view == null)
            {
                return;
            }

            var route = GetOrComputeRoute(quest, view);
            var index = homeRouteProgress[quest.Id];
            var target = route[index];

            view.transform.position = Vector3.MoveTowards(view.transform.position, target, WalkHomeSpeed * deltaTime);

            if (Vector3.Distance(view.transform.position, target) > WaypointArriveDistance)
            {
                return;
            }

            if (index + 1 < route.Count)
            {
                homeRouteProgress[quest.Id] = index + 1;
                return;
            }

            homeRoutes.Remove(quest.Id);
            homeRouteProgress.Remove(quest.Id);

            State.Quests.NotifyDogArrivedHome(quest);
            DeliveryTruckView.Spawn(worldRoot).DeliverTo(target, () =>
            {
                State.Quests.DeliverPackage(quest);
                RefreshDecorations();
                SaveStore.Save(State);
            });
        }

        /// <summary>
        /// Core computes the actual route (#106): shortest path over the
        /// sidewalk/crosswalk/driveway-stub network from the dog's current
        /// position to its house lot, ending via that lot's driveway stub.
        /// Computed once per quest and cached — this layer only walks it.
        /// </summary>
        private List<Vector3> GetOrComputeRoute(Quest quest, DogView view)
        {
            if (homeRoutes.TryGetValue(quest.Id, out var existing))
            {
                return existing;
            }

            var lot = NeighborhoodLayout.GetHouseLot(view.Dog.HouseId);
            var start = new GridPoint(view.transform.position.x, view.transform.position.z);
            var waypoints = NeighborhoodLayout.WalkNetwork.FindPath(start, lot.Position);

            var route = waypoints.Count > 0
                ? waypoints.Select(p => new Vector3(p.X, 0f, p.Z)).ToList()
                : new List<Vector3> { new Vector3(lot.Position.X, 0f, lot.Position.Z) };

            homeRoutes[quest.Id] = route;
            homeRouteProgress[quest.Id] = 0;
            return route;
        }
    }
}
