using System;
using Doggiehood.Core.Versioning;
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
    /// x86_64 ABI, the `.emulator` applicationId suffix, the emulator-safe
    /// graphics profile and the disabled audio engine (#705) are applied only when
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
        private bool _originalDisableUnityAudio;

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
            _originalDisableUnityAudio = EmulatorBuildProcessor.GetDisableUnityAudio();
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
            EmulatorBuildProcessor.SetDisableUnityAudio(_originalDisableUnityAudio);
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
            EmulatorBuildProcessor.SetDisableUnityAudio(false);
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
        public void ApplyIfRequested_DisablesUnityAudio_WhenEmulatorBuildRequested()
        {
            // #705: the reporter's per-thread profile showed `FMOD stream thr`
            // and `AudioTrack` — Unity's audio engine — pinned at ~100% CPU
            // while the render thread slept. The game ships no audio assets at
            // all, so the emulator variant gives the whole subsystem up rather
            // than let it spin against a virtual audio HAL.
            //
            // That trace came from the DEVICE APK, not an emulator build
            // (#706/#707) — v0.14.0's `-emulator.apk` was a byte-for-byte copy
            // of the ARM64 device APK — so the mitigation stays unconfirmed
            // until a verified emulator build is profiled.
            SeedDeviceDefaults();
            RequestEmulatorBuild();

            EmulatorBuildProcessor.ApplyIfRequested();

            Assert.That(EmulatorBuildProcessor.GetDisableUnityAudio(), Is.True);
        }

        [Test]
        public void DisableUnityAudioAccessor_ResolvesTheSerializedProperty_SoTheSettingCannotBeSilentlyIgnored()
        {
            // Rule #6 guard: `m_DisableAudio` on the AudioManager singleton is
            // written through serialized data (no public API). If the key name
            // or singleton name ever stops resolving, the accessor degrades to a
            // no-op and the emulator APK would ship with audio still on — this
            // asserts the round trip actually takes.
            EmulatorBuildProcessor.SetDisableUnityAudio(true);
            Assert.That(EmulatorBuildProcessor.GetDisableUnityAudio(), Is.True);

            EmulatorBuildProcessor.SetDisableUnityAudio(false);
            Assert.That(EmulatorBuildProcessor.GetDisableUnityAudio(), Is.False);
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
            Assert.That(EmulatorBuildProcessor.GetDisableUnityAudio(), Is.False,
                "The device build keeps Unity audio on — only the emulator variant gives it up.");
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
            Assert.That(EmulatorBuildProcessor.GetDisableUnityAudio(), Is.False);
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
        public void ApplyIfRequested_AppliesTheWholeProfile_WhenRequestedOnTheCommandLineAlone()
        {
            // #731: game-ci/unity-builder runs Unity inside a Docker container
            // and forwards only a fixed allowlist of environment variables, so
            // DOGGIEHOOD_EMULATOR_BUILD set on the workflow step never reaches
            // the editor — v0.15.0 shipped no APKs because the gate correctly
            // caught the resulting second device build. The command line
            // (unity-builder's `customParameters`) is the channel that does
            // arrive, and it must be sufficient on its own, with the
            // environment variable unset.
            SeedDeviceDefaults();
            RequestDeviceBuild();

            EmulatorBuildProcessor.ApplyIfRequested(
                envValue: null,
                commandLineArgs: new[] { "unity-editor", EmulatorBuildProfile.CommandLineFlag });

            Assert.That(PlayerSettings.Android.targetArchitectures, Is.EqualTo(AndroidArchitecture.X86_64));
            Assert.That(
                PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Android),
                Is.EqualTo("com.derekwinters.doggiehood.emulator"));
            Assert.That(PlayerSettings.GetUseDefaultGraphicsAPIs(BuildTarget.Android), Is.False);
            Assert.That(
                PlayerSettings.GetGraphicsAPIs(BuildTarget.Android),
                Is.EqualTo(new[] { GraphicsDeviceType.Vulkan, GraphicsDeviceType.OpenGLES3 }));
            Assert.That(PlayerSettings.GetMobileMTRendering(NamedBuildTarget.Android), Is.False);
            Assert.That(EmulatorBuildProcessor.GetColorGamuts(), Is.EqualTo(new[] { ColorGamut.sRGB }));
            Assert.That(EmulatorBuildProcessor.GetDisableUnityAudio(), Is.True);
        }

        [Test]
        public void RestoreIfApplied_PutsEveryMutatedSettingBack_AfterACommandLineRequestedEmulatorBuild()
        {
            SeedDeviceDefaults();
            var deviceGraphicsApis = PlayerSettings.GetGraphicsAPIs(BuildTarget.Android);
            RequestDeviceBuild();

            EmulatorBuildProcessor.ApplyIfRequested(
                envValue: null,
                commandLineArgs: new[] { "unity-editor", EmulatorBuildProfile.CommandLineFlag });
            EmulatorBuildProcessor.RestoreIfApplied();

            Assert.That(PlayerSettings.Android.targetArchitectures, Is.EqualTo(AndroidArchitecture.ARM64));
            Assert.That(
                PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Android),
                Is.EqualTo(PermanentApplicationId));
            Assert.That(PlayerSettings.GetUseDefaultGraphicsAPIs(BuildTarget.Android), Is.True);
            Assert.That(PlayerSettings.GetGraphicsAPIs(BuildTarget.Android), Is.EqualTo(deviceGraphicsApis));
            Assert.That(PlayerSettings.GetMobileMTRendering(NamedBuildTarget.Android), Is.True);
            Assert.That(EmulatorBuildProcessor.GetColorGamuts(), Is.EqualTo(new[] { ColorGamut.sRGB }));
            Assert.That(EmulatorBuildProcessor.GetDisableUnityAudio(), Is.False);
        }

        [Test]
        public void ApplyIfRequested_LeavesEveryDeviceSettingUntouched_WhenNeitherChannelRequestsIt()
        {
            // The device build passes no switch and sets no variable — the
            // invariant that the emulator profile never changes what the
            // device APK ships now has to hold across both channels.
            SeedDeviceDefaults();
            var deviceGraphicsApis = PlayerSettings.GetGraphicsAPIs(BuildTarget.Android);
            RequestDeviceBuild();

            EmulatorBuildProcessor.ApplyIfRequested(
                envValue: null,
                commandLineArgs: new[] { "unity-editor", "-batchmode", "-buildTarget", "Android" });

            Assert.That(PlayerSettings.Android.targetArchitectures, Is.EqualTo(AndroidArchitecture.ARM64));
            Assert.That(
                PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Android),
                Is.EqualTo(PermanentApplicationId));
            Assert.That(PlayerSettings.GetUseDefaultGraphicsAPIs(BuildTarget.Android), Is.True);
            Assert.That(PlayerSettings.GetGraphicsAPIs(BuildTarget.Android), Is.EqualTo(deviceGraphicsApis));
            Assert.That(PlayerSettings.GetMobileMTRendering(NamedBuildTarget.Android), Is.True);
            Assert.That(EmulatorBuildProcessor.GetColorGamuts(), Is.EqualTo(new[] { ColorGamut.sRGB }));
            Assert.That(EmulatorBuildProcessor.GetDisableUnityAudio(), Is.False);
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
