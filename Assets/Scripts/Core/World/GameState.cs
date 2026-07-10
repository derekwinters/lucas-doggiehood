using System.Collections.Generic;
using System.Linq;
using Doggiehood.Core.Dogs;

namespace Doggiehood.Core.World
{
    /// <summary>
    /// Root of the mutable game state. A new game starts with the four
    /// starting houses, one per quadrant of the intersection (#38), and the
    /// eight roster dogs living in them (#63).
    /// </summary>
    public sealed class GameState
    {
        private readonly List<PlacedItem> placedItems = new List<PlacedItem>();

        public IReadOnlyList<House> Houses { get; }
        public IReadOnlyList<Dog> Dogs { get; }
        public Economy.Wallet Wallet { get; }
        public Quests.QuestManager Quests { get; }

        /// <summary>Permanent world changes from completed quests (#27).</summary>
        public IReadOnlyList<PlacedItem> PlacedItems
        {
            get { return placedItems; }
        }

        private GameState(IReadOnlyList<House> houses, IReadOnlyList<Dog> dogs)
        {
            Houses = houses;
            Dogs = dogs;
            Wallet = new Economy.Wallet();
            Quests = new Quests.QuestManager(this);
        }

        public static GameState CreateNew()
        {
            var houses = NeighborhoodLayout.HouseLots
                .Select(lot => new House(lot.HouseId, lot.Quadrant))
                .ToList();

            return new GameState(houses, DogRoster.CreateStartingDogs());
        }

        public void AddPlacedItem(int houseId, string itemName)
        {
            placedItems.Add(new PlacedItem(houseId, itemName));
        }
    }
}
