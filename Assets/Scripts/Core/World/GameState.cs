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
        public IReadOnlyList<House> Houses { get; }
        public IReadOnlyList<Dog> Dogs { get; }

        private GameState(IReadOnlyList<House> houses, IReadOnlyList<Dog> dogs)
        {
            Houses = houses;
            Dogs = dogs;
        }

        public static GameState CreateNew()
        {
            var houses = NeighborhoodLayout.HouseLots
                .Select(lot => new House(lot.HouseId, lot.Quadrant))
                .ToList();

            return new GameState(houses, DogRoster.CreateStartingDogs());
        }
    }
}
