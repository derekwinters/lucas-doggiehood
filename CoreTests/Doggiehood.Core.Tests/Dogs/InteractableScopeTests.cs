using System.Linq;
using Doggiehood.Core.Dogs;
using NUnit.Framework;

namespace Doggiehood.Core.Tests.Dogs
{
    /// <summary>#37: dogs are the only interactable characters for v1.</summary>
    public class InteractableScopeTests
    {
        [Test]
        public void DogIsTheOnlyInteractableCharacterKind()
        {
            Assert.That(InteractableCharacters.Kinds, Is.EquivalentTo(new[] { "Dog" }));
        }

        [Test]
        public void CoreContainsNoOtherCharacterTypes()
        {
            // Guard: no cats, people, mail carriers, squirrels... sneaking in.
            // Matches whole PascalCase words so DogLocation ("cat") and
            // Personality ("person") don't false-positive.
            // (Invariant guard — no red phase.)
            var forbidden = new[] { "cat", "cats", "person", "people", "human", "carrier", "squirrel", "bird" };

            var offenders = typeof(Dog).Assembly.GetTypes()
                .Where(t => t.Namespace != null
                    && t.Namespace.StartsWith("Doggiehood.Core")
                    && !t.Namespace.Contains("Tests"))
                .Where(t => PascalWords(t.Name).Any(word => forbidden.Contains(word)))
                .Select(t => t.FullName)
                .ToList();

            Assert.That(offenders, Is.Empty);
        }

        private static string[] PascalWords(string typeName)
        {
            return System.Text.RegularExpressions.Regex
                .Split(typeName, "(?<!^)(?=[A-Z])")
                .Select(word => word.ToLowerInvariant())
                .ToArray();
        }
    }
}
