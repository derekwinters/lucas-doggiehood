using System;
using System.Collections.Generic;
using System.Linq;
using Doggiehood.Core.Dogs;
using Doggiehood.Core.Economy;
using Doggiehood.Core.World;

namespace Doggiehood.Core.Quests
{
    /// <summary>
    /// Owns quest instances and the daily rotation (#23, #26). The sole
    /// place coins are deposited — completion pays the flat payout (#24) —
    /// and the arbiter of each quest type's completion rule: tap the hidden
    /// item (#31), delivery after the dog sits waiting (#30), spray the
    /// right house (#53). Quests never expire (#28).
    /// </summary>
    public sealed class QuestManager
    {
        private const float LostItemTapRadius = 1.5f;
        private const float HiddenItemExtent = 25f;

        private static readonly string[] LostItems = { "toy", "ball", "puppy" };
        private static readonly string[] GiftItems = { "toy", "ball", "chew bone", "pool" };
        private static readonly string[] DecorationItems = { "bed", "cushion", "blanket" };
        private static readonly QuestType[] RotationTypes =
        {
            QuestType.LostItem,
            QuestType.BuyGift,
            QuestType.PestControl,
        };

        private readonly GameState state;
        private readonly List<Quest> quests = new List<Quest>();
        private int nextQuestId = 1;

        public QuestManager(GameState state)
        {
            this.state = state;
        }

        public IEnumerable<Quest> ActiveQuests
        {
            get { return quests.Where(q => q.Status != QuestStatus.Completed); }
        }

        /// <summary>Daily rotation (#26): 2-4 quest-free dogs get new quests;
        /// dogs holding an uncompleted quest are never overwritten.</summary>
        public void StartNewDay(Random rng)
        {
            var freeDogs = state.Dogs.Where(d => !d.HasActiveQuest).ToList();
            var count = Math.Min(freeDogs.Count, 2 + rng.Next(3));

            for (var i = 0; i < count; i++)
            {
                var dog = freeDogs[rng.Next(freeDogs.Count)];
                freeDogs.Remove(dog);
                GiveQuestTo(dog, RotationTypes[rng.Next(RotationTypes.Length)], rng);
            }
        }

        public Quest GiveQuestTo(Dog dog, QuestType type, Random rng)
        {
            string item;
            GridPoint? hidden = null;
            int? cost = null;
            int? targetHouse = null;

            switch (type)
            {
                case QuestType.LostItem:
                    item = LostItems[rng.Next(LostItems.Length)];
                    hidden = new GridPoint(
                        (float)(rng.NextDouble() * 2 - 1) * HiddenItemExtent,
                        (float)(rng.NextDouble() * 2 - 1) * HiddenItemExtent);
                    break;
                case QuestType.BuyGift:
                    item = GiftItems[rng.Next(GiftItems.Length)];
                    cost = ItemCatalog.Get(item).Cost;
                    break;
                case QuestType.DecorationRequest:
                    item = DecorationItems[rng.Next(DecorationItems.Length)];
                    cost = ItemCatalog.Get(item).Cost;
                    break;
                default:
                    item = "bug spray";
                    targetHouse = dog.HouseId;
                    break;
            }

            var quest = new Quest(nextQuestId++, type, dog.Name, item,
                QuestTemplates.For(type).Render(dog, item), hidden, cost, targetHouse);
            quests.Add(quest);
            dog.GiveQuest();
            return quest;
        }

        /// <summary>Accepts a quest. Buy-type quests deduct their cost here
        /// (docs/specs/quests/quest-content.md) and are rejected — spend
        /// untouched — when unaffordable (#25).</summary>
        public bool Accept(Quest quest)
        {
            if (quest.Status != QuestStatus.Available)
            {
                return false;
            }

            if (quest.Cost.HasValue && !state.Wallet.TrySpend(quest.Cost.Value))
            {
                return false;
            }

            quest.Status = QuestStatus.Accepted;
            if (quest.Type == QuestType.BuyGift || quest.Type == QuestType.DecorationRequest)
            {
                quest.DeliveryPhase = DeliveryPhase.HeadingHome;
            }

            return true;
        }

        /// <summary>#30: the dog reaches home and sits waiting for the truck.</summary>
        public void NotifyDogArrivedHome(Quest quest)
        {
            if (quest.DeliveryPhase != DeliveryPhase.HeadingHome)
            {
                return;
            }

            quest.DeliveryPhase = DeliveryPhase.WaitingForDelivery;
            FindDog(quest).TrySit(buyQuestAccepted: true, isAtHome: true);
        }

        /// <summary>#30: the truck delivers — the item appears at the house
        /// permanently (#27) and only now does the quest complete and pay.</summary>
        public void DeliverPackage(Quest quest)
        {
            if (quest.DeliveryPhase != DeliveryPhase.WaitingForDelivery)
            {
                return;
            }

            quest.DeliveryPhase = DeliveryPhase.Delivered;
            var dog = FindDog(quest);
            state.AddPlacedItem(dog.HouseId, quest.ItemName);
            dog.PlaceOnStreet();
            Complete(quest);
        }

        /// <summary>#31: hidden-object search — tapping the hidden spot of an
        /// accepted LostItem quest completes it; anywhere else, nothing.</summary>
        public bool TapWorldPosition(GridPoint tap)
        {
            var hit = quests.FirstOrDefault(q =>
                q.Type == QuestType.LostItem
                && q.Status == QuestStatus.Accepted
                && q.HiddenItemPosition.HasValue
                && TapResolver.IsHit(q.HiddenItemPosition.Value, tap, LostItemTapRadius));

            if (hit == null)
            {
                return false;
            }

            Complete(hit);
            return true;
        }

        /// <summary>#53: spraying the afflicted house completes its accepted
        /// PestControl quest; spraying anything else is a no-op.</summary>
        public bool SprayHouse(int houseId)
        {
            var hit = quests.FirstOrDefault(q =>
                q.Type == QuestType.PestControl
                && q.Status == QuestStatus.Accepted
                && q.TargetHouseId == houseId);

            if (hit == null)
            {
                return false;
            }

            Complete(hit);
            return true;
        }

        private void Complete(Quest quest)
        {
            quest.Status = QuestStatus.Completed;
            FindDog(quest).ClearQuest();
            state.Wallet.Deposit(EconomyNumbers.QuestPayout);
        }

        private Dog FindDog(Quest quest)
        {
            return state.Dogs.First(d => d.Name == quest.DogName);
        }
    }
}
