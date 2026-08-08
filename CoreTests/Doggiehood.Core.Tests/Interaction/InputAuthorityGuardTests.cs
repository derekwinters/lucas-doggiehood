using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace Doggiehood.Core.Tests.Interaction
{
    /// <summary>
    /// #670 — THE ANTI-REGRESSION GUARD. <b>Deleting or weakening this test
    /// reopens the bug it exists to prevent.</b>
    ///
    /// The defect was never "the modal gate is broken" — the gate worked, and
    /// all seven overlays registered with it. The defect was that blocking was
    /// a property individual consumers <em>opted into</em>, so every input path
    /// added since silently opted out: touch drag, mouse drag, pinch, twist and
    /// scroll all ran from raw polling straight to the camera without ever
    /// asking whether a dialog was open. Nothing failed when they were added.
    /// That is what this test changes: a consumer that reaches raw input around
    /// the single router, or drives a camera gesture directly, now breaks the
    /// build instead of quietly shipping.
    ///
    /// It is a source scan rather than a behavioural test on purpose — the
    /// behaviour it protects is "no future code does X", which no amount of
    /// exercising today's code can assert. It mirrors the source-scanning style
    /// of <c>WorldDimensionsGuardTests</c> and <c>VersionFileGuardTests</c>.
    ///
    /// See <c>docs/engineering/input-authority.md</c>. If you are here because
    /// this test failed: don't add your file to the allow-list — register your
    /// consumer with <c>InputAuthority</c> instead.
    /// </summary>
    public class InputAuthorityGuardTests
    {
        /// <summary>The one file allowed to touch <c>UnityEngine.Input</c>
        /// (R1: a single entry point). Everything else receives input by
        /// registering with the authority.</summary>
        private static readonly string[] RawInputAllowList = { "InputRouter.cs" };

        /// <summary>The camera's gesture entry points. Only the camera's own
        /// file may invoke these; anything else driving them is a second,
        /// ungated path to the camera — exactly what #670 removed.</summary>
        private static readonly string[] CameraGestureEntryPoints =
        {
            "HandleDrag", "HandlePinch", "HandleTwist", "HandleTap", "ProcessTwoFingerSample",
        };

        private const string CameraFileName = "CameraRig.cs";

        // "Input." as a raw-input read, but not the tail of an identifier
        // (ModalInputGate.) and not our own Core types (InputAuthority.Shared,
        // InputGesture.Pan) whose next character is never a dot.
        private static readonly Regex RawInputRead = new Regex(@"(?<!\w)Input\s*\.");

        [Test]
        public void OnlyTheRouterReadsRawInput()
        {
            var offenders = new List<string>();

            foreach (var file in UnityScriptFiles())
            {
                if (RawInputAllowList.Contains(Path.GetFileName(file)))
                {
                    continue;
                }

                var code = StripComments(File.ReadAllText(file));
                if (RawInputRead.IsMatch(code))
                {
                    offenders.Add(Path.GetFileName(file));
                }
            }

            Assert.That(offenders, Is.Empty,
                "raw UnityEngine.Input must be read in exactly one place — the input router — so every "
                + "gesture passes the InputAuthority's priority check. These files bypass it: "
                + string.Join(", ", offenders));
        }

        [Test]
        public void OnlyTheCameraDrivesCameraGestures()
        {
            var offenders = new List<string>();

            foreach (var file in UnityScriptFiles())
            {
                var name = Path.GetFileName(file);
                if (name == CameraFileName)
                {
                    continue;
                }

                var code = StripComments(File.ReadAllText(file));
                foreach (var entryPoint in CameraGestureEntryPoints)
                {
                    if (Regex.IsMatch(code, @"(?<!\w)" + entryPoint + @"\s*\("))
                    {
                        offenders.Add($"{name} calls {entryPoint}");
                    }
                }
            }

            Assert.That(offenders, Is.Empty,
                "a camera gesture may only be driven from the camera's own authority-registered consumer; "
                + "these drive it directly: " + string.Join(", ", offenders));
        }

        [Test]
        public void TheRouterHandsItsGesturesToTheAuthority()
        {
            // Stops the first guard being satisfied by simply renaming whichever
            // file polls input to InputRouter.cs: the allow-listed file has to
            // actually go through InputAuthority.
            var router = UnityScriptFiles()
                .FirstOrDefault(f => Path.GetFileName(f) == RawInputAllowList[0]);

            Assert.That(router, Is.Not.Null,
                $"the single input entry point {RawInputAllowList[0]} must exist");
            Assert.That(File.ReadAllText(router), Does.Contain("InputAuthority"),
                "the input router must hand every gesture to the InputAuthority, not act on it itself");
        }

        [Test]
        public void TheCameraReachesInputOnlyAsARegisteredConsumer()
        {
            // R1: the camera is an ordinary consumer, offered a gesture only
            // after every tier above it has declined. If it ever stops
            // registering, it has gone back to being a privileged path.
            var camera = UnityScriptFiles().FirstOrDefault(f => Path.GetFileName(f) == CameraFileName);

            Assert.That(camera, Is.Not.Null);
            var code = File.ReadAllText(camera);
            Assert.That(code, Does.Contain("InputTier.Camera"),
                "the camera must declare itself as the lowest-priority input tier");
            Assert.That(code, Does.Contain("Register"),
                "the camera must register with the InputAuthority to receive anything");
        }

        private static IEnumerable<string> UnityScriptFiles()
            => Directory.EnumerateFiles(UnityScriptsRoot(), "*.cs", SearchOption.AllDirectories)
                .OrderBy(path => path, StringComparer.Ordinal);

        private static string UnityScriptsRoot()
        {
            var root = Path.Combine(RepoRoot(), "Assets", "Scripts", "Unity");
            Assert.That(Directory.Exists(root), $"expected the Unity script folder at {root}");
            return root;
        }

        private static string RepoRoot([CallerFilePath] string thisFile = null)
            => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(thisFile), "..", "..", ".."));

        /// <summary>Drops line and block comments so a prose mention of a
        /// forbidden name (this project documents heavily) can't fail the scan
        /// — only real code counts.</summary>
        private static string StripComments(string code)
        {
            var withoutBlocks = Regex.Replace(code, @"/\*.*?\*/", string.Empty, RegexOptions.Singleline);
            return Regex.Replace(withoutBlocks, @"//.*?$", string.Empty, RegexOptions.Multiline);
        }
    }
}
