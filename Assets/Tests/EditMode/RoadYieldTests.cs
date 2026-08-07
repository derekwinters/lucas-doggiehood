using System.Linq;
using Doggiehood.Core.Dogs;
using Doggiehood.Core.World;
using Doggiehood.Unity;
using NUnit.Framework;
using UnityEngine;

namespace Doggiehood.Unity.EditModeTests
{
    /// <summary>
    /// #546: a vehicle and a dog must never occupy the same point on a
    /// crosswalk. Right-of-way is first-come, mediated by the shared
    /// <see cref="RoadCrossingGate"/>: whoever claims a crosswalk first crosses,
    /// the other waits at its own boundary. These tests drive the thin Unity
    /// wiring (DogView / DeliveryTruckView) that consumes the gate.
    /// </summary>
    public class RoadYieldTests
    {
        private const float HalfCrosswalk = 3f / 2f; // WorldDimensions.CrosswalkWidth / 2

        [SetUp]
        public void ClearSharedGate()
        {
            // The gate is a process-global shared instance; a claim a prior test
            // left behind would wrongly block this fixture.
            RoadCrossingGate.Shared.Clear();
            DeliveryTruckView.ForcePrimitiveFallback = false;
        }

        [TearDown]
        public void ResetGate()
        {
            RoadCrossingGate.Shared.Clear();
            DeliveryTruckView.ForcePrimitiveFallback = false;
        }

        private static Road NorthSouthRoad()
        {
            return NeighborhoodLayout.Roads.First(r => r.Orientation == StreetOrientation.NorthSouth);
        }

        // #599: the truck routes over the LIVE map. The starting FourWay origin
        // tile derives the same road geometry as the classic NeighborhoodLayout,
        // so its NS road carries the same north crosswalk these tests exercise.
        private static TileMap OriginMap()
        {
            return new TileMap(new TileCoordinate(0, 0), TileType.FourWay);
        }

        private static bool ReachedFarCurb(Vector3 dogPosition, GridPoint farCurb)
        {
            return new Vector2(dogPosition.x - farCurb.X, dogPosition.z - farCurb.Z).magnitude < 0.2f;
        }

        private static WalkEdge NorthCrosswalk()
        {
            // The north-south road's crosswalk on the +Z side of the origin,
            // at along = +4.75 (the crossing road's sidewalk offset).
            var road = NorthSouthRoad();
            return NeighborhoodLayout.WalkNetwork.Edges.Single(e =>
                e.Kind == WalkEdgeKind.Crosswalk
                && Mathf.Abs(((e.A.X + e.B.X) / 2f) - road.Center.X) < 0.01f
                && ((e.A.Z + e.B.Z) / 2f) > 0f);
        }

        [Test]
        public void DogHoldsAtTheCurb_WhenItsNextHopCrosswalkIsClaimedByTheTruck_ResumesOnRelease()
        {
            var network = NeighborhoodLayout.WalkNetwork;
            var crosswalk = NorthCrosswalk();

            // The truck (a stand-in occupant on the shared gate) arrives first and
            // claims the crosswalk.
            var truck = new object();
            Assert.That(RoadCrossingGate.Shared.TryEnter(crosswalk, truck), Is.True);

            var dog = new Dog("Rex", Breed.Beagle, Personality.Brave, 1, false);
            dog.PlaceOnStreet();
            var go = new GameObject("dog");
            try
            {
                var view = go.AddComponent<DogView>();
                view.Init(dog, null, () => network);

                // Stand the dog at the curb node (crosswalk endpoint A) and aim its
                // next wander hop straight across the crosswalk to endpoint B.
                go.transform.position = new Vector3(crosswalk.A.X, 0f, crosswalk.A.Z);
                view.BeginWanderHop(crosswalk.B);

                var curb = go.transform.position;
                for (var i = 0; i < 200; i++)
                {
                    view.TickWander(0.1f);
                }

                var heldFlat = new Vector2(go.transform.position.x - curb.x, go.transform.position.z - curb.z);
                Assert.That(heldFlat.magnitude, Is.LessThan(0.01f),
                    "while the truck holds the crosswalk, the dog must hold at the curb and not step onto it");

                // The truck clears the crosswalk; the dog may now cross.
                RoadCrossingGate.Shared.Exit(crosswalk, truck);

                var closestToFarCurb = float.MaxValue;
                for (var i = 0; i < 400; i++)
                {
                    view.TickWander(0.1f);
                    var flat = new Vector2(
                        go.transform.position.x - crosswalk.B.X, go.transform.position.z - crosswalk.B.Z);
                    closestToFarCurb = Mathf.Min(closestToFarCurb, flat.magnitude);
                }

                Assert.That(closestToFarCurb, Is.LessThan(0.2f),
                    "once the gate releases, the dog resumes and crosses to the far curb");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void TruckPausesAtACrosswalkNearEdge_WhenADogHoldsIt_ThenResumesOnRelease()
        {
            var crosswalk = NorthCrosswalk();

            // A dog arrives at the crosswalk first and claims it.
            var dog = new object();
            Assert.That(RoadCrossingGate.Shared.TryEnter(crosswalk, dog), Is.True);

            var root = new GameObject("truck-root");
            try
            {
                var truck = DeliveryTruckView.Spawn(root.transform);
                // Deliver to the NE door: the route drives down the north-south
                // road, crossing the north crosswalk on the way out.
                truck.DeliverTo(new Vector3(
                    NeighborhoodLayout.LotDistanceFromCenter, 0f, NeighborhoodLayout.LotDistanceFromCenter),
                    OriginMap(), NeighborhoodLayout.WalkNetwork, () => { });

                // Near edge on the +Z approach side of the +4.75 crosswalk band.
                var nearEdgeZ = 4.75f + HalfCrosswalk; // 6.25

                // #639: transform.position is the CENTRE of the truck body, so
                // stopping THAT at the near edge leaves the front half hanging
                // over the stripes and clipping the dog. Measure the LEADING
                // EDGE instead — half a body ahead of the pivot, and travelling
                // south the front leads in -Z. Both figures come from the view's
                // own measured body, so this tracks the real model rather than a
                // literal.
                var halfBody = truck.BodyLength / 2f;
                Assert.That(halfBody, Is.GreaterThan(0f),
                    "the spawned truck must expose a measurable body length to yield with");
                var expectedStopZ = nearEdgeZ + truck.CrosswalkFrontSetback;

                var reachedStop = false;
                for (var step = 0; step < 3000 && !truck.IsGone; step++)
                {
                    truck.Tick(0.05f);
                    var z = truck.transform.position.z;
                    var frontBumperZ = z - halfBody;
                    Assert.That(frontBumperZ, Is.GreaterThan(nearEdgeZ - 0.05f),
                        "the truck's FRONT must never overhang a crosswalk band a dog holds");
                    if (Mathf.Abs(z - expectedStopZ) < 0.1f)
                    {
                        reachedStop = true;
                    }
                }

                Assert.That(reachedStop, Is.True,
                    "the truck should have driven up to its stop boundary — a front setback "
                    + "short of the crosswalk's near edge — and paused there");
                Assert.That(truck.IsGone, Is.False, "the truck cannot finish while the dog holds the crosswalk");

                // The dog clears the crosswalk; the truck resumes and completes.
                RoadCrossingGate.Shared.Exit(crosswalk, dog);
                for (var step = 0; step < 4000 && !truck.IsGone; step++)
                {
                    truck.Tick(0.05f);
                }

                Assert.That(truck.IsGone, Is.True,
                    "once the dog clears the crosswalk the truck drives through and finishes its route");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void ADogWaitingAtTheCurb_IsOnlyReleased_OnceTheTrucksTRAILINGEdgeClearsTheBand()
        {
            // #658: the mirror of #639's stop. transform.position is the CENTRE
            // of the truck body, so releasing the crosswalk the moment THAT
            // passes the far edge handed the band back — and let the waiting dog
            // step onto it — while the truck's whole back half was still over the
            // stripes, clipping the dog from behind.
            var network = NeighborhoodLayout.WalkNetwork;
            var crosswalk = NorthCrosswalk();

            // The truck drives south down the north-south road, so it leaves the
            // +4.75 band across the -Z (far) side and its tail trails on the +Z
            // side of its pivot.
            var farEdgeZ = 4.75f - HalfCrosswalk; // 3.25

            var dog = new Dog("Pip", Breed.Beagle, Personality.Brave, 1, false);
            dog.PlaceOnStreet();
            var dogGo = new GameObject("dog");
            var root = new GameObject("truck-root");
            try
            {
                var truck = DeliveryTruckView.Spawn(root.transform);
                truck.DeliverTo(new Vector3(
                    NeighborhoodLayout.LotDistanceFromCenter, 0f, NeighborhoodLayout.LotDistanceFromCenter),
                    OriginMap(), NeighborhoodLayout.WalkNetwork, () => { });

                var halfBody = truck.BodyLength / 2f;
                Assert.That(halfBody, Is.GreaterThan(0f),
                    "the spawned truck must expose a measurable body length to release with");

                // Right-of-way is first-come, so drive the truck onto the band
                // BEFORE the dog arrives — the dog is the one that must wait.
                for (var step = 0; step < 3000 && !truck.IsGone
                                   && truck.transform.position.z > 4.75f; step++)
                {
                    truck.Tick(0.05f);
                }

                Assert.That(truck.IsGone, Is.False);
                Assert.That(truck.transform.position.z, Is.LessThanOrEqualTo(4.75f),
                    "the truck should have driven onto the crosswalk band and claimed it");

                var view = dogGo.AddComponent<DogView>();
                view.Init(dog, null, () => network);
                dogGo.transform.position = new Vector3(crosswalk.A.X, 0f, crosswalk.A.Z);
                view.BeginWanderHop(crosswalk.B);
                var curb = dogGo.transform.position;

                var steppedOn = false;
                var truckTailAtStepOn = float.NaN;
                var truckPivotAtStepOn = float.NaN;
                for (var step = 0; step < 3000 && !steppedOn && !truck.IsGone; step++)
                {
                    truck.Tick(0.05f);
                    if (truck.IsGone)
                    {
                        break;
                    }

                    view.TickWander(0.05f);

                    var moved = new Vector2(
                        dogGo.transform.position.x - curb.x, dogGo.transform.position.z - curb.z);
                    if (moved.magnitude > 0.01f)
                    {
                        steppedOn = true;
                        truckPivotAtStepOn = truck.transform.position.z;
                        truckTailAtStepOn = truckPivotAtStepOn + halfBody;
                    }
                }

                Assert.That(steppedOn, Is.True,
                    "the dog must eventually be released onto the crosswalk (no deadlock)");
                Assert.That(truckTailAtStepOn, Is.LessThanOrEqualTo(farEdgeZ + 0.05f),
                    "the dog stepped onto the band while the truck's TRAILING EDGE was still over it");
                Assert.That(truckPivotAtStepOn, Is.LessThan(farEdgeZ),
                    "the truck's CENTRE was already past the far edge — releasing there is the bug");
            }
            finally
            {
                Object.DestroyImmediate(dogGo);
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void AVehicleAndADog_NeverShareAPointOnACrosswalk_TheInvariant()
        {
            var network = NeighborhoodLayout.WalkNetwork;
            var crosswalk = NorthCrosswalk();

            var dog = new Dog("Scout", Breed.Beagle, Personality.Brave, 1, false);
            dog.PlaceOnStreet();
            var dogGo = new GameObject("dog");
            var root = new GameObject("truck-root");
            try
            {
                var view = dogGo.AddComponent<DogView>();
                view.Init(dog, null, () => network);
                dogGo.transform.position = new Vector3(crosswalk.A.X, 0f, crosswalk.A.Z);
                view.BeginWanderHop(crosswalk.B);

                var truck = DeliveryTruckView.Spawn(root.transform);
                truck.DeliverTo(new Vector3(
                    NeighborhoodLayout.LotDistanceFromCenter, 0f, NeighborhoodLayout.LotDistanceFromCenter),
                    OriginMap(), NeighborhoodLayout.WalkNetwork, () => { });

                var dogReachedFarCurb = false;
                for (var step = 0; step < 6000 && !truck.IsGone; step++)
                {
                    truck.Tick(0.05f);
                    view.TickWander(0.05f);

                    var dogPos = dogGo.transform.position;

                    // The truck destroys its GameObject the instant it departs
                    // (IsGone); once gone there is no vehicle left to share a point
                    // with, so only assert the separation invariant while the truck
                    // still exists — reading its transform afterwards would hit a
                    // destroyed object.
                    if (!truck.IsGone)
                    {
                        var truckPos = truck.transform.position;
                        var flat = new Vector2(truckPos.x - dogPos.x, truckPos.z - dogPos.z);
                        Assert.That(flat.magnitude, Is.GreaterThan(1.0f),
                            $"vehicle and dog occupied the same crosswalk span at step {step}");
                    }

                    if (ReachedFarCurb(dogPos, crosswalk.B))
                    {
                        dogReachedFarCurb = true;
                    }
                }

                Assert.That(truck.IsGone, Is.True,
                    "the truck must actually finish its route (no deadlock)");

                // The truck may depart before the dog has finished crossing; keep
                // ticking the dog until it reaches the far curb, proving it was
                // never permanently blocked (no deadlock). Bounded, so a genuine
                // stall still fails.
                for (var step = 0; step < 2000 && !dogReachedFarCurb; step++)
                {
                    view.TickWander(0.05f);
                    if (ReachedFarCurb(dogGo.transform.position, crosswalk.B))
                    {
                        dogReachedFarCurb = true;
                    }
                }

                Assert.That(dogReachedFarCurb, Is.True,
                    "the dog must actually complete its crossing (no deadlock)");
            }
            finally
            {
                Object.DestroyImmediate(dogGo);
                Object.DestroyImmediate(root);
            }
        }
    }
}
