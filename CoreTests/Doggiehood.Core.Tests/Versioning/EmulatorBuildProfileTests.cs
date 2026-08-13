using Doggiehood.Core.Versioning;
using NUnit.Framework;

namespace Doggiehood.Core.Tests.Versioning
{
    /// <summary>
    /// Covers the emulator build variant's Unity-independent half (#648):
    /// the <c>DOGGIEHOOD_EMULATOR_BUILD</c> truthy convention and the
    /// <c>.emulator</c> applicationId suffix. Mirrors
    /// <see cref="ApplicationIdSuffixTests"/>, which covers the sibling
    /// <c>.debug</c> mechanism.
    /// </summary>
    public class EmulatorBuildProfileTests
    {
        private const string PermanentApplicationId = "com.derekwinters.doggiehood";

        [Test]
        public void Suffix_IsTheEmulatorSuffix()
        {
            Assert.That(EmulatorBuildProfile.Suffix, Is.EqualTo(".emulator"));
        }

        [Test]
        public void Apply_AppendsEmulatorSuffix_WhenEmulatorBuildRequested()
        {
            Assert.That(
                EmulatorBuildProfile.Apply(PermanentApplicationId, isEmulatorBuild: true),
                Is.EqualTo("com.derekwinters.doggiehood.emulator"));
        }

        [Test]
        public void Apply_LeavesIdentifierUnchanged_WhenEmulatorBuildNotRequested()
        {
            Assert.That(
                EmulatorBuildProfile.Apply(PermanentApplicationId, isEmulatorBuild: false),
                Is.EqualTo(PermanentApplicationId));
        }

        [Test]
        public void Apply_IsIdempotent_WhenSuffixAlreadyPresent()
        {
            // A postprocess restore that fails to run, or a double-invocation,
            // must not compound the suffix into ".emulator.emulator".
            Assert.That(
                EmulatorBuildProfile.Apply("com.derekwinters.doggiehood.emulator", isEmulatorBuild: true),
                Is.EqualTo("com.derekwinters.doggiehood.emulator"));
        }

        [Test]
        public void Apply_StacksOnTopOfTheDebugSuffix_SoTheVariantsInstallSideBySide()
        {
            // The emulator variant is a debug-signed build; if a run ever sets
            // both env vars the two suffixes must compose rather than clash.
            Assert.That(
                EmulatorBuildProfile.Apply("com.derekwinters.doggiehood.debug", isEmulatorBuild: true),
                Is.EqualTo("com.derekwinters.doggiehood.debug.emulator"));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("   ")]
        public void Apply_RejectsMissingBaseApplicationId(string baseApplicationId)
        {
            Assert.That(
                () => EmulatorBuildProfile.Apply(baseApplicationId, isEmulatorBuild: true),
                Throws.ArgumentException);
        }

        [TestCase("1", true)]
        [TestCase("true", true)]
        [TestCase("TRUE", true)]
        [TestCase(" true ", true)]
        [TestCase("0", false)]
        [TestCase("false", false)]
        [TestCase("", false)]
        [TestCase(null, false)]
        [TestCase("   ", false)]
        [TestCase("yes", false)]
        public void IsEmulatorBuildRequested_ParsesTruthyEnvironmentValues(string envValue, bool expected)
        {
            Assert.That(EmulatorBuildProfile.IsEmulatorBuildRequested(envValue), Is.EqualTo(expected));
        }

        [Test]
        public void CommandLineFlag_IsTheBuilderCustomParameterSwitch()
        {
            // The workflows spell this exact switch in `customParameters`; a
            // rename on either side silently produces a device build, so the
            // name is pinned here.
            Assert.That(EmulatorBuildProfile.CommandLineFlag, Is.EqualTo("-doggiehoodEmulatorBuild"));
        }

        [Test]
        public void IsEmulatorBuildRequested_IsTrue_FromTheCommandLineAlone()
        {
            // #731: game-ci/unity-builder runs Unity in a Docker container and
            // forwards only a fixed allowlist of environment variables, so
            // DOGGIEHOOD_EMULATOR_BUILD never arrives. The command line is the
            // channel that does, and it must be sufficient on its own.
            var args = new[] { "unity-editor", EmulatorBuildProfile.CommandLineFlag };

            Assert.That(
                EmulatorBuildProfile.IsEmulatorBuildRequested(envValue: null, commandLineArgs: args),
                Is.True);
        }

        [Test]
        public void IsEmulatorBuildRequested_IsTrue_FromTheEnvironmentAlone()
        {
            // The local/editor channel keeps working — the command line is an
            // addition, not a replacement.
            var args = new[] { "unity-editor", "-batchmode" };

            Assert.That(
                EmulatorBuildProfile.IsEmulatorBuildRequested(envValue: "true", commandLineArgs: args),
                Is.True);
        }

        [Test]
        public void IsEmulatorBuildRequested_IsFalse_WhenNeitherChannelRequestsIt()
        {
            var args = new[] { "unity-editor", "-batchmode" };

            Assert.That(
                EmulatorBuildProfile.IsEmulatorBuildRequested(envValue: null, commandLineArgs: args),
                Is.False);
        }

        [Test]
        public void IsEmulatorBuildRequested_IsTrue_WhenBothChannelsRequestIt()
        {
            var args = new[] { "unity-editor", EmulatorBuildProfile.CommandLineFlag };

            Assert.That(
                EmulatorBuildProfile.IsEmulatorBuildRequested(envValue: "true", commandLineArgs: args),
                Is.True);
        }
    }
}
