using System;
using Doggiehood.Core.Versioning;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.Rendering;

namespace Doggiehood.Unity.Editor
{
    /// <summary>
    /// Turns a normal Android build into the emulator-targeted variant (#648)
    /// when <see cref="EmulatorBuildEnvironmentVariable"/> is truthy ("1" or
    /// "true", case-insensitive) — the x86_64 ABI, the `.emulator`
    /// applicationId suffix, an emulator-safe graphics profile and a disabled
    /// audio engine (#705, the game ships no audio assets yet). Nothing
    /// here is committed to <c>ProjectSettings.asset</c>: under Design A the
    /// device/release build stays ARM64-only with its default graphics
    /// config, and only a run that opts in via the environment variable sees
    /// any of these values. Every mutated setting is put back in
    /// <see cref="RestoreIfApplied"/> so repeated local/CI builds in the same
    /// editor session don't inherit the emulator profile.
    ///
    /// Runs after <see cref="DebugApplicationIdBuildProcessor"/> (callback
    /// order 1 vs 0). The release workflow never sets both variables in one
    /// run; if it ever did, the two capture-and-restore hooks would need to
    /// be merged rather than stacked.
    /// </summary>
    public class EmulatorBuildProcessor : IPreprocessBuildWithReport, IPostprocessBuildWithReport
    {
        public const string EmulatorBuildEnvironmentVariable = "DOGGIEHOOD_EMULATOR_BUILD";

        /// <summary>Runs after the `.debug` suffix hook (callback order 0).</summary>
        private const int EmulatorCallbackOrder = 1;

        /// <summary>The emulator variant is x86_64-only — carrying ARM64 too would just bloat it.</summary>
        private const AndroidArchitecture EmulatorArchitectures = AndroidArchitecture.X86_64;

        /// <summary>Pinning the order requires Auto Graphics API to be off.</summary>
        private const bool EmulatorUsesAutomaticGraphicsApis = false;

        /// <summary>Vulkan first, GLES3 as the fallback for emulators without a usable Vulkan ICD.</summary>
        private static readonly GraphicsDeviceType[] EmulatorGraphicsApis =
        {
            GraphicsDeviceType.Vulkan,
            GraphicsDeviceType.OpenGLES3,
        };

        /// <summary>The render worker is what hangs on the reported emulator host.</summary>
        private const bool EmulatorMultithreadedRendering = false;

        /// <summary>sRGB only — i.e. wide colour gamut off.</summary>
        private static readonly ColorGamut[] EmulatorColorGamuts = { ColorGamut.sRGB };

        /// <summary>
        /// Colour gamut has no public PlayerSettings API in Unity 6
        /// (GetColorGamuts/SetColorGamuts are internal), so it is read and
        /// written through the PlayerSettings singleton's serialized data.
        /// The key name is verified against real ProjectSettings.asset files
        /// per docs/engineering/unity-serialization.md.
        /// </summary>
        private const string ColorGamutsSerializedProperty = "m_ColorGamuts";

        private const string PlayerSettingsSingletonName = "PlayerSettings";

        /// <summary>
        /// Unity's audio engine is what spins on the reported Waydroid host
        /// (#705): a per-thread profile of the hung emulator APK showed
        /// `FMOD stream thr` and `AudioTrack` pinned at ~100% CPU with
        /// `UnityMain` blocked behind them, while `UnityGfxDeviceW` slept. The
        /// project ships no audio assets at all, so the emulator variant gives
        /// the whole subsystem up rather than let it spin against a virtual
        /// audio HAL. Emulator-only — the device build keeps audio on, ready
        /// for the clips that land later.
        /// </summary>
        private const bool EmulatorDisablesUnityAudio = true;

        /// <summary>
        /// "Disable Unity Audio" has no public PlayerSettings API either, and
        /// it lives on a different settings singleton (AudioManager, not
        /// PlayerSettings), so it is read and written the same serialized way
        /// as the colour gamut list. The singleton name and the key name are
        /// verified against real ProjectSettings/AudioManager.asset files and
        /// against Unity's own AudioManagerInspector, per
        /// docs/engineering/unity-serialization.md — not guessed.
        /// </summary>
        private const string DisableAudioSerializedProperty = "m_DisableAudio";

        private const string AudioManagerSingletonName = "AudioManager";

        private static bool _profileApplied;
        private static string _originalApplicationId;
        private static AndroidArchitecture _originalArchitectures;
        private static bool _originalUseDefaultGraphicsApis;
        private static GraphicsDeviceType[] _originalGraphicsApis;
        private static bool _originalMultithreadedRendering;
        private static ColorGamut[] _originalColorGamuts;
        private static bool _originalDisableUnityAudio;

        public int callbackOrder => EmulatorCallbackOrder;

        public void OnPreprocessBuild(BuildReport report) => ApplyIfRequested();

        public void OnPostprocessBuild(BuildReport report) => RestoreIfApplied();

        /// <summary>
        /// Applies the emulator profile if
        /// <see cref="EmulatorBuildEnvironmentVariable"/> is truthy, capturing
        /// the pre-build value of every field it touches. Public (rather than
        /// private) so EditMode tests can drive it without constructing a
        /// <see cref="BuildReport"/> — Unity's Bee build strips non-public
        /// members from an assembly's ref.dll, so `internal` isn't visible to
        /// the separate EditMode test assembly.
        /// </summary>
        public static void ApplyIfRequested()
        {
            var envValue = Environment.GetEnvironmentVariable(EmulatorBuildEnvironmentVariable);
            if (!EmulatorBuildProfile.IsEmulatorBuildRequested(envValue))
            {
                _profileApplied = false;
                return;
            }

            _originalApplicationId = PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Android);
            _originalArchitectures = PlayerSettings.Android.targetArchitectures;
            _originalUseDefaultGraphicsApis = PlayerSettings.GetUseDefaultGraphicsAPIs(BuildTarget.Android);
            _originalGraphicsApis = PlayerSettings.GetGraphicsAPIs(BuildTarget.Android);
            _originalMultithreadedRendering = PlayerSettings.GetMobileMTRendering(NamedBuildTarget.Android);
            _originalColorGamuts = GetColorGamuts();
            _originalDisableUnityAudio = GetDisableUnityAudio();
            _profileApplied = true;

            PlayerSettings.SetApplicationIdentifier(
                NamedBuildTarget.Android,
                EmulatorBuildProfile.Apply(_originalApplicationId, isEmulatorBuild: true));
            PlayerSettings.Android.targetArchitectures = EmulatorArchitectures;
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, EmulatorUsesAutomaticGraphicsApis);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, EmulatorGraphicsApis);
            PlayerSettings.SetMobileMTRendering(NamedBuildTarget.Android, EmulatorMultithreadedRendering);
            SetColorGamuts(EmulatorColorGamuts);
            SetDisableUnityAudio(EmulatorDisablesUnityAudio);
        }

        /// <summary>
        /// Restores every field captured by <see cref="ApplyIfRequested"/>.
        /// A no-op when the emulator profile was never applied.
        /// </summary>
        public static void RestoreIfApplied()
        {
            if (!_profileApplied)
            {
                return;
            }

            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, _originalApplicationId);
            PlayerSettings.Android.targetArchitectures = _originalArchitectures;
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, _originalGraphicsApis);
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, _originalUseDefaultGraphicsApis);
            PlayerSettings.SetMobileMTRendering(NamedBuildTarget.Android, _originalMultithreadedRendering);
            SetColorGamuts(_originalColorGamuts);
            SetDisableUnityAudio(_originalDisableUnityAudio);

            _profileApplied = false;
            _originalApplicationId = null;
            _originalGraphicsApis = null;
            _originalColorGamuts = null;
        }

        /// <summary>
        /// Reads the project's colour gamut list. Returns an empty array if
        /// the serialized property can't be resolved, so a build never fails
        /// over a setting that is already at its (sRGB) default.
        /// </summary>
        public static ColorGamut[] GetColorGamuts()
        {
            var property = FindColorGamutsProperty();
            if (property == null)
            {
                return Array.Empty<ColorGamut>();
            }

            var gamuts = new ColorGamut[property.arraySize];
            for (var i = 0; i < gamuts.Length; i++)
            {
                gamuts[i] = (ColorGamut)property.GetArrayElementAtIndex(i).intValue;
            }

            return gamuts;
        }

        /// <summary>
        /// Writes the project's colour gamut list. A no-op if the serialized
        /// property can't be resolved or there is nothing to write.
        /// </summary>
        public static void SetColorGamuts(ColorGamut[] gamuts)
        {
            if (gamuts == null || gamuts.Length == 0)
            {
                return;
            }

            var property = FindColorGamutsProperty();
            if (property == null)
            {
                return;
            }

            property.arraySize = gamuts.Length;
            for (var i = 0; i < gamuts.Length; i++)
            {
                property.GetArrayElementAtIndex(i).intValue = (int)gamuts[i];
            }

            property.serializedObject.ApplyModifiedProperties();
        }

        /// <summary>
        /// Reads whether Unity's audio engine is switched off project-wide
        /// ("Disable Unity Audio"). Returns false if the serialized property
        /// can't be resolved, so a build never fails over a setting that is
        /// already at its (audio-enabled) default.
        /// </summary>
        public static bool GetDisableUnityAudio()
        {
            var property = FindDisableAudioProperty();
            return property != null && property.boolValue;
        }

        /// <summary>
        /// Switches Unity's audio engine off (or back on) project-wide. A
        /// no-op if the serialized property can't be resolved.
        /// </summary>
        public static void SetDisableUnityAudio(bool disabled)
        {
            var property = FindDisableAudioProperty();
            if (property == null)
            {
                return;
            }

            property.boolValue = disabled;
            property.serializedObject.ApplyModifiedProperties();
        }

        private static SerializedProperty FindColorGamutsProperty()
        {
            var property = FindSettingsProperty(PlayerSettingsSingletonName, ColorGamutsSerializedProperty);
            if (property == null)
            {
                return null;
            }

            if (!property.isArray)
            {
                Debug.LogWarning($"Could not resolve {ColorGamutsSerializedProperty}; leaving the colour gamut list alone.");
                return null;
            }

            return property;
        }

        private static SerializedProperty FindDisableAudioProperty()
        {
            return FindSettingsProperty(AudioManagerSingletonName, DisableAudioSerializedProperty);
        }

        private static SerializedProperty FindSettingsProperty(string singletonName, string propertyName)
        {
            var settings = Unsupported.GetSerializedAssetInterfaceSingleton(singletonName);
            if (settings == null)
            {
                Debug.LogWarning($"Could not resolve the {singletonName} singleton; leaving {propertyName} alone.");
                return null;
            }

            var serializedObject = new SerializedObject(settings);
            var property = serializedObject.FindProperty(propertyName);
            if (property == null)
            {
                Debug.LogWarning($"Could not resolve {propertyName} on {singletonName}; leaving it alone.");
                return null;
            }

            return property;
        }
    }
}
