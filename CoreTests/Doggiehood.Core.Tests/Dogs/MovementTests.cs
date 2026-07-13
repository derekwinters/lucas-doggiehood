using System.Collections.Generic;
using System.Linq;
using Doggiehood.Core.Dogs;
using Doggiehood.Core.World;
using NUnit.Framework;

namespace Doggiehood.Core.Tests.Dogs
{
    public class MovementTests
    {
        [Test]
        public void ExcitedDogs_MoveFasterThanBase()
        {
            // #89: Excited = fast.
            Assert.That(MovementProfile.ForPersonality(Personality.Excited).Speed,
                Is.GreaterThan(MovementProfile.Base.Speed));
        }

        [Test]
        public void ExcitedDogs_TurnLessOften()
        {
            // #89: Excited = long straight stretches.
            Assert.That(MovementProfile.ForPersonality(Personality.Excited).TurnProbability,
                Is.LessThan(MovementProfile.Base.TurnProbability));
        }

        [Test]
        public void ExcitedProfile_ProducesMeasurablyFewerTurns_ThroughTheDefaultNextTargetOverload()
        {
            // #89/#106: MovementProfile.TurnProbability must actually shape
            // wander's path through the network-based WanderBehavior, via
            // the parameterless NextTarget(current) overload every caller
            // (DogView) uses today — not just sit unread on the struct.
            // Excited's low TurnProbability should produce measurably
            // longer straight stretches (fewer direction changes) than
            // Base's higher one, over the same seeded roll sequence.
            Assert.That(CountTurns(MovementProfile.ForPersonality(Personality.Excited), seed: 99),
                Is.LessThan(CountTurns(MovementProfile.Base, seed: 99)));
        }

        private static int CountTurns(MovementProfile profile, int seed)
        {
            var wander = new WanderBehavior(seed, profile, NeighborhoodLayout.WalkNetwork);
            var position = NeighborhoodLayout.Intersection;
            var turns = 0;
            var lastDirection = default(GridPoint);
            var first = true;

            for (var step = 0; step < 400; step++)
            {
                var next = wander.NextTarget(position);
                var direction = new GridPoint(
                    System.Math.Sign(next.X - position.X),
                    System.Math.Sign(next.Z - position.Z));

                if (!first && !direction.Equals(lastDirection))
                {
                    turns++;
                }

                first = false;
                lastDirection = direction;
                position = next;
            }

            return turns;
        }

        [Test]
        public void GrumpyDogs_UseBaseParametersForMvp()
        {
            // #89: Grumpy's distinct shuffle is explicitly deferred out of
            // MVP — parity with base is intentional, not an oversight.
            var grumpy = MovementProfile.ForPersonality(Personality.Grumpy);

            Assert.That(grumpy.Speed, Is.EqualTo(MovementProfile.Base.Speed));
            Assert.That(grumpy.TurnProbability, Is.EqualTo(MovementProfile.Base.TurnProbability));
        }

        [Test]
        public void ParametersComeFromThePersonality_ForEveryPersonality()
        {
            // #89: lookup by Personality never throws for any defined value.
            foreach (Personality personality in System.Enum.GetValues(typeof(Personality)))
            {
                Assert.That(MovementProfile.ForPersonality(personality).Speed, Is.GreaterThan(0f));
            }
        }

        [Test]
        public void Wander_PositionsAlwaysLieOnTheWalkNetwork_NeverOnARoadOrDriveway()
        {
            // #106: the rewritten WanderBehavior is a node-to-node random
            // walk over the sidewalk+crosswalk graph — positions are
            // always real network nodes, and driveway stubs (which lead
            // only to house lots) are never entered.
            var network = NeighborhoodLayout.WalkNetwork;
            var wander = new WanderBehavior(seed: 1234, MovementProfile.Base, network);
            var position = NeighborhoodLayout.Intersection;
            var nodes = new HashSet<GridPoint>(network.Nodes);
            var houseLotPositions = new HashSet<GridPoint>(NeighborhoodLayout.HouseLots.Select(l => l.Position));

            for (var step = 0; step < 500; step++)
            {
                position = wander.NextTarget(position);

                Assert.That(nodes.Contains(position), Is.True, $"step {step}: {position} is not a network node");
                Assert.That(houseLotPositions.Contains(position), Is.False,
                    $"step {step}: {position} is a house lot / driveway endpoint");
            }
        }

        [Test]
        public void Wander_IsDeterministicForASeed()
        {
            var network = NeighborhoodLayout.WalkNetwork;
            var a = new WanderBehavior(seed: 7, MovementProfile.Base, network);
            var b = new WanderBehavior(seed: 7, MovementProfile.Base, network);
            var positionA = NeighborhoodLayout.Intersection;
            var positionB = NeighborhoodLayout.Intersection;

            for (var step = 0; step < 50; step++)
            {
                positionA = a.NextTarget(positionA);
                positionB = b.NextTarget(positionB);
                Assert.That(positionA, Is.EqualTo(positionB));
            }
        }

        // The following tests drive a fully controlled two-hop scenario on
        // the real starting network: a WanderBehavior placed at the far,
        // driveway-free tip of the EW road's north sidewalk east arm
        // (26, 4) always takes its only exit — the NE box corner
        // (4, 4) — on the first hop (nothing to weigh, only one
        // candidate). Arriving there heading west (-1, 0), the corner has
        // exactly one "continue straight" edge (the north crosswalk, to
        // (-4, 4) — crossing the NS road continues the EW road's
        // line) and three "deviate" edges (back the way it came, up the
        // NS road's sidewalk, and across the east crosswalk). That makes
        // the second hop a clean, known-shape test of the weighted
        // continue-vs-deviate mechanism.
        private static GridPoint Offset(float x, float z)
        {
            var half = WorldDimensions.RoadWidth / 2f + WorldDimensions.GrassVergeWidth + WorldDimensions.SidewalkWidth / 2f;
            return new GridPoint(x * half, z * half);
        }

        private static GridPoint ArmTip()
        {
            return new GridPoint(NeighborhoodLayout.StreetHalfLength, Offset(0f, 1f).Z);
        }

        private static readonly GridPoint Corner = Offset(1f, 1f); // NE box corner: (4, 4)
        private static readonly GridPoint ContinueTarget = Offset(-1f, 1f); // north crosswalk destination: (-4, 4)

        [Test]
        public void NextTarget_ContinueWeightOnly_AlwaysTakesTheStraightEdge()
        {
            var network = NeighborhoodLayout.WalkNetwork;

            for (var seed = 0; seed < 25; seed++)
            {
                var wander = new WanderBehavior(seed, MovementProfile.Base, network);
                var afterFirstHop = wander.NextTarget(ArmTip());
                Assert.That(afterFirstHop, Is.EqualTo(Corner), "first hop has only one candidate");

                var afterSecondHop = wander.NextTarget(afterFirstHop, continueWeight: 1f, deviateWeight: 0f);
                Assert.That(afterSecondHop, Is.EqualTo(ContinueTarget));
            }
        }

        [Test]
        public void NextTarget_DeviateWeightOnly_NeverTakesTheStraightEdge()
        {
            var network = NeighborhoodLayout.WalkNetwork;

            for (var seed = 0; seed < 25; seed++)
            {
                var wander = new WanderBehavior(seed, MovementProfile.Base, network);
                var afterFirstHop = wander.NextTarget(ArmTip());

                var afterSecondHop = wander.NextTarget(afterFirstHop, continueWeight: 0f, deviateWeight: 1f);
                Assert.That(afterSecondHop, Is.Not.EqualTo(ContinueTarget));
            }
        }

        [Test]
        public void NextTarget_NoWeightsPassed_DerivesTheSplitFromMovementProfileTurnProbability()
        {
            // #89/#106: callers today (DogView) pass no explicit weights;
            // that must land on the dog's own MovementProfile.TurnProbability
            // rather than a flat/uniform split — Base's continue-fraction
            // should track (1 - TurnProbability), i.e. ~0.65 for Base.
            var network = NeighborhoodLayout.WalkNetwork;
            const int trials = 300;
            var continueCount = 0;

            for (var seed = 0; seed < trials; seed++)
            {
                var wander = new WanderBehavior(seed, MovementProfile.Base, network);
                var afterFirstHop = wander.NextTarget(ArmTip());
                var afterSecondHop = wander.NextTarget(afterFirstHop);

                if (afterSecondHop.Equals(ContinueTarget))
                {
                    continueCount++;
                }
            }

            var fraction = continueCount / (float)trials;
            var expected = 1f - MovementProfile.Base.TurnProbability;
            Assert.That(fraction, Is.InRange(expected - 0.15f, expected + 0.15f),
                $"expected roughly a {expected:P0} continue split (1 - Base.TurnProbability), got {continueCount}/{trials}");
        }

        [Test]
        public void TapResolution_HitsAWanderingDogAtItsCurrentPosition()
        {
            // #8: the interaction system can resolve a tap against wherever
            // the dog currently is along its path.
            var wander = new WanderBehavior(seed: 5, MovementProfile.Base, NeighborhoodLayout.WalkNetwork);
            var dog = new Dog("Tappy", Breed.Beagle, Personality.Brave, 1, false);
            var position = NeighborhoodLayout.Intersection;

            for (var step = 0; step < 20; step++)
            {
                position = wander.NextTarget(position);

                Assert.That(TapResolver.IsHit(position, tap: position, radius: 1.5f), Is.True);
                Assert.That(TapResolver.IsHit(position, new GridPoint(position.X + 5f, position.Z), 1.5f), Is.False);
            }
        }
    }
}
