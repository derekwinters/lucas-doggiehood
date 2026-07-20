using System;
using System.Collections.Generic;
using System.Linq;
using Doggiehood.Core.Dogs;
using Doggiehood.Core.World;

namespace Doggiehood.Core.Expansion
{
    /// <summary>
    /// Bridges MoveInSystem's abstract VacantHouses id set (#54) to real
    /// World.House objects (#58): derives the current vacant set from
    /// each House's <see cref="House.IsVacant"/> flag, runs one
    /// MoveInSystem roll, and — only on a successful move-in — marks the
    /// filled house occupied. This is the single place
    /// <see cref="House.MarkOccupied"/> is ever called from production
    /// wiring, so vacancy flips exactly when a household moves in, never
    /// otherwise.
    /// </summary>
    public static class HouseOccupancy
    {
        /// <summary>Call once per completed quest (GameState.HandleQuestCompleted).
        /// Returns the newly moved-in household (empty if no move-in
        /// happened this time, e.g. no vacant house exists or the pity
        /// roll failed).</summary>
        public static IReadOnlyList<Dog> ApplyMoveIn(IReadOnlyList<House> houses, MoveInSystem moveInSystem,
            IReadOnlyList<Dog> activeDogs, Random rng)
        {
            var vacantHouses = new VacantHouses(houses.Where(h => h.IsVacant).Select(h => h.Id));
            var household = moveInSystem.OnQuestCompleted(vacantHouses, activeDogs, rng);

            if (household.Count > 0)
            {
                var occupiedHouseId = household[0].HouseId;
                houses.First(h => h.Id == occupiedHouseId).MarkOccupied();
            }

            return household;
        }
    }
}
