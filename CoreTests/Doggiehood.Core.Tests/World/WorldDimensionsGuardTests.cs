using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Doggiehood.Core.World;
using NUnit.Framework;

namespace Doggiehood.Core.Tests.World
{
    /// <summary>
    /// Guard (#105): the 7 locked <see cref="WorldDimensions"/> values must
    /// be declared exactly once, in WorldDimensions.cs.
    ///
    /// This can't be a reflection-over-compiled-values scan like
    /// <see cref="NoPlayerCharacterTests"/> / <see cref="LightingPresetTests"/>'s
    /// <c>CoreContainsNoTimeOfDayOrWeatherTypes</c>: C# <c>const</c> fields
    /// are inlined at compile time, so a legitimate alias such as
    /// <c>NeighborhoodLayout.StreetWidth = WorldDimensions.RoadWidth</c>
    /// becomes reflectively indistinguishable from a real duplicate literal
    /// <c>6f</c> — both just report the value 6. Nor can it be a whole-Core
    /// scan for these 7 numbers: Core already has coincidentally
    /// same-valued constants that are semantically unrelated (e.g.
    /// <c>CameraController.MinZoom = 6f</c> is a zoom level, not a road
    /// width; <c>WanderBehavior.StepLength = 3f</c> is a movement step, not
    /// a crosswalk width) and those would false-positive.
    ///
    /// So this scans *source text*: every <c>.cs</c> file in
    /// <c>Assets/Scripts/Core/World/</c> (found relative to this test
    /// file's own path via <see cref="CallerFilePathAttribute"/>, since the
    /// World folder is exactly 3 levels above CoreTests) except
    /// WorldDimensions.cs itself, looking for a
    /// <c>float &lt;Name&gt; = &lt;number&gt;f</c> field-declaration shape
    /// whose number matches one of the 7 locked values. Scoping the scan to
    /// the World folder — rather than all of Core — is what avoids the
    /// CameraController/WanderBehavior false positives: those live in
    /// Cameras/ and Dogs/ respectively, domains this guard doesn't touch.
    /// </summary>
    public class WorldDimensionsGuardTests
    {
        // GrassVergeWidth is back in the locked list: it was intentionally
        // absent while Derek's first 2026-07-13 decision put it at 0m (a
        // locked 0 would false-positive every ordinary `float x = 0f`
        // initializer), but his same-day midpoint follow-up made it 0.75m
        // — a real dimension again, so the duplicate-literal guard applies.
        private static readonly float[] LockedValues =
        {
            WorldDimensions.TileSize,
            WorldDimensions.RoadWidth,
            WorldDimensions.GrassVergeWidth,
            WorldDimensions.SidewalkWidth,
            WorldDimensions.CrosswalkWidth,
            WorldDimensions.CulDeSacBulbRadius,
            WorldDimensions.OpposingTurnArchRadius,
        };

        // Matches "float SomeName = 6f" / "float SomeName = 1.5f" style
        // field-declaration initializers, wherever they sit on the line
        // (const/readonly/access modifiers, if present, come before
        // "float" and aren't required by the pattern).
        private static readonly Regex FloatFieldDeclaration =
            new Regex(@"float\s+\w+\s*=\s*(\d+(?:\.\d+)?)f\b", RegexOptions.Compiled);

        private static string GetCoreWorldSourceDirectory([CallerFilePath] string thisFilePath = null)
        {
            // thisFilePath: .../CoreTests/Doggiehood.Core.Tests/World/WorldDimensionsGuardTests.cs
            // Three levels up from its directory is the repo root.
            var testFileDirectory = Path.GetDirectoryName(thisFilePath);
            var repoRoot = Path.GetFullPath(Path.Combine(testFileDirectory, "..", "..", ".."));
            return Path.Combine(repoRoot, "Assets", "Scripts", "Core", "World");
        }

        [Test]
        public void CoreWorldFolder_DeclaresEachLockedDimensionExactlyOnceInWorldDimensions()
        {
            var worldSourceDirectory = GetCoreWorldSourceDirectory();
            Assert.That(Directory.Exists(worldSourceDirectory), Is.True,
                $"Expected to find Core World source folder at {worldSourceDirectory}");

            var offenders = new List<string>();

            foreach (var file in Directory.GetFiles(worldSourceDirectory, "*.cs"))
            {
                if (string.Equals(Path.GetFileName(file), "WorldDimensions.cs", StringComparison.Ordinal))
                {
                    continue;
                }

                var sourceText = File.ReadAllText(file);

                foreach (Match match in FloatFieldDeclaration.Matches(sourceText))
                {
                    var declaredValue = float.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);

                    if (LockedValues.Any(locked => Math.Abs(locked - declaredValue) < 0.0001f))
                    {
                        offenders.Add($"{Path.GetFileName(file)}: '{match.Value}'");
                    }
                }
            }

            Assert.That(offenders, Is.Empty,
                "World dimension values must be defined exactly once, in WorldDimensions.cs (#105); "
                    + "found duplicate literal declaration(s): " + string.Join(", ", offenders));
        }
    }
}
