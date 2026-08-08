using System.Collections.Generic;
using System.Linq;
using Doggiehood.Core.Dogs;
using Doggiehood.Core.World;
using Doggiehood.Unity;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Doggiehood.Unity.EditModeTests
{
    /// <summary>
    /// #456 regression: tapping a house must open its profile — the missing
    /// end-to-end counterpart to <see cref="DogLayerTests.TapRaycast_OnADogsBody_OpensItsProfile"/>
    /// (#148) for <see cref="HouseView"/>. The reported break was specific to
    /// an <b>empty (vacant)</b> house, and it turned out not to be in
    /// <see cref="TapRouter"/> at all (the #422 pointer-over-UI guard is
    /// untouched here): a starter house is wired to the profile at bootstrap and
    /// an occupied house is re-wired on every move-in/upgrade rebuild
    /// (<see cref="HouseUpgradeDirector.RefreshHouse"/>), but a house freshly
    /// built on an empty lot by <see cref="ExpansionDirector"/> — the only house
    /// that is vacant and has never been rebuilt — had no subscriber on its
    /// <see cref="HouseView.ProfileRequested"/> event, so the tap reached the world and
    /// then did nothing. These tests lock both vacancy states so neither can
    /// silently regress again.
    /// </summary>
    public class HouseTapRoutingTests
    {
        private const string BundledFontPath = "Assets/UI/Fonts/Resources/DejaVuSans.ttf";

        [SetUp]
        public void ImportFontAndResetTapSeam()
        {
            // #291: the overlay binds a bundled UI font via Resources.Load; force
            // its import so a fresh CI Library resolves it before the overlay builds.
            AssetDatabase.ImportAsset(BundledFontPath, ImportAssetOptions.ForceSynchronousImport);

            // The pointer-over-UI seam is process-global (#422); restore the
            // production default so a prior test's override can't leak in and
            // swallow our world tap.
            TapRouter.IsPointerOverUi = TapRouter.DefaultIsPointerOverUi;

            // #544: the modal seam and its shared gate are also process-global.
            // RouteTap short-circuits while a modal is registered, so restore
            // the production seam and clear the gate — otherwise a profile a
            // prior test opened swallows this fixture's house tap.
            TapRouter.IsModalOpen = TapRouter.DefaultIsModalOpen;
            Doggiehood.Core.Cameras.ModalInputGate.Shared.Clear();
        }

        [TearDown]
        public void RestoreTapSeam()
        {
            TapRouter.IsPointerOverUi = TapRouter.DefaultIsPointerOverUi;
        }

        [Test]
        public void TapRaycast_OnAVacantHousesBody_OpensItsProfile()
        {
            // #456: a house just built on an empty lot is vacant and, before the
            // fix, its profile-open event had no subscriber — so a real camera-ray tap
            // reached the HouseView but nothing opened. Drive the actual
            // ExpansionDirector build path, wired to the profile exactly as
            // WorldBootstrap wires it (its onHouseBuilt callback), then tap the
            // resulting house through TapRouter.
            var state = GameState.CreateNew();
            state.Wallet.Deposit(150); // 100 to unlock the first zone + 50 to build a house
            state.SetTargetMap(FrontierEditModeWorld.LoadTargetMap());
            state.TryUnlockTile(FrontierEditModeWorld.FirstTile);

            var worldRoot = WorldBuilder.Build(state);
            var canvasHost = new GameObject("ui-canvas", typeof(Canvas));
            canvasHost.AddComponent<UiCanvas>().Configure();

            var overlay = BuildOverlay(canvasHost);

            var dialogHost = new GameObject("dialog");
            dialogHost.transform.SetParent(canvasHost.transform, false);
            var dialog = dialogHost.AddComponent<ConfirmationDialog>();
            dialog.Init();

            var directorHost = new GameObject("expansion-director-host");
            directorHost.transform.SetParent(worldRoot.transform);
            var director = directorHost.AddComponent<ExpansionDirector>();

            // Wire the built house to the profile exactly as WorldBootstrap does
            // via ExpansionDirector's onHouseBuilt callback (the mirror of the
            // HouseUpgradeDirector rebuild callback).
            director.Init(state, worldRoot.transform, dialog,
                built => built.ProfileRequested += () => OpenHouseProfile(overlay, state, built.HouseId));

            try
            {
                var lotView = worldRoot.GetComponentsInChildren<EmptyLotView>().First();
                var houseId = lotView.HouseId;

                lotView.OnTapped();
                dialog.YesButton.onClick.Invoke();

                var houseView = worldRoot.GetComponentsInChildren<HouseView>()
                    .Single(h => h.HouseId == houseId);
                Assert.That(state.Houses.Single(h => h.Id == houseId).IsVacant, Is.True,
                    "sanity: a freshly built lot house is vacant");

                var routed = RouteTapAtHouse(houseView, new Vector3(0f, 0f, 600f));

                Assert.That(routed, Is.True,
                    "a raycast tap at a just-built empty house must hit its collider and route to its HouseView");
                Assert.That(overlay.IsOpen, Is.True,
                    "tapping a vacant (just-built) house must open its profile");
                Assert.That(overlay.CurrentHouse.IsVacant, Is.True,
                    "the opened profile is the vacant house's");
                Assert.That(overlay.Residents, Is.Empty,
                    "a vacant house shows no resident rows");
            }
            finally
            {
                Object.DestroyImmediate(canvasHost);
                Object.DestroyImmediate(worldRoot);
            }
        }

        [Test]
        public void TapRaycast_OnAnOccupiedHousesBody_OpensItsProfile()
        {
            // Companion lock for the occupied path: a starter house (never
            // vacant, wired at bootstrap) opens its profile with residents
            // populated. Built directly via WorldBuilder.BuildHouse and wired
            // exactly as WorldBootstrap's startup foreach does.
            var state = GameState.CreateNew();
            var worldRoot = new GameObject("world");
            var canvasHost = new GameObject("ui-canvas", typeof(Canvas));
            canvasHost.AddComponent<UiCanvas>().Configure();
            var overlay = BuildOverlay(canvasHost);

            var house = state.Houses.First(h => !h.IsVacant);
            var houseRoot = WorldBuilder.BuildHouse(worldRoot.transform, house);
            var houseView = houseRoot.GetComponent<HouseView>();
            houseView.ProfileRequested += () => OpenHouseProfile(overlay, state, house.Id);

            try
            {
                Assert.That(state.Dogs.Any(d => d.HouseId == house.Id), Is.True,
                    "sanity: the chosen occupied starter house has residents");

                var routed = RouteTapAtHouse(houseView, new Vector3(0f, 0f, -600f));

                Assert.That(routed, Is.True,
                    "a raycast tap at an occupied house must hit its collider and route to its HouseView");
                Assert.That(overlay.IsOpen, Is.True, "tapping an occupied house opens its profile");
                Assert.That(overlay.CurrentHouse.IsVacant, Is.False, "the opened profile is the occupied house's");
                Assert.That(overlay.Residents.Count, Is.GreaterThan(0),
                    "an occupied house's profile lists its resident dog(s)");
            }
            finally
            {
                Object.DestroyImmediate(canvasHost);
                Object.DestroyImmediate(worldRoot);
            }
        }

        // --- helpers ---

        private static HouseProfileOverlay BuildOverlay(GameObject canvasHost)
        {
            var overlayHost = new GameObject("house-profile-overlay");
            overlayHost.transform.SetParent(canvasHost.transform, false);
            var overlay = overlayHost.AddComponent<HouseProfileOverlay>();
            overlay.Init();
            return overlay;
        }

        /// <summary>Places the house alone at <paramref name="isolatedPosition"/>
        /// (away from all other world colliders), points a close orthographic
        /// camera at its model, and routes a tap at its on-screen centre through
        /// <see cref="TapRouter.RouteTap"/> — the house analogue of the dog-tap
        /// regression rig (#148).</summary>
        private static bool RouteTapAtHouse(HouseView houseView, Vector3 isolatedPosition)
        {
            // #568: this models a physical world tap, which in production occurs
            // in its own frame — the prior frame's CameraRig.LateUpdate has
            // already run ModalInputGate.Shared.EndFrame(), clearing the
            // this-frame close latch a preceding UI close (e.g. the build
            // confirmation dialog's Yes button, which unregisters and latches
            // ClosedThisFrame) would otherwise leave set. The EditMode rig has no
            // frame loop, so end the frame here to model that boundary; without
            // it the latch would (correctly, for the closing tap) suppress this
            // genuinely separate house tap.
            Doggiehood.Core.Cameras.ModalInputGate.Shared.EndFrame();

            houseView.transform.position = isolatedPosition;

            var camGo = new GameObject("tap-cam", typeof(Camera));
            var cam = camGo.GetComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 12f;
            var texture = new RenderTexture(1920, 1080, 0);
            cam.targetTexture = texture;
            try
            {
                var target = CombinedRendererBounds(houseView.transform).center;
                cam.transform.position = target + new Vector3(0f, 24f, -24f);
                cam.transform.LookAt(target);
                Physics.SyncTransforms();

                return TapRouter.RouteTap(cam, cam.WorldToScreenPoint(target));
            }
            finally
            {
                cam.targetTexture = null;
                Object.DestroyImmediate(texture);
                Object.DestroyImmediate(camGo);
            }
        }

        /// <summary>Mirror of WorldBootstrap.OpenHouseProfile: resolves the Core
        /// house and its resident dogs from live state and opens the overlay. A
        /// no-op if the house id isn't found.</summary>
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

            var residents = new List<Dog>();
            foreach (var dog in state.Dogs)
            {
                if (dog.HouseId == houseId)
                {
                    residents.Add(dog);
                }
            }

            overlay.Open(house, residents);
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
