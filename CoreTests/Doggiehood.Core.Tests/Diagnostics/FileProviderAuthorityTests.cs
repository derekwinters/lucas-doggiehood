using System;
using Doggiehood.Core.Diagnostics;
using Doggiehood.Core.Versioning;
using NUnit.Framework;

namespace Doggiehood.Core.Tests.Diagnostics
{
    /// <summary>
    /// #695: the Android <c>FileProvider</c> authority a shared bug report's
    /// <c>content://</c> URI is issued under.
    ///
    /// <para>This is the one string in the feature that fails <b>at runtime on
    /// the device</b> when it is wrong — the test suite cannot see a mismatched
    /// authority, the share sheet just refuses the file. So the rule is stated
    /// once here, in engine-free Core, and both ends read it: the manifest via
    /// <see cref="FileProviderAuthority.ManifestAuthority"/>'s Gradle
    /// <c>${applicationId}</c> placeholder, and the runtime via
    /// <see cref="FileProviderAuthority.For"/> on the live application id.</para>
    ///
    /// <para>Deriving it (rather than hard-coding
    /// <c>com.derekwinters.doggiehood.fileprovider</c>) is what keeps the
    /// side-by-side <c>.debug</c> build working — see
    /// <see cref="ApplicationIdSuffix"/> (#80) and #734.</para>
    /// </summary>
    public class FileProviderAuthorityTests
    {
        private const string ReleaseApplicationId = "com.derekwinters.doggiehood";

        [Test]
        public void TheAuthority_IsTheApplicationIdPlusTheSharedSuffix()
        {
            Assert.That(FileProviderAuthority.For(ReleaseApplicationId),
                Is.EqualTo("com.derekwinters.doggiehood.fileprovider"));
        }

        [Test]
        public void ADebugBuildsAuthority_CarriesTheDebugSuffixToo_SoBothCanBeInstalled()
        {
            // #80/#734: two apps cannot share an authority string, so the
            // side-by-side .debug build must get its own — which it does for free
            // as long as the authority is derived rather than typed.
            var debugId = ApplicationIdSuffix.Apply(ReleaseApplicationId, isDebugBuild: true);

            Assert.That(FileProviderAuthority.For(debugId),
                Is.EqualTo("com.derekwinters.doggiehood.debug.fileprovider"));
            Assert.That(FileProviderAuthority.For(debugId),
                Is.Not.EqualTo(FileProviderAuthority.For(ReleaseApplicationId)));
        }

        [Test]
        public void TheManifestAuthority_UsesGradlesApplicationIdPlaceholder_NotALiteralId()
        {
            // The manifest cannot know which build it is being merged into, so it
            // defers to Gradle's placeholder — the same derivation, one build step
            // earlier.
            Assert.That(FileProviderAuthority.ManifestAuthority,
                Is.EqualTo("${applicationId}" + FileProviderAuthority.Suffix));
            Assert.That(FileProviderAuthority.ManifestAuthority, Does.Not.Contain(ReleaseApplicationId),
                "no build's application id is baked into the manifest");
        }

        [Test]
        public void ApplyingTheSuffixTwice_IsANoOp()
        {
            var once = FileProviderAuthority.For(ReleaseApplicationId);

            Assert.That(FileProviderAuthority.For(once), Is.EqualTo(once));
        }

        [Test]
        public void AMissingApplicationId_Throws_RatherThanShippingABareAuthority()
        {
            Assert.Throws<ArgumentException>(() => FileProviderAuthority.For(null));
            Assert.Throws<ArgumentException>(() => FileProviderAuthority.For("   "));
        }
    }
}
