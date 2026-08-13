using System;
using System.Collections.Generic;
using Doggiehood.Core.Dogs;

namespace Doggiehood.Core.Quests
{
    /// <summary>#708: who owes the next action on an accepted quest — the one
    /// Core-side reading that both the reminder pool and the reminder's dismiss
    /// pill follow, so the line and the label can never disagree.
    /// <see cref="Player"/> means there is still work for the player to do (find
    /// the lost item, clear the bugs); <see cref="Game"/> means the player is
    /// already done (the purchase was made at accept) and the game is finishing
    /// the job — the delivery is in flight.</summary>
    public enum PendingActionOwner
    {
        Player,
        Game,
    }

    /// <summary>
    /// A reusable quest dialogue template (#69) with variable slots:
    /// {dog} and {item}. Line variety (#189, "Model 2"): the opener, the
    /// closer, and the active-quest reminder (#472) each draw from a
    /// personality-agnostic default pool plus an optional small
    /// per-personality pool. The candidate set for a render is default
    /// UNION this dog's personality pool, and one line is picked uniformly
    /// at random per string (not per bucket) via an injectable RNG — pure
    /// random each fire, no anti-repeat memory, no per-dog/session persisted
    /// state, matching the move-in system's seeded-<see cref="Random"/>
    /// convention (docs/specs/expansion.md).
    /// </summary>
    public sealed class QuestTemplate
    {
        private readonly IReadOnlyList<string> defaultOpeners;
        private readonly IReadOnlyDictionary<Personality, IReadOnlyList<string>> flavoredOpeners;
        private readonly IReadOnlyList<string> defaultClosers;
        private readonly IReadOnlyDictionary<Personality, IReadOnlyList<string>> flavoredClosers;
        private readonly IReadOnlyList<string> defaultReminders;
        private readonly IReadOnlyDictionary<Personality, IReadOnlyList<string>> flavoredReminders;

        /// <summary>#708: the dismiss pill's label when the player still owes
        /// the next action — the quest is genuinely outstanding work.</summary>
        public const string PlayerOwedDismissLabel = "Still looking";

        /// <summary>#708: the dismiss pill's label when the game owes the next
        /// action — the item is bought and the delivery is in flight, so
        /// "Still looking" would ask for work the player already did.</summary>
        public const string GameOwedDismissLabel = "On its way";

        public QuestTemplate(
            IReadOnlyList<string> defaultOpeners,
            IReadOnlyDictionary<Personality, IReadOnlyList<string>> flavoredOpeners,
            IReadOnlyList<string> defaultClosers,
            IReadOnlyDictionary<Personality, IReadOnlyList<string>> flavoredClosers,
            IReadOnlyList<string> defaultReminders,
            IReadOnlyDictionary<Personality, IReadOnlyList<string>> flavoredReminders,
            PendingActionOwner reminderOwner = PendingActionOwner.Player)
        {
            ReminderOwner = reminderOwner;
            this.defaultOpeners = defaultOpeners;
            this.flavoredOpeners = flavoredOpeners;
            this.defaultClosers = defaultClosers;
            this.flavoredClosers = flavoredClosers;
            this.defaultReminders = defaultReminders;
            this.flavoredReminders = flavoredReminders;
        }

        public IReadOnlyList<string> DefaultOpeners => defaultOpeners;
        public IReadOnlyDictionary<Personality, IReadOnlyList<string>> FlavoredOpeners => flavoredOpeners;
        public IReadOnlyList<string> DefaultClosers => defaultClosers;
        public IReadOnlyDictionary<Personality, IReadOnlyList<string>> FlavoredClosers => flavoredClosers;
        public IReadOnlyList<string> DefaultReminders => defaultReminders;
        public IReadOnlyDictionary<Personality, IReadOnlyList<string>> FlavoredReminders => flavoredReminders;

        /// <summary>#708: who owes the next action once this quest is accepted.
        /// The reminder pool above is written in that voice, and
        /// <see cref="ReminderDismissLabel"/> is derived from it — one source of
        /// truth, so the pill can never contradict the line.</summary>
        public PendingActionOwner ReminderOwner { get; }

        /// <summary>#708: the label on the active-quest reminder's dismiss pill
        /// (#472) — "Still looking" while the player still owes the next action,
        /// "On its way" once the purchase is made and the delivery is in
        /// flight.</summary>
        public string ReminderDismissLabel => ReminderOwner == PendingActionOwner.Game
            ? GameOwedDismissLabel
            : PlayerOwedDismissLabel;

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

        /// <summary>#472: the single contextual line shown when a dog with an
        /// already-<c>Accepted</c> quest is re-tapped. Same pooled Model 2 pick
        /// and {dog}/{item} substitution as <see cref="Render"/>, just one line
        /// (a reminder, not a fresh opener/closer offer).</summary>
        public string RenderReminder(Dog dog, string itemName, Random random)
        {
            var reminder = PickLine(defaultReminders, flavoredReminders, dog.Personality, random);
            return Fill(reminder, dog, itemName);
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
