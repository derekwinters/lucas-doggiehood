using System;
using Doggiehood.Unity.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;
using UnityEngine.Rendering;

namespace Doggiehood.Unity.EditModeTests
{
    /// <summary>
    /// Covers the emulator-targeted build variant's editor hook (#648): the
    /// x86_64 ABI, the `.emulator` applicationId suffix and the emulator-safe
    /// graphics profile are applied only when
    /// <c>DOGGIEHOOD_EMULATOR_BUILD</c> is truthy, and every mutated
    /// PlayerSettings field is put back afterwards. PlayerSettings and the
    /// environment variable are both process-global, so every test saves and
    /// restores them (same pattern as
    /// <see cref="DebugApplicationIdBuildProcessorTests"/>).
    /// </summary>
    public class EmulatorBuildProcessorTests
    {
        private const string PermanentApplicationId = "com.derekwinters.doggiehood";
        private const string ProjectSettingsPath = "ProjectSettings/ProjectSettings.asset";
        private const string CommittedDeviceArchitecturesEntry = "AndroidTargetArchitectures: 2";

        private string _originalApplicationId;
        private string _originalEnvValue;
        private AndroidArchitecture _originalArchitectures;
        private bool _originalUseDefaultGraphicsApis;
        private GraphicsDeviceType[] _originalGraphicsApis;
        private bool _originalMultithreadedRendering;
        private ColorGamut[] _originalColorGamuts;

        [SetUp]
        public void SaveGlobalState()
        {
            _originalApplicationId = PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Android);
            _originalEnvValue = Environment.GetEnvironmentVariable(EmulatorBuildProcessor.EmulatorBuildEnvironmentVariable);
            _originalArchitectures = PlayerSettings.Android.targetArchitectures;
            _originalUseDefaultGraphicsApis = PlayerSettings.GetUseDefaultGraphicsAPIs(BuildTarget.Android);
            _originalGraphicsApis = PlayerSettings.GetGraphicsAPIs(BuildTarget.Android);
            _originalMultithreadedRendering = PlayerSettings.GetMobileMTRendering(NamedBuildTarget.Android);
            _originalColorGamuts = EmulatorBuildProcessor.GetColorGamuts();
        }

        [TearDown]
        public void RestoreGlobalState()
        {
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, _originalApplicationId);
            Environment.SetEnvironmentVariable(EmulatorBuildProcessor.EmulatorBuildEnvironmentVariable, _originalEnvValue);
            PlayerSettings.Android.targetArchitectures = _originalArchitectures;
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, _originalGraphicsApis);
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, _originalUseDefaultGraphicsApis);
            PlayerSettings.SetMobileMTRendering(NamedBuildTarget.Android, _originalMultithreadedRendering);
            EmulatorBuildProcessor.SetColorGamuts(_originalColorGamuts);
        }

        private static void RequestEmulatorBuild()
        {
            Environment.SetEnvironmentVariable(EmulatorBuildProcessor.EmulatorBuildEnvironmentVariable, "true");
        }

        private static void RequestDeviceBuild()
        {
            Environment.SetEnvironmentVariable(EmulatorBuildProcessor.EmulatorBuildEnvironmentVariable, null);
        }

        private static void SeedDeviceDefaults()
        {
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, PermanentApplicationId);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, true);
            PlayerSettings.SetMobileMTRendering(NamedBuildTarget.Android, true);
            EmulatorBuildProcessor.SetColorGamuts(new[] { ColorGamut.sRGB });
        }

        [Test]
        public void ApplyIfRequested_TargetsX86_64Only_WhenEmulatorBuildRequested()
        {
            SeedDeviceDefaults();
            RequestEmulatorBuild();

            EmulatorBuildProcessor.ApplyIfRequested();

            Assert.That(PlayerSettings.Android.targetArchitectures, Is.EqualTo(AndroidArchitecture.X86_64));
        }

        [Test]
        public void ApplyIfRequested_AppendsEmulatorSuffix_WhenEmulatorBuildRequested()
        {
            SeedDeviceDefaults();
            RequestEmulatorBuild();

            EmulatorBuildProcessor.ApplyIfRequested();

            Assert.That(
                PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Android),
                Is.EqualTo("com.derekwinters.doggiehood.emulator"));
        }

        [Test]
        public void ApplyIfRequested_PinsTheEmulatorGraphicsApiOrder_WhenEmulatorBuildRequested()
        {
            SeedDeviceDefaults();
            RequestEmulatorBuild();

            EmulatorBuildProcessor.ApplyIfRequested();

            Assert.That(PlayerSettings.GetUseDefaultGraphicsAPIs(BuildTarget.Android), Is.False,
                "Auto Graphics API must be off, or the pinned order is ignored.");
            Assert.That(
                PlayerSettings.GetGraphicsAPIs(BuildTarget.Android),
                Is.EqualTo(new[] { GraphicsDeviceType.Vulkan, GraphicsDeviceType.OpenGLES3 }));
        }

        [Test]
        public void ApplyIfRequested_DisablesMultithreadedRendering_WhenEmulatorBuildRequested()
        {
            SeedDeviceDefaults();
            RequestEmulatorBuild();

            EmulatorBuildProcessor.ApplyIfRequested();

            Assert.That(PlayerSettings.GetMobileMTRendering(NamedBuildTarget.Android), Is.False);
        }

        [Test]
        public void ApplyIfRequested_DisablesWideColorGamut_WhenEmulatorBuildRequested()
        {
            SeedDeviceDefaults();
            EmulatorBuildProcessor.SetColorGamuts(new[] { ColorGamut.sRGB, ColorGamut.DisplayP3 });
            RequestEmulatorBuild();

            EmulatorBuildProcessor.ApplyIfRequested();

            Assert.That(EmulatorBuildProcessor.GetColorGamuts(), Is.EqualTo(new[] { ColorGamut.sRGB }));
        }

        [Test]
        public void ApplyIfRequested_LeavesEveryDeviceSettingUntouched_WhenEnvironmentVariableUnset()
        {
            SeedDeviceDefaults();
            var deviceGraphicsApis = PlayerSettings.GetGraphicsAPIs(BuildTarget.Android);
            RequestDeviceBuild();

            EmulatorBuildProcessor.ApplyIfRequested();

            Assert.That(PlayerSettings.Android.targetArchitectures, Is.EqualTo(AndroidArchitecture.ARM64));
            Assert.That(
                PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Android),
                Is.EqualTo(PermanentApplicationId));
            Assert.That(PlayerSettings.GetUseDefaultGraphicsAPIs(BuildTarget.Android), Is.True);
            Assert.That(PlayerSettings.GetGraphicsAPIs(BuildTarget.Android), Is.EqualTo(deviceGraphicsApis));
            Assert.That(PlayerSettings.GetMobileMTRendering(NamedBuildTarget.Android), Is.True);
            Assert.That(EmulatorBuildProcessor.GetColorGamuts(), Is.EqualTo(new[] { ColorGamut.sRGB }));
        }

        [Test]
        public void RestoreIfApplied_PutsEveryMutatedSettingBack_AfterAnEmulatorBuild()
        {
            SeedDeviceDefaults();
            var deviceGraphicsApis = PlayerSettings.GetGraphicsAPIs(BuildTarget.Android);
            RequestEmulatorBuild();

            EmulatorBuildProcessor.ApplyIfRequested();
            EmulatorBuildProcessor.RestoreIfApplied();

            Assert.That(PlayerSettings.Android.targetArchitectures, Is.EqualTo(AndroidArchitecture.ARM64));
            Assert.That(
                PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Android),
                Is.EqualTo(PermanentApplicationId));
            Assert.That(PlayerSettings.GetUseDefaultGraphicsAPIs(BuildTarget.Android), Is.True);
            Assert.That(PlayerSettings.GetGraphicsAPIs(BuildTarget.Android), Is.EqualTo(deviceGraphicsApis));
            Assert.That(PlayerSettings.GetMobileMTRendering(NamedBuildTarget.Android), Is.True);
            Assert.That(EmulatorBuildProcessor.GetColorGamuts(), Is.EqualTo(new[] { ColorGamut.sRGB }));
        }

        [Test]
        public void RestoreIfApplied_IsANoOp_WhenNoEmulatorProfileWasApplied()
        {
            SeedDeviceDefaults();
            RequestDeviceBuild();

            EmulatorBuildProcessor.ApplyIfRequested();
            EmulatorBuildProcessor.RestoreIfApplied();

            Assert.That(PlayerSettings.Android.targetArchitectures, Is.EqualTo(AndroidArchitecture.ARM64));
            Assert.That(
                PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Android),
                Is.EqualTo(PermanentApplicationId));
        }

        [Test]
        public void CommittedProjectSettings_StillTargetArm64Only_SoTheDeviceBuildIsUnaffected()
        {
            // Design A (#648): the x86_64 ABI lives only in the build hook, never
            // in the committed defaults. If this entry ever changes, the normal
            // release APK silently stopped being ARM64-only.
            var yaml = System.IO.File.ReadAllText(ProjectSettingsPath);

            Assert.That(yaml, Does.Contain(CommittedDeviceArchitecturesEntry));
        }
    }
}
