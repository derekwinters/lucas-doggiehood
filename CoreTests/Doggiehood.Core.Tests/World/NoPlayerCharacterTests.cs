using System.Linq;
using Doggiehood.Core.World;
using NUnit.Framework;

namespace Doggiehood.Core.Tests.World
{
    /// <summary>
    /// #19: the player is an unseen observer — no player character or avatar
    /// entity may exist in game state. Guard test: scans every type in the
    /// Doggiehood.Core namespace so a violation fails CI the moment it is
    /// introduced. (As an invariant guard, this test has no red phase.)
    /// </summary>
    public class NoPlayerCharacterTests
    {
        [Test]
        public void CoreContainsNoPlayerOrAvatarTypes()
        {
            var forbidden = new[] { "player", "avatar" };

            var offenders = typeof(NeighborhoodLayout).Assembly.GetTypes()
                .Where(t => t.Namespace != null
                    && t.Namespace.StartsWith("Doggiehood.Core")
                    && !t.Namespace.Contains("Tests"))
                .Where(t => forbidden.Any(f => t.Name.ToLowerInvariant().Contains(f)))
                .Select(t => t.FullName)
                .ToList();

            Assert.That(offenders, Is.Empty,
                "Core must not model a player character/avatar (#19): " + string.Join(", ", offenders));
        }
    }
}
