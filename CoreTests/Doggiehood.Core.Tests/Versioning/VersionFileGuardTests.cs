using System.IO;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace Doggiehood.Core.Tests.Versioning
{
    /// <summary>
    /// Guard (#114): the repo-root <c>VERSION</c> file and
    /// <c>.github/release-please/manifest.json</c> must never drift apart.
    ///
    /// #114 happened because release-please's generic <c>extra-files</c>
    /// updater only rewrites a plain-text file when it contains an
    /// <c>x-release-please-version</c> marker on the line with the version
    /// (see release-please's <c>src/updaters/generic.ts</c>: it scans line
    /// by line for that marker text and only then substitutes a
    /// semver-looking substring on that line). <c>VERSION</c> used to be a
    /// bare <c>0.1.0</c> with no marker, so release-please's own bookkeeping
    /// in <c>manifest.json</c> advanced to <c>0.2.0</c> across a real release
    /// while the generic updater silently left <c>VERSION</c> on
    /// <c>0.1.0</c> forever, since it never found a line to replace.
    ///
    /// This can't be caught by exercising release-please itself here (that
    /// requires actually running the next release PR in CI, which can't be
    /// verified synchronously). What this test *can* do is fail loudly, the
    /// next time anyone runs the Core suite, if the two files are ever
    /// inconsistent again — whether from the same bug recurring or a manual
    /// slip. It mirrors the source-scanning style of
    /// <see cref="World.WorldDimensionsGuardTests"/>: read the real repo
    /// files as text (located relative to this test file via
    /// <see cref="CallerFilePathAttribute"/>) rather than trying to invoke
    /// release-please.
    /// </summary>
    public class VersionFileGuardTests
    {
        // VERSION's content is the bare version, optionally followed by the
        // "# x-release-please-version" marker release-please's generic
        // updater needs in order to find and rewrite the line (#114).
        private static readonly Regex VersionFilePattern =
            new Regex(@"^(?<version>\d+\.\d+\.\d+(?:-[0-9A-Za-z-.]+)?(?:\+[0-9A-Za-z-.]+)?)(\s*#.*)?\s*$",
                RegexOptions.Compiled);

        private static readonly Regex ManifestRootVersionPattern =
            new Regex("\"\\.\"\\s*:\\s*\"(?<version>[^\"]+)\"", RegexOptions.Compiled);

        private static string GetRepoRoot([CallerFilePath] string thisFilePath = null)
        {
            // thisFilePath: .../CoreTests/Doggiehood.Core.Tests/Versioning/VersionFileGuardTests.cs
            // Three levels up from its directory is the repo root.
            var testFileDirectory = Path.GetDirectoryName(thisFilePath);
            return Path.GetFullPath(Path.Combine(testFileDirectory, "..", "..", ".."));
        }

        private static string ReadVersionFileVersion()
        {
            var versionFilePath = Path.Combine(GetRepoRoot(), "VERSION");
            Assert.That(File.Exists(versionFilePath), Is.True, $"Expected to find {versionFilePath}");

            var content = File.ReadAllText(versionFilePath).Trim();
            var match = VersionFilePattern.Match(content);
            Assert.That(match.Success, Is.True,
                $"VERSION must contain a bare semver (optionally with a trailing '# x-release-please-version' "
                    + $"marker comment release-please's generic updater needs to find it); found: '{content}'");

            return match.Groups["version"].Value;
        }

        private static string ReadManifestTrackedVersion()
        {
            var manifestPath = Path.Combine(GetRepoRoot(), ".github", "release-please", "manifest.json");
            Assert.That(File.Exists(manifestPath), Is.True, $"Expected to find {manifestPath}");

            var content = File.ReadAllText(manifestPath);
            var match = ManifestRootVersionPattern.Match(content);
            Assert.That(match.Success, Is.True,
                $"Expected a root package (\".\") entry in {manifestPath}");

            return match.Groups["version"].Value;
        }

        [Test]
        public void VersionFile_ContainsABareValidSemver()
        {
            Assert.That(ReadVersionFileVersion(), Does.Match(@"^\d+\.\d+\.\d+"));
        }

        [Test]
        public void VersionFile_MatchesReleasePleaseManifestTrackedVersion()
        {
            Assert.That(ReadVersionFileVersion(), Is.EqualTo(ReadManifestTrackedVersion()),
                "VERSION and .github/release-please/manifest.json's tracked version have drifted apart (#114). "
                    + "manifest.json is release-please's source of truth for 'what was actually last released' — "
                    + "VERSION should be corrected to match it.");
        }
    }
}
