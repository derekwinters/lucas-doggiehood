using System.Linq;
using Doggiehood.Core.Quests;
using Doggiehood.Core.World;
using Doggiehood.Unity;
using NUnit.Framework;
using UnityEngine;

namespace Doggiehood.Unity.EditModeTests
{
    public class QuestDirectorTests
    {
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
            // #30/#106: WalkDogHome used to be a straight-line
            // Vector3.MoveTowards ignoring streets entirely. Now Core
            // computes the route over the sidewalk/crosswalk/driveway-stub
            // network and this layer only walks the waypoints. Place the
            // dog far from home (as if it had been out wandering) so the
            // real route has to detour via the network instead of cutting
            // the direct diagonal — a straight-line bug would show zero
            // deviation from that diagonal.
            state.Wallet.Deposit(1000);
            var dog = state.Dogs.First(d => d.HouseId == 3); // SouthEast house, (14, -14)
            var quest = state.Quests.GiveQuestTo(dog, QuestType.BuyGift, new System.Random(3));
            Assert.That(state.Quests.Accept(quest), Is.True);
            Assert.That(quest.DeliveryPhase, Is.EqualTo(DeliveryPhase.HeadingHome));

            var view = worldRoot.GetComponentsInChildren<DogView>().Single(v => v.Dog.Name == dog.Name);
            var half = WorldDimensions.RoadWidth / 2f + WorldDimensions.GrassVergeWidth + WorldDimensions.SidewalkWidth / 2f;
            var farAway = new Vector3(-half, 0f, NeighborhoodLayout.StreetHalfLength); // far NW sidewalk tip
            view.transform.position = farAway;

            var lot = NeighborhoodLayout.GetHouseLot(dog.HouseId);
            var home = new Vector3(lot.Position.X, 0f, lot.Position.Z);

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
