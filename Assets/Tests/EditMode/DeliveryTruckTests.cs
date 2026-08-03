using Doggiehood.Core.World;
using Doggiehood.Unity;
using NUnit.Framework;
using UnityEngine;

namespace Doggiehood.Unity.EditModeTests
{
    public class DeliveryTruckTests
    {
        [SetUp]
        public void ResetModalGate()
        {
            // #544: RouteTap short-circuits while any modal is registered on the
            // process-global gate. Restore the production modal seam and clear
            // the shared gate so a modal a prior test opened can't leave the gate
            // blocking and swallow this fixture's package tap.
            TapRouter.IsModalOpen = TapRouter.DefaultIsModalOpen;
            Doggiehood.Core.Cameras.ModalInputGate.Shared.Clear();
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

                truck.DeliverTo(door, () => { });

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

                truck.DeliverTo(housePosition, () => delivered++);

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

                truck.DeliverTo(doorTarget, () => { });

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
                truck.DeliverTo(new Vector3(14f, 0f, 14f), () => { });

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
                truck.DeliverTo(new Vector3(14f, 0f, 14f), () => { });

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
