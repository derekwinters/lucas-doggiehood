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
                var reachedNearEdge = false;
                for (var step = 0; step < 3000 && !truck.IsGone; step++)
                {
                    truck.Tick(0.05f);
                    var z = truck.transform.position.z;
                    Assert.That(z, Is.GreaterThan(nearEdgeZ - 0.05f),
                        "the truck must never drive into a crosswalk band a dog holds");
                    if (Mathf.Abs(z - nearEdgeZ) < 0.1f)
                    {
                        reachedNearEdge = true;
                    }
                }

                Assert.That(reachedNearEdge, Is.True,
                    "the truck should have driven up to the crosswalk's near edge and paused there");
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
