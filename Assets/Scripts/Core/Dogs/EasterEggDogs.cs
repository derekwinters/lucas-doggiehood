using System;
using System.Collections.Generic;
using System.Linq;

namespace Doggiehood.Core.Dogs
{
    /// <summary>
    /// Reserved easter-egg names (#68): these always resolve to one fixed
    /// breed (and coat where noted) — never randomized, never in the
    /// general name pool (#67).
    /// </summary>
    public static class EasterEggDogs
    {
        public readonly struct FixedAppearance
        {
            public Breed Breed { get; }
            public CoatColor Coat { get; }

            public FixedAppearance(Breed breed, CoatColor coat)
            {
                Breed = breed;
                Coat = coat;
            }
        }

        private static readonly IReadOnlyDictionary<string, FixedAppearance> Table =
            new Dictionary<string, FixedAppearance>
            {
                { "Rex", new FixedAppearance(Breed.GermanShepherd, CoatColor.Black) },
                { "Arnie", new FixedAppearance(Breed.GoldenRetriever, CoatColor.Light) },
                { "Hank", new FixedAppearance(Breed.GoldenRetriever, CoatColor.Dark) },
                { "Stella", new FixedAppearance(Breed.Chihuahua, CoatColor.Default) },
                { "Muffin", new FixedAppearance(Breed.Puggle, CoatColor.Default) },
                { "Akon", new FixedAppearance(Breed.Puggle, CoatColor.Default) },
                { "Brody", new FixedAppearance(Breed.GoldenRetriever, CoatColor.Default) },
            };

        public static IReadOnlyList<string> ReservedNames
        {
            get { return Table.Keys.ToList(); }
        }

        public static bool IsReserved(string name)
        {
            return Table.ContainsKey(name);
        }

        public static FixedAppearance Resolve(string name)
        {
            if (!Table.TryGetValue(name, out var appearance))
            {
                throw new ArgumentException($"'{name}' is not a reserved easter-egg name.", nameof(name));
            }

            return appearance;
        }
    }
}
