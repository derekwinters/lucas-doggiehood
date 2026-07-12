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
    }
}
