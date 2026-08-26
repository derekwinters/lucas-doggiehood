using Doggiehood.Core.Versioning;
using NUnit.Framework;

namespace Doggiehood.Core.Tests.Versioning
{
    public class ApplicationIdSuffixTests
    {
        [Test]
        public void Apply_AppendsDebugSuffix_WhenDebugBuildRequested()
        {
            Assert.That(
                ApplicationIdSuffix.Apply("com.derekwinters.doggiehood", isDebugBuild: true),
                Is.EqualTo("com.derekwinters.doggiehood.debug"));
        }

        [Test]
        public void Apply_LeavesIdentifierUnchanged_WhenDebugBuildNotRequested()
        {
            Assert.That(
                ApplicationIdSuffix.Apply("com.derekwinters.doggiehood", isDebugBuild: false),
                Is.EqualTo("com.derekwinters.doggiehood"));
        }

        [Test]
        public void Apply_IsIdempotent_WhenSuffixAlreadyPresent()
        {
            // A postprocess restore that fails to run, or a double-invocation,
            // must not compound the suffix into ".debug.debug".
            Assert.That(
                ApplicationIdSuffix.Apply("com.derekwinters.doggiehood.debug", isDebugBuild: true),
                Is.EqualTo("com.derekwinters.doggiehood.debug"));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("   ")]
        public void Apply_RejectsMissingBaseApplicationId(string baseApplicationId)
        {
            Assert.That(() => ApplicationIdSuffix.Apply(baseApplicationId, isDebugBuild: true), Throws.ArgumentException);
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
        public void IsDebugBuildRequested_ParsesTruthyEnvironmentValues(string envValue, bool expected)
        {
            Assert.That(ApplicationIdSuffix.IsDebugBuildRequested(envValue), Is.EqualTo(expected));
        }

        [Test]
        public void CommandLineFlag_IsTheBuilderCustomParameterSwitch()
        {
            // pr-build.yml and rc-build.yml spell this exact switch in
            // `customParameters`; a rename on either side silently ships an
            // unsuffixed debug APK, so the name is pinned here.
            Assert.That(ApplicationIdSuffix.CommandLineFlag, Is.EqualTo("-doggiehoodDebugBuild"));
        }

        [Test]
        public void IsDebugBuildRequested_IsTrue_FromTheCommandLineAlone()
        {
            // #734 (the same defect as #731, on this flag): game-ci/unity-builder
            // runs Unity in a Docker container and forwards only a fixed
            // allowlist of environment variables, so DOGGIEHOOD_DEBUG_BUILD set
            // on the workflow step never arrives. The command line is the
            // channel that does, and it must be sufficient on its own.
            var args = new[] { "unity-editor", ApplicationIdSuffix.CommandLineFlag };

            Assert.That(
                ApplicationIdSuffix.IsDebugBuildRequested(envValue: null, commandLineArgs: args),
                Is.True);
        }

        [Test]
        public void IsDebugBuildRequested_IsTrue_FromTheEnvironmentAlone()
        {
            // The local/editor channel keeps working — the command line is an
            // addition, not a replacement.
            var args = new[] { "unity-editor", "-batchmode" };

            Assert.That(
                ApplicationIdSuffix.IsDebugBuildRequested(envValue: "true", commandLineArgs: args),
                Is.True);
        }

        [Test]
        public void IsDebugBuildRequested_IsFalse_WhenNeitherChannelRequestsIt()
        {
            // The release build passes no switch and sets no variable, and must
            // keep shipping the bare com.derekwinters.doggiehood id.
            var args = new[] { "unity-editor", "-batchmode" };

            Assert.That(
                ApplicationIdSuffix.IsDebugBuildRequested(envValue: null, commandLineArgs: args),
                Is.False);
        }

        [Test]
        public void IsDebugBuildRequested_IsTrue_WhenBothChannelsRequestIt()
        {
            var args = new[] { "unity-editor", ApplicationIdSuffix.CommandLineFlag };

            Assert.That(
                ApplicationIdSuffix.IsDebugBuildRequested(envValue: "true", commandLineArgs: args),
                Is.True);
        }
    }
}
