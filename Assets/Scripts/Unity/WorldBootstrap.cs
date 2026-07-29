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

            // Shared UI canvas (#256): the Settings panel and the dog profile
            // overlay both live under it so each px constant keeps a fixed
            // on-screen meaning across tablet sizes.
            var canvas = BuildUiCanvas();

            // Settings panel (#219): opened from the HUD gear. Version comes
            // from the build (release-please owns it); the Debug fence toggle
            // rebuilds only the fences live.
            var settings = BuildSettingsPanel(canvas, state, root.transform);

            // Dog profile overlay (#165): tapping a dog's body opens it. Its
            // Home button flies the camera to that dog's house.
            BuildDogProfileOverlay(canvas, root.transform);

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
        /// Creates the shared UI canvas (#256) with the CanvasScaler configured
        /// and the single legacy-input EventSystem present. #327: the canvas's
        /// GraphicRaycaster is inert without an EventSystem driving it, and
        /// Unity never auto-creates one for runtime-built UI, so every UGUI
        /// control (close ✕, scrim, buttons) needs it to receive taps on device.
        /// </summary>
        private GameObject BuildUiCanvas()
        {
            var canvasObject = new GameObject("UiCanvas", typeof(Canvas), typeof(UiCanvas));
            canvasObject.transform.SetParent(gameObject.transform);
            canvasObject.GetComponent<UiCanvas>().Configure();
            UiEventSystem.Ensure();
            return canvasObject;
        }

        /// <summary>
        /// Builds the Settings panel (#219) under the shared canvas, wiring the
        /// fence debug toggle to a live, fence-only rebuild of the given world
        /// root. The panel takes the live <paramref name="state"/> so its Debug
        /// "Add coins" action (#286) can deposit into the wallet. Version text
        /// comes from the build via <c>Application.version</c> — release-please
        /// owns the value, this only reads it (never hand-edited).
        /// </summary>
        private SettingsPanel BuildSettingsPanel(GameObject canvas, GameState state, Transform worldRoot)
        {
            var panelObject = new GameObject("SettingsPanel");
            panelObject.transform.SetParent(canvas.transform, false);
            var settings = panelObject.AddComponent<SettingsPanel>();
            settings.Init(state, Application.version);
            settings.WorldRebuild = () => WorldBuilder.RebuildFences(worldRoot);
            return settings;
        }

        /// <summary>
        /// Builds the dog profile overlay (#165) under the shared canvas and
        /// wires its Home button to fly the camera to the tapped dog's house.
        /// The overlay decides nothing about the camera — Core
        /// (<see cref="Doggiehood.Core.Cameras.CameraController.FocusOn"/>) owns
        /// the move; this only resolves the house's world position and applies
        /// the resulting controller state to the live rig.
        /// </summary>
        private DogProfileOverlay BuildDogProfileOverlay(GameObject canvas, Transform worldRoot)
        {
            var overlayObject = new GameObject("DogProfileOverlay");
            overlayObject.transform.SetParent(canvas.transform, false);
            var overlay = overlayObject.AddComponent<DogProfileOverlay>();
            overlay.Init();
            overlay.HomeRequested += houseId => FlyCameraToHouse(houseId, worldRoot);
            return overlay;
        }

        /// <summary>Recentres the camera on a house lot (#165 Home button):
        /// resolves the house's world position from its <see cref="HouseView"/>,
        /// asks the Core controller to focus there (clamped to bounds), and
        /// re-applies the state to the live rig. A no-op if the house or rig
        /// can't be found.</summary>
        private static void FlyCameraToHouse(int houseId, Transform worldRoot)
        {
            HouseView house = null;
            foreach (var view in worldRoot.GetComponentsInChildren<HouseView>())
            {
                if (view.HouseId == houseId)
                {
                    house = view;
                    break;
                }
            }

            var rig = FindFirstObjectByType<CameraRig>();
            if (house == null || rig == null)
            {
                return;
            }

            var position = house.transform.position;
            rig.Controller.FocusOn(new Doggiehood.Core.World.GridPoint(position.x, position.z));
            rig.ApplyConfiguration();
        }
    }
}
