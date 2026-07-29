using System;
using System.Collections.Generic;
using Doggiehood.Core.Decorations;
using Doggiehood.Core.Dogs;
using Doggiehood.Core.World;
using NUnit.Framework;

namespace Doggiehood.Core.Tests.Decorations
{
    /// <summary>
    /// #112: approach-to-rest is a real walk-over now, not an instant flip.
    /// On a successful RestChancePerTick roll a dog computes a path over the
    /// #106/#128 walk network to its comfort decoration, advances along it
    /// over time, and only settles into the Rest pose once it arrives.
    /// </summary>
    public class RestApproachTests
    {
        private static WalkNetwork Network()
        {
            return WalkNetwork.BuildFrom(NeighborhoodLayout.Roads, NeighborhoodLayout.HouseLots);
        }

        /// <summary>A Random whose NextDouble returns a fixed queue of values,
        /// so the RestChancePerTick gate can be driven deterministically.</summary>
        private sealed class ScriptedRandom : Random
        {
            private readonly Queue<double> values;

            public ScriptedRandom(params double[] doubles)
            {
                values = new Queue<double>(doubles);
            }

            public override double NextDouble()
            {
                return values.Dequeue();
            }
        }

        [Test]
        public void TryBeginApproach_OnRollSuccess_ComputesNonEmptyRoute_AndDoesNotRestYet()
        {
            // Checklist 1: a path to the decoration is computed BEFORE the dog
            // enters Rest — no teleport into the pose.
            var state = GameState.CreateNew();
            var dog = state.Dogs[0];
            var yard = YardPlacement.PositionFor(dog.HouseId, 0);
            state.AddDecoration(new Decoration("bed", dog.HouseId, yard));

            var dogPosition = NeighborhoodLayout.Intersection;
            var rng = new ScriptedRandom(0.0); // < RestChancePerTick -> approach starts

            var approach = RestBehavior.TryBeginApproach(dog, state, dogPosition, Network(), rng);

            Assert.That(approach, Is.Not.Null, "roll succeeded, an approach should begin");
            Assert.That(approach.Waypoints.Count, Is.GreaterThan(0), "route to the decoration must be non-empty");
            Assert.That(approach.HasArrived, Is.False, "should not have arrived at the start");
            Assert.That(dog.State, Is.EqualTo(DogState.IdleWander), "must not enter Rest before arriving");
        }

        [Test]
        public void Advance_MovesPositionAlongRouteOverTime_ArrivingOnlyAtTheEndpoint()
        {
            // Checklist 2: the dog's position advances along the route across
            // ticks (not instant) and only reaches HasArrived at the endpoint.
            var state = GameState.CreateNew();
            var dog = state.Dogs[0];
            var yard = YardPlacement.PositionFor(dog.HouseId, 0);
            var decoration = new Decoration("bed", dog.HouseId, yard);
            state.AddDecoration(decoration);

            var dogPosition = NeighborhoodLayout.Intersection;
            var approach = RestApproach.Begin(dogPosition, decoration, Network());
            var endpoint = approach.Waypoints[approach.Waypoints.Count - 1];

            Assert.That(approach.Position, Is.EqualTo(dogPosition), "starts at the dog's position");

            var startDistance = Distance(approach.Position, endpoint);

            approach.Advance(RestApproach.ApproachSpeed); // one second of walking
            Assert.That(approach.HasArrived, Is.False, "still walking after one step");
            Assert.That(Distance(approach.Position, endpoint), Is.LessThan(startDistance),
                "position moved closer to the decoration");
            Assert.That(approach.Position, Is.Not.EqualTo(dogPosition), "position changed — not a teleport");

            // Walk to completion.
            for (var i = 0; i < 1000 && !approach.HasArrived; i++)
            {
                approach.Advance(RestApproach.ApproachSpeed);
            }

            Assert.That(approach.HasArrived, Is.True, "eventually arrives");
            Assert.That(Distance(approach.Position, endpoint), Is.LessThan(RestApproach.ArriveDistance),
                "final position is on the decoration's yard spot");
        }

        [Test]
        public void Begin_DogAlreadyAtDecoration_RestsImmediately_EmptyRoute()
        {
            // Checklist 2: a dog already on the decoration rests immediately.
            var state = GameState.CreateNew();
            var dog = state.Dogs[0];
            var yard = YardPlacement.PositionFor(dog.HouseId, 0);
            var decoration = new Decoration("bed", dog.HouseId, yard);

            var approach = RestApproach.Begin(yard, decoration, Network());

            Assert.That(approach.Waypoints, Is.Empty, "no walking needed when already there");
            Assert.That(approach.HasArrived, Is.True, "immediately arrived");
        }

        [Test]
        public void TryBeginApproach_ProbabilisticTrigger_GatesWhenApproachStarts()
        {
            // Checklist 3: the RestChancePerTick roll still gates whether an
            // approach starts at all.
            var state = GameState.CreateNew();
            var dog = state.Dogs[0];
            var yard = YardPlacement.PositionFor(dog.HouseId, 0);
            state.AddDecoration(new Decoration("bed", dog.HouseId, yard));
            var dogPosition = NeighborhoodLayout.Intersection;

            var failed = RestBehavior.TryBeginApproach(
                dog, state, dogPosition, Network(), new ScriptedRandom(0.99)); // >= chance
            Assert.That(failed, Is.Null, "a failed roll starts no approach");
            Assert.That(dog.State, Is.EqualTo(DogState.IdleWander));

            var started = RestBehavior.TryBeginApproach(
                dog, state, dogPosition, Network(), new ScriptedRandom(0.0)); // < chance
            Assert.That(started, Is.Not.Null, "a successful roll starts an approach");
        }

        [Test]
        public void TryBeginApproach_IsDeterministicForAFixedSeed()
        {
            // Checklist 3: deterministic for a fixed seed.
            var state = GameState.CreateNew();
            var dog = state.Dogs[0];
            var yard = YardPlacement.PositionFor(dog.HouseId, 0);
            state.AddDecoration(new Decoration("bed", dog.HouseId, yard));
            var dogPosition = NeighborhoodLayout.Intersection;

            var first = RestBehavior.TryBeginApproach(dog, state, dogPosition, Network(), new Random(4242));
            var second = RestBehavior.TryBeginApproach(dog, state, dogPosition, Network(), new Random(4242));

            Assert.That(first == null, Is.EqualTo(second == null),
                "same seed -> same begin-or-not decision");
            if (first != null && second != null)
            {
                Assert.That(first.Waypoints, Is.EqualTo(second.Waypoints), "same seed -> same route");
            }
        }

        [Test]
        public void TryBeginApproach_NoComfortDecoration_ReturnsNull()
        {
            var state = GameState.CreateNew();
            var dog = state.Dogs[0];

            var approach = RestBehavior.TryBeginApproach(
                dog, state, NeighborhoodLayout.Intersection, Network(), new ScriptedRandom(0.0));

            Assert.That(approach, Is.Null, "no comfort decoration -> no approach");
        }

        private static float Distance(GridPoint a, GridPoint b)
        {
            var dx = a.X - b.X;
            var dz = a.Z - b.Z;
            return (float)Math.Sqrt(dx * dx + dz * dz);
        }
    }
}
