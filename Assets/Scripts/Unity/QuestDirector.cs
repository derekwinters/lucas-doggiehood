using System.Linq;
using Doggiehood.Core.Quests;
using Doggiehood.Core.World;
using UnityEngine;

namespace Doggiehood.Unity
{
    /// <summary>
    /// Scene-side glue for the quest loop (#23, #30, #31, #53): watches
    /// accepted quests and animates their consequences — walks buy-quest
    /// dogs home at medium speed, spawns the delivery truck, places lost
    /// items in the world, routes house taps to the spray action, and saves
    /// after every completion. Every decision stays in Core's QuestManager.
    /// </summary>
    public sealed class QuestDirector : MonoBehaviour
    {
        private const float WalkHomeSpeed = 1.6f;

        public GameState State { get; private set; }

        private Transform worldRoot;

        public void Init(GameState state, Transform worldRoot)
        {
            State = state;
            this.worldRoot = worldRoot;

            foreach (var house in Object.FindObjectsByType<HouseView>(FindObjectsSortMode.None))
            {
                var houseId = house.HouseId;
                house.Tapped += () => State.Quests.SprayHouse(houseId);
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
            foreach (var quest in State.Quests.ActiveQuests.ToList())
            {
                if (quest.DeliveryPhase == DeliveryPhase.HeadingHome)
                {
                    WalkDogHome(quest);
                }
            }
        }

        private void WalkDogHome(Quest quest)
        {
            var view = Object.FindObjectsByType<DogView>(FindObjectsSortMode.None)
                .FirstOrDefault(v => v.Dog.Name == quest.DogName);
            if (view == null)
            {
                return;
            }

            var lot = NeighborhoodLayout.GetHouseLot(view.Dog.HouseId);
            var home = new Vector3(lot.Position.X, 0f, lot.Position.Z);

            view.transform.position = Vector3.MoveTowards(
                view.transform.position, home, WalkHomeSpeed * Time.deltaTime);

            if (Vector3.Distance(view.transform.position, home) < 1f)
            {
                State.Quests.NotifyDogArrivedHome(quest);
                DeliveryTruckView.Spawn(worldRoot).DeliverTo(home, () =>
                {
                    State.Quests.DeliverPackage(quest);
                    SaveStore.Save(State);
                });
            }
        }
    }
}
