using System.Collections.Generic;
using Doggiehood.Core.Quests;
using Doggiehood.Core.World;
using Doggiehood.Unity;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Doggiehood.Unity.EditModeTests
{
    /// <summary>
    /// #422: an open dialog/menu is modal — a tap that lands on UI must never
    /// also reach a world <see cref="IInteractable"/> behind it. Before this
    /// fix the same tap was hit-tested twice (once by UGUI, once by
    /// <see cref="TapRouter"/>'s raw Physics.Raycast), so tapping Accept over a
    /// house also opened the house, and tapping Close over a house reopened it.
    ///
    /// The guard has two halves: the UGUI overlays (ConfirmationDialog /
    /// HouseProfileOverlay / DogProfileOverlay / the migrated ConversationPresenter)
    /// are covered by <c>EventSystem.IsPointerOverGameObject</c>, injected here
    /// through the <see cref="TapRouter.IsPointerOverUi"/> seam because that API
    /// reads the input module's last-processed pointer, which is empty under
    /// headless EditMode; the still-IMGUI HUD gear is covered by a screen-space
    /// rect check (<see cref="Doggiehood.Core.Cameras.GearTapZone"/> +
    /// <see cref="HudOverlay.ComputeGearRect"/>), interim scaffolding until #370.
    /// </summary>
    public class TapRouterUiGuardTests
    {
        private sealed class CountingInteractable : MonoBehaviour, IInteractable
        {
            public int TapCount { get; private set; }

            public void OnTapped()
            {
                TapCount++;
            }
        }

        private GameObject rigObject;
        private CameraRig rig;
        private Camera cam;
        private RenderTexture texture;
        private GameObject targetObject;
        private CountingInteractable target;

        [SetUp]
        public void SetUp()
        {
            // The seam is process-global; restore the production default before
            // each test so a prior test's override can't leak.
            TapRouter.IsPointerOverUi = TapRouter.DefaultIsPointerOverUi;

            // #544: the modal-open seam is also process-global. Pin it to "no
            // modal" here so these #422 EventSystem-guard tests stay independent
            // of the shared ModalInputGate; the modal-specific tests below set
            // it explicitly.
            TapRouter.IsModalOpen = () => false;

            rigObject = new GameObject("rig-under-test", typeof(Camera));
            cam = rigObject.GetComponent<Camera>();
            rig = rigObject.AddComponent<CameraRig>();
            rig.ApplyConfiguration();

            // A real pixel rect so ScreenPointToRay/WorldToScreenPoint work headless.
            texture = new RenderTexture(1920, 1080, 0);
            cam.targetTexture = texture;

            // A collider large enough that any on-screen tap ray hits it, so a
            // world tap is unmissable when the guards let it through.
            targetObject = new GameObject("world-target", typeof(BoxCollider));
            var center = cam.transform.position + cam.transform.forward * 20f;
            targetObject.transform.position = center;
            targetObject.transform.rotation = Quaternion.LookRotation(-cam.transform.forward);
            targetObject.transform.localScale = new Vector3(1000f, 1000f, 1f);
            target = targetObject.AddComponent<CountingInteractable>();
            Physics.SyncTransforms();
        }

        [TearDown]
        public void TearDown()
        {
            TapRouter.IsPointerOverUi = TapRouter.DefaultIsPointerOverUi;
            TapRouter.IsModalOpen = TapRouter.DefaultIsModalOpen;

            cam.targetTexture = null;
            Object.DestroyImmediate(texture);
            Object.DestroyImmediate(targetObject);
            Object.DestroyImmediate(rigObject);

            foreach (var es in Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None))
            {
                Object.DestroyImmediate(es.gameObject);
            }
        }

        private Vector2 TargetScreenPoint()
        {
            var p = cam.WorldToScreenPoint(targetObject.transform.position);
            return new Vector2(p.x, p.y);
        }

        [Test]
        public void TapNotOverUi_RoutesToTheWorldInteractable()
        {
            // Baseline: with nothing over the pointer, world routing is unchanged.
            TapRouter.IsPointerOverUi = _ => false;

            rig.HandleTap(TargetScreenPoint());

            Assert.That(target.TapCount, Is.EqualTo(1),
                "a tap not over any UI must still reach the world IInteractable");
        }

        [Test]
        public void TapOverUi_IsAbsorbed_AndNeverReachesTheWorld()
        {
            // #422 core: a modal overlay over the pointer absorbs the tap.
            TapRouter.IsPointerOverUi = _ => true;

            rig.HandleTap(TargetScreenPoint());

            Assert.That(target.TapCount, Is.EqualTo(0),
                "a tap over an open UGUI overlay must not fire the world object behind it");
        }

        [Test]
        public void TouchPath_ThreadsFingerId_WhileMousePath_PassesNull()
        {
            // #422: the touch release must use IsPointerOverGameObject(fingerId),
            // not the parameterless mouse overload — so HandleTap threads the
            // fingerId all the way to the pointer-over-UI check, and the mouse
            // path leaves it null.
            var seen = new List<int?>();
            TapRouter.IsPointerOverUi = id =>
            {
                seen.Add(id);
                return false;
            };
            var point = TargetScreenPoint();

            rig.HandleTap(point, pointerId: 7);
            rig.HandleTap(point);

            Assert.That(seen[0], Is.EqualTo((int?)7),
                "the touch release threads its fingerId into the pointer-over-UI check");
            Assert.That(seen[1], Is.Null,
                "the mouse release leaves the pointerId null (parameterless overload)");
        }

        [Test]
        public void ModalOpen_AbsorbsTheTap_EvenWhenPointerOverUiReportsFalse()
        {
            // #544 core: the actual fix. Simulate the touch tap-release gap
            // where EventSystem.IsPointerOverGameObject(fingerId) reports false
            // even though the finger is over an open profile's scrim. With a
            // modal registered open, the world object behind it must NOT fire —
            // this guard is independent of the EventSystem pointer timing.
            TapRouter.IsPointerOverUi = _ => false;
            TapRouter.IsModalOpen = () => true;

            rig.HandleTap(TargetScreenPoint());

            Assert.That(target.TapCount, Is.EqualTo(0),
                "with a modal open the tap is swallowed before the world raycast, regardless of the pointer-over-UI signal");
        }

        [Test]
        public void ModalClosed_LeavesWorldRoutingIntact()
        {
            // The modal guard must not dead the world when nothing is open.
            TapRouter.IsPointerOverUi = _ => false;
            TapRouter.IsModalOpen = () => false;

            rig.HandleTap(TargetScreenPoint());

            Assert.That(target.TapCount, Is.EqualTo(1),
                "with no modal open, a world tap still reaches its IInteractable");
        }

        [Test]
        public void DefaultIsModalOpen_TracksTheSharedModalInputGate()
        {
            // The production default reads the shared gate, so a panel that
            // registers anywhere blocks world taps everywhere.
            TapRouter.IsModalOpen = TapRouter.DefaultIsModalOpen;
            var token = new object();
            Doggiehood.Core.Cameras.ModalInputGate.Shared.Register(token);
            try
            {
                Assert.That(TapRouter.IsModalOpen(), Is.True,
                    "the default modal seam reflects a registered modal on the shared gate");
            }
            finally
            {
                Doggiehood.Core.Cameras.ModalInputGate.Shared.Unregister(token);
            }

            Assert.That(TapRouter.IsModalOpen(), Is.False,
                "once unregistered, the default modal seam reports no modal");
        }

        [Test]
        public void TapOverTheHudGearRect_IsAbsorbed_OnlyWhenTheGearIsPresent()
        {
            // The HUD gear is still IMGUI (#370 pending), outside the EventSystem,
            // so it gets a screen-space rect guard instead of IsPointerOverUi.
            TapRouter.IsPointerOverUi = _ => false;

            // ComputeGearRect is GUI space (top-left origin); taps are screen
            // space (bottom-left origin) — flip Y to match the production guard.
            var gearGui = HudOverlay.ComputeGearRect(Screen.width, Screen.height);
            var gearTap = new Vector2(gearGui.center.x, Screen.height - gearGui.center.y);

            // No gear in the scene: the corner routes to the world as usual.
            rig.HandleTap(gearTap);
            Assert.That(target.TapCount, Is.EqualTo(1),
                "with no HUD gear present, the corner tap still reaches the world");

            var hudObject = new GameObject("hud", typeof(HudOverlay));
            try
            {
                rig.HandleTap(gearTap);
                Assert.That(target.TapCount, Is.EqualTo(1),
                    "with the gear present, a tap on its rect is absorbed — the world count is unchanged");
            }
            finally
            {
                Object.DestroyImmediate(hudObject);
            }
        }

        [Test]
        public void DefaultPointerOverUi_ReturnsFalse_WhenNoEventSystemIsPresent()
        {
            // The production default is null-safe: with no EventSystem it never
            // blocks (so world taps still route), for both overloads.
            foreach (var es in Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None))
            {
                Object.DestroyImmediate(es.gameObject);
            }

            Assert.That(TapRouter.DefaultIsPointerOverUi(null), Is.False,
                "no EventSystem -> the mouse overload can't be over UI");
            Assert.That(TapRouter.DefaultIsPointerOverUi(3), Is.False,
                "no EventSystem -> the touch overload can't be over UI");
        }

        [Test]
        public void LostItemTapRouting_StillCompletes_WhenNotOverUiOrGear()
        {
            // #311 regression: the new guards are early-outs, so the existing
            // lost-item screen-space routing still fires when the tap is not
            // over UI or the gear.
            TapRouter.IsPointerOverUi = _ => false;

            var state = GameState.CreateNew();
            var itemParent = new GameObject("lost-item-parent");
            var camGo = new GameObject("li-cam", typeof(Camera));
            var liCam = camGo.GetComponent<Camera>();
            liCam.orthographic = true;
            liCam.orthographicSize = 3f;
            var liTexture = new RenderTexture(1920, 1080, 0);
            liCam.targetTexture = liTexture;
            try
            {
                var quest = new Quest(
                    1, QuestType.LostItem, "Zeus", "puppy",
                    new string[0], new GridPoint(3f, -4f), null, null);
                var view = LostItemView.Spawn(state, quest, itemParent.transform);
                view.transform.position = new Vector3(500f, 0f, 500f);

                var bounds = CombinedRendererBounds(view.transform);
                liCam.transform.position = bounds.center + new Vector3(0f, 6f, -6f);
                liCam.transform.LookAt(bounds.center);
                Physics.SyncTransforms();

                var screenCenter = liCam.WorldToScreenPoint(bounds.center);
                var handled = TapRouter.RouteTap(liCam, new Vector2(screenCenter.x, screenCenter.y));

                Assert.That(handled, Is.True,
                    "lost-item routing still completes when the tap is not over UI or the gear");
            }
            finally
            {
                liCam.targetTexture = null;
                Object.DestroyImmediate(liTexture);
                Object.DestroyImmediate(camGo);
                Object.DestroyImmediate(itemParent);
            }
        }

        private static Bounds CombinedRendererBounds(Transform root)
        {
            var renderers = root.GetComponentsInChildren<Renderer>();
            var bounds = renderers[0].bounds;
            for (var i = 1; i < renderers.Length; i++)
            {
                bounds.Encapsulate(renderers[i].bounds);
            }

            return bounds;
        }
    }
}
