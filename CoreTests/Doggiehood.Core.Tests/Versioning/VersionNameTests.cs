using Doggiehood.Core.Versioning;
using NUnit.Framework;

namespace Doggiehood.Core.Tests.Versioning
{
    public class VersionNameTests
    {
        [Test]
        public void ForDebugBuild_AppendsShortShaToBaseVersion()
        {
            Assert.That(VersionName.ForDebugBuild("0.1.0", "a1b2c3d"), Is.EqualTo("0.1.0-a1b2c3d"));
        }

        [Test]
        public void ForDebugBuild_TrimsWhitespaceFromInputs()
        {
            // VERSION file contents typically arrive with a trailing newline.
            Assert.That(VersionName.ForDebugBuild("0.1.0\n", " a1b2c3d "), Is.EqualTo("0.1.0-a1b2c3d"));
        }

        [TestCase(null, "a1b2c3d")]
        [TestCase("", "a1b2c3d")]
        [TestCase("   ", "a1b2c3d")]
        public void ForDebugBuild_RejectsMissingBaseVersion(string baseVersion, string shortSha)
        {
            Assert.That(() => VersionName.ForDebugBuild(baseVersion, shortSha), Throws.ArgumentException);
        }

        [TestCase("0.1.0", null)]
        [TestCase("0.1.0", "")]
        [TestCase("0.1.0", "   ")]
        public void ForDebugBuild_RejectsMissingShortSha(string baseVersion, string shortSha)
        {
            Assert.That(() => VersionName.ForDebugBuild(baseVersion, shortSha), Throws.ArgumentException);
        }
    }
}
