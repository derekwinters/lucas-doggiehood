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

            // Quest pacing (#310 / #312). Before onboarding completes, the #312
            // seam seeds exactly one easy lost-item quest and suppresses the
            // 2-4 rotation. Once onboarding is complete, the recurring refresh
            // is owned by the Core QuestPacingPolicy: MaybeStartNewDay checks
            // the 8h UTC boundary (DateTime.UtcNow — never device-local) and
            // tops up toward the population-scaled cap. No pacing logic lives
            // here; this only picks the pre- vs post-onboarding entry point.
            if (state.OnboardingComplete)
            {
                state.Quests.MaybeStartNewDay(System.DateTime.UtcNow, new System.Random());
            }
            else if (!System.Linq.Enumerable.Any(state.Quests.ActiveQuests))
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
            var dogProfile = BuildDogProfileOverlay(canvas, root.transform);

            // House profile overlay (#208): tapping a house opens it. A
            // resident row opens that dog's profile (the reciprocal of the dog
            // profile's Home button); the Upgrade button (#294, Option A) spends
            // coins directly via GameState.TryUpgradeHouse — no confirmation.
            BuildHouseProfileOverlay(canvas, state, root.transform, dogProfile);

            // Persistent HUD (#159): the currency chip now wears the full Candy
            // Cottage chrome (#65/#296) — cream pill, Ink outline, hard shadow,
            // gold coin token. The top-right gear opens Settings (#219).
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

        /// <summary>
        /// Builds the house profile overlay (#208) under the shared canvas and
        /// wires it up: tapping any <see cref="HouseView"/> opens the profile
        /// for that house and its residents (the dogs living there); a resident
        /// row opens that dog's profile via the dog overlay; the Upgrade button
        /// (#294, Derek's Option A) spends coins directly through the Core entry
        /// point <see cref="Doggiehood.Core.World.GameState.TryUpgradeHouse"/> —
        /// no confirmation screen — with the button's affordability read live
        /// from the wallet. The overlay decides its own display from Core
        /// (<see cref="Doggiehood.Core.World.HouseProfile"/>); this only resolves
        /// which dogs live in the tapped house and injects the wallet/upgrade
        /// wiring.
        /// </summary>
        private HouseProfileOverlay BuildHouseProfileOverlay(
            GameObject canvas, GameState state, Transform worldRoot, DogProfileOverlay dogProfile)
        {
            var overlayObject = new GameObject("HouseProfileOverlay");
            overlayObject.transform.SetParent(canvas.transform, false);
            var overlay = overlayObject.AddComponent<HouseProfileOverlay>();
            overlay.Init();

            // #294: live wallet read for affordability + the direct-spend upgrade
            // call. The overlay re-reads the balance every render (never cached),
            // the same contract the currency HUD uses.
            overlay.ConfigureUpgrade(() => state.Wallet.Coins, houseId => state.TryUpgradeHouse(houseId));

            overlay.ResidentSelected += dog => dogProfile.Open(dog);

            foreach (var view in worldRoot.GetComponentsInChildren<HouseView>())
            {
                var houseId = view.HouseId;
                view.Tapped += () => OpenHouseProfile(overlay, state, houseId);
            }

            return overlay;
        }

        /// <summary>Opens the house profile (#208) for the tapped house,
        /// resolving its Core <see cref="House"/> and the dogs that live there
        /// (residents) from the live state. A no-op if the house id isn't found
        /// (e.g. a lot with no committed house yet).</summary>
        private static void OpenHouseProfile(HouseProfileOverlay overlay, GameState state, int houseId)
        {
            House house = null;
            foreach (var candidate in state.Houses)
            {
                if (candidate.Id == houseId)
                {
                    house = candidate;
                    break;
                }
            }

            if (house == null)
            {
                return;
            }

            var residents = new System.Collections.Generic.List<Doggiehood.Core.Dogs.Dog>();
            foreach (var dog in state.Dogs)
            {
                if (dog.HouseId == houseId)
                {
                    residents.Add(dog);
                }
            }

            overlay.Open(house, residents);
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
