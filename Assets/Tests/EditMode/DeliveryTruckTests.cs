using Doggiehood.Core.World;
using Doggiehood.Unity;
using NUnit.Framework;
using UnityEngine;

namespace Doggiehood.Unity.EditModeTests
{
    public class DeliveryTruckTests
    {
        // #599: the truck now routes over the LIVE map. These fixtures run on the
        // starting FourWay origin tile, whose derived roads match the classic
        // NeighborhoodLayout.Roads geometry, and its walk network (for crosswalk
        // yielding). Delivering to a lot door still enters off-map and stays on
        // the roadway, exactly as the on-device starting neighborhood does.
        private static TileMap OriginMap()
        {
            return new TileMap(new TileCoordinate(0, 0), TileType.FourWay);
        }

        private static WalkNetwork OriginNetwork()
        {
            return NeighborhoodLayout.WalkNetwork;
        }

        [SetUp]
        public void ResetModalGate()
        {
            // #544: RouteTap short-circuits while any modal is registered on the
            // process-global gate. Restore the production modal seam and clear
            // the shared gate so a modal a prior test opened can't leave the gate
            // blocking and swallow this fixture's package tap.
            TapRouter.IsModalOpen = TapRouter.DefaultIsModalOpen;
            Doggiehood.Core.Cameras.ModalInputGate.Shared.Clear();
            // #546: the truck yields at crosswalks via a process-global gate;
            // clear it so a stray claim can't stall the truck's route mid-test.
            RoadCrossingGate.Shared.Clear();
        }

        [TearDown]
        public void ResetFallbackSeam()
        {
            // #547: the graybox-fallback seam is static; never leave it forced
            // on for the next test (which expects the real model to load).
            DeliveryTruckView.ForcePrimitiveFallback = false;
        }

        [Test]
        public void TruckStaysOnTheRoadway_ForItsEntireRoute_AndStopsShortOfTheDog()
        {
            // #538 invariant: a delivery truck never leaves the roadway, and it
            // stops short of the dog waiting at the door instead of driving into
            // it. Drive the animation and assert every position the truck holds
            // is on a road, and it never overlaps the door.
            var root = new GameObject("truck-test-root");
            try
            {
                var truck = DeliveryTruckView.Spawn(root.transform);
                var door = new Vector3(
                    NeighborhoodLayout.LotDistanceFromCenter, 0f, NeighborhoodLayout.LotDistanceFromCenter);

                truck.DeliverTo(door, OriginMap(), OriginNetwork(), () => { });

                var clearance = WorldDimensions.RoadWidth / 2f
                                + WorldDimensions.GrassVergeWidth
                                + WorldDimensions.SidewalkWidth;
                var closestToDoor = float.MaxValue;

                for (var step = 0; step < 4000 && !truck.IsGone; step++)
                {
                    var pos = truck.transform.position;
                    var point = new GridPoint(pos.x, pos.z);
                    Assert.That(OnAnyRoad(point), Is.True,
                        $"truck left the roadway at {point}");

                    var flatDoor = new Vector3(door.x, pos.y, door.z);
                    closestToDoor = Mathf.Min(closestToDoor, Vector3.Distance(pos, flatDoor));

                    truck.Tick(0.05f);
                }

                Assert.That(closestToDoor, Is.GreaterThan(clearance),
                    "truck must stop short of the waiting dog at the door, never overlapping it");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void DeliveryModelResource_Resolves()
        {
            // #547: the swap depends on the staged Car Kit "delivery" model
            // loading by name (Assets/Art/Vehicles/CarKit/Resources/delivery.fbx),
            // exactly like the road/house kit models. Guard that the asset is
            // present and loadable so the model path — not the fallback — is the
            // one that renders in the neighborhood.
            var model = Resources.Load<GameObject>("delivery");
            Assert.That(model, Is.Not.Null,
                "Resources.Load<GameObject>(\"delivery\") must resolve the staged Car Kit model");
        }

        [Test]
        public void Spawn_RendersTheKitModel_NotAPrimitiveCube()
        {
            // #547: Spawn must instantiate the staged "delivery" model under the
            // truck root, not build a graybox primitive cube.
            var root = new GameObject("truck-test-root");
            try
            {
                var truck = DeliveryTruckView.Spawn(root.transform);

                var model = truck.transform.Find("Model");
                Assert.That(model, Is.Not.Null, "the truck must carry the kit model as a 'Model' child");
                Assert.That(truck.transform.Find("Graybox"), Is.Null,
                    "no graybox fallback should be built when the model loads");
                Assert.That(truck.GetComponent<MeshFilter>(), Is.Null,
                    "the truck root must not itself be a primitive cube");
                Assert.That(model.GetComponentInChildren<Renderer>(), Is.Not.Null,
                    "the kit model must contribute a renderer");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void SpawnedModel_IsUniformlyScaledForTheNeighborhood()
        {
            // #547: the truck reads at a believable size next to the houses — a
            // single uniform kit scale, mirroring HouseKitScale's discipline.
            var root = new GameObject("truck-test-root");
            try
            {
                var truck = DeliveryTruckView.Spawn(root.transform);
                var model = truck.transform.Find("Model");
                Assert.That(model, Is.Not.Null);

                Assert.That(model.localScale,
                    Is.EqualTo(Vector3.one * DeliveryTruckView.ModelScale),
                    "the kit model must be uniformly scaled by DeliveryTruckView.ModelScale");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void Spawn_FallsBackToGrayboxCube_WhenModelCannotLoad()
        {
            // #547: same fallback discipline the road/house kit loaders use — when
            // the model can't load, spawn the original graybox cube at its
            // pre-#547 (1.4, 1.4, 2.6) footprint so the truck is never invisible.
            var root = new GameObject("truck-test-root");
            try
            {
                DeliveryTruckView.ForcePrimitiveFallback = true;
                var truck = DeliveryTruckView.Spawn(root.transform);

                var graybox = truck.transform.Find("Graybox");
                Assert.That(graybox, Is.Not.Null, "the graybox fallback cube must spawn when the model can't load");
                Assert.That(truck.transform.Find("Model"), Is.Null, "no kit model should be built on the fallback path");
                Assert.That(graybox.GetComponent<MeshFilter>(), Is.Not.Null, "the fallback must be a primitive cube");
                Assert.That(graybox.localScale, Is.EqualTo(DeliveryTruckView.FallbackScale),
                    "the fallback keeps the pre-#547 graybox footprint");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static bool OnAnyRoad(GridPoint point)
        {
            foreach (var road in NeighborhoodLayout.Roads)
            {
                if (road.Contains(point))
                {
                    return true;
                }
            }

            return false;
        }

        [Test]
        public void TruckDrivesIn_Delivers_AndDrivesAwayAgain()
        {
            // #30: the truck animates to the house and away after delivering.
            var root = new GameObject("truck-test-root");
            try
            {
                var truck = DeliveryTruckView.Spawn(root.transform);
                var delivered = 0;
                var housePosition = new Vector3(14f, 0f, 14f);

                truck.DeliverTo(housePosition, OriginMap(), OriginNetwork(), () => delivered++);

                var reachedHouse = false;
                for (var step = 0; step < 2000 && !truck.IsGone; step++)
                {
                    truck.Tick(0.05f);
                    if (truck.HasDelivered && !reachedHouse)
                    {
                        reachedHouse = true;
                        Assert.That(delivered, Is.EqualTo(1), "delivery callback fires exactly once, at the door");
                    }
                }

                Assert.That(reachedHouse, Is.True, "truck never reached the house");
                Assert.That(truck.IsGone, Is.True, "truck never drove away");
                Assert.That(root.transform.Find("Package"), Is.Not.Null, "package left at the door");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void DeliverTo_DropsThePackageExactlyAtTheDoorTarget_NoScaling()
        {
            // #471: DeliverTo used to rescale the door position by * 0.35f / * 0.8f
            // — a leftover from before front-walkway routing, when the caller
            // passed a lot-center. WalkDogHome now passes the dog's actual door
            // node, so the package must land there unscaled (a fraction of the
            // input would drop it away from the sitting dog).
            var root = new GameObject("truck-test-root");
            try
            {
                var truck = DeliveryTruckView.Spawn(root.transform);
                var doorTarget = new Vector3(14f, 0f, 14f);

                truck.DeliverTo(doorTarget, OriginMap(), OriginNetwork(), () => { });

                for (var step = 0; step < 2000 && !truck.HasDelivered; step++)
                {
                    truck.Tick(0.05f);
                }

                var package = root.transform.Find("Package");
                Assert.That(package, Is.Not.Null, "package left at the door");
                Assert.That(package.position.x, Is.EqualTo(doorTarget.x).Within(0.0001f),
                    "package X must equal the door target X, not a scaled fraction of it");
                Assert.That(package.position.z, Is.EqualTo(doorTarget.z).Within(0.0001f),
                    "package Z must equal the door target Z, not a scaled fraction of it");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void DroppedPackage_CarriesAnInteractable_ThatRaisesTappedOnTap()
        {
            // #471: the package was spawned with no IInteractable, so TapRouter
            // (which resolves hit.collider.GetComponentInParent<IInteractable>())
            // silently swallowed taps. Mirror the EmptyLotView/HouseView stub:
            // OnTapped raises Tapped and increments TapCount.
            var root = new GameObject("truck-test-root");
            try
            {
                var truck = DeliveryTruckView.Spawn(root.transform);
                truck.DeliverTo(new Vector3(14f, 0f, 14f), OriginMap(), OriginNetwork(), () => { });

                for (var step = 0; step < 2000 && !truck.HasDelivered; step++)
                {
                    truck.Tick(0.05f);
                }

                var package = root.transform.Find("Package");
                Assert.That(package, Is.Not.Null, "package left at the door");

                var interactable = package.GetComponent<IInteractable>();
                Assert.That(interactable, Is.Not.Null,
                    "the delivered package must carry an IInteractable so TapRouter can route to it");

                var view = package.GetComponent<PackageView>();
                var raised = 0;
                view.Tapped += () => raised++;

                interactable.OnTapped();

                Assert.That(view.TapCount, Is.EqualTo(1), "OnTapped increments TapCount");
                Assert.That(raised, Is.EqualTo(1), "OnTapped raises the Tapped event");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void RouteTap_AtThePackage_RoutesToItsOnTapped()
        {
            // #471: end-to-end — a camera-ray tap at the delivered package must
            // reach its OnTapped (previously swallowed by the null interactable
            // lookup). Mirrors HouseTapRoutingTests' isolated-collider tap rig.
            var previousPointerOverUi = TapRouter.IsPointerOverUi;
            TapRouter.IsPointerOverUi = TapRouter.DefaultIsPointerOverUi;

            var root = new GameObject("truck-test-root");
            var camGo = new GameObject("tap-cam", typeof(Camera));
            var texture = new RenderTexture(1920, 1080, 0);
            try
            {
                var truck = DeliveryTruckView.Spawn(root.transform);
                truck.DeliverTo(new Vector3(14f, 0f, 14f), OriginMap(), OriginNetwork(), () => { });

                for (var step = 0; step < 2000 && !truck.HasDelivered; step++)
                {
                    truck.Tick(0.05f);
                }

                var package = root.transform.Find("Package");
                Assert.That(package, Is.Not.Null, "package left at the door");

                // Isolate the package away from any other world colliders.
                package.position = new Vector3(0f, 0f, 600f);

                var view = package.GetComponent<PackageView>();
                var raised = 0;
                view.Tapped += () => raised++;

                var cam = camGo.GetComponent<Camera>();
                cam.orthographic = true;
                cam.orthographicSize = 12f;
                cam.targetTexture = texture;

                var target = package.position;
                cam.transform.position = target + new Vector3(0f, 24f, -24f);
                cam.transform.LookAt(target);
                Physics.SyncTransforms();

                var routed = TapRouter.RouteTap(cam, cam.WorldToScreenPoint(target));

                Assert.That(routed, Is.True,
                    "a raycast tap at the package must hit its collider and route to its PackageView");
                Assert.That(raised, Is.EqualTo(1), "the tap must reach the package's OnTapped");
            }
            finally
            {
                camGo.GetComponent<Camera>().targetTexture = null;
                Object.DestroyImmediate(texture);
                Object.DestroyImmediate(camGo);
                Object.DestroyImmediate(root);
                TapRouter.IsPointerOverUi = previousPointerOverUi;
            }
        }
    }
}
