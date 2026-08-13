using Doggiehood.Core.Versioning;
using NUnit.Framework;

namespace Doggiehood.Core.Tests.Versioning
{
    /// <summary>
    /// Covers the command-line half of the build-variant flag convention
    /// (#731). <c>game-ci/unity-builder</c> runs Unity inside a Docker
    /// container and forwards only a fixed allowlist of environment
    /// variables, so a repo-specific variable like
    /// <c>DOGGIEHOOD_EMULATOR_BUILD</c> never reaches the Unity process —
    /// the variant has to be requested on Unity's own command line instead,
    /// via the builder's <c>customParameters</c> input. This is the pure
    /// parser for that argument vector; the truthy/falsy convention it
    /// applies to an explicit value is the shared one in
    /// <see cref="BuildEnvironmentFlagTests"/>.
    /// </summary>
    public class BuildCommandLineFlagTests
    {
        private const string Flag = "-doggiehoodEmulatorBuild";

        [Test]
        public void IsEnabled_IsFalse_WhenTheFlagIsAbsent()
        {
            var args = new[] { "/opt/unity/Editor/Unity", "-batchmode", "-quit" };

            Assert.That(BuildCommandLineFlag.IsEnabled(args, Flag), Is.False);
        }

        [Test]
        public void IsEnabled_IsFalse_WhenTheArgumentVectorIsNull()
        {
            // Unity always hands back an argv, but a null must never be read
            // as "the variant was requested".
            Assert.That(BuildCommandLineFlag.IsEnabled(null, Flag), Is.False);
        }

        [Test]
        public void IsEnabled_IsTrue_WhenTheBareFlagIsPresent()
        {
            // The shape the release workflows use: `customParameters` appends
            // the bare switch to unity-editor's command line.
            var args = new[] { "unity-editor", "-buildTarget", "Android", Flag };

            Assert.That(BuildCommandLineFlag.IsEnabled(args, Flag), Is.True);
        }

        [Test]
        public void IsEnabled_IsTrue_WhenTheFlagIsFollowedByAnotherSwitch()
        {
            var args = new[] { "unity-editor", Flag, "-nographics" };

            Assert.That(BuildCommandLineFlag.IsEnabled(args, Flag), Is.True);
        }

        [Test]
        public void IsEnabled_MatchesTheFlagCaseInsensitively()
        {
            // Unity's own switches are matched case-insensitively; a workflow
            // that types `-doggiehoodemulatorbuild` should not silently
            // produce a device build.
            var args = new[] { "unity-editor", "-DOGGIEHOODEMULATORBUILD" };

            Assert.That(BuildCommandLineFlag.IsEnabled(args, Flag), Is.True);
        }

        [Test]
        public void IsEnabled_IgnoresSurroundingWhitespaceOnAnArgument()
        {
            var args = new[] { "unity-editor", "  " + Flag + " " };

            Assert.That(BuildCommandLineFlag.IsEnabled(args, Flag), Is.True);
        }

        [Test]
        public void IsEnabled_DoesNotMatchAFlagThatMerelyStartsTheSame()
        {
            var args = new[] { "unity-editor", Flag + "Extra" };

            Assert.That(BuildCommandLineFlag.IsEnabled(args, Flag), Is.False);
        }

        [TestCase("true", true)]
        [TestCase("TRUE", true)]
        [TestCase("1", true)]
        [TestCase("false", false)]
        [TestCase("0", false)]
        [TestCase("yes", false)]
        public void IsEnabled_ParsesAnExplicitValueWithTheSharedTruthyConvention(
            string value, bool expected)
        {
            // `-doggiehoodEmulatorBuild false` must mean off, exactly as
            // DOGGIEHOOD_EMULATOR_BUILD=false does — one convention, two
            // channels, so a reader can't be surprised by either.
            var args = new[] { "unity-editor", Flag, value };

            Assert.That(BuildCommandLineFlag.IsEnabled(args, Flag), Is.EqualTo(expected));
        }

        [Test]
        public void IsEnabled_IgnoresNullEntriesInTheArgumentVector()
        {
            var args = new[] { "unity-editor", null, Flag };

            Assert.That(BuildCommandLineFlag.IsEnabled(args, Flag), Is.True);
        }

        [Test]
        public void IsEnabled_IsFalse_WhenNoFlagNameIsGiven()
        {
            var args = new[] { "unity-editor", "-batchmode" };

            Assert.That(BuildCommandLineFlag.IsEnabled(args, null), Is.False);
        }
    }
}
