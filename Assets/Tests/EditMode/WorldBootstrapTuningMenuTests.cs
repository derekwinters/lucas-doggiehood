using System.Reflection;
using Doggiehood.Core.Cameras;
using Doggiehood.Core.Debugging;
using Doggiehood.Core.Tuning;
using Doggiehood.Core.World;
using Doggiehood.Unity;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Doggiehood.Unity.EditModeTests
{
    /// <summary>
    /// #656: the bootstrap's tuning-menu wiring. Derek's correction to #622 is
    /// that the balance tuning panel is part of the <em>existing debug menu</em>
    /// — present in <b>every</b> build (development, release-candidate and the
    /// shipping release alike) and reached only through the existing 10-tap
    /// Debug unlock (#219). There is no build-configuration gate anywhere in
    /// the path, so these tests assert (a) the overlay is built unconditionally
    /// and wired to the Debug row, and (b) the unlock gesture — now the only
    /// gate in a shipping build — genuinely holds end to end.
    /// </summary>
    public class WorldBootstrapTuningMenuTests
    {
        private const string BundledFontPath = "Assets/UI/Fonts/Resources/DejaVuSans.ttf";

        private GameObject bootstrapHost;
        private GameObject canvasHost;
        private GameObject worldRoot;
        private SettingsPanel settings;
        private TuningMenuOverlay overlay;
        private TuningConfig configAtStart;

        [SetUp]
        public void BuildBootstrapWiring()
        {
            // #544: the modal-input gate is a process-global singleton.
            ModalInputGate.Shared.Clear();

            // #291: labels bind a bundled UI font via Resources.Load; force-import
            // it so a fresh CI Library resolves it before the UI is built
            // (docs/engineering/unity-serialization.md §4).
            AssetDatabase.ImportAsset(BundledFontPath, ImportAssetOptions.ForceSynchronousImport);

            // TuningConfig.Active is process-global: snapshot it so nothing here
            // can leak balance changes into another test.
            configAtStart = TuningConfig.Active;
            TuningConfig.Active = new TuningConfig();

            canvasHost = new GameObject("ui-canvas", typeof(Canvas));
            canvasHost.AddComponent<UiCanvas>().Configure();
            worldRoot = new GameObject("world-root");

            // The one wiring step under test is invoked directly rather than
            // mirrored, so these assertions run against the shipped code path
            // instead of a copy of it. The host stays INACTIVE so Awake() can
            // never fire and build a whole world underneath the test (edit mode
            // would not call it anyway — WorldBootstrap carries no
            // ExecuteInEditMode/ExecuteAlways — but this makes it impossible).
            bootstrapHost = new GameObject("world-bootstrap");
            bootstrapHost.SetActive(false);
            var bootstrap = bootstrapHost.AddComponent<WorldBootstrap>();
            var build = typeof(WorldBootstrap).GetMethod(
                "BuildSettingsPanel", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(build, Is.Not.Null,
                "WorldBootstrap.BuildSettingsPanel(canvas, state, worldRoot) is the wiring under test");

            settings = (SettingsPanel)build.Invoke(
                bootstrap, new object[] { canvasHost, GameState.CreateNew(), worldRoot.transform });
            overlay = canvasHost.GetComponentInChildren<TuningMenuOverlay>(includeInactive: true);
        }

        [TearDown]
        public void Cleanup()
        {
            Object.DestroyImmediate(bootstrapHost);
            Object.DestroyImmediate(worldRoot);
            Object.DestroyImmediate(canvasHost);
            TuningConfig.Active = configAtStart;
            TuningConfig.ResetToDefaults();
            ModalInputGate.Shared.Clear();
        }

        [Test]
        public void Bootstrap_BuildsTheTuningOverlay_WithoutConsultingADevBuildGate()
        {
            Assert.That(overlay, Is.Not.Null,
                "the overlay ships in every build; nothing about the build configuration gates it");
            Assert.That(overlay.IsOpen, Is.False, "it starts closed, behind the Debug row");

            // "Layer, don't replace": built after the Settings panel, so it is a
            // later canvas sibling and draws over it.
            Assert.That(overlay.transform.GetSiblingIndex(),
                Is.GreaterThan(settings.transform.GetSiblingIndex()));
        }

        [Test]
        public void DevBuildGate_IsGoneFromTheUnityAssembly()
        {
            // #656: the tuning menu was its only consumer, so an unreferenced
            // gate would imply a rule the project no longer has.
            Assert.That(typeof(SettingsPanel).Assembly.GetType("Doggiehood.Unity.DevBuildGate"),
                Is.Null,
                "DevBuildGate is removed — the 10-tap unlock is the only gate now");
        }

        [Test]
        public void Bootstrap_WiresTheDebugRowToTheOverlay()
        {
            Assert.That(settings.TuneBalanceRequested, Is.Not.Null);

            settings.TuneBalanceRequested.Invoke();

            Assert.That(overlay.IsOpen, Is.True);
        }

        [Test]
        public void TuningOverlay_IsUnreachableUntilTheTenTapDebugUnlock()
        {
            // The security-relevant assertion of #656: this unlock is the ONLY
            // thing standing between a shipping player and the balance sliders.
            settings.Open();

            Assert.That(settings.DebugTabVisible, Is.False);
            Assert.That(settings.TuneBalanceButtonRect.gameObject.activeInHierarchy, Is.False);
            Assert.That(overlay.IsOpen, Is.False);

            // One tap short of the gesture: still locked, still no way through.
            for (var i = 0; i < DebugUnlockGesture.TapsToUnlock - 1; i++)
            {
                settings.TapVersion(i * 0.2);
            }

            Assert.That(settings.DebugTabVisible, Is.False);
            Assert.That(settings.TuneBalanceButtonRect.gameObject.activeInHierarchy, Is.False);
            Assert.That(overlay.IsOpen, Is.False);

            // The tenth tap unlocks the tab; selecting it exposes the row, and
            // only then does the entry pill actually open the panel.
            settings.TapVersion(DebugUnlockGesture.TapsToUnlock * 0.2);
            Assert.That(settings.DebugTabVisible, Is.True);
            Assert.That(overlay.IsOpen, Is.False, "revealing the tab does not open the tuning panel");

            settings.DebugTabRect.GetComponent<Button>().onClick.Invoke();
            Assert.That(settings.TuneBalanceButtonRect.gameObject.activeInHierarchy, Is.True);

            settings.TuneBalanceButtonRect.GetComponent<Button>().onClick.Invoke();
            Assert.That(overlay.IsOpen, Is.True);
            Assert.That(settings.IsOpen, Is.True, "Settings stays open behind it");
        }
    }
}
