using System.Collections.Generic;
using System.Linq;
using Doggiehood.Core.Dogs;
using Doggiehood.Core.Expansion;
using Doggiehood.Core.Quests;
using Doggiehood.Core.World;
using NUnit.Framework;
using UnityEngine;

namespace Doggiehood.Unity.EditModeTests
{
    /// <summary>
    /// #518: <see cref="WelcomePopupDirector"/> raises the "Welcome to the
    /// neighborhood!" pop-up (docs/specs/ui/welcome-popup.md) off the same Core
    /// move-in event the world-spawn path uses
    /// (<see cref="QuestManager.MoveInOccurred"/>), a
    /// <see cref="WelcomePopup.WelcomePopupDelaySeconds"/> beat after the move-in
    /// so it never stacks on the quest-resolution feedback that triggered it. The
    /// copy is the Core-composed <see cref="WelcomeMessage"/>; "Say hi!" pans the
    /// camera to the new house, the scrim tap dismisses without panning.
    /// </summary>
    public class WelcomePopupDirectorTests
    {
        private static readonly int MaxCompletionsToGuaranteeMoveIn =
            (int)System.Math.Ceiling(1.0 / MoveInNumbers.MoveInChanceIncrementPerQuest) + 1;

        private GameState state;
        private GameObject worldRoot;
        private GameObject canvasHost;
        private GameObject cameraHost;
        private int vacantHouseId;
        private WelcomePopup popup;
        private WelcomePopupDirector director;
        private CameraRig rig;

        [SetUp]
        public void SetUp()
        {
            WorldBuilder.ForcePrimitiveFallback = false;

            state = GameState.CreateNew();
            state.Wallet.Deposit(100_000);
            state.SetTargetMap(FrontierEditModeWorld.LoadTargetMap());
            Assert.That(state.TryUnlockTile(FrontierEditModeWorld.FirstTile), Is.True);
            vacantHouseId = state.LotsForUnlockedTile(FrontierEditModeWorld.FirstTile)[0].HouseId;
            Assert.That(state.TryBuildHouse(vacantHouseId), Is.True);

            worldRoot = WorldBuilder.Build(state);
            DogSpawner.SpawnDogs(state, worldRoot.transform);

            canvasHost = new GameObject("ui-canvas", typeof(Canvas));
            canvasHost.AddComponent<UiCanvas>().Configure();
            var popupHost = new GameObject("welcome-popup");
            popupHost.transform.SetParent(canvasHost.transform, false);
            popup = popupHost.AddComponent<WelcomePopup>();
            popup.Init();

            // A camera rig whose pan bounds cover the unlocked map, so a
            // FocusOn on the frontier house lands exactly rather than clamping.
            cameraHost = new GameObject("camera", typeof(Camera));
            rig = cameraHost.AddComponent<CameraRig>();
            rig.Controller.RecomputeBoundsFromMap(state.Map);
            rig.ApplyConfiguration();

            director = new GameObject("welcome-director").AddComponent<WelcomePopupDirector>();
            director.transform.SetParent(worldRoot.transform);
            director.Init(state, popup, worldRoot.transform);
        }

        [TearDown]
        public void Cleanup()
        {
            WorldBuilder.ForcePrimitiveFallback = false;
            foreach (var presenter in Object.FindObjectsByType<ConversationPresenter>(FindObjectsSortMode.None))
            {
                Object.DestroyImmediate(presenter.gameObject);
            }

            if (worldRoot != null)
            {
                Object.DestroyImmediate(worldRoot);
            }

            Object.DestroyImmediate(canvasHost);
            Object.DestroyImmediate(cameraHost);
        }

        private IReadOnlyList<Dog> TriggerMoveIn()
        {
            var dog = state.Dogs.First(d => d.HouseId != vacantHouseId && !d.HasActiveQuest);
            for (var i = 0; i < MaxCompletionsToGuaranteeMoveIn; i++)
            {
                var before = state.Dogs.Count;
                var quest = state.Quests.GiveQuestTo(dog, QuestType.PestControl, new System.Random(i));
                Assert.That(state.Quests.Accept(quest), Is.True);
                Assert.That(state.Quests.SprayHouse(quest.TargetHouseId.Value), Is.True);
                if (state.Dogs.Count > before)
                {
                    return state.Dogs.Skip(before).ToList();
                }
            }

            Assert.Fail("no move-in occurred within the guaranteed completion bound");
            return null;
        }

        [Test]
        public void MoveIn_DoesNotPopImmediately_ButArmsADelayedWelcome()
        {
            TriggerMoveIn();

            Assert.That(popup.IsOpen, Is.False,
                "the welcome waits a beat so it never stacks on the quest-resolution feedback");
            Assert.That(director.HasPendingWelcome, Is.True);
        }

        [Test]
        public void AfterTheDelay_TheWelcomePops_WithTheCoreComposedCopy()
        {
            var household = TriggerMoveIn();
            var expected = WelcomeMessage.ForHousehold(household);

            director.Tick(WelcomePopup.WelcomePopupDelaySeconds);

            Assert.That(popup.IsOpen, Is.True, "the welcome pops once the delay elapses");
            Assert.That(director.HasPendingWelcome, Is.False);
            Assert.That(popup.NameLabel.text, Is.EqualTo(expected.NameLine));
            Assert.That(popup.MetaLabel.text, Is.EqualTo(expected.MetaLine));
            Assert.That(popup.MemberRow.activeSelf, Is.EqualTo(expected.ShowsMemberChips));
        }

        [Test]
        public void SayHi_PansTheCameraToTheNewHouse()
        {
            TriggerMoveIn();
            director.Tick(WelcomePopup.WelcomePopupDelaySeconds);

            var house = worldRoot.GetComponentsInChildren<HouseView>()
                .Single(v => v.HouseId == vacantHouseId);
            var housePosition = house.transform.position;

            popup.ActionButton.onClick.Invoke();

            Assert.That(popup.IsOpen, Is.False, "Say hi! dismisses");
            Assert.That(rig.Controller.Position.X, Is.EqualTo(housePosition.x).Within(0.01f),
                "Say hi! focuses the camera on the new house (X)");
            Assert.That(rig.Controller.Position.Z, Is.EqualTo(housePosition.z).Within(0.01f),
                "Say hi! focuses the camera on the new house (Z)");
        }

        [Test]
        public void ScrimTap_Dismisses_WithoutPanningTheCamera()
        {
            TriggerMoveIn();
            director.Tick(WelcomePopup.WelcomePopupDelaySeconds);
            var before = rig.Controller.Position;

            popup.ScrimRect.GetComponent<UnityEngine.UI.Button>().onClick.Invoke();

            Assert.That(popup.IsOpen, Is.False, "the scrim tap dismisses");
            Assert.That(rig.Controller.Position.X, Is.EqualTo(before.X),
                "the scrim tap does NOT move the camera (X)");
            Assert.That(rig.Controller.Position.Z, Is.EqualTo(before.Z),
                "the scrim tap does NOT move the camera (Z)");
        }
    }
}
