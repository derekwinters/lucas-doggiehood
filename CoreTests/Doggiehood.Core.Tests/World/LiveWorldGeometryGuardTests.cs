using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace Doggiehood.Core.Tests.World
{
    /// <summary>
    /// #677 — THE ANTI-REGRESSION GUARD. <b>Deleting or weakening this test
    /// reopens the bug it exists to prevent.</b>
    ///
    /// Dogs wander the whole unlocked map (#398) and houses are built on unlocked
    /// tiles (#453), so any runtime question about world geometry has exactly one
    /// correct source: the live <c>GameState</c> (<c>GameState.WalkNetwork</c> /
    /// <c>GameState.GetHouseLot</c>). The origin-tile-only
    /// <c>NeighborhoodLayout</c> singleton describes the starting intersection
    /// alone — it cannot see an unlocked tile's sidewalks and it throws for a
    /// player-built house id. The origin -> live migration was done at call site
    /// after call site (#430/#461/#509/#455) and <c>QuestDirector</c>'s two
    /// (the walk-home route and the rest approach) were missed, which is what put
    /// a dog "home" on a sidewalk in the middle of the street.
    ///
    /// It is a source scan rather than a behavioural test on purpose: what it
    /// protects is "no future code reads the origin singleton here", which no
    /// amount of exercising today's code can assert. Same style as
    /// <c>InputAuthorityGuardTests</c> / <c>WorldDimensionsGuardTests</c>.
    ///
    /// If you are here because this test failed: read the live network/lot from
    /// the <c>GameState</c> the director already holds, rather than adding your
    /// file to an allow-list.
    /// </summary>
    public class LiveWorldGeometryGuardTests
    {
        /// <summary>The runtime directors whose world-geometry reads must resolve
        /// against live state. (World BUILD code legitimately still has
        /// starting-layout overloads for isolated call sites/tests, so it is not
        /// in scope here — a director asking "where is home, and what can this dog
        /// walk on right now?" is.)</summary>
        private static readonly string[] LiveStateOnlyFiles = { "QuestDirector.cs" };

        /// <summary>Any read of the origin-only singleton's geometry.</summary>
        private static readonly Regex OriginSingletonRead =
            new Regex(@"(?<!\w)NeighborhoodLayout\s*\.");

        [Test]
        public void TheQuestDirectorResolvesWorldGeometryFromLiveState_NotTheOriginSingleton()
        {
            foreach (var fileName in LiveStateOnlyFiles)
            {
                var path = Path.Combine(UnityScriptsRoot(), fileName);
                Assert.That(File.Exists(path), $"expected {fileName} at {path}");

                var code = StripComments(File.ReadAllText(path));
                Assert.That(OriginSingletonRead.IsMatch(code), Is.False,
                    $"{fileName} must read world geometry from the live GameState (State.WalkNetwork / "
                    + "State.GetHouseLot), never the origin-tile-only NeighborhoodLayout singleton — a dog "
                    + "off the starting tile, or living in a player-built house, is invisible to it");
            }
        }

        private static string UnityScriptsRoot()
        {
            var root = Path.Combine(RepoRoot(), "Assets", "Scripts", "Unity");
            Assert.That(Directory.Exists(root), $"expected the Unity script folder at {root}");
            return root;
        }

        private static string RepoRoot([CallerFilePath] string thisFile = null)
            => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(thisFile), "..", "..", ".."));

        /// <summary>Drops line and block comments so a prose mention of the
        /// forbidden name (this project documents heavily) can't fail the scan —
        /// only real code counts.</summary>
        private static string StripComments(string code)
        {
            var withoutBlocks = Regex.Replace(code, @"/\*.*?\*/", string.Empty, RegexOptions.Singleline);
            return Regex.Replace(withoutBlocks, @"//.*?$", string.Empty, RegexOptions.Multiline);
        }
    }
}
