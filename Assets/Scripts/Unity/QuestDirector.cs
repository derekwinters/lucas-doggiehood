using System.Collections.Generic;
using System.Linq;
using Doggiehood.Core.Economy;
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
        private HouseUpgradeDirector houseRefresher;
        private readonly System.Random restRng = new System.Random();
        private float restTickTimer;

        // Per-quest home route (Core-computed waypoints) and how far along
        // it that dog has walked so far.
        private readonly Dictionary<int, List<Vector3>> homeRoutes = new Dictionary<int, List<Vector3>>();
        private readonly Dictionary<int, int> homeRouteProgress = new Dictionary<int, int>();

        // #600: the set of delivery trucks currently on the road. The director
        // owns their tick (the views no longer self-tick) so that, each frame, a
        // follower can see the truck ahead of it on its segment and hold one car
        // length back — car-following the crosswalk gate alone can't provide.
        private readonly List<DeliveryTruckView> activeTrucks = new List<DeliveryTruckView>();

        // HouseViews whose Tapped is already wired to the spray path — so a
        // re-wire after #407 rebuilds one on upgrade never double-subscribes an
        // existing view (the same idempotency ExpansionDirector.WireLots uses).
        private readonly HashSet<HouseView> wiredHouses = new HashSet<HouseView>();

        /// <summary>#436: <paramref name="houseRefresher"/> re-renders the filled
        /// house on a move-in so its vacancy greyscale (#58) drops the moment a
        /// household moves in — the same destroy-and-rebuild
        /// <see cref="HouseUpgradeDirector.RefreshHouse"/> already does for the
        /// #407 upgrade path, preserving the house's rolled variant/tint. Left
        /// null (e.g. a bare EditMode Init that isn't exercising move-in) simply
        /// skips the re-render; the dogs still spawn.</summary>
        public void Init(GameState state, Transform worldRoot, HouseUpgradeDirector houseRefresher = null)
        {
            State = state;
            this.worldRoot = worldRoot;
            this.houseRefresher = houseRefresher;

            WireHouses();
            RefreshDecorations();
            RefreshBugSwarms();

            // #436: reflect a move-in the instant Core reports one — spawn the
            // new resident(s) and drop the filled house's vacancy tint.
            State.Quests.MoveInOccurred += OnMoveInOccurred;
        }

        private void OnDestroy()
        {
            if (State != null)
            {
                State.Quests.MoveInOccurred -= OnMoveInOccurred;
            }
        }

        /// <summary>#436: a household just moved into a previously vacant house.
        /// Spawns a <see cref="DogView"/> for each new member — bound to their
        /// houseId, tappable and wandering, via the shared
        /// <see cref="DogSpawner.SpawnDog"/> — without touching any existing
        /// DogView, then re-renders the filled house so its vacancy greyscale
        /// drops while its variant/tint is preserved. Core has already added the
        /// household to the roster and flipped the house occupied.</summary>
        private void OnMoveInOccurred(IReadOnlyList<Doggiehood.Core.Dogs.Dog> household)
        {
            if (household == null || household.Count == 0)
            {
                return;
            }

            var houseId = household[0].HouseId;

            // Stagger the new residents after any dogs already shown at this
            // house (a vacant house has none, but stay robust if it doesn't).
            var index = Object.FindObjectsByType<DogView>(FindObjectsSortMode.None)
                .Count(v => v.Dog != null && v.Dog.HouseId == houseId);

            foreach (var dog in household)
            {
                DogSpawner.SpawnDog(State, worldRoot, dog, index);
                index++;
            }

            // Drop the vacancy tint by rebuilding the now-occupied house (#58),
            // reusing the #407 destroy-and-rebuild that preserves variant/tint.
            houseRefresher?.RefreshHouse(houseId);

            SaveStore.Save(State);
        }

        /// <summary>Subscribes every <see cref="HouseView"/> in the scene to the
        /// spray path, skipping any already wired — idempotent, so it can be
        /// called again after #407 rebuilds a HouseView on upgrade (a fresh
        /// object this loop hasn't seen) without double-firing existing ones.
        /// Mirrors <see cref="ExpansionDirector.WireLots"/> for EmptyLotView.</summary>
        public void WireHouses()
        {
            foreach (var house in Object.FindObjectsByType<HouseView>(FindObjectsSortMode.None))
            {
                if (!wiredHouses.Add(house))
                {
                    continue;
                }

                var houseId = house.HouseId;

                // #670: supply the live "does this house have bugs?" predicate
                // the Core arbiter resolves the tap with, then take only the
                // spray outcome. The profile-open outcome is WorldBootstrap's,
                // and the two are now mutually exclusive by construction rather
                // than by two subscribers both firing on one tap.
                house.HasPendingSpray = () => State.Quests.IsAwaitingSpray(houseId);
                house.SprayRequested += () => OnHouseSprayed(houseId);
            }
        }

        /// <summary>#53: a house tap on a bugged house is a spray. When it
        /// clears the bug quest, the swarm feedback is removed and the world
        /// saved.</summary>
        private void OnHouseSprayed(int houseId)
        {
            if (State.Quests.SprayHouse(houseId))
            {
                RefreshBugSwarms();
                SaveStore.Save(State);
            }
        }

        /// <summary>#53/#157: keeps a bug swarm on exactly the houses Core
        /// reports as awaiting a spray — spawns one on newly-bugged houses,
        /// removes it from houses that have been sprayed. Idempotent, so both
        /// accept-time and spray-time just re-sync.</summary>
        public void RefreshBugSwarms()
        {
            var needed = new HashSet<int>(State.Quests.HousesAwaitingSpray());
            var existing = Object.FindObjectsByType<BugSwarmView>(FindObjectsSortMode.None);

            foreach (var swarm in existing)
            {
                if (needed.Contains(swarm.HouseId))
                {
                    needed.Remove(swarm.HouseId);
                }
                else if (Application.isPlaying)
                {
                    Destroy(swarm.gameObject);
                }
                else
                {
                    DestroyImmediate(swarm.gameObject);
                }
            }

            if (needed.Count == 0)
            {
                return;
            }

            var houses = Object.FindObjectsByType<HouseView>(FindObjectsSortMode.None)
                .ToDictionary(h => h.HouseId, h => h.transform);
            foreach (var houseId in needed)
            {
                if (houses.TryGetValue(houseId, out var houseTransform))
                {
                    BugSwarmView.Spawn(houseId, houseTransform, worldRoot);
                }
            }
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
            else if (quest.Type == QuestType.PestControl)
            {
                RefreshBugSwarms();
            }
            else if (quest.Type == QuestType.BuyGift
                && quest.ItemName == ItemCatalog.FenceItemName)
            {
                // #318: the fence purchase has no delivery truck — Core already
                // completed it and recorded the placed fence on accept, so
                // rebuild the fences here to show it immediately, no animation
                // and no walk-home (Tick never runs for it — its DeliveryPhase
                // stays None, not HeadingHome).
                WorldBuilder.RebuildFences(worldRoot, State);
            }

            SaveStore.Save(State);
        }

        private void Update()
        {
            Tick(Time.deltaTime);

            // Autonomous comfort use (#52, #112): Core decides who walks over
            // to their comfort item and when; the walk-over itself is driven
            // frame-by-frame by each DogView. No teleport into the Rest pose.
            restTickTimer += Time.deltaTime;
            if (restTickTimer >= RestTickInterval)
            {
                restTickTimer = 0f;
                TickRestApproaches();
            }
        }

        /// <summary>#112: on each rest tick, ask Core whether any wandering dog
        /// with a comfort decoration should start walking over to it. The
        /// gating roll and the route both live in Core
        /// (<see cref="Doggiehood.Core.Decorations.RestBehavior.TryBeginApproach"/>);
        /// this layer only hands the returned approach to the dog's view to
        /// walk. Dogs already en route are skipped. #677: the route is planned
        /// over the LIVE map-spanning network, so a dog on an unlocked tile walks
        /// over to its comfort item along real sidewalks instead of beelining
        /// toward the origin tile; and one dog's failure can't stop the others'.
        /// Called by Update at runtime and directly by EditMode tests.</summary>
        public void TickRestApproaches()
        {
            var network = State.WalkNetwork;
            foreach (var view in Object.FindObjectsByType<DogView>(FindObjectsSortMode.None))
            {
                if (view.IsApproachingRest)
                {
                    continue;
                }

                try
                {
                    var position = new GridPoint(view.transform.position.x, view.transform.position.z);
                    var approach = Doggiehood.Core.Decorations.RestBehavior.TryBeginApproach(
                        view.Dog, State, position, network, restRng);
                    if (approach != null)
                    {
                        view.BeginRestApproach(approach);
                    }
                }
                catch (System.Exception failure)
                {
                    Debug.LogException(failure);
                }
            }
        }

        /// <summary>Advances every heading-home quest's walk; called by
        /// Update at runtime and directly by EditMode tests.
        ///
        /// #677: each quest's step is isolated. Nothing here used to catch, so one
        /// unroutable delivery threw out of Update every frame and took
        /// <see cref="TickTrucks"/> and the rest approaches down with it — every
        /// later delivery in the session was broken too, and any truck already on
        /// the road simply stopped. One quest's failure now stays that quest's
        /// failure, and that quest fails safely (<see cref="AbandonDelivery"/>)
        /// rather than retrying forever.</summary>
        public void Tick(float deltaTime)
        {
            foreach (var quest in State.Quests.ActiveQuests.ToList())
            {
                if (quest.DeliveryPhase != DeliveryPhase.HeadingHome)
                {
                    continue;
                }

                try
                {
                    WalkDogHome(quest, deltaTime);
                }
                catch (System.Exception failure)
                {
                    Debug.LogException(failure);
                    AbandonDelivery(quest);
                }
            }

            TickTrucks(deltaTime);
        }

        /// <summary>#677: this quest's delivery can't be carried out — the walk
        /// home couldn't be planned, or the truck couldn't be routed to the door.
        /// Core resolves it safely (the paid-for item still arrives and the dog
        /// goes back to wandering instead of waiting for a truck that will never
        /// come); this layer drops the cached route, shows any delivered
        /// decoration, and saves.</summary>
        private void AbandonDelivery(Quest quest)
        {
            homeRoutes.Remove(quest.Id);
            homeRouteProgress.Remove(quest.Id);

            State.Quests.FailDelivery(quest);
            RefreshDecorations();
            SaveStore.Save(State);
        }

        /// <summary>#600: advances every active delivery truck, holding each one
        /// car length behind its immediate leader on its road segment. The set is
        /// snapshotted first so every follower's leader is resolved from the same
        /// start-of-tick positions regardless of advance order; leader selection
        /// and the following clamp are pure Core
        /// (<see cref="RoadTraffic"/> / <see cref="CarFollowing"/>). Finished
        /// trucks (destroyed views) are pruned each tick.</summary>
        private void TickTrucks(float deltaTime)
        {
            activeTrucks.RemoveAll(t => t == null || t.IsGone);

            var snapshot = new List<(object Segment, float TravelSign, float Along)>();
            foreach (var truck in activeTrucks)
            {
                if (truck.IsDriving)
                {
                    snapshot.Add((truck.CurrentSegmentKey, truck.TravelSign, truck.CurrentAlong));
                }
            }

            // ToList so a truck that finishes and destroys itself mid-loop can't
            // disturb the iteration.
            foreach (var truck in activeTrucks.ToList())
            {
                if (truck == null)
                {
                    continue;
                }

                // #677: one truck's failure must not stop the others driving.
                try
                {
                    float? leaderAlong = null;
                    if (truck.IsDriving)
                    {
                        leaderAlong = RoadTraffic.ImmediateLeaderAlong(
                            truck.CurrentSegmentKey, truck.TravelSign, truck.CurrentAlong, snapshot);
                    }

                    truck.Tick(deltaTime, leaderAlong);
                }
                catch (System.Exception failure)
                {
                    Debug.LogException(failure);
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

            // #470: the scripted walk owns the transform — turn to face the
            // next waypoint before stepping toward it (no moonwalk), and the
            // stale wander target was already dropped by BeginQuestWalk when
            // this route was first computed.
            view.WalkTowardWaypoint(target, WalkHomeSpeed * deltaTime);

            if (Vector3.Distance(view.transform.position, target) > WaypointArriveDistance)
            {
                return;
            }

            // Snap exactly onto the waypoint on arrival — MoveTowards only
            // guarantees landing within WaypointArriveDistance, and that
            // slack must not leak into the dog's final resting position.
            view.transform.position = target;

            if (index + 1 < route.Count)
            {
                homeRouteProgress[quest.Id] = index + 1;
                return;
            }

            homeRoutes.Remove(quest.Id);
            homeRouteProgress.Remove(quest.Id);

            // #677: the dog is standing on the last waypoint of a route that can
            // only end at its own front door, so this is the one place it may be
            // reported home — never a sidewalk the destination got snapped to.
            State.Quests.NotifyDogArrivedHome(quest);

            Dispatch(target, () =>
            {
                State.Quests.DeliverPackage(quest);
                RefreshDecorations();
                SaveStore.Save(State);
            });
        }

        /// <summary>#677: how a delivery is dispatched once its dog is at its
        /// door. Production spawns the real truck; an EditMode test substitutes a
        /// dispatch that fails, to prove one failed delivery is contained and its
        /// dog is never left stranded in the waiting pose. Leave null for the
        /// production behavior.</summary>
        public System.Action<Vector3, System.Action> DeliveryDispatcher { get; set; }

        private void Dispatch(Vector3 door, System.Action onDelivered)
        {
            if (DeliveryDispatcher != null)
            {
                DeliveryDispatcher(door, onDelivered);
                return;
            }

            var truck = DeliveryTruckView.Spawn(worldRoot);
            try
            {
                truck.DeliverTo(door, State.Map, State.WalkNetwork, onDelivered);
            }
            catch
            {
                // #677: an undrivable route leaves no half-spawned truck parked in
                // the world (or in the ticked set) — clean up, then let the caller
                // contain the failure.
                if (Application.isPlaying)
                {
                    Destroy(truck.gameObject);
                }
                else
                {
                    DestroyImmediate(truck.gameObject);
                }

                throw;
            }

            // #600: register the truck in the director-owned set so it is ticked
            // with awareness of any truck ahead of it (car-following), rather than
            // self-ticking blind to the others.
            activeTrucks.Add(truck);
        }

        /// <summary>
        /// Core computes the actual route (#106, #128, #677): shortest path over
        /// the LIVE map-spanning sidewalk/crosswalk/front-walkway network
        /// (<see cref="GameState.WalkNetwork"/>) from the dog's current position to
        /// its house's FRONT DOOR — the lot-side node of the lot's front walkway.
        /// <see cref="WalkHomeRoute"/> owns every decision, including refusing to
        /// substitute a destination; this layer only walks the waypoints, and
        /// contains the failure if there is no route. Computed once per quest and
        /// cached.
        /// </summary>
        private List<Vector3> GetOrComputeRoute(Quest quest, DogView view)
        {
            if (homeRoutes.TryGetValue(quest.Id, out var existing))
            {
                return existing;
            }

            var start = new GridPoint(view.transform.position.x, view.transform.position.z);
            var planned = WalkHomeRoute.Plan(State, view.Dog.HouseId, start);
            var route = planned.Waypoints.Select(p => new Vector3(p.X, 0f, p.Z)).ToList();

            homeRoutes[quest.Id] = route;
            homeRouteProgress[quest.Id] = 0;

            // #470: the moment quest movement takes over, drop any wander
            // target the DogView had cached so the wander branch never drives
            // this transform alongside the scripted walk, and so the resume
            // after delivery picks a fresh target.
            view.BeginQuestWalk();
            return route;
        }
    }
}
