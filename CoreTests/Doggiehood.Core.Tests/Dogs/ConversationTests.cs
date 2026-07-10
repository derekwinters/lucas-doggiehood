using System.Linq;
using Doggiehood.Core.Dogs;
using NUnit.Framework;

namespace Doggiehood.Core.Tests.Dogs
{
    public class ConversationTests
    {
        private static Dog QuestDog()
        {
            var dog = new Dog("Talky", Breed.Chihuahua, Personality.Grumpy, 3, false);
            dog.GiveQuest();
            return dog;
        }

        [Test]
        public void DogExposesHasActiveQuest()
        {
            // #10: the speech bubble binds to this flag.
            var dog = new Dog("Quiet", Breed.Beagle, Personality.Shy, 4, false);

            Assert.That(dog.HasActiveQuest, Is.False);
            dog.GiveQuest();
            Assert.That(dog.HasActiveQuest, Is.True);
            dog.ClearQuest();
            Assert.That(dog.HasActiveQuest, Is.False);
        }

        [Test]
        public void OpeningAConversation_RequiresAnActiveQuest()
        {
            // #11: no quest, no conversation — a no-op returning null.
            var dog = new Dog("Quiet", Breed.Beagle, Personality.Shy, 4, false);

            Assert.That(ConversationStarter.TryOpen(dog), Is.Null);

            dog.GiveQuest();
            var conversation = ConversationStarter.TryOpen(dog);

            Assert.That(conversation, Is.Not.Null);
            Assert.That(conversation.Lines, Is.Not.Empty);
        }

        [Test]
        public void ConversationsAreLinear_LinesThenOneEndingAction()
        {
            // #33: a linear sequence of plain text lines with exactly one
            // closing action — the schema has nowhere to hang a branch.
            var conversation = ConversationStarter.TryOpen(QuestDog());

            Assert.That(conversation.Lines, Is.All.InstanceOf<string>());
            Assert.That(conversation.Ending,
                Is.EqualTo(ConversationEnding.Accept).Or.EqualTo(ConversationEnding.Complete));
        }

        [Test]
        public void ConversationSchema_StructurallyPreventsBranching()
        {
            // #33: no property of Conversation (or anything it exposes) can
            // represent multiple outgoing choices from a line. Lines are raw
            // strings and the ending is a single enum value — verified by
            // reflection so a schema change that adds choice nodes fails here.
            var properties = typeof(Conversation).GetProperties();

            Assert.That(properties.Select(p => p.Name),
                Is.EquivalentTo(new[] { "Lines", "Ending" }),
                "Conversation must stay {Lines, Ending} — no choice/branch members");

            var linesType = typeof(Conversation).GetProperty("Lines").PropertyType;
            Assert.That(linesType.GenericTypeArguments.Single(), Is.EqualTo(typeof(string)),
                "lines must be plain strings, not nodes that could hold choices");
        }

        [Test]
        public void NoQuestLogOrJournalExists()
        {
            // #32: the speech bubble is the only quest-discovery surface.
            // (Invariant guard — no red phase.)
            var forbidden = new[] { "questlog", "journal" };

            var offenders = typeof(Conversation).Assembly.GetTypes()
                .Where(t => t.Namespace != null
                    && t.Namespace.StartsWith("Doggiehood.Core")
                    && !t.Namespace.Contains("Tests"))
                .Where(t => forbidden.Any(f => t.Name.ToLowerInvariant().Contains(f)))
                .Select(t => t.FullName)
                .ToList();

            Assert.That(offenders, Is.Empty);
        }
    }
}
