using System.Linq;
using Doggiehood.Core.Art;
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

        // #660: the measured kit body is a renderer-bounds AABB brought back into
        // the truck root's local space, so it is allowed to differ from Core's
        // nominal figure by a little without meaning anything. This tolerance is
        // wide enough to absorb that and still catch what matters — a different
        // kit model, or a ModelScale change that skipped the Core constraint.
        private const float NominalBodyLengthTolerance = 0.25f;

        // #672: the truck steps toward its lane line at a fixed speed per tick, so
        // a sampled position can sit a fraction of a step off the exact lane
        // centre. Wide enough to absorb that, far narrower than the 1.5m lane
        // offset it guards.
        private const float LaneTolerance = 0.01f;

        private static Road NorthSouthRoad()
        {
            return NeighborhoodLayout.Roads.First(r => r.Orientation == StreetOrientation.NorthSouth);
        }

        // The north-south road's crosswalk on the +Z side of the origin — the one
        // the truck meets just beyond its NE delivery stop.
        private static WalkEdge NorthCrosswalkOn(Road road)
        {
            return NeighborhoodLayout.WalkNetwork.Edges.Single(e =>
                e.Kind == WalkEdgeKind.Crosswalk
                && Mathf.Abs(((e.A.X + e.B.X) / 2f) - road.Center.X) < 0.01f
                && ((e.A.Z + e.B.Z) / 2f) > 0f);
        }

        private static GridPoint Midpoint(WalkEdge edge)
        {
            return new GridPoint((edge.A.X + edge.B.X) / 2f, (edge.A.Z + edge.B.Z) / 2f);
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
            // #601: the per-spawn car-color pick keeps a process-static in-use
            // color set. EditMode doesn't reliably fire OnDestroy on
            // DestroyImmediate, so trucks from earlier tests can leave colors
            // reserved and fill the set — collapsing the "distinct from active"
            // guarantee that TwoConcurrentTrucks_GetDistinctCarColors relies on.
            // Reset it so each case starts from an empty pool.
            DeliveryTruckView.ResetSpawnColorStateForTests();
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
        public void TruckKeepsToItsRightHandLane_ForItsEntireRoute()
        {
            // #672: the truck used to drive straddling the centre line the whole
            // way down the street, because every road position it could express
            // WAS the centerline. Drive the real route and assert two things per
            // tick: it never strays onto the wrong side of the centerline for the
            // leg it is driving, and it does actually leave the middle of the road
            // — reaching its lane centre, a quarter of the road width over.
            var root = new GameObject("truck-test-root");
            try
            {
                var truck = DeliveryTruckView.Spawn(root.transform);
                var door = new Vector3(
                    NeighborhoodLayout.LotDistanceFromCenter, 0f, NeighborhoodLayout.LotDistanceFromCenter);

                truck.DeliverTo(door, OriginMap(), OriginNetwork(), () => { });

                var reachedItsLaneCentre = false;

                for (var step = 0; step < 4000 && !truck.IsGone; step++)
                {
                    if (truck.IsDriving)
                    {
                        // The wrong-side test. A leg handover at an intersection
                        // hands the truck over at the NEW leg's centerline (the old
                        // leg's lane point sits on the new road's own axis), so it
                        // moves from 0 out to its lane offset and never through it.
                        Assert.That(truck.CurrentLateral * truck.LaneOffset,
                            Is.GreaterThanOrEqualTo(-LaneTolerance),
                            "the truck crossed to the oncoming side of the centerline");

                        Assert.That(Mathf.Abs(truck.CurrentLateral),
                            Is.LessThanOrEqualTo(RoadLane.Offset + LaneTolerance),
                            "the truck drifted outside its own lane");

                        if (Mathf.Abs(Mathf.Abs(truck.CurrentLateral) - RoadLane.Offset) < LaneTolerance)
                        {
                            reachedItsLaneCentre = true;
                        }
                    }

                    Assert.That(OnAnyRoad(new GridPoint(truck.transform.position.x, truck.transform.position.z)),
                        Is.True, "keeping right must not push the truck off the roadway (#538)");

                    truck.Tick(0.05f);
                }

                Assert.That(reachedItsLaneCentre, Is.True,
                    "the truck must actually drive its lane, not the centre of the road");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void TruckDelivers_FromItsOwnLane_NotFromTheMiddleOfTheRoad()
        {
            // #672: the delivery stop is still the road point nearest the door,
            // but the truck parks there in its lane. A door on the far side of the
            // street does NOT pull the truck across the centerline — the package
            // is carried to the door, the truck stays where it belongs.
            var root = new GameObject("truck-test-root");
            try
            {
                var truck = DeliveryTruckView.Spawn(root.transform);
                var door = new Vector3(
                    NeighborhoodLayout.LotDistanceFromCenter, 0f, NeighborhoodLayout.LotDistanceFromCenter);

                truck.DeliverTo(door, OriginMap(), OriginNetwork(), () => { });

                for (var step = 0; step < 4000 && !truck.HasDelivered && !truck.IsGone; step++)
                {
                    truck.Tick(0.05f);
                }

                Assert.That(truck.HasDelivered, Is.True, "the truck must reach its delivery stop");
                Assert.That(Mathf.Abs(truck.CurrentLateral), Is.EqualTo(RoadLane.Offset).Within(LaneTolerance),
                    "the truck delivers from its lane centre, not straddling the middle of the street");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void CrosswalkYield_StopsTheTruckAtTheSameAlongCoordinate_WithTheLaneOffsetApplied()
        {
            // #639/#658 regression guard for #672: crosswalk claims and setbacks
            // are ALONG-road quantities, so shifting the truck sideways into its
            // lane must not move where it stops. Hold the band the truck meets
            // beyond its stop and assert it halts with its bumper the stop gap
            // short of the band's near edge — while sitting in its lane.
            var road = NorthSouthRoad();
            var band = NorthCrosswalkOn(road);
            var bandAlong = road.AlongAxis(Midpoint(band));

            var root = new GameObject("truck-test-root");
            try
            {
                var truck = DeliveryTruckView.Spawn(root.transform);
                var dog = new object();
                Assert.That(RoadCrossingGate.Shared.TryEnter(band, dog), Is.True);

                // A door far up the north arm, so the truck's route must run
                // THROUGH the held band rather than stopping before reaching it.
                var door = new Vector3(NeighborhoodLayout.LotDistanceFromCenter, 0f, 28f);
                truck.DeliverTo(door, OriginMap(), OriginNetwork(), () => { });

                for (var step = 0; step < 4000 && !truck.IsGone; step++)
                {
                    truck.Tick(0.05f);
                }

                Assert.That(truck.IsGone, Is.False,
                    "a held crosswalk on its route must hold the truck, not let it drive through");

                var stoppedAlong = road.AlongAxis(
                    new GridPoint(truck.transform.position.x, truck.transform.position.z));
                var bumperAlong = stoppedAlong + truck.TravelSign * truck.CrosswalkFrontSetback;
                var nearEdgeAlong = bandAlong - truck.TravelSign * (WorldDimensions.CrosswalkWidth / 2f);

                Assert.That((nearEdgeAlong - bumperAlong) * truck.TravelSign,
                    Is.GreaterThanOrEqualTo(-LaneTolerance),
                    "the bumper must stay behind the band's near edge — unchanged by the lane offset");
                Assert.That(Mathf.Abs(truck.CurrentLateral), Is.EqualTo(RoadLane.Offset).Within(LaneTolerance),
                    "and it waits in its own lane");
            }
            finally
            {
                RoadCrossingGate.Shared.Clear();
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
        public void Spawn_TintsTheModelWithAStandardCarColor()
        {
            // #601: a spawned truck picks one of the curated standard car colors
            // (Palette.CarColorHex) and applies it as a material color-multiply
            // over the kit model's renderers — the same ApplyPaletteTint path the
            // houses use — so trucks aren't all identical.
            var root = new GameObject("truck-test-root");
            try
            {
                var truck = DeliveryTruckView.Spawn(root.transform);
                var model = truck.transform.Find("Model");
                Assert.That(model, Is.Not.Null);

                Assert.That(truck.CarColorHex, Is.EqualTo(Palette.CarColorHex(truck.CarColorIndex)),
                    "the exposed hex matches the assigned car-color index");

                var expected = CoreColors.FromHex(truck.CarColorHex);
                var renderers = model.GetComponentsInChildren<Renderer>();
                Assert.That(renderers, Is.Not.Empty, "the kit model contributes renderers to tint");
                foreach (var renderer in renderers)
                {
                    Assert.That(renderer.sharedMaterial.color, Is.EqualTo(expected),
                        "each model renderer carries the assigned car color as a material multiply");
                }
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void TwoConcurrentTrucks_GetDistinctCarColors()
        {
            // #601 (optional rule): while both trucks are on the road at once,
            // the second draws a color distinct from the first's so they don't
            // look identical.
            var root = new GameObject("truck-test-root");
            try
            {
                var first = DeliveryTruckView.Spawn(root.transform);
                var second = DeliveryTruckView.Spawn(root.transform);

                Assert.That(second.CarColorHex, Is.Not.EqualTo(first.CarColorHex),
                    "two concurrent trucks pick distinct car colors");
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
        public void CrosswalkFrontSetback_IsDerivedFromTheMeasuredBodyLength_PlusAStopGap()
        {
            // #639: the truck yields at its FRONT BUMPER, so it needs to know how
            // far its pivot sits behind that bumper. That distance is MEASURED off
            // the spawned kit body (its renderer bounds, already at ModelScale) —
            // never a hand-tuned literal (#161) — so it tracks the real model.
            var root = new GameObject("truck-test-root");
            try
            {
                var truck = DeliveryTruckView.Spawn(root.transform);

                Assert.That(truck.BodyLength, Is.GreaterThan(0f),
                    "the truck measures its own body length from the spawned kit model");
                Assert.That(truck.CrosswalkFrontSetback, Is.GreaterThan(truck.BodyLength / 2f),
                    "the setback is the pivot-to-bumper half body PLUS a visible stop gap");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void CrosswalkRearSetback_IsHalfTheMeasuredBody_AndPairsWithTheFrontInsideTheBudget()
        {
            // #658: the release mirror of the test above. The truck hands a
            // crosswalk back at its TAIL, so it needs its pivot-to-tail distance
            // too — drawn from the same measured body (#161), and the two
            // together must still fit the #660 budget on the real spawned truck,
            // not just on the Core nominal figure.
            var root = new GameObject("truck-test-root");
            try
            {
                var truck = DeliveryTruckView.Spawn(root.transform);

                Assert.That(truck.CrosswalkRearSetback,
                    Is.EqualTo(truck.BodyLength / 2f).Within(0.0001f),
                    "the rear setback is the pivot-to-tail half body, with no stop gap");
                Assert.That(truck.CrosswalkFrontSetback + truck.CrosswalkRearSetback,
                    Is.LessThan(DeliveryTruckFootprint.ClearGapBetweenCrosswalkBands),
                    "both setbacks must fit between an intersection's two bands, or the truck "
                    + "holds both at once and two oncoming trucks wedge permanently (#658/#660)");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void MeasuredBodyLength_MatchesTheCoreNominal_AndFitsBetweenTheCrosswalkBands()
        {
            // #660: the Core constraint is checked against
            // DeliveryTruckFootprint.NominalBodyLength, a PREDICTION (the model's
            // imported length times ModelScale). This is the test that ties that
            // prediction to the body the truck actually spawns, so swapping
            // delivery.fbx for a longer kit model can't quietly invalidate the
            // Core proof — the deadlock it guards against is permanent and takes
            // any crossing dog down with it (#658).
            var root = new GameObject("truck-test-root");
            try
            {
                var truck = DeliveryTruckView.Spawn(root.transform);

                Assert.That(truck.BodyLength,
                    Is.EqualTo(DeliveryTruckFootprint.NominalBodyLength).Within(NominalBodyLengthTolerance),
                    "the measured kit body must match what Core predicts from the imported "
                    + "model length and ModelScale");
                Assert.That(DeliveryTruckFootprint.FitsBetweenCrosswalkBands(truck.BodyLength), Is.True,
                    "the truck's front and rear setbacks together must fit in the clear gap between "
                    + "an intersection's two crosswalk bands, or two oncoming trucks deadlock");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void GrayboxFallbackBody_AlsoFitsBetweenTheCrosswalkBands()
        {
            // #660: the fallback path is a real runtime path (a kit model that
            // fails to load), so it is bound by the same constraint.
            Assert.That(DeliveryTruckFootprint.FitsBetweenCrosswalkBands(DeliveryTruckView.FallbackScale.z),
                Is.True, "the graybox footprint must satisfy the same fits-between-the-bands rule");
        }

        [Test]
        public void TruckStillReachesItsDeliveryStop_WithMarginBeforeTheCrosswalkBoundary()
        {
            // #639/#660: the truck's stop is the road point nearest the door, and
            // the next crosswalk band lies BEYOND it. The front setback pushes the
            // point at which a held band stops the truck back up the road toward
            // that stop — so a big enough setback would strand the truck short of
            // its own delivery. Assert the remaining margin, and prove it
            // behaviourally by holding the band for the whole run.
            var road = NorthSouthRoad();
            var band = NorthCrosswalkOn(road);
            var route = DeliveryTruckRoute.ToDoor(
                OriginMap(),
                new GridPoint(NeighborhoodLayout.LotDistanceFromCenter, NeighborhoodLayout.LotDistanceFromCenter));

            var entryAlong = road.AlongAxis(route.Entry);
            var stopAlong = road.AlongAxis(route.Stop);
            var travelSign = stopAlong < entryAlong ? -1f : 1f;
            var bandAlong = road.AlongAxis(Midpoint(band));

            // Road between the stop and the band's centre, measured forward.
            var gapToBand = (bandAlong - stopAlong) * travelSign;
            Assert.That(gapToBand, Is.GreaterThan(0f),
                "this fixture assumes the band lies beyond the delivery stop");

            var root = new GameObject("truck-test-root");
            try
            {
                var truck = DeliveryTruckView.Spawn(root.transform);

                // The truck halts with its bumper a stop gap short of the band's
                // near edge, i.e. half a band plus its own front setback back from
                // the band centre. Whatever is left over is the delivery margin.
                var margin = gapToBand - (WorldDimensions.CrosswalkWidth / 2f + truck.CrosswalkFrontSetback);
                Assert.That(margin, Is.GreaterThan(0f),
                    "the truck must reach its delivery stop before a held crosswalk can halt it");
                Assert.That(margin, Is.GreaterThan(truck.BodyLength / 2f),
                    "and with real margin — more than half a truck — not by a hair");

                // Behavioural half: a dog holds the band for the entire run, so the
                // truck is stopped as early as it ever can be. It must still deliver.
                var dog = new object();
                Assert.That(RoadCrossingGate.Shared.TryEnter(band, dog), Is.True);

                truck.DeliverTo(
                    new Vector3(
                        NeighborhoodLayout.LotDistanceFromCenter, 0f, NeighborhoodLayout.LotDistanceFromCenter),
                    OriginMap(), OriginNetwork(), () => { });

                for (var step = 0; step < 3000 && !truck.HasDelivered && !truck.IsGone; step++)
                {
                    truck.Tick(0.05f);
                }

                Assert.That(truck.HasDelivered, Is.True,
                    "the truck delivers even while the crosswalk beyond its stop is held");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void BodyLength_FallsBackToTheGrayboxFootprint_WhenTheModelCannotLoad()
        {
            // #639: on the graybox path the body IS the fallback cube, so its
            // footprint length is the measurement the setback derives from.
            var root = new GameObject("truck-test-root");
            try
            {
                DeliveryTruckView.ForcePrimitiveFallback = true;
                var truck = DeliveryTruckView.Spawn(root.transform);

                Assert.That(truck.BodyLength, Is.EqualTo(DeliveryTruckView.FallbackScale.z).Within(0.0001f),
                    "the graybox path measures the pre-#547 footprint's own length");
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

        // ---------------------------------------------------------------
        // #703: the delivered package is TRANSIENT — it shows for a short
        // beat and then removes itself, so no white cube piles up in a
        // doorway swallowing every later tap on that door.
        // ---------------------------------------------------------------

        /// <summary>Drives a freshly spawned truck to <paramref name="door"/> and
        /// returns the package it drops there.</summary>
        private static PackageView DeliverAPackage(GameObject root, Vector3 door)
        {
            var truck = DeliveryTruckView.Spawn(root.transform);
            truck.DeliverTo(door, OriginMap(), OriginNetwork(), () => { });

            for (var step = 0; step < 2000 && !truck.HasDelivered; step++)
            {
                truck.Tick(0.05f);
            }

            var package = root.transform.Find("Package");
            Assert.That(package, Is.Not.Null, "package left at the door");
            return package.GetComponent<PackageView>();
        }

        /// <summary>Steps a package's own beat by <paramref name="seconds"/>, in
        /// frame-sized slices, stopping if it removes itself along the way.</summary>
        private static void TickPackage(PackageView package, float seconds)
        {
            const float frame = 0.05f;
            for (var elapsed = 0f; elapsed < seconds && package != null; elapsed += frame)
            {
                package.Tick(frame);
            }
        }

        [Test]
        public void DroppedPackage_IsStillAtTheDoor_BeforeItsBeatElapses()
        {
            // #703 guard: the beat must have a visible front half — a fix that
            // removes the box instantly (or never drops one) fails here.
            var root = new GameObject("truck-test-root");
            try
            {
                var package = DeliverAPackage(root, new Vector3(14f, 0f, 14f));

                TickPackage(package, DeliveredPackageLifetime.VisibleSeconds / 2f);

                Assert.That(package != null, Is.True, "the package must still be there mid-beat");
                Assert.That(root.transform.Find("Package"), Is.Not.Null,
                    "the dropped package is visible while its beat runs");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void DroppedPackage_IsGone_OnceItsBeatElapses()
        {
            // #703: the box never disappeared at all — one cube per delivery
            // stayed in the doorway for the rest of the session.
            var root = new GameObject("truck-test-root");
            try
            {
                var package = DeliverAPackage(root, new Vector3(14f, 0f, 14f));

                TickPackage(package, DeliveredPackageLifetime.VisibleSeconds * 2f);

                Assert.That(root.transform.Find("Package"), Is.Null,
                    "no package GameObject may remain at the door after its beat");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void Package_OutlivesItsTruck_ButStillRemovesItselfWhenTheBeatElapses()
        {
            // #703: the package is parented to the world root, not the truck, so
            // the truck's teardown can neither take it with it nor strand it —
            // the lifetime is owned by the package.
            var root = new GameObject("truck-test-root");
            try
            {
                var truck = DeliveryTruckView.Spawn(root.transform);
                truck.DeliverTo(new Vector3(14f, 0f, 14f), OriginMap(), OriginNetwork(), () => { });

                for (var step = 0; step < 2000 && !truck.HasDelivered; step++)
                {
                    truck.Tick(0.05f);
                }

                var package = root.transform.Find("Package").GetComponent<PackageView>();
                TickPackage(package, DeliveredPackageLifetime.VisibleSeconds / 2f);

                // The truck vanishes mid-beat (its route ends, it is torn down).
                Object.DestroyImmediate(truck.gameObject);
                Assert.That(package != null, Is.True, "the package outlives the truck that dropped it");

                TickPackage(package, DeliveredPackageLifetime.VisibleSeconds);

                Assert.That(root.transform.Find("Package"), Is.Null,
                    "a package whose truck is gone still removes itself when its beat elapses");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void TwoDeliveriesToTheSameDoor_LeaveTheDoorwayClear()
        {
            // #703: repeat deliveries to the same house stacked cubes at the
            // same point. After both beats the doorway must be empty.
            var root = new GameObject("truck-test-root");
            try
            {
                var door = new Vector3(14f, 0f, 14f);

                var first = DeliverAPackage(root, door);
                TickPackage(first, DeliveredPackageLifetime.VisibleSeconds * 2f);

                var second = DeliverAPackage(root, door);
                TickPackage(second, DeliveredPackageLifetime.VisibleSeconds * 2f);

                Assert.That(root.GetComponentsInChildren<PackageView>(), Is.Empty,
                    "two deliveries to one house leave no package behind");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void ConcurrentPackages_EachRunTheirOwnBeat()
        {
            // #600/#703: more than one delivery can be in flight, so two packages
            // can sit at two doors at once. One package's beat must not remove
            // the other's, and each must go when its own beat elapses.
            var root = new GameObject("truck-test-root");
            try
            {
                var first = DeliverAPackage(root, new Vector3(14f, 0f, 14f));
                first.name = "Package A";
                var second = DeliverAPackage(root, new Vector3(-14f, 0f, 14f));
                second.name = "Package B";

                TickPackage(first, DeliveredPackageLifetime.VisibleSeconds * 2f);

                Assert.That(first == null, Is.True, "the first package's own beat removed it");
                Assert.That(second != null, Is.True,
                    "one delivery's beat must not remove another delivery's package");

                TickPackage(second, DeliveredPackageLifetime.VisibleSeconds * 2f);

                Assert.That(root.GetComponentsInChildren<PackageView>(), Is.Empty,
                    "each package goes when its own beat elapses");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void AfterItsBeat_ATapAtTheDoor_NoLongerResolvesToAPackage()
        {
            // #703 invariant — nothing inert blocks a tap. Hiding the cube's
            // renderer while leaving its collider would satisfy "the box is
            // gone" on screen and still eat every tap on that doorway, so this
            // asserts through a real TapRouter raycast rather than renderer
            // state. Mirrors RouteTap_AtThePackage_RoutesToItsOnTapped's rig.
            var previousPointerOverUi = TapRouter.IsPointerOverUi;
            TapRouter.IsPointerOverUi = TapRouter.DefaultIsPointerOverUi;

            var root = new GameObject("truck-test-root");
            var camGo = new GameObject("tap-cam", typeof(Camera));
            var texture = new RenderTexture(1920, 1080, 0);
            try
            {
                var package = DeliverAPackage(root, new Vector3(14f, 0f, 14f));

                // Isolate the doorway away from any other world colliders.
                package.transform.position = new Vector3(0f, 0f, 600f);
                var doorway = package.transform.position;

                var cam = camGo.GetComponent<Camera>();
                cam.orthographic = true;
                cam.orthographicSize = 12f;
                cam.targetTexture = texture;
                cam.transform.position = doorway + new Vector3(0f, 24f, -24f);
                cam.transform.LookAt(doorway);
                Physics.SyncTransforms();

                Assert.That(TapRouter.RouteTap(cam, cam.WorldToScreenPoint(doorway)), Is.True,
                    "while its beat runs the package is a real tap target");

                TickPackage(package, DeliveredPackageLifetime.VisibleSeconds * 2f);
                Physics.SyncTransforms();

                Assert.That(TapRouter.RouteTap(cam, cam.WorldToScreenPoint(doorway)), Is.False,
                    "after removal a tap at the doorway must not resolve to a PackageView");
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
