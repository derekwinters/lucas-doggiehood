using System;
using System.Collections.Generic;
using System.Linq;
using Doggiehood.Core.Dogs;

namespace Doggiehood.Core.Expansion
{
    /// <summary>
    /// Count-weighted breed randomness (#54): "each breed's weight is
    /// inversely proportional to how many of that breed currently live in
    /// the neighborhood, so the math adapts to whatever the existing
    /// distribution is." Used once the reserved French Bulldog/Puggle
    /// move-ins have both happened (see MoveInSystem).
    /// </summary>
    public static class BreedWeighting
    {
        public static Breed PickWeighted(IReadOnlyList<Dog> activeDogs, Random rng)
        {
            var breeds = (Breed[])Enum.GetValues(typeof(Breed));
            var weights = breeds
                .Select(breed => 1.0 / (activeDogs.Count(d => d.Breed == breed) + MoveInNumbers.BreedWeightSmoothing))
                .ToArray();
            var total = weights.Sum();

            var roll = rng.NextDouble() * total;
            var cumulative = 0.0;
            for (var i = 0; i < breeds.Length; i++)
            {
                cumulative += weights[i];
                if (roll < cumulative)
                {
                    return breeds[i];
                }
            }

            // Floating-point edge case: fall back to the last breed.
            return breeds[breeds.Length - 1];
        }
    }
}
