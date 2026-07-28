using Doggiehood.Core.World;
using UnityEngine;

namespace Doggiehood.Unity
{
    /// <summary>Builds the neighborhood and its dogs when the scene starts.</summary>
    public sealed class WorldBootstrap : MonoBehaviour
    {
        private void Awake()
        {
            var state = SaveStore.LoadOrCreate();
            var root = WorldBuilder.Build(state);
            DogSpawner.SpawnDogs(state, root.transform);

            var director = gameObject.AddComponent<QuestDirector>();
            director.Init(state, root.transform);

            // #57: wires tapping an empty lot in an unlocked zone to
            // GameState.TryBuildHouse.
            gameObject.AddComponent<ExpansionDirector>().Init(state, root.transform);

            var presenter = FindFirstObjectByType<ConversationPresenter>();
            if (presenter == null)
            {
                presenter = gameObject.AddComponent<ConversationPresenter>();
            }

            presenter.State = state;
            presenter.Director = director;

            // Initial quest seeding (#312). Before onboarding completes this
            // seeds exactly one easy lost-item quest and suppresses the 2-4
            // rotation; afterwards it's the normal day-one rotation. The Core
            // seam owns that branch — no game logic here. Real once-per-
            // calendar-day gating lands with the vertical-slice integration
            // (milestone 08).
            if (!System.Linq.Enumerable.Any(state.Quests.ActiveQuests))
            {
                state.Quests.BeginInitialQuests(new System.Random());
            }

            gameObject.AddComponent<SfxPlayer>();

            // Settings panel (#219): built under the #256 CanvasScaler, opened
            // from the HUD gear. Version comes from the build (release-please
            // owns it); the Debug fence toggle rebuilds only the fences live.
            var settings = BuildSettingsPanel(state, root.transform);

            // Persistent HUD (#159): graybox currency chip, restyled by #65.
            // The top-right gear opens Settings (#219).
            var hud = gameObject.AddComponent<HudOverlay>();
            hud.Init(state);
            hud.GearTapped += settings.Open;

            // First launch only (#44): tutorial prompts over live gameplay.
            if (Doggiehood.Core.Onboarding.OnboardingSequence.ShouldRun(state))
            {
                // #207: don't silently drop onboarding when no CameraRig is
                // found — resolve one from the main camera if needed, and wire
                // the overlay regardless (it tolerates a null rig).
                var rig = FindFirstObjectByType<CameraRig>();
                if (rig == null && Camera.main != null)
                {
                    rig = Camera.main.GetComponent<CameraRig>()
                        ?? Camera.main.gameObject.AddComponent<CameraRig>();
                }

                gameObject.AddComponent<OnboardingOverlay>().Init(state, rig, presenter);
            }
        }

        /// <summary>
        /// Creates the UI canvas (#256) and the Settings panel (#219) under
        /// it, wiring the fence debug toggle to a live, fence-only rebuild of
        /// the given world root. The panel takes the live <paramref name="state"/>
        /// so its Debug "Add coins" action (#286) can deposit into the wallet.
        /// Version text comes from the build via <c>Application.version</c> —
        /// release-please owns the value, this only reads it (never hand-edited).
        /// </summary>
        private SettingsPanel BuildSettingsPanel(GameState state, Transform worldRoot)
        {
            var canvasObject = new GameObject("UiCanvas", typeof(Canvas), typeof(UiCanvas));
            canvasObject.transform.SetParent(gameObject.transform);
            canvasObject.GetComponent<UiCanvas>().Configure();

            // #327: the canvas's GraphicRaycaster is inert without an
            // EventSystem driving it, and Unity never auto-creates one for
            // runtime-built UI — so no UGUI control in the panel (close ✕,
            // version-tap unlock, scrim, debug toggles) received taps on
            // device. Ensure the single legacy-input EventSystem exists here,
            // guarded against duplicates.
            UiEventSystem.Ensure();

            var panelObject = new GameObject("SettingsPanel");
            panelObject.transform.SetParent(canvasObject.transform, false);
            var settings = panelObject.AddComponent<SettingsPanel>();
            settings.Init(state, Application.version);
            settings.WorldRebuild = () => WorldBuilder.RebuildFences(worldRoot);
            return settings;
        }
    }
}
