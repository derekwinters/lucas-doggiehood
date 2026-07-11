using System;
using System.Collections.Generic;

namespace Doggiehood.Core.Dogs
{
    /// <summary>
    /// The curated 66-name pool for dogs that move in later (#67). Hank and
    /// Stella are deliberately absent — they're reserved easter-egg names
    /// (#68). No live consumer until expansion (post-MVP); that's expected.
    /// </summary>
    public static class NamePool
    {
        public static IReadOnlyList<string> Names { get; } = new[]
        {
            // Classic
            "Buddy", "Max", "Charlie", "Rocky", "Cooper", "Bear", "Tucker",
            "Jack", "Toby", "Milo", "Oliver", "Leo", "Winston", "Baxter",
            "Bentley", "Gus", "Murphy", "Finn", "Otis", "Chase", "Rusty",
            "Sam", "Boomer", "Ollie", "Louie", "Bruno", "Diesel", "Sarge",
            "Ranger", "Frank", "Gordon", "Reggie", "Wally",
            // Playful / food-themed
            "Biscuit", "Pretzel", "Peanut", "Nugget", "Ziggy", "Mochi",
            "Taco", "Noodle", "Pickles", "Beans", "Cricket", "Marshmallow",
            "Biscotti", "Nutmeg", "Cinnamon", "Pumpkin", "Buttercup",
            "Petunia", "Juniper",
            // Classic (softer)
            "Bella", "Luna", "Daisy", "Coco", "Molly", "Sadie", "Ruby",
            "Rosie", "Lucy", "Zoe", "Maggie", "Penny", "Roxy", "Willow",
            "Ginger", "Honey",
        };

        /// <summary>Picks a random unused name; deterministic per RNG seed.</summary>
        public static string PickName(Random random, ISet<string> namesInUse)
        {
            var available = new List<string>();
            foreach (var name in Names)
            {
                if (!namesInUse.Contains(name))
                {
                    available.Add(name);
                }
            }

            if (available.Count == 0)
            {
                throw new InvalidOperationException("Every name in the pool is already in use.");
            }

            return available[random.Next(available.Count)];
        }
    }
}
