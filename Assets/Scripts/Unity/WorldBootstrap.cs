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

            // #453 (Decision A): supply Core the authored target neighborhood
            // (docs/tools/map-data.json, staged under Resources/) BEFORE anything
            // reads GameState.UnlockableFrontier — otherwise the frontier is
            // empty and no expansion locks appear. Rejected authoring coordinates
            // are logged by the loader, not silently dropped.
            MapDataLoader.Apply(state);

            var root = WorldBuilder.Build(state);
            DogSpawner.SpawnDogs(state, root.transform);

            // #407/#436: the upgrade re-renderer is created up front so it can be
            // handed to the QuestDirector below, which reuses its
            // destroy-and-rebuild to drop a house's vacancy tint on a live
            // move-in (#436) — the same path #407 uses on upgrade. Its own
            // rebuild callback (tap re-wiring) is configured later, in
            // BuildHouseProfileOverlay, but that runs before any quest can
            // complete, so the reference is fully wired by the time a move-in
            // fires. (Construction order moved earlier than #407's original
            // profile-overlay closure so QuestDirector can depend on it.)
            var upgradeDirector = gameObject.AddComponent<HouseUpgradeDirector>();

            var director = gameObject.AddComponent<QuestDirector>();
            director.Init(state, root.transform, upgradeDirector);

            // #571: mark the onboarding "fix up a home" target house with the
            // existing red ground-ring highlight (#535) while the reward chain
            // waits on its upgrade step, so it's obvious which house to tap and
            // upgrade. Purely feedback attached to that house; the show/target
            // decision is Core's (OnboardingHouseHighlight), and — unlike the #506
            // coach bar — it is NOT suppressed while a centered profile panel is
            // open, filling exactly that gap. Harmless outside onboarding (Core
            // reports no target), so it is wired unconditionally.
            gameObject.AddComponent<OnboardingHouseHighlightDirector>()
                .Init(state, root.transform);

            // Quest pacing (#310 / #312 / #316). The whole phase decision lives
            // in Core (QuestManager.EnsureQuestsForLaunch): pre-chain it seeds
            // the one tutorial quest, mid-chain (the guided upgrade/expand/build
            // reward-chain steps) it stays suppressed, and post-chain it runs
            // the recurring hourly #310/#543 refresh (DateTime.UtcNow — never
            // device-local). No pacing logic lives in this MonoBehaviour.
            state.Quests.EnsureQuestsForLaunch(System.DateTime.UtcNow, new System.Random());

            gameObject.AddComponent<SfxPlayer>();

            // Shared UI canvas (#256): the Settings panel and the dog profile
            // overlay both live under it so each px constant keeps a fixed
            // on-screen meaning across tablet sizes.
            var canvas = BuildUiCanvas();

            // Conversation/quest panel (#11/#408): the Candy Cottage DialogueBox
            // (#175) now lives under the shared canvas so its px constants keep a
            // fixed on-screen meaning; DogView finds it by type on a bubble tap.
            var presenter = BuildConversationPresenter(canvas, state, director);

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
            // coins directly via GameState.TryUpgradeHouse — no confirmation —
            // and (#407) re-renders the world house so its mesh visibly grows.
            // The quest director is passed so the #407 re-render can re-wire the
            // rebuilt HouseView's spray tap alongside the profile tap.
            var houseProfile = BuildHouseProfileOverlay(
                canvas, state, root.transform, dogProfile, director, upgradeDirector);

            // Reusable confirmation dialog (#343/#344), shared by both expansion
            // spends below. A device-safe UGUI overlay (#298/#291) built under the
            // canvas, so it must exist before the directors that raise it.
            var confirmationDialog = BuildConfirmationDialog(canvas);

            // #57/#406: tapping an empty lot in an unlocked zone raises the
            // confirmation dialog ("Build a house here?" + the flat cost on Yes),
            // and only Yes calls GameState.TryBuildHouse — a stray tap no longer
            // spends coins.
            // #456: a house built on an empty lot is vacant and never rebuilt, so
            // — unlike a starter house (wired in BuildHouseProfileOverlay's
            // foreach) or an occupied house (re-wired on move-in/upgrade rebuild)
            // — nothing else subscribes to its tap. Wire the freshly built house
            // to the profile-open outcome here (WireHouses supplies the spray
            // side), the same wiring the HouseUpgradeDirector rebuild callback
            // applies to a rebuilt house.
            var expansionDirector = gameObject.AddComponent<ExpansionDirector>();
            expansionDirector.Init(state, root.transform, confirmationDialog, built =>
            {
                var builtId = built.HouseId;
                built.ProfileRequested += () => OpenHouseProfile(houseProfile, state, builtId);
                director.WireHouses();
            });

            // Map-expansion unlock trigger (#453, Option A): one lock indicator
            // per unlockable frontier coordinate; tapping an affordable lock
            // raises the same reusable confirmation dialog, and Yes calls
            // GameState.TryUnlockTile for THAT coordinate (spend + the tile
            // appears + save).
            gameObject.AddComponent<ExpansionUnlockDirector>()
                .Init(state, root.transform, confirmationDialog, expansionDirector);

            // Completion toasts (#541): the non-modal top-left toast lane
            // (docs/specs/ui/toast.md) celebrates two — and only two — triggers,
            // each enqueued onto one shared single-slot queue and rendered one at a
            // time by the ToastView. This reverses the #374 modal reward panel: the
            // onboarding reward-chain step feedback now surfaces as a toast, never a
            // blocking modal. Core owns both payouts (the toasts move no coins), and
            // both directors stay silent when their event never fires (a returning
            // player's completed chain never re-pays).
            var toastQueue = new Doggiehood.Core.Ui.ToastQueue<ToastRequest>();
            gameObject.AddComponent<ToastView>().Init(toastQueue);
            gameObject.AddComponent<OnboardingRewardDirector>().Init(state, toastQueue);
            gameObject.AddComponent<QuestCompletionDirector>().Init(state, toastQueue);

            // Move-in welcome (#518): the approved "Welcome to the
            // neighborhood!" pop-up (docs/specs/ui/welcome-popup.md) pops a beat
            // after a household moves into a vacant house, off the same Core
            // MoveInOccurred event QuestDirector uses to spawn the new dogs — so
            // the player is told a move-in happened and where. Its "Say hi!"
            // button pans the camera to the new house AND opens that house's
            // profile (#604) — reusing the same OpenHouseProfile resolve a house
            // tap uses, so the resident dog(s) are one tap away — while the
            // director only presents the Core-composed copy (WelcomeMessage) and
            // moves no state.
            var welcomePopup = BuildWelcomePopup(canvas);
            gameObject.AddComponent<WelcomePopupDirector>().Init(
                state, welcomePopup, root.transform,
                houseId => OpenHouseProfile(houseProfile, state, houseId));

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

                // #506: the bottom-anchored coach bar would otherwise cover a
                // centered modal panel's controls — most visibly the house
                // profile's footer Upgrade button during the Upgrade step. Suppress
                // the bar while any centered panel is open. Composed here (not
                // special-cased inside the overlay) so it generalizes to every
                // current/future centered panel — house and dog profiles today.
                gameObject.AddComponent<OnboardingOverlay>()
                    .Init(state, rig, presenter,
                        () => houseProfile.IsOpen || dogProfile.IsOpen);
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
        /// Builds the conversation/quest panel (#11/#408) under the shared canvas
        /// and starts it closed. The Candy Cottage DialogueBox (#175) is device-
        /// safe UGUI (#298/#291); its game logic stays in Core (accept flows
        /// through <paramref name="state"/>, quest completion through the
        /// <paramref name="director"/>).
        /// </summary>
        private ConversationPresenter BuildConversationPresenter(GameObject canvas, GameState state, QuestDirector director)
        {
            var presenterObject = new GameObject("ConversationPresenter");
            presenterObject.transform.SetParent(canvas.transform, false);
            var presenter = presenterObject.AddComponent<ConversationPresenter>();
            presenter.State = state;
            presenter.Director = director;
            presenter.Init();
            return presenter;
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
            settings.WorldRebuild = () => WorldBuilder.RebuildFences(worldRoot, state);
            // #611: the debug-colors toggle repaints the ground plane and
            // reconfigures the camera backstop live, so the loud debug colours
            // swap in/out on-device without a restart.
            settings.DebugColorsRefresh = () =>
            {
                WorldBuilder.RepaintGround(worldRoot);
                var rig = UnityEngine.Object.FindFirstObjectByType<CameraRig>();
                if (rig != null)
                {
                    rig.ApplyConfiguration();
                }
            };

            // #622/#656: the balance tuning menu. Built in every build, because
            // it is part of the existing debug menu — the Settings Debug tab's
            // 10-tap unlock (#219) is its only gate, exactly like the rows
            // above it. Built AFTER the Settings panel so it is a later canvas
            // sibling and therefore layers OVER it, per the wireframe's "layer,
            // don't replace" (docs/specs/ui/debug-tuning-menu.md).
            var tuningMenu = TuningMenuOverlay.Create(canvas.transform);
            settings.TuneBalanceRequested = tuningMenu.Open;

            return settings;
        }

        /// <summary>
        /// Builds the reusable confirmation dialog (#343/#344) under the shared
        /// canvas, starting closed. Any spend-confirming affordance raises it by
        /// supplying its own title/body/cost + confirm callback; its consumers
        /// are the house-build (#57/#406) and zone-unlock (#343) triggers wired
        /// above.
        /// </summary>
        private ConfirmationDialog BuildConfirmationDialog(GameObject canvas)
        {
            var dialogObject = new GameObject("ConfirmationDialog");
            dialogObject.transform.SetParent(canvas.transform, false);
            var dialog = dialogObject.AddComponent<ConfirmationDialog>();
            dialog.Init();
            return dialog;
        }

        /// <summary>
        /// Builds the reusable move-in welcome pop-up (#518,
        /// docs/specs/ui/welcome-popup.md) under the shared canvas, starting
        /// closed. The welcome director raises it a beat after each move-in with
        /// the Core-composed copy; the pop-up is pure presentation apart from the
        /// "Say hi!" camera pan the director wires.
        /// </summary>
        private WelcomePopup BuildWelcomePopup(GameObject canvas)
        {
            var popupObject = new GameObject("WelcomePopup");
            popupObject.transform.SetParent(canvas.transform, false);
            var popup = popupObject.AddComponent<WelcomePopup>();
            popup.Init();
            return popup;
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
            GameObject canvas, GameState state, Transform worldRoot, DogProfileOverlay dogProfile,
            QuestDirector questDirector, HouseUpgradeDirector upgradeDirector)
        {
            var overlayObject = new GameObject("HouseProfileOverlay");
            overlayObject.transform.SetParent(canvas.transform, false);
            var overlay = overlayObject.AddComponent<HouseProfileOverlay>();
            overlay.Init();

            // #407: the upgrade re-renders the world house so its mesh swaps up
            // the ladder (the profile panel already advanced, the world didn't).
            // On rebuild the fresh HouseView is re-wired to both tap outcomes
            // — the profile-open handler here and QuestDirector's spray handler
            // (#670: mutually exclusive, arbitrated in Core) — since a rebuilt
            // object neither one-time bootstrap loop has seen.
            // #436: the same rebuild + re-wire runs when a move-in drops a
            // house's vacancy tint (QuestDirector calls RefreshHouse), so the
            // rebuilt house's taps keep reaching the profile and spray paths.
            // The director is created earlier in Awake so QuestDirector can hold
            // a reference; its rebuild callback is configured here.
            upgradeDirector.Init(state, worldRoot, rebuilt =>
            {
                var houseId = rebuilt.HouseId;
                rebuilt.ProfileRequested += () => OpenHouseProfile(overlay, state, houseId);
                questDirector.WireHouses();
            });

            // #294: live wallet read for affordability + the direct-spend upgrade
            // call. The overlay re-reads the balance every render (never cached),
            // the same contract the currency HUD uses. #407: on a successful Core
            // upgrade, re-render the world house before reporting success so the
            // overlay's panel refresh and the world mesh swap land together.
            overlay.ConfigureUpgrade(
                () => state.Wallet.Coins,
                houseId =>
                {
                    if (!state.TryUpgradeHouse(houseId))
                    {
                        return false;
                    }

                    upgradeDirector.RefreshHouse(houseId);
                    return true;
                },
                // #469: fold "not the eligible house right now" into the button's
                // existing disabled state — a non-target house during onboarding's
                // "upgrade a house" step greys out like an unaffordable one.
                houseId => state.IsHouseUpgradeEligible(houseId));

            overlay.ResidentSelected += dog => dogProfile.Open(dog);

            foreach (var view in worldRoot.GetComponentsInChildren<HouseView>())
            {
                var houseId = view.HouseId;
                view.ProfileRequested += () => OpenHouseProfile(overlay, state, houseId);
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
