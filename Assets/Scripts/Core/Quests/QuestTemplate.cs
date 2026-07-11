using System.Collections.Generic;
using Doggiehood.Core.Dogs;

namespace Doggiehood.Core.Quests
{
    /// <summary>
    /// A reusable quest dialogue template (#69) with variable slots:
    /// {dog} and {item}, plus a personality-flavored variant of the opening
    /// line. Templates are why the daily rotation never needs hand-written
    /// per-dog conversations.
    /// </summary>
    public sealed class QuestTemplate
    {
        private readonly IReadOnlyDictionary<Personality, string> flavoredOpeners;
        private readonly string defaultOpener;
        private readonly IReadOnlyList<string> followUpLines;

        public QuestTemplate(string defaultOpener,
            IReadOnlyDictionary<Personality, string> flavoredOpeners,
            IReadOnlyList<string> followUpLines)
        {
            this.defaultOpener = defaultOpener;
            this.flavoredOpeners = flavoredOpeners;
            this.followUpLines = followUpLines;
        }

        public IReadOnlyList<string> Render(Dog dog, string itemName)
        {
            var opener = flavoredOpeners.TryGetValue(dog.Personality, out var flavored)
                ? flavored
                : defaultOpener;

            var lines = new List<string> { Fill(opener, dog, itemName) };
            foreach (var line in followUpLines)
            {
                lines.Add(Fill(line, dog, itemName));
            }

            return lines;
        }

        private static string Fill(string template, Dog dog, string itemName)
        {
            return template.Replace("{dog}", dog.Name).Replace("{item}", itemName);
        }
    }
}
