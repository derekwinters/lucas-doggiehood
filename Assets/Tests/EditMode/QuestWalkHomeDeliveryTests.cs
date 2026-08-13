using System.Linq;
using Doggiehood.Core.Decorations;
using Doggiehood.Core.Dogs;
using Doggiehood.Core.Economy;
using Doggiehood.Core.Quests;
using Doggiehood.Core.World;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Doggiehood.Unity.EditModeTests
{
    /// <summary>
    /// #677: the buy-gift delivery flow for a dog that is NOT on the starting
    /// tile — the case a playtest broke. The walk home was planned against the
    /// origin-tile-only <c>NeighborhoodLayout</c> singleton while the dog was
    /// wandering an unlocked tile (#398), so the route both started with a beeline
    /// across the yards and ended on a sidewalk nowhere near the dog's house,
    /// where the dog sat down in the waiting pose; the truck was then dispatched to
    /// that wrong point, threw, and — with no try/catch in <c>Update</c> — took
    /// every later delivery, every driving truck, and the rest approaches down with
    /// it for the rest of the session.
    /// </summary>
    public class QuestWalkHomeDeliveryTests
    {
        /// <summary>Simulation step (s) — the same 20fps step the other director
        /// tests drive.</summary>
        private const float Step = 0.05f;

        /// <summary>Safety cap on simulated frames, so a routing regression fails
        /// the assertion instead of hanging the suite.</summary>
        private const int MaxSteps = 20000;

        /// <summary>How close (m) counts as standing on a point.</summary>
        private const float Tolerance = 0.05f;

        /// <summary>Frames to watch something keep moving over.</summary>
        private const int ObservationSteps = 20;

        /// <summary>Rest ticks to give the 5%-per-tick comfort roll — a dog with a
        /// comfort decoration is then all but certain to start an approach.</summary>
        private const int RestTicks = 500;

        private GameObject worldRoot;
        private GameState state;
        private QuestDirector director;

        [SetUp]
        public void BuildUnlockedWorldWithDogsAndDirector()
        {
            TapRouter.IsModalOpen = TapRouter.DefaultIsModalOpen;
            ModalInputGate.Shared.Clear();
            RoadCrossingGate.Shared.Clear();

            // A world with the first frontier tile unlocked, so there is real map
            // off the origin tile for a dog to be standing on.
            state = FrontierEditModeWorld.WithFirstTileUnlocked(5000);
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
            // The contained failures below log through Debug.LogException on
            // purpose; don't leak the suppression into the next test.
            LogAssert.ignoreFailingMessages = false;

            foreach (var presenter in Object.FindObjectsByType<ConversationPresenter>(FindObjectsSortMode.None))
            {
                Object.DestroyImmediate(presenter.gameObject);
            }

            Object.DestroyImmediate(worldRoot);
        }

        [Test]
        public void ADogOffTheOriginTile_WalksHomeOnThePavement_AndOnlySitsAtItsOwnDoor()
        {
            var dog = state.Dogs.First(d => d.HouseId == 3);
            var quest = AcceptBuyGift(dog);
            var view = ViewFor(dog);

            var network = state.WalkNetwork;
            Assert.That(network.TryGetFrontWalkway(dog.HouseId, out var walkway), Is.True);
            var door = new Vector2(walkway.A.X, walkway.A.Z);

            // Standing at the top of the newly unlocked cul-de-sac's bulb — a real
            // place a wandering dog reaches (#398), and 60m+ from any node of the
            // origin-only network the route used to be planned on.
            var start = network.Nodes.OrderByDescending(n => n.Z).First();
            view.transform.position = new Vector3(start.X, view.transform.position.y, start.Z);

            var previous = Xz(view.transform.position);
            for (var step = 0; step < MaxSteps && quest.DeliveryPhase == DeliveryPhase.HeadingHome; step++)
            {
                director.Tick(Step);
                var now = Xz(view.transform.position);

                Assert.That(
                    network.SegmentStaysOnPavement(
                        new GridPoint(previous.x, previous.y), new GridPoint(now.x, now.y)),
                    Is.True,
                    $"the dog left the walk network walking from ({previous.x}, {previous.y}) to "
                    + $"({now.x}, {now.y}) — a walk home never crosses a yard, open ground, or the roadway "
                    + "off a crosswalk");

                if (dog.State == DogState.Sit)
                {
                    Assert.That(Vector2.Distance(now, door), Is.LessThan(Tolerance),
                        "the dog entered the waiting pose somewhere that is not its own front door");
                }

                previous = now;
            }

            Assert.That(quest.DeliveryPhase, Is.EqualTo(DeliveryPhase.WaitingForDelivery),
                "the dog never reported home");
            Assert.That(Vector2.Distance(Xz(view.transform.position), door), Is.LessThan(Tolerance),
                "the dog must end its walk home at its own front door");
        }

        [Test]
        public void ADogLivingInAPlayerBuiltHouse_RoutesToItsOwnDoor_AndTheTruckCanReachIt()
        {
            // NeighborhoodLayout.GetHouseLot throws for any player-built house id
            // (>= 5), so this dog's walk home used to throw on its very first
            // frame. It now routes to its own door and the truck can drive there.
            // (The walk itself isn't ticked here: this stand-in resident is not on
            // the Core roster, so arrival's dog lookup has nothing to find — the
            // full ticked walk home is covered by the tests above.)
            var houseId = FrontierEditModeWorld.FirstLotId;
            Assert.That(state.TryBuildHouse(houseId), Is.True, "precondition: the frontier lot builds");

            var resident = state.Dogs.First(d => d.HouseId == 3);
            var newHouseDog = new Dog(
                resident.Name + "-frontier", resident.Breed, resident.Personality, houseId, false);
            DogSpawner.SpawnDog(state, worldRoot.transform, newHouseDog, 0);
            var view = ViewFor(newHouseDog);

            Assert.That(state.WalkNetwork.TryGetFrontWalkway(houseId, out var walkway), Is.True);
            var door = new Vector2(walkway.A.X, walkway.A.Z);

            var route = WalkHomeRoute.Plan(
                state, houseId, new GridPoint(view.transform.position.x, view.transform.position.z));

            Assert.That(
                Vector2.Distance(
                    new Vector2(route.FrontDoor.X, route.FrontDoor.Z), door), Is.LessThan(Tolerance),
                "a player-built house's resident routes to its OWN front door");

            // And the truck can be dispatched to that door over the live map.
            var truck = DeliveryTruckView.Spawn(worldRoot.transform);
            Assert.That(
                () => truck.DeliverTo(
                    new Vector3(route.FrontDoor.X, 0f, route.FrontDoor.Z), state.Map, state.WalkNetwork, null),
                Throws.Nothing,
                "the delivery truck must reach a player-built house's door on the live map");
        }

        [Test]
        public void AFailedDelivery_NeverStrandsItsDogInTheWaitingPose()
        {
            var dog = state.Dogs.First(d => d.HouseId == 3);
            var quest = AcceptBuyGift(dog);
            var view = ViewFor(dog);
            PlaceAtItsWalkwayAttach(dog, view);

            // Stand in for a truck that cannot be routed to the door.
            director.DeliveryDispatcher = (door, delivered) =>
                throw new System.InvalidOperationException("no road route to the door");
            LogAssert.ignoreFailingMessages = true;

            RunUntilResolved(quest);

            Assert.That(quest.DeliveryPhase, Is.EqualTo(DeliveryPhase.Delivered),
                "a delivery that cannot be driven must still resolve — the player already paid");
            Assert.That(dog.State, Is.Not.EqualTo(DogState.Sit),
                "the dog must not be left sitting in the waiting pose for a truck that never comes");
            Assert.That(dog.WantsToWander, Is.True, "the dog is handed back to wander");
            Assert.That(state.WalkNetwork.TryGetFrontWalkway(dog.HouseId, out var walkway), Is.True);
            Assert.That(
                Vector2.Distance(Xz(view.transform.position), new Vector2(walkway.A.X, walkway.A.Z)),
                Is.LessThan(Tolerance),
                "it recovers standing at its own front door — where it legitimately walked to");
            Assert.That(Object.FindObjectsByType<DeliveryTruckView>(FindObjectsSortMode.None), Is.Empty,
                "the director must not leave a truck in the world for a delivery it could not dispatch");
        }

        [Test]
        public void AFailedDelivery_IsContainedToItsOwnQuest_OtherDogsTrucksAndRestApproachesKeepTicking()
        {
            // The reported cascade: "I did 2 delivery quests and then the next 3
            // deliveries were hit by this bug" — one throw out of Update stopped
            // every walk, every truck, and every rest approach, every frame after.
            var failingDog = state.Dogs.First(d => d.HouseId == 3);
            var walkingDog = state.Dogs.First(d => d.HouseId == 1);
            var failingQuest = AcceptBuyGift(failingDog);
            var walkingQuest = AcceptBuyGift(walkingDog);

            var failingView = ViewFor(failingDog);
            PlaceAtItsWalkwayAttach(failingDog, failingView);

            var walkingView = ViewFor(walkingDog);
            var farAway = state.WalkNetwork.Nodes.OrderByDescending(n => n.Z).First();
            walkingView.transform.position =
                new Vector3(farAway.X, walkingView.transform.position.y, farAway.Z);

            director.DeliveryDispatcher = (door, delivered) =>
                throw new System.InvalidOperationException("no road route to the door");
            LogAssert.ignoreFailingMessages = true;

            RunUntilResolved(failingQuest);
            Assert.That(failingQuest.DeliveryPhase, Is.EqualTo(DeliveryPhase.Delivered));
            Assert.That(walkingQuest.DeliveryPhase, Is.EqualTo(DeliveryPhase.HeadingHome),
                "test setup: the second dog is still on its way home when the first delivery fails");

            // 1. The other dog keeps walking after the failure.
            var before = Xz(walkingView.transform.position);
            director.DeliveryDispatcher = null; // the real truck again
            for (var step = 0; step < ObservationSteps; step++)
            {
                director.Tick(Step);
            }

            Assert.That(Vector2.Distance(Xz(walkingView.transform.position), before), Is.GreaterThan(0f),
                "a second heading-home dog must keep walking after another quest's delivery failed");

            // 2. Trucks still tick: the second dog gets home and its truck drives.
            for (var step = 0; step < MaxSteps && walkingQuest.DeliveryPhase == DeliveryPhase.HeadingHome; step++)
            {
                director.Tick(Step);
            }

            Assert.That(walkingQuest.DeliveryPhase, Is.Not.EqualTo(DeliveryPhase.HeadingHome),
                "the second dog must still reach home after the first delivery failed");
            var truck = Object.FindObjectsByType<DeliveryTruckView>(FindObjectsSortMode.None).FirstOrDefault();
            Assert.That(truck, Is.Not.Null, "the second delivery still spawns a truck");

            var truckBefore = truck.transform.position;
            for (var step = 0; step < ObservationSteps; step++)
            {
                director.Tick(Step);
            }

            Assert.That(Vector3.Distance(truck.transform.position, truckBefore), Is.GreaterThan(0f),
                "trucks must keep driving after another quest's delivery failed");

            // 3. Rest approaches still tick.
            var restDog = state.Dogs.First(d => d.HouseId == 2 && !d.HasActiveQuest);
            state.AddDecoration(new Decoration(
                ComfortDecorations.ItemNames.First(), restDog.HouseId,
                YardPlacement.PositionFor(restDog.HouseId, 0)));

            var began = false;
            for (var tick = 0; tick < RestTicks && !began; tick++)
            {
                director.TickRestApproaches();
                began = Object.FindObjectsByType<DogView>(FindObjectsSortMode.None).Any(v => v.IsApproachingRest);
            }

            Assert.That(began, Is.True,
                "comfort-item rest approaches must keep being offered after a delivery failed");
        }

        private Quest AcceptBuyGift(Dog dog)
        {
            state.Wallet.Deposit(1000);
            var quest = state.Quests.GiveQuestTo(dog, QuestType.BuyGift, new System.Random(3));
            Assert.That(quest.ItemName, Is.Not.EqualTo(ItemCatalog.FenceItemName),
                "the fence purchase has no delivery leg — this fixture needs a delivered gift");
            Assert.That(state.Quests.Accept(quest), Is.True);
            Assert.That(quest.DeliveryPhase, Is.EqualTo(DeliveryPhase.HeadingHome));
            return quest;
        }

        /// <summary>Puts the dog on its own front walkway's sidewalk attach point,
        /// a couple of metres from its door, so it arrives home quickly.</summary>
        private void PlaceAtItsWalkwayAttach(Dog dog, DogView view)
        {
            Assert.That(state.WalkNetwork.TryGetFrontWalkway(dog.HouseId, out var walkway), Is.True);
            view.transform.position = new Vector3(walkway.B.X, view.transform.position.y, walkway.B.Z);
        }

        private void RunUntilResolved(Quest quest)
        {
            for (var step = 0; step < MaxSteps && quest.DeliveryPhase != DeliveryPhase.Delivered; step++)
            {
                director.Tick(Step);
            }
        }

        private DogView ViewFor(Dog dog)
        {
            return Object.FindObjectsByType<DogView>(FindObjectsSortMode.None)
                .Single(v => v.Dog.Name == dog.Name);
        }

        private static Vector2 Xz(Vector3 position)
        {
            return new Vector2(position.x, position.z);
        }
    }
}
