using System;
using System.Collections.Generic;
using Doggiehood.Core.Dogs;

namespace Doggiehood.Core.Quests
{
    /// <summary>
    /// A reusable quest dialogue template (#69) with variable slots:
    /// {dog} and {item}. Line variety (#189, "Model 2"): both the opener
    /// and the closer draw from a personality-agnostic default pool plus
    /// an optional small per-personality pool. The candidate set for a
    /// render is default UNION this dog's personality pool, and one line
    /// is picked uniformly at random per string (not per bucket) via an
    /// injectable RNG — pure random each fire, no anti-repeat memory, no
    /// per-dog/session persisted state, matching the move-in system's
    /// seeded-<see cref="Random"/> convention (docs/specs/expansion.md).
    /// </summary>
    public sealed class QuestTemplate
    {
        private readonly IReadOnlyList<string> defaultOpeners;
        private readonly IReadOnlyDictionary<Personality, IReadOnlyList<string>> flavoredOpeners;
        private readonly IReadOnlyList<string> defaultClosers;
        private readonly IReadOnlyDictionary<Personality, IReadOnlyList<string>> flavoredClosers;

        public QuestTemplate(
            IReadOnlyList<string> defaultOpeners,
            IReadOnlyDictionary<Personality, IReadOnlyList<string>> flavoredOpeners,
            IReadOnlyList<string> defaultClosers,
            IReadOnlyDictionary<Personality, IReadOnlyList<string>> flavoredClosers)
        {
            this.defaultOpeners = defaultOpeners;
            this.flavoredOpeners = flavoredOpeners;
            this.defaultClosers = defaultClosers;
            this.flavoredClosers = flavoredClosers;
        }

        public IReadOnlyList<string> DefaultOpeners => defaultOpeners;
        public IReadOnlyDictionary<Personality, IReadOnlyList<string>> FlavoredOpeners => flavoredOpeners;
        public IReadOnlyList<string> DefaultClosers => defaultClosers;
        public IReadOnlyDictionary<Personality, IReadOnlyList<string>> FlavoredClosers => flavoredClosers;

        public IReadOnlyList<string> Render(Dog dog, string itemName, Random random)
        {
            var opener = PickLine(defaultOpeners, flavoredOpeners, dog.Personality, random);
            var closer = PickLine(defaultClosers, flavoredClosers, dog.Personality, random);

            return new List<string>
            {
                Fill(opener, dog, itemName),
                Fill(closer, dog, itemName),
            };
        }

        private static string PickLine(
            IReadOnlyList<string> defaults,
            IReadOnlyDictionary<Personality, IReadOnlyList<string>> flavored,
            Personality personality,
            Random random)
        {
            var candidates = new List<string>(defaults);
            if (flavored.TryGetValue(personality, out var personalityLines))
            {
                candidates.AddRange(personalityLines);
            }

            return candidates[random.Next(candidates.Count)];
        }

        private static string Fill(string template, Dog dog, string itemName)
        {
            return template.Replace("{dog}", dog.Name).Replace("{item}", itemName);
        }
    }
}
