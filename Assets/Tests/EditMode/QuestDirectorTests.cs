using System.Linq;
using Doggiehood.Core.Cameras;
using Doggiehood.Core.Decorations;
using Doggiehood.Core.Dogs;
using Doggiehood.Core.Economy;
using Doggiehood.Core.Quests;
using Doggiehood.Core.World;
using Doggiehood.Unity;
using NUnit.Framework;
using UnityEngine;

namespace Doggiehood.Unity.EditModeTests
{
    public class QuestDirectorTests
    {
        /// <summary>An axis-aligned bounding box has 8 corners; used by
        /// ProjectedScreenBounds (#311).</summary>
        private const int BoundsCornerCount = 8;

        private GameObject worldRoot;
        private GameState state;
        private QuestDirector director;

        [SetUp]
        public void BuildWorldWithDogsAndDirector()
        {
            state = GameState.CreateNew();
            worldRoot = WorldBuilder.Build(state);
            DogSpawner.SpawnDogs(state, worldRoot.transform);

            var host = new GameObject("quest-director-host");
            host.transform.SetParent(worldRoot.transform);
            director = host.AddComponent<QuestDirector>();
            director.Init(state, worldRoot.transform);
        }

        [TearDown]
        public void Cleanup()
        {
            foreach (var presenter in Object.FindObjectsByType<ConversationPresenter>(FindObjectsSortMode.None))
            {
                Object.DestroyImmediate(presenter.gameObject);
            }

            Object.DestroyImmediate(worldRoot);
        }

        [Test]
        public void WalkingHomeQuest_RoutesOverTheNetwork_RatherThanBeeliningHome()
        {
            // #30/#106/#128: WalkDogHome used to be a straight-line
            // Vector3.MoveTowards ignoring streets entirely. Now Core
            // computes the route over the sidewalk/crosswalk/front-walkway
            // network and this layer only walks the waypoints — ending at
            // the house's actual FRONT DOOR (the walkway's lot-side node),
            // not the old lot-center stub anchor. Place the dog far from
            // home (as if it had been out wandering) so the real route has
            // to detour via the network instead of cutting the direct
            // diagonal — a straight-line bug would show zero deviation
            // from that diagonal.
            state.Wallet.Deposit(1000);
            var dog = state.Dogs.First(d => d.HouseId == 3); // SouthEast house, (14, -14)
            var quest = state.Quests.GiveQuestTo(dog, QuestType.BuyGift, new System.Random(3));
            Assert.That(state.Quests.Accept(quest), Is.True);
            Assert.That(quest.DeliveryPhase, Is.EqualTo(DeliveryPhase.HeadingHome));

            var view = worldRoot.GetComponentsInChildren<DogView>().Single(v => v.Dog.Name == dog.Name);
            var half = WorldDimensions.RoadWidth / 2f + WorldDimensions.GrassVergeWidth + WorldDimensions.SidewalkWidth / 2f;
            var farAway = new Vector3(-half, 0f, NeighborhoodLayout.StreetHalfLength); // far NW sidewalk tip
            view.transform.position = farAway;

            Assert.That(NeighborhoodLayout.WalkNetwork.TryGetFrontWalkway(dog.HouseId, out var walkway),
                Is.True, "the dog's house must have a front walkway");
            var home = new Vector3(walkway.A.X, 0f, walkway.A.Z); // the front door

            var maxDeviation = 0f;
            var reachedWaitingPhase = false;
            for (var step = 0; step < 5000 && !reachedWaitingPhase; step++)
            {
                director.Tick(0.05f);
                maxDeviation = Mathf.Max(maxDeviation, PerpendicularDistanceFromLine(view.transform.position, farAway, home));

                if (quest.DeliveryPhase == DeliveryPhase.WaitingForDelivery)
                {
                    reachedWaitingPhase = true;
                }
            }

            Assert.That(reachedWaitingPhase, Is.True, "dog never made it home");
            Assert.That(maxDeviation, Is.GreaterThan(2f),
                "route never deviated from the direct line home — looks like the old straight-line bug");

            Assert.That(view.transform.position.x, Is.EqualTo(home.x).Within(0.01f));
            Assert.That(view.transform.position.z, Is.EqualTo(home.z).Within(0.01f));
        }

        [Test]
        public void WalkingHome_ClearsAnyStaleWanderTarget_WhenTheQuestWalkTakesOver()
        {
            // #470: DogView.Update's wander branch and QuestDirector.WalkDogHome
            // both wrote transform.position. When the scripted walk takes over
            // it must drop any cached wander target so, once control hands back
            // after delivery, the dog can't beeline to that stale point.
            state.Wallet.Deposit(1000);
            var dog = state.Dogs.First(d => d.HouseId == 3);
            var view = worldRoot.GetComponentsInChildren<DogView>().Single(v => v.Dog.Name == dog.Name);

            // Establish a wander target while the dog is still wandering.
            view.TickWander(0.01f);
            Assert.That(view.HasWanderTarget, Is.True, "precondition: the dog has cached a wander target");

            var quest = state.Quests.GiveQuestTo(dog, QuestType.BuyGift, new System.Random(3));
            Assert.That(state.Quests.Accept(quest), Is.True);
            Assert.That(quest.DeliveryPhase, Is.EqualTo(DeliveryPhase.HeadingHome));

            // First director tick: the quest walk takes over movement.
            director.Tick(0.05f);

            Assert.That(view.HasWanderTarget, Is.False,
                "the scripted walk home must clear the stale wander target on takeover");
        }

        [Test]
        public void WalkingHome_FacesTheDirectionOfTravel_TurnBeforeMove()
        {
            // #470 (moonwalk): WalkDogHome never wrote transform.rotation, so
            // facing was left to the stale wander branch — the dog walked home
            // backwards. Now the scripted walk turns toward each waypoint it
            // moves toward.
            state.Wallet.Deposit(1000);
            var dog = state.Dogs.First(d => d.HouseId == 3);
            var quest = state.Quests.GiveQuestTo(dog, QuestType.BuyGift, new System.Random(3));
            Assert.That(state.Quests.Accept(quest), Is.True);

            var view = worldRoot.GetComponentsInChildren<DogView>().Single(v => v.Dog.Name == dog.Name);
            var half = WorldDimensions.RoadWidth / 2f + WorldDimensions.GrassVergeWidth + WorldDimensions.SidewalkWidth / 2f;
            view.transform.position = new Vector3(-half, 0f, NeighborhoodLayout.StreetHalfLength); // far NW

            // Take a few steps so the dog is mid-route (not snapping onto the
            // final waypoint), then measure facing against actual travel.
            director.Tick(0.05f);
            var before = view.transform.position;
            director.Tick(0.05f);
            var after = view.transform.position;

            var travel = new Vector3(after.x - before.x, 0f, after.z - before.z);
            Assert.That(travel.sqrMagnitude, Is.GreaterThan(0.0001f), "the dog must actually move this frame");

            var forward = new Vector3(view.transform.forward.x, 0f, view.transform.forward.z);
            Assert.That(Vector3.Angle(forward, travel), Is.LessThan(5f),
                "the dog must face the way it is walking (no backwards moonwalk home)");
        }

        [Test]
        public void AfterDelivery_ResumesWanderWithAFreshOnNetworkTarget_NotTheStalePreQuestOne()
        {
            // #470 (beeline): once delivered, DogView.PlaceOnStreet re-armed
            // wander with the leftover cached target and MoveTowards beelined
            // off-network to it. Now delivery hands back with a fresh target
            // picked from the dog's real (home) position, on the walk network.
            state.Wallet.Deposit(1000);
            var dog = state.Dogs.First(d => d.HouseId == 3);
            var view = worldRoot.GetComponentsInChildren<DogView>().Single(v => v.Dog.Name == dog.Name);

            // Cache a stale wander target far from home while wandering.
            var half = WorldDimensions.RoadWidth / 2f + WorldDimensions.GrassVergeWidth + WorldDimensions.SidewalkWidth / 2f;
            view.transform.position = new Vector3(-half, 0f, NeighborhoodLayout.StreetHalfLength);
            view.TickWander(0.01f);
            Assert.That(view.HasWanderTarget, Is.True);
            var staleTarget = view.WanderTarget;

            var quest = state.Quests.GiveQuestTo(dog, QuestType.BuyGift, new System.Random(3));
            Assert.That(state.Quests.Accept(quest), Is.True);

            // Walk the dog home over the network until it's waiting for the truck.
            for (var step = 0; step < 5000 && quest.DeliveryPhase != DeliveryPhase.WaitingForDelivery; step++)
            {
                director.Tick(0.05f);
            }
            Assert.That(quest.DeliveryPhase, Is.EqualTo(DeliveryPhase.WaitingForDelivery), "dog never made it home");

            // The truck's arrival is device/PlayMode; drive the Core hand-back.
            state.Quests.DeliverPackage(quest);
            Assert.That(dog.WantsToWander, Is.True);

            // The dog is now standing at its home node (front door). Capture
            // that position BEFORE resuming so we can prove the fresh target is
            // a real one-hop wander step from where it actually stands.
            var homePosition = view.transform.position;

            // Resume the wander in the view — it must recompute, not resume stale.
            view.TickWander(0.0001f);
            Assert.That(view.HasWanderTarget, Is.True);
            var freshTarget = view.WanderTarget;

            Assert.That(freshTarget, Is.Not.EqualTo(staleTarget),
                "the resumed wander must pick a fresh target, not the stale pre-quest one");

            // The real "resumes on-network wander, not a stale beeline" property:
            // the fresh target must be a SINGLE walk-network edge-hop from the
            // node the dog currently stands on — exactly what a legitimate
            // wander step is, and precisely what the #470 bug violated (it
            // resumed the cached target from clear across the map, many hops
            // away and reached by cutting straight off the network).
            //
            // NB: a plain distance-to-NearestWalkableNode check is the WRONG
            // invariant here — after delivery the dog stands at its own front
            // door, and per #430 a resident dog may step onto its own front-door
            // node via its FrontWalkway edge. Front-door nodes are deliberately
            // excluded from NearestWalkableNode (WalkNetwork), so that legitimate
            // on-network pick sits one walkway-length (~4.75u) from the nearest
            // *walkable* node even though it is genuinely connected by a real
            // edge. The edge-adjacency check below is offset-agnostic and still
            // fails a cross-map beeline.
            var network = state.WalkNetwork;
            var homeNode = network.NearestWalkableNode(new GridPoint(homePosition.x, homePosition.z));
            var oneHopTargets = network.EdgesFrom(homeNode).Select(e => e.Other(homeNode)).ToList();
            var freshIsOneHop = oneHopTargets.Any(p =>
                Vector2.Distance(new Vector2(p.X, p.Z), new Vector2(freshTarget.x, freshTarget.z)) < 0.01f);
            Assert.That(freshIsOneHop, Is.True,
                "the resumed wander target must be one walk-network edge-hop from the dog's current node, "
                + "not a stale cross-map point reached by beelining off the network");

            // And the stale pre-quest target is emphatically NOT one such hop —
            // guarding that the assertion above actually discriminates the bug.
            var staleIsOneHop = oneHopTargets.Any(p =>
                Vector2.Distance(new Vector2(p.X, p.Z), new Vector2(staleTarget.x, staleTarget.z)) < 0.01f);
            Assert.That(staleIsOneHop, Is.False,
                "test sanity: the stale far-away target must not itself be adjacent to home, "
                + "or the one-hop assertion wouldn't catch a beeline-to-stale regression");
        }

        [Test]
        public void AcceptingAFenceQuest_ShowsTheFence_WithNoDeliveryTruckAndNoWalkHome()
        {
            // #318: the fence purchase has no delivery flow. Accepting it
            // completes the quest immediately (Core records the placed fence),
            // no DeliveryTruckView spawns, the dog never walks home (its
            // DeliveryPhase stays None, so Tick ignores it), and the fence
            // appears on the accept-time refresh with no animation.
            state.Wallet.Deposit(1000);
            var dog = state.Dogs.First(d => d.HouseId == 3);
            var view = worldRoot.GetComponentsInChildren<DogView>().Single(v => v.Dog.Name == dog.Name);
            var positionBefore = view.transform.position;

            // A fence BuyGift quest, built directly so the subject is
            // deterministic (the population-gated pool is covered in Core).
            var quest = new Quest(9001, QuestType.BuyGift, dog.Name, ItemCatalog.FenceItemName,
                new[] { "Could you fence my yard?" }, null, ItemCatalog.FenceCost, null);

            Assert.That(state.Quests.Accept(quest), Is.True);
            Assert.That(quest.Status, Is.EqualTo(QuestStatus.Completed),
                "the fence completes immediately on accept");
            Assert.That(quest.DeliveryPhase, Is.EqualTo(DeliveryPhase.None),
                "the fence never enters a delivery phase");

            director.OnQuestAccepted(quest);

            // No delivery truck anywhere in the scene.
            Assert.That(Object.FindObjectsByType<DeliveryTruckView>(FindObjectsSortMode.None),
                Is.Empty, "the fence purchase must not spawn a delivery truck");

            // The dog stays put — advancing the director walks nothing home.
            director.Tick(1f);
            Assert.That(view.transform.position, Is.EqualTo(positionBefore),
                "the dog must not walk home for a fence purchase");

            // The fence container for that house now exists in the world.
            var fenceContainerName = WorldBuilder.FenceNamePrefix + dog.HouseId;
            var fenceContainer = worldRoot.transform.Cast<Transform>()
                .FirstOrDefault(t => t.name == fenceContainerName);
            Assert.That(fenceContainer, Is.Not.Null,
                "the purchased fence must render on the accept-time refresh");
        }

        [Test]
        public void AcceptingABugQuest_ShowsABugSwarmOnThatHouse_AndSprayingClearsIt()
        {
            // #53/#157: the bug-problem flow was invisible — nothing marked
            // which house needed spraying. Accepting a pest-control quest now
            // spawns a bug swarm on exactly that house; tapping the house
            // (the spray) completes the quest and removes the swarm.
            var dog = state.Dogs.First(d => d.HouseId == 3);
            var quest = state.Quests.GiveQuestTo(dog, QuestType.PestControl, new System.Random(5));
            Assert.That(state.Quests.Accept(quest), Is.True);

            director.OnQuestAccepted(quest);

            var swarms = Object.FindObjectsByType<BugSwarmView>(FindObjectsSortMode.None);
            Assert.That(swarms.Length, Is.EqualTo(1), "exactly the bugged house shows a swarm");
            Assert.That(swarms[0].HouseId, Is.EqualTo(dog.HouseId));

            var houseView = Object.FindObjectsByType<HouseView>(FindObjectsSortMode.None)
                .Single(h => h.HouseId == dog.HouseId);
            var swarmXz = new Vector2(swarms[0].transform.position.x, swarms[0].transform.position.z);
            var houseXz = new Vector2(houseView.transform.position.x, houseView.transform.position.z);
            Assert.That(Vector2.Distance(swarmXz, houseXz), Is.LessThan(2f),
                "the swarm hovers over the affected house");

            // Spray via the real tap wiring the director hooked up in Init.
            var before = state.Wallet.Coins;
            houseView.OnTapped();

            Assert.That(quest.Status, Is.EqualTo(QuestStatus.Completed),
                "spraying the bugged house completes the quest");
            Assert.That(state.Wallet.Coins, Is.EqualTo(before + Doggiehood.Core.Economy.EconomyNumbers.QuestPayout),
                "completion pays the flat quest payout");
            Assert.That(Object.FindObjectsByType<BugSwarmView>(FindObjectsSortMode.None), Is.Empty,
                "the swarm is cleared once the house is sprayed");
        }

        [Test]
        public void BugSwarmIndicator_IsReadable_RisesClearOfTheRoof_IsTall_Bright_AndFeedbackOnly()
        {
            // #331: the indicator machinery worked but read as nothing on
            // device — three tiny (0.35m) near-black cubes hovering a blind 3m
            // over the roof under the 45-deg ortho camera. This locks in a
            // legibility contract so it can't regress to "tiny dark cubes":
            // the marker must rise clear of the affected house's ACTUAL roof
            // (so a roof can't occlude it), have obvious vertical extent, be
            // brightly colored, and stay feedback-only (no collider, so the
            // house underneath remains the tap target). On-screen readability
            // itself is confirmed by Derek on device — headless CI can't
            // assert visual legibility.
            var dog = state.Dogs.First(d => d.HouseId == 3);
            var quest = state.Quests.GiveQuestTo(dog, QuestType.PestControl, new System.Random(5));
            Assert.That(state.Quests.Accept(quest), Is.True);

            director.OnQuestAccepted(quest);

            var swarm = Object.FindObjectsByType<BugSwarmView>(FindObjectsSortMode.None).Single();
            var houseView = Object.FindObjectsByType<HouseView>(FindObjectsSortMode.None)
                .Single(h => h.HouseId == dog.HouseId);

            var roofTop = CombinedRendererBounds(houseView.GetComponentsInChildren<Renderer>()).max.y;
            var indicator = CombinedRendererBounds(swarm.GetComponentsInChildren<Renderer>());

            Assert.That(indicator.max.y, Is.GreaterThan(roofTop + 1.5f),
                "the indicator must rise well clear of the roof so the roof can't hide it");
            Assert.That(indicator.size.y, Is.GreaterThan(1.5f),
                "the indicator needs obvious vertical extent, not three tiny cubes at one height");

            var brightest = swarm.GetComponentsInChildren<Renderer>()
                .Max(r => r.sharedMaterial.color.maxColorComponent);
            Assert.That(brightest, Is.GreaterThan(0.6f),
                "the indicator must be brightly colored, not the old near-black swarm");

            Assert.That(swarm.GetComponentsInChildren<Collider>(), Is.Empty,
                "the indicator is feedback-only — no collider may intercept the tap meant for the house");
        }

        [Test]
        public void AcceptingALostItemQuest_SpawnsATappableItem_ThatCompletesOnRaycastTap()
        {
            // #12/#31/#157: end-to-end lost-item path — accepting spawns a
            // visible, physically hittable item at the Core-chosen position;
            // a camera raycast tap dead-center on it routes to Core and
            // completes the quest (guards the collider wiring the way #148
            // did for dogs).
            //
            // #311: this test deliberately isolates the item and uses an
            // artificial close-up camera so it *only* exercises "does a
            // pixel-exact tap on the collider still work" — it must NOT be
            // read as coverage for real taps in the real scene (ground
            // Plane present, 45-degree rig, imprecise finger touch); that
            // was exactly the false confidence #311 diagnosed. See
            // AcceptingALostItemQuest_TapNearButNotOnTheBall_CompletesTheQuest_InTheRealScene
            // below for that coverage.
            var dog = state.Dogs[0];
            var quest = state.Quests.GiveQuestTo(dog, QuestType.LostItem, new System.Random(9));
            Assert.That(state.Quests.Accept(quest), Is.True);

            director.OnQuestAccepted(quest);

            var view = Object.FindObjectsByType<LostItemView>(FindObjectsSortMode.None).Single();

            // Isolate the item so no ground/house/fence intercepts the ray —
            // intentional here, unlike the real-scene test below.
            view.transform.position = new Vector3(500f, 0.3f, 500f);

            var camGo = new GameObject("tap-cam", typeof(Camera));
            var cam = camGo.GetComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 3f;
            var texture = new RenderTexture(1920, 1080, 0);
            cam.targetTexture = texture;
            try
            {
                var target = view.transform.position;
                cam.transform.position = target + new Vector3(0f, 6f, -6f);
                cam.transform.LookAt(target);
                Physics.SyncTransforms();

                var before = state.Wallet.Coins;
                var routed = TapRouter.RouteTap(cam, cam.WorldToScreenPoint(target));

                Assert.That(routed, Is.True,
                    "a raycast tap on the lost item must hit its collider and route to the view");
                Assert.That(quest.Status, Is.EqualTo(QuestStatus.Completed),
                    "tapping the found item completes the quest");
                Assert.That(state.Wallet.Coins, Is.EqualTo(before + Doggiehood.Core.Economy.EconomyNumbers.QuestPayout),
                    "completion pays the flat quest payout");
                Assert.That(Object.FindObjectsByType<LostItemView>(FindObjectsSortMode.None), Is.Empty,
                    "the found item disappears once collected");
            }
            finally
            {
                cam.targetTexture = null;
                Object.DestroyImmediate(texture);
                Object.DestroyImmediate(camGo);
            }
        }

        [Test]
        public void AcceptingALostItemQuest_TapNearButNotOnTheBall_CompletesTheQuest_InTheRealScene()
        {
            // #311: the item is left exactly where Core placed it — inside
            // the full-map ground Plane (WorldBuilder.BuildGround,
            // GroundExtent 30, comfortably covering the item's spawn region,
            // the quest dog's own home-tile quadrant bounds, #520) and clear
            // of that lot's house footprint (#290) — and viewed through the real rig's
            // 45-degree pitch/DefaultZoom, unlike the isolated dead-center
            // test above. Under that real view the ball's SphereCollider
            // (radius 0.3) projects to only ~18px, so a tap that lands near
            // — not exactly on — its rendered silhouette must still
            // complete the quest via the #311 padded screen-space fallback
            // (LostItemTapZone), not just a pixel-exact Physics.Raycast hit.
            var dog = state.Dogs[0];
            var quest = state.Quests.GiveQuestTo(dog, QuestType.LostItem, new System.Random(9));
            Assert.That(state.Quests.Accept(quest), Is.True);

            director.OnQuestAccepted(quest);

            var view = Object.FindObjectsByType<LostItemView>(FindObjectsSortMode.None).Single();
            var target = view.transform.position;

            var camGo = new GameObject("tap-cam", typeof(Camera));
            var cam = camGo.GetComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = CameraController.DefaultZoom;
            cam.transform.rotation = Quaternion.Euler(
                CameraRigConfig.PitchDegrees, CameraController.DefaultYaw, 0f);
            var texture = new RenderTexture(1920, 1080, 0);
            cam.targetTexture = texture;
            try
            {
                cam.transform.position = target - cam.transform.forward * CameraRigConfig.RigDistance;
                Physics.SyncTransforms();

                var screenBounds = ProjectedScreenBounds(cam, view.transform);

                // Just beyond the ball's own rendered/collider bounds
                // (halfway into the #311 padding margin), directly above
                // its screen-space top edge — exactly how a real, slightly
                // imprecise touch tap would land.
                var probeScreen = new Vector3(
                    (screenBounds.minX + screenBounds.maxX) / 2f,
                    screenBounds.maxY + LostItemTapZone.PaddingPixels / 2f,
                    0f);

                var rawRaycastHitTheItem =
                    Physics.Raycast(cam.ScreenPointToRay(probeScreen), out var rawHit, Mathf.Infinity)
                    && rawHit.collider.GetComponentInParent<LostItemView>() == view;
                Assert.That(rawRaycastHitTheItem, Is.False,
                    "test setup sanity: the probe point must sit outside the ball's raw collider, " +
                    "so this test exercises the #311 padding rather than redundantly re-testing dead center");

                var before = state.Wallet.Coins;
                var routed = TapRouter.RouteTap(cam, probeScreen);

                Assert.That(routed, Is.True,
                    "a tap just beyond the ball's rendered bounds, but within the #311 padding margin, " +
                    "must still route to the lost item — in the real scene, ground Plane and all");
                Assert.That(quest.Status, Is.EqualTo(QuestStatus.Completed),
                    "tapping near (not just exactly on) the lost item completes the quest");
                Assert.That(state.Wallet.Coins, Is.EqualTo(before + Doggiehood.Core.Economy.EconomyNumbers.QuestPayout),
                    "completion pays the flat quest payout");
                Assert.That(Object.FindObjectsByType<LostItemView>(FindObjectsSortMode.None), Is.Empty,
                    "the found item disappears once collected");
            }
            finally
            {
                cam.targetTexture = null;
                Object.DestroyImmediate(texture);
                Object.DestroyImmediate(camGo);
            }
        }

        [Test]
        public void RestApproach_WalksTheDogToItsComfortDecoration_ThenRestsOnArrival_NoTeleport()
        {
            // #52/#112: rest mode used to be an instant state flip — the dog
            // teleported into the Rest pose. Now Core computes a walk-over
            // route to the comfort decoration and the DogView follows it frame
            // by frame, only settling into Rest once it arrives. This drives
            // the real view step (TickRestApproach) the Play-mode Update loop
            // would call, proving the dog moves across the world before
            // resting rather than snapping straight into the pose.
            var dog = state.Dogs.First(d => d.HouseId == 3); // SouthEast house, (14, -14)
            var yard = YardPlacement.PositionFor(dog.HouseId, 0);
            state.AddDecoration(new Decoration("bed", dog.HouseId, yard));
            var decoration = state.Decorations.Last();

            var view = worldRoot.GetComponentsInChildren<DogView>().Single(v => v.Dog.Name == dog.Name);

            // Put the dog out on the network, away from its own yard.
            var start = new Vector3(NeighborhoodLayout.Intersection.X, 0f, NeighborhoodLayout.Intersection.Z);
            view.transform.position = start;

            var approach = RestApproach.Begin(
                new GridPoint(start.x, start.z), decoration, NeighborhoodLayout.WalkNetwork);
            Assert.That(approach.Waypoints.Count, Is.GreaterThan(0), "a real route to the decoration must exist");

            view.BeginRestApproach(approach);
            Assert.That(view.IsApproachingRest, Is.True);

            // One step in: the dog has moved but is still walking, not resting —
            // the no-teleport guarantee.
            view.TickRestApproach(0.05f);
            Assert.That(view.Dog.State, Is.EqualTo(DogState.IdleWander), "must not rest before arriving");
            Assert.That(Vector3.Distance(view.transform.position, start), Is.GreaterThan(0f),
                "the dog moved off its start toward the decoration");
            var arrivedXz = new Vector2(yard.X, yard.Z);
            Assert.That(
                Vector2.Distance(new Vector2(view.transform.position.x, view.transform.position.z), arrivedXz),
                Is.GreaterThan(1f), "one step in, the dog is still en route, not already on the decoration");

            for (var step = 0; step < 5000 && view.Dog.State != DogState.Rest; step++)
            {
                view.TickRestApproach(0.05f);
            }

            Assert.That(view.Dog.State, Is.EqualTo(DogState.Rest), "the dog rests once it reaches the decoration");
            Assert.That(view.IsApproachingRest, Is.False, "the approach is cleared on arrival");
            Assert.That(view.transform.position.x, Is.EqualTo(yard.X).Within(0.01f));
            Assert.That(view.transform.position.z, Is.EqualTo(yard.Z).Within(0.01f));
        }

        private static (float minX, float minY, float maxX, float maxY) ProjectedScreenBounds(
            Camera camera, Transform root)
        {
            var renderers = root.GetComponentsInChildren<Renderer>();
            var bounds = renderers[0].bounds;
            for (var i = 1; i < renderers.Length; i++)
            {
                bounds.Encapsulate(renderers[i].bounds);
            }

            var minX = float.MaxValue;
            var maxX = float.MinValue;
            var minY = float.MaxValue;
            var maxY = float.MinValue;
            for (var i = 0; i < BoundsCornerCount; i++)
            {
                var corner = bounds.center + Vector3.Scale(bounds.extents, new Vector3(
                    (i & 1) == 0 ? -1f : 1f,
                    (i & 2) == 0 ? -1f : 1f,
                    (i & 4) == 0 ? -1f : 1f));
                var screen = camera.WorldToScreenPoint(corner);
                minX = Mathf.Min(minX, screen.x);
                maxX = Mathf.Max(maxX, screen.x);
                minY = Mathf.Min(minY, screen.y);
                maxY = Mathf.Max(maxY, screen.y);
            }

            return (minX, minY, maxX, maxY);
        }

        private static Bounds CombinedRendererBounds(Renderer[] renderers)
        {
            Assert.That(renderers, Is.Not.Empty, "expected at least one renderer to bound");
            var bounds = renderers[0].bounds;
            for (var i = 1; i < renderers.Length; i++)
            {
                bounds.Encapsulate(renderers[i].bounds);
            }

            return bounds;
        }

        private static float PerpendicularDistanceFromLine(Vector3 point, Vector3 lineStart, Vector3 lineEnd)
        {
            var ab = new Vector2(lineEnd.x - lineStart.x, lineEnd.z - lineStart.z);
            var ap = new Vector2(point.x - lineStart.x, point.z - lineStart.z);
            if (ab.sqrMagnitude < 0.0001f)
            {
                return ap.magnitude;
            }

            var cross = ab.x * ap.y - ab.y * ap.x;
            return Mathf.Abs(cross) / ab.magnitude;
        }
    }
}
