using System.Collections.Generic;
using System.Linq;

namespace Doggiehood.Core.World
{
    /// <summary>
    /// Root of the mutable game state. A new game starts with the four
    /// starting houses, one per quadrant of the intersection (#38).
    /// </summary>
    public sealed class GameState
    {
        public IReadOnlyList<House> Houses { get; }

        private GameState(IReadOnlyList<House> houses)
        {
            Houses = houses;
        }

        public static GameState CreateNew()
        {
            var houses = NeighborhoodLayout.HouseLots
                .Select(lot => new House(lot.HouseId, lot.Quadrant))
                .ToList();

            return new GameState(houses);
        }
    }
}
