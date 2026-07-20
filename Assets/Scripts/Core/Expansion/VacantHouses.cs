using System;
using System.Collections.Generic;

namespace Doggiehood.Core.Expansion
{
    /// <summary>
    /// The abstract set of vacant (unoccupied) house ids (#54). Deliberately
    /// decoupled from World.House/HouseLot geometry: #56/#57/#58 build the
    /// actual zone-unlock/house-building/vacancy-visual pipeline against
    /// this surface (or extend it) once tile placement (#109) lands.
    /// </summary>
    public sealed class VacantHouses
    {
        private readonly List<int> houseIds;

        public VacantHouses(IEnumerable<int> initialVacantHouseIds = null)
        {
            houseIds = initialVacantHouseIds == null ? new List<int>() : new List<int>(initialVacantHouseIds);
        }

        public bool HasAny
        {
            get { return houseIds.Count > 0; }
        }

        public IReadOnlyList<int> Ids
        {
            get { return houseIds; }
        }

        /// <summary>A newly built house starts vacant (#57/#58) until a dog moves in.</summary>
        public void Add(int houseId)
        {
            houseIds.Add(houseId);
        }

        /// <summary>Uniformly picks one vacant house and marks it occupied
        /// (removed from the vacant set).</summary>
        public int TakeRandom(Random rng)
        {
            if (!HasAny)
            {
                throw new InvalidOperationException("No vacant houses to take.");
            }

            var index = rng.Next(houseIds.Count);
            var houseId = houseIds[index];
            houseIds.RemoveAt(index);
            return houseId;
        }
    }
}
